"""python slim_glb.py in.glb [out.glb]   drops what the plugin's loader never reads: every texture but each material's base-colour map (normal / occlusion / metal-rough / emissive maps) and
the buffer data that becomes unreferenced.  The meshes, skin and animation are untouched."""
import json, struct, sys

def read_glb(path):
    d = open(path, 'rb').read(); l, t = struct.unpack_from('<II', d, 12); js = json.loads(d[20:20 + l]); o = 20 + l
    bl, bt = struct.unpack_from('<II', d, o); return js, d[o + 8:o + 8 + bl]

def slim(g, b):
    used_tex = []
    for m in g.get('materials', []):
        for k in ('normalTexture', 'occlusionTexture', 'emissiveTexture'): m.pop(k, None)
        ex = m.get('extensions')
        if ex:                                                    # extensions that point at textures (specular maps) would dangle
            for k in [k for k in ex if k != 'KHR_materials_emissive_strength']: ex.pop(k)
            if not ex: m.pop('extensions')
        pbr = m.get('pbrMetallicRoughness', {}); pbr.pop('metallicRoughnessTexture', None)
        bt = pbr.get('baseColorTexture')
        if bt is not None: used_tex.append(bt['index'])
    used_tex = sorted(set(used_tex)); tmap = {t: i for i, t in enumerate(used_tex)}
    textures = [g['textures'][t] for t in used_tex]; used_img = sorted(set(t['source'] for t in textures)); imap = {i: k for k, i in enumerate(used_img)}
    for t in textures: t['source'] = imap[t['source']]
    images = [g['images'][i] for i in used_img]
    for m in g.get('materials', []):
        bt = m.get('pbrMetallicRoughness', {}).get('baseColorTexture')
        if bt is not None: bt['index'] = tmap[bt['index']]
    g['textures'] = textures; g['images'] = images
    keep = {'KHR_materials_emissive_strength'}
    for key in ('extensionsUsed', 'extensionsRequired'):
        if key in g:
            g[key] = [e for e in g[key] if e in keep]
            if not g[key]: g.pop(key)
    # keep only referenced accessors / buffer views
    used = []
    for m in g['meshes']:
        for pr in m['primitives']:
            used += list(pr['attributes'].values()) + ([pr['indices']] if 'indices' in pr else [])
    for sk in g.get('skins', []):
        if 'inverseBindMatrices' in sk: used.append(sk['inverseBindMatrices'])
    for an in g.get('animations', []):
        for s in an['samplers']: used += [s['input'], s['output']]
    used = sorted(set(used)); amap = {a: k for k, a in enumerate(used)}
    nb = bytearray(); views = []; vmap = {}
    def bv_new(bi):
        if bi in vmap: return vmap[bi]
        v = g['bufferViews'][bi]; off = v.get('byteOffset', 0); data = b[off:off + v['byteLength']]
        while len(nb) % 4: nb.append(0)
        nv = {k: x for k, x in v.items() if k != 'byteOffset'}; nv['buffer'] = 0; nv['byteOffset'] = len(nb); nb.extend(data); views.append(nv); vmap[bi] = len(views) - 1; return vmap[bi]
    accs = []
    for a in used:
        ac = dict(g['accessors'][a]); ac['bufferView'] = bv_new(ac['bufferView']); accs.append(ac)
    for im in g['images']:
        if 'bufferView' in im: im['bufferView'] = bv_new(im['bufferView'])
    for m in g['meshes']:
        for pr in m['primitives']:
            pr['attributes'] = {k: amap[v] for k, v in pr['attributes'].items()}
            if 'indices' in pr: pr['indices'] = amap[pr['indices']]
    for sk in g.get('skins', []):
        if 'inverseBindMatrices' in sk: sk['inverseBindMatrices'] = amap[sk['inverseBindMatrices']]
    for an in g.get('animations', []):
        for s in an['samplers']: s['input'] = amap[s['input']]; s['output'] = amap[s['output']]
    g['accessors'] = accs; g['bufferViews'] = views; g['buffers'][0]['byteLength'] = len(nb)
    return g, bytes(nb)

def write_glb(path, g, b):
    js = json.dumps(g, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    b = bytes(b) + b'\0' * (-len(b) % 4)
    open(path, 'wb').write(struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(b)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(b), 0x004E4942) + b)

if __name__ == '__main__':
    src = sys.argv[1]; dst = sys.argv[2] if len(sys.argv) > 2 else src
    g, b = read_glb(src); n0 = len(b); g, b = slim(g, b); write_glb(dst, g, b)
    print('%s: %d MB -> %d MB (%d textures kept)' % (dst, n0 // 1048576, len(b) // 1048576, len(g['textures'])))
