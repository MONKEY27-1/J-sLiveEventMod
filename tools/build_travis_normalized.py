import bpy, math
from mathutils import Vector, Quaternion, Euler

print("=== Building Normalized Travis Scott Model ===")
bpy.ops.wm.read_factory_settings(use_empty=True)

# 1. Load old avatar to get track1 action
old_path = r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb"
bpy.ops.import_scene.gltf(filepath=old_path)
old_arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
old_arm.name = "OldArmature"
track1_act = None
for a in bpy.data.actions:
    if "track1" in a.name.lower():
        track1_act = a
        break
print("Found old track1:", track1_act.name if track1_act else "None")

# 2. Load Fortnite skin
skin_path = r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"
bpy.ops.import_scene.gltf(filepath=skin_path)
new_arm = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o != old_arm][0]
new_arm.name = "NewArmature"

# Remove any dummy Icosphere
if "Icosphere" in bpy.data.objects:
    bpy.data.objects.remove(bpy.data.objects["Icosphere"], do_unlink=True)

# Find the root that actually owns the newly imported skin rig.  The previous
# exporter picked the first Sketchfab root in the scene, which belonged to the
# old animation donor.  That left the new rig at its raw 16,000-unit scale.
new_root = new_arm
while new_root.parent is not None:
    new_root = new_root.parent
print("New skin root:", new_root.name)

# Normalizing scale to 1.95 meters
char_meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("Object_")]
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
target_h = 1.95
scale_factor = target_h / current_h
print(f"Current height: {current_h}, applying scale factor: {scale_factor}")

if new_root:
    new_root.scale *= scale_factor
    bpy.context.view_layer.update()

# Recalculate and ground at Z=0, center at X=0, Y=0
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

shift = Vector((- (bbox_min2.x + bbox_max2.x) * 0.5, - (bbox_min2.y + bbox_max2.y) * 0.5, - bbox_min2.z))
if new_root:
    new_root.location += shift
    bpy.context.view_layer.update()

print("Model grounded and scaled. Now setting up animations...")

# Map bones between armatures
old_bones = {b.name: b for b in old_arm.data.bones}
new_bones = {b.name: b for b in new_arm.data.bones}

mapping = {}
for nb_name in new_bones:
    parts = nb_name.split('_')
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

print(f"Mapped {len(mapping)} bones.")

bpy.context.view_layer.objects.active = new_arm
bpy.ops.object.mode_set(mode='POSE')

def find_bone(*keywords):
    for pb in new_arm.pose.bones:
        nl = pb.name.lower()
        if all(k in nl for k in keywords):
            return pb
    return None

pelvis = find_bone("pelvis")
spine1 = find_bone("spine", "01")
spine2 = find_bone("spine", "02")
spine3 = find_bone("spine", "03")
spine4 = find_bone("spine", "04")
spine5 = find_bone("spine", "05")
head = find_bone("head")
arm_l = find_bone("upperarm", "l")
arm_r = find_bone("upperarm", "r")
forearm_l = find_bone("lowerarm", "l")
forearm_r = find_bone("lowerarm", "r")
hand_l = find_bone("hand", "l")
hand_r = find_bone("hand", "r")
thigh_l = find_bone("thigh", "l")
thigh_r = find_bone("thigh", "r")
calf_l = find_bone("calf", "l")
calf_r = find_bone("calf", "r")

rest_quats = {pb.name: pb.rotation_quaternion.copy() for pb in new_arm.pose.bones}
rest_locs = {pb.name: pb.location.copy() for pb in new_arm.pose.bones}

def reset_pose():
    for pb in new_arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = rest_quats[pb.name].copy()
        pb.location = rest_locs[pb.name].copy()

# -------------------------------------------------------------
# Action 1: Retarget track1_7473-8320_PRC (rotations only to protect human-scale pelvis)
# -------------------------------------------------------------
if track1_act:
    print("Retargeting track1_7473-8320_PRC...")
    old_arm.animation_data.action = track1_act
    act_track1 = bpy.data.actions.new("track1_7473-8320_PRC")
    new_arm.animation_data.action = act_track1
    start_f = int(track1_act.frame_range[0])
    end_f = int(track1_act.frame_range[1])
    for f in range(start_f, end_f + 1):
        bpy.context.scene.frame_set(f)
        for nb_name, ob_name in mapping.items():
            if ob_name in old_arm.pose.bones and nb_name in new_arm.pose.bones:
                opb = old_arm.pose.bones[ob_name]
                npb = new_arm.pose.bones[nb_name]
                npb.rotation_quaternion = opb.rotation_quaternion
                npb.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 2: Travis_IntroIdle
# -------------------------------------------------------------
print("Generating Travis_IntroIdle...")
reset_pose()
act_idle = bpy.data.actions.new("Travis_IntroIdle")
new_arm.animation_data.action = act_idle
total_f = 60 # 2s loop
for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    breathe = math.sin(phi) * 0.05
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((1, 0, 0), math.radians(breathe * 20))
        spine2.keyframe_insert("rotation_quaternion", frame=f)
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((0, 0, 1), math.radians(math.sin(phi * 0.5) * 8)) @ Quaternion((1, 0, 0), math.radians(-5))
        head.keyframe_insert("rotation_quaternion", frame=f)
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((0, 0, 1), math.radians(-10))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((0, 0, 1), math.radians(10))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 3: Travis_SickoMode_Hype
# -------------------------------------------------------------
print("Generating Travis_SickoMode_Hype...")
reset_pose()
act_hype = bpy.data.actions.new("Travis_SickoMode_Hype")
new_arm.animation_data.action = act_hype
total_f = 120
bpm = 130.0
beats_per_sec = bpm / 60.0

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    t = f / 30.0
    beat_phase = t * beats_per_sec * 2.0 * math.pi
    bounce_angle = math.sin(beat_phase) * 0.12
    
    if pelvis:
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(-abs(math.sin(beat_phase)) * 15)) @ Quaternion((0, 1, 0), math.radians(math.sin(beat_phase * 0.5) * 8))
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((1, 0, 0), math.radians(math.sin(beat_phase) * 10))
        spine2.keyframe_insert("rotation_quaternion", frame=f)
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-abs(math.sin(beat_phase)) * 20 - 5))
        head.keyframe_insert("rotation_quaternion", frame=f)
    if arm_l: # mic hold
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(35 + math.sin(beat_phase) * 6)) @ Quaternion((0, 0, 1), math.radians(-25))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_l:
        forearm_l.rotation_quaternion = rest_quats[forearm_l.name] @ Quaternion((1, 0, 0), math.radians(70 + math.sin(beat_phase) * 8))
        forearm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r: # crowd pump
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(40 + math.sin(beat_phase) * 22)) @ Quaternion((0, 0, 1), math.radians(30))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_r:
        forearm_r.rotation_quaternion = rest_quats[forearm_r.name] @ Quaternion((1, 0, 0), math.radians(35 + math.cos(beat_phase) * 15))
        forearm_r.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_l and calf_l:
        k = abs(math.sin(beat_phase)) * 15
        thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(k))
        thigh_l.keyframe_insert("rotation_quaternion", frame=f)
        calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-k * 1.2))
        calf_l.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_r and calf_r:
        k = abs(math.sin(beat_phase)) * 15
        thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(k))
        thigh_r.keyframe_insert("rotation_quaternion", frame=f)
        calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-k * 1.2))
        calf_r.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 4: Travis_GiantWalk
# -------------------------------------------------------------
print("Generating Travis_GiantWalk...")
reset_pose()
act_walk = bpy.data.actions.new("Travis_GiantWalk")
new_arm.animation_data.action = act_walk
total_f = 120
for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    stride_l = math.sin(phi) * 25.0
    stride_r = -stride_l
    if pelvis:
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((0, 1, 0), math.radians(math.cos(phi) * 8)) @ Quaternion((0, 0, 1), math.radians(math.sin(phi) * 4))
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-15 + math.sin(phi * 2) * 3))
        head.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_l and calf_l:
        thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(stride_l))
        thigh_l.keyframe_insert("rotation_quaternion", frame=f)
        bend = max(0.0, -stride_l) * 1.5
        calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-bend))
        calf_l.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_r and calf_r:
        thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(stride_r))
        thigh_r.keyframe_insert("rotation_quaternion", frame=f)
        bend = max(0.0, -stride_r) * 1.5
        calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-bend))
        calf_r.keyframe_insert("rotation_quaternion", frame=f)
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(-stride_l * 0.8)) @ Quaternion((0, 0, 1), math.radians(-15))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(-stride_r * 0.8)) @ Quaternion((0, 0, 1), math.radians(15))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 5: Travis_CosmicLevitate
# -------------------------------------------------------------
print("Generating Travis_CosmicLevitate...")
reset_pose()
act_cosmic = bpy.data.actions.new("Travis_CosmicLevitate")
new_arm.animation_data.action = act_cosmic
total_f = 180
for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    if spine3:
        spine3.rotation_quaternion = rest_quats[spine3.name] @ Quaternion((1, 0, 0), math.radians(10 + math.sin(phi) * 2))
        spine3.keyframe_insert("rotation_quaternion", frame=f)
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(35 + math.sin(phi) * 5))
        head.keyframe_insert("rotation_quaternion", frame=f)
    spread = math.sin(phi) * 4.0
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((0, 0, 1), math.radians(-75 + spread))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((0, 0, 1), math.radians(75 - spread))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 6: Travis_UnderwaterFloat
# -------------------------------------------------------------
print("Generating Travis_UnderwaterFloat...")
reset_pose()
act_water = bpy.data.actions.new("Travis_UnderwaterFloat")
new_arm.animation_data.action = act_water
total_f = 180
for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((1, 0, 0), math.radians(math.sin(phi) * 10))
        spine2.keyframe_insert("rotation_quaternion", frame=f)
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(20 + math.sin(phi) * 15)) @ Quaternion((0, 0, 1), math.radians(-45))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(20 + math.sin(phi) * 15)) @ Quaternion((0, 0, 1), math.radians(45))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

# -------------------------------------------------------------
# Action 7: Travis_FinaleTitanSlam
# -------------------------------------------------------------
print("Generating Travis_FinaleTitanSlam...")
reset_pose()
act_slam = bpy.data.actions.new("Travis_FinaleTitanSlam")
new_arm.animation_data.action = act_slam
total_f = 150
for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    if f <= 60:
        p = f / 60.0
        p_smooth = p * p * (3.0 - 2.0 * p)
        if head:
            head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(p_smooth * 45))
            head.keyframe_insert("rotation_quaternion", frame=f)
        if arm_l and arm_r:
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((0, 0, 1), math.radians(-p_smooth * 135))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((0, 0, 1), math.radians(p_smooth * 135))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
    elif f <= 75:
        p = (f - 60) / 15.0
        crouch = p * 45.0
        if head:
            head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-p * 30))
            head.keyframe_insert("rotation_quaternion", frame=f)
        if arm_l and arm_r:
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(p * 90))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(p * 90))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
        if thigh_l and thigh_r:
            thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            thigh_l.keyframe_insert("rotation_quaternion", frame=f)
            thigh_r.keyframe_insert("rotation_quaternion", frame=f)
    else:
        p = (f - 75) / 75.0
        p_smooth = p * p * (3.0 - 2.0 * p)
        crouch = 45.0 * (1.0 - p_smooth)
        if arm_l and arm_r:
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(90 * (1.0 - p_smooth)))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(90 * (1.0 - p_smooth)))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
        if thigh_l and thigh_r:
            thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            thigh_l.keyframe_insert("rotation_quaternion", frame=f)
            thigh_r.keyframe_insert("rotation_quaternion", frame=f)

# Clean up every object belonging to the old imported animation donor.  Removing
# only its armature left a second scene root in the GLB and confused the runtime
# skin/bone hierarchy.
bpy.ops.object.mode_set(mode='OBJECT')
old_root = old_arm
while old_root.parent is not None:
    old_root = old_root.parent
to_delete = [old_root]
for o in list(bpy.data.objects):
    p = o.parent
    while p is not None:
        if p == old_root:
            to_delete.append(o)
            break
        p = p.parent
for o in set(to_delete):
    bpy.data.objects.remove(o, do_unlink=True)

out_dest = r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb"
print("Exporting clean normalized GLB to:", out_dest)
bpy.ops.export_scene.gltf(
    filepath=out_dest,
    export_format='GLB',
    export_animation_mode='ACTIONS'
)
print("=== Done! Model successfully normalized to 1.95m and exported! ===")
