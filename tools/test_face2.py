import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from body import *
print({b: [round(x, 3) for x in sg['nodes'][N(b)].get('translation', [0,0,0])] for b in ('L_lip_corner','C_brow_mid','L_brow_outer','L_eye_lid_upper_mid','C_lip_upper_mid','C_lip_lower_mid','L_cheek_inner','C_jaw')})
frames = []
def mk(bone=None, d=(0, 0, 0)):
    P = copy_pose(BASE_P)
    return (P, BASE_DPEL, {bone: list(d)} if bone else {})
tests = [None, ('L_lip_corner', (1.0, 0, 0)), ('L_lip_corner', (0, 1.0, 0)), ('L_lip_corner', (0, 0, 1.0)),
         ('L_eye_lid_upper_mid', (0, 0.4, 0)), ('L_eye_lid_upper_mid', (0, -0.4, 0)), ('L_eye_lid_upper_mid', (0.4, 0, 0)), ('L_eye_lid_upper_mid', (0, 0, 0.4)),
         ('C_brow_mid', (0, 0.6, 0)), ('C_brow_mid', (0.6, 0, 0)), ('C_brow_mid', (0, 0, 0.6))]
frames = [mk(b, d) if b else mk() for b, d in [(t if t else (None, (0,0,0))) for t in tests]]
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_face2.glb", frames, fps=1.0, name='test_face2')
