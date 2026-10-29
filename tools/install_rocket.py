"""python install_rocket.py -> installs Rocket Launch (2018) as an extra event (AstronomicalAssets/events/rocketlaunch) and the freshly built dll."""
import os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); SD = os.path.join(HERE, 'cache', 'rocket')
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'rocketlaunch'); os.makedirs(E, exist_ok=True)
for f, src in (('rocket.glb', os.path.join(SD, 'rocket.glb')), ('cues.json', os.path.join(SD, 'cues.json')), ('event.json', os.path.join(SD, 'event.json')),
               ('rocket.mp3', r'C:\Users\drago\Downloads\Fortnite Rocket Launch - Season 4.mp3')):
    assert os.path.exists(src), src
    shutil.copy2(src, os.path.join(E, f)); print('installed', f, os.path.getsize(os.path.join(E, f)) // 1024, 'KB')
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
