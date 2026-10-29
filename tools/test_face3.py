import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *
fr = []
def mk(**kw):
    p = Pose(); p.head(pitch=-2); p.face(**kw); return p.frame_tuple()
for kw in (dict(), dict(jaw=10), dict(jaw=20), dict(jaw=28), dict(blink=1), dict(blink=2.5), dict(blink=5), dict(brow=3), dict(brow=-3), dict(smile=3), dict(smile=-3), dict(jaw=14, smile=3)): fr.append(mk(**kw))
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_face3.glb", fr, fps=1.0, name='test_face3')
