"""python fix_up.py a.glb [b.glb ...]   (edits in place) The baked dancers come out of Blender lying down: the exporter turns the armature's own up axis into whatever it was in Blender. This reads the REST pose
of each file (hips -> head), and sets the rotation of the root node ('Sketchfab_model') to the smallest turn that makes that vector point up (+Y), so the plugin stands the model on its feet."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
def q2m(q):
    x, y, z, w = q; return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)], [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)], [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
def fix(fn):
    g, b = slim_glb.read_glb(fn); N = g['nodes']; par = {}
    for i, n in enumerate(N):
        for c in n.get('children', []): par[c] = i
    root = next(i for i, n in enumerate(N) if n['name'] == 'Sketchfab_model'); rot0 = N[root].get('rotation')
    N[root]['rotation'] = [0, 0, 0, 1]; W = {}
    def world(i):
        if i in W: return W[i]
        n = N[i]; M = np.eye(4); M[:3, :3] = q2m(n.get('rotation', [0, 0, 0, 1])) @ np.diag(n.get('scale', [1, 1, 1])); M[:3, 3] = n.get('translation', [0, 0, 0])
        W[i] = world(par[i]) @ M if i in par else M; return W[i]
    def find(pred):
        for i, n in enumerate(N):
            if pred(n['name'].lower()): return i
    hips = find(lambda s: s.startswith('mixamorig:hips') or s.startswith('pelvis')); head = find(lambda s: (s.startswith('mixamorig:head_') or s.startswith('head_0') or s == 'head') and 'end' not in s and 'top' not in s)
    U = world(head)[:3, 3] - world(hips)[:3, 3]; U = U / np.linalg.norm(U); h = 0.70710678
    cands = [[0, 0, 0, 1.0], [h, 0, 0, h], [-h, 0, 0, h], [1.0, 0, 0, 0], [0, 0, h, h], [0, 0, -h, h]]                  # nothing, +-90 about x / z, 180 about x: the turns an exporter makes between up axes
    q = max(cands, key=lambda c: float((q2m(c) @ U)[1]))                                                        # the one that leaves the hips -> head vector most upright
    N[root]['rotation'] = q; slim_glb.write_glb(fn, g, b)
    print('%s: %s -> %s (up before %s)' % (os.path.basename(fn), rot0, np.round(q, 3).tolist(), np.round(U, 2).tolist()))
for f in sys.argv[1:]: fix(f)
