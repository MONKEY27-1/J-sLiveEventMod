import sys, math; sys.path.insert(0,'.')
import real_monster as M
from rig import *
J=[N(n) for n in ('pelvis','spine_01','spine_03','spine_04','neck_01','head','upperarm_l','upperarm_r','lowerarm_l','lowerarm_r','thigh_l','thigh_r','calf_l','calf_r','jaw_01') if n in s_name]
def ang(a,b):
    d=abs(sum(x*y for x,y in zip(a,b))); return 2*math.degrees(math.acos(min(1,d)))
prev=None; rows=[]
for k in range(0,int((M.DUR)*5)):
    t=k/5.0; q,tr,pel=M.sample_m(t+M.OFFSET)
    if prev is not None: rows.append((t, sum(ang(q[j],prev[j]) for j in J)*5))   # deg/s summed over joints
    prev=q
# per 1 s windows: mean activity
print('audio sec: activity (deg/s sum over 15 joints) -- peaks flagged')
for s in range(150,262):
    seg=[r[1] for r in rows if s<=r[0]<s+1]; 
    if not seg: continue
    m=sum(seg)/len(seg); mx=max(seg)
    print('%3d %6.0f %s'%(s,m,'#'*int(m/40)))
