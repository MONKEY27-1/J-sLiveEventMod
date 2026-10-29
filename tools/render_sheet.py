"""blender -b -P render_sheet.py -- <glb> <out.png> <action prefix> <view> <cols> <t1> [t2 ...]   -> one contact sheet with a tile per time"""
import bpy, math, sys, os, tempfile
import numpy as np
args = sys.argv[sys.argv.index('--') + 1:]
GLB, OUT, ACT, VIEW, COLS = args[:5]; COLS = int(COLS); times = [float(x) for x in args[5:]]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
sc = bpy.context.scene; fps = sc.render.fps
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = [a for a in bpy.data.actions if a.name.startswith(ACT)] or list(bpy.data.actions)
arm.animation_data_create(); arm.animation_data.action = act[0]
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
TW, TH = 330, 480
sc.render.resolution_x = TW; sc.render.resolution_y = TH
sc.render.film_transparent = False
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 1e6
cfg = {'full': (21000, (0, 60000, 8300), (90, 0, 180)), 'upper': (10500, (0, 60000, 12000), (90, 0, 180)), 'face': (3600, (0, 60000, 14900), (90, 0, 180)),
       'side': (21000, (-60000, 0, 8300), (90, 0, -90)), 'wide': (30000, (0, 60000, 8300), (90, 0, 180)), 'back': (21000, (0, -60000, 8300), (90, 0, 0)),
       'three': (22000, (42000, 42000, 8600), (90, 0, 135)),
       'tall': (36000, (0, 60000, 15500), (90, 0, 180)), 'sidetall': (36000, (-60000, 0, 15500), (90, 0, -90))}
K = float(os.environ.get('RS_K', '1'))
sc_, loc, rot = cfg[VIEW]
sc_ *= K; loc = tuple(x * K for x in loc)
cam.data.ortho_scale = sc_; cam.location = loc; cam.rotation_euler = tuple(math.radians(a) for a in rot)
tiles = []
tmp = tempfile.mkdtemp()
hb = [b for b in arm.pose.bones if b.name == 'head' or (b.name.startswith('head') and 'end' not in b.name and 'offset_end' not in b.name)][0]
for k, t in enumerate(times):
    sc.frame_set(int(round(t * fps)))
    bpy.context.view_layer.update()
    hp = arm.matrix_world @ hb.head                      # keep the camera on the character so travelling / floating moves stay in frame
    if VIEW in ('side', 'sidetall'): cam.location.y = loc[1] + hp.y
    elif VIEW == 'three': cam.location.x = loc[0] + hp.x * 0.7; cam.location.y = loc[1] - hp.x * 0.7
    else: cam.location.x = loc[0] + hp.x
    if VIEW in ('face', 'upper'): cam.location.z = loc[2] + (hp.z - 14900 * K)
    p = os.path.join(tmp, "t%d.png" % k); sc.render.filepath = p; bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(p); a = np.array(img.pixels[:]).reshape(TH, TW, 4); a[..., :3] = a[..., :3] * a[..., 3:4] + (1 - a[..., 3:4]); a[..., 3] = 1.0; tiles.append(a); bpy.data.images.remove(img)
rows = (len(tiles) + COLS - 1) // COLS
sheet = np.ones((rows * TH, COLS * TW, 4), dtype=np.float32)
for k, a in enumerate(tiles):
    r, c = divmod(k, COLS); sheet[(rows - 1 - r) * TH:(rows - r) * TH, c * TW:(c + 1) * TW] = a
img = bpy.data.images.new('sheet', COLS * TW, rows * TH, alpha=False); img.pixels = sheet.flatten().tolist()
img.filepath_raw = OUT; img.file_format = 'PNG'; img.save()
print("sheet", OUT, len(tiles))
