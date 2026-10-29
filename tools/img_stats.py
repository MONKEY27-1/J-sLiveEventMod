import bpy, sys
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=sys.argv[sys.argv.index('--')+1])
import numpy as np
for im in bpy.data.images:
    if im.size[0]==0: continue
    a=np.array(im.pixels[:]).reshape(-1,im.channels) if im.channels else None
    print(im.name, im.size[:], 'mean', a.mean(axis=0).round(3).tolist(), 'max', a.max(axis=0).round(2).tolist())
for m in bpy.data.materials:
    b=m.node_tree.nodes.get('Principled BSDF')
    if b: print('MAT',m.name,'base',[round(x,2) for x in b.inputs['Base Color'].default_value[:3]],'emis',[round(x,2) for x in b.inputs['Emission Color'].default_value[:3]],b.inputs['Emission Strength'].default_value, [l.from_node.name+':'+l.to_socket.name for l in m.node_tree.links][:6])
