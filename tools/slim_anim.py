"""python slim_anim.py in.glb out.glb [clip]   the baked dance clips key every bone on every frame; this drops each channel that never leaves the node's rest transform (the loader leaves such a node as it
was built) - bone scale and the small translation noise of the baking included, only the hips keep their translation - repairs frames the exporter wrote as NaN, optionally keeps only one clip, then runs slim_glb
(unreferenced accessors / textures go)."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
NC = {'scalar': 1, 'vec3': 3, 'vec4': 4}
def acc(g, b, i):
    a = g['accessors'][i]; v = g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); n = a['count'] * NC[a['type'].lower()]
    return np.frombuffer(b, np.float32, n, off).reshape(a['count'], -1)
src, dst = sys.argv[1], sys.argv[2]; g, b = slim_glb.read_glb(src); b = bytearray(b); kept = dropped = fixed = 0
if len(sys.argv) > 3: g['animations'] = [a for a in g['animations'] if a.get('name') == sys.argv[3]]            # optional: keep only this clip
REST = {'translation': [0, 0, 0], 'rotation': [0, 0, 0, 1], 'scale': [1, 1, 1]}
for an in g['animations']:
    out = []; tr = []
    for ch in an['channels']:
        node = g['nodes'][ch['target']['node']]; path = ch['target']['path']; rest = np.array(node.get(path, REST[path]), np.float32); v = acc(g, b, an['samplers'][ch['sampler']]['output'])
        bad = ~np.isfinite(v).all(1)
        if bad.any(): v[bad] = rest; fixed += int(bad.sum())          # a frame the exporter could not decompose gets the node's rest value (one frame, nobody sees it); v is a writable view of b
        dev = np.abs(v - rest).max()
        if path == 'rotation' and dev > 1e-3 and np.abs(v + rest).max() < 1e-3: dev = 0.0               # the same rotation with the opposite sign
        tr.append((ch, path, node['name'], dev))
    top = max([d for c, p, n, d in tr if p == 'translation'] + [1e-9])                      # the hips' bounce is by far the largest translation
    for ch, path, name, dev in tr:
        lim = 2e-3 if path == 'rotation' else 0.03 if path == 'scale' else top * 0.02
        if dev < lim: dropped += 1
        else: out.append(ch); kept += 1
    used = sorted(set(c['sampler'] for c in out)); m = {s: k for k, s in enumerate(used)}
    an['samplers'] = [an['samplers'][s] for s in used]
    for c in out: c['sampler'] = m[c['sampler']]
    an['channels'] = out
g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb)
print('%s: %d channels kept, %d dropped, %d bad frames repaired, %d MB -> %d MB' % (dst, kept, dropped, fixed, len(b) // 1048576, len(nb) // 1048576))
