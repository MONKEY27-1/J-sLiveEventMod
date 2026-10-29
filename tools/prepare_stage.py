import bpy
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\stage_3.glb")

# Calculate world bounding box
bbox_min = Vector((float('inf'), float('inf'), float('inf')))
bbox_max = Vector((float('-inf'), float('-inf'), float('-inf')))

for obj in bpy.data.objects:
    if obj.type == 'MESH':
        for v in obj.bound_box:
            world_v = obj.matrix_world @ Vector(v)
            bbox_min.x = min(bbox_min.x, world_v.x)
            bbox_min.y = min(bbox_min.y, world_v.y)
            bbox_min.z = min(bbox_min.z, world_v.z)
            bbox_max.x = max(bbox_max.x, world_v.x)
            bbox_max.y = max(bbox_max.y, world_v.y)
            bbox_max.z = max(bbox_max.z, world_v.z)

print(f"Original Min: {bbox_min}, Max: {bbox_max}")
# We want to shift everything so center X=0, center Y=0, and Min Z=0
shift = Vector((- (bbox_min.x + bbox_max.x) * 0.5, - (bbox_min.y + bbox_max.y) * 0.5, - bbox_min.z))

for obj in bpy.data.objects:
    if obj.parent is None:
        obj.location += shift

# Update scene
bpy.context.view_layer.update()

# Recalculate bbox
bbox_min2 = Vector((float('inf'), float('inf'), float('inf')))
bbox_max2 = Vector((float('-inf'), float('-inf'), float('-inf')))
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        for v in obj.bound_box:
            world_v = obj.matrix_world @ Vector(v)
            bbox_min2.x = min(bbox_min2.x, world_v.x)
            bbox_min2.y = min(bbox_min2.y, world_v.y)
            bbox_min2.z = min(bbox_min2.z, world_v.z)
            bbox_max2.x = max(bbox_max2.x, world_v.x)
            bbox_max2.y = max(bbox_max2.y, world_v.y)
            bbox_max2.z = max(bbox_max2.z, world_v.z)

print(f"New Min: {bbox_min2}, Max: {bbox_max2}")

# Ensure emissive color on light_blue material is glowing bright cyan
for mat in bpy.data.materials:
    if mat.name == "light_blue":
        if mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    n.inputs['Base Color'].default_value = (0.1, 0.8, 1.0, 1.0)
                    if 'Emission Color' in n.inputs:
                        n.inputs['Emission Color'].default_value = (0.2, 0.85, 1.0, 1.0)
                    if 'Emission Strength' in n.inputs:
                        n.inputs['Emission Strength'].default_value = 5.0

out_stage = r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\stage.glb"
bpy.ops.export_scene.gltf(filepath=out_stage, export_format='GLB')
print("Exported stage to", out_stage)
