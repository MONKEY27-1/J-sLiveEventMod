import sys, re
sys.path.insert(0,'.')
from glbmath import *
import math
def probe(path, strip, names):
    g,b=load_glb(path); order,par=order_of(g); sk=g['skins'][0]
    ibm=[from_colmajor(m) for m in read_acc(g,b,sk['inverseBindMatrices'])]
    idx={ji:k for k,ji in enumerate(sk['joints'])}
    def base(n,s):
        t=n.split('_')
        for _ in range(s):
            if len(t)>1 and t[-1].isdigit(): t=t[:-1]
        return '_'.join(t)
    nm={}
    for ji in sk['joints']: nm.setdefault(base(g['nodes'][ji]['name'],strip),ji)
    print(path.split('/')[-1], 'joints',len(sk['joints']))
    for n in names:
        if n not in nm: print('  missing',n); continue
        W=inv4(ibm[idx[nm[n]]]); print('  %-14s'%n,[round(W[r][3],2) for r in range(3)], 'colX',[round(W[r][0],2) for r in range(3)], 'scale', round(math.sqrt(sum(W[r][0]**2 for r in range(3))),3))
    # node chain above skeleton
    j0=sk['joints'][0]; chain=[]; i=j0
    while i in par and par[i]>=0: i=par[i]; chain.append((g['nodes'][i].get('name'), g['nodes'][i].get('rotation'), g['nodes'][i].get('scale'), g['nodes'][i].get('translation')))
    for c in chain: print('   chain',c)
    # mesh bounds
    return g
names=['pelvis','spine_01','head','neck_01','foot_l','foot_r','ball_l','hand_l','hand_r','upperarm_l','thigh_l','eye','jaw_01','tail','calf_l']


