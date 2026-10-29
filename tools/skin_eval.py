"""Evaluate glTF linear-blend skinning (reference implementation) for a node pose, return vertices in global space."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glbmath import *

def skin_vertices(g, b, globals_by_node, mesh_index_list=None, stride=1):
    sk = g['skins'][0]; ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
    jm = [mmul(globals_by_node[ji], ibm[k]) for k, ji in enumerate(sk['joints'])]   # Gjoint * IBM  (mesh node global cancels)
    out = []
    for mi, mesh in enumerate(g['meshes']):
        p = mesh['primitives'][0]['attributes']
        pos = read_acc(g, b, p['POSITION']); jo = read_acc(g, b, p['JOINTS_0']); we = read_acc(g, b, p['WEIGHTS_0'])
        for v in range(0, len(pos), stride):
            x, y, z = pos[v]; acc = [0.0, 0.0, 0.0]
            for k in range(4):
                w = we[v][k]
                if w <= 0: continue
                M = jm[jo[v][k]]
                for r in range(3): acc[r] += w * (M[r][0] * x + M[r][1] * y + M[r][2] * z + M[r][3])
            out.append((mi, acc))
    return out

def node_globals(g, local_overrides=None):
    order, par = order_of(g); G = {}
    for i in order:
        L = (local_overrides or {}).get(i) or node_local(g['nodes'][i])
        G[i] = mmul(G[par[i]], L) if par[i] >= 0 else L
    return G

def bounds(verts):
    lo = [1e18] * 3; hi = [-1e18] * 3
    for _, a in verts:
        for r in range(3): lo[r] = min(lo[r], a[r]); hi[r] = max(hi[r], a[r])
    return lo, hi

if __name__ == '__main__':
    g, b = load_glb(sys.argv[1])
    G = node_globals(g)
    v = skin_vertices(g, b, G, stride=7)
    lo, hi = bounds(v)
    print("skinned bounds (node rest pose):", [round(x, 2) for x in lo], [round(x, 2) for x in hi], "size", [round(h - l, 2) for l, h in zip(lo, hi)])
    # bind pose: all joints at inverse(IBM) -> equals the raw vertices transformed by the mesh node global
    sk = g['skins'][0]; ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
    Gb = {ji: inv4(ibm[k]) for k, ji in enumerate(sk['joints'])}
    v2 = skin_vertices(g, b, Gb, stride=7); lo, hi = bounds(v2)
    print("skinned bounds (bind pose, = raw mesh coords):", [round(x, 2) for x in lo], [round(x, 2) for x in hi], "size", [round(h - l, 2) for l, h in zip(lo, hi)])
