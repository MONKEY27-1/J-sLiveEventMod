"""python build_end.py -> cache/end/{rocket.glb,meteor.glb,cues.json,event.json}; python install_end.py installs them as events/theend.
The big moments are pinned to the times read off the replay (AstronomicalEnd.cs has the same numbers: T_*), the small ones (pulses / lightning) to the audio hits
found by analyze_audio.py in the soundtrack (cache/end/audio.json)."""
import json, math, os, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'end'); DUR = 309.7
for name, src in (('rocket.glb', os.path.join(HERE, 'cache', 'rocket', 'rocket.glb')), ('meteor.glb', os.path.join(OUT, 'meteor.glb'))):
    g, b = slim_glb.read_glb(src); g, b = slim_glb.slim(g, b); slim_glb.write_glb(os.path.join(OUT, name), g, b)
A = json.load(open(os.path.join(OUT, 'audio.json'))); r = A['rms']; mr = max(r); rr = [x / mr for x in r]
hits = []
for i in range(6, len(rr) - 3):
    rise = rr[i + 2] - min(rr[i - 6:i])
    if rise > 0.12 and rr[i] >= max(rr[i - 3:i + 4]): hits.append((i / 30.0, rise))
m = []
for h in hits:
    if m and h[0] - m[-1][0] < 0.8:
        if h[1] > m[-1][1]: m[-1] = h
    else: m.append(h)
hits = [(round(t, 2), round(s, 2)) for t, s in m]
ev = []
def E(t, type, **k): d = {'t': round(t, 2), 'type': type}; d.update(k); ev.append(d)
T_IGN, T_LIFT, T_RIFT1, T_ENTER1, T_SEVEN, T_SEVEN_IN, T_AGAIN, T_METEOR, T_GONE = 20, 24, 55, 58, 88, 132, 146, 169, 175
T_HIGH, T_DIVE1, T_SKY, T_DIVE6, T_HIT, T_BACK, T_PULL, T_BLACK, T_BH = 186, 193, 195, 208, 237, 241, 250, 277, 296
def dive(i): return T_DIVE1 + 9 if i == 0 else T_DIVE6 + 8 + 1.1 * (i - 1)
# 0:20 the rocket launches
E(T_IGN - 0.1, 'flash', at='pad', size=60, color='orange'); E(T_IGN, 'boom', at='pad', size=40, color='orange')
E(T_LIFT - 0.05, 'boom', at='pad', size=70, color='orange'); E(T_LIFT, 'dust', at='pad', size=14.0); E(T_LIFT, 'shock', kind='ground', at='pad', size=260, color='orange'); E(T_LIFT + 0.5, 'dust', at='pad', size=10.0)
# 0:55 the first rift; 0:58 the rocket flies into it
E(T_RIFT1 - 0.05, 'flash', at='rift', size=90, color='white'); E(T_RIFT1, 'boom', at='rift', size=50, color='violet'); E(T_RIFT1, 'shock', kind='sky', at='rift', size=500, color='violet')
E(T_ENTER1 - 0.2, 'flash', at='rift', size=140, color='white'); E(T_ENTER1, 'boom', at='rift', size=70, color='white')
# the other rifts open as the rocket reappears
for k, tt in enumerate((62, 70, 75.5, 81, 84.5, 86.5)): E(tt, 'flash', at='r%d' % (k + 2), size=60, color='violet')
# 1:28 seven rockets; 2:12 they all fly into one rift
E(T_SEVEN, 'flash', at='r2', size=140, color='white'); E(T_SEVEN, 'shock', kind='sky', at='rift', size=700, color='white'); E(T_SEVEN, 'boom', at='rift', size=60, color='violet')
E(T_SEVEN_IN, 'flash', at='rift', size=160, color='white'); E(T_SEVEN_IN, 'boom', at='rift', size=90, color='white'); E(T_SEVEN_IN, 'shock', kind='sky', at='rift', size=800, color='violet')
# 2:26 they run into it again, it gets unstable; 2:49 the meteor moves into it, 2:55 it is gone
E(T_AGAIN, 'flash', at='rift', size=110, color='violet')
E(T_METEOR, 'flash', at='meteor', size=120, color='cyan'); E(T_METEOR, 'boom', at='meteor', size=60, color='cyan'); E(T_METEOR, 'shock', kind='sky', at='rift', size=900, color='cyan')
E(T_GONE - 0.5, 'flash', at='rift', size=320, color='white'); E(T_GONE - 0.4, 'boom', at='rift', size=130, color='violet'); E(T_GONE - 0.4, 'shock', kind='sky', at='rift', size=1300, color='white')
# 3:06 the rift over the Zero Point (its bubble breaks); 3:13 the first rocket dives; 3:15 we enter the sky; 3:28 six rockets and the meteor dive
E(T_HIGH, 'shatter', at='zp', size=120, color='cyan'); E(T_HIGH, 'shock', kind='sky', at='rifth', size=800, color='cyan')
E(T_DIVE1, 'flash', at='rifth', size=90, color='white')
E(T_SKY - 0.2, 'flash', at='rifth', size=320, color='white'); E(T_SKY, 'shock', kind='sky', at='rifth', size=1300, color='white'); E(T_SKY, 'boom', at='rifth', size=140, color='cyan')
E(T_DIVE6, 'flash', at='rifth', size=220, color='white'); E(T_DIVE6, 'shock', kind='sky', at='rifth', size=1000, color='violet'); E(T_DIVE6, 'boom', at='rifth', size=100, color='violet')
t = T_DIVE1 + 6; E(t, 'zphit', size=80, color='cyan'); E(t, 'boom', at='zp', size=44, color='cyan'); E(t, 'shock', kind='ground', at='zp', size=200, color='cyan')   # the first rocket dives in; the other six guide the meteor and fly away
# 3:57 the meteor hits the Zero Point; 4:01 you are pulled back; 4:10 the black hole pushes things in
E(T_HIT - 0.15, 'flash', at='zp', size=420, color='white'); E(T_HIT, 'boom', at='zp', size=230, color='orange'); E(T_HIT, 'shock', kind='ground', at='zp', size=800, color='white')
E(T_HIT, 'shock', kind='sky', at='rifth', size=1300, color='white'); E(T_HIT, 'dust', at='zp', size=20.0); E(T_HIT + 0.35, 'boom', at='zp', size=160, color='white'); E(T_HIT + 1.0, 'dust', at='zp', size=16.0)
E(T_BACK, 'shock', kind='ground', at='zp', size=900, color='white'); E(T_BACK, 'boom', at='zp', size=120, color='cyan')
E(T_PULL, 'flash', at='bh', size=160, color='white'); E(T_PULL, 'shock', kind='ground', at='zp', size=500, color='violet')
# audio hits: a pulse on every one, a bolt of lightning on the strong ones while the rifts are unstable / the sky is open
for t, s in hits:
    if T_RIFT1 < t < T_BLACK - 1 and s >= 0.12: E(t, 'pulse', s=s)
    if T_AGAIN < t < T_GONE - 1 and s >= 0.18: E(t, 'bolt', s=s, color='white', **{'from': 'main'})
    if T_SKY + 1 < t < T_HIT - 0.5 and s >= 0.18: E(t, 'bolt', s=s, color='cyan' if s < 0.3 else 'white', **{'from': 'rifth'})
    if T_HIT + 3 < t < T_BLACK - 2 and s >= 0.2: E(t, 'bolt', s=s, color='violet', **{'from': 'rifth'})
ev.sort(key=lambda d: d['t'])
PAD = (-45, 115); ZP = (30, 150)
pts = [[0, 0]] + [[PAD[0], PAD[1]], [ZP[0], ZP[1]]] + [[round(c[0] + 45 * math.cos(a * 0.7854)), round(c[1] + 45 * math.sin(a * 0.7854))] for c in (PAD, ZP) for a in range(8)]
json.dump({'events': ev, 'windows': [], 'arena': {'pts': pts, 'margin': 24}}, open(os.path.join(OUT, 'cues.json'), 'w'), separators=(',', ':'))
bounds = [0, 20, 24, 55, 58, 68, 88, 132, 146, 169, 175, 186, 193, 195, 208, 237, 241, 250, 277, 296] + [DUR]
json.dump({'id': 'theend', 'name': 'The End (2019)', 'kind': 'end', 'music': 'theend.mp3', 'lead': 0, 'length': DUR, 'bounds': bounds,
           'names': ['Waiting', 'Ignition', 'Lift-off', 'The First Rift', 'The Rocket Enters', 'Rockets Reappear', 'Seven Rockets', 'Into One Rift', 'Again, Unstable', 'The Meteor Enters', 'The Rift Is Gone', 'The Zero Point Rift', 'The First Dive', 'We Enter the Sky', 'Six Rockets and the Meteor', 'The Impact', 'Pushed Back', 'The Black Hole Pulls', 'Black Screen', 'The Black Hole'], 'cues': 'cues.json', 'plateau': 0.0, 'props': [{'id': 'zeropoint', 'glb': 'zeropoint.glb'}, {'id': 'blackhole', 'glb': 'blackhole.glb'}, {'id': 'vault', 'glb': 'vault.glb'}],
           'actors': [{'id': 'rocket', 'glb': 'rocket.glb', 'height': 150, 'ankle': 5.0}, {'id': 'meteor', 'glb': 'meteor.glb', 'height': 70, 'ankle': 5.0}]}, open(os.path.join(OUT, 'event.json'), 'w'), indent=1)
print(len(ev), 'cues,', len(hits), 'audio hits')
