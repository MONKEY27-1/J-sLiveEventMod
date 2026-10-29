"""blender -b -P make_stage.py -- <in concert_stage.glb> <out stage.glb>
Textures and splits the untextured concert stage (Sketchfab 'concert_stage'). Faces are classified by island (connected pieces) into parts, each its own named object with its own material and planar uv:
  StageDeck (dark glossy floor with LED grid lines) | StageWall (back wall panels) | StageArch (the big front frame) | StageMetal (trusses, steps, lights, the rest) |
  LEDBack, LEDSideL, LEDSideR (the three screens; A_ = unlit, uv 0..1 over the whole screen: the plugin plays flip-book visuals on them) | the drum kit is removed.
Added: DjBooth (riser, two decks, mixer, Marshmello face plate) | Strip0..Strip{n} (light strips: deck edge, arch beam; A_) | text objects in the Impact font: TextAlone, TextWant, TextHappier, LogoF, TextMello (flat, fixed
orientation, A_ unlit) in front of the back screen.  Blender frame: stage front faces -Y, deck at z = 3.2, back screen centre x = -10.5."""
import bpy, bmesh, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
src, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup()
bpy.ops.import_scene.gltf(filepath=src)
objs = [o for o in bpy.data.objects if o.type == 'MESH']
for o in objs: o.parent = None
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join()
st = bpy.context.view_layer.objects.active; st.name = 'Stage'
for e in [o for o in bpy.data.objects if o.type == 'EMPTY']: bpy.data.objects.remove(e, do_unlink=True)
me = st.data; me.materials.clear()
# ---- materials
def tex_deck():
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 6, 71); cell = 8; gl = np.exp(-(np.minimum((U * cell) % 1, 1 - (U * cell) % 1) / 0.03) ** 2) + np.exp(-(np.minimum((V * cell) % 1, 1 - (V * cell) % 1) / 0.03) ** 2)
    base = np.stack([0.05 + 0.04 * n, 0.06 + 0.05 * n, 0.12 + 0.07 * n], -1) + np.stack([0.0 * gl, 0.35 * gl, 0.55 * gl], -1) * 0.8; return rgba(np.clip(base[..., 0], 0, 1), np.clip(base[..., 1], 0, 1), np.clip(base[..., 2], 0, 1), 1.0)
def tex_wall():
    W = H = 128; U, V = grid(W, H); n = fbm(W, H, 5, 72); col = 0.08 + 0.07 * n + 0.04 * V; return rgba(col, col * 1.05, col * 1.4, 1.0)
def tex_arch():
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 6, 73); s = ((U * 14 + V * 14) % 1 < 0.5) & (V > 0.86) | ((U * 14 - V * 14) % 1 < 0.5) & (V < 0.14); col = np.where(s[..., None], np.array([0.95, 0.75, 0.1]), np.array([0.05, 0.05, 0.07])[None, None, :] + 0.05 * n[..., None]); return rgba(col[..., 0], col[..., 1], col[..., 2], 1.0)
def tex_led_fallback():
    W, H = 256, 128; U, V = grid(W, H); return rgba(0.3 + 0.7 * U, 0.2 + 0.6 * np.abs(np.sin(V * 6)), 0.9 - 0.5 * U, 1.0)
M = {n: material(n, a) for n, a in (('Deck', tex_deck()), ('Wall', tex_wall()), ('Arch', tex_arch()), ('Metal', tex_concrete(74, (0.18, 0.19, 0.22))), ('A_LED', tex_led_fallback()))}
M['A_Strip'] = material('A_Strip', tex_solid(1, 1, 1)); M['A_TextY'] = material('A_TextY', tex_solid(1.0, 0.85, 0.1)); M['A_TextW'] = material('A_TextW', tex_solid(1, 1, 1)); M['A_TextP'] = material('A_TextP', tex_solid(0.75, 0.45, 1.0))
M['Booth'] = material('Booth', tex_concrete(75, (0.7, 0.72, 0.8))); M['Black'] = material('Black', tex_solid(0.03, 0.03, 0.04)); M['A_Face'] = material('A_Face', tex_solid(1, 1, 1))
names = ['Deck', 'Wall', 'Arch', 'Metal', 'A_LED']
for n in names: me.materials.append(M[n])
IDX = {n: i for i, n in enumerate(names)}
# ---- classify islands
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); seen = set(); islands = []
for f in bm.faces:
    if f.index in seen: continue
    stack = [f]; seen.add(f.index); comp = []
    while stack:
        c = stack.pop(); comp.append(c)
        for e in c.edges:
            for nb in e.link_faces:
                if nb.index not in seen: seen.add(nb.index); stack.append(nb)
    islands.append(comp)
led = {'LEDBack': [], 'LEDSideL': [], 'LEDSideR': []}; kill = []
for comp in islands:
    pts = [v.co for f in comp for v in f.verts]; mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)]); area = sum(f.calc_area() for f in comp)
    nrm = sum((f.normal * f.calc_area() for f in comp), Vector()); nrm.normalize(); mat = 'Metal'
    if -16 < mn.x and mx.x < -4 and 69.5 < mn.y and mx.y < 77 and mn.z > 1.5 and mx.z < 8.2: kill += comp; continue                       # the drum kit
    if abs(mn.y - 82.6) < 0.2 and abs(mx.y - 82.6) < 0.2 and nrm.y < -0.9 and area > 200: mat = 'A_LED'; led['LEDBack'] += comp
    elif 10 < mn.z < 11 and 18 < mx.z < 19 and area > 80 and nrm.y < -0.5 and mx.x < -20: mat = 'A_LED'; led['LEDSideL'] += comp
    elif 10 < mn.z < 11 and 18 < mx.z < 19 and area > 80 and nrm.y < -0.5 and mn.x > 0: mat = 'A_LED'; led['LEDSideR'] += comp
    elif area > 2000 and nrm.z > 0.9: mat = 'Deck'
    elif mx.z > 30 and mn.y > 78 and area > 300: mat = 'Wall'
    elif 68 < mn.y < 72.5 and area > 60: mat = 'Arch'
    for f in comp: f.material_index = IDX[mat]
bmesh.ops.delete(bm, geom=kill, context='FACES_ONLY')
bm.to_mesh(me); bm.free()
# uv: planar by the dominant axis of each face (screens: 0..1 over the screen)
def planar_uv(ob, tile=6.0, fixed=None):
    m = ob.data; uv = m.uv_layers.new();
    for p in m.polygons:
        n = p.normal; a = (abs(n.x), abs(n.y), abs(n.z));
        for li, vi in zip(p.loop_indices, p.vertices):
            c = m.vertices[vi].co
            if fixed: uv.data[li].uv = fixed(c)
            else: uv.data[li].uv = ((c.y / tile, c.z / tile) if a[0] >= max(a) else (c.x / tile, c.z / tile) if a[1] >= max(a) else (c.x / tile, c.y / tile))
planar_uv(st)
bpy.ops.object.select_all(action='DESELECT'); st.select_set(True); bpy.context.view_layer.objects.active = st; bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.mesh.separate(type='MATERIAL')
parts = {}
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    mname = o.data.materials[0].name if o.data.materials else ''
    nm = {'Deck': 'StageDeck', 'Wall': 'StageWall', 'Arch': 'StageArch', 'Metal': 'StageMetal', 'A_LED': 'LEDAll'}.get(mname.split('.')[0], o.name)
    o.name = nm; parts[nm] = o
# split the LED object into the three screens by position and give each screen uv 0..1
led_all = parts.pop('LEDAll', None)
if led_all:
    bpy.ops.object.select_all(action='DESELECT'); led_all.select_set(True); bpy.context.view_layer.objects.active = led_all
    bm = bmesh.new(); bm.from_mesh(led_all.data); bm.faces.ensure_lookup_table(); grp = {'LEDBack': [], 'LEDSideL': [], 'LEDSideR': []}
    for f in bm.faces:
        c = f.calc_center_median(); (grp['LEDBack'] if abs(c.y - 82.6) < 0.3 else grp['LEDSideL'] if c.x < -15 else grp['LEDSideR']).append(f.index)
    for name, idxs in grp.items():
        if not idxs: continue
        b2 = bmesh.new(); b2.from_mesh(led_all.data); b2.faces.ensure_lookup_table(); drop = [f for f in b2.faces if f.index not in idxs]; bmesh.ops.delete(b2, geom=drop, context='FACES_ONLY')
        me2 = bpy.data.meshes.new(name); b2.to_mesh(me2); b2.free(); me2.materials.append(M['A_LED']); o2 = bpy.data.objects.new(name, me2); bpy.context.collection.objects.link(o2)
        xs = [v.co.x for v in me2.vertices]; zs = [v.co.z for v in me2.vertices]; ys = [v.co.y for v in me2.vertices]; x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs); y0, y1 = min(ys), max(ys)
        horiz = (lambda c: ((c.x - x0) / max(1e-6, x1 - x0), (c.z - z0) / max(1e-6, z1 - z0))) if name == 'LEDBack' else (lambda c: ((c.x - x0) / max(1e-6, x1 - x0) if (y1 - y0) < 1e-3 or name == 'LEDSideL' else (c.x - x0) / max(1e-6, x1 - x0), (c.z - z0) / max(1e-6, z1 - z0)))
        planar_uv(o2, fixed=horiz); parts[name] = o2
    bpy.data.objects.remove(led_all, do_unlink=True)
# ---- the DJ booth
bb = MB([M['Booth'], M['Black'], M['A_Face'], M['A_Strip']])
bb.box((-10, 73.2, 3.2 + 0.55), (11, 6.5, 1.1), 0, 3.0); bb.box((-10, 73.2, 3.2 + 1.2), (11.4, 6.9, 0.2), 1, 2.0)                     # riser
for sx in (-1, 1):
    bb.cyl((-10 + sx * 3.2, 72.2, 3.2 + 1.45), 1.5, 0.25, 1, 24); bb.cyl((-10 + sx * 3.2, 72.2, 3.2 + 1.62), 1.2, 0.05, 3, 24)                # two decks with platters
bb.box((-10, 72.2, 3.2 + 1.5), (2.6, 1.6, 0.3), 1, 2.0)                                                                                  # mixer
bb.quad((-14.5, 69.9, 3.2 + 0.1), (-5.5, 69.9, 3.2 + 0.1), (-5.5, 69.9, 3.2 + 1.0), (-14.5, 69.9, 3.2 + 1.0), 3, [(0, 0), (1, 0), (1, 1), (0, 1)])
booth = bb.build('DjBooth')
# the Marshmello face plate on the front of the riser (own object, unlit): white square, two X eyes, a smile
W_, H_ = 256, 256; Ug, Vg = grid(W_, H_); face = np.ones((H_, W_, 4), np.float32)
for cx in (0.3, 0.7):
    d = np.minimum(np.abs((Ug - cx) - (Vg - 0.62)), np.abs((Ug - cx) + (Vg - 0.62))); m = (d < 0.035) & (np.abs(Ug - cx) < 0.1) & (np.abs(Vg - 0.62) < 0.1); face[m, :3] = 0.05
mouth = (np.abs(Vg - (0.3 + 0.35 * (Ug - 0.5) ** 2)) < 0.025) & (Ug > 0.22) & (Ug < 0.78); face[mouth, :3] = 0.05; face[(Ug < 0.02) | (Ug > 0.98) | (Vg < 0.02) | (Vg > 0.98), :3] = 0.6
fm = material('A_Face2', face); fp = MB([fm]); fp.quad((-13.5, 69.85, 3.4), (-6.5, 69.85, 3.4), (-6.5, 69.85, 4.2), (-13.5, 69.85, 4.2), 0, [(0, 0), (1, 0), (1, 1), (0, 1)]); fo = fp.build('MelloFace')
# the face square needs to be square: a 1.1 m plate
fo.data.vertices[0].co = Vector((-10.55, 69.85, 3.35)); fo.data.vertices[1].co = Vector((-9.45, 69.85, 3.35)); fo.data.vertices[2].co = Vector((-9.45, 69.85, 4.45)); fo.data.vertices[3].co = Vector((-10.55, 69.85, 4.45))
# ---- light strips: along the front edge of the deck and along the arch beam (named Strip0..)
sk = 0
def strip(p0, p1, th=0.18):
    global sk
    sb = MB([M['A_Strip']]); L = math.dist(p0, p1); cx = [(p0[i] + p1[i]) / 2 for i in range(3)]
    if abs(p1[0] - p0[0]) > abs(p1[2] - p0[2]): sb.box(cx, (L, th, th), 0, 1.0)
    else: sb.box(cx, (th, th, L), 0, 1.0)
    o = sb.build('Strip%d' % sk); sk += 1
for i in range(28): x0 = -44.0 + i * (66.0 / 28); strip((x0, 53.7, 3.45), (x0 + 66.0 / 28 * 0.85, 53.7, 3.45))
for i in range(22): x0 = -38.5 + i * (60.5 / 22); strip((x0, 70.6, 28.4), (x0 + 60.5 / 22 * 0.85, 70.6, 28.4))
for i in range(8): z0 = 4.5 + i * 3.0; strip((-44.0, 70.6, z0), (-44.0, 70.6, z0 + 2.4)); strip((22.2, 70.6, z0), (22.2, 70.6, z0 + 2.4))
# ---- the text (Impact), flat and fixed: it never turns towards the viewer
fnt = bpy.data.fonts.load(r'C:\Windows\Fonts\impact.ttf')
def text_obj(name, body, size, x, z, mat, y=82.0, extrude=0.18, align='CENTER'):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.font = fnt; cu.size = size; cu.extrude = extrude; cu.align_x = align; cu.align_y = 'CENTER'; cu.space_character = 1.0
    o = bpy.data.objects.new(name, cu); bpy.context.collection.objects.link(o); o.rotation_euler = (math.pi / 2, 0, 0); o.location = (x, y, z)          # stands upright facing -Y (the audience)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.convert(target='MESH'); o.data.materials.append(mat); return o
text_obj('TextAlone', 'ALONE', 6.0, -10.5, 15.0, M['A_TextW']); text_obj('TextWant', 'I WANT YOU TO BE', 2.1, -10.5, 18.4, M['A_TextW']); text_obj('TextHappier', 'HAPPIER', 5.4, -10.5, 13.4, M['A_TextY']); text_obj('TextMello', 'MARSHMELLO', 3.2, -10.5, 15.0, M['A_TextW'])
f = text_obj('LogoF', 'F', 8.0, -10.5, 33.75, M['A_TextP'], y=70.2, extrude=0.4)
fr = MB([M['A_TextP']]); fr.box((-10.5, 70.2, 31.0), (11, 0.4, 0.6), 0, 1.0); fr.box((-10.5, 70.2, 31.0 + 5.5), (11, 0.4, 0.6), 0, 1.0); fr.box((-10.5 - 5.5, 70.2, 31.0 + 2.75), (0.6, 0.4, 6.0), 0, 1.0); fr.box((-10.5 + 5.5, 70.2, 31.0 + 2.75), (0.6, 0.4, 6.0), 0, 1.0); fr.build('LogoFrame')
export(out)
print('parts', sorted(o.name for o in bpy.data.objects if o.type == 'MESH')[:60])
