"""python build_device.py -> cache/device/{cues.json,event.json};  python build_device.py install  also copies everything (and the dll) into the game's plugin folder (events/thedevice).
The moments of the event are the user's reading of the replay and live in AstronomicalDevice.cs; this file only gives the beat grid / loudness (for the orb and the lightning) and the Skip / Next sections."""
import json, os, sys, shutil
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'device')
A = json.load(open(os.path.join(D, 'audio.json'))); DUR = round(A['duration'], 2); r = np.array(A['rms'], np.float32); r = r / r.max()
beats = [b for b in A['beats'] if b >= 0]; dense = []
for x, y in zip(beats[:-1], beats[1:]):
    k = max(1, int(round((y - x) / 0.6))); dense += [x + (y - x) * q / k for q in range(k)]
beats = dense + [beats[-1]]
energy = [round(float(r[int(t * 30):int(t * 30) + 30].mean()), 2) if int(t * 30) < len(r) else 0.0 for t in np.arange(0, DUR, 0.5)]
json.dump({'beats': [round(b, 3) for b in beats], 'energy': energy}, open(os.path.join(D, 'cues.json'), 'w'), separators=(',', ':'))
S = [(0, 'Rods rise'), (50, 'All rods up'), (71, 'The Agency explodes'), (107, 'The Device at full height'), (111, 'The orb rises'), (130, 'First lightning'), (160, 'Office'), (176, 'Back on the island'), (193, 'The island is unstable'),
     (218, 'Lightning returns'), (256, 'Storm pushed back'), (288, 'Pylons retract'), (302, 'Office 2'), (328, 'The water storm'), (363, 'Office 3'), (385, 'White'), (398, 'The ruins')]
json.dump({'id': 'thedevice', 'name': 'The Device (2020)', 'kind': 'device', 'music': 'device.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S], 'cues': 'cues.json',
           'props': [{'id': p, 'glb': g, 'lit': True} for p, g in (('agency', 'agency.glb'), ('office', 'office.glb'), ('device', 'device.glb'), ('tentacle', 'tentacle.glb'), ('tower', 'tower.glb'), ('cyclo', 'cyclo.glb'), ('jones', 'jones.glb'))]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
print(len(beats), 'beats, duration', DUR)
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'thedevice'); os.makedirs(E, exist_ok=True)
    for f, src in (('cues.json', 'cues.json'), ('event.json', 'event.json'), ('agency.glb', 'agency2.glb'), ('office.glb', 'office.glb'), ('device.glb', 'device.glb'), ('tentacle.glb', 'tentacle.glb'), ('tower.glb', 'tower.glb'), ('cyclo.glb', 'cyclo_c.glb'), ('jones.glb', 'jones_c.glb')):
        shutil.copy2(os.path.join(D, src), os.path.join(E, f)); print('installed', f, os.path.getsize(os.path.join(E, f)) // 1024, 'KB')
    shutil.copy2(r'C:\Users\drago\Downloads\The Device Event Full Cinematic in 4K - Fortnite.mp3', os.path.join(E, 'device.mp3')); print('installed device.mp3')
    dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
