"""python build_unvault.py -> installs The Unvaulting (events/unvaulting) next to the other events: event.json, the six baked weapons (tools/bake_weapons.py -> cache/unvault), the vault prop and the music.
The whole show is coded in AstronomicalUnvault.cs (times are constants there); event.json only names the files and the section times."""
import json, os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); SRC = os.path.join(HERE, 'cache', 'unvault'); PROPS = os.path.join(HERE, 'cache', 'props')
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'unvaulting'); os.makedirs(E, exist_ok=True)
ITEMS = [('w0', 'zapotron'), ('w1', 'infinity_blade'), ('w2', 'fortnite_scar'), ('w3', 'fortnite_plane'), ('w4', 'tactical_submachine_gun'), ('w5', 'fortnite_-_drum_gun')]   # pillar order = vote index
MUSIC = os.path.join(HERE, 'cache', 'audio', 'unvault_loud.mp3')      # the original raised by 17 dB with tools/loud.py (it was at -34 dBFS rms)
for pid, name in ITEMS: shutil.copy2(os.path.join(SRC, name + '.glb'), os.path.join(E, pid + '.glb'))
shutil.copy2(os.path.join(PROPS, 'vault.glb'), os.path.join(E, 'vault.glb')); shutil.copy2(os.path.join(PROPS, 'zeropoint.glb'), os.path.join(E, 'zeropoint.glb'))
for _f in ('site', 'buildings', 'rim', 'volcano', 'pillar'): shutil.copy2(os.path.join(PROPS, _f + '.glb'), os.path.join(E, _f + '.glb'))
shutil.copy2(MUSIC, os.path.join(E, 'unvault.mp3'))
json.dump({'id': 'unvaulting', 'name': 'The Unvaulting (2019)', 'kind': 'unvault', 'music': 'unvault.mp3', 'lead': 0, 'length': 340.0,
           'bounds': [0, 15, 55, 240, 248, 266, 281, 308, 319, 328, 330, 340.0], 'names': ['Waiting', 'The Vault', 'Voting', 'The Winner Is Decided', 'The Item Breaks', 'The Volcano Explodes', 'The First Rock', 'The Second Rock', 'The Rock Splits', 'Tilted Towers Is Hit', 'The Final Rock'],
           'props': [{'id': pid, 'glb': pid + '.glb', 'lit': True} for pid, _ in ITEMS] + [{'id': 'vault', 'glb': 'vault.glb'}, {'id': 'zeropoint', 'glb': 'zeropoint.glb'}, {'id': 'site', 'glb': 'site.glb', 'lit': True}, {'id': 'buildings', 'glb': 'buildings.glb', 'lit': True}, {'id': 'rim', 'glb': 'rim.glb', 'lit': True}, {'id': 'volcano', 'glb': 'volcano.glb', 'lit': True}, {'id': 'pillar', 'glb': 'pillar.glb'}]}, open(os.path.join(E, 'event.json'), 'w'), indent=1)
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll'))
print('installed', sorted(os.listdir(E)), 'dll', os.path.getsize(dll) // 1024, 'KB')
