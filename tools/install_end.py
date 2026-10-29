"""python install_end.py -> installs The End (2019) as an extra event (AstronomicalAssets/events/theend) and the freshly built dll."""
import os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); SD = os.path.join(HERE, 'cache', 'end')
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'theend'); os.makedirs(E, exist_ok=True)
for f, src in (('rocket.glb', os.path.join(SD, 'rocket.glb')), ('meteor.glb', os.path.join(SD, 'meteor.glb')), ('cues.json', os.path.join(SD, 'cues.json')), ('event.json', os.path.join(SD, 'event.json')),
               ('theend.mp3', r'C:\Users\drago\Downloads\Fortnite The End Live Event FULL REPLAY! Chapter 1 (Black Hole).mp3')):
    assert os.path.exists(src), src
    shutil.copy2(src, os.path.join(E, f)); print('installed', f, os.path.getsize(os.path.join(E, f)) // 1024, 'KB')
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
