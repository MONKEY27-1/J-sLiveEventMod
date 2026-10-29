"""blender -b -P make_klomberry.py -- <out.glb>
A Klomberry plant (Fortnite): a tuft of slender green leaves round three stalks tipped with clusters of glowing blue berries. One node 'Klomberry' (0.9 m tall, base at z = 0): materials [Leaf (lit), A_Berry (unlit, the plugin pulses its colour)]."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
def tex_leaf(): W = H = 64; U, V = grid(W, H); n = fbm(W, H, 8, 3); return rgba(0.12 + 0.1 * n, 0.45 + 0.2 * n, 0.2 + 0.1 * n, 1.0)
def tex_berry(): W = H = 64; U, V = grid(W, H); d = np.hypot(U - 0.5, V - 0.5) * 2; g = np.clip(1.2 - d, 0, 1); return rgba(0.25 + 0.5 * g, 0.65 + 0.35 * g, 1.0, 1.0)
LEAF = material('Leaf', tex_leaf()); BERRY = material('A_Berry', tex_berry())
b = MB([LEAF, BERRY]); rnd = np.random.default_rng(4)
for k in range(9):                                                                                     # blades
    ang = k * 2 * math.pi / 9 + 0.2; h = 0.55 + 0.25 * rnd.random(); r = 0.05
    pass
tips = []
for k, (x, y, h) in enumerate(((0.0, 0.0, 0.95), (0.18, 0.1, 0.78), (-0.15, 0.12, 0.85))):
    b.cyl((x, y, h / 2), 0.012, h, 0, seg=6, r2=0.008); tips.append((x, y, h))
o = b.build('Klomberry')
parts = [o]
for k in range(10):                                                                                     # leaf blades: slim cones leaning outward
    lm = MB([LEAF]); lm.cyl((0, 0, 0.3), 0.045, 0.6, 0, 5, r2=0.0); l = lm.build('Blade%d' % k); a = k * 2 * math.pi / 10; l.location = (math.cos(a) * 0.03, math.sin(a) * 0.03, 0)
    l.rotation_euler = (0.55 * math.sin(a) * (1 + 0.3 * rnd.random()), -0.55 * math.cos(a), 0); l.scale = (1, 1, 0.8 + 0.7 * rnd.random()); parts.append(l)
for k, (x, y, h) in enumerate(tips):                                                                    # berries: a cluster of 6 at every stalk tip
    for q in range(6):
        bm = displace_icosphere(0.045, 2, 70 + k * 6 + q, 0.02); me = bpy.data.meshes.new('berry'); bm.to_mesh(me); bm.free()
        for p in me.polygons: p.use_smooth = True
        s = new_obj('Berry%d_%d' % (k, q), me, BERRY); a = q * 2 * math.pi / 6; s.location = (x + math.cos(a) * 0.05, y + math.sin(a) * 0.05, h + 0.02 * (q % 3) - 0.04); parts.append(s)
bpy.ops.object.select_all(action='DESELECT')
for p_ in parts: p_.select_set(True)
bpy.context.view_layer.objects.active = o; bpy.ops.object.join(); o.name = 'Klomberry'
export(out)
