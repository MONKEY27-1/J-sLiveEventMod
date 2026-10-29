import sys, math; sys.path.insert(0,'.')
import real_monster as M
from rig import *
def circ(angs):
    x=sum(math.cos(math.radians(a)) for a in angs); y=sum(math.sin(math.radians(a)) for a in angs); return math.degrees(math.atan2(y,x))
def head_hdg(m):
    # use rc.act_pose (file-frame P deltas, bind forward +Z)
    for a,s0,d in M.SEG:
        if m<s0+d or a==M.SEG[-1][0]: break
    P,raw=M.rc.act_pose(a,min(m-s0,d)); f=mv(P[N('head')],[0,0,1]); fp=mv(P[PEL],[0,0,1]); return M.heading(f), M.heading(fp), f[1]
for name,(m0,m1) in {'walk-in':(62,84),'beam(1B jaw open)':(86,134),'1B 100-112':(100,112),'1B 112-122':(112,122),'1B 122-134':(122,134),'1C fight':(134,166),'2 stand':(205,224),'3A kneel':(250,283),'charge':(283,292),'3B':(304,336)}.items():
    hs=[head_hdg(m) for m in [m0+(m1-m0)*i/20 for i in range(21)]]
    print('%-20s head heading mean %5.0f  pelvis heading mean %5.0f  pitch(y) mean %5.2f  head hdgs %s'%(name,circ([h[0] for h in hs]),circ([h[1] for h in hs]),sum(h[2] for h in hs)/len(hs),[round(h[0]) for h in hs[::4]]))
