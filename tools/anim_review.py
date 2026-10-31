"""py anim_review.py Clip f1,f2,... out_prefix [front|side]   skins the clip on EVERY character (exact glTF skinning, numpy), renders one row per character (frames across) and stacks them into out_prefix_sheet.png so the whole cast can be compared together.
Characters: jones foundation slone guard gunnar scientist visitor (the installed clip sets: cache/collision + cache/theend2021)."""
import sys, os, subprocess, numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import slim_glb
BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
CAST = [('jones', 'cache/collision/jones.glb', 0.85), ('foundation', 'cache/collision/foundation.glb', 0.95), ('slone', 'cache/collision/slone.glb', 0.85), ('guard', 'cache/collision/guard.glb', 0.9), ('gunnar', 'cache/theend2021/gunnar.glb', 0.9), ('scientist', 'cache/theend2021/the_scientist.glb', 0.9), ('visitor', 'cache/theend2021/the_visitor.glb', 0.9)]
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(G, B_, i):
    a = G['accessors'][i]; v = G['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(B_, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
    return np.frombuffer(B_, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
class Char:
    def __init__(s, path, rifle):
        s.g, s.b = slim_glb.read_glb(path); s.N = s.g['nodes']; s.par = {}
        for i, n in enumerate(s.N):
            for c in n.get('children', []): s.par[c] = i
        s.GUNI = next(i for i, n in enumerate(s.N) if n.get('name') == 'Gun'); rg, rb = slim_glb.read_glb(rifle); rv = []; rf = []
        for n in rg['nodes']:
            if 'mesh' not in n: continue
            for pr in rg['meshes'][n['mesh']]['primitives']: base = sum(len(v) for v in rv); rv.append(acc(rg, rb, pr['attributes']['POSITION'])); rf.append(acc(rg, rb, pr['indices']).astype(int).reshape(-1, 3) + base)
        s.RV = np.concatenate(rv); s.RF = np.concatenate(rf); s.skins = []
        for ni, n in enumerate(s.N):
            if 'mesh' not in n or 'skin' not in n: continue
            sk = s.g['skins'][n['skin']]; J = sk['joints']; ibm = acc(s.g, s.b, sk['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
            for pr in s.g['meshes'][n['mesh']]['primitives']:
                at = pr['attributes']; P = acc(s.g, s.b, at['POSITION']); s.skins.append((J, ibm, np.concatenate([P, np.ones((len(P), 1))], 1), acc(s.g, s.b, at['JOINTS_0']).astype(int), acc(s.g, s.b, at['WEIGHTS_0']), acc(s.g, s.b, pr['indices']).astype(int).reshape(-1, 3)))
    def clip(s, name):
        an = next(a for a in s.g['animations'] if a['name'] == name); s.tracks = [(ch['target']['node'], ch['target']['path'], acc(s.g, s.b, an['samplers'][ch['sampler']]['output'])) for ch in an['channels']]
    def worlds(s, fr):
        N = s.N; T = [np.array(n.get('translation', [0, 0, 0]), float) for n in N]; R = [np.array(n.get('rotation', [0, 0, 0, 1]), float) for n in N]; S = [np.array(n.get('scale', [1, 1, 1]), float) for n in N]
        for node, path, v in s.tracks:
            k = min(fr, len(v) - 1)
            if path == 'rotation': R[node] = v[k]
            elif path == 'translation': T[node] = v[k]
            elif path == 'scale': S[node] = v[k]
        W = {}
        def w(i):
            if i in W: return W[i]
            if 'matrix' in N[i]: M = np.array(N[i]['matrix'], float).reshape(4, 4).T
            else: M = np.eye(4); M[:3, :3] = q2m(R[i]) @ np.diag(S[i]); M[:3, 3] = T[i]
            W[i] = w(s.par[i]) @ M if i in s.par else M; return W[i]
        return [w(i) for i in range(len(N))]
    def mesh(s, fr, gs, off, k=1.0):
        W1 = s.worlds(fr); vs = []; fs = []
        for J, ibm, Ph, JI, WT, F in s.skins:
            M = np.array([W1[J[j]] @ ibm[j] for j in range(len(J))]); o = np.zeros((len(Ph), 3))
            for k in range(4): o += WT[:, k:k + 1] * np.einsum('vij,vj->vi', M[JI[:, k]], Ph)[:, :3]
            fs.append(F + sum(len(v) for v in vs)); vs.append(o * k + off)
        Wg = W1[s.GUNI]; gv = (Wg[:3, :3] @ (s.RV * gs).T).T + Wg[:3, 3]; gv = gv * k + off
        return np.concatenate(vs), np.concatenate(fs), gv
if __name__ == '__main__':
    clip, frames, out = sys.argv[1:4]; view = sys.argv[4] if len(sys.argv) > 4 else 'front'; frames = [int(x) for x in frames.split(',')]; os.chdir(HERE); rows = []
    for name, path, gs in CAST:
        if os.environ.get('CAST') and name not in os.environ['CAST'].split(','): continue
        SP = 0.6 * 5.0
        c = Char(path, 'cache/collision/rifle.glb'); c.clip(clip); c.tracks, tr0 = [], c.tracks; V0 = c.mesh(0, gs, np.zeros(3))[0]; hh = float(V0[:, 1].max() - V0[:, 1].min()); k = 1.0 if hh < 12.0 else 5.0 / hh; print(name, 'rest height', round(hh, 2)); c.tracks = tr0; cv = []; cf = []; gv = []; gf = []
        for i, fr in enumerate(frames):
            V, F, G = c.mesh(fr, gs, (np.array([0, 0, i * SP]) if view == 'side' else np.array([i * SP, 0, 0])), k); cf.append(F + sum(len(x) for x in cv)); cv.append(V); gf.append(c.RF + sum(len(x) for x in gv)); gv.append(G)
        po = '%s_%s' % (out, name)
        with open(po + '.obj', 'w') as f:
            f.write('o char\n'); [f.write('v %.4f %.4f %.4f\n' % tuple(v)) for v in np.concatenate(cv)]; [f.write('f %d %d %d\n' % tuple(t + 1)) for t in np.concatenate(cf)]
            n0 = sum(len(x) for x in cv); f.write('o gun\n'); [f.write('v %.4f %.4f %.4f\n' % tuple(v)) for v in np.concatenate(gv)]; [f.write('f %d %d %d\n' % tuple(t + 1 + n0)) for t in np.concatenate(gf)]
        subprocess.run([BL, '-b', '-P', os.path.join(HERE, 'review_render.py'), '--', os.path.abspath(po + '.obj'), os.path.abspath(po), str(5.3 * k), str(SP * len(frames)), view], capture_output=True); rows.append(os.path.abspath('%s_%s.png' % (po, view))); print('row', name)
    subprocess.run([BL, '-b', '-P', os.path.join(HERE, 'prev_sheet.py'), '--', os.path.abspath(out + '_sheet.png'), '1'] + rows, capture_output=True); print('sheet', out + '_sheet.png')
