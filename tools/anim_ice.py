"""python anim_ice.py in.glb out.glb
Adds MANY one-shot clips (8 s, 12 fps each, same channel layout so the plugin can crossfade them) to the ORIGINAL Ice King glb (Y-up Maya-style rig, bones along local x):
  I_Curl I_Emerge I_Awake I_Rise I_Stand I_Survey I_Flex I_Conjure1 I_Conjure2 I_Gather I_Advance I_Windup I_Strike I_Roar I_SweepL I_SweepR I_Slam I_Tower
The Ice Storm scene plays each of them exactly once, in order (they are not loops). Same method as anim_clips.py: limb directions are given in the CHARACTER frame (side / forward / up, measured from the rest pose),
the bones are aimed with a small forward-kinematics model of the file's own hierarchy, so nothing depends on bone roll or units. Cape and cloth bones get a lagged swing on top."""
import sys, os, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, dst = sys.argv[1:3]
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
    ax = np.cross(a, c); return axq(ax, math.acos(max(-1, min(1, d))))
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
class FK:
    def __init__(s): s.R0 = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; s.T0 = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; s.S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]; s.reset()
    def reset(s): s.R = [r.copy() for r in s.R0]; s.T = [t.copy() for t in s.T0]; s.c = {}
    def world(s, i):
        if i in s.c: return s.c[i]
        M = np.eye(4); M[:3, :3] = q2m(s.R[i]) @ np.diag(s.S[i]); M[:3, 3] = s.T[i]; s.c[i] = s.world(par[i]) @ M if i in par else M; return s.c[i]
    def pos(s, i): return s.world(i)[:3, 3]
    def wrot(s, i): m = s.world(i)[:3, :3]; return m / np.linalg.norm(m, axis=0)
    def rot_q(s, i, q):
        Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ q2m(q) @ Rp @ q2m(s.R[i])); s.c = {}
    def aim(s, i, child, d): s.rot_q(i, between(s.pos(child) - s.pos(i), d))
    def move(s, i, dw): s.T[i] = s.T[i] + np.linalg.inv(s.world(par[i])[:3, :3]) @ dw; s.c = {}
fk = FK()
def nm(prefix):
    for i, n in enumerate(N):
        if n['name'].startswith(prefix): return i
    raise KeyError(prefix)
B = {'pelvis': nm('bip_pelvis'), 'spine_01': nm('bip_spine_0_'), 'spine_02': nm('bip_spine_1_'), 'spine_03': nm('bip_spine_2_'), 'spine_04': nm('bip_spine_3_'), 'neck': nm('bip_neck'), 'head': nm('bip_head'),
     'upperarm_l': nm('bip_upperarm_l'), 'lowerarm_l': nm('bip_lowerarm_l'), 'hand_l': nm('bip_hand_l'), 'upperarm_r': nm('bip_upperarm_r'), 'lowerarm_r': nm('bip_lowerarm_r'), 'hand_r': nm('bip_hand_r'),
     'thigh_l': nm('bip_hip_l'), 'calf_l': nm('bip_knee_l'), 'foot_l': nm('bip_foot_l'), 'thigh_r': nm('bip_hip_r'), 'calf_r': nm('bip_knee_r'), 'foot_r': nm('bip_foot_r')}
CAPE = [nm('cape_0%d_%s' % (i, s)) for s in 'lr' for i in range(1, 8)]
CLOTH = [nm('dyn_loinClothRear_0%d' % i) for i in range(1, 7)] + [nm('dyn_loinClothFront_0%d' % i) for i in range(1, 6)] + [nm('dyn_hipCloth_0%d_l' % i) for i in range(1, 8)]
up = fk.pos(B['head']) - fk.pos(B['pelvis']); up /= np.linalg.norm(up); left = fk.pos(B['thigh_l']) - fk.pos(B['thigh_r']); left -= up * (left @ up); left /= np.linalg.norm(left); fwd = np.cross(left, up)
print('frame  up', np.round(up, 2), 'left', np.round(left, 2), 'fwd', np.round(fwd, 2))
def dirw(side, f, u): v = left * side + fwd * f + up * u; return v / np.linalg.norm(v)
ARM = {'rest': ((0.25, 0.05, -0.95), (0.2, 0.5, -0.85)), 'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'wideout': ((1, 0, 0.35), (1, 0, 0.45)), 'up': ((0.25, 0, 1), (0.1, 0, 1)),
       'overhead': ((0.1, 0.1, 1), (0.0, 0.2, 1)), 'wideup': ((0.7, 0, 0.7), (0.5, 0, 1)), 'fwd': ((0.2, 1, 0), (0.1, 1, 0.1)), 'sweep': ((0.9, 0.4, 0.2), (1.0, 0.5, 0.2)), 'fling': ((0.9, 0.3, 0.35), (1.0, 0.5, 0.45)),
       'chest': ((0.2, 0.5, -0.3), (-0.3, 0.9, 0.1)), 'cupped': ((0.3, 0.7, -0.4), (0.2, 1.0, 0.1)), 'cast': ((0.4, 0.9, 0.3), (0.2, 1, 0.5)), 'slam': ((0.2, 0.3, -1), (0.1, 0.2, -1)), 'tback': ((0.4, -0.9, 0.4), (0.4, -1, 0.5)),
       'tfwd': ((0.3, 1, 0.1), (0.2, 1, 0.2)), 'cross': ((0.1, 0.4, -0.9), (-1.0, 0.5, 0.0)), 'curl': ((0.3, 0.7, -0.3), (-0.5, 0.8, 0.25)), 'hug': ((0.2, 0.6, -0.5), (-0.8, 0.7, 0)), 'scoop': ((0.5, 0.6, -0.5), (0.3, 1, 0.2)),
       'wings': ((0.7, -0.6, 0.2), (0.6, -0.8, 0.3)), 'low': ((0.5, 0.3, -0.8), (0.5, 0.8, -0.4)), 'hips': ((0.8, -0.1, -0.4), (-0.4, 0.3, -0.5)), 'reach': ((0.3, 1, 0.3), (0.2, 1, 0.35)), 'back': ((0.35, -0.5, -0.8), (0.3, -0.8, -0.5)),
       'across': ((-0.3, 1, 0.1), (-0.9, 0.8, 0.0)), 'open': ((0.45, 0.5, -0.6), (0.5, 0.9, 0.1))}
LEG = {'stand': ((0.06, 0, -1), (0.03, 0, -1)), 'hang': ((0.12, 0.25, -1), (0.05, -0.55, -0.85)), 'wide': ((0.25, 0, -1), (0.2, 0, -1)), 'back': ((0.08, -0.2, -1), (0.04, -0.5, -0.9)), 'fwd': ((0.08, 0.3, -1), (0.05, 0.1, -1)), 'tuck': ((0.1, 0.8, -0.6), (0.05, -0.9, -0.5))}
def AR(a, b=None, k=0.0): k = max(0.0, min(1.0, k)); A_, B_ = ARM[a], ARM[b if b else a]; return tuple(tuple((1 - k) * x + k * y for x, y in zip(u, v)) for u, v in zip(A_, B_))
def LG(a, b=None, k=0.0): k = max(0.0, min(1.0, k)); A_, B_ = LEG[a], LEG[b if b else a]; return tuple(tuple((1 - k) * x + k * y for x, y in zip(u, v)) for u, v in zip(A_, B_))
def sn(t, f, ph=0.0): return math.sin(2 * math.pi * (t * f + ph))
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ramp(t, a, b): return sm((t - a) / (b - a))
def mix(a, b, k): return a + (b - a) * k
def pose(p, t):
    fk.reset()
    h = p.get('hip', (0, 0, 0)); fk.move(B['pelvis'], left * h[0] + fwd * h[1] + up * h[2]); fk.rot_q(B['pelvis'], axq(up, p.get('ptw', 0.0)))
    for k in ('spine_01', 'spine_02', 'spine_03', 'spine_04'):
        fk.rot_q(B[k], axq(left, p.get('lean', 0.0) / 4)); fk.rot_q(B[k], axq(fwd, p.get('roll', 0.0) / 4)); fk.rot_q(B[k], axq(up, p.get('twist', 0.0) / 4))
    for k, f in (('neck', 0.4), ('head', 0.6)): fk.rot_q(B[k], axq(left, p.get('nod', 0.0) * f)); fk.rot_q(B[k], axq(up, p.get('turn', 0.0) * f)); fk.rot_q(B[k], axq(fwd, p.get('tilt', 0.0) * f))
    for s_, sg in (('l', 1), ('r', -1)):
        lg = p.get('leg_' + s_, LG('stand')); fk.aim(B['thigh_' + s_], B['calf_' + s_], dirw(sg * lg[0][0], lg[0][1], lg[0][2])); fk.aim(B['calf_' + s_], B['foot_' + s_], dirw(sg * lg[1][0], lg[1][1], lg[1][2]))
        a = p.get('arm_' + s_, AR('rest')); fk.aim(B['upperarm_' + s_], B['lowerarm_' + s_], dirw(sg * a[0][0], a[0][1], a[0][2])); fk.aim(B['lowerarm_' + s_], B['hand_' + s_], dirw(sg * a[1][0], a[1][1], a[1][2]))
    # cape and cloth: lagged swing (fore-aft and sideways), stronger in the storm clips
    cw = p.get('cape', 0.5); wind = p.get('wind', 0.0)
    for n_, i in enumerate(CAPE):
        k = n_ % 7; fk.rot_q(i, axq(left, cw * 0.13 * math.sin(2 * math.pi * (t * 0.7 - k * 0.11)) + wind * 0.25 + p.get('lean', 0.0) * -0.1)); fk.rot_q(i, axq(fwd, (cw * 0.07 + wind * 0.1) * math.sin(2 * math.pi * (t * 0.43 - k * 0.09 + (0.3 if n_ >= 7 else 0.0)))))
    for n_, i in enumerate(CLOTH):
        k = n_ % 6; fk.rot_q(i, axq(left, cw * 0.1 * math.sin(2 * math.pi * (t * 0.8 - k * 0.1 + n_ * 0.07)) + wind * 0.2)); fk.rot_q(i, axq(fwd, cw * 0.06 * math.sin(2 * math.pi * (t * 0.55 - k * 0.08))))
    return fk
def br(t, f=0.3): return 0.012 * sn(t, f)                       # breathing
# ---- the clips: functions of t (seconds into the clip, 0 .. 8, NOT periodic) -> pose parameters
CL = {
 'Curl':    lambda t: dict(leg_l=LG('tuck'), leg_r=LG('tuck'), arm_l=AR('curl'), arm_r=AR('curl'), lean=0.55 + 0.05 * sn(t, 0.3), nod=0.45, twist=0.25 * sn(t, 0.18), roll=0.1 * sn(t, 0.22), hip=(0, 0, br(t)), cape=0.15),
 'Emerge':  lambda t: dict(leg_l=LG('tuck', 'stand', ramp(t, 0.2, 2.0)), leg_r=LG('tuck', 'stand', ramp(t, 0.4, 2.2)), arm_l=AR('curl', 'out', ramp(t, 0.3, 2.0)), arm_r=AR('curl', 'out', ramp(t, 0.5, 2.2)), lean=mix(0.55, -0.12, ramp(t, 0.2, 2.2)), nod=mix(0.45, -0.15, ramp(t, 0.5, 2.4)), twist=0.25 * (1 - ramp(t, 0, 2)), cape=0.3 + 0.4 * ramp(t, 0, 2), hip=(0, 0, 0.03 * ramp(t, 0, 2))),
 'Awake':   lambda t: dict(arm_l=AR('out', 'fling', ramp(t, 0.2, 1.8)), arm_r=AR('out', 'fling', ramp(t, 0.4, 2.0)), turn=0.5 * sn(t, 0.35), nod=-0.15 + 0.05 * sn(t, 0.5), lean=-0.12, hip=(0, 0, 0.03 + br(t)), cape=0.6, tilt=0.12 * sn(t, 0.3)),
 'Rise':    lambda t: dict(arm_l=AR('fling', 'wideup', ramp(t, 0.2, 2.6)), arm_r=AR('fling', 'wideup', ramp(t, 0.3, 2.7)), lean=mix(-0.12, -0.35, ramp(t, 0, 2.8)), nod=mix(-0.15, -0.35, ramp(t, 0, 2.8)), hip=(0, 0, mix(0.03, 0.08, ramp(t, 0, 3))), twist=0.1 * sn(t, 0.3), cape=0.8, wind=0.2 * ramp(t, 0, 3)),
 'Stand':   lambda t: dict(arm_l=AR('wideup', 'down', ramp(t, 0.1, 2.4)), arm_r=AR('wideup', 'down', ramp(t, 0.3, 2.6)), lean=mix(-0.35, -0.02, ramp(t, 0, 2.5)), nod=mix(-0.35, 0.05, ramp(t, 0, 2.5)), hip=(0, 0, 0.06 + br(t)), turn=0.15 * sn(t, 0.25), cape=0.6, leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4)),
 'Survey':  lambda t: dict(arm_l=AR('down', 'open', 0.3 + 0.2 * sn(t, 0.3)), arm_r=AR('down', 'open', 0.3 - 0.2 * sn(t, 0.3)), turn=0.8 * sn(t, 0.33), nod=0.12 + 0.1 * sn(t, 0.6), twist=0.2 * sn(t, 0.33, 0.1), hip=(0, 0, 0.06 + br(t)), cape=0.6, tilt=0.08 * sn(t, 0.33)),
 'Flex':    lambda t: dict(arm_l=AR('down', 'hips', ramp(t, 0.3, 1.2)), arm_r=AR('down', 'cross', ramp(t, 0.9, 1.9)), lean=0.1 * sn(t, 0.5) - 0.05, nod=0.08, twist=0.3 * sn(t, 0.4), roll=0.1 * sn(t, 0.5), turn=-0.25 * sn(t, 0.4), cape=0.7, hip=(0, 0, 0.06 + 0.02 * sn(t, 0.5))),
 'Conjure1': lambda t: dict(arm_l=AR('rest', 'cupped', ramp(t, 0, 1.2)), arm_r=AR('rest', 'cupped', ramp(t, 0.2, 1.4)), nod=0.3, lean=0.15 + 0.05 * sn(t, 1.5), roll=0.04 * sn(t, 0.9), hip=(0, 0, 0.07 + 0.03 * sn(t, 1.5)), cape=0.9, wind=0.15 * ramp(t, 0, 3)),
 'Conjure2': lambda t: dict(arm_l=AR('cupped', 'cast', ramp(t, 0.1, 1.2) * 0.6 + 0.4 * sn(t, 0.9)), arm_r=AR('cupped', 'cast', ramp(t, 0.1, 1.2) * 0.6 + 0.4 * sn(t, 0.9, 0.5)), twist=0.45 * sn(t, 0.45), roll=0.12 * sn(t, 0.9), nod=0.1, lean=0.05, hip=(0, 0, 0.08), cape=1.0, wind=0.3),
 'Gather':  lambda t: dict(arm_l=AR('cast', 'overhead', ramp(t, 0, 1.6)), arm_r=AR('cast', 'overhead', ramp(t, 0.1, 1.7)), lean=mix(0.05, -0.5, ramp(t, 0, 1.7)), nod=mix(0.1, -0.45, ramp(t, 0, 1.7)), hip=(0, 0, 0.08 + 0.05 * ramp(t, 0, 1.7)), cape=1.1, wind=0.45, leg_l=LG('stand', 'back', 0.4), leg_r=LG('stand', 'back', 0.2)),
 'Advance': lambda t: dict(arm_l=AR('wideup', 'wings', ramp(t, 0, 1.8)), arm_r=AR('wideup', 'wings', ramp(t, 0.1, 1.9)), lean=mix(-0.5, 0.3, ramp(t, 0, 2.2)), nod=mix(-0.45, 0.15, ramp(t, 0, 2.2)), hip=(0, 0.04 * ramp(t, 0, 2.5), 0.1), leg_l=LG('hang', 'fwd', 0.5 + 0.5 * sn(t, 0.4)), leg_r=LG('hang', 'fwd', 0.5 - 0.5 * sn(t, 0.4)), cape=1.2, wind=0.6),
 'Windup':  lambda t: dict(arm_l=AR('wings', 'tback', ramp(t, 0, 2.0)), arm_r=AR('wings', 'tback', ramp(t, 0.1, 2.1)), lean=mix(0.3, -0.35, ramp(t, 0, 2.2)), nod=mix(0.15, -0.2, ramp(t, 0, 2.2)), twist=0.6 * ramp(t, 0, 2.2), ptw=0.3 * ramp(t, 0, 2.2), roll=0.15 * ramp(t, 0, 2.2), hip=(0, -0.03 * ramp(t, 0, 2.2), 0.1), cape=1.3, wind=0.7),
 'Strike':  lambda t: dict(arm_l=AR('tback', 'slam', ramp(t, 0.55, 1.1)) if t > 0.55 else AR('tback'), arm_r=AR('tback', 'tfwd', ramp(t, 0.5, 1.0)), lean=mix(-0.35, 0.65, ramp(t, 0.55, 1.05)) - 0.3 * ramp(t, 1.6, 2.5), nod=mix(-0.2, 0.55, ramp(t, 0.55, 1.05)) - 0.4 * ramp(t, 1.6, 2.5), twist=mix(0.6, -0.4, ramp(t, 0.55, 1.05)), ptw=mix(0.3, -0.2, ramp(t, 0.55, 1.05)), hip=(0, 0.1 * ramp(t, 0.55, 1.05), 0.1 - 0.06 * ramp(t, 0.55, 1.05)), cape=1.5, wind=1.0, leg_l=LG('stand', 'fwd', ramp(t, 0.55, 1.05)), leg_r=LG('stand', 'back', ramp(t, 0.55, 1.05))),
 'Roar':    lambda t: dict(arm_l=AR('slam', 'wideup', ramp(t, 0, 0.9)), arm_r=AR('slam', 'wideup', ramp(t, 0.1, 1.0)), lean=mix(0.3, -0.32, ramp(t, 0, 0.9)) + 0.03 * sn(t, 8), nod=mix(0.3, -0.5, ramp(t, 0, 0.9)) + 0.04 * sn(t, 8), hip=(0, 0, 0.1 + 0.015 * sn(t, 8)), cape=1.6, wind=1.0, leg_l=LG('wide'), leg_r=LG('wide')),
 'SweepL':  lambda t: dict(arm_r=AR('wideup', 'across', ramp(t, 0.2, 1.4)), arm_l=AR('wideup', 'sweep', ramp(t, 0.2, 1.6)), twist=mix(-0.2, 0.6, ramp(t, 0.2, 1.6)), turn=mix(0, 0.4, ramp(t, 0.2, 1.6)), lean=0.1 * ramp(t, 0.2, 1.4) - 0.15, roll=0.12 * ramp(t, 0.2, 1.6), cape=1.5, wind=1.0, hip=(0, 0, 0.1)),
 'SweepR':  lambda t: dict(arm_l=AR('across', 'wideup', ramp(t, 0.3, 1.6)), arm_r=AR('sweep', 'wideup', ramp(t, 0.1, 1.5)), twist=mix(0.6, -0.6, ramp(t, 0.2, 1.8)), turn=mix(0.4, -0.5, ramp(t, 0.2, 1.8)), lean=-0.1, roll=-0.12 * ramp(t, 0.2, 1.8), cape=1.5, wind=1.0, hip=(0, 0, 0.1)),
 'Slam':    lambda t: dict(arm_l=AR('wideup', 'slam', ramp(t, 0.5, 1.0)) if t < 1.8 else AR('slam', 'low', ramp(t, 1.8, 2.3)), arm_r=AR('wideup', 'slam', ramp(t, 0.3, 0.8)) if t < 1.6 else AR('slam', 'wideup', ramp(t, 1.6, 2.2)), lean=0.55 * ramp(t, 0.4, 0.9) - 0.4 * ramp(t, 1.6, 2.4), nod=0.5 * ramp(t, 0.4, 0.9) - 0.5 * ramp(t, 1.6, 2.4), hip=(0, 0, 0.1 - 0.1 * ramp(t, 0.4, 0.9) + 0.1 * ramp(t, 1.6, 2.4)), cape=1.6, wind=1.0),
 'Tower':   lambda t: dict(arm_l=AR('wideup', 'wideout', 0.5 + 0.5 * sn(t, 0.2)), arm_r=AR('wideup', 'wideout', 0.5 - 0.5 * sn(t, 0.2)), lean=-0.2 + 0.05 * sn(t, 0.3), nod=-0.3, hip=(0, 0, 0.1 + br(t)), twist=0.15 * sn(t, 0.17), cape=1.7, wind=1.0, leg_l=LG('wide'), leg_r=LG('wide')),
}
chan = [('pelvis', 'rotation'), ('pelvis', 'translation')] + [(k, 'rotation') for k in ('spine_01', 'spine_02', 'spine_03', 'spine_04', 'neck', 'head', 'upperarm_l', 'lowerarm_l', 'upperarm_r', 'lowerarm_r', 'thigh_l', 'calf_l', 'thigh_r', 'calf_r')]
NODE = dict(B);
for n_, i in enumerate(CAPE): NODE['cape%d' % n_] = i
for n_, i in enumerate(CLOTH): NODE['cloth%d' % n_] = i
chan += [('cape%d' % n_, 'rotation') for n_ in range(len(CAPE))] + [('cloth%d' % n_, 'rotation') for n_ in range(len(CLOTH))]
def add_accessor(arr, typ, minmax=False):
    arr = np.ascontiguousarray(arr, np.float32); off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(arr.tobytes()); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}); a = {'bufferView': len(g['bufferViews']) - 1, 'componentType': 5126, 'count': len(arr), 'type': typ}
    if minmax: a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
    g['accessors'].append(a); return len(g['accessors']) - 1
T = np.arange(NF + 1) / FPS; tin = add_accessor(T, 'SCALAR', minmax=True); g['animations'] = []
rep = []
for name, fn in CL.items():
    tracks = {c: [] for c in chan}
    for f in range(NF + 1):
        t = f / FPS; pose(fn(t), t)
        for (k, path) in chan: tracks[(k, path)].append(fk.R[NODE[k]].copy() if path == 'rotation' else fk.T[NODE[k]].copy())
    samplers = []; channels = []
    for (k, path), arr in tracks.items():
        arr = np.array(arr)
        if path == 'rotation':
            for i in range(1, len(arr)):
                if float(arr[i] @ arr[i - 1]) < 0: arr[i] = -arr[i]
        out = add_accessor(arr, 'VEC4' if path == 'rotation' else 'VEC3'); samplers.append({'input': tin, 'output': out, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': NODE[k], 'path': path}})
    g['animations'].append({'name': 'I_' + name, 'samplers': samplers, 'channels': channels})
    pose(fn(1.2), 1.2); rel = lambda k: np.round([(fk.pos(B[k]) - fk.pos(B['pelvis'])) @ left, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ up, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ fwd], 2).tolist()
    rep.append('%-9s hand_l %s hand_r %s head %s foot_l %s' % (name, rel('hand_l'), rel('hand_r'), rel('head'), rel('foot_l')))
g['buffers'][0]['byteLength'] = len(b)
print('\n'.join(rep)); g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb); print(dst, os.path.getsize(dst) // 1024, 'KB', len(CL), 'clips')
