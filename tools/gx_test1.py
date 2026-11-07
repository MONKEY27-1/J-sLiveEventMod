import bpy, sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gx_view, gx_fk
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "prev2")
sc, arm, rig, cam = gx_view.open_rig()
dn = np.array([0, 0, -1.0])
poses = {
 'rest': {},
 'armsdown': {'aim': {'UpperArmL': ((0.1, 0, -1), 'Chest'), 'ForeArmL': ((0.1, -0.15, -1), 'Chest'), 'UpperArmR': ((-0.1, 0, -1), 'Chest'), 'ForeArmR': ((-0.1, -0.15, -1), 'Chest')}},
 'armsup': {'aim': {'UpperArmL': ((1, 0, 0.2), 'Chest'), 'ForeArmL': ((1, 0, 0.2), 'Chest'), 'UpperArmR': ((-1, 0, 0.2), 'Chest'), 'ForeArmR': ((-1, 0, 0.2), 'Chest')}},
 'armsfwd': {'aim': {'UpperArmL': ((0.2, -1, 0), 'Chest'), 'ForeArmL': ((0.1, -1, 0), 'Chest'), 'UpperArmR': ((-0.2, -1, 0), 'Chest'), 'ForeArmR': ((-0.1, -1, 0), 'Chest')}},
 'bend': {'rot': {'Spine': (25, 0, 0), 'Chest': (30, 0, 0), 'Neck': (-15, 0, 0), 'Head': (-20, 0, 0)}, 'root': (0, -2, -2)},
 'twist': {'rot': {'Spine': (0, 0, 20), 'Chest': (0, 0, 30), 'Head': (0, 0, -20)}},
 'kneel': {'ik': {'ThighL': {'target': (11, -10, -22), 'pole': (0, -1, 0)}, 'ThighR': {'target': (-6, 6, -33), 'pole': (0, -1, 0)}}, 'root': (0, 0, -9), 'rot': {'FootL': (0, 0, 0)}},
}
paths = []
for nm, sp in poses.items():
    gx_fk.apply(arm, rig.solve(sp)); bpy.context.view_layer.update(); p = os.path.join(OUT, 't1_' + nm + '.png'); gx_view.shoot(sc, cam, p, (0, 0, 0), 80, 'front'); paths.append(p)
    p2 = os.path.join(OUT, 't1s_' + nm + '.png'); gx_view.shoot(sc, cam, p2, (0, 0, 0), 80, 'side'); paths.append(p2)
gx_view.sheet(paths, os.path.join(OUT, 't1_sheet.png'), cols=6, labels=[n for n in poses for _ in (0, 1)])
print('DONE')
