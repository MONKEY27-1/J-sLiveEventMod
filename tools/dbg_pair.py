import bpy,sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\drago\Documents\AstronomicalEventMod\tools\cache\showdown\robot.glb")
for o in bpy.data.objects: print(o.name,o.type,o.parent.name if o.parent else None,tuple(round(v,2) for v in o.scale),tuple(round(v,1) for v in o.location))
