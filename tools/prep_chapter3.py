"""blender -b -P prep_chapter3.py -- in.glb out.glb [target_tris] [tex_px]
Decimates the Chapter 3 island (282k verts) for the finale: joins every mesh by material, collapse-decimates to ~target_tris, shrinks images to tex_px, recentres (centre of the footprint at x,z = 0, highest point at y = 0) and scales the footprint diameter to 200 units."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]; target = int(a[2]) if len(a) > 2 else 50000; px = int(a[3]) if len(a) > 3 else 1024
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ms = [o for o in bpy.data.objects if o.type == 'MESH']; tris = sum(len(o.data.polygons) for o in ms); print('IN meshes', len(ms), 'polys', tris, 'images', [(i.name, tuple(i.size)) for i in bpy.data.images][:20])
for o in bpy.data.objects: o.select_set(False)
for o in ms:
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM'); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.join(); o = bpy.context.view_layer.objects.active; print('JOINED polys', len(o.data.polygons), 'mats', len(o.data.materials))
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=0.001); bpy.ops.object.mode_set(mode='OBJECT')
for v in o.data.vertices: v.co = (v.co.x, v.co.z, -v.co.y)            # file +z (the map's up) -> Blender +Z
import bmesh
bm = bmesh.new(); bm.from_mesh(o.data); xs0 = [v.co.x for v in bm.verts]; ys0 = [v.co.y for v in bm.verts]; ccx, ccy = (min(xs0) + max(xs0)) / 2, (min(ys0) + max(ys0)) / 2; rad = min(max(xs0) - min(xs0), max(ys0) - min(ys0)) / 2 * 0.985
bad = [f for f in bm.faces if math.hypot(f.calc_center_median().x - ccx, f.calc_center_median().y - ccy) > rad]; bmesh.ops.delete(bm, geom=bad, context='FACES'); bm.to_mesh(o.data); bm.free(); print('CULLED', len(bad))
n0 = len(o.data.polygons); ratio = min(1.0, target / max(n0 * 1.0, 1))
m = o.modifiers.new('dec', 'DECIMATE'); m.ratio = ratio; m.use_collapse_triangulate = True; bpy.ops.object.modifier_apply(modifier='dec'); print('DECIMATED', n0, '->', len(o.data.polygons), 'ratio', ratio)
for im in bpy.data.images:
    if im.size[0] > px or im.size[1] > px:
        k = max(im.size[0], im.size[1]) / px; im.scale(max(int(im.size[0] / k), 4), max(int(im.size[1] / k), 4))
o.data.update(); bb = [mathutils.Vector(c) for c in o.bound_box]; xs = [v.x for v in bb]; ys = [v.y for v in bb]; zs = [v.z for v in bb]
cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2; top = max(zs); dia = max(max(xs) - min(xs), max(ys) - min(ys)); print('BBOX x', min(xs), max(xs), 'y', min(ys), max(ys), 'z', min(zs), max(zs), 'dia', dia)
k = 200.0 / dia
for v in o.data.vertices: v.co = ((v.co.x - cx) * k, (v.co.y - cy) * k, (v.co.z - top) * k)
o.location = (0, 0, 0); o.rotation_euler = (0, 0, 0); o.scale = (1, 1, 1)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_image_format='JPEG', export_jpeg_quality=80, export_apply=True); print('WROTE', out)
