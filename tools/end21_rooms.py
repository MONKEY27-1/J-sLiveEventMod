"""blender -b -P end21_rooms.py -- <facility|chamber> <out.glb>
THE END part 2 rooms.  Unity local: x right, z forward (the player looks +z), y up.  Blender here: (x, -z, y).
facility: the I.O. facility room, 24 x 18 x 7 m, floor at 0, centre at the origin; front wall (z = -9) has the blast door (separate DoorL / DoorR objects), console wall at z = +9 with screens, a gyro machine (Gyro: three turning rings + a pulsing core) at x = -7.5, the gyro control stand with a lever at x = +7.8, an interrogation table.
chamber: the Zero Point chamber, a round steel hall (radius 16, 14 high) with a ring catwalk round a glass tube; the wall at +z is broken open (the giant caretaker comes through)."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 2)
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
def Z(x, y, z): return (x, -z, y)
if kind == 'facility':
    FL = material('Floor', panel((0.16, 0.17, 0.2), 1, lines=10)); WL = material('Wall', panel((0.2, 0.22, 0.27), 2, lines=5, stripe=(0.8, 0.2, 0.15))); CE = material('Ceil', panel((0.12, 0.13, 0.16), 3, lines=8)); RED = solid('Red', 1.0, 0.18, 0.12); AMB = solid('Amber', 1.0, 0.6, 0.15)
    GRN = material('Green', screen_tex(np.array([0.3, 1, 0.5]), np.array([0.2, 0.8, 1.0]), 1)); BLU = material('Blue', screen_tex(np.array([0.3, 0.8, 1.0]), np.array([0.6, 0.5, 1.0]), 2)); ORG = material('Orange', screen_tex(np.array([1.0, 0.6, 0.15]), np.array([1.0, 0.85, 0.3]), 3))
    STE = material('Steel', panel((0.5, 0.53, 0.6), 4, lines=2)); DK = material('Dark', panel((0.08, 0.09, 0.12), 5, lines=8)); WH = solid('White', 0.95, 0.95, 1.0); CYA = solid('Cyan', 0.3, 0.95, 1.0)
    W_ = 24.0
    def ecg_tex(W=256, H=128):                       # a heart-rate trace on a dark screen: the vitals computer
        img = np.zeros((H, W, 4), np.float32); img[..., :3] = (0.02, 0.05, 0.06); img[..., 3] = 1
        for x in range(W):
            ph = (x % 64) / 64.0; y = 0.5 + (0.0 if ph < 0.55 else 0.0)
            if 0.55 <= ph < 0.60: y = 0.5 + 0.35 * (ph - 0.55) / 0.05
            elif 0.60 <= ph < 0.66: y = 0.85 - 1.0 * (ph - 0.60) / 0.06
            elif 0.66 <= ph < 0.72: y = -0.15 + 0.65 * (ph - 0.66) / 0.06
            yy = int(np.clip(y, 0.02, 0.98) * (H - 1)); img[max(yy - 1, 0):yy + 2, x, :3] = (0.3, 1.0, 0.55)
        img[:, ::32, :3] += 0.04; return img
    ECG = material('ECG', ecg_tex())
    b = MB([FL, WL, CE, RED, AMB, GRN, BLU, ORG, STE, DK, WH, ECG, CYA]); F_, WLL, C_, R_, A_, G_, B_, O_, S_, D_, H_, E_, Y_ = range(13)
    def bx(x, y, z, w, h, d, m, tile=3.0, top=None): b.box(Z(x, y, z), (w, d, h), m, tile, top=top)          # unity axes: width (x), height (y), depth (z)
    bx(0, -0.1, 0, 24, 0.2, 18, F_); bx(0, 7.1, 0, 24, 0.2, 18, C_)
    bx(0, 3.5, 9.1, 24.4, 7, 0.2, WLL); bx(-12.1, 3.5, 0, 0.2, 7, 18, WLL)
    bx(12.1, 3.5, -7.1, 0.2, 7, 3.8, WLL); bx(12.1, 3.5, 3.9, 0.2, 7, 10.2, WLL); bx(12.1, 5.2, -4.0, 0.2, 3.6, 2.4, WLL)      # the east wall with the security door gap (z -5.2 .. -2.8, 3.4 high)
    for z in (-5.3, -2.7): bx(12.0, 1.7, z, 0.3, 3.4, 0.2, R_, 1.0)
    bx(12.0, 3.45, -4.0, 0.3, 0.2, 2.8, R_, 1.0)
    bx(-7.6, 3.5, -9.1, 9.2, 7, 0.2, WLL); bx(7.6, 3.5, -9.1, 9.2, 7, 0.2, WLL); bx(0, 5.85, -9.1, 6.0, 2.3, 0.2, WLL)
    for x in (-3.15, 3.15): bx(x, 2.3, -9.0, 0.3, 4.6, 0.5, R_, 1.0)
    bx(0, 4.7, -9.0, 6.6, 0.3, 0.5, R_, 1.0)
    for z in (-6, -2, 2, 6):
        bx(0, 6.95, z, 16, 0.25, 0.5, H_, 1.0); bx(-11.95, 5.2, z, 0.1, 0.25, 4.0, R_, 1.0); bx(11.95, 5.2, z, 0.1, 0.25, 4.0, R_, 1.0)
    for x in (-8, -4, 4, 8): b.cyl(Z(x, 3.5, 8.4), 0.28, 7.0, S_, 10)
    bx(0, 0.55, 7.6, 14, 1.1, 1.4, D_, 2.0, top=S_)
    for x, m in ((-4.5, G_), (0, B_), (4.5, O_)): bx(x, 1.5, 7.55, 3.6, 0.9, 0.12, m, 1.0)
    bx(0, 4.2, 8.88, 11.6, 4.0, 0.1, D_, 1.0); bx(0, 4.2, 8.95, 11.0, 3.4, 0.12, B_, 1.0)
    for x, m in ((-4.5, G_), (0, B_), (4.5, O_)): bx(x, 2.4, 8.8, 3.6, 1.4, 0.1, m, 1.0)
    # lab racks along the west wall
    for k in range(5):
        z = -6.4 + k * 3.2; bx(-11.0, 1.3, z, 1.4, 2.6, 1.8, D_, 1.0); bx(-10.28, 1.8, z, 0.06, 1.7, 1.4, [G_, B_, O_, G_, B_][k], 1.0); bx(-11.0, 2.7, z, 1.5, 0.12, 1.9, A_ if k % 2 else Y_, 1.0)
    for x, z, h in ((10.5, 6.5, 1.0), (10.5, 4.8, 1.0), (10.5, 5.7, 2.1)): bx(x, h / 2, z, 1.4, h, 1.4, S_ if h < 2 else WLL, 1.0)
    for z in (7.5, 6.0): b.cyl(Z(10.9, 0.6, z), 0.5, 1.2, A_, 10)
    # ---- Jones's scanner chair: Jones sits reclined facing the door (-z); seat top 0.44, back leans away from him (+z)
    bx(0, 0.22, 3.05, 0.9, 0.44, 0.9, D_, 1.0, top=S_)                                    # seat block
    for sx in (-0.5, 0.5): bx(sx, 0.62, 3.0, 0.1, 0.1, 0.7, D_, 1.0)                    # armrests
    bx(0, 0.02, 3.05, 1.3, 0.04, 1.6, A_, 1.0)                                              # a hazard-yellow floor plate
    bk = MB([DK, STE, CYA]); bk.box((0, 0, 0.55), (0.9, 0.12, 1.1), 0, 1.0, top=1); bk.box((0, 0.0, 1.18), (0.34, 0.16, 0.26), 1, 1.0); o = bk.build('ChairBack'); o.location = Z(0, 0.44, 3.4); o.rotation_euler = (math.radians(24), 0, 0)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.44, minor_radius=0.032, major_segments=40, minor_segments=8); ring = bpy.context.active_object; ring.name = 'ScanRing'; ring.data.materials.append(CYA); ring.location = Z(0, 1.38, 3.55); ring.rotation_euler = (math.radians(24), 0, 0)
    arm = MB([STE, DK]); arm.box((0, 0, 0), (0.1, 0.1, 1.5), 0, 1.0); arm.box((0, -0.35, 0.72), (0.1, 0.8, 0.1), 0, 1.0); oa = arm.build('ScanArm'); oa.location = Z(0, 1.1, 3.7); oa.rotation_euler = (math.radians(24), 0, 0)
    # ---- the vitals computer: Gunnar works at it; the Foundation slams him into it at 5:12
    bx(2.2, 0.5, 3.0, 0.9, 1.0, 1.7, D_, 1.0, top=S_); bx(2.2, 1.02, 3.0, 0.8, 0.06, 1.5, S_, 1.0)
    bx(1.78, 1.5, 2.45, 0.06, 0.62, 0.8, E_, 1.0); bx(1.78, 1.5, 3.55, 0.06, 0.62, 0.8, B_, 1.0); bx(1.74, 1.5, 3.0, 0.04, 0.5, 0.4, G_, 1.0)
    bx(1.9, 1.15, 2.2, 0.5, 0.06, 0.4, O_, 1.0); bx(2.5, 1.9, 3.0, 0.12, 1.8, 1.9, D_, 1.0)
    bx(0.0, 1.0, -0.1, 0.02, 0.02, 0.02, D_, 1.0)
    # crates
    for x, z, h in ((-10.5, -6.5, 1.0), (-10.5, -4.8, 1.0), (-10.5, -5.7, 2.1)): bx(x, h / 2, z - 2.5, 1.4, h, 1.4, S_ if h < 2 else WLL, 1.0)
    bx(0, 0.01, -8.0, 6.4, 0.04, 2.0, A_, 1.0)
    b.build('Facility')
    for name, sx in (('DoorL', -1.5), ('DoorR', 1.5)):
        d = MB([DK, RED]); d.box(Z(sx, 2.2, -9.0), (3.0, 0.35, 4.4), 0, 1.0); d.box(Z(sx, 3.3, -8.8), (2.4, 0.05, 0.2), 1, 1.0); d.build(name)
    d = MB([DK, RED, CYA]); d.box(Z(12.0, 1.7, -4.0), (0.3, 2.4, 3.4), 0, 1.0); d.box(Z(11.8, 3.0, -4.0), (0.05, 1.6, 0.2), 1, 1.0); d.box(Z(11.8, 1.7, -4.0), (0.05, 0.05, 2.2), 2, 1.0); d.build('DoorS')
    export(out)
elif kind == 'chamber':
    WL = material('Wall', panel((0.13, 0.2, 0.3), 6, lines=6)); FL = material('Floor', panel((0.1, 0.13, 0.19), 7, lines=10)); STE = material('Steel', panel((0.5, 0.55, 0.65), 8, lines=2)); GLS = material('A_Glass', tex_glass(0.28)); CY = solid('Cyan', 0.3, 0.9, 1.0); RED = solid('Red', 1.0, 0.2, 0.15)
    b = MB([WL, FL, STE, GLS, CY, RED]); W_, F_, S_, G_, C_, R_ = range(6)
    b.cyl(Z(0, -0.1, 0), 16.0, 0.2, F_, 40, axis='z'); b.cyl(Z(0, 14.1, 0), 16.0, 0.2, F_, 40, axis='z')
    for k in range(36):
        a = (k + 0.5) * 2 * math.pi / 36; x, z = math.cos(a) * 16.0, math.sin(a) * 16.0
        if 70 < math.degrees(a) < 110: continue
        b.box(Z(x, 7.0, z), (1.9, 0.4, 14), W_, 2.0)
    b.cyl(Z(0, 0.3, 0), 4.6, 0.6, S_, 24, axis='z'); b.cyl(Z(0, 7.0, 0), 3.0, 14.0, G_, 24, axis='z'); b.cyl(Z(0, 14.0, 0), 3.4, 0.4, S_, 24, axis='z')
    for k in range(10): a = k * 2 * math.pi / 10; b.box(Z(math.cos(a) * 3.4, 7.0, math.sin(a) * 3.4), (0.3, 14, 0.3), C_, 1.0)
    for k in range(14):
        a = k * 2 * math.pi / 14; x, z = math.cos(a) * 9.5, math.sin(a) * 9.5; b.box(Z(x, 0.2, z), (1.9, 4.4, 0.3), S_, 2.0)
        b.cyl(Z(math.cos(a) * 11.6, 0.9, math.sin(a) * 11.6), 0.07, 1.4, S_, 6, axis='z')
    for k in range(8): a = k * 2 * math.pi / 8 + 0.2; b.box(Z(math.cos(a) * 14.5, 12.5, math.sin(a) * 14.5), (1.8, 0.2, 0.2), R_, 1.0)
    o = b.build('Chamber'); keys(o, 'rotation_euler', lambda t: (0, 0, 0), 24, 2, 2); export(out)
