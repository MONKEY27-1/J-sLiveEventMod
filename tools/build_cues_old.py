"""python build_cues.py -> cache/showdown/cues.json (what the plugin does besides playing the two baked clips) + event.json"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sd_common as C
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache', 'showdown')
ev = []; win = []
def E(t, type, **k): d = {'t': round(t, 2), 'type': type}; d.update(k); ev.append(d)
def W(t0, t1, type, **k): d = {'t0': round(t0, 2), 't1': round(t1, 2), 'type': type}; d.update(k); win.append(d)

# --- the monster arrives and roars
for t in (C.T_ROAR1, 92.2, 140.0, 162.2, C.T_LEAP, 260.4): E(t, 'roar', a='monster', size=1.0 if t != C.T_LEAP else 1.5)
E(C.T_ROAR1 - 1, 'dust', at='monster:foot_l', size=1.0)
# --- the beam sweeps the rim, then two blasts into the pit
W(61.0, 72.0, 'beam', frm='monster', pts=[[61, -38, -12, 48], [66.5, 0, -12, 50], [72, 38, -12, 48]], width=1.0)
W(73.7, 75.4, 'beam', frm='monster', pts=[[73.7, 6, -22, 12], [75.4, 6, -22, 12]], width=2.2)
W(76.5, 78.1, 'beam', frm='monster', pts=[[76.5, -6, -22, -4], [78.1, -6, -22, -4]], width=2.2)
E(C.T_BLAST[0], 'boom', at=[6, -22, 12], size=34, color='green'); E(C.T_BLAST[1], 'boom', at=[-6, -22, -4], size=38, color='green')
for t in (66.0, 69.0): E(t, 'boom', at=[-10 + (t - 66) * 8, -12, 48], size=14, color='green')
# --- the robot wakes
E(C.T_ACTIVATE, 'eyes', on=True); W(86.0, 94.0, 'jets', actor='robot', bones=['foot_l', 'foot_r', 'spine_03'], power=1.4)
E(C.T_ACTIVATE, 'flash', at='robot:head_offset', size=40, color='cyan')
E(88.5, 'dust', at=[100, 0, 70], size=3.0)
# --- the first battle: three volleys of missiles
for i, tm in enumerate(C.T_MISSILE): E(tm - 1.1, 'missiles', n=4, fly=1.1, frm='robot', to='monster:spine_03', size=18)
W(135.4, 138.0, 'beam', frm='monster', to='robot:spine_03', width=2.6)
E(135.6, 'boom', at='robot:spine_03', size=34, color='green')
E(139.0, 'dust', at='robot:pelvis', size=3.0)
E(C.T_TACKLE, 'boom', at=[40, 30, 102], size=30, color='orange'); E(C.T_TACKLE, 'dust', at=[40, 0, 102], size=4.0)
E(C.T_TEAR, 'armtear'); E(C.T_TEAR, 'boom', at='robot:upperarm_r', size=22, color='orange')
for t in C.PEAKS_BATTLE: E(t, 'boom', at='robot:pelvis', size=10, color='orange')
E(C.T_KICK, 'boom', at='monster:pelvis', size=18, color='orange')
# --- the energy source
W(184.0, C.T_FLASH, 'orb', pos=list(C.ORB), radius=14.0)
W(198.0, C.T_FLASH, 'absorb', frm='orb', to='robot:hand_l')
E(C.T_FLASH, 'flash', at=list(C.ORB), size=70, color='white'); E(C.T_FLASH, 'boom', at='robot:spine_03', size=38, color='cyan')
W(C.T_FLASH, 296.0, 'aura', actor='robot', color='orange')
E(C.T_PUNCH, 'boom', at='monster:spine_03', size=46, color='white'); E(C.T_PUNCH, 'dust', at='monster:spine_03', size=5.0)
E(C.T_LAND, 'boom', at=[-42, 0, 168], size=34, color='orange'); E(C.T_LAND, 'dust', at=[-42, 0, 168], size=6.0)
# --- the hidden sword
W(222.0, C.T_SWORD, 'sword', mode='planted', pos=list(C.SWORD))
E(C.T_SWORD - 0.3, 'boom', at=[C.SWORD[0], 2, C.SWORD[2]], size=22, color='cyan')
W(C.T_SWORD, 303.5, 'sword', mode='held', bone='robot:weapon_l', forearm='robot:lowerarm_l')
# --- the final strike
W(254.0, C.T_STAB, 'jets', actor='robot', bones=['foot_l', 'foot_r'], power=0.9)
E(C.T_STAB, 'flash', at='monster:eye', size=90, color='white'); E(C.T_STAB, 'boom', at='monster:eye', size=52, color='cyan'); E(C.T_STAB, 'eyeout')
E(270.6, 'dust', at=[-52, 0, 128], size=8.0); E(270.6, 'boom', at=[-52, 0, 128], size=26, color='orange')
W(262.0, 303.5, 'smoke', actor='monster', bone='pelvis', size=7.0)
W(C.T_TEAR + 2.0, 303.5, 'smoke', actor='robot', bone='upperarm_r', size=2.0)
# --- victory and departure
W(286.0, 296.0, 'jets', actor='robot', bones=['foot_l', 'foot_r', 'spine_03'], power=2.2)
E(C.T_FLY, 'boom', at='robot:pelvis', size=18, color='cyan')
E(296.0, 'hide', a='robot')
ev.sort(key=lambda d: d['t'])
json.dump({'events': ev, 'windows': win}, open(os.path.join(OUT, 'cues.json'), 'w'), separators=(',', ':'))
json.dump({'id': 'finalshowdown', 'name': 'The Final Showdown', 'kind': 'showdown', 'music': 'showdown.mp3', 'lead': 0, 'length': C.DUR,
           'bounds': C.SEQ, 'names': C.SEQ_NAMES, 'cues': 'cues.json', 'sword': 'sword.glb',
           'plateau': 24.0,
           'actors': [{'id': 'monster', 'glb': 'monster.glb', 'height': C.MONSTER_H, 'ankle': 11.0}, {'id': 'robot', 'glb': 'robot.glb', 'height': C.ROBOT_H, 'ankle': 5.0}]},
          open(os.path.join(OUT, 'event.json'), 'w'), indent=1)
print(len(ev), 'events', len(win), 'windows')
