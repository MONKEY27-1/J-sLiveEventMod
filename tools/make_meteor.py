"""blender -b -P make_meteor.py -- <in.stl> <out.glb>   STL meteor (Thingiverse 6823866 by DeltaX_F, CC BY-NC-SA) -> decimated, UV-unwrapped, procedurally textured (dark rock, glowing blue-white cracks) glb,
scaled so the longest side is 1.0 and sitting on y = 0 (the plugin loader re-centres / scales it)."""
import bpy, sys, math
import numpy as np
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.stl_import(filepath=src)
o = bpy.context.selected_objects[0]; bpy.context.view_layer.objects.active = o
bpy.ops.object.shade_smooth()
m = o.modifiers.new('dec', 'DECIMATE'); m.ratio = min(1.0, 9000.0 / max(1, len(o.data.polygons)))
bpy.ops.object.modifier_apply(modifier='dec')
d = max(o.dimensions); o.scale = (1 / d,) * 3; bpy.ops.object.transform_apply(scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.01); bpy.ops.object.mode_set(mode='OBJECT')
print('tris', len(o.data.polygons), 'dims', tuple(o.dimensions))
# procedural texture: value noise rock + thin glowing cracks (ridges of a second noise)
N = 1024; rng = np.random.default_rng(7)
def noise(res, oct_=5):
    acc = np.zeros((N, N), np.float32); amp = 1.0; tot = 0.0
    for k in range(oct_):
        r = res * 2 ** k; g = rng.random((r + 1, r + 1)).astype(np.float32); g[-1] = g[0]; g[:, -1] = g[:, 0]
        xs = np.linspace(0, r, N, endpoint=False); i = xs.astype(int); f = xs - i; f = f * f * (3 - 2 * f)
        a = g[i][:, i] * (1 - f)[None, :] * (1 - f)[:, None] + g[i][:, i + 1] * f[None, :] * (1 - f)[:, None] + g[i + 1][:, i] * (1 - f)[None, :] * f[:, None] + g[i + 1][:, i + 1] * f[None, :] * f[:, None]
        acc += a * amp; tot += amp; amp *= 0.5
    return acc / tot
rock = noise(6); fine = noise(40, 3); ridge = 1 - np.abs(noise(5, 4) * 2 - 1); crack = np.clip((ridge - 0.93) / 0.07, 0, 1) ** 1.5
base = 0.07 + 0.2 * rock + 0.08 * fine
img = np.stack([base * 1.05, base * 0.95, base * 0.9, np.ones((N, N), np.float32)], -1)
glow = np.stack([0.35 * crack + 0.1 * crack ** 3, 0.65 * crack + 0.3 * crack ** 3, 1.0 * crack + 0.5 * crack ** 3, np.zeros((N, N), np.float32)], -1)
img = np.clip(img + glow, 0, 1); img[..., 3] = 1
im = bpy.data.images.new('MeteorTex', N, N, alpha=False); im.pixels = img.flatten().tolist(); im.pack()
mat = bpy.data.materials.new('MeteorMat'); mat.use_nodes = True; bsdf = mat.node_tree.nodes['Principled BSDF']
tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = im; mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color']); bsdf.inputs['Roughness'].default_value = 0.9
o.data.materials.clear(); o.data.materials.append(mat)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_apply=True)
print('wrote', out)
