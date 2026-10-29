import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *
print("lengths", {k: round(v) for k, v in LEN.items()}, "ankle rest y", round(ANKLE_REST_Y))
frames = []
# 0 neutral
p = Pose(); stand_legs(p); frames.append(p.frame_tuple())
# 1 crouch (pelvis down), wide feet
p = Pose(); p.pelvis(dy=-0.12); stand_legs(p, 1.7); frames.append(p.frame_tuple())
# 2 spine forward bend + head down
p = Pose(); stand_legs(p); p.spine(bend=25); p.head(pitch=10); frames.append(p.frame_tuple())
# 3 twist + lean
p = Pose(); stand_legs(p); p.spine(twist=30, lean=10); p.head(yaw=-20); frames.append(p.frame_tuple())
# 4 hands forward at chest height (point at crowd)
p = Pose(); stand_legs(p)
for sd in SIDES:
    S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 1200, -300, 5000]))
frames.append(p.frame_tuple())
# 5 arms up high (grab stars)
p = Pose(); stand_legs(p)
for sd in SIDES:
    S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 1000, 5200, 1500]))
frames.append(p.frame_tuple())
# 6 right hand at mouth (mic), left arm wide
p = Pose(); stand_legs(p)
mouth = p.pos('C_jaw'); p.arm('r', vadd(mouth, [-700, -300, 1500]))
S = p.pos('upperarm_l'); p.arm('l', vadd(S, [5000, 600, 600]))
p.hand('r', curl=0.8, thumb=0.6); frames.append(p.frame_tuple())
# 7 one leg lifted (foot raised forward), arms out
p = Pose(); p.pelvis(dy=-0.03); stand_legs(p); p.leg('r', p.ankle_ground(BP[N('foot_r')][0] * 1.2, BP[N('foot_r')][2] + 3000, lift=2200), 0, -15)
for sd in SIDES:
    S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 5200, 300, 0]))
frames.append(p.frame_tuple())
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_pose1.glb", frames, fps=1.0, name='test_pose1')
