import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from glbmath import *

SKIN = r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"
ANIM = r"C:\Users\drago\Downloads\travis_scott_fortnite_event_animation_1.glb"

def base(name, strip):
    t = name.split('_')
    for _ in range(strip):
        if len(t) > 1 and t[-1].isdigit(): t = t[:-1]
    return '_'.join(t)

def rest_check(path, strip):
    g, b = load_glb(path); order, par = order_of(g); sk = g['skins'][0]
    ibm = read_acc(g, b, sk['inverseBindMatrices'])
    G = {}
    for i in order:
        L = node_local(g['nodes'][i]); G[i] = mmul(G[par[i]], L) if par[i] >= 0 else L
    mesh_nodes0 = [i for i, n in enumerate(g["nodes"]) if "skin" in n][0]
    dev = []; neg = 0
    for k, ji in enumerate(sk['joints']):
        prod = mmul(mmul(inv4(G[mesh_nodes0]), G[ji]), from_colmajor(ibm[k]))
        d = max(abs(prod[r][c] - (1.0 if r == c else 0.0)) for r in range(4) for c in range(4))
        dev.append(d)
        if det3(rot_of(G[ji])) < 0: neg += 1
    dev.sort()
    print(os.path.basename(path), "joints", len(sk['joints']), "rest*IBM deviation from identity: median %.4g p90 %.4g max %.4g; mirrored globals: %d" % (dev[len(dev) // 2], dev[int(len(dev) * 0.9)], dev[-1], neg))
    mesh_nodes = [i for i, n in enumerate(g['nodes']) if 'skin' in n]
    for i in mesh_nodes: print("  mesh node", g['nodes'][i]['name'], "global translation", [round(G[i][r][3], 3) for r in range(3)], "scale", [round(math.sqrt(sum(G[i][r][c] ** 2 for r in range(3))), 4) for c in range(3)])
    j0 = sk['joints'][0]; print("  root joint global scale", [round(math.sqrt(sum(G[j0][r][c] ** 2 for r in range(3))), 4) for c in range(3)])

rest_check(SKIN, 2)
rest_check(ANIM, 1)


