import sys, math; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
G=s_G0[s_par[PEL]]
def wpel(raw): return [sum(G[r][c]*v for c,v in enumerate(list(raw)+[1])) for r in range(3)]
def hdg(v): return math.degrees(math.atan2(-v[0], v[2]))   # unity heading (mirror x)
print('bind facing check, RB head', [[round(x,2) for x in r] for r in RB[N('head')]])
off=0
for act in rc.ORDER:
    d=rc.DUR[act]; t=0.0
    while t<d:
        m=off+t
        if int(m)%6==0 or True:
            P,raw=rc.act_pose(act,t)
            fp=mv(P[PEL],[0,0,-1]); fh=mv(P[N('head')],[0,0,-1]); fs=mv(P[N('spine_03')],[0,0,-1])
            print('%6.1f %-12s pelHdg %5.0f  spineHdg %5.0f headHdg %5.0f headPitch(y) %5.2f   headFwd(unity) (%5.2f %5.2f %5.2f)'%(m,act[7:],hdg(fp),hdg(fs),hdg(fh),fh[1],-fh[0],fh[1],fh[2]))
        t+=6.0
    off+=d
