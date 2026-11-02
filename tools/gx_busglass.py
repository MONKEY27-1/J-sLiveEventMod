"""blender -b -P gx_busglass.py -- in.glb out.glb     the Stark bus (zc_prep output): the painted-brown window faces of the body (material_0) become a transparent glass material (A_ = alpha in the plugin), and an inside is added:
a floor, two rows of blue benches, the green glowing equipment, red emergency lights and ceiling lamps (G_ = glowing materials).  Everything in the model's own units, so it scales with the bus."""
import bpy, sys, bmesh, mathutils
import numpy as np, time
T0 = time.time()
def P(*x): print('T', round(time.time() - T0, 1), *x, flush=True)
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
body = None
for o in bpy.data.objects:
    if o.type == 'MESH' and any(m and m.name.startswith('material_0') for m in o.data.materials): body = o
me = body.data; W = body.matrix_world
print('BODY', body.name, len(me.polygons), 'tris-polys')
slot = next(i for i, m in enumerate(me.materials) if m and m.name.startswith('material_0'))
img = None
for n in me.materials[slot].node_tree.nodes:
    if n.type == 'TEX_IMAGE': img = n.image
P('image', img.size[0], img.size[1]); w, h = img.size; px = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(px); px = px.reshape(h, w, 4)
uv = me.uv_layers.active.data
def tex(p):
    u, v = p; return px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w)))][:3]
P('pixels read'); win = []
VV = np.array([(W @ v.co)[:] for v in me.vertices]); ZLOW = float(VV[:, 2].min() + 0.42 * (VV[:, 2].max() - VV[:, 2].min()))
for pl in me.polygons:
    if pl.material_index != slot: continue
    c = np.mean([uv[l].uv[:] for l in pl.loop_indices], axis=0); r, g, b = tex(c)
    zc_ = np.mean([(W @ me.vertices[vi].co)[2] for vi in pl.vertices])
    if (0.01 < r < 0.25 and r - b > 0.008 and g > b * 0.9 and r > g * 0.9) or (max(r, g, b) < 0.1 and zc_ > ZLOW): win.append(pl.index)   # the brown window paint + the near-black windshield / rear glass above the sill
P('classified'); print('WINDOW faces', len(win), 'of', len(me.polygons))
V = np.array([(W @ v.co)[:] for v in me.vertices])
if win:
    WV = np.array([(W @ me.vertices[vi].co)[:] for pi in win for vi in me.polygons[pi].vertices])
    print('WINDOW bounds min', WV.min(0).round(2), 'max', WV.max(0).round(2))
if win:
    zc = np.array([np.mean([(W @ me.vertices[vi].co)[2] for vi in me.polygons[pi].vertices]) for pi in win]); yc = np.array([np.mean([(W @ me.vertices[vi].co)[1] for vi in me.polygons[pi].vertices]) for pi in win]); xc = np.array([np.mean([(W @ me.vertices[vi].co)[0] for vi in me.polygons[pi].vertices]) for pi in win])
    print('WZ hist', np.histogram(zc, bins=10)); print('WX hist', np.histogram(xc, bins=8)); print('WY hist', np.histogram(yc, bins=10))
print('BODY bounds min', V.min(0).round(2), 'max', V.max(0).round(2))
def solid(name, rgb, glow=False, alpha=False):
    img2 = bpy.data.images.new(name + '_t', 4, 4); img2.pixels = [rgb[0], rgb[1], rgb[2], 1.0] * 16; img2.pack()
    m = bpy.data.materials.new('SF_' + ('G_' if glow else 'A_' if alpha else '') + name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img2; nt.links.new(tn.outputs['Color'], bs.inputs['Base Color']); bs.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); return m
P('bounds done'); glass = solid('glass', (0.05, 0.08, 0.12), alpha=True); me.materials.append(glass); gi = len(me.materials) - 1
for pi in win: me.polygons[pi].material_index = gi
# ---- the inside (this model is in centimetres: the bus is 13.5 m long). Side windows span z -70 .. +30, so the floor is ~1.1 m under the sill.
floor = -185.0; hx = 175.0; y0, y1 = -590.0, 590.0
def mat_plain(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True; m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); return m
seat_m = mat_plain('seat', (0.08, 0.16, 0.5)); floor_m = mat_plain('floor', (0.16, 0.16, 0.18))
gm = solid('green', (0.2, 1.0, 0.35), glow=True); rm = solid('red', (1.0, 0.18, 0.12), glow=True); lm = solid('lamp', (1.0, 0.95, 0.8), glow=True)
def new_obj(name, mat):
    me2 = bpy.data.meshes.new(name); ob2 = bpy.data.objects.new(name, me2); bpy.context.collection.objects.link(ob2); me2.materials.append(mat); return ob2, bmesh.new()
def box(bm, c, sz):
    r = bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=sz, verts=r['verts']); bmesh.ops.translate(bm, vec=c, verts=r['verts'])
def cyl(bm, c, rad, hgt):
    r = bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=rad, radius2=rad, depth=hgt); bmesh.ops.translate(bm, vec=c, verts=r['verts'])
P('materials made')
import os
if os.environ.get('NOINT'):
    bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False); P('exported (no interior)'); sys.exit(0)
seat_o, sb = new_obj('seats', seat_m); floor_o, fb = new_obj('floor', floor_m); green_o, gb = new_obj('green', gm); red_o, rb = new_obj('red', rm); lamp_o, lb = new_obj('lamps', lm)
box(fb, (0, (y0 + y1) / 2, floor), (2 * hx - 10, y1 - y0 + 100, 8))
n = 11
for sgn in (-1, 1):
    for i in range(n):
        y = y0 + 60 + i * (y1 - y0 - 120) / (n - 1)
        box(sb, (sgn * (hx - 55), y, floor + 42), (105, 62, 16)); box(sb, (sgn * (hx - 55), y - 28, floor + 88), (105, 12, 80))
        box(lb, (sgn * (hx - 75), y, floor + 232), (22, 50, 6))
cyl(gb, (0, y1 - 230, floor + 90), 55, 160); cyl(gb, (0, y1 - 230, floor + 190), 30, 80); cyl(gb, (90, y1 - 120, floor + 60), 22, 100); cyl(gb, (-90, y1 - 120, floor + 60), 22, 100)
for yy in (y0 + 20, y1 - 20, 0.0): box(rb, (0, yy, floor + 215), (80, 8, 18))
for ob2, b2 in ((seat_o, sb), (floor_o, fb), (green_o, gb), (red_o, rb), (lamp_o, lb)): b2.to_mesh(ob2.data); b2.free()
P('inside built')
print('INSIDE floor z', floor, 'rows', n)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
P('exported')
