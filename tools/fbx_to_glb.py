"""Extract one tree (trunk + leaves) from the ASCII FBX tree pack and write a GLB (metres, Y-up, base at origin, textures embedded).
Usage: python -I fbx_to_glb.py <GorillaTrees.fbx> <Textures dir> <out.glb> [trunkModel] [leavesModel]"""
import sys, json, struct, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fbx_ascii import parse, find

fbx, texdir, out = sys.argv[1:4]
trunk_name = sys.argv[4] if len(sys.argv) > 4 else 'SmallTreeWood__2_'
leaves_name = sys.argv[5] if len(sys.argv) > 5 else 'smallleaves'
tree = parse(open(fbx, encoding='utf-8', errors='replace').read())
objs = [c for t in find(tree, 'Objects') for c in t[2]]
conns = [c[1] for t in find(tree, 'Connections') for c in t[2]]
byid = {o[1][0]: o for o in objs if o[1]}

def model_parts(name):
    mid = [o[1][0] for o in objs if o[0] == 'Model' and o[1][1] == 'Model::' + name][0]
    geo = mat = None
    for c in conns:
        if c[0] == 'OO' and c[2] == mid:
            o = byid.get(c[1])
            if o and o[0] == 'Geometry': geo = o
            if o and o[0] == 'Material': mat = o
    return geo, mat

def geom_data(geo):
    ch = {c[0]: c for c in geo[2]}
    verts = ch['Vertices'][1][0]
    idx = [int(v) for v in ch['PolygonVertexIndex'][1][0]]
    ln = [c for c in geo[2] if c[0] == 'LayerElementNormal'][0]
    lu = [c for c in geo[2] if c[0] == 'LayerElementUV'][0]
    ln = {c[0]: c for c in ln[2]}; lu = {c[0]: c for c in lu[2]}
    normals = ln['Normals'][1][0]; uvs = lu['UV'][1][0]
    nidx = [int(v) for v in ln['NormalsIndex'][1][0]] if 'NormalsIndex' in ln else None
    uidx = [int(v) for v in lu['UVIndex'][1][0]] if 'UVIndex' in lu else None
    return verts, idx, normals, nidx, uvs, uidx

def rot(x, y, z):  # Rx(-90deg), then cm -> m is applied to positions by caller
    return (x, z, -y)

def build(geo):
    verts, idx, normals, nidx, uvs, uidx = geom_data(geo)
    pos, nrm, uv, tri, cache = [], [], [], [], {}
    poly, p = [], 0
    for raw in idx:
        end = raw < 0
        vi = ~raw if end else raw
        key = (vi, nidx[p] if nidx else p, uidx[p] if uidx else p)
        if key not in cache:
            cache[key] = len(pos)
            pos.append(tuple(c * 0.01 for c in rot(*verts[vi * 3:vi * 3 + 3])))
            ni = key[1]; nrm.append(rot(*normals[ni * 3:ni * 3 + 3]))
            ui = key[2]; uv.append((uvs[ui * 2], 1.0 - uvs[ui * 2 + 1]))  # glTF v is flipped; the Unity loader flips it back
        poly.append(cache[key]); p += 1
        if end:
            for k in range(1, len(poly) - 1): tri += [poly[0], poly[k], poly[k + 1]]
            poly = []
    return pos, nrm, uv, tri

tg, _ = model_parts(trunk_name); lg, _ = model_parts(leaves_name)
meshes = [build(tg), build(lg)]
# recentre: trunk base centre -> origin
allp = [q for m in meshes for q in m[0]]
miny = min(q[1] for q in allp)
tp = meshes[0][0]
cx = (min(q[0] for q in tp) + max(q[0] for q in tp)) / 2; cz = (min(q[2] for q in tp) + max(q[2] for q in tp)) / 2
meshes = [([(q[0] - cx, q[1] - miny, q[2] - cz) for q in m[0]], m[1], m[2], m[3]) for m in meshes]

buf = bytearray(); views = []; accs = []
def add(data, target=None):
    while len(buf) % 4: buf.append(0)
    views.append({'buffer': 0, 'byteOffset': len(buf), 'byteLength': len(data), **({'target': target} if target else {})}); buf.extend(data)
    return len(views) - 1
gl_meshes = []
for pos, nrm, uv, tri in meshes:
    a = {}
    flat = [c for q in pos for c in q]
    accs.append({'bufferView': add(struct.pack('<%df' % len(flat), *flat), 34962), 'componentType': 5126, 'count': len(pos), 'type': 'VEC3', 'min': [min(q[i] for q in pos) for i in range(3)], 'max': [max(q[i] for q in pos) for i in range(3)]}); a['POSITION'] = len(accs) - 1
    flat = [c for q in nrm for c in q]
    accs.append({'bufferView': add(struct.pack('<%df' % len(flat), *flat), 34962), 'componentType': 5126, 'count': len(nrm), 'type': 'VEC3'}); a['NORMAL'] = len(accs) - 1
    flat = [c for q in uv for c in q]
    accs.append({'bufferView': add(struct.pack('<%df' % len(flat), *flat), 34962), 'componentType': 5126, 'count': len(uv), 'type': 'VEC2'}); a['TEXCOORD_0'] = len(accs) - 1
    accs.append({'bufferView': add(struct.pack('<%dI' % len(tri), *tri), 34963), 'componentType': 5125, 'count': len(tri), 'type': 'SCALAR'})
    gl_meshes.append({'primitives': [{'attributes': a, 'indices': len(accs) - 1, 'material': len(gl_meshes), 'mode': 4}]})
images = []
for f in ('bark.png', 'forestatlas.png'):
    images.append({'bufferView': add(open(os.path.join(texdir, f), 'rb').read()), 'mimeType': 'image/png'})
doc = {'asset': {'version': '2.0'}, 'scene': 0, 'scenes': [{'nodes': [0, 1]}],
       'nodes': [{'name': 'trunk', 'mesh': 0}, {'name': 'leaves', 'mesh': 1}], 'meshes': gl_meshes,
       'materials': [{'name': 'bark', 'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}}}, {'name': 'leaves', 'pbrMetallicRoughness': {'baseColorTexture': {'index': 1}}}],
       'textures': [{'source': 0}, {'source': 1}], 'images': images, 'bufferViews': views, 'accessors': accs, 'buffers': [{'byteLength': len(buf)}]}
js = json.dumps(doc, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
while len(buf) % 4: buf.append(0)
glb = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(buf)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(buf), 0x004E4942) + bytes(buf)
open(out, 'wb').write(glb)
h = max(q[1] for m in meshes for q in m[0])
w = max(max(abs(q[0]) for q in m[0]) for m in meshes)
print('wrote', out, len(glb), 'bytes; verts', [len(m[0]) for m in meshes], 'height %.2f m, half-width %.2f m' % (h, w))
