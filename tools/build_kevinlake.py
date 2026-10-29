"""python build_kevinlake.py [install] -> the Cube Sinking (Loot Lake) event: event.json + install (model, mp3, dll)."""
import json, os, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'kevinlake'); os.makedirs(D, exist_ok=True)
MP3 = r'C:\Users\drago\Downloads\Fortnite chapter 1 season 5 live event.mp3'; GLB = r'C:\Users\drago\Downloads\kevin_the_cube.glb'; DUR = 105.0
S = [(0, 'The cube on the dock'), (11, 'The cube tips over'), (15, 'It hits the water'), (18, 'The cube dissolves'), (50, 'The water turns purple'), (85, 'The lake goes normal'), (86, 'The burst of light'), (89, 'The purple lake')]
json.dump({'id': 'cubelake', 'name': 'The Cube Sinking - Loot Lake (2018)', 'kind': 'cubelake', 'music': 'lake.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': 'cube', 'glb': 'cube.glb', 'lit': True}]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'cubelake'); os.makedirs(E, exist_ok=True)
    shutil.copy2(os.path.join(D, 'event.json'), E); shutil.copy2(GLB, os.path.join(E, 'cube.glb')); shutil.copy2(MP3, os.path.join(E, 'lake.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
