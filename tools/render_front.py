import bpy, math, sys
GLB, OUTDIR, ACT = sys.argv[sys.argv.index('--')+1:][:3]
times = [float(x) for x in sys.argv[sys.argv.index('--')+4:]]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
sc = bpy.context.scene; fps = sc.render.fps
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = [a for a in bpy.data.actions if a.name.startswith(ACT)] or list(bpy.data.actions)
print("actions:", [a.name for a in bpy.data.actions], "using", act[0].name, "fps", fps)
arm.animation_data_create(); arm.animation_data.action = act[0]
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 700; sc.render.resolution_y = 900
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 21000; cam.data.clip_end = 1e6
cam.location = (0, 60000, 8300); cam.rotation_euler = (math.radians(90), 0, math.radians(180))
for t in times:
    sc.frame_set(int(round(t * fps)))
    sc.render.filepath = OUTDIR + "\\front_%05.2f.png" % t
    bpy.ops.render.render(write_still=True)
    print("rendered", t)

