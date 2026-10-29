import sys; sys.path.insert(0,'.')
import monster_events as ME
print('t   pelvis | spine_03 | head | eye | hand_l | hand_r')
for t in [200,206,215,225,235,238,240,242,243,244,245,246,247,247.5,248,248.5,249,250,254,258,260,262,265,270,275,280,285,290,295,300]:
    f=lambda b:'(%4.0f %4.0f %4.0f)'%tuple(ME.at(b,t))
    print('%5.1f'%t,f('pelvis'),f('spine_03'),f('head'),f('eye'),f('hand_l'),f('hand_r'))
