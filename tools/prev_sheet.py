"""blender -b -P prev_sheet.py -- out.png cols img1.png img2.png ...  -> contact sheet (each tile halved to 500 wide); no PIL needed"""
import bpy, sys, numpy as np
import os
a = sys.argv[sys.argv.index('--') + 1:]; out, cols = os.path.abspath(a[0]), int(a[1]); tiles = []
for p in a[2:]:
    im = bpy.data.images.load(os.path.abspath(p)); w, h = im.size; px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    px = px[:h // 2 * 2, :w // 2 * 2].reshape(h // 2, 2, w // 2, 2, 4).mean(axis=(1, 3)); tiles.append(px)
th, tw = max(t.shape[0] for t in tiles), max(t.shape[1] for t in tiles); rows = (len(tiles) + cols - 1) // cols
S = np.zeros((th * rows, tw * cols, 4), np.float32); S[..., 3] = 1
for k, t in enumerate(tiles): r, c = k // cols, k % cols; S[(rows - 1 - r) * th:(rows - 1 - r) * th + t.shape[0], c * tw:c * tw + t.shape[1]] = t   # blender images are bottom-up
img = bpy.data.images.new('sheet', S.shape[1], S.shape[0]); img.pixels = S.ravel().tolist(); img.filepath_raw = out; img.file_format = 'PNG'; img.save(); print('SHEET', out, S.shape)
