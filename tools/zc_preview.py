"""blender -b -P zc_preview.py -- out_sheet.png cols in1.glb in2.glb ...   one front-view tile (500x600, Workbench textured) per glb, tiled into a sheet; prints bounds per model"""
import bpy, sys, os, math, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; out = os.path.abspath(a[0]); cols = int(a[1]); files = a[2:]
TW, TH = 400, 520; tiles = []
for f in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    (bpy.ops.wm.obj_import(filepath=f) if f.endswith('.obj') else bpy.ops.import_scene.gltf(filepath=f)); sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = TW; sc.render.resolution_y = TH
    dg = bpy.context.evaluated_depsgraph_get(); lo = mathutils.Vector((1e9,) * 3); hi = mathutils.Vector((-1e9,) * 3)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg)
            for v in ev.data.vertices:
                p = ev.matrix_world @ v.co
                for k in range(3): lo[k] = min(lo[k], p[k]); hi[k] = max(hi[k], p[k])
    c = (lo + hi) / 2; size = max(hi - lo)
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.clip_end = 1e7; cam.data.clip_start = 0.001; cam.data.ortho_scale = size * 1.1
    d = mathutils.Vector((0.35, -1, 0.15)).normalized(); cam.location = c + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(os.path.dirname(out), '_tile.png'); bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(sc.render.filepath); w, h = im.size; px = np.array(im.pixels[:], np.float32).reshape(h, w, 4); tiles.append(px)
    print('MODEL', os.path.basename(f), 'size', tuple(round(x, 2) for x in (hi - lo)), 'min', tuple(round(x, 2) for x in lo))
rows = (len(tiles) + cols - 1) // cols
S = np.zeros((TH * rows, TW * cols, 4), np.float32); S[..., 3] = 1
for k, t in enumerate(tiles): r, c = k // cols, k % cols; S[(rows - 1 - r) * TH:(rows - r) * TH, c * TW:(c + 1) * TW] = t
img = bpy.data.images.new('sheet', S.shape[1], S.shape[0]); img.pixels = S.ravel().tolist(); img.filepath_raw = out; img.file_format = 'PNG'; img.save(); print('SHEET', out, S.shape)
