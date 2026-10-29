"""blender -b -P make_zeropoint.py -- <out.glb>
The Zero Point as in the game (reference screenshot): a smooth translucent violet sphere, a bright white-blue core shining through it, thin white-blue lightning cracks across the
surface (two layers that crawl against each other), a soft blue-violet glow and a star-burst of rays around it. Facing +Z (Blender -Y) glow / burst planes: the plugin turns the model to the viewer.
Shell radius 1, glow to 2.6, burst to 3.6. Node animation 'Animation' (24 s loop): the two crack layers turn in opposite directions, the core breathes, the burst turns and pulses.
Materials named A_* are alphaMode BLEND. Draw order is set by the plugin from the node names (Core opaque, Glow, Shell, Shell2, Burst)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_tex import *
out = sys.argv[sys.argv.index('--') + 1]; setup()

def tex_core():
    W, H = 512, 256; n = fbm(W, H, 3, 1); v = 0.8 + 0.2 * n; return rgba(1.0, 1.0, 1.0, 1.0) * np.array([1, 1, 1, 1], np.float32) if False else rgba(0.9 + 0.1 * v, 0.97, 1.0, 1.0)
def tex_shell():
    W, H = 1024, 512; U, V = grid(W, H); e = voronoi_edges(W, H, 26, 11, warp=0.07); crack = 1 - smooth(0.0, 0.010, e); glow = 1 - smooth(0.0, 0.07, e)
    n = fbm(W, H, 3, 20)
    base = np.stack([0.30 + 0.12 * n, 0.12 + 0.08 * n, 0.62 + 0.15 * n], -1)                  # violet glass
    a = 0.50 + 0.12 * n + glow * 0.25
    col = base + (glow[..., None] * np.array([0.10, 0.25, 0.30])) + (crack[..., None] * np.array([0.7, 0.75, 0.4]))
    return rgba(col[..., 0], col[..., 1], col[..., 2], np.clip(a + crack * 0.6, 0, 1))
def tex_shell2():
    W, H = 1024, 512; e = voronoi_edges(W, H, 16, 77, warp=0.09); crack = 1 - smooth(0.0, 0.008, e); glow = 1 - smooth(0.0, 0.05, e)
    return rgba(0.75 + 0.25 * crack, 0.85 + 0.15 * crack, 1.0, np.clip(crack * 0.95 + glow * 0.18, 0, 1))
def tex_glow():
    W, H = 256, 128; U, V = grid(W, H); a = np.clip(np.exp(-(V * 2.1) ** 2) * 1.05, 0, 1) * (1 - smooth(0.55, 1.0, V)); return rgba(0.55 + 0.45 * (1 - V) ** 2, 0.5 + 0.5 * (1 - V) ** 2, 1.0, a)
def tex_burst():
    W, H = 1024, 128; U, V = grid(W, H); r = np.random.default_rng(4); acc = np.zeros((H, W), np.float32)
    for k in range(34):
        c = r.random(); w = 0.0025 + r.random() * 0.01; ln = 0.35 + r.random() * 0.65; d = np.minimum(abs(U - c), 1 - abs(U - c)); acc += np.exp(-(d / w) ** 2) * (1 - smooth(0.0, ln, V)) ** 1.5 * (0.4 + 0.6 * r.random())
    a = np.clip(acc, 0, 1) * (1 - smooth(0.9, 1.0, V)) * 0.9; return rgba(0.7, 0.75, 1.0, a)

M_core = material('Core', tex_core()); M_shell = material('A_Shell', tex_shell()); M_shell2 = material('A_Shell2', tex_shell2()); M_glow = material('A_Glow', tex_glow()); M_burst = material('A_Burst', tex_burst())
root = empty('ZeroPoint')
core = empty('CorePulse', root); new_obj('Core', sphere(0.62), M_core, core)
s1 = empty('ShellSpin', root); new_obj('Shell', sphere(1.0, 64, 32), M_shell, s1)
s2 = empty('Shell2Spin', root); new_obj('Shell2', sphere(1.025, 64, 32), M_shell2, s2)
new_obj('Glow', annulus(0.001, 2.6, 64, xz=True), M_glow, root)
bs = empty('BurstSpin', root); new_obj('Burst', annulus(0.001, 3.6, 96, xz=True), M_burst, bs)
keys(s1, 'rotation_euler', lambda t: (0.25, 0, 2 * math.pi * t / 24 * 1))
keys(s2, 'rotation_euler', lambda t: (-0.3, 0.2, -2 * math.pi * t / 24 * 2))
keys(core, 'scale', lambda t: (1 + 0.06 * math.sin(2 * math.pi * t / 3.0),) * 3)
keys(bs, 'rotation_euler', lambda t: (0, 2 * math.pi * t / 24 * 1, 0))
keys(bs, 'scale', lambda t: (1 + 0.07 * math.sin(2 * math.pi * t / 4.0),) * 3)
export(out)
