"""blender -b -P zc_outliers.py -- in.glb action time : which vertices (and bones) fly away in that pose"""
import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, name, t = a[0], a[1], float(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; act = [x for x in bpy.data.actions if x.name.endswith(name)][0]
arm.animation_data_create(); arm.animation_data.action = act
try: arm.animation_data.action_slot = act.slots[0]
except Exception: pass
print('armature', arm.name, 'scale', tuple(arm.scale), 'parent', arm.parent.name if arm.parent else None, 'bones', len(arm.data.bones))
sc.frame_set(int(round(t * sc.render.fps)) + 1); dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    ev = o.evaluated_get(dg); vs = [ev.matrix_world @ v.co for v in ev.data.vertices]; c = sum(vs, mathutils.Vector()) / len(vs)
    d = sorted(((v - c).length, i) for i, v in enumerate(vs)); med = d[len(d) // 2][0]; far = [(x, i) for x, i in d if x > med * 6]
    gn = {g.index: g.name for g in o.vertex_groups}; cnt = {}
    for x, i in far:
        for g in o.data.vertices[i].groups: cnt[gn.get(g.group)] = cnt.get(gn.get(g.group), 0) + 1
    print(o.name, 'verts', len(vs), 'median r', round(med, 2), 'outliers', len(far), 'groups', sorted(cnt.items(), key=lambda kv: -kv[1])[:8])
bone = arm.pose.bones
for n in ('pelvis_', 'spine_01_'):
    b = [x for x in bone if x.name.startswith(n)][0]; print(b.name, 'rot', tuple(round(v, 3) for v in b.rotation_quaternion), 'loc', tuple(round(v, 3) for v in b.location), 'scale', tuple(round(v, 3) for v in b.scale))
