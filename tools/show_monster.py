"""python show_monster.py [quick]  -> cache/showdown/monster.glb + monster_trace.json  (ACTOR=monster)"""
import os, sys
os.environ['ACTOR'] = 'monster'
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
import sd_common as C
OUT = os.path.join(HERE_G, 'cache', 'showdown'); os.makedirs(OUT, exist_ok=True)
rng = random.Random(1109); S = new_scene(1109)
D = {sd: (S.tr['arm_x_' + sd].default, S.tr['arm_y_' + sd].default, S.tr['arm_z_' + sd].default) for sd in SIDES}
SGN = {'l': 1, 'r': -1}

def arm_off(t, sd, dx=0.0, dy=0.0, dz=0.0, elb=0.5, curl=0.3):
    x, y, z = D[sd]; K(S, t, **{'arm_w_' + sd: 1.0, 'arm_x_' + sd: x + dx, 'arm_y_' + sd: y + dy, 'arm_z_' + sd: z + dz, 'arm_elb_' + sd: elb, 'curl_' + sd: curl})
def arms_off(t, dx=0.0, dy=0.0, dz=0.0, elb=0.5): arm_off(t, 'l', dx, dy, dz, elb); arm_off(t, 'r', dx, dy, dz, elb)
def face_key(t, ux, uz, tx, tz):
    set_root(S, t, ux, uz, heading_to(ux, uz, tx, tz))
def breathe(t0, t1, step=1.7, amp=1.0):
    t = t0; n = 0
    while t < t1:
        K(S, t, sp_bend=S.get('sp_bend', t) + 0.0, shl_up_l=amp * 1.5 * (1 if n % 2 else -1), shl_up_r=amp * 1.5 * (1 if n % 2 else -1)); t += step; n += 1
def roar(ta, tp, power=1.0, tend=None):
    tend = tend or tp + 2.6
    K(S, ta, sp_bend=S.get('sp_bend', ta), hd_pitch=S.get('hd_pitch', ta), jaw=0.0, shk=0.0)
    K(S, tp - 0.9, sp_bend=16 * power, hd_pitch=16 * power, jaw=0.0, pel_pitch=5)
    K(S, tp - 0.2, sp_bend=-24 * power, hd_pitch=-34 * power, jaw=38 * power, shk=0.55 * power, pel_pitch=-7)
    arms_off(tp - 0.2, 0.28 * power, 0.45 * power, 0.12 * power, 0.2)
    K(S, tp + 1.0, jaw=30 * power, shk=0.4 * power); K(S, tend, sp_bend=4, hd_pitch=0, jaw=0.0, shk=0.0, pel_pitch=0)
    arms_off(tend, 0, 0, 0)
def stomp(t, sd, h=0.1, dip=-0.03):
    S.K('ft_lift_' + sd, t - 1.0, 0.0); S.K('ft_lift_' + sd, t - 0.45, h * BODY_H); S.K('ft_lift_' + sd, t, 0.0)
    S.K('ft_pitch_' + sd, t - 0.45, -14); S.K('ft_pitch_' + sd, t, 3); S.K('ft_pitch_' + sd, t + 0.3, 0)
    K(S, t - 0.5, pel_dy=0.01); K(S, t, pel_dy=dip); K(S, t + 0.6, pel_dy=0.0)
    S.K('shk', t - 0.1, S.get('shk', t - 0.1)); S.K('shk', t, max(S.get('shk', t), 0.5)); S.K('shk', t + 0.7, 0.0)
def hit(t, power=1.0, side=1):
    K(S, t - 0.05, sp_bend=S.get('sp_bend', t - 0.05), hd_pitch=S.get('hd_pitch', t - 0.05), hd_yaw=S.get('hd_yaw', t - 0.05), pel_dz=S.get('pel_dz', t - 0.05))
    K(S, t + 0.12, sp_bend=-16 * power, hd_pitch=-18 * power, hd_yaw=side * 14 * power, pel_dz=-0.04 * power, shk=0.6 * power, jaw=22 * power)
    K(S, t + 1.4, sp_bend=2, hd_pitch=0, hd_yaw=0, pel_dz=0.0, shk=0.0, jaw=0.0)
def stand_pose(t, ux, uz, tx, tz):
    face_key(t, ux, uz, tx, tz); plant_all(S, t)

# ================================================================== 0 - 47  the monster arrives
x0, z0 = C.MON_START; xn, zn = C.MON_NEAR
set_root(S, 0, x0, z0, heading_to(x0, z0, xn, zn)); plant_all(S, 0)
arms_off(0, 0, 0, 0)
land = [3.0]
while land[-1] < 22: land.append(land[-1] + rng.uniform(3.0, 3.5))
land += [26.0, 31.4, 35.4, 38.6, 41.9, 45.2]
WP = [(0, x0, z0), (26, 40, 147), (31.4, 38, 145), (45.6, xn, zn), (47, xn, zn)]
roar(24.6, C.T_ROAR1, 1.0, 31.0)
end = walk(S, rng, 0, 47, WP, landings=land, skip=[(24.0, 31.0)], lean=9.0, bob=0.024, swing=0.12, lift=0.075)
stomp_marks = [35.4, 38.6]
for tm in stomp_marks: K(S, tm, shk=0.35); K(S, tm + 0.8, shk=0.0)

# ================================================================== 47 - 81.5  the Forest is threatened
face_key(47.5, xn, zn, 0, 0)
for i, (t, y, p) in enumerate([(48, 24, 8), (52, -30, 10), (56, 12, 16), (59.5, 0, 22)]): K(S, t, hd_yaw=y, hd_pitch=p)
K(S, 56, sp_bend=12); K(S, 60.5, sp_bend=22, pel_pitch=6)
arms_off(57, 0.1, 0.05, 0.05); arms_off(60.5, 0.22, 0.02, 0.0, 0.3)
K(S, 60.0, jaw=0.0); K(S, 61.5, jaw=34)
tx = -38.0
t = 61.0
while t <= 72.0:                                                   # the beam sweeps the rim of the pit
    aim_head(S, t, (-38 + 76 * (t - 61.0) / 11.0, -12, 48)); K(S, t, shk=0.25); t += 0.5
K(S, 72.6, jaw=14, shk=0.0)
for tb, tgt in zip(C.T_BLAST, [(6, -22, 12), (-6, -22, -4)]):     # two big blasts into the pit
    K(S, tb - 1.6, sp_bend=14, hd_pitch=-24, jaw=6); aim_head(S, tb - 0.8, tgt)
    K(S, tb - 0.3, jaw=42, shk=0.7, sp_bend=26, pel_pitch=7); aim_head(S, tb, tgt); K(S, tb + 1.2, jaw=10, shk=0.1, sp_bend=8, pel_pitch=2)
stomp(79.5, 'l', 0.12); stomp(81.0, 'r', 0.1)
arms_off(79.0, 0.28, 0.35, 0.2, 0.3); arms_off(81.5, 0.05, 0.0, 0.0)

# ================================================================== 81.5 - 116.5  the robot awakens
K(S, 82.0, hd_yaw=0.0, hd_pitch=4.0, jaw=0.0, sp_bend=6, shk=0.0)
rx_, rz_ = C.ROBOT_PATH[1][1], C.ROBOT_PATH[1][2]
pivot_yaw = heading_to(xn, zn, rx_, rz_)
K(S, 83.0, pel_yaw=S.get('pel_yaw', 83.0)); K(S, 88.0, pel_yaw=wrap_to(S.get('pel_yaw', 83.0), yaw_of(pivot_yaw)))
walk(S, rng, 83.0, 89.5, [(83, xn, zn), (89.5, xn, zn)], landings=[85.4, 88.4], face_travel=False, arms=False, close=False, lift=0.06)
for t, y in ((90.0, 0), (96, -18), (101, 12), (106, 0)): K(S, t, hd_yaw=y)
roar(90.2, 92.2, 0.9, 96.0)
for t, p in ((99, 6), (104, 10), (108, 6)): K(S, t, hd_pitch=p)
breathe(96, 116, 1.8, 1.2)
arms_off(100, 0.05, 0.02, 0.08); arms_off(110, 0.1, 0.05, 0.12)
tmp = walk(S, rng, 108, 116.5, [(108, xn, zn), (116.5, 6, 112)], landings=[111.5, 114.6], face_travel=False, arms=False, close=False, lift=0.07)

# ================================================================== 116.5 - 153.5  the first battle
face_key(117, 6, 112, rx_, rz_)
ROBX, ROBZ = C.robot_xz(120)
arms_off(118, 0.0, 0.18, 0.32, 0.35); arms_off(123, 0.02, 0.2, 0.34, 0.35)
K(S, 120, hd_yaw=-8, hd_pitch=4, jaw=10)
for i, th in enumerate(C.T_MISSILE): hit(th, 1.0 + 0.2 * i, 1 if i % 2 == 0 else -1)
K(S, 131.8, sp_bend=2, hd_pitch=0, jaw=0.0)
# gathering the beam, then firing it at the robot
K(S, 132.4, sp_bend=-12, hd_pitch=-26, jaw=18, shk=0.3, pel_pitch=-4); arms_off(133.0, 0.3, 0.3, 0.1, 0.3)
K(S, 134.4, jaw=44, shk=0.7, sp_bend=-22)
for t in (135.4, 135.9, 136.4, 136.9, 137.4, 137.9):
    rc = C.robot_chest(t); aim_head(S, t, (rc[0], rc[1], rc[2])); K(S, t, shk=0.5)
K(S, 138.6, jaw=12, shk=0.0, sp_bend=4, hd_pitch=0, hd_yaw=0)
roar(138.6, 140.0, 1.0, 143.0)
arms_off(144, 0.05, 0.0, 0.1)
w2 = walk(S, rng, 143.0, 153.0, [(143, 6, 112), (153, 30, 106)], landings=[145.6, 148.7, 151.8], face_travel=False, arms=False, close=False, lift=0.07)
face_key(144.0, 6, 112, 82, 82)
set_root(S, 153, 30, 106, heading_to(30, 106, 40, 98))
arms_off(152.0, 0.12, 0.3, 0.45, 0.4); K(S, 153.5, sp_bend=14, hd_pitch=8, jaw=20)

# ================================================================== 153.5 - 161  pushed over the rim, then the ambush
K(S, C.T_TACKLE - 0.2, sp_bend=14, hd_pitch=8, shk=0.0, pel_dz=0.0)
K(S, C.T_TACKLE + 0.3, sp_bend=-26, hd_pitch=-28, jaw=34, shk=0.9, pel_dz=-0.06)
# knocked back and over: airborne, then sinking out of sight behind the scenery
for sd in SIDES: S.K('ft_w_' + sd, C.T_TACKLE - 0.1, 1.0); S.K('ft_w_' + sd, C.T_TACKLE + 0.3, 0.0)
set_root(S, C.T_TACKLE, 30, 106); set_root(S, 157.6, 34, 122); set_root(S, 158.8, 46, 150); set_root(S, 160.0, 70, 160)
K(S, C.T_TACKLE, lift=0.0, pel_pitch=0); K(S, 157.4, lift=0.20, pel_pitch=-35); K(S, 158.6, lift=0.05, pel_pitch=-75)
K(S, 158.4, root_dy=0.0); K(S, 159.6, root_dy=-1.5)
K(S, 160.0, root_dy=-1.5, lift=0.0, pel_pitch=-75, jaw=0.0, shk=0.0)
# 160.0 - 161.0: the leap back, landing on the robot's shoulder
set_root(S, 160.0, 76, 150, heading_to(76, 150, 40, 100)); set_root(S, 160.55, 62, 126); set_root(S, C.T_TEAR, 50, 107)
K(S, 160.1, root_dy=-1.5, pel_pitch=-20, sp_bend=-10); K(S, 160.5, root_dy=-0.1, lift=0.30, pel_pitch=25, sp_bend=26); K(S, 160.82, root_dy=0.0, lift=0.06, pel_pitch=20, sp_bend=24)
K(S, C.T_TEAR, root_dy=0.0, lift=0.0, pel_pitch=10, sp_bend=24)
for sd in SIDES: S.K('ft_w_' + sd, 160.1, 0.0); S.K('ft_w_' + sd, 160.85, 0.0); S.K('ft_w_' + sd, 161.1, 1.0)
land_x, land_z = 50, 107
for sd in SIDES:
    ox, oz = stance_off(sd); a = math.radians(S.get('pel_yaw', 161.1))
    hx, hz = S.get('home_x', C.T_TEAR) * BODY_H + PEL_B[0], S.get('home_z', C.T_TEAR) * BODY_H + PEL_B[2]
    S.K('ft_wx_' + sd, 160.8, hx + ox * math.cos(a) + oz * math.sin(a)); S.K('ft_wz_' + sd, 160.8, hz - ox * math.sin(a) + oz * math.cos(a))
    S.K('ft_wx_' + sd, 164, S.get('ft_wx_' + sd, 160.8)); S.K('ft_wz_' + sd, 164, S.get('ft_wz_' + sd, 160.8)); S.K('ft_yaw_' + sd, 160.8, S.get('pel_yaw', 160.8))
S.K('pel_dy', 160.82, 0.0); S.K('pel_dy', 161.1, -0.04); S.K('pel_dy', 162.0, 0.0)
# the right hand grabs the robot's right shoulder and rips the arm off
reach_world(S, 160.95, 'r', (40 + 9.5, 66, 100.0), elb=0.5)
arm_off(C.T_TEAR + 0.5, 'r', 0.05, 0.2, -0.45, 0.3, 1.0)
arm_off(C.T_TEAR + 1.4, 'r', 0.1, 0.62, 0.05, 0.3, 1.0)
arm_off(C.T_TEAR + 0.3, 'l', 0.15, 0.1, 0.25)
K(S, C.T_TEAR + 0.4, jaw=40, shk=0.7, sp_bend=-20, hd_pitch=-25)
K(S, C.T_TEAR + 2.0, jaw=42, shk=0.5)

# ================================================================== 162 - 182  the monster dominates
K(S, 165.5, jaw=8, shk=0.0, sp_bend=10, hd_pitch=10)
arm_off(166, 'r', 0.08, 0.3, 0.1, 0.3, 1.0)
rcx, rcy, rcz = C.robot_chest(170)
t_prev = 166
for i, tp in enumerate(C.PEAKS_BATTLE):
    kind = i % 3
    aim_head(S, tp - 0.9, (rcx, rcy, rcz))
    if kind == 0: stomp(tp, 'l' if i % 2 == 0 else 'r', 0.13, -0.04)
    elif kind == 1:
        sd = 'l' if i % 2 == 0 else 'r'; reach_world(S, tp - 0.7, sd, (rcx, 28, rcz), elb=0.5); K(S, tp - 0.7, sp_bend=22)
        reach_world(S, tp, sd, (rcx, 2, rcz - 3), elb=0.5); K(S, tp, sp_bend=30, hd_pitch=18, shk=0.5); arm_off(tp + 1.0, sd, 0.05, 0.05, 0.1)
    else:
        K(S, tp - 0.7, sp_bend=8, hd_pitch=-12, jaw=40); K(S, tp, sp_bend=34, hd_pitch=26, jaw=6, shk=0.5, pel_pitch=8); K(S, tp + 1.0, sp_bend=10, hd_pitch=6, jaw=0, pel_pitch=0)
    K(S, tp + 1.2, shk=0.0)
K(S, 182, sp_bend=10, hd_pitch=8, jaw=0, shk=0.0, pel_pitch=0)

# ================================================================== 182 - 204  kicked back, stalking, then a lunge at the robot
K(S, C.T_KICK - 0.1, sp_bend=10, pel_dz=0.0); K(S, C.T_KICK + 0.35, sp_bend=-20, hd_pitch=-20, jaw=30, shk=0.8, pel_dz=-0.07); K(S, C.T_KICK + 2.0, sp_bend=8, pel_dz=0, jaw=0, shk=0.0)
for sd in SIDES: S.K('ft_w_' + sd, C.T_KICK - 0.1, 1.0)
walk(S, rng, 187.9, 192.5, [(187.9, 50, 107), (192.5, 40, 124)], landings=[190.0, 191.8], face_travel=False, arms=False, close=False, lift=0.08)
set_root(S, 192.6, 40, 124, heading_to(40, 124, 25, 62))
walk(S, rng, 193, 203.5, [(193, 40, 124), (198, 32, 114), (203.5, 26, 96)], landings=[195.5, 198.5, 201.4, 203.4], face_travel=False, arms=False, close=False, lift=0.07)
for t, y in ((194, 0), (199, 10), (203, 0)): K(S, t, hd_yaw=y)
arms_off(200, 0.15, 0.2, 0.3, 0.3); arms_off(203.8, 0.2, 0.55, 0.55, 0.3)
K(S, 202.0, sp_bend=14, hd_pitch=-8, jaw=34)
# the energy flash throws him back
fl = C.T_FLASH
K(S, fl - 0.05, sp_bend=14, pel_dz=0.0, shk=0.0); K(S, fl + 0.3, sp_bend=-28, hd_pitch=-30, jaw=44, shk=1.0, pel_dz=-0.1, pel_pitch=-8)
set_root(S, fl, 26, 96); set_root(S, fl + 2.0, 24, 108)
for sd in SIDES: S.K('ft_w_' + sd, fl - 0.05, 1.0)
arms_off(fl + 0.3, 0.3, 0.4, -0.1, 0.2)
K(S, fl + 2.5, sp_bend=8, hd_pitch=0, jaw=0.0, shk=0.0, pel_dz=0.0, pel_pitch=0)
face_key(207.5, 24, 108, 22, 82)
arms_off(208.0, 0.05, 0.0, 0.1)
land2 = walk(S, rng, 208.5, 213.8, [(208.5, 24, 108), (213.8, 22, 97)], landings=[210.5, 212.3, 213.8], face_travel=False, arms=False, close=False, lift=0.08)
arms_off(213.0, 0.1, 0.4, 0.45, 0.3)

# ================================================================== 215  punched away
tp0 = C.T_PUNCH
K(S, tp0 - 0.05, sp_bend=14, pel_dz=0.0, pel_pitch=0, shk=0.0, jaw=0.0)
K(S, tp0 + 0.3, sp_bend=-34, hd_pitch=-36, jaw=48, shk=1.0, pel_dz=-0.12, pel_pitch=-12)
for sd in SIDES: S.K('ft_w_' + sd, tp0 - 0.05, 1.0); S.K('ft_w_' + sd, tp0 + 0.25, 0.0)
set_root(S, tp0, 22, 97); set_root(S, tp0 + 1.6, 4, 125); set_root(S, tp0 + 3.2, -22, 150); set_root(S, C.T_LAND, -42, 168)
K(S, tp0, lift=0.0, root_dy=0.0); K(S, tp0 + 1.7, lift=0.55, pel_pitch=-70, pel_roll=40); K(S, C.T_LAND - 0.3, lift=0.04, pel_pitch=-110, pel_roll=60); K(S, C.T_LAND, lift=-0.02, pel_pitch=-92, pel_roll=20)
arms_off(tp0 + 0.4, 0.35, 0.45, -0.1, 0.2); arms_off(tp0 + 2.0, 0.3, 0.2, 0.3, 0.3)
# landing and lying on its back
lyx, lyz = -42, 168
K(S, C.T_LAND + 0.6, pel_dy=-0.26, pel_pitch=-88, pel_roll=0, jaw=10, shk=0.5); K(S, 222, pel_dy=-0.26, shk=0.0)
K(S, 222, hd_pitch=-20, jaw=4)
for sd in SIDES:
    S.K('ft_w_' + sd, C.T_LAND - 0.2, 0.0)
    S.K('ft_rx_' + sd, C.T_LAND, 0.1 * (1 if sd == 'l' else -1)); S.K('ft_ry_' + sd, C.T_LAND, -0.4); S.K('ft_rz_' + sd, C.T_LAND, 0.0)

# ================================================================== 222 - 254  recovers, returns, roars, and leaps
# stays lying until 226, rolls over, pushes up and stands
set_root(S, 226, lyx, lyz)
K(S, 226, pel_dy=-0.26, pel_pitch=-88, pel_roll=0, sp_bend=0)
K(S, 229, pel_pitch=-30, pel_roll=70, pel_dy=-0.28, sp_bend=10, hd_pitch=10)
K(S, 232, pel_pitch=40, pel_roll=0, pel_dy=-0.32, sp_bend=24, hd_pitch=16)            # on hands and knees
arm_off(229, 'l', 0.15, -0.35, 0.3, 0.3); arm_off(229, 'r', 0.15, -0.35, 0.3, 0.3)
arm_off(232, 'l', 0.2, -0.45, 0.45, 0.2); arm_off(232, 'r', 0.2, -0.45, 0.45, 0.2)
K(S, 236, pel_pitch=6, pel_roll=0, pel_dy=-0.04, sp_bend=14, hd_pitch=4)
arms_off(236, 0.05, 0.0, 0.1)
for sd in SIDES:
    S.K('ft_w_' + sd, 233.5, 0.0); S.K('ft_w_' + sd, 236.5, 1.0)
    ox, oz = stance_off(sd); a = math.radians(S.get('pel_yaw', 236)); hx, hz = S.get('home_x', 236) * BODY_H + PEL_B[0], S.get('home_z', 236) * BODY_H + PEL_B[2]
    S.K('ft_wx_' + sd, 233.0, hx + ox * math.cos(a) + oz * math.sin(a)); S.K('ft_wz_' + sd, 233.0, hz - ox * math.sin(a) + oz * math.cos(a))
    S.K('ft_yaw_' + sd, 233.0, S.get('pel_yaw', 233)); S.K('ft_lift_' + sd, 233.0, 0.0)
face_key(236.2, lyx, lyz, -85, 90)
K(S, 237, jaw=0.0, hd_pitch=0)
w3 = walk(S, rng, 237.5, 244.0, [(237.5, lyx, lyz), (244.0, -45, 150)], landings=[240.0, 242.4, 244.0], face_travel=True, arms=True, lift=0.075, bob=0.024, swing=0.12, lean=9, skip=[(243.2, 249)], close=False)
roar(243.4, C.T_LEAP, 1.4, 248.0)
fx_, fz_ = -45, 150
run_land = [248.6, 249.8, 251.0, 252.2, 253.4]
walk(S, rng, 248.0, 254.0, [(248.0, fx_, fz_), (254.0, -52, 124)], landings=run_land, face_travel=False, arms=False, close=False, lift=0.1, bob=0.03, lean=14)
set_root(S, 248.0, fx_, fz_, heading_to(fx_, fz_, -60, 110))
arms_off(250, 0.2, 0.35, 0.3, 0.3); arms_off(253, 0.1, 0.3, 0.45, 0.3)
K(S, 252.0, sp_bend=14, hd_pitch=4, jaw=22, pel_pitch=4)

# ================================================================== 254 - 272  the lunge, the sword in the eye, the fall
for sd in SIDES: S.K('ft_w_' + sd, 254.2, 1.0); S.K('ft_w_' + sd, 254.8, 0.0)
sx0, sz0 = -52, 124
set_root(S, 254.0, sx0, sz0); set_root(S, 258.0, -57, 114); set_root(S, C.T_STAB, -58, 111)
K(S, 254.0, lift=0.0, pel_dy=-0.12, sp_bend=22, pel_pitch=14); K(S, 255.4, lift=0.0, pel_dy=-0.18, sp_bend=26)
K(S, 256.5, lift=0.22, pel_dy=0.0, pel_pitch=30, sp_bend=34, hd_pitch=-10, jaw=40); K(S, 259.0, lift=0.14, pel_pitch=34, sp_bend=36, jaw=44); K(S, C.T_STAB, lift=0.1, pel_pitch=32, sp_bend=34, jaw=46)
reach_world(S, 257.0, 'r', (-62, 60, 106), elb=0.4); reach_world(S, 259.4, 'r', (-64, 70, 104), elb=0.4); reach_world(S, 257.0, 'l', (-58, 68, 104), elb=0.4); reach_world(S, 259.4, 'l', (-60, 74, 104), elb=0.4)
ts = C.T_STAB
K(S, ts + 0.2, jaw=48, hd_pitch=-26, sp_bend=-20, shk=1.0, lift=0.04, pel_pitch=6)
for sd in SIDES: S.K('ft_w_' + sd, ts - 0.1, 0.0); S.K('ft_w_' + sd, ts + 0.9, 1.0)
set_root(S, ts + 1.0, -57, 118); set_root(S, ts + 4, -52, 124)
for sd in SIDES:
    ox, oz = stance_off(sd); a = math.radians(S.get('pel_yaw', ts + 1)); hx, hz = S.get('home_x', ts + 1) * BODY_H + PEL_B[0], S.get('home_z', ts + 1) * BODY_H + PEL_B[2]
    S.K('ft_wx_' + sd, ts + 0.8, hx + ox * math.cos(a) + oz * math.sin(a)); S.K('ft_wz_' + sd, ts + 0.8, hz - ox * math.sin(a) + oz * math.cos(a)); S.K('ft_yaw_' + sd, ts + 0.8, S.get('pel_yaw', ts + 0.8))
    S.K('ft_lift_' + sd, ts + 0.8, 0.0); S.K('ft_pitch_' + sd, ts + 0.8, 0.0)
    S.K('ft_wx_' + sd, ts + 6, S.get('ft_wx_' + sd, ts + 0.8)); S.K('ft_wz_' + sd, ts + 6, S.get('ft_wz_' + sd, ts + 0.8))
K(S, ts + 2.0, lift=0.0, jaw=38, sp_bend=-8, shk=0.6, pel_pitch=0, hd_pitch=-14); K(S, ts + 4.0, sp_bend=2, hd_pitch=-20, jaw=30, shk=0.4)
arms_off(ts + 1.2, 0.3, 0.4, 0.0, 0.2); arms_off(ts + 4.0, 0.2, 0.1, 0.0, 0.3)
# collapse onto its back
K(S, 265.5, pel_dy=-0.12, pel_pitch=-4, sp_bend=-6, hd_pitch=-16, jaw=28, shk=0.3)
K(S, 268.0, pel_dy=-0.26, pel_pitch=-50, sp_bend=-10, shk=0.5, hd_pitch=-20, jaw=24)
K(S, 270.6, pel_dy=-0.27, pel_pitch=-88, sp_bend=0, shk=0.2, hd_pitch=-22, jaw=14)
for sd in SIDES:
    S.K('ft_w_' + sd, 267.0, 1.0); S.K('ft_w_' + sd, 268.6, 0.0)
    S.K('ft_rx_' + sd, 268.6, 0.1 * (1 if sd == 'l' else -1)); S.K('ft_ry_' + sd, 268.6, -0.4); S.K('ft_rz_' + sd, 268.6, 0.0)
arms_off(268.0, 0.3, -0.1, 0.1, 0.3); arms_off(271.0, 0.3, -0.35, 0.05, 0.2)
K(S, 272.5, jaw=6, shk=0.0, hd_pitch=-22)
for tt in (276.0, 281.0): K(S, tt, jaw=8 + rng.uniform(0, 5), shk=0.05)
K(S, 284.0, jaw=4, shk=0.0)
K(S, 303.5, jaw=3)
set_root(S, 303.5, -52, 124)

if __name__ == '__main__':
    quick = len(sys.argv) > 1 and sys.argv[1] == 'quick'
    t1 = 303.5 if not quick else float(sys.argv[2]) if len(sys.argv) > 2 else 60.0
    if not quick: write_trace(S, 0, 303.5, os.path.join(OUT, 'monster_trace.json')); print('trace done', flush=True)
    bake(S, 0, t1, os.path.join(OUT, 'monster.glb' if not quick else 'monster_quick.glb'), name='Showdown_Monster', sim=not quick)
