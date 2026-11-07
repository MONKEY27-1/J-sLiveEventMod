"""blender -b -P thor_isl.py -- in.glb out_prefix    islands of every Thor mesh: size / bbox, plus a render with each island its own colour"""
import bpy, sys, bmesh, mathutils, colorsys
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
dg = bpy.context.evaluated_depsgraph_get(); n = 0
ob = [o for o in bpy.data.objects if o.type == 'MESH' and o.name != 'Icosphere']
for o in bpy.data.objects:
    if o.name == 'Icosphere': o.hide_render = True
mats = []
for o in ob:
    ev = o.evaluated_get(dg); W = ev.matrix_world; me = o.data; bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); seen = set(); isl = []
    for v0 in bm.verts:
        if v0.index in seen: continue
        st = [v0]; seen.add(v0.index); comp = []
        while st:
            v = st.pop(); comp.append(v.index)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w.index not in seen: seen.add(w.index); st.append(w)
        isl.append(comp)
    bm.free(); me.materials.clear(); vc = {}
    P = np.array([(W @ mathutils.Vector(v.co))[:] for v in ev.data.vertices])
    print('OBJ', o.name, 'verts', len(P), 'polys', len(me.polygons), 'islands', len(isl))
    for k, comp in enumerate(sorted(isl, key=lambda c: -len(c))):
        Q = P[comp]; print('  ISL', o.name, k, 'v', len(comp), 'min', Q.min(0).round(2), 'max', Q.max(0).round(2))
        for i in comp: vc[i] = n % 12
        n += 1
    for c in range(12):
        m = bpy.data.materials.new('c%d' % c); m.use_nodes = False; r, g, b = colorsys.hsv_to_rgb(c / 12.0, 0.8, 0.9); m.diffuse_color = (r, g, b, 1); me.materials.append(m)
    for pl in me.polygons: pl.material_index = vc[pl.vertices[0]]
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'MATERIAL'; sc.display.shading.light = 'STUDIO'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.9
for name, d in (('front', (0, -1, 0.05)), ('back', (0, 1, 0.05))):
    d = mathutils.Vector(d).normalized(); cam.location = mathutils.Vector((-0.1, -0.1, 0.72)) + d * 5; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = out + '_' + name + '.png'; bpy.ops.render.render(write_still=True)
