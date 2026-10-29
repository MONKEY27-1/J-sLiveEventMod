import bpy, math, sys
a=sys.argv[sys.argv.index('--')+1:]; glbs=a[:-1]; out=a[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
for g in glbs: bpy.ops.import_scene.gltf(filepath=g)
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'
sc.render.resolution_x=1200; sc.render.resolution_y=700
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.clip_end=1000; cam.location=(55,-60,45); cam.rotation_euler=(math.radians(62),0,math.radians(42))
sc.render.filepath=out; bpy.ops.render.render(write_still=True)
