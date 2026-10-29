"""Repairs the zeroed inverse-bind matrices of the real-event Cattus GLB (keeps every animation) -> cache/showdown/cattus_real_fixed.glb"""
import os, sys, json, struct
os.environ.update(RIG_SKIN=r'C:\Users\drago\Downloads\cattus_-_event_animations_-_fortnite.glb', RIG_STRIP='1', RIG_BODYH='1.6', RIG_KEEP_T='1', RIG_ALIAS='spine_05=spine_04')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
G = rig.sg; G['buffers'][0]['byteLength'] = len(rig.sb)
js = json.dumps(G, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
b = bytes(rig.sb)
while len(b) % 4: b += b'\0'
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache', 'showdown', 'cattus_real_fixed.glb')
open(out, 'wb').write(struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(b)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(b), 0x004E4942) + b)
print('wrote', out, len(b) // 1024 // 1024, 'MB', 'joints', len(rig.s_joint_list))
