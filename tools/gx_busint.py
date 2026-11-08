"""blender -b -P gx_busint.py -- out.glb      the BATTLE BUS INTERIOR (3:35 - 4:35, 6:04, 7:52): a standalone human-scale cabin you stand inside, windows transparent (SF_A_), built in metres, nose toward Blender -Y (= +Z in the game).
floor / ceiling / walls with 6 side windows each, header band with the EMERGENCY sign, a big windshield over the dashboard, driver seat + steering wheel + gauges, 5 rows of blue seats, the glowing green GAMMA CELLS (spiked orbs) on the seats, ceiling lights, red emergency lights, a rear door.
Origin = the middle of the floor.  Floor 0, ceiling 2.5, x +-1.5, y -6.2 (nose) .. +6.2 (tail)."""
import bpy, bmesh, math, sys, mathutils
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index('--') + 1:]; out = a[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name, rgb, rough=0.6):
    m = bpy.data.materials.new(name); m.use_nodes = True; bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); bs.inputs['Roughness'].default_value = rough; return m
M = {'wall': mat('GxBusWall', (0.72, 0.78, 0.9)), 'trim': mat('GxBusTrim', (0.12, 0.17, 0.5)), 'floor': mat('GxBusFloor', (0.2, 0.21, 0.26)), 'seat': mat('GxBusSeat', (0.08, 0.16, 0.62)), 'dash': mat('GxBusDash', (0.16, 0.17, 0.2)), 'black': mat('GxBusBlack', (0.03, 0.03, 0.04)),
     'glass': mat('SF_A_GxBusGlass', (0.05, 0.08, 0.12)), 'light': mat('SF_G_GxBusLight', (0.9, 0.95, 1.0)), 'red': mat('SF_G_GxBusRed', (0.85, 0.08, 0.06)), 'green': mat('SF_G_GxBusGamma', (0.06, 0.55, 0.1)), 'sign': mat('SF_G_GxBusSign', (0.92, 0.94, 1.0)), 'metal': mat('GxBusMetal', (0.42, 0.44, 0.5))}
KEYS = list(M)
parts = {k: bmesh.new() for k in KEYS}
def box(k, c, sz, bevel=0.0):
    bm = parts[k]; r = bmesh.ops.create_cube(bm, size=1.0); vs = r['verts']; bmesh.ops.scale(bm, vec=Vector(sz), verts=vs); bmesh.ops.translate(bm, vec=Vector(c), verts=vs)
    if bevel > 0: bmesh.ops.bevel(bm, geom=list({e for v in vs for e in v.link_edges}), offset=bevel, segments=2, profile=0.6, affect='EDGES')
def cyl(k, c, r, h, axis='Z', seg=16):
    bm = parts[k]; rr = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=h); vs = rr['verts']
    if axis == 'X': bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'), verts=vs)
    if axis == 'Y': bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'X'), verts=vs)
    bmesh.ops.translate(bm, vec=Vector(c), verts=vs)
def sph(k, c, r, seg=14):
    bm = parts[k]; rr = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=10, radius=r); bmesh.ops.translate(bm, vec=Vector(c), verts=rr['verts'])
def cone(k, c, d, r, h):
    bm = parts[k]; rr = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=r, radius2=0.0, depth=h); vs = rr['verts']
    q = Vector((0, 0, 1)).rotation_difference(Vector(d)); bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=q.to_matrix(), verts=vs); bmesh.ops.translate(bm, vec=Vector(c), verts=vs)
Y0, Y1 = -6.2, 6.2; W = 1.5; H = 2.5      # nose = -Y
# floor + aisle strip, ceiling
box('floor', (0, 0, -0.05), (3.1, 12.5, 0.1)); box('trim', (0, 0, 0.003), (0.5, 11.4, 0.01)); box('wall', (0, 0, H + 0.05), (3.1, 12.5, 0.1))
# side walls: sill, header and pillars around 6 windows
wz = [-4.2 + i * 1.7 for i in range(6)]       # window centres along Y (from the nose: -4.2 .. 4.3)
for sx in (-1, 1):
    x = sx * W
    if sx == 1:   # the DOOR on the right side by the driver: the first window is an opening down to the floor (the hero stands in it at 4:08 / 6:08)
        d0, d1 = wz[0] - 0.65, wz[0] + 0.65
        for (y0, y1) in ((-6.2, d0), (d1, 6.2)): box('wall', (x, (y0 + y1) / 2, 0.5), (0.1, y1 - y0, 1.0)); box('trim', (x - sx * 0.04, (y0 + y1) / 2, 1.03), (0.16, y1 - y0, 0.06))
        box('wall', (x, 0, 2.3), (0.1, 12.4, 0.4)); box('trim', (x - 0.04, wz[0], 2.08), (0.2, 1.4, 0.06)); box('trim', (x - 0.04, d0, 1.05), (0.2, 0.08, 2.1)); box('trim', (x - 0.04, d1, 1.05), (0.2, 0.08, 2.1))
        box('metal', (x + 0.2, wz[0], -0.06), (0.7, 1.3, 0.12), 0.02); box('red', (x - 0.02, wz[0], 0.02), (0.06, 1.3, 0.02))       # the step + a warning strip
        box('wall', (x + 0.28, d0 - 0.02, 1.0), (0.5, 0.05, 2.0)); box('wall', (x + 0.28, d1 + 0.02, 1.0), (0.5, 0.05, 2.0))         # the two folded door leaves, swung out
        box('light', (x - 0.06, wz[0], 2.0), (0.1, 0.9, 0.03))
    else:
        box('wall', (x, 0, 0.5), (0.1, 12.4, 1.0)); box('trim', (x - sx * 0.04, 0, 1.03), (0.16, 12.4, 0.06)); box('wall', (x, 0, 2.3), (0.1, 12.4, 0.4))
    pil = [Y0] + [c + 0.8 for c in wz[:-1]] + [Y1]
    edges = [(-6.2, wz[0] - 0.65)] + [(wz[i] + 0.65, wz[i + 1] - 0.65) for i in range(5)] + [(wz[-1] + 0.65, 6.2)]
    for (y0, y1) in edges: box('wall', (x, (y0 + y1) / 2, 1.65), (0.1, y1 - y0, 1.3)); box('trim', (x - sx * 0.04, (y0 + y1) / 2, 1.65), (0.04, max(0.05, y1 - y0 - 0.06), 1.3))
    # ceiling lights
    for i in range(6): box('light', (sx * 1.0, wz[i], H - 0.03), (0.12, 0.8, 0.03))
# rear wall + door
box('wall', (0, Y1, 1.25), (3.1, 0.1, 2.5)); box('trim', (0, Y1 - 0.06, 1.05), (1.0, 0.04, 2.1)); box('red', (0, Y1 - 0.09, 2.2), (1.1, 0.02, 0.08)); box('red', (0, Y1 - 0.09, 0.0 + 0.05), (1.1, 0.02, 0.08))
for sx in (-1, 1): box('red', (sx * 0.55, Y1 - 0.09, 1.1), (0.08, 0.02, 2.0))
box('red', (0, Y1 - 0.15, 2.4), (0.6, 0.04, 0.08))
# the front: dashboard, windshield, posts
box('dash', (0, Y0 + 0.55, 0.45), (3.0, 1.1, 0.9), 0.05); box('dash', (0, Y0 + 0.2, 1.0), (3.0, 0.5, 0.18), 0.05); box('black', (0, Y0 + 0.62, 0.92), (2.8, 0.7, 0.04))
box('wall', (0, Y0 + 0.05, 2.35), (3.1, 0.1, 0.3)); box('wall', (-W + 0.05, Y0 + 0.05, 1.6), (0.1, 0.1, 1.5)); box('wall', (W - 0.05, Y0 + 0.05, 1.6), (0.1, 0.1, 1.5));
box('wall', (0, -4.9, 2.42), (3.0, 0.5, 0.2))
for gx in (-1.0, -0.6, 0.6, 1.0): cyl('light', (gx, Y0 + 1.04, 0.9), 0.07, 0.03, 'Y', 12)
for gx, kk in ((-0.2, 'green'), (0.0, 'red'), (0.2, 'green')): box(kk, (gx, Y0 + 1.1, 0.935), (0.1, 0.05, 0.03))
# driver seat + steering wheel (driver at -x), mirror and a hanging charm
box('seat', (-0.6, -4.0, 0.45), (0.65, 0.65, 0.2), 0.04); box('seat', (-0.6, -3.65, 0.95), (0.65, 0.15, 0.95), 0.04); box('black', (-0.6, -4.0, 0.2), (0.12, 0.12, 0.3)); box('metal', (-0.6, -3.55, 1.5), (0.5, 0.12, 0.12), 0.03)
bm = parts['black']; r = bmesh.ops.create_circle(bm, cap_ends=False, segments=24, radius=0.24);
tor = bmesh.new(); bmesh.ops.create_circle(tor, cap_ends=False, segments=28, radius=0.24)
cyl('black', (-0.6, -5.0, 1.15), 0.02, 0.5, 'Y', 8)
for ang in range(0, 360, 30):
    c = math.radians(ang); cyl('black', (-0.6 + 0.24 * math.cos(c), -5.05, 1.2 + 0.24 * math.sin(c)), 0.035, 0.08, 'Y', 6)
box('black', (-0.6, -5.05, 1.2), (0.46, 0.05, 0.05)); box('black', (-0.6, -5.05, 1.2), (0.05, 0.05, 0.46)); cyl('metal', (-0.6, -5.08, 1.2), 0.07, 0.04, 'Y', 10)
# five rows of seats on both sides (facing the nose); gamma cells on the seats
rows = [-2.2, -0.6, 1.0, 2.6, 4.2]
for sx in (-1, 1):
    for yy in rows:
        box('seat', (sx * 1.0, yy, 0.45), (0.8, 0.8, 0.14), 0.04); box('seat', (sx * 1.0, yy + 0.38, 0.95), (0.8, 0.14, 0.85), 0.04); box('trim', (sx * 1.0, yy, 0.18), (0.7, 0.7, 0.36)); box('metal', (sx * 0.62, yy, 0.5), (0.04, 0.78, 0.05))
for sx, yy in ((-1, -2.2), (1, -2.2), (-1, 1.0), (1, 1.0), (-1, 4.2), (1, 4.2)):
    c = (sx * 1.0, yy, 0.52 + 0.3); sph('green', c, 0.28); cyl('black', (c[0], c[1], 0.57), 0.34, 0.06, 'Z', 14)
    for i in range(10):
        th = math.pi * (0.2 + 0.6 * i / 9.0); ph = i * 2.4; d = Vector((math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th))); cone('black', Vector(c) + d * 0.3, d, 0.05, 0.14)
# EMERGENCY sign on the left header, reading from the aisle (text built as curves then meshed)
bpy.ops.object.text_add(); t = bpy.context.active_object; t.data.body = 'EMERGENCY'; t.data.size = 0.3; t.data.extrude = 0.01; t.data.align_x = 'CENTER'; t.data.align_y = 'CENTER'; bpy.ops.object.convert(target='MESH'); tm = bpy.context.active_object
Rm = Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 0, 1))); Rm = Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))).to_4x4()
tm.data.transform(Matrix.Translation(Vector((-W + 0.07, 1.4, 2.28))) @ Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1))))
sb = parts['sign']; sb.from_mesh(tm.data); bpy.data.objects.remove(tm)
# build the objects
bpy.ops.object.select_all(action='DESELECT'); ob = None
me = bpy.data.meshes.new('GxBusInt'); allbm = bmesh.new()
for i, k in enumerate(KEYS):
    parts[k].verts.ensure_lookup_table()
    for f in parts[k].faces: f.material_index = i
    tmp = bpy.data.meshes.new('t' + k); parts[k].to_mesh(tmp); allbm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
allbm.to_mesh(me); allbm.free()
for k in KEYS: me.materials.append(M[k])
for f in me.polygons: pass
ob = bpy.data.objects.new('GxBusInt', me); bpy.context.collection.objects.link(ob)
# material_index was set on the faces before merging: re-assign by vertex ranges (from_mesh keeps it); check
print('BUSINT polys', len(me.polygons), 'mats', len(me.materials))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.glb', '.blend'))
print('EXPORTED', out)
