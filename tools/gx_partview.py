"""blender -b -P gx_partview.py -- in.glb out.png [node=rx,ry,rz (degrees) ...]  front render of the split Galactus, every part its own colour; optional per-node rotations to test the pivots"""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]; rots = {x.split('=')[0]: [float(v) for v in x.split('=')[1].split(',')] for x in a[2:]}
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
cols = {'Torso': (0.6, 0.6, 0.6, 1), 'Head': (1, 0.3, 0.3, 1), 'ShL': (0.3, 1, 0.3, 1), 'ElL': (0.3, 1, 1, 1), 'ShR': (1, 1, 0.2, 1), 'ElR': (1, 0.4, 1, 1)}
for o in bpy.data.objects:
    nm = o.name.split('.')[0]
    if nm in cols and o.type == 'MESH': o.color = cols[nm]
    if nm in rots: o.rotation_mode = 'XYZ'; o.rotation_euler = [math.radians(v) for v in rots[nm]]
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
for o in bpy.data.objects:
    if o.type == 'MESH':
        ev = o.evaluated_get(dg)
        for v in ev.data.vertices:
            p = ev.matrix_world @ v.co
            for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
c = (lo + hi) / 2; size = max(hi - lo); cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.1; cam.data.clip_end = 1e5
d = mathutils.Vector((0.0, -1, 0.05)).normalized(); cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = out; bpy.ops.render.render(write_still=True)
