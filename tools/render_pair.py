"""blender -b -P render_pair.py -- <monster.glb> <robot.glb> <out.png> <cols> <ortho_m> t:x:y:z:az ...   both giants in the show's unity-local frame (X = x, Y = z, Z = up), one tile per spec:
audio time t, focus point (metres), camera azimuth in degrees (0 = looking along +Y). The monster / robot glbs are placed exactly as the plugin's loader places them."""
import bpy, math, sys, os, json, tempfile
import numpy as np
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
MON, ROB, OUT, COLS, SCALE = args[:5]; COLS = int(COLS); SCALE = float(SCALE); specs = [a.split(':') for a in args[5:]]
HERE = os.path.dirname(os.path.abspath(__file__)); OUTD = os.path.join(HERE, 'cache', 'showdown')
meta = json.load(open(os.path.join(OUTD, 'monster_meta.json')))
bpy.ops.wm.read_factory_settings(use_empty=True)
def load(path, name):
    before = set(bpy.data.objects); acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=path)
    arm = [o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE'][0]
    act = [a for a in bpy.data.actions if a not in acts_before][0]
    arm.animation_data_create(); arm.animation_data.action = act
    top = arm
    while top.parent: top = top.parent
    return arm, top
am, topm = load(MON, 'm'); ar, topr = load(ROB, 'r')
# placement: fit a similarity transform (scale, rotation / reflection, shift) from each model's Blender bone positions to the unity-local positions the baked traces say they have (X = x, Y = z, Z = up)
import numpy as np
def fit(arm, top, trace, times):
    dt = trace['dt']; names = [b for b in ('pelvis', 'head', 'hand_l', 'hand_r', 'spine_03') if b in trace['b']]
    P = []; Q = []; sc_ = bpy.context.scene
    for t in times:
        sc_.frame_set(int(round(t * sc_.render.fps))); bpy.context.view_layer.update()
        for nm in names:
            bs = [b for b in arm.pose.bones if b.name == nm or (b.name.startswith(nm + '_') and b.name[len(nm) + 1:].replace('_', '').isdigit())]
            if not bs: continue
            bone = min(bs, key=lambda b: len(b.name)); w = arm.matrix_world @ bone.head; P.append([w.x, w.y, w.z])
            i = min(len(trace['b'][nm]) - 1, int(round(t / dt))); u = trace['b'][nm][i]; Q.append([u[0], u[2], u[1]])
    P = np.array(P); Q = np.array(Q); mp, mq = P.mean(0), Q.mean(0); A = (P - mp); B = (Q - mq)
    U, S_, Vt = np.linalg.svd(B.T @ A); R = U @ Vt; s = S_.sum() / (A ** 2).sum()
    M = np.eye(4); M[:3, :3] = s * R; M[:3, 3] = mq - s * R @ mp
    from mathutils import Matrix
    top.matrix_world = Matrix(M.tolist()) @ top.matrix_world
    bpy.context.view_layer.update()
    err = np.sqrt((((s * (R @ A.T)).T - B) ** 2).sum(1).mean()); print('fit scale %.4f rms error %.2f m det %.1f' % (s, err, np.linalg.det(R)))
mt = json.load(open(os.path.join(OUTD, 'monster_trace.json'))); rt = json.load(open(os.path.join(OUTD, 'robot_trace.json')))
FT = [70.0, 125.0, 175.0, 215.0, 250.0]
fit(am, topm, mt, FT); fit(ar, topr, rt, FT)
sc = bpy.context.scene; fps = sc.render.fps
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
TW = TH = 400; sc.render.resolution_x = TW; sc.render.resolution_y = TH; sc.render.film_transparent = False
bpy.ops.mesh.primitive_plane_add(size=2000, location=(0, 0, 0)); pl = bpy.context.object
mat = bpy.data.materials.new('g'); mat.diffuse_color = (0.30, 0.36, 0.26, 1); pl.data.materials.append(mat)
for (x, y) in ((0, 0),):                                                     # the players' spot (the gazebo) as a pole
    bpy.ops.mesh.primitive_cylinder_add(radius=1.5, depth=20, location=(x, y, 10))
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.clip_end = 1e5; cam.data.ortho_scale = SCALE
tiles = []; tmp = tempfile.mkdtemp()
for k, sp in enumerate(specs):
    t, fx, fy, fz, az = [float(v) for v in sp]
    sc.frame_set(int(round(t * fps))); bpy.context.view_layer.update()
    a = math.radians(az); d = Vector((math.sin(a) * math.cos(0.3), -math.cos(a) * math.cos(0.3), math.sin(0.3)))
    tgt = Vector((fx, fz, fy)); cam.location = tgt + d * 600; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    p = os.path.join(tmp, 't%d.png' % k); sc.render.filepath = p; bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(p); arr = np.array(img.pixels[:]).reshape(TH, TW, 4); arr[..., :3] = arr[..., :3] * arr[..., 3:4] + (1 - arr[..., 3:4]); arr[..., 3] = 1.0; tiles.append(arr); bpy.data.images.remove(img)
rows = (len(tiles) + COLS - 1) // COLS
sheet = np.ones((rows * TH, COLS * TW, 4), dtype=np.float32)
for k, a_ in enumerate(tiles):
    r, c = divmod(k, COLS); sheet[(rows - 1 - r) * TH:(rows - r) * TH, c * TW:(c + 1) * TW] = a_
img = bpy.data.images.new('sheet', COLS * TW, rows * TH, alpha=False); img.pixels = sheet.flatten().tolist(); img.filepath_raw = OUT; img.file_format = 'PNG'; img.save()
print('sheet', OUT, len(tiles))
if os.environ.get('FOOTDBG'):
    for nm, arm_ in (('monster', am), ('robot', ar)):
        for t in (60, 100, 110, 120, 215, 232, 238, 271, 278):
            sc.frame_set(int(round(t * fps))); bpy.context.view_layer.update()
            zs = []
            for key in ('foot_l', 'foot_r'):
                bs = [b for b in arm_.pose.bones if b.name.startswith(key + '_') and b.name[len(key) + 1:].replace('_', '').isdigit()]
                if bs: zs.append(round((arm_.matrix_world @ min(bs, key=lambda b: len(b.name)).head).z, 1))
            print('FOOT', nm, t, zs)
if os.environ.get('SAVEBLEND'):
    bpy.ops.wm.save_as_mainfile(filepath=os.environ['SAVEBLEND'])
