import sys, math; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
for b in ('eye','jaw_01','FX_mouthLaser','head','neck_01','spine_03','pelvis','foot_l','ball_l','hand_l'):
    if b in s_name: print(b,[round(x,3) for x in BP[N(b)]])
d=vsub(BP[N('FX_mouthLaser')],BP[N('head')]); print('mouth-head',[round(x,3) for x in d])
d2=vsub(BP[N('ball_l')],BP[N('foot_l')]); print('ball-foot',[round(x,3) for x in d2])
