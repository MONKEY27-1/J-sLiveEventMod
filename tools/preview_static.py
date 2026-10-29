"""blender -b -P preview_static.py -- in.glb out.png camx camy camz [tx ty tz] [res]   one workbench render (texture colours) of a glb from a camera position (Blender Z-up coordinates of the IMPORTED scene), looking at a target (default origin)."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]; cam = [float(x) for x in a[2:5]]; tgt = [float(x) for x in a[5:8]] if len(a) >= 8 else [0, 0, 0]; res = int(a[8]) if len(a) > 8 else 900
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'; sc.render.resolution_x = res; sc.render.resolution_y = int(res * 0.62)
w = bpy.data.worlds.new('w'); w.color = (0.08, 0.09, 0.14); sc.world = w
c = bpy.data.cameras.new('c'); c.lens = 24; c.clip_end = 5000; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); co.location = cam
d = mathutils.Vector(tgt) - mathutils.Vector(cam); co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.camera = co
sc.render.filepath = out; bpy.ops.render.render(write_still=True); print('rendered', out)
