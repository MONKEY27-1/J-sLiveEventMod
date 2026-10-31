"""python build_end21.py [install] -> THE END (Chapter 2 finale, 4 Dec 2021): event.json from the user's timestamps (video 11:04) and install.
Models come from tools/cache/theend2021 (prepared with shrink_glb.py / make_end21.py)."""
import json, os, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'theend2021'); DL = r'C:\Users\drago\Downloads'
MP3 = os.path.join(DL, 'The End - Chapter 2 Finale (Full In-game Event Video).mp3'); DUR = 664.0
S = [(0, 'The blue cube and its force field'), (50, 'The sky darkens'), (85, 'Lightning and the beam'), (95, 'The Cube Queen opens the rift'), (125, 'The Last Reality fleet'), (160, 'The Cube monsters'), (195, 'The large enemy'), (225, 'The abductors'), (255, 'The shield is attacked'), (270, 'The I.O. facility'),
     (296, 'The Foundation breaks in'), (332, 'The hallways'), (356, 'The Zero Point machinery'), (400, 'The Seven rescue you'), (430, 'Down the shaft'), (435, 'The underground base'), (508, 'The Zero Point chamber'), (545, 'The Caretaker'), (555, 'The ocean'), (605, 'The island flips'), (630, 'Chapter 3'), (660, 'THE END')]
LAZY = {'facility': 'facility.glb', 'hall': 'w_hall.glb', 'gyro': 'w_gyro.glb', 'hatch': 'w_hatch.glb', 'base': 'w_base.glb', 'chapter3': 'chapter_3.glb', 'jones': 'jones.glb', 'foundation': 'foundation.glb', 'slone': 'slone.glb', 'guard': 'guard.glb', 'rifle': 'rifle.glb', 'zeropoint': 'zeropoint.glb', 'gunnar': 'gunnar.glb', 'scientist': 'the_scientist.glb', 'visitor': 'the_visitor.glb'}   # loaded in waves after the start
COLL = os.path.join(HERE, 'cache', 'collision')
for f in ('jones', 'foundation', 'slone', 'guard', 'rifle', 'zeropoint'): shutil.copy2(os.path.join(COLL, f + '.glb'), os.path.join(D, f + '.glb'))
FILES = {'bluecube': 'bluecube.glb', 'caretaker': 'caretaker.glb', 'cube_queen': 'cube_queen.glb', 'mothership': 'mothership.glb', 'abductor': 'abductor.glb', 'cradle': 'the_cubes_cradle.glb', 'pyramid': 'cube_pyramid.glb'}
if not os.path.exists(os.path.join(D, 'bluecube.glb')):
    shutil.copy2(r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\events\kevincube\cube.glb', os.path.join(D, 'bluecube.glb'))
json.dump({'id': 'end21', 'name': 'THE END (Chapter 2 Finale, 2021)', 'kind': 'end21', 'music': 'end21.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S],
           'props': [{'id': k, 'glb': v, 'lit': False} for k, v in FILES.items()] + [{'id': k, 'glb': v, 'lit': False, 'lazy': True} for k, v in LAZY.items()]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'end21'); os.makedirs(E, exist_ok=True)
    for f in ['event.json'] + list(FILES.values()) + list(LAZY.values()): shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'end21.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
