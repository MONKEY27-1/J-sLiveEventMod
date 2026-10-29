import bpy

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Downloads\stage_3.glb")
for mat in bpy.data.materials:
    print('Material:', mat.name)
    if mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type == 'BSDF_PRINCIPLED':
                base_color = list(node.inputs['Base Color'].default_value)
                print(f'  Base Color: {base_color}')
