"""blender -b -P make_collision_env.py -- <cabin|moonbase|tank|dome> <out.glb>
cabin: the enclosed head of the mech round the player's head (origin = head, forward = Blender -Y = glTF +Z): shell, wide visor window, dashboard with screens + bear emblem, joysticks, seats, ribbed roof with the five portrait frames, pipes, red trim; clip with joysticks, radar, beacons.
moonbase: a cratered moon-rock disc with a landing pad, hangar, light towers, antenna masts and a spinning radar dish, a big planet in the sky.
tank: the I.O. tank (hull, treads with wheels, turret, barrel with muzzle brake, hatch, antenna); clip = the turret scans and the barrel recoils (drives toward +Y of the file = glTF -Z).
dome: the orange hex energy barrier (half sphere); clip = slow spin and pulse."""
import bpy, sys, math, os, bmesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 8); R = np.random.default_rng(11)
UV4 = [(0, 0), (1, 0), (1, 1), (0, 1)]

def panel_tex(base, seed, W=256, H=256, lines=6, rivet=True, stripe=None, wear=0.35):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); col = np.array(base)[None, None, :] * (0.78 + wear * n[..., None])
    gx = np.abs(((U * lines) % 1) - 0.5); gy = np.abs(((V * lines) % 1) - 0.5); seam = ((gx > 0.485) | (gy > 0.485)); col = np.where(seam[..., None], col * 0.55, col)
    if rivet:
        rv = (np.hypot(((U * lines * 4) % 1) - 0.5, ((V * lines * 4) % 1) - 0.5) < 0.12) & ((gx > 0.4) | (gy > 0.4)); col = np.where(rv[..., None], col * 1.35, col)
    if stripe is not None:
        st = (((U + V) * 14) % 1 < 0.5) & (V > 0.86); col = np.where(st[..., None], np.array(stripe)[None, None, :], col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)

def bars(c1, c2, seed, n=7, W=96, H=48):
    r = np.random.default_rng(seed); img = np.zeros((H, W, 4), np.float32); img[..., :3] = (0.02, 0.05, 0.07); img[..., 3] = 1
    for k in range(n):
        y0 = 4 + k * (H - 8) // n; L = int(W * (0.25 + 0.7 * r.random())); img[y0:y0 + 3, 4:4 + L, :3] = c1 if k % 2 == 0 else c2
    img[:2, :, :3] = c1; img[-2:, :, :3] = c1; img[:, :2, :3] = c1; img[:, -2:, :3] = c1
    return img

def part(mb, name, loc=(0, 0, 0), parent=None):
    o = mb.build(name); o.location = loc
    if parent is not None: o.parent = parent
    return o

if kind == 'cabin':
    NAVY = material('Navy', panel_tex((0.2, 0.26, 0.42), 1, lines=5, stripe=(1.0, 0.55, 0.1))); DARK = material('Dark', panel_tex((0.1, 0.12, 0.2), 2, lines=8)); RED = solid('Red', 1.0, 0.25, 0.2); AMB = solid('Amber', 1.0, 0.65, 0.15); STEEL = material('Steel', panel_tex((0.5, 0.54, 0.62), 3, lines=3))
    GREEN = material('Green', bars(np.array([0.3, 1, 0.5]), np.array([0.2, 0.8, 1.0]), 1)); BLUE = material('Blue', bars(np.array([0.3, 0.8, 1.0]), np.array([0.6, 0.5, 1.0]), 2)); ORG = material('Orange', bars(np.array([1.0, 0.6, 0.15]), np.array([1.0, 0.85, 0.3]), 3))
    PORT = [solid('Portrait%d' % i, *c) for i, c in enumerate(((0.9, 0.55, 0.2), (0.6, 0.65, 0.85), (0.85, 0.35, 0.7), (0.95, 0.8, 0.45), (0.7, 0.5, 0.35)))]; GLOW = solid('Glow', 0.4, 0.9, 1.0)
    M = [NAVY, DARK, RED, AMB, STEEL, GREEN, BLUE, ORG, GLOW] + PORT; NV, DK, RD, AM, ST, GR, BL, OR, GL = range(9); P0 = 9
    b = MB(M); X1, Y0, Y1, Z0, Z1 = 2.5, -3.3, 2.4, -1.75, 1.35
    b.box((0, 0, Z0 - 0.1), (5.6, 6.2, 0.2), DK, 2.0)
    for k in range(7): b.box((0, Y0 + 0.4 + k * 0.9, Z0 + 0.02), (4.6, 0.12, 0.05), ST)
    b.box((0, Y1 + 0.1, 0), (5.6, 0.2, 3.3), NV, 2.0, top=DK)
    for s_ in (-1, 1):
        b.box((s_ * (X1 + 0.1), 0, 0), (0.2, 6.2, 3.3), NV, 2.0)
        b.box((s_ * (X1 - 0.25), -0.5, -0.9), (0.5, 3.6, 1.5), DK, 1.5)
        for k in range(5): b.quad((s_ * (X1 - 0.52), -2.1 + k * 0.7, -0.3), (s_ * (X1 - 0.52), -1.6 + k * 0.7, -0.3), (s_ * (X1 - 0.52), -1.6 + k * 0.7, -0.78), (s_ * (X1 - 0.52), -2.1 + k * 0.7, -0.78), [GR, BL, OR][k % 3], UV4)
        b.box((s_ * 2.15, 1.3, -1.3), (0.8, 1.0, 0.9), ST); b.box((s_ * 2.15, 1.35, -0.6), (0.8, 0.9, 0.18), DK); b.box((s_ * 2.15, 1.85, -0.2), (0.8, 0.18, 1.0), DK)
        b.box((s_ * 2.4, -3.2, 0.2), (0.34, 0.4, 3.3), NV, 2.0)
    b.box((0, Y0 + 0.05, 1.25), (5.4, 0.4, 0.9), NV, 2.0, top=DK)
    b.box((0, Y0 + 0.05, -1.35), (5.4, 0.5, 0.8), NV, 2.0)
    b.box((0, Y0 + 0.1, 0.78), (5.0, 0.3, 0.12), RD); b.box((0, Y0 + 0.1, -0.78), (5.0, 0.3, 0.1), RD)
    b.box((0, -2.55, -1.15), (5.2, 1.5, 0.9), DK, 2.0, top=NV)
    b.box((0, -2.2, -0.62), (5.0, 1.0, 0.16), NV, 2.0)
    for k, x in enumerate((-1.9, -0.65, 0.65, 1.9)): b.quad((x - 0.5, -2.75, -0.52), (x + 0.5, -2.75, -0.52), (x + 0.5, -2.2, -0.66), (x - 0.5, -2.2, -0.66), [GR, BL, OR, GR][k], UV4)
    b.cyl((0, -2.45, -0.6), 0.34, 0.08, RD, 8, axis='z'); b.cyl((0, -2.45, -0.55), 0.2, 0.06, AM, 6, axis='z')
    b.box((0, 0, Z1 + 0.05), (5.6, 6.2, 0.18), DK, 2.0)
    for k in range(8):
        b.box((0, Y0 + 0.5 + k * 0.7, Z1 - 0.1), (5.2, 0.18, 0.18), ST)
        if k % 2 == 0: b.box((0, Y0 + 0.5 + k * 0.7, Z1 - 0.19), (3.4, 0.3, 0.04), RD)
    for k, x in enumerate((-2.1, -1.05, 0.0, 1.05, 2.1)): b.quad((x - 0.42, -2.0, Z1 - 0.2), (x + 0.42, -2.0, Z1 - 0.2), (x + 0.42, -2.0, Z1 - 0.9), (x - 0.42, -2.0, Z1 - 0.9), P0 + k, UV4)
    for s_ in (-1, 1):
        b.cyl((s_ * 2.3, 0, Z1 - 0.3), 0.07, 6.0, ST, 8, axis='y'); b.cyl((s_ * 2.15, 0, Z1 - 0.5), 0.05, 6.0, RD, 6, axis='y')
        for k in range(4): b.cyl((s_ * 2.3, -2.4 + k * 1.6, Z1 - 0.3), 0.11, 0.18, DK, 8, axis='y')
    for k in range(4): b.cyl((-1.5 + k, 2.2, -0.6), 0.16, 0.12, AM, 8, axis='y'); b.cyl((-1.5 + k, 2.28, 0.2), 0.2, 0.1, GL, 8, axis='y')
    for s_ in (-1, 1):
        b.box((s_ * 2.15, 1.7, 0.45), (0.8, 0.22, 0.5), DK); b.box((s_ * 2.15, 1.7, -0.05), (0.3, 0.2, 0.2), ST)
        for k in range(3): b.quad((s_ * 2.05 - 0.3, Y1 - 0.02, -0.5 + k * 0.55), (s_ * 2.05 + 0.3, Y1 - 0.02, -0.5 + k * 0.55), (s_ * 2.05 + 0.3, Y1 - 0.02, -0.1 + k * 0.55), (s_ * 2.05 - 0.3, Y1 - 0.02, -0.1 + k * 0.55), [GR, BL, OR][(k + (s_ > 0)) % 3], UV4)
        for k in range(5): b.cyl((s_ * (X1 - 0.15), -2.4 + k * 1.1, 0.5), 0.06, 0.5, RD if k % 2 == 0 else AM, 6, axis='x')
    for k in range(4): b.box((-1.5 + k * 1.0, 0.0, Z1 - 0.06), (0.7, 0.9, 0.04), GL)
    for k in range(3): b.cyl((-1.0 + k * 1.0, 1.9, -0.2), 0.05, 2.4, ST, 6, axis='z')
    body = b.build('Cabin')
    def joy(name, x):
        jb = MB([DARK, RED, AMB]); jb.cyl((0, 0, 0.18), 0.03, 0.36, 0, 6, axis='z'); jb.cyl((0, 0, 0.4), 0.07, 0.14, 1, 8, axis='z'); jb.cyl((0, 0, -0.02), 0.1, 0.06, 0, 8, axis='z'); return part(jb, name, (x, -2.05, -0.55), body)
    jl = joy('JoyL', -0.5); jr = joy('JoyR', 0.5)
    rb = MB([STEEL, GLOW]); rb.cyl((0, 0, 0.0), 0.28, 0.04, 0, 12, axis='z'); rb.box((0.14, 0, 0.03), (0.28, 0.03, 0.02), 1); rd = part(rb, 'Radar', (1.9, -2.4, -0.42), body)
    lb = MB([RED]); lb.cyl((0, 0, 0), 0.07, 0.07, 0, 8, axis='z'); l1 = part(lb, 'BeaconL', (-2.25, 2.1, 1.0), body)
    lb2 = MB([RED]); lb2.cyl((0, 0, 0), 0.07, 0.07, 0, 8, axis='z'); l2 = part(lb2, 'BeaconR', (2.25, 2.1, 1.0), body)
    keys(jl, 'rotation_euler', lambda t: (0.25 * math.sin(2 * math.pi * t / 4), 0.2 * math.sin(2 * math.pi * t / 2), 0), 24, 8, 4)
    keys(jr, 'rotation_euler', lambda t: (0.22 * math.sin(2 * math.pi * t / 4 + 1), 0.25 * math.sin(2 * math.pi * t / 2 + 2), 0), 24, 8, 4)
    keys(rd, 'rotation_euler', lambda t: (0, 0, 2 * math.pi * t / 2), 24, 8, 4)
    keys(l1, 'scale', lambda t: (1, 1, 1) if int(t * 2) % 2 == 0 else (0.01, 0.01, 0.01), 24, 8, 12)
    keys(l2, 'scale', lambda t: (0.01, 0.01, 0.01) if int(t * 2) % 2 == 0 else (1, 1, 1), 24, 8, 12)
    export(out)

elif kind == 'tank':
    HULL = material('Hull', panel_tex((0.36, 0.4, 0.34), 5, lines=4)); TREAD = material('Tread', panel_tex((0.12, 0.12, 0.14), 6, lines=10)); TUR = material('Turret', panel_tex((0.42, 0.46, 0.4), 7, lines=3, stripe=(0.95, 0.35, 0.2))); RDM = solid('RedLight', 1.0, 0.2, 0.15); STEEL = material('Steel', panel_tex((0.5, 0.52, 0.56), 8, lines=2))
    b = MB([HULL, TREAD, TUR, RDM, STEEL]); b.box((0, 0, 0.7), (3.0, 6.0, 0.9), 0, 2.0, top=2); b.box((0, 1.6, 1.0), (2.6, 2.0, 0.5), 0, 2.0, top=2); b.box((0, -2.9, 0.65), (2.6, 0.5, 0.7), 0)
    for s_ in (-1, 1):
        b.box((s_ * 1.75, 0, 0.55), (0.9, 6.4, 0.9), 1, 1.5)
        for k in range(6): b.cyl((s_ * 1.75, -2.5 + k * 1.0, 0.45), 0.42, 0.92, 4, 12, axis='x')
        b.box((s_ * 1.75, 0, 1.05), (1.0, 6.2, 0.14), 4)
    b.box((0, -3.05, 0.9), (0.5, 0.1, 0.18), 3); b.cyl((0.9, 2.95, 0.8), 0.12, 0.1, 3, 8, axis='y'); b.cyl((-0.9, 2.95, 0.8), 0.12, 0.1, 3, 8, axis='y')
    hull = b.build('Tank')
    tb = MB([TUR, RDM, STEEL, TREAD]); tb.cyl((0, 0, 0.35), 1.35, 0.7, 0, 14, axis='z', r2=1.1); tb.box((0, -0.3, 0.35), (2.0, 1.6, 0.6), 0, 2.0, top=0); tb.cyl((0.6, -0.3, 0.8), 0.32, 0.12, 2, 10, axis='z'); tb.cyl((-0.7, -0.5, 0.78), 0.05, 0.9, 2, 5, axis='z'); tb.cyl((0, 1.0, 0.45), 0.22, 0.4, 1, 8, axis='y')
    turret = part(tb, 'Turret', (0, 0.0, 1.1), hull)
    bb = MB([STEEL, TREAD]); bb.cyl((0, 1.9, 0.0), 0.16, 3.8, 0, 10, axis='y'); bb.cyl((0, 3.9, 0.0), 0.25, 0.5, 1, 10, axis='y'); bb.cyl((0, 0.1, 0.0), 0.3, 0.5, 1, 10, axis='y')
    barrel = part(bb, 'Barrel', (0, 0.7, 0.35), turret)
    keys(turret, 'rotation_euler', lambda t: (0, 0, 0.7 * math.sin(2 * math.pi * t / 8)), 24, 8, 4)
    keys(barrel, 'location', lambda t: (0, 0.7 - 0.45 * max(0.0, math.sin(2 * math.pi * (t - 1.0) / 2.0)) ** 6, 0.35), 24, 8, 2)
    export(out)

elif kind == 'asteroid':
    n_ = fbm(256, 256, 9, 4); ROCKM = material('Asteroid', rgba(0.35 + 0.3 * n_, 0.3 + 0.25 * n_, 0.28 + 0.22 * n_, 1.0))
    bm = displace_icosphere(1.0, 3, 17, 0.42); me = bpy.data.meshes.new('Asteroid'); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new('Asteroid', me); bpy.context.collection.objects.link(o); o.data.materials.append(ROCKM)
    uvl = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): v = me.vertices[vi].co; uvl.data[li].uv = (math.atan2(v.y, v.x) / (2 * math.pi) + 0.5, v.z * 0.5 + 0.5)
    keys(o, 'rotation_euler', lambda t: (2 * math.pi * t / 8, 0.5 * 2 * math.pi * t / 8, 0.3 * 2 * math.pi * t / 8), 24, 8, 4)
    export(out)

elif kind == 'dome':
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1.0); o = bpy.context.active_object; bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.05], context='VERTS'); bm.to_mesh(o.data); bm.free()
    W = H = 256; U, V = grid(W, H); hx = np.abs(np.sin(U * 40 + np.sin(V * 20) * 0.5)) * np.abs(np.sin(V * 46)); line = np.exp(-((1 - hx) / 0.05) ** 2); n = fbm(W, H, 5, 9)
    col = np.stack([np.ones_like(U), 0.55 + 0.3 * n + 0.25 * line, 0.15 + 0.4 * line], -1); alpha = np.clip(0.14 + 0.5 * line + 0.06 * n, 0, 1)
    o.data.materials.append(material('A_Dome', rgba(col[..., 0], col[..., 1], col[..., 2], alpha)))
    for p in o.data.polygons: p.use_smooth = True
    me = o.data; uv = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): v = me.vertices[vi].co; uv.data[li].uv = (math.atan2(v.y, v.x) / (2 * math.pi) + 0.5, v.z * 0.9)
    o.name = 'Dome'
    keys(o, 'rotation_euler', lambda t: (0, 0, 2 * math.pi * t / 8), 24, 8, 4)
    keys(o, 'scale', lambda t: (1.0 + 0.02 * math.sin(2 * math.pi * t / 2), 1.0 + 0.02 * math.sin(2 * math.pi * t / 2), 1.0 + 0.03 * math.sin(2 * math.pi * t / 2)), 24, 8, 4)
    export(out)

else:
    rock_n = fbm(256, 256, 7, 3); ROCK = material('MoonRock', rgba(0.3 + 0.35 * rock_n, 0.3 + 0.35 * rock_n, 0.32 + 0.35 * rock_n, 1.0)); PAD = material('Pad', panel_tex((0.36, 0.4, 0.5), 4, lines=4, stripe=(1.0, 0.6, 0.15))); HNG = material('Hangar', panel_tex((0.3, 0.34, 0.45), 5, lines=6)); STEEL = material('Steel', panel_tex((0.55, 0.58, 0.65), 6, lines=3)); LIGHT = solid('Light', 1.0, 0.62, 0.25); REDL = solid('RedL', 1.0, 0.2, 0.15)
    pn = fbm(256, 128, 5, 8); land = (pn > 0.5)[..., None]; PLAN = material('Planet', rgba(np.where(land, 0.25, 0.1)[..., 0], np.where(land, 0.6, 0.3)[..., 0], np.where(land, 0.3, 0.7)[..., 0], 1.0))
    nvx = 96; rad = 320.0; verts = []; faces = []
    def Hf(x, y):
        r = math.hypot(x, y); h = 7 * math.sin(x * 0.02) * math.cos(y * 0.017) + 4 * math.sin(x * 0.07 + y * 0.05)
        for ci in range(14):
            cx = (CR[ci][0] - 0.5) * 520; cy = (CR[ci][1] - 0.5) * 520; cr = 14 + CR[ci][2] * 38; d = math.hypot(x - cx, y - cy) / cr
            if d < 1.25: h += -cr * 0.18 * (1 - d * d) + (cr * 0.07 * (1.25 - d) if d > 0.85 else 0)
        return h * min(1.0, max(0.0, (r - 70) / 70)) - (max(0, r - 270) / 50) ** 2 * 30
    CR = R.random((14, 3))
    for j in range(nvx + 1):
        for i in range(nvx + 1): x = (i / nvx - 0.5) * 2 * rad; y = (j / nvx - 0.5) * 2 * rad; verts.append((x, y, Hf(x, y)))
    for j in range(nvx):
        for i in range(nvx): a = j * (nvx + 1) + i; faces.append((a, a + 1, a + nvx + 2, a + nvx + 1))
    me = bpy.data.meshes.new('MoonGround'); me.from_pydata(verts, [], faces); uvl = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): uvl.data[li].uv = (verts[vi][0] / 80.0, verts[vi][1] / 80.0)
    me.materials.append(ROCK); ground = bpy.data.objects.new('MoonGround', me); bpy.context.collection.objects.link(ground)
    b = MB([PAD, HNG, STEEL, LIGHT, REDL]); b.box((0, 0, 0.4), (120, 150, 0.8), 0, 12.0, top=0)
    for k in range(14): a = k * 2 * math.pi / 14; b.box((math.cos(a) * 58, math.sin(a) * 73, 1.2), (3, 3, 2.4), 3)
    b.box((0, 105, 22), (110, 30, 44), 1, 8.0, top=1); b.box((0, 105, 46), (96, 24, 4), 2); b.box((0, 88, 14), (60, 2, 28), 2)
    for s_ in (-1, 1): b.box((s_ * 70, -20, 30), (10, 10, 60), 1, 6.0); b.box((s_ * 70, -20, 62), (14, 14, 4), 3); b.box((s_ * 95, 40, 20), (8, 8, 40), 1, 6.0); b.box((s_ * 95, 40, 42), (12, 12, 4), 3)
    for k in range(5):
        a = 0.5 + k * 1.1; x, y = math.cos(a) * 190, math.sin(a) * 170; b.cyl((x, y, 30), 1.2, 60, 2, 6); b.box((x, y, 61), (2.5, 2.5, 2.5), 4)
    for k in range(6): x = -70 + k * 28; b.cyl((x, 118, 8), 7, 16, 1, 14, axis='z'); b.cyl((x, 118, 17), 3, 3, 2, 8, axis='z'); b.box((x, 118, 2), (8, 2, 4), 2)
    b.box((55, 40, 30), (6, 6, 60), 2); b.box((40, 40, 62), (36, 4, 4), 2); b.box((24, 40, 52), (1.2, 1.2, 20), 4); b.box((24, 40, 41), (3, 3, 2), 3)
    for k in range(4): b.box((-60 + k * 40, -70, 2), (22, 4, 5), 0, 6.0); b.box((-60 + k * 40, -76, 4), (20, 8, 8), 1, 6.0)
    for k in range(10): a = k * math.pi * 2 / 10; b.box((math.cos(a) * 100, math.sin(a) * 100, 1.8), (12, 3, 3.6), 1, 4.0)
    fixed = b.build('BaseFixed')
    db = MB([STEEL, REDL]); db.cyl((0, 0, 0), 6, 2, 0, 16, axis='z', r2=16); db.box((9, 0, 2), (18, 1.2, 14), 0); db.cyl((0, 0, 8), 1.5, 6, 1, 8, axis='z'); dish = part(db, 'Dish', (-130, 90, 12))
    keys(dish, 'rotation_euler', lambda t: (0, 0, 2 * math.pi * t / 8), 24, 8, 4)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, segments=48, ring_count=24, location=(-900, 700, 520)); pl = bpy.context.active_object; pl.scale = (330, 330, 330); pl.data.materials.append(PLAN); pl.name = 'Planet'
    for p in pl.data.polygons: p.use_smooth = True
    keys(pl, 'rotation_euler', lambda t: (0, 0, 0.1 * t / 8), 24, 8, 4)
    export(out)
