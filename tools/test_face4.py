import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *
fr = []
def mk(up, lo, smile=0.0):
    p = Pose(); p.head(pitch=-2)
    for sd in 'LR':
        p.tr['%s_eye_lid_upper_mid' % sd] = [0, up, 0]; p.tr['%s_eye_lid_lower_mid' % sd] = [0, lo, 0]
        if smile: p.tr['%s_lip_corner' % sd] = [0, -smile, 0]
    return p.frame_tuple()
for up, lo in ((0, 0), (0.5, 0), (1.0, 0), (1.5, 0), (2.0, 0), (2.5, 0), (1.5, -0.5), (1.5, 0.5), (2.0, -1.0), (2.5, -1.0), (3.5, -1.0), (0, 0)): fr.append(mk(up, lo))
fr[-1] = mk(0, 0, 2.0)
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_face4.glb", fr, fps=1.0, name='test_face4')
