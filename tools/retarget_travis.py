"""Retarget the Fortnite event animations (334-joint 'Cyclone' rig file) onto the Travis skin (275-joint rig) and write a new GLB.

Both rigs are the same character with the same T-pose bind pose, but different local bone axes and a ~10000x unit difference,
so local rotations cannot be copied. For every mapped bone we take its rotation-from-bind in mesh space,
    P = rot(Gmesh)^T * rot(Gjoint(t)) * rot(IBM)
and rebuild the skin's joint rotations from the same P. Pelvis translation is carried over in mesh space and scaled by the unit ratio.
Usage: python -I retarget_travis.py <skin.glb> <anim.glb> <out.glb>   (RT_DEBUG=1 prints bind-pose FK diagnostics and exits)"""
import sys, os, math, json, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glbmath import *

SKIN, ANIM, OUT = sys.argv[1:4]
from clip_specs import SPECS, rot as _rot
I3 = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]

def base(name, strip):
    t = name.split('_')
    for _ in range(strip):
        if len(t) > 1 and t[-1].isdigit(): t = t[:-1]
    return '_'.join(t)

def col_scale(M):
    return math.sqrt(sum(M[r][0] ** 2 for r in range(3)))

sg, sb = load_glb(SKIN); ag, ab = load_glb(ANIM)
s_order, s_par = order_of(sg); a_order, a_par = order_of(ag)
ssk = sg['skins'][0]; ask = ag['skins'][0]
s_ibm = [from_colmajor(m) for m in read_acc(sg, sb, ssk['inverseBindMatrices'])]
a_ibm = [from_colmajor(m) for m in read_acc(ag, ab, ask['inverseBindMatrices'])]
s_joint_index = {ji: k for k, ji in enumerate(ssk['joints'])}
a_joint_index = {ji: k for k, ji in enumerate(ask['joints'])}

# ---- name mapping (skin names carry two numeric suffixes, animation names one) ----
s_name = {}; a_name = {}
for ji in ssk['joints']: s_name.setdefault(base(sg['nodes'][ji]['name'], 2), ji)
for ji in ask['joints']: a_name.setdefault(base(ag['nodes'][ji]['name'], 1), ji)
mapping = {s_name[k]: a_name[k] for k in s_name if k in a_name}   # skin node -> anim node
print("skin joints %d, anim joints %d, mapped %d" % (len(ssk['joints']), len(ask['joints']), len(mapping)))
print("unmapped skin joints (follow parent):", [k for k in s_name if k not in a_name])
for need in ('pelvis', 'head', 'hand_l', 'foot_l', 'upperarm_l'):
    if need not in s_name or need not in a_name: raise SystemExit("missing key bone " + need)

def global_rest(g, order, par):
    G = {}
    for i in order:
        L = node_local(g['nodes'][i]); G[i] = mmul(G[par[i]], L) if par[i] >= 0 else L
    return G
s_G0 = global_rest(sg, s_order, s_par); a_G0 = global_rest(ag, a_order, a_par)
s_mesh = [i for i, n in enumerate(sg['nodes']) if 'skin' in n][0]; a_mesh = [i for i, n in enumerate(ag['nodes']) if 'skin' in n][0]
# NOTE: the exporters' inverse bind matrices already cancel the root-chain rotation (G*IBM at bind is a pure uniform scale), so the net mesh rotation is identity.
s_Rm = I3; a_Rm = I3

# unit factor: bind-pose skinning matrix G*IBM is f * rotation(Gmesh) (f = chain scale * IBM scale); mesh-space = final / f
s_f = col_scale(s_G0[s_mesh]) * col_scale(s_ibm[s_joint_index[s_name['pelvis']]]); a_f = col_scale(a_G0[a_mesh]) * col_scale(a_ibm[a_joint_index[a_name['pelvis']]])
print("unit factors: skin f=%.4g anim f=%.4g" % (s_f, a_f))

def bind_pos(ibm):
    W = inv4(ibm); return [W[r][3] for r in range(3)]
def apply_pt(M, p): return [sum(M[r][c] * (list(p) + [1.0])[c] for c in range(4)) for r in range(3)]
def to_mesh(Rm, f, gp): return [sum(Rm[c][r] * gp[c] for c in range(3)) / f for r in range(3)]   # Rm^T * gp / f
def to_global(Rm, f, mp): return [sum(Rm[r][c] * mp[c] for c in range(3)) * f for r in range(3)]

sb_pelvis = bind_pos(s_ibm[s_joint_index[s_name['pelvis']]]); ab_pelvis = bind_pos(a_ibm[a_joint_index[a_name['pelvis']]])
sb_head = bind_pos(s_ibm[s_joint_index[s_name['head']]]); ab_head = bind_pos(a_ibm[a_joint_index[a_name['head']]])
K = math.dist(sb_pelvis, sb_head) / math.dist(ab_pelvis, ab_head)
print("mesh-space unit ratio skin/anim (pelvis-head) = %.3f" % K)

s_IBMrot = {ji: rot_of(s_ibm[k]) for ji, k in s_joint_index.items()}
a_IBMrot = {ji: rot_of(a_ibm[k]) for ji, k in a_joint_index.items()}
s_is_joint = set(ssk['joints'])
s_joint_list = [i for i in s_order if i in s_is_joint]          # parents before children
# unmapped joints above mapped ones (the exporter's root joints) carry the Z-up->Y-up frame change: keep their static rest rotation
static_joints = set()
_p = s_par[s_name['pelvis']]
while _p in s_is_joint:      # every joint above the pelvis
    static_joints.add(_p); _p = s_par[_p]
def make_targets(P):
    return {sj: (rot_of(s_G0[sj]) if sj in static_joints else m3mul(m3mul(s_Rm, P[sj]), m3T(s_IBMrot[sj]))) for sj in s_joint_list}

def static_parent_rot(i):
    return rot_of(s_G0[s_par[i]]) if s_par[i] >= 0 else I3

def skin_fk(rot_target, pelvis_global=None):
    """Forward kinematics of the skin: rot_target[j] = desired global rotation. Returns (SG, local quats, pelvis local translation)."""
    SG = {}; Rg = {}; quats = {}; pel_local = None
    for sj in s_joint_list:
        par = s_par[sj]
        Rpar = Rg[par] if par in Rg else static_parent_rot(sj)
        q = m3_to_quat(m3mul(m3T(Rpar), rot_target[sj])); quats[sj] = q; Rg[sj] = rot_target[sj]
        n = sg['nodes'][sj]; T = n.get('translation', [0, 0, 0])
        Gpar = SG[par] if par in SG else s_G0[par]
        if pelvis_global is not None and sj == s_name['pelvis']:
            T = apply_pt(inv4(Gpar), pelvis_global); pel_local = tuple(T)
        SG[sj] = mmul(Gpar, trs_to_m4(T, q, n.get('scale', [1, 1, 1])))
    return SG, quats, pel_local

if os.environ.get('RT_DEBUG'):
    SG, _, _ = skin_fk(make_targets({sj: I3 for sj in s_joint_list}))
    height = math.dist(sb_pelvis, sb_head) * 2.9      # pelvis-head is ~0.35 of body height
    errs = []
    for sj in s_joint_list:
        b = bind_pos(s_ibm[s_joint_index[sj]]); p = to_mesh(s_Rm, s_f, [SG[sj][r][3] for r in range(3)])
        errs.append((math.dist(p, b) / height, sg['nodes'][sj]['name']))
    for nm in ('pelvis', 'spine_01', 'head', 'hand_l'):
        sj = s_name[nm]; print('DEBUG', nm, 'fk', [round(x) for x in to_mesh(s_Rm, s_f, [SG[sj][r][3] for r in range(3)])], 'bind', [round(x) for x in bind_pos(s_ibm[s_joint_index[sj]])], 'static_joints', len(static_joints))
    errs.sort(reverse=True)
    print("DEBUG bind-pose FK error (fraction of body height): median %.5f p90 %.5f worst %s" % (errs[len(errs) // 2][0], errs[int(len(errs) * 0.9)][0], [(round(e, 4), n) for e, n in errs[:5]]))
    sys.exit(0)

# ---- animation sampling (anim file) ----
def anim_tracks(g, b, clip):
    tr = {}; cache = {}
    for ch in clip['channels']:
        n = ch['target'].get('node'); p = ch['target']['path']
        if n is None or p not in ('translation', 'rotation', 'scale'): continue
        s = clip['samplers'][ch['sampler']]
        if s['input'] not in cache: cache[s['input']] = [x[0] for x in read_acc(g, b, s['input'])]
        tr.setdefault(n, {})[p] = (cache[s['input']], read_acc(g, b, s['output']))
    return tr

def sample(track, t, is_rot):
    ts, vs = track
    if t <= ts[0]: return vs[0]
    if t >= ts[-1]: return vs[-1]
    lo, hi = 0, len(ts) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if ts[mid] <= t: lo = mid
        else: hi = mid
    f = (t - ts[lo]) / (ts[hi] - ts[lo])
    if is_rot: return slerp(tuple(vs[lo]), tuple(vs[hi]), f)
    return tuple(x + (y - x) * f for x, y in zip(vs[lo], vs[hi]))

def anim_globals(t, tracks):
    G = {}
    for i in a_order:
        n = ag['nodes'][i]
        if i in tracks and 'matrix' not in n:
            tk = tracks[i]
            T = sample(tk['translation'], t, False) if 'translation' in tk else n.get('translation', [0, 0, 0])
            Q = sample(tk['rotation'], t, True) if 'rotation' in tk else n.get('rotation', [0, 0, 0, 1])
            S = sample(tk['scale'], t, False) if 'scale' in tk else n.get('scale', [1, 1, 1])
            L = trs_to_m4(T, Q, S)
        else: L = node_local(n)
        G[i] = mmul(G[a_par[i]], L) if a_par[i] >= 0 else L
    return G

children = {}
for _sj in s_joint_list: children.setdefault(s_par[_sj], []).append(_sj)
def _sub(n):
    out = [n]
    for c in children.get(n, []): out += _sub(c)
    return out
subtree = {sj: _sub(sj) for sj in s_joint_list}
BODY_H = math.dist(sb_pelvis, sb_head) * 2.9      # pelvis-head is ~0.35 of body height
for _need in ('clavicle_l', 'clavicle_r', 'upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r', 'thigh_l', 'thigh_r', 'calf_l', 'calf_r', 'spine_01', 'spine_05', 'neck_01'):
    if _need not in s_name: raise SystemExit('missing bone for clip specs: ' + _need)

# ---- arm / torso collision avoidance ----
def build_torso_profile():
    torso_names = {'pelvis', 'spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05'}
    node_name = {ji: base(sg['nodes'][ji]['name'], 2) for ji in ssk['joints']}
    pts = []
    for mesh in sg['meshes']:
        at = mesh['primitives'][0]['attributes']
        pos = read_acc(sg, sb, at['POSITION']); jo = read_acc(sg, sb, at['JOINTS_0']); we = read_acc(sg, sb, at['WEIGHTS_0'])
        for v in range(len(pos)):
            best = max(range(4), key=lambda k: we[v][k])
            if we[v][best] > 0.6 and node_name[ssk['joints'][int(jo[v][best])]] in torso_names: pts.append(pos[v])
    y0 = sb_pelvis[1]; y1 = bind_pos(s_ibm[s_joint_index[s_name['neck_01']]])[1]
    bands = []
    for b_ in range(10):
        lo = y0 + (y1 - y0) * b_ / 10; hi = y0 + (y1 - y0) * (b_ + 1) / 10
        sel = [p for p in pts if lo <= p[1] < hi]
        if not sel: bands.append(bands[-1] if bands else (1.0, 0.0, 1.0)); continue
        zmin = min(p[2] for p in sel); zmax = max(p[2] for p in sel)
        bands.append((max(abs(p[0]) for p in sel), (zmin + zmax) / 2 - sb_pelvis[2], (zmax - zmin) / 2))
    return y0, y1, bands
T_Y0, T_Y1, T_BANDS = build_torso_profile()
print('torso profile (half-width, centre z, half-depth) per height band:', [tuple(round(x) for x in b) for b in T_BANDS][::3])
def _vsub(a, b): return [a[i] - b[i] for i in range(3)]
def _dot(a, b): return sum(x * y for x, y in zip(a, b))
def _norm(a):
    l = math.sqrt(_dot(a, a)) or 1.0; return [x / l for x in a]
def _cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def _mpos(SGq, name): return to_mesh(s_Rm, s_f, [SGq[s_name[name]][r][3] for r in range(3)])
def torso_frame(SGq):
    pel = _mpos(SGq, 'pelvis'); neck = _mpos(SGq, 'neck_01')
    up = _norm(_vsub(neck, pel)); side = _vsub(_mpos(SGq, 'upperarm_l'), _mpos(SGq, 'upperarm_r'))
    d = _dot(side, up); side = _norm([side[i] - up[i] * d for i in range(3)])
    return pel, side, up, _cross(side, up)
def arm_hits(SGq, sd, frame):
    pel, side, up, fwd = frame; margin = 0.02 * BODY_H; hit = False
    pts = [_mpos(SGq, 'lowerarm_' + sd), _mpos(SGq, 'hand_' + sd)]
    pts.append([(pts[0][i] + pts[1][i]) / 2 for i in range(3)])
    if ('middle_01_' + sd) in s_name: pts.append(_mpos(SGq, 'middle_01_' + sd))
    for p in pts:
        d = _vsub(p, pel); x, y, z = _dot(d, side), _dot(d, up), _dot(d, fwd)
        h = y / (T_Y1 - T_Y0)
        if h < 0.0 or h > 1.05: continue
        a, cz, b = T_BANDS[min(9, int(h * 10))]
        if (x / (a + margin)) ** 2 + ((z - cz) / (b + margin)) ** 2 < 1.0: hit = True
    return hit
def rot_about(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); x, y, z = axis; C = 1 - c
    return [[c + x * x * C, x * y * C - z * s, x * z * C + y * s], [y * x * C + z * s, c + y * y * C, y * z * C - x * s], [z * x * C - y * s, z * y * C + x * s, c + z * z * C]]
RES = {'frames': 0, 'fixed': 0, 'deg': 0.0}
def resolve_arms(P, pm):
    """Swing an upper arm outward (about the torso's forward axis, 3 deg at a time, max 45) until elbow/forearm/hand are outside the torso."""
    for it in range(15):
        SGq, _, _ = skin_fk(make_targets(P), to_global(s_Rm, s_f, pm)); fr = torso_frame(SGq)
        hits = {sd: arm_hits(SGq, sd, fr) for sd in 'lr'}
        if it == 0: RES['frames'] += 1; RES['fixed'] += 1 if (hits['l'] or hits['r']) else 0
        if not hits['l'] and not hits['r']: return
        for sd in 'lr':
            if hits[sd]:
                D = rot_about(fr[3], 3.0 if sd == 'l' else -3.0); RES['deg'] += 3.0
                for sj in subtree[s_name['upperarm_' + sd]]: P[sj] = m3mul(D, P[sj])
REF = {}   # facing of the first clip (the dance); every other clip is yawed to match
def retarget_clip(clip, spec):
    tracks = anim_tracks(ag, ab, clip)
    times = None
    for tk in tracks.values():
        for v in tk.values():
            if times is None or len(v[0]) > len(times): times = v[0]
    base_dur = times[-1]
    if spec.get('dur'):                      # procedural clip: resample the base pose at 30 fps over the requested length
        times = [k / 30.0 for k in range(int(spec['dur'] * 30) + 1)]
    samp = (lambda t: t % base_dur) if spec.get('dur') else (lambda t: t)
    print('  clip %s: %d animated nodes, %d keyframes, %.2fs' % (spec['name'], len(tracks), len(times), times[-1]))
    out_rot = {i: [] for i in s_joint_list}; out_pel = []; prevq = {}; errs = []
    a_pelvis = a_name['pelvis']
    AGS = [anim_globals(samp(t), tracks) for t in times]
    pas = [to_mesh(a_Rm, a_f, [AG[a_pelvis][r][3] for r in range(3)]) for AG in AGS]
    dtf = (times[-1] - times[0]) / max(1, len(times) - 1); W = max(1, int(1.2 / dtf))
    base_xz = []
    for i in range(len(times)):
        lo = max(0, i - W); hi = min(len(times), i + W + 1); n_ = hi - lo
        base_xz.append((sum(pas[k][0] for k in range(lo, hi)) / n_, sum(pas[k][2] for k in range(lo, hi)) / n_))   # slow travel; removed so he dances in place
    def pose_P(AG):
        Pq = {}
        for sj in s_joint_list:
            if sj in mapping: Pq[sj] = m3mul(m3mul(m3T(a_Rm), rot_of(AG[mapping[sj]])), a_IBMrot[mapping[sj]])
            else: Pq[sj] = Pq[s_par[sj]] if s_par[sj] in Pq else I3
        return Pq
    def facing_of(Pq):   # yaw of the hip line (left hip -> right hip) in mesh space
        SGq, _, _ = skin_fk(make_targets(Pq), to_global(s_Rm, s_f, list(sb_pelvis)))
        hl = to_mesh(s_Rm, s_f, [SGq[s_name['thigh_l']][r][3] for r in range(3)]); hr = to_mesh(s_Rm, s_f, [SGq[s_name['thigh_r']][r][3] for r in range(3)])
        return math.atan2(-(hl[2] - hr[2]), hl[0] - hr[0])
    ks = [int(i * (len(times) - 1) / 9) for i in range(10)]
    ths = [facing_of(pose_P(AGS[k])) for k in ks]
    phi = math.atan2(sum(math.sin(x) for x in ths), sum(math.cos(x) for x in ths))    # circular mean facing of this clip
    REF['phi'] = -math.pi          # bind pose faces +Z; every clip is turned to face exactly -Z (toward the audience)
    Rcorr = _rot('y', math.degrees(REF['phi'] - phi))
    print('  mean facing %.1f deg, correction %.1f deg' % (math.degrees(phi), math.degrees(REF['phi'] - phi)))
    for ti, t in enumerate(times):
        AG = AGS[ti]
        P = {}
        for sj in s_joint_list:
            if sj in mapping:
                aj = mapping[sj]
                P[sj] = m3mul(m3mul(m3T(a_Rm), rot_of(AG[aj])), a_IBMrot[aj])
            else:
                P[sj] = P[s_par[sj]] if s_par[sj] in P else I3
        for sj in s_joint_list: P[sj] = m3mul(Rcorr, P[sj])
        off_dy = (0, 0, 0)
        if spec.get('fn'):
            offs, off_dy = spec['fn'](t, times[-1])
            for bone, D in offs.items():
                for sj in subtree[s_name[bone]]: P[sj] = m3mul(D, P[sj])
        pa = pas[ti]
        pm_orig = [sb_pelvis[r] + K * (pa[r] - ab_pelvis[r]) for r in range(3)]
        pm = [sb_pelvis[0] + K * (pa[0] - base_xz[ti][0]), pm_orig[1], sb_pelvis[2] + K * (pa[2] - base_xz[ti][1])]
        pm = [pm[r] + off_dy[r] * BODY_H for r in range(3)]
        pm_delta = [pm[r] - pm_orig[r] for r in range(3)]
        resolve_arms(P, pm)
        targets = make_targets(P)
        SG, quats, pel_local = skin_fk(targets, to_global(s_Rm, s_f, pm))
        for sj in s_joint_list:                    # keep quaternion hemisphere continuous
            q = quats[sj]
            if sj in prevq and sum(a * b for a, b in zip(q, prevq[sj])) < 0: q = tuple(-x for x in q)
            prevq[sj] = q; out_rot[sj].append(q)
        out_pel.append(pel_local)
        if ti % max(1, len(times) // 10) == 0 and not spec.get('fn'):
            for nm in ('hand_l', 'hand_r', 'foot_l', 'foot_r', 'head'):
                sp = to_mesh(s_Rm, s_f, [SG[s_name[nm]][r][3] for r in range(3)])
                ap = to_mesh(a_Rm, a_f, [AG[a_name[nm]][r][3] for r in range(3)])
                exp = [sb_pelvis[r] + K * (ap[r] - ab_pelvis[r]) + pm_delta[r] for r in range(3)]
                errs.append(math.dist(sp, exp) / (K * 1.67))
    n = len(times); N = max(2, int(0.6 / dtf))      # blend the last 0.6 s back into frame 0 so the clip loops seamlessly
    for i in range(n - N, n):
        w = (i - (n - N)) / (N - 1); w = w * w * (3 - 2 * w)
        for sj in s_joint_list: out_rot[sj][i] = slerp(out_rot[sj][i], out_rot[sj][0], w)
        out_pel[i] = tuple(a + (b - a) * w for a, b in zip(out_pel[i], out_pel[0]))
    if errs: print('  position error of hands/feet/head vs source animation (fraction of body height): mean %.4f max %.4f' % (sum(errs) / len(errs), max(errs)))
    return times, out_rot, out_pel

# ---- build output GLB ----
binb = bytearray(sb)
views = sg['bufferViews']; accs = sg['accessors']
def add_data(data, comp=5126, typ='SCALAR', count=0, mn=None, mx=None):
    while len(binb) % 4: binb.append(0)
    views.append({'buffer': 0, 'byteOffset': len(binb), 'byteLength': len(data)}); binb.extend(data)
    a = {'bufferView': len(views) - 1, 'componentType': comp, 'count': count, 'type': typ}
    if mn is not None: a['min'] = mn; a['max'] = mx
    accs.append(a); return len(accs) - 1

anims = []
for spec in SPECS:
    idx = spec['src']; name = spec['name']
    times, rots, pel = retarget_clip(ag['animations'][idx], spec)
    ti = add_data(struct.pack('<%df' % len(times), *times), 5126, 'SCALAR', len(times), [min(times)], [max(times)])
    samplers = []; channels = []
    for sj in s_joint_list:
        flat = [c for q in rots[sj] for c in q]
        ai = add_data(struct.pack('<%df' % len(flat), *flat), 5126, 'VEC4', len(times))
        samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': sj, 'path': 'rotation'}})
    flat = [c for v in pel for c in v]
    ai = add_data(struct.pack('<%df' % len(flat), *flat), 5126, 'VEC3', len(times))
    samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': s_name['pelvis'], 'path': 'translation'}})
    anims.append({'name': name, 'samplers': samplers, 'channels': channels})
sg['animations'] = anims
sg['buffers'][0]['byteLength'] = len(binb)
js = json.dumps(sg, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
while len(binb) % 4: binb.append(0)
glb = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binb)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(binb), 0x004E4942) + bytes(binb)
open(OUT, 'wb').write(glb)
print("arm/torso collision: %d frames checked, %d needed correction (%.0f deg total)" % (RES["frames"], RES["fixed"], RES["deg"]))
print("wrote", OUT, len(glb), "bytes;", [(a['name'], len(a['channels'])) for a in anims])

















