"""python build_icestorm.py [install] -> the Ice Storm event (Chapter 1 Season 7): Blender clips on the Ice King, event.json, install (model, mp3, dll)."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'icestorm'); os.makedirs(D, exist_ok=True)
MP3 = r'C:\Users\drago\Downloads\Ice storm Fortnite chapter season 7 Live event.mp3'; GLB = r'C:\Users\drago\Downloads\ice_king_fortnite.glb'; DUR = 50.0
PY = r'C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'
S = [(0, 'The ice orb'), (4, 'The orb cracks'), (7, 'The Ice King appears'), (11, 'The Ice King stands'), (20, 'He conjures the frozen wind'), (27, 'The storm comes closer'), (33, 'The storm hits'), (36, 'The blizzard'), (48, 'Whiteout')]
json.dump({'id': 'icestorm', 'name': 'The Ice Storm (Season 7, 2019)', 'kind': 'icestorm', 'music': 'storm.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': 'king', 'glb': 'ice_king.glb', 'lit': False}]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if not os.path.exists(os.path.join(D, 'ice_king_c.glb')) or os.path.getmtime(os.path.join(D, 'ice_king_c.glb')) < os.path.getmtime(os.path.join(HERE, 'anim_ice.py')): subprocess.check_call([PY, os.path.join(HERE, 'anim_ice.py'), GLB, os.path.join(D, 'ice_king_c.glb')])
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'icestorm'); os.makedirs(E, exist_ok=True)
    shutil.copy2(os.path.join(D, 'event.json'), E); shutil.copy2(os.path.join(D, 'ice_king_c.glb'), os.path.join(E, 'ice_king.glb')); shutil.copy2(MP3, os.path.join(E, 'storm.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
