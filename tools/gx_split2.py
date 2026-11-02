"""blender -b -P gx_split2.py -- in.glb out.glb height_m      (the NEW Galactus: galactus_-_fortnite.glb, 6 static meshes by material)
Splits it into rigid parts the plugin poses: Gx > Torso (vest + belt + legs), Head (helmet + face), ShL > ElL (upper arm / forearm + fist), ShR > ElR, plus marker empties
(EyeL EyeR MouthP under Head, ChestP under Torso, HandL under ElL, HandR under ElR) so the code does not need guessed constants.
material_0 = both arms (12 islands: sleeves, elbow guards, fists); the upper arm = the islands that reach above z 125, everything else of that arm hangs on the elbow.
Scaled to height_m, origin at the belt, faces -y in Blender (+z in glTF; the plugin's yaw 180 turns him towards you).  Left = +x."""
import bpy, bmesh, sys, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out, HM = a[0], a[1], float(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
ms = sorted([o for o in sc.objects if o.type == 'MESH'], key=lambda o: o.name)
for o in sc.objects: o.select_set(False)
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(sc.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
ARMS = next(i for i, o in enumerate(ms) if 'material_0' in o.name)
HEADM = [i for i, o in enumerate(ms) if 'material_1' in o.name or 'material_5' in o.name]
BELTM = next(i for i, o in enumerate(ms) if 'material_2' in o.name); FACEM = next(i for i, o in enumerate(ms) if 'material_5' in o.name)
for i, o in enumerate(ms):
    at = o.data.attributes.new('src', 'INT', 'POINT')
    for k in range(len(o.data.vertices)): at.data[k].value = i
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join(); ob = bpy.context.active_object
me = ob.data; V = np.array([v.co[:] for v in me.vertices]); SRC = np.array([d.value for d in me.attributes['src'].data])
lo = V.min(0); H = V[:, 2].max() - lo[2]; k = HM / H
cx = (V[SRC == BELTM, 0].min() + V[SRC == BELTM, 0].max()) / 2; cxf = (V[SRC == FACEM, 0].min() + V[SRC == FACEM, 0].max()) / 2   # body centre / face centre in x
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); seen = set(); isl = []
for v0 in bm.verts:
    if v0.index in seen: continue
    st = [v0]; seen.add(v0.index); comp = []
    while st:
        v = st.pop(); comp.append(v.index)
        for e in v.link_edges:
            w = e.other_vert(v)
            if w.index not in seen: seen.add(w.index); st.append(w)
    isl.append(comp)
part_of = np.array(['Torso'] * len(V), dtype=object)
for i in HEADM: part_of[SRC == i] = 'Head'
arm_isl = {'L': [], 'R': []}
for comp in isl:
    if SRC[comp[0]] != ARMS: continue
    P = V[comp]; side = 'L' if P[:, 0].mean() > cx else 'R'; arm_isl[side].append(comp)
    pt = ('Up' if P[:, 2].max() > lo[2] + 0.70 * H else 'Lo') + side
    part_of[comp] = pt
from collections import Counter
print('SPLIT', dict(Counter(part_of)), 'islands', len(isl))
# ---- joints: shoulder = the top of the upper arm sunk into the pauldron, elbow = its bottom end, hand = the two islands farthest from the elbow
piv = {}; hand = {}
for side in 'LR':
    up = [c for c in arm_isl[side] if V[c][:, 2].max() > lo[2] + 0.70 * H]; lw = [c for c in arm_isl[side] if c not in up]
    U = V[[i for c in up for i in c]]; zmax, zmin = U[:, 2].max(), U[:, 2].min()
    S = U[U[:, 2] > zmax - 10].mean(0); S[2] = zmax - 11.0; E = U[U[:, 2] < zmin + 4].mean(0)
    big = sorted([c for c in lw if len(c) > 1000], key=lambda c: -np.linalg.norm(V[c].mean(0) - E))[:2]
    Hd = V[[i for c in big for i in c]].mean(0)
    piv['Sh' + side] = S; piv['El' + side] = E; hand[side] = Hd
    print('JOINT', side, 'shoulder', S.round(1), 'elbow', E.round(1), 'hand', Hd.round(1), 'upper', np.linalg.norm(E - S).round(1), 'fore', np.linalg.norm(Hd - E).round(1))
ymid = float(np.median(V[:, 1])); R0 = np.array([cx, ymid, lo[2] + 0.55 * H]); piv['Torso'] = R0
HV = V[part_of == 'Head']; neck = np.array([cx, float(HV[:, 1].mean()), float(HV[:, 2].min()) + 2.0]); piv['Head'] = neck
# markers (front of the face / vest found from the mesh)
def front(zc, xc=0.0, dx=6.0, dz=3.0, mask=None):
    m = (np.abs(V[:, 0] - cxf - xc) < dx) & (np.abs(V[:, 2] - zc) < dz)
    if mask is not None: m &= mask
    return float(V[m, 1].min()) if m.any() else ymid - 15.0
zE, zM, zC = lo[2] + 0.843 * H, lo[2] + 0.80 * H, lo[2] + 0.685 * H
yE = front(zE, mask=(part_of == 'Head')); yM = front(zM, mask=(part_of == 'Head')); yC = front(zC, mask=(part_of == 'Torso'))
print('MARK eyes z', round(zE, 1), 'y', round(yE, 1), 'mouth z', round(zM, 1), 'y', round(yM, 1), 'chest z', round(zC, 1), 'y', round(yC, 1), 'belt', R0.round(1), 'neck', neck.round(1))
marks = {'EyeL': (np.array([cxf + 3.6, yE + 1.5, zE]), 'Head'), 'EyeR': (np.array([cxf - 3.6, yE + 1.5, zE]), 'Head'), 'MouthP': (np.array([cxf, yM + 1.5, zM]), 'Head'), 'ChestP': (np.array([cx, yC + 2.0, zC]), 'Torso'),
         'HandL': (hand['L'], 'ElL'), 'HandR': (hand['R'], 'ElR')}
tree = {'Torso': 'Gx', 'Head': 'Torso', 'ShL': 'Torso', 'ElL': 'ShL', 'ShR': 'Torso', 'ElR': 'ShR'}
mesh_of = {'Torso': ['Torso'], 'Head': ['Head'], 'ShL': ['UpL'], 'ElL': ['LoL'], 'ShR': ['UpR'], 'ElR': ['LoR']}
objs = {}
root = bpy.data.objects.new('Gx', None); sc.collection.objects.link(root)
def make(node):
    pv = piv[node]; keep = {i for i in range(len(V)) if part_of[i] in mesh_of[node]}
    b2 = bmesh.new(); b2.from_mesh(me); b2.verts.ensure_lookup_table()
    bmesh.ops.delete(b2, geom=[v for v in b2.verts if v.index not in keep], context='VERTS'); m2 = bpy.data.meshes.new(node + 'M'); b2.to_mesh(m2); b2.free()
    for v in m2.vertices: v.co = mathutils.Vector((np.array(v.co[:]) - pv) * k)
    for mat in me.materials: m2.materials.append(mat)
    o = bpy.data.objects.new(node, m2); sc.collection.objects.link(o)
    o.parent = root if tree[node] == 'Gx' else objs[tree[node]]; o.location = mathutils.Vector((pv - (R0 if tree[node] == 'Gx' else piv[tree[node]])) * k); objs[node] = o
for n in ('Torso', 'Head', 'ShL', 'ElL', 'ShR', 'ElR'): make(n)
for nm, (p, par) in marks.items():
    e = bpy.data.objects.new(nm, None); sc.collection.objects.link(e); e.parent = objs[par]; e.location = mathutils.Vector((p - piv[par]) * k)
bpy.data.objects.remove(ob)
# the plugin only reads the base colour: paint the emissive map (the red neon lines) into it, then drop the emission / normal links so the glb stays small
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    nt = mat.node_tree; bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None: continue
    def img_of(sock):
        if not sock.is_linked: return None
        n = sock.links[0].from_node
        return n.image if n.type == 'TEX_IMAGE' else None
    bi, ei = img_of(bsdf.inputs['Base Color']), img_of(bsdf.inputs['Emission Color'])
    if bi is not None and ei is not None:
        w, h = bi.size; e2 = ei
        if tuple(ei.size) != (w, h): e2 = ei.copy(); e2.scale(w, h)
        B = np.empty(w * h * 4, dtype=np.float32); E = np.empty(w * h * 4, dtype=np.float32); bi.pixels.foreach_get(B); e2.pixels.foreach_get(E)
        R = np.clip(B + E * 1.0, 0.0, 1.0); R[3::4] = B[3::4]; bi.pixels.foreach_set(R); bi.update(); print('BAKED', mat.name, w, h, 'emission mean', float(E.mean()))
    for nm in ('Emission Color', 'Normal'):
        for l in list(bsdf.inputs[nm].links): nt.links.remove(l)
    bsdf.inputs['Emission Strength'].default_value = 0.0
print('SPLIT done, height', HM, 'k', round(k, 4))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
