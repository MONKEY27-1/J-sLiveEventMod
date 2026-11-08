"""blender -b -P gx_office2.py -- out.glb      THE I.O. OFFICE (9:10 - 9:19.7): 14 x 12 m room, red emergency lighting, executive desk + lamp + chair, ringing phone, monitors / papers, filing cabinets, wall displays, door, scattered documents.
Game axes: x right, z forward (Blender -y), floor y = 0 (Blender z).  The player stands at z = +1 facing +z; Jones lies at (0.8, 0.6).  Origin = middle of the floor."""
import bpy, bmesh, math, sys
from mathutils import Vector, Matrix
a = sys.argv[sys.argv.index('--') + 1:]; out = a[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name, rgb, rough=0.7):
    m = bpy.data.materials.new(name); m.use_nodes = True; bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); bs.inputs['Roughness'].default_value = rough; return m
M = {'floor': mat('GxOffFloor', (0.16, 0.13, 0.14)), 'wall': mat('GxOffWall', (0.34, 0.27, 0.25)), 'panel': mat('GxOffPanel', (0.22, 0.15, 0.12)), 'wood': mat('GxOffWood', (0.30, 0.16, 0.08)), 'metal': mat('GxOffMetal', (0.42, 0.44, 0.48)), 'dark': mat('GxOffDark', (0.05, 0.05, 0.06)),
     'paper': mat('GxOffPaper', (0.85, 0.85, 0.8)), 'chair': mat('GxOffChair', (0.07, 0.07, 0.09)), 'ceil': mat('GxOffCeil', (0.2, 0.17, 0.17)),
     'red': mat('SF_G_GxOffRed', (0.95, 0.07, 0.05)), 'screen': mat('SF_G_GxOffScreen', (0.45, 0.7, 1.0)), 'lamp': mat('SF_G_GxOffLamp', (1.0, 0.82, 0.5)), 'logo': mat('SF_G_GxOffLogo', (0.9, 0.2, 0.15)), 'exit': mat('SF_G_GxOffExit', (0.1, 0.75, 0.2)), 'day': mat('SF_G_GxOffDay', (0.75, 0.85, 1.0)), 'light': mat('SF_G_GxOffLight', (0.85, 0.9, 1.0))}
KEYS = list(M); P = {k: bmesh.new() for k in KEYS}
def box(k, c, sz, bevel=0.0, rotz=0.0):
    bm = P[k]; r = bmesh.ops.create_cube(bm, size=1.0); vs = r['verts']; bmesh.ops.scale(bm, vec=Vector(sz), verts=vs)
    if rotz: bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(rotz, 3, 'Z'), verts=vs)
    bmesh.ops.translate(bm, vec=Vector(c), verts=vs)
    if bevel > 0: bmesh.ops.bevel(bm, geom=list({e for v in vs for e in v.link_edges}), offset=bevel, segments=2, profile=0.6, affect='EDGES')
def cyl(k, c, r, h, seg=14):
    bm = P[k]; rr = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=h); bmesh.ops.translate(bm, vec=Vector(c), verts=rr['verts'])
def sph(k, c, r):
    bm = P[k]; rr = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=r); bmesh.ops.translate(bm, vec=Vector(c), verts=rr['verts'])
def G(x, z, y):    # game (x, z, height) -> Blender (x, -z, height)
    return (x, -z, y)
W, D, H = 7.0, 6.0, 3.4
box('floor', G(0, 0, -0.1), (2 * W + 0.4, 2 * D + 0.4, 0.2)); box('ceil', G(0, 0, H + 0.1), (2 * W + 0.4, 2 * D + 0.4, 0.2))
box('wall', G(-(W + 0.1), 0, H / 2), (0.2, 2 * D + 0.4, H)); box('panel', G(-(W - 0.03), 0, 0.55), (0.08, 2 * D, 1.1))
# THE DOOR TO THE OUTSIDE: an open doorway in the right wall (z 0.7 .. 2.3), the door leaf swung into the room, a green EXIT sign, and a short bright corridor leading out
d0, d1 = 0.7, 2.3
for (z0, z1) in ((-D - 0.2, d0), (d1, D + 0.2)): box('wall', G(W + 0.1, (z0 + z1) / 2, H / 2), (0.2, z1 - z0, H)); box('panel', G(W - 0.03, (z0 + z1) / 2, 0.55), (0.08, z1 - z0, 1.1))
box('wall', G(W + 0.1, (d0 + d1) / 2, (2.4 + H) / 2), (0.2, d1 - d0, H - 2.4)); box('dark', G(W - 0.04, d0, 1.2), (0.12, 0.1, 2.4)); box('dark', G(W - 0.04, d1, 1.2), (0.12, 0.1, 2.4)); box('dark', G(W - 0.04, (d0 + d1) / 2, 2.42), (0.12, d1 - d0 + 0.1, 0.08))
box('chair', G(W - 0.85, d0 + 0.05, 1.15), (1.5, 0.08, 2.3), 0.02); box('metal', G(W - 0.2, d0 + 0.1, 1.05), (0.08, 0.1, 0.1)); box('exit', G(W - 0.12, (d0 + d1) / 2, 2.75), (0.1, 0.8, 0.28), 0.02)
box('floor', G(W + 2.7, (d0 + d1) / 2, -0.1), (5.4, 1.9, 0.2)); box('ceil', G(W + 2.7, (d0 + d1) / 2, 2.6), (5.4, 1.9, 0.2))
for zz in (d0 - 0.1, d1 + 0.1): box('wall', G(W + 2.7, zz, 1.3), (5.4, 0.1, 2.6))
box('day', G(W + 5.4, (d0 + d1) / 2, 1.3), (0.1, 1.9, 2.6)); box('light', G(W + 1.2, (d0 + d1) / 2, 2.48), (0.9, 0.3, 0.04)); box('light', G(W + 3.2, (d0 + d1) / 2, 2.48), (0.9, 0.3, 0.04))
for sz in (-1, 1): box('wall', G(0, sz * (D + 0.1), H / 2), (2 * W + 0.4, 0.2, H)); box('panel', G(0, sz * (D - 0.03), 0.55), (2 * W, 0.08, 1.1))
box('dark', G(0, 1.0, 0.01), (4.2, 5.0, 0.02))
for z in (-4.0, -1.3, 1.4, 4.1): box('red', G(0, z, H - 0.04), (10.0, 0.18, 0.05))
for sx in (-1, 1):
    box('red', G(sx * (W - 0.12), 0, H - 0.3), (0.06, 10.0, 0.14)); sph('red', G(sx * (W - 0.5), -D + 0.5, H - 0.5), 0.25); sph('red', G(sx * (W - 0.5), D - 0.5, H - 0.5), 0.25)
dz = 3.6
box('wood', G(0, dz, 0.4), (3.2, 1.4, 0.8), 0.04); box('wood', G(0, dz, 0.84), (3.4, 1.55, 0.08), 0.03); box('dark', G(-1.3, dz - 0.1, 0.4), (0.5, 1.1, 0.7)); box('dark', G(1.3, dz - 0.1, 0.4), (0.5, 1.1, 0.7))
cyl('metal', G(-1.2, dz + 0.4, 0.92), 0.12, 0.04); cyl('metal', G(-1.2, dz + 0.4, 1.12), 0.015, 0.4, 6); box('lamp', G(-1.2, dz + 0.32, 1.38), (0.34, 0.2, 0.12), 0.03)
box('dark', G(0.6, dz + 0.35, 1.2), (0.9, 0.06, 0.5), 0.02); box('screen', G(0.6, dz + 0.31, 1.2), (0.8, 0.02, 0.42)); box('dark', G(0.6, dz + 0.35, 0.95), (0.14, 0.14, 0.12))
box('dark', G(-0.3, dz - 0.2, 0.92), (0.34, 0.28, 0.08), 0.02); box('dark', G(-0.3, dz - 0.15, 0.99), (0.34, 0.1, 0.06), 0.02); box('red', G(-0.18, dz - 0.3, 0.97), (0.05, 0.05, 0.02))
for i, (px, pz, r) in enumerate([(0.2, dz - 0.3, 0.2), (0.45, dz - 0.15, -0.3), (-0.7, dz - 0.35, 0.5), (1.1, dz - 0.4, 0.1)]): box('paper', G(px, pz, 0.89 + 0.012 * i), (0.3, 0.42, 0.012), 0.0, r)
box('chair', G(0, dz - 1.6, 0.5), (0.8, 0.8, 0.12), 0.04); box('chair', G(0, dz - 2.05, 1.0), (0.8, 0.12, 0.9), 0.05); box('dark', G(0, dz - 1.6, 0.25), (0.1, 0.1, 0.5)); cyl('dark', G(0, dz - 1.6, 0.03), 0.4, 0.06)
for i in range(4):
    z = -4.2 + i * 1.0; box('metal', G(W - 0.5, z, 0.7), (0.9, 0.8, 1.4), 0.02)
    for d in range(3): box('dark', G(W - 0.96, z, 0.25 + d * 0.45), (0.02, 0.6, 0.06)); box('dark', G(W - 0.97, z, 0.4 + d * 0.45), (0.02, 0.12, 0.03))
for i, zz in enumerate((-3.5, -1.5, 0.5)): box('dark', G(-W + 0.12, zz, 1.9), (0.1, 1.5, 0.95), 0.02); box('screen', G(-W + 0.18, zz, 1.9), (0.02, 1.35, 0.8))
box('dark', G(0, D - 0.12, 2.5), (2.6, 0.1, 0.5), 0.02); box('logo', G(0, D - 0.18, 2.5), (2.2, 0.02, 0.3))
box('dark', G(-4.5, -D + 0.12, 1.1), (1.2, 0.1, 2.2), 0.02); box('metal', G(-4.1, -D + 0.2, 1.05), (0.1, 0.06, 0.1)); box('red', G(-4.5, -D + 0.17, 2.3), (1.0, 0.02, 0.08))
box('wood', G(5.2, 4.8, 0.3), (1.0, 1.0, 0.6), 0.03); cyl('dark', G(5.2, 4.8, 0.7), 0.2, 0.2)
for i, (px, pz, r) in enumerate([(2.0, 0.5, 0.4), (-1.6, 1.4, -0.7), (0.0, -0.8, 0.2), (1.5, 2.2, 1.1), (-0.9, -1.6, 0.9), (2.6, -0.9, 0.0)]): box('paper', G(px, pz, 0.025 + 0.002 * i), (0.32, 0.44, 0.01), 0.0, r)
me = bpy.data.meshes.new('GxOffice'); allbm = bmesh.new()
for i, k in enumerate(KEYS):
    for f in P[k].faces: f.material_index = i
    tmp = bpy.data.meshes.new('t' + k); P[k].to_mesh(tmp); allbm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
allbm.to_mesh(me); allbm.free()
for k in KEYS: me.materials.append(M[k])
ob = bpy.data.objects.new('Office', me); bpy.context.collection.objects.link(ob)
print('OFFICE polys', len(me.polygons))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.glb', '.blend'))
print('EXPORTED', out)
