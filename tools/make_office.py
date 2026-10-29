"""blender -b -P make_office.py -- <out.glb>
The Agency office of the cutscenes (retro grey 1980s: steel desk, beige CRT, long architect lamp, manila folders, white venetian blinds on a floor-to-ceiling window wall with dark mullions), 9 x 7 x 3.1 m
(Blender: Z up, floor z = 0, centred on x = y = 0; Unity z = -Blender y, Unity x = -Blender x). The player stands at Blender y = +1.4 looking along -y at: the steel desk (y = -0.9) with Agent Jonesy behind it,
and the window wall (y = -3.5) with the storm outside. Left (Blender +x): filing cabinets, pinboard, clock, bin; right (Blender -x): bookcase, plant; behind (+y): the wooden door with a transom.
Nodes: Shell (floor, walls, ceiling, skirting, window frame - mesh collider) | Furniture (desk, chairs, cabinets, bookcase - mesh collider) | Props | Plants | Glow (A_ = unlit: CRT screens, storm behind the blinds,
the slats of the blinds themselves, ceiling panels, lamp) | TextTopSecret / TextName (Impact)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
RW, RD, RH = 9.0, 7.0, 3.1; HX, HY = RW / 2, RD / 2; DY = -0.9; TZ = 0.76
def tex_wall(seed): W = H = 128; n = fbm(W, H, 6, seed); return rgba(0.78 + 0.05 * n, 0.8 + 0.05 * n, 0.82 + 0.05 * n, 1.0)
def tex_floor(seed): W = H = 128; U, V = grid(W, H); n = fbm(W, H, 14, seed); t = (((U * 6) % 1 < 0.5) ^ ((V * 6) % 1 < 0.5)).astype(np.float32); c = 0.42 + 0.05 * n + 0.02 * t; return rgba(c, c * 1.02, c * 1.05, 1.0)
def tex_steel(seed, col=(0.62, 0.64, 0.64)): W = H = 128; n = fbm(W, H, 10, seed); return rgba(col[0] * (0.88 + 0.2 * n), col[1] * (0.88 + 0.2 * n), col[2] * (0.88 + 0.2 * n), 1.0)
def tex_desktop(seed): W = H = 128; U, V = grid(W, H); n = fbm(W, H, 8, seed); edge = (U < 0.03) | (U > 0.97) | (V < 0.03) | (V > 0.97); c = np.stack([0.46 + 0.06 * n, 0.5 + 0.06 * n, 0.47 + 0.06 * n], -1); c = np.where(edge[..., None], np.array((0.3, 0.32, 0.3)), c); return rgba(c[..., 0], c[..., 1], c[..., 2], 1.0)
def tex_beige(seed): W = H = 64; n = fbm(W, H, 10, seed); return rgba(0.82 * (0.94 + 0.1 * n), 0.78 * (0.94 + 0.1 * n), 0.66 * (0.94 + 0.1 * n), 1.0)
def tex_manila(seed): W = H = 64; n = fbm(W, H, 10, seed); return rgba(0.86 * (0.94 + 0.1 * n), 0.72 * (0.94 + 0.1 * n), 0.46 * (0.94 + 0.1 * n), 1.0)
def tex_wood(seed, col=(0.45, 0.28, 0.16)):
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 5, seed); g = 0.5 + 0.5 * np.sin((V * 22 + n * 5) * np.pi); k = 0.8 + 0.2 * g * n + 0.06 * g; return rgba(col[0] * k, col[1] * k, col[2] * k, 1.0)
def tex_books(seed):
    W, H = 256, 64; r = np.random.default_rng(seed); img = np.zeros((H, W, 4), np.float32); img[..., 3] = 1; x = 0
    while x < W:
        w = int(r.integers(5, 12)); col = np.array([r.random() * 0.6 + 0.2, r.random() * 0.5 + 0.15, r.random() * 0.5 + 0.15]); img[:, x:x + w, :3] = col[None, None, :] * (0.7 + 0.3 * r.random()); img[:, x:x + 1, :3] *= 0.5
        yb = int(r.integers(10, 50)); img[yb:yb + 4, x:x + w, :3] = np.array((0.9, 0.8, 0.4)); x += w
    return img
def tex_crt(seed):
    W, H = 128, 96; U, V = grid(W, H); n = fbm(W, H, 5, seed); r, g, b = ramp(V, [(0, (0.85, 0.9, 1.0)), (0.5, (0.45, 0.65, 0.9)), (1, (0.2, 0.3, 0.55))]); c = 0.8 + 0.2 * n; scan = 0.9 + 0.1 * (np.arange(H)[:, None] % 2); return rgba(r * c * scan, g * c * scan, b * c * scan, 1.0)
def tex_crt2(seed):
    W, H = 128, 96; r_ = np.random.default_rng(seed); img = np.zeros((H, W, 4), np.float32); img[..., :3] = np.array((0.02, 0.06, 0.05)); img[..., 3] = 1
    for y in range(8, H - 4, 6):
        x = 6
        while x < W - 8: w = int(r_.integers(3, 18)); img[y:y + 2, x:x + w, :3] = np.array((0.3, 1.0, 0.55)) * (0.5 + 0.5 * r_.random()); x += w + int(r_.integers(2, 6))
    return img
def tex_window():
    W = H = 256; U, V = grid(W, H); T = 2 * np.pi; n = np.sin(T * (U * 3 + 0.25 * np.sin(T * V * 2))) + np.sin(T * (V * 4 + 0.3 * np.sin(T * U * 3))) + np.sin(T * ((U + V) * 2)); n = n / 3 * 0.5 + 0.5
    r, g, b = ramp(n, [(0, (0.14, 0.1, 0.5)), (0.5, (0.5, 0.3, 1.0)), (1, (1.0, 0.8, 1.0))]); glow = np.exp(-(((U - 0.5) ** 2) / 0.08 + ((V - 0.5) ** 2) / 0.08)); return rgba(r + glow * 0.4, g + glow * 0.3, b + glow * 0.3, 1.0)
def tex_blinds():                                                                                                  # slats with gaps (alpha) so the storm light shows between them
    W, H = 16, 128; img = np.zeros((H, W, 4), np.float32); idx = np.arange(H)
    for y in range(H):
        ph = y % 8
        if ph < 6: s = 0.9 - 0.25 * (ph / 6.0); img[y, :, :3] = np.array((0.86, 0.88, 0.9)) * s; img[y, :, 3] = 1.0
    return img
def tex_clock():
    W = H = 128; U, V = grid(W, H); d = np.hypot(U - 0.5, V - 0.5) * 2; face = d < 0.86; rim = (d >= 0.86) & (d < 1.0); ticks = (np.abs(np.hypot(U - 0.5, V - 0.5) * 2 - 0.75) < 0.07) & ((np.round(np.arctan2(V - 0.5, U - 0.5) / (np.pi / 6)) * (np.pi / 6) - np.arctan2(V - 0.5, U - 0.5)) ** 2 < 0.004)
    hands = ((np.abs(U - 0.5) < 0.015) & (V > 0.5) & (V < 0.8)) | ((np.abs(V - 0.5) < 0.015) & (U > 0.5) & (U < 0.7)); c = np.stack([np.where(face, 0.92, 0.2), np.where(face, 0.94, 0.3), np.where(face, 0.96, 0.6)], -1); c = np.where(ticks[..., None] | hands[..., None], np.array((0.1, 0.1, 0.15)), c); return rgba(c[..., 0], c[..., 1], c[..., 2], 1.0)
def tex_map(seed): W = H = 128; U, V = grid(W, H); n = fbm(W, H, 6, seed); land = n > 0.5; g = ((U * 10) % 1 < 0.04) | ((V * 10) % 1 < 0.04); c = np.where(land[..., None], np.array((0.45, 0.62, 0.4)), np.array((0.18, 0.32, 0.5))); c = np.where(g[..., None], c * 0.6 + 0.4, c); return rgba(c[..., 0], c[..., 1], c[..., 2], 1.0)
M = dict(wall=material('WallPaint', tex_wall(2)), floor=material('Carpet', tex_floor(1)), steel=material('Steel', tex_steel(6)), steel2=material('SteelDark', tex_steel(7, (0.4, 0.42, 0.44))), top=material('DeskTop', tex_desktop(8)), beige=material('Beige', tex_beige(9)),
         manila=material('Manila', tex_manila(10)), wood=material('Wood', tex_wood(3)), wood2=material('WoodLight', tex_wood(4, (0.6, 0.42, 0.25))), white=material('White', tex_solid(0.93, 0.93, 0.91)), black=material('BlackPlastic', tex_solid(0.05, 0.05, 0.06)), brass=material('Brass', tex_solid(0.8, 0.62, 0.22)),
         red=material('Red', tex_solid(0.75, 0.1, 0.08)), leaf=material('Leaf', tex_solid(0.2, 0.5, 0.22)), pot=material('Pot', tex_solid(0.8, 0.8, 0.78)), frame=material('FrameDark', tex_solid(0.16, 0.17, 0.2)), grey=material('GreyFabric', tex_solid(0.42, 0.45, 0.5)),
         paper=material('Paper', tex_solid(0.95, 0.95, 0.9)), cork=material('Cork', tex_wood(7, (0.6, 0.45, 0.28))), map=material('Map', tex_map(30)), clock=material('Clock', tex_clock()), books=[material('Books%d' % i, tex_books(10 + i)) for i in range(3)], bin=material('Bin', tex_solid(0.5, 0.52, 0.55)),
         A_Crt=material('A_Crt', tex_crt(1)), A_Crt2=material('A_Crt2', tex_crt2(2)), A_Window=material('A_Window', tex_window()), A_Blinds=material('A_Blinds', tex_blinds()), A_Panel=material('A_Panel', tex_solid(1.0, 0.98, 0.92)), A_Lamp=material('A_Lamp', tex_solid(1.0, 0.95, 0.75)))
def mb(keys): return MB([M[k] if not k.startswith('books') else M['books'][int(k[-1])] for k in keys]), {k: i for i, k in enumerate(keys)}
UVQ = [(0, 0), (1, 0), (1, 1), (0, 1)]
# ---------------------------------------------------------------- shell + the window wall
sh, si = mb(['floor', 'wall', 'white', 'frame', 'steel2', 'wood', 'brass', 'steel'])
sh.box((0, 0, 0.015), (RW, RD, 0.03), si['floor'], 1.5); sh.box((0, 0, RH + 0.1), (RW + 0.4, RD + 0.4, 0.2), si['white'], 2.0)
sh.box((0, HY + 0.1, RH / 2), (RW + 0.4, 0.2, RH), si['wall'], 2.0)                                                                  # back wall (door side)
for sx in (-1, 1): sh.box((sx * (HX + 0.1), 0, RH / 2), (0.2, RD, RH), si['wall'], 2.0)
sh.box((0, -HY - 0.15, RH / 2), (RW + 0.4, 0.1, RH), si['frame'], 2.0)                                                                 # the outside skin behind the glass (dark, the storm is a plane in front of it)
for sy in (-1, 1): sh.box((0, sy * (HY - 0.03), 0.07), (RW, 0.06, 0.14), si['white'], 1.0)
for sx in (-1, 1): sh.box((sx * (HX - 0.03), 0, 0.07), (0.06, RD, 0.14), si['white'], 1.0)
for k in range(7): sh.box((-4.35 + k * 1.45, -HY + 0.05, RH / 2), (0.12, 0.14, RH), si['frame'], 1.0)                                  # mullions
for z in (0.04, 2.45, RH - 0.05): sh.box((0, -HY + 0.05, z), (RW, 0.14, 0.1), si['frame'], 1.0)                                         # sill, transom, head
sh.box((HX - 0.04, HY - 1.15, 1.05), (0.08, 1.1, 2.1), si['wood'], 1.0); sh.box((HX - 0.08, HY - 1.15, 2.14), (0.1, 1.3, 0.06), si['white'], 1.0); sh.box((HX - 0.08, HY - 1.15, 2.65), (0.1, 1.1, 0.5), si['frame'], 1.0)     # door + frame + transom
sh.box((HX - 0.1, HY - 1.7, 1.0), (0.05, 0.1, 0.08), si['brass'], 1.0)
sh.build('Shell')
# ---------------------------------------------------------------- furniture
f, fi = mb(['steel', 'steel2', 'top', 'grey', 'black', 'wood', 'wood2', 'books0', 'books1', 'books2', 'white', 'bin', 'brass', 'pot'])
f.box((0, DY, 0.72), (2.2, 1.0, 0.05), fi['top'], 1.2); f.box((0, DY - 0.02, 0.69), (2.16, 0.96, 0.04), fi['steel'], 1.0)                 # steel desk top
f.box((0.98, DY, 0.35), (0.02, 0.92, 0.7), fi['steel'], 1.0); f.box((0.0, DY - 0.45, 0.4), (2.0, 0.02, 0.6), fi['steel'], 1.0)           # right panel, modesty panel
f.box((-0.78, DY, 0.35), (0.46, 0.92, 0.7), fi['steel'], 1.0)                                                                         # drawer pedestal
for k in range(3): f.box((-0.78, DY + 0.465, 0.12 + k * 0.22), (0.4, 0.02, 0.18), fi['steel2'], 1.0); f.box((-0.78, DY + 0.485, 0.14 + k * 0.22), (0.14, 0.03, 0.02), fi['brass'], 1.0)
def chair(x, y, rot, fabric, tall):
    c = MB([M['grey'], M['black'], M['steel2']]); ca, sa = math.cos(rot), math.sin(rot)
    def P(dx, dy): return (x + dx * ca - dy * sa, y + dx * sa + dy * ca)
    px, py = P(0, 0); c.box((px, py, 0.48), (0.55, 0.52, 0.1), 0, 1.0); bx, by = P(0, 0.26); c.box((bx, by, 0.48 + (0.3 if tall else 0.22)), (0.5, 0.07, 0.55 if tall else 0.4), 0, 1.0); c.cyl((px, py, 0.27), 0.04, 0.3, 2, 8)
    for k in range(5): a = k * 2 * math.pi / 5; c.box((px + math.cos(a) * 0.22, py + math.sin(a) * 0.22, 0.07), (0.34, 0.04, 0.04), 1, 1.0); c.cyl((px + math.cos(a) * 0.36, py + math.sin(a) * 0.36, 0.04), 0.04, 0.05, 1, 8)
    return c
chair(-1.6, -2.0, 0.0, 0, True).build('ChairExec'); chair(-0.8, 0.45, math.pi, 1, False).build('ChairV0'); chair(0.8, 0.45, math.pi, 1, False).build('ChairV1')
# filing cabinets along the left wall (Blender +x), a bin, bookcase on the right wall
for k in range(3):
    y_ = -2.3 + k * 0.7; f.box((HX - 0.35, y_, 0.66), (0.6, 0.62, 1.32), fi['steel'], 1.0)
    for d in range(4): f.box((HX - 0.656, y_, 0.18 + d * 0.31), (0.012, 0.52, 0.26), fi['steel2'], 1.0); f.box((HX - 0.675, y_, 0.22 + d * 0.31), (0.03, 0.2, 0.025), fi['brass'], 1.0)
f.cyl((HX - 0.4, 0.5, 0.25), 0.2, 0.5, fi['bin'], 16, r2=0.24)
BX = -HX + 0.25; f.box((BX, 0.6, 0.9), (0.4, 2.4, 1.8), fi['wood'], 1.0)
for r in range(4): f.box((BX, 0.6, 0.1 + r * 0.45), (0.38, 2.3, 0.04), fi['wood2'], 1.0)
for r in range(4): f.quad((BX + 0.206, -0.55, 0.14 + r * 0.45), (BX + 0.206, 1.75, 0.14 + r * 0.45), (BX + 0.206, 1.75, 0.5 + r * 0.45), (BX + 0.206, -0.55, 0.5 + r * 0.45), fi['books%d' % (r % 3)], UVQ)
f.box((0, 0, 0.0), (0.01, 0.01, 0.01), fi['white'], 1.0)
f.build('Furniture')
# ---------------------------------------------------------------- props on the desk and walls
p, pi_ = mb(['beige', 'black', 'steel', 'steel2', 'white', 'red', 'manila', 'paper', 'brass', 'wood2', 'frame', 'cork', 'map', 'clock', 'grey'])
CX = 0.0                                                                                                             # CRT (faces +y: the visitor): body, bezel, base
p.box((CX, DY - 0.2, TZ + 0.2), (0.5, 0.42, 0.4), pi_['beige'], 1.0); p.box((CX, DY - 0.2 + 0.01, TZ + 0.02), (0.26, 0.26, 0.04), pi_['beige'], 1.0); p.box((CX, DY + 0.005, TZ + 0.2), (0.44, 0.03, 0.34), pi_['black'], 1.0)
p.box((CX, DY + 0.0, TZ + 0.025), (0.46, 0.17, 0.03), pi_['beige'], 1.0); p.box((CX + 0.3, DY + 0.02, TZ + 0.02), (0.06, 0.09, 0.03), pi_['beige'], 1.0)    # keyboard, mouse
for k in range(3): p.box((0.6 + 0.04 * k, DY - 0.08 + 0.025 * k, TZ + 0.013 + 0.012 * k), (0.34, 0.26, 0.012), pi_['manila'], 1.0)                      # folders
p.box((0.62, DY - 0.05, TZ + 0.045), (0.34, 0.012, 0.02), pi_['red'], 1.0)
for k in range(2): p.box((-0.85, DY - 0.2 + 0.02 * k, TZ + 0.012 + 0.012 * k), (0.28, 0.2, 0.01), pi_['paper'], 1.0)
p.cyl((-0.45, DY + 0.32, TZ + 0.05), 0.045, 0.1, pi_['white'], 14); p.box((-0.45, DY + 0.32, TZ + 0.06), (0.095, 0.095, 0.02), pi_['red'], 1.0)    # mug with a red stripe
p.box((0.2, DY + 0.38, TZ + 0.04), (0.42, 0.08, 0.06), pi_['wood2'], 1.0); p.box((0.2, DY + 0.42, TZ + 0.055), (0.34, 0.01, 0.04), pi_['brass'], 1.0)    # nameplate
p.box((0.85, DY - 0.3, TZ + 0.04), (0.28, 0.2, 0.08), pi_['steel'], 1.0); p.box((0.85, DY - 0.2, TZ + 0.05), (0.04, 0.04, 0.03), pi_['red'], 1.0); p.cyl((0.85, DY - 0.32, TZ + 0.32), 0.016, 0.5, pi_['steel2'], 6)     # lamp base + switch + arm
p.box((0.7, DY - 0.32, TZ + 0.6), (0.7, 0.14, 0.07), pi_['steel'], 1.0)                                                              # the long shade
p.box((HX - 0.015, 1.8, 2.2), (0.02, 0.66, 0.66), pi_['frame'], 1.0); p.quad((HX - 0.032, 1.52, 1.93), (HX - 0.032, 2.08, 1.93), (HX - 0.032, 2.08, 2.47), (HX - 0.032, 1.52, 2.47), pi_['clock'], UVQ)       # round clock face on its own panel
p.box((HX - 0.03, -1.0, 1.9), (0.04, 1.8, 0.9), pi_['cork'], 1.0)                                                                       # pinboard over the cabinets
for k in range(7): p.box((HX - 0.04, -1.6 + 0.2 * (k % 4) + 0.05 * (k // 4), 1.65 + 0.2 * (k % 3)), (0.01, 0.13, 0.15), pi_['paper'] if k % 3 else pi_['red'], 1.0)
p.box((-HX + 0.015, -2.2, 2.0), (0.02, 1.0, 0.7), pi_['frame'], 1.0); p.quad((-HX + 0.032, -2.65, 1.7), (-HX + 0.032, -1.75, 1.7), (-HX + 0.032, -1.75, 2.3), (-HX + 0.032, -2.65, 2.3), pi_['map'], UVQ)
p.build('Props')
# plants: pots with spiky leaves (separate rotated cones)
parts = []; rnd = np.random.default_rng(5)
def plant(name, x, y, h):
    pm = MB([M['pot']]); pm.cyl((0, 0, 0.2), 0.22, 0.4, 0, 14, r2=0.26); o = pm.build(name + 'Pot'); o.location = (x, y, 0); parts.append(o)
    for k in range(11):
        lm = MB([M['leaf']]); lm.cyl((0, 0, h * 0.5), 0.07, h, 0, 5, r2=0.0); o = lm.build('%sLeaf%d' % (name, k)); a = k * 2.4; o.location = (x, y, 0.38); o.rotation_euler = (rnd.uniform(0.2, 0.7) * math.cos(a), rnd.uniform(0.2, 0.7) * math.sin(a), 0); parts.append(o)
plant('PlantA', HX - 0.5, 2.3, 0.9); plant('PlantB', -HX + 0.5, -2.7, 0.8); plant('PlantC', 3.3, -HY + 0.5, 1.1)
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join(); parts[0].name = 'Plants'
# ---------------------------------------------------------------- glow: CRT screens, the storm behind the window, the slats of the blinds, ceiling panels, the lamp
gl = MB([M['A_Crt'], M['A_Crt2'], M['A_Window'], M['A_Panel'], M['A_Lamp'], M['A_Blinds']])
gl.quad((CX - 0.17, DY + 0.025, TZ + 0.06), (CX + 0.17, DY + 0.025, TZ + 0.06), (CX + 0.17, DY + 0.025, TZ + 0.34), (CX - 0.17, DY + 0.025, TZ + 0.34), 0, UVQ)                      # CRT screen (in front of the black bezel)
gl.quad((-0.84, DY + 0.35, TZ + 0.0), (-0.52, DY + 0.35, TZ + 0.0), (-0.52, DY + 0.35, TZ + 0.0), (-0.84, DY + 0.35, TZ + 0.0), 1, UVQ) if False else None
gl.quad((-HX + 0.2, -HY - 0.08, 0.1), (HX - 0.2, -HY - 0.08, 0.1), (HX - 0.2, -HY - 0.08, RH - 0.1), (-HX + 0.2, -HY - 0.08, RH - 0.1), 2, UVQ)                                 # the storm
for k in range(6):                                                                                                                                                              # blinds, drawn to about 70 % in each pane
    x0 = -4.29 + k * 1.45; x1 = x0 + 1.33; top = 2.4 if k % 2 == 0 else 2.3; bot = 0.5 + 0.35 * (k % 3)
    gl.quad((x0, -HY + 0.12, bot), (x1, -HY + 0.12, bot), (x1, -HY + 0.12, top), (x0, -HY + 0.12, top), 5, [(0, 0), (1, 0), (1, (top - bot) * 8), (0, (top - bot) * 8)])
for x, y in ((-2.0, -2.0), (2.0, -2.0), (-2.0, 1.2), (2.0, 1.2)): gl.box((x, y, RH - 0.03), (1.2, 0.6, 0.04), 3, 1.0)
gl.quad((0.7 - 0.34, DY - 0.32 + 0.075, TZ + 0.6), (0.7 + 0.34, DY - 0.32 + 0.075, TZ + 0.6), (0.7 + 0.34, DY - 0.32 + 0.075, TZ + 0.6), (0.7 - 0.34, DY - 0.32 + 0.075, TZ + 0.6), 4, UVQ) if False else None
gl.box((0.7, DY - 0.32, TZ + 0.555), (0.62, 0.1, 0.012), 4, 1.0)                                                                                                                  # the lit underside of the lamp shade
gl.build('Glow')
# ---------------------------------------------------------------- lettering (Impact)
fnt = bpy.data.fonts.load(r'C:\Windows\Fonts\impact.ttf')
def text_obj(name, body, size, loc, rot, mat, extrude=0.004):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.font = fnt; cu.size = size; cu.extrude = extrude; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    o = bpy.data.objects.new(name, cu); bpy.context.collection.objects.link(o); o.rotation_euler = rot; o.location = loc
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.convert(target='MESH'); o.data.materials.append(mat); return o
text_obj('TextTopSecret', 'TOP SECRET', 0.04, (0.62, DY - 0.07, TZ + 0.05), (0, 0, math.pi), M['red'], 0.002)
text_obj('TextName', 'AGENT JONES', 0.04, (0.2, DY + 0.425, TZ + 0.058), (math.pi / 2, 0, math.pi), M['black'], 0.003)
export(out)
