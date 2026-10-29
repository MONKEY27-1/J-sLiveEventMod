"""blender -b -P make_island.py -- <out.glb>
The island the Collision mech fights on: a 2400 m desert heightfield (192 x 192 vertices) with a flat basin round the mech's route, a road from the start to the Collider arena (a flat concrete circle 330 m ahead = Blender -Y),
rolling dunes, grass patches, canyon mesas and mountains on the horizon, rock on the steep parts, all baked into one 1024 px texture. Origin = the nominal ground level; forward (Unity +Z) = Blender -Y."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1:][0]; setup(24, 1)
N = 192; HALF = 1200.0; S = 2 * HALF / N
def smooth(a, b, v): t = np.clip((v - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def noise(X, Y, sc, seed):
    out = np.zeros_like(X, dtype=np.float64)
    for o in range(3):
        f = sc * (2 ** o); uu = (X + HALF) / (2 * HALF) * f; vv = (Y + HALF) / (2 * HALF) * f; i0 = np.floor(uu).astype(int); j0 = np.floor(vv).astype(int); fu = uu - i0; fv = vv - j0
        G = np.random.default_rng(seed + o).random((f + 3, f + 3)); fu = fu * fu * (3 - 2 * fu); fv = fv * fv * (3 - 2 * fv)
        out += (G[j0, i0] * (1 - fu) * (1 - fv) + G[j0, i0 + 1] * fu * (1 - fv) + G[j0 + 1, i0] * (1 - fu) * fv + G[j0 + 1, i0 + 1] * fu * fv) * (0.5 ** o)
    return out / 1.75
def height(X, Y):
    n1 = noise(X, Y, 6, 1); n2 = noise(X, Y, 14, 2); n3 = noise(X, Y, 40, 3); dist = np.hypot(X, Y)
    bx = smooth(230, 420, np.abs(X)); by = np.maximum(smooth(120, 420, Y), smooth(560, 760, -Y)); away = np.maximum(bx, by)       # flat basin round the route (x +-230 m, 120 m behind the start to 560 m ahead)
    H = (9.0 * (n1 - 0.5) + 3.0 * (n3 - 0.5) + 70.0 * smooth(0.60, 0.68, n2) * smooth(380, 560, dist) + 260.0 * smooth(700, 1180, dist) * (0.5 + n1)) * away
    return H * smooth(80, 140, np.hypot(X, Y + 330.0))                                                                         # the Collider arena (0, -330) is flat
xs = np.linspace(-HALF, HALF, N + 1); X, Y = np.meshgrid(xs, xs); H = height(X, Y)
# ---- the baked colour map
T = 1024; tx = np.linspace(-HALF, HALF, T); TX, TY = np.meshgrid(tx, tx); TH = height(TX, TY); gy, gx = np.gradient(TH, 2 * HALF / T); slope = np.hypot(gx, gy)
g = noise(TX, TY, 9, 7); fine = noise(TX, TY, 90, 9); sand = np.stack([0.84 + 0.1 * (fine - 0.5), 0.62 + 0.1 * (fine - 0.5), 0.38 + 0.08 * (fine - 0.5)], -1); grass = np.stack([0.34 + 0.08 * (fine - 0.5), 0.5 + 0.1 * (fine - 0.5), 0.22 + 0 * fine], -1)
rock = np.stack([0.5 + 0.12 * (fine - 0.5), 0.34 + 0.1 * (fine - 0.5), 0.24 + 0.08 * (fine - 0.5)], -1); col = sand * (1 - smooth(0.55, 0.65, g)[..., None]) + grass * smooth(0.55, 0.65, g)[..., None]
col = col * (1 - smooth(0.04, 0.16, slope)[..., None]) + rock * smooth(0.04, 0.16, slope)[..., None]
col = col * (0.9 + 0.2 * smooth(0, 90, TH)[..., None])
road = (np.abs(TX) < 9) & (TY < 60) & (TY > -330); col = np.where(road[..., None], np.array((0.45, 0.43, 0.41)) * (0.95 + 0.1 * fine[..., None]), col)                    # the road
road2 = (np.abs(TX) < 0.8) & (TY < 60) & (TY > -330) & (((TY / 14.0) % 1) < 0.5); col = np.where(road2[..., None], np.array((0.9, 0.85, 0.5)), col)
ar = np.hypot(TX, TY + 330.0); col = np.where((ar < 92)[..., None], np.array((0.5, 0.5, 0.54)) * (0.92 + 0.12 * fine[..., None]), col); col = np.where(((ar > 88) & (ar < 92))[..., None], np.array((0.95, 0.6, 0.15)), col)   # the arena ring
tex = rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), np.ones_like(col[..., 0])); MAT = material('Island', tex[::-1])
# ---- the mesh
verts = [(float(X[j, i]), float(Y[j, i]), float(H[j, i])) for j in range(N + 1) for i in range(N + 1)]; faces = [(j * (N + 1) + i, j * (N + 1) + i + 1, (j + 1) * (N + 1) + i + 1, (j + 1) * (N + 1) + i) for j in range(N) for i in range(N)]
me = bpy.data.meshes.new('Island'); me.from_pydata(verts, [], faces); uvl = me.uv_layers.new()
for p in me.polygons:
    for li, vi in zip(p.loop_indices, p.vertices): uvl.data[li].uv = ((verts[vi][0] + HALF) / (2 * HALF), (verts[vi][1] + HALF) / (2 * HALF))
me.materials.append(MAT)
for p in me.polygons: p.use_smooth = True
o = bpy.data.objects.new('Island', me); bpy.context.collection.objects.link(o); export(out)
