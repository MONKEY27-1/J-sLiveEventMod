import sys; sys.path.insert(0,'.')
import monster_events as ME
print('t   pelvis | hand_l | hand_r | head | spine_03')
for t in [155.5,156.5,157.5,158.5,159.5,160.5,161.0,161.5,162,163,164,166,168,170,172,174,176,178,180,181,182,183,184]:
    f=lambda b:'(%4.0f %4.0f %4.0f)'%tuple(ME.at(b,t))
    print('%5.1f'%t,f('pelvis'),f('hand_l'),f('hand_r'),f('head'),f('spine_03'))
print('jaw/dir at blasts:')
for t in (73.0,73.6,74.0,74.4,75.0,76.0,77.1,78.0,79.0):
    i=int(round(t/ME.DT)); g,hit=ME.ground_hit(i); print(t,'jaw',ME.BM['jaw'][i],'dir',ME.BM['dir'][i],'ground',[round(v) for v in g],hit,'mouth',[round(v) for v in ME.BM['mouth'][i]])
