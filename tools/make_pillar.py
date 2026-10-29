"""blender -b -P make_pillar.py -- <out.glb>
The Unvaulting voting pillar (Fortnite Season 8 'Unvaulting' glass monoliths): a tall six-sided glass crystal on a stepped plinth, pink at the foot to pale blue at the top, a slanted faceted top, bright edge lines,
a glowing inner core and a ring of light at the base. 14 loose glass shards sit inside it (node 'Shards', the plugin hides it and throws the shards out when the pillar breaks).
Node tree: Pillar > Plinth, GlassGroup (Glass, GlassInner, Edges), Light (base glow ring), Shards (Shard0..Shard13).  Metres, z up, base on z = 0; the item floats at about z = 2.5. A_* materials are alpha blended."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
H = 22.0; RAD = 2.3
def glass_tex():
    W, Hh = 64, 256; U, V = grid(W, Hh); n = fbm(W, Hh, 5, 61); streak = 0.5 + 0.5 * np.sin(U * 2 * np.pi * 6 + n * 3) * 0.5
    base = np.stack([0.95 - 0.35 * V, 0.50 + 0.30 * V, 0.95 + 0.0 * V], -1) * (0.85 + 0.25 * n[..., None]) * (0.9 + 0.2 * streak[..., None])
    a = np.clip(0.55 - 0.32 * V + 0.12 * (n - 0.5) + 0.1 * np.exp(-((V - 0.12) / 0.1) ** 2), 0.08, 0.9)
    return rgba(np.clip(base[..., 0], 0, 1), np.clip(base[..., 1], 0, 1), np.clip(base[..., 2], 0, 1), a)
def core_tex():
    W, Hh = 16, 128; U, V = grid(W, Hh); a = 0.34 * np.exp(-((U - 0.5) * 3.2) ** 2) * (0.35 + 0.65 * (1 - V) ** 0.7); return rgba(1.0, 0.55 + 0.35 * V, 0.95, a)
def light_tex():
    W, Hh = 128, 16; U, V = grid(W, Hh); a = np.exp(-((V - 0.5) * 3.0) ** 2) * (0.75 + 0.25 * np.sin(U * 2 * np.pi * 12)); return rgba(1.0, 0.75, 1.0, a)
M_glass = material('A_Glass', glass_tex()); M_core = material('A_Core', core_tex()); M_light = material('A_Light', light_tex())
M_edge = material('Edge', tex_solid(0.9, 0.92, 1.0)); M_plinth = material('Plinth', tex_concrete(62, (0.78, 0.8, 0.95))); M_shard = M_glass
root = bpy.data.objects.new('Pillar', None); bpy.context.collection.objects.link(root)
# plinth: two hexagonal steps
mb = MB([M_plinth, M_edge])
for k, (r, h, z) in enumerate(((3.4, 0.35, 0.0), (2.9, 0.3, 0.35))): mb.cyl((0, 0, z + h / 2), r, h, 0, 6, tile=3.0)
mb.build('Plinth', root)
# the glass crystal: a hexagonal prism with a slanted, chamfered top
def prism(name, r, rot, mat, base_z=0.65, top_cut=1.8, phi=0.8, chamfer=0.0):
    b = MB([mat]); N = 6; ang = [rot + 2 * math.pi * k / N for k in range(N)]
    def zt(a, rr=1.0): return H + top_cut * math.cos(a - phi) * rr
    for k in range(N):
        a0, a1 = ang[k], ang[(k + 1) % N]; p0 = (r * math.cos(a0), r * math.sin(a0)); p1 = (r * math.cos(a1), r * math.sin(a1))
        b.quad((p0[0], p0[1], base_z), (p1[0], p1[1], base_z), (p1[0], p1[1], zt(a1) - chamfer), (p0[0], p0[1], zt(a0) - chamfer), 0, [(k / N, 0), ((k + 1) / N, 0), ((k + 1) / N, 1), (k / N, 1)])
        if chamfer > 0:
            q0 = (p0[0] * 0.82, p0[1] * 0.82); q1 = (p1[0] * 0.82, p1[1] * 0.82)
            b.quad((p0[0], p0[1], zt(a0) - chamfer), (p1[0], p1[1], zt(a1) - chamfer), (q1[0], q1[1], zt(a1)), (q0[0], q0[1], zt(a0)), 0, [(k / N, 0.97), ((k + 1) / N, 0.97), ((k + 1) / N, 1), (k / N, 1)])
    f = 0.82 if chamfer > 0 else 1.0
    for k in range(N):
        a0, a1 = ang[k], ang[(k + 1) % N]
        b.tri((0, 0, H + top_cut * 0.0), (r * f * math.cos(a0), r * f * math.sin(a0), zt(a0)), (r * f * math.cos(a1), r * f * math.sin(a1), zt(a1)), 0, [(0.5, 1), (k / N, 1), ((k + 1) / N, 1)])
    return b
gg = bpy.data.objects.new('GlassGroup', None); bpy.context.collection.objects.link(gg); gg.parent = root
prism('Glass', RAD, 0.0, M_glass, chamfer=0.35).build('Glass', gg)
prism('GlassInner', RAD * 0.72, math.pi / 6, M_core, base_z=0.9, top_cut=1.2, phi=0.8).build('GlassInner', gg)
eb = MB([M_edge]); N = 6
for k in range(N):
    a = 2 * math.pi * k / N; x, y = RAD * math.cos(a), RAD * math.sin(a); eb.box((x, y, 0.65 + (H + 1.8 * math.cos(a - 0.8) - 0.65) / 2), (0.09, 0.09, H + 1.8 * math.cos(a - 0.8) - 0.65), 0, 1.0)
    a2 = 2 * math.pi * (k + 1) / N; x2, y2 = RAD * math.cos(a2), RAD * math.sin(a2)
    z1, z2 = H + 1.8 * math.cos(a - 0.8), H + 1.8 * math.cos(a2 - 0.8); L = math.hypot(x2 - x, y2 - y)
    for zz, (xa, ya, xb, yb, za, zb) in (('top', (x, y, x2, y2, z1, z2)),):                           # a thin rail along each top edge (built from short boxes)
        steps = 8
        for s in range(steps): t = (s + 0.5) / steps; eb.box((xa + (xb - xa) * t, ya + (yb - ya) * t, za + (zb - za) * t), (L / steps * 1.05, 0.09, 0.09), 0, 1.0)
eb.build('Edges', gg)
# base light ring
lb = MB([M_light]); r0, r1 = 3.2, 5.6
for k in range(48):
    a0 = 2 * math.pi * k / 48; a1 = 2 * math.pi * (k + 1) / 48
    lb.quad((r0 * math.cos(a0), r0 * math.sin(a0), 0.06), (r1 * math.cos(a0), r1 * math.sin(a0), 0.06), (r1 * math.cos(a1), r1 * math.sin(a1), 0.06), (r0 * math.cos(a1), r0 * math.sin(a1), 0.06), 0, [(k / 48, 0), (k / 48, 1), ((k + 1) / 48, 1), ((k + 1) / 48, 0)])
lb.build('Light', root)
# loose shards: irregular glass chunks round the item's height
sh = bpy.data.objects.new('Shards', None); bpy.context.collection.objects.link(sh); sh.parent = root; rng = np.random.default_rng(7)
for k in range(14):
    b = MB([M_shard]); s = 0.5 + rng.random() * 0.9; hx, hy, hz = s * (0.4 + rng.random() * 0.6), s * (0.2 + rng.random() * 0.4), s * (0.6 + rng.random() * 1.0)
    pts = [(-hx, -hy, -hz), (hx, -hy * 0.6, -hz * 0.8), (hx * 0.7, hy, -hz * 0.5), (-hx * 0.6, hy * 0.8, -hz), (-hx * 0.4, -hy * 0.5, hz), (hx * 0.9, -hy * 0.2, hz * 0.7), (hx * 0.3, hy * 0.9, hz * 0.9), (-hx * 0.8, hy * 0.4, hz * 0.6)]
    quads = ((0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0))
    for q in quads: b.quad(pts[q[0]], pts[q[1]], pts[q[2]], pts[q[3]], 0, [(0.1, 0.5), (0.4, 0.5), (0.4, 0.9), (0.1, 0.9)])
    o = b.build('Shard%d' % k, sh); a = rng.random() * 2 * math.pi; rr = rng.random() * RAD * 0.7; o.location = (rr * math.cos(a), rr * math.sin(a), 1.2 + rng.random() * 6.0); o.rotation_euler = (rng.random() * 6, rng.random() * 6, rng.random() * 6)
export(out)
