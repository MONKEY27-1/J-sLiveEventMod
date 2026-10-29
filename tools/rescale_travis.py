import bpy
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")

# Find the root empty
root_obj = None
for o in bpy.data.objects:
    if o.parent is None:
        root_obj = o
        break

print("Root object:", root_obj.name if root_obj else "None")

# Calculate current height
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bbox_min = Vector((float('inf'), float('inf'), float('inf')))
bbox_max = Vector((float('-inf'), float('-inf'), float('-inf')))

for m in meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min.x = min(bbox_min.x, w.x)
        bbox_min.y = min(bbox_min.y, w.y)
        bbox_min.z = min(bbox_min.z, w.z)
        bbox_max.x = max(bbox_max.x, w.x)
        bbox_max.y = max(bbox_max.y, w.y)
        bbox_max.z = max(bbox_max.z, w.z)

current_h = bbox_max.z - bbox_min.z
target_h = 1.9 # 1.9 meters
scale_factor = target_h / current_h

print(f"Current height: {current_h}, scale factor: {scale_factor}")

# Scale root
if root_obj:
    root_obj.scale *= scale_factor
    # Center X and Y, and place feet at Z=0
    bpy.context.view_layer.update()
    
# Check new height
bbox_min2 = Vector((float('inf'), float('inf'), float('inf')))
bbox_max2 = Vector((float('-inf'), float('-inf'), float('-inf')))
for m in meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min2.x = min(bbox_min2.x, w.x)
        bbox_min2.y = min(bbox_min2.y, w.y)
        bbox_min2.z = min(bbox_min2.z, w.z)
        bbox_max2.x = max(bbox_max2.x, w.x)
        bbox_max2.y = max(bbox_max2.y, w.y)
        bbox_max2.z = max(bbox_max2.z, w.z)

print(f"New BBox Min: {bbox_min2}")
print(f"New BBox Max: {bbox_max2}")
print(f"New Height: {bbox_max2.z - bbox_min2.z}")

# Shift to ground at Z=0 and center X/Y
shift = Vector((- (bbox_min2.x + bbox_max2.x) * 0.5, - (bbox_min2.y + bbox_max2.y) * 0.5, - bbox_min2.z))
if root_obj:
    root_obj.location += shift
    bpy.context.view_layer.update()

# Final check
bbox_min3 = Vector((float('inf'), float('inf'), float('inf')))
bbox_max3 = Vector((float('-inf'), float('-inf'), float('-inf')))
for m in meshes:
    for v in m.bound_box:
        w = m.matrix_world @ Vector(v)
        bbox_min3.x = min(bbox_min3.x, w.x)
        bbox_min3.y = min(bbox_min3.y, w.y)
        bbox_min3.z = min(bbox_min3.z, w.z)
        bbox_max3.x = max(bbox_max3.x, w.x)
        bbox_max3.y = max(bbox_max3.y, w.y)
        bbox_max3.z = max(bbox_max3.z, w.z)

print(f"Final BBox Min: {bbox_min3}, Max: {bbox_max3}")
print(f"Final Height: {bbox_max3.z - bbox_min3.z} meters (feet at Z={bbox_min3.z})")
