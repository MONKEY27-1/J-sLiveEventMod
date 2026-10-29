"""python show_robot.py [quick [t1]]  -> cache/showdown/robot.glb (ACTOR=robot). Needs monster_trace.json (run show_monster.py first) for the contact beats."""
import os, sys
os.environ['ACTOR'] = 'robot'
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
import sd_common as C
OUT = os.path.join(HERE_G, 'cache', 'showdown'); os.makedirs(OUT, exist_ok=True)
load_trace('monster')
rng = random.Random(2207); S = new_scene(2207)
D = {sd: (S.tr['arm_x_' + sd].default, S.tr['arm_y_' + sd].default, S.tr['arm_z_' + sd].default) for sd in SIDES}
SWORD_LEN = 58.0

def arm_off(t, sd, dx=0.0, dy=0.0, dz=0.0, elb=0.5, curl=0.3):
    x, y, z = D[sd]; K(S, t, **{'arm_w_' + sd: 1.0, 'arm_x_' + sd: x + dx, 'arm_y_' + sd: y + dy, 'arm_z_' + sd: z + dz, 'arm_elb_' + sd: elb, 'curl_' + sd: curl})
def arms_off(t, dx=0.0, dy=0.0, dz=0.0, elb=0.5, curl=0.3): arm_off(t, 'l', dx, dy, dz, elb, curl); arm_off(t, 'r', dx, dy, dz, elb, curl)
def face_key(t, ux, uz, tx, tz): set_root(S, t, ux, uz, heading_to(ux, uz, tx, tz))
def mon(bone, t, fb=(0.0, 50.0, 100.0)): return other_pos(bone, t, fb)
def feet_air(t0, t1):
    for sd in SIDES: S.K('ft_w_' + sd, t0, 1.0); S.K('ft_w_' + sd, t0 + 0.25, 0.0); S.K('ft_w_' + sd, t1 - 0.2, 0.0)
def land_plant(t, dur=0.35):
    """Feet back on the floor under the pelvis at time t (the pelvis must already be where it will stand)."""
    px, pz = S.get('home_x', t) * BODY_H + PEL_B[0], S.get('home_z', t) * BODY_H + PEL_B[2]; yaw = S.get('pel_yaw', t)
    for sd in SIDES:
        nx, nz = foot_world(S, sd, t, px, pz, yaw, (0, 0), 0.0)
        S.K('ft_wx_' + sd, t - dur, nx); S.K('ft_wz_' + sd, t - dur, nz); S.K('ft_yaw_' + sd, t - dur, yaw); S.K('ft_lift_' + sd, t - dur, 0.0); S.K('ft_pitch_' + sd, t - dur, 0.0)
        S.K('ft_wx_' + sd, t + 3, nx); S.K('ft_wz_' + sd, t + 3, nz)
        S.K('ft_w_' + sd, t - dur, 0.0); S.K('ft_w_' + sd, t, 1.0)
        for k in ('ft_rx_', 'ft_ry_', 'ft_rz_'): S.K(k + sd, t - dur, S.get(k + sd, t - dur)); S.K(k + sd, t, S.tr[k + sd].default)
def lying(t, dy=-0.45, pitch=-88, roll=0.0):
    K(S, t, pel_pitch=pitch, pel_roll=roll, pel_dy=dy, ft_w_l=0.0, ft_w_r=0.0)
def flinch(t, power=1.0):
    K(S, t - 0.05, shk=S.get('shk', t - 0.05)); K(S, t + 0.1, shk=0.7 * power, hd_roll=rng.uniform(-12, 12) * power); K(S, t + 0.9, shk=0.0, hd_roll=0.0)
def kneel_rise(t0, t1, ux, uz):
    """Lying on the back -> stands: rolls onto the side, onto hands and knees, up."""
    d = t1 - t0
    K(S, t0, pel_pitch=-88, pel_dy=-0.45, pel_roll=0, sp_bend=0)
    K(S, t0 + d * 0.28, pel_pitch=-40, pel_roll=65, pel_dy=-0.46, sp_bend=12, hd_pitch=12)
    K(S, t0 + d * 0.55, pel_pitch=40, pel_roll=0, pel_dy=-0.50, sp_bend=26, hd_pitch=16)
    arm_off(t0 + d * 0.3, 'l', 0.1, -0.5, 0.3, 0.3); arm_off(t0 + d * 0.3, 'r', 0.1, -0.5, 0.3, 0.3)
    arm_off(t0 + d * 0.55, 'l', 0.15, -0.8, 0.5, 0.2); arm_off(t0 + d * 0.55, 'r', 0.15, -0.8, 0.5, 0.2)
    K(S, t0 + d * 0.8, pel_pitch=22, pel_dy=-0.22, sp_bend=22, hd_pitch=8)
    K(S, t1, pel_pitch=4, pel_dy=-0.02, pel_roll=0, sp_bend=8, hd_pitch=0)
    arms_off(t1, 0.0, 0.0, 0.0)

# ================================================================== 0 - 82  below the ground at the east side
sx, sz = C.ROB_START
set_root(S, 0, sx, sz, heading_to(sx, sz, 10, 118)); plant_all(S, 0)
K(S, 0, root_dy=-1.4, pel_pitch=30, sp_bend=30, hd_pitch=18); arms_off(0, 0, 0, 0)
K(S, 82.0, root_dy=-1.4, pel_pitch=30, sp_bend=30, hd_pitch=18, shk=0.0)

# ================================================================== 82 - 93  rises out of the ground, eyes ignite at 90.2
K(S, 84.0, root_dy=-1.15, shk=0.5); K(S, 87.0, root_dy=-0.6, pel_pitch=24, sp_bend=26, shk=0.45); K(S, 89.6, root_dy=0.0, pel_pitch=6, sp_bend=8, hd_pitch=8, shk=0.2)
arms_off(85.0, 0.05, 0.2, 0.2); arms_off(88.0, 0.2, 0.55, 0.1, 0.3)
K(S, C.T_ACTIVATE, sp_bend=-8, hd_pitch=-14, shk=0.0, pel_pitch=0); arms_off(C.T_ACTIVATE, 0.35, 0.15, 0.0, 0.2, 0.0)
# ================================================================== 93 - 106  stands, flexes, looks at the monster
arms_off(93.4, 0.1, 0.18, 0.3, 0.9, 1.0); arms_off(96.0, 0.32, 0.12, 0.05, 0.2, 0.4)
arm_off(99.0, 'l', 0.2, 0.1, 0.1, 0.4, 1.0); arm_off(99.6, 'r', 0.1, 0.62, 0.05, 0.4, 1.0)       # right fist pump
arm_off(101.5, 'r', 0.15, 0.2, 0.1, 0.5, 1.0); arm_off(103.0, 'l', 0.1, 0.62, 0.05, 0.4, 1.0)    # left fist pump
K(S, 99.6, sp_bend=-6, shk=0.2); K(S, 100.6, shk=0.0); K(S, 103.6, sp_bend=-6, shk=0.2); K(S, 104.6, shk=0.0)
for t, w in ((97, 0.0), (101, 0.0), (104.0, 1.0)):
    mp = mon('head', t, (10, 100, 118)); aim_head(S, t, mp, w) if w else K(S, t, hd_yaw=rng.uniform(-18, 18), hd_pitch=-4)
arms_off(105.5, 0.05, 0.05, 0.1)
# ================================================================== 106 - 116  walks toward the monster
walk(S, rng, 106.0, 116.5, [(106, 100, 70), (116.5, 60, 92)], landings=[108.8, 111.9, 114.9], face_travel=True, lift=0.06, bob=0.016, swing=0.1, lean=3.0)
# ================================================================== 116.5 - 135.4  missile volleys
face_key(117.0, 60, 92, 10, 118)
for tt in (118.5, 121.0, 123.5):
    K(S, tt, shl_up_l=8, shl_up_r=8, sp_twist=rng.uniform(-8, 8))
arms_off(119.0, 0.3, 0.05, 0.1, 0.3, 0.2); arms_off(123.0, 0.34, 0.1, 0.12, 0.3, 0.2)
K(S, 120.0, hd_pitch=-6)
for i, tm in enumerate(C.T_MISSILE):
    K(S, tm - 0.15, sp_bend=-3, pel_dz=0.0); K(S, tm + 0.15, sp_bend=-9, pel_dz=-0.02, shk=0.35, hd_pitch=-10); K(S, tm + 1.0, sp_bend=-3, pel_dz=0.0, shk=0.0, hd_pitch=-4)
    K(S, tm - 1.0, sp_twist=(10 if i % 2 == 0 else -10))
K(S, 132.0, shl_up_l=0, shl_up_r=0, sp_twist=0)
arms_off(133.0, 0.2, 0.3, 0.45, 0.3, 1.0)

# ================================================================== 135.4  hit by the beam, knocked flat
tb = C.T_BEAM_HIT
K(S, tb - 0.1, sp_bend=-3, pel_pitch=0, lift=0.0, pel_dy=0.0, shk=0.0)
for sd in SIDES: S.K('ft_w_' + sd, tb - 0.1, 1.0); S.K('ft_w_' + sd, tb + 0.3, 0.0)
set_root(S, tb, 60, 92); set_root(S, tb + 1.2, 70, 88); set_root(S, 138.5, 82, 82)
K(S, tb + 0.5, lift=0.22, pel_pitch=-42, sp_bend=-20, shk=1.0, hd_pitch=-24); K(S, tb + 1.9, lift=0.02, pel_pitch=-86, shk=0.7, pel_dy=-0.30); K(S, 138.9, lift=0.0, pel_pitch=-88, pel_dy=-0.45, pel_roll=0, shk=0.2)
arms_off(tb + 0.5, 0.4, 0.3, -0.1, 0.2); arms_off(139.5, 0.3, -0.2, 0.05, 0.3)
K(S, 140.2, shk=0.0)
for tt in (141.5, 144.0): K(S, tt, hd_pitch=-10 + rng.uniform(-8, 8), hd_roll=rng.uniform(-10, 10))
# ================================================================== 140 - 150  lies still, then rises
set_root(S, 142.0, 82, 82); face_key(142.0, 82, 82, 40, 100)
kneel_rise(142.5, 149.0, 82, 82)
set_root(S, 148.0, 82, 82)
for sd in SIDES: S.K('ft_w_' + sd, 146.0, 0.0)
set_root(S, 150.0, 80, 82)
land_plant(150.0)

# ================================================================== 150 - 156.6  the charge
rl = [151.1, 152.1, 153.1, 154.1, 155.1, 156.1]
K(S, 150.2, sp_bend=18, pel_dy=-0.03)
walk(S, rng, 150.2, 156.3, [(150.2, 80, 82), (156.3, 44, 97)], landings=rl, face_travel=True, lift=0.12, bob=0.03, swing=0.22, lean=18, close=False)
tt = C.T_TACKLE
arms_off(155.0, 0.2, 0.2, 0.5, 0.3, 1.0)
mc = mon('spine_03', tt, (30, 60, 106)); mh = mon('hand_l', tt, (30, 60, 106))
reach_world(S, tt - 0.1, 'l', (mc[0] + 4, mc[1] - 6, mc[2] - 6), elb=0.4); reach_world(S, tt - 0.1, 'r', (mc[0] - 4, mc[1] - 6, mc[2] - 6), elb=0.4)
K(S, tt - 0.1, sp_bend=26, pel_pitch=14); K(S, tt + 0.5, sp_bend=22, pel_pitch=16, shk=0.7); K(S, tt + 1.5, shk=0.2)
set_root(S, tt, 42, 98); set_root(S, tt + 1.0, 40, 101); set_root(S, 158.5, 40, 100)
# 156.6 - 161  pushes it away, raises a fist, then turns to the ambush
K(S, 158.2, sp_bend=10, pel_pitch=4, shk=0.0)
arm_off(158.5, 'r', 0.15, 0.7, 0.05, 0.3, 1.0); arm_off(158.5, 'l', 0.15, 0.25, 0.2)
K(S, 159.4, sp_bend=-6, hd_pitch=-8)
aim_head(S, 160.2, mon('pelvis', 160.2, (70, 40, 150)))
K(S, 160.7, sp_bend=4, hd_pitch=4)

# ================================================================== 161  the arm is torn off, robot knocked down
tk = C.T_TEAR
K(S, tk - 0.05, lift=0.0, pel_pitch=0, shk=0.0)
for sd in SIDES: S.K('ft_w_' + sd, tk - 0.05, 1.0); S.K('ft_w_' + sd, tk + 0.2, 0.0)
set_root(S, tk, 40, 100); set_root(S, tk + 1.1, 34, 104)
K(S, tk + 0.1, sp_bend=-14, hd_pitch=-24, shk=1.0, pel_dz=-0.05)
K(S, tk + 0.5, lift=0.12, pel_pitch=-40, shk=0.8); K(S, tk + 1.3, lift=0.0, pel_pitch=-87, pel_dy=-0.44, shk=0.5); K(S, tk + 2.0, pel_pitch=-88, pel_dy=-0.45, shk=0.0, pel_dz=0.0)
arm_off(tk + 0.3, 'l', 0.4, 0.4, -0.15, 0.2); arm_off(tk + 1.5, 'l', 0.3, -0.1, 0.05, 0.3)
arm_off(tk + 0.3, 'r', 0.3, 0.3, -0.2, 0.3)

# ================================================================== 162 - 182  lies struggling while the monster stomps and claws
t = 163.0
while t < 182:
    K(S, t, hd_pitch=-12 + rng.uniform(-8, 8), hd_roll=rng.uniform(-14, 14), hd_yaw=rng.uniform(-18, 18))
    arm_off(t, 'l', rng.uniform(0.1, 0.4), rng.uniform(-0.3, 0.3), rng.uniform(-0.1, 0.4), 0.3, rng.uniform(0.2, 1.0))
    K(S, t, pel_roll=rng.uniform(-10, 10), sp_bend=rng.uniform(-3, 6))
    t += rng.uniform(1.0, 1.7)
for pk in C.PEAKS_BATTLE: flinch(pk, rng.uniform(0.7, 1.1))
for sd in SIDES:
    for k, v in (('ft_rx_', 0.12 * (1 if sd == 'l' else -1)), ('ft_ry_', -0.46), ('ft_rz_', 0.0)): S.K(k + sd, 166.0, v)
# ================================================================== 182 - 192  gets up and kicks the monster away at 187.8
kneel_rise(181.5, 188.0, 34, 104)
set_root(S, 181.5, 34, 104); face_key(184.0, 34, 104, 50, 107); set_root(S, 188.0, 34, 104)
for sd in SIDES: S.K('ft_w_' + sd, 184.5, 0.0)
land_plant(188.2)
for sd in SIDES: S.K('ft_rx_' + sd, 188.0, S.tr['ft_rx_' + sd].default); S.K('ft_ry_' + sd, 188.0, S.tr['ft_ry_' + sd].default)
tkk = C.T_KICK
S.K('ft_lift_r', tkk - 0.7, 0.0); S.K('ft_lift_r', tkk - 0.05, 0.22 * BODY_H); S.K('ft_lift_r', tkk + 0.9, 0.0)
S.K('ft_wz_r', tkk - 0.7, S.get('ft_wz_r', tkk - 0.7)); S.K('ft_wz_r', tkk - 0.05, S.get('ft_wz_r', tkk - 0.7) - 0.3 * BODY_H); S.K('ft_wz_r', tkk + 0.9, S.get('ft_wz_r', tkk - 0.7))
K(S, tkk - 0.1, sp_bend=-8, pel_pitch=-10, shk=0.0); K(S, tkk + 0.5, sp_bend=4, pel_pitch=0, shk=0.5); K(S, tkk + 1.5, shk=0.0)
arm_off(tkk, 'l', 0.2, 0.15, 0.1)
# ================================================================== 192 - 204  limps to the orb and drinks its energy
face_key(191.5, 34, 104, 25, 62)
ox_, oy_, oz_ = C.ORB
walk(S, rng, 192.0, 201.0, [(192, 34, 104), (201, 25, 62)], landings=[194.6, 197.6, 200.5], face_travel=True, lift=0.05, bob=0.022, swing=0.06, lean=10, sway=2.2, arms=False)
arm_off(194.0, 'l', 0.0, 0.0, 0.05); arm_off(198.0, 'l', 0.1, -0.1, 0.05)
for i, tt in enumerate((198.5, 200.0, 201.5, 202.8, 203.8)):
    reach_world(S, tt, 'l', (ox_ + rng.uniform(-1, 1), oy_ - 5 + i * 1.2, oz_ - 2 + rng.uniform(-1, 1)), elb=0.4, curl=0.0)
K(S, 200.5, sp_bend=-12, hd_pitch=-14); K(S, 203.6, sp_bend=-26, hd_pitch=-30, shk=0.4)
# ================================================================== 204 - 215  powers up and punches
fl = C.T_FLASH
K(S, fl, sp_bend=-18, hd_pitch=-24, shk=0.9, pel_pitch=0); K(S, fl + 1.2, sp_bend=-4, hd_pitch=-8, shk=0.2)
arm_off(fl + 1.0, 'l', 0.25, 0.4, 0.05, 0.5, 1.0); K(S, fl + 3.0, shk=0.0)
mpos = mon('head', 208.0, (22, 90, 100)); aim_head(S, 208.0, mpos)
walk(S, rng, 208.5, 214.6, [(208.5, 25, 62), (214.6, 22, 80)], landings=[210.2, 212.4, 214.0], face_travel=False, arms=False, close=False, lift=0.08)
face_key(209.0, 25, 62, 22, 100)
K(S, 213.0, sp_twist=-34, sp_bend=8, pel_yaw=S.get('pel_yaw', 213.0)); arm_off(213.2, 'l', 0.1, 0.15, -0.35, 0.8, 1.0)
tp0 = C.T_PUNCH
mc = mon('spine_03', tp0, (22, 70, 97))
reach_world(S, tp0 - 0.05, 'l', (mc[0], mc[1], mc[2] - 3), elb=0.3, curl=1.0)
K(S, tp0 - 0.05, sp_twist=34, sp_bend=10, pel_pitch=10, shk=0.0); K(S, tp0 + 0.8, sp_twist=30, shk=0.9, sp_bend=14); K(S, tp0 + 2.2, sp_twist=8, shk=0.0, sp_bend=2, pel_pitch=0)
K(S, 218.0, sp_twist=0); arm_off(218.5, 'l', 0.1, 0.1, 0.1, 0.5, 0.6)
aim_head(S, 217.0, mon('pelvis', 217.0, (-30, 40, 150)))
# ================================================================== 222 - 236  walks west to the hidden sword
K(S, 221.0, hd_pitch=0, hd_yaw=0)
walk(S, rng, 221.5, 236.0, [(221.5, 22, 80), (236.0, -80, 72)], landings=[224.5, 228.2, 231.8, 235.4], face_travel=True, lift=0.07, bob=0.02, swing=0.08, lean=5.0, arms=False)
arm_off(224, 'l', 0.0, 0.0, 0.05); arm_off(230, 'l', 0.04, 0.0, 0.1)
# ================================================================== 236 - 244  pulls the sword from the ground
sw = C.SWORD
tsw = C.T_SWORD
face_key(236.2, -80, 72, sw[0], sw[2])
K(S, 236.4, sp_bend=8, pel_dy=-0.02, pel_pitch=0)
K(S, tsw - 1.0, sp_bend=34, pel_dy=-0.20, pel_pitch=24, hd_pitch=18)
reach_world(S, tsw - 0.6, 'l', (sw[0] + 2, 6, sw[2]), elb=0.4, curl=0.2)
reach_world(S, tsw + 0.2, 'l', (sw[0] + 2, 4, sw[2]), elb=0.4, curl=1.0)
K(S, tsw + 0.2, sp_bend=40, pel_dy=-0.24, pel_pitch=28, shk=0.5)
for i, (tt, h) in enumerate(((tsw + 1.5, 14), (tsw + 3.0, 34), (tsw + 4.4, 54), (tsw + 5.6, 76))):
    reach_world(S, tt, 'l', (sw[0] + 2 + 0.4 * i, h, sw[2]), elb=0.45, curl=1.0)
K(S, tsw + 1.5, sp_bend=30, pel_dy=-0.16, pel_pitch=18, shk=0.7); K(S, tsw + 3.0, sp_bend=14, pel_dy=-0.08, pel_pitch=8, shk=0.4); K(S, tsw + 5.0, sp_bend=-4, pel_dy=-0.01, pel_pitch=0, shk=0.0)
K(S, 245.0, sp_bend=-10, hd_pitch=-14)
for sd in SIDES: S.K('ft_w_' + sd, tsw - 1.2, 1.0)
# ================================================================== 244 - 254  charges the monster with the sword up
face_key(246.0, -80, 72, -50, 124)
walk(S, rng, 246.0, 254.0, [(246, -80, 72), (254, -62, 104)], landings=[247.6, 248.8, 250.0, 251.2, 252.4, 253.6], face_travel=True, lift=0.11, bob=0.03, swing=0.1, lean=14, arms=False, close=False)
reach_world(S, 249.0, 'l', (-78, 62, 84), elb=0.5, curl=1.0); reach_world(S, 253.0, 'l', (-66, 56, 106), elb=0.5, curl=1.0)
# ================================================================== 254 - 262  the leap and the stab (slow motion: the arc is stretched)
K(S, 254.0, sp_bend=24, pel_dy=-0.04); K(S, 255.4, sp_bend=34, pel_dy=-0.22, pel_pitch=16)
for sd in SIDES: S.K('ft_w_' + sd, 255.3, 1.0); S.K('ft_w_' + sd, 255.9, 0.0)
set_root(S, 254.0, -62, 104); set_root(S, 258.8, -59, 107); set_root(S, C.T_STAB, -58, 109)
K(S, 255.9, lift=0.0, pel_dy=-0.1, sp_bend=26); K(S, 257.4, lift=0.45, pel_dy=0.0, pel_pitch=26, sp_bend=22); K(S, 259.0, lift=0.5, pel_pitch=32, sp_bend=20); K(S, C.T_STAB, lift=0.46, pel_pitch=34, sp_bend=18)
for t in (256.4, 257.6, 258.8, 259.5, C.T_STAB - 0.02):
    eye = mon('eye', t + 0.0, (-62, 70, 108)); ch = robot_c = C.robot_chest(t)
    d = vnorm(vsub(list(ch), list(eye))); tgt = (eye[0] + d[0] * SWORD_LEN * 0.82, eye[1] + d[1] * SWORD_LEN * 0.82, eye[2] + d[2] * SWORD_LEN * 0.82)
    reach_world(S, t, 'l', tgt, elb=0.3, curl=1.0)
ts = C.T_STAB
K(S, ts + 0.2, shk=1.0, pel_pitch=30, sp_bend=14); K(S, ts + 2.0, shk=0.0, lift=0.36)
K(S, ts + 3.0, lift=0.0, pel_pitch=10, sp_bend=10, pel_dy=-0.12); K(S, ts + 4.0, lift=0.0, pel_dy=-0.02, pel_pitch=0, sp_bend=4)
for sd in SIDES: S.K('ft_w_' + sd, ts + 2.5, 0.0); S.K('ft_w_' + sd, ts + 3.4, 1.0)
set_root(S, ts + 3.4, -58, 108); set_root(S, ts + 6.0, -54, 100)
land_plant(ts + 3.4)
# ================================================================== 264 - 271.5  withdraws the sword, steps back
reach_world(S, ts + 2.0, 'l', (-62, 70, 104), elb=0.4, curl=1.0); arm_off(ts + 4.5, 'l', 0.15, 0.55, 0.1, 0.5, 1.0)
K(S, 266.0, hd_pitch=10, shk=0.0)
walk(S, rng, 265.5, 271.0, [(265.5, -58, 108), (271.0, -54, 98)], landings=[267.6, 269.8], face_travel=False, arms=False, close=True, lift=0.06)
K(S, 270.0, hd_pitch=-6, sp_bend=-4); arm_off(270.5, 'l', 0.3, 0.7, 0.1, 0.4, 0.6)
# ================================================================== 271.5 - 282  the floss
td = C.T_DANCE
face_key(td - 0.5, -54, 100, 0, 0)
t = td; n = 0; per = 0.5
while t < 282.0:
    a = 1 if n % 2 == 0 else -1; per = 0.5 * rng.uniform(0.92, 1.08)
    K(S, t, pel_dx=a * 0.07 * rng.uniform(0.8, 1.2), pel_roll=a * rng.uniform(7, 11), sp_lean=-a * rng.uniform(7, 12), pel_dy=-0.04 + 0.015 * (n % 2), hd_roll=-a * rng.uniform(4, 10), hd_yaw=a * rng.uniform(6, 14), sp_twist=a * rng.uniform(4, 10))
    arm_off(t, 'l', (0.32 if a > 0 else -0.24) * rng.uniform(0.85, 1.15), 0.05 + 0.1 * rng.random(), (0.05 if a > 0 else 0.35), 0.35, 0.2)
    t += per; n += 1
K(S, 282.0, pel_dx=0.0, pel_roll=0, sp_lean=0, pel_dy=0.0, hd_roll=0, hd_yaw=0, sp_twist=0)
# ================================================================== 282 - 286  the salute
hp = bone_world(S, 283.4, 'head') if False else None
arm_off(283.0, 'l', 0.05, 0.05, 0.1, 0.5, 0.0)
ps_ = build_pose(S, 283.6); hp_ = ps_.pos('head'); hu = to_unity(hp_)
reach_world(S, 283.8, 'l', (hu[0] + 2.5, hu[1] + 0.5, hu[2] + 5.5), elb=0.9, curl=0.0)
K(S, 283.8, hd_pitch=-6, sp_bend=-4); K(S, 285.2, hd_pitch=-6)
arm_off(285.9, 'l', 0.08, 0.0, 0.1, 0.5, 0.3); K(S, 285.9, hd_pitch=-12, sp_bend=-6)
# ================================================================== 286 - 296  flies away
for sd in SIDES: S.K('ft_w_' + sd, 286.4, 1.0); S.K('ft_w_' + sd, 287.4, 0.0)
K(S, 286.4, lift=0.0, pel_pitch=0, shk=0.4, pel_dy=0.0); K(S, 288.5, lift=0.5, pel_pitch=10, shk=0.5); K(S, 291.0, lift=1.3, pel_pitch=55, sp_bend=10, shk=0.1); K(S, 296.0, lift=2.4, pel_pitch=72, shk=0.0)
set_root(S, 286.4, -54, 100); set_root(S, 291.0, -40, 130); set_root(S, 296.0, 0, 190)
arm_off(288.0, 'l', 0.05, 0.4, 0.4, 0.3, 1.0)
for sd in SIDES:
    for k, v in (('ft_rx_', 0.07 * (1 if sd == 'l' else -1)), ('ft_ry_', -0.49), ('ft_rz_', -0.03)): S.K(k + sd, 288.0, v)
K(S, 303.5, lift=2.4)
set_root(S, 303.5, 0, 190)

def scale_fn(t):
    return {'upperarm_r': (0.0001, 0.0001, 0.0001)} if t >= C.T_TEAR + 0.35 else {'upperarm_r': (1.0, 1.0, 1.0)}

if __name__ == '__main__':
    quick = len(sys.argv) > 1 and sys.argv[1] == 'quick'
    t1 = 303.5 if not quick else float(sys.argv[2]) if len(sys.argv) > 2 else 60.0
    bake(S, 0, t1, os.path.join(OUT, 'robot.glb' if not quick else 'robot_quick.glb'), name='Showdown_Robot', sim=not quick, scale_fn=scale_fn)
