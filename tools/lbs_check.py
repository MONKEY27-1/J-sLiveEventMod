import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
p=Pose(); p.local('clavicle_l', rz(30)); p.local('upperarm_l', rz(40))
tg=make_targets(p.P); SG,q,pl=skin_fk(tg, vadd(PEL_B, p.frame_tuple()[1]))
SG0,q0,_=skin_fk(make_targets(Pose().P), PEL_B)
Jn=ssk['joints']
for mi,me in enumerate(sg['meshes']):
    pr=me['primitives'][0]; at=pr['attributes']
    if 'JOINTS_0' not in at: continue
    pos=read_acc(sg,sb,at['POSITION']); jo=read_acc(sg,sb,at['JOINTS_0']); we=read_acc(sg,sb,at['WEIGHTS_0'])
    mx=0; worst=None
    for k in range(0,len(pos),7):
        v=list(pos[k])+[1.0]; a=[0,0,0]; b=[0,0,0]
        for jj,ww in zip(jo[k],we[k]):
            if ww<=0: continue
            jn=Jn[int(jj)]; I=s_ibm[int(jj)]
            Mp=mmul(SG[jn], I); M0=mmul(SG0[jn], I)
            for r in range(3):
                a[r]+=ww*sum(Mp[r][c]*v[c] for c in range(4)); b[r]+=ww*sum(M0[r][c]*v[c] for c in range(4))
        d=vlen(vsub(a,b))
        if d>mx: mx=d; worst=(k,[(sg['nodes'][Jn[int(j)]]['name'][:20],round(w,2)) for j,w in zip(jo[k],we[k]) if w>0])
    print(me['name'], 'max displacement', round(mx,1), worst)
