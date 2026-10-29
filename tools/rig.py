"""Rig toolkit for the Travis skin: bind data, rotation-from-bind (P) space forward kinematics, retarget extraction and GLB export.

Conventions
  * P[j] = 3x3 WORLD rotation of joint j relative to its bind orientation (identity = T-pose).  Bone orientation = P[j] * Rbind[j].
  * Authoring frame = bind facing: +Z forward, +Y up, +X is the character's LEFT.  Export applies Ry(180deg) so he faces -Z in the game.
  * Positions are mesh space (the skin's raw units, ~16000 per body height).
"""
from array import array
import sys, os, math, json, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glbmath import *

SKIN = os.environ.get('RIG_SKIN', r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")
GENERIC = 'RIG_SKIN' in os.environ                       # another character (robot / monster): no retarget clip, bind data rebuilt from the node tree
ANIM = None if GENERIC else r"C:\Users\drago\Downloads\travis_scott_fortnite_event_animation_1.glb"
STRIP = int(os.environ.get('RIG_STRIP', '2'))
I3 = [[1.0, 0, 0], [0, 1.0, 0], [0, 0, 1.0]]

def base(name, strip):
    t = name.split('_')
    for _ in range(strip):
        if len(t) > 1 and t[-1].isdigit(): t = t[:-1]
    return '_'.join(t)

# ---------- small vector / rotation helpers ----------
def vadd(a, b): return [a[i] + b[i] for i in range(3)]
def vsub(a, b): return [a[i] - b[i] for i in range(3)]
def vmul(a, s): return [x * s for x in a]
def vdot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def vcross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def vlen(a): return math.sqrt(vdot(a, a))
def vnorm(a):
    l = vlen(a) or 1.0
    return [x / l for x in a]
def mv(R, v): return [R[r][0] * v[0] + R[r][1] * v[1] + R[r][2] * v[2] for r in range(3)]
def mmT(A, B): return m3mul(m3T(A), B)         # A^T B

def rx(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); return [[1, 0, 0], [0, c, -s], [0, s, c]]
def ry(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
def rz(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); return [[c, -s, 0], [s, c, 0], [0, 0, 1]]
def r_axis(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a); x, y, z = vnorm(axis); C = 1 - c
    return [[c + x * x * C, x * y * C - z * s, x * z * C + y * s], [y * x * C + z * s, c + y * y * C, y * z * C - x * s], [z * x * C - y * s, z * y * C + x * s, c + z * z * C]]
def frame(axis, hint):
    """Orthonormal rotation whose X column is `axis` and whose Y column is the part of `hint` orthogonal to it."""
    a = vnorm(axis); h = vsub(hint, vmul(a, vdot(hint, a)))
    if vlen(h) < 1e-6:
        h = [0, 1, 0] if abs(a[1]) < 0.9 else [0, 0, 1]; h = vsub(h, vmul(a, vdot(h, a)))
    y = vnorm(h); z = vcross(a, y)
    return [[a[0], y[0], z[0]], [a[1], y[1], z[1]], [a[2], y[2], z[2]]]
def slerp_m(A, B, t):
    """Interpolate two rotation matrices (via quaternions)."""
    qa = m3_to_quat(A); qb = m3_to_quat(B)
    return quat_to_m3(slerp(qa, qb, t))
def mirror_rot(R):
    """Mirror a rotation across the X=0 plane (left <-> right)."""
    S = [[-1, 0, 0], [0, 1, 0], [0, 0, 1]]
    return m3mul(m3mul(S, R), S)
def smooth(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

# ---------- load skin ----------
sg, sb = load_glb(SKIN)
s_order, s_par = order_of(sg)
ssk = sg['skins'][0]
s_ibm = [from_colmajor(m) for m in read_acc(sg, sb, ssk['inverseBindMatrices'])]
s_joint_index = {ji: k for k, ji in enumerate(ssk['joints'])}
s_name = {}
for ji in ssk['joints']: s_name.setdefault(base(sg['nodes'][ji]['name'], STRIP), ji)
node_to_name = {ji: base(sg['nodes'][ji]['name'], STRIP) for ji in ssk['joints']}
for _al in filter(None, os.environ.get('RIG_ALIAS', '').split(',')):       # e.g. head=head_offset
    _a, _b = _al.split('=')
    if _b in s_name: s_name[_a] = s_name[_b]
s_is_joint = set(ssk['joints'])
s_joint_list = [i for i in s_order if i in s_is_joint]
children = {}
for _j in s_joint_list: children.setdefault(s_par[_j], []).append(_j)
def _sub(n):
    out = [n]
    for c in children.get(n, []): out += _sub(c)
    return out
subtree = {j: _sub(j) for j in s_joint_list}

def _col_scale(M): return math.sqrt(sum(M[r][0] ** 2 for r in range(3)))
def _global_rest(g, order, par):
    G = {}
    for i in order:
        L = node_local(g['nodes'][i]); G[i] = mmul(G[par[i]], L) if par[i] >= 0 else L
    return G
s_G0 = _global_rest(sg, s_order, s_par)
s_mesh = [i for i, n in enumerate(sg['nodes']) if 'skin' in n][0]
s_f = _col_scale(s_G0[s_mesh]) * _col_scale(s_ibm[s_joint_index[s_name['pelvis']]])   # unit factor (=1)
S_RM = I3   # net mesh rotation is identity (the exporter's IBMs cancel the root chain rotation)

def bind_pos(name_or_node):
    j = s_name[name_or_node] if isinstance(name_or_node, str) else name_or_node
    W = inv4(s_ibm[s_joint_index[j]]); return [W[r][3] for r in range(3)]
def _ibm_ok(j): return j in s_joint_index and abs(_col_scale(inv4(s_ibm[s_joint_index[j]])) - 1.0) < 0.05
BP = {}; RB = {}
for _j in s_joint_list:
    if _ibm_ok(_j) or not GENERIC:
        BP[_j] = bind_pos(_j); RB[_j] = m3T(rot_of(s_ibm[s_joint_index[_j]]))
    else:                                                                     # unweighted bones of the exported models carry a zeroed inverse-bind matrix: rebuild from the parent
        _n = sg['nodes'][_j]; _p = s_par[_j]; _q = _n.get('rotation', [0, 0, 0, 1])
        if _p in BP:
            BP[_j] = vadd(BP[_p], mv(RB[_p], _n.get('translation', [0, 0, 0]))); RB[_j] = m3mul(RB[_p], quat_to_m3(_q))
        else: BP[_j] = [0, 0, 0]; RB[_j] = I3
if GENERIC:                                                               # make the node rest pose agree with the bind pose the mesh was skinned to
    _static0 = set(); _q = s_par[s_name['pelvis']]
    while _q in s_is_joint: _static0.add(_q); _q = s_par[_q]
    for _j in s_joint_list:
        _p = s_par[_j]
        if _p in BP and _j not in _static0 and _p not in _static0 and not os.environ.get('RIG_KEEP_T'):
            sg['nodes'][_j]['translation'] = mv(m3T(RB[_p]), vsub(BP[_j], BP[_p]))
    s_G0 = _global_rest(sg, s_order, s_par)
    # repair the zeroed inverse-bind matrices of unweighted joints (some importers choke on them) so the exported file is clean
    _acc = sg['accessors'][ssk['inverseBindMatrices']]; _bv = sg['bufferViews'][_acc['bufferView']]; _off = _bv.get('byteOffset', 0) + _acc.get('byteOffset', 0)
    for _k, _ji in enumerate(ssk['joints']):
        if not _ibm_ok(_ji):
            _M = [[RB[_ji][r][c] for c in range(3)] + [BP[_ji][r]] for r in range(3)] + [[0, 0, 0, 1]]
            _I = inv4(_M); s_ibm[_k] = _I
            struct.pack_into('<16f', sb, _off + _k * 64, *[_I[r][c] for c in range(4) for r in range(4)])
PEL = s_name['pelvis']; PEL_B = BP[PEL]
BODY_H = float(os.environ['RIG_BODYH']) if 'RIG_BODYH' in os.environ else vlen(vsub(BP[s_name['head']], PEL_B)) * 2.9

def N(name): return s_name[name]
def jpar(j): return s_par[j] if s_par[j] in s_is_joint else None

# static (root-chain) joints above the pelvis keep their rest rotation
static_joints = set(); _p = s_par[PEL]
while _p in s_is_joint: static_joints.add(_p); _p = s_par[_p]
def static_parent_rot(i): return rot_of(s_G0[s_par[i]]) if s_par[i] >= 0 else I3

# ---------- P-space forward kinematics (positions only) ----------
def fk_positions(P, pelvis_pos, joints=None):
    """Joint positions from per-joint world rotations. pos[child] = pos[parent] + P[parent] * (bind[child]-bind[parent])."""
    pos = {PEL: list(pelvis_pos)}
    for j in (joints or s_joint_list):
        if j == PEL or j in static_joints: continue
        p = s_par[j]
        if p not in pos: continue
        pos[j] = vadd(pos[p], mv(P[p], vsub(BP[j], BP[p])))
    return pos

# ---------- export to full skin local quaternions ----------
def make_targets(P):
    return {sj: (rot_of(s_G0[sj]) if sj in static_joints else m3mul(m3mul(S_RM, P[sj]), RB[sj])) for sj in s_joint_list}

def skin_fk(rot_target, pelvis_global=None):
    SG = {}; Rg = {}; quats = {}; pel_local = None
    for sj in s_joint_list:
        par = s_par[sj]
        Rpar = Rg[par] if par in Rg else static_parent_rot(sj)
        q = m3_to_quat(m3mul(m3T(Rpar), rot_target[sj])); quats[sj] = q; Rg[sj] = rot_target[sj]
        n = sg['nodes'][sj]; T = n.get('translation', [0, 0, 0])
        Gpar = SG[par] if par in SG else s_G0[par]
        if pelvis_global is not None and sj == PEL:
            T = [sum(inv4(Gpar)[r][c] * ([x * s_f for x in pelvis_global] + [1.0])[c] for c in range(4)) for r in range(3)]; pel_local = tuple(T)      # s_f: node-world units per mesh (bind) unit (1 for most files, 100 for the Multibot skin)
        SG[sj] = mmul(Gpar, trs_to_m4(T, q, n.get('scale', [1, 1, 1])))
    return SG, quats, pel_local

FACE = ry(180)   # authoring frame (faces +Z) -> game frame (faces -Z)

def pose_to_quats(P, pelvis_pos, face=True):
    """Full pose -> per-joint local quaternions + pelvis local translation (for the GLB)."""
    Pf = {j: (m3mul(FACE, P[j]) if face else P[j]) for j in s_joint_list}
    pp = mv(FACE, pelvis_pos) if face else list(pelvis_pos)
    # the pelvis rest position rotates with the facing too (rotate about the vertical axis through the bind pelvis column)
    _, quats, pel_local = skin_fk(make_targets(Pf), pp)
    return quats, pel_local

# ---------- retarget extraction of the real clips (author frame) ----------
if ANIM:
    ag, ab = load_glb(ANIM)
    a_order, a_par = order_of(ag)
    ask = ag['skins'][0]
    a_ibm = [from_colmajor(m) for m in read_acc(ag, ab, ask['inverseBindMatrices'])]
    a_joint_index = {ji: k for k, ji in enumerate(ask['joints'])}
    a_name = {}
    for ji in ask['joints']: a_name.setdefault(base(ag['nodes'][ji]['name'], 1), ji)
    mapping = {s_name[k]: a_name[k] for k in s_name if k in a_name}
    a_G0 = _global_rest(ag, a_order, a_par)
    a_mesh = [i for i, n in enumerate(ag['nodes']) if 'skin' in n][0]
    a_f = _col_scale(a_G0[a_mesh]) * _col_scale(a_ibm[a_joint_index[a_name['pelvis']]])
    a_IBMrot = {ji: rot_of(a_ibm[k]) for ji, k in a_joint_index.items()}
    a_bind_pelvis = [inv4(a_ibm[a_joint_index[a_name['pelvis']]])[r][3] for r in range(3)]
    a_bind_head = [inv4(a_ibm[a_joint_index[a_name['head']]])[r][3] for r in range(3)]
    K_UNIT = vlen(vsub(BP[s_name['head']], PEL_B)) / vlen(vsub(a_bind_head, a_bind_pelvis))

    def _anim_tracks(clip):
        tr = {}; cache = {}
        for ch in clip['channels']:
            n = ch['target'].get('node'); p = ch['target']['path']
            if n is None or p not in ('translation', 'rotation', 'scale'): continue
            s = clip['samplers'][ch['sampler']]
            if s['input'] not in cache: cache[s['input']] = [x[0] for x in read_acc(ag, ab, s['input'])]
            tr.setdefault(n, {})[p] = (cache[s['input']], read_acc(ag, ab, s['output']))
        return tr
    def _sample(track, t, is_rot):
        ts, vs = track
        if t <= ts[0]: return vs[0]
        if t >= ts[-1]: return vs[-1]
        lo, hi = 0, len(ts) - 1
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if ts[mid] <= t: lo = mid
            else: hi = mid
        f = (t - ts[lo]) / (ts[hi] - ts[lo])
        return slerp(tuple(vs[lo]), tuple(vs[hi]), f) if is_rot else tuple(x + (y - x) * f for x, y in zip(vs[lo], vs[hi]))
    def _anim_globals(t, tracks):
        G = {}
        for i in a_order:
            n = ag['nodes'][i]
            if i in tracks and 'matrix' not in n:
                tk = tracks[i]
                T = _sample(tk['translation'], t, False) if 'translation' in tk else n.get('translation', [0, 0, 0])
                Q = _sample(tk['rotation'], t, True) if 'rotation' in tk else n.get('rotation', [0, 0, 0, 1])
                S = _sample(tk['scale'], t, False) if 'scale' in tk else n.get('scale', [1, 1, 1])
                L = trs_to_m4(T, Q, S)
            else: L = node_local(n)
            G[i] = mmul(G[a_par[i]], L) if a_par[i] >= 0 else L
        return G

    def _pose_from_anim(AG):
        Pq = {}
        for sj in s_joint_list:
            if sj in mapping: Pq[sj] = m3mul(rot_of(AG[mapping[sj]]), a_IBMrot[mapping[sj]])
            else: Pq[sj] = Pq[s_par[sj]] if s_par[sj] in Pq else I3
        return Pq

    def hip_yaw(P, pelvis_pos=None):
        pos = fk_positions(P, pelvis_pos or PEL_B)
        lat = vsub(pos[N('thigh_l')], pos[N('thigh_r')])
        return math.atan2(-lat[2], lat[0])      # 0 for the bind pose (left hip at +X)

    def extract_clip(index, fps=30.0):
        """Real clip -> list of (P in the authoring frame, pelvis pos (mesh space)) at fps, facing normalised to +Z by its mean hip yaw."""
        clip = ag['animations'][index]; tracks = _anim_tracks(clip)
        dur = max(v[0][-1] for tk in tracks.values() for v in tk.values())
        n = int(dur * fps) + 1
        poses = []; thetas = []
        for k in range(n):
            AG = _anim_globals(k / fps, tracks); Pq = _pose_from_anim(AG)
            pa = [sum(AG[a_name['pelvis']][r][3] for _ in [0]) for r in range(3)]
            pa = [x / a_f for x in pa]                                   # anim mesh space
            pel = [PEL_B[r] + K_UNIT * (pa[r] - a_bind_pelvis[r]) for r in range(3)]
            poses.append((Pq, pel)); thetas.append(hip_yaw(Pq, pel))
        mean = math.atan2(sum(math.sin(x) for x in thetas), sum(math.cos(x) for x in thetas))
        Rc = ry(-math.degrees(mean))
        out = []
        for Pq, pel in poses:
            out.append(({j: m3mul(Rc, Pq[j]) for j in s_joint_list}, [pel[0] - PEL_B[0], pel[1] - PEL_B[1], pel[2] - PEL_B[2]]))
        return out, dur


# ---------- GLB writer ----------
def write_glb(path, clips):
    """clips: list of dicts {name, times, rots: {joint: [quat per frame]}, pelvis: [local translation per frame]}. Constant channels are stored as 2 keys."""
    G = json.loads(json.dumps(sg)); G.pop('animations', None)
    binb = bytearray(sb); views = G['bufferViews']; accs = G['accessors']
    def add(data, typ, count, mn=None, mx=None):
        while len(binb) % 4: binb.append(0)
        views.append({'buffer': 0, 'byteOffset': len(binb), 'byteLength': len(data)}); binb.extend(data)
        a = {'bufferView': len(views) - 1, 'componentType': 5126, 'count': count, 'type': typ}
        if mn is not None: a['min'] = mn; a['max'] = mx
        accs.append(a); return len(accs) - 1
    anims = []
    for c in clips:
        times = c['times']; n = len(times)
        ti = add(struct.pack('<%df' % n, *times), 'SCALAR', n, [min(times)], [max(times)])
        t2 = add(struct.pack('<2f', times[0], times[-1]), 'SCALAR', 2, [times[0]], [times[-1]])
        samplers = []; channels = []
        moving = 0
        for sj in s_joint_list:
            qs = c['rots'][sj]
            if isinstance(qs, array):                       # flat float array: x,y,z,w per frame
                f0 = qs[:4]; const = all(abs(qs[i] - f0[i & 3]) < 1e-5 for i in range(len(qs)))
                flat = list(f0) * 2 if const else qs
            else:
                const = all(abs(a - b) < 1e-5 for q in qs for a, b in zip(q, qs[0]))
                flat = list(qs[0]) * 2 if const else [x for q in qs for x in q]
            if const: ai = add(struct.pack('<8f', *flat), 'VEC4', 2); inp = t2
            else: ai = add(struct.pack('<%df' % len(flat), *flat), 'VEC4', n); inp = ti; moving += 1
            samplers.append({'input': inp, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': sj, 'path': 'rotation'}})
        flat = c['pelvis'] if isinstance(c['pelvis'], array) else [x for v in c['pelvis'] for x in v]
        ai = add(struct.pack('<%df' % len(flat), *flat), 'VEC3', n)
        samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': PEL, 'path': 'translation'}})
        for tj, vals in c.get('trans', {}).items():
            flat = vals if isinstance(vals, array) else [x for v in vals for x in v]; ai = add(struct.pack('<%df' % len(flat), *flat), 'VEC3', n)
            samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': tj, 'path': 'translation'}})
        for tj, vals in c.get('scales', {}).items():
            ai = add(struct.pack('<%df' % len(vals), *vals), 'VEC3', n)
            samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': tj, 'path': 'scale'}})
        anims.append({'name': c['name'], 'samplers': samplers, 'channels': channels})
        print("  clip %s: %d frames, %d animated joints" % (c['name'], n, moving))
    G['animations'] = anims; G['buffers'][0]['byteLength'] = len(binb)
    js = json.dumps(G, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(binb) % 4: binb.append(0)
    glb = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binb)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(binb), 0x004E4942) + bytes(binb)
    open(path, 'wb').write(glb)
    print("wrote", path, len(glb) // 1024, "KB")

def bake_clip(name, frames, fps):
    """frames: list of (P, pelvis_offset[, {bone: local translation delta}]) in the authoring frame -> clip dict (hemisphere-continuous quaternions)."""
    rots = {j: [] for j in s_joint_list}; pel = []; prev = {}; trans = {}
    for fi, fr in enumerate(frames):
        P, pp = fr[0], fr[1]; tr = fr[2] if len(fr) > 2 else {}
        quats, pl = pose_to_quats(P, vadd(PEL_B, pp))      # pp = pelvis offset from the bind pelvis (mesh units)
        for j in s_joint_list:
            q = quats[j]
            if j in prev and sum(a * b for a, b in zip(q, prev[j])) < 0: q = tuple(-x for x in q)
            prev[j] = q; rots[j].append(q)
        pel.append(pl)
        for bone, d in tr.items():
            j = N(bone); trans.setdefault(j, [None] * len(frames))
        for j in list(trans):
            rest = sg['nodes'][j].get('translation', [0, 0, 0]); d = tr.get(node_to_name[j], [0, 0, 0])
            trans[j][fi] = (rest[0] + d[0], rest[1] + d[1], rest[2] + d[2])
    for j, vals in trans.items():                      # frames before the bone first appears keep the rest translation
        rest = tuple(sg['nodes'][j].get('translation', [0, 0, 0]))
        for k in range(len(vals)):
            if vals[k] is None: vals[k] = rest
    return {'name': name, 'times': [k / fps for k in range(len(frames))], 'rots': rots, 'pelvis': pel, 'trans': trans}

class ClipBaker:
    """Incremental bake_clip: feed one frame at a time (keeps memory flat for a 12k-frame show)."""
    def __init__(self): self.rots = {j: array('f') for j in s_joint_list}; self.pel = array('f'); self.prev = {}; self.trans = {}; self.scales = {}; self.n = 0
    def add(self, P, pp, tr, sc=None):
        quats, pl = pose_to_quats(P, vadd(PEL_B, pp))
        for j in s_joint_list:
            q = quats[j]
            if j in self.prev and sum(a * b for a, b in zip(q, self.prev[j])) < 0: q = tuple(-x for x in q)
            self.prev[j] = q; self.rots[j].extend(q)
        self.pel.extend(pl)
        for bone in tr:
            j = N(bone)
            if j not in self.trans:                     # first time this bone moves: earlier frames keep its rest translation
                rest = sg['nodes'][j].get('translation', [0, 0, 0]); self.trans[j] = array('f', list(rest) * self.n)
        for j, arr in self.trans.items():
            rest = sg['nodes'][j].get('translation', [0, 0, 0]); d = tr.get(node_to_name[j], [0, 0, 0])
            arr.extend((rest[0] + d[0], rest[1] + d[1], rest[2] + d[2]))
        sc = sc or {}
        for bone in sc:
            j = N(bone)
            if j not in self.scales: self.scales[j] = array('f', [1.0, 1.0, 1.0] * self.n)
        for j, arr in self.scales.items(): arr.extend(sc.get(node_to_name[j], (1.0, 1.0, 1.0)))
        self.n += 1
    def finish(self, name, fps):
        return {'name': name, 'times': [k / fps for k in range(self.n)], 'rots': self.rots, 'pelvis': self.pel, 'trans': self.trans, 'scales': self.scales}

if __name__ == '__main__':
    print("joints", len(s_joint_list), "mapped", len(mapping), "unit", s_f, a_f, "K", K_UNIT, "BODY_H", round(BODY_H))
    for nm in ('pelvis', 'thigh_l', 'calf_l', 'foot_l', 'upperarm_l', 'lowerarm_l', 'hand_l', 'head', 'C_jaw', 'neck_01'):
        if nm in s_name:
            print(nm, [round(x) for x in BP[N(nm)]])
