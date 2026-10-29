"""blender -b -P make_volcano.py -- <out_dir>
The Season 8 volcano as in the reference screenshots: a tall cone with a notched crater, a canyon cut down the front, a lava river in it that splits into two near the foot, grassy lower slopes with trees,
dark ash-grey rock above. Writes <out_dir>/volcano.glb: the mountain + trees (lit by the scene) and the glowing lava ribbons + crater lava (A_Lava, alpha blend: drawn unlit whatever the model's lit flag).
Units: metres. Base radius R = 120, rim height H = 105, base on z = 0. The canyon / lava faces Blender -Y (the plugin's +Z, which it turns towards the viewer). Crater centre at (0, 0, RIM_Z) -> printed."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
outdir = sys.argv[sys.argv.index('--') + 1]; setup()
R, HR = 120.0, 105.0; U_RIM = 0.13; H0 = HR / (1 - U_RIM) ** 1.45; TH0 = -math.pi / 2

def meander(u): return 5.0 * np.sin(u * 9.0) * (0.4 + u)
def lateral(x, y, branch):
    """signed lateral distance (m) from a lava river centreline; branch 0 = main, 1 / -1 = the two branches below u = 0.55"""
    r = np.sqrt(x * x + y * y) + 1e-6; th = np.arctan2(y, x); dth = (th - TH0 + np.pi) % (2 * np.pi) - np.pi; u = r / R
    split = np.clip((u - 0.52) / 0.35, 0, 1) ** 1.4 * 46.0; return r * np.sin(dth) - meander(u) - branch * split
def height(x, y):
    r = np.sqrt(x * x + y * y); u = r / R; th = np.arctan2(y, x)
    cone = np.where(u < 1.0, H0 * np.clip(1 - u, 0, 1) ** 1.45, 0.0)
    rough = 1 + (0.05 * np.sin(th * 7 + 1.3) + 0.04 * np.sin(th * 13 + 0.4) + 0.03 * np.sin(th * 23)) * np.clip((u - 0.13) / 0.3, 0, 1)
    n = (np.sin(x * 0.11 + y * 0.07) * 0.5 + np.sin(x * 0.23 - y * 0.19) * 0.3 + np.sin(x * 0.51 + y * 0.43) * 0.2) * 2.4 * np.clip(1 - u, 0, 1) ** 0.5
    h = cone * rough + n
    bowl = U_RIM * 1.0; inside = u < bowl; fl = np.clip(u / bowl, 0, 1); h = np.where(inside, HR - 15.0 * (1 - fl ** 2) + n * 0.4, h)       # crater bowl
    notch = np.exp(-(((th - TH0 + np.pi) % (2 * np.pi) - np.pi) / 0.22) ** 2) * np.clip(1 - np.abs(u - 0.13) / 0.12, 0, 1) * 7.0; h = h - notch                      # broken rim at the canyon
    for br in (0, 1, -1):                                       # the canyon
        l = lateral(x, y, br); wc = 4.0 + 9.0 * u; dmax = (12.0 * np.clip(1 - u, 0, 1) ** 0.4 + 2.0) * (1.0 if br == 0 else np.clip((u - 0.5) * 4, 0, 1)) * (np.clip((u - 0.1) * 8, 0, 1)) * (1 if br == 0 else 0.7)
        if br == 0: dmax = dmax * np.where(u < 0.55, 1.0, np.clip(1 - (u - 0.55) / 0.2, 0.3, 1))
        carve = dmax * np.exp(-(l / wc) ** 2 * 2.2); h = h - np.where((u > 0.1) & (u < 0.98), carve, 0.0)
    return np.maximum(h, 0.0)
def lava_mask(x, y):
    u = np.sqrt(x * x + y * y) / R; m = np.zeros_like(x)
    for br in (0, 1, -1):
        l = lateral(x, y, br); w = 2.6 - 1.2 * u; on = (u > 0.11) & (u < 0.99) & ((u < 0.55) if br == 0 else (u > 0.5))
        if br == 0: on = (u > 0.11) & (u < 0.6)
        m = np.maximum(m, np.where(on, np.clip(1 - np.abs(l) / w, 0, 1), 0.0))
    return np.where(u < U_RIM * 0.8, np.maximum(m, np.clip((1 - u / (U_RIM * 0.8)) * 1.5, 0, 1)), m)

# ---------------------------------------------------------------- mesh
NR, NS = 130, 220; rs = np.linspace(0.0, 1.12, NR + 1) ** 0.85 * R; ths = np.linspace(0, 2 * np.pi, NS, endpoint=False)
RR, TT = np.meshgrid(rs, ths, indexing='ij'); X = RR * np.cos(TT); Y = RR * np.sin(TT); Z = height(X, Y)
verts = np.stack([X, Y, Z], -1).reshape(-1, 3); faces = []
for i in range(NR):
    for j in range(NS):
        a = i * NS + j; b = i * NS + (j + 1) % NS; c = (i + 1) * NS + (j + 1) % NS; d = (i + 1) * NS + j
        if i == 0: faces.append((a, d, c)) if False else faces.append((a, c, d)) if False else faces.append((d, c, b))
        else: faces.append((a, d, c, b))
me = bpy.data.meshes.new('Volcano'); me.from_pydata([tuple(v) for v in verts], [], [tuple(f[::-1]) for f in faces]); uv = me.uv_layers.new()
for p in me.polygons:
    for li, vi in zip(p.loop_indices, p.vertices): uv.data[li].uv = ((me.vertices[vi].co.x / (2 * R * 1.12)) + 0.5, (me.vertices[vi].co.y / (2 * R * 1.12)) + 0.5)
for p in me.polygons: p.use_smooth = True
# colour map (computed from the same height function)
TS = 1536; xs = (np.arange(TS) + 0.5) / TS - 0.5; GX, GY = np.meshgrid(xs * 2 * R * 1.12, xs * 2 * R * 1.12); GZ = height(GX, GY)
gx = np.gradient(GZ, axis=1) / (2 * R * 1.12 / TS); gy = np.gradient(GZ, axis=0) / (2 * R * 1.12 / TS); slope = np.sqrt(gx * gx + gy * gy)
Ur = np.sqrt(GX * GX + GY * GY) / R; f = GZ / HR; nz = fbm(TS, TS, 12, 3, 5); n2 = fbm(TS, TS, 40, 8, 3); strata = 0.5 + 0.5 * np.sin(GZ * 0.9 + nz * 6)
grass = np.stack([0.26 + 0.14 * nz, 0.46 + 0.14 * nz, 0.16 + 0.08 * nz], -1); rock = np.stack([0.30 + 0.04 * strata, 0.27 + 0.03 * strata, 0.28 + 0.03 * strata], -1) * (0.8 + 0.35 * nz[..., None]) * (0.92 + 0.16 * n2[..., None])
ash = np.stack([0.30 + 0 * f, 0.24 + 0 * f, 0.32 + 0 * f], -1); cliff = np.stack([0.50 + 0.05 * strata, 0.42 + 0.04 * strata, 0.38 + 0.03 * strata], -1) * (0.85 + 0.3 * n2[..., None])
gmix = np.clip((0.66 - f) * 3.5, 0, 1) * np.clip((1.3 - slope) * 2.0, 0, 1); col = rock * (1 - gmix[..., None]) + grass * gmix[..., None]
col = col * (1 - np.clip((f - 0.7) * 3, 0, 1)[..., None]) + ash * np.clip((f - 0.7) * 3, 0, 1)[..., None] * (0.8 + 0.4 * n2[..., None])
cl = np.clip((slope - 0.9) * 1.6, 0, 1) * (Ur > 0.1); col = col * (1 - cl[..., None]) + cliff * cl[..., None]
lm = lava_mask(GX, GY); heat = np.clip(lm * 1.6, 0, 1); core = np.clip((lm - 0.55) * 2.2, 0, 1)
lava = np.stack([1.0 * np.ones_like(lm), 0.38 + 0.5 * core, 0.05 + 0.4 * core], -1); col = col * (1 - heat[..., None]) + lava * heat[..., None]
glowrim = np.clip(1 - np.abs(lava_mask(GX, GY) - 0.0), 0, 1) * 0                                              # (kept for clarity: the glow around the river comes from the ribbon mesh)
img_tex = rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
mv = material('Mountain', img_tex); me.materials.append(mv); vo = bpy.data.objects.new('Volcano', me); bpy.context.collection.objects.link(vo)
# trees round the foot (merged: pines + round trees)
rng = np.random.default_rng(11); tm = material('TreeGreen', tex_rock(14, (0.55, 1.0, 0.45))); tt = material('Trunk', tex_solid(0.32, 0.22, 0.15)); b = MB([tm, tt]); placed = 0
while placed < 110:
    u = 0.72 + rng.random() * 0.4; th = rng.random() * 2 * math.pi; x, y = u * R * math.cos(th), u * R * math.sin(th)
    if abs(lateral(np.array([x]), np.array([y]), 0)[0]) < 22 and u < 0.95: continue
    z = float(height(np.array([x]), np.array([y]))[0]); s = 0.8 + rng.random() * 0.9
    if rng.random() < 0.5: b.cyl((x, y, z + 1.2 * s), 0.35 * s, 2.4 * s, 1, 6); b.cyl((x, y, z + 5.0 * s), 2.6 * s, 5.5 * s, 0, 9, r2=0.2 * s); b.cyl((x, y, z + 7.6 * s), 1.9 * s, 4.0 * s, 0, 9, r2=0.1 * s)
    else: b.cyl((x, y, z + 1.5 * s), 0.45 * s, 3.0 * s, 1, 6); b.cyl((x, y, z + 5.0 * s), 3.4 * s, 3.2 * s, 0, 10, r2=0.5 * s); b.cyl((x, y, z + 4.2 * s), 0.5 * s, 0.1 * s, 0, 4, r2=3.6 * s)
    placed += 1
to = b.build('Trees')
print('crater rim height', HR, 'crater floor', HR - 15)

# ---------------------------------------------------------------- the lava (second file, unlit)
lt = np.zeros((16, 64, 4), np.float32); vv = (np.arange(64) + 0.5) / 64; prof = np.clip(1 - np.abs(vv * 2 - 1), 0, 1)
for k in range(16): lt[k, :, 0] = 1.0; lt[k, :, 1] = 0.35 + 0.6 * prof ** 2; lt[k, :, 2] = 0.05 + 0.5 * prof ** 4; lt[k, :, 3] = np.clip(prof * 2.2, 0, 1) * 0.95
ML = material('A_Lava', lt); LM = [ML]
def ribbon(name, br, u0, u1, width):
    rb = MB(LM); n = 60; pts = []
    for k in range(n + 1):
        u = u0 + (u1 - u0) * k / n; r = u * R; l0 = meander(u) + br * np.clip((u - 0.52) / 0.35, 0, 1) ** 1.4 * 46.0
        x = r * math.cos(TH0) - l0 * math.sin(TH0) * 1.0; y = r * math.sin(TH0) + l0 * math.cos(TH0) * 1.0
        pts.append((x, y, float(height(np.array([x]), np.array([y]))[0]) + 0.5, u))
    for k in range(n):
        a, c = pts[k], pts[k + 1]; wa = width * (1.1 - 0.5 * a[3]); wc2 = width * (1.1 - 0.5 * c[3])
        def side(p, w): return (p[0] - w, p[1], p[2]), (p[0] + w, p[1], p[2])
        al, ar = side(a, wa); cl_, cr = side(c, wc2); rb.quad(al, ar, cr, cl_, 0, [(0, k / 8), (1, k / 8), (1, (k + 1) / 8), (0, (k + 1) / 8)])
    return rb.build(name)
ribbon('LavaMain', 0, 0.11, 0.58, 2.7); ribbon('LavaL', 1, 0.5, 0.97, 2.2); ribbon('LavaR', -1, 0.5, 0.97, 2.2)
cm = MB(LM); cz = HR - 14.0
for k in range(24):
    a0 = 2 * math.pi * k / 24; a1 = 2 * math.pi * (k + 1) / 24; rr = R * U_RIM * 0.75
    cm.tri((0, 0, cz + 1.2), (rr * math.cos(a0), rr * math.sin(a0), cz + 0.2), (rr * math.cos(a1), rr * math.sin(a1), cz + 0.2), 0, [(0.5, 0.9), (0, 0.1), (1, 0.1)])
cm.build('CraterLava')
export(os.path.join(outdir, 'volcano.glb'))
print('wrote volcano + lava')
