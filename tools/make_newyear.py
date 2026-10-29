"""blender -b -P make_newyear.py -- <disco|crystal> <out.glb>
disco: a mirror ball (1 m radius, centre 1.4 m above the origin; flat tiles of silver / pale blue / pink glass, dark grout, a cable and hook on top).
crystal: the 2022 New Year's ball (1 m radius; triangular crystal facets in white / ice blue, a dark ring of rods round the middle and a short pole below)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
import bmesh
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(); rnd = np.random.default_rng(31)
def tint(k, pal):
    c = np.array(pal[int(rnd.integers(len(pal)))]); return np.clip(c * (0.72 + 0.28 * rnd.random()), 0, 1)
if kind == 'disco':
    LON, LAT = 32, 16; pal = [(0.95, 0.95, 1.0), (0.8, 0.9, 1.0), (1.0, 0.82, 0.92), (0.9, 0.92, 0.95), (0.85, 1.0, 0.95), (1.0, 0.95, 0.8)]
    img = np.zeros((LAT, LON, 4), np.float32)
    for i in range(LAT):
        for j in range(LON): img[i, j, :3] = tint(0, pal); img[i, j, 3] = 1.0
    TILE = material('Disco', img); DARK = solid('Grout', 0.05, 0.05, 0.07); STEEL = solid('Steel', 0.55, 0.57, 0.62)
    b = MB([TILE, DARK, STEEL]); C = np.array([0, 0, 1.4])
    def P(i, j, s=1.0):
        th = math.pi * i / LAT; ph = 2 * math.pi * j / LON; return C + s * np.array([math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)])
    for i in range(LAT):
        for j in range(LON):
            uv = ((j + 0.5) / LON, (i + 0.5) / LAT); cen = (P(i, j) + P(i + 1, j + 1)) / 2; k = 0.97      # each tile shrunk a little: the grout shows between them
            def Q(ii, jj): p = P(ii, jj); return tuple(C + (p - C) * 1.0 + (cen - p) * (1 - k) * 0.0 if False else cen + (p - cen) * k)
            if i == 0: b.tri(Q(0, j), Q(1, j + 1), Q(1, j), 0, [uv] * 3)
            elif i == LAT - 1: b.tri(Q(i, j), Q(i, j + 1), Q(LAT, j), 0, [uv] * 3)
            else: b.quad(Q(i, j), Q(i, j + 1), Q(i + 1, j + 1), Q(i + 1, j), 0, [uv] * 4)
    parts = [b.build('Tiles')]
    h = MB([STEEL]); h.cyl((0, 0, 2.7), 0.025, 0.7, 0, 8); h.cyl((0, 0, 2.4), 0.12, 0.1, 0, 12); parts.append(h.build('Cable'))
    name = 'DiscoBall'
else:
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0); pal = [(1, 1, 1), (0.82, 0.93, 1.0), (0.7, 0.86, 1.0), (0.95, 0.97, 1.0)]
    N = 64; img = np.zeros((1, N, 4), np.float32)
    for k in range(N): img[0, k, :3] = tint(0, pal); img[0, k, 3] = 1.0
    CR = material('Crystal', img); DARK = solid('Frame', 0.08, 0.09, 0.12); b = MB([CR]); C = np.array([0, 0, 1.6])
    for n_, f in enumerate(bm.faces):
        vs = [np.array(v.co) for v in f.verts]; u = ((n_ % N) + 0.5) / N; cen = sum(vs) / 3; vs = [cen + (v - cen) * 0.95 for v in vs]; b.tri(*[tuple(C + v) for v in vs], 0, [(u, 0.5)] * 3)
    parts = [b.build('Facets')]; r = MB([DARK]); r.cyl((0, 0, 0.8), 0.06, 1.6, 0, 8)
    for k in range(10): a = k * math.pi * 2 / 10; r.cyl((math.cos(a) * 1.0, math.sin(a) * 1.0, 1.6), 0.03, 1.8, 0, 6)
    parts.append(r.build('Frame')); name = 'CrystalBall'
bpy.ops.object.select_all(action='DESELECT')
for p_ in parts: p_.select_set(True)
bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join(); parts[0].name = name
export(out)
