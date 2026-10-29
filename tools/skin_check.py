"""python skin_check.py a.glb [clip frame]  exact glTF skinning in numpy (what a Unity loader does): vertex displacement of the first frame of animation 0 against the rest pose -> largest moves per mesh."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
g, b = slim_glb.read_glb(sys.argv[1]); FR = int(sys.argv[2]) if len(sys.argv) > 2 else 0; N = g['nodes']
CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(i):
    a = g['accessors'][i]; v = g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(b, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; arr = np.ascontiguousarray(raw).view(dt).reshape(a['count'], n)
    else: arr = np.frombuffer(b, dt, a['count'] * n, off).reshape(a['count'], n)
    if a.get('normalized'): arr = arr / float(np.iinfo(dt).max)
    return arr.astype(np.float64)
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
def worlds(anim_frame):
    T = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; R = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]
    if anim_frame is not None and g.get('animations'):
        an = g['animations'][int(os.environ.get('CLIP', '0'))]
        for ch in an['channels']:
            s = an['samplers'][ch['sampler']]; v = acc(s['output']); k = min(anim_frame, len(v) - 1); n = ch['target']['node']; p = ch['target']['path']
            if p == 'rotation': R[n] = v[k]
            elif p == 'translation': T[n] = v[k]
            else: S[n] = v[k]
    W = {}
    def w(i):
        if i in W: return W[i]
        if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T
        else: M = np.eye(4); M[:3, :3] = q2m(R[i]) @ np.diag(S[i]); M[:3, 3] = T[i]
        W[i] = w(par[i]) @ M if i in par else M; return W[i]
    return [w(i) for i in range(len(N))]
W0 = worlds(None); W1 = worlds(FR)
for ni, n in enumerate(N):
    if 'mesh' not in n or 'skin' not in n: continue
    sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    for pr in g['meshes'][n['mesh']]['primitives']:
        at = pr['attributes']
        if 'JOINTS_0' not in at: continue
        P = acc(at['POSITION']); JI = acc(at['JOINTS_0']).astype(int); WT = acc(at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1)
        def skin(W):
            out = np.zeros((len(P), 3))
            for k in range(4):
                M = np.array([W[J[j]] @ ibm[j] for j in range(len(J))]); mv = np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]; out += WT[:, k:k + 1] * mv
            return out
        a0 = skin(W0); a1 = skin(W1); d = np.linalg.norm(a1 - a0, axis=1); print(n['name'], 'verts', len(P), 'max move', round(float(d.max()), 3), 'rest-vs-bind error', round(float(np.linalg.norm(a0 - P, axis=1).max()), 3), ' >20% of height:', int((d > 0.2 * (P[:, 1].max() - P[:, 1].min())).sum()))
for key in ('hand_l', 'hand_r', 'head_0', 'foot_l'):
    i = next((i for i, n in enumerate(N) if n['name'].lower().startswith(key)), None)
    if i is None: continue
    p = next(i2 for i2, n in enumerate(N) if n['name'].lower().startswith('pelvis')); print('  ', key, 'rest', np.round(W0[i][:3, 3] - W0[p][:3, 3], 2), 'frame', np.round(W1[i][:3, 3] - W1[p][:3, 3], 2))
