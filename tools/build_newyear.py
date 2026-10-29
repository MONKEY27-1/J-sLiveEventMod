"""python build_newyear.py [install] -> the New Year event (2019-2022 by setting): Blender disco / crystal balls, event.json, install (models, mp3, dll)."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'newyear'); os.makedirs(D, exist_ok=True)
MP3 = r'C:\Users\drago\Downloads\Fortnite 2021 new year event no commentary.mp3'; BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'; DUR = 130.0
S = [(0, 'The night sky'), (5, 'The rift opens'), (9, 'The ball appears'), (18, 'The countdown'), (28, 'Midnight!'), (38, 'Confetti'), (42, 'The new year'), (63, 'The end')]
json.dump({'id': 'newyear', 'name': 'New Year Countdown (2019-2022)', 'kind': 'newyear', 'music': 'newyear.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': 'disco', 'glb': 'disco.glb', 'lit': False}, {'id': 'crystal', 'glb': 'crystal.glb', 'lit': False}]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
for k in ('disco', 'crystal'):
    if not os.path.exists(os.path.join(D, k + '.glb')): subprocess.check_call([BL, '-b', '-P', os.path.join(HERE, 'make_newyear.py'), '--', k, os.path.join(D, k + '.glb')])
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'newyear'); os.makedirs(E, exist_ok=True)
    for f in ('event.json', 'disco.glb', 'crystal.glb'): shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'newyear.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
