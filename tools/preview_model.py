"""blender -b -P preview_model.py -- <in.glb> <out_prefix> <frame> [frame ...]   front + side renders of a skinned model (evaluated pose), the camera framed on the evaluated mesh bounds."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]; frames = [int(x) for x in a[2:]] or [0]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
    try: sc.render.engine = eng; break
    except Exception: pass
sc.render.resolution_x = 700; sc.render.resolution_y = 700
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.25, 0.27, 0.35, 1); sc.world = w
for m in bpy.data.materials:
    if m.node_tree:
        b = m.node_tree.nodes.get('Principled BSDF'); tex = [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']
        if b and tex:
            try: m.node_tree.links.new(tex[0].outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 1.0
            except Exception: pass
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.clip_end = 1e6; cam.data.clip_start = 0.001
for i, f in enumerate(frames):
    sc.frame_set(f); dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg)
            for v in ev.data.vertices:
                p = ev.matrix_world @ v.co
                for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
    c = (lo + hi) / 2; size = max(hi - lo); cam.data.ortho_scale = size * 1.15
    for k, d in enumerate((mathutils.Vector((0, -1, 0.1)), mathutils.Vector((1, 0, 0.1)))):
        d = d.normalized(); cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s%d_%d.png' % (out, i, k); bpy.ops.render.render(write_still=True)
    print('frame', f, 'bounds', tuple(round(x, 2) for x in lo), tuple(round(x, 2) for x in hi))
