"""py prev_parts.py model.glb out_prefix  -> skins every mesh node of a (rest-pose) skinned glb separately (numpy, exact glTF skinning) and renders head close-ups with parts shown / hidden"""
import sys, os, subprocess, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, out = sys.argv[1:3]; g, b = slim_glb.read_glb(src); N = g['nodes']
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
W = {}
def w(i):
    if i in W: return W[i]
    n = N[i]
    if 'matrix' in n: M = np.array(n['matrix'], float).reshape(4, 4).T
    else: M = np.eye(4); M[:3, :3] = q2m(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); M[:3, 3] = n.get('translation', [0, 0, 0])
    W[i] = w(par[i]) @ M if i in par else M; return W[i]
parts = {}
for ni, n in enumerate(N):
    if 'mesh' not in n or 'skin' not in n: continue
    sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    for pr in g['meshes'][n['mesh']]['primitives']:
        at = pr['attributes']; P = acc(at['POSITION']); JI = acc(at['JOINTS_0']).astype(int); WT = acc(at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1); o = np.zeros((len(P), 3))
        M = np.array([w(J[j]) @ ibm[j] for j in range(len(J))])
        for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
        parts['n%d' % ni] = (o, acc(pr['indices']).astype(int).reshape(-1, 3))
for k, (v, f) in parts.items(): print('PART', k, len(v), 'y', round(v[:, 1].min(), 3), round(v[:, 1].max(), 3), 'x', round(v[:, 0].min(), 3), round(v[:, 0].max(), 3), 'z', round(v[:, 2].min(), 3), round(v[:, 2].max(), 3))
with open(out + '.obj', 'w') as fo:
    base = 0
    for k, (v, f) in parts.items():
        fo.write('o %s\n' % k)
        for p in v: fo.write('v %.4f %.4f %.4f\n' % tuple(p))
        for t in f: fo.write('f %d %d %d\n' % tuple(t + 1 + base))
        base += len(v)
print('obj', out + '.obj')
