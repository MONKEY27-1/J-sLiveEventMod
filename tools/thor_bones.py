import bpy, sys
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=a[0])
for o in bpy.data.objects:
    if o.type == 'ARMATURE':
        print('ARM', o.name, len(o.data.bones), 'loc', tuple(o.location), 'scale', tuple(o.scale), 'rot', tuple(o.rotation_euler))
        for b in o.data.bones: print('BONE', b.name, tuple(round(x, 3) for x in b.head_local), '->', b.parent.name if b.parent else None)
    if o.type == 'MESH': print('MESH', o.name, 'parent', o.parent.name if o.parent else None, 'mods', [m.type for m in o.modifiers], [g.name for g in o.vertex_groups][:6])
