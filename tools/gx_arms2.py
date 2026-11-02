"""blender -b -P gx_arms2.py -- in.glb    islands of the arm mesh (material_0) of the new Galactus: bounds, centroid, and the mean position per slice along z (to find shoulders / elbows)"""
import bpy, sys, bmesh
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=a[0])
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('SM_Junior_Static_mo_material_0'):
        me = o.data; W = o.matrix_world; V = np.array([(W @ v.co)[:] for v in me.vertices])
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
        print('ISLANDS', len(isl), [len(c) for c in isl])
        for k, c in enumerate(isl):
            P = V[c]; print('ISL', k, 'n', len(c), 'min', P.min(0).round(1), 'max', P.max(0).round(1), 'mean', P.mean(0).round(1))
            if len(c) > 1000:
                z0, z1 = P[:, 2].min(), P[:, 2].max()
                for s in range(12):
                    lo = z0 + (z1 - z0) * s / 12; hi = z0 + (z1 - z0) * (s + 1) / 12; m = (P[:, 2] >= lo) & (P[:, 2] < hi)
                    if m.sum(): print('   z', round(lo, 1), 'n', int(m.sum()), 'mean', P[m].mean(0).round(1), 'x', round(P[m, 0].min(), 1), round(P[m, 0].max(), 1), 'y', round(P[m, 1].min(), 1), round(P[m, 1].max(), 1))
