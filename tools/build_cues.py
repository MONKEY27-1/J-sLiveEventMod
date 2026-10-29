"""python build_cues.py -> cache/showdown/cues.json (what the plugin does besides playing the two baked clips) + event.json.
Needs monster_trace.json / monster_beam.json (real_monster.py bake) and robot_plan.json (show_robot.py)."""
import json, os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sd_common as C
import monster_events as ME
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache', 'showdown')
PL = json.load(open(os.path.join(OUT, 'robot_plan.json'))); META = json.load(open(os.path.join(OUT, 'monster_meta.json')))
ev = []; win = []
def E(t, type, **k): d = {'t': round(t, 2), 'type': type}; d.update(k); ev.append(d)
def W(t0, t1, type, **k): d = {'t0': round(t0, 2), 't1': round(t1, 2), 'type': type}; d.update(k); win.append(d)
def ground(t):
    p = ME.at('pelvis', t); return [round(p[0], 1), 0.0, round(p[2], 1)]
R0, R1, ORB, SW = PL['R0'], PL['R1'], PL['orb'], PL['sw']

# --- the monster arrives (real clip): roars where its jaw swings open, the green beam wherever the mouth is wide open and points at / over the ground
for t in ME.roars():
    if 38.0 < t < 296.0: E(t, 'roar', a='monster', size=1.0)
for a, b, pts in ME.beam_windows(40.0, 92.0):
    W(a, b, 'beam', frm='monster', pts=pts, width=1.0)
    g = pts[-1]
    if g[2] == 0.0 and abs(g[1]) < 260: E(b, 'boom', at=[g[1], 0, g[3]], size=24, color='green')
# the two big blasts at the beat
for t in C.T_BLAST:
    i = int(round(t / ME.DT)); g, hit = ME.ground_hit(i)
    if hit and abs(g[0]) < 300: E(t, 'boom', at=[round(g[0], 1), 0, round(g[2], 1)], size=34, color='green')
# --- the robot wakes
E(C.T_ACTIVATE, 'eyes', on=True); W(86.0, 94.0, 'jets', actor='robot', bones=['foot_l', 'foot_r', 'spine_03'], power=1.4)
E(C.T_ACTIVATE, 'flash', at='robot:head_offset', size=40, color='cyan')
E(88.5, 'dust', at=[R0[0], 0, R0[1]], size=3.0)
# --- the first battle: three volleys of missiles hit the monster as it is thrown skywards and dives into the ground
for tm in C.T_MISSILE: E(tm - 1.1, 'missiles', n=4, fly=1.1, frm='robot', to='monster:spine_03', size=18)
hs = ME.hidden_spans()
for a, b in hs: E(a + 0.2, 'hide', a='monster'); E(b - 0.3, 'show', a='monster')
if hs: E(hs[0][0] + 0.1, 'boom', at=ground(hs[0][0] - 0.4), size=40, color='orange'); E(hs[0][0] + 0.1, 'dust', at=ground(hs[0][0] - 0.4), size=8.0)
# a beam erupts from the ground under the robot and knocks it flat
W(C.T_BEAM_HIT, C.T_BEAM_HIT + 2.4, 'beam', frmpos=[R1[0], -40, R1[1]], to='robot:spine_03', width=2.6)
E(C.T_BEAM_HIT + 0.2, 'boom', at='robot:spine_03', size=34, color='green')
E(139.0, 'dust', at='robot:pelvis', size=3.0)
# --- the ambush: the monster bursts out of the ground, lands on the robot, tears its arm off
if hs: E(hs[0][1], 'boom', at=ground(hs[0][1] + 0.3), size=46, color='orange'); E(hs[0][1], 'dust', at=ground(hs[0][1] + 0.3), size=9.0)
E(C.T_LAND, 'boom', at=ground(C.T_LAND + 0.2), size=48, color='orange'); E(C.T_LAND, 'dust', at=ground(C.T_LAND + 0.2), size=8.0)
E(C.T_TEAR, 'armtear'); E(C.T_TEAR, 'boom', at='robot:upperarm_r', size=22, color='orange')
for t in C.PEAKS_BATTLE[1:]: E(t, 'boom', at='robot:pelvis', size=10, color='orange')
# the robot's palm blast throws the monster away
W(C.T_KICK - 0.2, C.T_KICK + 0.6, 'beam', frmb='robot:hand_l', to='monster:spine_03', width=1.6, color='cyan')
E(C.T_KICK + 0.1, 'boom', at='monster:spine_03', size=30, color='orange'); E(C.T_KICK + 0.1, 'flash', at='robot:hand_l', size=30, color='cyan')
# --- the energy source
W(184.0, C.T_FLASH, 'orb', pos=list(ORB), radius=14.0)
W(198.0, C.T_FLASH, 'absorb', frm='orb', to='robot:hand_l')
E(C.T_FLASH, 'flash', at=list(ORB), size=70, color='white'); E(C.T_FLASH, 'boom', at='robot:spine_03', size=38, color='cyan')
W(C.T_FLASH, 296.0, 'aura', actor='robot', color='orange')
# --- the monster leaps, the powered punch meets it, it crashes down
E(C.T_PUNCH, 'boom', at='monster:spine_03', size=46, color='white'); E(C.T_PUNCH, 'dust', at='monster:spine_03', size=5.0)
E(247.5, 'boom', at=ground(247.6), size=34, color='orange'); E(247.5, 'dust', at=ground(247.6), size=7.0)
# --- the hidden sword
E(206.6, 'flash', at=[SW[0], 5, SW[2]], size=45, color='cyan'); E(206.6, 'boom', at=[SW[0], 2, SW[2]], size=28, color='cyan'); E(206.6, 'dust', at=[SW[0], 0, SW[2]], size=6.0)      # the ground cracks open and the sword rises out of it
W(207.0, C.T_SWORD, 'sword', mode='planted', pos=[SW[0], 0.0, SW[2]])
E(C.T_SWORD - 0.3, 'boom', at=[SW[0], 2, SW[2]], size=22, color='cyan')
W(C.T_SWORD, 296.0, 'sword', mode='held', bone='robot:hand_l', forearm='robot:lowerarm_l')
# --- the final strike
W(256.4, C.T_STAB, 'jets', actor='robot', bones=['foot_l', 'foot_r'], power=0.9)
E(C.T_STAB, 'flash', at='monster:eye', size=90, color='white'); E(C.T_STAB, 'boom', at='monster:eye', size=52, color='cyan'); E(C.T_STAB, 'eyeout')
W(262.0, 300.0, 'smoke', actor='monster', bone='pelvis', size=7.0)
E(262.3, 'roar', a='monster', size=1.4)                                           # its last roar
W(266.0, 300.0, 'decay', actor='monster')                                          # green energy pours from the eye, the body crumbles to dust and shrinks away
E(300.4, 'hide', a='monster')
W(C.T_TEAR + 2.0, 303.5, 'smoke', actor='robot', bone='upperarm_r', size=2.0)
# --- victory and departure
W(286.0, 296.0, 'jets', actor='robot', bones=['foot_l', 'foot_r', 'spine_03'], power=2.2)
E(C.T_FLY, 'boom', at='robot:pelvis', size=18, color='cyan')
W(C.T_FLY + 0.4, 296.0, 'trail', actor='robot')                                      # light trail behind the flight
E(C.T_FLY + 2.2, 'sonic', at='robot:pelvis', size=60)                               # sonic boom as it breaks away
E(296.0, 'hide', a='robot')
# the arena the plugin fits onto the Forest's real floor: every spot the giants stand on (the monster's far start before 38 s is left out: it may arrive from beyond the edge)
rt_ = json.load(open(os.path.join(OUT, 'robot_trace.json'))); pts = []
for t in range(38, 296, 4):
    if not any(a <= t <= b for a, b in hs): m = ME.at('pelvis', t); pts.append([round(m[0]), round(m[2])])
for t in range(84, 296, 4): r = rt_['b']['pelvis'][int(t / rt_['dt'])]; pts.append([round(r[0]), round(r[2])])
pts += [[round(ORB[0]), round(ORB[2])], [round(SW[0]), round(SW[2])], [round(R0[0]), round(R0[1])]]
ARENA = {'pts': [list(p) for p in sorted(set(map(tuple, pts)))], 'margin': 40}
ev.sort(key=lambda d: d['t'])
json.dump({'events': ev, 'windows': win, 'arena': ARENA}, open(os.path.join(OUT, 'cues.json'), 'w'), separators=(',', ':'))
json.dump({'id': 'finalshowdown', 'name': 'The Final Showdown', 'kind': 'showdown', 'music': 'showdown.mp3', 'lead': 0, 'length': C.DUR,
           'bounds': [0.0, 47.0, 81.5, 116.5, 124.6, 135.4, 156.3, 162.2, 181.1, 184.0, 205.3, 206.6, 245.6, 260.0, 266.0, 286.4, 296.0, 303.5], 'names': ['The Monster Arrives', 'The Forest Is Threatened', 'The Robot Awakens', 'The First Battle', 'Missiles Hit the Monster', 'The Beam Under the Robot', 'The Ambush', 'The Arm Is Torn Off', 'The Palm Blast', 'The Zero Point', 'The Robot Absorbs It', 'The Hidden Sword', 'The Power Punch', 'The Final Strike', 'The Monster Dies', 'Victory', 'Departure'], 'cues': 'cues.json', 'sword': 'sword.glb',
           'plateau': 0.0, 'props': [{'id': 'zeropoint', 'glb': 'zeropoint.glb'}, {'id': 'vault', 'glb': 'vault.glb'}],
           'actors': [{'id': 'monster', 'glb': 'monster.glb', 'height': round(META['height'], 2), 'ankle': 9.0}, {'id': 'robot', 'glb': 'robot.glb', 'height': round(C.ROBOT_H * C.ROBOT_JB / C.ROBOT_BODY, 2), 'ankle': 5.0}]},
          open(os.path.join(OUT, 'event.json'), 'w'), indent=1)
print(len(ev), 'events', len(win), 'windows; hidden', [(round(a, 1), round(b, 1)) for a, b in hs])
