import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from moves import *

S = Scene(seed=7)
t = 30.0; end = 60.0
order = ['mic_rap', 'bounce', 'point_crowd', 'jump', 'fist_pump', 'rock', 'arms_wide', 'crouch_hype', 'step_touch', 'stomp', 'head_bang', 'chest_beat']
sched = []
for nm in order:
    fn = GROUND[nm][0]; t0 = t; t = fn(S, t, S.rng); sched.append((nm, round(t0, 1), round(t, 1)))
print(sched)
t_end = t
frames = sample_scene(S, 30.0, t_end)
print("frames", len(frames))
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_choreo1.glb", frames, fps=FPS, name='test_choreo1')
import json; json.dump(sched, open(r"C:\Users\drago\AppData\Local\Temp\rp\test_choreo1.json", 'w'))
