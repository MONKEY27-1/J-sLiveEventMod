"""blender -b -P end21_tower.py -- out.glb   THE END: the Cube Queen's tower rising out of the sea: a tall twisting crystal spire (150 m, blue / violet, glowing seams and lit window rows), broken crystal shards around its flanks, floating gold-edged cubes, and a rocky crown. Unity y = height."""
import bpy, sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup(24, 8); R = random.Random(7)
def crystal_tex(W=256, H=256):
    U, V = grid(W, H); n = fbm(W, H, 6, 3); n2 = fbm(W, H, 14, 5)
    base = np.stack([0.16 + 0.22 * n, 0.14 + 0.2 * n, 0.42 + 0.35 * n], -1)
    seam = np.exp(-(np.minimum((U * 6) % 1, 1 - (U * 6) % 1) / 0.03) ** 2); seam2 = np.exp(-(np.minimum((V * 14) % 1, 1 - (V * 14) % 1) / 0.04) ** 2)
    win = (((U * 12) % 1 > 0.25) & ((U * 12) % 1 < 0.7) & ((V * 40) % 1 > 0.3) & ((V * 40) % 1 < 0.75) & (n2 > 0.52)).astype(np.float32)
    col = base + seam[..., None] * np.array([0.1, 0.45, 0.65]) + seam2[..., None] * np.array([0.25, 0.1, 0.5]) + win[..., None] * np.array([0.7, 0.55, 0.9])
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
CR = material('Crystal', crystal_tex()); ROCK = material('Rock', rgba(*(lambda n: (0.25 + 0.2 * n, 0.2 + 0.17 * n, 0.3 + 0.22 * n, 1.0))(fbm(64, 64, 5, 9)))); GOLD = solid('Gold', 1.0, 0.72, 0.2); GLOW = solid('Glow', 0.55, 0.9, 1.0)
b = MB([CR, ROCK, GOLD, GLOW]); C_, K_, G_, L_ = range(4)
H = 150.0; LAYERS = 30; SEG = 7
def ring(h):
    f = h / H; r = 9.0 * (1 - f) ** 1.15 + 1.8 + 1.6 * math.sin(f * 18) ** 2 * (1 - f); tw = f * 5.5; lx = 5.5 * f * f * math.sin(1.1); ly = 5.5 * f * f * math.cos(1.1); pts = []
    for k in range(SEG):
        a = k * 2 * math.pi / SEG + tw; jr = r * (0.82 + 0.3 * ((k * 7 + int(h * 3)) % 5) / 4.0); pts.append((lx + math.cos(a) * jr, ly + math.sin(a) * jr, h))
    return pts
rings = [ring(i * H / LAYERS) for i in range(LAYERS + 1)]
for i in range(LAYERS):
    for k in range(SEG):
        a, c = rings[i][k], rings[i][(k + 1) % SEG]; d, e = rings[i + 1][k], rings[i + 1][(k + 1) % SEG]; u0, u1 = k / SEG, (k + 1) / SEG; v0, v1 = i / LAYERS * 2.0, (i + 1) / LAYERS * 2.0
        b.quad(a, c, e, d, C_, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
top = rings[-1]; cx = sum(p[0] for p in top) / SEG; cy = sum(p[1] for p in top) / SEG
for k in range(SEG): b.tri(top[k], top[(k + 1) % SEG], (cx, cy, H + 9), C_, [(0, 0), (1, 0), (0.5, 1)])
def shard(base, d, length, width, mat):
    d = np.array(d, float); d /= np.linalg.norm(d); up = np.array([0, 0, 1.0]) if abs(d[2]) < 0.9 else np.array([1.0, 0, 0]); s = np.cross(d, up); s /= np.linalg.norm(s); t = np.cross(d, s); base = np.array(base, float); tip = base + d * length
    p = [base + (s * math.cos(a) + t * math.sin(a)) * width for a in (0, 2.094, 4.189)]
    for k in range(3): b.tri(p[k], p[(k + 1) % 3], tip, mat, [(0, 0), (1, 0), (0.5, 1)])
    b.tri(p[0], p[2], p[1], mat, [(0, 0), (1, 0), (0.5, 1)])
for i in range(70):                                                     # crystal shards jutting out of the spire
    h = R.uniform(4, H * 0.93); f = h / H; ring_ = rings[min(LAYERS, int(h / H * LAYERS))]; c0 = np.array(ring_[R.randrange(SEG)]); out_d = np.array([c0[0], c0[1], 0.0]); nd = np.linalg.norm(out_d)
    out_d = out_d / nd if nd > 0.1 else np.array([1.0, 0, 0]); d = out_d * 0.8 + np.array([R.uniform(-.4, .4), R.uniform(-.4, .4), R.uniform(0.1, 0.7)]); shard(c0, d, R.uniform(5, 16) * (1.1 - f * 0.5), R.uniform(0.9, 2.6) * (1.1 - f * 0.4), C_ if i % 5 else L_)
for i in range(26):                                                     # the rocky crown at the water: a broken base of crystal spurs
    a = R.uniform(0, 6.283); r = R.uniform(8, 20); shard((math.cos(a) * r * 0.5, math.sin(a) * r * 0.5, -2), (math.cos(a) * 0.8, math.sin(a) * 0.8, R.uniform(0.9, 1.6)), R.uniform(16, 46), R.uniform(3, 6.5), K_)
for i in range(22):                                                     # floating cubes, gold-edged, orbiting the upper spire
    h = R.uniform(30, H + 20); a = R.uniform(0, 6.283); r = R.uniform(16, 34) * (1.1 - h / (H + 40) * 0.4); s = R.uniform(2.0, 5.5); b.box((math.cos(a) * r, math.sin(a) * r, h), (s, s, s), C_, 2.0, top=G_)
b.box((cx, cy, H + 3), (4, 4, 4), G_, 1.0)
b.build('Spire'); export(out)
