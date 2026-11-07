"""gx_view.py - helpers to open the Galactus rig blend, pose it with gx_fk and render textured Workbench frames + a contact sheet (PIL).  import from other scripts run by blender -b -P."""
import bpy, sys, os, math, numpy as np, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gx_fk
BLEND = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache', 'gx2', 'gx_rig.blend')
def open_rig():
    bpy.ops.wm.open_mainfile(filepath=BLEND); sc = bpy.context.scene; arm = bpy.data.objects['Gx']; rig = gx_fk.Rig(arm)
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = False
    sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_backface_culling = False
    sc.render.film_transparent = False; sc.world = bpy.data.worlds.new('w') if sc.world is None else sc.world; sc.world.color = (0.55, 0.57, 0.62)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
    return sc, arm, rig, cam
def shoot(sc, cam, path, centre, size, view='front', res=(360, 480)):
    sc.render.resolution_x, sc.render.resolution_y = res; cam.data.ortho_scale = size
    d = {'front': (0, -1, 0.1), 'side': (1, 0, 0.1), 'q': (0.7, -1, 0.25), 'back': (0, 1, 0.1), 'top': (0, -0.05, 1)}[view]; d = mathutils.Vector(d).normalized()
    cam.location = mathutils.Vector(centre) + d * 300; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); sc.render.filepath = path; bpy.ops.render.render(write_still=True)
def sheet(paths, out, cols=6, labels=None):
    """tile PNGs (no PIL in Blender): bpy images -> numpy -> one image"""
    ims = []
    for p in paths:
        im = bpy.data.images.load(p); w, h = im.size; a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a); ims.append(a.reshape(h, w, 4)); bpy.data.images.remove(im)
    h, w = ims[0].shape[:2]; rows = (len(ims) + cols - 1) // cols; S = np.zeros((rows * h, cols * w, 4), np.float32); S[..., 3] = 1
    for i, a in enumerate(ims): r, c = i // cols, i % cols; S[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = a
    o = bpy.data.images.new('sheet', cols * w, rows * h); o.pixels.foreach_set(S.ravel()); o.filepath_raw = out; o.file_format = 'PNG'; o.save()
