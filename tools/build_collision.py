"""python build_collision.py [install] -> Collision, milestone 1 (0:00 - 8:00): Blender cockpit / sword / Collider prep, event.json, install."""
import json, os, sys, shutil, subprocess
HERE = os.path.dirname(os.path.abspath(__file__)); D = os.path.join(HERE, 'cache', 'collision'); os.makedirs(D, exist_ok=True); DL = r'C:\Users\drago\Downloads'
MP3 = os.path.join(DL, 'Fortnite Collision Event - 4K.mp3'); BL = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'; DUR = 741.0
S = [(0, 'The Moon Base'), (95, 'The launch'), (115, 'Entering the atmosphere'), (191, 'Landing'), (200, 'The tanks and the barriers'), (251, 'The weapon hits'), (465, 'The Collider')]
S += [(477, 'The ground gives way'), (493, 'Underground'), (516, 'Below the Collider'), (582, 'Up the stairs'), (626, 'Slone'), (644, 'The mech hand'), (689, 'The Collider falls'), (711, 'Falling'), (735, 'To be continued')]
json.dump({'id': 'collision', 'name': 'Collision (Season 2 Finale, 2022) - part 1', 'kind': 'collision', 'music': 'collision.mp3', 'lead': 0, 'length': DUR, 'bounds': [s for s, _ in S] + [DUR], 'names': [n for _, n in S],
           'props': [{'id': k, 'glb': k + '.glb', 'lit': False, 'lazy': k in ('jones', 'foundation', 'guard', 'slone', 'cavern', 'zeropoint', 'crate', 'boulder', 'tbc', 'mushroom', 'rifle')} for k in ('cabin', 'moonbase', 'collider', 'mech', 'tank', 'dome', 'brute', 'sword', 'origin', 'asteroid', 'island', 'jones', 'foundation', 'guard', 'slone', 'cavern', 'zeropoint', 'crate', 'boulder', 'tbc', 'mushroom', 'rifle')]}, open(os.path.join(D, 'event.json'), 'w'), indent=1)
if not os.path.exists(os.path.join(D, 'origin.glb')): shutil.copy2(os.path.join(DL, 'the_origin_chapter_3_season_2.glb'), os.path.join(D, 'origin.glb'))
if len(sys.argv) > 1 and sys.argv[1] == 'install':
    DEST = r'C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod'; E = os.path.join(DEST, 'AstronomicalAssets', 'events', 'collision'); os.makedirs(E, exist_ok=True)
    for f in ('event.json', 'cabin.glb', 'moonbase.glb', 'collider.glb', 'mech.glb', 'tank.glb', 'dome.glb', 'brute.glb', 'sword.glb', 'origin.glb', 'asteroid.glb', 'island.glb', 'jones.glb', 'foundation.glb', 'guard.glb', 'slone.glb', 'cavern.glb', 'zeropoint.glb', 'crate.glb', 'boulder.glb', 'tbc.glb', 'mushroom.glb', 'rifle.glb'): shutil.copy2(os.path.join(D, f), E)
    shutil.copy2(MP3, os.path.join(E, 'collision.mp3')); print('assets installed')
    shutil.copy2(os.path.join(os.path.dirname(HERE), 'bin', 'Release', 'net472', 'JsLiveEventsMod.dll'), os.path.join(DEST, 'JsLiveEventsMod.dll')); print('dll installed')
