"""py hand_check.py <clip> <time> <out>   the mech alone in a clip (scale 1 of the loader's 1.8 units): renders the right hand from above / the side / the front and prints the palm point (hand bone local) so the plugin can carry you on it."""
import sys, os, numpy as np, subprocess, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scene_check as sc
clip, tc, out = sys.argv[1], float(sys.argv[2]), sys.argv[3]
m = sc.Model('mech'); W, R, pos = sc.put('mech', m, (0, 0, 0), 0, 1.0, clip, tc, color=(0.6, 0.75, 0.95))
hp = m.node_local(W, 'hand_l_014'); print('hand (model-local, loader units)', np.round(hp, 3))
# the hand's own vertices (skin weight on the hand_r bone or any finger bone), posed
g = m.g; N = m.N; hi = next(i for i, n in enumerate(N) if n['name'].startswith('hand_l_014'))
def sub(i): return [i] + [x for c in N[i].get('children', []) for x in sub(c)]
HS = set(sub(hi)); pts = []
for ni, n in enumerate(N):
    if 'mesh' not in n or 'skin' not in n: continue
    sk = g['skins'][n['skin']]; J = np.array(sk['joints']); ibm = m.acc(sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    for pr in g['meshes'][n['mesh']]['primitives']:
        at = pr['attributes']; P = m.acc(at['POSITION']); JI = m.acc(at['JOINTS_0']).astype(int); WT = m.acc(at['WEIGHTS_0']); Ph = np.concatenate([P, np.ones((len(P), 1))], 1); o = np.zeros((len(P), 3)); MM = np.array([W[J[j]] @ ibm[j] for j in range(len(J))])
        for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', MM[JI[:, k]], Ph)[:, :3]
        wh = sum(WT[:, k] * np.isin(J[JI[:, k]], list(HS)) for k in range(4)); pts.append(o[wh > 0.5])
HV = np.concatenate(pts) * [-1, 1, 1] * m.norm + m.off                        # hand vertices, model-local Unity coordinates
Wh = W[hi]; Rh = Wh[:3, :3] / np.linalg.norm(Wh[:3, :3], axis=0); NL = float(os.environ.get('LSIGN', '1')) * np.array([0.244, -0.442, 0.863]); NL /= np.linalg.norm(NL); nw = Rh @ NL * [-1, 1, 1]       # palm normal in Unity axes
c = HV.mean(0); th = (HV - c) @ nw; surf = c + nw * th.max(); print('palm normal (Unity)', np.round(nw, 3), 'palm up-ness', round(float(nw[1]), 2), 'thickness', round(float(th.max() - th.min()), 3), 'hand extent', np.round(HV.max(0) - HV.min(0), 3))
cgl = np.array([-(c[0] - m.off[0]) / m.norm, (c[1] - m.off[1]) / m.norm, (c[2] - m.off[2]) / m.norm]); sg = np.array([-(surf[0] - m.off[0]) / m.norm, (surf[1] - m.off[1]) / m.norm, (surf[2] - m.off[2]) / m.norm])
l1 = np.linalg.inv(Wh) @ np.append(cgl, 1.0); l2 = np.linalg.inv(Wh) @ np.append(sg, 1.0); print('palm centre, node space -> Unity local', np.round([-l1[0], l1[1], l1[2]], 4), ' palm SURFACE point ->', np.round([-l2[0], l2[1], l2[2]], 4))
c = surf
OBJS = sc.OBJS; OBJS.append(('player', np.array([c + d for d in ([-0.02, 0.0, -0.02], [0.02, 0.0, -0.02], [0.02, 0.0, 0.02], [-0.02, 0.0, 0.02], [-0.02, 0.09, -0.02], [0.02, 0.09, -0.02], [0.02, 0.09, 0.02], [-0.02, 0.09, 0.02])]), np.array([[0, 1, 2], [0, 2, 3], [4, 6, 5], [4, 7, 6], [0, 4, 5], [0, 5, 1], [1, 5, 6], [1, 6, 2], [2, 6, 7], [2, 7, 3], [3, 7, 4], [3, 4, 0]]), (1, 0.1, 0.1)))
with open(out + '.obj', 'w') as f:
    n0 = 0
    for name, VV, F, col in OBJS:
        f.write('o %s\n' % name); [f.write('v %.4f %.4f %.4f\n' % (v[0], v[1], v[2])) for v in VV]; [f.write('f %d %d %d\n' % (t[0] + 1 + n0, t[1] + 1 + n0, t[2] + 1 + n0)) for t in F]; n0 += len(VV)
head = list(c + [0, 0.1, 0]); cams = {'top': ('%f,%f,%f' % (c[0], c[1] + 0.9, c[2] + 0.001), list(c)), 'side': ('%f,%f,%f' % (c[0] + 0.9, c[1] + 0.2, c[2]), list(c)), 'front': ('%f,%f,%f' % (c[0], c[1] + 0.2, c[2] + 0.9), list(c)), 'body': ('%f,%f,%f' % (c[0] + 2.2, c[1] + 0.6, c[2] + 2.2), [0, 0.7, 0])}
json.dump({'head': head, 'cols': {'mech': (0.6, 0.75, 0.95), 'player': (1, 0.1, 0.1)}, 'view': cams, 'zp': [0, 0, 0], 'L4': [0, 0, 0]}, open(out + '.json', 'w'))
subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe', '-b', '-P', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'scene_render.py'), '--', out + '.obj', out + '.json', out], capture_output=True)
