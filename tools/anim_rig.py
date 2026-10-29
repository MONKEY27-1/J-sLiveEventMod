"""blender-python / python (numpy)  anim_rig.py <mech|brute> in.glb out.glb
Adds MANY clips (12 fps) to an ORIGINAL skinned Epic glb with forward-kinematics aiming (same method as anim_ice.py / anim_clips.py: limb directions are given in the CHARACTER frame, measured from the rest pose, so no bone
roll / axis / unit assumptions), slims it (base-colour maps only) and shrinks its textures.
mech  (Collision giant robot, UE mannequin names): M_Idle M_Idle2 M_Hover M_Brace M_Land M_Rise M_WalkA M_WalkB M_Fire M_FireL M_Hit M_Stagger M_SwordDraw M_SwordRaise M_SwordSlash M_Fall M_Reach M_Hand
brute (B.R.U.T.E., custom names): B_Idle B_Walk B_Run B_Aim B_Fire B_Stagger B_Death B_Alert
walk / fire clips loop (period divides 8 s); the one-shot clips hold their last pose."""
import sys, os, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
prof, src, dst = sys.argv[1:4]
g, b = slim_glb.read_glb(src); b = bytearray(b); N = g['nodes']; D = 8.0; FPS = 12; NF = int(D * FPS)
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
def m2q(m):
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0: s = math.sqrt(t + 1) * 2; q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, s / 4]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]: s = math.sqrt(1 + m[0, 0] - m[1, 1] - m[2, 2]) * 2; q = [s / 4, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]: s = math.sqrt(1 + m[1, 1] - m[0, 0] - m[2, 2]) * 2; q = [(m[0, 1] + m[1, 0]) / s, s / 4, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else: s = math.sqrt(1 + m[2, 2] - m[0, 0] - m[1, 1]) * 2; q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, s / 4, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q); return q / np.linalg.norm(q)
def axq(ax, a): ax = np.asarray(ax, float); ax = ax / np.linalg.norm(ax); s = math.sin(a / 2); return np.array([ax[0] * s, ax[1] * s, ax[2] * s, math.cos(a / 2)])
def between(a, c):
    a = a / np.linalg.norm(a); c = c / np.linalg.norm(c); d = float(a @ c)
    if d > 0.999999: return np.array([0, 0, 0, 1.0])
    if d < -0.999999: p = np.cross(a, [1, 0, 0]); p = p if np.linalg.norm(p) > 1e-3 else np.cross(a, [0, 1, 0]); return axq(p, math.pi)
    return axq(np.cross(a, c), math.acos(max(-1, min(1, d))))
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
class FK:
    def __init__(s): s.R0 = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; s.T0 = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; s.S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]; s.reset()
    def reset(s): s.R = [r.copy() for r in s.R0]; s.T = [t.copy() for t in s.T0]; s.c = {}
    def world(s, i):
        if i in s.c: return s.c[i]
        if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T          # the Sketchfab / FBX wrapper nodes carry a matrix (cm -> m, Z-up -> Y-up)
        else: M = np.eye(4); M[:3, :3] = q2m(s.R[i]) @ np.diag(s.S[i]); M[:3, 3] = s.T[i]
        s.c[i] = s.world(par[i]) @ M if i in par else M; return s.c[i]
    def pos(s, i): return s.world(i)[:3, 3]
    def wrot(s, i): m = s.world(i)[:3, :3]; return m / np.linalg.norm(m, axis=0)
    def rot_q(s, i, q): Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ q2m(q) @ Rp @ q2m(s.R[i])); s.c = {}
    def set_wrot(s, i, Rw): Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ Rw); s.c = {}
    def aim(s, i, child, d): s.rot_q(i, between(s.pos(child) - s.pos(i), d))
    def move(s, i, dw): s.T[i] = s.T[i] + np.linalg.inv(s.world(par[i])[:3, :3]) @ dw; s.c = {}
fk = FK()
def nm(prefix):
    for i, n in enumerate(N):
        if n['name'].startswith(prefix): return i
    raise KeyError(prefix)
if prof == 'mech':
    B = dict(pelvis=nm('pelvis_05'), s1=nm('spine_01_06'), s2=nm('spine_02_07'), s3=nm('spine_03_08'), s4=nm('spine_04_09'), s5=nm('spine_05_010'), neck=nm('neck_01_0128'), head=nm('head_0130'),
             ual=nm('upperarm_l_012'), lal=nm('lowerarm_l_013'), hl=nm('hand_l_014'), uar=nm('upperarm_r_081'), lar=nm('lowerarm_r_082'), hr=nm('hand_r_083'), tl=nm('thigh_l_0234'), cl=nm('calf_l_0235'), fl=nm('foot_l_0238'), tr=nm('thigh_r_0264'), cr=nm('calf_r_0265'), fr=nm('foot_r_0268'))
else:
    B = dict(pelvis=nm('root hips'), s1=nm('spine_01_02'), s2=nm('spine lower'), s3=nm('spine middle'), s4=nm('spine upper'), s5=nm('ValveBiped.Bip01_Spine3'), neck=nm('ValveBiped.Bip01_Spine3'), head=nm('spine upper'),
             ual=nm('arm left shoulder 1'), lal=nm('arm left elbow'), hl=nm('arm left wrist'), uar=nm('arm right shoulder 1'), lar=nm('arm right elbow'), hr=nm('arm right wrist'), tl=nm('leg left thigh'), cl=nm('leg left knee'), fl=nm('leg left ankle'), tr=nm('leg right thigh'), cr=nm('leg right knee'), fr=nm('leg right ankle'))
SPINE = ['s1', 's2', 's3', 's4'] + (['s5'] if prof == 'mech' else [])
up = fk.pos(B['s4']) - fk.pos(B['pelvis']); up /= np.linalg.norm(up); left = fk.pos(B['tl']) - fk.pos(B['tr']); left -= up * (left @ up); left /= np.linalg.norm(left); fwd = np.cross(left, up)
print('frame up', np.round(up, 2), 'left', np.round(left, 2), 'fwd', np.round(fwd, 2), 'height', round(float(np.linalg.norm(fk.pos(B['s4']) - fk.pos(B['fl']))), 2))
def dirw(side, f, u): v = left * side + fwd * f + up * u; return v / np.linalg.norm(v)
def lerp3(a, b, k): k = max(0.0, min(1.0, k)); return tuple(tuple((1 - k) * x + k * y for x, y in zip(u, v)) for u, v in zip(a, b))
ARM = {'rest': ((0.25, 0.05, -0.95), (0.2, 0.5, -0.85)), 'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'up': ((0.25, 0, 1), (0.1, 0, 1)), 'fwd': ((0.2, 1, 0), (0.1, 1, 0.05)), 'aim': ((0.15, 1, 0.1), (0.05, 1, 0.1)),
       'brace': ((0.3, 0.8, 0.5), (-0.2, 1, 0.9)), 'guard': ((0.2, 0.7, 0.2), (-0.3, 1, 0.5)), 'back': ((0.3, -0.7, -0.7), (0.3, -0.9, -0.5)), 'wide': ((1, 0.1, 0.4), (1, 0.2, 0.5)), 'overhead': ((0.1, 0.2, 1), (0, 0.3, 1)),
       'swordhold': ((0.35, 0.8, 0.3), (0.0, 1, 0.3)), 'raise': ((0.2, 0.3, 1), (0.1, 0.5, 1)), 'slash': ((0.2, 1, -0.6), (0.1, 1, -0.7)), 'reach': ((0.4, 1, -0.1), (0.2, 1, -0.2)), 'cup': ((0.45, 0.9, -0.35), (0.3, 1, -0.3)),
       'offer': ((0.5, 0.8, -0.25), (0.15, 1, 0.0)), 'lift': ((0.35, 0.5, 0.85), (0.1, 0.8, 0.6)), 'pull': ((0.3, 0.3, -0.8), (0.1, 1.0, 0.1)), 'hang': ((0.2, 0.0, -1), (0.1, 0.25, -1)), 'carry': ((0.3, 0.4, -0.7), (0.1, 0.9, -0.2))}
LEG = {'stand': ((0.08, 0, -1), (0.04, 0, -1)), 'wide': ((0.3, 0, -1), (0.25, 0, -1)), 'fwdstep': ((0.08, 0.7, -0.8), (0.05, 0.4, -1)), 'backstep': ((0.08, -0.7, -0.8), (0.05, -0.9, -0.5)), 'lift': ((0.1, 0.5, -0.8), (0.06, -0.7, -0.7)),
       'tuck': ((0.1, 0.3, -1), (0.05, -0.95, -0.3)), 'kneel': ((0.1, 0.9, -0.4), (0.05, -0.2, -1)), 'crouch': ((0.25, 0.6, -0.8), (0.15, -0.5, -0.9)), 'trail': ((0.1, -0.5, -1), (0.05, -0.8, -0.6)), 'brace': ((0.35, 0.3, -0.9), (0.3, -0.2, -1))}
def AR(a, b=None, k=0.0): return lerp3(ARM[a], ARM[b if b else a], k)
def LG(a, b=None, k=0.0): return lerp3(LEG[a], LEG[b if b else a], k)
def sn(t, f, ph=0.0): return math.sin(2 * math.pi * (t * f + ph))
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ramp(t, a, b): return sm((t - a) / (b - a))
def mix(a, b, k): return a + (b - a) * k
FD_L = np.array([0.988, -0.009, -0.152]); N_L = float(os.environ.get('LSIGN', '-1')) * np.array([0.244, -0.442, 0.863])       # tools/mech_palm.py: the right hand's finger direction and palm normal in the hand bone's frame
def palm(k):                                           # turn the right hand so the palm faces UP and the fingers continue the forearm (weight k)
    fk.c = {}; U = np.array([0.0, 1.0, 0.0]); F = fk.pos(B['hl']) - fk.pos(B['lal']); F = F - U * float(F @ U); F = F / np.linalg.norm(F)
    fd = FD_L / np.linalg.norm(FD_L); n = N_L - fd * float(N_L @ fd); n = n / np.linalg.norm(n); Bl = np.stack([fd, np.cross(n, fd), n], 1); Bw = np.stack([F, np.cross(U, F), U], 1); Rt = Bw @ Bl.T
    qc = m2q(fk.wrot(B['hl'])); qt = m2q(Rt)
    if float(qc @ qt) < 0: qt = -qt
    qs = (1 - k) * qc + k * qt; fk.set_wrot(B['hl'], q2m(qs / np.linalg.norm(qs)))
def pose(p, t):
    fk.reset(); h = p.get('hip', (0, 0, 0)); fk.move(B['pelvis'], left * h[0] + fwd * h[1] + up * h[2]); fk.rot_q(B['pelvis'], axq(up, p.get('ptw', 0.0))); fk.rot_q(B['pelvis'], axq(left, p.get('ptilt', 0.0))); fk.rot_q(B['pelvis'], axq(fwd, p.get('proll', 0.0)))
    for k in SPINE: fk.rot_q(B[k], axq(left, p.get('lean', 0.0) / len(SPINE))); fk.rot_q(B[k], axq(fwd, p.get('roll', 0.0) / len(SPINE))); fk.rot_q(B[k], axq(up, p.get('twist', 0.0) / len(SPINE)))
    if prof == 'mech':
        for k, f in (('neck', 0.4), ('head', 0.6)): fk.rot_q(B[k], axq(left, p.get('nod', 0.0) * f)); fk.rot_q(B[k], axq(up, p.get('turn', 0.0) * f)); fk.rot_q(B[k], axq(fwd, p.get('tilt', 0.0) * f))
    for s_, sg in (('l', 1), ('r', -1)):
        lg = p.get('leg_' + s_, LG('stand')); T_, C_, F_ = (B['tl'], B['cl'], B['fl']) if s_ == 'l' else (B['tr'], B['cr'], B['fr']); fk.aim(T_, C_, dirw(sg * lg[0][0], lg[0][1], lg[0][2])); fk.aim(C_, F_, dirw(sg * lg[1][0], lg[1][1], lg[1][2]))
        a = p.get('arm_' + s_, AR('rest')); U_, L_, H_ = (B['ual'], B['lal'], B['hl']) if s_ == 'l' else (B['uar'], B['lar'], B['hr']); fk.aim(U_, L_, dirw(sg * a[0][0], a[0][1], a[0][2])); fk.aim(L_, H_, dirw(sg * a[1][0], a[1][1], a[1][2]))
    if prof == 'mech' and p.get('palm', 0.0) > 0: palm(p['palm'])
    return fk
def stride(t, f=0.5, amp=1.0):                        # walk phase: left foot phase in [-1, 1]
    return math.sin(2 * math.pi * f * t) * amp
def walk_legs(t, f=0.5, amp=1.0):
    s_ = math.sin(2 * math.pi * f * t); c_ = math.cos(2 * math.pi * f * t); L = LG('backstep', 'fwdstep', 0.5 + 0.5 * s_ * amp); R = LG('backstep', 'fwdstep', 0.5 - 0.5 * s_ * amp)
    if c_ > 0.0: L = lerp3(L, LG('lift'), 0.8 * c_ * amp)
    else: R = lerp3(R, LG('lift'), 0.8 * -c_ * amp)
    return L, R
def walk(t, f=0.5, amp=1.0, swing=1.0, extra=None):
    L, R = walk_legs(t, f, amp); s_ = math.sin(2 * math.pi * f * t)
    d = dict(leg_l=L, leg_r=R, arm_l=AR('rest', 'carry', 0.5 + 0.5 * s_ * swing), arm_r=AR('rest', 'carry', 0.5 - 0.5 * s_ * swing), twist=0.12 * s_ * swing, hip=(0.0, 0.0, -0.03 * abs(math.cos(2 * math.pi * f * t)) * amp), lean=0.06, ptw=-0.1 * s_ * swing, roll=0.05 * math.cos(2 * math.pi * f * t))
    if extra: d.update(extra)
    return d
MECH = {
 'Idle':       lambda t: dict(arm_l=AR('rest', 'hang', 0.5 + 0.5 * sn(t, 0.25)), arm_r=AR('rest', 'hang', 0.5 - 0.5 * sn(t, 0.25)), hip=(0, 0, 0.01 * sn(t, 0.3)), lean=0.02 * sn(t, 0.3), turn=0.1 * sn(t, 0.125), leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Idle2':      lambda t: dict(arm_l=AR('guard', 'brace', 0.35 + 0.25 * sn(t, 0.17)), arm_r=AR('swordhold', 'aim', 0.3 + 0.3 * sn(t, 0.13, 0.3)), turn=0.35 * sn(t, 0.09), nod=0.06 * sn(t, 0.2), twist=0.1 * sn(t, 0.09, 0.2), lean=0.09 + 0.02 * sn(t, 0.22), hip=(0.012 * sn(t, 0.1), 0.0, 0.014 * sn(t, 0.2)), roll=0.04 * sn(t, 0.1), leg_l=LG('wide', 'stand', 0.5 + 0.5 * sn(t, 0.05)), leg_r=LG('wide', 'stand', 0.5 - 0.5 * sn(t, 0.05))),
 'Hover':      lambda t: dict(arm_l=AR('back', 'wide', 0.15 + 0.1 * sn(t, 0.2)), arm_r=AR('back', 'wide', 0.15 - 0.1 * sn(t, 0.2)), leg_l=LG('trail', 'stand', 0.2 + 0.1 * sn(t, 0.2)), leg_r=LG('trail', 'stand', 0.2 - 0.1 * sn(t, 0.2)), lean=0.55, ptilt=0.1, hip=(0, 0, 0.05 * sn(t, 0.25)), nod=-0.5, roll=0.05 * sn(t, 0.15)),
 'Brace':      lambda t: dict(arm_l=AR('brace'), arm_r=AR('brace'), leg_l=LG('brace'), leg_r=LG('brace'), lean=0.3 + 0.02 * sn(t, 6), nod=0.3, hip=(0, 0, -0.08)),
 'Land':       lambda t: dict(arm_l=AR('wide', 'hang', ramp(t, 0.2, 2.0)), arm_r=AR('wide', 'hang', ramp(t, 0.2, 2.0)), leg_l=LG('tuck', 'kneel', ramp(t, 0, 0.8)), leg_r=LG('tuck', 'crouch', ramp(t, 0, 0.8)), lean=mix(0.7, 0.5, ramp(t, 0, 1.5)), hip=(0, 0, mix(0.0, -0.45, ramp(t, 0, 0.7))), nod=0.3, ptilt=mix(0.25, 0.4, ramp(t, 0, 1))),
 'Rise':       lambda t: dict(arm_l=AR('hang', 'rest', ramp(t, 1, 4)), arm_r=AR('hang', 'rest', ramp(t, 1.2, 4.2)), leg_l=LG('kneel', 'stand', ramp(t, 0.5, 4)), leg_r=LG('crouch', 'stand', ramp(t, 0.3, 3.8)), lean=mix(0.5, 0.0, ramp(t, 0.5, 4)), hip=(0, 0, mix(-0.45, 0.0, ramp(t, 0.5, 4))), nod=mix(0.3, -0.1, ramp(t, 1, 4)), ptilt=mix(0.4, 0.0, ramp(t, 0.5, 4))),
 'WalkA':      lambda t: walk(t, 0.5, 1.35, 0.7, dict(lean=0.1 + 0.02 * abs(math.sin(2 * math.pi * 0.5 * t)), twist=0.14 * math.sin(2 * math.pi * 0.5 * t), nod=0.04 * math.sin(2 * math.pi * 1.0 * t), roll=0.06 * math.cos(2 * math.pi * 0.5 * t), arm_l=AR('rest', 'carry', 0.5 + 0.5 * math.sin(2 * math.pi * 0.5 * t) * 0.8), arm_r=AR('rest', 'carry', 0.5 - 0.5 * math.sin(2 * math.pi * 0.5 * t) * 0.8))),
 'WalkB':      lambda t: walk(t, 0.375, 1.3, 0.6, dict(arm_l=AR('guard'), arm_r=AR('swordhold', 'carry', 0.5 + 0.5 * math.sin(2 * math.pi * 0.375 * t)), turn=0.25 * math.sin(2 * math.pi * 0.1875 * t))),
 'Fire':       lambda t: dict(arm_r=AR('aim'), arm_l=AR('aim', 'guard', 0.4), leg_l=LG('wide'), leg_r=LG('stand', 'wide', 0.6), lean=0.12 - 0.1 * max(0, sn(t, 3)), turn=-0.2, twist=-0.15, hip=(0, -0.02 * max(0, sn(t, 3)), -0.04), nod=0.05, ptw=-0.1),
 'FireL':      lambda t: dict(arm_l=AR('aim'), arm_r=AR('aim', 'guard', 0.4), leg_l=LG('stand', 'wide', 0.6), leg_r=LG('wide'), lean=0.12 - 0.1 * max(0, sn(t, 3.5)), turn=0.2, twist=0.15, hip=(0, -0.02 * max(0, sn(t, 3.5)), -0.04), nod=0.05, ptw=0.1),
 'Hit':        lambda t: dict(arm_l=AR('guard', 'wide', ramp(t, 0.1, 0.4)), arm_r=AR('guard', 'wide', ramp(t, 0.1, 0.4)), leg_l=LG('brace'), leg_r=LG('brace'), lean=mix(0.1, -0.5, ramp(t, 0, 0.25)) + 0.5 * ramp(t, 0.8, 3), nod=mix(0, -0.4, ramp(t, 0, 0.25)) + 0.4 * ramp(t, 0.8, 3), hip=(0, -0.1 * ramp(t, 0, 0.3), -0.1 * ramp(t, 0, 0.3))),
 'Stagger':    lambda t: dict(arm_l=AR('wide', 'down', ramp(t, 0, 2)), arm_r=AR('wide', 'down', ramp(t, 0.3, 2.3)), leg_l=LG('backstep', 'wide', ramp(t, 0, 2)), leg_r=LG('wide'), lean=mix(-0.4, 0.1, ramp(t, 0, 2.5)), roll=0.15 * sn(t, 0.5), ptw=0.2 * sn(t, 0.4)),
 'SwordDraw':  lambda t: dict(arm_r=AR('rest', 'swordhold', ramp(t, 0.5, 2.5)), arm_l=AR('rest', 'guard', ramp(t, 1, 3)), twist=-0.3 * ramp(t, 0.5, 2.5), turn=-0.2 * ramp(t, 0.5, 2.5), leg_l=LG('stand', 'wide', 0.6), leg_r=LG('stand', 'wide', 0.6), lean=0.05),
 'SwordRaise': lambda t: dict(arm_r=AR('swordhold', 'raise', ramp(t, 0.5, 3.5)), arm_l=AR('guard', 'raise', ramp(t, 1, 3.5)), lean=mix(0.05, -0.35, ramp(t, 0.5, 3.5)), nod=-0.2 * ramp(t, 0.5, 3.5), leg_l=LG('wide'), leg_r=LG('wide'), hip=(0, 0, 0.02 * ramp(t, 0, 3))),
 'SwordSlash': lambda t: dict(arm_r=AR('raise', 'slash', ramp(t, 0.4, 1.2)), arm_l=AR('raise', 'slash', ramp(t, 0.5, 1.3)), lean=mix(-0.35, 0.7, ramp(t, 0.4, 1.2)), nod=mix(-0.2, 0.4, ramp(t, 0.4, 1.2)), leg_l=LG('wide', 'fwdstep', ramp(t, 0.4, 1.2)), leg_r=LG('wide', 'backstep', ramp(t, 0.4, 1.2)), hip=(0, 0.1 * ramp(t, 0.4, 1.2), -0.08 * ramp(t, 0.4, 1.2))),
 'Fall':       lambda t: dict(arm_l=AR('wide', 'up', 0.5 + 0.5 * sn(t, 0.6)), arm_r=AR('wide', 'up', 0.5 - 0.5 * sn(t, 0.6)), leg_l=LG('tuck', 'wide', 0.5 + 0.5 * sn(t, 0.5)), leg_r=LG('tuck', 'wide', 0.5 - 0.5 * sn(t, 0.5)), lean=0.3 * sn(t, 0.3), roll=0.2 * sn(t, 0.4)),
 'Reach':      lambda t: dict(arm_r=AR('rest', 'reach', ramp(t, 0.3, 2.5)), arm_l=AR('rest', 'guard', ramp(t, 1, 3)), lean=mix(0.0, 0.5, ramp(t, 0.3, 2.5)), leg_l=LG('stand', 'fwdstep', ramp(t, 0.3, 2.5)), leg_r=LG('stand', 'backstep', ramp(t, 0.3, 2.5)), hip=(0, 0.05 * ramp(t, 0.3, 2.5), -0.1 * ramp(t, 0.3, 2.5))),
 'HandUp':     lambda t: dict(palm=1.0, arm_l=AR('offer', 'lift', ramp(t, 0.5, 7.5)), arm_r=AR('guard', 'cup', 1.0), lean=mix(0.5, 0.05, ramp(t, 0.5, 7.5)), leg_l=LG('crouch', 'stand', ramp(t, 0.5, 7.5)), leg_r=LG('crouch', 'stand', ramp(t, 0.5, 7.5)), hip=(0, 0.05, mix(-0.2, 0.0, ramp(t, 0.5, 7.5))), nod=mix(0.3, -0.1, ramp(t, 0.5, 7.5))),
 'HandIn':     lambda t: dict(palm=1.0, arm_l=AR('lift', 'pull', ramp(t, 0.3, 7.5)), arm_r=AR('guard', 'cup', 1.0), lean=mix(0.05, 0.45, ramp(t, 0.3, 7.5)), leg_l=LG('stand', 'crouch', ramp(t, 0.3, 7.5)), leg_r=LG('stand', 'crouch', ramp(t, 0.3, 7.5)), hip=(0, 0.05, mix(0.0, -0.2, ramp(t, 0.3, 7.5))), nod=mix(-0.1, 0.4, ramp(t, 0.3, 7.5))),
 'Hand':       lambda t: dict(palm=ramp(t, 0.3, 2.2), arm_l=AR('reach', 'offer', ramp(t, 0.2, 2.0)), arm_r=AR('guard', 'cup', ramp(t, 0.2, 2.0)), lean=0.5, leg_l=LG('crouch'), leg_r=LG('crouch'), hip=(0, 0.05, -0.2), nod=0.3),
}
def bwalk(t, f, amp, extra=None):
    d = walk(t, f, amp, 0.6, extra); return d
BRUTE = {
 'Idle':    lambda t: dict(arm_l=AR('hang', 'rest', 0.5 + 0.5 * sn(t, 0.25)), arm_r=AR('hang', 'rest', 0.5 - 0.5 * sn(t, 0.25)), hip=(0, 0, 0.012 * sn(t, 0.5)), lean=0.02 * sn(t, 0.5), leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4), twist=0.1 * sn(t, 0.125)),
 'Alert':   lambda t: dict(arm_l=AR('guard'), arm_r=AR('aim', 'guard', 0.5 + 0.5 * sn(t, 0.5)), twist=0.5 * sn(t, 0.25), leg_l=LG('wide'), leg_r=LG('wide'), lean=0.1, ptw=0.15 * sn(t, 0.25)),
 'Walk':    lambda t: walk(t, 0.75, 1.0, 0.6),
 'Run':     lambda t: walk(t, 1.25, 1.2, 1.2, dict(lean=0.25)),
 'Aim':     lambda t: dict(arm_l=AR('aim', 'aim', 0), arm_r=AR('aim', 'aim', 0), lean=0.1, leg_l=LG('wide'), leg_r=LG('wide'), twist=0.05 * sn(t, 0.25)),
 'Fire':    lambda t: dict(arm_l=AR('aim'), arm_r=AR('aim'), lean=0.1 - 0.08 * max(0, sn(t, 4)), hip=(0, -0.03 * max(0, sn(t, 4)), 0), leg_l=LG('wide'), leg_r=LG('wide'), roll=0.03 * sn(t, 4)),
 'Stagger': lambda t: dict(arm_l=AR('wide', 'down', ramp(t, 0, 2)), arm_r=AR('wide', 'down', ramp(t, 0.2, 2)), lean=mix(-0.5, 0.1, ramp(t, 0, 2.5)), leg_l=LG('backstep', 'wide', ramp(t, 0, 2)), leg_r=LG('wide'), roll=0.2 * sn(t, 0.5)),
 'Death':   lambda t: dict(arm_l=AR('wide', 'down', ramp(t, 0, 2)), arm_r=AR('wide', 'hang', ramp(t, 0, 2)), lean=mix(0, -1.3, ramp(t, 0.3, 2.6)), leg_l=LG('stand', 'kneel', ramp(t, 0.2, 2)), leg_r=LG('stand', 'crouch', ramp(t, 0.2, 2)), hip=(0, -0.2 * ramp(t, 0.3, 2.6), -0.9 * ramp(t, 0.3, 2.6)), ptilt=-0.9 * ramp(t, 0.3, 2.6)),
}
CL = MECH if prof == 'mech' else BRUTE; PRE = 'M_' if prof == 'mech' else 'B_'
chan = [('pelvis', 'rotation'), ('pelvis', 'translation')] + [(k, 'rotation') for k in SPINE] + ([('neck', 'rotation'), ('head', 'rotation')] if prof == 'mech' else []) + [(k, 'rotation') for k in ('ual', 'lal', 'uar', 'lar', 'tl', 'cl', 'tr', 'cr')] + ([('hl', 'rotation')] if prof == 'mech' else [])
def add_accessor(arr, typ, minmax=False):
    arr = np.ascontiguousarray(arr, np.float32); off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(arr.tobytes()); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}); a = {'bufferView': len(g['bufferViews']) - 1, 'componentType': 5126, 'count': len(arr), 'type': typ}
    if minmax: a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
    g['accessors'].append(a); return len(g['accessors']) - 1
T = np.arange(NF + 1) / FPS; tin = add_accessor(T, 'SCALAR', minmax=True); g['animations'] = []; rep = []
for name, fn in CL.items():
    tracks = {c: [] for c in chan}
    for f in range(NF + 1):
        t = f / FPS; pose(fn(t), t)
        for (k, path) in chan: tracks[(k, path)].append(fk.R[B[k]].copy() if path == 'rotation' else fk.T[B[k]].copy())
    samplers = []; channels = []
    for (k, path), arr in tracks.items():
        arr = np.array(arr)
        if path == 'rotation':
            for i in range(1, len(arr)):
                if float(arr[i] @ arr[i - 1]) < 0: arr[i] = -arr[i]
        out = add_accessor(arr, 'VEC4' if path == 'rotation' else 'VEC3'); samplers.append({'input': tin, 'output': out, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': B[k], 'path': path}})
    g['animations'].append({'name': PRE + name, 'samplers': samplers, 'channels': channels})
    pose(fn(1.0), 1.0); rel = lambda k: np.round([(fk.pos(B[k]) - fk.pos(B['pelvis'])) @ left, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ up, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ fwd], 2).tolist()
    rep.append('%-10s hand_r %s foot_l %s head %s' % (name, rel('hr'), rel('fl'), rel('head')))
g['buffers'][0]['byteLength'] = len(b); print('\n'.join(rep)); g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb); print(dst, os.path.getsize(dst) // 1024, 'KB', len(CL), 'clips')
