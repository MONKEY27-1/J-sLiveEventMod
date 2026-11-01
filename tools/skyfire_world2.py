"""blender -b -P skyfire_world2.py -- <interior|cube> <out.glb>
Operation: Sky Fire, second pass. Reuses the helpers / base materials of skyfire_world.py and adds: a holding-cell block you start in (energy barriers are separate objects), animated parts (blast doors, pistons, fans,
rotating rings, a conveyor), a lift cabin with a sliding shaft, a huge Cube hall with rack towers for hundreds of Cubes, and a beveled Cube model with inset circuit panels.
Unity axes: x right, y up, z forward (Blender here: (x, -z, y)). Separate objects ("parts") have their origin where they pivot / slide from; the plugin moves them by name."""
import bpy, sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
exec(compile(open(os.path.join(HERE, 'skyfire_world.py'), encoding='utf-8').read().split("if kind == 'interior':")[0], 'skyfire_world.py', 'exec'), globals())

def hazard_tex(W=128, H=128):
    U, V = grid(W, H); s = ((U + V) * 8) % 1 < 0.5; c = np.where(s[..., None], np.array([0.92, 0.74, 0.08])[None, None, :], np.array([0.05, 0.05, 0.07])[None, None, :])
    return rgba(c[..., 0], c[..., 1], c[..., 2], 1.0)
def hex_tex(W=256, H=256, n=6, base=(0.07, 0.09, 0.22), glowc=(0.35, 0.5, 1.0)):
    U, V = grid(W, H); px = U * n; py = V * n * 1.7320508; sx, sy = 1.0, 1.7320508
    ax = (px % sx) - 0.5 * sx; ay = (py % sy) - 0.5 * sy; bx_ = ((px - 0.5 * sx) % sx) - 0.5 * sx; by_ = ((py - 0.5 * sy) % sy) - 0.5 * sy
    ua = (ax * ax + ay * ay) < (bx_ * bx_ + by_ * by_); gx = np.where(ua, ax, bx_); gy = np.where(ua, ay, by_); d = np.maximum(np.abs(gy), np.abs(gx) * 0.8660254 + np.abs(gy) * 0.5)
    n_ = fbm(W, H, 6, 5); col = np.array(base)[None, None, :] * (0.85 + 0.3 * n_[..., None]); edge = (d > 0.44)[..., None]; rim = ((d > 0.38) & (d <= 0.44))[..., None]
    col = np.where(rim, np.array(glowc)[None, None, :] * 0.55, col); col = np.where(edge, col * 0.25, col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def belt_tex(W=64, H=128):
    U, V = grid(W, H); chev = np.abs(((V * 8 + np.abs(U - 0.5) * 2.2) % 1) - 0.5) < 0.12; col = np.where(chev[..., None], np.array([0.2, 0.9, 1.0])[None, None, :], np.array([0.05, 0.08, 0.16])[None, None, :])
    return rgba(col[..., 0], col[..., 1], col[..., 2], 1.0)
def cubeface_tex(W=128, H=128):
    r = np.random.default_rng(7); img = np.zeros((H, W, 4), np.float32); img[..., :3] = (0.05, 0.04, 0.09); img[..., 3] = 1
    for k in range(40):
        x0 = int(r.integers(6, W - 30)); y0 = int(r.integers(6, H - 30)); w = int(r.integers(6, 24)); h = int(r.integers(4, 18)); c = (0.35, 0.2, 0.8)
        img[y0:y0 + 2, x0:x0 + w, :3] = c; img[y0:y0 + h, x0:x0 + 2, :3] = c
        if r.random() < 0.5: img[y0 + h:y0 + h + 2, x0:x0 + w // 2, :3] = c
    return img

HZ = material('SF_Hazard', hazard_tex()); HX = material('SF_Hex', hex_tex()); BT = material('SF_Belt', belt_tex()); GD = material('SF_Gold', glow((0.9, 0.55, 0.08), (1.0, 0.92, 0.5)))
CFR = material('SF_CubeFrame', panel((0.1, 0.09, 0.15), 31, lines=1, wear=0.5)); CFC = material('SF_CubeFace', cubeface_tex()); CGW = material('SF_CubeGlow', glow((0.5, 0.2, 1.0), (0.9, 0.6, 1.0)))
MATS = MATS + [HZ, HX, BT, GD, CFR, CFC, CGW]; HZ_, HX_, BT_, GD_, CFR_, CFC_, CGW_ = range(20, 27)

def part(name, x, y, z, fn):
    b = new(); fn(b); o = b.build(name); o.location = Z(x, y, z); return o
def conduits(b, x0, z0, z1, w, ys=((0.25, 6.9), (0.18, 6.3), (0.3, 5.6))):
    zc = (z0 + z1) / 2
    for sx in (-1, 1):
        for r_, yy in ys: cy(b, x0 + sx * (w / 2 - 0.4), yy, zc, r_, z1 - z0, ME_, 10, axis='z')
        zz = z0 + 4.0
        while zz < z1: bx(b, x0 + sx * (w / 2 - 0.4), ys[-1][1], zz, 0.3, 1.8, 0.3, RB_, 1.0); zz += 8.0
def screens(b, x0, w, zs, y=3.6, mat=None):
    for sx in (-1, 1):
        for zz in zs: bx(b, x0 + sx * (w / 2 - 0.15), y, zz, 0.1, 1.6, 2.6, mat if mat is not None else GR_, 1.0); bx(b, x0 + sx * (w / 2 - 0.12), y, zz, 0.06, 2.0, 3.0, RB_, 1.0)
def doorway(b, x0, z, w_open, wall_w, h, name_l, name_r, mat=ME_):
    """static wall segments + lintel round a sliding double door; returns nothing, the two door halves are made as parts"""
    half = w_open / 2; side = (wall_w - w_open) / 2
    for sx in (-1, 1): bx(b, x0 + sx * (half + side / 2), h / 2, z, side, h, 0.8, RB_, 1.0)
    bx(b, x0, h - 0.6, z, w_open, 1.2, 0.8, RB_, 1.0); bx(b, x0, 0.03, z - 1.4, w_open, 0.05, 1.6, HZ_, 1.0); bx(b, x0, h - 1.3, z - 0.45, w_open, 0.1, 0.1, CY_, 1.0)
    for nm, sx in ((name_l, -1), (name_r, 1)): part(nm, x0 + sx * half / 2, (h - 1.2) / 2, z, lambda b2: bx(b2, 0, 0, 0, half, h - 1.2, 0.5, mat, 2.0))

def detail(b, x0, z0, z1, w, h):
    """greebles for a room: hex wall panels between the ribs, a cable tray with cables, ceiling trusses with light bars, vents, floor grates"""
    zc = (z0 + z1) / 2; L = z1 - z0; z = z0 + 2.5; k = 0
    for sx in (-1, 1):
        xw = x0 + sx * (w / 2 - 0.03)
        bx(b, x0 + sx * (w / 2 - 0.45), h - 1.25, zc, 0.5, 0.14, L, DK_, 1.0)
        for j, (col, dy) in enumerate(((RE_, 0.06), (CY_, 0.0), (PU_, -0.06))): cy(b, x0 + sx * (w / 2 - 0.45 + 0.12 * (j - 1)), h - 1.12 + dy, zc, 0.04, L, col, 6, axis='z')
        bx(b, xw, 0.45, zc, 0.06, 0.9, L, HZ_, 2.0)
    while z < z1 - 1.5:
        for sx in (-1, 1):
            xw = x0 + sx * (w / 2 - 0.03)
            bx(b, xw, h * 0.52, z, 0.04, h * 0.42, 3.4, HX_, 1.0); bx(b, xw, h * 0.52 + h * 0.23, z, 0.07, 0.08, 3.5, CY_, 1.0)
            if k % 3 == 1: bx(b, xw - sx * 0.05, 1.4, z + 1.7, 0.1, 0.7, 1.3, DK_, 1.0); [bx(b, xw - sx * 0.1, 1.15 + 0.17 * q, z + 1.7, 0.04, 0.05, 1.2, ME_, 1.0) for q in range(3)]
        bx(b, x0, h - 0.18, z, w - 0.5, 0.14, 0.3, RB_, 1.0); bx(b, x0, h - 0.3, z, 2.0, 0.06, 0.12, WH_, 1.0)
        if k % 2 == 0: bx(b, x0 + R.uniform(-w / 4, w / 4), 0.025, z + 1.2, 1.4, 0.03, 1.4, DK_, 1.0); bx(b, x0 + R.uniform(-w / 4, w / 4), 0.04, z + 0.2, 1.4, 0.03, 0.12, HZ_, 1.0)
        z += 2.5; k += 1

if kind == 'interior':
    BX = {'CELLS': 0, 'HALL': 60, 'WALK': 130, 'TUBE': 200, 'ROOMS': 240, 'PURPLE': 300, 'BLUE': 380, 'CUBES': 440}
    # ---- CELLS: you start inside a holding cell (six in a row, an energy barrier across each front); the barriers drop and the squad runs out
    b = new(); x0 = BX['CELLS']; W = 26.0
    room(b, x0, -8, 14, W, 6.0); strips(b, x0, -8, 14, W, 6.0); conduits(b, x0, -8, 14, W); detail(b, x0, -8, 14, W, 6.0)
    for k in range(7): bx(b, x0 - 12.6 + k * 4.2, 1.6, -4, 0.3, 3.2, 8.0, RB_, 1.0); bx(b, x0 - 12.6 + k * 4.2, 5.0, -4, 0.3, 2.0, 8.0, RB_, 1.0)
    for i in range(6):
        cx = x0 - 10.5 + i * 4.2
        bx(b, cx, 2.3, -7.85, 2.8, 0.7, 0.1, PK_, 1.0); bx(b, cx, 0.45, -6.7, 2.4, 0.9, 1.0, ME_, 1.0); bx(b, cx, 0.02, -0.9, 3.8, 0.04, 1.6, HZ_, 1.0); bx(b, cx, 3.3, 0.0, 4.0, 0.35, 0.5, RB_, 1.0); bx(b, cx, 3.0, -0.15, 3.6, 0.08, 0.1, PK_, 1.0)
        bx(b, cx, 5.4, -3.0, 1.4, 0.2, 5.0, WH_, 1.0)
    bx(b, x0, 3.0, -8.1, W, 6.0, 0.2, 1)
    for sx in (-1, 1): bx(b, x0 + sx * 8.5, 3.0, 14.1, 8.8, 6.0, 0.2, 1); bx(b, x0, 5.4, 14.1, 8.2, 1.2, 0.2, 1)
    for sx in (-1, 1): bx(b, x0 + sx * 4.2, 3.0, 14.0, 0.5, 6.0, 0.7, RB_, 1.0)
    for zz in (4, 9): bx(b, x0, 5.7, zz, 5.0, 0.2, 0.4, CY_, 1.0)
    # the corridor behind the cell hall: more cells behind pink fields on both sides
    room(b, x0, 14, 70, 8.0, 5.5); strips(b, x0, 14, 70, 8.0, 5.5); detail(b, x0, 14, 70, 8.0, 5.5); ribs(b, x0, 14, 70, 8.0, 5.5, 5.0); windows(b, x0, 14, 70, 8.0, 5.5, PK_, 5.0); conduits(b, x0, 14, 70, 8.0, ys=((0.2, 5.0),))
    bx(b, x0, 2.0, 70.2, 8.0, 5.5, 0.2, BL_, 1.0)
    for z in (24, 40, 56): bx(b, x0, 5.0, z, 3.0, 0.3, 0.3, CY_, 1.0)
    b.build('CELLS'); marks('CELLS', x0, 0, 0, 56)
    for i in range(6): part('Barrier%d' % i, x0 - 10.5 + i * 4.2, 1.6, -0.1, lambda b2: bx(b2, 0, 0, 0, 3.9, 3.0, 0.06, PK_, 1.0))
    # ---- HALL: blast doors, pistons, fans, conduits, screens
    b = new(); x0 = BX['HALL']; room(b, x0, -4, 94, 14.0, 8.0); strips(b, x0, -4, 94, 14.0, 8.0); ribs(b, x0, -4, 94, 14.0, 8.0, 6.0, 0.9); conduits(b, x0, -4, 94, 14.0); screens(b, x0, 14.0, (14, 44, 76)); detail(b, x0, -4, 94, 14.0, 8.0)
    for sx in (-1, 1):
        for zz in range(8, 90, 9):
            if R.random() < 0.8 and abs(zz - 30) > 4 and abs(zz - 62) > 4:
                for k in range(3): cy(b, x0 + sx * (7 - 1.0 - 1.45 * (k % 2)), 0.75, zz + (k // 2) * 1.5 - 0.7, 0.7, 1.5, BA_, 12)
    for z in (20, 52, 76): bx(b, x0, 7.3, z, 14.0, 0.5, 0.5, PU_, 1.0)
    for zz in (30, 62): doorway(b, x0, zz, 7.0, 14.0, 8.0, 'BlastDoor%dL' % zz, 'BlastDoor%dR' % zz)
    for k, zz in enumerate((14, 24, 38, 50, 72, 84)): bx(b, x0 + (3.5 if k % 2 else -3.5), 7.75, zz, 1.4, 0.5, 1.4, RB_, 1.0)
    bx(b, x0, 4.0, 94.2, 14.0, 8.0, 0.2, 1)
    for k in range(6): a = k / 6 * math.tau; bx(b, x0 + math.cos(a) * 3.4, 3.2 + math.sin(a) * 3.4, 94.0, 1.4, 1.4, 0.5, RB_, 1.0)
    bx(b, x0, 3.2, 94.05, 5.6, 5.6, 0.3, WH_, 1.0)
    for k, zz in enumerate((14, 24, 38, 50, 72, 84)): part('Piston%d' % k, x0 + (3.5 if k % 2 else -3.5), 7.5, zz, lambda b2: (cy(b2, 0, -1.1, 0, 0.22, 2.2, ME_, 10), bx(b2, 0, -2.4, 0, 1.2, 0.5, 1.2, HZ_, 1.0)))
    for k, (sx, zz) in enumerate(((-1, 18), (1, 40), (-1, 56), (1, 82))):
        bx(b, x0 + sx * 6.9, 5.4, zz, 0.2, 3.0, 3.0, DK_, 1.0)
        part('Fan%d' % k, x0 + sx * 6.7, 5.4, zz, lambda b2: [bx(b2, 0, 0, 0, 0.06, 2.6, 0.3, ME_, 1.0), bx(b2, 0, 0, 0, 0.06, 0.3, 2.6, ME_, 1.0)])
    b.build('HALL'); marks('HALL', x0, 0, 0, 90)
    for k, zz in enumerate((10, 34, 56, 70)): empty('STEAM%d' % k, None, Z(x0 + (-6.3 if k % 2 else 6.3), 0.4, zz))
    # ---- WALK: a conveyor over the void, windows, the cylinder hall
    b = new(); x0 = BX['WALK']; W0 = 18.0
    bx(b, x0, -14.0, 20, W0, 0.4, 48, DK_); bx(b, x0, 11.0, 20, W0, 0.3, 48, C_)
    for sx in (-1, 1):
        bx(b, x0 + sx * 2.6, 1.15, 20, 0.12, 0.1, 48, ST_, 1.0); bx(b, x0 + sx * 2.6, 0.5, 20, 0.08, 0.08, 48, CY_, 1.0); bx(b, x0 + sx * (W0 / 2 + 0.1), -2, 20, 0.2, 18, 48, 1); bx(b, x0 + sx * 3.0, -0.2, 20, 0.6, 0.3, 48, ME_, 1.0)
        for zz in range(-3, 44, 6): bx(b, x0 + sx * 2.6, 0.6, zz, 0.2, 1.4, 0.2, CY_, 1.0); bx(b, x0 + sx * 8.5, -6.0, zz + 3, 1.2, 14, 1.2, RB_, 1.0)
    for z in range(0, 44, 11): bx(b, x0, 6.0, z, W0, 0.6, 0.8, RB_, 1.0); bx(b, x0, 5.6, z, 3.0, 0.2, 0.3, PU_, 1.0)
    for zz in (8, 22, 36): bx(b, x0, 0.08, zz, 5.0, 0.02, 0.5, HZ_, 1.0)
    z0, z1 = 44.0, 74.0; room(b, x0, z0, z1, 14.0, 7.0); strips(b, x0, z0, z1, 14.0, 7.0); detail(b, x0, z0, z1, 14.0, 7.0); ribs(b, x0, z0, z1, 14.0, 7.0, 7.5, 0.6)
    for sx in (-1, 1):
        for zz in (51.5, 59.0, 66.5): bx(b, x0 + sx * 6.9, 3.8, zz, 0.12, 4.6, 6.0, PK_, 1.0); bx(b, x0 + sx * 6.97, 3.8, zz, 0.06, 5.0, 6.4, CY_, 1.0)
    bx(b, x0 - 6.8, 3.0, 73.0, 0.12, 3.4, 3.4, PU_, 1.0)
    for k in range(6): a = k / 6 * math.tau; bx(b, x0 - 6.74, 3.0 + math.sin(a) * 1.2, 73.0 + math.cos(a) * 1.2, 0.05, 0.3, 0.3, WH_, 1.0)
    z0, z1 = 74.0, 104.0; room(b, x0, z0, z1, 16.0, 11.0); strips(b, x0, z0, z1, 16.0, 11.0); detail(b, x0, z0, z1, 16.0, 11.0); ribs(b, x0, z0, z1, 16.0, 11.0, 7.5, 0.8)
    cy(b, x0 + 4.2, 4.5, 92.0, 3.0, 9.0, GL_, 20); cy(b, x0 + 4.2, 0.3, 92.0, 3.4, 0.6, ME_, 20); cy(b, x0 + 4.2, 9.2, 92.0, 3.4, 0.6, ME_, 20); cy(b, x0 + 4.2, 4.5, 92.0, 0.9, 9.0, CY_, 12)
    for k in range(4): cy(b, x0 - 5.0, 1.4 + 2.6 * k, 90.0, 1.8 - 0.2 * k, 0.9, ME_, 14)
    bx(b, x0, 5.5, 104.2, 16.0, 11.0, 0.2, 1); bx(b, x0, 3.0, 104.0, 4.0, 5.0, 0.2, BL_, 1.0)
    b.build('WALK'); marks('WALK', x0, 0, 0, 100)
    part('Belt', x0, 0.0, 20.0, lambda b2: bx(b2, 0, 0, 0, 5.0, 0.12, 48, BT_, 4.0))
    # ---- TUBE: unchanged (cyan swirl wall, hollow rings)
    b = new(); x0 = BX['TUBE']; NS = 20
    def tp(a, z, r): return (x0 + math.cos(a) * r, -z, math.sin(a) * r)
    for i in range(NS):
        a0 = i / NS * math.tau; a1 = (i + 1) / NS * math.tau
        for z in range(-4, 78, 3):
            z2 = z + 3; b.quad(tp(a0, z, 3.4), tp(a1, z, 3.4), tp(a1, z2, 3.4), tp(a0, z2, 3.4), SW_, [(i / NS * 3, z / 6.0), ((i + 1) / NS * 3, z / 6.0), ((i + 1) / NS * 3, z2 / 6.0), (i / NS * 3, z2 / 6.0)])
        for z in range(0, 74, 3): b.quad(tp(a0, z - 0.25, 3.3), tp(a1, z - 0.25, 3.3), tp(a1, z + 0.25, 3.3), tp(a0, z + 0.25, 3.3), CY_, [(0, 0), (1, 0), (1, 1), (0, 1)])
    cy(b, x0, 0, 77.5, 3.2, 0.4, WH_, 18, axis='z'); cy(b, x0, 0, -4.5, 3.2, 0.4, WH_, 18, axis='z'); b.build('TUBE'); marks('TUBE', x0, -0.9, -2, 72)
    # ---- ROOMS: arrival room, windows, a piston door into the machinery room whose rings turn
    b = new(); x0 = BX['ROOMS']; room(b, x0, -4, 20, 12.0, 6.5); strips(b, x0, -4, 20, 12.0, 6.5); detail(b, x0, -4, 20, 12.0, 6.5); ribs(b, x0, -4, 20, 12.0, 6.5, 6.0, 0.7); conduits(b, x0, -4, 20, 12.0)
    for k in range(4): cy(b, x0 + (-3.5 + 2.3 * k), 1.1, 14.0, 0.5, 2.2, ME_, 10)
    bx(b, x0, 3.2, -4.1, 12.0, 6.5, 0.2, 1)
    z0, z1 = 20.0, 52.0; room(b, x0, z0, z1, 9.0, 6.0); strips(b, x0, z0, z1, 9.0, 6.0); detail(b, x0, z0, z1, 9.0, 6.0); ribs(b, x0, z0, z1, 9.0, 6.0, 6.5, 0.5); windows(b, x0, z0, z1, 9.0, 6.0, GR_, 6.5, 5.0, 3.6, 1.2, left=True, right=False)
    z0, z1 = 52.0, 86.0; room(b, x0, z0, z1, 26.0, 15.0); strips(b, x0, z0, z1, 26.0, 15.0, False); detail(b, x0, z0, z1, 26.0, 15.0); ribs(b, x0, z0, z1, 26.0, 15.0, 8.5, 1.2)
    doorway(b, x0, 52.0, 9.0, 26.0, 6.0, 'PDoorL', 'PDoorR')
    cy(b, x0, 7.0, 74.0, 1.1, 14.0, CY_, 10); bx(b, x0, 8.5, 64.0, 26.0, 0.5, 0.5, PU_, 1.0); screens(b, x0, 26.0, (60, 74), 6.0)
    bx(b, x0, 7.5, 86.2, 26.0, 15.0, 0.2, 1); bx(b, x0, 3.0, 86.0, 4.0, 5.0, 0.2, BL_, 1.0); b.build('ROOMS'); marks('ROOMS', x0, 0, 0, 80)
    for k in range(5): part('Ring%d' % k, x0, 3.0 + k * 2.4, 74.0, lambda b2, k=k: (cy(b2, 0, 0, 0, 6.5 - 0.5 * k, 1.1, ME_, 22), cy(b2, 0, 0.56, 0, 6.7 - 0.5 * k, 0.12, CY_, 22)))
    # ---- PURPLE: as before
    b = new(); x0 = BX['PURPLE']; zc = 30.0; Rr = 28.0
    for k in range(20):
        a = k / 20 * math.tau; bx(b, x0 + math.sin(a) * Rr, 11.0, zc + math.cos(a) * Rr, 4.2, 22.0, 4.2, RB_, 2.0)
    bx(b, x0, -0.1, zc, 62.0, 0.2, 62.0, F_, 4.0); bx(b, x0, 22.1, zc, 62.0, 0.2, 62.0, C_, 4.0)
    for k in range(40):
        a = k / 40 * math.tau; bx(b, x0 + math.sin(a) * (Rr + 0.2), 11.0, zc + math.cos(a) * (Rr + 0.2), 4.3, 22.0, 4.3, 1)
    for k in range(12): a = k / 12 * math.tau; bx(b, x0 + math.sin(a) * 13.5, 0.5, zc + math.cos(a) * 13.5, 1.2, 1.0, 1.2, RB_, 1.0); bx(b, x0 + math.sin(a) * 13.5, 1.05, zc + math.cos(a) * 13.5, 0.7, 0.1, 0.7, PU_, 1.0)
    for k in range(8):
        a = k / 8 * math.tau + 0.2; px, pz = x0 + math.sin(a) * 22.0, zc + math.cos(a) * 22.0; bx(b, px, 0.9, pz, 2.4, 1.8, 1.4, DK_, 1.0); bx(b, px, 1.85, pz, 2.0, 0.08, 1.1, PU_, 1.0); cy(b, px, 3.0, pz, 0.12, 6.0, ME_, 8)
    for yy in (8.0, 15.0): [bx(b, x0 + math.sin(a) * 25.5, yy, zc + math.cos(a) * 25.5, 5.0, 0.3, 1.6, ME_, 2.0) for a in [k / 10 * math.tau for k in range(10)]]
    bx(b, x0, 0.4, 1.0, 10.0, 0.8, 4.0, ME_, 2.0); b.build('PURPLE'); marks('PURPLE', x0, 0, 2, 16)
    pb = new(); cy(pb, x0, 0.04, zc, 12.0, 0.1, PU_, 36); cy(pb, x0, 0.12, zc, 12.8, 0.1, RB_, 36, r2=12.0); cy(pb, x0, 0.16, zc, 8.0, 0.06, PK_, 28)
    for r_ in (14.0, 17.0, 20.0): cy(pb, x0, 0.03, zc, r_, 0.05, ST_, 36, r2=r_ - 0.25)
    pb.build('POOL')
    # ---- BLUE: the Cube chamber is a lift cabin; the shaft (a separate object) slides down past its open sides when it rises
    b = new(); x0 = BX['BLUE']
    bx(b, x0, -0.1, 24, 26, 0.2, 56, F_); bx(b, x0, 16.1, 24, 26, 0.2, 56, C_); bx(b, x0, 8.0, -4.1, 26, 16, 0.2, 1); bx(b, x0, 8.0, 52.2, 26, 16, 0.2, 1)
    for sx in (-1, 1):
        for zz in range(-2, 52, 8): bx(b, x0 + sx * 12.9, 8, zz, 1.2, 16, 1.2, RB_, 1.0); bx(b, x0 + sx * 12.5, 8, zz + 4, 0.2, 14, 0.2, CY_, 1.0)
        bx(b, x0 + sx * 12.9, 1.1, 24, 0.2, 0.15, 56, ST_, 1.0); bx(b, x0 + sx * 12.9, 0.5, 24, 0.4, 1.0, 56, ME_, 1.0); bx(b, x0 + sx * 12.9, 15.4, 24, 0.5, 0.5, 56, RB_, 1.0)
    for z in (6, 20, 34): bx(b, x0, 0.04, z, 18, 0.06, 0.25, CY_, 1.0)
    bx(b, x0, 5.0, 44.0, 12.0, 10.0, 1.0, DK_, 1.0)
    for sx in (-1, 1): bx(b, x0 + sx * 6.3, 5.0, 43.5, 0.8, 10.0, 1.2, RB_, 1.0)
    bx(b, x0, 10.2, 43.5, 13.6, 0.8, 1.2, RB_, 1.0); bx(b, x0, 8.0, 43.4, 10.0, 0.18, 0.3, BL_, 1.0); bx(b, x0, 0.04, 40, 10, 0.06, 2.0, HZ_, 1.0)
    for sx in (-1, 1):
        for zz in (6, 18, 30): bx(b, x0 + sx * 10.5, 0.7, zz, 2.2, 1.4, 1.8, DK_, 1.0); bx(b, x0 + sx * 10.2, 1.5, zz, 1.4, 0.1, 1.2, BL_, 1.0); bx(b, x0 + sx * 10.8, 2.6, zz, 0.15, 1.8, 1.4, GR_, 1.0)
        cy(b, x0 + sx * 11.8, 8.0, 24, 0.35, 16.0, ME_, 10, axis='y')
    b.build('BLUE'); marks('BLUE', x0, 0, 2, 26)
    def shaft(b2):
        for sx in (-1, 1):
            for yy in range(-90, 130, 4): bx(b2, sx * 14.9, yy, 24, 0.8, 0.5, 60, RB_, 1.0); bx(b2, sx * 14.45, yy + 0.3, 24, 0.2, 0.3, 58, CY_, 1.0)
            for zz in (-4, 24, 52): bx(b2, sx * 14.9, 20, zz, 1.0, 230, 1.0, RB_, 1.0)
    part('Shaft', x0, 0, 0, shaft)
    # ---- CUBES: the hall that stores the Cubes: a dock, rack towers on both sides (the plugin fills the racks with hundreds of Cubes), catwalks, the golden dais at the far end
    b = new(); x0 = BX['CUBES']; H = 44.0; WD = 64.0
    bx(b, x0, -0.1, 53, WD, 0.2, 118, F_, 4.0); bx(b, x0, H + 0.1, 53, WD, 0.2, 118, C_, 4.0)
    for sx in (-1, 1): bx(b, x0 + sx * (WD / 2 + 0.1), H / 2, 53, 0.2, H, 118, 1, 4.0)
    bx(b, x0, H / 2, -6.1, WD, H, 0.2, 1, 4.0); bx(b, x0, H / 2, 112.1, WD, H, 0.2, 1, 4.0)
    bx(b, x0, 0.08, 4, 14, 0.12, 14, ME_, 2.0); bx(b, x0, 0.16, 4, 12, 0.05, 12, CY_, 1.0); bx(b, x0, 0.2, 4, 10, 0.05, 10, DK_, 1.0)
    for sx in (-1, 1):
        for xi in (9.5, 16, 22.5, 29):
            for zi in range(8, 99, 6): bx(b, x0 + sx * xi, H / 2 - 2, zi, 0.6, H - 4, 0.6, RB_, 1.0)
        for xi in (12.75, 19.25, 25.75):
            for yi in range(2, 40, 6): bx(b, x0 + sx * xi, yi, 53, 6.3, 0.4, 92, ME_, 2.0)
        for yi in (8, 20, 32): bx(b, x0 + sx * 8.4, yi, 53, 1.6, 0.25, 92, ME_, 2.0); bx(b, x0 + sx * 7.7, yi + 0.7, 53, 0.08, 1.2, 92, ST_, 1.0)
        for zi in range(14, 99, 12): bx(b, x0 + sx * 8.4, 20, zi, 0.5, 40, 0.5, RB_, 1.0)
    bx(b, x0, H - 0.2, 53, 1.2, 0.1, 110, WH_, 1.0)
    for zz in (28, 58, 88): cy(b, x0, H, zz, 6.0, 0.4, DK_, 18)
    cy(b, x0, 0.5, 104, 12.0, 1.0, ME_, 24); cy(b, x0, 1.2, 104, 9.5, 0.5, RB_, 24); cy(b, x0, 1.52, 104, 8.0, 0.1, GD_, 24); cy(b, x0, 0.9, 104, 12.3, 0.12, GD_, 24)
    for a in (0.7, 2.3, 3.9, 5.5): bx(b, x0 + math.cos(a) * 9.0, 7.0, 104 + math.sin(a) * 9.0, 1.2, 14.0, 1.2, RB_, 1.0); bx(b, x0 + math.cos(a) * 9.0, 14.2, 104 + math.sin(a) * 9.0, 0.8, 0.5, 0.8, GD_, 1.0)
    for zz in range(10, 100, 10): bx(b, x0, H - 4.0, zz, 0.15, 3.6, 0.15, ME_, 1.0); bx(b, x0, H - 6.0, zz, 3.0, 0.3, 0.8, WH_, 1.0)
    for sx in (-1, 1): bx(b, x0 + sx * 6.0, H - 3.0, 53, 0.8, 0.8, 110, RB_, 1.0); bx(b, x0 + sx * 5.0, 0.03, 53, 0.12, 0.05, 100, CY_, 1.0)
    for zz in range(8, 100, 12): bx(b, x0, 0.025, zz, 8.0, 0.03, 0.5, HZ_, 1.0)
    for k in range(5): bx(b, x0, 0.12 * k, 14.0 + 0.6 * k, 12.0, 0.12, 0.6, ME_, 1.0)
    b.build('CUBES'); marks('CUBES', x0, 0, 4, 52)
    export(out)

elif kind == 'debris':
    def obj(name, fn): b = new(); fn(b); b.build(name)
    def t_plate(b): bx(b, 0, 0, 0, 6.0, 0.25, 4.5, ME_, 2.0); bx(b, -2.2, 0.18, 0.5, 1.4, 0.3, 1.2, RB_, 1.0); bx(b, 1.6, 0.16, -1.2, 2.2, 0.1, 0.12, CY_, 1.0); bx(b, 0.2, 0.14, 1.6, 0.12, 0.08, 2.0, PK_, 1.0)
    def t_plate2(b): bx(b, 0, 0, 0, 4.0, 0.2, 6.5, ME_, 2.0); bx(b, 0, 0.12, 0, 3.0, 0.06, 5.2, HX_, 1.0); bx(b, 1.8, 0.14, 2.8, 0.5, 0.2, 0.5, GD_, 1.0)
    def t_beam(b): bx(b, 0, 0, 0, 0.5, 0.18, 9.0, ME_, 1.0); bx(b, 0, 0.3, 0, 0.18, 0.5, 9.0, ME_, 1.0); bx(b, 0, -0.3, 0, 0.5, 0.12, 9.0, ME_, 1.0); bx(b, 0, 0.1, 4.4, 0.7, 0.7, 0.2, HZ_, 1.0)
    def t_beam2(b): bx(b, 0, 0, 0, 0.6, 0.6, 6.0, RB_, 1.0); [bx(b, 0, 0, -2.5 + 1.0 * k, 0.9, 0.9, 0.15, ME_, 1.0) for k in range(6)]; bx(b, 0, 0.35, 0, 0.12, 0.08, 6.0, CY_, 1.0)
    def t_pipe(b): cy(b, 0, 0, 0, 0.7, 8.0, ME_, 12, axis='z'); cy(b, 0, 0, -2.0, 0.95, 0.5, RB_, 12, axis='z'); cy(b, 0, 0, 2.5, 0.95, 0.5, RB_, 12, axis='z'); cy(b, 0, 0, 0, 0.74, 0.3, CY_, 12, axis='z')
    def t_pipe2(b): cy(b, 0, 0, 0, 0.35, 5.0, ME_, 8, axis='z'); cy(b, 0.9, 0.2, 0, 0.3, 4.0, RE_, 8, axis='z'); cy(b, -0.8, 0.1, 0.5, 0.25, 3.0, PU_, 8, axis='z')
    def t_strut(b): [bx(b, 0, 0, k * 1.0 - 2.5, 0.15, 0.15, 1.2, ME_, 1.0) for k in range(6)]; [bx(b, 0.6 * (k % 2) - 0.3, 0.6 * ((k + 1) % 2) - 0.3, k * 1.0 - 2.5, 0.12, 0.12, 1.4, RB_, 1.0) for k in range(6)]; bx(b, 0, 0, 0, 1.0, 1.0, 0.2, GR_, 1.0)
    def t_ring(b): [bx(b, math.cos(k / 12 * math.tau) * 2.6, math.sin(k / 12 * math.tau) * 2.6, 0, 1.6, 0.5, 0.7, ME_, 1.0) for k in range(12)]; [bx(b, math.cos(k / 12 * math.tau) * 2.0, math.sin(k / 12 * math.tau) * 2.0, 0, 0.9, 0.18, 0.4, CY_, 1.0) for k in range(12)]
    def t_panel(b): bx(b, 0, 0, 0, 3.2, 0.18, 2.4, DK_, 1.0); bx(b, 0, 0.1, 0, 2.7, 0.05, 1.9, PK_, 1.0); bx(b, 0, 0.12, 0, 3.3, 0.08, 0.15, CY_, 1.0)
    def t_tank(b): cy(b, 0, 0, 0, 1.5, 3.5, ME_, 14, axis='z'); cy(b, 0, 0, 1.9, 1.1, 0.5, RB_, 14, axis='z'); cy(b, 0, 0, -1.9, 1.1, 0.5, RB_, 14, axis='z'); cy(b, 0, 0, 0, 1.55, 0.25, HZ_, 14, axis='z')
    def t_chunk(b):
        for k in range(14): bx(b, R.uniform(-2, 2), R.uniform(-1.2, 1.2), R.uniform(-2, 2), R.uniform(1.0, 2.6), R.uniform(0.8, 2.0), R.uniform(1.0, 2.6), ME_ if k % 3 else RB_, 2.0)
        bx(b, 0, 0.2, 0, 1.4, 1.4, 1.4, OR_, 1.0)
    def t_chunk2(b):
        for k in range(10): bx(b, R.uniform(-3, 3), R.uniform(-0.8, 0.8), R.uniform(-3, 3), R.uniform(1.6, 3.4), R.uniform(0.3, 0.7), R.uniform(1.6, 3.4), HX_ if k % 2 else ME_, 2.0)
        bx(b, 0, 0.5, 0, 0.8, 1.0, 0.8, RE_, 1.0)
    def t_seat(b): bx(b, 0, 0, 0, 1.4, 0.3, 1.4, DK_, 1.0); bx(b, 0, 0.7, -0.6, 1.4, 1.4, 0.25, DK_, 1.0); bx(b, 0.6, 0.35, 0, 0.12, 0.4, 1.2, ME_, 1.0); bx(b, -0.6, 0.35, 0, 0.12, 0.4, 1.2, ME_, 1.0)
    def t_crate(b): bx(b, 0, 0, 0, 1.8, 1.4, 1.8, RB_, 1.0); bx(b, 0, 0, 0, 1.9, 0.2, 1.9, HZ_, 1.0); bx(b, 0, 0.7, 0, 1.3, 0.05, 1.3, BL_, 1.0)
    for nm, fn in (('Deb_Plate1', t_plate), ('Deb_Plate2', t_plate2), ('Deb_Beam1', t_beam), ('Deb_Beam2', t_beam2), ('Deb_Pipe1', t_pipe), ('Deb_Pipe2', t_pipe2), ('Deb_Strut', t_strut), ('Deb_Ring', t_ring), ('Deb_Panel', t_panel), ('Deb_Tank', t_tank), ('Deb_Chunk1', t_chunk), ('Deb_Chunk2', t_chunk2), ('Deb_Seat', t_seat), ('Deb_Crate', t_crate)): obj(nm, fn)
    export(out)

elif kind == 'cube':
    import bmesh
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=2.0); bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.1, segments=1, affect='EDGES')
    big = [f for f in bm.faces if f.calc_area() > 1.5]
    r1 = bmesh.ops.inset_individual(bm, faces=big, thickness=0.14, depth=0.0)['faces']
    r2 = bmesh.ops.inset_individual(bm, faces=big, thickness=0.26, depth=-0.07)['faces']
    for f in bm.faces: f.material_index = 0
    for f in r1: f.material_index = 0
    for f in r2: f.material_index = 1
    for f in big: f.material_index = 2
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i])); a, c = [i for i in range(3) if i != ax]
        for l in f.loops: l[uv].uv = ((l.vert.co[a] + 1.0) * 0.5, (l.vert.co[c] + 1.0) * 0.5)
    me = bpy.data.meshes.new('Cube'); bm.to_mesh(me); bm.free()
    for m in (CFR, CFC, CGW): me.materials.append(m)
    for p in me.polygons: p.use_smooth = False
    o = bpy.data.objects.new('Cube', me); bpy.context.collection.objects.link(o)
    export(out)
