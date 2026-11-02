"""blender -b -P gx_world.py -- <galactus|corridor|office> <out.glb>
GALACTUS event props (Chapter 2 Season 4 finale, 1 Dec 2020). Unity axes: x right, y up, z forward (Blender here: (x, -z, y)); glowing materials SF_G_*, translucent SF_A_*, the rest lit.
galactus: ~130 m figure facing +z, a node hierarchy the plugin poses: Gx > Torso (Core, Mouth) > Head (EyeL, EyeR) / ShL > ElL > HdL > FgL0-3 > FkL0-3, ThL (mirrored R) / LegL, LegR. Pivot of every joint is its node origin.
corridor: one 60 m red-lit tube section (z 0..60) of the inside of Galactus.  office: the small room where Agent Jones lies (10:05)."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 2)
def Z(x, y, z): return (x, -z, y)
def Zv(v): return (v[0], -v[2], v[1])
def gradtex(name, W, H, f):
    U, V = grid(W, H); r, g, b, a = f(U, V); return material(name, rgba(np.clip(r, 0, 1), np.clip(g, 0, 1), np.clip(b, 0, 1), np.clip(a, 0, 1)))
def flat(name, r, g, bb): return gradtex(name, 8, 8, lambda U, V: (r + 0 * U, g + 0 * U, bb + 0 * U, 1 + 0 * U))
def noisy(name, base, seed, k=0.35):
    U, V = grid(64, 64); n = fbm(64, 64, 5, seed); seam = ((np.abs(((U * 4) % 1) - 0.5) > 0.47) | (np.abs(((V * 4) % 1) - 0.5) > 0.47)) * 0.35
    c = [x * (0.8 + k * n) * (1 - seam) for x in base]; return material(name, rgba(np.clip(c[0], 0, 1), np.clip(c[1], 0, 1), np.clip(c[2], 0, 1), 1.0))
def obox(b, c, s, rot, mat):
    """rotated box: centre c, size s, rotation (rx, ry, rz) radians about the Unity axes"""
    rx, ry, rz = rot; cx, sx_ = math.cos(rx), math.sin(rx); cy, sy_ = math.cos(ry), math.sin(ry); cz, sz_ = math.cos(rz), math.sin(rz)
    def R(p):
        x, y, z = p; y, z = y * cx - z * sx_, y * sx_ + z * cx; x, z = x * cy + z * sy_, -x * sy_ + z * cy; x, y = x * cz - y * sz_, x * sz_ + y * cz; return (x + c[0], y + c[1], z + c[2])
    h = [s[0] / 2, s[1] / 2, s[2] / 2]; P = {}
    for i in (0, 1):
        for j in (0, 1):
            for k in (0, 1): P[(i, j, k)] = Zv(R(((i * 2 - 1) * h[0], (j * 2 - 1) * h[1], (k * 2 - 1) * h[2])))
    uv = [(0, 0), (1, 0), (1, 1), (0, 1)]
    for q in (((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)), ((1, 0, 1), (0, 0, 1), (0, 1, 1), (1, 1, 1)), ((0, 0, 1), (0, 0, 0), (0, 1, 0), (0, 1, 1)), ((1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)), ((0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)), ((0, 0, 1), (1, 0, 1), (1, 0, 0), (0, 0, 0))):
        b.quad(P[q[0]], P[q[1]], P[q[2]], P[q[3]], mat, uv)
def tube(b, c, r, h, mat, seg=14, r2=None): b.cyl(Z(*c), r, h, mat, seg, r2=r2, axis='z')       # unity y axis
def part(name, parent, pos, mats, fn):
    b = MB(mats); fn(b); o = b.build(name, parent); o.location = Z(*pos); return o
def node(name, parent, pos): return empty(name, parent, Z(*pos))

if kind == 'galactus':
    AR = noisy('SF_Armor', (0.18, 0.12, 0.38), 3); AR2 = noisy('SF_Armor2', (0.3, 0.2, 0.52), 4); RD = gradtex('SF_G_Red', 8, 32, lambda U, V: (1 + 0 * U, 0.2 + 0.35 * V, 0.12 + 0.1 * V, 1 + 0 * U)); FC = flat('SF_Face', 0.72, 0.5, 0.74)
    EY = flat('SF_G_Eye', 1.0, 0.85, 0.7); WH = flat('SF_G_White', 1, 1, 1); DK = flat('SF_Dark', 0.03, 0.02, 0.08); M = [AR, AR2, RD, FC, EY, WH, DK]; (A_, A2_, R_, F_, E_, W_, D_) = range(7)
    root = node('Gx', None, (0, 0, 0))
    def torso(b):
        b.box(Z(0, 7, 0), (20, 14, 12), A2_, 4.0); b.box(Z(0, 25, 0), (34, 22, 16), A_, 4.0); b.box(Z(0, 36.5, 0), (30, 3, 14), A2_, 4.0); b.box(Z(0, -1.5, 0), (24, 3.4, 13.5), A2_, 4.0); b.box(Z(0, -1.5, 6.9), (12, 1.6, 0.5), R_, 1.0)
        for sx in (-1, 1): b.box(Z(sx * 16.9, 25, 0), (0.4, 18, 14), R_, 1.0); b.box(Z(sx * 10, 25, 8.1), (0.5, 17, 0.4), R_, 1.0)
        for yy in (17.2, 33.0): b.box(Z(0, yy, 8.15), (22, 0.6, 0.4), R_, 1.0)
        for sx in (-1, 1): b.box(Z(sx * 11.2, 25, 8.15), (0.6, 16, 0.4), R_, 1.0); b.box(Z(sx * 5.5, 20.2, 8.15), (11, 0.6, 0.4), R_, 1.0); b.box(Z(sx * 5.5, 29.8, 8.15), (11, 0.6, 0.4), R_, 1.0)
        b.cyl(Z(0, 39, 0), 5.2, 6, A_, 12, axis='z')
    t = part('Torso', root, (0, 0, 0), M, torso); core = part('Core', t, (0, 25, 8.3), M, lambda b: tube(b, (0, 0, 0), 3.3, 0.6, R_, 20)); part('CoreHot', core, (0, 0, 0.4), M, lambda b: tube(b, (0, 0, 0), 1.8, 0.6, W_, 16))
    def head(b):
        b.box(Z(0, 6, 0), (13, 14, 12), A_, 4.0); b.box(Z(0, 5.2, 6.2), (9, 8.5, 0.8), F_, 2.0); b.box(Z(0, -0.4, 5.4), (8, 3.6, 3.6), A2_, 2.0); b.box(Z(0, 14.5, -1), (2.2, 8, 9), A2_, 2.0); b.box(Z(0, 14.5, 3.4), (0.5, 7, 0.3), R_, 1.0)
        for sx in (-1, 1):
            obox(b, (sx * 11.5, 11, -2), (14, 1.8, 8), (0, 0, sx * 0.42), A2_); obox(b, (sx * 11.5, 11.9, -2), (13.6, 0.4, 7.6), (0, 0, sx * 0.42), R_); b.box(Z(sx * 6.6, 6, 6.4), (0.5, 12, 0.4), R_, 1.0); obox(b, (sx * 7.5, 15.5, -1), (1.4, 9, 6), (0, 0, sx * -0.35), A_)
    h = part('Head', t, (0, 40, 0), M, head)
    for sx, nm in ((-1, 'EyeL'), (1, 'EyeR')): part(nm, h, (sx * 2.5, 6.6, 6.7), M, lambda b: b.box(Z(0, 0, 0), (3.2, 0.9, 0.5), E_, 1.0))
    part('Mouth', h, (0, 2.0, 6.7), M, lambda b: b.box(Z(0, 0, 0), (4.5, 1.1, 0.5), W_, 1.0))
    def arm(sg, s):
        sh = node('Sh' + s, t, (sg * 19.5, 33, 0)); part('Pauld' + s, sh, (0, 0, 0), M, lambda b: (b.box(Z(sg * 1.5, 2.6, 0), (14, 9, 17), A_, 4.0), b.box(Z(sg * 1.5, 7.3, 0), (13, 0.5, 15), R_, 1.0), b.box(Z(sg * 8.6, 2.6, 0), (0.4, 7, 14), R_, 1.0)))
        part('Up' + s, sh, (0, 0, 0), M, lambda b: (tube(b, (0, -11, 0), 4.5, 22, A_, 14), tube(b, (0, -4, 0), 5.0, 1.0, R_, 14)))
        el = node('El' + s, sh, (0, -22, 0)); part('Lo' + s, el, (0, 0, 0), M, lambda b: (tube(b, (0, -9, 0), 3.9, 18, A_, 14), b.box(Z(0, -14, 0), (9.6, 10, 9.6), A2_, 3.0), tube(b, (0, -8.4, 0), 5.4, 0.7, R_, 14), b.box(Z(0, -14, 4.9), (4, 6, 0.3), R_, 1.0)))
        hd = node('Hd' + s, el, (0, -20, 0)); part('Palm' + s, hd, (0, 0, 0), M, lambda b: b.box(Z(0, -4.5, 0), (8.6, 8.2, 3.4), A_, 2.0))
        for i in range(4):
            fg = node('Fg%s%d' % (s, i), hd, (-3.0 + i * 2.0, -8.6, 0)); part('Fa%s%d' % (s, i), fg, (0, 0, 0), M, lambda b, i=i: b.box(Z(0, -2.8, 0), (1.8, 5.8, 2.2), A2_, 1.0))
            fk = node('Fk%s%d' % (s, i), fg, (0, -5.6, 0)); part('Fb%s%d' % (s, i), fk, (0, 0, 0), M, lambda b, i=i: b.box(Z(0, -2.6, 0), (1.6, 5.2, 2.0), A2_, 1.0))
        th = node('Th' + s, hd, (sg * 4.4, -3.2, 0)); part('Tb' + s, th, (0, 0, 0), M, lambda b: obox(b, (sg * 1.4, -2.2, 0), (2.0, 5.6, 2.4), (0, 0, sg * 0.5), A2_))
    arm(-1, 'L'); arm(1, 'R')
    for sg, s in ((-1, 'L'), (1, 'R')): part('Leg' + s, root, (sg * 8.2, -2, 0), M, lambda b, sg=sg: (tube(b, (0, -34, 0), 7.4, 68, A_, 14), tube(b, (0, -20, 0), 7.9, 1.2, R_, 14), tube(b, (0, -46, 0), 8.0, 4, A2_, 14)))
    export(out)

elif kind == 'corridor':
    DK = flat('SF_Dark', 0.05, 0.04, 0.1); WL = noisy('SF_Wall', (0.2, 0.12, 0.3), 5, 0.5); WL2 = noisy('SF_Wall2', (0.32, 0.18, 0.28), 6, 0.5); RD = gradtex('SF_G_Red', 8, 32, lambda U, V: (1 + 0 * U, 0.2 + 0.35 * V, 0.12 + 0.1 * V, 1 + 0 * U))
    OR = flat('SF_G_Orange', 1.0, 0.55, 0.15); GR = flat('SF_G_Green', 0.3, 1.0, 0.45); BL = flat('SF_G_Blue', 0.35, 0.6, 1.0); M = [DK, WL, WL2, RD, OR, GR, BL]; (D_, W_, W2_, R_, O_, G_, B_) = range(7)
    rr = np.random.default_rng(7); b = MB(M); L = 60.0
    b.box(Z(0, -0.5, L / 2), (26, 1, L), D_, 4.0); b.box(Z(0, 18.5, L / 2), (26, 1, L), W_, 4.0)
    for sx in (-1, 1):
        b.box(Z(sx * 13.5, 9, L / 2), (1, 18, L), W2_, 4.0); b.box(Z(sx * 12.9, 2.2, L / 2), (0.3, 0.5, L), R_, 1.0); b.box(Z(sx * 12.9, 15.8, L / 2), (0.3, 0.5, L), R_, 1.0)
    b.box(Z(0, 0.06, L / 2), (1.6, 0.12, L), G_, 1.0)
    for k in range(6):
        z = 5 + k * 10
        for sx in (-1, 1): b.box(Z(sx * 12.4, 9, z), (0.8, 18, 1.4), W_, 2.0); b.box(Z(sx * 11.9, 9, z), (0.3, 16, 0.3), R_, 1.0)
        b.box(Z(0, 17.6, z), (26, 0.8, 1.4), W_, 2.0); b.box(Z(0, 17.1, z), (22, 0.3, 0.3), R_, 1.0)
    for k in range(40):
        sx = -1 if k % 2 else 1; z = rr.random() * L; h = 2 + rr.random() * 9; w = 1.2 + rr.random() * 3; d = 1.5 + rr.random() * 4
        b.box(Z(sx * (11.4 - d / 2), h / 2 + 0.4, z), (d, h, w), (W_ if k % 3 else W2_), 2.0); b.box(Z(sx * (11.4 - d), h * 0.5 + 0.4, z), (0.2, 0.5, w * 0.6), (O_ if k % 4 == 0 else R_), 1.0)
    b.build('Hall')
    bb = MB(M)
    for k in range(3): z = 8 + k * 22 + rr.random() * 6; x = (rr.random() - 0.5) * 16; bb.box(Z(x, 9, z), (0.7, 18, 0.7), O_, 1.0)
    bb.build('Beams')
    export(out)

elif kind == 'office':
    FL = noisy('SF_Floor', (0.18, 0.1, 0.1), 8); WL = noisy('SF_Wall', (0.3, 0.14, 0.12), 9); WD = noisy('SF_Wood', (0.4, 0.22, 0.14), 10); RD = flat('SF_G_Red', 1.0, 0.18, 0.1); SC = flat('SF_G_Screen', 0.4, 0.7, 1.0); DK = flat('SF_Dark', 0.05, 0.04, 0.05)
    M = [FL, WL, WD, RD, SC, DK]; (F_, W_, D_, R_, S_, K_) = range(6); b = MB(M)
    b.box(Z(0, -0.1, 0), (14, 0.2, 12), F_, 3.0); b.box(Z(0, 3.2, 0), (14, 0.2, 12), W_, 3.0)
    for (x, z, w, d) in ((0, 6.1, 14.4, 0.2), (0, -6.1, 14.4, 0.2), (7.1, 0, 0.2, 12), (-7.1, 0, 0.2, 12)): b.box(Z(x, 1.6, z), (w, 3.4, d), W_, 3.0)
    b.box(Z(0, 0.9, 4.2), (5.0, 0.12, 1.8), D_, 2.0); b.box(Z(-2.2, 0.45, 4.2), (0.3, 0.9, 1.6), D_, 2.0); b.box(Z(2.2, 0.45, 4.2), (0.3, 0.9, 1.6), D_, 2.0)
    b.box(Z(-0.9, 1.45, 4.3), (1.4, 0.9, 0.08), S_, 1.0); b.box(Z(1.0, 1.4, 4.3), (1.1, 0.7, 0.08), S_, 1.0); b.box(Z(0, 1.7, 5.9), (6.0, 0.15, 0.1), R_, 1.0); b.box(Z(0, 3.0, 0), (10, 0.12, 0.3), R_, 1.0)
    b.box(Z(-4.5, 0.5, -3), (1.2, 1.0, 1.2), K_, 2.0); b.box(Z(4.8, 1.0, -4.5), (1.5, 2.0, 0.8), D_, 2.0)
    b.build('Office'); empty('S_FLOOR', None, Z(0, 0, 0))
    export(out)
print('done', kind)
