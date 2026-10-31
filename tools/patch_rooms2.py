s = open('end21_rooms.py').read()
a = s.index("if kind == 'facility':"); b_ = s.index("elif kind == 'chamber':")
new = '''if kind == 'facility':
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
    bx(0, 0.62, 2.28, 0.9, 0.12, 0.7, S_, 1.0); bx(0, 0.28, 2.28, 0.14, 0.56, 0.14, D_, 1.0)  # legrest on a post (feet side, towards the player)
    for sx in (-0.5, 0.5): bx(sx, 0.62, 3.0, 0.1, 0.1, 0.7, D_, 1.0)                    # armrests
    bx(0, 0.02, 3.05, 1.3, 0.04, 1.6, A_, 1.0)                                              # a hazard-yellow floor plate
    bk = MB([D_, S_, Y_]); bk.box((0, 0, 0.55), (0.9, 0.12, 1.1), 0, 1.0, top=1); bk.box((0, 0.0, 1.18), (0.34, 0.16, 0.26), 1, 1.0); o = bk.build('ChairBack'); o.location = Z(0, 0.44, 3.4); o.rotation_euler = (math.radians(-24), 0, 0)
    sc_ = MB([Y_, S_]); sc_.cyl((0, 0, 0), 0.52, 0.04, 1, 20)                               # the scanner crown: a ring round the head
    bpy.ops.mesh.primitive_torus_add(major_radius=0.44, minor_radius=0.032, major_segments=40, minor_segments=8); ring = bpy.context.active_object; ring.name = 'ScanRing'; ring.data.materials.append(CYA); ring.location = Z(0, 1.38, 3.55); ring.rotation_euler = (math.radians(-24 - 90 + 90), 0, 0)
    ring.rotation_euler = (math.radians(-24), 0, 0)
    arm = MB([S_, D_]); arm.box((0, 0, 0), (0.1, 0.1, 1.5), 0, 1.0); arm.box((0, -0.35, 0.72), (0.1, 0.8, 0.1), 0, 1.0); oa = arm.build('ScanArm'); oa.location = Z(0, 1.1, 3.7); oa.rotation_euler = (math.radians(-24), 0, 0)
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
'''
open('end21_rooms.py', 'w').write(s[:a] + new + s[b_:])
print('ok')
