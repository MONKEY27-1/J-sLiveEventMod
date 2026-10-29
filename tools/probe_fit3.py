import sys, math; sys.path.insert(0,'.')
import real_monster as M
M.GROUND_K=0.3
M.KSEG[:]=[(61.0,85.0,'ik',0.15),(165.5,176.0,'air',0.1),(199.0,205.2,'air',0.1),(223.3,227.5,'air',0.2),(280.5,292.5,'ik',0.1),(329.0,334.0,'air',0.25)]
ms,cw,cx,cz,ks,_=M.build_path(M.OFFSET,M.OFFSET+M.DUR)
for d in (75,90,105):
    r=math.radians(d); c,s=math.cos(r),math.sin(r); pts=[]
    for i in range(len(ms)):
        t=i/M.FPS
        x=-cx[i]*M.SC; z=cz[i]*M.SC; pts.append((x*c+z*s,-x*s+z*c,t,(128.9<t<156.5)))
    vis=[p for p in pts if not p[3]]
    print('DELTA %d  x[%4.0f,%4.0f]  z[%4.0f,%4.0f]'%(d,min(p[0] for p in vis),max(p[0] for p in vis),min(p[1] for p in vis),max(p[1] for p in vis)))
    if d==90:
        for t in (0,19,30,40,56,74,90,104,120,124,129,157,161,176,183,190,206,240,248,260,280,292,303):
            p=pts[min(len(pts)-1,int(round(t*M.FPS)))]; print('   audio %4d  x %5.0f z %5.0f'%(t,p[0],p[1]))
