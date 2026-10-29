"""blender -b -P make_vault.py -- <out.glb>
The Vault the Zero Point floats over (reference screenshot): a big round metal hatch with concentric rings, a ring of curved armour plates folding down into a funnel, a dark shaft in the
middle with violet light at the bottom. Flat (unlit) textures with shading painted in. Outer radius 3 (the plugin scales it), flat on y = 0 (Blender z = 0), funnel down to z = -1.
Node animation: the light at the bottom breathes. Materials named A_* are alphaMode BLEND."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_tex import *
out = sys.argv[sys.argv.index('--') + 1]; setup()

def tex_metal(seed, plates, tint):
    W, H = 1024, 256; U, V = grid(W, H); n = fbm(W, H, 5, seed); pl = (U * plates) % 1.0
    seam = np.exp(-(np.minimum(pl, 1 - pl) / 0.018) ** 2); ridge = np.exp(-((V - 0.5) / 0.06) ** 2) * 0.25
    shade = 0.62 + 0.3 * n + 0.18 * np.sin(2 * np.pi * pl * 1.0 + 1.0) * (1 - V) - 0.45 * seam + ridge
    return rgba(tint[0] * shade, tint[1] * shade, tint[2] * shade, 1.0)
def tex_ring(seed):
    W, H = 1024, 64; U, V = grid(W, H); n = fbm(W, H, 4, seed); line = np.exp(-((V - 0.5) / 0.08) ** 2); lights = (np.sin(U * 2 * np.pi * 48) > 0.9) * np.exp(-((V - 0.5) / 0.2) ** 2)
    shade = 0.55 + 0.25 * n + 0.2 * line; return rgba(0.36 * shade + lights * 0.4, 0.45 * shade + lights * 0.6, 0.62 * shade + lights * 1.0, 1.0)
def tex_shaft():
    W, H = 256, 128; U, V = grid(W, H); a = 0.35 + 0.65 * V; return rgba(0.04 + 0.5 * V ** 2, 0.04 + 0.3 * V ** 2, 0.12 + 0.95 * V ** 2, 1.0)
def tex_glow():
    W, H = 256, 128; U, V = grid(W, H); a = np.exp(-(V * 1.6) ** 2) * 0.9; return rgba(0.5, 0.35, 1.0, a)

M_o = material('Outer', tex_metal(2, 36, (0.42, 0.52, 0.72))); M_p = material('Plates', tex_metal(5, 12, (0.34, 0.45, 0.68))); M_r = material('Ring', tex_ring(7)); M_s = material('Shaft', tex_shaft()); M_g = material('A_ShaftGlow', tex_glow())
root = empty('Vault')
new_obj('OuterDeck', annulus(2.35, 3.0, 128, z=0.0), M_o, root)
new_obj('OuterRim', annulus(2.2, 2.35, 128, z=0.08), M_r, root)
new_obj('InnerRing', annulus(1.75, 2.2, 128, z=-0.12), M_r, root)
# armour plates folding into the funnel: a cone surface from r = 1.75 (z = -0.12) down to r = 0.8 (z = -1.0), with plate seams in the texture
me = bpy.data.meshes.new('funnel'); vs = []; fs = []; seg = 96
for k in range(seg + 1):
    a = 2 * math.pi * k / seg; c, s = math.cos(a), math.sin(a); vs += [(c * 1.75, s * 1.75, -0.12), (c * 0.8, s * 0.8, -1.0)]
for k in range(seg): i = k * 2; fs.append((i, i + 1, i + 3, i + 2))
me.from_pydata(vs, [], fs); uv = me.uv_layers.new()
for pi, p in enumerate(me.polygons):
    for li, (u, v) in zip(p.loop_indices, [(pi / seg, 0), (pi / seg, 1), ((pi + 1) / seg, 1), ((pi + 1) / seg, 0)]): uv.data[li].uv = (u, v)
new_obj('Funnel', me, M_p, root)
wall = bpy.data.meshes.new('skirt'); vs = []; fs = []
for k in range(seg + 1):
    a = 2 * math.pi * k / seg; c, s_ = math.cos(a), math.sin(a); vs += [(c * 3.0, s_ * 3.0, 0.0), (c * 3.0, s_ * 3.0, -1.0)]
for k in range(seg): i = k * 2; fs.append((i, i + 1, i + 3, i + 2))
wall.from_pydata(vs, [], fs); uv = wall.uv_layers.new()
for pi, p in enumerate(wall.polygons):
    for li, (u, v) in zip(p.loop_indices, [(pi / seg, 0), (pi / seg, 1), ((pi + 1) / seg, 1), ((pi + 1) / seg, 0)]): uv.data[li].uv = (u, 0.03 + 0.94 * v)
new_obj('Skirt', wall, M_o, root)
new_obj('ShaftFloor', annulus(0.001, 0.8, 64, z=-1.0), M_s, root)
gl = empty('ShaftPulse', root); new_obj('ShaftGlow', annulus(0.001, 1.6, 64, z=-0.9), M_g, gl)
keys(gl, 'scale', lambda t: (1 + 0.12 * math.sin(2 * math.pi * t / 3.0),) * 3)
export(out)
