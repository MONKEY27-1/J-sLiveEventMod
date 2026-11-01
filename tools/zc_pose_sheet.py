"""blender -b -P zc_pose_sheet.py -- in.glb out.png cols view act:time [act:time ...]   one tile per (action, time): the pose of that clip at that time (view = front | side | three)"""
import bpy, sys, os, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out, cols, view = a[0], os.path.abspath(a[1]), int(a[2]), a[3]; specs = a[4:]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.show_shadows = False
TW, TH = 360, 480; sc.render.resolution_x = TW; sc.render.resolution_y = TH
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE']; arm = arm[0] if arm else None
acts = {x.name: x for x in bpy.data.actions}; fps = sc.render.fps
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.clip_end = 1e7; cam.data.clip_start = 0.001
tiles = []; box = None
for sp in specs:
    name, t = sp.rsplit(':', 1); t = float(t); act = acts.get(name) or next((v for k, v in acts.items() if k.endswith(name)), None)
    if arm is not None and act is not None:
        arm.animation_data_create(); arm.animation_data.action = act
        try: arm.animation_data.action_slot = act.slots[0]
        except Exception: pass
    sc.frame_set(int(round(t * fps)) + 1); dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg)
            for v in ev.data.vertices:
                p = ev.matrix_world @ v.co
                for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
    if box is None: c0 = (lo + hi) / 2; size = max(hi - lo) * 1.35; box = (c0, size)     # first tile fixes the framing so poses compare
    c, size = box; cam.data.ortho_scale = size
    d = {'front': mathutils.Vector((0, -1, 0.08)), 'side': mathutils.Vector((1, 0, 0.08)), 'three': mathutils.Vector((0.6, -1, 0.1))}[view].normalized()
    cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = os.path.join(os.path.dirname(out), '_t.png'); bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(sc.render.filepath); w, h = im.size; tiles.append(np.array(im.pixels[:], np.float32).reshape(h, w, 4)); bpy.data.images.remove(im)
rows = (len(tiles) + cols - 1) // cols; S = np.zeros((TH * rows, TW * cols, 4), np.float32); S[..., 3] = 1
for k, t in enumerate(tiles): r, c = k // cols, k % cols; S[(rows - 1 - r) * TH:(rows - r) * TH, c * TW:(c + 1) * TW] = t
img = bpy.data.images.new('sheet', S.shape[1], S.shape[0]); img.pixels = S.ravel().tolist(); img.filepath_raw = out; img.file_format = 'PNG'; img.save(); print('SHEET', out, S.shape, 'actions', sorted(acts)[:12])
