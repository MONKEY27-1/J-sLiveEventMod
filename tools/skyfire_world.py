"""blender -b -P skyfire_world.py -- <interior|view|props> <out.glb>
Operation: Sky Fire (12 Sep 2021): the Mothership interior, rebuilt from the user's recording (tools/skyfire_ref frames). Unity axes: x right, y up, z forward (Blender here: (x, -z, y)).
interior: eight closed blocks side by side (block k at x = BX[k]), each running along +z from z = 0 with the floor at y = 0; empties S_<name> / E_<name> mark where you start / stop walking.
view: the open viewing deck (hex frames, floor strips) you stand on to look at the island.   props: backpack, rocks (floating debris), crystals.
All dark navy walls, magenta / pink windows, cyan and white light strips like the recording; geometry only (the plugin lights it)."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 2)
R = np.random.default_rng(12)
def Z(x, y, z): return (x, -z, y)
def panel(base, seed, W=128, H=128, lines=4, trim=None, wear=0.25, dots=None):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); col = np.array(base)[None, None, :] * (0.8 + wear * n[..., None])
    gx = np.abs(((U * lines) % 1) - 0.5); gy = np.abs(((V * lines) % 1) - 0.5); seam = (gx > 0.47) | (gy > 0.47); col = np.where(seam[..., None], col * 0.45, col)
    if trim is not None: col = np.where(((V > 0.9) | (V < 0.03))[..., None], np.array(trim)[None, None, :], col)
    if dots is not None: col = np.where((((U * lines * 4) % 1 < 0.1) & ((V * lines * 4) % 1 < 0.1))[..., None], np.array(dots)[None, None, :], col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def glow(c1, c2, W=64, H=64, hexa=False):
    U, V = grid(W, H); e = np.minimum(np.minimum(U, 1 - U), np.minimum(V, 1 - V)); k = np.clip(1 - e * 5.5, 0, 1) ** 1.5      # brighter towards the frame
    c1 = np.array(c1)[None, None, :]; c2 = np.array(c2)[None, None, :]; col = c1 * (1 - k[..., None]) + c2 * k[..., None]
    if hexa: col = col * (0.82 + 0.18 * np.sin(V * 40)[..., None])
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def swirl(W=128, H=128):
    U, V = grid(W, H); a = 0.5 + 0.5 * np.sin(U * 6.2832 * 3 + V * 18); b = 0.5 + 0.5 * np.sin(V * 40 - U * 6.2832 * 2); k = np.clip(a * 0.6 + b * 0.5, 0, 1)
    return rgba(0.05 + 0.4 * k, 0.45 + 0.5 * k, 0.8 + 0.2 * k, 1.0)

FL = material('SF_Floor', panel((0.09, 0.1, 0.22), 1, lines=6, wear=0.3)); WL = material('SF_Wall', panel((0.07, 0.1, 0.26), 2, lines=3, trim=(0.35, 0.55, 1.0), dots=(0.4, 0.6, 1.0)))
CE = material('SF_Ceil', panel((0.05, 0.07, 0.17), 3, lines=5)); RIB = material('SF_Rib', panel((0.12, 0.14, 0.3), 4, lines=2, trim=(0.7, 0.4, 1.0)))
PINK = material('SF_Pink', glow((0.7, 0.2, 0.7), (1.0, 0.55, 1.0))); PURP = material('SF_Purp', glow((0.45, 0.1, 0.8), (0.85, 0.4, 1.0))); CYA = material('SF_Cyan', glow((0.1, 0.8, 1.0), (0.8, 1.0, 1.0)))
STRIP = solid('SF_Strip', 1.0, 0.85, 1.0); WH = solid('SF_White', 1.0, 1.0, 1.0); BLU = material('SF_BlueGlow', glow((0.1, 0.4, 1.0), (0.5, 0.8, 1.0)))
MET = material('SF_Metal', panel((0.28, 0.3, 0.42), 5, lines=2, wear=0.35)); DK = solid('SF_Dark', 0.03, 0.04, 0.09); BAR = material('SF_Barrel', panel((0.1, 0.32, 0.95), 6, lines=2, trim=(0.9, 0.95, 1.0)))
GRN = material('SF_Teal', glow((0.1, 0.7, 0.55), (0.6, 1.0, 0.8))); SWI = material('SF_Swirl', swirl()); CRY = material('SF_Crystal', glow((0.35, 0.08, 0.7), (0.95, 0.5, 1.0)))
GLS = material('A_Glass', np.zeros((4, 4, 4), np.float32) + np.array([0.3, 0.9, 1.0, 0.35], np.float32)); RED = solid('SF_Red', 0.9, 0.12, 0.1); GRY = solid('SF_Grey', 0.55, 0.55, 0.62); ORN = solid('SF_Orange', 0.95, 0.45, 0.1)
MATS = [FL, WL, CE, RIB, PINK, PURP, CYA, STRIP, WH, BLU, MET, DK, BAR, GRN, SWI, CRY, GLS, RED, GRY, ORN]
(F_, W_, C_, RB_, PK_, PU_, CY_, ST_, WH_, BL_, ME_, DK_, BA_, GR_, SW_, CR_, GL_, RE_, GY_, OR_) = range(len(MATS))

def new(): return MB(MATS)
def bx(b, x, y, z, w, h, d, m, tile=3.0, top=None): b.box(Z(x, y, z), (w, d, h), m, tile, top=top)       # unity axes: centre (x, y, z), width, height, depth
def cy(b, x, y, z, r, h, m, seg=14, axis='y', r2=None): b.cyl(Z(x, y, z), r, h, m, seg, r2=r2, axis={'x': 'x', 'y': 'z', 'z': 'y'}[axis])   # unity axis y = blender z
def room(b, x0, z0, z1, w, h, wm=1, fm=0, cm=2):
    L = z1 - z0; zc = (z0 + z1) / 2
    bx(b, x0, -0.1, zc, w, 0.2, L, fm); bx(b, x0, h + 0.1, zc, w, 0.2, L, cm); bx(b, x0 - w / 2 - 0.1, h / 2, zc, 0.2, h, L, wm); bx(b, x0 + w / 2 + 0.1, h / 2, zc, 0.2, h, L, wm)
def strips(b, x0, z0, z1, w, h, ceil=True):
    for sx in (-1, 1): bx(b, x0 + sx * (w / 2 - 0.35), 0.03, (z0 + z1) / 2, 0.14, 0.05, z1 - z0, ST_, 1.0)
    if ceil: bx(b, x0, h - 0.05, (z0 + z1) / 2, 0.5, 0.06, z1 - z0, WH_, 1.0)
def ribs(b, x0, z0, z1, w, h, every=5.0, depth=0.5):
    z = z0 + every / 2
    while z < z1:
        for sx in (-1, 1): bx(b, x0 + sx * (w / 2 - depth / 2), h / 2, z, depth, h, 0.7, RB_, 1.0)
        bx(b, x0, h - 0.25, z, w, 0.5, 0.7, RB_, 1.0); z += every
def windows(b, x0, z0, z1, w, h, mat, every=5.0, ww=3.2, wh=2.6, y0=1.3, left=True, right=True):
    z = z0 + every / 2
    while z < z1:
        for sx, on in ((-1, left), (1, right)):
            if on: bx(b, x0 + sx * (w / 2 - 0.05), y0 + wh / 2, z, 0.12, wh, ww, mat, 1.0); bx(b, x0 + sx * (w / 2), y0 + wh / 2, z, 0.05, wh + 0.4, ww + 0.4, CY_, 1.0)
        z += every
def marks(name, x, y, z0, z1): empty('S_' + name, None, Z(x, y, z0)); empty('E_' + name, None, Z(x, y, z1))

if kind == 'interior':
    BX = {'CELLS': 0, 'HALL': 60, 'WALK': 130, 'TUBE': 200, 'ROOMS': 240, 'PURPLE': 300, 'BLUE': 380, 'CRYSTAL': 440}
    # ---- CELLS (0:25 - 1:05): a long corridor, pink-lit cells behind hex windows, white floor strips, a blue-lit passage at the far end
    b = new(); x0 = BX['CELLS']; room(b, x0, -4, 60, 8.0, 5.5); strips(b, x0, -4, 60, 8.0, 5.5); ribs(b, x0, -4, 60, 8.0, 5.5, 5.0); windows(b, x0, -4, 60, 8.0, 5.5, PK_, 5.0)
    bx(b, x0, 2.75, -4.1, 8.0, 5.5, 0.2, 1); bx(b, x0, 2.0, 60.2, 8.0, 5.5, 0.2, BL_, 1.0)
    for z in (14, 34, 50): bx(b, x0, 5.0, z, 3.0, 0.3, 0.3, CY_, 1.0)        # cross lights
    b.build('CELLS'); marks('CELLS', x0, 0, 0, 56)
    # ---- HALL (1:05 - 1:55): the larger blue hall, barrels along the walls, a glowing hex door at the end
    b = new(); x0 = BX['HALL']; room(b, x0, -4, 94, 14.0, 8.0); strips(b, x0, -4, 94, 14.0, 8.0); ribs(b, x0, -4, 94, 14.0, 8.0, 6.0, 0.9)
    for sx in (-1, 1):
        for zz in range(8, 90, 9):
            if R.random() < 0.8:
                for k in range(3): cy(b, x0 + sx * (7 - 1.0 - 1.45 * (k % 2)), 0.75, zz + (k // 2) * 1.5 - 0.7, 0.7, 1.5, BA_, 12)
        bx(b, x0 + sx * 6.2, 3.5, 40, 0.9, 0.6, 94, ME_, 2.0)        # a pipe run
    for z in (20, 52, 76): bx(b, x0, 7.3, z, 14.0, 0.5, 0.5, PU_, 1.0)
    bx(b, x0, 4.0, 94.2, 14.0, 8.0, 0.2, 1)
    for k in range(6): a = k / 6 * math.tau; bx(b, x0 + math.cos(a) * 3.4, 3.2 + math.sin(a) * 3.4, 94.0, 1.4, 1.4, 0.5, RB_, 1.0)
    bx(b, x0, 3.2, 94.05, 5.6, 5.6, 0.3, WH_, 1.0); b.build('HALL'); marks('HALL', x0, 0, 0, 90)
    # ---- WALK (1:55 - 2:45): an elevated gantry over a dark void, a room of big windows (a purple symbol on the wall), the hall with the glowing cylinder
    b = new(); x0 = BX['WALK']; W0 = 18.0
    bx(b, x0, -0.1, 20, 5.0, 0.2, 48, F_); bx(b, x0, -14.0, 20, W0, 0.4, 48, DK_); bx(b, x0, 11.0, 20, W0, 0.3, 48, C_)
    for sx in (-1, 1):
        bx(b, x0 + sx * 2.6, 0.6, 20, 0.12, 1.2, 48, ST_, 1.0); bx(b, x0 + sx * (W0 / 2 + 0.1), -2, 20, 0.2, 18, 48, 1)
        for zz in range(-3, 44, 6): bx(b, x0 + sx * 2.6, 0.6, zz, 0.2, 1.4, 0.2, CY_, 1.0); bx(b, x0 + sx * 8.5, -6.0, zz + 3, 1.2, 14, 1.2, RB_, 1.0)
    for z in range(0, 44, 11): bx(b, x0, 6.0, z, W0, 0.6, 0.8, RB_, 1.0); bx(b, x0, 5.6, z, 3.0, 0.2, 0.3, PU_, 1.0)
    z0, z1 = 44.0, 74.0; room(b, x0, z0, z1, 14.0, 7.0); strips(b, x0, z0, z1, 14.0, 7.0); ribs(b, x0, z0, z1, 14.0, 7.0, 7.5, 0.6)
    for sx in (-1, 1):
        for zz in (51.5, 59.0, 66.5): bx(b, x0 + sx * 6.9, 3.8, zz, 0.12, 4.6, 6.0, PK_, 1.0); bx(b, x0 + sx * 6.85, 3.8, zz, 0.06, 5.0, 6.4, CY_, 1.0)
    bx(b, x0 - 6.8, 3.0, 73.0, 0.12, 3.4, 3.4, PU_, 1.0)      # the purple hex symbol
    for k in range(6): a = k / 6 * math.tau; bx(b, x0 - 6.74 + 0.0, 3.0 + math.sin(a) * 1.2, 73.0 + math.cos(a) * 1.2, 0.05, 0.3, 0.3, WH_, 1.0)
    z0, z1 = 74.0, 104.0; room(b, x0, z0, z1, 16.0, 11.0); strips(b, x0, z0, z1, 16.0, 11.0); ribs(b, x0, z0, z1, 16.0, 11.0, 7.5, 0.8)
    cy(b, x0 + 4.2, 4.5, 92.0, 3.0, 9.0, GL_, 20); cy(b, x0 + 4.2, 0.3, 92.0, 3.4, 0.6, ME_, 20); cy(b, x0 + 4.2, 9.2, 92.0, 3.4, 0.6, ME_, 20); cy(b, x0 + 4.2, 4.5, 92.0, 0.9, 9.0, CY_, 12)
    for k in range(4): cy(b, x0 - 5.0, 1.4 + 2.6 * k, 90.0, 1.8 - 0.2 * k, 0.9, ME_, 14)
    bx(b, x0, 5.5, 104.2, 16.0, 11.0, 0.2, 1); bx(b, x0, 3.0, 104.0, 4.0, 5.0, 0.2, BL_, 1.0); b.build('WALK'); marks('WALK', x0, 0, 0, 100)
    # ---- TUBE (2:45 and 3:45): the cyan energy transport tube, rings every 3 m, a bright mouth at both ends
    b = new(); x0 = BX['TUBE']; NS = 20
    def tp(a, z, r): return (x0 + math.cos(a) * r, -z, math.sin(a) * r)       # a point on the tube wall (unity z -> blender -y)
    for i in range(NS):
        a0 = i / NS * math.tau; a1 = (i + 1) / NS * math.tau
        for z in range(-4, 78, 3):
            z2 = z + 3; b.quad(tp(a0, z, 3.4), tp(a1, z, 3.4), tp(a1, z2, 3.4), tp(a0, z2, 3.4), SW_, [(i / NS * 3, z / 6.0), ((i + 1) / NS * 3, z / 6.0), ((i + 1) / NS * 3, z2 / 6.0), (i / NS * 3, z2 / 6.0)])
        for z in range(0, 74, 3): b.quad(tp(a0, z - 0.25, 3.3), tp(a1, z - 0.25, 3.3), tp(a1, z + 0.25, 3.3), tp(a0, z + 0.25, 3.3), CY_, [(0, 0), (1, 0), (1, 1), (0, 1)])       # hollow glowing rings
    cy(b, x0, 0, 77.5, 3.2, 0.4, WH_, 18, axis='z'); cy(b, x0, 0, -4.5, 3.2, 0.4, WH_, 18, axis='z'); b.build('TUBE'); marks('TUBE', x0, -0.9, -2, 72)
    # ---- ROOMS (3:00 - 3:45): the arrival room, a corridor of big windows onto a teal glow, the machinery room with giant rings
    b = new(); x0 = BX['ROOMS']; room(b, x0, -4, 20, 12.0, 6.5); strips(b, x0, -4, 20, 12.0, 6.5); ribs(b, x0, -4, 20, 12.0, 6.5, 6.0, 0.7)
    for k in range(4): cy(b, x0 + (-3.5 + 2.3 * k), 1.1, 14.0, 0.5, 2.2, ME_, 10)
    bx(b, x0, 3.2, -4.1, 12.0, 6.5, 0.2, 1)
    z0, z1 = 20.0, 52.0; room(b, x0, z0, z1, 9.0, 6.0); strips(b, x0, z0, z1, 9.0, 6.0); ribs(b, x0, z0, z1, 9.0, 6.0, 6.5, 0.5); windows(b, x0, z0, z1, 9.0, 6.0, GR_, 6.5, 5.0, 3.6, 1.2, left=True, right=False)
    z0, z1 = 52.0, 86.0; room(b, x0, z0, z1, 26.0, 15.0); strips(b, x0, z0, z1, 26.0, 15.0, False); ribs(b, x0, z0, z1, 26.0, 15.0, 8.5, 1.2)
    for k in range(5): cy(b, x0, 3.0 + k * 2.4, 74.0, 6.5 - 0.5 * k, 1.1, ME_, 22); cy(b, x0, 3.55 + k * 2.4, 74.0, 6.7 - 0.5 * k, 0.12, CY_, 22)
    cy(b, x0, 7.0, 74.0, 1.1, 14.0, CY_, 10); bx(b, x0, 8.5, 64.0, 26.0, 0.5, 0.5, PU_, 1.0)
    bx(b, x0, 7.5, 86.2, 26.0, 15.0, 0.2, 1); bx(b, x0, 3.0, 86.0, 4.0, 5.0, 0.2, BL_, 1.0); b.build('ROOMS'); marks('ROOMS', x0, 0, 0, 80)
    # ---- PURPLE (4:45 - 6:00): a dark round chamber with a glowing magenta pool in the floor and dark pillars (the formation itself is a coded cube)
    b = new(); x0 = BX['PURPLE']; zc = 30.0; Rr = 28.0
    for k in range(20):
        a = k / 20 * math.tau; bx(b, x0 + math.sin(a) * Rr, 11.0, zc + math.cos(a) * Rr, 4.2, 22.0, 4.2, RB_, 2.0)
    bx(b, x0, -0.1, zc, 62.0, 0.2, 62.0, F_, 4.0); bx(b, x0, 22.1, zc, 62.0, 0.2, 62.0, C_, 4.0)
    for k in range(40):
        a = k / 40 * math.tau; bx(b, x0 + math.sin(a) * (Rr + 0.2), 11.0, zc + math.cos(a) * (Rr + 0.2), 4.3, 22.0, 4.3, 1)
    bx(b, x0, 0.4, 1.0, 10.0, 0.8, 4.0, ME_, 2.0); b.build('PURPLE'); marks('PURPLE', x0, 0, 2, 16)
    pb = new(); cy(pb, x0, 0.04, zc, 12.0, 0.1, PU_, 36); cy(pb, x0, 0.12, zc, 12.8, 0.1, RB_, 36, r2=12.0); cy(pb, x0, 0.16, zc, 8.0, 0.06, PK_, 28)
    for r_ in (14.0, 17.0, 20.0): cy(pb, x0, 0.03, zc, r_, 0.05, ST_, 36, r2=r_ - 0.25)
    pb.build('POOL')
    # ---- BLUE (6:50 - 7:55 and 8:15): a dark hall with a big doorway; the blue wall (a coded cube) stands in front of it
    b = new(); x0 = BX['BLUE']; room(b, x0, -4, 52, 26.0, 16.0); strips(b, x0, -4, 52, 26.0, 16.0, False); ribs(b, x0, -4, 52, 26.0, 16.0, 8.0, 1.2)
    bx(b, x0, 8.0, 52.2, 26.0, 16.0, 0.2, 1)
    for sx in (-1, 1): bx(b, x0 + sx * 6.3, 5.0, 43.5, 0.8, 10.0, 1.2, RB_, 1.0)
    bx(b, x0, 10.2, 43.5, 13.6, 0.8, 1.2, RB_, 1.0); bx(b, x0, 8.0, 43.4, 10.0, 0.18, 0.3, BL_, 1.0)
    bx(b, x0, 8.0, -4.1, 26.0, 16.0, 0.2, 1); b.build('BLUE'); marks('BLUE', x0, 0, 2, 26)
    # ---- CRYSTAL (8:30 - 9:11): the purple crystal hall: rows of hex crystal columns, low lamps on plinths, the Cube's plinth at the far end
    b = new(); x0 = BX['CRYSTAL']; room(b, x0, -4, 76, 40.0, 24.0, wm=1); strips(b, x0, -4, 76, 40.0, 24.0, False)
    for k in range(10):
        z = 4 + k * 7.5
        for sx in (-1, 1): cy(b, x0 + sx * 11.0, 12.0, z, 1.6, 24.0, CR_, 6); cy(b, x0 + sx * 11.0, 0.7, z, 2.4, 1.4, RB_, 6); cy(b, x0 + sx * 11.0, 23.4, z, 2.4, 1.2, RB_, 6)
    for k in range(14):
        z = 3 + k * 5.2
        for sx in (-1, 1):
            if R.random() < 0.7:
                px = x0 + sx * (6.0 + R.random() * 3.5); cy(b, px, 0.55, z + R.random() * 2, 0.35, 1.1, RB_, 6); cy(b, px, 1.2, z, 0.25, 0.25, PU_, 6)
    for k in range(18):
        sx = -1 if k % 2 else 1; px = x0 + sx * (15 + R.random() * 3.5); z = R.random() * 74; h = 0.6 + R.random() * 2.2; cy(b, px, h / 2, z, 0.5 + R.random() * 0.4, h, CR_, 5, r2=0.04)
    bx(b, x0, 0.5, 64.0, 9.0, 1.0, 9.0, RB_, 2.0); bx(b, x0, 1.03, 64.0, 7.5, 0.06, 7.5, PU_, 1.0); bx(b, x0, 12.0, 76.2, 40.0, 24.0, 0.2, 1); bx(b, x0, 0.4, -3.9, 40.0, 0.8, 0.2, 1)
    b.build('CRYSTAL'); marks('CRYSTAL', x0, 0, 0, 40)
    export(out)

elif kind == 'view':
    # the viewing deck (4:05): a floor with light strips, big angled hex frames either side and a lintel; the front is open: the island hangs in front of you
    b = new(); bx(b, 0, -0.15, -4, 40.0, 0.3, 28.0, F_, 4.0); bx(b, 0, 0.03, 8.0, 40.0, 0.06, 0.3, ST_, 1.0); bx(b, 0, 0.03, -18.0, 40.0, 0.06, 0.3, ST_, 1.0)
    for sx in (-1, 1): bx(b, sx * 19.7, 0.03, -4, 0.14, 0.05, 26.0, ST_, 1.0)
    for k in range(9): bx(b, -16 + 4 * k, 0.04, -4, 0.1, 0.04, 24.0, PU_, 1.0)
    bx(b, 0, 10.0, -18.2, 40.0, 20.0, 0.4, 1); bx(b, 0, 3.0, -18.0, 4.0, 6.0, 0.3, BL_, 1.0)         # the back wall with the door we came through
    for sx in (-1, 1):
        bx(b, sx * 17.5, 8.0, 7.0, 5.0, 16.0, 2.2, RB_, 2.0); bx(b, sx * 14.6, 8.0, 7.0, 0.8, 14.0, 1.6, PK_, 1.0); bx(b, sx * 19.0, 8.0, -4, 1.4, 16.0, 26.0, 1, 2.0)
        bx(b, sx * 13.0, 15.5, 7.0, 9.0, 1.2, 2.2, RB_, 1.0)
    bx(b, 0, 16.6, 7.0, 36.0, 1.4, 2.4, RB_, 2.0); bx(b, 0, 16.0, 6.9, 34.0, 0.3, 0.3, CY_, 1.0); bx(b, 0, 18.2, -4, 40.0, 0.4, 28.0, C_, 4.0)
    for z in (-12, -4): bx(b, 0, 17.8, z, 40.0, 0.2, 0.4, WH_, 1.0)
    for sx in (-1, 1): bx(b, sx * 8.0, 0.7, 5.0, 1.2, 1.4, 1.2, ME_, 1.0); bx(b, sx * 8.0, 1.45, 5.0, 0.8, 0.12, 0.8, PK_, 1.0)
    b.build('View'); marks('VIEW', 0, 0, -14, 2); export(out)

elif kind == 'props':
    # the backpack bomb (0.36 x 0.46 x 0.2, origin at the middle of the back), floating rocks and crystals
    b = new(); bx(b, 0, 0, 0, 0.36, 0.46, 0.2, BL_, 1.0); bx(b, 0, 0.0, -0.11, 0.26, 0.34, 0.03, GY_, 1.0); bx(b, 0, 0.0, -0.13, 0.2, 0.26, 0.02, RE_, 1.0)
    bx(b, 0, 0.26, 0.0, 0.2, 0.06, 0.12, RE_, 1.0); bx(b, -0.12, 0.0, 0.11, 0.05, 0.4, 0.03, GY_, 1.0); bx(b, 0.12, 0.0, 0.11, 0.05, 0.4, 0.03, GY_, 1.0); bx(b, 0, -0.2, -0.1, 0.3, 0.06, 0.06, CY_, 1.0)
    b.build('Backpack')
    ROCK = material('Rock', panel((0.34, 0.28, 0.36), 21, lines=2, wear=0.5)); TOP = material('Grass', panel((0.2, 0.42, 0.28), 22, lines=2, wear=0.4))
    for i, (r_, sd) in enumerate(((1.0, 3), (1.0, 7), (1.0, 11))):
        bm = displace_icosphere(r_, 3, sd, 0.28, (1.0, 0.55, 1.0)); me = bpy.data.meshes.new('rk'); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new('Rock%d' % (i + 1), me); bpy.context.collection.objects.link(o); o.data.materials.append(ROCK); o.data.materials.append(TOP)
        for p in me.polygons: p.material_index = 1 if p.center.z > 0.28 else 0; p.use_smooth = False
    export(out)
