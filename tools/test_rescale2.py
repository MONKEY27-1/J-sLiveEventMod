import bpy
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")

# Delete unneeded Icosphere if present
if "Icosphere" in bpy.data.objects:
    bpy.data.objects.remove(bpy.data.objects["Icosphere"], do_unlink=True)

sketchfab = bpy.data.objects.get("Sketchfab_model")
char_meshes = [o for o in bpy.data.objects if o.type == 'MESH']

bbox_min = Vector((float('inf'), float('inf'), float('inf')))
bbox_max = Vector((float('-inf'), float('-inf'), float('-inf')))

for m in char_meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min.x = min(bbox_min.x, w.x)
        bbox_min.y = min(bbox_min.y, w.y)
        bbox_min.z = min(bbox_min.z, w.z)
        bbox_max.x = max(bbox_max.x, w.x)
        bbox_max.y = max(bbox_max.y, w.y)
        bbox_max.z = max(bbox_max.z, w.z)

current_h = bbox_max.z - bbox_min.z
target_h = 1.95 # 1.95 meters
scale_factor = target_h / current_h

print(f"Current height: {current_h}, scale factor: {scale_factor}")

sketchfab.scale *= scale_factor
bpy.context.view_layer.update()

bbox_min2 = Vector((float('inf'), float('inf'), float('inf')))
bbox_max2 = Vector((float('-inf'), float('-inf'), float('-inf')))
for m in char_meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min2.x = min(bbox_min2.x, w.x)
        bbox_min2.y = min(bbox_min2.y, w.y)
        bbox_min2.z = min(bbox_min2.z, w.z)
        bbox_max2.x = max(bbox_max2.x, w.x)
        bbox_max2.y = max(bbox_max2.y, w.y)
        bbox_max2.z = max(bbox_max2.z, w.z)

print(f"Scaled BBox Min: {bbox_min2}, Max: {bbox_max2}")
print(f"Scaled Height: {bbox_max2.z - bbox_min2.z} meters")

# Shift so X=0, Y=0, and feet at Z=0
shift = Vector((- (bbox_min2.x + bbox_max2.x) * 0.5, - (bbox_min2.y + bbox_max2.y) * 0.5, - bbox_min2.z))
sketchfab.location += shift
bpy.context.view_layer.update()

bbox_min3 = Vector((float('inf'), float('inf'), float('inf')))
bbox_max3 = Vector((float('-inf'), float('-inf'), float('-inf')))
for m in char_meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min3.x = min(bbox_min3.x, w.x)
        bbox_min3.y = min(bbox_min3.y, w.y)
        bbox_min3.z = min(bbox_min3.z, w.z)
        bbox_max3.x = max(bbox_max3.x, w.x)
        bbox_max3.y = max(bbox_max3.y, w.y)
        bbox_max3.z = max(bbox_max3.z, w.z)

print(f"Final Grounded BBox Min: {bbox_min3}, Max: {bbox_max3}")
print(f"Final Character Height: {bbox_max3.z - bbox_min3.z} meters (feet at Z={bbox_min3.z})")
