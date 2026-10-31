"""blender -b -P end21_world.py -- <hall|gyro|base|hatch|dome> <out.glb>
THE END: the rest of the I.O. facility.  Unity local axes: x right, y up, z forward (Blender here: (x, -z, y)).
hall:  the facility hall (5:32 - 5:56): a green-lit server hall (z 0 - 16, 8 m wide, racks both sides) into a dark red hall (z 16 - 44, 4 m wide) with an alcove at z 24 and the blast door to the gyro chamber at z 44.  Path empties WP00..
gyro:  the Zero Point machinery chamber (5:56 - 6:40): a purple crystal cave 30 m across with a raised central platform, the control desk (screens, keypad, red button), a big blue-white energy arc behind it, two side doors (+-x).
base:  the underground I.O. base (7:12 - 9:13): the shaft (14 m), then a staircase of corridors and rooms (blue corridor, red/pink device room, red hall with displays, window room, big blue machinery hall, blue corridor, red/orange passage,
       red control room, hazy corridor) ending in the huge purple Zero Point chamber (round, 76 m, with the giant ring machine, glass dome = object 'Glass').  Empties WP00.. = the centre line (shaft top first), ZPC, RING, CH0 (chamber entry), CHC (chamber centre).
hatch: the island's stair hatch (6:49 - 7:09): a 6 x 6 m steel slab with a stair shaft; the sliding cover is the separate object 'Cover'."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 8)
def panel(base, seed, W=256, H=256, lines=6, stripe=None, wear=0.3):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); col = np.array(base)[None, None, :] * (0.78 + wear * n[..., None]); gx = np.abs(((U * lines) % 1) - 0.5); gy = np.abs(((V * lines) % 1) - 0.5)
    col = np.where(((gx > 0.485) | (gy > 0.485))[..., None], col * 0.5, col)
    if stripe is not None: col = np.where(((((U + V) * 14) % 1 < 0.5) & (V > 0.88))[..., None], np.array(stripe)[None, None, :], col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def screen_tex(c1, c2, seed, W=128, H=64):
    r = np.random.default_rng(seed); img = np.zeros((H, W, 4), np.float32); img[..., :3] = 0.03; img[..., 3] = 1
    for k in range(7):
        y0 = 4 + k * 8; L = int(W * (0.3 + 0.65 * r.random())); img[y0:y0 + 4, 4:4 + L, :3] = c1 if k % 2 == 0 else c2
    return img
def swirl(a, b_, c, seed, alpha=1.0, W=128):
    U, V = grid(W, W); T = np.pi * 2; n = np.sin(T * (U * 3 + 0.25 * np.sin(T * (V * 2 + seed)))) + np.sin(T * (V * 4 + 0.3 * np.sin(T * U * 3 + seed))) + np.sin(T * ((U + V) * 2)); n = n / 3 * 0.5 + 0.5
    a, b_, c = (np.array(x)[None, None, :] for x in (a, b_, c)); lo = n[..., None] < 0.5; col = np.where(lo, a + (b_ - a) * (n[..., None] * 2), b_ + (c - b_) * ((n[..., None] - 0.5) * 2)); return rgba(col[..., 0], col[..., 1], col[..., 2], alpha * (0.4 + 0.6 * n))
def Bz(x, y, z): return (x, -z, y)
FLOOR = material('Floor', panel((0.14, 0.15, 0.19), 1, lines=10)); WBLUE = material('WBlue', panel((0.12, 0.22, 0.36), 2, lines=5)); WRED = material('WRed', panel((0.3, 0.1, 0.1), 3, lines=5, stripe=(1.0, 0.5, 0.1)))
WPUR = material('WPurple', panel((0.2, 0.12, 0.34), 4, lines=4)); WGRN = material('WGreen', panel((0.07, 0.22, 0.14), 5, lines=6)); CEIL = material('Ceil', panel((0.1, 0.11, 0.15), 6, lines=8))
STEEL = material('Steel', panel((0.5, 0.53, 0.6), 7, lines=2)); DARK = material('Dark', panel((0.07, 0.08, 0.11), 8, lines=8))
CYAN = solid('Cyan', 0.3, 0.95, 1.0); REDL = solid('RedL', 1.0, 0.2, 0.15); AMB = solid('Amber', 1.0, 0.65, 0.15); PINK = solid('Pink', 1.0, 0.3, 0.7); PURP = solid('Purp', 0.72, 0.35, 1.0); WHITE = solid('White', 0.95, 0.97, 1.0); GREENL = solid('GreenL', 0.3, 1.0, 0.5)
SG = material('ScrG', screen_tex(np.array([0.3, 1, 0.5]), np.array([0.2, 0.8, 1.0]), 1)); SB = material('ScrB', screen_tex(np.array([0.3, 0.8, 1.0]), np.array([0.6, 0.5, 1.0]), 2)); SO = material('ScrO', screen_tex(np.array([1.0, 0.6, 0.15]), np.array([1.0, 0.85, 0.3]), 3)); SR = material('ScrR', screen_tex(np.array([1.0, 0.3, 0.3]), np.array([1.0, 0.6, 0.8]), 4))
CRY = solid('Crystal', 0.55, 0.28, 1.0); CRY2 = solid('Crystal2', 0.35, 0.5, 1.0)
M = [FLOOR, WBLUE, WRED, WPUR, WGRN, CEIL, STEEL, DARK, CYAN, REDL, AMB, PINK, PURP, WHITE, GREENL, SG, SB, SO, SR, CRY, CRY2]
(MF, MWB, MWR, MWP, MWG, MC, MS, MD, LC, LR, LA, LP, LPU, LW, LG, XG, XB, XO, XR, MCR, MCR2) = range(21)
b = MB(M)
def bx(x, y, z, w, h, d, m, tile=3.0, top=None): b.box(Bz(x, y, z), (w, d, h), m, tile, top=top)          # unity: centre x, y, z; size width (x), height (y), depth (z)
def cylv(x, y, z, r, h, m, seg=12, r2=None): b.cyl(Bz(x, y, z), r, h, m, seg, r2=r2)                         # a vertical cylinder centred at y
def walls_x(z, x0, x1, y0, h, m, gaps=(), th=0.3, oh=3.0):      # a wall along x at depth z from x0 to x1 with gaps [(c, w)] (doorways of height oh)
    cuts = sorted([(c - w / 2, c + w / 2) for c, w in gaps]); cur = x0
    for a0, a1 in cuts:
        if a0 > cur: bx((cur + a0) / 2, y0 + h / 2, z, a0 - cur, h, th, m)
        if h > oh: bx((a0 + a1) / 2, y0 + (oh + h) / 2, z, a1 - a0, h - oh, th, m)
        cur = a1
    if cur < x1: bx((cur + x1) / 2, y0 + h / 2, z, x1 - cur, h, th, m)
def walls_z(x, z0, z1, y0, h, m, gaps=(), th=0.3, oh=3.0):
    cuts = sorted([(c - w / 2, c + w / 2) for c, w in gaps]); cur = z0
    for a0, a1 in cuts:
        if a0 > cur: bx(x, y0 + h / 2, (cur + a0) / 2, th, h, a0 - cur, m)
        if h > oh: bx(x, y0 + (oh + h) / 2, (a0 + a1) / 2, th, h - oh, a1 - a0, m)
        cur = a1
    if cur < z1: bx(x, y0 + h / 2, (cur + z1) / 2, th, h, z1 - cur, m)
NOCEIL = bool(os.environ.get('NOCEIL'))
def room(cx, cz, w, d, h, wm, openings=(), y0=0.0, strip=LC, floor=MF, ceil=MC, strips=True):
    """a closed box room: centre (cx, cz), footprint w (x) by d (z), floor top at y0.  openings: (side, offset along the wall, width, height) with side in xm xp zm zp"""
    bx(cx, y0 - 0.15, cz, w + 0.6, 0.3, d + 0.6, floor)
    if not NOCEIL: bx(cx, y0 + h + 0.15, cz, w + 0.6, 0.3, d + 0.6, ceil)
    g = {'xm': [], 'xp': [], 'zm': [], 'zp': []}
    for s, off, ow, oh in openings: g[s].append((off, ow, oh))
    for s in g:
        oh = max([o[2] for o in g[s]] or [3.0]); gp = [(o[0], o[1]) for o in g[s]]
        if s == 'zp': walls_x(cz + d / 2, cx - w / 2, cx + w / 2, y0, h, wm, [(cx + c, ww) for c, ww in gp], oh=oh)
        if s == 'zm': walls_x(cz - d / 2, cx - w / 2, cx + w / 2, y0, h, wm, [(cx + c, ww) for c, ww in gp], oh=oh)
        if s == 'xp': walls_z(cx + w / 2, cz - d / 2, cz + d / 2, y0, h, wm, [(cz + c, ww) for c, ww in gp], oh=oh)
        if s == 'xm': walls_z(cx - w / 2, cz - d / 2, cz + d / 2, y0, h, wm, [(cz + c, ww) for c, ww in gp], oh=oh)
    if strips and not NOCEIL:      # ceiling light strips along the long sides
        if w >= d: bx(cx, y0 + h - 0.1, cz - d / 2 + 0.5, w - 1, 0.12, 0.25, strip, 1.0); bx(cx, y0 + h - 0.1, cz + d / 2 - 0.5, w - 1, 0.12, 0.25, strip, 1.0)
        else: bx(cx - w / 2 + 0.5, y0 + h - 0.1, cz, 0.25, 0.12, d - 1, strip, 1.0); bx(cx + w / 2 - 0.5, y0 + h - 0.1, cz, 0.25, 0.12, d - 1, strip, 1.0)
WPS = []
def wp(x, y, z): WPS.append((x, y, z))
def ring_obj(name, radius, tube, mat, loc, axis='z', seg=48, tseg=8, rot=None):
    """a torus object standing in the plane perpendicular to unity axis `axis` (z: faces the viewer looking down +z)"""
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=tube, major_segments=seg, minor_segments=tseg); o = bpy.context.active_object; o.name = name; o.data.materials.append(M[mat]); uvr = o.data.uv_layers.new()
    for p in o.data.polygons:
        for li in p.loop_indices: uvr.data[li].uv = (0.5, 0.5)
    o.location = Bz(*loc)
    if axis == 'z': o.rotation_euler = (math.pi / 2, 0, 0)          # blender torus axis is Z; stand it up so it faces unity +z (= blender -y)
    elif axis == 'x': o.rotation_euler = (0, math.pi / 2, 0)
    return o
if kind == 'hall':
    # ---- the green server hall
    room(0, 8, 8, 16, 4.0, MWG, [('zm', 0, 3.0, 3.0), ('zp', 0, 4.0, 3.0)], strip=LG)
    for side in (-1, 1):
        for k in range(6):
            z = 2.0 + k * 2.4
            bx(side * 3.0, 1.25, z, 1.3, 2.5, 1.7, MD, 1.0); bx(side * 2.3, 1.7, z, 0.06, 1.8, 1.3, XG, 1.0)                    # a server rack with its green screen
            bx(side * 3.0, 2.6, z, 1.5, 0.12, 1.9, LG, 1.0)
    for k in range(5): bx(0, 3.3, 2 + k * 3.2, 7, 0.1, 0.2, MS)                                                                  # cable trays across the ceiling
    # ---- the red hall (4 m wide) with doorways, screens and an alcove
    room(0, 30, 4, 28, 3.6, MWR, [('zm', 0, 4.0, 3.0), ('zp', 0, 4.0, 3.0), ('xp', -6, 3.0, 3.0)], strip=LR)
    for k in range(7):
        z = 18 + k * 3.8; bx(-1.93, 1.9, z, 0.08, 1.0, 1.6, XR if k % 2 == 0 else XO, 1.0)                                         # wall displays
        bx(1.93, 1.0, z + 1.0, 0.2, 2.0, 0.25, MS, 1.0)                                                                           # door pillars / pipes
    room(5.5, 24, 7, 5, 3.4, MWR, [('xm', 0, 3.0, 3.0)], strip=LA)                                                                # the alcove to peek out of (x +2 .. +9, z 21.5 .. 26.5)
    bx(5.5, 0.5, 24.8, 1.6, 1.0, 1.0, MD); bx(5.5, 1.1, 25.2, 1.2, 0.5, 0.1, XO, 1.0)
    # ---- the guards' lit room at the far end (z 44 - 52) and the sealed door to the gyro chamber
    room(0, 48, 8, 8, 3.8, MWB, [('zm', 0, 4.0, 3.0)], strip=LW)
    bx(0, 1.9, 51.9, 7, 3.2, 0.15, MD); bx(2.5, 1.5, 51.7, 0.7, 0.9, 0.12, XO, 1.0); bx(-2.5, 1.5, 51.7, 0.7, 0.9, 0.12, XB, 1.0); bx(0, 3.0, 51.6, 3.0, 0.15, 0.1, LR, 1.0)
    for k, z in enumerate((15.0, 28.0, 40.0)): bx(0, 1.7, z, 4, 0.4, 0.3, MD); bx(0, 1.0, z, 0.3, 2.0, 0.3, MS)                                         # door frames across the hall
    for z in (10, 22, 34, 44.5): wp(0, 0, z)
    b.build('Hall')
    for i, (x, y, z) in enumerate(WPS): empty('WP%02d' % i, None, Bz(x, y, z))
    export(out)
elif kind == 'gyro':
    R = 15.0
    b.cyl(Bz(0, -0.15, 0), R + 0.5, 0.3, MF, 40); b.cyl(Bz(0, 16.15, 0), R + 0.5, 0.3, MC, 40)
    for k in range(40):                                                                                                         # the round wall of dark purple panels with two doorways (+-x)
        a = (k + 0.5) * 2 * math.pi / 40; x, z = math.cos(a) * R, math.sin(a) * R
        if abs(math.degrees(a) - 0) < 11 or abs(math.degrees(a) - 180) < 11: continue
        b.box(Bz(x, 8.0, z), (2.4, 0.5, 16.0), MWP, 2.0)
    for sgn in (-1, 1):
        bx(sgn * (R - 0.2), 2.0, 5.0, 0.4, 4.0, 0.5, LW, 1.0); bx(sgn * (R - 0.2), 2.0, -5.0, 0.4, 4.0, 0.5, LW, 1.0); bx(sgn * (R - 0.2), 4.2, 0, 0.5, 0.4, 10.5, LW, 1.0)             # door frames, lit white-blue
    for k in range(26):                                                                                                         # purple crystal clusters on the floor edge and walls
        a = k * 2 * math.pi / 26 + 0.1; r = R - 1.5 - 2.0 * (k % 3); h = 2.5 + 3.0 * ((k * 7) % 5) / 4.0; x, z = math.cos(a) * r, math.sin(a) * r
        if abs(math.degrees(a) % 180) < 16 or abs(math.degrees(a) % 180) > 164: continue
        b.cyl(Bz(x, h / 2, z), 1.0 + 0.3 * (k % 2), h, MCR if k % 3 else MCR2, 6, r2=0.05)
    # the raised platform and the control desk
    b.cyl(Bz(0, 0.3, 2.0), 5.5, 0.6, MS, 24); b.cyl(Bz(0, 0.65, 2.0), 5.0, 0.1, LPU, 24)
    bx(0, 1.15, 3.5, 6.0, 1.1, 1.3, MD, 2.0, top=MS); bx(-1.9, 1.95, 3.9, 1.8, 0.9, 0.1, XB, 1.0); bx(0, 1.95, 3.9, 1.8, 0.9, 0.1, XG, 1.0); bx(1.9, 1.95, 3.9, 1.8, 0.9, 0.1, XR, 1.0)
    bx(-1.2, 1.78, 3.1, 1.0, 0.1, 0.6, XO, 1.0); cylv(1.4, 1.85, 3.1, 0.22, 0.2, LR, 10)                                          # the keypad and the big red button
    ring_obj('Arc', 7.0, 0.35, LW, (0, 8.0, 6.0), 'z', 64, 8); ring_obj('Arc2', 5.8, 0.18, LC, (0, 8.0, 5.9), 'z', 64, 6); ring_obj('Arc3', 8.4, 0.12, LPU, (0, 8.0, 6.1), 'z', 64, 6)
    b.cyl(Bz(0, 6.6, 7.0), 0.05, 2.8, MCR2, 8, r2=1.7); b.cyl(Bz(0, 9.4, 7.0), 1.7, 2.8, MCR, 8, r2=0.05)      # the Zero Point crystal at the centre of the ring (two cones)
    for k in range(8): a = k * 2 * math.pi / 8; bx(math.cos(a) * 8.2, 8.0 + math.sin(a) * 8.2, 6.2, 0.5, 0.5, 0.6, MS, 1.0)       # clamps round the arc
    arcs = [o for o in bpy.data.objects if o.name.startswith('Arc')]
    for k, o in enumerate(arcs): keys(o, 'scale', lambda t, k=k: (1.0 + 0.03 * math.sin(2 * math.pi * t / (1.6 + 0.4 * k)),) * 3, 24, 8, 2); keys(o, 'rotation_euler', lambda t, k=k: (math.pi / 2, (1 if k % 2 == 0 else -1) * 2 * math.pi * t / 8 * (1 + k), 0), 24, 8, 2)
    wp(0, 0, -6); wp(0, 0, 1.5)
    b.build('Gyro')
    for i, (x, y, z) in enumerate(WPS): empty('WP%02d' % i, None, Bz(x, y, z))
    export(out)
elif kind == 'hatch':
    bx(0, -0.05, 0, 8.0, 0.4, 8.0, MS, 2.0); bx(0, 0.15, 0, 7.0, 0.1, 7.0, MD, 2.0)
    for sx in (-1, 1): bx(sx * 2.0, 0.0, 0, 2.0, 0.5, 4.2, MS, 1.0)                                  # the frame of the stair shaft (the opening is 2 m wide, z -2.1 .. 2.1)
    bx(0, 0.0, -2.25, 2.0, 0.5, 0.3, MS, 1.0); bx(0, 0.0, 2.25, 2.0, 0.5, 0.3, MS, 1.0)
    for k in range(10): bx(0, -0.25 - k * 0.3, -1.8 + k * 0.38, 1.8, 0.12, 0.4, MD, 1.0); bx(0, -0.55 - k * 0.3, -1.8 + k * 0.38, 1.8, 0.02, 0.38, LC if k % 3 == 0 else MS, 1.0)   # stairs going down
    bx(0, -3.2, 2.2, 2.0, 6.0, 0.2, MD); bx(-1.05, -1.6, 0, 0.12, 3.2, 4.4, MD); bx(1.05, -1.6, 0, 0.12, 3.2, 4.4, MD)
    for sx in (-3.2, 3.2):
        for sz in (-3.2, 3.2): cylv(sx, 0.6, sz, 0.12, 1.2, MS, 6); bx(sx, 1.25, sz, 0.28, 0.1, 0.28, LR, 1.0)
    bx(0, 0.2, -3.0, 4.0, 0.06, 0.8, LA, 1.0)
    b.build('Hatch')
    c = MB(M); c.box(Bz(0, 0.12, 0), (2.4, 4.8, 0.3), 6, 1.0, top=7); c.box(Bz(0, 0.3, 0), (1.8, 4.2, 0.08), 9, 1.0); c.build('Cover')
    export(out)
elif kind == 'base':
    # ---------------- the path: a staircase of pieces, each (name, length, width, height, heading); a 6 x 6 plaza sits at every corner
    PIECES = [('blue_corr', 26, 4.0, 3.4, 'z'), ('device', 28, 18.0, 6.0, 'x'), ('red_hall', 16, 4.0, 3.4, 'z'), ('window', 12, 10.0, 5.0, 'x'), ('machinery', 24, 24.0, 12.0, 'z'), ('blue_corr2', 22, 4.0, 3.4, 'x'),
              ('red_orange', 8, 4.0, 3.4, 'z'), ('control', 14, 14.0, 5.0, 'x'), ('hazy', 10, 4.0, 3.4, 'z')]
    PL = 6.0; SH = 14.0
    room(0, 0, 6, 6, SH, MWB, [('zp', 0, 4.0, 3.0)], y0=0.0)                                                                  # the shaft: 6 x 6, 14 m tall, open at the bottom towards +z
    for k in range(7): bx(2.7, 1.5 + k * 2.0, 0, 0.1, 0.1, 1.0, MS, 1.0); bx(2.7, 1.5 + k * 2.0, -2.5, 0.1, 0.1, 0.1, LC, 1.0)
    for k in range(14): bx(-2.7, 0.5 + k, 1.0, 0.1, 0.1, 1.0, MS, 1.0)                                                          # ladder rungs
    bx(0, SH - 0.5, 0, 4.4, 0.2, 4.4, LC, 1.0)
    wp(0, SH - 1.0, 0); wp(0, 1.0, 0)
    px, pz = 0.0, 3.0        # the end of the previous piece (a piece starts on its edge)
    boxes = []
    def overlap(a, b2): return not (a[1] <= b2[0] or b2[1] <= a[0] or a[3] <= b2[2] or b2[3] <= a[2])
    for i, (name, ln, w, h, hd) in enumerate(PIECES):
        if hd == 'z': cx, cz, W_, D_ = px, pz + ln / 2, w, ln; ex, ez = px, pz + ln
        else: cx, cz, W_, D_ = px + ln / 2, pz, ln, w; ex, ez = px + ln, pz
        boxes.append((cx - W_ / 2, cx + W_ / 2, cz - D_ / 2, cz + D_ / 2, name))
        ops = []
        side_in, side_out = ('zm', 'zp') if hd == 'z' else ('xm', 'xp')
        gw = min(4.0, w) if name not in ('machinery', 'device', 'control', 'window') else 4.0
        ops = [(side_in, 0, gw, 3.0), (side_out, 0, gw, 3.0)]
        theme = {'blue_corr': (MWB, LC), 'device': (MWR, LP), 'red_hall': (MWR, LR), 'window': (MWB, LC), 'machinery': (MWB, LC), 'blue_corr2': (MWB, LC), 'red_orange': (MWR, LA), 'control': (MWR, LR), 'hazy': (MWP, LPU)}[name]
        room(cx, cz, W_, D_, h, theme[0], ops, strip=theme[1])
        along = lambda u, lat, y, w2, h2, d2, m, t=1.0: bx(cx + (lat if hd == 'z' else u), y, cz + (u if hd == 'z' else lat), w2 if hd == 'z' else d2, h2, d2 if hd == 'z' else w2, m, t)   # u along the piece (from its centre), lat across
        if name in ('blue_corr', 'blue_corr2', 'hazy', 'red_orange'):
            for k in range(int(ln // 4)):
                u = -ln / 2 + 2 + k * 4.0; along(u, 0, h - 0.4, w - 0.2, 0.18, 0.3, MS); along(u, -w / 2 + 0.2, h / 2, 0.2, h, 0.3, MS)
                along(u, w / 2 - 0.2, h / 2, 0.2, h, 0.3, MS)                                                                  # ribs
            for s_ in (-1, 1): along(0, s_ * (w / 2 - 0.3), 0.5, 0.25, 0.5, ln - 1, MD)                                         # cable runs along the foot of the walls
            if name == 'blue_corr2': along(0, w / 2 - 0.08, 1.9, 0.06, 1.8, 3.0, XB); along(2.0, -w / 2 + 0.08, 1.6, 0.06, 2.4, 6.0, SB if False else XB)   # a lit window panel (8:10)
        if name == 'red_hall':
            for k in range(4): along(-ln / 2 + 2 + k * 3.8, -w / 2 + 0.1, 1.9, 0.08, 1.0, 1.5, XR if k % 2 == 0 else XO)       # wall displays (7:45)
        if name == 'device':
            cylv(cx, 0.1, cz, 5.2, 0.2, MS, 32); cylv(cx, 0.25, cz, 4.0, 0.1, LP, 32); cylv(cx, 0.32, cz, 2.6, 0.1, LR, 32)
            for k in range(6): a = k * math.pi / 3; cylv(cx + math.cos(a) * 4.6, 1.8, cz + math.sin(a) * 4.6, 0.25, 3.6, MS, 8); cylv(cx + math.cos(a) * 4.6, 3.7, cz + math.sin(a) * 4.6, 0.35, 0.3, LP, 8)
            ring_obj('DevRing', 3.4, 0.28, LP, (cx, 3.4, cz), hd, 48, 8); ring_obj('DevRing2', 2.4, 0.2, LR, (cx, 3.4, cz), hd, 48, 8)
            cylv(cx, 1.6, cz, 0.7, 2.6, LR, 14); cylv(cx, 3.4, cz, 0.9, 0.9, LP, 14)
            for sgn in (-1, 1): along(sgn * 6.0, sgn * 6.8, 0.8, 2.0, 1.6, 2.0, MD); along(sgn * 6.0, sgn * 6.8, 1.7, 2.1, 0.1, 2.1, LA)
        if name == 'window':
            along(0, w / 2 - 0.1, 2.4, 7.0, 3.0, 0.1, XB); along(0, w / 2 - 0.14, 2.4, 7.3, 3.3, 0.1, MD)                                   # the big window (7:50): blue-lit
            along(-3, -w / 2 + 0.6, 0.6, 2.0, 1.2, 1.2, MD); along(-3, -w / 2 + 0.6, 1.3, 1.6, 0.1, 0.8, XO)
        if name == 'machinery':
            for k in range(5):
                zz = -ln / 2 + 3 + k * 4.5; ring_obj('Arch%d' % k, 8.0, 0.55, MS, (cx + (0 if hd == 'z' else zz), 0.0, cz + (zz if hd == 'z' else 0)), 'z' if hd == 'z' else 'x', 40, 6)
                ring_obj('ArchG%d' % k, 7.3, 0.14, LC, (cx + (0 if hd == 'z' else zz), 0.0, cz + (zz if hd == 'z' else 0)), 'z' if hd == 'z' else 'x', 40, 6)
            for sgn in (-1, 1): along(0, sgn * 9.5, 5.0, 3.0, 10.0, 3.0, MD, 1.0); along(0, sgn * 9.5, 5.0, 3.1, 0.3, 3.1, LC, 1.0); along(-4, sgn * 9.5, 8.0, 3.0, 0.4, 3.0, LC, 1.0)
            along(0, 0, 0.2, 4.0, 0.4, ln - 1, MS, 1.0)                                                                           # the catwalk
            for k in range(6): along(-ln / 2 + 2 + k * 4.0, 0, 8.2, 2.0, 0.2, 0.2, LC, 1.0)
        if name == 'control':
            for s_ in (-1, 1):
                for k in range(3): along(-4 + k * 4.0, s_ * (w / 2 - 1.0), 0.55, 2.4, 1.1, 1.2, MD); along(-4 + k * 4.0, s_ * (w / 2 - 1.5), 1.5, 2.2, 0.8, 0.1, [XG, XR, XO][k])
            along(0, 0, 0.5, 3.0, 1.0, 1.6, MS); along(0, 0, 1.1, 2.6, 0.1, 1.2, LR)
            along(0, w / 2 - 0.1, 2.6, 10.0, 1.4, 0.1, XB)
        if name == 'hazy':
            pass
        # path points: the start edge, the piece end; the corner plaza centre follows
        wp(*( (px, 0, pz + ln * 0.4) if hd == 'z' else (px + ln * 0.4, 0, pz) ))
        if hd == 'z': px, pz = ex, ez
        else: px, pz = ex, ez
        if i + 1 < len(PIECES):
            nhd = PIECES[i + 1][4]
            if nhd != hd:                                                                                                          # a corner plaza
                if hd == 'z': pcx, pcz = px, pz + PL / 2; ins, outs = 'zm', 'xp'
                else: pcx, pcz = px + PL / 2, pz; ins, outs = 'xm', 'zp'
                room(pcx, pcz, PL, PL, 3.6, theme[0], [(ins, 0, 4.0, 3.0), (outs, 0, 4.0, 3.0)], strip=theme[1])
                wp(pcx, 0, pcz)
                if hd == 'z': px, pz = pcx + PL / 2, pcz          # the next piece (heading x) starts on the plaza's +x edge
                else: px, pz = pcx, pcz + PL / 2                  # (heading z) starts on the +z edge
            else:
                pass
    # ---------------- the Zero Point chamber (a round hall) attached to the end of the hazy corridor (heading z)
    CR = 38.0; ccx, ccz = px, pz + CR
    b.cyl(Bz(ccx, -0.2, ccz), CR + 0.5, 0.4, MF, 48)
    for k in range(48):
        a = (k + 0.5) * 2 * math.pi / 48; x, z = ccx + math.cos(a) * CR, ccz + math.sin(a) * CR
        if abs(math.degrees(a) - 270) < 5.2: continue                                                                           # the entrance (south, the -z pole)
        b.box(Bz(x, 15.5, z), (3.2, 0.6, 31.0), MWP, 2.0)
    for k in range(16):                                                                                                         # crystals and pillars round the walls
        a = k * 2 * math.pi / 16 + 0.2; x, z = ccx + math.cos(a) * (CR - 3.0), ccz + math.sin(a) * (CR - 3.0)
        if abs(math.degrees(a) - 270) < 14: continue
        cylv(x, 4.0 + (k % 3), z, 1.6, 8.0 + 2 * (k % 3), MCR if k % 2 else MCR2, 6, r2=0.1); cylv(x, 14.0, z, 0.9, 28.0, MD, 8); cylv(x, 14.0, z, 0.95, 0.5, LPU, 8)
    bx(ccx, 0.2, ccz - CR + 8.0, 6.0, 0.4, 16.0, MS)                                                                            # the walkway from the entrance
    cylv(ccx, 0.5, ccz + 6.0, 9.0, 1.0, MS, 32); cylv(ccx, 1.05, ccz + 6.0, 8.2, 0.1, LPU, 32)                                  # the machine's base platform
    ring_obj('Ring', 15.0, 1.1, LPU, (ccx, 17.0, ccz + 6.0), 'z', 72, 10); ring_obj('Ring2', 12.5, 0.5, LC, (ccx, 17.0, ccz + 5.9), 'z', 72, 6); ring_obj('Ring3', 17.5, 0.35, LW, (ccx, 17.0, ccz + 6.2), 'z', 72, 6)
    for sgn in (-1, 1): bx(ccx + sgn * 16.0, 8.0, ccz + 6.0, 2.4, 16.0, 2.4, MD, 2.0); bx(ccx + sgn * 16.0, 8.0, ccz + 4.7, 0.3, 14.0, 0.2, LPU, 1.0)    # the pylons the ring hangs from
    for k in range(10): a = k * 2 * math.pi / 10; bx(ccx + math.cos(a) * 11.0, 17.0 + math.sin(a) * 11.0, ccz + 6.0, 1.0, 1.0, 1.2, MS, 1.0)
    wp(ccx, 0, ccz - CR); wp(ccx, 0, ccz - CR + 14)
    b.build('Base')
    # the glass dome (alpha, separate so the scene can shatter it) and the swirl disc inside the ring
    GL = material('A_Glass', tex_glass(0.22)); gm = sphere(CR + 0.2, 40, 16)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(gm); bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < -0.5], context='FACES'); bm.to_mesh(gm); bm.free()
    go = bpy.data.objects.new('Glass', gm); bpy.context.collection.objects.link(go); go.data.materials.append(GL); go.location = Bz(ccx, 31.0, ccz); go.scale = (1, 1, 0.37)
    for nm, r_, spd in (('Ring', 0, 1), ('Ring2', 0, -1.5), ('Ring3', 0, 0.7)):
        o = bpy.data.objects[nm]; keys(o, 'rotation_euler', lambda t, spd=spd: (math.pi / 2, spd * 2 * math.pi * t / 8, 0), 24, 8, 2)
    for o in bpy.data.objects:
        if o.name in ('DevRing', 'DevRing2'): keys(o, 'rotation_euler', lambda t, n=o.name: (o.rotation_euler[0], (1 if n == 'DevRing' else -1) * 2 * math.pi * t / 8, o.rotation_euler[2]), 24, 8, 2) if False else None
    empty('ZPC', None, Bz(ccx, 17.0, ccz + 6.0)); empty('CHC', None, Bz(ccx, 0, ccz)); empty('CH0', None, Bz(ccx, 0, ccz - CR + 2))
    for i, (x, y, z) in enumerate(WPS): empty('WP%02d' % i, None, Bz(x, y, z))
    over = [(a[4], b2[4]) for i, a in enumerate(boxes) for b2 in boxes[i + 2:] if overlap(a, b2)]
    print('PIECE OVERLAPS', over, 'END', px, pz, 'WPS', len(WPS))
    export(out)
