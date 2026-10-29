"""python anim_inject.py in.glb out.glb [armsign] [fix_up]
Adds a looping 'Talk' clip (8 s, 12 fps) straight to the ORIGINAL Agent Jonesy glb (the Blender round trip scales his pelvis by 100: the skin explodes). Small rotations are put on top of each bone's REST rotation:
the head turns and nods, the spine sways, the arms gesture now and then. Axes (up / left / forward) are measured from the rest pose in the file, so the clip does not depend on bone roll. The file is Z-up with no root
rotation, so the root node is turned -90 about x (like fix_orient.py) to stand the model up in a Y-up scene."""
import sys, os, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, dst = sys.argv[1], sys.argv[2]; ASIGN = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
g, b = slim_glb.read_glb(src); b = bytearray(b); N = g['nodes']
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
def m2q(m):
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0: s = math.sqrt(t + 1) * 2; q = [(m[2, 1] - m[1, 2]) / s, (m[0, 2] - m[2, 0]) / s, (m[1, 0] - m[0, 1]) / s, s / 4]
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]: s = math.sqrt(1 + m[0, 0] - m[1, 1] - m[2, 2]) * 2; q = [s / 4, (m[0, 1] + m[1, 0]) / s, (m[0, 2] + m[2, 0]) / s, (m[2, 1] - m[1, 2]) / s]
    elif m[1, 1] > m[2, 2]: s = math.sqrt(1 + m[1, 1] - m[0, 0] - m[2, 2]) * 2; q = [(m[0, 1] + m[1, 0]) / s, s / 4, (m[1, 2] + m[2, 1]) / s, (m[0, 2] - m[2, 0]) / s]
    else: s = math.sqrt(1 + m[2, 2] - m[0, 0] - m[1, 1]) * 2; q = [(m[0, 2] + m[2, 0]) / s, (m[1, 2] + m[2, 1]) / s, s / 4, (m[1, 0] - m[0, 1]) / s]
    q = np.array(q); return q / np.linalg.norm(q)
def axis_angle(ax, a): ax = np.asarray(ax, float) / np.linalg.norm(ax); s = math.sin(a / 2); return np.array([ax[0] * s, ax[1] * s, ax[2] * s, math.cos(a / 2)])
par = {}
for i, n in enumerate(N):
    for c in n.get('children', []): par[c] = i
Wm = {}
def world(i):
    if i in Wm: return Wm[i]
    n = N[i]; M = np.eye(4); M[:3, :3] = q2m(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); M[:3, 3] = n.get('translation', [0, 0, 0]); Wm[i] = world(par[i]) @ M if i in par else M; return Wm[i]
def wrot(i): m = world(i)[:3, :3]; return m / np.linalg.norm(m, axis=0)
def find(prefix):
    for i, n in enumerate(N):
        s = n['name'].lower()
        if s.startswith(prefix) and 'twist' not in s and 'end' not in s and 'ik' != s[:2]: return i
    raise KeyError(prefix)
H, P, TL, TR = find('head_0'), find('pelvis'), find('thigh_l'), find('thigh_r')
up = world(H)[:3, 3] - world(P)[:3, 3]; up /= np.linalg.norm(up); left = world(TL)[:3, 3] - world(TR)[:3, 3]; left -= up * left.dot(up); left /= np.linalg.norm(left); fwd = np.cross(left, up)
print('axes (file space) up', np.round(up, 2), 'left', np.round(left, 2), 'fwd', np.round(fwd, 2))
bones = {k: find(k) for k in ('head_0', 'neck_01', 'neck_02', 'spine_02', 'spine_04', 'upperarm_l', 'lowerarm_l', 'upperarm_r', 'lowerarm_r', 'clavicle_l', 'clavicle_r')}
rest = {k: np.array(N[i].get('rotation', [0, 0, 0, 1]), float) for k, i in bones.items()}
def local_delta(i, ax, ang):
    """a rotation by `ang` about the WORLD axis `ax`, expressed on top of node i's local rotation (the parent's world rotation turns the axis into the parent's frame)"""
    Rp = wrot(par[i]); axl = Rp.T @ np.asarray(ax, float); return axis_angle(axl, ang)
def qmul(a, b):
    x1, y1, z1, w1 = a; x2, y2, z2, w2 = b; return np.array([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2])
FPS, DUR = 12, 8.0; NF = int(FPS * DUR) + 1; T = np.arange(NF) / FPS
def S(t, k, ph=0.0): return math.sin((t / DUR * k + ph) * 2 * math.pi)
tracks = {k: np.zeros((NF, 4)) for k in bones}
for f, t in enumerate(T):
    g1 = max(0.0, S(t, 2)) ** 2; g2 = max(0.0, S(t, 3, 0.4)) ** 2
    for k, i in bones.items():
        q = np.array([0, 0, 0, 1.0])
        def add(ax, ang): nonlocal_q.append(local_delta(i, ax, ang))
        nonlocal_q = []
        if os.environ.get('MODE') == 'cyclo':
            RS = float(os.environ.get('RSIGN', '1')); a = S(t, 1); b2 = S(t, 2, 0.1)
            if k == 'head_0': add(up, 0.35 * S(t, 1, 0.1)); add(left, 0.12 + 0.1 * b2); add(fwd, 0.15 * S(t, 2))
            elif k == 'neck_02': add(left, 0.1)
            elif k == 'spine_02': add(left, 0.12 + 0.08 * a); add(up, 0.25 * S(t, 1, 0.25)); add(fwd, 0.1 * S(t, 1, 0.5))
            elif k == 'spine_04': add(left, 0.06 + 0.04 * a)
            elif k == 'upperarm_l': add(fwd, RS * (0.9 + 0.25 * a)); add(left, ASIGN * 0.35)
            elif k == 'lowerarm_l': add(left, ASIGN * (0.3 + 0.2 * b2))
            elif k == 'upperarm_r': add(fwd, -RS * (0.9 - 0.25 * a)); add(left, ASIGN * 0.35)
            elif k == 'lowerarm_r': add(left, ASIGN * (0.3 - 0.2 * b2))
        elif k == 'head_0': add(up, 0.5 * S(t, 1, 0.1)); add(left, 0.12 * S(t, 3, 0.2) + 0.05); add(fwd, 0.08 * S(t, 2, 0.3))
        elif k == 'neck_01': add(up, 0.2 * S(t, 1, 0.1))
        elif k == 'neck_02': add(up, 0.4 * S(t, 1, 0.1)); add(left, 0.1 * S(t, 3, 0.2) + 0.04)
        elif k == 'spine_02': add(up, 0.07 * S(t, 1)); add(fwd, 0.03 * S(t, 2))
        elif k == 'spine_04': add(left, 0.03 * S(t, 2))
        elif k == 'upperarm_l': add(left, ASIGN * 0.9 * g1); add(fwd, 0.0)
        elif k == 'lowerarm_l': add(left, ASIGN * 1.1 * g1)
        elif k == 'upperarm_r': add(left, ASIGN * 0.7 * g2)
        elif k == 'lowerarm_r': add(left, ASIGN * 0.9 * g2)
        for d in nonlocal_q: q = qmul(d, q)
        tracks[k][f] = qmul(q, rest[k]) if False else qmul(q, rest[k])
        tracks[k][f] /= np.linalg.norm(tracks[k][f])
def add_accessor(arr, typ, comp=5126, minmax=False):
    arr = np.ascontiguousarray(arr, np.float32); off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(arr.tobytes()); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}); a = {'bufferView': len(g['bufferViews']) - 1, 'componentType': comp, 'count': len(arr), 'type': typ}
    if minmax: a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
    g['accessors'].append(a); return len(g['accessors']) - 1
tin = add_accessor(T, 'SCALAR', minmax=True); samplers = []; channels = []
for k, i in bones.items():
    if k.startswith('clavicle') or (os.environ.get('ONLY') and k not in os.environ['ONLY'].split(',')): continue
    out = add_accessor(tracks[k], 'VEC4'); samplers.append({'input': tin, 'output': out, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': i, 'path': 'rotation'}})
g.setdefault('animations', []).append({'name': 'Hover' if os.environ.get('MODE') == 'cyclo' else 'Talk', 'samplers': samplers, 'channels': channels}); g['buffers'][0]['byteLength'] = len(b)
root = next(i for i, n in enumerate(N) if n['name'] == 'Sketchfab_model')
if len(sys.argv) <= 4 or sys.argv[4] != 'noup': N[root]['rotation'] = [-0.70710678, 0, 0, 0.70710678]
g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb); print(dst, os.path.getsize(dst) // 1024, 'KB', len(channels), 'channels')
