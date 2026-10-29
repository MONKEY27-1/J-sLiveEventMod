"""py prev_h2.py in.glb Clip f1,f2,.. rifle.glb out_prefix [gun_scale]   skins the clip at the given frames side by side (numpy, exact glTF skinning) + the SCAR on the Gun node, then renders front and 3/4 views with Blender."""
import sys, os, subprocess, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, clip, frames, rif, out = sys.argv[1:6]; GS = float(sys.argv[6]) if len(sys.argv) > 6 else 0.85; frames = [int(x) for x in frames.split(',')]
g, b = slim_glb.read_glb(src); N = g['nodes']; rg, rb = slim_glb.read_glb(rif)
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(G, B_, i):
    a = G['accessors'][i]; v = G['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(B_, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
    return np.frombuffer(B_, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
an = next(a for a in g['animations'] if a['name'] == clip); tracks = [(ch['target']['node'], ch['target']['path'], acc(g, b, an['samplers'][ch['sampler']]['output'])) for ch in an['channels']]
GUNI = next(i for i, n in enumerate(N) if n.get('name') == 'Gun')
def worlds(fr):
    T = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; R = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]
    for node, path, v in tracks:
        k = min(fr, len(v) - 1)
        if path == 'rotation': R[node] = v[k]
        elif path == 'translation': T[node] = v[k]
    W = {}
    def w(i):
        if i in W: return W[i]
        if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T
        else: M = np.eye(4); M[:3, :3] = q2m(R[i]) @ np.diag(S[i]); M[:3, 3] = T[i]
        W[i] = w(par[i]) @ M if i in par else M; return W[i]
    return [w(i) for i in range(len(N))]
def skin(W1):
    vs = []; fs = []
    for ni, n in enumerate(N):
        if 'mesh' not in n or 'skin' not in n: continue
        sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(g, b, sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
        for pr in g['meshes'][n['mesh']]['primitives']:
            at = pr['attributes']; P = acc(g, b, at['POSITION']); JI = acc(g, b, at['JOINTS_0']).astype(int); WT = acc(g, b, at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1); o = np.zeros((len(P), 3))
            M = np.array([W1[J[j]] @ ibm[j] for j in range(len(J))])
            for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
            base = sum(len(v) for v in vs); vs.append(o); fs.append(acc(g, b, pr['indices']).astype(int).reshape(-1, 3) + base)
    return np.concatenate(vs), np.concatenate(fs)
rv = []; rf = []
for ni, n in enumerate(rg['nodes']):
    if 'mesh' not in n: continue
    for pr in rg['meshes'][n['mesh']]['primitives']:
        base = sum(len(v) for v in rv); rv.append(acc(rg, rb, pr['attributes']['POSITION'])); rf.append(acc(rg, rb, pr['indices']).astype(int).reshape(-1, 3) + base)
RV = np.concatenate(rv); RF = np.concatenate(rf)
cv = []; cf = []; gv = []; gf = []
for i, fr in enumerate(frames):
    W1 = worlds(fr); V, F = skin(W1); V = V + np.array([i * 1.3, 0, 0] if not os.environ.get('ROWZ') else [0, 0, i * 1.6]); cf.append(F + sum(len(x) for x in cv)); cv.append(V)
    Wg = W1[GUNI]; gvv = (Wg[:3, :3] @ (RV * GS).T).T + Wg[:3, 3] + np.array([i * 1.3, 0, 0] if not os.environ.get('ROWZ') else [0, 0, i * 1.6]); gf.append(RF + sum(len(x) for x in gv)); gv.append(gvv)
with open(out + '.obj', 'w') as f:
    f.write('o char\n'); [f.write('v %.4f %.4f %.4f\n' % tuple(v)) for v in np.concatenate(cv)]; CF = np.concatenate(cf); [f.write('f %d %d %d\n' % tuple(t + 1)) for t in CF]
    n0 = sum(len(x) for x in cv); f.write('o gun\n'); [f.write('v %.4f %.4f %.4f\n' % tuple(v)) for v in np.concatenate(gv)]; [f.write('f %d %d %d\n' % tuple(t + 1 + n0)) for t in np.concatenate(gf)]
print('obj', out + '.obj'); subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe', '-b', '-P', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'prev_rows.py'), '--', out + '.obj', out], capture_output=True)
