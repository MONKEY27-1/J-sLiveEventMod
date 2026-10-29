import bpy

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\stage_3.glb")

print("Stage objects:")
for o in bpy.data.objects:
    if o.type == 'MESH':
        print(f"  {o.name}: loc={o.location}, rot={o.rotation_euler}, scale={o.scale}")
