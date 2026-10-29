"""python build_butterfly.py [install] -> the Butterfly Event (Season 6, 2018): Blender butterfly + In-Between, the Kevin cube, event.json, install."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'butterfly'); os.makedirs(D, exist_ok=True)
MP3 = r'C:\Users\drago\Downloads\Season 6 Cube Explosion Event (Fortnite Battle Royale).mp3'; BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'; CUBE = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\events\kevincube\cube.glb'; DUR = 140.0
S = [(0, 'The spinning cube'), (60, 'The explosion'), (62, 'The In-Between'), (108, 'The butterfly appears'), (130, 'It flies to you'), (135, 'It lands on your hand'), (137, 'The white flash')]
json.dump({'id': 'butterfly', 'name': 'The Butterfly Event (Season 6, 2018)', 'kind': 'butterfly', 'music': 'butterfly.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S],
           'props': [{'id': 'cube', 'glb': 'cube.glb', 'lit': True}, {'id': 'butterfly', 'glb': 'butterfly.glb', 'lit': False}, {'id': 'inbetween', 'glb': 'inbetween.glb', 'lit': False}]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
for k in ('butterfly', 'inbetween'):
    if not os.path.exists(os.path.join(D, k + '.glb')): subprocess.check_call([BL, '-b', '-P', os.path.join(HERE, 'make_butterfly.py'), '--', k, os.path.join(D, k + '.glb')])
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'butterfly'); os.makedirs(E, exist_ok=True)
    for f in ('event.json', 'butterfly.glb', 'inbetween.glb'): shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(CUBE, os.path.join(E, 'cube.glb')); shutil.copy2(MP3, os.path.join(E, 'butterfly.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
