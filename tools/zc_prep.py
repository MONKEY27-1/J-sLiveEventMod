"""blender -b -P zc_prep.py -- in.glb out.glb mode tris [rename=old:new,...]   mode: base (bottom centre at the origin) | mid (bounds centre at the origin).  Decimates every mesh to `tris` triangles in total, applies the transforms and exports (y up)."""
import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out, mode, tris = a[0], a[1], a[2], int(a[3]); ren = dict(x.split(':') for x in a[4].split(',')) if len(a) > 4 and a[4] else {}
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']; total = sum(len(o.data.polygons) for o in ms)
for m in bpy.data.materials:
    for k, v in ren.items():
        if k in m.name: m.name = v + m.name
for o in ms:
    if tris > 0 and total > tris:
        md = o.modifiers.new('d', 'DECIMATE'); md.ratio = max(0.02, tris / total); bpy.context.view_layer.objects.active = o
        try: bpy.ops.object.modifier_apply(modifier='d')
        except Exception as e: print('decimate failed', o.name, e)
dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
for o in ms:
    ev = o.evaluated_get(dg)
    for v in ev.data.vertices:
        p = ev.matrix_world @ v.co
        for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
c = (lo + hi) / 2; off = mathutils.Vector((-c.x, -c.y, -lo.z if mode == 'base' else -c.z))
roots = [o for o in bpy.context.scene.objects if o.parent is None]; e = bpy.data.objects.new('Root', None); bpy.context.collection.objects.link(e)
for o in roots: o.parent = e
e.location = off
print('PREP', src.split('\\')[-1], 'tris', total, '->', sum(len(o.data.polygons) for o in ms), 'size', tuple(round(x, 2) for x in (hi - lo)), 'offset', tuple(round(x, 2) for x in off))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
