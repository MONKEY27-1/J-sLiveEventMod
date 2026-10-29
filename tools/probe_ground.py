import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
par=s_par[PEL]; G=s_G0[par]
print('PEL_B',[round(x,3) for x in PEL_B],'parent G0',[[round(v,3) for v in r] for r in G])
def wpel(raw): return [sum(G[r][c]*v for c,v in enumerate(list(raw)+[1])) for r in range(3)]
print('pel local bind', sg['nodes'][PEL].get('translation'), 'world of raw', wpel(sg['nodes'][PEL].get('translation')))
for act in rc.ORDER:
    d=rc.DUR[act]; 
    for t in [d*k/8 for k in range(9)]:
        P,raw=rc.act_pose(act,t); w=wpel(raw); pos=fk_positions(P,w)
        fy=min(pos[N('ball_l')][1],pos[N('ball_r')][1],pos[N('foot_l')][1],pos[N('foot_r')][1]); hy=pos[N('head')][1]
        print(act,round(t,1),'pel',[round(x,2) for x in w],'foot min y',round(fy,2),'head y',round(hy,2))
