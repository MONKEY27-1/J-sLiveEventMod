"""python slone_paint.py in.glb out.glb   Doctor Slone's battlesuit glb has no textures: this paints it from the reference image (navy suit, brown skin, dark afro, yellow goggles, maroon knee / elbow / shoulder pads, silver chest plate, tan belt and holsters,
dark boots) by splitting the body mesh into regions (by each vertex's dominant bone + position, in the bind pose) and giving every region a tiny solid-colour texture, so the plugin's loader shows the colours."""
import sys, os, struct, zlib, json, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
src, dst = sys.argv[1:3]; g, b = slim_glb.read_glb(src); b = bytearray(b); N = g['nodes']
CT = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}; NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
def acc(i):
    a = g['accessors'][i]; v = g['bufferViews'][a['bufferView']]; off = v.get('byteOffset', 0) + a.get('byteOffset', 0); dt = CT[a['componentType']]; n = NC[a['type']]; st = v.get('byteStride')
    if st and st != np.dtype(dt).itemsize * n:
        raw = np.frombuffer(b, np.uint8, a['count'] * st, off).reshape(a['count'], st)[:, :np.dtype(dt).itemsize * n]; return np.ascontiguousarray(raw).view(dt).reshape(a['count'], n).astype(np.float64)
    return np.frombuffer(b, dt, a['count'] * n, off).reshape(a['count'], n).astype(np.float64)
def png(rgb):
    w = h = 8; raw = b''.join(b'\x00' + bytes(rgb) * w for _ in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b'')
def add_view(data):
    off = len(b)
    while off % 4: b.append(0); off += 1
    b.extend(data); g['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}); return len(g['bufferViews']) - 1
g.setdefault('images', []); g.setdefault('textures', []); COL = {}
def material_for(name, rgb):
    key = tuple(rgb)
    if key in COL: return COL[key]
    img = len(g['images']); g['images'].append({'bufferView': add_view(png([int(c * 255) for c in rgb])), 'mimeType': 'image/png'}); tex = len(g['textures']); g['textures'].append({'source': img})
    g.setdefault('samplers', []); g['materials'].append({'name': name, 'pbrMetallicRoughness': {'baseColorTexture': {'index': tex}, 'metallicFactor': 0.0, 'roughnessFactor': 0.8}, 'doubleSided': True}); COL[key] = len(g['materials']) - 1; return COL[key]
NAVY, MAROON, SILVER, TAN, BOOT, SKIN, HAIR, GOGGLE, DARK = (0.10, 0.13, 0.20), (0.36, 0.17, 0.15), (0.70, 0.72, 0.78), (0.45, 0.31, 0.19), (0.07, 0.07, 0.09), (0.38, 0.24, 0.16), (0.07, 0.06, 0.06), (1.0, 0.85, 0.18), (0.10, 0.07, 0.06)
skin = g['skins'][0]; J = skin['joints']; ibm = acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1); jnames = [N[j]['name'].lower() for j in J]
jpos = np.array([np.linalg.inv(m)[:3, 3] for m in ibm])                                                       # bind-pose joint positions in mesh space
def jfind(prefix):
    for i, n in enumerate(jnames):
        if n.startswith(prefix): return jpos[i]
    return None
body_mat = next(i for i, m in enumerate(g['materials']) if 'armor' in m['name'].lower() or 'body' in m['name'].lower())
for n in N:
    if 'mesh' not in n: continue
    for p in g['meshes'][n['mesh']]['primitives']:
        mname = g['materials'][p['material']]['name'].lower(); nm = None
        if 'head' in mname: p['material'] = material_for('Slone_Skin', SKIN)
        elif 'hair' in mname: p['material'] = material_for('Slone_Hair', HAIR)
        elif 'glasses' in mname: p['material'] = material_for('Slone_Goggles', GOGGLE)
        elif 'faceacc' in mname: p['material'] = material_for('Slone_Face', DARK)
        elif p['material'] == body_mat:
            P = acc(p['attributes']['POSITION']); JI = acc(p['attributes']['JOINTS_0']).astype(int); WT = acc(p['attributes']['WEIGHTS_0']); dom = JI[np.arange(len(P)), WT.argmax(1)]; dn = [jnames[k] for k in dom]
            elL = jfind('lowerarm_l_'); elR = jfind('lowerarm_r_'); shL = jfind('upperarm_l_'); shR = jfind('upperarm_r_'); knL = jfind('calf_l_'); knR = jfind('calf_r_')
            def near(pt, v, r): return pt is not None and np.linalg.norm(v - pt) < r
            reg = np.zeros(len(P), int); cols = [NAVY, MAROON, SILVER, TAN, BOOT, SKIN]; NV, MR, SV, TN, BT, SK = range(6)
            for i in range(len(P)):
                x, y, z = P[i]; nme = dn[i]; r = NV
                if 'hand' in nme or 'finger' in nme or any(s in nme for s in ('index', 'middle', 'pinky', 'ring', 'thumb')): r = NV
                elif 'foot' in nme or 'ball' in nme or 'toe' in nme or y < 0.09: r = BT
                elif 'neck' in nme: r = SK
                elif 'lowerarm' in nme or 'arm' in nme and 'upper' not in nme: r = MR if (near(elL, P[i], 0.075) or near(elR, P[i], 0.075)) else NV
                elif 'upperarm' in nme or 'clavicle' in nme or 'shldr' in nme or 'shoulder' in nme: r = MR if (near(shL, P[i], 0.085) or near(shR, P[i], 0.085) or (y > 1.28 and abs(x) > 0.16)) else NV
                elif 'calf' in nme: r = MR if ((near(knL, P[i], 0.085) or near(knR, P[i], 0.085)) and z > -0.02) else NV
                elif 'thigh' in nme: r = TN if (0.50 < y < 0.72 and abs(x) > 0.11) else NV
                elif 'pelvis' in nme: r = TN if 0.86 < y < 0.96 else NV
                elif 'spine' in nme: r = SV if (1.14 < y < 1.32 and abs(x) < 0.095 and z > 0.0) else (TN if 0.86 < y < 0.96 else NV)
                reg[i] = r
            idx = acc(p['indices']).astype(np.int64).reshape(-1, 3); tri_reg = np.array([np.bincount(reg[t], minlength=6).argmax() for t in idx]); first = True; prims = []
            for k in range(6):
                sel = idx[tri_reg == k]
                if len(sel) == 0: continue
                data = sel.astype(np.uint32).flatten().tobytes(); vi = add_view(data); g['accessors'].append({'bufferView': vi, 'componentType': 5125, 'count': int(sel.size), 'type': 'SCALAR'}); ai = len(g['accessors']) - 1
                q = dict(p); q['indices'] = ai; q['material'] = material_for('Slone_%d' % k, cols[k]); prims.append(q)
            m_ = g['meshes'][n['mesh']]; pi = m_['primitives'].index(p); m_['primitives'][pi:pi + 1] = prims
            print('body regions (tris):', {['navy', 'maroon', 'silver', 'tan', 'boot', 'skin'][k]: int((tri_reg == k).sum()) for k in range(6)}); break
g['buffers'][0]['byteLength'] = len(b); g, nb = slim_glb.slim(g, bytes(b)); slim_glb.write_glb(dst, g, nb); print(dst, os.path.getsize(dst) // 1024, 'KB')
