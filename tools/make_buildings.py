"""blender -b -P make_buildings.py -- <out.glb>
Tilted Towers / Retail Row buildings, named objects standing on z = 0 (origin at the centre of the base): Bld0..Bld5 (office towers with a shop-front ground floor, window grids with frames, set-backs,
parapets, roof machinery) and Shop0..Shop2 (low shops with awnings, signs and pitched roofs). The plugin clones them by name; sizes are metres (design units)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
PAL = [((0.78, 0.76, 0.72), 1), ((0.62, 0.46, 0.38), 2), ((0.55, 0.62, 0.70), 3), ((0.80, 0.78, 0.66), 4), ((0.50, 0.52, 0.56), 5), ((0.72, 0.70, 0.76), 6)]
FAC = [material('Facade%d' % i, tex_facade(w, s)) for i, (w, s) in enumerate(PAL)]
SHOP = [material('Shop%dTex' % i, tex_storefront(c, 20 + i)) for i, c in enumerate(((0.70, 0.62, 0.50), (0.55, 0.45, 0.45), (0.45, 0.55, 0.60)))]
CONC = material('Concrete', tex_concrete(11)); ROOF = material('Roof', tex_gravel(12)); STEEL = material('Steel', tex_concrete(13, (0.5, 0.52, 0.56))); WHITE = material('White', tex_solid(0.9, 0.9, 0.88)); DARK = material('Dark', tex_solid(0.14, 0.14, 0.16))
AWN = [material('Awn%d' % i, tex_solid(*c)) for i, c in enumerate(((0.75, 0.2, 0.18), (0.2, 0.45, 0.75), (0.9, 0.62, 0.15)))]
RF = [material('RoofCol%d' % i, tex_solid(*c)) for i, c in enumerate(((0.7, 0.2, 0.18), (0.22, 0.42, 0.7), (0.78, 0.55, 0.15)))]
objs = []
def tower(i, x, w, d, floors, fac, setback=None):
    b = MB([FAC[fac], CONC, ROOF, STEEL, SHOP[i % 3], DARK, WHITE]); fh = 3.4; h = floors * fh; g = 4.6
    b.box((0, 0, g / 2), (w + 0.3, d + 0.3, g), 4, tile=w / 2.0 + 0.001, tile_v=g)                       # shop-front ground floor (storefront texture spans a face)
    b.box((0, 0, g + 0.15), (w + 0.5, d + 0.5, 0.3), 1, 4.0)                                              # string course
    z0 = g + 0.3
    b.box((0, 0, z0 + h / 2), (w, d, h), 0, tile=7.0, tile_v=6.8, top=2)                                  # body: 4 bays x 2 floors per texture tile
    for sx in (-1, 1):                                                                                      # corner pilasters
        for sy in (-1, 1): b.box((sx * w / 2, sy * d / 2, z0 + h / 2), (0.6, 0.6, h), 1, 3.0)
    top = z0 + h
    if setback:
        sw, sd, sf = setback; b.box((0, 0, top + 0.2), (w + 0.4, d + 0.4, 0.4), 1, 4.0); sh = sf * fh; b.box((0, 0, top + 0.4 + sh / 2), (sw, sd, sh), 0, tile=7.0, tile_v=6.8, top=2); top += 0.4 + sh; w, d = sw, sd
    b.box((0, 0, top + 0.35), (w + 0.5, d + 0.5, 0.7), 1, 4.0)                                             # parapet band
    top += 0.7
    rnd = np.random.default_rng(i * 5 + 1)
    for k in range(3 + i % 3):                                                                              # roof machinery
        rx_, ry_ = (rnd.random() - 0.5) * (w - 3), (rnd.random() - 0.5) * (d - 3); sx_, sy_, sz_ = 1.2 + rnd.random() * 1.6, 1.0 + rnd.random() * 1.4, 0.8 + rnd.random() * 1.0
        b.box((rx_, ry_, top + sz_ / 2), (sx_, sy_, sz_), 3, 2.0)
    b.cyl((w * 0.28, -d * 0.2, top + 1.6), 1.1, 3.2, 3, 12); b.cyl((w * 0.28, -d * 0.2, top + 3.4), 1.25, 0.4, 5, 12)    # water tank
    if i % 2 == 0: b.cyl((-w * 0.3, d * 0.25, top + 5.0), 0.08, 10.0, 3, 6); b.box((-w * 0.3, d * 0.25, top + 9.6), (0.5, 0.5, 0.5), 6, 1.0)   # antenna + lamp
    o = b.build('Bld%d' % i); o.location = (x, 0, 0); objs.append(o)
tower(0, 0, 16, 15, 5, 0)
tower(1, 30, 14, 14, 8, 1, (9, 9, 3))
tower(2, 60, 17, 16, 10, 2, (11, 10, 4))
tower(3, 90, 15, 17, 6, 3)
tower(4, 120, 18, 15, 13, 4, (12, 10, 5))
tower(5, 150, 14, 16, 9, 5, (9, 9, 2))

def shop(i, x):
    b = MB([SHOP[i % 3], CONC, AWN[i % 3], RF[i % 3], WHITE, DARK, ROOF]); w, d, h = 14, 11, 6.2
    b.box((0, 0, 0.3), (w + 0.3, d + 0.3, 0.6), 1, 4.0)
    b.box((0, 0, 0.6 + (h - 0.6) / 2), (w, d, h - 0.6), 0, tile=w / 2.0 + 0.001, tile_v=h - 0.6)                  # storefront walls
    b.box((0, -d / 2 - 1.1, 3.6), (w - 1.0, 2.4, 0.18), 2, 2.0)                                               # awning
    for k in range(5): b.box((-w / 2 + 1.4 + k * 2.8, -d / 2 - 2.2, 2.4), (0.08, 0.08, 2.4), 5, 1.0)
    b.box((0, -d / 2 - 0.15, h + 0.5), (w * 0.6, 0.3, 1.0), 4, 2.0); b.box((0, -d / 2 - 0.32, h + 0.5), (w * 0.5, 0.05, 0.6), 5, 2.0)    # sign board
    for k in range(4):                                                                                      # pyramid roof (stepped to stay a box mesh)
        s = 1.0 - k * 0.22; b.box((0, 0, h + 0.4 * k + 0.2), (w * s + 0.6, d * s + 0.6, 0.4), 3, 4.0)
    b.box((w * 0.25, d * 0.15, h + 2.1), (1.2, 1.2, 1.6), 1, 2.0)
    o = b.build('Shop%d' % i); o.location = (x, 40, 0); objs.append(o)
for i in range(3): shop(i, 30 * i)
export(out)
