"""blender -b -P gx_maw.py -- out.glb [preview.png]      GALACTUS' OPEN MOUTH (8:13 - 8:29): a shallow dish, radius 1, seen from the front (Blender -y):
a dark throat with glowing rings that crowd together towards a white-hot core, so it reads as a deep tunnel although it is nearly flat (it has to sit IN FRONT of the face mesh).
Double sided (every face twice), so the loader's handedness / culling does not matter.  The plugin scales it wider than tall and puts it on the MouthP bone."""
import bpy, bmesh, math, sys
a = sys.argv[sys.argv.index('--') + 1:]; out = a[0]; prev = a[1] if len(a) > 1 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name, rgb, glow=False):
    m = bpy.data.materials.new(name); m.use_nodes = True; bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); bs.inputs['Base Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); bs.inputs['Roughness'].default_value = 0.9
    if glow: bs.inputs['Emission Color'].default_value = (rgb[0], rgb[1], rgb[2], 1.0); bs.inputs['Emission Strength'].default_value = 2.0
    return m
MATS = [mat('GxMawLip', (0.10, 0.03, 0.12)), mat('GxMawDark', (0.015, 0.0, 0.03)), mat('SF_G_GxMawA', (0.55, 0.06, 0.34), True), mat('SF_G_GxMawB', (1.0, 0.40, 0.78), True), mat('SF_G_GxMawC', (1.0, 0.92, 1.0), True)]
SEG, N, DEPTH = 48, 44, 0.16
# band per ring step, rim -> core: a lip, then dark with ever closer glowing rings, then the core
def band(i):
    if i <= 1: return 0
    if i >= N - 4: return 4
    if i >= N - 10: return 3
    return 2 if i % 7 == 6 else 1          # thin dim rings far apart on the dark throat
bm = bmesh.new()
for flip in (False, True):          # two shells with their own vertices: front faces and back faces
    rings = []
    for i in range(N + 1):
        k = i / N; r = (1.0 - k) ** 1.5 * (1.0 + 0.045 * math.sin(i * 2.1)) if i < N else 0.0; y = DEPTH * k ** 0.7     # rings crowd towards the centre = perspective of a deep throat
        wob = 0.05 * (1.0 - k)                                                                                    # the rim is slightly ragged
        rings.append([bm.verts.new((r * (1.0 + wob * math.sin(5 * t + i)) * math.cos(t), y, r * (1.0 + wob * math.cos(3 * t + 2 * i)) * math.sin(t))) for t in [2 * math.pi * s / SEG for s in range(SEG)]] if i < N else [bm.verts.new((0, y, 0))])
    for i in range(N):
        A, B = rings[i], rings[i + 1]
        for s in range(SEG):
            s2 = (s + 1) % SEG; vs = [A[s], A[s2], B[s2], B[s]] if len(B) > 1 else [A[s], A[s2], B[0]]
            f = bm.faces.new(vs[::-1] if flip else vs); f.material_index = band(i)
me = bpy.data.meshes.new('GxMaw'); bm.to_mesh(me); bm.free()
for m in MATS: me.materials.append(m)
ob = bpy.data.objects.new('Maw', me); bpy.context.collection.objects.link(ob)
print('MAW polys', len(me.polygons))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=False)
if prev:
    sc = bpy.context.scene; cam = bpy.data.objects.new('C', bpy.data.cameras.new('C')); sc.collection.objects.link(cam); sc.camera = cam; cam.location = (0.9, -2.6, 0.5)
    cam.rotation_euler = (math.radians(79), 0, math.radians(19)); sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'MATERIAL'
    for m in MATS: bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); m.diffuse_color = tuple(bs.inputs['Base Color'].default_value)
    sc.render.resolution_x = 640; sc.render.resolution_y = 480; sc.render.filepath = prev; bpy.ops.render.render(write_still=True)
print('EXPORTED', out)
