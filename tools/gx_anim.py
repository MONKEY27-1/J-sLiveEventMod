"""blender -b -P gx_anim.py -- [preview|export] [clip ...]
preview: renders a 3/4 + side contact sheet per clip (12 frames across its length) into tools/prev2/clip_<name>.png so the motion can be looked at.
export:  keys every clip (24 fps, all bones) as an Action and writes tools/cache/gx2/galactus_anim.glb"""
import bpy, sys, os, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gx_view, gx_fk, gx_clips
a = sys.argv[sys.argv.index('--') + 1:]; mode = a[0] if a else 'preview'; want = a[1:]
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'prev2'); FPS = 24
sc, arm, rig, cam = gx_view.open_rig(); gx_clips.setup(rig)
names = want or list(gx_clips.CLIPS)
if mode == 'preview':
    for nm in names:
        fn, dur = gx_clips.CLIPS[nm]; paths = []; n = 12
        for i in range(n):
            t = dur * i / n; gx_fk.apply(arm, rig.solve(fn(t))); bpy.context.view_layer.update(); hp = rig.last[1]['Hips']
            p = os.path.join(OUT, 'f_%s_%02d.png' % (nm, i)); gx_view.shoot(sc, cam, p, (hp[0] * 0 + 0, 0, 0 + hp[2] * 0), 84, 'q', res=(300, 400)); paths.append(p)
        gx_view.sheet(paths, os.path.join(OUT, 'clip_%s.png' % nm), cols=6); print('SHEET', nm)
else:
    bpy.context.view_layer.objects.active = arm; arm.animation_data_create()
    for nm in names:
        fn, dur = gx_clips.CLIPS[nm]; act = bpy.data.actions.new(nm); arm.animation_data.action = act; nf = int(round(dur * FPS))
        for f in range(nf + 1):
            gx_fk.apply(arm, rig.solve(fn((f / FPS) if f < nf else dur - 1e-6 if nm in ('Backhand', 'Roar') else dur)), frame=f + 1)
        act.use_fake_user = True; print('ACTION', nm, nf + 1, 'frames')
    arm.animation_data.action = None
    sc.frame_start, sc.frame_end = 1, 400; sc.render.fps = FPS
    outp = os.path.join(HERE, 'cache', 'gx2', 'galactus_anim.glb')
    bpy.ops.export_scene.gltf(filepath=outp, export_format='GLB', export_yup=True, export_animations=True, export_animation_mode='ACTIONS', export_skins=True, export_force_sampling=True, export_optimize_animation_size=False)
    print('EXPORTED', outp)
