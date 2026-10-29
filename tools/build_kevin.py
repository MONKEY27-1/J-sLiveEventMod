"""python build_kevin.py [install] -> event.json for the Kevin the Cube mini event (cache/kevin/) and, with 'install', copies it, the cube model and the mp3 into the game's plugin folder."""
import json, os, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'kevin'); os.makedirs(D, exist_ok=True)
MP3 = r'C:\Users\drago\Downloads\FORTNITE CUBE SPAWNING (FOOTAGE OF KEVIN).mp3'; GLB = r'C:\Users\drago\Downloads\kevin_the_cube.glb'; DUR = 51.0
S = [(0, 'The rift opens'), (5, 'The rift grows'), (10, 'First lightning'), (18, 'The lightning builds'), (23, 'The cube begins to appear'), (25, 'Kevin'), (29, 'Kevin floats'), (33, 'The rift is gone')]
json.dump({'id': 'kevincube', 'name': 'Kevin the Cube (2018)', 'kind': 'cube', 'music': 'kevin.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'props': [{'id': 'cube', 'glb': 'cube.glb', 'lit': True}]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'kevincube'); os.makedirs(E, exist_ok=True)
    shutil.copy2(os.path.join(D, 'event.json'), E); shutil.copy2(GLB, os.path.join(E, 'cube.glb')); shutil.copy2(MP3, os.path.join(E, 'kevin.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
