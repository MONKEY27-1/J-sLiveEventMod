"""python build_zerocrisis.py [install]   ZERO CRISIS FINALE (16 Mar 2021, 11:08): stages the glbs (cast from zc_chars.py, props from zc_world.py / zc_prep.py), writes event.json and (with `install`) copies everything into the game.
Run zc_chars.py and the Blender prop scripts first (see PROJECT_STATUS.md, update 60)."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); C = os.path.join(HERE, 'cache'); Z = os.path.join(C, 'zc'); D = os.path.join(Z, 'event'); os.makedirs(D, exist_ok=True)
DL = r'C:\Users\drago\Downloads'; MP3 = os.path.join(DL, 'Fortnite Zero Crisis Finale Live Event Gameplay.mp3'); DUR = 668.7
PY = r'C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'; BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
S = [(0, 'Black'), (4, 'The Battle Bus'), (36, 'The meadow'), (50, 'The Hunters'), (88, 'The run'), (118, 'The meteor'), (150, 'The Foundation'), (200, 'The Zero Point'), (280, 'Reality Wave: Raz'), (340, 'Reality Wave: Cluck'), (390, 'The dark'), (400, 'Lara Croft'),
     (450, 'The bloom'), (500, 'The Spire'), (552, 'The Zero Point chamber'), (602, 'Season 6 reel')]
CAST = ['jones', 'foundation', 'kratos', 'xeno', 'sarah', 'chunli', 'trigger', 'hybrid', 'mancake', 'terminator', 'dog', 'meowscle', 'chief', 'ryu', 'tntina', 'ripley', 'predator']
# prop id -> (source, texture px or 0 = copy as is)
PROPS = {'bus': (os.path.join(Z, 'p_bus.glb'), 512), 'spire': (os.path.join(Z, 'p_spire.glb'), 0), 'wolf': (os.path.join(Z, 'p_wolf.glb'), 512), 'device': (os.path.join(Z, 'p_device.glb'), 256), 'meteor': (os.path.join(Z, 'p_meteor.glb'), 512),
         'zeropoint': (os.path.join(C, 'props', 'zeropoint.glb'), 0), 'butterfly': (os.path.join(C, 'butterfly', 'butterfly.glb'), 0), 'crystals': (os.path.join(Z, 'w_crystals.glb'), 0), 'lotus': (os.path.join(Z, 'w_lotus.glb'), 0),
         'rift': (os.path.join(Z, 'w_rift.glb'), 0), 'rocks': (os.path.join(Z, 'w_rocks.glb'), 0), 'chamber': (os.path.join(Z, 'w_chamber.glb'), 0), 'tree': (os.path.join(HERE, 'tree.glb'), 0), 'peely': (os.path.join(DL, 'fortnite_peely_skin.glb'), 512)}
def stage():
    for cid in CAST: shutil.copy2(os.path.join(Z, cid + '.glb'), os.path.join(D, cid + '.glb'))
    for pid, (src, px) in PROPS.items():
        dst = os.path.join(D, pid + '.glb')
        if px:
            tmp = dst + '.slim'; subprocess.run([PY, os.path.join(HERE, 'slim_glb.py'), src, tmp], capture_output=True); sp = tmp if os.path.exists(tmp) else src
            r = subprocess.run([BL, '-b', '-P', os.path.join(HERE, 'shrink_glb.py'), '--', sp, dst, str(px)], capture_output=True, text=True)
            if os.path.exists(tmp): os.remove(tmp)
            if not os.path.exists(dst): shutil.copy2(src, dst)
        else: shutil.copy2(src, dst)
stage()
ids = CAST + list(PROPS)
json.dump({'id': 'zerocrisis', 'name': 'Zero Crisis Finale (2021)', 'kind': 'zerocrisis', 'music': 'zerocrisis.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': k, 'glb': k + '.glb', 'lit': False} for k in ids]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
print('staged', len(ids), 'models,', sum(os.path.getsize(os.path.join(D, k + '.glb')) for k in ids) // 1024, 'KB')
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'zerocrisis'); os.makedirs(E, exist_ok=True)
    for f in ['event.json'] + [k + '.glb' for k in ids]: shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'zerocrisis.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
