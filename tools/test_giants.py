"""RIG_SKIN=... RIG_STRIP=.. python test_giants.py out.glb -> neutral + a few IK poses of a generic character."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *
out = sys.argv[1]
fr = []
def neutral():
    p = Pose()
    for sd in SIDES:
        S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 0.05 * BODY_H, -0.5 * BODY_H, 0.04 * BODY_H]))
    stand_legs(p); return p
fr.append(neutral().frame_tuple())
p = neutral(); p.pelvis(dy=-0.12); stand_legs(p, 1.6); fr.append(p.frame_tuple())
p = Pose(); stand_legs(p); p.spine(bend=25); p.head(pitch=15)
for sd in SIDES:
    S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 0.05 * BODY_H, -0.3 * BODY_H, 0.45 * BODY_H]))
fr.append(p.frame_tuple())
p = Pose(); stand_legs(p)
for sd in SIDES:
    S = p.pos('upperarm_' + sd); p.arm(sd, vadd(S, [sgn(sd) * 0.15 * BODY_H, 0.5 * BODY_H, 0.1 * BODY_H]))
if 'jaw_01' in s_name: p.local('jaw_01', rx(25))
fr.append(p.frame_tuple())
write_preview(out, fr, fps=1.0, name='test')
