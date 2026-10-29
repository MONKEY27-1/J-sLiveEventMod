"""blender -b -P make_agency.py -- <out.glb>
The Agency as in the game (Chapter 2): a sandy stone fortress - a stepped central tower with groups of tall slit windows, two big round bastions with crenellated tops and round medallions at the front corners, a round
side tower, terraces with gold railings and stairs, a paved plaza with a golden monument, benches, round planters with topiary, silver orbs on pedestals, pines and trees, flagpoles with black flags.
Blender: metres, Z up, origin at the centre of the base; the front / plaza faces +Y (= Unity -z, the side the player starts on). Nodes (one shared frame):
 Agency (stone buildings, terraces, retaining walls, stairs: the plugin gives it a mesh collider) | AgencyDeco (railings, flags, benches, planters, monument, orbs, trees, lamp posts) |
 Ruins (scorched ground, tilted stone slabs, wall stumps, rebar, burnt cars, the bastion stumps) | RuinsRubble (rock piles). When the Agency explodes the first two are hidden and the last two shown."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
STONE = (0.74, 0.69, 0.58)
def tex_stone(seed, col=STONE, slits=False):                                                         # 4 x 4 m: 8 courses of blocks, staggered joints; optional vertical slit windows (two per tile)
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 8, seed); row = np.floor(V * 8); off = (row % 2) * 0.5; ju = ((U * 4 + off) % 1) < 0.03; jv = ((V * 8) % 1) < 0.06
    shade = 0.84 + 0.22 * fbm(W, H, 16, seed + 3) + 0.08 * ((row * 7 + np.floor(U * 4 + off)) % 5) / 5.0; c = np.array(col)[None, None, :] * shade[..., None] * (0.95 + 0.1 * n[..., None]); c = np.where((ju | jv)[..., None], c * 0.62, c)
    if slits:
        for cx in (0.25, 0.75):
            s = (np.abs(U - cx) < 0.07) & (V > 0.14) & (V < 0.86); fr = (np.abs(U - cx) < 0.1) & (V > 0.11) & (V < 0.89) & ~s; g = np.stack([0.08 + 0.1 * V, 0.13 + 0.12 * V, 0.2 + 0.2 * V], -1)
            c = np.where(fr[..., None], np.array((0.85, 0.82, 0.72)), c); c = np.where(s[..., None], g, c)
    return rgba(c[..., 0], c[..., 1], c[..., 2], 1.0)
def tex_pave(seed):
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 12, seed); line = ((U * 8) % 1 < 0.04) | ((V * 8) % 1 < 0.04); c = 0.72 + 0.1 * n; c = np.where(line, 0.5, c); return rgba(c, c * 0.97, c * 0.9, 1.0)
def tex_char(seed): W = H = 128; n = fbm(W, H, 6, seed); k = 0.05 + 0.1 * n; hot = np.clip((fbm(W, H, 9, seed + 4) - 0.62) * 6, 0, 1); return rgba(k + hot * 0.9, k * 0.9 + hot * 0.3, k * 0.9, 1.0)
M = dict(st=material('Stone', tex_stone(1)), slit=material('StoneSlits', tex_stone(2, slits=True)), trim=material('StoneTrim', tex_stone(3, (0.86, 0.83, 0.74))), dark=material('StoneDark', tex_stone(4, (0.5, 0.48, 0.44))), roof=material('Roof', tex_gravel(5)), pave=material('Pavers', tex_pave(6)),
         gold=material('Gold', tex_solid(0.9, 0.7, 0.2)), blue=material('Bench', tex_solid(0.2, 0.5, 0.85)), green=material('Topiary', tex_solid(0.2, 0.5, 0.2)), pine=material('Pine', tex_solid(0.12, 0.35, 0.2)), leaf=material('Leaf', tex_solid(0.25, 0.55, 0.22)), bark=material('Bark', tex_solid(0.3, 0.2, 0.12)),
         silver=material('Silver', tex_solid(0.8, 0.82, 0.86)), black=material('Black', tex_solid(0.05, 0.05, 0.07)), metal=material('Metal', tex_concrete(7, (0.45, 0.47, 0.5))), car=material('Car', tex_solid(0.06, 0.06, 0.08)), flagc=material('Flag', tex_solid(0.04, 0.04, 0.06)),
         emb=material('Emblem', tex_solid(0.62, 0.56, 0.42)), char=material('Charred', tex_char(51)), rock=material('RuinRock', tex_rock(52, (0.55, 0.52, 0.48))), rust=material('Rust', tex_solid(0.35, 0.2, 0.12)), grass=material('Grass', tex_solid(0.35, 0.6, 0.25)))
L = ['st', 'slit', 'trim', 'dark', 'roof', 'pave', 'emb', 'grass']; a = MB([M[k] for k in L]); I = {k: i for i, k in enumerate(L)}
def parapet(b, cx, cy, w, d, z, h=0.9, mat='trim'):                                                                                    # a low wall round a roof edge
    for sy in (-1, 1): b.box((cx, cy + sy * (d / 2 - 0.2), z + h / 2), (w, 0.4, h), I[mat], 2.0)
    for sx in (-1, 1): b.box((cx + sx * (w / 2 - 0.2), cy, z + h / 2), (0.4, d - 0.8, h), I[mat], 2.0)
def crenels(b, cx, cy, r, z, n=18):
    for k in range(n): ang = k * 2 * math.pi / n; b.box((cx + math.cos(ang) * (r - 0.3), cy + math.sin(ang) * (r - 0.3), z + 0.5), (1.1, 1.1, 1.0), I['trim'], 2.0)
# ---- platform + retaining walls + stairs
a.box((0, 0, 0.8), (68, 62, 1.6), I['st'], 4.0, top=I['pave'])                                                                       # the terrace the Agency stands on
a.box((0, 31.2, 0.9), (68.6, 0.8, 1.9), I['dark'], 4.0); a.box((0, -31.2, 0.9), (68.6, 0.8, 1.9), I['dark'], 4.0)
for sx in (-1, 1): a.box((sx * 34.2, 0, 0.9), (0.8, 62.6, 1.9), I['dark'], 4.0)
for k in range(6): a.box((0, 33 + k * 0.9, 1.4 - k * 0.24), (16 - 0.4 * k, 0.9, 0.3 + 0.0), I['trim'], 3.0, top=I['pave'])                       # grand steps down to the pier (front, +y)
a.box((0, 40, 0.4), (22, 14, 0.8), I['pave'], 4.0)                                                                                    # pier
# ---- central stepped tower
a.box((0, 0, 7.6), (34, 22, 12.0), I['slit'], tile=4.0, tile_v=4.0, top=I['roof']); a.box((0, 0, 13.7), (35, 23, 0.5), I['trim'], 4.0); parapet(a, 0, 0, 35, 23, 13.9)
a.box((0, 0, 17.8), (26, 17, 8.0), I['slit'], tile=4.0, tile_v=4.0, top=I['roof']); a.box((0, 0, 22.0), (27, 18, 0.5), I['trim'], 4.0); parapet(a, 0, 0, 27, 18, 22.2)
a.box((0, 0, 26.6), (17, 12, 9.0), I['slit'], tile=4.0, tile_v=4.0, top=I['roof']); a.box((0, 0, 31.4), (18, 13, 0.6), I['trim'], 4.0); parapet(a, 0, 0, 18, 13, 31.7)
a.box((0, 0, 34.5), (7, 5, 5.5), I['trim'], 3.0, top=I['roof']); a.cyl((0, 0, 38.0), 2.2, 1.5, I['st'], 16, r2=1.4)
for k in range(-3, 4): a.box((k * 4.6, 11.4, 7.6), (1.3, 0.9, 12.0), I['trim'], 3.0); a.box((k * 3.6, 8.9, 17.8), (1.1, 0.8, 8.0), I['trim'], 3.0)       # pilasters on the front faces
for k in range(-2, 3): a.box((k * 3.4, 6.4, 26.6), (1.0, 0.8, 9.0), I['trim'], 3.0)
a.box((0, 11.6, 3.0), (7, 1.6, 6.0), I['trim'], 3.0); a.box((0, 12.5, 6.2), (8, 2.4, 0.5), I['trim'], 3.0)                                 # the main entrance and its canopy
# ---- two round bastions at the front corners, a round side tower, wings
for sx in (-1, 1):
    x = sx * 20.0; y_ = 18.0
    a.cyl((x, y_, 7.4), 10.0, 11.6, I['st'], seg=36, tile=4.0); a.cyl((x, y_, 13.4), 10.6, 0.8, I['trim'], 36); a.cyl((x, y_, 14.2), 9.2, 0.6, I['roof'], 36); crenels(a, x, y_, 10.4, 14.5)
    a.cyl((x, y_ + 9.95, 7.4), 2.3, 0.5, I['trim'], 24, axis='y'); a.cyl((x, y_ + 10.2, 7.4), 1.6, 0.3, I['emb'], 24, axis='y')                  # the round medallion on the front of each bastion
    for k in range(4): a.cyl((x, y_, 2.0 + k * 3.0), 10.15, 0.5, I['trim'], 36)                                                          # stone string courses
a.cyl((26, -14, 11.0), 7.0, 20.0, I['st'], 30, tile=4.0); a.cyl((26, -14, 21.4), 7.6, 0.8, I['trim'], 30); a.cyl((26, -14, 22.4), 6.2, 0.6, I['roof'], 30); crenels(a, 26, -14, 7.4, 22.7, 14)
a.box((-24, -12, 6.0), (20, 16, 9.0), I['slit'], tile=4.0, tile_v=4.0, top=I['roof']); a.box((-24, -12, 10.7), (21, 17, 0.5), I['trim'], 4.0); parapet(a, -24, -12, 21, 17, 10.9)
a.box((-8, 24, 3.4), (16, 10, 3.6), I['st'], 4.0, top=I['roof']); a.box((8, 24, 3.4), (16, 10, 3.6), I['st'], 4.0, top=I['roof'])                      # low terraces between the bastions
a.build('Agency')

# ---- decoration: railings, benches, planters, monument, orbs, trees, flags, lamps
dl = ['gold', 'blue', 'green', 'pine', 'leaf', 'bark', 'silver', 'black', 'trim', 'metal', 'flagc', 'pave', 'st']
d = MB([M[k] for k in dl]); J = {k: i for i, k in enumerate(dl)}
def rail(b, x0, y0, x1, y1, z, n=None):
    L_ = math.hypot(x1 - x0, y1 - y0); n = n or max(2, int(L_ / 2.0))
    for k in range(n + 1): t = k / n; b.box((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, z + 0.45), (0.1, 0.1, 0.9), J['gold'], 1.0)
    b.box(((x0 + x1) / 2, (y0 + y1) / 2, z + 0.9), (max(0.08, abs(x1 - x0)), max(0.08, abs(y1 - y0)), 0.08), J['gold'], 1.0)
rail(d, -33, 30.5, -9, 30.5, 1.6); rail(d, 9, 30.5, 33, 30.5, 1.6); rail(d, -33.5, -30, -33.5, 30, 1.6); rail(d, 33.5, -30, 33.5, 30, 1.6)
for sx in (-1, 1): rail(d, sx * 8.2, 33, sx * 8.2, 38.5, 0.4)
for x in (-24, 24):
    for y_ in (24, ):
        d.box((x, y_, 4.4), (14, 0.1, 0.9), J['gold'], 1.0)
for (x, y_) in ((-12, 27), (12, 27), (-26, 5), (26, 5), (-12, -2), (0, 33.5), (-5, 28), (5, 28)): d.box((x, y_, 2.1), (2.2, 0.6, 0.5), J['blue'], 1.0); d.box((x, y_ - 0.2, 2.5), (2.2, 0.12, 0.5), J['blue'], 1.0); d.box((x - 0.9, y_, 1.85), (0.12, 0.5, 0.5), J['metal'], 1.0); d.box((x + 0.9, y_, 1.85), (0.12, 0.5, 0.5), J['metal'], 1.0)     # benches
d.cyl((0, 28, 1.75), 8.0, 0.3, J['trim'], 40); d.cyl((0, 28, 2.1), 6.6, 0.25, J['pave'], 40); d.cyl((0, 28, 2.5), 1.6, 1.0, J['trim'], 20); d.cyl((0, 28, 5.4), 0.9, 5.4, J['gold'], 16, r2=0.15); d.cyl((0, 28, 8.4), 0.6, 1.0, J['gold'], 12, r2=0.0)      # the plaza and the golden monument
trees = []
for k, (x, y_) in enumerate(((-9, 24), (9, 24), (-27, 24), (27, 24), (0, 22.5), (-14, 8), (14, 8), (-30, 12))):
    pm = MB([M['green'], M['trim']]); pm.cyl((0, 0, 1.9), 1.5, 0.7, 1, 16); o = pm.build('Planter%d' % k); o.location = (x, y_, 0); trees.append(o)
    bm = displace_icosphere(1.1, 2, 300 + k, 0.08); me = bpy.data.meshes.new('top'); bm.to_mesh(me); bm.free(); o = new_obj('Topiary%d' % k, me, M['green']); o.location = (x, y_, 3.4); trees.append(o)
for k, (x, y_) in enumerate(((-6, 33), (6, 33))):
    pm = MB([M['trim']]); pm.cyl((0, 0, 0.7), 0.7, 1.4, 0, 14, r2=0.5); o = pm.build('Pedestal%d' % k); o.location = (x, y_ - 0.5, 0.6); trees.append(o)
    bm = displace_icosphere(0.9, 3, 400 + k, 0.01); me = bpy.data.meshes.new('orb'); bm.to_mesh(me); bm.free(); o = new_obj('Orb%d' % k, me, M['silver']); o.location = (x, y_ - 0.5, 2.6); trees.append(o)
rr = np.random.default_rng(11)
for k in range(22):                                                                                                                     # pines along the edges
    x = rr.uniform(-33, 33); y_ = rr.choice([-29.5, -25, 29, 24]) + rr.uniform(-1, 1); h = rr.uniform(6, 11)
    if abs(x) < 10 and y_ > 20: continue
    pm = MB([M['pine'], M['bark']]); pm.cyl((0, 0, 0.8), 0.3, 1.6, 1, 6); pm.cyl((0, 0, 2.0 + h * 0.3), 2.3, h * 0.6, 0, 10, r2=0.1); pm.cyl((0, 0, 2.0 + h * 0.62), 1.6, h * 0.5, 0, 10, r2=0.05); o = pm.build('Pine%d' % k); o.location = (x, y_, 1.6); trees.append(o)
for k, (x, y_) in enumerate(((-31, 20), (31, 20), (-31, -18), (31, -18))):
    tm = MB([M['bark']]); tm.cyl((0, 0, 2.5), 0.45, 5.0, 0, 8); o = tm.build('Trunk%d' % k); o.location = (x, y_, 1.6); trees.append(o)
    for q in range(3): bm = displace_icosphere(3.0 - 0.4 * q, 2, 500 + k * 3 + q, 0.25, (1, 1, 0.8)); me = bpy.data.meshes.new('crown'); bm.to_mesh(me); bm.free(); o = new_obj('Crown%d_%d' % (k, q), me, M['leaf']); o.location = (x + (q - 1) * 1.2, y_, 7.0 + q * 1.4); trees.append(o)
for k, (x, y_, z) in enumerate(((-14, 6, 31.5), (14, 6, 31.5), (-12, 8, 22.2), (12, 8, 22.2), (-20, 18, 15.2), (20, 18, 15.2))):         # flagpoles with black flags
    fm = MB([M['metal'], M['flagc']]); fm.cyl((0, 0, 4.5), 0.12, 9.0, 0, 8); fm.box((1.4, 0, 7.8), (2.6, 0.05, 1.6), 1, 1.0); o = fm.build('Flag%d' % k); o.location = (x, y_, z); trees.append(o)
for (x, y_) in ((-28, 28), (28, 28), (-28, -26), (28, -26), (-10, 35), (10, 35)):
    lm = MB([M['metal'], M['black']]); lm.cyl((0, 0, 3.0), 0.14, 6.0, 0, 8); lm.box((0, 0, 6.1), (1.2, 0.5, 0.25), 1, 1.0); o = lm.build('Lamp'); o.location = (x, y_, 1.6 if abs(y_) < 33 else 0.4); trees.append(o)
base = trees[0]; bpy.ops.object.select_all(action='DESELECT')
d_obj = d.build('AgencyDeco'); d_obj.select_set(True)
for o in trees: o.select_set(True)
bpy.context.view_layer.objects.active = d_obj; bpy.ops.object.join(); d_obj.name = 'AgencyDeco'

# ---- ruins
RL = ['char', 'st', 'rock', 'rust', 'dark', 'trim']; R = MB([M[k] for k in RL]); RI = {k: i for i, k in enumerate(RL)}; rnd = np.random.default_rng(7)
R.cyl((0, 0, 1.62), 33, 0.1, RI['char'], seg=44, tile=8.0)
for sx in (-1, 1): R.cyl((sx * 20, 18, 3.0), 10.0, 3.0, RI['st'], 20, r2=9.3); R.cyl((sx * 20, 18, 4.8), 7.0, 0.7, RI['char'], 14)                      # bastion stumps
R.cyl((26, -14, 4.0), 7.0, 4.5, RI['st'], 18, r2=6.2); R.box((0, 0, 4.2), (22, 14, 5.0), RI['st'], 4.0); R.box((0, 0, 7.2), (12, 8, 2.0), RI['char'], 4.0)                                    # what is left of the tower's base
for k in range(16):
    ang = rnd.uniform(0, 6.283); rad = rnd.uniform(5, 27); sx, sy, sz = rnd.uniform(2.5, 8), rnd.uniform(1.2, 4.0), rnd.uniform(0.6, 4.0); R.box((math.cos(ang) * rad, math.sin(ang) * rad, 1.6 + sz / 2), (sx, sy, sz), RI['st'] if k % 3 else RI['trim'], 4.0)
rb = R.build('RuinsBase'); parts = [rb]
for k in range(22):
    sm = MB([M['st'], M['char']]); sm.box((0, 0, 0), (rnd.uniform(3, 9), rnd.uniform(2, 6), rnd.uniform(0.4, 0.9)), k % 2, 3.0); o = sm.build('Slab%d' % k); ang = rnd.uniform(0, 6.283); rad = rnd.uniform(3, 28)
    o.location = (math.cos(ang) * rad, math.sin(ang) * rad, 1.6 + rnd.uniform(0.6, 2.6)); o.rotation_euler = (rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(0, 6.28)); parts.append(o)
for k in range(40):
    sm = MB([M['rust']]); sm.cyl((0, 0, 1.5), 0.05, 3.0, 0, 5); o = sm.build('Rebar%d' % k); ang = rnd.uniform(0, 6.283); rad = rnd.uniform(3, 28); o.location = (math.cos(ang) * rad, math.sin(ang) * rad, 2.0); o.rotation_euler = (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), 0); parts.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = rb; bpy.ops.object.join(); rb.name = 'Ruins'
bm_all = []
for k in range(30):
    ang = rnd.uniform(0, 6.283); rad = rnd.uniform(2, 30); rr_ = rnd.uniform(1.0, 3.6)
    bm = displace_icosphere(rr_, 2, 100 + k, 0.45, (1.0, rnd.uniform(0.7, 1.1), rnd.uniform(0.5, 0.8))); me = bpy.data.meshes.new('rub%d' % k); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    o = new_obj('Rub%d' % k, me, M['rock']); o.location = (math.cos(ang) * rad, math.sin(ang) * rad, 1.6 + rr_ * 0.4); o.rotation_euler = (0, 0, rnd.uniform(0, 6.28)); bm_all.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in bm_all: o.select_set(True)
bpy.context.view_layer.objects.active = bm_all[0]; bpy.ops.object.join(); bm_all[0].name = 'RuinsRubble'
export(out)
