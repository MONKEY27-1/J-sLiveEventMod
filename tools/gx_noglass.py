"""blender -b -P gx_noglass.py -- in.glb out.glb     delete every face of a material whose name contains 'glass' (the additive SF_A_ glass shows as an opaque radial gradient from INSIDE a bus): windows become real openings"""
import bpy, bmesh, sys
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); n = 0
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    idx = [i for i, m in enumerate(o.data.materials) if m and 'glass' in m.name.lower()]
    if not idx: continue
    bm = bmesh.new(); bm.from_mesh(o.data); gone = [f for f in bm.faces if f.material_index in idx]; n += len(gone); bmesh.ops.delete(bm, geom=gone, context='FACES'); bm.to_mesh(o.data); bm.free()
print('REMOVED glass faces', n)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
