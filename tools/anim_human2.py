"""anim_human2.py <jones|ue> in.glb out.glb [gun_scale]   IK humanoid clips (H_ prefix, 20 fps, 12 s) for Jones / Foundation / Slone.
Feet are IK-planted (walk / run cycles are driven by distance walked, so no sliding), the hands are IK-solved to the SCAR's grips and the rifle is a 'Gun' node under the top spine bone that the clips animate (the plugin glues the SCAR model to it).
Every pose is data (BASE overridden by keys), clips are keyframed poses + small procedural overlays."""
import sys, os, math, copy, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
prof, src, dst = sys.argv[1:4]; GS = float(sys.argv[4]) if len(sys.argv) > 4 else 0.85
g, b = slim_glb.read_glb(src); b = bytearray(b); N = g['nodes']; D = 12.0; FPS = 20; NF = int(D * FPS)
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
        if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T
        else: M = np.eye(4); M[:3, :3] = q2m(s.R[i]) @ np.diag(s.S[i]); M[:3, 3] = s.T[i]
        s.c[i] = s.world(par[i]) @ M if i in par else M; return s.c[i]
    def pos(s, i): return s.world(i)[:3, 3]
    def wrot(s, i): m = s.world(i)[:3, :3]; return m / np.linalg.norm(m, axis=0)
    def rot_q(s, i, q): Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ q2m(q) @ Rp @ q2m(s.R[i])); s.c = {}
    def set_wrot(s, i, Rw): Rp = s.wrot(par[i]); s.R[i] = m2q(Rp.T @ Rw); s.c = {}
    def aim(s, i, child, d): s.rot_q(i, between(s.pos(child) - s.pos(i), d))
    def move(s, i, dw): s.T[i] = s.T[i] + np.linalg.inv(s.world(par[i])[:3, :3]) @ dw; s.c = {}
fk = FK()
import re
def nm(prefix):
    for i, n in enumerate(N):
        if n['name'].startswith(prefix): return i
    raise KeyError(prefix)
def nmre(pat):
    for i, n in enumerate(N):
        if re.match(pat, n['name']): return i
    raise KeyError(pat)
if prof == 'jones':
    B = dict(pelvis=nm('Hips_02'), s1=nm('Spine_03'), s2=nm('Chest_04'), neck=nm('Neck_047'), head=nm('Head_048'), ual=nm('Left arm_06'), lal=nm('Left elbow_07'), hl=nm('Left wrist_08'), uar=nm('Right arm_027'), lar=nm('Right elbow_028'), hr=nm('Right wrist_029'),
             tl=nm('Left leg_0149'), cl=nm('Left knee_0150'), fl=nm('Left ankle_0151'), tr=nm('Right leg_0163'), cr=nm('Right knee_0164'), fr=nm('Right ankle_0165')); SPINE = ['s1', 's2']
elif prof == 'care':
    B = dict(pelvis=nm('C_BipedSpine_Main_Pelvis'), s1=nm('C_BipedSpine_Main_Spine1'), s2=nm('C_BipedSpine_Main_Spine2'), s3=nm('C_BipedSpine_Main_Spine3'), s4=nm('C_BipedSpine_Main_Chest'), neck=nm('C_BipedHeadNeck_A_NeckBase'), head=nm('C_BipedHeadNeck_A_Head_Jnt'),
             ual=nm('L_BipedArm_A_Shoulder_Jnt'), lal=nm('L_BipedArm_A_Elbow_Jnt'), hl=nm('L_BipedArm_A_Wrist_Jnt'), uar=nm('R_BipedArm_A_Shoulder_Jnt'), lar=nm('R_BipedArm_A_Elbow_Jnt'), hr=nm('R_BipedArm_A_Wrist_Jnt'),
             tl=nm('L_BipedLeg_A_Thigh'), cl=nm('L_BipedLeg_A_Knee'), fl=nm('L_BipedLeg_A_Ankle'), tr=nm('R_BipedLeg_A_Thigh'), cr=nm('R_BipedLeg_A_Knee'), fr=nm('R_BipedLeg_A_Ankle')); SPINE = ['s1', 's2', 's3', 's4']
else:
    B = dict(pelvis=nm('pelvis_'), s1=nm('spine_01_'), s2=nm('spine_02_'), s3=nm('spine_03_'), s4=nm('spine_04_'), s5=nm('spine_05_'), neck=nm('neck_01_'), head=nmre(r'^head_\d+$'), ual=nm('upperarm_l_'), lal=nm('lowerarm_l_'), hl=nm('hand_l_'), uar=nm('upperarm_r_'), lar=nm('lowerarm_r_'), hr=nm('hand_r_'),
             tl=nm('thigh_l_'), cl=nm('calf_l_'), fl=nm('foot_l_'), tr=nm('thigh_r_'), cr=nm('calf_r_'), fr=nm('foot_r_')); SPINE = ['s1', 's2', 's3', 's4', 's5']
TOP = SPINE[-1]
# ---- the character frame: up (pelvis -> neck), left (right shoulder -> left shoulder), fwd = left x up (glTF: Jones faces +Z)
up = np.array([0.0, 1.0, 0.0]); left = np.array([1.0, 0.0, 0.0]); fwd = np.array([0.0, 0.0, 1.0])      # every mesh faces +Z in the file (the rest poses are NOT symmetric, so the shoulders / hips can't define the frame)
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NCc = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(i):
    a = g['accessors'][i]; v = g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NCc[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(b, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
    return np.frombuffer(b, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
def rest_mesh():                                          # the skinned mesh in the rest pose (vertices only)
    vs = []
    for ni, n in enumerate(N):
        if 'mesh' not in n or 'skin' not in n: continue
        sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
        for pr in g['meshes'][n['mesh']]['primitives']:
            at = pr['attributes']; Pp = acc(at['POSITION']); JI = acc(at['JOINTS_0']).astype(int); WT = acc(at['WEIGHTS_0']); Ph = np.concatenate([Pp, np.ones((len(Pp), 1))], 1); o = np.zeros((len(Pp), 3)); M = np.array([fk.world(J[j]) @ ibm[j] for j in range(len(J))])
            for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
            vs.append(o)
    return np.concatenate(vs)
RV0 = rest_mesh(); lowv = RV0[RV0[:, 1] < RV0[:, 1].min() + 0.1]; FOOTYAW = {}
for s_, sg in (('l', 1), ('r', -1)):                       # how far each foot is turned from +Z in the rest pose (the clips set the foot world rotation, so this is taken out)
    f = lowv[(lowv[:, 0] * sg) > 0.02]; c = f[:, [0, 2]] - f[:, [0, 2]].mean(0); ax_ = np.linalg.svd(c, full_matrices=False)[2][0]; ax_ = ax_ if ax_[1] > 0 else -ax_; FOOTYAW[s_] = math.atan2(ax_[0], ax_[1])
print('rest foot yaw', {k: round(math.degrees(v), 1) for k, v in FOOTYAW.items()}, 'mesh bbox', np.round(RV0.min(0), 2), np.round(RV0.max(0), 2))
A0 = np.stack([left, up, fwd], axis=1); WR0 = fk.wrot(B[TOP]).copy(); RF0 = {'fl': fk.wrot(B['fl']).copy(), 'fr': fk.wrot(B['fr']).copy()}
HW = abs(float((fk.pos(B['tl']) - fk.pos(B['tr'])) @ left)) / 2; ANK = float(fk.pos(B['fl']) @ up); P0 = fk.pos(B['pelvis']); O = P0 - up * float(P0 @ up) + 0.0; H0 = float(P0 @ up); FX = HW + 0.07 * (H0 / 0.934); SCL = H0 / 0.934
def L(a, c): return float(np.linalg.norm(fk.pos(B[a]) - fk.pos(B[c])))
LEN = {'ual': (L('ual', 'lal'), L('lal', 'hl')), 'uar': (L('uar', 'lar'), L('lar', 'hr')), 'tl': (L('tl', 'cl'), L('cl', 'fl')), 'tr': (L('tr', 'cr'), L('cr', 'fr'))}; REACH = LEN['ual'][0] + LEN['ual'][1]
print('frame up', np.round(up, 2), 'left', np.round(left, 2), 'fwd', np.round(fwd, 2), 'hip half', round(HW, 3), 'ankle', round(ANK, 3), 'hip h', round(H0, 3), 'reach', round(REACH, 3))
# ---- the Gun node (child of the top spine bone), animated by the clips
GUN = len(N); N.append({'name': 'Gun'}); N[par_top := B[TOP]].setdefault('children', []).append(GUN); par[GUN] = B[TOP]
def ik(root, mid, end, target, pole, key):
    p0 = fk.pos(root); l1, l2 = LEN[key]; d = target - p0; dist = float(np.linalg.norm(d)); dn = d / max(dist, 1e-6); dc = min(max(dist, abs(l1 - l2) + 1e-3), l1 + l2 - 1e-3)
    a = (l1 * l1 - l2 * l2 + dc * dc) / (2 * dc); h = math.sqrt(max(l1 * l1 - a * a, 0.0)); pv = pole - dn * float(pole @ dn); nv = float(np.linalg.norm(pv))
    pv = pv / nv if nv > 1e-5 else np.cross(dn, left); elbow = p0 + dn * a + pv * h; wrist = p0 + dn * dc
    fk.aim(root, mid, elbow - p0); fk.aim(mid, end, wrist - fk.pos(mid))
def sn(t, f, ph=0.0): return math.sin(2 * math.pi * (t * f + ph))
def cs(t, f, ph=0.0): return math.cos(2 * math.pi * (t * f + ph))
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def ramp(t, a, c): return sm((t - a) / (c - a))
def mix(a, c, k): return a + (c - a) * k
# ---- gun poses: (pad left, pad up, pad fwd) of the stock end relative to the shoulder centre in chest axes, then pitch (+ = muzzle down), yaw (+ = muzzle to the left), roll
SHOULDER = (-0.085, -0.03, 0.08, 0.0, 0.03, 0.0); HIGH = (-0.10, -0.12, 0.12, -0.5, 0.12, 0.0); LOW = (-0.10, -0.26, 0.13, 0.7, 0.12, 0.0); HIP = (-0.13, -0.32, 0.10, 0.12, 0.12, 0.0)
SLING = (-0.03, -0.44, 0.15, 1.25, 0.5, 0.25); RELOAD = (-0.12, -0.28, 0.14, -0.15, 0.3, 0.9); ONEHAND = (-0.12, -0.34, 0.14, 0.35, 0.15, 0.0)
BACK = (-0.02, -0.12, -0.26, 1.45, 0.0, 0.2)
FORE = (0.0, 0.03, 0.30); MAG = (0.0, -0.075, 0.17); CHARGE = (0.045, 0.085, 0.05)
BASE = dict(pel=(0.0, 0.0, 0.0), pyaw=0.0, ppitch=0.0, proll=0.0, lean=0.0, twist=0.0, roll=0.0, nod=0.0, turn=0.0, tilt=0.0, fl=(FX, 0.0, 0.0), fr=(-FX, 0.0, 0.0), flp=0.0, frp=0.0, fly=0.0, fry=0.0,
            hl=(0.25, -1.0, 0.05, 0.97), hr=(0.25, -1.0, 0.05, 0.97), el=(0.6, -0.8, -0.5), er=(0.6, -0.8, -0.5), gl=0.0, gr=0.0, gun=SLING, glp=FORE, kl=(0.2, 0.0, 1.0), kr=(0.2, 0.0, 1.0))
def P(base=None, **kw): p = copy.deepcopy(base if base is not None else BASE); p.update(kw); return p
def add(p, **d):
    p = copy.deepcopy(p)
    for k, v in d.items(): p[k] = tuple(np.array(p[k], float) + np.array(v, float)) if isinstance(p[k], tuple) else p[k] + v
    return p
def lerp_pose(a, c, k):
    k = max(0.0, min(1.0, k)); out = {}
    for key, va in a.items(): vb = c[key]; out[key] = tuple((1 - k) * x + k * y for x, y in zip(va, vb)) if isinstance(va, tuple) else (1 - k) * va + k * vb
    return out
def kf(keys, t, ease=sm):
    if t <= keys[0][0]: return keys[0][1]
    for i in range(1, len(keys)):
        if t < keys[i][0]: a, c = keys[i - 1], keys[i]; return lerp_pose(a[1], c[1], ease((t - a[0]) / (c[0] - a[0])))
    return keys[-1][1]
def cyc(keys, t, period):  # keys over [0, period] looping (last key should equal the first)
    return kf(keys, t % period)
# ---- the pose solver
def solve(p):
    fk.reset(); o = tuple(x * SCL for x in p['pel']); fk.move(B['pelvis'], left * o[0] + up * o[1] + fwd * o[2]); fk.rot_q(B['pelvis'], axq(up, p['pyaw'])); fk.rot_q(B['pelvis'], axq(left, p['ppitch'])); fk.rot_q(B['pelvis'], axq(fwd, p['proll']))
    n = len(SPINE)
    for k in SPINE: fk.rot_q(B[k], axq(left, p['lean'] / n)); fk.rot_q(B[k], axq(fwd, p['roll'] / n)); fk.rot_q(B[k], axq(up, p['twist'] / n))
    for k, f in (('neck', 0.4), ('head', 0.6)): fk.rot_q(B[k], axq(left, p['nod'] * f)); fk.rot_q(B[k], axq(up, p['turn'] * f)); fk.rot_q(B[k], axq(fwd, p['tilt'] * f))
    ax = (fk.wrot(B[TOP]) @ WR0.T) @ A0
    for s_, sg in (('l', 1), ('r', -1)):                                                         # legs
        f = p['f' + s_]; tgt = O + left * f[0] + up * (ANK + f[1] * SCL) + fwd * f[2] * SCL; kn = p['k' + s_]; pole = left * sg * kn[0] + up * kn[1] + fwd * kn[2]
        ik(B['t' + s_], B['c' + s_], B['f' + s_], tgt, pole, 't' + s_)
        Rf = axq(up, p['f' + s_ + 'y'] - FOOTYAW[s_]); Rp = axq(left, p['f' + s_ + 'p']); fk.set_wrot(B['f' + s_], q2m(Rf) @ q2m(Rp) @ RF0['f' + s_])
    SL, SR = fk.pos(B['ual']), fk.pos(B['uar']); SC = (SL + SR) / 2; G = p['gun']; Rl = q2m(axq([1, 0, 0], G[3])) @ q2m(axq([0, 1, 0], G[4])) @ q2m(axq([0, 0, 1], G[5])); Rg = ax @ Rl
    padl = np.array([0.0, 0.12, -0.255]) * GS; gpos = SC + ax @ np.array(G[:3]) - Rg @ padl
    grip = {'r': gpos + Rg @ (np.array([0.0, -0.015, -0.075]) * GS), 'l': gpos + Rg @ (np.array(p['glp']) * GS + np.array([0.0, -0.02, -0.07]) * GS)}
    for s_, sg, U, Lw, H in (('l', 1, 'ual', 'lal', 'hl'), ('r', -1, 'uar', 'lar', 'hr')):
        h = p['h' + s_]; d = np.array([sg * h[0], h[1], h[2]], float); d /= np.linalg.norm(d); free = fk.pos(B[U]) + ax @ (d * (h[3] * REACH)); w = p['g' + s_]; tgt = free * (1 - w) + grip[s_] * w
        e = p['e' + s_]; ik(B[U], B[Lw], B[H], tgt, ax @ np.array([sg * e[0], e[1], e[2]], float), 'ua' + s_ if False else ('ual' if s_ == 'l' else 'uar'))
    Wg = np.eye(4); Wg[:3, :3] = Rg; Wg[:3, 3] = gpos; return Wg
# ---- gait: S = stride (m / cycle), Tc = cycle time, beta = duty factor
def gait(t, Tc, S, beta, lift, bob, lean, swing, gunp, run=False):
    u = (t / Tc) % 1.0
    def foot(u):
        if u < beta: return S * beta / 2 * (1 - 2 * u / beta), 0.0, (0.35 * sm((u - beta * 0.8) / (beta * 0.2)) if u > beta * 0.8 else 0.0)
        k = (u - beta) / (1 - beta); return -S * beta / 2 + S * beta * sm(k), lift * math.sin(math.pi * k) ** 0.9, mix(0.5, -0.1, sm(k))
    zl, yl, pl = foot(u); zr, yr, pr = foot((u + 0.5) % 1.0); ph = 2 * math.pi * u
    py = -0.10 + 0.08 * (0.5 - 0.5 * math.cos(4 * math.pi * (u - beta / 2))) if run else -0.052 + bob * (0.5 - 0.5 * math.cos(4 * math.pi * u))
    p = P(pel=(0.02 * math.sin(ph), py, 0.0), pyaw=0.09 * math.sin(ph), proll=0.03 * math.cos(ph) * (1 if not run else 0.5), twist=-0.1 * math.sin(ph), lean=lean, fl=(FX - 0.02, yl, zl), fr=(-FX + 0.02, yr, zr), flp=pl, frp=pr, nod=-lean * 0.7, turn=0.0, gun=gunp, gl=1.0, gr=1.0)
    p['roll'] = 0.02 * math.sin(ph); return p
def gait_free(t, Tc, S, beta, lift, bob, lean, swing, run=False):
    p = gait(t, Tc, S, beta, lift, bob, lean, swing, SLING, run); ph = 2 * math.pi * ((t / Tc) % 1.0); p['gl'] = p['gr'] = 0.0
    p['hl'] = (0.22, -1.0, -swing * math.sin(ph) * (1.0 if not run else 1.6), 0.96); p['hr'] = (0.22, -1.0, swing * math.sin(ph) * (1.0 if not run else 1.6), 0.96)
    if run: p['el'] = p['er'] = (0.5, -0.4, -1.0); p['hl'] = (0.3, -0.9, -swing * 1.6 * math.sin(ph) + 0.35, 0.8); p['hr'] = (0.3, -0.9, swing * 1.6 * math.sin(ph) + 0.35, 0.8)
    return p
STAND = P(pel=(0, -0.012, 0)); WIDE = dict(fl=(FX + 0.06, 0.0, 0.05), fr=(-FX - 0.06, 0.0, -0.1))
def two_hand(gunp, **kw): return P(gun=gunp, gl=1.0, gr=1.0, **kw)
AIM = two_hand(SHOULDER, pel=(0, -0.03, 0), lean=0.07, twist=0.12, turn=-0.1, nod=0.05, el=(0.4, -1, -0.1), er=(0.5, -1, -0.3), fl=(FX + 0.05, 0.0, 0.1), fr=(-FX - 0.02, 0.0, -0.18), pyaw=0.12)
READY = two_hand(LOW, pel=(0, -0.015, 0), lean=0.05, twist=0.05, el=(0.5, -1, -0.2), er=(0.6, -1, -0.2), fl=(FX + 0.03, 0.0, 0.08), fr=(-FX - 0.02, 0.0, -0.1))
HIGHP = two_hand(HIGH, pel=(0, -0.02, 0), lean=0.04, twist=0.08, el=(0.5, -1, -0.2), er=(0.6, -1, -0.2), fl=(FX + 0.04, 0.0, 0.08), fr=(-FX - 0.02, 0.0, -0.12), pyaw=0.08)
IDLE = P(pel=(0, -0.012, 0), gun=SLING, hl=(0.2, -1, 0.05, 0.96), hr=(0.2, -1, 0.05, 0.96)); IDLEB = P(IDLE, gun=BACK)
def recoil(t, period=2.4, shots=9, gap=0.1):
    ph = t % period; r = 0.0
    for k in range(shots):
        dt_ = ph - k * gap
        if dt_ >= 0: r = max(r, math.exp(-dt_ / 0.045) * (0.85 + 0.15 * ((k * 7) % 3) / 2))
    return r
def with_recoil(p, r): return add(p, gun=(0, 0, -0.03 * r, -0.05 * r, 0.0, 0.0), lean=-0.025 * r, pel=(0, 0, -0.012 * r), nod=-0.02 * r)
def sway(p, t, a=1.0): return add(p, lean=0.008 * a * sn(t, 0.27), turn=0.05 * a * sn(t, 0.11), pel=(0.012 * a * sn(t, 0.15), 0.003 * a * sn(t, 0.3), 0), nod=0.01 * a * sn(t, 0.21))
# ================= the clips =================
def c_idle(t): return sway(add(IDLE, **{}), t, 1.3)
def c_ready(t): return sway(READY, t, 1.0)
def c_alert(t): return add(sway(HIGHP, t, 1.0), turn=0.3 * sn(t, 0.12), twist=0.1 * sn(t, 0.12))
def c_aim(t): return add(sway(AIM, t, 0.4), gun=(0.0, 0.004 * sn(t, 0.7), 0.0, 0.004 * sn(t, 0.5), 0.0, 0.0))
def c_fire(t): return with_recoil(add(AIM, turn=0.0, pel=(0, 0, 0)), recoil(t))
def c_firek(t): return with_recoil(P(AIM, pel=(0, -0.42, 0.04), fl=(FX + 0.03, 0.0, 0.25), fr=(-FX - 0.02, -0.2, -0.28), lean=0.28, twist=0.15, frp=0.5, ppitch=0.1), recoil(t))
def c_hold(t): return sway(add(READY, turn=-0.1), t, 0.8)
def c_crouch(t): return sway(P(READY, pel=(0, -0.38, 0.02), fl=(FX + 0.05, 0.0, 0.17), fr=(-FX - 0.04, 0.0, -0.1), lean=0.35, ppitch=0.05, nod=0.1, gun=HIGH), t, 0.6)
def c_peek(t): return kf([(0.0, c_crouch(0)), (0.9, c_crouch(0)), (1.6, with_recoil(add(c_aim(0), pel=(0.08, -0.15, 0.1)), 0)), (3.2, with_recoil(add(c_aim(0), pel=(0.08, -0.15, 0.1)), 0)), (4.0, c_crouch(0))], t)
def c_kneel(t): return c_firek(t)
def c_reload(t):
    a = c_ready(0); mag_tilt = P(RELOAD and READY, gun=RELOAD, gl=1.0, gr=1.0, glp=FORE, lean=0.12, nod=0.18, twist=0.1, pel=(0, -0.03, 0.0), fl=READY['fl'], fr=READY['fr']); pocket = P(mag_tilt, gl=0.0, hl=(0.12, -0.6, 0.78, 0.8), glp=MAG)
    k = [(0.0, c_aim(0)), (0.5, mag_tilt), (0.85, P(mag_tilt, glp=MAG)), (1.1, P(mag_tilt, glp=MAG)), (1.5, pocket), (1.9, pocket), (2.3, P(mag_tilt, glp=MAG)), (2.55, P(mag_tilt, glp=MAG, gun=add(mag_tilt, gun=(0, 0, 0, 0, 0, 0))['gun'])), (2.85, P(mag_tilt, glp=CHARGE)), (3.15, P(mag_tilt, glp=CHARGE, lean=0.1)), (3.45, P(mag_tilt, glp=FORE)), (4.1, c_aim(0))]
    p = kf(k, t)
    if 2.55 <= t < 2.75: p = add(p, gun=(0, 0.01, 0, 0, 0, 0))
    if 2.85 <= t < 3.15: p = add(p, hl=(0, 0, -0.15 * sn(t, 2.5), 0))
    return p
def c_walk(t): return gait(t, 1.0, 1.5, 0.62, 0.07, 0.025, 0.06, 0.0, LOW)
def c_run(t): return gait(t, 0.75, 3.2, 0.40, 0.17, 0.05, 0.2, 0.0, HIP, True)
def c_walks(t): return gait_free(t, 1.0, 1.5, 0.62, 0.07, 0.025, 0.05, 0.45)
def c_climb(t): p = gait(t, 1.05, 1.35, 0.64, 0.12, 0.03, 0.12, 0.0, LOW); return add(p, nod=-0.05, pel=(0, 0.0, 0.03))
def c_beckon(t):
    wave = sn(t, 1.4); return P(IDLE, gun=ONEHAND, gl=1.0, glp=FORE, turn=-0.35, twist=0.18, lean=0.06, hr=(-0.2 + 0.25 * wave, 0.0 + 0.3 * wave, 0.9, 0.95), er=(0.7, -0.2, -0.3), fl=(FX + 0.04, 0, 0.1), fr=(-FX - 0.02, 0, -0.1)) if t > 0.4 else lerp_pose(sway(P(IDLE), 0), P(IDLE, gun=ONEHAND, gl=1.0, glp=FORE), 0.0)
def c_point(t):
    k = ramp(t, 0.2, 0.8); base = P(IDLE, gun=ONEHAND, gl=1.0, glp=FORE, twist=0.1 * k, turn=-0.25 * k, lean=0.06 * k, fl=(FX + 0.04, 0, 0.12 * k), fr=(-FX - 0.02, 0, -0.12 * k)); return lerp_pose(IDLE, P(base, hr=(0.05, 0.05, 1.0, 1.0), er=(0.5, -1, -0.1)), k)
def c_look(t):
    k = ramp(t, 0.3, 1.2); return lerp_pose(IDLE, P(IDLE, hr=(-0.1, 0.55, 0.85, 0.85), er=(0.7, 0.4, -0.2), nod=-0.18, turn=0.15 * sn(t, 0.2), lean=-0.05, fl=(FX + 0.05, 0, 0.05), fr=(-FX - 0.02, 0, -0.1), tilt=0.06), k)
def c_wake(t):
    k = [(0.0, P(IDLEB, pel=(0, -0.55, 0.05), fl=(FX, 0.0, 0.22), fr=(-FX, -0.25, -0.25), lean=0.55, frp=0.8, hr=(-0.25, -0.3, 0.9, 0.8), er=(0.6, 0.0, -0.2), nod=0.2, turn=0.2, ppitch=0.15)), (0.6, P(IDLEB, pel=(0, -0.55, 0.05), fl=(FX, 0.0, 0.22), fr=(-FX, -0.25, -0.25), lean=0.55, frp=0.8, hr=(-0.25, -0.3, 0.95, 0.9), er=(0.6, 0.0, -0.2), nod=0.2, turn=0.2, ppitch=0.15)),
         (1.0, P(IDLEB, pel=(0, -0.55, 0.05), fl=(FX, 0.0, 0.22), fr=(-FX, -0.25, -0.25), lean=0.55, frp=0.8, hr=(-0.25, -0.35, 0.95, 0.9), er=(0.6, 0.0, -0.2), nod=0.2, turn=0.2, ppitch=0.15)), (1.4, P(IDLEB, pel=(0, -0.55, 0.05), fl=(FX, 0.0, 0.22), fr=(-FX, -0.25, -0.25), lean=0.55, frp=0.8, hr=(-0.25, -0.3, 0.95, 0.9), er=(0.6, 0.0, -0.2), nod=0.2, turn=0.2, ppitch=0.15)),
         (2.8, P(IDLEB, pel=(0, -0.3, 0.03), lean=0.3, fl=(FX, 0, 0.1), fr=(-FX, 0, -0.1), turn=-0.2)), (4.0, c_idle(0))]
    p = kf(k, t)
    if t < 1.8: p = add(p, hr=(0, 0.12 * sn(t, 2.0), 0, 0), lean=0.02 * sn(t, 2.0))
    return p
def c_shove(t):
    k = ramp(t, 0.3, 0.9) - ramp(t, 1.6, 2.4); hand = (0.1, 0.15, 1.0, 1.0)
    return lerp_pose(c_ready(0), P(READY, gun=BACK, gl=0.0, gr=0.0, hl=hand, hr=hand, el=(0.9, -0.4, -0.2), er=(0.9, -0.4, -0.2), lean=0.28, pel=(0, -0.08, 0.1), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.35), twist=0.0, nod=0.1), k)
def c_throw(t):
    crouchp = P(IDLEB, gun=BACK, pel=(0, -0.42, 0.05), lean=0.4, fl=(FX + 0.04, 0, 0.12), fr=(-FX - 0.04, 0, -0.1), hl=(0.1, -0.5, 0.9, 0.8), hr=(0.1, -0.5, 0.9, 0.8), el=(0.8, -0.5, 0.0), er=(0.8, -0.5, 0.0), nod=0.15)
    back = P(IDLEB, gun=BACK, pel=(0, -0.1, -0.05), lean=-0.3, twist=0.7, fl=(FX + 0.05, 0, 0.05), fr=(-FX - 0.05, 0, -0.3), hl=(0.2, 0.5, -0.5, 0.95), hr=(0.2, 0.5, -0.5, 0.95), el=(0.5, 0.3, -1.0), er=(0.5, 0.3, -1.0), turn=0.4)
    fwdp = P(IDLEB, gun=BACK, pel=(0, -0.12, 0.15), lean=0.4, twist=-0.6, fl=(FX + 0.04, 0, 0.35), fr=(-FX - 0.02, 0, -0.35), hl=(0.1, 0.4, 1.0, 1.0), hr=(0.1, 0.4, 1.0, 1.0), el=(0.8, 0.0, 0.0), er=(0.8, 0.0, 0.0), turn=-0.3)
    return kf([(0.0, c_ready(0)), (0.6, crouchp), (1.1, crouchp), (1.7, back), (2.15, fwdp), (3.0, c_ready(0))], t)
def c_talk(t):
    a = sn(t, 0.43); c = sn(t, 0.31, 0.3)
    return P(IDLE, hl=(0.5 + 0.2 * a, -0.15 + 0.3 * c, 0.65, 0.75), hr=(0.5 - 0.2 * c, -0.15 + 0.3 * a, 0.7, 0.75), el=(0.8, -0.5, -0.1), er=(0.8, -0.5, -0.1), turn=0.25 * sn(t, 0.17), nod=0.07 * sn(t, 0.5), lean=0.03, pel=(0.015 * sn(t, 0.12), -0.012, 0), fl=(FX + 0.03, 0, 0.05), fr=(-FX - 0.01, 0, -0.06))
def c_plead(t):
    k = ramp(t, 0.3, 1.5); p = P(IDLE, hl=(0.35, -0.3, 0.9, 0.9), hr=(0.35, -0.3, 0.9, 0.9), el=(0.9, -0.5, 0.0), er=(0.9, -0.5, 0.0), lean=0.1, nod=0.15, fl=(FX, 0, 0.28), fr=(-FX, 0, -0.1), pel=(0, -0.03, 0.06)); p = add(p, hl=(0.05 * sn(t, 1.2), 0.1 * sn(t, 0.9), 0, 0), hr=(0.05 * sn(t, 1.1), 0.1 * sn(t, 0.7), 0, 0)); return lerp_pose(IDLE, p, k)
def c_shrug(t):
    k = ramp(t, 0.1, 0.5) - ramp(t, 1.8, 2.4); return lerp_pose(IDLE, P(IDLE, hl=(0.7, -0.25, 0.4, 0.75), hr=(0.7, -0.25, 0.4, 0.75), el=(1.0, -0.2, 0.0), er=(1.0, -0.2, 0.0), tilt=0.15, nod=-0.05, pel=(0, -0.005, 0)), k)
def c_hit(t):
    k = ramp(t, 0.0, 0.12) * (1 - ramp(t, 0.12, 1.6)); return lerp_pose(c_ready(t), P(READY, gun=LOW, lean=-0.35, nod=-0.3, pel=(0, -0.05, -0.12), fl=(FX, 0, 0.28), fr=(-FX, 0, -0.3), hl=(0.2, 0.5, 0.8, 0.8), hr=(0.2, 0.5, 0.8, 0.8), gl=0.0, gr=0.0, el=(0.8, 0.4, 0.0), er=(0.8, 0.4, 0.0)), k)
def c_stagger(t):
    k = ramp(t, 0.0, 0.15) * (1 - ramp(t, 0.2, 1.8)); return lerp_pose(c_ready(t), P(READY, lean=-0.4, roll=0.2 * sn(t, 1.0), pel=(0.05, -0.08, -0.12), fl=(FX, 0, 0.35), fr=(-FX, 0, -0.35), nod=-0.25, gl=0.0, gr=1.0, gun=ONEHAND, hl=(0.9, 0.3, 0.2, 0.9), el=(1, 0, 0)), k)
def c_dodge(t):
    s = math.sin(math.pi * min(1.0, t / 1.0)); return P(READY, pel=(0.3 * s, -0.2 * s - 0.02, 0.0), proll=0.35 * s, roll=0.2 * s, lean=0.2, fl=(FX + 0.3 * s, 0, 0.0), fr=(-FX + 0.3 * s, 0.1 * s, 0.0), gun=LOW)
def c_cheer(t): return P(IDLEB, hl=(0.8, 0.9, 0.2, 1.0), hr=(0.8, 0.9, 0.2, 1.0), el=(0.8, 0.0, 0.0), er=(0.8, 0.0, 0.0), pel=(0, -0.012 + 0.03 * abs(sn(t, 1.0)), 0), lean=-0.08, nod=-0.1, fl=(FX + 0.06, 0, 0), fr=(-FX - 0.06, 0, 0))
def c_fistbump(t):
    k = ramp(t, 0.3, 1.0) * (1 - ramp(t, 2.4, 3.2)); bump = 0.05 * math.sin(2 * math.pi * 2 * t) * (1 if 1.0 < t < 2.2 else 0); return lerp_pose(IDLE, P(IDLE, hr=(-0.15, 0.1, 1.0 + bump, 1.0), er=(0.6, -0.5, -0.5), turn=-0.3, twist=0.2, lean=0.05, fl=(FX + 0.04, 0, 0.1), fr=(-FX - 0.03, 0, -0.1)), k)
def c_fall(t):
    wob = sn(t, 0.45); return P(IDLEB, hl=(0.95, 0.15, 0.1, 1.0), hr=(0.95, 0.15, 0.1, 1.0), el=(0.5, -0.2, -1.0), er=(0.5, -0.2, -1.0), pel=(0, -0.2, 0), lean=-0.25 + 0.05 * wob, nod=-0.4, fl=(FX + 0.15, 0.25, -0.35 + 0.1 * wob), fr=(-FX - 0.15, 0.1, -0.5 - 0.1 * wob), ppitch=0.1, roll=0.12 * wob)
def c_land(t):
    k = [(0.0, c_fall(0)), (0.25, P(IDLEB, gun=BACK, pel=(0, -0.55, 0.1), lean=0.7, fl=(FX, 0, 0.3), fr=(-FX, -0.28, -0.3), frp=0.9, hl=(0.2, -0.6, 0.5, 0.6), hr=(0.1, -0.9, 0.55, 0.8), er=(0.5, -0.5, 0.5), nod=0.3)), (1.1, P(IDLEB, gun=BACK, pel=(0, -0.55, 0.1), lean=0.7, fl=(FX, 0, 0.3), fr=(-FX, -0.28, -0.3), frp=0.9, hl=(0.2, -0.6, 0.5, 0.6), hr=(0.1, -0.9, 0.55, 0.8), er=(0.5, -0.5, 0.5), nod=0.3)),
         (2.2, P(IDLEB, gun=BACK, pel=(0, -0.2, 0.05), lean=0.2, fl=(FX, 0, 0.1), fr=(-FX, 0, -0.1), nod=0.1)), (3.0, c_ready(0))]
    return kf(k, t)
def c_jump(t):
    k = [(0.0, c_ready(0)), (0.35, P(IDLEB, gun=BACK, pel=(0, -0.35, 0.05), lean=0.35, fl=(FX, 0, 0.15), fr=(-FX, 0, -0.1), hl=(0.3, -0.3, -0.6, 0.9), hr=(0.3, -0.3, -0.6, 0.9), el=(0.5, -0.2, -1.0), er=(0.5, -0.2, -1.0))), (0.7, P(IDLEB, gun=BACK, pel=(0, 0.04, 0.05), lean=-0.1, fl=(FX, 0.25, 0.3), fr=(-FX, 0.05, -0.1), hl=(0.8, 0.8, 0.4, 1.0), hr=(0.8, 0.8, 0.4, 1.0), el=(0.8, 0, 0), er=(0.8, 0, 0))),
         (1.6, c_fall(0)), (4.0, c_fall(0))]
    return kf(k, t)
def c_brace(t): return P(READY, gun=BACK, gl=0.0, gr=0.0, hl=(0.3, 0.3, 0.9, 0.8), hr=(0.3, 0.3, 0.9, 0.8), el=(0.7, 0.3, -0.3), er=(0.7, 0.3, -0.3), lean=0.2, pel=(0, -0.1, 0.0), fl=(FX + 0.1, 0, 0.2), fr=(-FX - 0.1, 0, -0.2), nod=0.2)
# Slone (no rifle): commands from her hatch
def c_smug(t): return P(IDLE, hl=(-0.55, 0.0, 0.8, 0.8), hr=(-0.55, 0.0, 0.8, 0.8), el=(0.8, -0.6, 0.3), er=(0.8, -0.6, 0.3), tilt=0.12 * sn(t, 0.2), nod=-0.1, turn=0.2 * sn(t, 0.13), lean=-0.05, pel=(0.015 * sn(t, 0.1), -0.012, 0), fl=(FX + 0.05, 0, 0.0), fr=(-FX - 0.05, 0, 0.0))
def c_stalk(t):
    a = sn(t, 0.5); return P(IDLE, hl=(0.2, -0.9, 0.1, 0.95), hr=(0.25 + 0.1 * a, 0.2 + 0.2 * sn(t, 0.7), 0.85, 0.95), er=(0.8, -0.4, -0.2), turn=0.18 * sn(t, 0.2), nod=0.06 * sn(t, 0.6), lean=0.06, tilt=0.05 * sn(t, 0.25), pel=(0, -0.012, 0), fl=(FX + 0.05, 0, 0.05), fr=(-FX - 0.02, 0, -0.1))
def c_panic(t):
    a = sn(t, 2.2); return P(IDLE, hl=(0.5 + 0.2 * a, 0.7, 0.45, 0.9), hr=(0.5 - 0.2 * a, 0.7, 0.45, 0.9), el=(1.0, 0.0, -0.3), er=(1.0, 0.0, -0.3), lean=-0.2, nod=-0.25, turn=0.4 * sn(t, 1.3), pel=(0.02 * a, -0.08, -0.06), fl=(FX + 0.08, 0, 0.12 * a), fr=(-FX - 0.08, 0, -0.12 * a))
def c_crushed(t):
    k = ramp(t, 0.1, 1.2); return lerp_pose(c_panic(t), P(IDLE, hl=(0.3, -0.6, 0.6, 0.7), hr=(0.3, -0.6, 0.6, 0.7), el=(1.0, -0.2, 0.0), er=(1.0, -0.2, 0.0), lean=1.0, pel=(0, -0.65, 0.2), fl=(FX, 0, 0.3), fr=(-FX, -0.3, -0.3), frp=0.7, nod=0.5, ppitch=0.3), k)
def c_death(t):
    ko = P(READY, gun=BACK, gl=0.0, gr=0.0, lean=-0.4, nod=-0.3, pel=(0, -0.08, -0.12), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.25), hl=(0.9, 0.3, 0.2, 0.95), hr=(0.9, 0.3, 0.2, 0.95), el=(1, 0, 0), er=(1, 0, 0))
    kn = P(ko, pel=(0, -0.5, -0.3), lean=-0.6, ppitch=-0.5, fl=(FX, 0.0, 0.25), fr=(-FX, 0.15, -0.1), hl=(0.8, 0.2, 0.5, 0.9), hr=(0.8, 0.2, 0.5, 0.9), nod=-0.4)
    dn = P(ko, pel=(0, -0.8, -0.9), ppitch=-1.4, lean=-0.1, fl=(FX, 0.1, 0.5), fr=(-FX, 0.1, 0.4), hl=(1.0, -0.2, 0.2, 0.95), hr=(1.0, -0.2, 0.2, 0.95), nod=-0.2, roll=0.0)
    return kf([(0.0, c_ready(0)), (0.18, ko), (1.0, kn), (2.0, dn), (12.0, dn)], t)
def c_fallbump(side):
    def f(t):
        k = ramp(t, 0.3, 3.0); p = c_fall(t); h_in = (1.0, 0.0, 0.0, 1.0); h_out = (0.5, -0.3, -0.9, 0.9)
        return lerp_pose(p, dict(p, **{('hr' if side == 'r' else 'hl'): h_in, ('hl' if side == 'r' else 'hr'): h_out, ('er' if side == 'r' else 'el'): (0.6, -0.2, -0.5)}), k)
    return f
CLIPS = {'Idle': c_idle, 'Ready': c_ready, 'Alert': c_alert, 'Aim': c_aim, 'Fire': c_fire, 'FireK': c_firek, 'Hold': c_hold, 'Crouch': c_crouch, 'Peek': c_peek, 'Kneel': c_kneel, 'Reload': c_reload, 'Walk': c_walk, 'Run': c_run, 'WalkS': c_walks, 'Climb': c_climb,
         'Beckon': c_beckon, 'Point': c_point, 'Look': c_look, 'Wake': c_wake, 'Shove': c_shove, 'Throw': c_throw, 'Talk': c_talk, 'Plead': c_plead, 'Shrug': c_shrug, 'Hit': c_hit, 'Stagger': c_stagger, 'Dodge': c_dodge, 'Cheer': c_cheer, 'Fistbump': c_fistbump,
         'Fall': c_fall, 'Land': c_land, 'Jump': c_jump, 'Brace': c_brace, 'Smug': c_smug, 'STalk': c_stalk, 'Panic': c_panic, 'Crushed': c_crushed, 'Death': c_death, 'FallBumpR': c_fallbump('r'), 'FallBumpL': c_fallbump('l')}

# ================= creature clip sets (CLIPSET=care for the caretaker, CLIPSET=queen for the Cube Queen); arms are free (no rifle)
FREE = P(IDLE)
def arms(hl, hr, el=(0.6, -0.5, -0.2), er=(0.6, -0.5, -0.2)): return dict(hl=hl, hr=hr, el=el, er=er)
def csway(p, t, a=1.0): return sway(p, t, a)
def cc_idle(t): return csway(add(P(FREE, **arms((0.25 + 0.05 * sn(t, 0.3), -1.0, 0.2 * sn(t, 0.2), 0.97), (0.25 - 0.05 * sn(t, 0.3), -1.0, -0.2 * sn(t, 0.2), 0.97))), lean=0.1, nod=0.12 + 0.05 * sn(t, 0.4), turn=0.35 * sn(t, 0.13)), t, 1.5)
def cc_walk(t): p = gait_free(t, 1.35, 2.4, 0.62, 0.14, 0.05, 0.14, 0.75); p['nod'] = 0.12 + 0.06 * sn(t, 0.74); p['turn'] = 0.2 * sn(t, 0.37); return p
def cc_run(t): p = gait_free(t, 0.9, 4.6, 0.42, 0.24, 0.07, 0.34, 1.4, True); p['nod'] = 0.25; return p
def cc_attack(t):
    wind = P(FREE, **arms((0.3, 0.85, 0.4, 1.0), (0.3, 0.85, 0.4, 1.0), (0.5, 0.6, -0.5), (0.5, 0.6, -0.5)), lean=-0.35, nod=-0.2, pel=(0, -0.03, -0.05))
    hit = P(FREE, **arms((0.15, -0.6, 0.85, 1.0), (0.15, -0.6, 0.85, 1.0), (0.4, -0.8, 0.0), (0.4, -0.8, 0.0)), lean=0.7, nod=0.3, pel=(0, -0.1, 0.12), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.2))
    return kf([(0.0, cc_idle(0)), (0.7, wind), (1.0, wind), (1.25, hit), (1.9, hit), (2.8, cc_idle(0))], t % 3.0 if t < 3.0 else cc_idle(0) and 0.0)
def cc_slam(t):
    up = P(FREE, **arms((0.15, 1.0, 0.0, 1.0), (0.15, 1.0, 0.0, 1.0), (0.5, 0.8, -0.3), (0.5, 0.8, -0.3)), lean=-0.5, nod=-0.4, pel=(0, 0.02, -0.08), fl=(FX + 0.1, 0, 0.0), fr=(-FX - 0.1, 0, -0.1))
    dn = P(FREE, **arms((0.1, -0.7, 0.8, 1.0), (0.1, -0.7, 0.8, 1.0), (0.4, -0.8, 0.0), (0.4, -0.8, 0.0)), lean=0.8, nod=0.4, pel=(0, -0.2, 0.15), fl=(FX, 0, 0.35), fr=(-FX, 0, 0.2))
    return kf([(0.0, cc_idle(0)), (1.2, up), (1.6, up), (1.85, dn), (3.0, dn), (4.5, cc_idle(0))], t)
def cc_roar(t):
    r = P(FREE, **arms((0.9, 0.4, 0.2, 1.0), (0.9, 0.4, 0.2, 1.0), (0.8, 0.0, -0.1), (0.8, 0.0, -0.1)), lean=-0.4, nod=-0.55, tilt=0.0, pel=(0, 0.01, 0), fl=(FX + 0.1, 0, 0.05), fr=(-FX - 0.1, 0, -0.05))
    return kf([(0.0, cc_idle(0)), (0.8, r), (3.2, add(r, nod=-0.1, lean=0.08)), (4.2, cc_idle(0))], t)
def cc_stagger(t): return kf([(0.0, cc_idle(0)), (0.15, P(FREE, **arms((0.8, 0.3, 0.2, 0.9), (0.8, 0.3, 0.2, 0.9)), lean=-0.5, nod=-0.4, pel=(0, -0.06, -0.15), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.35))), (1.6, cc_idle(0))], t)
def cc_death(t):
    k = P(FREE, **arms((0.9, 0.2, 0.3, 0.95), (0.9, 0.2, 0.3, 0.95)), lean=-0.5, nod=-0.4, pel=(0, -0.3, -0.25), fl=(FX, 0, 0.3), fr=(-FX, 0.1, -0.2))
    dn = P(FREE, **arms((1.0, -0.2, 0.3, 1.0), (1.0, -0.2, 0.3, 1.0)), lean=0.2, ppitch=-1.4, pel=(0, -0.95, -0.85), fl=(FX, 0.2, 0.5), fr=(-FX, 0.2, 0.4), nod=-0.2)
    return kf([(0.0, cc_idle(0)), (0.3, k), (1.4, dn), (12.0, dn)], t)
def cc_cast(t): return kf([(0.0, cc_idle(0)), (0.8, P(FREE, **arms((0.25, -1.0, 0.1, 0.97), (0.1, 0.9, 0.5, 1.0)), lean=-0.15, nod=-0.2, turn=-0.2)), (4.0, P(FREE, **arms((0.25, -1.0, 0.1, 0.97), (0.1, 0.9, 0.5, 1.0)), lean=-0.2, nod=-0.25, turn=-0.2)), (5.0, cc_idle(0))], t)
CARE = {'Idle': cc_idle, 'Walk': cc_walk, 'Run': cc_run, 'Attack': cc_attack, 'Slam': cc_slam, 'Roar': cc_roar, 'Stagger': cc_stagger, 'Death': cc_death, 'Cast': cc_cast}
# the Cube Queen hovers: feet hang a little, pelvis floats; everything slow and regal
def hov(p, t, lift=0.25): return add(p, pel=(0, lift + 0.05 * sn(t, 0.25), 0), fl=(0, 0.2 + 0.05 * sn(t, 0.3), -0.05), fr=(0, 0.1 + 0.05 * sn(t, 0.3, 0.4), -0.1))
def qq_float(t): return hov(csway(P(FREE, **arms((0.55 + 0.08 * sn(t, 0.2), -0.75, 0.15, 0.97), (0.55 - 0.08 * sn(t, 0.2), -0.75, 0.15, 0.97), (0.9, -0.2, -0.3), (0.9, -0.2, -0.3))), t, 1.0), t)
def qq_cast(t):
    up = P(FREE, **arms((0.55, 0.8, 0.35, 1.0), (0.55, 0.8, 0.35, 1.0), (0.9, 0.5, -0.4), (0.9, 0.5, -0.4)), lean=-0.15, nod=-0.35)
    return hov(kf([(0.0, qq_float(0)), (1.2, up), (5.0, add(up, lean=-0.05)), (6.5, qq_float(0))], t), t)
def qq_point(t):
    pt = P(FREE, **arms((0.25, -0.9, 0.2, 0.97), (0.05, 0.1, 1.0, 1.0), (0.9, -0.3, -0.2), (0.3, -0.2, 0.0)), turn=-0.2, twist=-0.15, lean=0.06)
    return hov(kf([(0.0, qq_float(0)), (0.8, pt), (3.0, pt), (4.0, qq_float(0))], t), t)
def qq_taunt(t):
    w = P(FREE, **arms((1.0, 0.1, 0.1, 1.0), (1.0, 0.1, 0.1, 1.0), (0.9, 0.0, 0.0), (0.9, 0.0, 0.0)), nod=-0.3, lean=-0.1, tilt=0.12)
    return hov(kf([(0.0, qq_float(0)), (1.0, w), (4.0, add(w, tilt=-0.2)), (5.0, qq_float(0))], t), t)
def qq_rage(t):
    w = P(FREE, **arms((0.6, 0.9, 0.3, 1.0), (0.6, 0.9, 0.3, 1.0), (0.8, 0.5, -0.3), (0.8, 0.5, -0.3)), lean=-0.5, nod=-0.5, roll=0.1 * sn(t, 1.5))
    return hov(kf([(0.0, qq_float(0)), (0.5, w), (4.0, w), (5.0, qq_float(0))], t), t, 0.4)
def qq_hit(t): return hov(kf([(0.0, qq_float(0)), (0.12, P(FREE, **arms((0.9, 0.2, 0.3, 0.9), (0.9, 0.2, 0.3, 0.9)), lean=-0.45, nod=-0.3, roll=0.2)), (1.8, qq_float(0))], t), t)
def qq_retreat(t):
    c = P(FREE, **arms((0.2, 0.5, 0.8, 0.9), (0.2, 0.5, 0.8, 0.9), (0.6, 0.3, 0.2), (0.6, 0.3, 0.2)), lean=0.2, nod=0.2, turn=0.5)
    return hov(kf([(0.0, qq_float(0)), (1.0, c), (3.0, add(c, pel=(0, 0.6, 0)))], t), t)
def qq_teleport(t):
    x = P(FREE, **arms((0.15, 0.4, 0.9, 0.9), (0.15, 0.4, 0.9, 0.9), (0.5, 0.1, 0.6), (0.5, 0.1, 0.6)), nod=-0.1)
    return hov(kf([(0.0, qq_float(0)), (1.5, x), (3.0, add(x, pel=(0, 0.2, 0)))], t), t)
QUEEN = {'Float': qq_float, 'Cast': qq_cast, 'Point': qq_point, 'Taunt': qq_taunt, 'Rage': qq_rage, 'Hit': qq_hit, 'Retreat': qq_retreat, 'Teleport': qq_teleport}
_cs = os.environ.get('CLIPSET', '')
if _cs == 'care': CLIPS = CARE
elif _cs == 'queen': CLIPS = QUEEN
CHAN = [('pelvis', 'rotation'), ('pelvis', 'translation')] + [(k, 'rotation') for k in SPINE] + [(k, 'rotation') for k in ('neck', 'head', 'ual', 'lal', 'uar', 'lar', 'tl', 'cl', 'tr', 'cr', 'fl', 'fr')]
def add_accessor(arr, typ, minmax=False):
    arr = np.ascontiguousarray(arr, np.float32); off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(arr.tobytes()); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}); a = {'bufferView': len(g['bufferViews']) - 1, 'componentType': 5126, 'count': len(arr), 'type': typ}
    if minmax: a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
    g['accessors'].append(a); return len(g['accessors']) - 1
def run(only=None, report=True):
    T = np.arange(NF + 1) / FPS; tin = add_accessor(T, 'SCALAR', minmax=True); g['animations'] = []; gun_scale = None
    for name, fn in CLIPS.items():
        if only and name not in only: continue
        tracks = {c: [] for c in CHAN}; gr = []; gt = []
        for f in range(NF + 1):
            t = f / FPS; Wg = solve(fn(t))
            for (k, path) in CHAN: tracks[(k, path)].append(fk.R[B[k]].copy() if path == 'rotation' else fk.T[B[k]].copy())
            M = np.linalg.inv(fk.world(par[GUN])) @ Wg; sc = np.linalg.norm(M[:3, :3], axis=0); gun_scale = sc; gr.append(m2q(M[:3, :3] / sc)); gt.append(M[:3, 3].copy())
        samplers = []; channels = []
        for (k, path), arr in list(tracks.items()) + [(('gun', 'rotation'), gr), (('gun', 'translation'), gt)]:
            arr = np.array(arr); node = GUN if k == 'gun' else B[k]
            if path == 'rotation':
                for i in range(1, len(arr)):
                    if float(arr[i] @ arr[i - 1]) < 0: arr[i] = -arr[i]
            out = add_accessor(arr, 'VEC4' if path == 'rotation' else 'VEC3'); samplers.append({'input': tin, 'output': out, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': path}})
        g['animations'].append({'name': 'H_' + name, 'samplers': samplers, 'channels': channels})
    N[GUN]['scale'] = [float(x) for x in gun_scale]; N[GUN]['translation'] = [0.0, 0.0, 0.0]; N[GUN]['rotation'] = [0.0, 0.0, 0.0, 1.0]
    g['buffers'][0]['byteLength'] = len(b); g2, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g2, nb); print(dst, os.path.getsize(dst) // 1024, 'KB', len(g['animations']), 'clips')
if __name__ == '__main__':
    run(os.environ.get('ONLY', '').split(',') if os.environ.get('ONLY') else None)
