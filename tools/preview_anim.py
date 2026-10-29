"""blender -b -P preview_anim.py -- in.glb out_prefix action_prefix frame [frame ...]   renders the model (workbench, texture colours) with an action from its glb at the given frames, front + side, camera framed on the evaluated bounds."""
import bpy, sys, math, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out, act = a[:3]; frames = [int(x) for x in a[3:]] or [0]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']; actions = [x for x in bpy.data.actions if x.name.startswith(act) or act in x.name]
print('actions', [x.name for x in bpy.data.actions][:30], 'using', actions[0].name if actions else None)
for ar in arms:
    ar.animation_data_create(); ar.animation_data.action = actions[0]
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'; sc.render.resolution_x = 700; sc.render.resolution_y = 700
w = bpy.data.worlds.new('w'); w.color = (0.3, 0.32, 0.4); sc.world = w
c = bpy.data.cameras.new('c'); c.lens = 40; c.clip_end = 5000; co = bpy.data.objects.new('c', c); bpy.context.collection.objects.link(co); sc.camera = co
def bounds():
    dg = bpy.context.evaluated_depsgraph_get(); mn = mathutils.Vector((1e9,) * 3); mx = mathutils.Vector((-1e9,) * 3)
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        for v in me.vertices:
            p = ev.matrix_world @ v.co; mn = mathutils.Vector(map(min, mn, p)); mx = mathutils.Vector(map(max, mx, p))
        ev.to_mesh_clear()
    return mn, mx
for f in frames:
    sc.frame_set(f); mn, mx = bounds(); ctr = (mn + mx) / 2; size = max((mx - mn).length, 0.1)
    for view, off in (('front', mathutils.Vector((0, -1, 0.15))), ('side', mathutils.Vector((1, 0, 0.15)))):
        co.location = ctr + off.normalized() * size * 1.25; d = ctr - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = '%s_%s_f%d.png' % (out, view, f); bpy.ops.render.render(write_still=True)
print('done', mn, mx)
