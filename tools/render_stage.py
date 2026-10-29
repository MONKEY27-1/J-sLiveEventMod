import bpy, math, sys
GLB, OUT = sys.argv[sys.argv.index('--')+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x = 900; sc.render.resolution_y = 600
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 70; cam.data.clip_end = 1e5
views = {'front_minusY': ((0, -80, 12), (math.radians(80), 0, 0)), 'iso': ((55, -55, 45), (math.radians(60), 0, math.radians(45))), 'top': ((0, 0, 90), (0, 0, 0))}
for name, (loc, rot) in views.items():
    cam.location = loc; cam.rotation_euler = rot
    sc.render.filepath = OUT + "\\stage_" + name + ".png"; bpy.ops.render.render(write_still=True); print("rendered", name)
dims = {}
for o in bpy.data.objects:
    if o.type == 'MESH': print(o.name, [round(x,1) for x in o.dimensions], [round(x,1) for x in o.location])
