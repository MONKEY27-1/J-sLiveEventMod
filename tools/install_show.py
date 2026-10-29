"""python install_show.py  -> installs the baked show: backs up the current avatar GLB, copies travis_show_full.glb as travis_avatar.glb, writes travis_choreo.json
(move log + seat windows) next to it, copies the freshly built DLL. Does not touch any other plugin or the original Fortnite files."""
import os, json, shutil, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
A = os.path.join(DEST, 'AstronomicalAssets')
glb = os.path.join(HERE, 'cache', 'travis_show_full.glb')
assert os.path.getsize(glb) > 20e6, 'show glb missing or too small'
cur = os.path.join(A, 'travis_avatar.glb'); bak = os.path.join(A, 'travis_avatar.9clips-before-show.glb.bak')
if os.path.exists(cur) and not os.path.exists(bak): shutil.copy2(cur, bak); print('backed up', bak)
shutil.copy2(glb, cur); print('installed', cur, os.path.getsize(cur) // 1024, 'KB')
log = json.load(open(os.path.join(HERE, 'cache', 'choreo_log.json')))
json.dump({'log': [[a, b, n, s] for a, b, n, s in log['log']], 'events': log['events']}, open(os.path.join(A, 'travis_choreo.json'), 'w'))
print('choreo json:', len(log['log']), 'moves,', len([e for e in log['events'] if e[2] == 'seat']), 'seat windows')
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll')
shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')

for f in ('coaster_track.glb','coaster_cart.glb','coaster_path.json'): shutil.copy2(os.path.join(HERE,'cache','coaster',f), os.path.join(A,f)); print('installed',f)
