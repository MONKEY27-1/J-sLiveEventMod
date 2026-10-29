import bpy, math

# Script to retarget track1_7473-8320_PRC from old avatar to new skin
bpy.ops.wm.read_factory_settings(use_empty=True)

# 1. Import old avatar
bpy.ops.import_scene.gltf(filepath=r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")
old_arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
old_arm.name = "OldArmature"

# Find the track1 action
track1_act = None
for a in bpy.data.actions:
    if "track1" in a.name.lower():
        track1_act = a
        break
print("Found old track action:", track1_act.name if track1_act else "None")

# 2. Import new skin
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")
new_arm = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o != old_arm][0]
new_arm.name = "NewArmature"

# Build bone mapping
old_bones = {b.name: b for b in old_arm.data.bones}
new_bones = {b.name: b for b in new_arm.data.bones}

mapping = {}
for nb_name in new_bones:
    parts = nb_name.split('_')
    # try matching against old_bones
    for ob_name in old_bones:
        oparts = ob_name.split('_')
        if parts[0] == oparts[0]:
            if len(parts) > 1 and len(oparts) > 1:
                if parts[1] == oparts[1]:
                    mapping[nb_name] = ob_name
                    break
            elif parts[0] in ['pelvis', 'head', '_rootJoint']:
                mapping[nb_name] = ob_name
                break

print(f"Mapped {len(mapping)} bones for retargeting")

# Now apply action to old_arm
old_arm.animation_data.action = track1_act

# Create new action for new_arm
new_act = bpy.data.actions.new("track1_7473-8320_PRC")
new_arm.animation_data.action = new_act

start_f = int(track1_act.frame_range[0])
end_f = int(track1_act.frame_range[1])
print(f"Sampling frames {start_f} to {end_f}...")

# Switch to pose mode
bpy.context.view_layer.objects.active = new_arm
bpy.ops.object.mode_set(mode='POSE')

for pb in new_arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

for f in range(start_f, min(end_f + 1, start_f + 100), 2): # test first 100 frames
    bpy.context.scene.frame_set(f)
    # copy local rotation from old pose bone to new pose bone
    for nb_name, ob_name in mapping.items():
        if ob_name in old_arm.pose.bones and nb_name in new_arm.pose.bones:
            opb = old_arm.pose.bones[ob_name]
            npb = new_arm.pose.bones[nb_name]
            npb.rotation_quaternion = opb.rotation_quaternion
            npb.keyframe_insert("rotation_quaternion", frame=f)
            if "pelvis" in nb_name.lower() or "root" in nb_name.lower():
                npb.location = opb.location
                npb.keyframe_insert("location", frame=f)

print("Sampled and keyframed successfully!")
