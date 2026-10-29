import sys, os, pickle
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from face import *
S = pickle.load(open(os.path.join(HERE, 'cache', 'choreo_scene.pkl'), 'rb'))
import json
log = json.load(open(os.path.join(HERE, 'cache', 'choreo_log.json')))['log']
def move_at(t):
    for a, b, n, s in log:
        if a <= t < b: return n
    return '-'
from collections import Counter
byMove = Counter(); worst = {}; tot = 0
for k in range(0, int(500 * 24), 5):
    t = k / 24.0; before = len(CLAMPS.get('over', []))
    build_pose(S, t)
    ov = CLAMPS.get('over', [])
    for o in ov[before:]:
        m = move_at(t); byMove[m] += 1; worst[m] = max(worst.get(m, 0), o); tot += 1
print('clamped leg-frames in', 500 * 24 // 5, 'frames:', tot)
for m, c in byMove.most_common(14): print(m, c, 'worst overshoot %.0f units (%.3f body heights)' % (worst[m], worst[m] / BODY_H))
