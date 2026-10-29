import bpy, re

def clean_bone_name(name):
    # e.g. "spine_01_05_9" -> "spine_01"
    # "upperarm_l_011_15" -> "upperarm_l"
    # "pelvis_04_8" -> "pelvis"
    # "head_076_104" -> "head"
    # "neck_01_074_102" -> "neck_01"
    # remove trailing _numbers
    # Try pattern
    m = re.match(r"^([a-zA-Z]+(?:_[a-zA-Z0-9]+)*?)(?:_\d+)+$", name)
    if m:
        # let's see what base is
        base = m.group(1)
        # also handle case where base is like spine_01_05 -> spine_01
        m2 = re.match(r"^([a-zA-Z]+(?:_[a-zA-Z]+)*)(?:_\d+)*$", base)
        return m2.group(1) if m2 else base
    return name

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")
arm1 = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bones1 = [b.name for b in arm1.data.bones]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")
arm2 = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bones2 = [b.name for b in arm2.data.bones]

print(f"Skin bones: {len(bones1)}, Old avatar bones: {len(bones2)}")

map_b1_to_b2 = {}
for b1 in bones1:
    # clean b1
    # For example: pelvis_04_8 -> starts with pelvis
    # spine_01_05_9 -> spine_01
    parts1 = b1.split('_')
    # try matching against bones2
    best = None
    for b2 in bones2:
        parts2 = b2.split('_')
        # if both share the main identifier: e.g. ("spine", "01") or ("clavicle", "l") or ("pelvis") or ("head")
        if parts1[0] == parts2[0]:
            if len(parts1) > 1 and len(parts2) > 1:
                if parts1[1] == parts2[1]:
                    best = b2
                    break
            elif len(parts1) == 1 and len(parts2) == 1:
                best = b2
                break
            elif parts1[0] in ['pelvis', 'head', 'root']:
                best = b2
                break
    if best:
        map_b1_to_b2[b1] = best

print(f"Matched {len(map_b1_to_b2)} bones! Here are some key matches:")
for k, v in list(map_b1_to_b2.items())[:25]:
    print(f"  {k}  <==>  {v}")
