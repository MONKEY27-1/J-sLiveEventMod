import bpy
from mathutils import Vector

# Bake the supplied skin's rest pose to ordinary meshes.  The custom Unity loader
# can render MeshRenderer assets reliably; its hand-built SkinnedMeshRenderer path
# cannot reproduce this exporter's bind-pose hierarchy.
SRC = r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"
OUT = r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)

# The source contains a helper icosphere that is not part of the character.
for obj in list(bpy.data.objects):
    if obj.type == 'MESH' and obj.name.lower().startswith('icosphere'):
        bpy.data.objects.remove(obj, do_unlink=True)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if not meshes:
    raise RuntimeError('No character meshes were imported')

# Evaluate armature deformation once, then discard the armature relationship.
depsgraph = bpy.context.evaluated_depsgraph_get()
baked = []
for obj in meshes:
    evaluated = obj.evaluated_get(depsgraph)
    baked_mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
    baked_mesh.materials.clear()
    for material in obj.data.materials:
        baked_mesh.materials.append(material)
    baked_obj = bpy.data.objects.new('TravisBaked_' + obj.name, baked_mesh)
    bpy.context.collection.objects.link(baked_obj)
    baked_obj.matrix_world = obj.matrix_world
    baked.append(baked_obj)

for obj in list(bpy.context.scene.objects):
    if obj not in baked:
        bpy.data.objects.remove(obj, do_unlink=True)

# Centre in X/Y and place feet on Z=0, then normalize to a 1.95 m source height.
lo = Vector((float('inf'), float('inf'), float('inf')))
hi = Vector((float('-inf'), float('-inf'), float('-inf')))
for obj in baked:
    for v in obj.bound_box:
        p = obj.matrix_world @ Vector(v)
        lo.x, lo.y, lo.z = min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)
        hi.x, hi.y, hi.z = max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)
height = hi.z - lo.z
if height <= 0:
    raise RuntimeError('Character mesh has no height')

root = bpy.data.objects.new('TravisStaticRoot', None)
bpy.context.collection.objects.link(root)
root.location = Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -lo.z))
root.scale = Vector((1.95 / height,) * 3)
for obj in baked:
    matrix = obj.matrix_world.copy()
    obj.parent = root
    obj.matrix_world = matrix

bpy.context.view_layer.objects.active = root
root.select_set(True)
for obj in baked:
    obj.select_set(True)

bpy.ops.export_scene.gltf(
    filepath=OUT,
    export_format='GLB',
    use_selection=True,
    export_animations=False,
)
print('Baked static Travis:', len(baked), 'meshes; source height', height, '-> 1.95m')
