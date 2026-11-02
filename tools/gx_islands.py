"""blender -b -P gx_islands.py -- in.glb : lists the connected islands of the (joined) mesh with normalised bounds (x across, z up; height = 1)"""
import bpy, bmesh, sys
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src = a[0]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
ms = [o for o in sc.objects if o.type == 'MESH']
for o in sc.objects: o.select_set(False)
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(sc.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join(); ob = bpy.context.active_object
bm = bmesh.new(); bm.from_mesh(ob.data); V = np.array([v.co[:] for v in bm.verts]); lo = V.min(0); H = V[:, 2].max() - lo[2]; cx = (V[:, 0].min() + V[:, 0].max()) / 2
seen = set(); isl = []
for v0 in bm.verts:
    if v0.index in seen: continue
    st = [v0]; seen.add(v0.index); comp = []
    while st:
        v = st.pop(); comp.append(v.index)
        for e in v.link_edges:
            w = e.other_vert(v)
            if w.index not in seen: seen.add(w.index); st.append(w)
    isl.append(comp)
print('ISLANDS', len(isl), 'verts', len(V), 'H', round(H, 3), 'cx', round(cx, 3))
rows = []
for comp in isl:
    P = V[comp]; mn = (P.min(0) - np.array([cx, 0, lo[2]])) / H; mx = (P.max(0) - np.array([cx, 0, lo[2]])) / H; rows.append((len(comp), mn, mx))
rows.sort(key=lambda r: -r[0])
for n, mn, mx in rows[:40]: print('  %6d  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f' % (n, mn[0], mx[0], mn[1], mx[1], mn[2], mx[2]))
