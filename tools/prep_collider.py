"""blender -b -P prep_collider.py -- <in.glb> <out.glb>
Keeps only the Collider device (tower, clamps, ring, casing, emissive bars, trims, the ascender platform), drops the 1000 m terrain / props / FX, shrinks textures to 512 px, and ANIMATES it (clip, 8 s loop):
the big ring turns, the casing counter-turns, the clamps / trims / bars breathe, a glowing magenta energy beam runs up the tower and pulses, and two counter-rotating energy rings circle the top."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene; sc.render.fps = 24; sc.frame_start = 0; sc.frame_end = 24 * 8
KEEP = {'Object_%d' % k for k in (21, 24, 27, 30, 33, 36, 46)}
keep = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.split('.')[0] in KEEP]; mws = [o.matrix_world.copy() for o in keep]
for o, mw in zip(keep, mws): o.parent = None; o.matrix_world = mw
for o in list(bpy.data.objects):
    if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
for act in list(bpy.data.actions): bpy.data.actions.remove(act)
used = set()
for o in bpy.data.objects:
    for m in o.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image: used.add(n.image)
for im in bpy.data.images:
    if im in used and (im.size[0] > 512 or im.size[1] > 512): im.scale(512, 512)
byname = {o.name.split('.')[0]: o for o in bpy.data.objects}

def key(o, prop, fn, step=6):
    for f in range(0, 24 * 8 + 1, step): setattr(o, prop, fn(f / 24.0)); o.keyframe_insert(prop, frame=f)

# the ring and the casing turn about the tower's own axis (world +Z in Blender after import)
for name, spd in (('Object_30', 1.0), ('Object_33', -0.25)):
    o = byname[name]; o.rotation_mode = 'QUATERNION'; q0 = o.rotation_quaternion.copy(); axis = (o.matrix_world.to_3x3().inverted() @ mathutils.Vector((0, 0, 1))).normalized()
    for f in range(0, 24 * 8 + 1, 6): o.rotation_quaternion = q0 @ mathutils.Quaternion(axis, spd * 2 * math.pi * (f / 24.0) / 8.0); o.keyframe_insert('rotation_quaternion', frame=f)
for name, amp, per in (('Object_21', 0.012, 2.0), ('Object_24', 0.01, 4.0), ('Object_36', 0.01, 4.0), ('Object_27', 0.03, 1.0)):
    o = byname[name]; s0 = tuple(o.scale)
    key(o, 'scale', lambda t, s0=s0, amp=amp, per=per: tuple(v * (1.0 + amp * math.sin(2 * math.pi * t / per)) for v in s0), 3)

# the energy beam: a magenta glowing column up the tower, pulsing; two energy rings round the top
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=1.0, depth=1.0, end_fill_type='NOTHING', location=(0, 0, 0)); beam = bpy.context.active_object; beam.name = 'Beam'
mat = bpy.data.materials.new('A_Beam'); mat.use_nodes = True; nt = mat.node_tree; bsdf = nt.nodes['Principled BSDF']
im = bpy.data.images.new('BeamTex', 8, 64, alpha=True); px = []
for y in range(64):
    for x in range(8):
        v = 1.0 - abs(y / 63.0 - 0.5) * 2; px += [1.0, 0.35 + 0.4 * v, 1.0, 0.1 + 0.4 * v]
im.pixels = px; im.pack(); tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = im; nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color']); nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha']); beam.data.materials.append(mat)
if not beam.data.uv_layers: beam.data.uv_layers.new()
uvl = beam.data.uv_layers[0]
for p in beam.data.polygons:
    for li, vi in zip(p.loop_indices, p.vertices): v = beam.data.vertices[vi].co; uvl.data[li].uv = (0.5, v.z + 0.5)
beam.location = (0, 0, 74.0); beam.scale = (3.5, 3.5, 140.0)
key(beam, 'scale', lambda t: (3.5 * (1.0 + 0.15 * math.sin(2 * math.pi * t / 1.0)), 3.5 * (1.0 + 0.15 * math.sin(2 * math.pi * t / 1.0 + 1)), 140.0), 3)
for k, (r, z) in enumerate(((16.0, 96.0), (12.0, 108.0))):
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.6, major_segments=48, minor_segments=8, location=(0, 0, z)); t = bpy.context.active_object; t.name = 'EnergyRing%d' % k; t.data.materials.append(mat)
    uv = t.data.uv_layers.new()
    for p in t.data.polygons:
        for li in p.loop_indices: uv.data[li].uv = (0.5, 0.5)
    key(t, 'rotation_euler', lambda tt, k=k: (0.12 * math.sin(2 * math.pi * tt / 4 + k), 0.12 * math.cos(2 * math.pi * tt / 4 + k), (1 if k == 0 else -1) * 2 * math.pi * tt / 8), 3)
print('KEPT', [o.name for o in bpy.data.objects], 'images', len(used))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=True, export_animation_mode='ACTIVE_ACTIONS', export_force_sampling=True, export_image_format='AUTO')
