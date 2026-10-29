"""blender -b -P make_collision_env2.py -- <cavern|zeropoint|crate|boulder|tbc|mushroom> <out.glb>
The Collision milestone 2 / 3 sets (Blender Z-up, forward = -Y):
cavern: the underground under the Collider: a rock floor and a ring wall (radius 150 m), the Collider's dark base pillar in the middle (radius 26 m, 280 m tall), a spiral staircase with railings and amber lamps climbing round it (radius 40 m, 2.2 turns, 130 m rise) with four wide landings and a big top platform, crates on the landings.
  Stair path (used by the scene): angle a(s) = 0.5 * pi + s * 2.2 * 2pi, height z(s) = 4 + s * 130, s in [0, 1], radius 40, counter-clockwise seen from above.
zeropoint: the Zero Point: a glowing purple swirl sphere (radius 1, clip: turns + pulses), a translucent halo shell, 4 crystals (nodes Crystal0..3) above it.
crate: a bomb crate (the Foundation throws it).  boulder: a rock.  tbc: the 'TO BE CONTINUED...' text.  mushroom: a cluster of glowing blue mushrooms (clip: the caps sway)."""
import bpy, sys, math, os, bmesh, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 8); R = np.random.default_rng(21)
UV4 = [(0, 0), (1, 0), (1, 1), (0, 1)]
def panel_tex(base, seed, W=256, H=256, lines=6, stripe=None, wear=0.35):
    U, V = grid(W, H); n = fbm(W, H, 6, seed); col = np.array(base)[None, None, :] * (0.78 + wear * n[..., None]); gx = np.abs(((U * lines) % 1) - 0.5); gy = np.abs(((V * lines) % 1) - 0.5); col = np.where(((gx > 0.485) | (gy > 0.485))[..., None], col * 0.55, col)
    if stripe is not None: col = np.where(((((U + V) * 14) % 1 < 0.5) & (V > 0.86))[..., None], np.array(stripe)[None, None, :], col)
    return rgba(np.clip(col[..., 0], 0, 1), np.clip(col[..., 1], 0, 1), np.clip(col[..., 2], 0, 1), 1.0)
def rock_tex(seed, tint=(1, 1, 1), glow=0.0):
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 6, seed); strata = 0.5 + 0.5 * np.sin(V * 2 * np.pi * 7 + n * 5); crack = np.exp(-((n - 0.5) / 0.03) ** 2)
    c = np.stack([0.09 + 0.09 * strata, 0.10 + 0.09 * strata, 0.15 + 0.11 * strata], -1) * np.array(tint)[None, None, :] + crack[..., None] * np.array((0.55, 0.25, 0.95))[None, None, :] * glow
    return rgba(np.clip(c[..., 0], 0, 1), np.clip(c[..., 1], 0, 1), np.clip(c[..., 2], 0, 1), 1.0)
def swirl_tex(a, b, c, seed, alpha=1.0, W=128):
    U, V = grid(W, W); T = np.pi * 2; n = np.sin(T * (U * 3 + 0.25 * np.sin(T * (V * 2 + seed)))) + np.sin(T * (V * 4 + 0.3 * np.sin(T * U * 3 + seed))) + np.sin(T * ((U + V) * 2)); n = n / 3 * 0.5 + 0.5
    a, b, c = (np.array(x)[None, None, :] for x in (a, b, c)); lo = n[..., None] < 0.5; col = np.where(lo, a + (b - a) * (n[..., None] * 2), b + (c - b) * ((n[..., None] - 0.5) * 2)); return rgba(col[..., 0], col[..., 1], col[..., 2], alpha * (0.4 + 0.6 * n))
def part(mb, name, loc=(0, 0, 0), parent=None):
    o = mb.build(name); o.location = loc
    if parent is not None: o.parent = parent
    return o
def uv_sphere_obj(name, r, mat, loc=(0, 0, 0), seg=40, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=seg, ring_count=rings, location=loc); o = bpy.context.active_object; o.name = name; o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o

if kind == 'cavern':
    ROCK = material('Rock', rock_tex(3, (1, 1, 1.05), 0.12)); ROCK2 = material('RockWall', rock_tex(8, (0.95, 0.95, 1.1), 0.3)); GLOWP = material('A_PurpleGlow', swirl_tex((0.4, 0.15, 0.8), (0.8, 0.45, 1.0), (1, 0.95, 1), 0.3, 0.45)); STEEL = material('Steps', panel_tex((0.2, 0.24, 0.36), 4, lines=4, stripe=(1.0, 0.6, 0.12))); RAIL = material('Rail', panel_tex((0.5, 0.52, 0.6), 5, lines=2)); LAMP = solid('Lamp', 1.0, 0.7, 0.2)
    PILLAR = material('Pillar', panel_tex((0.2, 0.22, 0.36), 6, lines=12, stripe=(0.7, 0.35, 1.0))); CRATE = material('Crate', panel_tex((0.7, 0.45, 0.2), 9, lines=3, stripe=(0.15, 0.15, 0.15))); GLOW = solid('PurpleGlow', 0.8, 0.35, 1.0)
    # the floor + the ring wall (displaced grids / cylinder)
    def disc_mesh(name, radius, n_r, n_t, hfun, up=True):
        verts = []; faces = []
        for i in range(n_r + 1):
            for j in range(n_t): r = radius * i / n_r; a = j * 2 * math.pi / n_t; verts.append((math.cos(a) * r, math.sin(a) * r, hfun(r, a)))
        for i in range(n_r):
            for j in range(n_t): a0 = i * n_t + j; a1 = i * n_t + (j + 1) % n_t; faces.append((a0, a1, a1 + n_t, a0 + n_t) if up else (a0 + n_t, a1 + n_t, a1, a0))
        me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); uv = me.uv_layers.new()
        for p in me.polygons:
            for li, vi in zip(p.loop_indices, p.vertices): uv.data[li].uv = (verts[vi][0] / 40.0, verts[vi][1] / 40.0)
        me.materials.append(ROCK); o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); return o
    rr = np.random.default_rng(4)
    floor = disc_mesh('Floor', 150, 40, 96, lambda r, a: (2.5 * math.sin(r * 0.15 + a * 3) + 1.5 * math.sin(a * 7 + r * 0.4)) * min(1.0, r / 50.0) if r > 30 else 0.0)
    wv = []; wf = []; NT = 96; NZ = 24
    for k in range(NZ + 1):
        for j in range(NT): a = j * 2 * math.pi / NT; rad = 150 + 14 * math.sin(a * 5 + k * 0.6) + 8 * math.sin(a * 13 - k * 0.9) + (k / NZ) * 30; wv.append((math.cos(a) * rad, math.sin(a) * rad, -2 + 300 * k / NZ))
    for k in range(NZ):
        for j in range(NT): a0 = k * NT + j; a1 = k * NT + (j + 1) % NT; wf.append((a1, a0, a0 + NT, a1 + NT))
    wme = bpy.data.meshes.new('Wall'); wme.from_pydata(wv, [], wf); wuv = wme.uv_layers.new()
    for p in wme.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): wuv.data[li].uv = (math.atan2(wv[vi][1], wv[vi][0]) / (2 * math.pi) * 8, wv[vi][2] / 60.0)
    wme.materials.append(ROCK2); wall = bpy.data.objects.new('Wall', wme); bpy.context.collection.objects.link(wall)
    b = MB([PILLAR, STEEL, RAIL, LAMP, CRATE, GLOW, ROCK]); b.cyl((0, 0, 140), 26, 280, 0, 36, axis='z', r2=24)                                           # the Collider's base pillar
    for k in range(10): z = 20 + k * 26; b.cyl((0, 0, z), 27.5, 1.6, 2, 36, axis='z'); b.cyl((0, 0, z), 27.0, 0.5, 5, 36, axis='z') if k % 3 == 0 else None
    RAD = 40.0; TURNS = 2.2; RISE = 130.0; NS = 260
    for i in range(NS):                                                                                                                           # the spiral staircase
        s = (i + 0.5) / NS; a = 0.5 * math.pi + s * TURNS * 2 * math.pi; z = 4 + s * RISE; cx, cy = math.cos(a) * RAD, math.sin(a) * RAD; ang = a + math.pi / 2
        c, sn_ = math.cos(ang), math.sin(ang); w, d, th = 9.0, 1.9, 0.7                                                                         # a step: 9 m wide (radial), 1.9 m deep (tangential)
        pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)]; top = [(cx + px * math.cos(a) - py * math.sin(a) * -1 if False else cx + px * math.cos(a) + py * c, cy + px * math.sin(a) + py * sn_) for px, py in pts]
        P = lambda z_: [(x, y, z_) for x, y in top]; t_ = P(z); u_ = P(z - th)
        b.quad(t_[3], t_[2], t_[1], t_[0], 1, UV4); b.quad(u_[0], u_[1], u_[2], u_[3], 1, UV4)
        for e in range(4): e2 = (e + 1) % 4; b.quad(u_[e], u_[e2], t_[e2], t_[e], 1, UV4)
        if i % 5 == 0:                                                                                                                              # railing posts + lamp posts
            for rs in (-1, 1): px = rs * (w / 2 - 0.3); ox = cx + px * math.cos(a); oy = cy + px * math.sin(a); b.cyl((ox, oy, z + 0.6), 0.1, 1.2, 2, 5, axis='z')
        if i % 25 == 0: ox = cx + (w / 2 + 0.4) * math.cos(a); oy = cy + (w / 2 + 0.4) * math.sin(a); b.cyl((ox, oy, z + 3.5), 0.18, 6.5, 2, 6, axis='z'); b.box((ox, oy, z + 7.2), (1.8, 1.8, 0.8), 3)
    for k, s in enumerate((0.0, 0.22, 0.46, 0.72, 1.0)):                                                                                          # landings: flat platforms off the stair at five heights
        a = 0.5 * math.pi + s * TURNS * 2 * math.pi; z = 4 + s * RISE; big = (k == 4); w = 38 if big else 22
        cx, cy = math.cos(a) * (RAD + (4 if big else 2)), math.sin(a) * (RAD + (4 if big else 2)); b.box((cx, cy, z - 0.8), (w, w, 1.6), 1, 6.0)
        for q in range(3 if not big else 6): b.box((cx + (q - 1) * 4.5, cy + 6 * (1 if q % 2 else -1), z + 1.0), (3.2, 3.2, 2.0), 4, 2.0)
    # more set dressing: rock buttresses round the wall, under-step struts, orange work lamps and hazard barriers along the floor path, loose rocks, glowing rings round the pillar (animated)
    for k in range(18):
        a = k * 2 * math.pi / 18 + 0.2 * math.sin(k); rr_ = 132 + 10 * math.sin(k * 2.3); r1 = 9 + 7 * abs(math.sin(k * 1.7)); b.cyl((math.cos(a) * rr_, math.sin(a) * rr_, 100), r1, 220, 6, 9, r2=r1 * 0.55)
    for i in range(0, NS, 14):
        s = (i + 0.5) / NS; a = 0.5 * math.pi + s * TURNS * 2 * math.pi; z = 4 + s * RISE; b.cyl((math.cos(a) * (RAD + 3.5), math.sin(a) * (RAD + 3.5), z / 2 - 0.4), 0.5, z - 0.8, 2, 6)
        b.box((math.cos(a) * (RAD - 3.5), math.sin(a) * (RAD - 3.5), z - 1.6), (1.0, 1.0, 1.8), 1)
    for k in range(9):
        y = 56 + k * 9.0; b.cyl((-9, y, 3.2), 0.12, 6.4, 2, 6); b.box((-9, y, 6.6), (1.6, 1.0, 0.7), 3); b.cyl((9, y + 3, 3.2), 0.12, 6.4, 2, 6); b.box((9, y + 3, 6.6), (1.6, 1.0, 0.7), 3)
    for k in range(6): y = 60 + k * 12; b.box((-4 + (k % 3) * 4, y, 0.7), (4.0, 1.0, 1.4), 1, 2.0); b.box((7 - (k % 2) * 14, y + 5, 0.9), (1.5, 1.5, 1.8), 4, 2.0)
    body = b.build('Structure'); extra = []
    for k in range(40):
        a = rr.random() * 2 * math.pi; d = 30 + rr.random() * 110; rad_ = 0.8 + rr.random() * 3.2
        bm = displace_icosphere(rad_, 2, 30 + k, 0.4); me = bpy.data.meshes.new('lr%d' % k); bm.to_mesh(me); bm.free(); me.materials.append(ROCK)
        uvl = me.uv_layers.new()
        for p in me.polygons:
            for li, vi in zip(p.loop_indices, p.vertices): v = me.vertices[vi].co; uvl.data[li].uv = (math.atan2(v.y, v.x) / (2 * math.pi) + 0.5, v.z * 0.5 + 0.5)
        o = bpy.data.objects.new('LooseRock%d' % k, me); bpy.context.collection.objects.link(o); o.location = (math.cos(a) * d, math.sin(a) * d, rad_ * 0.3); extra.append(o)
    rings = []
    for k, z in enumerate((45.0, 95.0, 150.0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=28.5, minor_radius=0.7, major_segments=48, minor_segments=8, location=(0, 0, z)); t = bpy.context.active_object; t.name = 'GlowRing%d' % k; t.data.materials.append(GLOWP)
        uvt = t.data.uv_layers.new()
        for p in t.data.polygons:
            for li in p.loop_indices: uvt.data[li].uv = (0.5, 0.5)
        keys(t, 'rotation_euler', lambda tt, k=k: (0, 0, (1 if k % 2 == 0 else -1) * 2 * math.pi * tt / 8), 24, 8, 4); keys(t, 'scale', lambda tt, k=k: (1.0 + 0.04 * math.sin(2 * math.pi * tt / 2 + k),) * 2 + (1.0,), 24, 8, 4); rings.append(t)
    bpy.ops.object.select_all(action='DESELECT')
    for p_ in [body, floor, wall] + extra: p_.select_set(True)
    bpy.context.view_layer.objects.active = body; bpy.ops.object.join(); body.name = 'Cavern'; export(out)

elif kind == 'zeropoint':
    ZP = material('ZeroPoint', swirl_tex((0.35, 0.1, 0.7), (0.75, 0.4, 1.0), (1.0, 0.95, 1.0), 0.2, 1.0)); HALO = material('A_Halo', swirl_tex((0.5, 0.2, 0.9), (0.9, 0.6, 1.0), (1, 1, 1), 0.7, 0.55)); CRY = material('Crystal', swirl_tex((0.4, 0.9, 1.0), (0.8, 1.0, 1.0), (1, 1, 1), 0.4, 1.0))
    core = uv_sphere_obj('Core', 1.0, ZP); halo = uv_sphere_obj('Halo', 1.12, HALO); keys(core, 'rotation_euler', lambda t: (0, 0, 2 * math.pi * t / 8), 24, 8, 4); keys(halo, 'rotation_euler', lambda t: (0.3 * math.sin(2 * math.pi * t / 8), 0, -2 * math.pi * t / 8 * 1.5), 24, 8, 4)
    keys(halo, 'scale', lambda t: (1.0 + 0.03 * math.sin(2 * math.pi * t / 2),) * 3, 24, 8, 4)
    HALO2 = material('A_Halo2', swirl_tex((0.8, 0.9, 1.0), (1.0, 0.85, 1.0), (1, 1, 1), 0.2, 0.22)); halo2 = uv_sphere_obj('Halo2', 1.4, HALO2); keys(halo2, 'rotation_euler', lambda t: (0, 0.3 * math.sin(2 * math.pi * t / 8), 2 * math.pi * t / 8), 24, 8, 4); keys(halo2, 'scale', lambda t: (1.0 + 0.05 * math.sin(2 * math.pi * t / 1.5),) * 3, 24, 8, 4)
    # --- plasma: 18 ribbons that arc from the sphere up to the crystals, a flat swirl disc round the equator, an outer thin shell and energy spikes (all A_ glow, animated)
    PLASMA = material('A_Plasma', swirl_tex((0.6, 1.0, 1.0), (1.0, 0.9, 1.0), (1, 1, 1), 0.5, 0.85)); DISC = material('A_Disc', swirl_tex((0.4, 0.8, 1.0), (0.8, 0.4, 1.0), (1, 1, 1), 0.9, 0.35)); SHELL = material('A_Shell3', swirl_tex((0.4, 0.9, 1.0), (0.8, 0.5, 1.0), (1, 1, 1), 0.1, 0.12))
    sh3 = uv_sphere_obj('Shell3', 1.85, SHELL); keys(sh3, 'rotation_euler', lambda t: (0.2 * math.sin(2 * math.pi * t / 8), 0, 2 * math.pi * t / 8 * 0.5), 24, 8, 4)
    rr_ = np.random.default_rng(5)
    for k in range(18):
        th = rr_.random() * 2 * math.pi; ph = 0.3 + rr_.random() * 0.9; p0 = np.array((math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th), math.cos(ph))) * 1.02; ca = (k % 4) * math.pi / 2 + math.pi / 4; p2 = np.array((math.cos(ca) * 0.55, math.sin(ca) * 0.55, 1.4)); pm = (p0 + p2) / 2 + np.array((math.cos(th), math.sin(th), 0.0)) * (0.5 + rr_.random() * 0.5) + np.array((0, 0, 0.3))
        verts = []; faces = []; NS = 14
        for j in range(NS + 1):
            u = j / NS; pt = (1 - u) ** 2 * p0 + 2 * u * (1 - u) * pm + u * u * p2; tg = 2 * (1 - u) * (pm - p0) + 2 * u * (p2 - pm); sd = np.cross(tg, (0, 0, 1)); sd = sd / max(1e-6, np.linalg.norm(sd)) * (0.1 * (1 - 0.5 * u)); verts += [tuple(pt - sd), tuple(pt + sd)]
        for j in range(NS): faces.append((j * 2, j * 2 + 1, j * 2 + 3, j * 2 + 2))
        me = bpy.data.meshes.new('pl%d' % k); me.from_pydata(verts, [], faces); me.materials.append(PLASMA); uvq = me.uv_layers.new()
        for p in me.polygons:
            for li in p.loop_indices: uvq.data[li].uv = (0.5, 0.5)
        o = bpy.data.objects.new('Plasma%d' % k, me); bpy.context.collection.objects.link(o)
        keys(o, 'rotation_euler', lambda t, k=k: (0, 0, 2 * math.pi * t / 8 * (1 if k % 2 == 0 else -1) * (1 + 0.3 * (k % 3))), 24, 8, 4); keys(o, 'scale', lambda t, k=k: (1.0 + 0.12 * math.sin(2 * math.pi * t / 1.0 + k), 1.0 + 0.12 * math.sin(2 * math.pi * t / 1.0 + k), 1.0 + 0.08 * math.sin(2 * math.pi * t / 2.0 + k)), 24, 8, 2)
    for k in range(26):
        th = rr_.random() * 2 * math.pi; ph = math.acos(2 * rr_.random() - 1); d_ = np.array((math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th), math.cos(ph)))
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=4, radius1=0.12, radius2=0.0, depth=0.6 + 0.5 * rr_.random()); me = bpy.data.meshes.new('sp%d' % k); bm.to_mesh(me); bm.free(); me.materials.append(PLASMA); uvq = me.uv_layers.new()
        for p in me.polygons:
            for li in p.loop_indices: uvq.data[li].uv = (0.5, 0.5)
        o = bpy.data.objects.new('Spike%d' % k, me); bpy.context.collection.objects.link(o); o.location = tuple(d_ * 1.15); o.rotation_euler = mathutils.Vector((0, 0, 1)).rotation_difference(mathutils.Vector(d_)).to_euler()
        keys(o, 'scale', lambda t, k=k: (1.0, 1.0, 0.6 + 0.6 * abs(math.sin(2 * math.pi * t / 1.5 + k))), 24, 8, 2)
    for k in range(4):
        cb = MB([CRY]); cb.cyl((0, 0, 0.12), 0.0, 0.0, 0, 4) if False else None
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=6, radius1=0.09, radius2=0.0, depth=0.5); me = bpy.data.meshes.new('cry%d' % k); bm.to_mesh(me); bm.free(); me.materials.append(CRY)
        uvl = me.uv_layers.new()
        for p in me.polygons:
            for li in p.loop_indices: uvl.data[li].uv = (0.5, 0.5)
        o = bpy.data.objects.new('Crystal%d' % k, me); bpy.context.collection.objects.link(o); a = k * math.pi / 2 + math.pi / 4; o.location = (math.cos(a) * 0.55, math.sin(a) * 0.55, 1.35); o.rotation_euler = (math.sin(a) * 0.5, -math.cos(a) * 0.5, 0)
        keys(o, 'location', lambda t, k=k, a=a: (math.cos(a) * 0.55, math.sin(a) * 0.55, 1.35 + 0.03 * math.sin(2 * math.pi * t / 2 + k)), 24, 8, 4)
    export(out)

elif kind == 'rifle':
    BODY = material('RifleBody', panel_tex((0.18, 0.2, 0.26), 21, lines=3, stripe=(1.0, 0.55, 0.12))); GUN = solid('GunMetal', 0.08, 0.09, 0.11); ORG = solid('RifleAmber', 1.0, 0.6, 0.15); b = MB([BODY, GUN, ORG])
    b.box((0, -0.35, 0.05), (0.07, 0.62, 0.1), 0, 1.0); b.cyl((0, -0.85, 0.07), 0.018, 0.5, 1, 8, axis='y'); b.cyl((0, -1.12, 0.07), 0.03, 0.1, 1, 8, axis='y'); b.box((0, 0.12, 0.02), (0.07, 0.3, 0.12), 0, 1.0)
    b.box((0, -0.3, 0.03), (0.06, 0.12, 0.2), 1, 1.0); b.box((0, 0.0, -0.1), (0.05, 0.08, 0.2), 1, 1.0); b.cyl((0, -0.38, 0.17), 0.03, 0.22, 1, 8, axis='y'); b.box((0, -0.5, 0.11), (0.02, 0.2, 0.02), 2, 1.0)
    o = b.build('Rifle'); keys(o, 'rotation_euler', lambda t: (0, 0, 0), 24, 8, 8); export(out)

elif kind == 'crate':
    CR = material('Crate', panel_tex((0.72, 0.5, 0.22), 11, lines=3, stripe=(0.12, 0.12, 0.12))); RD = solid('RedLight', 1.0, 0.2, 0.15); b = MB([CR, RD]); b.box((0, 0, 0.5), (1.0, 1.0, 1.0), 0, 1.0); b.box((0, 0, 1.02), (0.5, 0.3, 0.06), 1); o = b.build('Crate')
    keys(o, 'rotation_euler', lambda t: (0, 0, 0), 24, 8, 8); export(out)

elif kind == 'boulder':
    RK = material('Boulder', rock_tex(14, (1.1, 1.0, 0.95), 0.0)); bm = displace_icosphere(1.0, 3, 23, 0.35); me = bpy.data.meshes.new('Boulder'); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new('Boulder', me); bpy.context.collection.objects.link(o); o.data.materials.append(RK)
    uvl = me.uv_layers.new()
    for p in me.polygons:
        for li, vi in zip(p.loop_indices, p.vertices): v = me.vertices[vi].co; uvl.data[li].uv = (math.atan2(v.y, v.x) / (2 * math.pi) + 0.5, v.z * 0.5 + 0.5)
    keys(o, 'rotation_euler', lambda t: (2 * math.pi * t / 8 * 0.5, 2 * math.pi * t / 8 * 0.3, 0), 24, 8, 4); export(out)

elif kind == 'tbc':
    WH = solid('White', 1.0, 1.0, 1.0); bpy.ops.object.text_add(location=(0, 0, 0)); t = bpy.context.active_object; t.data.body = 'TO BE CONTINUED...'; t.data.extrude = 0.08; t.data.size = 1.0; t.data.align_x = 'CENTER'; t.data.align_y = 'CENTER'
    bpy.ops.object.convert(target='MESH'); t = bpy.context.active_object; t.data.materials.append(WH); t.rotation_euler = (math.pi / 2, 0, 0); bpy.ops.object.transform_apply(rotation=True); t.name = 'Text'; export(out)

else:
    STEM = material('Stem', panel_tex((0.15, 0.1, 0.5), 15, lines=2, wear=0.5)); CAP = material('Cap', panel_tex((0.2, 0.2, 0.7), 16, lines=2, wear=0.4)); RIM = solid('A_Rim', 0.4, 0.95, 1.0); parts = []
    for k in range(6):
        x, y = (R.random() - 0.5) * 6, (R.random() - 0.5) * 4; h = 0.8 + R.random() * 1.6; r = 0.7 + R.random() * 1.0
        sb = MB([STEM]); sb.cyl((0, 0, h / 2), 0.22, h, 0, 8, r2=0.14); stem = part(sb, 'Stem%d' % k, (x, y, 0))
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=20, ring_count=8, location=(0, 0, h)); cap = bpy.context.active_object; cap.scale = (1, 1, 0.45); cap.name = 'Cap%d' % k; cap.data.materials.append(CAP); cap.parent = stem
        for p in cap.data.polygons: p.use_smooth = True
        rb = MB([RIM]); rb.cyl((0, 0, 0), r * 0.97, 0.04, 0, 20, axis='z'); rim = part(rb, 'Rim%d' % k, (0, 0, h - 0.05), stem); rim.parent = stem
        keys(stem, 'rotation_euler', lambda t, k=k: (0.04 * math.sin(2 * math.pi * t / 4 + k), 0.04 * math.cos(2 * math.pi * t / 4 + k * 0.7), 0), 24, 8, 4)
    export(out)
