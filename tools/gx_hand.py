"""gx_hand.py - an articulated armoured hand for Galactus (palm + 4 fingers x 3 segments + thumb x 3), built with bmesh in any Blender session.
build_hand(side, origin, ydir, mid, length) -> dict(objs=[(obj, bone_name)], bones=[(name, parent, head, tail)])   (all in the caller's armature space)
The hand is modelled in a canonical frame (a LEFT hand, palm down: fingers +Y, dorsal +Z, thumb +X) and mapped so that the fingers follow `ydir`, the palm faces `mid` (towards the body's midline) and the thumb falls out of the right-hand rule.
Bones: Wrist (the caller's) > Idx/Mid/Rng/Pnk + Thm, segments 1..3 ("IdxL1"); all rigid segments (armoured plates), so every segment is 100 % weighted to its own bone."""
import bpy, bmesh, math, mathutils
from mathutils import Vector, Matrix
def _mat(name, rgb, glow=False):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); bs.inputs['Roughness'].default_value = 0.5; return m
def _box(bm, c, sz, bevel=0.05, mat=0):
    r = bmesh.ops.create_cube(bm, size=1.0); vs = r['verts']; bmesh.ops.scale(bm, vec=Vector(sz), verts=vs); bmesh.ops.translate(bm, vec=Vector(c), verts=vs)
    if bevel > 0:
        es = list({e for v in vs for e in v.link_edges}); bmesh.ops.bevel(bm, geom=es, offset=bevel, segments=2, profile=0.6, affect='EDGES')
    return vs
def _tilt(bm, verts, origin, axis_angle):
    bmesh.ops.rotate(bm, cent=Vector(origin), matrix=Matrix.Rotation(axis_angle[1], 3, axis_angle[0]), verts=verts)
FING = {'Idx': (0.78, (1.0, 0.8, 0.66), 0.52, 4.0), 'Mid': (0.26, (1.12, 0.9, 0.7), 0.52, 0.0), 'Rng': (-0.26, (1.0, 0.85, 0.66), 0.5, -4.0), 'Pnk': (-0.76, (0.8, 0.66, 0.54), 0.46, -9.0)}
PALM_L = 2.1
def build_hand(side, origin, ydir, mid, length=4.85, name_prefix=''):
    s = 'L' if side > 0 else 'R'; y = Vector(ydir).normalized(); mid = Vector(mid); p = (mid - y * mid.dot(y)).normalized()      # palm normal (out of the palm)
    z = -p; x = y.cross(z).normalized(); k = length / 4.85
    # canonical -> armature: columns x, y, z (a right hand is the left hand mirrored in X before mapping)
    M = Matrix(((x.x, y.x, z.x, 0), (x.y, y.y, z.y, 0), (x.z, y.z, z.z, 0), (0, 0, 0, 1))) @ Matrix.Diagonal((k * 1.3, k, k * 1.4, 1)); M.translation = Vector(origin)
    mx = 1.0 if side > 0 else -1.0
    dark, armor, red = _mat('GxHandDark', (0.07, 0.065, 0.085)), _mat('GxHandArmor', (0.2, 0.19, 0.235)), _mat('SF_G_GxHandRed', (0.5, 0.04, 0.03))
    out = {'objs': [], 'bones': [], 'z': z.copy()}
    def mk(nm, bone, build):
        bm = bmesh.new(); build(bm)
        for v in bm.verts: v.co.x *= mx
        if mx < 0: bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
        me = bpy.data.meshes.new(nm); me.materials.append(dark); me.materials.append(armor); me.materials.append(red); bm.to_mesh(me); bm.free()
        ob = bpy.data.objects.new(nm, me); bpy.context.collection.objects.link(ob); me.transform(M); out['objs'].append((ob, bone))
    def facemat(bm, idx, pred):
        for f in bm.faces:
            if pred(f.calc_center_median()): f.material_index = idx
    # palm + the armoured back of the hand with a neon strip
    def palm(bm):
        _box(bm, (0, PALM_L / 2, 0), (2.25, PALM_L, 0.9), 0.14); _box(bm, (0, PALM_L / 2 + 0.05, 0.5), (1.95, PALM_L - 0.25, 0.3), 0.08); _box(bm, (0, PALM_L / 2 + 0.05, 0.68), (0.14, PALM_L - 0.5, 0.05), 0.0)
        facemat(bm, 1, lambda c: c.z > 0.4 and c.z < 0.66); facemat(bm, 2, lambda c: c.z >= 0.66)
    mk(name_prefix + 'HandPalm' + s, 'Wrist' + s, palm)
    out['palm_origin'] = Vector(origin)
    def seg_geo(bm, c, sz, tip=False):
        _box(bm, c, sz, 0.07); _box(bm, (c[0], c[1], c[2] + sz[2] / 2 + 0.0), (sz[0] * 0.45, sz[1] * 0.85, 0.06), 0.0)
        facemat(bm, 1, lambda q: q.z > c[2] + sz[2] / 2 - 0.02); facemat(bm, 2, lambda q: q.z > c[2] + sz[2] / 2 + 0.01)
        if tip:
            r = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=sz[0] * 0.52, radius2=0.04, depth=0.34)
            bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, 'X'), verts=r['verts']); bmesh.ops.translate(bm, vec=Vector((c[0], c[1] + sz[1] / 2 + 0.12, c[2] - 0.02)), verts=r['verts'])
    def chain(prefix, base, dirv, lens, wid, thick):
        pos = Vector(base); d = Vector(dirv).normalized(); par = 'Wrist' + s; names = []
        for i, L in enumerate(lens):
            head = pos.copy(); tail = pos + d * L; nm = '%s%s%d' % (prefix, s, i + 1); out['bones'].append((nm, par, M @ head, M @ tail)); names.append(nm)
            # a box along d: build along +Y then rotate to d
            def b(bm, i=i, L=L, head=head, d=d):
                bm.verts.ensure_lookup_table(); n0 = len(bm.verts); seg_geo(bm, (0, L / 2, 0), (wid * (1 - 0.06 * i), L * 0.94, thick * (1 - 0.06 * i)), tip=(i == len(lens) - 1)); bm.verts.ensure_lookup_table()
                R = Vector((0, 1, 0)).rotation_difference(d).to_matrix().to_4x4(); bmesh.ops.transform(bm, matrix=Matrix.Translation(head) @ R, verts=list(bm.verts)[n0:])
            mk(name_prefix + nm, nm, b); pos = tail; par = nm
    for pre, (fx, lens, wid, spread) in FING.items():
        a = math.radians(spread); chain(pre, (fx, PALM_L - 0.1, 0.0), (math.sin(a), math.cos(a), 0.0), lens, wid, 0.5)
    ta = math.radians(28); chain('Thm', (1.12, 0.45, -0.12), (math.sin(ta), math.cos(ta), -0.12), (0.95, 0.8, 0.66), 0.58, 0.55)
    return out
