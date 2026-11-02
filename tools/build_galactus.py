"""python build_galactus.py [install]   THE GALACTUS EVENT (1 Dec 2020, 10:50 recording): stages the glbs (cast from zc_chars.py: iron / thor / wolverine / jones; props from zc_prep.py / gx_world.py / gx_split.py), writes event.json and (with `install`) copies everything into the game."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); Z = os.path.join(HERE, 'cache', 'zc'); D = os.path.join(Z, 'gx_event'); os.makedirs(D, exist_ok=True)
DL = r'C:\Users\drago\Downloads'; MP3 = os.path.join(DL, 'Fortnite Galactus Event on PS5 No Commentary (Chapter 2 Season 4 Live Event).mp3'); DUR = 650.9
PY = r'C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'; BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
S = [(0, 'The Helicarrier'), (55, 'Galactus arrives'), (120, 'He reaches'), (180, 'The fall'), (270, 'The Battle Bus'), (330, 'The assault'), (420, 'Inside Galactus'), (510, 'Space'), (535, 'The face'), (575, 'The blast'), (600, 'Black'), (605, 'Agent Jones'), (620, 'Season 5')]
# prop id -> (source, texture px or 0 = copy as is)
FILES = {'heli': (os.path.join(Z, 'g_heli.glb'), 512), 'sbus': (os.path.join(Z, 'g_sbus.glb'), 512), 'sbusfar': (os.path.join(Z, 'g_sbusfar.glb'), 256), 'galactus': (os.path.join(Z, 'gx_galactus2.glb'), 0), 'corridor': (os.path.join(Z, 'gx_corridor.glb'), 0),
         'office': (os.path.join(Z, 'gx_office.glb'), 0), 'iron': (os.path.join(Z, 'iron.glb'), 0), 'thor': (os.path.join(Z, 'thor.glb'), 0), 'wolverine': (os.path.join(Z, 'wolverine.glb'), 0), 'jones': (os.path.join(Z, 'jones.glb'), 0)}
for pid, (src, px) in FILES.items():
    dst = os.path.join(D, pid + '.glb')
    if px:
        tmp = dst + '.slim'; subprocess.run([PY, os.path.join(HERE, 'slim_glb.py'), src, tmp], capture_output=True); sp = tmp if os.path.exists(tmp) else src
        subprocess.run([BL, '-b', '-P', os.path.join(HERE, 'shrink_glb.py'), '--', sp, dst, str(px)], capture_output=True, text=True)
        if os.path.exists(tmp): os.remove(tmp)
        if not os.path.exists(dst): shutil.copy2(src, dst)
    else: shutil.copy2(src, dst)
json.dump({'id': 'galactus', 'name': 'The Galactus Event (2020)', 'kind': 'galactus', 'music': 'galactus.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': k, 'glb': k + '.glb', 'lit': False} for k in FILES]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
print('staged', len(FILES), 'models,', sum(os.path.getsize(os.path.join(D, k + '.glb')) for k in FILES) // 1024, 'KB')
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'galactus'); os.makedirs(E, exist_ok=True)
    for f in ['event.json'] + [k + '.glb' for k in FILES]: shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'galactus.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
