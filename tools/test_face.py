import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from body import *
frames = []
# 0: neutral, 1: jaw +20, 2: jaw -20 (sign test), 3: lip corners pulled / puckered test
def pose(jaw=0, lipc=0, brow=0):
    P = copy_pose(BASE_P)
    if jaw: apply_local(P, 'C_jaw', rx(jaw))
    if lipc:
        apply_local(P, 'L_lip_corner', rz(lipc)); apply_local(P, 'R_lip_corner', rz(-lipc))
    if brow:
        apply_local(P, 'L_brow_outer', rx(brow)); apply_local(P, 'R_brow_outer', rx(brow)); apply_local(P, 'C_brow_mid', rx(brow))
    return (P, [BASE_DPEL[0], BASE_DPEL[1], BASE_DPEL[2]])
frames = [pose(), pose(jaw=20), pose(jaw=-20), pose(jaw=20, lipc=25), pose(brow=20)]
write_preview(r"C:\Users\drago\AppData\Local\Temp\rp\test_face.glb", frames, fps=1.0, name='test_face')
