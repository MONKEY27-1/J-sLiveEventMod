import bpy, sys, os, json, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; obj, js, out = a[:3]; J = json.load(open(js))
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.wm.obj_import(filepath=obj, forward_axis='Y', up_axis='Z')   # Unity (x, y up, z) -> file as Blender Z-up: obj import with up Z keeps our Y as Blender -Y? use explicit remap below
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'; sc.render.resolution_x = 1100; sc.render.resolution_y = 700
w = bpy.data.worlds.new('w'); w.color = (0.08, 0.07, 0.12); sc.world = w
def U(v): return mathutils.Vector((v[0], -v[2], v[1]))        # Unity -> Blender coordinates (x, z->y, y->z) for cameras
# the OBJ was written as Unity (x, y, z); the importer (forward Y, up Z) mapped file y -> Blender y: undo by rotating every mesh so Unity-up lies on Blender Z
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    o.rotation_euler = (math.pi / 2, 0, 0)       # the OBJ is Unity Y-up; Blender cameras track with Z up
    col = tuple(J['cols'].get(o.name.split('.')[0], (0.7, 0.7, 0.7))) + (1,); mt = bpy.data.materials.new(o.name); mt.diffuse_color = col; o.data.materials.clear(); o.data.materials.append(mt)
    if os.environ.get('HIDE') and o.name in os.environ['HIDE'].split(','): o.hide_render = True
    if o.name.startswith('cavern'): o.display_type = 'SOLID'
c = bpy.data.cameras.new('c'); c.clip_end = 3000; c.lens = 24; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
head = U(J['head']); zp = U(J['zp']); l4 = U(J['L4'])
views = J['view'] or {}
V = {'player': (head, (zp - head) * 1.0 + head if (zp - head).length > 1 else head + mathutils.Vector((0, 1, 0))), 'over': (l4 + mathutils.Vector((70, -90, 60)), l4 + mathutils.Vector((-10, 15, 0))), 'side': (mathutils.Vector((0, -330, 120)), mathutils.Vector((0, 0, 100)))}
ORTHO = float(os.environ.get('ORTHO', '0'))
if ORTHO > 0:
    c.type = 'ORTHO'; c.ortho_scale = ORTHO; c.clip_start = 1; c.clip_end = 2000; ctr = mathutils.Vector([float(x) for x in os.environ.get('CTR', '%f,%f,%f' % (J['head'][0], J['head'][1], J['head'][2])).split(',')]); ctr = U(ctr)
    V = {'top': (ctr + mathutils.Vector((0, 0, 600)), ctr), 'east': (ctr + mathutils.Vector((600, 0, 0)), ctr), 'south': (ctr + mathutils.Vector((0, -600, 0)), ctr), 'north': (ctr + mathutils.Vector((0, 600, 0)), ctr)}
    views = None
if views: V = {n: (head if c == 'head' else U([float(x) for x in c.split(',')]), U(tg)) for n, (c, tg) in views.items()}
for name, (loc, tgt) in V.items():
    co.location = loc; d = tgt - loc; print('VIEW', name, tuple(loc), tuple(tgt)); co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, name); bpy.context.view_layer.update(); import bpy_extras; print('PROJ', name, bpy_extras.object_utils.world_to_camera_view(sc, co, tgt)[:]); bpy.ops.render.render(write_still=True)
