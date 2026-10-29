"""blender -b -P make_scar.py -- in.glb out.glb   the supplied Fortnite SCAR as a static prop: skin / armature dropped, origin moved to the PISTOL GRIP (so the plugin puts the gun where the right hand holds it),
muzzle along Blender -Y (= Unity +Z), top +Z."""
import bpy, sys, mathutils
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[:2]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and len(o.data.vertices) > 100]
gun = meshes[0]
for o in list(bpy.data.objects):
    if o is not gun and o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)
for m in list(gun.modifiers): gun.modifiers.remove(m)
mw = gun.matrix_world.copy(); gun.parent = None; gun.matrix_world = mw; gun.vertex_groups.clear()
bpy.context.view_layer.objects.active = gun; gun.select_set(True); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
sh = mathutils.Vector((-0.016, 0.13, 0.03))
for v in gun.data.vertices: v.co += sh
for o in list(bpy.data.objects):
    if o is not gun: bpy.data.objects.remove(o, do_unlink=True)
pts = [v.co for v in gun.data.vertices]; print('bbox', [round(min(p[i] for p in pts), 3) for i in range(3)], [round(max(p[i] for p in pts), 3) for i in range(3)])
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_skins=False, export_animations=False, export_apply=False)
print('wrote', out)
