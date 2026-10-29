"""python install_concert.py -> installs Marshmello Showtime (AstronomicalAssets/events/marshmello): cues, event.json, the stage / visuals / dancers glbs and the music, plus the freshly built dll."""
import os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); M = os.path.join(HERE, 'cache', 'marsh'); P = os.path.join(HERE, 'cache', 'props')
DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'
E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'marshmello'); os.makedirs(E, exist_ok=True)
SRC = [('cues.json', os.path.join(M, 'cues.json')), ('event.json', os.path.join(M, 'event.json')), ('stage.glb', os.path.join(P, 'stage.glb')), ('visuals.glb', os.path.join(P, 'visuals.glb')),
       ('marsh_dj.glb', os.path.join(M, 'marsh_dj.glb')), ('marsh_dance.glb', os.path.join(M, 'marsh_dance_s.glb')), ('bear_a.glb', os.path.join(M, 'bear_a.glb')), ('bear_b.glb', os.path.join(M, 'bear_b.glb')), ('marshmello.mp3', r'C:\Users\drago\Downloads\Marshmello Holds First Ever Fortnite Concert Live at Pleasant Park.mp3')]
for f, src in SRC:
    if not os.path.exists(src): print('MISSING', src); continue
    shutil.copy2(src, os.path.join(E, f)); print('installed', f, os.path.getsize(os.path.join(E, f)) // 1024, 'KB')
dll = os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'); shutil.copy2(dll, os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll', os.path.getsize(dll) // 1024, 'KB')
