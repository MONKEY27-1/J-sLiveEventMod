"""blender -b -P make_collision_props.py -- <cockpit|sword> <out.glb>
cockpit: the mech-head cockpit round the player's head (origin = head, window straight ahead = Blender -Y = glTF +Z): a dashboard with green / blue / red screens and the bear emblem, a wide windscreen frame (open: you see the world through it), the five portrait frames in the roof, side consoles, glowing red trim.
sword: the mech's energy sword (1 m = scale 1; blade along +Z, about 20 m long, hilt at the origin)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 1)
def bars(c1, c2, seed, n=6):
    W, H = 64, 32; r = np.random.default_rng(seed); img = np.zeros((H, W, 4), np.float32); img[..., :3] = 0.04; img[..., 3] = 1
    for k in range(n):
        y0 = 3 + k * (H - 6) // n; L = int(W * (0.3 + 0.65 * r.random())); img[y0:y0 + 3, 3:3 + L, :3] = c1 if k % 2 == 0 else c2
    return img
if kind == 'cockpit':
    METAL = solid('Metal', 0.14, 0.17, 0.26); DARK = solid('Dark', 0.06, 0.07, 0.11); RED = solid('Red', 1.0, 0.25, 0.2); GREEN = material('Green', bars(np.array([0.3, 1, 0.5]), np.array([0.2, 0.8, 1.0]), 1)); BLUE = material('Blue', bars(np.array([0.3, 0.8, 1.0]), np.array([0.5, 0.5, 1.0]), 2))
    PORT = [solid('Portrait%d' % i, *c) for i, c in enumerate(((0.9, 0.55, 0.2), (0.6, 0.65, 0.85), (0.85, 0.35, 0.7), (0.95, 0.8, 0.45), (0.7, 0.5, 0.35)))]
    b = MB([METAL, DARK, RED, GREEN, BLUE] + PORT); Y = -2.6
    b.box((0, Y + 0.7, -1.2), (6.4, 1.6, 0.9), 0, top=1)                                   # dashboard block under the window
    b.box((0, Y + 0.9, -0.72), (5.6, 1.1, 0.12), 1)                                        # sloped-looking top slab
    for k, x in enumerate((-1.8, -0.6, 0.6, 1.8)): b.quad((x - 0.45, Y + 0.55, -0.66), (x + 0.45, Y + 0.55, -0.66), (x + 0.45, Y + 0.28, -0.7), (x - 0.45, Y + 0.28, -0.7), 3 if k % 2 == 0 else 4, [(0, 0), (1, 0), (1, 1), (0, 1)])
    b.cyl((0, Y + 0.45, -0.62), 0.3, 0.06, 2, 6, axis='z')                                  # the bear emblem plate
    b.box((0, Y, 1.75), (6.2, 0.3, 0.5), 0)                                               # top beam
    for s_ in (-1, 1):
        b.box((s_ * 3.1, Y, 0.35), (0.35, 0.4, 3.4), 0); b.box((s_ * 3.3, Y + 1.8, -0.4), (0.4, 3.6, 2.6), 1)                 # pillars and side consoles
        b.quad((s_ * 3.12, Y + 0.3, 0.4), (s_ * 3.12, Y + 0.3, 1.6), (s_ * 3.12, Y + 0.3, 1.6), (s_ * 3.12, Y + 0.3, 0.4), 2, [(0, 0), (1, 0), (1, 1), (0, 1)])
    for k, x in enumerate((-2.55, -1.2, 0.0, 1.2, 2.55)): b.quad((x - 0.5, Y + 0.18, 1.2 if k in (0, 4) else 1.55), (x + 0.5, Y + 0.18, 1.2 if k in (0, 4) else 1.55), (x + 0.5, Y + 0.18, (0.35 if k in (0, 4) else 0.7) + 0.0), (x - 0.5, Y + 0.18, 0.35 if k in (0, 4) else 0.7), 5 + k, [(0, 0), (1, 0), (1, 1), (0, 1)])
    b.box((0, Y + 0.1, 1.5), (6.0, 0.1, 0.08), 2)                                           # red trim line under the top beam
    b.box((0, Y + 0.1, -0.6), (6.0, 0.1, 0.06), 2)
    b.box((0, 0.0, -1.75), (6.0, 6.0, 0.1), 1)                                              # floor plate
    o = b.build('Cockpit'); export(out)
else:
    BL = material('A_Blade', np.concatenate([np.linspace(np.array([[1.0, 0.45, 0.1, 0.9]]), np.array([[1.0, 0.95, 0.8, 0.95]]), 16)[:, None, :].repeat(4, 1)], 0).astype(np.float32).reshape(16, 4, 4)); HILT = solid('Hilt', 0.2, 0.22, 0.3)
    b = MB([HILT, BL]); b.cyl((0, 0, 1.5), 0.2, 3.0, 0, 10); b.box((0, 0, 3.1), (1.6, 0.4, 0.3), 0); b.box((0, 0, 13.3), (1.8, 0.55, 20.4), 1); b.cyl((0, 0, 23.6), 0.04, 0.1, 1, 4)
    b.build('Sword'); export(out)
