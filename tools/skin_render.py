"""python skin_render.py in.glb clip_name frame out_prefix   exact glTF skinning in numpy (what the plugin does) for one clip frame, written as an OBJ (the posed mesh, no rig) and rendered front / side with Blender
(blender -b -P preview_static.py) when BLENDER_RENDER=1. Use it instead of Blender's own armature preview for the Epic rigs (the importer stretches them into spikes)."""
import sys, os, subprocess, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, clip, FR, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
g, b = slim_glb.read_glb(src); N = g['nodes']
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(i):
    a = g['accessors'][i]; v = g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(b, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
    return np.frombuffer(b, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
def worlds(frame):
    T = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; R = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]
    if frame is not None:
        an = next(a for a in g['animations'] if a['name'] == clip)
        for ch in an['channels']:
            s = an['samplers'][ch['sampler']]; v = acc(s['output']); k = min(frame, len(v) - 1); n = ch['target']['node']; p = ch['target']['path']
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
W1 = worlds(FR); verts = []; faces = []
for ni, n in enumerate(N):
    if 'mesh' not in n or 'skin' not in n: continue
    sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    for pr in g['meshes'][n['mesh']]['primitives']:
        at = pr['attributes']; P = acc(at['POSITION']); JI = acc(at['JOINTS_0']).astype(int); WT = acc(at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1); outp = np.zeros((len(P), 3))
        M = np.array([W1[J[j]] @ ibm[j] for j in range(len(J))])
        for k in range(4): outp += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
        base = sum(len(v) for v in verts); verts.append(outp); idx = acc(pr['indices']).astype(int).reshape(-1, 3); faces.append(idx + base)
V = np.concatenate(verts); F = np.concatenate(faces)
with open(out + '.obj', 'w') as f:
    for v in V: f.write('v %.4f %.4f %.4f\n' % (v[0], v[1], v[2]))
    for t in F: f.write('f %d %d %d\n' % (t[0] + 1, t[1] + 1, t[2] + 1))
print('written', out + '.obj', 'bbox', np.round(V.min(0), 2), np.round(V.max(0), 2))
