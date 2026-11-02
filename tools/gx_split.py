"""blender -b -P gx_split.py -- in.glb out.glb height_m
Splits the (static) Galactus mesh into rigid parts the plugin can pose: Gx > Torso (body + legs), Head, ShL > UpL > ElL > LoL (arm pieces), ShR ... . The mesh is cut along its connected islands (the armour pieces are separate islands),
each island goes to the part its centre is in; pivots (neck, shoulders, elbows) are estimated from this model's pose (right arm raised, fist forward; left arm hanging). Scaled to height_m, origin at the belt, faces +z in the plugin."""
import bpy, bmesh, sys, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out, HM = a[0], a[1], float(a[2])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
ms = [o for o in sc.objects if o.type == 'MESH']
for o in sc.objects: o.select_set(False)
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(sc.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join(); ob = bpy.context.active_object
me = ob.data; V = np.array([v.co[:] for v in me.vertices]); lo = V.min(0); H = V[:, 2].max() - lo[2]; cx = (V[:, 0].min() + V[:, 0].max()) / 2
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
def N(p): return np.array([(p[0] - cx) / H, p[1] / H, (p[2] - lo[2]) / H])          # normalised: x across (+x = the character's left), y depth (front = -y), z up (height 1)
SR, ER, HR = np.array([-0.20, 0.00, 0.735]), np.array([-0.20, 0.01, 0.585]), np.array([-0.185, -0.17, 0.62])      # right arm (raised, fist forward)
SL, EL, HL = np.array([0.19, 0.00, 0.735]), np.array([0.20, 0.06, 0.55]), np.array([0.185, 0.105, 0.465])         # left arm (hanging)
def segd(p, a_, b_):
    d = b_ - a_; t = np.clip(((p - a_) @ d) / (d @ d), 0, 1); return float(np.linalg.norm(p - (a_ + t * d)))
part_of = {}; head_idx = []
for ci, comp in enumerate(isl):
    P = V[comp]; c = N(P.mean(0)); mn = N(P.min(0)); mx = N(P.max(0))
    if c[2] > 0.74 and abs(c[0]) < 0.13: part = 'Head'; head_idx += comp
    elif (abs(c[0]) >= 0.15 or (abs(c[0]) >= 0.088 and c[2] >= 0.56)) and 0.38 < c[2] < 0.82:
        side = 'L' if c[0] > 0 else 'R'; S_, E_, H_ = (SL, EL, HL) if side == 'L' else (SR, ER, HR)
        part = ('Up' if segd(c, S_, E_) <= segd(c, E_, H_) else 'Lo') + side
    else: part = 'Torso'
    for i in comp: part_of[i] = part
from collections import Counter
print('SPLIT', dict(Counter(part_of[c[0]] for c in isl)), 'islands', len(isl))
# ---- scale / origin: metres, origin at the belt, y of the body's middle
k = HM / H; ymid = float(np.median(V[:, 1])); R0 = np.array([cx, ymid, lo[2] + 0.46 * H])
def W(p_norm): return np.array([cx + p_norm[0] * H, p_norm[1] * H, lo[2] + p_norm[2] * H])
hv = V[head_idx]; neck = np.array([cx, float(hv[:, 1].mean()), float(hv[:, 2].min()) + 0.01 * H])
piv = {'Torso': R0, 'Head': neck, 'ShL': W(SL), 'ElL': W(EL), 'ShR': W(SR), 'ElR': W(ER)}
tree = {'Torso': 'Gx', 'Head': 'Torso', 'ShL': 'Torso', 'ElL': 'ShL', 'ShR': 'Torso', 'ElR': 'ShR'}
mesh_of = {'Torso': 'Torso', 'Head': 'Head', 'UpL': 'ShL', 'LoL': 'ElL', 'UpR': 'ShR', 'LoR': 'ElR'}      # which joint node carries which mesh part
objs = {}
root = bpy.data.objects.new('Gx', None); sc.collection.objects.link(root)
def make(node, parts):
    """a joint node (an Empty at the pivot) that carries the meshes of `parts`, built from the islands assigned to them"""
    pv = piv[node]; keep = [i for i in range(len(V)) if part_of[i] in parts]
    b2 = bmesh.new(); b2.from_mesh(me); b2.verts.ensure_lookup_table(); ks = set(keep)
    bmesh.ops.delete(b2, geom=[v for v in b2.verts if v.index not in ks], context='VERTS'); m2 = bpy.data.meshes.new(node + 'M'); b2.to_mesh(m2); b2.free()
    for v in m2.vertices: v.co = mathutils.Vector((np.array(v.co[:]) - pv) * k)
    for mat in me.materials: m2.materials.append(mat)
    o = bpy.data.objects.new(node, m2); sc.collection.objects.link(o)
    o.parent = root if tree[node] == 'Gx' else objs[tree[node]]; o.location = mathutils.Vector((pv - (R0 if tree[node] == 'Gx' else piv[tree[node]])) * k); objs[node] = o
make('Torso', ['Torso']); make('Head', ['Head']); make('ShL', ['UpL']); make('ElL', ['LoL']); make('ShR', ['UpR']); make('ElR', ['LoR'])
bpy.data.objects.remove(ob)
# the unit scale: Blender -y is the front (glTF +z); the plugin's yaw 180 turns him towards you
print('SPLIT done, height', HM, 'neck', tuple(round(float(x), 2) for x in (neck - R0) * k), 'shoulder L', tuple(round(float(x), 2) for x in (piv['ShL'] - R0) * k))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
