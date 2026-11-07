"""blender -b -P thor_paint2.py -- in.glb out.glb     Thor, painted PER ISLAND (every armour piece / strap / eye of the sculpt is its own connected island, so the colours have clean edges instead of the old height bands):
steel armour (pauldrons, bracers, greaves, knee plates, spikes), navy tunic, dark trousers, brown leather (belt, straps, boots), gold medallions / buckles / arm bands, skin, golden hair (two tones), eyes, lips.
Exports a glb with the armature kept (zc_chars.py thor -> zc_anim.py makes the clips from it)."""
import bpy, sys, bmesh, mathutils
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
rng = np.random.default_rng(11); N = 256
def fbm(n):
    z = np.zeros((n, n), np.float32)
    for o, w in ((4, 0.4), (8, 0.3), (16, 0.2), (32, 0.1)):
        g = rng.random((o, o)).astype(np.float32); z += w * np.kron(g, np.ones((n // o, n // o), np.float32))
    return z
def make(name, base, kind, rough=0.7):
    f = fbm(N); yy, xx = np.mgrid[0:N, 0:N]; c = np.array(base, np.float32)
    if kind == 'fabric': v = 0.86 + 0.16 * f + 0.05 * np.sin(xx * 1.2) * np.sin(yy * 1.2)
    elif kind == 'steel': v = 0.78 + 0.28 * f + 0.10 * np.sin(yy * 0.3 + 4 * f)
    elif kind == 'leather': v = 0.78 + 0.32 * f
    elif kind == 'hair': v = 0.72 + 0.12 * np.sin(xx * 0.45 + 3 * f) + 0.18 * f
    elif kind == 'gold': v = 0.8 + 0.22 * f + 0.1 * np.sin(xx * 0.2 + yy * 0.2)
    elif kind == 'skin': v = 0.9 + 0.12 * f
    else: v = 0.95 + 0.05 * f
    img = np.clip(c[None, None, :3] * v[..., None], 0, 1); px = np.ones((N, N, 4), np.float32); px[..., :3] = img
    im = bpy.data.images.new(name + '_t', N, N); im.pixels.foreach_set(px.ravel()); im.pack()
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']; tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = im
    nt.links.new(tn.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Roughness'].default_value = rough
    return m
M = {'skin': make('thor_skin', (0.80, 0.57, 0.45), 'skin'), 'hair': make('thor_hair', (0.88, 0.68, 0.26), 'hair'), 'hair2': make('thor_hair2', (0.70, 0.50, 0.17), 'hair'),
     'steel': make('thor_steel', (0.50, 0.54, 0.62), 'steel', 0.4), 'navy': make('thor_navy', (0.08, 0.12, 0.30), 'fabric'), 'dark': make('thor_dark', (0.045, 0.055, 0.11), 'fabric'),
     'leather': make('thor_leather', (0.36, 0.20, 0.10), 'leather'), 'gold': make('thor_gold', (0.92, 0.72, 0.22), 'gold', 0.35), 'white': make('thor_white', (0.95, 0.95, 0.95), 'x'),
     'iris': make('thor_iris', (0.25, 0.5, 0.95), 'x'), 'black': make('thor_black', (0.03, 0.03, 0.04), 'x'), 'lip': make('thor_lip', (0.58, 0.30, 0.28), 'skin')}
KEYS = list(M)
CLS = {
 'Object_10': lambda r, lo, hi, n: 'skin',
 'Object_11': lambda r, lo, hi, n: 'hair' if r % 2 == 0 else 'hair2',
 'Object_8': lambda r, lo, hi, n: 'skin' if r == 0 else 'white' if r in (1, 2) else 'iris' if r in (5, 6) else 'black' if r in (10, 11, 14, 15) else 'lip',
 'Object_7': lambda r, lo, hi, n: ('steel' if r in (0, 1, 2, 3, 8, 9) or 15 <= r <= 22 else 'skin' if r in (5, 6) else 'leather' if r in (4, 7) else 'gold'),
 'Object_9': lambda r, lo, hi, n: ('navy' if r in (0, 7, 9, 10, 11, 12, 14) else 'dark' if r in (1, 29, 30) else 'leather' if r in (5, 6) else 'gold' if r in (15, 16, 17, 18, 23, 24, 37, 38, 39, 40) else 'steel'),
}
dg = bpy.context.evaluated_depsgraph_get()
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name != 'Icosphere' and o.name in CLS]:
    ev = o.evaluated_get(dg); W = ev.matrix_world; me = o.data
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
    bm.free(); P = np.array([(W @ mathutils.Vector(v.co))[:] for v in ev.data.vertices]); vm = {}
    for r, comp in enumerate(sorted(isl, key=lambda c: -len(c))):
        k = CLS[o.name](r, P[comp].min(0), P[comp].max(0), len(comp))
        for i in comp: vm[i] = KEYS.index(k)
    me.materials.clear()
    for k in KEYS: me.materials.append(M[k])
    for pl in me.polygons: pl.material_index = vm.get(pl.vertices[0], 0)
    print('PAINT', o.name, len(isl), 'islands')
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
print('EXPORTED', out)
