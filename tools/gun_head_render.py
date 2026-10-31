import bpy, sys, os, mathutils, math
a = sys.argv[sys.argv.index('--') + 1:]; src, out = os.path.abspath(a[0]), os.path.abspath(a[1])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.wm.obj_import(filepath=src, forward_axis='NEGATIVE_Z', up_axis='Y'); sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'; sc.render.resolution_x = 600; sc.render.resolution_y = 600
w = bpy.data.worlds.new('w'); w.color = (0.25, 0.27, 0.35); sc.world = w
COL = {'n277': (0.55, 0.58, 0.65, 1), 'n279': (0.9, 0.7, 0.55, 1), 'n280': (1.0, 0.3, 0.6, 1), 'n282': (0.2, 0.35, 0.9, 1)}
ob = {o.name.split('.')[0]: o for o in bpy.data.objects if o.type == 'MESH'}
for k, o in ob.items(): o.color = COL.get(k, (1, 1, 1, 1))
c = bpy.data.cameras.new('c'); c.type = 'ORTHO'; c.ortho_scale = 0.62; c.clip_end = 100; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
ctr = mathutils.Vector((0, 0, 1.5))
def shoot(name, hide, ang):
    for k, o in ob.items(): o.hide_render = k in hide
    d = mathutils.Vector((math.sin(ang), -math.cos(ang), 0.15)); co.location = ctr + d * 5; v = ctr - co.location; co.rotation_euler = v.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, name); bpy.ops.render.render(write_still=True)
for name, hide in (('all', []), ('nomask', ['n282']), ('nomask_noacc', ['n282', 'n280']), ('onlymask', ['n277', 'n279', 'n280'])):
    shoot(name + '_f', hide, 0.0); shoot(name + '_q', hide, 0.8)
