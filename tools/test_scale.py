import bpy
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")

arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
meshes = [o for o in bpy.data.objects if o.type == 'MESH']

print("Armature:", arm.name)
print("Meshes:", [m.name for m in meshes])

# Calculate total character bounding box
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

current_height = bbox_max.z - bbox_min.z
print(f"World BBox Min: {bbox_min}")
print(f"World BBox Max: {bbox_max}")
print(f"Current character height in Blender: {current_height}")
