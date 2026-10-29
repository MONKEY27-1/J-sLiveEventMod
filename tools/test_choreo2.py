import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from moves2 import *

S = Scene(seed=11)
t = 100.0
seq = [('walk', lambda t: m_walk(S, t, S.rng, dx=0.35, dz=0.1, style='swagger')), ('lift', lambda t: lift_off(S, t, 0.55, 1.0)), ('float_hang', None), ('superman', None), ('reach_sky', None),
       ('float_spin', None), ('float_tuck', None), ('enter_seat', lambda t: enter_seat(S, t)), ('seat_rap', None), ('seat_conduct', None), ('land', lambda t: land(S, t, 1.0)),
       ('fin_charge', None), ('fin_roar', None), ('fin_slam', None), ('fin_pose', None)]
lib = {}; [lib.update(d) for d in (AIR, SEAT, SWIM, FINALE)]
sched = []
for nm, fn in seq:
    t0 = t
    t = fn(t) if fn else lib[nm][0](S, t, S.rng)
    sched.append((nm, round(t0, 1), round(t, 1)))
print(sched)
frames = sample_scene(S, 100.0, t)
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_choreo2.glb", frames, fps=FPS, name='test_choreo2')
json.dump(sched, open(r"C:\Users\drago\AppData\Local\Temp\rp\test_choreo2.json", 'w'))
