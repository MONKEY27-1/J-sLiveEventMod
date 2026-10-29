import bpy, json, os, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Documents\AstronomicalEventMod\tools\cache\showdown\robot.glb")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; arm.animation_data_create(); arm.animation_data.action=list(bpy.data.actions)[0]
tr=json.load(open(r"C:\Users\drago\Documents\AstronomicalEventMod\tools\cache\showdown\robot_trace.json"))
sc=bpy.context.scene
print('fps',sc.render.fps, 'action range', list(bpy.data.actions)[0].frame_range)
for t in (70,125,175,250):
    sc.frame_set(int(round(t*sc.render.fps))); bpy.context.view_layer.update()
    for nm in ('pelvis_02','head_078','hand_l_011'):
        b=arm.pose.bones.get(nm)
        if b is None: print('nobone',nm,[x.name for x in arm.pose.bones if x.name.startswith('head')][:3]); continue
        w=arm.matrix_world@b.head; print(t,nm,[round(v) for v in w])
    i=int(t/tr['dt']); print('  trace',{k:[round(v) for v in tr['b'][k][i]] for k in ('pelvis','head','hand_l')})
