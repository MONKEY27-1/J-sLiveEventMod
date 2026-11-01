"""blender -b -P zc_world.py -- <crystals|lotus|rift|rocks|chamber> <out.glb>
ZERO CRISIS props (16 Mar 2021). Unity axes: x right, y up, z forward (Blender here: (x, -z, y)); glowing materials are called SF_G_*, translucent ones SF_A_* (the plugin tells them apart by name), everything else is lit.
crystals: CryA..CryF pink glass clusters (glowing core), Spike, DarkA..DarkC corrupted rocks with red eyes.  lotus: the Zero Point bloom, petals hinged at one point (Petal<ring>_<k>), Core.
rift: Ring0-3, Disc, Beam, Shard0-11 (a Reality Rift, 1 m radius, faces +z).  rocks: floating blue stones, hex slabs and pillars.  chamber: the circular deck on top of the Spire."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 2)
R = np.random.default_rng(21)
def Z(x, y, z): return (x, -z, y)
def Zv(v): return (v[0], -v[2], v[1])
def gradtex(name, W, H, f, alpha=True):
    """f(u, v) -> (r, g, b, a) arrays; v runs 0..1 up the texture"""
    U, V = grid(W, H); r, g, b, a = f(U, V); return material(name, rgba(np.clip(r, 0, 1), np.clip(g, 0, 1), np.clip(b, 0, 1), np.clip(a, 0, 1)))
def basis(d):
    d = np.array(d, float); d /= np.linalg.norm(d); t = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 0, 1.0]); u = np.cross(d, t); u /= np.linalg.norm(u); v = np.cross(d, u); return d, u, v
def prism(b, base, d, r, h, mat, sides=6, tipfrac=0.28, taper=0.12, cap=True):
    """a pointed crystal: a prism of height h*(1-tipfrac) topped by a pyramid; uv v = 0 at the base .. 1 at the tip"""
    d, u, v = basis(d); base = np.array(base, float); ring = lambda k, rr, t: base + d * (h * t) + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * rr
    t1 = 1 - tipfrac; apex = base + d * h
    for k in range(sides):
        a, c = k, (k + 1) % sides; u0, u1 = k / sides, (k + 1) / sides
        b.quad(Zv(ring(a, r, 0)), Zv(ring(c, r, 0)), Zv(ring(c, r * (1 - taper), t1)), Zv(ring(a, r * (1 - taper), t1)), mat, [(u0, 0), (u1, 0), (u1, t1), (u0, t1)])
        b.tri(Zv(ring(a, r * (1 - taper), t1)), Zv(ring(c, r * (1 - taper), t1)), Zv(apex), mat, [(u0, t1), (u1, t1), ((u0 + u1) / 2, 1)])
def cluster(name, seed, n, spread, hmin, hmax, m_body, m_core, tilt=0.55):
    rr = np.random.default_rng(seed); b = MB([m_body, m_core])
    for i in range(n):
        ang = rr.random() * 6.2832; rad = spread * (0.0 if i == 0 else 0.35 + 0.65 * rr.random()); h = hmax if i == 0 else hmin + (hmax - hmin) * rr.random() ** 1.3; r = 0.22 * h ** 0.7 + 0.1
        d = np.array([math.sin(ang) * tilt * (0.3 + rr.random()) * (0.0 if i == 0 else 1.0), 1.0, math.cos(ang) * tilt * (0.3 + rr.random()) * (0.0 if i == 0 else 1.0)]); base = np.array([math.cos(ang) * rad, -0.2, math.sin(ang) * rad])
        prism(b, base, d, r, h, 0, 6); prism(b, base + d / np.linalg.norm(d) * 0.1, d, r * 0.38, h * 0.84, 1, 6, 0.3, 0.1)
    return b.build(name)

if kind == 'crystals':
    body = gradtex('SF_A_Crystal', 16, 64, lambda U, V: (0.55 + 0.45 * V ** 1.5 + 0 * U, 0.12 + 0.5 * V ** 1.8, 0.8 + 0.2 * V, 0.42 + 0.4 * V))
    core = gradtex('SF_G_Pink', 16, 64, lambda U, V: (0.95 + 0 * U, 0.3 + 0.55 * V, 0.9 + 0.1 * V, 1 + 0 * U))
    for i, (n, sp, h0, h1) in enumerate(((7, 1.6, 1.5, 4.5), (5, 1.2, 1.0, 3.2), (9, 2.2, 1.8, 6.0), (4, 0.9, 1.0, 2.4), (6, 1.8, 2.0, 5.2), (8, 2.6, 1.2, 4.0))): cluster('Cry' + 'ABCDEF'[i], 30 + i, n, sp, h0, h1, body, core)
    b = MB([body, core]); prism(b, (0, -0.5, 0), (0.05, 1, 0.02), 1.3, 16.0, 0, 6, 0.22, 0.05); prism(b, (0, 0.0, 0), (0.05, 1, 0.02), 0.5, 13.0, 1, 6, 0.25, 0.05); b.build('Spike')
    dk = solid('SF_Dark', 0.05, 0.04, 0.07); eye = gradtex('SF_G_Red', 8, 8, lambda U, V: (1 + 0 * U, 0.25 + 0.3 * V, 0.15 + 0 * U, 1 + 0 * U)); rk = material('SF_Rock', tex_rock(5, (0.45, 0.4, 0.5)))
    for i in range(3):
        bm = displace_icosphere(2.2 + i * 0.6, 3, 70 + i, 0.4, (1.0, 0.8 + 0.2 * i, 0.9)); me = bpy.data.meshes.new('dk'); bm.to_mesh(me); bm.free(); o = new_obj('Dark' + 'ABC'[i], me, dk)
        for p in me.polygons: p.use_smooth = False
        o.data.materials.append(eye)
        # red eyes: small glowing crystals poking out of the rock
        rr = np.random.default_rng(80 + i); b = MB([eye])
        for k in range(5): a = rr.random() * 6.2832; el = 0.1 + 0.8 * rr.random(); d = np.array([math.cos(a) * math.cos(el), math.sin(el), math.sin(a) * math.cos(el)]); prism(b, d * (1.9 + i * 0.5), d, 0.28, 0.9, 0, 5)
        eo = b.build('DarkEye' + 'ABC'[i]); eo.parent = o
    export(out)

elif kind == 'lotus':
    mats = [gradtex('SF_A_PetalIn', 16, 64, lambda U, V: (0.8 + 0.2 * V + 0 * U, 0.9 + 0.1 * V, 1 + 0 * U, 0.25 + 0.65 * V ** 0.8 * (1 - 0.3 * np.abs(U - 0.5)))), gradtex('SF_A_PetalMid', 16, 64, lambda U, V: (1 + 0 * U, 0.55 + 0.4 * V, 0.9 + 0.1 * V, 0.2 + 0.7 * V ** 0.8 * (1 - 0.3 * np.abs(U - 0.5)))),
            gradtex('SF_A_PetalOut', 16, 64, lambda U, V: (0.55 + 0.45 * V + 0 * U, 0.6 + 0.4 * V, 1 + 0 * U, 0.2 + 0.6 * V ** 0.8 * (1 - 0.3 * np.abs(U - 0.5))))]
    core = gradtex('SF_G_White', 8, 8, lambda U, V: (1 + 0 * U, 1 + 0 * U, 1 + 0 * U, 1 + 0 * U)); root = empty('Lotus')
    def petal(name, L, W, bend, mat, parent):
        b = MB([mat]); N = 8; pts = []
        for k in range(N + 1):
            s = k / N; w = W * math.sin(math.pi * s ** 0.72) ** 0.9; y = L * s; z = bend * L * s * s; pts.append(((-w, y, z - 0.04 * L * (1 - s)), (0, y, z + 0.05 * L * math.sin(math.pi * s)), (w, y, z - 0.04 * L * (1 - s))))
        for k in range(N):
            for j in range(2):
                a, c = pts[k], pts[k + 1]; v0, v1 = k / N, (k + 1) / N; b.quad(Zv(a[j]), Zv(a[j + 1]), Zv(c[j + 1]), Zv(c[j]), 0, [(j / 2, v0), ((j + 1) / 2, v0), ((j + 1) / 2, v1), (j / 2, v1)])
        o = b.build(name); o.parent = parent; return o
    for ring, (n, L, W, bend, mi) in enumerate(((10, 1.0, 0.30, 0.55, 2), (14, 0.86, 0.26, 0.5, 1), (18, 0.66, 0.22, 0.4, 0))):
        for k in range(n): petal('Petal%d_%d' % (ring, k), L, W, bend, mats[mi], root)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=0.09, calc_uvs=True); me = bpy.data.meshes.new('core'); bm.to_mesh(me); bm.free(); co = new_obj('Core', me, core, root)
    export(out)

elif kind == 'rift':
    root = empty('Rift')
    cols = [('SF_G_Purp', (0.7, 0.3, 1.0)), ('SF_G_Pink', (1.0, 0.45, 0.95)), ('SF_G_White', (1.0, 1.0, 1.0)), ('SF_G_Cyan', (0.35, 0.9, 1.0))]
    for i, (nm, c) in enumerate(cols):
        m = gradtex(nm, 8, 32, lambda U, V, c=c: (c[0] + 0 * U, c[1] + 0 * U, c[2] + 0 * U, np.clip(1 - np.abs(V - 0.5) * 2.0, 0, 1) ** 0.6 * (0.55 + 0.45 * np.sin(U * 6.2832 * (3 + i) + i)))); r0 = 0.82 + 0.06 * i; me = annulus(r0, r0 + 0.12 + 0.04 * i, 96, xz=True); new_obj('Ring%d' % i, me, m, root)
    sw = gradtex('SF_A_Swirl', 128, 128, lambda U, V: (0.6 + 0.4 * np.sin(U * 6.2832 * 5 + V * 14) + 0 * U, 0.3 + 0.5 * np.sin(V * 30 - U * 6.2832 * 3) ** 2, 1 + 0 * U, np.clip(0.25 + 0.55 * np.sin(U * 6.2832 * 3 + V * 9) ** 2, 0, 1) * np.clip(1.4 - V * 1.0, 0, 1)))
    me = annulus(0.0, 0.84, 64, xz=True); new_obj('Disc', me, sw, root)
    bm = MB([gradtex('SF_A_Beam', 16, 64, lambda U, V: (0.9 + 0 * U, 0.7 + 0.3 * V, 1 + 0 * U, np.clip(1 - V, 0, 1) ** 1.3 * 0.55 * (0.6 + 0.4 * np.sin(U * 6.2832 * 4))))])
    seg = 16
    for k in range(seg):
        a0, a1 = 2 * math.pi * k / seg, 2 * math.pi * (k + 1) / seg; bm.quad(Z(math.cos(a0), 0, math.sin(a0)), Z(math.cos(a1), 0, math.sin(a1)), Z(math.cos(a1), 1, math.sin(a1)), Z(math.cos(a0), 1, math.sin(a0)), 0, [(k / seg, 0), ((k + 1) / seg, 0), ((k + 1) / seg, 1), (k / seg, 1)])
    bo = bm.build('Beam'); bo.parent = root; wm = gradtex('SF_G_White2', 8, 8, lambda U, V: (1 + 0 * U, 1 + 0 * U, 1 + 0 * U, 1 + 0 * U))
    for k in range(12):
        a = k / 12 * 6.2832; b = MB([wm]); prism(b, (0, 0, 0), (0, 1, 0), 0.05, 0.28, 0, 4, 0.5, 0.0); o = b.build('Shard%d' % k); o.parent = root; o.location = (math.cos(a) * 0.95, 0, math.sin(a) * 0.95)
    export(out)

elif kind == 'rocks':
    U, V = grid(64, 64); n = fbm(64, 64, 5, 7); rk = material('SF_Rock', rgba(0.16 + 0.12 * n, 0.26 + 0.14 * n, 0.58 + 0.2 * n, 1.0)); tp = material('SF_RockTop', rgba(0.5 + 0.2 * n, 0.72 + 0.15 * n, 1.0, 1.0)); gl = gradtex('SF_G_Cyan', 8, 8, lambda U, V: (0.4 + 0 * U, 0.9 + 0 * U, 1 + 0 * U, 1 + 0 * U))
    for i in range(6):
        bm = displace_icosphere(1.0, 3, 90 + i, 0.32, (1.0, 0.5 + 0.1 * (i % 3), 1.0)); me = bpy.data.meshes.new('rk'); bm.to_mesh(me); bm.free(); o = new_obj('Rock%d' % i, me, rk); o.data.materials.append(tp)
        for p in me.polygons: p.material_index = 1 if p.center.z > 0.22 else 0; p.use_smooth = False
    b = MB([rk, tp, gl])
    for i in range(3):
        bb = MB([rk, tp, gl]); r = 3.0 + i * 0.8; bb.cyl(Z(0, 0, 0), r, 0.7, 0, 6, r2=r * 0.8, axis='z'); bb.cyl(Z(0, 0.36, 0), r * 0.97, 0.05, 1, 6, axis='z'); bb.cyl(Z(0, 0.4, 0), r * 0.55, 0.04, 2, 6, axis='z'); bb.build('Slab%d' % i)
    for i in range(2): bb = MB([rk, tp, gl]); bb.cyl(Z(0, 0, 0), 1.4, 12.0 + 4 * i, 0, 6, r2=1.1, axis='z'); bb.cyl(Z(0, 6.2 + 2 * i, 0), 1.0, 0.3, 2, 6, axis='z'); bb.build('Pillar%d' % i)
    export(out)

elif kind == 'chamber':
    U, V = grid(64, 64); n = fbm(64, 64, 5, 9); rk = material('SF_Rock', rgba(0.2 + 0.1 * n, 0.3 + 0.12 * n, 0.6 + 0.2 * n, 1.0)); tp = material('SF_RockTop', rgba(0.55 + 0.15 * n, 0.75 + 0.1 * n, 1.0, 1.0))
    gl = gradtex('SF_G_Cyan', 8, 8, lambda U, V: (0.45 + 0 * U, 0.92 + 0 * U, 1 + 0 * U, 1 + 0 * U)); pk = gradtex('SF_G_Pink', 8, 8, lambda U, V: (1 + 0 * U, 0.55 + 0 * U, 0.95 + 0 * U, 1 + 0 * U)); wh = gradtex('SF_G_White', 8, 8, lambda U, V: (1 + 0 * U, 1 + 0 * U, 1 + 0 * U, 1 + 0 * U))
    b = MB([rk, tp, gl, pk, wh]); b.cyl(Z(0, -0.6, 0), 17.0, 1.2, 0, 40, r2=14.0, axis='z'); b.cyl(Z(0, 0.05, 0), 16.0, 0.12, 1, 40, axis='z')
    for r in (5.0, 9.0, 13.0): b.cyl(Z(0, 0.14, 0), r, 0.05, 2, 48, r2=r - 0.35, axis='z')
    for k in range(8):
        a = k / 8 * 6.2832; x, z = math.cos(a) * 14.2, math.sin(a) * 14.2; b.cyl(Z(x, 3.5, z), 0.75, 7.0, 0, 6, r2=0.55, axis='z'); b.cyl(Z(x, 7.4, z), 0.5, 0.8, 4, 6, r2=0.2, axis='z'); b.cyl(Z(x, 0.3, z), 1.1, 0.6, 1, 6, axis='z')
    b.cyl(Z(0, 0.9, 0), 3.4, 1.7, 0, 24, r2=2.6, axis='z'); b.cyl(Z(0, 1.78, 0), 3.0, 0.08, 2, 24, axis='z'); b.cyl(Z(0, 1.9, 0), 1.6, 0.25, 4, 24, axis='z')
    o = b.build('Chamber'); empty('E_CHAMBER', None, Z(0, 0.1, 0))
    export(out)
print('done', kind)
