import bpy, math
from mathutils import Vector, Quaternion, Euler

print("Starting Travis Scott animation builder...")
bpy.ops.wm.read_factory_settings(use_empty=True)

# 1. Load old avatar to get track1
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

# 2. Load new skin
skin_path = r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"
bpy.ops.import_scene.gltf(filepath=skin_path)
new_arm = [o for o in bpy.data.objects if o.type == 'ARMATURE' and o != old_arm][0]
new_arm.name = "NewArmature"

# Map bones
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

print(f"Mapped {len(mapping)} bones between armatures.")

bpy.context.view_layer.objects.active = new_arm
bpy.ops.object.mode_set(mode='POSE')

# Helper to find pose bone by keyword
def find_bone(*keywords):
    for pb in new_arm.pose.bones:
        name_lower = pb.name.lower()
        if all(k in name_lower for k in keywords):
            return pb
    return None

pelvis = find_bone("pelvis")
spine1 = find_bone("spine", "01")
spine2 = find_bone("spine", "02")
spine3 = find_bone("spine", "03")
spine4 = find_bone("spine", "04")
spine5 = find_bone("spine", "05")
neck1 = find_bone("neck", "01")
head = find_bone("head")
clav_l = find_bone("clavicle", "l")
clav_r = find_bone("clavicle", "r")
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
foot_l = find_bone("foot", "l")
foot_r = find_bone("foot", "r")

print(f"Key bones found: pelvis={pelvis.name if pelvis else None}, head={head.name if head else None}, arm_l={arm_l.name if arm_l else None}, arm_r={arm_r.name if arm_r else None}")

# Store rest rotations for key bones
rest_quats = {pb.name: pb.rotation_quaternion.copy() for pb in new_arm.pose.bones}
rest_locs = {pb.name: pb.location.copy() for pb in new_arm.pose.bones}

def reset_pose():
    for pb in new_arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = rest_quats[pb.name].copy()
        pb.location = rest_locs[pb.name].copy()

# -------------------------------------------------------------
# Action 1: Retarget track1_7473-8320_PRC from old avatar
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
                if "pelvis" in nb_name.lower() or "root" in nb_name.lower():
                    npb.location = opb.location
                    npb.keyframe_insert("location", frame=f)
    print("track1_7473-8320_PRC retarget complete!")

# -------------------------------------------------------------
# Action 2: Travis_SickoMode_Hype
# -------------------------------------------------------------
print("Generating Travis_SickoMode_Hype...")
reset_pose()
act_hype = bpy.data.actions.new("Travis_SickoMode_Hype")
new_arm.animation_data.action = act_hype
total_f = 120 # 4 seconds at 30 fps
bpm = 130.0
beats_per_sec = bpm / 60.0 # ~2.167 beats/sec

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    t = f / 30.0
    beat_phase = t * beats_per_sec * 2.0 * math.pi
    
    # Pelvis bounce & hip sway
    bounce = -abs(math.sin(beat_phase)) * 0.12 # drops on the kick
    sway = math.sin(t * beats_per_sec * math.pi) * 0.05
    if pelvis:
        pelvis.location = rest_locs[pelvis.name] + Vector((sway, 0, bounce))
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((0, 1, 0), math.radians(sway * 50)) @ Quaternion((1, 0, 0), math.radians(bounce * 30))
        pelvis.keyframe_insert("location", frame=f)
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
    
    # Spine leaning into the rap
    spine_nod = math.sin(beat_phase) * 0.1
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((1, 0, 0), spine_nod * 0.5)
        spine2.keyframe_insert("rotation_quaternion", frame=f)
    if spine4:
        spine4.rotation_quaternion = rest_quats[spine4.name] @ Quaternion((1, 0, 0), spine_nod * 0.7)
        spine4.keyframe_insert("rotation_quaternion", frame=f)
        
    # Head aggressive headbob
    if head:
        head_pitch = -abs(math.sin(beat_phase)) * 0.25 - 0.05
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), head_pitch) @ Quaternion((0, 0, 1), math.sin(beat_phase * 0.5) * 0.08)
        head.keyframe_insert("rotation_quaternion", frame=f)
        
    # Left Arm: Holding mic near mouth, bouncing with beat
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(35 + math.sin(beat_phase) * 8)) @ Quaternion((0, 0, 1), math.radians(-25))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_l:
        forearm_l.rotation_quaternion = rest_quats[forearm_l.name] @ Quaternion((1, 0, 0), math.radians(75 + math.sin(beat_phase) * 10))
        forearm_l.keyframe_insert("rotation_quaternion", frame=f)
    if hand_l:
        hand_l.rotation_quaternion = rest_quats[hand_l.name] @ Quaternion((0, 1, 0), math.radians(-20))
        hand_l.keyframe_insert("rotation_quaternion", frame=f)
        
    # Right Arm: Pumping the crowd, pointing, downbeat gestures
    if arm_r:
        arm_r_pump = math.sin(beat_phase) * 20
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(45 + arm_r_pump)) @ Quaternion((0, 0, 1), math.radians(35))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_r:
        forearm_r.rotation_quaternion = rest_quats[forearm_r.name] @ Quaternion((1, 0, 0), math.radians(40 + math.cos(beat_phase) * 15))
        forearm_r.keyframe_insert("rotation_quaternion", frame=f)
    if hand_r:
        hand_r.rotation_quaternion = rest_quats[hand_r.name] @ Quaternion((1, 0, 0), math.radians(math.sin(beat_phase) * 25))
        hand_r.keyframe_insert("rotation_quaternion", frame=f)
        
    # Knees flexing with bounce
    if thigh_l and calf_l:
        knee_bend = abs(bounce) * 2.0
        thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(knee_bend * 30))
        thigh_l.keyframe_insert("rotation_quaternion", frame=f)
        calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-knee_bend * 40))
        calf_l.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_r and calf_r:
        knee_bend = abs(bounce) * 2.0
        thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(knee_bend * 30))
        thigh_r.keyframe_insert("rotation_quaternion", frame=f)
        calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-knee_bend * 40))
        calf_r.keyframe_insert("rotation_quaternion", frame=f)

print("Travis_SickoMode_Hype complete!")

# -------------------------------------------------------------
# Action 3: Travis_GiantWalk
# -------------------------------------------------------------
print("Generating Travis_GiantWalk...")
reset_pose()
act_walk = bpy.data.actions.new("Travis_GiantWalk")
new_arm.animation_data.action = act_walk
total_f = 120 # 4 second stride loop

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    
    # Stride angle
    stride_l = math.sin(phi) * 25.0
    stride_r = -stride_l
    
    # Pelvis sway and bob
    if pelvis:
        p_bob = -abs(math.sin(phi * 2.0)) * 0.08
        p_sway = math.cos(phi) * 0.08
        pelvis.location = rest_locs[pelvis.name] + Vector((p_sway, 0, p_bob))
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((0, 1, 0), math.radians(math.cos(phi) * 8)) @ Quaternion((0, 0, 1), math.radians(math.sin(phi) * 4))
        pelvis.keyframe_insert("location", frame=f)
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
        
    # Spine counter-twist
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((0, 1, 0), math.radians(-math.cos(phi) * 6))
        spine2.keyframe_insert("rotation_quaternion", frame=f)
        
    # Head looking down at audience
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-15 + math.sin(phi * 2) * 3))
        head.keyframe_insert("rotation_quaternion", frame=f)
        
    # Legs
    if thigh_l and calf_l:
        thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(stride_l))
        thigh_l.keyframe_insert("rotation_quaternion", frame=f)
        bend_l = max(0.0, -stride_l) * 1.5
        calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-bend_l))
        calf_l.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_r and calf_r:
        thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(stride_r))
        thigh_r.keyframe_insert("rotation_quaternion", frame=f)
        bend_r = max(0.0, -stride_r) * 1.5
        calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-bend_r))
        calf_r.keyframe_insert("rotation_quaternion", frame=f)
        
    # Arms swing in opposition
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(-stride_l * 0.9)) @ Quaternion((0, 0, 1), math.radians(-15))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(-stride_r * 0.9)) @ Quaternion((0, 0, 1), math.radians(15))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

print("Travis_GiantWalk complete!")

# -------------------------------------------------------------
# Action 4: Travis_CosmicLevitate (Stargazing / Moon sequence)
# -------------------------------------------------------------
print("Generating Travis_CosmicLevitate...")
reset_pose()
act_cosmic = bpy.data.actions.new("Travis_CosmicLevitate")
new_arm.animation_data.action = act_cosmic
total_f = 180 # 6 seconds slow float

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    
    float_y = math.sin(phi) * 0.25
    if pelvis:
        pelvis.location = rest_locs[pelvis.name] + Vector((0, 0, float_y))
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(8 + math.sin(phi) * 3))
        pelvis.keyframe_insert("location", frame=f)
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
        
    # Arched spine
    if spine3:
        spine3.rotation_quaternion = rest_quats[spine3.name] @ Quaternion((1, 0, 0), math.radians(10 + math.sin(phi) * 2))
        spine3.keyframe_insert("rotation_quaternion", frame=f)
    if spine5:
        spine5.rotation_quaternion = rest_quats[spine5.name] @ Quaternion((1, 0, 0), math.radians(8 + math.sin(phi) * 2))
        spine5.keyframe_insert("rotation_quaternion", frame=f)
        
    # Head looking up at stars
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(35 + math.sin(phi) * 5))
        head.keyframe_insert("rotation_quaternion", frame=f)
        
    # Outstretched majestic arms (wings / T-pose)
    arm_spread = math.sin(phi) * 4.0
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((0, 0, 1), math.radians(-75 + arm_spread)) @ Quaternion((0, 1, 0), math.radians(10))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((0, 0, 1), math.radians(75 - arm_spread)) @ Quaternion((0, 1, 0), math.radians(-10))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_l:
        forearm_l.rotation_quaternion = rest_quats[forearm_l.name] @ Quaternion((1, 0, 0), math.radians(15 + math.sin(phi) * 3))
        forearm_l.keyframe_insert("rotation_quaternion", frame=f)
    if forearm_r:
        forearm_r.rotation_quaternion = rest_quats[forearm_r.name] @ Quaternion((1, 0, 0), math.radians(15 + math.sin(phi) * 3))
        forearm_r.keyframe_insert("rotation_quaternion", frame=f)
        
    # Dangling legs in zero-G
    if thigh_l:
        thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(-10 + math.sin(phi + 0.5) * 4))
        thigh_l.keyframe_insert("rotation_quaternion", frame=f)
    if thigh_r:
        thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(-10 + math.cos(phi + 0.5) * 4))
        thigh_r.keyframe_insert("rotation_quaternion", frame=f)

print("Travis_CosmicLevitate complete!")

# -------------------------------------------------------------
# Action 5: Travis_UnderwaterFloat (Highest in the Room)
# -------------------------------------------------------------
print("Generating Travis_UnderwaterFloat...")
reset_pose()
act_water = bpy.data.actions.new("Travis_UnderwaterFloat")
new_arm.animation_data.action = act_water
total_f = 180

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    phi = (f / float(total_f)) * 2.0 * math.pi
    
    # Smooth sine wave along body
    if pelvis:
        pelvis.location = rest_locs[pelvis.name] + Vector((math.sin(phi * 0.5) * 0.15, 0, math.cos(phi) * 0.18))
        pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(math.sin(phi) * 12)) @ Quaternion((0, 1, 0), math.radians(math.cos(phi) * 8))
        pelvis.keyframe_insert("location", frame=f)
        pelvis.keyframe_insert("rotation_quaternion", frame=f)
        
    if spine2:
        spine2.rotation_quaternion = rest_quats[spine2.name] @ Quaternion((1, 0, 0), math.radians(math.sin(phi - 0.4) * 8))
        spine2.keyframe_insert("rotation_quaternion", frame=f)
    if spine5:
        spine5.rotation_quaternion = rest_quats[spine5.name] @ Quaternion((1, 0, 0), math.radians(math.sin(phi - 0.8) * 8))
        spine5.keyframe_insert("rotation_quaternion", frame=f)
    if head:
        head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(math.sin(phi - 1.2) * 12)) @ Quaternion((0, 0, 1), math.radians(math.cos(phi) * 6))
        head.keyframe_insert("rotation_quaternion", frame=f)
        
    # Swimming arms
    stroke = math.sin(phi) * 20.0
    if arm_l:
        arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(20 + stroke)) @ Quaternion((0, 0, 1), math.radians(-45 - stroke * 0.5))
        arm_l.keyframe_insert("rotation_quaternion", frame=f)
    if arm_r:
        arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(20 + stroke)) @ Quaternion((0, 0, 1), math.radians(45 + stroke * 0.5))
        arm_r.keyframe_insert("rotation_quaternion", frame=f)

print("Travis_UnderwaterFloat complete!")

# -------------------------------------------------------------
# Action 6: Travis_FinaleTitanSlam
# -------------------------------------------------------------
print("Generating Travis_FinaleTitanSlam...")
reset_pose()
act_slam = bpy.data.actions.new("Travis_FinaleTitanSlam")
new_arm.animation_data.action = act_slam
total_f = 150 # 5 seconds

for f in range(1, total_f + 1):
    bpy.context.scene.frame_set(f)
    
    if f <= 60:
        # Phase 1: Buildup (arms raise high overhead, energy gathering)
        p = f / 60.0
        p_smooth = p * p * (3.0 - 2.0 * p)
        shake = math.sin(f * 2.0) * (p * 0.04) # intense vibration
        
        if pelvis:
            pelvis.location = rest_locs[pelvis.name] + Vector((shake, 0, p_smooth * 0.3))
            pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(p_smooth * 15))
            pelvis.keyframe_insert("location", frame=f)
            pelvis.keyframe_insert("rotation_quaternion", frame=f)
        if head:
            head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(p_smooth * 45))
            head.keyframe_insert("rotation_quaternion", frame=f)
        if arm_l and arm_r:
            # Raise both arms straight up to heaven
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((0, 0, 1), math.radians(-p_smooth * 135)) @ Quaternion((1, 0, 0), math.radians(p_smooth * 30))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((0, 0, 1), math.radians(p_smooth * 135)) @ Quaternion((1, 0, 0), math.radians(p_smooth * 30))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
            
    elif f <= 75:
        # Phase 2: Violent downward slam! (frames 61 to 75)
        p = (f - 60) / 15.0 # 0 to 1
        # Quick plunge down
        slam_y = -p * 0.6
        crouch = p * 60.0
        
        if pelvis:
            pelvis.location = rest_locs[pelvis.name] + Vector((0, 0, slam_y))
            pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(-p * 35))
            pelvis.keyframe_insert("location", frame=f)
            pelvis.keyframe_insert("rotation_quaternion", frame=f)
        if head:
            head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-p * 30))
            head.keyframe_insert("rotation_quaternion", frame=f)
        if arm_l and arm_r:
            # Slam both fists down toward ground
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(p * 90)) @ Quaternion((0, 0, 1), math.radians(-20))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(p * 90)) @ Quaternion((0, 0, 1), math.radians(20))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
        if thigh_l and calf_l and thigh_r and calf_r:
            thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-crouch * 1.3))
            thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-crouch * 1.3))
            thigh_l.keyframe_insert("rotation_quaternion", frame=f)
            calf_l.keyframe_insert("rotation_quaternion", frame=f)
            thigh_r.keyframe_insert("rotation_quaternion", frame=f)
            calf_r.keyframe_insert("rotation_quaternion", frame=f)
            
    else:
        # Phase 3: Recoil & recovery (frames 76 to 150)
        p = (f - 75) / 75.0
        p_smooth = p * p * (3.0 - 2.0 * p)
        # Recover back to standing
        slam_y = -0.6 * (1.0 - p_smooth)
        crouch = 60.0 * (1.0 - p_smooth)
        
        if pelvis:
            pelvis.location = rest_locs[pelvis.name] + Vector((0, 0, slam_y))
            pelvis.rotation_quaternion = rest_quats[pelvis.name] @ Quaternion((1, 0, 0), math.radians(-35 * (1.0 - p_smooth)))
            pelvis.keyframe_insert("location", frame=f)
            pelvis.keyframe_insert("rotation_quaternion", frame=f)
        if head:
            head.rotation_quaternion = rest_quats[head.name] @ Quaternion((1, 0, 0), math.radians(-30 * (1.0 - p_smooth)))
            head.keyframe_insert("rotation_quaternion", frame=f)
        if arm_l and arm_r:
            arm_l.rotation_quaternion = rest_quats[arm_l.name] @ Quaternion((1, 0, 0), math.radians(90 * (1.0 - p_smooth))) @ Quaternion((0, 0, 1), math.radians(-20))
            arm_r.rotation_quaternion = rest_quats[arm_r.name] @ Quaternion((1, 0, 0), math.radians(90 * (1.0 - p_smooth))) @ Quaternion((0, 0, 1), math.radians(20))
            arm_l.keyframe_insert("rotation_quaternion", frame=f)
            arm_r.keyframe_insert("rotation_quaternion", frame=f)
        if thigh_l and calf_l and thigh_r and calf_r:
            thigh_l.rotation_quaternion = rest_quats[thigh_l.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            calf_l.rotation_quaternion = rest_quats[calf_l.name] @ Quaternion((1, 0, 0), math.radians(-crouch * 1.3))
            thigh_r.rotation_quaternion = rest_quats[thigh_r.name] @ Quaternion((1, 0, 0), math.radians(crouch))
            calf_r.rotation_quaternion = rest_quats[calf_r.name] @ Quaternion((1, 0, 0), math.radians(-crouch * 1.3))
            thigh_l.keyframe_insert("rotation_quaternion", frame=f)
            calf_l.keyframe_insert("rotation_quaternion", frame=f)
            thigh_r.keyframe_insert("rotation_quaternion", frame=f)
            calf_r.keyframe_insert("rotation_quaternion", frame=f)

print("Travis_FinaleTitanSlam complete!")

# -------------------------------------------------------------
# Clean up old armature and export final GLB
# -------------------------------------------------------------
bpy.ops.object.mode_set(mode='OBJECT')
# Delete old_arm and its children meshes
for o in list(old_arm.children) + [old_arm]:
    bpy.data.objects.remove(o, do_unlink=True)

out_dest = r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb"
print("Exporting final GLB to:", out_dest)
bpy.ops.export_scene.gltf(
    filepath=out_dest,
    export_format='GLB',
    export_animation_mode='ACTIONS'
)
print("Travis Scott GLB successfully created and exported!")
