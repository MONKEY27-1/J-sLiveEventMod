"""python install_props.py -> copies the Blender-made props (cache/props/zeropoint.glb, blackhole.glb; tools/make_zeropoint.py, make_blackhole.py) into the events that use them and
adds them to those events' event.json ("props"): The End (Zero Point + black hole) and The Final Showdown (its Zero Point, the energy orb). Run again after rebuilding the glbs."""
import json, os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); SRC = os.path.join(HERE, 'cache', 'props')
EV = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\events'
for ev, props in (('theend', ('zeropoint', 'blackhole', 'vault')), ('finalshowdown', ('zeropoint', 'vault'))):
    d = os.path.join(EV, ev); jf = os.path.join(d, 'event.json'); j = json.load(open(jf))
    j['props'] = [{'id': p, 'glb': p + '.glb'} for p in props]; json.dump(j, open(jf, 'w'), indent=1)
    for p in props: shutil.copy2(os.path.join(SRC, p + '.glb'), os.path.join(d, p + '.glb')); print(ev, 'installed', p, os.path.getsize(os.path.join(d, p + '.glb')) // 1024, 'KB')
# keep the generators' own event.json in step so a rebuild does not drop the props
for ev, f in (('end', 'build_end.py'),):
    p = os.path.join(HERE, f); s = open(p, encoding='utf-8').read()
    if "'props'" not in s: s = s.replace("'cues': 'cues.json', 'plateau': 0.0,", "'cues': 'cues.json', 'plateau': 0.0, 'props': [{'id': 'zeropoint', 'glb': 'zeropoint.glb'}, {'id': 'blackhole', 'glb': 'blackhole.glb'}, {'id': 'vault', 'glb': 'vault.glb'}],", 1); open(p, 'w', encoding='utf-8').write(s)
p = os.path.join(HERE, 'build_cues.py'); s = open(p, encoding='utf-8').read()
if "'props'" not in s: s = s.replace("'plateau': 0.0,", "'plateau': 0.0, 'props': [{'id': 'zeropoint', 'glb': 'zeropoint.glb'}],", 1); open(p, 'w', encoding='utf-8').write(s)
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(EV, '..', '..', 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
