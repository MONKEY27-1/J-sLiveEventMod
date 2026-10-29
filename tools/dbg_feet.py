import sys; sys.path.insert(0,'.')
import show_robot as R
from gcore import *
P=R.derive(R.anchors()); R.build(P)
for t in (100,120,215,232):
    ps=build_pose(R.S,t); f=lambda n:[round(v,1) for v in to_unity(ps.pos(n))]
    print(t,'pelvis',f('pelvis'),'foot_l',f('foot_l'),'foot_r',f('foot_r'),'ANKLE_REST_Y',ANKLE_REST_Y*KM)
    print('  ft_w',round(R.S.get('ft_w_l',t),2),round(R.S.get('ft_w_r',t),2),'lift',round(R.S.get('ft_lift_l',t),1),round(R.S.get('ft_lift_r',t),1),'root_dy',round(R.S.get('root_dy',t),2))
import pose
print('LEN',{k:round(v,1) for k,v in pose.LEN.items() if k.startswith(('th','sh'))})
ps=build_pose(R.S,100)
for sd in 'lr':
    for n in ('thigh_','calf_','foot_'): print(n+sd,[round(v/KM*0+x,1) for v,x in zip([1,1,1],ps.pos(n+sd))])
print('CLAMPS',pose.CLAMPS.get('leg'), pose.CLAMPS.get('over',[])[:5])
S=R.S
for t in (0,50,100,106,110):
    print(t,'home',[round(S.get('home_x',t)*KM*BODY_H,1),round(-S.get('home_z',t)*KM*BODY_H,1)],'ft_wx_l/z',round(S.get('ft_wx_l',t),1),round(S.get('ft_wz_l',t),1),'ft_wx_r/z',round(S.get('ft_wx_r',t),1),round(S.get('ft_wz_r',t),1),'ft_w',S.get('ft_w_l',t),'pel_yaw',round(S.get('pel_yaw',t),1))
