"""blender -b -P gun_head_view.py -- model.glb out_prefix  : head close-ups of Gunnar with the mask / face accessories hidden or shown (front and 3/4 views)"""
import bpy, sys, os, mathutils, math
a = sys.argv[sys.argv.index('--') + 1:]; glb, out = os.path.abspath(a[0]), os.path.abspath(a[1])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_cavity = False
sc.render.resolution_x = 700; sc.render.resolution_y = 700; w = bpy.data.worlds.new('w'); w.color = (0.3, 0.32, 0.4); sc.world = w
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in meshes: print('OBJ', o.name, len(o.data.vertices))
def bbox(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]; mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))); return mn, mx
head = next(o for o in meshes if o.name == 'Object_279'); mn, mx = bbox(head); ctr = (mn + mx) / 2; size = max((mx - mn).x, (mx - mn).y, (mx - mn).z); print('HEAD', mn, mx, 'size', size)
c = bpy.data.cameras.new('c'); c.type = 'ORTHO'; c.ortho_scale = size * 2.2; c.clip_end = 1000; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
def shoot(name, hide, ang):
    for o in meshes: o.hide_render = o.name.startswith('Icosphere') or any(h in o.name for h in hide)
    d = mathutils.Vector((math.sin(ang), -math.cos(ang), 0.0)); co.location = ctr + d * size * 6; v = ctr - co.location; co.rotation_euler = v.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, name); bpy.ops.render.render(write_still=True)
for name, hide in (('all', []), ('nomask', ['Object_282']), ('nomask_noacc', ['Object_282', 'Object_280']), ('maskonly', ['Object_279', 'Object_280', 'Object_277'])):
    shoot(name + '_f', hide, 0.0); shoot(name + '_q', hide, 0.7)
