import bpy

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")

arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
print(f"Total bones: {len(arm.data.bones)}")
main_keywords = ['root', 'pelvis', 'spine', 'neck', 'head', 'clavicle', 'upperarm', 'lowerarm', 'hand', 'thigh', 'calf', 'foot']
for b in arm.data.bones:
    name_lower = b.name.lower()
    if any(k in name_lower for k in main_keywords):
        # filter out twist or finger bones if needed, or print them
        if not any(x in name_lower for x in ['twist', 'index', 'middle', 'ring', 'pinky', 'thumb', 'attach']):
            print(f"  {b.name} -> parent: {b.parent.name if b.parent else 'None'}")
