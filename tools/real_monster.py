"""python real_monster.py [bake|probe]  -> cache/showdown/monster.glb (+ monster_trace.json, monster_beam.json)
The REAL Fortnite event animations of the Devourer (cattus_-_event_animations_-_fortnite.glb: Act1A, Act1B, Act1_C, Act2, Act3_A, Act3_B) stitched into one clip that
runs against the supplied soundtrack (monster time = audio time + OFFSET), with the root path moved into the arena.
All maths are in the file's own frame (y up, bind faces +Z); the plugin mirrors X when it loads, so  unity = Sc * (-x, y, z) - centre."""
import os, sys, math, json, bisect
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import real_clip as rc
import rig as R
from rig import *
from array import array

HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'showdown'); os.makedirs(OUT, exist_ok=True)
SC = 65.0                           # metres per file unit (the body is ~1.6 units long: ~104 m)
OFFSET = 43.6                       # monster clip time = audio time + OFFSET (found by correlating the activity with the soundtrack)
FPS = 30.0
DUR = 303.5

# ------------------------------------------------------------------ 1. node rest pose := the bind pose (the loader sizes / centres the skeleton from the rest pose; the file rests in a lying pose)
_SG, _Q, _PL = skin_fk(make_targets({j: I3 for j in s_joint_list}), PEL_B)
for j in s_joint_list:
    if j not in static_joints: sg['nodes'][j]['rotation'] = [float(x) for x in _Q[j]]
sg['nodes'][PEL]['translation'] = [float(x) for x in _PL]
for _n, _t in (('ik_hand_root', (0.0, 0.0, 0.0)), ('ik_foot_l', (0.3, 0.0, 0.0)), ('ik_foot_r', (-0.3, 0.0, 0.0)), ('ik_hand_gun', (0.0, 0.0, 0.0)), ('ik_hand_l', (0.0, 0.0, 0.0)), ('ik_hand_r', (0.0, 0.0, 0.0))):      # unskinned IK helpers rest 5-10 units away from the body in the file: bring them home so they do not widen the loader's measured skeleton
    if _n in s_name: sg['nodes'][s_name[_n]]['translation'] = list(_t)
s_G0.update(R._global_rest(sg, s_order, s_par))
REST_Q = {j: tuple(sg['nodes'][j].get('rotation', [0, 0, 0, 1])) for j in s_joint_list}
REST_T = {j: tuple(sg['nodes'][j].get('translation', [0, 0, 0])) for j in s_joint_list}
REST_S = {j: tuple(sg['nodes'][j].get('scale', [1, 1, 1])) for j in s_joint_list}

# loader maths: joint bounds of the (mirrored) rest pose -> normalise to 1.8, then * HEIGHT / 1.8  => metres per file unit = HEIGHT / jb.size.y
_jx = []; _jy = []; _jz = []
for j in ssk['joints']:
    G = s_G0[j]; _jx.append(-G[0][3]); _jy.append(G[1][3]); _jz.append(G[2][3])
JB_H = max(_jy) - min(_jy); HEIGHT = SC * JB_H          # HEIGHT = event.json 'height' (the plugin scales by height / joint-bounds height)
CX = (max(_jx) + min(_jx)) / 2; MINY = min(_jy); CZ = (max(_jz) + min(_jz)) / 2

def to_unity(p):                    # file-frame point -> unity-local metres (show origin frame, before the plateau offset)
    return (SC * (-p[0] - CX), SC * (p[1] - MINY), SC * (p[2] - CZ))
def file_xz(ux, uz):                # unity-local x,z (metres) -> file-frame x,z
    return (-(ux / SC + CX), uz / SC + CZ)
def heading(v): return math.degrees(math.atan2(-v[0], v[2]))      # unity heading (0 = +z, 90 = +x) of a file-frame direction

# ------------------------------------------------------------------ 2. the six real acts as one timeline
ORDER = rc.ORDER
SEG = []; _t = 0.0
for a in ORDER: SEG.append((a, _t, rc.DUR[a])); _t += rc.DUR[a]
TOTAL = _t
IKN = {j for j in s_joint_list if node_to_name[j].startswith('ik_')}
VTR = set()                         # joints (not pelvis / IK helpers) whose translation varies in any act
for a in ORDER:
    for j, (ts, vs) in rc._acts[a]['tr'].items():
        if j == PEL or j in IKN or j not in s_joint_list: continue
        if max(max(abs(v[i] - vs[0][i]) for i in range(3)) for v in vs) > 1e-4: VTR.add(j)
PEL_G = s_G0[s_par[PEL]]            # rotation only (-90 deg about X): file-world = PEL_G * parent-local

def _tr(track, t):
    ts, vs = track
    if t <= ts[0]: return vs[0]
    if t >= ts[-1]: return vs[-1]
    i = bisect.bisect_right(ts, t) - 1; f = (t - ts[i]) / (ts[i + 1] - ts[i]); a, b = vs[i], vs[i + 1]
    return tuple(a[k] + (b[k] - a[k]) * f for k in range(len(a)))

def act_sample(act, t):
    A = rc._acts[act]; q = {}; tr = {}
    for j in s_joint_list:
        if j in A['rot']: ts, vs = A['rot'][j]; q[j] = _slerp_track(ts, vs, t)
        else: q[j] = REST_Q[j]
    for j in VTR: tr[j] = _tr(A['tr'][j], t) if j in A['tr'] else REST_T[j]
    pel = _tr(A['tr'][PEL], t) if PEL in A['tr'] else REST_T[PEL]
    return q, tr, pel

def _slerp_track(ts, vs, t):
    if t <= ts[0]: return vs[0]
    if t >= ts[-1]: return vs[-1]
    i = bisect.bisect_right(ts, t) - 1; f = (t - ts[i]) / (ts[i + 1] - ts[i]); return slerp(vs[i], vs[i + 1], f)

def _blend(A, B, w):
    qa, ta, pa = A; qb, tb, pb = B
    q = {j: slerp(qa[j], qb[j], w) for j in qa}
    tr = {j: tuple(ta[j][k] + (tb[j][k] - ta[j][k]) * w for k in range(3)) for j in ta}
    pel = tuple(pa[k] + (pb[k] - pa[k]) * w for k in range(3))
    return q, tr, pel

XFADE = 0.6
def sample_m(m):
    """Pose (rotations, varying translations, pelvis raw translation) of the stitched real timeline at monster time m, crossfading each seam."""
    if m >= TOTAL: a, s0, d = SEG[-1]; return act_sample(a, d)
    m = max(0.0, m)
    for i, (a, s0, d) in enumerate(SEG):
        if m < s0 + d: break
    t = m - s0
    if i + 1 < len(SEG) and t > d - XFADE / 2:
        w = (t - (d - XFADE / 2)) / XFADE; w = w * w * (3 - 2 * w)
        return _blend(act_sample(a, min(t, d)), act_sample(SEG[i + 1][0], max(0.0, m - (s0 + d))), w)
    if i > 0 and t < XFADE / 2:
        w = (t + XFADE / 2) / XFADE; w = w * w * (3 - 2 * w)
        return _blend(act_sample(SEG[i - 1][0], rc.DUR[SEG[i - 1][0]]), act_sample(a, t), w)
    return act_sample(a, t)

# ------------------------------------------------------------------ node forward kinematics (file frame)
def local_m4(j, q, tr, pel):
    T = pel if j == PEL else tr.get(j, REST_T[j]); return trs_to_m4(T, q[j], REST_S[j])
def globals_of(q, tr, pel):
    G = {}
    for j in s_order:
        if j in s_joint_list:
            L = local_m4(j, q, tr, pel)
        else:
            L = node_local(sg['nodes'][j])
        G[j] = mmul(G[s_par[j]], L) if s_par[j] >= 0 else L
    return G
def gpos(G, name): m = G[N(name)]; return [m[0][3], m[1][3], m[2][3]]


# ------------------------------------------------------------------ 3. the root path: clip pelvis -> arena
DELTA = 90.0                         # extra unity heading (deg) added to everything: the clip's fight direction (~25 deg) ends up pointing along +x
ANCH_M = 100.0                       # clip time of the anchor (the beam stance, Act1B)
ANCH_U = (0.0, 190.0)                # unity-local x,z of the pelvis there (metres)
# Horizontal root motion is shrunk so the show fits the arena.  GROUND_K applies to ordinary crawling everywhere (hands / feet are re-reached so planted ones do not slide);
# KSEG overrides it: (m0, m1, kind, k)  kind 'ik' = ground gait with its own factor, 'air' = jumps / flings / hidden travel: plain shrink without limb changes
GROUND_K = 1.0 if os.environ.get('RM_K1') else 0.3
KSEG = [(61.0, 85.0, 'ik', 1.0 if os.environ.get('RM_K1') else 0.15), (165.5, 176.0, 'air', 0.10), (199.0, 205.2, 'air', 0.10), (223.3, 227.5, 'air', 0.20), (280.5, 292.5, 'ik', 1.0 if os.environ.get('RM_K1') else 0.10), (304.0, 329.0, 'ik', 1.0 if os.environ.get('RM_K1') else 0.15), (329.0, 334.0, 'air', 0.25)]
RAMP = 1.0
def k_at(m):
    for m0, m1, kind, k in KSEG:
        if m0 - RAMP < m < m1 + RAMP:
            e = min(1.0, (m - (m0 - RAMP)) / RAMP, ((m1 + RAMP) - m) / RAMP); e = e * e * (3 - 2 * e)
            base = GROUND_K if kind == 'ik' else 1.0
            return base + (k - base) * e, kind, (m0, m1), 1.0 if kind == 'ik' else e
    return GROUND_K, 'ik', None, 1.0
PEL_R = rot_of(PEL_G)                        # the static parent chain's rotation (-90 deg about X)
def pel_world(raw): return mv(PEL_R, raw)
def pel_raw(w): return mv(m3T(PEL_R), w)
def ry(th):                                  # file-frame rotation about +Y by th degrees
    c, s_ = math.cos(math.radians(th)), math.sin(math.radians(th)); return [[c, 0, s_], [0, 1, 0], [-s_, 0, c]]
THETA = -DELTA                               # unity heading +DELTA == file-frame rotation of -DELTA (the loader mirrors X)

def clip_pel(m):
    q, tr, pel = sample_m(m); return pel_world(pel)
def build_path(m_start, m_end):
    n = int(round((m_end - m_start) * FPS)) + 1; ms = [m_start + i / FPS for i in range(n)]
    cw = [clip_pel(m) for m in ms]; cx = [0.0] * n; cz = [0.0] * n; ks = [1.0] * n
    for i in range(1, n):
        dx, dz = cw[i][0] - cw[i - 1][0], cw[i][2] - cw[i - 1][2]; k = k_at(ms[i])[0]; ks[i] = k
        cx[i] = cx[i - 1] + dx * k; cz[i] = cz[i - 1] + dz * k
    ia = int(round((ANCH_M - m_start) * FPS)); ax, az = cx[ia], cz[ia]
    return ms, cw, [x - ax for x in cx], [z - az for z in cz], ks, None
def placed(dcx, dcz):
    """offset (clip frame, compressed, relative to the anchor) -> file-frame world x,z of the pelvis (rotated by DELTA, anchored at ANCH_U)"""
    fx0, fz0 = file_xz(*ANCH_U); R_ = ry(THETA)
    return fx0 + R_[0][0] * dcx + R_[0][2] * dcz, fz0 + R_[2][0] * dcx + R_[2][2] * dcz

# ------------------------------------------------------------------ 4. limb re-reach (planted hands / feet while the gait is shrunk) - two-bone IK in the clip frame
def rot3(G): return [[G[r][c] for c in range(3)] for r in range(3)]
def rot_between(a, b):
    c = vdot(a, b)
    if c > 0.999999: return [[1.0 if r == k else 0.0 for k in range(3)] for r in range(3)]
    if c < -0.999999:
        ax = vnorm(vcross(a, [1, 0, 0] if abs(a[0]) < 0.9 else [0, 1, 0])); return [[2 * ax[r] * ax[k] - (1.0 if r == k else 0.0) for k in range(3)] for r in range(3)]
    v = vcross(a, b); k = 1.0 / (1.0 + c)
    return [[c + v[0] * v[0] * k, v[0] * v[1] * k - v[2], v[0] * v[2] * k + v[1]], [v[0] * v[1] * k + v[2], c + v[1] * v[1] * k, v[1] * v[2] * k - v[0]], [v[0] * v[2] * k - v[1], v[1] * v[2] * k + v[0], c + v[2] * v[2] * k]]
def smoothstep(a, b, x):
    u = max(0.0, min(1.0, (x - a) / (b - a))); return u * u * (3 - 2 * u)
def gpos2(G, j): m = G[j]; return [m[0][3], m[1][3], m[2][3]]
CHAINS = []
for _sd in ('l', 'r'):
    CHAINS.append(('arm_' + _sd, N('upperarm_' + _sd), N('lowerarm_' + _sd), N('hand_' + _sd), N('hand_' + _sd)))
    CHAINS.append(('leg_' + _sd, N('thigh_' + _sd), N('calf_' + _sd), N('foot_' + _sd), N('ball_' + _sd)))
def _anc(j):
    out = []
    while j >= 0: out.append(j); j = s_par[j]
    return out[::-1]
CH_PATH = {}                                  # ancestors (root first) needed to place a chain
for ch in CHAINS:
    CH_PATH[ch[0]] = sorted(set(_anc(ch[3]) + _anc(ch[4]) + _anc(N('pelvis'))), key=lambda j: s_order.index(j))
def chain_globals(q, tr, pel, ch):
    G = {}
    for j in CH_PATH[ch[0]]:
        L = local_m4(j, q, tr, pel) if j in s_joint_list else node_local(sg['nodes'][j])
        G[j] = mmul(G[s_par[j]], L) if s_par[j] >= 0 else L
    return G
def reach(G, q, chain, corr):
    """two-bone IK: move the chain's end by corr (clip frame) keeping the bend direction; edits the three local rotations in q"""
    _, j0, j1, j2, _t = chain
    P0, P1, P2 = gpos2(G, j0), gpos2(G, j1), gpos2(G, j2)
    l1, l2 = vlen(vsub(P1, P0)), vlen(vsub(P2, P1)); d = vsub(vadd(P2, corr), P0); D = vlen(d)
    D = max(abs(l1 - l2) + 1e-3, min(l1 + l2 - 1e-3, D)); a1 = vnorm(d); tgt = vadd(P0, [x * D for x in a1])
    a0 = vnorm(vsub(P2, P0)); v = vsub(P1, P0); pole = vsub(v, [x * vdot(v, a0) for x in a0])
    if vlen(pole) < 1e-4: pole = [0.0, 0.0, 1.0]
    pole = vnorm(vsub(pole, [x * vdot(pole, a1) for x in a1]))
    xx = (l1 * l1 - l2 * l2 + D * D) / (2 * D); hh = math.sqrt(max(0.0, l1 * l1 - xx * xx))
    P1n = vadd(P0, vadd([x * xx for x in a1], [x * hh for x in pole]))
    R0w, R1w, R2w = rot3(G[j0]), rot3(G[j1]), rot3(G[j2]); Rpar = rot3(G[s_par[j0]])
    S0 = rot_between(vnorm(vsub(P1, P0)), vnorm(vsub(P1n, P0))); R0n = m3mul(S0, R0w)
    S1 = rot_between(vnorm(vsub(P2, P1)), vnorm(vsub(tgt, P1n))); R1n = m3mul(S1, R1w)
    q[j0] = m3_to_quat(m3mul(m3T(Rpar), R0n)); q[j1] = m3_to_quat(m3mul(m3T(R0n), R1n)); q[j2] = m3_to_quat(m3mul(m3T(R1n), R2w))


def compact():
    """drop the buffer data of the file's original animations (73 MB of orphaned keys) - keep meshes, skin, images"""
    G = R.sg; old_b = bytes(R.sb); used = []
    for m in G['meshes']:
        for pr in m['primitives']:
            used += list(pr['attributes'].values()) + ([pr['indices']] if 'indices' in pr else [])
    for sk in G.get('skins', []):
        if 'inverseBindMatrices' in sk: used.append(sk['inverseBindMatrices'])
    used = sorted(set(used)); amap = {a: k for k, a in enumerate(used)}
    nb = bytearray(); views = []; vmap = {}
    def bv_new(bi):
        if bi in vmap: return vmap[bi]
        v = G['bufferViews'][bi]; off = v.get('byteOffset', 0); data = old_b[off:off + v['byteLength']]
        while len(nb) % 4: nb.append(0)
        nv = {k: x for k, x in v.items() if k != 'byteOffset'}; nv['buffer'] = 0; nv['byteOffset'] = len(nb); nb.extend(data); views.append(nv); vmap[bi] = len(views) - 1; return vmap[bi]
    accs = []
    for a in used:
        ac = dict(G['accessors'][a]); ac['bufferView'] = bv_new(ac['bufferView']); accs.append(ac)
    for im in G.get('images', []):
        if 'bufferView' in im: im['bufferView'] = bv_new(im['bufferView'])
    for m in G['meshes']:
        for pr in m['primitives']:
            pr['attributes'] = {k: amap[v] for k, v in pr['attributes'].items()}
            if 'indices' in pr: pr['indices'] = amap[pr['indices']]
    for sk in G.get('skins', []):
        if 'inverseBindMatrices' in sk: sk['inverseBindMatrices'] = amap[sk['inverseBindMatrices']]
    G['accessors'] = accs; G['bufferViews'] = views; G['buffers'][0]['byteLength'] = len(nb); G.pop('animations', None)
    R.sb = bytes(nb)
    print('compacted buffer %d MB -> %d MB' % (len(old_b) // 1048576, len(nb) // 1048576))

# ------------------------------------------------------------------ 5. bake
JAW = N('jaw_01'); MOUTH = N('FX_mouthLaser'); HEADJ = N('head')
TRACE_BONES = [b for b in ('head', 'eye', 'jaw_01', 'hand_l', 'hand_r', 'spine_03', 'pelvis', 'upperarm_r', 'upperarm_l', 'weapon_l', 'weapon_r', 'foot_l', 'foot_r', 'ball_l', 'ball_r', 'FX_mouthLaser') if b in s_name]
def jaw_open_deg(q):
    d = abs(sum(x * y for x, y in zip(q[JAW], REST_Q[JAW]))); return math.degrees(2 * math.acos(min(1.0, d)))

def bake(t0=0.0, t1=DUR, out=None):
    ms, cw, cx, cz, ks, seg_u = build_path(OFFSET, OFFSET + DUR)
    N0 = int(round(t0 * FPS)); N1 = min(len(ms), int(round(t1 * FPS)) + 1); n = len(ms); W = int(5.5 * FPS)
    # ---- pass 1: sample the real clip, note where every hand / foot is relative to the pelvis
    Q = [None] * n; TR = [None] * n; PL = [None] * n; OFFS = {ch[0]: [(0, 0, 0)] * n for ch in CHAINS}; TIPY = {ch[0]: [9.0] * n for ch in CHAINS}
    lo_ = max(0, N0 - W - 2); hi_ = min(n, N1 + W + 2)
    for i in range(lo_, hi_):
        q, tr, pel = sample_m(ms[i]); Q[i] = q; TR[i] = tr; PL[i] = pel
        for ch in CHAINS:
            G = chain_globals(q, tr, pel, ch); E = gpos2(G, ch[3]); tip = gpos2(G, ch[4])
            OFFS[ch[0]][i] = vsub(E, cw[i]); TIPY[ch[0]][i] = tip[1]          # end point relative to the pelvis position (world axes: no coupling with the pelvis' rotation)
        if i % 600 == 0: print('pass 1', i, '/', n, flush=True)
    # smoothed horizontal velocity direction of the clip pelvis (travel axis) and speed
    U = []; SP = []
    for i in range(n):
        a = max(0, i - int(0.5 * FPS)); b = min(n - 1, i + int(0.5 * FPS)); dx, dz = cw[b][0] - cw[a][0], cw[b][2] - cw[a][2]; dt = (b - a) / FPS or 1.0
        L = math.hypot(dx, dz); U.append((dx / L, dz / L) if L > 1e-6 else (0.0, 0.0)); SP.append(L / dt)
    # ---- pass 2: re-reach, turn, place, write
    rots = {j: array('f') for j in s_joint_list}; pel_arr = array('f'); trans = {j: array('f') for j in VTR}
    trace = {'dt': 0.1, 'b': {b: [] for b in TRACE_BONES}}; beam = {'dt': 0.1, 'mouth': [], 'dir': [], 'jaw': []}
    prev = {}; Rd = ry(THETA); stats = {'edits': 0, 'max_corr': 0.0}
    nb = {ch[0]: None for ch in CHAINS}
    for i in range(N0, N1):
        m = ms[i]; q = Q[i]; tr = TR[i]; pel = PL[i]; k, kind, seg, e = k_at(m)
        fx, fz = placed(cx[i], cz[i]); wy = cw[i][1] + MINY
        ux, uz = U[i]; sw = smoothstep(0.05, 0.2, SP[i])
        if kind == 'ik' and k < 0.999 and sw > 0.01:
            for ch in CHAINS:
                lo = max(lo_, i - W); hi = min(hi_, i + W + 1); arr = OFFS[ch[0]]
                if seg is not None: lo = max(lo_, int(round((seg[0] - OFFSET) * FPS))); hi = min(hi_, int(round((seg[1] - OFFSET) * FPS)) + 1)          # a named gait segment: one neutral stance for the whole walk
                neutral = [sum(arr[t][c] for t in range(lo, hi)) / (hi - lo) for c in range(3)]
                wgt = e * sw * (1.0 - smoothstep(0.45, 0.9, TIPY[ch[0]][i]))
                if wgt < 0.01: continue
                G = chain_globals(q, tr, pel, ch); E = gpos2(G, ch[3])
                rel = vsub(arr[i], neutral); a = rel[0]
                corr = [-(1.0 - k) * rel[0] * wgt, 0.0, -(1.0 - k) * rel[2] * wgt]       # planted end: E' = k E + c' - k c + (1-k) neutral = constant while the pelvis path is shrunk by k
                if abs(corr[0]) + abs(corr[2]) > 1e-4:
                    reach(G, q, ch, corr); stats['edits'] += 1; stats['max_corr'] = max(stats['max_corr'], vlen(corr))
                    if os.environ.get('RM_DBG') and ch[0] == 'leg_l' and i % 5 == 0:
                        G2 = chain_globals(q, tr, pel, ch); E2 = gpos2(G2, ch[3]); print('dbg t %.2f rel_a %.3f corr %.3f %.3f achieved %.3f %.3f k %.2f wgt %.2f' % (i / FPS, a, corr[0], corr[2], E2[0] - E[0], E2[2] - E[2], k, wgt))
        Rw2 = m3mul(Rd, m3mul(PEL_R, quat_to_m3(q[PEL]))); q[PEL] = m3_to_quat(m3mul(m3T(PEL_R), Rw2))        # whole body turned by DELTA about the vertical
        praw = pel_raw([fx, wy, fz])
        for j in s_joint_list:
            qq = q[j]
            if j in prev and sum(a * b for a, b in zip(qq, prev[j])) < 0: qq = tuple(-x for x in qq)
            prev[j] = qq; rots[j].extend(qq)
        pel_arr.extend(praw)
        for j in VTR: trans[j].extend(tr[j])
        if i % 3 == 0:                                # 10 Hz trace for the robot / cues
            G = globals_of(q, tr, tuple(praw))
            for b in TRACE_BONES: trace['b'][b].append([round(v, 2) for v in to_unity(gpos2(G, N(b)))])
            mp = to_unity(gpos2(G, MOUTH)); hf = mv(rot3(G[HEADJ]), mv(m3T(RB[HEADJ]), [0, 0, 1])); fwd = [-hf[0], hf[1], hf[2]]
            beam['mouth'].append([round(v, 2) for v in mp]); beam['dir'].append([round(v, 3) for v in vnorm(fwd)]); beam['jaw'].append(round(jaw_open_deg(q), 1))
        if i % 600 == 0: print('pass 2', i, '/', n, 'm %.1f' % m, flush=True)
    print('limb re-reach edits', stats['edits'], 'max correction %.2f units' % stats['max_corr'])
    clip = {'name': 'Showdown_Monster', 'times': [k_ / FPS for k_ in range(N1 - N0)], 'rots': rots, 'pelvis': pel_arr, 'trans': trans, 'scales': {}}
    if out:
        write_glb(out, [clip]); tag = os.environ.get('RM_TAG', ''); json.dump({'height': HEIGHT, 'sc': SC, 'offset': OFFSET, 'delta': DELTA, 'anchor': list(ANCH_U)}, open(os.path.join(OUT, 'monster_meta%s.json' % tag), 'w')); json.dump(trace, open(os.path.join(OUT, 'monster_trace%s.json' % tag), 'w')); json.dump(beam, open(os.path.join(OUT, 'monster_beam%s.json' % tag), 'w'))
    return clip, trace, beam

if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'path':
        ms, cw, cx, cz, ks, seg_u = build_path(OFFSET, OFFSET + DUR)
        for t in (0, 10, 19, 25, 30, 40, 45, 60, 75, 80, 85, 90, 100, 110, 120, 124, 128, 131, 134, 140, 150, 157, 158, 159, 161, 165, 175, 183, 190, 200, 206, 215, 225, 235, 240, 244.6, 248, 255, 260, 262, 270, 290, 303):
            i = int(round(t * FPS)); fx, fz = placed(cx[i], cz[i]); u = to_unity([fx, 0, fz]); print('audio %6.1f (m %6.1f) pelvis y %5.2f  unity x %7.1f z %7.1f   k %.2f' % (t, ms[i], cw[i][1], u[0], u[2], ks[i]))
    elif len(sys.argv) > 1 and sys.argv[1] in ('bake', 'quick'):
        compact()
        quick = sys.argv[1] == 'quick'; t0 = float(sys.argv[2]) if len(sys.argv) > 3 else 0.0; t1 = float(sys.argv[-1]) if len(sys.argv) > 2 else DUR
        bake(t0, t1, os.path.join(OUT, 'monster_quick.glb' if quick else 'monster.glb'))
    print('rest pose := bind;  joint-bounds height %.4f  =>  event.json height %.2f for %.1f m per file unit;  centre x %.3f z %.3f  min y %.3f' % (JB_H, HEIGHT, SC, CX, CZ, MINY))
    print('timeline', [(a[7:], round(s_, 2), round(d, 2)) for a, s_, d in SEG], 'total', round(TOTAL, 2), 'varying translations', len(VTR))
