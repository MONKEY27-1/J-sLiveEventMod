"""python show_robot.py [quick [t1]]  -> cache/showdown/robot.glb (ACTOR=robot) + robot_trace.json + robot_plan.json.
Needs monster_trace.json (python real_monster.py bake). Every contact with the monster (arm tear, punch, stab) is aligned to the monster's real animation: the script is built several times and the
robot's root is moved by the miss distance each time."""
import os, sys, json
os.environ['ACTOR'] = 'robot'
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
import sd_common as C
OUT = os.path.join(HERE_G, 'cache', 'showdown'); os.makedirs(OUT, exist_ok=True)
load_trace('monster')
S = None; rng = None; D = None; STAB = {}; PUNCH = {}

def arm_off(t, sd, dx=0.0, dy=0.0, dz=0.0, elb=0.5, curl=0.3):
    x, y, z = D[sd]; K(S, t, **{'arm_w_' + sd: 1.0, 'arm_x_' + sd: x + dx, 'arm_y_' + sd: y + dy, 'arm_z_' + sd: z + dz, 'arm_elb_' + sd: elb, 'curl_' + sd: curl})
def arms_off(t, dx=0.0, dy=0.0, dz=0.0, elb=0.5, curl=0.3): arm_off(t, 'l', dx, dy, dz, elb, curl); arm_off(t, 'r', dx, dy, dz, elb, curl)
def face_key(t, ux, uz, tx, tz): set_root(S, t, ux, uz, heading_to(ux, uz, tx, tz))
def mon(bone, t, fb=(0.0, 50.0, 100.0)): return other_pos(bone, t, fb)
def land_plant(t, dur=0.35):
    """Feet back on the floor under the pelvis at time t (the pelvis must already be where it will stand)."""
    px, pz = S.get('home_x', t) * BODY_H + PEL_B[0], S.get('home_z', t) * BODY_H + PEL_B[2]; yaw = S.get('pel_yaw', t)
    for sd in SIDES:
        nx, nz = foot_world(S, sd, t, px, pz, yaw, (0, 0), 0.0)
        S.K('ft_wx_' + sd, t - dur, nx); S.K('ft_wz_' + sd, t - dur, nz); S.K('ft_yaw_' + sd, t - dur, yaw); S.K('ft_lift_' + sd, t - dur, 0.0); S.K('ft_pitch_' + sd, t - dur, 0.0)
        S.K('ft_wx_' + sd, t + 3, nx); S.K('ft_wz_' + sd, t + 3, nz)
        S.K('ft_w_' + sd, t - dur, 0.0); S.K('ft_w_' + sd, t, 1.0)
        for k in ('ft_rx_', 'ft_ry_', 'ft_rz_'): S.K(k + sd, t - dur, S.get(k + sd, t - dur)); S.K(k + sd, t, S.tr[k + sd].default)
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
def mxz(t): p = mon('pelvis', t); return p[0], p[2]
def add(a, b, k=1.0): return (a[0] + k * b[0], a[1] + k * b[1])

def build(P):
    """the whole robot performance for the given anchor positions P (unity-local metres)"""
    global S, rng, D
    rng = random.Random(2207); S = new_scene(2207); STAB.clear(); PUNCH.clear()
    D = {sd: (S.tr['arm_x_' + sd].default, S.tr['arm_y_' + sd].default, S.tr['arm_z_' + sd].default) for sd in SIDES}
    R0, R1, RD, rt, rp, rs = P['R0'], P['R1'], P['RD'], P['rt'], P['rp'], P['rs']
    rtb = add(rt, (8.0, 3.0)); rtb2 = P['rtb2']; rp2 = P['rp2']; rpn = P['rpn']; ORB = P['orb']; ro = P['ro']; sw = P['sw']; sws = P['sws']
    MH = mxz(100.0)
    # ================================================================== 0 - 82  below the ground
    set_root(S, 0, R0[0], R0[1], heading_to(R0[0], R0[1], MH[0], MH[1])); plant_all(S, 0)
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
    # ================================================================== 106 - 116.5  walks toward the monster
    walk(S, rng, 106.0, 116.5, [(106, R0[0], R0[1]), (116.5, R1[0], R1[1])], landings=[108.8, 111.9, 114.9], face_travel=True, lift=0.06, bob=0.016, swing=0.1, lean=3.0)
    # ================================================================== 116.5 - 135.4  missile volleys at the monster (it is thrown into the air and dives)
    face_key(117.0, R1[0], R1[1], *mxz(117.0))
    for tt in (118.5, 121.0, 123.5): K(S, tt, shl_up_l=8, shl_up_r=8, sp_twist=rng.uniform(-8, 8))
    arms_off(119.0, 0.3, 0.05, 0.1, 0.3, 0.2); arms_off(123.0, 0.34, 0.1, 0.12, 0.3, 0.2)
    K(S, 120.0, hd_pitch=-6)
    for i, tm in enumerate(C.T_MISSILE):
        K(S, tm - 0.15, sp_bend=-3, pel_dz=0.0); K(S, tm + 0.15, sp_bend=-9, pel_dz=-0.02, shk=0.35, hd_pitch=-10); K(S, tm + 1.0, sp_bend=-3, pel_dz=0.0, shk=0.0, hd_pitch=-4)
        K(S, tm - 1.0, sp_twist=(10 if i % 2 == 0 else -10))
    K(S, 132.0, shl_up_l=0, shl_up_r=0, sp_twist=0)
    arms_off(133.0, 0.2, 0.3, 0.45, 0.3, 1.0)
    # ================================================================== 135.4  a beam erupts under the robot, knocked flat (away from the monster)
    tb = C.T_BEAM_HIT
    K(S, tb - 0.1, sp_bend=-3, pel_pitch=0, lift=0.0, pel_dy=0.0, shk=0.0)
    for sd in SIDES: S.K('ft_w_' + sd, tb - 0.1, 1.0); S.K('ft_w_' + sd, tb + 0.3, 0.0)
    set_root(S, tb, R1[0], R1[1]); set_root(S, tb + 1.2, (R1[0] + RD[0]) / 2, (R1[1] + RD[1]) / 2 - 3); set_root(S, 138.5, RD[0], RD[1])
    K(S, tb + 0.5, lift=0.22, pel_pitch=-42, sp_bend=-20, shk=1.0, hd_pitch=-24); K(S, tb + 1.9, lift=0.02, pel_pitch=-86, shk=0.7, pel_dy=-0.30); K(S, 138.9, lift=0.0, pel_pitch=-88, pel_dy=-0.45, pel_roll=0, shk=0.2)
    arms_off(tb + 0.5, 0.4, 0.3, -0.1, 0.2); arms_off(139.5, 0.3, -0.2, 0.05, 0.3)
    K(S, 140.2, shk=0.0)
    for tt in (141.5, 144.0): K(S, tt, hd_pitch=-10 + rng.uniform(-8, 8), hd_roll=rng.uniform(-10, 10))
    # ================================================================== 140 - 150  lies still, then rises
    set_root(S, 142.0, RD[0], RD[1]); face_key(142.0, RD[0], RD[1], rt[0], rt[1])
    kneel_rise(142.5, 149.0, RD[0], RD[1])
    set_root(S, 148.0, RD[0], RD[1])
    for sd in SIDES: S.K('ft_w_' + sd, 146.0, 0.0)
    set_root(S, 150.0, RD[0] - 2, RD[1])
    land_plant(150.0)
    # ================================================================== 150 - 158.4  runs at the spot where the ground is about to burst open (the monster erupts at 156.3)
    K(S, 150.2, sp_bend=18, pel_dy=-0.03)
    walk(S, rng, 150.2, 158.4, [(150.2, RD[0] - 2, RD[1]), (158.4, rt[0], rt[1])], landings=[151.5, 153.2, 154.9, 156.6, 158.3], face_travel=True, lift=0.12, bob=0.03, swing=0.22, lean=18, close=False)
    arms_off(155.0, 0.2, 0.2, 0.5, 0.3, 1.0)
    K(S, 156.3, sp_bend=8, shk=0.5); K(S, 157.4, shk=0.3)
    # ================================================================== 160.6  the monster lands on it; knocked down, the arm is torn off at 162.4
    tk = C.T_LAND
    K(S, tk - 0.05, lift=0.0, pel_pitch=0, shk=0.0)
    for sd in SIDES: S.K('ft_w_' + sd, tk - 0.05, 1.0); S.K('ft_w_' + sd, tk + 0.2, 0.0)
    set_root(S, tk, rt[0], rt[1]); set_root(S, tk + 1.1, rtb[0], rtb[1]); set_root(S, tk + 3.0, rtb[0], rtb[1])
    K(S, tk + 0.1, sp_bend=-14, hd_pitch=-24, shk=1.0, pel_dz=-0.05)
    K(S, tk + 0.5, lift=0.12, pel_pitch=-40, shk=0.8); K(S, tk + 1.3, lift=0.0, pel_pitch=-87, pel_dy=-0.44, shk=0.5); K(S, tk + 2.0, pel_pitch=-88, pel_dy=-0.45, shk=0.0, pel_dz=0.0)
    arm_off(tk + 0.3, 'l', 0.4, 0.4, -0.15, 0.2); arm_off(tk + 1.5, 'l', 0.3, -0.1, 0.05, 0.3)
    arm_off(tk + 0.3, 'r', 0.3, 0.3, -0.2, 0.3)
    # ================================================================== 162 - 181  lies struggling while the monster claws at it
    t = 163.0
    while t < 180:
        K(S, t, hd_pitch=-12 + rng.uniform(-8, 8), hd_roll=rng.uniform(-14, 14), hd_yaw=rng.uniform(-18, 18))
        arm_off(t, 'l', rng.uniform(0.1, 0.4), rng.uniform(-0.3, 0.3), rng.uniform(-0.1, 0.4), 0.3, rng.uniform(0.2, 1.0))
        K(S, t, pel_roll=rng.uniform(-10, 10), sp_bend=rng.uniform(-3, 6))
        t += rng.uniform(1.0, 1.7)
    for pk in C.PEAKS_BATTLE: flinch(pk, rng.uniform(0.7, 1.1))
    for sd in SIDES:
        for k, v in (('ft_rx_', 0.12 * (1 if sd == 'l' else -1)), ('ft_ry_', -0.46), ('ft_rz_', 0.0)): S.K(k + sd, 166.0, v)
    # ================================================================== 181.1  palm blast throws the monster off (it flies 40 m and lies still until 205)
    K(S, 180.4, pel_pitch=-88, sp_bend=0, hd_pitch=-14, shk=0.0); K(S, 181.0, sp_bend=14, hd_pitch=-20, shk=0.6); K(S, 182.2, sp_bend=2, hd_pitch=0, shk=0.0)
    arm_off(180.5, 'l', 0.1, 0.1, 0.1, 0.4, 0.4); arm_off(181.0, 'l', 0.05, 0.78, 0.18, 0.3, 0.0); arm_off(182.2, 'l', 0.2, 0.2, 0.1, 0.5, 0.4)
    # ================================================================== 183 - 191  gets up
    kneel_rise(183.0, 190.0, rtb2[0], rtb2[1])
    set_root(S, 181.2, rtb[0], rtb[1]); set_root(S, 183.4, rtb2[0], rtb2[1]); face_key(186.0, rtb2[0], rtb2[1], ORB[0], ORB[2]); set_root(S, 190.0, rtb2[0], rtb2[1])      # the palm blast throws it clear of the monster too
    for sd in SIDES: S.K('ft_w_' + sd, 186.0, 0.0)
    land_plant(190.4)
    for sd in SIDES: S.K('ft_rx_' + sd, 190.0, S.tr['ft_rx_' + sd].default); S.K('ft_ry_' + sd, 190.0, S.tr['ft_ry_' + sd].default)
    arm_off(190.4, 'l', 0.2, 0.15, 0.1)
    # ================================================================== 192 - 204  limps to the orb and drinks its energy
    face_key(191.5, rtb2[0], rtb2[1], ORB[0], ORB[2])
    walk(S, rng, 192.0, 201.0, [(192, rtb2[0], rtb2[1]), (201, ro[0], ro[1])], landings=[194.6, 197.6, 200.5], face_travel=True, lift=0.05, bob=0.022, swing=0.06, lean=10, sway=2.2, arms=False)
    arm_off(194.0, 'l', 0.0, 0.0, 0.05); arm_off(198.0, 'l', 0.1, -0.1, 0.05)
    for i, tt in enumerate((198.5, 200.0, 201.5, 202.8, 203.8)):
        reach_world(S, tt, 'l', (ORB[0] + rng.uniform(-1, 1), ORB[1] - 5 + i * 1.2, ORB[2] - 2 + rng.uniform(-1, 1)), elb=0.4, curl=0.0)
    face_key(201.2, ro[0], ro[1], ORB[0], ORB[2])
    K(S, 200.5, sp_bend=-12, hd_pitch=-14); K(S, 203.6, sp_bend=-26, hd_pitch=-30, shk=0.4)
    # ================================================================== 205.3  powered up; the monster stirs
    fl = C.T_FLASH
    K(S, fl, sp_bend=-18, hd_pitch=-24, shk=0.9, pel_pitch=0); K(S, fl + 1.2, sp_bend=-4, hd_pitch=-8, shk=0.2)
    arm_off(fl + 1.0, 'l', 0.25, 0.4, 0.05, 0.5, 1.0); K(S, fl + 3.0, shk=0.0)
    aim_head(S, 209.0, mon('head', 209.0, (-50, 30, 130)))
    arm_off(214.0, 'l', 0.05, 0.1, 0.1, 0.5, 0.6); arm_off(218.0, 'l', 0.18, 0.3, 0.05, 0.6, 1.0)
    # ================================================================== 221 - 238  steps out to meet the monster
    K(S, 221.0, hd_pitch=0, hd_yaw=0)
    walk(S, rng, 221.5, 237.0, [(221.5, ro[0], ro[1]), (229.0, P['via'][0], P['via'][1]), (237.0, rp[0], rp[1])], landings=[224.8, 228.4, 232.0, 235.6], face_travel=True, lift=0.07, bob=0.02, swing=0.08, lean=5.0, arms=False)
    arm_off(224, 'l', 0.0, 0.0, 0.05); arm_off(230, 'l', 0.04, 0.0, 0.1)
    face_key(238.0, rp[0], rp[1], *mxz(240.0))
    aim_head(S, 240.0, mon('head', 240.0, (-30, 70, 130)))
    # ================================================================== 244 - 249  the monster leaps; the powered punch meets it in the air at 245.6
    tp0 = C.T_PUNCH
    plant_all(S, 238.4)
    set_root(S, 243.4, rp[0], rp[1]); set_root(S, 245.3, rpn[0], rpn[1])                         # waits clear of the charging monster, then steps in for the blow
    for sd in SIDES: S.K('ft_w_' + sd, 244.3, 1.0); S.K('ft_w_' + sd, 244.6, 0.0)
    land_plant(245.4)
    K(S, 243.0, sp_twist=-34, sp_bend=8, pel_yaw=S.get('pel_yaw', 243.0)); arm_off(243.2, 'l', 0.1, 0.15, -0.35, 0.8, 1.0)
    mc = mon('spine_03', tp0, (-22, 70, 120)); tgt = (mc[0], min(mc[1], 84.0), mc[2]); PUNCH['t'] = tgt
    face_key(243.2, rp[0], rp[1], mc[0], mc[2])                                    # squares up to the chest it is about to hit
    reach_world(S, tp0 - 0.05, 'l', tgt, elb=0.3, curl=1.0)
    K(S, tp0 - 0.05, sp_twist=34, sp_bend=10, pel_pitch=10, shk=0.0); K(S, tp0 + 0.8, sp_twist=30, shk=0.9, sp_bend=14); K(S, tp0 + 2.2, sp_twist=8, shk=0.0, sp_bend=2, pel_pitch=0)
    K(S, 249.0, sp_twist=0); arm_off(249.0, 'l', 0.1, 0.1, 0.1, 0.5, 0.6)
    set_root(S, tp0 + 0.1, rpn[0], rpn[1]); set_root(S, 248.4, rp2[0], rp2[1])                      # the impact staggers it back, clear of where the monster crashes down
    for sd in SIDES: S.K('ft_w_' + sd, tp0 + 0.2, 0.0)
    land_plant(248.9)
    aim_head(S, 247.5, mon('pelvis', 247.5, (-10, 30, 110)))
    # ================================================================== 249 - 257  fetches the hidden sword and pulls it out at 253.4
    tsw = C.T_SWORD
    walk(S, rng, 249.4, 252.0, [(249.4, rp2[0], rp2[1]), (252.0, sws[0], sws[1])], landings=[250.6, 252.0], face_travel=True, lift=0.06, bob=0.02, swing=0.06, lean=6.0, arms=False, close=False)
    face_key(252.2, sws[0], sws[1], sw[0], sw[2])
    K(S, 252.4, sp_bend=8, pel_dy=-0.02, pel_pitch=0)
    K(S, tsw - 0.5, sp_bend=34, pel_dy=-0.20, pel_pitch=24, hd_pitch=18)
    reach_world(S, tsw - 0.3, 'l', (sw[0] + 2, 6, sw[2]), elb=0.4, curl=0.2)
    reach_world(S, tsw + 0.1, 'l', (sw[0] + 2, 4, sw[2]), elb=0.4, curl=1.0)
    K(S, tsw + 0.1, sp_bend=40, pel_dy=-0.24, pel_pitch=28, shk=0.5)
    for i, (tt, h) in enumerate(((tsw + 0.9, 14), (tsw + 1.7, 34), (tsw + 2.4, 54), (tsw + 3.0, 74))):
        reach_world(S, tt, 'l', (sw[0] + 2 + 0.4 * i, h, sw[2]), elb=0.45, curl=1.0)
    K(S, tsw + 0.9, sp_bend=30, pel_dy=-0.16, pel_pitch=18, shk=0.7); K(S, tsw + 1.7, sp_bend=14, pel_dy=-0.08, pel_pitch=8, shk=0.4); K(S, tsw + 2.6, sp_bend=-4, pel_dy=-0.01, pel_pitch=0, shk=0.0)
    for sd in SIDES: S.K('ft_w_' + sd, tsw - 0.6, 1.0)
    # ================================================================== 256 - 262  the leap and the stab (slow motion: the arc is stretched)
    set_root(S, 256.4, sws[0], sws[1]); set_root(S, 258.4, (sws[0] + rs[0]) / 2, (sws[1] + rs[1]) / 2); set_root(S, C.T_STAB, rs[0], rs[1])
    K(S, 256.0, sp_bend=24, pel_dy=-0.04); K(S, 256.5, sp_bend=34, pel_dy=-0.22, pel_pitch=16)
    for sd in SIDES: S.K('ft_w_' + sd, 256.6, 1.0); S.K('ft_w_' + sd, 257.1, 0.0)
    LF = P['lift']; K(S, 257.1, lift=0.0, pel_dy=-0.1, sp_bend=16); K(S, 258.0, lift=LF - 0.01, pel_dy=0.0, pel_pitch=16, sp_bend=12); K(S, 259.3, lift=LF + 0.04, pel_pitch=20, sp_bend=12); K(S, C.T_STAB, lift=LF, pel_pitch=22, sp_bend=10)
    for sd, fz, fy in (('l', 0.26, -0.34), ('r', -0.20, -0.40)):       # airborne: one knee forward, the other leg trailing (not two straight legs in line with the body)
        S.K('ft_rz_' + sd, 257.1, S.tr['ft_rz_' + sd].default); S.K('ft_ry_' + sd, 257.1, S.tr['ft_ry_' + sd].default)
        S.K('ft_rz_' + sd, 258.4, fz); S.K('ft_ry_' + sd, 258.4, fy); S.K('ft_rz_' + sd, C.T_STAB, fz); S.K('ft_ry_' + sd, C.T_STAB, fy)
        S.K('ft_rz_' + sd, C.T_STAB + 2.5, S.tr['ft_rz_' + sd].default); S.K('ft_ry_' + sd, C.T_STAB + 2.5, S.tr['ft_ry_' + sd].default)
    e8 = mon('eye', 258.0); face_key(256.4, sws[0], sws[1], e8[0], e8[2])
    for t in (257.4, 258.4, 259.2, 259.6, C.T_STAB - 0.02):
        eye = mon('eye', t); cx, cz = from_home(S.get('home_x', t), S.get('home_z', t)); ch = (cx, 50.0 + S.get('lift', t) * C.ROBOT_H, cz)
        d = vnorm(vsub(list(ch), list(eye))); tg = (eye[0] + d[0] * C.SWORD_LEN * 0.82, eye[1] + d[1] * C.SWORD_LEN * 0.82, eye[2] + d[2] * C.SWORD_LEN * 0.82)
        reach_world(S, t, 'l', tg, elb=0.3, curl=1.0); STAB[t] = tg
    ts = C.T_STAB
    K(S, ts + 0.2, shk=1.0, pel_pitch=30, sp_bend=14); K(S, ts + 2.0, shk=0.0, lift=0.36)
    K(S, ts + 3.0, lift=0.0, pel_pitch=10, sp_bend=10, pel_dy=-0.12); K(S, ts + 4.0, lift=0.0, pel_dy=-0.02, pel_pitch=0, sp_bend=4)
    for sd in SIDES: S.K('ft_w_' + sd, ts + 2.5, 0.0); S.K('ft_w_' + sd, ts + 3.4, 1.0)
    set_root(S, ts + 3.4, rs[0] + 1, rs[1] - 1); set_root(S, ts + 6.0, rs[0] + 4, rs[1] - 6)
    land_plant(ts + 3.4)
    # ================================================================== 264 - 271.5  withdraws the sword, steps back
    eye = mon('eye', ts + 2.0)
    reach_world(S, ts + 2.0, 'l', (eye[0] + 8, eye[1] + 40, eye[2] - 4), elb=0.4, curl=1.0); arm_off(ts + 4.5, 'l', 0.15, 0.55, 0.1, 0.5, 1.0)
    K(S, 266.0, hd_pitch=10, shk=0.0)
    walk(S, rng, 265.5, 271.0, [(265.5, rs[0] + 3, rs[1] - 4), (271.0, rs[0] + 6, rs[1] - 12)], landings=[267.6, 269.8], face_travel=False, arms=False, close=True, lift=0.06)
    K(S, 270.0, hd_pitch=-6, sp_bend=-4); arm_off(270.5, 'l', 0.3, 0.7, 0.1, 0.4, 0.6)
    # ================================================================== 271.5 - 282  the floss, facing the Forest (the players)
    td = C.T_DANCE; fx, fz = rs[0] + 6, rs[1] - 12
    face_key(td - 0.5, fx, fz, 0, 0)
    # every swing lands on a beat of the soundtrack: the section's beat period is 0.3 s (autocorrelation of the audio, 200 bpm grid); the floss swings on every second beat, 271.67 + 0.6 k, bouncing on the beats between
    n = 0; t = 271.67
    while t < 281.9:
        a = 1 if n % 2 == 0 else -1
        K(S, t, pel_dx=a * 0.07 * rng.uniform(0.85, 1.15), pel_roll=a * rng.uniform(8, 11), sp_lean=-a * rng.uniform(8, 12), pel_dy=-0.045, hd_roll=-a * rng.uniform(4, 10), hd_yaw=a * rng.uniform(6, 14), sp_twist=a * rng.uniform(5, 10))
        arm_off(t, 'l', (0.32 if a > 0 else -0.24) * rng.uniform(0.9, 1.1), 0.05 + 0.1 * rng.random(), (0.05 if a > 0 else 0.35), 0.35, 0.2)
        K(S, t + 0.3, pel_dy=-0.005)
        t += 0.6; n += 1
    K(S, 282.0, pel_dx=0.0, pel_roll=0, sp_lean=0, pel_dy=0.0, hd_roll=0, hd_yaw=0, sp_twist=0)
    # ================================================================== 282 - 286  the salute
    arm_off(283.0, 'l', 0.05, 0.05, 0.1, 0.5, 0.0)
    ps_ = build_pose(S, 283.6); hu = to_unity(ps_.pos('head'))
    reach_world(S, 283.8, 'l', (hu[0] + 2.5, hu[1] + 0.5, hu[2] + 5.5), elb=0.9, curl=0.0)
    K(S, 283.8, hd_pitch=-6, sp_bend=-4); K(S, 285.2, hd_pitch=-6)
    arm_off(285.9, 'l', 0.08, 0.0, 0.1, 0.5, 0.3); K(S, 285.9, hd_pitch=-12, sp_bend=-6)
    # ================================================================== 286 - 296  flies away over the tree line
    for sd in SIDES: S.K('ft_w_' + sd, 286.4, 1.0); S.K('ft_w_' + sd, 287.4, 0.0)
    K(S, 286.4, lift=0.0, pel_pitch=0, shk=0.4, pel_dy=0.0); K(S, 288.5, lift=0.5, pel_pitch=10, shk=0.5); K(S, 291.0, lift=1.3, pel_pitch=55, sp_bend=10, shk=0.1); K(S, 296.0, lift=2.4, pel_pitch=72, shk=0.0)
    set_root(S, 286.4, fx, fz); set_root(S, 291.0, fx + 10, fz + 55); set_root(S, 296.0, fx + 30, fz + 190)
    arm_off(288.0, 'l', 0.05, 0.4, 0.4, 0.3, 1.0)
    for sd in SIDES:
        for k, v in (('ft_rx_', 0.07 * (1 if sd == 'l' else -1)), ('ft_ry_', -0.49), ('ft_rz_', -0.03)): S.K(k + sd, 288.0, v)
    K(S, 303.5, lift=2.4)
    set_root(S, 303.5, fx + 30, fz + 190)

def scale_fn(t):
    return {'upperarm_r': (0.0001, 0.0001, 0.0001)} if t >= C.T_TEAR + 0.35 else {'upperarm_r': (1.0, 1.0, 1.0)}

def pos_of(bone, t): return to_unity(build_pose(S, t).pos(bone))

def measure(P):
    """how far the robot is from where it has to be at the three contacts (unity metres, xz)"""
    hr = mon('hand_r', 162.0); sh = pos_of('upperarm_r', 162.0)
    tp = PUNCH['t']; hl = pos_of('hand_l', C.T_PUNCH)
    ts_ = C.T_STAB - 0.02; tg = STAB[ts_]; hs = pos_of('hand_l', ts_)
    return {'tear': (hr[0] - sh[0], hr[2] - sh[2]), 'punch': (tp[0] - hl[0], tp[2] - hl[2], tp[1] - hl[1]), 'stab': (tg[0] - hs[0], tg[2] - hs[2], tg[1] - hs[1])}

def anchors():
    sp = mon('spine_03', C.T_PUNCH, (-22, 90, 145)); eye = mon('eye', C.T_STAB, (-62, 14, 57))
    # the monster's real flight after the punch goes south-east, so the robot stands north-west of the impact; at the stab it stands on the side of the head away from the body
    bones = ('head', 'eye', 'hand_l', 'hand_r', 'spine_03', 'pelvis'); best = None
    for k in range(72):
        a = k * math.pi / 36; c = (sp[0] + 31.0 * math.cos(a), sp[2] + 31.0 * math.sin(a))
        md = min(math.hypot(c[0] - mon(b, t)[0], c[1] - mon(b, t)[2]) for t in range(234, 245) for b in bones)
        if best is None or md > best[0]: best = (md, c)
    print('punch stand: closest approach of a monster limb before the hit %.0f m' % best[0])
    return dict(R0=(125.0, 140.0), R1=(100.0, 172.0), RD=(114.0, 174.0), rt=(-40.0, 115.0), rp=best[1], rs=(eye[0] - 31.0, eye[2] - 22.0), lift=0.3)

def unit(a, b):
    d = math.hypot(b[0] - a[0], b[1] - a[1]) or 1.0; return ((b[0] - a[0]) / d, (b[1] - a[1]) / d)
def derive(P):
    rtb = add(P['rt'], (8.0, 3.0)); K0 = mxz(190.0); u = unit((0.0, 0.0), (1.0, 0.3))
    P['rtb2'] = (K0[0] + 58.0 * u[0], K0[1] + 58.0 * u[1])                               # stands up well clear of the monster that lies there
    P['orb'] = (P['rtb2'][0] + 34.0 * u[0], 72.0, P['rtb2'][1] + 34.0 * u[1]); P['ro'] = (P['orb'][0] - 16.0 * u[0], P['orb'][2] - 16.0 * u[1])      # the orb is straight ahead of it
    mid = ((P['ro'][0] + P['rp'][0]) / 2, (P['ro'][1] + P['rp'][1]) / 2); dd = unit(P['ro'], P['rp']); bestv = None
    for off in (-90, -70, -50, -30, 0, 30, 50, 70, 90):
        v = (mid[0] - off * dd[1], mid[1] + off * dd[0])
        md = min(math.hypot(v[0] - mon(b, t)[0], v[1] - mon(b, t)[2]) for t in (222, 226, 230, 234) for b in ('head', 'hand_l', 'hand_r', 'pelvis', 'spine_03'))
        if bestv is None or md > bestv[0]: bestv = (md, v)
    P['via'] = bestv[1]
    sp6 = mon('spine_03', 245.6, (-22, 70, 120)); ur = unit((sp6[0], sp6[2]), P['rp'])
    if 'rpn' not in P: P['rpn'] = (sp6[0] + 12.0 * ur[0], sp6[2] + 12.0 * ur[1])
    M8 = mxz(248.0); u2 = unit(M8, P['rp']); P['rp2'] = (M8[0] + 48.0 * u2[0], M8[1] + 48.0 * u2[1])
    best = None                                                                          # the sword stands in the open: farthest from every monster limb while it lies there
    for f in (0.3, 0.4, 0.5, 0.6):
        for lat in (-50, -30, -15, 0, 15, 30, 50):
            ax, az = P['rp2'][0] + f * (P['rs'][0] - P['rp2'][0]), P['rp2'][1] + f * (P['rs'][1] - P['rp2'][1]); d_ = unit(P['rp2'], P['rs']); c = (ax - lat * d_[1], az + lat * d_[0])
            md = min(math.hypot(c[0] - mon(b, 253.0)[0], c[1] - mon(b, 253.0)[2]) for b in ('head', 'hand_l', 'hand_r', 'pelvis', 'spine_03', 'eye'))
            if best is None or md > best[0]: best = (md, c)
    P['sw'] = (best[1][0], 0.0, best[1][1]); P['sws'] = (P['sw'][0] + 12.0 * unit(P['rp2'], best[1])[0] * -1, P['sw'][2] + 12.0 * unit(P['rp2'], best[1])[1] * -1)
    return P

def punch_res(P):
    build(P); m = measure(P)['punch']; return m
def solve():
    P = derive(anchors())
    for it in range(8):
        build(P); m = measure(P)
        print('pass', it, {k: tuple(round(x, 1) for x in v) for k, v in m.items()}, flush=True)
        P['rt'] = add(P['rt'], m['tear'], 0.9); P['rs'] = add(P['rs'], (m['stab'][0], m['stab'][1]), 0.8); P['lift'] = max(0.1, min(0.8, P['lift'] + 0.8 * m['stab'][2] / C.ROBOT_H))
        r = m['punch']; P['rpn'] = add(P['rpn'], (r[0], r[1]), 0.7)
        derive(P)
    build(P); return P

if __name__ == '__main__':
    quick = len(sys.argv) > 1 and sys.argv[1] == 'quick'
    t1 = 303.5 if not quick else float(sys.argv[2]) if len(sys.argv) > 2 else 60.0
    P = solve(); m = measure(P); print('final misses', {k: tuple(round(x, 1) for x in v) for k, v in m.items()})
    json.dump({k: (list(v) if isinstance(v, tuple) else v) for k, v in P.items()}, open(os.path.join(OUT, 'robot_plan.json'), 'w'))
    write_trace(S, 0, 303.5, os.path.join(OUT, 'robot_trace.json')); print('trace done', flush=True)
    bake(S, 0, t1, os.path.join(OUT, 'robot.glb' if not quick else 'robot_quick.glb'), name='Showdown_Robot', sim=not quick, scale_fn=scale_fn)
