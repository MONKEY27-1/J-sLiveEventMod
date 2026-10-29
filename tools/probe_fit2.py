import sys, math; sys.path.insert(0,'.')
import real_monster as M
for gk,wk,ck in ((0.5,0.22,0.15),(0.4,0.2,0.12),(0.35,0.2,0.12)):
    M.GROUND_K=gk
    M.KSEG[0]=(61.0,85.0,'ik',wk); M.KSEG[4]=(280.5,292.5,'ik',ck)
    ms,cw,cx,cz,ks,seg_u=M.build_path(M.OFFSET,M.OFFSET+M.DUR)
    for d in (150,165,180):
        r=math.radians(d); c,s=math.cos(r),math.sin(r); xs=[];zs=[]
        for i in range(len(ms)):
            t=i/M.FPS
            if 128.9<t<156.5: continue
            x=-cx[i]*M.SC; z=cz[i]*M.SC; xs.append(x*c+z*s); zs.append(-x*s+z*c)
        print('K %.2f walk %.2f charge %.2f DELTA %d  x[%5.0f,%5.0f] (%3.0f) z[%5.0f,%5.0f] (%3.0f)'%(gk,wk,ck,d,min(xs),max(xs),max(xs)-min(xs),min(zs),max(zs),max(zs)-min(zs)))
