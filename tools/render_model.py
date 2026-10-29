import bpy, math, sys
a=sys.argv[sys.argv.index('--')+1:]; glb=a[0]; out=a[1]; scale=float(a[2]); cx=float(a[3]); cz=float(a[4])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
sc.render.resolution_x=600; sc.render.resolution_y=600; sc.render.film_transparent=False
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.clip_end=1e6; cam.data.ortho_scale=scale
import tempfile, os
views={'front':((cx,2000,cz),(90,0,180)),'side':((-2000,0,cz),(90,0,-90)),'three':((cx+1400,1400,cz),(90,0,135))}
for k,(loc,rot) in views.items():
    cam.location=loc; cam.rotation_euler=tuple(math.radians(x) for x in rot); sc.render.filepath=out+'_'+k+'.png'; bpy.ops.render.render(write_still=True)
print('ok')
