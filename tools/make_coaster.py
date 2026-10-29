"""blender -b -P make_coaster.py -- <outdir>   -> coaster_track.glb, coaster_cart.glb, coaster_path.json
Geometry is authored in Unity-space numbers and converted to Blender space (bx,by,bz) = (-ux,-uz,uy) so that after the glTF exporter and the plugin's
X-mirror the pieces land exactly where the path says. Track: banked twin rails + spine + sleepers + supports + station posts. Cart: a 3-car train."""
import bpy, bmesh, math, json, sys, os
from mathutils import Vector
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)

def conv(v): return Vector((-v[0], -v[2], v[1]))
def path_point(a):
    r = 28 + 6 * math.sin(3 * a); y = 8 + 5.5 * math.sin(2 * a) + 2.2 * math.sin(5 * a)      # raised and more dramatic than the old ring; never below 1.5 m
    return Vector((math.sin(a) * r, y, math.cos(a) * r))

N = 480
P = [path_point(2 * math.pi * k / N) for k in range(N)]
T = [(P[(k + 1) % N] - P[k - 1]).normalized() for k in range(N)]
UP = []
for k in range(N):
    a = 2 * math.pi * k / N
    bank = 0.5 * math.sin(3 * a + 0.6)                                             # roll into the curves
    lat = Vector((0, 1, 0)).cross(T[k]).normalized()
    up = Vector((0, 1, 0)); up.rotate(__import__('mathutils').Quaternion(T[k], bank)); UP.append(up)
LAT = [UP[k].cross(T[k]).normalized() * -1 for k in range(N)]                     # points to the car's left in Unity space handedness-agnostic: used symmetrically

mats = {}
def mat(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']; b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Metallic'].default_value = 0.6; b.inputs['Roughness'].default_value = 0.35
    if emit > 0:
        b.inputs['Emission Color'].default_value = (*rgb, 1); b.inputs['Emission Strength'].default_value = emit
    mats[name] = m; return m
mat('rail', (1.0, 0.15, 0.55), 1.2); mat('spine', (0.12, 0.12, 0.2)); mat('tie', (0.85, 0.85, 0.95)); mat('support', (0.5, 0.55, 0.65)); mat('glow', (0.1, 0.9, 1.0), 2.0)
mat('car', (0.1, 0.85, 1.0), 0.8); mat('seat', (0.95, 0.9, 0.2)); mat('dark', (0.08, 0.08, 0.1)); mat('wheel', (0.7, 0.7, 0.75))

def new_obj(name, bm, mname):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob); ob.data.materials.append(mats[mname]); return ob

def tube(bm, pts, ups, radius, sides=8, closed=True):
    ring_prev = None; first = None; n = len(pts); rings = []
    for k in range(n):
        t = (pts[(k + 1) % n] - pts[k - 1]).normalized(); u = ups[k]; s = t.cross(u).normalized(); u2 = s.cross(t).normalized()
        rings.append([bm.verts.new(conv(pts[k] + (s * math.cos(2 * math.pi * j / sides) + u2 * math.sin(2 * math.pi * j / sides)) * radius)) for j in range(sides)])
    for k in range(n if closed else n - 1):
        a, b = rings[k], rings[(k + 1) % n]
        for j in range(sides): bm.faces.new((a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j]))

def box(bm, c, ax, ay, az, hx, hy, hz):
    vs = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1): vs.append(bm.verts.new(conv(c + ax * hx * sx + ay * hy * sy + az * hz * sz)))
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)): bm.faces.new([vs[i] for i in f])

def cyl(bm, base, top, r, sides=10):
    d = (top - base).normalized(); s = d.cross(Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 0, 1))).normalized(); u = d.cross(s).normalized()
    a = [bm.verts.new(conv(base + (s * math.cos(2 * math.pi * j / sides) + u * math.sin(2 * math.pi * j / sides)) * r)) for j in range(sides)]
    b = [bm.verts.new(conv(top + (s * math.cos(2 * math.pi * j / sides) + u * math.sin(2 * math.pi * j / sides)) * r)) for j in range(sides)]
    for j in range(sides): bm.faces.new((a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j]))
    bm.faces.new(a[::-1]); bm.faces.new(b)

# ---------------- track
rail_l = bmesh.new(); rail_r = bmesh.new(); spine = bmesh.new(); ties = bmesh.new(); sup = bmesh.new(); glow = bmesh.new()
tube(rail_l, [P[k] + LAT[k] * 0.85 for k in range(N)], UP, 0.13)
tube(rail_r, [P[k] - LAT[k] * 0.85 for k in range(N)], UP, 0.13)
tube(spine, [P[k] - UP[k] * 0.55 for k in range(N)], UP, 0.3, 10)
for k in range(0, N, 3):                                                          # sleepers + rail brackets every ~1.1 m
    box(ties, P[k] - UP[k] * 0.1, LAT[k], UP[k], T[k], 1.05, 0.07, 0.14)
    cyl(ties, P[k] - UP[k] * 0.5, P[k] - UP[k] * 0.1, 0.07, 6)
for k in range(0, N, 12):                                                         # supports from the ground (y = 0 is the arena floor)
    top = P[k] - UP[k] * 0.85; g = Vector((top.x, 0, top.z))
    if top.y > 1.5:
        cyl(sup, g, top, 0.28, 8); cyl(sup, g, g + Vector((0, 0.35, 0)), 0.7, 8)
        for sgn in (-1, 1): cyl(sup, g + Vector((sgn * 1.3, 0, 0)), top - Vector((0, top.y * 0.45, 0)), 0.1, 6)   # splayed braces
for k in range(0, N, 24): box(glow, P[k] + UP[k] * 0.35, LAT[k], UP[k], T[k], 0.05, 0.05, 0.05)
for nm, bm_, mn in (('rail_l', rail_l, 'rail'), ('rail_r', rail_r, 'rail'), ('spine', spine, 'spine'), ('ties', ties, 'tie'), ('supports', sup, 'support'), ('lamps', glow, 'glow')): new_obj(nm, bm_, mn)

# bounds (Unity space) so the plugin can undo the loader's re-centring on the model bounds
allv = [conv_v for ob in bpy.context.scene.objects for conv_v in [ob.data.vertices[i].co for i in range(len(ob.data.vertices))]]
mnv = Vector((min(v.x for v in allv), min(v.y for v in allv), min(v.z for v in allv))); mxv = Vector((max(v.x for v in allv), max(v.y for v in allv), max(v.z for v in allv)))
# blender (bx,by,bz) -> unity (-bx, bz, -by)
ux = (-mxv.x, -mnv.x); uy = (mnv.z, mxv.z); uz = (-mxv.y, -mnv.y)
origin = [(ux[0] + ux[1]) / 2, uy[0], (uz[0] + uz[1]) / 2]

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, name), export_format='GLB', use_selection=True, export_yup=True, export_apply=True)
export('coaster_track.glb')
for ob in list(bpy.context.scene.objects): bpy.data.objects.remove(ob, do_unlink=True)

# ---------------- cart: three cars along +Z (Unity), origin = bottom centre
def car(bm_body, bm_seat, bm_dark, bm_wheel, z0):
    c = Vector((0, 0, z0)); X = Vector((1, 0, 0)); Y = Vector((0, 1, 0)); Z = Vector((0, 0, 1))
    box(bm_body, c + Y * 0.62, X, Y, Z, 0.85, 0.32, 1.15)                          # tub
    box(bm_body, c + Y * 0.98, X, Y, Z, 0.78, 0.04, 1.1)                           # rim
    box(bm_body, c + Y * 0.75 + Z * 1.2, X, Y, Z, 0.7, 0.2, 0.18)                  # nose
    box(bm_dark, c + Y * 0.3, X, Y, Z, 0.55, 0.12, 1.0)                            # chassis
    for dz in (-0.55, 0.55): box(bm_seat, c + Y * 1.0 + Z * dz, X, Y, Z, 0.62, 0.1, 0.28); box(bm_seat, c + Y * 1.28 + Z * (dz - 0.2), X, Y, Z, 0.6, 0.28, 0.06)
    for sx in (-1, 1):
        for dz in (-0.7, 0.7):
            cyl(bm_wheel, c + Vector((sx * 0.9, 0.2, dz)), c + Vector((sx * 1.0, 0.2, dz)), 0.2, 10)
body = bmesh.new(); seat = bmesh.new(); dark = bmesh.new(); wheel = bmesh.new()
for z0 in (-2.7, 0.0, 2.7): car(body, seat, dark, wheel, z0)
for z0 in (-1.35, 1.35): box(dark, Vector((0, 0.45, z0)), Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), 0.08, 0.08, 0.2)     # couplers
for nm, bm_, mn in (('car', body, 'car'), ('seats', seat, 'seat'), ('chassis', dark, 'dark'), ('wheels', wheel, 'wheel')): new_obj(nm, bm_, mn)
export('coaster_cart.glb')

json.dump({'points': [[round(p.x, 3), round(p.y, 3), round(p.z, 3)] for p in P], 'up': [[round(u.x, 4), round(u.y, 4), round(u.z, 4)] for u in UP],
           'origin': [round(x, 3) for x in origin], 'count': N}, open(os.path.join(out, 'coaster_path.json'), 'w'))
print('coaster ok', origin, len(P))
