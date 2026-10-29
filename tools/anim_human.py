"""anim_human.py <jones|ue> in.glb out.glb   humanoid clips (H_ prefix, 12 fps, 8 s) for the Collision characters: Jones (Mixamo-style names) / Foundation + Slone (UE names). Same FK method as anim_rig.py."""
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
    def aim(s, i, child, d): s.rot_q(i, between(s.pos(child) - s.pos(i), d))
    def move(s, i, dw): s.T[i] = s.T[i] + np.linalg.inv(s.world(par[i])[:3, :3]) @ dw; s.c = {}
fk = FK()
def nm(prefix):
    for i, n in enumerate(N):
        if n['name'].startswith(prefix): return i
    raise KeyError(prefix)
import re
def nmre(pat):
    for i, n in enumerate(N):
        if re.match(pat, n['name']): return i
    raise KeyError(pat)
def first(*prefixes):
    for p in prefixes:
        try: return nm(p)
        except KeyError: pass
    raise KeyError(prefixes)
if prof == 'jones':
    B = dict(pelvis=nm('Hips_02'), s1=nm('Spine_03'), s2=nm('Chest_04'), neck=nm('Neck_047'), head=nm('Head_048'), ual=nm('Left arm_06'), lal=nm('Left elbow_07'), hl=nm('Left wrist_08'), uar=nm('Right arm_027'), lar=nm('Right elbow_028'), hr=nm('Right wrist_029'),
             tl=nm('Left leg_0149'), cl=nm('Left knee_0150'), fl=nm('Left ankle_0151'), tr=nm('Right leg_0163'), cr=nm('Right knee_0164'), fr=nm('Right ankle_0165')); SPINE_KEYS = ['s1', 's2']
else:
    B = dict(pelvis=nm('pelvis_'), s1=nm('spine_01_'), s2=nm('spine_02_'), s3=nm('spine_03_'), s4=nm('spine_04_'), s5=nm('spine_05_'), neck=nm('neck_01_'), head=nmre(r'^head_\d+$'), ual=nm('upperarm_l_'), lal=nm('lowerarm_l_'), hl=nm('hand_l_'), uar=nm('upperarm_r_'), lar=nm('lowerarm_r_'), hr=nm('hand_r_'),
             tl=nm('thigh_l_'), cl=nm('calf_l_'), fl=nm('foot_l_'), tr=nm('thigh_r_'), cr=nm('calf_r_'), fr=nm('foot_r_')); SPINE_KEYS = ['s1', 's2', 's3', 's4', 's5']
SPINE = SPINE_KEYS
up = fk.pos(B['head']) - fk.pos(B['pelvis']); up /= np.linalg.norm(up); left = fk.pos(B['tl']) - fk.pos(B['tr']); left -= up * (left @ up); left /= np.linalg.norm(left); fwd = np.cross(left, up)
print('frame up', np.round(up, 2), 'left', np.round(left, 2), 'fwd', np.round(fwd, 2), 'height', round(float(np.linalg.norm(fk.pos(B['head']) - fk.pos(B['fl']))), 2))
def dirw(side, f, u): v = left * side + fwd * f + up * u; return v / np.linalg.norm(v)
def lerp3(a, b, k): k = max(0.0, min(1.0, k)); return tuple(tuple((1 - k) * x + k * y for x, y in zip(u, v)) for u, v in zip(a, b))
ARM = {'rest': ((0.25, 0.05, -0.95), (0.2, 0.5, -0.85)), 'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'up': ((0.25, 0, 1), (0.1, 0, 1)), 'fwd': ((0.2, 1, 0), (0.1, 1, 0.05)), 'aim': ((0.15, 1, 0.1), (0.05, 1, 0.1)),
       'brace': ((0.3, 0.8, 0.5), (-0.2, 1, 0.9)), 'guard': ((0.2, 0.7, 0.2), (-0.3, 1, 0.5)), 'back': ((0.3, -0.7, -0.7), (0.3, -0.9, -0.5)), 'wide': ((1, 0.1, 0.4), (1, 0.2, 0.5)), 'overhead': ((0.1, 0.2, 1), (0, 0.3, 1)),
       'swordhold': ((0.35, 0.8, 0.3), (0.0, 1, 0.3)), 'raise': ((0.2, 0.3, 1), (0.1, 0.5, 1)), 'slash': ((0.2, 1, -0.6), (0.1, 1, -0.7)), 'reach': ((0.4, 1, -0.1), (0.2, 1, -0.2)), 'cup': ((0.45, 0.9, -0.35), (0.3, 1, -0.3)),
       'hang': ((0.2, 0.0, -1), (0.1, 0.25, -1)), 'carry': ((0.3, 0.4, -0.7), (0.1, 0.9, -0.2))}
LEG = {'stand': ((0.08, 0, -1), (0.04, 0, -1)), 'wide': ((0.3, 0, -1), (0.25, 0, -1)), 'fwdstep': ((0.08, 0.7, -0.8), (0.05, 0.4, -1)), 'backstep': ((0.08, -0.7, -0.8), (0.05, -0.9, -0.5)), 'lift': ((0.1, 0.5, -0.8), (0.06, -0.7, -0.7)),
       'tuck': ((0.1, 0.3, -1), (0.05, -0.95, -0.3)), 'kneel': ((0.1, 0.9, -0.4), (0.05, -0.2, -1)), 'crouch': ((0.25, 0.6, -0.8), (0.15, -0.5, -0.9)), 'trail': ((0.1, -0.5, -1), (0.05, -0.8, -0.6)), 'brace': ((0.35, 0.3, -0.9), (0.3, -0.2, -1))}
def AR(a, b=None, k=0.0): return lerp3(ARM[a], ARM[b if b else a], k)
def LG(a, b=None, k=0.0): return lerp3(LEG[a], LEG[b if b else a], k)
def sn(t, f, ph=0.0): return math.sin(2 * math.pi * (t * f + ph))
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ramp(t, a, b): return sm((t - a) / (b - a))
def mix(a, b, k): return a + (b - a) * k
def pose(p, t):
    fk.reset(); h = p.get('hip', (0, 0, 0)); fk.move(B['pelvis'], left * h[0] + fwd * h[1] + up * h[2]); fk.rot_q(B['pelvis'], axq(up, p.get('ptw', 0.0))); fk.rot_q(B['pelvis'], axq(left, p.get('ptilt', 0.0))); fk.rot_q(B['pelvis'], axq(fwd, p.get('proll', 0.0)))
    for k in SPINE: fk.rot_q(B[k], axq(left, p.get('lean', 0.0) / len(SPINE))); fk.rot_q(B[k], axq(fwd, p.get('roll', 0.0) / len(SPINE))); fk.rot_q(B[k], axq(up, p.get('twist', 0.0) / len(SPINE)))
    if True:
        for k, f in (('neck', 0.4), ('head', 0.6)): fk.rot_q(B[k], axq(left, p.get('nod', 0.0) * f)); fk.rot_q(B[k], axq(up, p.get('turn', 0.0) * f)); fk.rot_q(B[k], axq(fwd, p.get('tilt', 0.0) * f))
    for s_, sg in (('l', 1), ('r', -1)):
        lg = p.get('leg_' + s_, LG('stand')); T_, C_, F_ = (B['tl'], B['cl'], B['fl']) if s_ == 'l' else (B['tr'], B['cr'], B['fr']); fk.aim(T_, C_, dirw(sg * lg[0][0], lg[0][1], lg[0][2])); fk.aim(C_, F_, dirw(sg * lg[1][0], lg[1][1], lg[1][2]))
        a = p.get('arm_' + s_, AR('rest')); U_, L_, H_ = (B['ual'], B['lal'], B['hl']) if s_ == 'l' else (B['uar'], B['lar'], B['hr']); fk.aim(U_, L_, dirw(sg * a[0][0], a[0][1], a[0][2])); fk.aim(L_, H_, dirw(sg * a[1][0], a[1][1], a[1][2]))
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
ARM.update({'chest': ((0.2, 0.5, -0.3), (-0.3, 0.9, 0.1)), 'rifR': ((0.35, 0.55, -0.45), (-0.15, 1.0, 0.05)), 'rifL': ((0.05, 0.75, -0.35), (-0.45, 1.0, 0.1)), 'rifaimR': ((0.2, 0.8, 0.15), (-0.2, 1.0, 0.1)), 'rifaimL': ((0.0, 0.85, 0.05), (-0.5, 1.0, 0.1)), 'rifdownR': ((0.3, 0.3, -0.85), (0.05, 0.8, -0.5)),
            'rifdownL': ((0.1, 0.5, -0.7), (-0.4, 0.9, -0.3)), 'plead': ((0.45, 0.5, -0.4), (0.5, 1.0, 0.15)), 'throwb': ((0.3, -0.6, 0.55), (0.3, -0.9, 0.5)), 'throwf': ((0.3, 1.0, 0.25), (0.15, 1.0, 0.3)), 'beckon': ((0.4, 0.5, 0.4), (0.0, 0.5, 0.9)),
            'wave': ((0.5, 0.2, 0.7), (0.5, 0.0, 1.0)), 'fist': ((0.3, 0.9, 0.0), (0.2, 1.0, 0.05)), 'sky': ((0.9, 0.0, 0.25), (1.0, 0.0, 0.3)), 'skyback': ((0.9, -0.3, 0.15), (0.9, -0.5, 0.1)), 'hips2': ((0.7, -0.1, -0.5), (-0.5, 0.3, -0.5)), 'cover': ((0.2, 0.6, 0.6), (-0.4, 0.8, 0.8)),
            'brow': ((0.4, 0.5, 0.2), (-0.3, 0.9, 0.6))})
LEG.update({'kneel': ((0.1, 0.9, -0.4), (0.05, -0.2, -1.0)), 'squat': ((0.25, 0.7, -0.75), (0.15, -0.6, -0.8)), 'jump': ((0.12, 0.6, -0.5), (0.06, -0.7, -0.6)), 'sky': ((0.3, 0.0, -1.0), (0.3, -0.2, -1.0)), 'skybend': ((0.3, 0.2, -0.9), (0.25, -0.8, -0.5))})
def rif(t, k=0.0): return dict(arm_r=AR('rifR', 'rifaimR', k), arm_l=AR('rifL', 'rifaimL', k))
HUMAN = {
 'Idle':    lambda t: dict(arm_l=AR('rest', 'hang', 0.5 + 0.5 * sn(t, 0.2)), arm_r=AR('rest', 'hang', 0.5 - 0.5 * sn(t, 0.2)), hip=(0, 0, 0.004 * sn(t, 0.3)), lean=0.015 * sn(t, 0.3), turn=0.1 * sn(t, 0.125), nod=0.02 * sn(t, 0.25), leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Alert':   lambda t: dict(rif(t, 0.0), lean=0.04, turn=0.3 * sn(t, 0.125), nod=0.05, leg_l=LG('stand', 'wide', 0.5), leg_r=LG('wide'), twist=0.1 * sn(t, 0.25)),
 'Aim':     lambda t: dict(rif(t, 1.0), lean=0.08, turn=-0.1, leg_l=LG('wide'), leg_r=LG('stand', 'wide', 0.5), nod=0.05, hip=(0, 0, -0.01)),
 'Fire':    lambda t: dict(rif(t, 1.0), lean=0.08 - 0.07 * max(0, sn(t, 3)), turn=-0.1, leg_l=LG('wide'), leg_r=LG('stand', 'wide', 0.5), nod=0.04, hip=(0, -0.01 * max(0, sn(t, 3)), -0.01), twist=-0.03 * max(0, sn(t, 3))),
 'Run':     lambda t: walk(t, 1.4, 1.2, 1.1, dict(rif(t, 0.2), lean=0.18, arm_r=AR('rifR'), arm_l=AR('rifL'))),
 'Walk':    lambda t: walk(t, 0.9, 0.9, 0.4, dict(rif(t, 0.0), lean=0.05)),
 'Crouch':  lambda t: dict(rif(t, 0.0), leg_l=LG('squat'), leg_r=LG('squat'), hip=(0, 0.03, -0.45), lean=0.4, nod=0.1, turn=0.15 * sn(t, 0.2)),
 'Peek':    lambda t: dict(rif(t, ramp(t, 0.5, 1.5)), leg_l=LG('squat', 'wide', ramp(t, 0.3, 1.8)), leg_r=LG('squat', 'wide', ramp(t, 0.2, 1.6)), hip=(0, 0, mix(-0.45, -0.05, ramp(t, 0.3, 1.8))), lean=mix(0.4, 0.1, ramp(t, 0.3, 1.8))),
 'Kneel':   lambda t: dict(rif(t, 0.8), leg_l=LG('kneel'), leg_r=LG('squat'), hip=(0, 0.02, -0.55), lean=0.2, nod=0.05),
 'Talk':    lambda t: dict(arm_l=AR('rest', 'plead', 0.4 + 0.4 * sn(t, 0.4)), arm_r=AR('rest', 'plead', 0.4 - 0.4 * sn(t, 0.4, 0.3)), turn=0.25 * sn(t, 0.2), nod=0.08 * sn(t, 0.5), lean=0.03, leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Plead':   lambda t: dict(arm_l=AR('rest', 'plead', ramp(t, 0.3, 1.5)), arm_r=AR('rest', 'plead', ramp(t, 0.3, 1.5)), lean=0.08 * ramp(t, 0.3, 1.5), nod=0.1 + 0.05 * sn(t, 0.5), leg_l=LG('stand', 'fwdstep', ramp(t, 0.3, 1.5)), leg_r=LG('stand', 'backstep', 0.3 * ramp(t, 0.3, 1.5))),
 'Throw':   lambda t: dict(arm_r=AR('rifdownR', 'throwb', ramp(t, 0.2, 1.0)) if t < 1.2 else AR('throwb', 'throwf', ramp(t, 1.2, 1.55)), arm_l=AR('rifL', 'cover', 0.3), lean=mix(0.0, -0.3, ramp(t, 0.2, 1.0)) + 0.6 * ramp(t, 1.2, 1.55), twist=mix(0.0, 0.5, ramp(t, 0.2, 1.0)) - 0.9 * ramp(t, 1.2, 1.55), leg_l=LG('stand', 'fwdstep', ramp(t, 1.2, 1.55)), leg_r=LG('stand', 'backstep', ramp(t, 0.2, 1.0))),
 'Beckon':  lambda t: dict(arm_r=AR('rest', 'beckon', ramp(t, 0.1, 0.8) * (0.7 + 0.3 * sn(t, 1.5))), arm_l=AR('rifL', 'rest', 0.5), lean=0.05, turn=-0.3, leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Wake':    lambda t: dict(arm_l=AR('hang', 'rest', ramp(t, 1.5, 3)), arm_r=AR('hang', 'rest', ramp(t, 1.5, 3)), leg_l=LG('kneel', 'stand', ramp(t, 1.0, 3)), leg_r=LG('squat', 'stand', ramp(t, 0.8, 2.8)), hip=(0, 0, mix(-0.6, 0.0, ramp(t, 1.0, 3))), lean=mix(0.6, 0.0, ramp(t, 1.0, 3)), nod=mix(0.3, 0.0, ramp(t, 2, 3))),
 'Jump':    lambda t: dict(arm_l=AR('wide', 'sky', 0.5), arm_r=AR('wide', 'sky', 0.5), leg_l=LG('jump', 'sky', ramp(t, 0.6, 1.4)), leg_r=LG('squat', 'jump', ramp(t, 0.2, 0.7)), hip=(0, 0, 0.3 * math.sin(math.pi * min(1.0, t / 1.4))), lean=0.2 - 0.3 * ramp(t, 0.5, 1.2)),
 'Land':    lambda t: dict(arm_r=AR('rifdownR', 'slam', ramp(t, 0, 0.3)) if False else AR('rifdownR', 'down', ramp(t, 0, 0.3)), arm_l=AR('wide', 'down', ramp(t, 0, 0.5)), leg_l=LG('kneel'), leg_r=LG('squat'), hip=(0, 0.05, -0.6), lean=0.5, nod=0.4 * (1 - ramp(t, 0, 1)) + 0.1, ptilt=0.1),
 'Fall':    lambda t: dict(arm_l=AR('sky', 'skyback', 0.5 + 0.5 * sn(t, 0.5)), arm_r=AR('sky', 'skyback', 0.5 - 0.5 * sn(t, 0.5)), leg_l=LG('sky', 'skybend', 0.5 + 0.5 * sn(t, 0.4)), leg_r=LG('sky', 'skybend', 0.5 - 0.5 * sn(t, 0.4)), lean=-0.2 + 0.05 * sn(t, 0.5), nod=-0.3, roll=0.1 * sn(t, 0.4)),
 'Fistbump':lambda t: dict(arm_r=AR('rest', 'fist', ramp(t, 0.2, 1.0)) if t < 1.6 else AR('fist', 'rest', ramp(t, 1.6, 2.4)), arm_l=AR('rest', 'hang', 0.5), turn=0.3, lean=0.05, leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Cheer':   lambda t: dict(arm_l=AR('wave', 'sky', 0.5 + 0.5 * sn(t, 1)), arm_r=AR('wave', 'sky', 0.5 - 0.5 * sn(t, 1)), hip=(0, 0, 0.03 * abs(sn(t, 1))), lean=-0.1, nod=-0.1),
 'Hit':     lambda t: dict(arm_l=AR('cover', 'rest', ramp(t, 0.3, 1.5)), arm_r=AR('cover', 'rest', ramp(t, 0.3, 1.5)), lean=mix(-0.5, 0.1, ramp(t, 0.1, 1.2)), nod=mix(-0.3, 0, ramp(t, 0.1, 1.2)), hip=(0, -0.05, -0.05), leg_l=LG('backstep', 'stand', ramp(t, 0, 1.2)), leg_r=LG('stand', 'wide', 0.4)),
 'Look':    lambda t: dict(arm_l=AR('rest', 'brow', ramp(t, 0.2, 0.8)), arm_r=AR('rest', 'hang', 0.5), turn=0.6 * sn(t, 0.15), nod=-0.1, leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4)),
 'Smug':    lambda t: dict(arm_l=AR('hips2', 'hips2', 0), arm_r=AR('hips2', 'hips2', 0), tilt=0.12 * sn(t, 0.2), nod=-0.1, turn=0.15 * sn(t, 0.15), lean=-0.05, leg_l=LG('stand', 'wide', 0.5), leg_r=LG('stand', 'wide', 0.5)),
 'Panic':   lambda t: dict(arm_l=AR('cover', 'wide', 0.5 + 0.5 * sn(t, 1.8)), arm_r=AR('cover', 'wide', 0.5 - 0.5 * sn(t, 1.8)), lean=-0.15, nod=-0.25, turn=0.4 * sn(t, 1.2), leg_l=LG('backstep', 'stand', 0.5 + 0.5 * sn(t, 1.5)), leg_r=LG('stand', 'backstep', 0.5 + 0.5 * sn(t, 1.5))),
 'Crushed': lambda t: dict(arm_l=AR('wide', 'down', ramp(t, 0, 1.0)), arm_r=AR('wide', 'down', ramp(t, 0, 1.0)), lean=mix(0.0, 1.1, ramp(t, 0.1, 1.2)), hip=(0, 0.1 * ramp(t, 0.1, 1.2), -0.7 * ramp(t, 0.1, 1.2)), leg_l=LG('stand', 'kneel', ramp(t, 0.1, 1.2)), leg_r=LG('stand', 'squat', ramp(t, 0.1, 1.2)), ptilt=0.4 * ramp(t, 0.1, 1.2)),
 'Hold':    lambda t: dict(rif(t, 0.0), lean=0.03, nod=0.04 * sn(t, 0.2), leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4), turn=-0.1),
 'Reload':  lambda t: dict(arm_r=AR('rifdownR', 'chest', ramp(t, 0.2, 0.8)) if t < 2.2 else AR('chest', 'rifaimR', ramp(t, 2.2, 3.0)), arm_l=AR('rifL', 'rifdownL', ramp(t, 0.3, 1.0)) if t < 2.0 else AR('rifdownL', 'rifaimL', ramp(t, 2.0, 3.0)), lean=0.08 + 0.05 * sn(t, 1.0), nod=0.2 * (1 - ramp(t, 2.2, 3.0)), leg_l=LG('stand', 'wide', 0.5), leg_r=LG('wide'), turn=-0.1),
 'Point':   lambda t: dict(arm_r=AR('rest', 'fist', ramp(t, 0.2, 0.9)), arm_l=AR('rifL', 'rest', 0.5), turn=-0.25 * ramp(t, 0.2, 0.9), lean=0.06, nod=0.05, leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4)),
 'Wave':    lambda t: dict(arm_r=AR('rest', 'wave', 0.5 + 0.5 * ramp(t, 0.1, 0.6)) if True else None, arm_l=AR('rest', 'hang', 0.5), tilt=0.1 * sn(t, 1.0), turn=0.2 * sn(t, 0.25), leg_l=LG('stand', 'wide', 0.3), leg_r=LG('stand', 'wide', 0.3)),
 'Shrug':   lambda t: dict(arm_l=AR('rest', 'plead', 0.6 * ramp(t, 0.1, 0.5)), arm_r=AR('rest', 'plead', 0.6 * ramp(t, 0.1, 0.5)), hip=(0, 0, 0.01 * ramp(t, 0.1, 0.5)), tilt=0.1, nod=-0.05, leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4)),
 'Stagger': lambda t: dict(arm_l=AR('wide', 'rest', ramp(t, 0.3, 1.5)), arm_r=AR('wide', 'rest', ramp(t, 0.3, 1.5)), lean=mix(-0.5, 0.05, ramp(t, 0.1, 1.5)), roll=0.15 * sn(t, 0.8), leg_l=LG('backstep', 'wide', ramp(t, 0, 1.2)), leg_r=LG('wide'), hip=(0, -0.04, -0.03 * ramp(t, 0, 0.5))),
 'Dodge':   lambda t: dict(rif(t, 0.0), roll=0.4 * math.sin(math.pi * min(1.0, t / 1.2)), hip=(0.25 * math.sin(math.pi * min(1.0, t / 1.2)), 0, -0.2 * math.sin(math.pi * min(1.0, t / 1.2))), lean=0.25, leg_l=LG('squat'), leg_r=LG('wide')),
 'Climb':   lambda t: walk(t, 0.7, 0.8, 0.3, dict(rif(t, 0.0), lean=0.1, hip=(0, 0, -0.03 * abs(math.cos(2 * math.pi * 0.7 * t))))),
}
ARM['down'] = ARM['down']; ARM['slam'] = ARM.get('slam', ((0.2, 0.3, -1), (0.1, 0.2, -1)))

CL = HUMAN; PRE = 'H_'
chan = [('pelvis', 'rotation'), ('pelvis', 'translation')] + [(k, 'rotation') for k in SPINE] + [('neck', 'rotation'), ('head', 'rotation')] + [(k, 'rotation') for k in ('ual', 'lal', 'uar', 'lar', 'tl', 'cl', 'tr', 'cr')]
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
