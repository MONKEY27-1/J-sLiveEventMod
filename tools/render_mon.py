"""blender -b -P render_mon.py -- <glb> <out.png> <cols> <azimuth_deg> <ortho_scale> <t1> [t2 ...]   contact sheet that follows the pelvis in 3D (Workbench, ground plane at file y = 0)
azimuth is the camera direction in blender world (0 = looking along +Y from -Y).  env RM_ROT=1 turns the view with the pelvis heading instead (so az 0 = from the front, 90 = from the left...)"""
import bpy, math, sys, os, tempfile
import numpy as np
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index('--') + 1:]
GLB, OUT, COLS, AZ, SCALE = args[:5]; COLS = int(COLS); AZ = float(AZ); SCALE = float(SCALE); times = [float(x) for x in args[5:]]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
sc = bpy.context.scene; fps = sc.render.fps
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
arm.animation_data_create(); arm.animation_data.action = list(bpy.data.actions)[0]
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
TW = TH = 330; sc.render.resolution_x = TW; sc.render.resolution_y = TH; sc.render.film_transparent = False
bpy.ops.mesh.primitive_plane_add(size=float(os.environ.get("RM_PLANE", "400")), location=(0, 0, float(os.environ.get('RM_GROUND', '-0.087')))); pl = bpy.context.object
mat = bpy.data.materials.new('g'); mat.diffuse_color = (0.35, 0.4, 0.3, 1); pl.data.materials.append(mat)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 1e7; cam.data.ortho_scale = SCALE
pb = arm.pose.bones['pelvis'] if 'pelvis' in arm.pose.bones else [b for b in arm.pose.bones if b.name.startswith('pelvis')][0]
tiles = []; tmp = tempfile.mkdtemp()
for k, t in enumerate(times):
    sc.frame_set(int(round(t * fps))); bpy.context.view_layer.update()
    tgt = arm.matrix_world @ pb.head
    az = math.radians(AZ)
    if os.environ.get('RM_ROT'):                                  # relative to the pelvis' facing (its +Y bone axis projected)
        fwd = (arm.matrix_world.to_3x3() @ pb.matrix.to_3x3() @ Vector((0, 0, 1))); az += math.atan2(fwd.x, fwd.y) if False else 0
    d = Vector((math.sin(az) * math.cos(0.25), -math.cos(az) * math.cos(0.25), math.sin(0.25)))
    cam.location = tgt + d * max(30.0, SCALE * 4); cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    p = os.path.join(tmp, 't%d.png' % k); sc.render.filepath = p; bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(p); a = np.array(img.pixels[:]).reshape(TH, TW, 4); a[..., :3] = a[..., :3] * a[..., 3:4] + (1 - a[..., 3:4]); a[..., 3] = 1.0; tiles.append(a); bpy.data.images.remove(img)
    print('t', t, 'pelvis blender', [round(v, 2) for v in tgt])
rows = (len(tiles) + COLS - 1) // COLS
sheet = np.ones((rows * TH, COLS * TW, 4), dtype=np.float32)
for k, a in enumerate(tiles):
    r, c = divmod(k, COLS); sheet[(rows - 1 - r) * TH:(rows - r) * TH, c * TW:(c + 1) * TW] = a
img = bpy.data.images.new('sheet', COLS * TW, rows * TH, alpha=False); img.pixels = sheet.flatten().tolist(); img.filepath_raw = OUT; img.file_format = 'PNG'; img.save()
print('sheet', OUT, len(tiles))
