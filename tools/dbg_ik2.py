import sys, math; sys.path.insert(0,'.')
import real_monster as M
from rig import *
ms,cw,cx,cz,ks,_=M.build_path(M.OFFSET,M.OFFSET+M.DUR)
ch=[c for c in M.CHAINS if c[0]=='leg_l'][0]
n=len(ms); W=int(5.5*M.FPS)
def off_at(i):
    q,tr,pel=M.sample_m(ms[i]); G=M.chain_globals(q,tr,pel,ch); Rp=M.rot3(G[PEL]); hip=M.gpos2(G,ch[1]); E=M.gpos2(G,ch[3]); return M.mv(M.m3T(Rp),M.vsub(E,hip)),E,hip,Rp,q,tr,pel
i0=int(round(61.0-M.OFFSET+M.OFFSET)*M.FPS) if False else int(round((61.0-M.OFFSET)*M.FPS))
seg=(61.0,85.0); lo=int(round((seg[0]-M.OFFSET)*M.FPS)); hi=int(round((seg[1]-M.OFFSET)*M.FPS))+1
print('segment frames',lo,hi,'ms',ms[lo],ms[hi-1])
offs=[off_at(i)[0] for i in range(lo,hi)]
neutral=[sum(o[c] for o in offs)/len(offs) for c in range(3)]
print('neutral local',[round(x,3) for x in neutral])
ux,uz=-0.998,0.062
for t in (22.9,23.2,23.5,23.8,24.1,24.4,24.7,25.0,25.3):
    i=int(round((t+M.OFFSET)*M.FPS))-0  # global index uses ms from OFFSET; ms[i]=OFFSET+i/FPS -> i=t*FPS
    i=int(round(t*M.FPS))
    o,E,hip,Rp,q,tr,pel=off_at(i)
    rel=M.vsub(E,M.vadd(hip,M.mv(Rp,neutral))); a=rel[0]*ux+rel[2]*uz
    k=M.k_at(ms[i])[0]
    corr_a=-(1-k)*a
    c_a=cw[i][0]*ux+cw[i][2]*uz; cp_a=cx[i]*ux+cz[i]*uz
    print('t %.1f  E_a %.3f hip_a %.3f rel_a %.3f  c_a %.3f  cprime_a %.3f  k %.2f   E_final_a = E_a + (c\'-c) + corr = %.3f'%(t,E[0]*ux+E[2]*uz,hip[0]*ux+hip[2]*uz,a,c_a,cp_a,k,(E[0]*ux+E[2]*uz)+(cp_a-c_a)+corr_a))
