"""blender -b -P make_blackhole.py -- <out.glb>
The black hole as seen in the game (reference screenshot): a small black dot, a soft blue-violet glow around it and long radial streaks of blue / white light with pink and cyan fringes
that swirl slowly. All layers are flat planes facing +Z (Blender -Y; the plugin turns the model to the viewer), so the swirl is a real node rotation, no UV tricks.
Horizon radius 1; the streaks reach 6.  Names carry the draw order (BH_1.. first, BH_2Horizon black over the middle, BH_3Rim last).  Clip 'Animation' (24 s loop).
Materials named A_* are alphaMode BLEND."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_tex import *
out = sys.argv[sys.argv.index('--') + 1]; setup()

def rays(seed, n, wmin, wmax, twist, warm, long_):
    W, H = 2048, 256; U, V = grid(W, H); r = np.random.default_rng(seed); A = np.zeros((H, W), np.float32); fr = np.zeros((H, W), np.float32)
    Ut = U + V * twist
    for k in range(n):
        c = r.random(); w = wmin + r.random() * (wmax - wmin); ln = (0.25 + 0.75 * r.random()) * long_; d = (Ut - c + 0.5) % 1 - 0.5
        prof = np.exp(-(d / w) ** 2) * (1 - smooth(0.0, ln, V)) ** 1.3 * (0.35 + 0.65 * r.random()); A += prof; fr += prof * np.tanh(d / w) * 0.5
    A = np.clip(A, 0, 1) * smooth(0.0, 0.03, V) * (1 - smooth(0.85, 1.0, V))
    R = 0.35 + 0.65 * A + warm * np.clip(fr, 0, 1) * 0.9; G = 0.5 + 0.5 * A + 0.35 * np.clip(-fr, 0, 1); B = np.ones_like(A)       # blue-white core, pink on one side of a ray, cyan on the other
    return rgba(np.clip(R, 0, 1), np.clip(G, 0, 1), B, A * 0.9)
def tex_glow():
    W, H = 256, 128; U, V = grid(W, H); a = np.exp(-(V * 2.2) ** 2) * 0.85 * (1 - smooth(0.7, 1.0, V)); return rgba(0.25 + 0.25 * (1 - V), 0.4 + 0.25 * (1 - V), 1.0, a)
def tex_rim():
    W, H = 256, 64; U, V = grid(W, H); a = np.exp(-(V * 3.5) ** 2) * 0.95; return rgba(0.7, 0.85, 1.0, a)

M_g = material('A_Glow', tex_glow()); M_a = material('A_RaysA', rays(3, 70, 0.002, 0.012, 0.06, 0.8, 1.0)); M_b = material('A_RaysB', rays(8, 46, 0.004, 0.02, -0.05, 0.9, 0.7)); M_r = material('A_Rim', tex_rim()); M_k = solid('Black', 0.0, 0.0, 0.0)
root = empty('BlackHole')
ga = empty('GlowSpin', root); new_obj('BH_1Glow', annulus(0.5, 4.2, 64, xz=True), M_g, ga)
ra = empty('RaysASpin', root); new_obj('BH_1RaysA', annulus(0.9, 6.0, 128, xz=True), M_a, ra)
rb = empty('RaysBSpin', root); new_obj('BH_1RaysB', annulus(0.9, 4.2, 128, xz=True), M_b, rb)
new_obj('BH_2Horizon', sphere(1.0), M_k, root)
rm = empty('RimPulse', root); new_obj('BH_3Rim', annulus(0.97, 1.55, 96, xz=True), M_r, rm)
# the facing plane is XZ (normal Y), so a spin about the view axis is a rotation about Y
keys(ra, 'rotation_euler', lambda t: (0, 2 * math.pi * t / 24 * 1, 0)); keys(rb, 'rotation_euler', lambda t: (0, -2 * math.pi * t / 24 * 2, 0))
keys(ga, 'scale', lambda t: (1 + 0.05 * math.sin(2 * math.pi * t / 3.0),) * 3); keys(rm, 'scale', lambda t: (1 + 0.04 * math.sin(2 * math.pi * t / 2.0 + 1),) * 3)
keys(ra, 'scale', lambda t: (1 + 0.04 * math.sin(2 * math.pi * t / 6.0),) * 3)
export(out)
