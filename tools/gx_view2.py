"""blender -b -P gx_view2.py -- in.glb out_prefix   front / side / back renders of a model, every mesh object its own colour; prints each mesh's bounds"""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
cols = [(0.7, 0.7, 0.7, 1), (1, 0.3, 0.3, 1), (0.3, 1, 0.3, 1), (0.3, 0.6, 1, 1), (1, 1, 0.2, 1), (1, 0.4, 1, 1), (0.3, 1, 1, 1), (1, 0.6, 0.2, 1)]
ms = [o for o in bpy.data.objects if o.type == 'MESH']
lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
for i, o in enumerate(ms):
    o.color = cols[i % len(cols)]
    mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
    for v in o.data.vertices:
        p = o.matrix_world @ v.co
        for k in range(3): mn[k] = min(mn[k], p[k]); mx[k] = max(mx[k], p[k]); lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
    print('MESH', i, o.name, 'verts', len(o.data.vertices), 'min', tuple(round(x, 3) for x in mn), 'max', tuple(round(x, 3) for x in mx))
print('ALL', tuple(round(x, 3) for x in lo), tuple(round(x, 3) for x in hi))
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
c = (lo + hi) / 2; size = max(hi - lo); cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.1; cam.data.clip_end = 1e5
for name, d in (('front', (0, -1, 0.05)), ('side', (1, 0, 0.05)), ('back', (0, 1, 0.05))):
    d = mathutils.Vector(d).normalized(); cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = out + '_' + name + '.png'; bpy.ops.render.render(write_still=True)
