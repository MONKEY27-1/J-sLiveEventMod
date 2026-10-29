"""py scene_check.py <t> <out_prefix> [camx camy camz tx ty tz]   re-creates the Collision underground at music time t (a Python port of AstronomicalCollision2.cs' placement maths, Unity axes) with EVERY model at once
(cave, Collider, Zero Point, mech with its clip, Slone's tank + Slone, Jones, Foundation, boulder, you), writes an OBJ and renders overview / side / player-view PNGs with Blender so collisions and bad placements can be seen.
The plugin's loader normalises skinned models to 1.8 units (skeleton height) and centres every model on its feet; that is reproduced here."""
import sys, os, math, json, subprocess, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache', 'collision')
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
def slerp(a, b, f):
    d = float(a @ b)
    if d < 0: b = -b; d = -d
    r = (1 - f) * a + f * b; return r / np.linalg.norm(r)
def sm(a, b, t): x = max(0.0, min(1.0, (t - a) / (b - a))); return x * x * (3 - 2 * x)
def yawm(deg): c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg)); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
class Model:
    def __init__(s, name):
        s.g, s.b = slim_glb.read_glb(os.path.join(D, name + '.glb')); g = s.g; s.N = g['nodes']; s.par = {}
        for i, n in enumerate(s.N):
            for c in n.get('children', []): s.par[c] = i
        s.skinned = any('skin' in n for n in s.N); s.anims = {a['name']: a for a in g.get('animations', [])}; s.cache = {}
        W = s.worlds(None, 0)
        if s.skinned:
            J = [W[j][:3, 3] * [-1, 1, 1] for sk in g['skins'] for j in sk['joints']]; J = np.array(J); lo, hi = J.min(0), J.max(0); s.norm = 1.8 / max(hi[1] - lo[1], 1e-3); s.off = np.array([-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2]) * s.norm
        else:
            V, _ = s.mesh(W); lo, hi = V.min(0), V.max(0); s.norm = 1.0; s.off = np.array([-(lo[0] + hi[0]) / 2, -lo[1], -(lo[2] + hi[2]) / 2])
    def acc(s, i):
        a = s.g['accessors'][i]; v = s.g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
        if st and st != np.dtype(dt).itemsize * n:
            raw = np.frombuffer(s.b, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
        return np.frombuffer(s.b, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
    def tracks(s, an):
        if an not in s.cache: a = s.anims[an]; s.cache[an] = [(ch['target']['node'], ch['target']['path'], s.acc(a['samplers'][ch['sampler']]['input'])[:, 0], s.acc(a['samplers'][ch['sampler']]['output'])) for ch in a['channels']]
        return s.cache[an]
    def worlds(s, an, t):
        N = s.N; T = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; R = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]
        if an and an in s.anims:
            tr = s.tracks(an); dur = max(x[2][-1] for x in tr); t = t % dur if dur > 0 else 0
            for node, path, ts, vals in tr:
                i = int(np.searchsorted(ts, t, side='right') - 1); i = max(0, min(i, len(ts) - 1)); j = min(i + 1, len(ts) - 1); f = 0 if j == i else (t - ts[i]) / (ts[j] - ts[i])
                if path == 'rotation': R[node] = slerp(vals[i], vals[j], f)
                elif path == 'translation': T[node] = (1 - f) * vals[i] + f * vals[j]
                else: S[node] = (1 - f) * vals[i] + f * vals[j]
        W = {}
        def w(i):
            if i in W: return W[i]
            if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T
            else: M = np.eye(4); M[:3, :3] = q2m(R[i]) @ np.diag(S[i]); M[:3, 3] = T[i]
            W[i] = w(s.par[i]) @ M if i in s.par else M; return W[i]
        return [w(i) for i in range(len(N))]
    def mesh(s, W):
        vs = []; fs = []
        for ni, n in enumerate(s.N):
            if 'mesh' not in n: continue
            for pr in s.g['meshes'][n['mesh']]['primitives']:
                at = pr['attributes']; P = s.acc(at['POSITION']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1)
                if 'skin' in n and 'JOINTS_0' in at:
                    sk = s.g['skins'][n['skin']]; J = sk['joints']; ibm = s.acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1); JI = s.acc(at['JOINTS_0']).astype(int); WT = s.acc(at['WEIGHTS_0']); o = np.zeros((len(P), 3)); M = np.array([W[J[j]] @ ibm[j] for j in range(len(J))])
                    for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
                else: o = (W[ni] @ Ph.T).T[:, :3]
                base = sum(len(v) for v in vs); vs.append(o); fs.append(s.acc(pr['indices']).astype(int).reshape(-1, 3) + base if 'indices' in pr else np.arange(len(P)).reshape(-1, 3) + base)
        return np.concatenate(vs), np.concatenate(fs)
    def local(s, an=None, t=0.0, step=1):
        """vertices in Pivot-local Unity space (loader normalisation applied) + the node dict {name: pos}"""
        W = s.worlds(an, t); V, F = s.mesh(W); V = V * [-1, 1, 1] * s.norm + s.off
        if step > 1: pass
        s.lastW = W; return V, F[:, ::-1]
    def node_local(s, W, prefix):
        for i, n in enumerate(s.N):
            if n['name'].startswith(prefix): return W[i][:3, 3] * [-1, 1, 1] * s.norm + s.off
        return None
OBJS = []
def put(name, m, pos, yaw=0.0, scale=1.0, an=None, t=0.0, by_origin=False, color=(0.8, 0.8, 0.8)):
    V, F = m.local(an, t); R = yawm(yaw)
    if by_origin: pos = np.asarray(pos) - R @ (scale * m.off)      # PlaceByFileOrigin: the file origin (Model position = Pivot + R s off) goes to pos
    VW = (R @ (V * scale).T).T + pos; OBJS.append((name, VW, F, color)); return m.lastW, R, np.asarray(pos)
# ---------------- the scene (same numbers as AstronomicalCollision2.cs)
SQ = 60.0; SQP = [(0, -SQ), (SQ, -SQ), (SQ, SQ), (-SQ, SQ), (-SQ, -SQ), (SQ, -SQ), (SQ, SQ), (-SQ, SQ), (-SQ, -SQ)]; SLONE_S = float(os.environ.get('SLONE_S', 0.733))
def Stair(s):
    s = min(max(s, 0), 1); total = 60 + 7 * 120; L = s * total; i = 0; acc = 0
    while i < len(SQP) - 2:
        seg = 60 if i == 0 else 120
        if L <= acc + seg: break
        acc += seg; i += 1
    ln = 60 if i == 0 else 120; u = min(max((L - acc) / ln, 0), 1); p = np.array(SQP[i]) * (1 - u) + np.array(SQP[i + 1]) * u; return np.array([p[0], 4 + 130 * s, p[1]])
def Out(s): p = Stair(s); p[1] = 0; n = np.linalg.norm(p); return p / n if n > 1e-4 else np.array([0, 0, 1.0])
def PathAt(q):
    if q < 0: z = -150 + 90 * (1 + q); return np.array([0, 4 * min(1, max(0, (z + 90) / 30)), z])
    p = Stair(q); p[1] = min(p[1], 4 + 130 * (0.733 - 24 / 900)); return p
def Yaw(q): d = PathAt(q + 0.003) - PathAt(q); return math.degrees(math.atan2(d[0], d[2]))
def ArcLen(q): return (q + 1) * 90 if q < 0 else 90 + q * 900
def DQ(m, q): return m / (90 if q < 0 else 900)
Q_END = float(os.environ.get('Q_END', 0.733 - 24 / 900))
WAY = [(493, -1), (504, -1), (516, 0), (540, 0), (547, 0.06), (554, 0.06), (558, 0.10), (571, 0.10), (579, 0.22), (582, 0.22), (588, 0.30), (613, 0.50), (619, 0.62), (625, Q_END), (900, Q_END)]
def QAt(t):
    if t <= WAY[0][0]: return WAY[0][1]
    for i in range(1, len(WAY)):
        if t < WAY[i][0]: return WAY[i - 1][1] + (WAY[i][1] - WAY[i - 1][1]) * sm(WAY[i - 1][0], WAY[i][0], t)
    return WAY[-1][1]
L4 = lambda: Stair(SLONE_S) + Out(SLONE_S) * 2
ZPC = np.array([0, 124.0, 0]); ZPR = 26.0
def run(t, out, cam=None):
    M = {k: Model(k) for k in ('cavern', 'collider', 'zeropoint', 'mech', 'tank', 'slone', 'jones', 'foundation', 'boulder')}
    put('cavern', M['cavern'], (0, 0, 0), by_origin=True, color=(0.45, 0.4, 0.5))
    fall = max(0, t - 692)
    if t < 705: put('collider', M['collider'], (0, -max(0, fall - 5) ** 2 * 6, 0), 0, 2.0, by_origin=True, color=(0.6, 0.65, 0.8))
    sink = max(0, t - 689); zpos = ZPC.copy(); zpos[1] -= 0.5 * 7 * sink * sink
    put('zeropoint', M['zeropoint'], zpos, t * 6, ZPR, by_origin=True, color=(0.6, 0.3, 0.9))
    q = QAt(t); pp = PathAt(q); walkHead = pp + [0, 1.6, 0]; headTarget = walkHead
    # mech (10:20 on)
    handHead = None
    if 622 <= t < 704.5:
        lp = lambda a, b, k: a + (b - a) * k
        Sd = Stair(Q_END); dn = Stair(Q_END + 0.003) - Sd; dn[1] = 0; dn /= np.linalg.norm(dn); sideV = np.cross([0, 1, 0], dn); Hside = Sd + sideV * 16.5 + [0, -0.4, 0]; Hrest = Hside + sideV * 55 + [0, 30, 0]; Hslam = L4() + [0, 3, 0]
        if t < 635.2: hd = lp(Hrest + sideV * 30 + [0, 20, 0], Hrest, sm(622, 630, t))
        elif t < 636.1: hd = lp(Hrest, Hslam, sm(635.2, 636, t))
        else: hd = lp(Hslam, Hside, sm(636.1, 644, t))
        if t < 635.2: c, off, spd, loop = 'M_Idle2', 0, 1, True
        elif t < 639: c, off, spd, loop = 'M_SwordSlash', 635.2, 1, False
        elif t < 664: c, off, spd, loop = 'M_Hand', 639, 1, False
        elif t < 689: c, off, spd, loop = 'M_HandUp', 664, 0.75, False
        elif t < 705: c, off, spd, loop = 'M_HandIn', 689, 0.5, False
        else: c, off, spd, loop = 'M_Brace', 0, 1, True
        mm = M['mech']; MS = float(os.environ.get('MECH_S', 63.0)); tc = t if loop else min((t - off) * spd, 7.97); yaw = math.degrees(math.atan2(-Hside[0], -Hside[2])); base = Hside + sideV * 60 + [0, -40, 0]
        hi = next(i for i, n in enumerate(mm.N) if n['name'].startswith('hand_l_014'))
        def palm_at(cn, tcc, pos0):
            W = mm.worlds(cn, tcc); pg = W[hi] @ np.array([0.0861, 0.02, -0.0561, 1.0]); R = yawm(yaw); return R @ (MS * (pg[:3] * [-1, 1, 1] * mm.norm + mm.off)) + pos0
        if t < 644: target = hd; ref = palm_at(c, tc, base)
        else: target = Hside; ref = palm_at('M_Hand', 5.0, base)
        pos = base + (target - ref)
        W, R, pos = put('mech', mm, pos, yaw, MS, c, tc, color=(0.55, 0.75, 0.95), by_origin=False)
        handpos = palm_at(c, tc, pos - 0); handpos = R @ (MS * ((W[hi] @ np.array([0.0861, 0.02, -0.0561, 1.0]))[:3] * [-1, 1, 1] * mm.norm + mm.off)) + pos
        if t >= 644: handHead = handpos + [0, 1.7, 0]; headTarget = walkHead * (1 - sm(644, 652, t)) + handHead * sm(644, 652, t)
        OBJS.append(('marker_hand', np.array([handpos + d for d in ([-1, -1, -1], [1, -1, -1], [0, 1, 1])]), np.array([[0, 1, 2]]), (1, 0, 0)))
    # Slone's tank (10:19 on) + Slone
    if 612 <= t < 650:
        tf = max(0, t - 636); tOut = Out(SLONE_S); tpos = np.array([60.0, Stair(Q_END)[1] + 0.2, 61.0]) + (tOut * (14 * tf) + [0, -0.5 * 30 * tf * tf, 0] if t >= 636 else 0); th = Yaw(Q_END)
        put('tank', M['tank'], tpos, th, 2.0, 'Animation', t, color=(0.3, 0.55, 0.35)); put('slone', M['slone'], tpos + [0, 4.0, 0], th + 180, 1.3, 'H_STalk', t % 6, color=(0.9, 0.6, 0.3))
    # Jones / Foundation
    lead = 8.0; jq = q + DQ(lead, q); fq = q + DQ(4, q); jyaw = Yaw(min(max(jq, -1), 0.99)); jp = PathAt(jq); fp = PathAt(fq); lat = yawm(jyaw) @ [1, 0, 0]; jp = jp + lat * 1.2; fp = fp - lat * 1.6
    zdir = ZPC - Stair(SLONE_S); zyaw = math.degrees(math.atan2(zdir[0], zdir[2])); face = sm(646, 652, t); a = (jyaw + 180) % 360 - 180; b2 = (zyaw + 180) % 360 - 180; d = ((b2 - a + 180) % 360) - 180; jyaw = jyaw + d * face
    jk = min(max((t - 663.3) / 3.2, 0), 1)
    if t >= 663.3: zn = zdir / np.linalg.norm(zdir); jp = jp * (1 - jk * jk) + (ZPC - zn * 6) * jk * jk + [0, 10 * math.sin(jk * math.pi), 0]; fp = fp * (1 - jk * jk) + (ZPC - zn * 6 + lat * 3) * jk * jk + [0, 8 * math.sin(jk * math.pi), 0]
    if t < 666.8:
        put('jones', M['jones'], jp, jyaw, 1.0, 'H_Aim', t % 12, color=(0.9, 0.5, 0.5)); put('foundation', M['foundation'], fp, jyaw, 1.0, 'H_Aim', t % 12, color=(0.5, 0.9, 0.5))
    OBJS.append(('player', np.array([headTarget + d for d in ([-0.25, -1.7, -0.25], [0.25, -1.7, -0.25], [0.25, -1.7, 0.25], [-0.25, -1.7, 0.25], [-0.25, 0.1, -0.25], [0.25, 0.1, -0.25], [0.25, 0.1, 0.25], [-0.25, 0.1, 0.25])]),
                 np.array([[0, 1, 2], [0, 2, 3], [4, 6, 5], [4, 7, 6], [0, 4, 5], [0, 5, 1], [1, 5, 6], [1, 6, 2], [2, 6, 7], [2, 7, 3], [3, 7, 4], [3, 4, 0]]), (1, 0.1, 0.1)))
    # obj
    with open(out + '.obj', 'w') as f:
        n0 = 0
        for name, V, F, col in OBJS:
            f.write('o %s\n' % name)
            for v in V: f.write('v %.3f %.3f %.3f\n' % tuple(v))
            for tr in F: f.write('f %d %d %d\n' % (tr[0] + 1 + n0, tr[1] + 1 + n0, tr[2] + 1 + n0))
            n0 += len(V)
    print('headTarget', np.round(headTarget, 1), 'q', round(q, 3), 'jones', np.round(jp, 1), 'found', np.round(fp, 1), 'L4', np.round(L4(), 1))
    for name, V, F, col in OBJS: print(' %-12s bbox %s .. %s' % (name, np.round(V.min(0), 1), np.round(V.max(0), 1)))
    # camera views: player view looking at the target, plus overviews
    cams = cam or {}
    json.dump({'head': list(headTarget), 'cols': {o[0]: o[3] for o in OBJS}, 'view': cams, 'zp': list(zpos), 'L4': list(L4())}, open(out + '.json', 'w'))
    subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe', '-b', '-P', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'scene_render.py'), '--', out + '.obj', out + '.json', out], capture_output=True)
if __name__ == '__main__':
    t = float(sys.argv[1]); cams = {}
    for a in sys.argv[3:]:           # name:cx,cy,cz:tx,ty,tz  (Unity axes; 'head' as camera = the player's head)
        n, c, tg = a.split(':'); cams[n] = (c, [float(x) for x in tg.split(',')])
    run(t, sys.argv[2], cams)
