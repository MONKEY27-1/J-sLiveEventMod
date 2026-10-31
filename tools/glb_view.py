"""blender -b -P glb_view.py -- model.glb out_prefix "name=cx,cy,cz,tx,ty,tz[,ortho_scale]" ...
Renders workbench previews of a glb from camera positions given in UNITY coordinates (x right, y up, z forward; glTF -> Blender maps (x, y, z) to (x, -z, y), which is what the importer does)."""
import bpy, sys, math, mathutils, os
a = sys.argv[sys.argv.index('--') + 1:]; glb, out = os.path.abspath(a[0]), os.path.abspath(a[1]); views = a[2:]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_cavity = False; sh.show_shadows = False
sc.render.resolution_x = int(os.environ.get('RX', '1000')); sc.render.resolution_y = int(os.environ.get('RY', '640'))
w = bpy.data.worlds.new('w'); w.color = (0.04, 0.04, 0.07); sc.world = w
for o in bpy.data.objects:                                  # the importer keeps the animation; pose at the middle of the first clip so rings are visible
    if os.environ.get('HIDE') and o.name in os.environ['HIDE'].split(','): o.hide_render = True
c = bpy.data.cameras.new('c'); c.clip_end = 4000; c.clip_start = 0.05; c.lens = float(os.environ.get('LENS', '22')); co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
def U(x, y, z): return mathutils.Vector((x, -z, y))
for v in views:
    name, rest = v.split('='); n = [float(x) for x in rest.split(',')]; loc = U(*n[:3]); tgt = U(*n[3:6])
    c.type = 'ORTHO' if len(n) > 6 else 'PERSP'
    if len(n) > 6: c.ortho_scale = n[6]
    co.location = loc; d = tgt - loc; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s.png' % (out, name); bpy.context.view_layer.update(); bpy.ops.render.render(write_still=True)
    print('RENDER', name, sc.render.filepath, os.path.exists(sc.render.filepath))
