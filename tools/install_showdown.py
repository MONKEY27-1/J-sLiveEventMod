"""python install_showdown.py -> installs The Final Showdown as an extra event (AstronomicalAssets/events/finalshowdown) next to the Travis event."""
import os, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
HERE = os.path.dirname(os.path.abspath(__file__)); SD = os.path.join(HERE, 'cache', 'showdown')
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'finalshowdown'); os.makedirs(E, exist_ok=True)
for f, src in (('monster.glb', os.path.join(SD, 'monster.glb')), ('robot.glb', os.path.join(SD, 'robot.glb')), ('sword.glb', os.path.join(SD, 'sword.glb')), ('cues.json', os.path.join(SD, 'cues.json')),
               ('event.json', os.path.join(SD, 'event.json')), ('showdown.mp3', r'C:\Users\drago\Downloads\youtube-audio.mp3')):
    assert os.path.exists(src), src
    if f in ('monster.glb', 'robot.glb'):                       # drop the maps the plugin never reads (normal / occlusion / ...) and orphaned buffer data
        g, b = slim_glb.read_glb(src); g, b = slim_glb.slim(g, b); slim_glb.write_glb(os.path.join(E, f), g, b)
    else: shutil.copy2(src, os.path.join(E, f))
    print('installed', f, os.path.getsize(os.path.join(E, f)) // 1024, 'KB')
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
