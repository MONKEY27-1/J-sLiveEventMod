"""blender -b -P make_site.py -- <out.glb>
The Vault site's set pieces (Fortnite Season 8 Loot Lake vault pit), each a named object standing on z = 0 with its origin at the centre of its base; the plugin clones them by name and places them on the floor:
  ContainerRed / ContainerBlue / ContainerGrey / ContainerOrange (ribbed shipping containers with doors, rust, stencils) | Bulldozer (tracks with cleats, hood, cab with glass, exhaust, blade on arms) | DumpTruck |
  DomeBuilding (teal container with a glass geodesic dome and a frame) | Boulder0..3 (faceted brown rock with strata; 1 and 3 carry teal crystals) | Pole (light pole) | Barrel | Crate | Cone.
Materials named A_* are alphaMode BLEND."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
M = {}
def mat(name, arr): M[name] = material(name, arr); return M[name]
mat('RedC', tex_container((0.72, 0.16, 0.12), 1)); mat('BlueC', tex_container((0.16, 0.34, 0.66), 2)); mat('GreyC', tex_container((0.55, 0.57, 0.6), 3)); mat('OrangeC', tex_container((0.88, 0.48, 0.1), 4))
mat('Yellow', tex_yellow(5)); mat('Rubber', tex_solid(0.08, 0.08, 0.09)); mat('Steel', tex_concrete(6, (0.45, 0.48, 0.52))); mat('Glass', tex_solid(0.12, 0.2, 0.3)); mat('Teal', tex_container((0.1, 0.55, 0.6), 7))
mat('A_DomeGlass', tex_glass(0.38)); mat('Frame', tex_solid(0.55, 0.95, 1.0)); mat('Rock', tex_rock(8)); mat('Crystal', tex_solid(0.35, 0.95, 0.95)); mat('Wood', tex_rock(9, (1.0, 0.85, 0.6))); mat('Orange', tex_solid(1.0, 0.45, 0.08)); mat('White', tex_solid(0.92, 0.92, 0.9)); mat('Lamp', tex_solid(1.0, 0.95, 0.7))
objs = []
def place(o, x): o.location = (x, 0, 0); objs.append(o)

def container(name, m, x):
    b = MB([M[m], M['Steel'], M['Rubber']]); L, W_, H = 6.06, 2.44, 2.59
    b.box((0, 0, H / 2), (L, W_, H), 0, tile=3.0, tile_v=1.3)
    for sx in (-1, 1):                                  # frame rails + corner posts
        b.box((0, sx * (W_ / 2 + 0.03), 0.08), (L + 0.1, 0.07, 0.16), 1, 2.0); b.box((0, sx * (W_ / 2 + 0.03), H - 0.08), (L + 0.1, 0.07, 0.16), 1, 2.0)
        for ex in (-1, 1): b.box((ex * (L / 2 + 0.02), sx * (W_ / 2 - 0.1), H / 2), (0.12, 0.2, H + 0.1), 1, 2.0)
    for k in range(2):                                 # the door end: two panels with locking bars
        yy = (-1 if k == 0 else 1) * W_ / 4; b.box((L / 2 + 0.05, yy, H / 2), (0.08, W_ / 2 - 0.12, H - 0.3), 0, 3.0, 1.3)
        for bar in range(2): b.box((L / 2 + 0.12, yy + (-1 if k == 0 else 1) * (0.15 + bar * 0.35) * 0.6, H / 2), (0.05, 0.05, H - 0.4), 1, 2.0)
        b.box((L / 2 + 0.14, yy, 1.1), (0.04, 0.3, 0.06), 1, 2.0)
    o = b.build(name); place(o, x); return o
for k, (n, m) in enumerate((('ContainerRed', 'RedC'), ('ContainerBlue', 'BlueC'), ('ContainerGrey', 'GreyC'), ('ContainerOrange', 'OrangeC'))): container(n, m, k * 8)

def bulldozer(x):
    b = MB([M['Yellow'], M['Rubber'], M['Glass'], M['Steel']])
    for s in (-1, 1):
        yy = s * 1.5; b.box((0, yy, 0.62), (5.0, 0.95, 1.1), 1, 2.0)
        for e in (-1, 1): b.cyl((e * 2.5, yy, 0.62), 0.55, 0.95, 1, 14, axis='y')
        for k in range(14): b.box((-2.4 + k * 0.37, yy + s * 0.02, 0.06), (0.18, 1.02, 0.12), 3, 1.0); b.box((-2.4 + k * 0.37, yy + s * 0.02, 1.2), (0.18, 1.02, 0.1), 3, 1.0)
        b.box((0, yy, 0.62), (4.4, 0.55, 0.5), 3, 2.0)
    b.box((-0.4, 0, 1.55), (3.8, 2.3, 1.1), 0, 3.0)                          # body
    b.box((1.9, 0, 1.45), (1.7, 1.9, 0.9), 0, 3.0)                           # hood
    b.cyl((2.5, 0.5, 2.3), 0.12, 0.9, 3, 10); b.box((2.9, 0, 1.4), (0.2, 1.6, 0.6), 3, 2.0)    # exhaust, grille
    b.box((-0.9, 0, 2.7), (2.0, 2.0, 1.6), 0, 3.0)                           # cab
    for s in (-1, 1): b.box((-0.9, s * 1.005, 2.8), (1.5, 0.02, 1.1), 2, 2.0)
    b.box((0.0, 0, 2.75), (0.02, 1.8, 1.0), 2, 2.0); b.box((-0.9, 0, 3.55), (2.3, 2.3, 0.18), 0, 3.0)   # windscreen, roof
    b.box((3.3, 0, 0.95), (0.35, 4.3, 1.5), 0, 3.0)                         # blade
    for s in (-1, 1): b.box((2.6, s * 1.9, 0.9), (1.4, 0.2, 0.25), 3, 2.0)  # push arms
    b.box((-2.5, 0, 1.0), (0.5, 1.4, 0.8), 3, 2.0)                           # ripper
    o = b.build('Bulldozer'); place(o, 5 * 8); return o
bulldozer(0)

def truck(x):
    b = MB([M['Orange'], M['Rubber'], M['Glass'], M['Steel'], M['Yellow']])
    b.box((-2.0, 0, 1.2), (6.0, 2.5, 0.35), 3, 2.0)                           # chassis
    b.box((2.0, 0, 2.0), (2.2, 2.4, 2.0), 0, 3.0); b.box((2.6, 0, 2.3), (0.8, 2.2, 1.0), 2, 2.0); b.box((2.0, 0, 3.15), (2.0, 2.3, 0.15), 0, 3.0)   # cab + windscreen + roof
    b.box((-2.0, 0, 2.3), (5.6, 2.8, 1.8), 4, 3.0)                            # bed
    for s in (-1, 1): b.box((-2.0, s * 1.45, 3.35), (5.8, 0.15, 0.4), 3, 2.0)
    for ax in (2.4, -1.2, -3.0):
        for s in (-1, 1): b.cyl((ax, s * 1.3, 0.65), 0.65, 0.5, 1, 16, axis='y')
    o = b.build('DumpTruck'); place(o, 6 * 8 + 4); return o
truck(0)

def dome(x):
    root = bpy.data.objects.new('DomeBuilding', None); bpy.context.collection.objects.link(root); root.location = (x, 0, 0)
    b = MB([M['Teal'], M['Steel'], M['Glass'], M['White']]); b.box((0, 0, 1.75), (11, 7, 3.5), 0, 3.0)
    for k in range(5): b.box((-4 + k * 2, 3.52, 1.9), (1.2, 0.05, 1.4), 2, 2.0)
    b.box((5.55, 0, 1.4), (0.1, 1.6, 2.4), 3, 2.0); b.box((0, -3.6, 3.55), (11.2, 0.3, 0.15), 1, 2.0); b.build('DomeBase', root)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0, calc_uvs=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.05], context='VERTS')
    for v in bm.verts: v.co = Vector((v.co.x * 5.4, v.co.y * 3.4, v.co.z * 3.6 + 3.5))
    me = bpy.data.meshes.new('DomeGlass'); bm.to_mesh(me); bm.free(); gl = bpy.data.objects.new('DomeGlass', me); bpy.context.collection.objects.link(gl); gl.parent = root; me.materials.append(M['A_DomeGlass'])
    fr = gl.copy(); fr.data = me.copy(); fr.name = 'DomeFrame'; bpy.context.collection.objects.link(fr); fr.parent = root; fr.data.materials.clear(); fr.data.materials.append(M['Frame'])
    w = fr.modifiers.new('wf', 'WIREFRAME'); w.thickness = 0.12; w.use_replace = True; bpy.context.view_layer.objects.active = fr; bpy.ops.object.modifier_apply(modifier='wf')
    objs.append(root)
dome(7 * 8 + 4)

def boulder(i, x, crystals):
    bm = displace_icosphere(2.2, 3, 30 + i * 7, 0.38, (1.0, 0.85, 0.7)); me = bpy.data.meshes.new('Boulder%d' % i); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    me.materials.append(M['Rock']); o = bpy.data.objects.new('Boulder%d' % i, me); bpy.context.collection.objects.link(o); o.location = (x, 0, 1.4 * (0.85)); o.scale = (1 + 0.3 * i % 1, 1, 1)
    me.uv_layers.new()
    for pl in me.polygons:                                      # box-project the uv by the dominant axis
        for li, vi in zip(pl.loop_indices, pl.vertices):
            c = me.vertices[vi].co; n = pl.normal; a = (abs(n.x), abs(n.y), abs(n.z)); me.uv_layers[0].data[li].uv = ((c.y, c.z) if a[0] >= max(a) else (c.x, c.z) if a[1] >= max(a) else (c.x, c.y))
    if crystals:
        for k in range(4):
            bc = MB([M['Crystal']]); ang = k * 1.7 + i; bc.cyl((0, 0, 0.5), 0.28, 1.1, 0, 6, r2=0.02); cb = bc.build('Crystal%d' % k, o); cb.location = (math.cos(ang) * 1.4, math.sin(ang) * 1.2, 1.3); cb.rotation_euler = (math.sin(ang) * 0.4, math.cos(ang) * 0.4, 0)
    objs.append(o)
for i in range(4): boulder(i, i * 7 + 60, i in (1, 3))
# drop the boulders onto z = 0: their lowest point sits 0 after the offset above; fix by lifting each by its own bound
def small():
    b = MB([M['Steel'], M['Lamp'], M['Yellow']]); b.cyl((0, 0, 3.0), 0.12, 6.0, 0, 8); b.box((0.5, 0, 6.0), (1.4, 0.35, 0.18), 0, 1.0); b.box((0.9, 0, 5.85), (0.5, 0.3, 0.1), 1, 1.0); place(b.build('Pole'), 100)
    b = MB([M['Yellow'], M['Rubber']]); b.cyl((0, 0, 0.45), 0.3, 0.9, 0, 12); b.cyl((0, 0, 0.3), 0.31, 0.05, 1, 12); b.cyl((0, 0, 0.6), 0.31, 0.05, 1, 12); place(b.build('Barrel'), 103)
    b = MB([M['Wood']]); b.box((0, 0, 0.6), (1.2, 1.2, 1.2), 0, 1.2); place(b.build('Crate'), 106)
    b = MB([M['Orange'], M['White']]); b.box((0, 0, 0.03), (0.5, 0.5, 0.06), 0, 1.0); b.cyl((0, 0, 0.33), 0.2, 0.55, 0, 10, r2=0.04); b.cyl((0, 0, 0.3), 0.14, 0.12, 1, 10); place(b.build('Cone'), 109)
small()
for o in objs: pass
bpy.context.view_layer.update()
# put every top level object's lowest point on z = 0
for o in objs:
    pts = [o.matrix_world @ v.co for ch in ([o] + list(o.children_recursive)) if ch.type == 'MESH' for v in ch.data.vertices]
    if pts: o.location.z -= min(p.z for p in pts)
export(out)
