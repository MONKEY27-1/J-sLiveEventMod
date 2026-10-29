"""blender -b -P render_views.py -- <glb> <outdir> <action prefix> <view: full|upper|face|side> <t1> [t2 ...]  (Workbench, textured, white background)"""
import bpy, math, sys
args = sys.argv[sys.argv.index('--') + 1:]
GLB, OUTDIR, ACT, VIEW = args[:4]; times = [float(x) for x in args[4:]]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
sc = bpy.context.scene; fps = sc.render.fps
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = [a for a in bpy.data.actions if a.name.startswith(ACT)] or list(bpy.data.actions)
arm.animation_data_create(); arm.animation_data.action = act[0]
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 700; sc.render.resolution_y = 900
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 1e6
# character faces +Y in Blender; glTF (x,y,z) -> Blender (x,-z,y); head ~ (0,170,15200)
if VIEW == 'face':
    cam.data.ortho_scale = 3600; cam.location = (0, 60000, 14900); cam.rotation_euler = (math.radians(90), 0, math.radians(180)); sc.render.resolution_x = 800; sc.render.resolution_y = 800
elif VIEW == 'upper':
    cam.data.ortho_scale = 10500; cam.location = (0, 60000, 12000); cam.rotation_euler = (math.radians(90), 0, math.radians(180))
elif VIEW == 'side':
    cam.data.ortho_scale = 21000; cam.location = (-60000, 0, 8300); cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
elif VIEW == 'wide':
    cam.data.ortho_scale = 34000; cam.location = (0, 60000, 8300); cam.rotation_euler = (math.radians(90), 0, math.radians(180)); sc.render.resolution_x = 1100; sc.render.resolution_y = 800
else:
    cam.data.ortho_scale = 21000; cam.location = (0, 60000, 8300); cam.rotation_euler = (math.radians(90), 0, math.radians(180))
for t in times:
    sc.frame_set(int(round(t * fps)))
    sc.render.filepath = OUTDIR + "\%s_%s_%07.2f.png" % (ACT[:12], VIEW, t)
    bpy.ops.render.render(write_still=True)
print("rendered", len(times))
