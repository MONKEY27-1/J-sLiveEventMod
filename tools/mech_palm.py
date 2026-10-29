"""py mech_palm.py   measures the mech's right hand in the rest pose: finger direction, palm normal (PCA of the hand's skinned vertices) in the hand bone's local frame, and the palm centre."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.argv = ['x', 'cache/collision/mech.glb', 'M_Idle', '0', 'cache/collision/rifle.glb', 'x']
ns = {'__name__': 'm', '__file__': os.path.abspath('prev_h2.py')}; exec(open('prev_h2.py').read().split('rv = []')[0].replace("GUNI = next(i for i, n in enumerate(N) if n.get('name') == 'Gun')", 'GUNI = 0'), ns)
N = ns['N']; g = ns['g']; acc = ns['acc']; b = ns['b']; q2m = ns['q2m']; par = ns['par']
W = {}
def w(i):
    if i in W: return W[i]
    n = N[i]
    if 'matrix' in n: M = np.array(n['matrix'], float).reshape(4, 4).T
    else: M = np.eye(4); M[:3, :3] = q2m(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); M[:3, 3] = n.get('translation', [0, 0, 0])
    W[i] = w(par[i]) @ M if i in par else M; return W[i]
HN = sys.argv_hand if hasattr(sys,'argv_hand') else 'hand_l_014'
Wl = [w(i) for i in range(len(N))]
hand = next(i for i, n in enumerate(N) if n['name'].startswith(HN))
def sub(i): return [i] + [x for c in N[i].get('children', []) for x in sub(c)]
H = set(sub(hand)); print('hand subtree', len(H), [N[i]['name'] for i in list(H)[:8]])
pts = []; wts = []
for ni, n in enumerate(N):
    if 'mesh' not in n or 'skin' not in n: continue
    sk = g['skins'][n['skin']]; J = sk['joints']; ibm = acc(g, b, sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    for pr in g['meshes'][n['mesh']]['primitives']:
        at = pr['attributes']; P = acc(g, b, at['POSITION']); JI = acc(g, b, at['JOINTS_0']).astype(int); WT = acc(g, b, at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1); o = np.zeros((len(P), 3)); M = np.array([Wl[J[j]] @ ibm[j] for j in range(len(J))])
        for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
        wh = sum(WT[:, k] * np.isin(np.array(J)[JI[:, k]], list(H)) for k in range(4)); pts.append(o[wh > 0.5]); wts.append(wh[wh > 0.5])
V = np.concatenate(pts); print('hand verts', len(V), 'bbox', V.min(0), V.max(0))
wr = Wl[hand][:3, 3]; c = V.mean(0); u, s, vt = np.linalg.svd(V - c, full_matrices=False); print('wrist', wr, 'centroid', c, 'sv', s, '\naxes', vt)
fd = c - wr; fd /= np.linalg.norm(fd); print('finger dir (world rest)', np.round(fd, 3)); print('normal (smallest)', np.round(vt[2], 3), 'mid', np.round(vt[1], 3), 'major', np.round(vt[0], 3))
R0 = Wl[hand][:3, :3] / np.linalg.norm(Wl[hand][:3, :3], axis=0); print('hand rest rot\n', np.round(R0, 3)); print('fd local', np.round(R0.T @ fd, 3), 'n local', np.round(R0.T @ vt[2], 3), 'palm centroid local (glTF node space)', np.round(np.linalg.inv(Wl[hand]) @ np.append(c, 1), 4)[:3])
print('thickness along n', (V - c) @ vt[2] @ np.ones(1) if False else float(((V - c) @ vt[2]).max() - ((V - c) @ vt[2]).min()), 'extent major', float(((V - c) @ vt[0]).max() - ((V - c) @ vt[0]).min()))
