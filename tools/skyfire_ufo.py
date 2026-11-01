"""blender -b -P skyfire_ufo.py -- in.glb out.glb target_tris : joins every mesh of the Fortnite UFO and decimates it (the original has 275k vertices)."""
import bpy, sys
a = sys.argv[sys.argv.index('--')+1:]; src, out, tgt = a[0], a[1], int(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects: o.select_set(False)
for o in ms:
    o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM'); bpy.ops.object.join()
o = bpy.context.active_object; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
n = len(o.data.polygons); md = o.modifiers.new('d', 'DECIMATE'); md.ratio = min(1.0, tgt / max(1, n)); bpy.ops.object.modifier_apply(modifier='d')
print('UFO tris', n, '->', len(o.data.polygons), 'dims', tuple(o.dimensions))
for ob in list(bpy.context.scene.objects):
    if ob is not o: bpy.data.objects.remove(ob)
o.name = 'Ufo'; bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True)
