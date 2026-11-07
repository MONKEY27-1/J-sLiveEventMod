"""blender -b -P view_tex.py -- in.glb out_prefix [frame]   textured Workbench renders (front / side / back) of any glb at its evaluated pose."""
import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
if len(a) > 2: sc.frame_set(int(a[2]))
ms = [o for o in bpy.data.objects if o.type == 'MESH' and o.name != 'Icosphere']
for o in bpy.data.objects:
    if o.name == 'Icosphere': o.hide_render = True
dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
for o in ms:
    ev = o.evaluated_get(dg)
    for v in ev.data.vertices:
        p = ev.matrix_world @ v.co
        for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
print('BOUNDS', tuple(round(x, 2) for x in lo), tuple(round(x, 2) for x in hi))
sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_backface_culling = False
sc.render.resolution_x = 600; sc.render.resolution_y = 800
c = (lo + hi) / 2; size = max(hi - lo)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.1
for name, d in (('front', (0, -1, 0.05)), ('side', (1, 0, 0.05)), ('back', (0, 1, 0.05))):
    d = mathutils.Vector(d).normalized(); cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = out + '_' + name + '.png'; bpy.ops.render.render(write_still=True)
