"""blender -b -P room_preview.py -- <in.glb> <out_prefix> <x y z yaw_deg pitch_deg fov>...   first-person style perspective renders of a room (Blender coordinates, camera looks along +Y at yaw 0, textures shown unlit-ish)"""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]; views = [a[i:i + 6] for i in range(2, len(a), 6)]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try: sc.render.engine = eng; break
    except Exception: pass
sc.render.resolution_x = 960; sc.render.resolution_y = 600
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.3, 0.3, 0.35, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1; sc.world = w
for m in bpy.data.materials:
    if m.node_tree:
        b = m.node_tree.nodes.get('Principled BSDF'); tex = [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']
        if b and tex:
            try: m.node_tree.links.new(tex[0].outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 0.9
            except Exception: pass
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.clip_start = 0.02; cam.data.clip_end = 500
for i, v in enumerate(views):
    x, y, z, yaw, pitch, fov = [float(q) for q in v]; cam.data.angle = math.radians(fov); cam.location = (x, y, z)
    # the glTF import is Y-up -> Blender Z-up with X mirrored by the importer's axis conversion; we look along the importer's +Y here
    cam.rotation_euler = mathutils.Euler((math.radians(90 + pitch), 0, math.radians(yaw)), 'XYZ'); sc.render.filepath = '%s%d.png' % (out, i); bpy.ops.render.render(write_still=True)
print('done')
