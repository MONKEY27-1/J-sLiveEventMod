import bpy, sys, math
a=sys.argv[sys.argv.index('--')+1:]; glb=a[0]; act=a[1]; times=[float(x) for x in a[2:]]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
sc=bpy.context.scene; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
ac=[x for x in bpy.data.actions if x.name==act][0]; arm.animation_data_create(); arm.animation_data.action=ac
print('fps',sc.render.fps,'armature scale',tuple(arm.scale),'loc',tuple(arm.location))
def bone(prefix):
    for b in arm.pose.bones:
        if b.name.startswith(prefix): return b
for t in times:
    sc.frame_set(int(round(t*sc.render.fps))); bpy.context.view_layer.update()
    out=[]
    for p in ('pelvis_03','head_0','hand_l_0','foot_l_0'):
        b=bone(p); w=arm.matrix_world@b.head; out.append((p,[round(x,2) for x in w]))
    print(t,out)
