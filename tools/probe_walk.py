import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
G=s_G0[s_par[PEL]]
def wpel(raw): return [sum(G[r][c]*v for c,v in enumerate(list(raw)+[1])) for r in range(3)]
for t in [x*1.0 for x in range(60,84,1)]:
    P,raw=rc.act_pose('Cattus_Act1A',t); w=wpel(raw); pos=fk_positions(P,w)
    f=lambda n:(round(pos[N(n)][0],2),round(pos[N(n)][1],2),round(pos[N(n)][2],2))
    print(t,'pel',f('pelvis'),'ballL',f('ball_l'),'ballR',f('ball_r'),'handL',f('hand_l'),'handR',f('hand_r'))
