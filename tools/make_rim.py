"""blender -b -P make_rim.py -- <out.glb>
The Loot Lake pit rim for the Vault site: a three-step terraced cliff ring (radius 52 -> 73 m, up to 8.4 m high) with rock faces and grassy tops and three ramps (gaps) leading in, standing round the vault.
One object 'PitRim' (origin = centre of the ring, base on z = 0). Rock faces use a strata texture, tops a grass texture."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
ROCK = material('RimRock', tex_rock(21, (0.95, 0.85, 0.8))); W = H = 128; n = fbm(W, H, 6, 22); GRASS = material('RimGrass', rgba(0.26 + 0.14 * n, 0.44 + 0.16 * n, 0.16 + 0.08 * n, 1.0))
GAPS = [math.radians(a) for a in (60, 180, 300)]; N = 200
def gap(th):
    d = min(abs((th - g + math.pi) % (2 * math.pi) - math.pi) for g in GAPS); return min(1.0, max(0.0, (d - math.radians(9)) / math.radians(15)))
def sm(t): return t * t * (3 - 2 * t)
rng = np.random.default_rng(3); jit = [(rng.random() - 0.5) for _ in range(N + 1)]
def rad(k, th, j): return 52.0 + 7.0 * k + 1.8 * math.sin(th * 5 + k) + 1.2 * math.sin(th * 11) + jit[j] * 0.8
b = MB([ROCK, GRASS])
for k in range(3):
    for j in range(N):
        t0 = 2 * math.pi * j / N; t1 = 2 * math.pi * (j + 1) / N
        h0 = 2.8 * (k + 1) * sm(gap(t0)) + 0.25 * jit[j] * gap(t0); h1 = 2.8 * (k + 1) * sm(gap(t1)) + 0.25 * jit[(j + 1) % N] * gap(t1)
        ri0, ri1 = rad(k, t0, j), rad(k, t1, (j + 1) % N); ro0, ro1 = ri0 + 7.0, ri1 + 7.0
        P = lambda r, t, z: (r * math.cos(t), r * math.sin(t), z)
        arc = (t1 - t0) * ri0
        b.quad(P(ri0, t0, 0), P(ri1, t1, 0), P(ri1, t1, h1), P(ri0, t0, h0), 0, [(j * arc / 8, 0), ((j + 1) * arc / 8, 0), ((j + 1) * arc / 8, h1 / 8), (j * arc / 8, h0 / 8)])        # cliff face (inner side)
        b.quad(P(ri0, t0, h0), P(ri1, t1, h1), P(ro1, t1, h1), P(ro0, t0, h0), 1, [(P(ri0, t0, 0)[0] / 8, P(ri0, t0, 0)[1] / 8), (P(ri1, t1, 0)[0] / 8, P(ri1, t1, 0)[1] / 8), (P(ro1, t1, 0)[0] / 8, P(ro1, t1, 0)[1] / 8), (P(ro0, t0, 0)[0] / 8, P(ro0, t0, 0)[1] / 8)])   # grassy top
        if k == 2: b.quad(P(ro1, t1, 0), P(ro0, t0, 0), P(ro0, t0, h0), P(ro1, t1, h1), 0, [(j * arc / 8, 0), ((j + 1) * arc / 8, 0), ((j + 1) * arc / 8, h1 / 8), (j * arc / 8, h0 / 8)])   # outer face
b.build('PitRim')
export(out)
