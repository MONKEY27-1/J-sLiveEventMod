import bpy

# Load travis_avatar.glb and check its animations
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")

print("Actions in travis_avatar.glb:")
for a in bpy.data.actions:
    print(f"Action: {a.name}, frames: {a.frame_range}")
