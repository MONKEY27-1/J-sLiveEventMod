"""blender -b -P gx_tile.py -- out.png cols scale img1 img2 ...   contact sheet of PNGs (downscaled by `scale`, e.g. 4)"""
import bpy, sys, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; out, cols, sc = a[0], int(a[1]), int(a[2]); ims = []
for p in a[3:]:
    im = bpy.data.images.load(p); w, h = im.size; px = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(px); px = px.reshape(h, w, 4)[::sc, ::sc]; ims.append(px); bpy.data.images.remove(im)
h, w = ims[0].shape[:2]; rows = (len(ims) + cols - 1) // cols; S = np.zeros((rows * h, cols * w, 4), np.float32); S[..., 3] = 1
for i, px in enumerate(ims): r, c = i // cols, i % cols; S[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px[:h, :w]
o = bpy.data.images.new('s', cols * w, rows * h); o.pixels.foreach_set(S.ravel()); o.filepath_raw = out; o.file_format = 'PNG'; o.save()
