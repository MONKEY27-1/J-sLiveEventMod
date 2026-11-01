"""python build_skyfire.py [install] -> Operation: Sky Fire (12 Sep 2021): event.json from the user's recording (10:09) and install.
Models come from tools/cache/skyfire (tools/skyfire_world.py: interior / view / props; skyfire_ufo.py: ufo; the Mothership, Jones, Foundation and the guard are copied)."""
import json, os, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'skyfire'); DL = r'C:\Users\drago\Downloads'
MP3 = os.path.join(DL, 'Operation Sky Fire (Full In-Game Event Video).mp3'); DUR = 609.3
S = [(0, 'Fade in'), (5, 'The beam'), (25, 'The cells'), (65, 'The hall'), (115, 'The walkway'), (165, 'Transport tube'), (180, 'The machinery'), (225, 'Transport tube'), (245, 'The deck'), (285, 'The purple chamber'),
     (360, 'The deck'), (410, 'The Cube chamber'), (426, 'The reboot'), (475, 'The deck'), (495, 'The blue Cube'), (510, 'The crystal hall'), (551, 'The backpacks go off'), (570, 'The fall'), (604, 'Operation: Sky Fire')]
FILES = ['interior', 'view', 'props', 'ufo', 'mothership', 'cube', 'jones', 'foundation', 'guard']
json.dump({'id': 'skyfire', 'name': 'Operation: Sky Fire (2021)', 'kind': 'skyfire', 'music': 'skyfire.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S],
           'props': [{'id': k, 'glb': k + '.glb', 'lit': False} for k in FILES]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'skyfire'); os.makedirs(E, exist_ok=True)
    for f in ['event.json'] + [k + '.glb' for k in FILES]: shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'skyfire.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
