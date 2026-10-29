import bpy, math

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")

arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')

# Let's inspect existing actions
print("Initial actions in blend file:")
for a in bpy.data.actions:
    print(" ", a.name)

# Create a test action
test_act = bpy.data.actions.new("Travis_TestHype")
arm.animation_data.action = test_act

# Find pelvis and head
pelvis = [pb for pb in arm.pose.bones if "pelvis" in pb.name.lower()][0]
head = [pb for pb in arm.pose.bones if "head" in pb.name.lower()][0]

pelvis.rotation_mode = 'XYZ'
head.rotation_mode = 'XYZ'

for frame in range(1, 61):
    bpy.context.scene.frame_set(frame)
    t = frame / 30.0 # 2 seconds total
    # 2 Hz bounce
    bounce_angle = math.sin(t * math.pi * 4) * 0.15
    pelvis.rotation_euler = (bounce_angle, 0, 0)
    pelvis.keyframe_insert(data_path="rotation_euler", frame=frame)
    head.rotation_euler = (bounce_angle * 1.5, 0, 0)
    head.keyframe_insert(data_path="rotation_euler", frame=frame)

print(f"Keyframed 60 frames on {test_act.name}!")
