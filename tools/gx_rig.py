"""blender -b -P gx_rig.py -- in.glb out.glb out.json [height]
The NEW Galactus skeleton: the static galactus_-_fortnite.glb (6 meshes by material, no bones) gets a full armature -
Hips > Spine > Chest > Neck > Head (+ EyeL EyeR MouthP markers), Chest > Clav[LR] > UpperArm[LR] > ForeArm[LR] > Wrist[LR] (+ HandL/HandR marker), Hips > Thigh[LR] > Shin[LR] > Foot[LR] > Toe[LR], ChestP marker -
and smooth skin weights worked out from the anatomy (trunk chain by height, legs by side + height, arms along their own axes, pauldrons shared with the clavicle).
Output meshes (all skinned to the one armature): Body, Head, ArmL, ArmR (so the arm shell can be hidden while the bus is inside it).  Scaled to <height> units, origin at the belt, faces -y, his left = +x.
out.json = the rest skeleton (head/tail per bone, parent) for the animation script."""
import bpy, bmesh, sys, json, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out, js = a[0], a[1], a[2]; HM = float(a[3]) if len(a) > 3 else 60.0
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
    for kk in range(len(o.data.vertices)): at.data[kk].value = i
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join(); ob = bpy.context.active_object
me = ob.data; V = np.array([v.co[:] for v in me.vertices]); SRC = np.array([d.value for d in me.attributes['src'].data])
lo = V.min(0); H = V[:, 2].max() - lo[2]; k = HM / H
cx = (V[SRC == BELTM, 0].min() + V[SRC == BELTM, 0].max()) / 2; cxf = (V[SRC == FACEM, 0].min() + V[SRC == FACEM, 0].max()) / 2
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
bm.free()
n = len(V); part = np.array(['Body'] * n, dtype=object)
for i in HEADM: part[SRC == i] = 'Head'
armIsl = {'L': [], 'R': []}; sideOf = np.zeros(n, int); upper = np.zeros(n, bool)
for comp in isl:
    if SRC[comp[0]] != ARMS: continue
    P = V[comp]; sd = 'L' if P[:, 0].mean() > cx else 'R'; armIsl[sd].append(comp)
    isup = P[:, 2].max() > lo[2] + 0.70 * H
    part[comp] = 'Arm' + sd
    for i in comp: upper[i] = isup
# ---- joints (original units)
J = {}
for sd in 'LR':
    up = [c for c in armIsl[sd] if V[c][:, 2].max() > lo[2] + 0.70 * H]; lw = [c for c in armIsl[sd] if c not in up]
    U = V[[i for c in up for i in c]]; zmax, zmin = U[:, 2].max(), U[:, 2].min()
    S = U[U[:, 2] > zmax - 10].mean(0); S[2] = zmax - 11.0; E = U[U[:, 2] < zmin + 4].mean(0)
    big = sorted([c for c in lw if len(c) > 1000], key=lambda c: -np.linalg.norm(V[c].mean(0) - E))[:2]
    Hd = V[[i for c in big for i in c]].mean(0)
    J['S' + sd] = S; J['E' + sd] = E; J['H' + sd] = Hd
ymid = float(np.median(V[:, 1])); belt = np.array([cx, ymid, lo[2] + 0.55 * H])
HV = V[part == 'Head']; neckZ = float(HV[:, 2].min()) + 2.0; headTop = float(HV[:, 2].max())
chestZ = lo[2] + 0.685 * H
# leg anatomy from the body mesh (vertices below the belt, per side)
B = (part == 'Body') & (V[:, 2] < belt[2] - 0.03 * H)
def legc(sd, zlo, zhi):
    s = 1.0 if sd == 'L' else -1.0; m = B & ((V[:, 0] - cx) * s > 0.015 * H) & (V[:, 2] > zlo) & (V[:, 2] < zhi); P = V[m]
    return P.mean(0) if len(P) else np.array([cx + s * 0.06 * H, ymid, (zlo + zhi) / 2])
for sd in 'LR':
    s = 1.0 if sd == 'L' else -1.0
    th = legc(sd, lo[2] + 0.34 * H, lo[2] + 0.46 * H); kn = legc(sd, lo[2] + 0.22 * H, lo[2] + 0.30 * H); an = legc(sd, lo[2] + 0.04 * H, lo[2] + 0.09 * H)
    J['Hip' + sd] = np.array([cx + s * (abs(th[0] - cx) * 0.95), ymid, belt[2] - 0.045 * H]); J['Knee' + sd] = np.array([kn[0], kn[1], lo[2] + 0.265 * H]); J['Ankle' + sd] = np.array([an[0], an[1], lo[2] + 0.062 * H])
    toe = np.array([an[0], an[1] - 0.075 * H, lo[2] + 0.018 * H]); J['Toe' + sd] = toe
for kk, v in J.items(): print('J', kk, np.round(v, 1))
# ---- bones (original units -> scaled, origin at the belt)
def T(p): return (np.array(p) - belt) * k
bones = {}   # name -> (parent, head, tail)
def add(nm, par, h, t): bones[nm] = (par, T(h), T(t))
cen = np.array([cx, ymid, 0.0]); up_ = np.array([0, 0, 1.0])
zSp = belt[2] + 0.035 * H; zMid = (belt[2] + chestZ) / 2 + 0.02 * H; zNB = neckZ - 0.02 * H
add('Hips', None, belt, [cx, ymid, zSp]); add('Spine', 'Hips', [cx, ymid, zSp], [cx, ymid, zMid]); add('Chest', 'Spine', [cx, ymid, zMid], [cx, ymid, zNB]); add('Neck', 'Chest', [cx, ymid, zNB], [cx, ymid, neckZ + 0.012 * H])
hy = float(HV[:, 1].mean()); add('Head', 'Neck', [cx, ymid, neckZ + 0.012 * H], [cx, hy, headTop])
for sd in 'LR':
    S, E, Hd = J['S' + sd], J['E' + sd], J['H' + sd]; s = 1.0 if sd == 'L' else -1.0
    W = E + (Hd - E) * 0.74; tipH = Hd + (Hd - E) * 0.28
    cl = np.array([cx + s * 0.018 * H, ymid, chestZ + 0.05 * H]); add('Clav' + sd, 'Chest', cl, S); add('UpperArm' + sd, 'Clav' + sd, S, E); add('ForeArm' + sd, 'UpperArm' + sd, E, W); add('Wrist' + sd, 'ForeArm' + sd, W, tipH)
    add('Thigh' + sd, 'Hips', J['Hip' + sd], J['Knee' + sd]); add('Shin' + sd, 'Thigh' + sd, J['Knee' + sd], J['Ankle' + sd]); add('Foot' + sd, 'Shin' + sd, J['Ankle' + sd], J['Toe' + sd])
    add('Toe' + sd, 'Foot' + sd, J['Toe' + sd], J['Toe' + sd] + np.array([0, -0.03 * H, 0]))
# markers (tiny bones used as empties)
def front(zc, xc=0.0, dx=6.0, dz=3.0, mask=None):
    m = (np.abs(V[:, 0] - cxf - xc) < dx) & (np.abs(V[:, 2] - zc) < dz)
    if mask is not None: m &= mask
    return float(V[m, 1].min()) if m.any() else ymid - 15.0
zE, zM = lo[2] + 0.843 * H, lo[2] + 0.80 * H
yE = front(zE, mask=(part == 'Head')); yM = front(zM, mask=(part == 'Head')); yC = front(chestZ, mask=(part == 'Body'))
def marker(nm, par, p): add(nm, par, p, np.array(p) + np.array([0, 0, 0.004 * H]))
marker('EyeL', 'Head', [cxf + 3.6, yE + 1.5, zE]); marker('EyeR', 'Head', [cxf - 3.6, yE + 1.5, zE]); marker('MouthP', 'Head', [cxf, yM + 1.5, zM]); marker('ChestP', 'Chest', [cx, yC + 2.0, chestZ])
marker('HandL', 'WristL', J['HL']); marker('HandR', 'WristR', J['HR'])
# ---- weights
Vs = (V - belt) * k; Z = V[:, 2]; X = V[:, 0] - cx
def Sm(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
names = list(bones); idx = {nm: i for i, nm in enumerate(names)}; W = np.zeros((n, len(names)), np.float32)
zHipJ = belt[2] - 0.045 * H; kneeZ = lo[2] + 0.265 * H; ankZ = lo[2] + 0.062 * H
# body (trunk + legs + pauldrons)
bi_ = np.where(part == 'Body')[0]
a_ = Sm(belt[2] + 0.0 * H, belt[2] + 0.07 * H, Z); b_ = Sm(zMid - 0.05 * H, zMid + 0.05 * H, Z); c_ = Sm(zNB - 0.05 * H, zNB + 0.01 * H, Z)
leg = 1 - Sm(zHipJ - 0.045 * H, zHipJ + 0.04 * H, Z); sidL = Sm(-0.03 * H, 0.03 * H, X); shin = 1 - Sm(kneeZ - 0.035 * H, kneeZ + 0.035 * H, Z); foot = 1 - Sm(ankZ - 0.012 * H, ankZ + 0.035 * H, Z)
latSh = abs(J['SL'][0] - cx); zSh = J['SL'][2]
shp = Sm(latSh * 0.52, latSh * 0.92, np.abs(X)) * Sm(zSh - 0.13 * H, zSh - 0.03 * H, Z)      # the pauldron / sleeve cap
for i in bi_:
    tr = np.zeros(len(names)); lg = leg[i]
    nl = 1 - lg; sp = 1 - shp[i]
    tr[idx['Hips']] = (1 - a_[i]) * nl * sp; tr[idx['Spine']] = a_[i] * (1 - b_[i]) * nl * sp; tr[idx['Chest']] = b_[i] * (1 - c_[i]) * nl * sp; tr[idx['Neck']] = c_[i] * nl * sp
    sd = 'L' if sidL[i] > 0.5 else 'R'; ws = sidL[i]
    for ssd, wsd in (('L', ws), ('R', 1 - ws)):
        l = lg * wsd; fo = foot[i]
        tr[idx['Thigh' + ssd]] += l * (1 - shin[i]); tr[idx['Shin' + ssd]] += l * shin[i] * (1 - fo); tr[idx['Foot' + ssd]] += l * shin[i] * fo
    ssd = 'L' if X[i] > 0 else 'R'; tr[idx['Clav' + ssd]] += shp[i] * 0.55; tr[idx['UpperArm' + ssd]] += shp[i] * 0.45 * Sm(0.0, 1.0, 1.0 - (zSh - Z[i]) / (0.13 * H))
    tr[idx['Chest']] += shp[i] * 0.45 * (1 - Sm(0.0, 1.0, 1.0 - (zSh - Z[i]) / (0.13 * H))); W[i] = tr
# head
for i in np.where(part == 'Head')[0]:
    hw = Sm(neckZ - 0.012 * H, neckZ + 0.04 * H, Z[i]); W[i, idx['Head']] = hw; W[i, idx['Neck']] = 1 - hw
# arms
for sd in 'LR':
    S, E, Hd = J['S' + sd], J['E' + sd], J['H' + sd]; ax1 = E - S; ax2 = Hd - E
    for i in np.where(part == 'Arm' + sd)[0]:
        p = V[i]
        if upper[i]:
            u = np.dot(p - S, ax1) / np.dot(ax1, ax1); wF = Sm(0.72, 1.2, u); wC = (1 - Sm(-0.1, 0.3, u)) * 0.55; W[i, idx['ForeArm' + sd]] = wF; W[i, idx['Clav' + sd]] = wC; W[i, idx['UpperArm' + sd]] = max(0.0, 1 - wF - wC)
        else:
            f = np.dot(p - E, ax2) / np.dot(ax2, ax2); wU = 1 - Sm(-0.25, 0.22, f); wH = Sm(0.58, 0.86, f); W[i, idx['UpperArm' + sd]] = wU * 0.9; W[i, idx['Wrist' + sd]] = wH * (1 - wU); W[i, idx['ForeArm' + sd]] = max(0.0, 1 - wU * 0.9 - wH * (1 - wU))
# keep the 4 strongest
for i in range(n):
    r = W[i]; o = np.argsort(-r)[4:]; r[o] = 0; s = r.sum(); W[i] = r / s if s > 1e-6 else r
    if s <= 1e-6: W[i, idx['Chest']] = 1.0
# ---- build the output meshes and the armature
mats = list(me.materials)
objs = {}
for pn in ('Body', 'Head', 'ArmL', 'ArmR'):
    keep = {i for i in range(n) if part[i] == pn}; b2 = bmesh.new(); b2.from_mesh(me); b2.verts.ensure_lookup_table()
    # carry the weights through the vertex deletion with a custom int layer
    lay = b2.verts.layers.int.new('oi')
    for v in b2.verts: v[lay] = v.index
    gone = [v for v in b2.verts if v.index not in keep]
    bmesh.ops.delete(b2, geom=gone, context='VERTS'); m2 = bpy.data.meshes.new(pn + 'M'); b2.verts.ensure_lookup_table(); oi = [v[lay] for v in b2.verts]; b2.to_mesh(m2); b2.free()
    for v, o in zip(m2.vertices, oi): v.co = mathutils.Vector(Vs[o])
    for mt in mats: m2.materials.append(mt)
    o = bpy.data.objects.new(pn, m2); sc.collection.objects.link(o); objs[pn] = (o, oi)
bpy.data.objects.remove(ob)
arm_d = bpy.data.armatures.new('GxArm'); arm = bpy.data.objects.new('Gx', arm_d); sc.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for nm in names:
    par, h, t = bones[nm]; eb = arm_d.edit_bones.new(nm); eb.head = mathutils.Vector(h); eb.tail = mathutils.Vector(t)
    if (eb.tail - eb.head).length < 1e-4: eb.tail = eb.head + mathutils.Vector((0, 0, 0.05))
for nm in names:
    par = bones[nm][0]
    if par: arm_d.edit_bones[nm].parent = arm_d.edit_bones[par]
bpy.ops.object.mode_set(mode='OBJECT')
for pn, (o, oi) in objs.items():
    for nm in names: o.vertex_groups.new(name=nm)
    for j, oidx in enumerate(oi):
        for b in np.nonzero(W[oidx])[0]: o.vertex_groups[names[b]].add([j], float(W[oidx, b]), 'REPLACE')
    md = o.modifiers.new('Armature', 'ARMATURE'); md.object = arm; o.parent = arm
# emissive -> base colour as before
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    nt = mat.node_tree; bsdf = next((nn for nn in nt.nodes if nn.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None: continue
    def img_of(sock):
        if not sock.is_linked: return None
        nn = sock.links[0].from_node
        return nn.image if nn.type == 'TEX_IMAGE' else None
    bi, ei = img_of(bsdf.inputs['Base Color']), img_of(bsdf.inputs['Emission Color'])
    if bi is not None and ei is not None:
        w, h = bi.size; e2 = ei
        if tuple(ei.size) != (w, h): e2 = ei.copy(); e2.scale(w, h)
        Bp = np.empty(w * h * 4, dtype=np.float32); Ep = np.empty(w * h * 4, dtype=np.float32); bi.pixels.foreach_get(Bp); e2.pixels.foreach_get(Ep)
        R = np.clip(Bp + Ep, 0.0, 1.0); R[3::4] = Bp[3::4]; bi.pixels.foreach_set(R); bi.update()
    for nm in ('Emission Color', 'Normal'):
        for l in list(bsdf.inputs[nm].links): nt.links.remove(l)
    bsdf.inputs['Emission Strength'].default_value = 0.0
json.dump({'bones': {nm: {'parent': bones[nm][0], 'head': [float(x) for x in bones[nm][1]], 'tail': [float(x) for x in bones[nm][2]]} for nm in names}, 'order': names, 'height': HM, 'k': float(k)}, open(js, 'w'), indent=1)
print('RIG bones', len(names), 'k', round(k, 4))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False, export_skins=True)
print('EXPORTED', out)
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.glb', '.blend'))
