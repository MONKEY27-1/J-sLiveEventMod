import sys, os
sys.path.insert(0, os.getcwd())
from glbmath import *
for path in (r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", r"C:\Users\drago\Downloads\travis_scott_fortnite_event_animation_1.glb"):
    g, b = load_glb(path); sk = g['skins'][0]
    ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
    neg = [g['nodes'][ji]['name'] for k, ji in enumerate(sk['joints']) if det3([[ibm[k][r][c] for c in range(3)] for r in range(3)]) < 0]
    negs = [g['nodes'][ji]['name'] for ji in sk['joints'] if any(x < 0 for x in g['nodes'][ji].get('scale', [1,1,1]))]
    sc = sorted(set(round(x, 3) for ji in sk['joints'] for x in g['nodes'][ji].get('scale', [1,1,1])))
    print(os.path.basename(path), "IBM with negative determinant:", len(neg), neg[:6], "| nodes with negative scale:", len(negs), negs[:6], "| distinct scales:", sc[:8])
