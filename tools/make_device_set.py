"""blender -b -P make_device_set.py -- <out_tower.glb> <out_agency.glb>
The Doomsday Device event's own set pieces (Blender units = metres, Z up, origins at the centre of the base):
 tower.glb  : Seg0..Seg5 (six telescoping sections of a dark blue column with gold bands, each 12 m, radius 3.0 .. 1.25 so they nest) and Cap (the red / pink pagoda roof with a gold finial). The plugin moves
              segment k up by k * 12 m * extension and the cap by 6 * 12 m * extension.
 agency.glb : Agency (the grey Agency complex: main block, drum tower with a dome, wings, portico, helipad, perimeter wall), Ruins + RuinsRubble (broken walls, slabs and rubble) and Crater (scorched ground).
              All four share one frame, so the plugin switches nodes on and off when the Agency blows up."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out_tower, out_agency = sys.argv[sys.argv.index('--') + 1:][:2]

def tex_tower(seed):
    W, H = 128, 256; U, V = grid(W, H); n = fbm(W, H, 4, seed); base = np.array((0.045, 0.075, 0.21))[None, None, :] * (0.8 + 0.5 * n[..., None])
    panel = np.exp(-(np.minimum((U * 12) % 1, 1 - (U * 12) % 1) / 0.03) ** 2) * 0.5; band = ((V < 0.045) | ((V > 0.935) & (V < 1.0))); ring = (V > 0.5) & (V < 0.515)
    col = base * (1 - 0.5 * panel[..., None]) + np.array((0.2, 0.35, 0.9))[None, None, :] * panel[..., None] * 0.35
    col = np.where(band[..., None], np.array((0.82, 0.64, 0.22)) * (0.8 + 0.3 * n[..., None]), col); col = np.where(ring[..., None], np.array((0.5, 0.65, 1.0)) * 0.8, col)
    return rgba(col[..., 0], col[..., 1], col[..., 2], 1.0)
def tex_char(seed):
    W = H = 128; n = fbm(W, H, 6, seed); k = 0.05 + 0.1 * n; hot = np.clip((fbm(W, H, 9, seed + 4) - 0.62) * 6, 0, 1); return rgba(k + hot * 0.9, k * 0.9 + hot * 0.3, k * 0.9, 1.0)

# ---------------------------------------------------------------- the tower
setup()
TW = material('TowerBlue', tex_tower(3)); PINK = material('Pagoda', tex_solid(0.82, 0.22, 0.30)); GOLD = material('Gold', tex_solid(0.85, 0.66, 0.22))
for k in range(6):
    b = MB([TW]); r = 3.0 - 0.35 * k; b.cyl((0, 0, 6), r, 12, 0, seg=24, tile=12.0); b.build('Seg%d' % k)
c = MB([PINK, GOLD])
c.cyl((0, 0, 0.4), 5.4, 0.8, 0, 24, r2=5.0); c.cyl((0, 0, 1.3), 3.2, 1.0, 1, 24); c.cyl((0, 0, 2.5), 6.2, 1.4, 0, 24, r2=3.0); c.cyl((0, 0, 3.6), 2.0, 1.0, 1, 24); c.cyl((0, 0, 4.8), 4.6, 1.4, 0, 24, r2=1.6); c.cyl((0, 0, 6.3), 0.9, 1.8, 1, 12, r2=0.3)
c.build('Cap'); export(out_tower)

# ---------------------------------------------------------------- the Agency
setup()
FAC = material('AgencyFacade', tex_facade((0.70, 0.72, 0.76), 31, bays=5, floors=2, lit=0.3, glass=(0.10, 0.17, 0.30))); FAC2 = material('AgencyFacade2', tex_facade((0.60, 0.63, 0.68), 32, bays=4, floors=2, lit=0.25, glass=(0.12, 0.2, 0.34)))
CONC = material('AgencyConcrete', tex_concrete(41, (0.58, 0.60, 0.63))); ROOF = material('AgencyRoof', tex_gravel(42)); WHITE = material('AgencyWhite', tex_solid(0.9, 0.9, 0.9)); DARK = material('AgencyDark', tex_solid(0.12, 0.13, 0.16)); DOME = material('AgencyDome', tex_solid(0.8, 0.85, 0.92))
a = MB([FAC, CONC, ROOF, DARK, WHITE, FAC2, DOME])
a.box((0, 0, 0.1), (62, 50, 0.2), 1, 6.0)                                                                       # courtyard slab
a.box((0, 0, 7.1), (30, 18, 14), 0, tile=6.0, tile_v=6.8, top=2); a.box((0, 0, 14.25), (31, 19, 0.5), 1, 4.0)       # main block + cornice
a.box((0, 0, 16.2), (18, 12, 3.4), 5, tile=6.0, tile_v=6.8, top=2); a.box((0, 0, 18.1), (19, 13, 0.5), 1, 4.0)       # set-back
a.cyl((-19, 0, 10.5), 7.0, 21, 5, seg=28, tile=7.0); a.cyl((-19, 0, 21.4), 7.5, 1.0, 1, 28); a.cyl((-19, 0, 23.4), 7.2, 3.0, 6, 28, r2=3.5); a.cyl((-19, 0, 25.9), 0.4, 3.0, 4, 8)        # drum tower, cornice, dome, aerial
a.box((22, -4, 4.7), (14, 12, 9), 5, tile=7.0, tile_v=4.5, top=2); a.box((22, -4, 9.2), (15, 13, 0.4), 1, 4.0)            # east wing
a.box((-8, -17, 3.2), (20, 8, 6.4), 5, tile=7.0, tile_v=3.2, top=2); a.box((-8, -17, 6.5), (21, 9, 0.4), 1, 4.0)          # south-west wing
a.box((0, -11.5, 3.2), (11, 4, 6.4), 1, 4.0, top=2)                                                             # portico
for i in range(4): a.cyl((-4.2 + i * 2.8, -14, 3.2), 0.45, 6.4, 4, 10)
a.box((0, -14.2, 6.8), (12, 2.6, 0.5), 1, 4.0)
for sx in (-1, 1): a.box((sx * 31, 0, 0.9), (0.8, 50, 1.8), 1, 4.0)                                                  # perimeter wall
for sy in (-1, 1): a.box((0, sy * 25, 0.9), (62, 0.8, 1.8), 1, 4.0)
a.cyl((16, 15, 0.3), 6.0, 0.4, 3, 28); a.box((16, 15, 0.55), (3.6, 0.6, 0.1), 4, 2.0); a.box((14.8, 15, 0.55), (0.6, 3.0, 0.1), 4, 2.0); a.box((17.2, 15, 0.55), (0.6, 3.0, 0.1), 4, 2.0)   # helipad H
rnd = np.random.default_rng(5)
for k in range(7): a.box((rnd.uniform(-10, 10), rnd.uniform(-4, 4), 18.6 + 0.7), (rnd.uniform(1.4, 2.8), rnd.uniform(1.2, 2.4), 1.4), 3, 2.0)                    # roof machinery
a.build('Agency')

CH = material('Charred', tex_char(51)); ROCK = material('RuinRock', tex_rock(52, (0.5, 0.5, 0.5)))
r = MB([CH, CONC, ROCK]); rnd = np.random.default_rng(7)
r.cyl((0, 0, 0.06), 29, 0.12, 0, seg=40, tile=8.0)                                                              # scorched ground
for k in range(22):                                                                                              # broken wall stumps and slabs
    ang = rnd.uniform(0, 6.283); rad = rnd.uniform(4, 25); sx, sy, sz = rnd.uniform(2.5, 9), rnd.uniform(1.2, 5), rnd.uniform(0.6, 5.5)
    r.box((math.cos(ang) * rad, math.sin(ang) * rad, sz / 2 + 0.1), (sx, sy, sz), 1 if k % 3 else 2, 4.0)
r.build('Ruins')
bm_all = []
for k in range(16):
    ang = rnd.uniform(0, 6.283); rad = rnd.uniform(2, 28); rr = rnd.uniform(1.2, 3.4)
    bm = displace_icosphere(rr, 2, 100 + k, 0.45, (1.0, rnd.uniform(0.7, 1.1), rnd.uniform(0.5, 0.8))); me = bpy.data.meshes.new('rub%d' % k); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    o = new_obj('Rub%d' % k, me, ROCK); o.location = (math.cos(ang) * rad, math.sin(ang) * rad, rr * 0.4); o.rotation_euler = (0, 0, rnd.uniform(0, 6.28)); bm_all.append(o)
# the rubble is one object so the plugin has a single node to switch
bpy.ops.object.select_all(action='DESELECT')
for o in bm_all: o.select_set(True)
bpy.context.view_layer.objects.active = bm_all[0]; bpy.ops.object.join(); bm_all[0].name = 'RuinsRubble'
export(out_agency)
