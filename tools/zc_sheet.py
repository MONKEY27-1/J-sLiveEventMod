# usage: blender -b -P zc_sheet.py -- ref_dir out_dir first last  (contact sheets of 3x3 frames from f%07.2f.jpg, 10 s apart unless files are missing)
import bpy, sys, os, glob
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]
ref, out = a[0], a[1]
files = sorted(glob.glob(os.path.join(ref, 'f*.jpg')))
W, H = 480, 270; C, R = 3, 3
for si in range(0, len(files), C * R):
    chunk = files[si:si + C * R]
    sheet = np.zeros((H * R, W * C, 4), np.float32); sheet[..., 3] = 1
    for k, f in enumerate(chunk):
        im = bpy.data.images.load(f); w, h = im.size
        px = np.array(im.pixels[:], np.float32).reshape(h, w, 4)
        ys = (np.arange(H) * h // H); xs = (np.arange(W) * w // W)
        small = px[ys][:, xs]
        r, c = divmod(k, C); y0 = (R - 1 - r) * H
        sheet[y0:y0 + H, c * W:(c + 1) * W] = small
        bpy.data.images.remove(im)
    img = bpy.data.images.new('s', W * C, H * R); img.pixels = sheet.ravel().tolist(); img.filepath_raw = os.path.join(out, 'sheet%02d.png' % (si // (C * R))); img.file_format = 'PNG'; img.save()
    print('sheet', si // (C * R), [os.path.basename(x) for x in chunk])
