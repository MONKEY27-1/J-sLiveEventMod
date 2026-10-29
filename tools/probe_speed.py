import sys, math; sys.path.insert(0,'.')
import real_monster as M
N_=int((M.TOTAL-M.OFFSET)*10); 
prev=None; rows=[]
for i in range(int(M.OFFSET*10), int(M.TOTAL*10)):
    m=i/10.0; w=M.clip_pel(m)
    if prev: rows.append((m, math.hypot(w[0]-prev[0],w[2]-prev[2])/0.1, w[1]))
    prev=w
# smoothed over 0.5 s
sp=[r[1] for r in rows]; out=[]
for k in range(len(rows)):
    a=max(0,k-2); b=min(len(rows),k+3); out.append(sum(sp[a:b])/(b-a))
cur=None
for (m,_,y),s in zip(rows,out):
    if s>0.20:
        if cur is None: cur=[m,m,s,y]
        else: cur[1]=m; cur[2]=max(cur[2],s)
    else:
        if cur is not None: print('m %6.1f-%6.1f (audio %6.1f-%6.1f) peak speed %.2f u/s = %.0f m/s' % (cur[0],cur[1],cur[0]-43.6,cur[1]-43.6,cur[2],cur[2]*65)); cur=None
