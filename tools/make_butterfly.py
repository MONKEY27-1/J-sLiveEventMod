"""blender -b -P make_butterfly.py -- <butterfly|inbetween> <out.glb>
butterfly: the Butterfly Event's rift butterfly (about 2 m wingspan, nose toward Blender -Y which is glTF +Z): a slim dark body and antennae, four see-through iridescent shard-patterned wings (WingL / WingR, each a fore and a hind wing, origin on the body axis) with a looping flap clip 'Flap' (2 flaps in 1 s).
inbetween: the In-Between: an inward-facing pale grey sphere (80 m radius, soft gradient) with ~90 floating white orbs of every size inside it (floor and sky are added by the plugin)."""
import bpy, sys, math, os, bmesh
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
kind, out = sys.argv[sys.argv.index('--') + 1:][:2]; setup(24, 1); rnd = np.random.default_rng(6)
def wing_tex():
    W = H = 256; U, V = grid(W, H); pts = rnd.random((22, 2)); d = np.stack([np.hypot(U - px, (V - py) * 0.8) for px, py in pts], 0); ds = np.sort(d, 0); cid = np.argmin(d, 0); edge = np.exp(-((ds[1] - ds[0]) / 0.018) ** 2)
    shade = (rnd.random(22)[cid] * 0.5 + 0.5); base = np.stack([0.35 + 0.35 * U + 0.1 * shade, 0.78 - 0.2 * U + 0.15 * shade, np.ones_like(U)], -1); glow = np.exp(-((U) / 0.35) ** 2)[..., None]
    col = np.clip(base * (0.7 + 0.3 * shade[..., None]) + edge[..., None] * 0.55 + glow * 0.45, 0, 1); alpha = np.clip(0.5 + 0.35 * shade + 0.5 * edge + 0.4 * glow[..., 0], 0, 1); return rgba(col[..., 0], col[..., 1], col[..., 2], alpha)
if kind == 'butterfly':
    WING = material('A_Wing', wing_tex()); BODY = solid('Body', 0.75, 0.95, 1.0)
    def poly(pts, sign, name):
        cx = sum(p[0] for p in pts) / len(pts); cy = sum(p[1] for p in pts) / len(pts); verts = [(sign * cx, cy, 0.1 * cx * cx)] + [(sign * x, y, 0.1 * x * x) for x, y in pts]; faces = []; uv = []
        for k in range(len(pts)):
            a, b = 1 + k, 1 + (k + 1) % len(pts); faces.append((0, a, b) if sign > 0 else (0, b, a))
        me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); l = me.uv_layers.new()
        for pi, p in enumerate(me.polygons):
            for li, vi in zip(p.loop_indices, p.vertices): x, y = verts[vi][0] * sign, verts[vi][1]; l.data[li].uv = (min(1, max(0, x / 1.0)), min(1, max(0, (y + 1.0) / 1.9)))
        me.materials.append(WING); return me
    fore = [(0.04, -0.08), (0.26, -0.62), (0.6, -1.0), (1.05, -0.95), (0.92, -0.46), (0.72, -0.14), (0.45, 0.02), (0.1, 0.06)]; hind = [(0.05, 0.0), (0.5, 0.02), (0.9, 0.22), (0.62, 0.52), (0.3, 0.8), (0.12, 0.34)]
    body = MB([BODY]); body.cyl((0, 0.0, 0.0), 0.05, 0.75, 0, 8, axis='y', r2=0.04); body.cyl((0, -0.42, 0.0), 0.06, 0.12, 0, 8, axis='y', r2=0.05)
    for s_ in (-1, 1): body.cyl((s_ * 0.1, -0.6, 0.12), 0.008, 0.38, 0, 5, axis='y')
    bo = body.build('Butterfly'); wings = []
    for side, sign in (('L', 1), ('R', -1)):
        o = bpy.data.objects.new('Wing' + side, poly(fore, sign, 'fw' + side)); bpy.context.collection.objects.link(o); o.parent = bo
        o2 = bpy.data.objects.new('Wing' + side + 'H', poly(hind, sign, 'hw' + side)); bpy.context.collection.objects.link(o2); o2.parent = o; wings.append((o, sign))
    for o, sign in wings: keys(o, 'rotation_euler', lambda t, s_=sign: (0.0, -s_ * (0.15 + 0.95 * (0.5 + 0.5 * math.sin(2 * math.pi * t * 2.0))), 0.0), 24, 1, 1)
    export(out)
else:
    bpy.ops.mesh.primitive_uv_sphere_add(radius=80, segments=48, ring_count=24); sh = bpy.context.active_object; sh.name = 'Shell'
    bm = bmesh.new(); bm.from_mesh(sh.data); bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(sh.data); bm.free()
    H = 64; V = np.linspace(0, 1, H)[:, None] * np.ones((1, 4)); g = 0.78 + 0.2 * np.exp(-((V - 0.5) / 0.22) ** 2) + 0.03 * np.sin(V * 40); SHELL = material('Shell', rgba(g * 0.97, g * 0.98, g * 1.0, np.ones_like(g))); sh.data.materials.append(SHELL)
    ORB = solid('Orb', 1.0, 1.0, 1.0); parts = [sh]
    for k in range(90):
        r = float(np.exp(rnd.uniform(math.log(0.4), math.log(3.2)))); a = rnd.uniform(0, 2 * math.pi); d = rnd.uniform(14, 62); z = rnd.uniform(1.5, 45)
        bpy.ops.mesh.primitive_ico_sphere_add(radius=r, subdivisions=2, location=(math.cos(a) * d, math.sin(a) * d, z)); o = bpy.context.active_object; o.data.materials.append(ORB); o.name = 'Orb%d' % k
        for p in o.data.polygons: p.use_smooth = True
        parts.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for p_ in parts: p_.select_set(True)
    bpy.context.view_layer.objects.active = sh; bpy.ops.object.join(); sh.name = 'InBetween'; export(out)
