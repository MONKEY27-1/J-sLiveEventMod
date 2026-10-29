"""Shared helpers for the Blender prop scripts (make_zeropoint.py / make_blackhole.py / make_vault.py): procedural textures, materials, meshes, glb export with forced alphaMode."""
import bpy, bmesh, math, json, struct
import numpy as np
from mathutils import Matrix

def smooth(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def rgba(r, g, b, a):
    sh = np.broadcast(r, g, b, a).shape; return np.stack([np.broadcast_to(c, sh) for c in (r, g, b, a)], -1).astype(np.float32)
def grid(W, H):
    u = (np.arange(W) + 0.5) / W; v = (np.arange(H) + 0.5) / H; return np.meshgrid(u, v)      # arrays [H, W]; rows = v
def ramp(v, stops):
    xs = [s[0] for s in stops]; return [np.interp(v, xs, [s[1][c] for s in stops]) for c in range(3)]
def periodic_noise(W, H, cu, cv, seed):
    r = np.random.default_rng(seed); g = r.random((cv + 1, cu)).astype(np.float32); g[-1] = g[0]; g = np.concatenate([g, g[:, :1]], 1)
    xs = np.linspace(0, cu, W, endpoint=False); ys = np.linspace(0, cv, H, endpoint=False); xi = xs.astype(int); yi = ys.astype(int); fx = xs - xi; fy = ys - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[yi][:, xi]; b = g[yi][:, xi + 1]; c = g[yi + 1][:, xi]; d = g[yi + 1][:, xi + 1]
    return a * (1 - fx)[None] * (1 - fy)[:, None] + b * fx[None] * (1 - fy)[:, None] + c * (1 - fx)[None] * fy[:, None] + d * fx[None] * fy[:, None]
def fbm(W, H, base, seed, octv=5):
    acc = np.zeros((H, W), np.float32); amp = 1.0; tot = 0.0
    for k in range(octv): acc += periodic_noise(W, H, base * 2 ** k * 2, base * 2 ** k, seed + k) * amp; tot += amp; amp *= 0.5
    return acc / tot
def voronoi_edges(W, H, n, seed, warp=0.0):
    """distance to the nearest cell border (d2 - d1), periodic in u (u spans 2x v so cells are round on a sphere); warp > 0 makes the borders jagged like lightning"""
    r = np.random.default_rng(seed); pts = np.stack([r.random(n) * 2, r.random(n)], 1)
    P = np.concatenate([pts + [-2, 0], pts, pts + [2, 0]]); xs = (np.arange(W) + 0.5) / W * 2; ys = (np.arange(H) + 0.5) / H; X, Y = np.meshgrid(xs, ys)
    if warp > 0: X = X + (fbm(W, H, 6, seed + 100, 4) - 0.5) * warp * 2; Y = Y + (fbm(W, H, 6, seed + 200, 4) - 0.5) * warp
    d1 = np.full((H, W), 9.0, np.float32); d2 = np.full((H, W), 9.0, np.float32)
    for px, py in P:
        d = np.sqrt((X - px) ** 2 + (Y - py) ** 2).astype(np.float32); m = d < d1; d2 = np.where(m, d1, np.minimum(d2, d)); d1 = np.where(m, d, d1)
    return d2 - d1
def make_image(name, arr):
    H, W = arr.shape[:2]; im = bpy.data.images.new(name, W, H, alpha=True); im.pixels = np.clip(arr, 0, 1).astype(np.float32).flatten().tolist(); im.pack(); return im
def material(name, arr):
    im = make_image(name + 'Tex', arr); m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = im; nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    if name.startswith('A_'): nt.links.new(t.outputs['Alpha'], b.inputs['Alpha'])
    return m
def solid(name, r, g, b, a=1.0): return material(name, np.zeros((4, 4, 4), np.float32) + np.array([r, g, b, a], np.float32))
def new_obj(name, mesh, mat, parent=None):
    o = bpy.data.objects.new(name, mesh); bpy.context.collection.objects.link(o); o.data.materials.append(mat); o.parent = parent; return o
def empty(name, parent=None, loc=(0, 0, 0), rot=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(e); e.parent = parent; e.location = loc; e.rotation_euler = rot; return e
def sphere(r, seg=48, rings=24):
    bm = bmesh.new(); bm.loops.layers.uv.verify(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r, calc_uvs=True); me = bpy.data.meshes.new('sph'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    return me
def annulus(r0, r1, seg=96, a0=0.0, a1=2 * math.pi, xz=False, z=0.0):
    """flat ring in the XY plane (xz=True: stood up into the XZ plane, facing Blender -Y = the plugin's +Z); uv: u = angle / 2pi, v = (r - r0) / (r1 - r0)"""
    me = bpy.data.meshes.new('ann'); vs = []; fs = []
    for k in range(seg + 1):
        a = a0 + (a1 - a0) * k / seg; c, s = math.cos(a), math.sin(a); vs += [(c * r0, s * r0, z), (c * r1, s * r1, z)]
    for k in range(seg): i = k * 2; fs.append((i, i + 1, i + 3, i + 2))
    me.from_pydata(vs, [], fs); uv = me.uv_layers.new()
    for pi, p in enumerate(me.polygons):
        ua = (a0 + (a1 - a0) * pi / seg) / (2 * math.pi); ub = (a0 + (a1 - a0) * (pi + 1) / seg) / (2 * math.pi)
        for li, (u, v) in zip(p.loop_indices, [(ua, 0), (ua, 1), (ub, 1), (ub, 0)]): uv.data[li].uv = (u, 0.03 + 0.94 * v)
    if xz: me.transform(Matrix.Rotation(math.pi / 2, 4, 'X'))
    return me
def keys(o, prop, fn, fps=24, dur=24, step=6):
    for i in range(int(fps * dur / step) + 1):
        f = i * step; setattr(o, prop, fn(f / fps)); o.keyframe_insert(prop, frame=f)
def setup(fps=24, dur=24):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
    sc = bpy.context.scene; sc.render.fps = fps; sc.frame_start = 0; sc.frame_end = fps * dur
def read_glb(path):
    d = open(path, 'rb').read(); l, t = struct.unpack_from('<II', d, 12); js = json.loads(d[20:20 + l]); o = 20 + l; bl, bt = struct.unpack_from('<II', d, o); return js, d[o + 8:o + 8 + bl]
def write_glb(path, js, b):
    j = json.dumps(js, separators=(',', ':')).encode(); j += b' ' * ((4 - len(j) % 4) % 4); b = b + b'\0' * ((4 - len(b) % 4) % 4)
    open(path, 'wb').write(struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(j) + 8 + len(b)) + struct.pack('<II', len(j), 0x4E4F534A) + j + struct.pack('<II', len(b), 0x004E4942) + b)
def export(out):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=True, export_animation_mode='ACTIVE_ACTIONS', export_force_sampling=True, export_image_format='AUTO')
    js, b = read_glb(out)
    for m in js['materials']:
        if m['name'].startswith('A_'): m['alphaMode'] = 'BLEND'
        m['doubleSided'] = True
    write_glb(out, js, b); print('wrote', out, 'nodes', len(js['nodes']), 'anims', [(a['name'], len(a['channels'])) for a in js.get('animations', [])], [n['name'] for n in js['nodes']][:40])
