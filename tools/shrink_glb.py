"""blender -b -P shrink_glb.py -- in.glb out.glb [max_px]   re-encodes every image of a glb (via Blender's image tools) at most max_px wide (default 1024); opaque ones as JPEG q82, the rest as PNG; the meshes / skin / clips are untouched."""
import bpy, sys, os, struct, json, tempfile
a = sys.argv[sys.argv.index('--') + 1:]; src, dst = a[:2]; MAXP = int(a[2]) if len(a) > 2 else 1024
d = open(src, 'rb').read(); l, t = struct.unpack_from('<II', d, 12); g = json.loads(d[20:20 + l]); o = 20 + l; bl, bt = struct.unpack_from('<II', d, o); b = bytearray(d[o + 8:o + 8 + bl])
tmp = tempfile.mkdtemp(); newbuf = bytearray(); views = g['bufferViews']; remap = {}
imgviews = {im['bufferView'] for im in g.get('images', []) if 'bufferView' in im}
for vi, v in enumerate(views):
    if vi in imgviews: continue
    off = len(newbuf)
    while off % 4: newbuf.append(0); off += 1
    newbuf.extend(b[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]); remap[vi] = (off, v['byteLength'])
for im in g.get('images', []):
    v = views[im['bufferView']]; raw = bytes(b[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]); ext = 'png' if im.get('mimeType', 'image/png') == 'image/png' else 'jpg'; p = os.path.join(tmp, 'i%d.%s' % (im['bufferView'], ext)); open(p, 'wb').write(raw)
    img = bpy.data.images.load(p); w, h = img.size; k = min(1.0, MAXP / max(w, h, 1))
    if k < 1.0: img.scale(max(4, int(w * k)), max(4, int(h * k)))
    if os.environ.get('GAIN'):                       # GAIN=1.8 [LIFT=0.06]: brighten dark textures (the I.O. guard's armour is almost black)
        import numpy as np
        arr = np.empty(len(img.pixels), dtype=np.float32); img.pixels.foreach_get(arr); arr = arr.reshape(-1, 4); arr[:, :3] = np.clip(arr[:, :3] * float(os.environ['GAIN']) + float(os.environ.get('LIFT', '0')), 0, 1); img.pixels.foreach_set(arr.ravel()); img.update()
    has_alpha = False
    try:
        px = img.pixels[3::4]; has_alpha = min(px) < 0.99 if len(px) < 2000000 else True
    except Exception: has_alpha = True
    out = os.path.join(tmp, 'o%d.%s' % (im['bufferView'], 'png' if has_alpha else 'jpg')); img.file_format = 'PNG' if has_alpha else 'JPEG'; img.filepath_raw = out
    if not has_alpha: bpy.context.scene.render.image_settings.quality = 82
    img.save(); nb = open(out, 'rb').read(); im['mimeType'] = 'image/png' if has_alpha else 'image/jpeg'; bpy.data.images.remove(img)
    off = len(newbuf)
    while off % 4: newbuf.append(0); off += 1
    newbuf.extend(nb); remap[im['bufferView']] = (off, len(nb))
for vi, (off, ln) in remap.items(): views[vi]['byteOffset'] = off; views[vi]['byteLength'] = ln
g['buffers'][0]['byteLength'] = len(newbuf); j = json.dumps(g, separators=(',', ':')).encode(); j += b' ' * ((4 - len(j) % 4) % 4); nbuf = bytes(newbuf) + b'\0' * ((4 - len(newbuf) % 4) % 4)
open(dst, 'wb').write(struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(j) + 8 + len(nbuf)) + struct.pack('<II', len(j), 0x4E4F534A) + j + struct.pack('<II', len(nbuf), 0x004E4942) + nbuf)
print('shrunk', os.path.getsize(src) // 1024, '->', os.path.getsize(dst) // 1024, 'KB', len(g.get('images', [])), 'images')
