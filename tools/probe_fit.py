import sys, math; sys.path.insert(0,'.')
import real_monster as M
ms,cw,cx,cz,ks,seg_u=M.build_path(M.OFFSET,M.OFFSET+M.DUR)
# unity-clip offsets (mirror x), relative to the anchor m=100
pts=[]
for i in range(len(ms)):
    t=i/M.FPS
    hidden = 128.9<t<156.5
    pts.append((t,-cx[i]*M.SC, cz[i]*M.SC,hidden))
def box(delta, ref_t):
    r=math.radians(delta); c,s=math.cos(r),math.sin(r)
    xs=[];zs=[]
    for t,x,z,h in pts:
        if h: continue
        X=x*c+z*s; Z=-x*s+z*c; xs.append(X); zs.append(Z)
    return min(xs),max(xs),min(zs),max(zs)
for d in range(0,360,15):
    a,b,c_,e=box(d,0); print('DELTA %3d  x[%6.0f,%6.0f] (%4.0f)  z[%6.0f,%6.0f] (%4.0f)'%(d,a,b,b-a,c_,e,e-c_))
