"""blender -b -P make_end21.py -- <fort|monster> <out.glb>
fort:    "Guava Fort" - a wooden fortress the loopers defend (origin = the middle of its floor, front = glTF +Z): 14 x 14 m deck, sandbag walls, a 7 m watchtower with a flag, crates, barrels, barricades and gun nests.
monster: a Sideways cube monster (origin = feet, faces glTF +Z, 3.4 m tall): purple cube body with glowing cyan seams, clip 'Animation' = a heavy 1.2 s walk cycle with arm swing and body sway."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 2)
def wood(base, seed, W=128, H=128, lines=8):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); g = 0.78 + 0.3 * n + 0.12 * np.sin(U * 2 * np.pi * lines + n * 4); plank = np.abs(((V * 6) % 1) - 0.5) > 0.47
    c = np.array(base)[None, None, :] * g[..., None]; c = np.where(plank[..., None], c * 0.55, c); return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
def cloth(base, seed, W=64, H=64):
    n = fbm(W, H, 5, seed); c = np.array(base)[None, None, :] * (0.8 + 0.35 * n[..., None]); return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
if kind == 'fort':
    WD = material('Wood', wood((0.62, 0.42, 0.22), 3)); WD2 = material('WoodDark', wood((0.38, 0.25, 0.14), 4)); SB = material('Sandbag', cloth((0.78, 0.66, 0.44), 5)); CR = material('Crate', wood((0.55, 0.4, 0.2), 6, lines=3)); ME = solid('Metal', 0.28, 0.3, 0.34)
    FL = material('Flag', cloth((0.9, 0.35, 0.25), 8)); GN = solid('Green', 0.3, 0.55, 0.25); B = MB([WD, WD2, SB, CR, ME, FL, GN]); W_, WD2_, SB_, CR_, ME_, FL_, GN_ = range(7)
    b = B; b.box((0, 0, 0.45), (14, 14, 0.9), WD2_, 2.0, top=W_)                                          # deck on stilts
    for x in (-6.5, 0, 6.5):
        for y in (-6.5, 0, 6.5): b.box((x, y, -0.8), (0.5, 0.5, 1.8), WD2_)
    for side in range(4):                                                                                 # sandbag walls with a gap at the front
        for k in range(14):
            if side == 0 and 5 <= k <= 8: continue
            x = -6.6 + k * 1.0 + 0.5 * (k % 2); h = 1.0 + 0.0
            if side == 0: b.box((x, -6.6, 1.0), (1.0, 0.8, 0.8), SB_); b.box((x + 0.5, -6.6, 1.7), (1.0, 0.8, 0.6), SB_)
            if side == 1: b.box((x, 6.6, 1.0), (1.0, 0.8, 0.8), SB_); b.box((x + 0.5, 6.6, 1.7), (1.0, 0.8, 0.6), SB_)
            if side == 2: b.box((-6.6, x, 1.0), (0.8, 1.0, 0.8), SB_); b.box((-6.6, x + 0.5, 1.7), (0.8, 1.0, 0.6), SB_)
            if side == 3: b.box((6.6, x, 1.0), (0.8, 1.0, 0.8), SB_); b.box((6.6, x + 0.5, 1.7), (0.8, 1.0, 0.6), SB_)
    for x, y in ((-5.5, 5.5), (5.5, 5.5), (-5.5, -5.5), (5.5, -5.5)): b.box((x, y, 4.2), (0.45, 0.45, 6.6), WD2_)                      # tower posts
    b.box((0, 5.5, 7.4), (12, 0.45, 0.3), WD2_); b.box((0, -5.5, 7.4), (12, 0.45, 0.3), WD2_); b.box((-5.5, 0, 7.4), (0.45, 11, 0.3), WD2_); b.box((5.5, 0, 7.4), (0.45, 11, 0.3), WD2_)
    b.box((0, 0, 7.7), (12.6, 12.6, 0.3), WD_ if False else W_, 2.0, top=ME_); b.cyl((0, 5.4, 3.4), 0.1, 0.2, ME_, 6)
    b.box((0, 0, 4.0), (11, 11, 0.25), W_, 2.0, top=W_)                                                      # upper deck
    for k in range(10): b.box((0, -2.2 + k * 0.3, 1.2 + k * 0.3), (2.4, 0.3, 0.2), WD2_)                      # ramp to the upper deck
    b.box((6.0, -6.0, 9.5), (0.12, 0.12, 3.6), ME_); b.quad((6.0, -6.0, 11.2), (9.0, -6.0, 11.2), (9.0, -6.0, 9.6), (6.0, -6.0, 9.6), FL_, [(0, 1), (1, 1), (1, 0), (0, 0)])
    for x, y in ((-4, 4), (4, 4), (3.5, -3), (-3.5, 2)): b.box((x, y, 1.5), (1.4, 1.4, 1.2), CR_); b.box((x, y, 2.2), (0.9, 0.9, 0.5), CR_)
    for x, y in ((-5.4, -4.4), (5.2, 4.8)): b.cyl((x, y, 1.5), 0.45, 1.2, GN_, 10)
    for x in (-3.4, 3.4): b.box((x, -6.1, 1.9), (0.3, 1.6, 0.3), ME_); b.cyl((x, -7.1, 1.9), 0.12, 1.6, ME_, 8, axis='y')                  # gun nests
    for x in (-11, 11):                                                                                       # tank traps beside the fort
        for k in range(3): b.box((x, -6 + k * 3, 0.9), (0.4, 3.4, 0.4), ME_); b.box((x, -6 + k * 3, 0.9), (3.4, 0.4, 0.4), ME_)
    o = b.build('Fort'); keys(o, 'rotation_euler', lambda t: (0, 0, 0), 24, 2, 2); export(out)
elif kind == 'monster':
    PU = material('Purple', cloth((0.38, 0.14, 0.62), 11, 128, 128)); SEAM = solid('Seam', 0.3, 0.95, 1.0); DK = solid('Dark', 0.12, 0.05, 0.2); EYE = solid('Eye', 1.0, 0.35, 0.2)
    def part(name, parent, loc, size, mat=0, glow=False):
        b = MB([PU, SEAM, DK, EYE]); b.box((0, 0, 0), size, mat); b.box((0, 0, 0), (size[0] * 1.02, size[1] * 1.02, size[2] * 0.12), 1) if glow else None
        o = b.build(name); o.parent = parent; o.location = loc; return o
    root = empty('MonsterRoot')
    hip = empty('Hip', root, (0, 0, 1.5)); torso = part('Torso', hip, (0, 0, 0.95), (1.7, 1.2, 1.6), 0, True); head = part('Head', torso, (0, 0.15, 1.35), (1.1, 1.0, 1.0), 0); part('Eyes', head, (0, 0.52, 0.1), (0.8, 0.05, 0.22), 3)
    part('Jaw', head, (0, 0.2, -0.45), (0.9, 0.7, 0.2), 2)
    armL = empty('ArmL', torso, (-1.15, 0, 0.5)); part('UpperArmL', armL, (0, 0, -0.7), (0.55, 0.55, 1.5), 0, True); part('FistL', armL, (0, 0.1, -1.6), (0.8, 0.8, 0.8), 2)
    armR = empty('ArmR', torso, (1.15, 0, 0.5)); part('UpperArmR', armR, (0, 0, -0.7), (0.55, 0.55, 1.5), 0, True); part('FistR', armR, (0, 0.1, -1.6), (0.8, 0.8, 0.8), 2)
    legL = empty('LegL', hip, (-0.5, 0, 0)); part('LegLm', legL, (0, 0, -0.75), (0.65, 0.7, 1.5), 0, True); part('FootL', legL, (0, 0.15, -1.45), (0.8, 1.1, 0.25), 2)
    legR = empty('LegR', hip, (0.5, 0, 0)); part('LegRm', legR, (0, 0, -0.75), (0.65, 0.7, 1.5), 0, True); part('FootR', legR, (0, 0.15, -1.45), (0.8, 1.1, 0.25), 2)
    P = 1.2
    def ph(t, o=0.0): return 2 * math.pi * (t / P + o)
    keys(legL, 'rotation_euler', lambda t: (0.7 * math.sin(ph(t)), 0, 0), 24, 2.4, 1); keys(legR, 'rotation_euler', lambda t: (-0.7 * math.sin(ph(t)), 0, 0), 24, 2.4, 1)
    keys(armL, 'rotation_euler', lambda t: (-0.9 * math.sin(ph(t)) - 0.4, 0, 0.1), 24, 2.4, 1); keys(armR, 'rotation_euler', lambda t: (0.9 * math.sin(ph(t)) - 0.4, 0, -0.1), 24, 2.4, 1)
    keys(hip, 'location', lambda t: (0.07 * math.sin(ph(t)), 0, 1.5 - 0.12 * abs(math.cos(ph(t)))), 24, 2.4, 1); keys(torso, 'rotation_euler', lambda t: (0.08, 0, 0.12 * math.sin(ph(t))), 24, 2.4, 1); keys(head, 'rotation_euler', lambda t: (0, 0, -0.1 * math.sin(ph(t))), 24, 2.4, 1)
    export(out)
