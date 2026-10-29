import bpy

def get_bones_and_actions(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    arm = None
    for obj in bpy.data.objects:
        if obj.type == 'ARMATURE':
            arm = obj
            break
    bones = [b.name for b in arm.data.bones] if arm else []
    actions = {}
    for act in bpy.data.actions:
        channels = [fc.data_path for fc in act.fcurves]
        actions[act.name] = {
            'range': (act.frame_range[0], act.frame_range[1]),
            'fcurves': len(act.fcurves),
            'sample_paths': channels[:10]
        }
    return arm.name if arm else None, bones, actions

arm1, bones1, actions1 = get_bones_and_actions(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")
print(f"=== travis_scott_fortnite_skin.glb ===")
print(f"Armature: {arm1}, Bones count: {len(bones1)}")
print(f"Actions: {list(actions1.keys())}")
for name, info in actions1.items():
    print(f"  {name}: {info['range']}, {info['fcurves']} fcurves")
    for sp in info['sample_paths'][:5]:
        print(f"    {sp}")

arm2, bones2, actions2 = get_bones_and_actions(r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")
print(f"\n=== travis_avatar.glb ===")
print(f"Armature: {arm2}, Bones count: {len(bones2)}")
print(f"Actions: {list(actions2.keys())}")
for name, info in actions2.items():
    print(f"  {name}: {info['range']}, {info['fcurves']} fcurves")
    for sp in info['sample_paths'][:5]:
        print(f"    {sp}")
