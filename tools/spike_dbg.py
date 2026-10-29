import bpy, sys, mathutils
src = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src); sc = bpy.context.scene
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
def pos(frame):
    sc.frame_set(frame); dg = bpy.context.evaluated_depsgraph_get(); out = {}
    for o in meshes:
        ev = o.evaluated_get(dg); out[o.name] = [ev.matrix_world @ v.co for v in ev.data.vertices]
    return out
a = pos(48); b = pos(0)
for name in a:
    d = [((p - q).length, i) for i, (p, q) in enumerate(zip(a[name], b[name]))]; d.sort(reverse=True); o = bpy.data.objects[name]
    print(name, 'max disp', round(d[0][0], 1), 'verts > 20:', sum(1 for x, _ in d if x > 20))
    for dist, i in d[:3]:
        v = o.data.vertices[i]; print('   v', i, round(dist, 1), 'rest', tuple(round(x, 1) for x in a[name][i]), [(o.vertex_groups[g.group].name, round(g.weight, 2)) for g in v.groups])
