"""python anim_clips.py <jones|cyclo> in.glb out.glb
Adds MANY looping clips (8 s, 12 fps each, same bone / channel layout so the plugin can crossfade them) straight to an ORIGINAL Epic-skeleton glb of the Doomsday Device event:
  jones : J_Idle J_Talk1 J_Talk2 J_Point J_Think J_Watch J_Tie J_Crossed J_Behind J_Shrug J_Look J_Ear J_Nervous J_Command J_Hips
  cyclo : C_Hover C_Float C_Cross C_Wings C_OrbHold C_Look (calm)  C_Charge C_Conduct C_Roar C_Spin C_Reach C_Pulse C_Slam C_Throw (energetic)
Every pose is made of limb directions in the CHARACTER frame (side / forward / up, measured from the rest pose): arms and legs are aimed (the bone is turned about its head so that the vector to its child
points where we want), the spine / neck / head take lean, twist, roll, nod, turn and tilt, the pelvis moves. Rotations are put on top of the rest transforms with a small forward-kinematics model of the file's own
hierarchy, so nothing depends on bone roll, units or Blender. The root node is turned -90 about x (both files are Z-up) to stand the model up."""
import sys, os, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
who, src, dst = sys.argv[1:4]
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
rootI = next(i for i, n in enumerate(N) if n['name'] == 'Sketchfab_model')
class FK:
    def __init__(s): s.R0 = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; s.T0 = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; s.S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]; s.reset()
    def reset(s): s.R = [r.copy() for r in s.R0]; s.T = [t.copy() for t in s.T0]; s.c = {}
    def world(s, i):
        if i in s.c: return s.c[i]
        M = np.eye(4); M[:3, :3] = q2m(s.R[i] if i != rootI else [0, 0, 0, 1]) @ np.diag(s.S[i]); M[:3, 3] = s.T[i]; s.c[i] = s.world(par[i]) @ M if i in par else M; return s.c[i]
    def pos(s, i): return s.world(i)[:3, 3]
    def wrot(s, i): m = s.world(i)[:3, :3]; return m / np.linalg.norm(m, axis=0)
    def rot_q(s, i, q):
        Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ q2m(q) @ Rp @ q2m(s.R[i])); s.c = {}
    def aim(s, i, child, d): s.rot_q(i, between(s.pos(child) - s.pos(i), d))
    def move(s, i, dw): s.T[i] = s.T[i] + np.linalg.inv(s.world(par[i])[:3, :3]) @ dw; s.c = {}
fk = FK()
def find(prefix, nth=0):
    k = 0
    for i, n in enumerate(N):
        s = n['name'].lower()
        if s.startswith(prefix) and 'twist' not in s and 'end' not in s and not s.startswith('ik'):
            if k == nth: return i
            k += 1
    raise KeyError(prefix)
B = {k: find(k) for k in ('pelvis', 'spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05', 'neck_01', 'neck_02', 'head_0', 'upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r', 'thigh_l', 'calf_l', 'foot_l', 'thigh_r', 'calf_r', 'foot_r')}
up = fk.pos(B['head_0']) - fk.pos(B['pelvis']); up /= np.linalg.norm(up); left = fk.pos(B['thigh_l']) - fk.pos(B['thigh_r']); left -= up * (left @ up); left /= np.linalg.norm(left); fwd = np.cross(left, up)
def dirw(side, f, u): v = left * side + fwd * f + up * u; return v / np.linalg.norm(v)
# ---- arm presets (character frame: side (toward the arm's own side), forward, up) for the upper arm and the forearm
ARM = {'rest': ((0.25, 0.05, -0.95), (0.2, 0.5, -0.85)), 'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'up': ((0.25, 0, 1), (0.1, 0, 1)), 'fwd': ((0.2, 1, 0), (0.1, 1, 0.1)),
       'hips': ((0.8, -0.1, -0.4), (-0.4, 0.3, -0.5)), 'cross': ((0.1, 0.4, -0.9), (-1.0, 0.5, 0.0)), 'chin': ((0.15, 0.3, -0.9), (-0.3, 0.55, 0.8)), 'tie': ((0.2, 0.35, -0.9), (-0.5, 0.8, 0.3)), 'watch': ((0.15, 0.5, -0.8), (-0.4, 0.8, 0.3)),
       'ear': ((0.5, 0.0, -0.4), (-0.3, 0.1, 0.95)), 'shrug': ((0.6, 0, -0.7), (0.8, 0.5, 0.2)), 'behind': ((0.3, -0.4, -0.85), (-0.5, -0.7, -0.3)), 'point': ((0.3, 0.8, -0.3), (0.15, 1.0, 0.0)), 'open': ((0.45, 0.5, -0.6), (0.5, 0.9, 0.1)),
       'shade': ((0.4, 0.2, 0.3), (-0.3, 0.8, 0.6)), 'sweep': ((0.9, 0.4, 0.2), (1.0, 0.5, 0.2)), 'fling': ((0.9, 0.3, 0.35), (1.0, 0.5, 0.45)), 'wideup': ((0.7, 0, 0.7), (0.5, 0, 1)), 'chest': ((0.2, 0.5, -0.3), (-0.3, 0.9, 0.1)),
       'cupped': ((0.3, 0.7, -0.4), (0.2, 1.0, 0.1)), 'wings': ((0.7, -0.6, 0.2), (0.6, -0.8, 0.3)), 'reach': ((0.3, 1, 0.3), (0.2, 1, 0.35)), 'slam': ((0.2, 0.3, -1), (0.1, 0.2, -1)), 'tback': ((0.4, -0.9, 0.4), (0.4, -1, 0.5)), 'tfwd': ((0.3, 1, 0.1), (0.2, 1, 0.2))}
LEG = {'stand': ((0.06, 0, -1), (0.03, 0, -1)), 'hang': ((0.12, 0.25, -1), (0.05, -0.55, -0.85)), 'wide': ((0.25, 0, -1), (0.2, 0, -1)), 'back': ((0.08, -0.2, -1), (0.04, -0.5, -0.9)), 'fwd': ((0.08, 0.3, -1), (0.05, 0.1, -1)), 'tuck': ((0.1, 0.5, -0.9), (0.05, -0.8, -0.6))}
def lerpd(a, b, k): return tuple(tuple((1 - k) * x + k * y for x, y in zip(ua, ub)) for ua, ub in zip(ARM[a] if isinstance(a, str) else a, ARM[b] if isinstance(b, str) else b))
def AR(a, b=None, k=0.0): return lerpd(a, b if b else a, max(0.0, min(1.0, k)))
def LG(a, b=None, k=0.0):
    A_, B_ = LEG[a], LEG[b if b else a]; k = max(0.0, min(1.0, k)); return tuple(tuple((1 - k) * x + k * y for x, y in zip(u, v)) for u, v in zip(A_, B_))
def sn(t, k, ph=0.0): return math.sin(2 * math.pi * (t / D * k + ph))
def pl(t, k, ph=0.0): return max(0.0, sn(t, k, ph))
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def trap(t, k, ph=0.0, w=0.25): return sm((sn(t, k, ph) + w) / (2 * w) * 0.5 + 0.5 - 0.5 + 0.0) if False else sm(0.5 + sn(t, k, ph) / (2 * w) * 0.5 + 0.0 * w)
def pose(p):
    fk.reset()
    h = p.get('hip', (0, 0, 0)); fk.move(B['pelvis'], left * h[0] + fwd * h[1] + up * h[2]); fk.rot_q(B['pelvis'], axq(up, p.get('ptw', 0.0)))
    chain = ('spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05')
    for k in chain:
        fk.rot_q(B[k], axq(left, p.get('lean', 0.0) / 5)); fk.rot_q(B[k], axq(fwd, p.get('roll', 0.0) / 5)); fk.rot_q(B[k], axq(up, p.get('twist', 0.0) / 5))
    for k, f in (('neck_01', 0.25), ('neck_02', 0.3), ('head_0', 0.45)): fk.rot_q(B[k], axq(left, p.get('nod', 0.0) * f)); fk.rot_q(B[k], axq(up, p.get('turn', 0.0) * f)); fk.rot_q(B[k], axq(fwd, p.get('tilt', 0.0) * f))
    for s_, sg in (('l', 1), ('r', -1)):
        lg = p.get('leg_' + s_, LG('stand')); fk.aim(B['thigh_' + s_], B['calf_' + s_], dirw(sg * lg[0][0], lg[0][1], lg[0][2])); fk.aim(B['calf_' + s_], B['foot_' + s_], dirw(sg * lg[1][0], lg[1][1], lg[1][2]))
        a = p.get('arm_' + s_, AR('rest')); fk.aim(B['upperarm_' + s_], B['lowerarm_' + s_], dirw(sg * a[0][0], a[0][1], a[0][2])); fk.aim(B['lowerarm_' + s_], B['hand_' + s_], dirw(sg * a[1][0], a[1][1], a[1][2]))
    return fk
# ---- the clips: functions of t (seconds, 0 .. 8, periodic) -> pose parameters
def stance(t, sh=0.0): return dict(leg_l=LG('stand', 'wide', 0.5 + 0.5 * sh), leg_r=LG('stand', 'wide', 0.5 - 0.5 * sh), hip=(0.015 * sh, 0, 0), ptw=0.04 * sh)
JONES = {
    'Idle': lambda t: dict(stance(t, sn(t, 1)), arm_l=AR('rest', 'down', 0.5 + 0.5 * sn(t, 2)), arm_r=AR('rest', 'down', 0.5 - 0.5 * sn(t, 2)), turn=0.15 * sn(t, 1), nod=0.03 * sn(t, 4), lean=0.02 * sn(t, 4)),
    'Talk1': lambda t: dict(stance(t, 0.3 * sn(t, 1)), arm_r=AR('rest', 'open', pl(t, 2)), turn=0.25 * sn(t, 1), nod=0.08 * sn(t, 4) + 0.04, lean=0.03),
    'Talk2': lambda t: dict(stance(t, 0.4 * sn(t, 1)), arm_l=AR('rest', 'open', pl(t, 2)), arm_r=AR('rest', 'open', pl(t, 2, 0.25)), turn=0.2 * sn(t, 1, 0.3), nod=0.1 * sn(t, 3), twist=0.1 * sn(t, 2)),
    'Point': lambda t: dict(stance(t), arm_r=AR('rest', 'point', sm(0.5 + 1.4 * sn(t, 1))), turn=-0.3 * sm(0.5 + 1.4 * sn(t, 1)), lean=0.06 * sm(0.5 + sn(t, 1)), twist=-0.15 * sm(0.5 + sn(t, 1))),
    'Think': lambda t: dict(stance(t), arm_r=AR('rest', 'chin', 0.85 + 0.1 * sn(t, 2)), arm_l=AR('rest', 'cross', 0.8), tilt=0.18 + 0.05 * sn(t, 1), nod=0.1, turn=0.2 * sn(t, 1)),
    'Watch': lambda t: dict(stance(t), arm_l=AR('rest', 'watch', sm(0.5 + 1.2 * sn(t, 1))), nod=0.25 * sm(0.5 + 1.2 * sn(t, 1)), lean=0.04, arm_r=AR('rest', 'hips', 0.3)),
    'Tie': lambda t: dict(stance(t), arm_r=AR('rest', 'tie', pl(t, 2) ** 0.5), nod=0.08 * pl(t, 2), turn=0.15 * sn(t, 1)),
    'Crossed': lambda t: dict(stance(t, 0.5 * sn(t, 1)), arm_l=AR('rest', 'cross', 0.95), arm_r=AR('rest', 'cross', 0.95), turn=0.3 * sn(t, 1, 0.2), tilt=0.05 * sn(t, 2), lean=-0.02),
    'Behind': lambda t: dict(leg_l=LG('stand', 'wide', 0.4), leg_r=LG('stand', 'wide', 0.4), arm_l=AR('rest', 'behind', 0.95), arm_r=AR('rest', 'behind', 0.95), lean=0.04 * sn(t, 2), nod=0.05 * sn(t, 2), turn=0.25 * sn(t, 1), hip=(0, 0.01 * sn(t, 2), 0)),
    'Shrug': lambda t: dict(stance(t), arm_l=AR('rest', 'shrug', pl(t, 2)), arm_r=AR('rest', 'shrug', pl(t, 2)), tilt=0.12 * pl(t, 2), nod=-0.05 * pl(t, 2), turn=0.1 * sn(t, 2), hip=(0, 0, 0.012 * pl(t, 2))),
    'Look': lambda t: dict(stance(t), turn=0.7 * sn(t, 2), nod=0.06 * sn(t, 4), arm_l=AR('rest', 'shade', pl(t, 1)), arm_r=AR('rest', 'down', 0.5), twist=0.15 * sn(t, 2)),
    'Ear': lambda t: dict(stance(t), arm_r=AR('rest', 'ear', 0.9 + 0.08 * sn(t, 3)), arm_l=AR('rest', 'hips', 0.4), tilt=-0.15, turn=-0.2 * sn(t, 1), nod=0.05),
    'Nervous': lambda t: dict(stance(t, sn(t, 3)), arm_l=AR('rest', 'tie', pl(t, 3)), arm_r=AR('rest', 'tie', pl(t, 3, 0.5)), turn=0.3 * sn(t, 3), lean=0.03 * sn(t, 6), hip=(0.02 * sn(t, 3), 0, 0)),
    'Command': lambda t: dict(stance(t), arm_r=AR('rest', 'sweep', pl(t, 2)), arm_l=AR('rest', 'hips', 0.9), turn=0.3 * sn(t, 2), twist=0.2 * sn(t, 2), lean=0.05),
    'Hips': lambda t: dict(stance(t, 0.3 * sn(t, 1)), arm_l=AR('rest', 'hips', 0.95), arm_r=AR('rest', 'hips', 0.95), turn=0.35 * sn(t, 1), nod=0.04 * sn(t, 2), lean=-0.03),
}
def hov(t, **kw): d = dict(leg_l=LG('hang', 'tuck', 0.2 + 0.2 * sn(t, 1)), leg_r=LG('hang', 'tuck', 0.2 - 0.2 * sn(t, 1)), hip=(0, 0, 0.05 * sn(t, 1)), lean=0.1); d.update(kw); return d
CYCLO = {
    'Hover': lambda t: hov(t, arm_l=AR('fling', 'wideup', 0.5 + 0.5 * sn(t, 1)), arm_r=AR('fling', 'wideup', 0.5 - 0.5 * sn(t, 1)), twist=0.25 * sn(t, 1, 0.25), turn=0.35 * sn(t, 1, 0.1), nod=0.15, tilt=0.15 * sn(t, 2)),
    'Float': lambda t: hov(t, arm_l=AR('rest', 'down', 0.5 + 0.5 * sn(t, 2)), arm_r=AR('rest', 'down', 0.5 - 0.5 * sn(t, 2)), hip=(0, 0, 0.09 * sn(t, 1)), tilt=0.2 * sn(t, 1), turn=0.2 * sn(t, 1, 0.3), lean=0.05, roll=0.08 * sn(t, 1)),
    'Cross': lambda t: hov(t, arm_l=AR('fling', 'cross', pl(t, 1)), arm_r=AR('fling', 'cross', pl(t, 1)), nod=0.2 * pl(t, 1), lean=0.15 * pl(t, 1)),
    'Wings': lambda t: hov(t, arm_l=AR('rest', 'wings', 0.5 + 0.5 * sn(t, 2)), arm_r=AR('rest', 'wings', 0.5 + 0.5 * sn(t, 2)), lean=0.25, nod=-0.1, hip=(0, 0, 0.06 * sn(t, 2))),
    'OrbHold': lambda t: hov(t, arm_l=AR('cupped', 'chest', 0.5 + 0.5 * sn(t, 2)), arm_r=AR('cupped', 'chest', 0.5 + 0.5 * sn(t, 2)), nod=0.25, lean=0.2, tilt=0.06 * sn(t, 1)),
    'Look': lambda t: hov(t, arm_l=AR('fling', None, 0), arm_r=AR('fling', None, 0), turn=0.8 * sn(t, 1), nod=0.1 * sn(t, 2), twist=0.3 * sn(t, 1, 0.1)),
    'Charge': lambda t: hov(t, arm_l=AR('chest', 'fling', sm(0.5 + sn(t, 2))), arm_r=AR('chest', 'fling', sm(0.5 + sn(t, 2))), lean=0.35 - 0.5 * sm(0.5 + sn(t, 2)), nod=0.3 - 0.7 * sm(0.5 + sn(t, 2)), hip=(0, 0, 0.1 * sm(0.5 + sn(t, 2)))),
    'Conduct': lambda t: hov(t, arm_l=AR('fling', 'wideup', 0.5 + 0.5 * sn(t, 2)), arm_r=AR('fling', 'wideup', 0.5 - 0.5 * sn(t, 2)), twist=0.35 * sn(t, 2, 0.25), roll=0.15 * sn(t, 2), turn=0.3 * sn(t, 2)),
    'Roar': lambda t: hov(t, arm_l=AR('wideup', None, 0), arm_r=AR('wideup', None, 0), lean=-0.25 + 0.04 * sn(t, 8), nod=-0.45 + 0.05 * sn(t, 8), hip=(0, 0, 0.1 + 0.02 * sn(t, 8)), leg_l=LG('wide'), leg_r=LG('wide')),
    'Spin': lambda t: hov(t, arm_l=AR('fling', None, 0), arm_r=AR('fling', None, 0), twist=0.9 * sn(t, 1), ptw=0.4 * sn(t, 1), roll=0.2 * sn(t, 2), turn=-0.5 * sn(t, 1)),
    'Reach': lambda t: hov(t, arm_r=AR('wings', 'reach', sm(0.5 + sn(t, 1))), arm_l=AR('wings', 'out', 0.5), twist=-0.4 * sm(0.5 + sn(t, 1)), lean=0.3, turn=-0.4 * sm(0.5 + sn(t, 1))),
    'Pulse': lambda t: hov(t, arm_l=AR('wideup', 'up', 0.5 + 0.5 * sn(t, 6) * 0.3 + 0.3), arm_r=AR('wideup', 'up', 0.5 + 0.5 * sn(t, 6, 0.5) * 0.3 + 0.3), nod=-0.2, lean=-0.1, hip=(0, 0, 0.03 * sn(t, 6))),
    'Slam': lambda t: hov(t, arm_l=AR('wideup', 'slam', sm(0.5 + 1.3 * sn(t, 1))), arm_r=AR('wideup', 'slam', sm(0.5 + 1.3 * sn(t, 1))), lean=-0.2 + 0.7 * sm(0.5 + 1.3 * sn(t, 1)), nod=0.5 * sm(0.5 + 1.3 * sn(t, 1)), hip=(0, 0, -0.12 * sm(0.5 + 1.3 * sn(t, 1)))),
    'Throw': lambda t: hov(t, arm_r=AR('tback', 'tfwd', sm(0.5 + 1.3 * sn(t, 2))), arm_l=AR('fling', 'reach', sm(0.5 + 1.3 * sn(t, 2))), twist=0.5 * (0.5 - sm(0.5 + 1.3 * sn(t, 2))) * 2, lean=0.1 + 0.3 * sm(0.5 + 1.3 * sn(t, 2))),
}
CL = JONES if who == 'jones' else CYCLO; PRE = 'J_' if who == 'jones' else 'C_'
chan = [('pelvis', 'rotation'), ('pelvis', 'translation')] + [(k, 'rotation') for k in ('spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05', 'neck_01', 'neck_02', 'head_0', 'upperarm_l', 'lowerarm_l', 'upperarm_r', 'lowerarm_r', 'thigh_l', 'calf_l', 'thigh_r', 'calf_r')]
def add_accessor(arr, typ, minmax=False):
    arr = np.ascontiguousarray(arr, np.float32); off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(arr.tobytes()); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}); a = {'bufferView': len(g['bufferViews']) - 1, 'componentType': 5126, 'count': len(arr), 'type': typ}
    if minmax: a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
    g['accessors'].append(a); return len(g['accessors']) - 1
T = np.arange(NF + 1) / FPS; tin = add_accessor(T, 'SCALAR', minmax=True); g['animations'] = []
cb = np.array([float(fk.pos(B['head_0'])[0])])                       # (keeps numpy happy about the module state)
rep = []
for name, fn in CL.items():
    tracks = {c: [] for c in chan}
    for f in range(NF + 1):
        t = (f % NF) / FPS; pose(fn(t))
        for (k, path) in chan: tracks[(k, path)].append(fk.R[B[k]].copy() if path == 'rotation' else fk.T[B[k]].copy())
    samplers = []; channels = []
    for (k, path), arr in tracks.items():
        arr = np.array(arr)
        if path == 'rotation':
            for i in range(1, len(arr)):
                if float(arr[i] @ arr[i - 1]) < 0: arr[i] = -arr[i]
        out = add_accessor(arr, 'VEC4' if path == 'rotation' else 'VEC3'); samplers.append({'input': tin, 'output': out, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': B[k], 'path': path}})
    g['animations'].append({'name': PRE + name, 'samplers': samplers, 'channels': channels})
    pose(fn(D * 0.31)); rel = lambda k: np.round([(fk.pos(B[k]) - fk.pos(B['pelvis'])) @ left, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ up, (fk.pos(B[k]) - fk.pos(B['pelvis'])) @ fwd], 2).tolist()
    rep.append('%-10s hand_l %s hand_r %s head %s foot_l %s' % (name, rel('hand_l'), rel('hand_r'), rel('head_0'), rel('foot_l')))
g['buffers'][0]['byteLength'] = len(b); N[rootI]['rotation'] = [-0.70710678, 0, 0, 0.70710678]
print('\n'.join(rep)); g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb); print(dst, os.path.getsize(dst) // 1024, 'KB', len(CL), 'clips')
