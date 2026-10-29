"""blender -b -P preview_obj.py -- in.obj out_prefix   renders an OBJ (glTF Y-up coordinates) from the front (+Z side looking back... i.e. camera at +Z) and from the side, grey shaded."""
import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y'); sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.75, 0.8, 0.9); sc.render.resolution_x = 700; sc.render.resolution_y = 700
w = bpy.data.worlds.new('w'); w.color = (0.25, 0.27, 0.35); sc.world = w
mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
for o in bpy.data.objects:
    if o.type == 'MESH':
        for v in o.data.vertices: p = o.matrix_world @ v.co; mn = mathutils.Vector(map(min, mn, p)); mx = mathutils.Vector(map(max, mx, p))
ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1); c = bpy.data.cameras.new('c'); c.lens = 40; c.clip_end = 5000; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
for view, off in (('front', mathutils.Vector((0, -1, 0.1))), ('side', mathutils.Vector((1, 0, 0.1)))):
    co.location = ctr + off.normalized() * size * 1.2; d = ctr - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, view); bpy.ops.render.render(write_still=True)
print('done')
