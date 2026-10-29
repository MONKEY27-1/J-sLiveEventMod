import sys, os, math
sys.path.insert(0, os.getcwd())
from glbmath import *
def vlen(v): return math.sqrt(sum(x*x for x in v))
for path in (r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", r"C:\Users\drago\Downloads\travis_scott_fortnite_event_animation_1.glb"):
    g, b = load_glb(path); sk = g['skins'][0]; order, par = order_of(g)
    ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
    idx = {ji: k for k, ji in enumerate(sk['joints'])}
    W = {ji: inv4(ibm[k]) for ji, k in idx.items()}      # bind world (mesh space), joint frame
    ratios = []; angs = []
    for ji in sk['joints']:
        p = par[ji]
        if p in W:
            d = [W[ji][r][3] - W[p][r][3] for r in range(3)]
            Rp = rot_of(W[p]); dl = [sum(Rp[c][r] * d[c] for c in range(3)) for r in range(3)]   # Rp^T d  (bind-frame offset)
            t = g['nodes'][ji].get('translation', [0,0,0])
            if vlen(t) > 1e-6 and vlen(d) > 1e-9:
                ratios.append(vlen(d) / vlen(t))
                cosv = sum(a * b2 for a, b2 in zip(dl, t)) / (vlen(dl) * vlen(t)); angs.append(math.degrees(math.acos(max(-1, min(1, cosv)))))
    ratios.sort(); angs.sort()
    print(os.path.basename(path), "bone length ratio (bind/node) min %.4g median %.4g max %.4g" % (ratios[0], ratios[len(ratios)//2], ratios[-1]), "| offset direction error deg: median %.2f p90 %.2f max %.2f" % (angs[len(angs)//2], angs[int(len(angs)*0.9)], angs[-1]))
