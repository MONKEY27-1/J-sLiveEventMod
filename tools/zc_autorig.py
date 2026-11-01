"""blender -b -P zc_autorig.py -- in.glb out.glb target_tris height_m
Gives a STATIC humanoid mesh (T / A pose, facing glTF +z) a skeleton with Fortnite-style bone names (pelvis_, spine_01_ .. spine_05_, neck_01_, head_, upperarm_l_, lowerarm_l_, hand_l_, thigh_l_, calf_l_, foot_l_ (+ _r_))
so that zc_anim.py's `ue` profile can animate it. Steps: join the meshes (transforms applied), decimate, scale to height_m, estimate the joints from the bounds (the arm angle is read from the mesh), weight each
vertex to its nearest bone segments (inverse distance, top 3, arms kept off the torso, legs split left / right), export as a skinned GLB."""
import bpy, sys, math
import numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; src, out, tris, HM = a[0], a[1], int(a[2]), float(a[3])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
ms = [o for o in sc.objects if o.type == 'MESH']
for o in sc.objects: o.select_set(False)
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(sc.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if len(ms) > 1: bpy.ops.object.join()
ob = bpy.context.active_object; n0 = len(ob.data.polygons)
if tris > 0 and n0 > tris:
    md = ob.modifiers.new('d', 'DECIMATE'); md.ratio = tris / n0; bpy.ops.object.modifier_apply(modifier='d')
V = np.array([ob.matrix_world @ v.co for v in ob.data.vertices], np.float64)
H0 = V[:, 2].max() - V[:, 2].min(); k = HM / H0; V = (V - np.array([0, 0, V[:, 2].min()])) * k
cx = (V[:, 0].min() + V[:, 0].max()) / 2; cy = float(np.median(V[:, 1])); V[:, 0] -= cx; V[:, 1] -= cy; H = HM
ob.data.vertices.foreach_set('co', V.ravel()); ob.data.update()
# ---- joints (blender: x = the character's left, y = back, z = up; the character faces -y)
shz, shx = 0.80 * H, 0.115 * H
R = float(np.abs(V[:, 0]).max()); tipv = V[np.abs(V[:, 0]) > R * 0.96]; ztip = float(tipv[:, 2].mean()) if len(tipv) else shz
theta = float(np.clip(math.atan2(shz - ztip, max(0.05, R - shx)), 0.0, math.radians(70)))
L = (R - shx) / max(0.3, math.cos(theta)); d = np.array([math.cos(theta), 0.0, -math.sin(theta)])
print('AUTORIG', src.split('\\')[-1], 'height', round(H0, 3), '->', HM, 'tris', n0, '->', len(ob.data.polygons), 'reach', round(R, 3), 'arm angle deg', round(math.degrees(theta), 1))
hip, kn, an = 0.085 * H, 0.09 * H, 0.09 * H
J = {}
J['pelvis'] = (0, 0, 0.52 * H); J['s1'] = (0, 0, 0.565 * H); J['s2'] = (0, 0, 0.61 * H); J['s3'] = (0, 0, 0.655 * H); J['s4'] = (0, 0, 0.70 * H); J['s5'] = (0, 0, 0.75 * H); J['neck'] = (0, 0, 0.815 * H); J['head'] = (0, 0, 0.865 * H); J['headtip'] = (0, 0, H)
bones = []   # (name, head, tail, parent)
bones.append(('pelvis_00', J['pelvis'], J['s1'], None)); chain = ['pelvis_00']
names = ['spine_01_01', 'spine_02_02', 'spine_03_03', 'spine_04_04', 'spine_05_05']; pts = ['s1', 's2', 's3', 's4', 's5']; nxt = ['s2', 's3', 's4', 's5', 'neck']
for nm_, p, q in zip(names, pts, nxt): bones.append((nm_, J[p], J[q], chain[-1])); chain.append(nm_)
bones.append(('neck_01_06', J['neck'], J['head'], 'spine_05_05')); bones.append(('head_07', J['head'], J['headtip'], 'neck_01_06'))
for s_, sg in (('l', 1), ('r', -1)):
    sh = np.array([sg * shx, 0, shz]); el = sh + d * np.array([sg, 1, 1]) * (0.5 * L); wr = sh + d * np.array([sg, 1, 1]) * (0.82 * L); hd = sh + d * np.array([sg, 1, 1]) * L
    bones.append(('upperarm_%s_08' % s_, tuple(sh), tuple(el), 'spine_05_05')); bones.append(('lowerarm_%s_09' % s_, tuple(el), tuple(wr), 'upperarm_%s_08' % s_)); bones.append(('hand_%s_10' % s_, tuple(wr), tuple(hd), 'lowerarm_%s_09' % s_))
    th = (sg * hip, 0, 0.50 * H); kb = (sg * kn, 0.004 * H, 0.285 * H); ab = (sg * an, 0, 0.045 * H); tb = (sg * an, -0.07 * H, 0.0)
    bones.append(('thigh_%s_11' % s_, th, kb, 'pelvis_00')); bones.append(('calf_%s_12' % s_, kb, ab, 'thigh_%s_11' % s_)); bones.append(('foot_%s_13' % s_, ab, tb, 'calf_%s_12' % s_))
# ---- armature
arm_d = bpy.data.armatures.new('Armature'); arm_o = bpy.data.objects.new('Armature', arm_d); sc.collection.objects.link(arm_o); bpy.context.view_layer.objects.active = arm_o; bpy.ops.object.mode_set(mode='EDIT')
eb = {}
for nm_, h_, t_, p_ in bones:
    e = arm_d.edit_bones.new(nm_); e.head = Vector(h_); e.tail = Vector(t_)
    if (e.tail - e.head).length < 1e-4: e.tail = e.head + Vector((0, 0, 0.01))
    eb[nm_] = e
for nm_, h_, t_, p_ in bones:
    if p_: eb[nm_].parent = eb[p_]
bpy.ops.object.mode_set(mode='OBJECT')
# ---- weights: inverse distance to the bone segments (top 3), arms off the torso, legs split by side
S0 = np.array([b[1] for b in bones], np.float64); S1 = np.array([b[2] for b in bones], np.float64); nb = len(bones); W = np.zeros((len(V), nb))
for bi in range(nb):
    p0, p1 = S0[bi], S1[bi]; dv = p1 - p0; l2 = float(dv @ dv) + 1e-9; t = np.clip(((V - p0) @ dv) / l2, 0, 1); dist = np.linalg.norm(V - (p0 + t[:, None] * dv), axis=1); W[:, bi] = 1.0 / (dist + 0.012 * H) ** 4
for bi, (nm_, _, _, _) in enumerate(bones):
    if nm_.startswith(('upperarm', 'lowerarm', 'hand')):
        side = 1 if '_l_' in nm_ else -1; W[:, bi] *= ((V[:, 0] * side) > shx * 0.75) | ((V[:, 2] < 0.62 * H) & ((V[:, 0] * side) > shx * 0.5))
    if nm_.startswith(('thigh', 'calf', 'foot')):
        side = 1 if '_l_' in nm_ else -1; W[:, bi] *= (V[:, 0] * side) > -0.02 * H
    if nm_.startswith(('pelvis', 'spine', 'neck', 'head')):
        W[:, bi] *= (V[:, 2] > 0.30 * H) | (nm_.startswith('pelvis'))
top = np.argsort(-W, axis=1)[:, :3]; Wt = np.take_along_axis(W, top, 1); Wt = Wt / np.maximum(Wt.sum(1, keepdims=True), 1e-12)
vgs = [ob.vertex_groups.new(name=b[0]) for b in bones]
for i in range(len(V)):
    for j in range(3):
        if Wt[i, j] > 0.005: vgs[top[i, j]].add([i], float(Wt[i, j]), 'REPLACE')
md = ob.modifiers.new('Armature', 'ARMATURE'); md.object = arm_o; ob.parent = arm_o
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
print('AUTORIG done', out)
