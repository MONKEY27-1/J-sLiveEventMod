import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y'); sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'; sc.render.resolution_x = 1400; sc.render.resolution_y = 560
w = bpy.data.worlds.new('w'); w.color = (0.22, 0.24, 0.32); sc.world = w
mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.color = (0.9, 0.35, 0.2, 1) if 'gun' in o.name else (0.75, 0.8, 0.9, 1)
        for v in o.data.vertices: p = o.matrix_world @ v.co; mn = mathutils.Vector(map(min, mn, p)); mx = mathutils.Vector(map(max, mx, p))
ctr = (mn + mx) / 2; c = bpy.data.cameras.new('c'); c.type = 'ORTHO'; c.clip_end = 5000; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
c.ortho_scale = max((mx.x - mn.x) * 1.05, (mx.z - mn.z) * 1.05, (mx.y - mn.y) * 2.5 * 1.05)
for view, off in (('front', mathutils.Vector((0, -1, 0))), ('side', mathutils.Vector((1, 0, 0)))):
    co.location = ctr + off * 30; d = ctr - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, view); bpy.ops.render.render(write_still=True)
