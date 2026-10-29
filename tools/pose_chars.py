"""blender -b -P pose_chars.py -- <cyclo|jones> <in.glb> <out.glb>
Gives the two supplied Epic skins (no animation in the files) a short looping animation, built with dance_lib's pose helpers:
 cyclo : clip 'Hover' (8 s) - arms flung out like the key art, slow body sway, head tilts (Midas inside the orb)
 jones : clip 'Talk'  (8 s) - standing, small head turns and nods, one hand now and then gesturing (the office scenes)
The character's own up / left / forward axes are measured from the rest pose (the files are Y-up or Z-up, and Blender turns them again); the loop is closed by the pose parameters being periodic."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dance_lib import *
from mathutils import Vector
which, src, out = sys.argv[sys.argv.index('--') + 1:][:3]
FPS = 12; DUR = 8.0
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][-1]; arm.animation_data_clear()
for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
names = dict(pelvis='pelvis', neck='neck_01', head='head', uarm_l='upperarm_l', farm_l='lowerarm_l', hand_l='hand_l', uarm_r='upperarm_r', farm_r='lowerarm_r', hand_r='hand_r', thigh_l='thigh_l', calf_l='calf_l', foot_l='foot_l', thigh_r='thigh_r', calf_r='calf_r', foot_r='foot_r')
pbs = {k: None for k in names}
def find(prefix):
    for pb in arm.pose.bones:
        if pb.name == prefix or pb.name.startswith(prefix + '_') or pb.name.startswith(prefix):
            if 'twist' in pb.name or 'end' in pb.name or 'ik' in pb.name.split('_')[0]: continue
            return pb
    raise KeyError(prefix)
bpy.context.view_layer.update(); mw = arm.matrix_world
def pos(n): return (mw @ find(n).matrix).translation
up = (pos('head') - pos('pelvis')).normalized(); left = (pos('thigh_l') - pos('thigh_r')); left = (left - up * left.dot(up)).normalized(); fwd = left.cross(up).normalized()
scale = (pos('head') - pos('pelvis')).length / 0.65                                  # the Epic skeleton: pelvis -> head is about 0.65 m
print('axes up', tuple(round(x, 2) for x in up), 'left', tuple(round(x, 2) for x in left), 'fwd', tuple(round(x, 2) for x in fwd), 'scale', round(scale, 3))
rig = Rig(arm, names, tuple(up), tuple(fwd), tuple(left), scale); chain = ['spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05']
for k in chain: rig.pb[k] = rig.find(k)
rig.n['spine_chain'] = chain; keyed = list(dict.fromkeys([rig.pb[k] for k in rig.pb]))
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_viewport = True
ARM = {'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'fling': ((0.9, 0.3, 0.35), (1.0, 0.5, 0.45)), 'fling2': ((0.8, 0.5, 0.1), (0.9, 0.7, 0.2)), 'rest': ((0.25, 0.05, -0.95), (0.2, 0.5, -0.85)),
       'gesture': ((0.35, 0.5, -0.55), (0.25, 0.9, -0.05)), 'point': ((0.3, 0.6, -0.4), (0.15, 1.0, 0.0)), 'hands': ((0.3, 0.45, -0.7), (-0.35, 0.8, -0.35))}
def S(t, k, ph=0.0): return math.sin((t / DUR * k + ph) * 2 * math.pi)
def pose_at(t):
    if which == 'cyclo':
        a = S(t, 1); b = S(t, 2, 0.1)
        return {'arm_l': ARM['fling'] if a > -0.2 else ARM['fling2'], 'arm_r': ARM['fling2'] if a > -0.2 else ARM['fling'], 'lean': 0.12 + 0.08 * a, 'twist': 0.25 * S(t, 1, 0.25), 'roll': 0.1 * S(t, 1, 0.5), 'hip': (0.0, 0.0, 0.04 * b),
                'nod': 0.15 + 0.1 * b, 'turn': 0.35 * S(t, 1, 0.1), 'tilt': 0.15 * S(t, 2), 'leg_l': (0.35, 0.1), 'leg_r': (0.45, -0.1), 'knee_l': 0.35, 'knee_r': 0.15 + 0.1 * a}
    g = max(0.0, S(t, 2, 0.0)); h = max(0.0, S(t, 3, 0.4))
    return {'arm_l': ARM['gesture'] if g > 0.6 else ARM['rest'], 'arm_r': ARM['point'] if h > 0.75 else ARM['hands'] if h > 0.3 else ARM['rest'], 'lean': 0.02 * S(t, 2), 'twist': 0.06 * S(t, 1), 'roll': 0.0, 'hip': (0.0, 0.0, 0.0),
            'nod': 0.12 * S(t, 3, 0.2) + 0.05, 'turn': 0.45 * S(t, 1, 0.1), 'tilt': 0.08 * S(t, 2, 0.3), 'leg_l': (0.0, 0.0), 'leg_r': (0.0, 0.0), 'knee_l': 0.02, 'knee_r': 0.02}
rec = []
arm.animation_data_create(); arm.animation_data.action = None
for f in range(int(DUR * FPS)): pose(rig, pose_at(f / FPS)); rec.append([(tuple(pb.rotation_quaternion), tuple(pb.location)) for pb in keyed])
rec.append(rec[0])                                                    # the loop closes on its first frame
act = bpy.data.actions.new('Hover' if which == 'cyclo' else 'Talk'); arm.animation_data.action = act
for f, row in enumerate(rec):
    for pb, (q, l) in zip(keyed, row): pb.rotation_quaternion = q; pb.location = l; pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f)
arm.animation_data.action = None
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_viewport = False
sc = bpy.context.scene; sc.render.fps = FPS; sc.frame_start = 0; sc.frame_end = int(DUR * FPS)
tr = arm.animation_data.nla_tracks.new(); tr.name = act.name; st = tr.strips.new(act.name, 0, act); st.name = act.name
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=True, export_animation_mode='NLA_TRACKS', export_force_sampling=True, export_optimize_animation_size=True, export_image_format='AUTO', export_frame_range=False)
print('wrote', out, os.path.getsize(out) // 1024, 'KB')
