import bpy, sys
from mathutils import Vector
GLB = sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
dg = bpy.context.evaluated_depsgraph_get()
print("PROFILE x=0 (blender y: +back/ring, -front) -> top surface height z")
for y in range(-12, 12, 1):
    hit, loc, n, idx, obj, mat = bpy.context.scene.ray_cast(dg, Vector((0.0, float(y), 50.0)), Vector((0, 0, -1)))
    print("PROFILE y=%+d  z=%s" % (y, round(loc.z, 2) if hit else None))
for x in (-6, -3, 3, 6):
    hit, loc, n, idx, obj, mat = bpy.context.scene.ray_cast(dg, Vector((float(x), 0.0, 50.0)), Vector((0, 0, -1)))
    print("PROFILE x=%+d y=0 z=%s" % (x, round(loc.z, 2) if hit else None))
