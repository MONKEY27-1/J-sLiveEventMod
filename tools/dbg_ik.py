import sys, math; sys.path.insert(0,'.')
import real_monster as M
from rig import *
ch=[c for c in M.CHAINS if c[0]=='leg_l'][0]
for t in (22.6,23.0,23.5,24.0,24.5,25.0):
    m=t+M.OFFSET; q,tr,pel=M.sample_m(m)
    G=M.chain_globals(q,tr,pel,ch); E=M.gpos2(G,ch[3]); hip=M.gpos2(G,ch[1]); kn=M.gpos2(G,ch[2]); tip=M.gpos2(G,ch[4])
    l1=M.vlen(M.vsub(kn,hip)); l2=M.vlen(M.vsub(E,kn))
    corr=[-0.5, 0.0, 0.0]
    q2=dict(q); M.reach(G,q2,ch,corr)
    G2=M.chain_globals(q2,tr,pel,ch); E2=M.gpos2(G2,ch[3]); tip2=M.gpos2(G2,ch[4])
    print(t,'E',[round(x,3) for x in E],'-> E2',[round(x,3) for x in E2],'delta',[round(E2[i]-E[i],3) for i in range(3)],'want',corr,'| reach l1 %.2f l2 %.2f |hip-E| %.2f ; ball delta'%(l1,l2,M.vlen(M.vsub(E,hip))),[round(tip2[i]-tip[i],3) for i in range(3)])
