import sys, os, math
sys.path.insert(0, os.getcwd())
from glbmath import *
def vlen(v): return math.sqrt(sum(x*x for x in v))
path = r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"
g, b = load_glb(path); sk = g['skins'][0]; order, par = order_of(g)
ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
idx = {ji: k for k, ji in enumerate(sk['joints'])}
W = {ji: inv4(ibm[k]) for ji, k in idx.items()}
bad = []; good = 0
for ji in sk['joints']:
    p = par[ji]
    if p in W:
        d = [W[ji][r][3] - W[p][r][3] for r in range(3)]
        Rp = rot_of(W[p]); dl = [sum(Rp[c][r] * d[c] for c in range(3)) for r in range(3)]
        t = g['nodes'][ji].get('translation', [0,0,0])
        if vlen(t) > 1e-6 and vlen(d) > 1e-9:
            cosv = sum(a * b2 for a, b2 in zip(dl, t)) / (vlen(dl) * vlen(t)); ang = math.degrees(math.acos(max(-1, min(1, cosv))))
            if ang > 5: bad.append((g['nodes'][ji]['name'], g['nodes'][p]['name'], round(ang), round(vlen(d)/vlen(t),1)))
            else: good += 1
print("good", good, "bad", len(bad))
for x in bad[:40]: print(x)
