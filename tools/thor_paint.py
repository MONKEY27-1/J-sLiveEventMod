"""blender -b -P thor_paint.py -- in.glb out.glb     the user's Thor has NO textures (flat grey): split every mesh into painted materials by region (world z of each face in the rest pose) and give each a small
generated texture (fabric / brushed steel / leather / hair strands / skin), then export a glb with the armature kept (zc_anim.py makes the clips from it).
Object_10 hands, Object_8 face, Object_11 hair, Object_7 arms + belt (steel armour), Object_9 tunic / legs / boots / breastplate."""
import bpy, sys, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
rng = np.random.default_rng(7); N = 256
def fbm(n):
    z = np.zeros((n, n), np.float32)
    for o, w in ((4, 0.5), (8, 0.3), (16, 0.2)):
        g = rng.random((o, o)).astype(np.float32); z += w * np.kron(g, np.ones((n // o, n // o), np.float32))
    return z
def make(name, base, kind):
    f = fbm(N); yy, xx = np.mgrid[0:N, 0:N]; c = np.array(base, np.float32)
    if kind == 'fabric': v = 0.9 + 0.12 * f
    elif kind == 'steel': v = 0.88 + 0.14 * f
    elif kind == 'leather': v = 0.85 + 0.2 * f
    elif kind == 'hair': v = 0.85 + 0.1 * np.sin(xx * 0.35 + 2 * f) + 0.1 * f
    else: v = 0.92 + 0.14 * f
    img = np.clip(c[None, None, :3] * v[..., None], 0, 1); px = np.ones((N, N, 4), np.float32); px[..., :3] = img
    im = bpy.data.images.new(name + '_t', N, N); im.pixels.foreach_set(px.ravel()); im.pack()
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']; tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = im
    nt.links.new(tn.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = 0.35 if kind == 'steel' else 0.8
    if kind == 'steel': bs.inputs['Metallic'].default_value = 0.0
    return m
M = {'skin': make('thor_skin', (0.74, 0.52, 0.42), 'skin'), 'hair': make('thor_hair', (0.78, 0.58, 0.2), 'hair'), 'band': make('thor_band', (0.6, 0.62, 0.7), 'steel'),
     'steel': make('thor_steel', (0.5, 0.54, 0.62), 'steel'), 'navy': make('thor_navy', (0.07, 0.11, 0.26), 'fabric'), 'leather': make('thor_leather', (0.3, 0.17, 0.09), 'leather'),
     'gold': make('thor_gold', (0.85, 0.66, 0.2), 'steel'), 'dark': make('thor_dark', (0.07, 0.09, 0.16), 'fabric')}
dg = bpy.context.evaluated_depsgraph_get()
def region(name, p):
    z = p.z
    if name == 'Object_10' or name == 'Object_8': return 'skin'
    if name == 'Object_11': return 'hair'
    if name == 'Object_7': return 'leather' if 0.88 < z < 1.02 and abs(p.x + 0.1) < 0.35 else 'steel'   # belt / pauldrons + bracers
    if name == 'Object_9':
        if z < 0.17: return 'leather'                                        # boots
        if z > 1.0 and abs(p.x + 0.05) < 0.17: return 'gold'                  # breastplate / medallion
        if z > 0.95: return 'navy'
        if z < 0.5 and (p.y > 0.0 or z < 0.35): return 'steel'                # shin + knee plates (rough)
        return 'navy'
    return 'navy'
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    if o.name == 'Icosphere': continue
    ev = o.evaluated_get(dg); W = ev.matrix_world; me = o.data; keys = list(M)
    me.materials.clear()
    for k in keys: me.materials.append(M[k])
    for pl, pe in zip(me.polygons, ev.data.polygons):   # the evaluated (skinned, posed) mesh has the same faces: its centres are where the costume really is
        c = W @ mathutils.Vector(pe.center); pl.material_index = keys.index(region(o.name, c))
    print('PAINT', o.name, {k: sum(1 for pl in me.polygons if pl.material_index == i) for i, k in enumerate(keys) if any(pl.material_index == i for pl in me.polygons)})
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
print('EXPORTED', out)
