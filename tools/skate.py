import json, math, sys
tr=json.load(open('cache/showdown/monster_trace%s.json' % (sys.argv[2] if len(sys.argv)>2 else ''))); dt=tr['dt']; t0=float(sys.argv[1]) if len(sys.argv)>1 else 0.0
for b in ('ball_l','ball_r','hand_l','hand_r'):
    P=tr['b'][b]; pel=tr['b']['pelvis']; rows=[]
    for i in range(1,len(P)):
        y=(P[i][1]+P[i-1][1])/2; sp=math.hypot(P[i][0]-P[i-1][0],P[i][2]-P[i-1][2])/dt; ps=math.hypot(pel[i][0]-pel[i-1][0],pel[i][2]-pel[i-1][2])/dt
        rows.append((t0+i*dt,y,sp,ps))
    planted=[r for r in rows if r[1]<10.0]
    sl=[r for r in planted if r[2]>6.0]
    print(b,'frames',len(rows),'planted(y<10m)',len(planted),'planted but moving >6 m/s:',len(sl),'worst %.1f m/s at t=%.1f (pelvis %.1f m/s)'%(max((r[2] for r in planted),default=0),max(planted,key=lambda r:r[2])[0] if planted else 0,max(planted,key=lambda r:r[2])[3] if planted else 0), 'mean pelvis speed %.1f'%(sum(r[3] for r in rows)/len(rows)))
