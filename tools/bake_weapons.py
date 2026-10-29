"""blender -b -P bake_weapons.py -- <out_dir> <in1.glb> <in2.glb> ...
Turns the Sketchfab weapon models into clean static glbs for the plugin: skins / armatures applied at their rest pose and removed, all meshes joined (materials kept), decimated to <= 15k tris,
textures scaled to <= 1024, resized so the longest side is 1.0, the long axis stood upright (glb +Y), centred.  Output: <out_dir>/<name>.glb  (name = input file name)."""
import bpy, sys, os, math
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index('--') + 1:]; outdir = args[0]; files = args[1:]; os.makedirs(outdir, exist_ok=True)
for path in files:
    name = os.path.splitext(os.path.basename(path))[0]
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH' and len(o.data.materials) > 0]      # (the importer also makes bone-shape helper meshes without materials)
    # apply armature modifiers (rest pose) by converting to plain meshes
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    for o in meshes:
        o.parent = None if o.parent is None else o.parent       # keep the world transform of the parented ones
    bpy.ops.object.convert(target='MESH')
    # world transforms baked into the vertices, then join
    for o in bpy.data.objects:
        if o.type == 'MESH':
            mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and len(o.data.materials) == 0]: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    ms = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in ms:
        o.select_set(True)
        for mod in list(o.modifiers):
            if mod.type == 'ARMATURE': o.modifiers.remove(mod)
    bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(ms) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    for ob in list(bpy.data.objects):
        if ob is not o: bpy.data.objects.remove(ob, do_unlink=True)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > 15000:
        m = o.modifiers.new('dec', 'DECIMATE'); m.ratio = 15000.0 / tris; bpy.ops.object.modifier_apply(modifier='dec')
    # long axis -> Z (becomes glb +Y), centred, longest side 1
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; mn = Vector([min(v[i] for v in bb) for i in range(3)]); mx = Vector([max(v[i] for v in bb) for i in range(3)]); size = mx - mn
    ax = max(range(3), key=lambda i: size[i])
    if ax == 0: o.rotation_euler = (0, math.radians(-90), 0)
    elif ax == 1: o.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bb = [Vector(c) for c in o.bound_box]; mn = Vector([min(v[i] for v in bb) for i in range(3)]); mx = Vector([max(v[i] for v in bb) for i in range(3)]); size = mx - mn; ctr = (mn + mx) / 2
    for v in o.data.vertices: v.co = (v.co - ctr) / max(size)
    for im in bpy.data.images:
        if im.size[0] > 1024 or im.size[1] > 1024: im.scale(1024, 1024)
    o.rotation_euler = (0, 0, 0); o.location = (0, 0, 0); o.scale = (1, 1, 1)
    bpy.ops.export_scene.gltf(filepath=os.path.join(outdir, name + '.glb'), export_format='GLB', export_yup=True, export_animations=False, export_image_format='AUTO')
    print('baked', name, 'tris', len(o.data.polygons), 'size', tuple(round(x, 3) for x in size), 'mats', len(o.data.materials))
