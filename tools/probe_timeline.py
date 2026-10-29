import sys, json; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
G=s_G0[s_par[PEL]]
def wpel(raw): return [sum(G[r][c]*v for c,v in enumerate(list(raw)+[1])) for r in range(3)]
print([n for n in s_name if 'jaw' in n or 'eye' in n or 'mouth' in n or 'tongue' in n][:20])
jawn=[n for n in s_name if n=='jaw_01' or n=='C_jaw' or n=='jaw']
print(jawn)
jj=N(jawn[0]) if jawn else None
off=0; rows=[]
for act in rc.ORDER:
    d=rc.DUR[act]; t=0.0
    while t<d:
        P,raw=rc.act_pose(act,t); w=wpel(raw); pos=fk_positions(P,w)
        hd=pos[N('head')]; hl=pos[N('hand_l')]; hr=pos[N('hand_r')]; 
        # jaw opening: angle of P[jaw] relative to P[head]
        import math
        ja=0
        if jj is not None:
            Rr=m3mul(m3T(P[N('head')]),P[jj]); ja=math.degrees(math.acos(max(-1,min(1,(Rr[0][0]+Rr[1][1]+Rr[2][2]-1)/2))))
        # up vector of the spine (uprightness) : P[spine_03] applied to bind up (0,1,0)
        up=mv(P[N('spine_03')],[0,1,0])
        rows.append((off+t,act,w,hd,hl,hr,ja,up))
        t+=2.0
    off+=d
for r in rows:
    print('%6.1f %-13s pel(%5.1f %4.1f %5.1f) head y%4.1f hand y L%4.1f R%4.1f jaw%4.0f spineUp%5.2f'%(r[0],r[1][7:],r[2][0],r[2][1],r[2][2],r[3][1],r[4][1],r[5][1],r[6],r[7][1]))
