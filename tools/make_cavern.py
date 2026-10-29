"""blender -b -P make_cavern.py -- <out.glb>
The Collision underground, version 3 (after the video): a big dark pit (ridged wall ~205 m, rubble floor 215 m) with the ROUND Collider in the middle (the real Collider model, placed by the plugin at scale 2) and a SQUARE staircase
that climbs round it: from the middle of the south side east, then round and round (9 sides: 60 m, then 7 x 120 m), 130 m rise, a landing at every corner, 1100 real treads with glowing nosings, handrails, trusses, rock supports; tripod amber lamp panels,
crates and barrels on the landings, two rope bridges to square ledges round the tower, tall dark columns with rows of amber dots, the wrecked mech's giant hand and head, broken slabs, rubble. Same stair numbers as AstronomicalCollision2.cs Stair().
Unity pos = (x, y, z); Blender = (-x, -z, y). Animated: glow rings round the tower, lightning ribbons, swinging lanterns."""
import bpy, sys, math, os, bmesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1:][0]; setup(24, 8); rr = np.random.default_rng(7)
UV4 = [(0, 0), (1, 0), (1, 1), (0, 1)]; SQ = 60.0; SQP = [(0, -SQ), (SQ, -SQ), (SQ, SQ), (-SQ, SQ), (-SQ, -SQ), (SQ, -SQ), (SQ, SQ), (-SQ, SQ), (-SQ, -SQ)]; TOTAL = 60 + 7 * 120.0
def stair(s):
    s = min(1.0, max(0.0, s)); L = s * TOTAL; i = 0; acc = 0.0
    while i < len(SQP) - 2:
        seg = 60.0 if i == 0 else 120.0
        if L <= acc + seg: break
        acc += seg; i += 1
    ln = 60.0 if i == 0 else 120.0; u = min(1.0, max(0.0, (L - acc) / ln)); x = SQP[i][0] + (SQP[i + 1][0] - SQP[i][0]) * u; z = SQP[i][1] + (SQP[i + 1][1] - SQP[i][1]) * u
    return np.array([x, 4 + 130 * s, z])
def B(p): return (float(-p[0]), float(-p[2]), float(p[1]))
def panel_tex(base, seed, W=256, H=256, lines=6, stripe=None, wear=0.35):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); col = np.array(base)[None, None, :] * (0.78 + wear * n[..., None]); gx = np.abs(((U * lines) % 1) - 0.5); gy = np.abs(((V * lines) % 1) - 0.5); col = np.where(((gx > 0.485) | (gy > 0.485))[..., None], col * 0.55, col)
    if stripe is not None: col = np.where(((((U + V) * 14) % 1 < 0.5) & (V > 0.86))[..., None], np.array(stripe)[None, None, :], col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def rock_tex(seed, tint=(1, 1, 1), glow=0.0):
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 6, seed); strata = 0.5 + 0.5 * np.sin(V * 2 * np.pi * 7 + n * 5); crack = np.exp(-((n - 0.5) / 0.03) ** 2)
    c = np.stack([0.15 + 0.14 * strata, 0.15 + 0.13 * strata, 0.27 + 0.2 * strata], -1) * np.array(tint)[None, None, :] + crack[..., None] * np.array((0.55, 0.25, 0.95))[None, None, :] * glow
    return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
def tread_tex():
    W = H = 128; U, V = grid(W, H); n = fbm(W, H, 8, 31); c = (0.34 + 0.12 * n)[..., None] * np.array((1.0, 0.98, 1.1))[None, None, :]; edge = (V < 0.08) | (V > 0.92); c = np.where(edge[..., None], c * 0.7, c); return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
def grate_tex():
    W = H = 128; U, V = grid(W, H); n = fbm(W, H, 8, 31); c = (0.17 + 0.07 * n)[..., None] * np.array((0.9, 0.95, 1.3))[None, None, :]
    slot = ((((U * 6) % 1) > 0.18) & (((U * 6) % 1) < 0.82) & (((V * 2) % 1) > 0.2) & (((V * 2) % 1) < 0.8)); c = np.where(slot[..., None], c * 0.3, c)
    nos = V > 0.93; c = np.where(nos[..., None], np.array((1.0, 0.3, 0.85))[None, None, :], c); return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
def swirl_tex(a, b, c, seed, alpha=1.0, W=128):
    U, V = grid(W, W); T = np.pi * 2; n = np.sin(T * (U * 3 + 0.25 * np.sin(T * (V * 2 + seed)))) + np.sin(T * (V * 4 + 0.3 * np.sin(T * U * 3 + seed))) + np.sin(T * ((U + V) * 2)); n = n / 3 * 0.5 + 0.5
    a, b, c = (np.array(x)[None, None, :] for x in (a, b, c)); lo = n[..., None] < 0.5; col = np.where(lo, a + (b - a) * (n[..., None] * 2), b + (c - b) * ((n[..., None] - 0.5) * 2)); return rgba(col[..., 0], col[..., 1], col[..., 2], alpha * (0.4 + 0.6 * n))
ROCK = material('Rock', rock_tex(3, (1, 1, 1.05), 0.12)); WALLM = material('RockWall', rock_tex(8, (0.95, 0.95, 1.1), 0.35)); TREAD = material('Tread', grate_tex()); DARK = material('Dark', panel_tex((0.14, 0.15, 0.26), 2, lines=8)); STEEL = material('Steel', panel_tex((0.55, 0.57, 0.66), 5, lines=2))
TOWER = material('Tower', panel_tex((0.2, 0.22, 0.36), 6, lines=10, stripe=(0.7, 0.35, 1.0))); LAMP = solid('Lamp', 1.0, 0.7, 0.2); CRATE = material('Crate', panel_tex((0.7, 0.45, 0.2), 9, lines=3, stripe=(0.15, 0.15, 0.15))); STRIP = solid('Strip', 1.0, 0.25, 0.85); ROPE = solid('Rope', 0.55, 0.42, 0.28); PLANK = material('Plank', panel_tex((0.5, 0.36, 0.22), 12, lines=2)); HAZ = material('Hazard', panel_tex((0.9, 0.7, 0.1), 13, lines=1, stripe=(0.1, 0.1, 0.1)))
WBLUE = material('WreckBlue', panel_tex((0.22, 0.42, 0.72), 41, lines=3, stripe=(0.9, 0.5, 0.15))); WORG = material('WreckOrange', panel_tex((0.85, 0.45, 0.15), 42, lines=2)); GLOWP = material('A_PurpleGlow', swirl_tex((0.4, 0.15, 0.8), (0.8, 0.45, 1.0), (1, 0.95, 1), 0.3, 0.5))
OBS = material('Obsidian', panel_tex((0.12, 0.13, 0.24), 77, lines=5, wear=0.5)); GEN = material('Generator', panel_tex((0.95, 0.75, 0.12), 78, lines=3, stripe=(0.12, 0.12, 0.12))); BARR = material('Barrier', panel_tex((0.3, 0.32, 0.4), 79, lines=2, stripe=(1.0, 0.55, 0.1)))
M = [ROCK, TREAD, DARK, STEEL, TOWER, LAMP, CRATE, STRIP, ROPE, PLANK, HAZ, WBLUE, WORG, OBS, GEN, BARR]; RK, TR, DK, ST, TW, LP, CR, SP, RP, PK, HZ, WB, WO, OB, GN, BR = range(16)
b = MB(M); UPB = np.array((0.0, 0.0, 1.0))
def obox(c, R, F, U, hx, hy, hz, mat):
    c, R, F, U = (np.array(v, float) for v in (c, R, F, U)); P = lambda sx, sy, sz: tuple(c + R * hx * sx + F * hy * sy + U * hz * sz)
    b.quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), mat, UV4); b.quad(P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), P(-1, -1, -1), mat, UV4)
    b.quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), mat, UV4); b.quad(P(-1, 1, -1), P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), mat, UV4)
    b.quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), mat, UV4); b.quad(P(1, 1, -1), P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), mat, UV4)
def frame(p0, p1):
    f = np.array(B(p1)) - np.array(B(p0)); f /= max(1e-6, np.linalg.norm(f)); r = np.cross(f, UPB)
    if np.linalg.norm(r) < 1e-4: r = np.array((1.0, 0, 0))
    r /= np.linalg.norm(r); u = np.cross(r, f); return f, r, u
# ---- the stairs: 1100 flat treads (9 m wide) over a solid sloped core, glowing nosings, handrails + posts + mid rails, trusses, rock supports
NS = 1100; WID = 9.0
for i in range(NS):
    s0, s1 = i / NS, (i + 1) / NS; p0, p1 = stair(s0), stair(s1); 
    if s0 > 0.69 and 46.0 < p0[0] < 75.0 and 33.0 < p0[2] < 71.0: continue
    q0, q1 = np.array(B(p0)), np.array(B(p1)); c = (q0 + q1) / 2; f, r, u = frame(p0, p1); ln = np.linalg.norm(q1 - q0)
    fh = np.array((f[0], f[1], 0.0)); nh = np.linalg.norm(fh)
    if nh < 1e-4: continue
    fh /= nh; rh = np.cross(fh, UPB); rh /= np.linalg.norm(rh); lh = max(0.2, np.linalg.norm(q1[:2] - q0[:2]))
    obox(np.array((c[0], c[1], q0[2] - 0.12)), rh, fh, UPB, WID / 2, lh / 2 * 1.04, 0.14, TR)
    if i % 3 == 0: obox(np.array((c[0], c[1], q0[2] + 0.03)) + fh * (lh / 2), rh, fh, UPB, WID / 2 - 0.15, 0.05, 0.03, SP)
    if i % 6 == 0: obox(c - u * 0.62, r, f, u, WID / 2, ln / 2 * 6.3, 0.34, DK)
    for sgn in (-1, 1):
        if i % 2 == 0: obox(np.array((c[0], c[1], c[2] + 0.55)) + rh * sgn * (WID / 2 - 0.1), rh, fh, UPB, 0.07, lh / 2 * 2.1, 0.04, ST)
        if i % 4 == 0: obox(np.array((c[0], c[1], c[2] + 0.25)) + rh * sgn * (WID / 2 - 0.1), rh, fh, UPB, 0.06, 0.06, 0.55, ST)
        if i % 4 == 2: obox(np.array((c[0], c[1], c[2] + 0.25)) + rh * sgn * (WID / 2 - 0.1), rh, fh, UPB, 0.05, lh / 2 * 4.2, 0.03, ST)
    # (the rock support columns under the stairs are removed)
    if i % 100 == 50:
        for sgn in (-1, 1): obox(c + rh * sgn * (WID / 2 + 0.4) - u * 1.3, rh, fh, UPB, 0.18, 4.0, 0.18, ST)
        obox(c - u * 1.6, rh, fh, UPB, WID / 2 + 0.4, 0.18, 0.18, ST)
# ---- the 8 corner landings (20 x 20 m) with tripod lamp panels, crates, barrels, hazard barriers
corners = [(0.0667 + k * 0.1333) for k in range(8)]
for k, L in enumerate(corners):
    if k == 5: continue
    p = stair(L); pb = np.array(B(p)); outv = np.array((pb[0], pb[1], 0.0)); outv /= max(1e-6, np.linalg.norm(outv)); tang = np.array((-outv[1], outv[0], 0.0)); big = (k == 5); w = 26.0 if big else 20.0
    c = pb + outv * 4.0; obox(np.array((c[0], c[1], pb[2] - 0.6)), tang, outv, UPB, w / 2, w / 2, 0.6, TR)       # the deck is turned like its rails (it was axis-aligned: the rail square floated off it)
    for sgn in (-1, 1): obox(c + tang * sgn * (w / 2) + np.array((0, 0, 0.6)), tang, outv, UPB, 0.08, w / 2, 0.05, ST); obox(c + outv * sgn * (w / 2) + np.array((0, 0, 0.6)), tang, outv, UPB, w / 2, 0.08, 0.05, ST)
    for q in range(3 if not big else 6):
        o = c + tang * ((q - 1) * 4.5) + outv * (2 + (q % 2) * 4.0); h = 1.0 + (q % 2) * 1.0; obox(o + np.array((0, 0, h * 0.6 + 0.2)), tang, outv, UPB, 1.1, 1.1, h * 0.6, CR)
        if q % 2 == 0: obox(o + np.array((0, 0, h * 1.2 + 0.3)), tang, outv, UPB, 0.5, 0.3, 0.06, LP)
    for sgn in (-1, 1):
        lp = c + tang * sgn * (w / 2 - 1.5) + outv * (w / 2 - 1.5)
        for lg in range(3): ang = lg * 2.09; b.cyl((lp[0] + math.cos(ang) * 0.9, lp[1] + math.sin(ang) * 0.9, lp[2] + 1.6), 0.07, 3.4, ST, 5)
        b.cyl((lp[0], lp[1], lp[2] + 2.9), 0.1, 5.8, ST, 6); obox(lp + np.array((0, 0, 6.3)), tang, outv, UPB, 1.7, 0.25, 0.95, LP)                      # the big amber lamp panel on its tripod
    for q in range(2): bo = c + tang * (-w / 2 + 3 + q * 2.0) + outv * (-w / 2 + 3.0); b.cyl((bo[0], bo[1], bo[2] + 0.6), 0.55, 1.2, HZ, 8)
    obox(c + tang * 4 + outv * (w / 2 - 4) + np.array((0, 0, 0.8)), tang, outv, UPB, 2.6, 0.25, 0.7, HZ)
# ---- the Collider itself is ROUND and is the real Collider model the plugin stands in the middle (scale 2, ~190 m tall); here: two round ledges round it (radius 31 - 39) at the heights of two corner landings, for the rope bridges
HT = 190.0
for zr in (47.3, 99.3):
    for k in range(32):
        a = (k + 0.5) * 2 * math.pi / 32; c = np.array((math.cos(a) * 35.0, math.sin(a) * 35.0, zr)); tang = np.array((-math.sin(a), math.cos(a), 0.0)); outv = np.array((math.cos(a), math.sin(a), 0.0))
        obox(c + np.array((0, 0, -0.4)), tang, outv, UPB, 3.9, 4.2, 0.4, TR); obox(c + outv * 4.1 + np.array((0, 0, 0.9)), tang, outv, UPB, 3.9, 0.07, 0.05, ST)
def bridge(pa, pb_, planks=30):
    pa, pb_ = np.array(pa, float), np.array(pb_, float); f = pb_ - pa; ln = np.linalg.norm(f); f /= ln; r = np.cross(f, UPB); r /= np.linalg.norm(r)
    for k in range(planks):
        t = (k + 0.5) / planks; sag = -2.2 * math.sin(math.pi * t); c = pa + f * ln * t + np.array((0, 0, sag)); obox(c, r, f, UPB, 1.5, ln / planks * 0.42, 0.06, PK)
    for sgn in (-1, 1):
        for k in range(12):
            t0, t1 = k / 12, (k + 1) / 12; q0 = pa + f * ln * t0 + np.array((0, 0, -2.2 * math.sin(math.pi * t0) + 1.0)) + r * sgn * 1.5; q1 = pa + f * ln * t1 + np.array((0, 0, -2.2 * math.sin(math.pi * t1) + 1.0)) + r * sgn * 1.5
            ff = q1 - q0; l2 = np.linalg.norm(ff); ff /= l2; rr2 = np.cross(ff, UPB); rr2 /= np.linalg.norm(rr2); uu = np.cross(rr2, ff); obox((q0 + q1) / 2, rr2, ff, uu, 0.05, l2 / 2 * 1.02, 0.05, RP)
        for t in (0.0, 1.0): q = pa + f * ln * t + r * sgn * 1.5; b.cyl((q[0], q[1], q[2] + 0.6), 0.1, 1.4, ST, 6)
for L, zr in ((0.3333, 47.3), (0.7333, 99.3)):
    pb = np.array(B(stair(L))); inward = -np.array((pb[0], pb[1], 0.0)); inward /= np.linalg.norm(inward); start = np.array((pb[0], pb[1], pb[2] + 0.08)) + inward * 6.0; end = np.array((-math.copysign(32.0, pb[0] * -1) * -1, 0, 0)) * 0
    ang = math.atan2(pb[1], pb[0]); tgt = np.array((math.cos(ang) * 38.0, math.sin(ang) * 38.0, zr + 0.05)); bridge(start, tgt)
# ---- the pit: columns with amber dots, the wrecked mech (giant hand + head), broken slabs
for k in range(26):
    a = k * 2 * math.pi / 26 + 0.1; rr_ = 168 + 14 * math.sin(k * 1.9); w = 10 + 4 * abs(math.sin(k * 2.3)); h = 210 + 40 * math.sin(k * 1.1); cx, cy = math.cos(a) * rr_, math.sin(a) * rr_
    if abs(cx) < 40.0 and cy > 110.0: continue                                            # nothing stands where you arrive (Unity z -150)
    b.cyl((cx, cy, h / 2 - 4), w * 0.75, h, OB, 6, r2=w * 0.5); b.cyl((cx, cy, h - 6), w * 0.9, 9, OB, 6, r2=w * 0.55)                         # tall dark angular pillar with a heavy cap
    tx, ty = -math.sin(a), math.cos(a); rad_in = rr_ - w * 0.5 * 0.9
    for j in range(int(h / 7)):
        rj = rr_ - (w * 0.75 - (w * 0.25) * ((8 + j * 7) / h)) * 0.87                                                                           # the dots follow the taper, facing the middle
        for off in (-w * 0.22, 0.0, w * 0.22): b.box((math.cos(a) * rj + tx * off, math.sin(a) * rj + ty * off, 8 + j * 7), (0.6, 0.6, 0.6), LP)
    if k % 2 == 0:
        for j in range(3): b.box((math.cos(a) * (rr_ - w * 0.6), math.sin(a) * (rr_ - w * 0.6), 40 + j * 55), (0.9, 0.9, 14.0), SP)                 # magenta light bars
def finger(x0, y0, ang, ln, mat):
    dx, dy = math.cos(ang), math.sin(ang)
    for seg in range(3):
        l = ln * (1 - 0.22 * seg); cxx, cyy = x0 + dx * (seg * ln * 0.8 + l * 0.5), y0 + dy * (seg * ln * 0.8 + l * 0.5); obox((cxx, cyy, 1.8 + seg * 0.3), (-dy, dx, 0), (dx, dy, 0), (0, 0, 1), 1.6 - seg * 0.2, l / 2, 1.5 - seg * 0.2, mat)
hx, hy = -95.0, 25.0
obox((hx, hy, 2.5), (1, 0, 0), (0, 1, 0), (0, 0, 1), 9, 8, 2.5, WB); obox((hx, hy - 6, 2.5), (1, 0, 0), (0, 1, 0), (0, 0, 1), 7, 4, 3, WO)
for q in range(4): finger(hx - 6 + q * 4.0, hy + 8, math.pi / 2 + (q - 1.5) * 0.12, 7.0, WB if q % 2 == 0 else WO)
finger(hx + 9, hy + 2, 0.2, 6.0, WB); obox((-70, -105, 5), (1, 0, 0), (0, 1, 0), (0, 0, 1), 12, 12, 5, WB); obox((-70, -105, 11.5), (1, 0, 0), (0, 1, 0), (0, 0, 1), 9, 9, 2, WO); obox((-70, -93.5, 6), (1, 0, 0), (0, 1, 0), (0, 0, 1), 7, 1, 2, SP)
for k in range(20):
    a = rr.random() * 2 * math.pi; d = 90 + rr.random() * 90; ang_ = rr.random() * math.pi; sx = 2 + rr.random() * 6; obox((math.cos(a) * d, math.sin(a) * d, sx * 0.4), (math.cos(ang_), math.sin(ang_), 0), (-math.sin(ang_), math.cos(ang_), 0), (0, 0, 1), sx, sx * 0.6, sx * 0.35, WB if k % 3 else WO)
# ---- Slone's deck (the corner at s = 0.7333, Unity (60, 99.3, 60)): 26 x 44 m, level with the stairs, the player stops at z = 30 and the tank sits on the far half
sd = stair(0.733 - 24.0 / 900.0); DY = float(sd[1]) + 0.1; b.box((-61.0, -48.0, DY - 0.6), (26.0, 44.0, 1.2), TR, 4.0, top=TR)
for sgx in (-13.0, 13.0): obox(np.array((-61.0 - sgx, -48.0, DY + 0.55)), (1, 0, 0), (0, 1, 0), UPB, 0.08, 22.0, 0.05, ST)
obox(np.array((-61.0, -70.0, DY + 0.55)), (1, 0, 0), (0, 1, 0), UPB, 13.0, 0.08, 0.05, ST)
for sgx in (-11.0, 11.0):
    for zz in (-30.0, -68.0): lp_ = np.array((-61.0 - sgx, zz, DY)); [b.cyl((lp_[0] + math.cos(g_) * 0.9, lp_[1] + math.sin(g_) * 0.9, DY + 1.6), 0.07, 3.4, ST, 5) for g_ in (0.0, 2.09, 4.19)]; b.cyl((lp_[0], lp_[1], DY + 2.9), 0.1, 5.8, ST, 6); obox(lp_ + np.array((0, 0, 6.3)), (1, 0, 0), (0, 1, 0), UPB, 1.7, 0.25, 0.95, LP)
for cx_, cz_ in ((-71.0, -30.0), (-71.0, -34.5), (-50.0, -30.5)): obox(np.array((cx_, cz_, DY + 0.9)), (1, 0, 0), (0, 1, 0), UPB, 1.4, 1.4, 0.9, CR)
# ---- the first fight (the floor in front of the stairs, Unity z -150 .. -60): jersey barriers, generators with amber lamps, hazard drums, a magenta guide strip
def U2B(x, y, z): return np.array((-x, -z, y))
for k in range(9):
    x = -34 + k * 8.5 + 3 * math.sin(k * 2.1)
    if abs(x) < 10: x += 14 if x >= 0 else -14                                                       # the route (x = 0) stays clear
    z = -44 - 16 * math.sin(k * 1.3) - (k % 3) * 9; obox(U2B(x, 1.3, z), (1, 0, 0), (0, 1, 0), UPB, 3.2, 0.9, 1.3, BR); obox(U2B(x, 2.7, z), (1, 0, 0), (0, 1, 0), UPB, 3.0, 0.3, 0.1, HZ)
for k, (x, z) in enumerate(((-26, -75), (24, -80), (-12, -100), (14, -108), (-30, -120), (30, -125))):
    obox(U2B(x, 1.8, z), (1, 0, 0), (0, 1, 0), UPB, 2.4, 1.6, 1.8, GN); obox(U2B(x, 4.0, z), (1, 0, 0), (0, 1, 0), UPB, 1.2, 0.3, 0.3, LP)
    for g_ in (0.0, 2.09, 4.19): b.cyl((-x + math.cos(g_) * 0.9 + 6, -z + math.sin(g_) * 0.9, 1.6), 0.07, 3.4, ST, 5)
    b.cyl((-x + 6, -z, 2.9), 0.1, 5.8, ST, 6); obox(U2B(x - 6, 6.3, z), (1, 0, 0), (0, 1, 0), UPB, 1.7, 0.25, 0.95, LP)
for k in range(10): obox(U2B(0, 0.12, -150 + k * 9.5), (1, 0, 0), (0, 1, 0), UPB, 0.35, 3.0, 0.06, SP)
for k in range(14): a_ = k * 0.45; obox(U2B(-40 + 6 * math.cos(a_ * 5), 0.9, -130 + k * 5.5), (1, 0, 0), (0, 1, 0), UPB, 0.45, 0.45, 0.9, HZ)
# ---- the way up to the stairs: 30 real steps from the floor (Unity z -90, y 0) to the first stair tread (z -60, y 4), with rails
for k in range(30):
    top = 4.0 * (k + 1) / 30.0; zc = -90.0 + k + 0.5; b.box((0, -zc, top / 2), (9.0, 1.0, top), TR, 4.0, top=TR)
    if k % 6 == 0: [b.box((sg * 4.5, -zc, top + 0.55), (0.12, 0.12, 1.1), ST) for sg in (-1, 1)]
body = b.build('Structure'); parts = [body]
# ---- the cave: ridged wall, rubble floor, loose rocks
def wallmesh():
    NT, NZ = 120, 28; wv = []; wf = []
    for k in range(NZ + 1):
        for j in range(NT):
            a = j * 2 * math.pi / NT; rad = 205 + 16 * math.sin(a * 3 + k * 0.4) + 11 * math.sin(a * 7 - k * 0.7) + 6 * math.sin(a * 17 + k * 1.3) + 3 * math.sin(a * 41 - k) + (k / NZ) * 28; wv.append((math.cos(a) * rad, math.sin(a) * rad, -2 + 310 * k / NZ))
    for k in range(NZ):
        for j in range(NT): a0 = k * NT + j; a1 = k * NT + (j + 1) % NT; wf.append((a1, a0, a0 + NT, a1 + NT))
    me = bpy.data.meshes.new('Wall'); me.from_pydata(wv, [], wf); uv = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): uv.data[li].uv = (math.atan2(wv[vi][1], wv[vi][0]) / (2 * math.pi) * 8, wv[vi][2] / 60.0)
    me.materials.append(WALLM); o = bpy.data.objects.new('Wall', me); bpy.context.collection.objects.link(o); return o
def floormesh():
    verts = []; faces = []; NR, NT = 48, 120
    for i in range(NR + 1):
        for j in range(NT):
            r = 215 * i / NR; a = j * 2 * math.pi / NT; h = (2.2 * math.sin(r * 0.17 + a * 3) + 1.4 * math.sin(a * 9 + r * 0.45) + 0.8 * math.sin(a * 23 - r * 0.9)) * (min(1.0, r / 45.0) if r > 20 else 0.0); bx_, by_ = math.cos(a) * r, math.sin(a) * r; sm_ = lambda v, lo, hi: (lambda t_: t_ * t_ * (3 - 2 * t_))(min(1.0, max(0.0, (v - lo) / (hi - lo)))); fl_ = sm_(r, 75.0, 115.0)
            if by_ > 20: fl_ = min(fl_, sm_(abs(bx_), 18.0, 45.0))                                   # the route you run (Unity z -150 .. -60) and the ground round the stairs are FLAT
            verts.append((bx_, by_, h * fl_ * 0.7))
    for i in range(NR):
        for j in range(NT): a0 = i * NT + j; a1 = i * NT + (j + 1) % NT; faces.append((a0, a1, a1 + NT, a0 + NT))
    me = bpy.data.meshes.new('Floor'); me.from_pydata(verts, [], faces); uv = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): uv.data[li].uv = (verts[vi][0] / 40.0, verts[vi][1] / 40.0)
    me.materials.append(ROCK); o = bpy.data.objects.new('Floor', me); bpy.context.collection.objects.link(o); return o
parts += [wallmesh(), floormesh()]
for k in range(80):
    a = rr.random() * 2 * math.pi; d = 30 + rr.random() * 160; rad_ = 0.7 + rr.random() * 3.6
    if abs(math.cos(a) * d) < 24 and math.sin(a) * d > 40: continue                          # nothing on the route
    bm = displace_icosphere(rad_, 2, 40 + k, 0.4); me = bpy.data.meshes.new('lr%d' % k); bm.to_mesh(me); bm.free(); me.materials.append(ROCK); uvl = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): v = me.vertices[vi].co; uvl.data[li].uv = (math.atan2(v.y, v.x) / (2 * math.pi) + 0.5, v.z * 0.5 + 0.5)
    o = bpy.data.objects.new('LooseRock%d' % k, me); bpy.context.collection.objects.link(o); o.location = (math.cos(a) * d, math.sin(a) * d, rad_ * 0.3); parts.append(o)
bpy.ops.object.select_all(action='DESELECT')
for p_ in parts: p_.select_set(True)
bpy.context.view_layer.objects.active = body; bpy.ops.object.join(); body.name = 'Cavern'
# ---- animated: glow rings round the tower, lightning ribbons above it, swinging lanterns on the landings
for k, z in enumerate((40.0, 90.0, 140.0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=33.0, minor_radius=0.8, major_segments=48, minor_segments=8, location=(0, 0, z)); t = bpy.context.active_object; t.name = 'GlowRing%d' % k; t.data.materials.append(GLOWP); uv = t.data.uv_layers.new()
    for p in t.data.polygons:
        for li in p.loop_indices: uv.data[li].uv = (0.5, 0.5)
    keys(t, 'rotation_euler', lambda tt, k=k: (0, 0, (1 if k % 2 == 0 else -1) * 2 * math.pi * tt / 8), 24, 8, 4); keys(t, 'scale', lambda tt, k=k: (1.0 + 0.05 * math.sin(2 * math.pi * tt / 2 + k),) * 2 + (1.0,), 24, 8, 4)
for k, L in enumerate(corners):
    pb = np.array(B(stair(L))); lb = MB([LAMP, STEEL]); lb.box((0, 0, -0.5), (0.7, 0.7, 1.0), 0); lb.cyl((0, 0, 0.2), 0.04, 0.4, 1, 5); lan = lb.build('Lantern%d' % k); lan.location = (pb[0] * 0.8, pb[1] * 0.8, pb[2] + 7.5)
    keys(lan, 'rotation_euler', lambda tt, k=k: (0.12 * math.sin(2 * math.pi * tt / 4 + k), 0.1 * math.cos(2 * math.pi * tt / 4 + k * 1.3), 0), 24, 8, 4)
for k in range(3):
    verts = []; faces = []; N = 60
    for j in range(N + 1):
        f = j / N; ang = k * 2.09 + f * 7.0; r = 5.5 + 2.5 * math.sin(f * 9 + k); z = HT + 8 + f * 120; verts.append((math.cos(ang) * r, math.sin(ang) * r, z)); verts.append((math.cos(ang) * r, math.sin(ang) * r, z + 1.2))
    for j in range(N): a0 = j * 2; faces.append((a0, a0 + 1, a0 + 3, a0 + 2))
    me = bpy.data.meshes.new('rb%d' % k); me.from_pydata(verts, [], faces); me.materials.append(GLOWP); uvr = me.uv_layers.new()
    for p in me.polygons:
        for li in p.loop_indices: uvr.data[li].uv = (0.5, 0.5)
    o = bpy.data.objects.new('Lightning%d' % k, me); bpy.context.collection.objects.link(o)
    keys(o, 'rotation_euler', lambda tt, k=k: (0, 0, (1 if k % 2 == 0 else -1) * 2 * math.pi * tt / 4), 24, 8, 4); keys(o, 'scale', lambda tt, k=k: (1.0 + 0.25 * math.sin(2 * math.pi * tt / 0.5 + k), 1.0 + 0.25 * math.sin(2 * math.pi * tt / 0.5 + k), 1.0), 24, 8, 2)
export(out)
