"""python zc_chars.py [id ...]   builds the ZERO CRISIS cast: zc_anim.py clips -> cache/zc/<id>.glb, then shrink_glb.py (textures) in place. No ids = all."""
import os, subprocess, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'zc'); os.makedirs(OUT, exist_ok=True); DL = r'C:\Users\drago\Downloads'
PY = r'C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'; BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
COMMON = 'Idle,Walk,Run,Sprint,Fight,Block,Punch,Kick,Lunge,Roar,Hit,Stagger,KnockDown,GetUp,Fall,Cheer,Victory,Taunt,Look,LookUp,Brace,Dodge,Alert,Point,Wave,Jump,Float,Talk,Crouch,Think'
JONES = 'Idle,Walk,Run,Sprint,Talk,Plead,Shrug,Point,Look,LookUp,Hang,Device,DeviceUp,Fall,Land,KnockDown,GetUp,Stagger,Hit,Dodge,Brace,Cheer,Wave,Think,Beckon,Crouch,Reach,Drag,Block,Fight,Victory,Float,Climb,Shove,Fistbump,Jump,Pray,Taunt,Punch,Channel,Kick,Lunge,Roar'
FOUND = 'Idle,Walk,Run,Talk,Shove,Reach,Channel,Look,LookUp,Point,Hit,Stagger,KnockDown,GetUp,Fistbump,Pray,Think,Brace,Fight,Punch,Roar,Float,Cheer,Beckon,Taunt,Fall,Crouch,Block,Kick,Lunge'
# id: (profile, source file, clips, texture px, env extras)
CAST = {
    'jones': ('ue', 'agent_jonesy_-_kepler.glb', JONES, 1024, {}), 'foundation': ('ue', 'fortnite_-_the_foundation.glb', FOUND, 1024, {}),
    'kratos': ('ue', 'kratos_fortnite.glb', COMMON + ',Shove', 512, {}), 'xeno': ('ue', 'xenomorph_-_fortnite_render.glb', COMMON, 512, {}), 'sarah': ('ue', 'sarah_connor_fortnite.glb', COMMON, 512, {}),
    'chunli': ('ue', 'chun-li_fortnite_skin.glb', COMMON, 512, {}), 'trigger': ('ue', 'triggerfish_-_fortnite_skin.glb', COMMON + ',Dance', 512, {}), 'hybrid': ('ue', 'hybrid_-_fortnite_skin.glb', COMMON, 512, {}),
    'mancake': ('ue', 'mancake.glb', COMMON + ',Dance', 512, {}), 'terminator': ('term', 'the_terminator.glb', COMMON, 512, {}),
    'chief': ('ue', os.path.join(OUT, 'ar_chief.glb'), COMMON, 512, {}), 'ryu': ('ue', os.path.join(OUT, 'ar_ryu.glb'), COMMON, 512, {}), 'tntina': ('ue', os.path.join(OUT, 'ar_tntina.glb'), COMMON + ',Dance', 512, {}),
    'ripley': ('ue', os.path.join(OUT, 'ar_ripley.glb'), COMMON, 512, {}), 'predator': ('ue', os.path.join(OUT, 'ar_predator.glb'), COMMON + ',Roar', 512, {}),
    'iron': ('ue', 'fortnite_iron_man.glb', COMMON + ',Hover,FlyPose,Repulsor', 1024, {}), 'thor': ('mixa', 'thor-fortnite.glb', COMMON, 512, {}), 'wolverine': ('wolv', 'rigged_wolverine.glb', COMMON + ',Lunge', 512, {}),
    'dog': ('mixa', 'skin_fortnite_dog_supreme_versionemote.glb', COMMON + ',Dance', 512, {'STRIP': 'Matriau,Matriau.001', 'KEEPANIM': '1'}), 'meowscle': ('mixa', 'meowscle_rig.glb', COMMON + ',Flex', 512, {}),
}
for cid in (sys.argv[1:] or list(CAST)):
    prof, src, clips, px, extra = CAST[cid]; dst = os.path.join(OUT, cid + '.glb'); env = dict(os.environ, DUR='6', FPS='15', ONLY=clips); env.update(extra)
    r = subprocess.run([PY, os.path.join(HERE, 'zc_anim.py'), prof, os.path.join(DL, src), dst], env=env, capture_output=True, text=True)
    print(cid, 'anim rc', r.returncode, (r.stdout.strip().splitlines() or [''])[-1][:160]);
    if r.returncode: print(r.stderr[-1500:]); continue
    tmp = dst + '.tmp'; r = subprocess.run([BL, '-b', '-P', os.path.join(HERE, 'shrink_glb.py'), '--', dst, tmp, str(px)], capture_output=True, text=True)
    if os.path.exists(tmp): shutil.move(tmp, dst); print(cid, 'final', os.path.getsize(dst) // 1024, 'KB')
    else: print(cid, 'shrink failed', r.stdout[-400:])
