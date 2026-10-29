"""python build_rocket.py -> cache/rocket/{rocket.glb,cues.json,event.json}; python install_rocket.py installs them as events/rocketlaunch.
Timeline anchors come from the soundtrack (analyze_audio.py -> cache/rocket/audio.json): rms hits at 7.57 (ignition), 10.43 (lift-off), 34.87 (the sky cracks),
the drop to silence at 51.7 (the rocket is swallowed), the build 73 -> 79.5 (the rift blast), the climax 107 -> 120 and the silence from 122."""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'rocket'); os.makedirs(OUT, exist_ok=True)
SRC = r'C:\Users\drago\Downloads\the_seven_rocket.glb'; DUR = 134.86
g, b = slim_glb.read_glb(SRC); g, b = slim_glb.slim(g, b); slim_glb.write_glb(os.path.join(OUT, 'rocket.glb'), g, b)
A = json.load(open(os.path.join(OUT, 'audio.json'))); r = A['rms']; mr = max(r); rr = [x / mr for x in r]
hits = []                                                       # audio hits: (time, strength 0..1)
for i in range(6, len(rr) - 3):
    rise = rr[i + 2] - min(rr[i - 6:i])
    if rise > 0.14 and rr[i] >= max(rr[i - 3:i + 4]): hits.append((i / 30.0, rise))
m = []
for h in hits:
    if m and h[0] - m[-1][0] < 0.7:
        if h[1] > m[-1][1]: m[-1] = h
    else: m.append(h)
hits = [(round(t, 2), round(s, 2)) for t, s in m if t > 5]
TL = dict(ign=7.5, lift=10.4, crack=34.9, enter=51.6, build=73.0, blast=79.5, climax=107.4, final=118.67, close=120.3, end=124.0)
ev = []
def E(t, type, **k): d = {'t': round(t, 2), 'type': type}; d.update(k); ev.append(d)
# ignition, lift-off
E(TL['ign'] - 0.1, 'flash', at='pad', size=60, color='orange'); E(TL['ign'], 'boom', at='pad', size=40, color='orange')
E(TL['lift'] - 0.05, 'boom', at='pad', size=70, color='orange'); E(TL['lift'], 'dust', at='pad', size=14.0); E(TL['lift'], 'shock', kind='ground', size=260, color='orange')
E(TL['lift'] + 0.5, 'dust', at='pad', size=10.0)
# the sky cracks: a flash at the rift, then a lightning bolt on each hit of the music
E(TL['crack'] - 0.05, 'flash', at='rift', size=80, color='white'); E(TL['crack'], 'shock', kind='sky', size=420, color='violet'); E(TL['crack'], 'boom', at='rift', size=50, color='violet')
for t, s in hits:
    if TL['crack'] + 0.4 < t < TL['enter'] - 0.6 and s >= 0.2: E(t, 'bolt', s=s, color='white')
    if TL['crack'] < t < TL['enter'] and s >= 0.15: E(t, 'pulse', s=s)
# the rocket is swallowed
E(TL['enter'] - 0.15, 'flash', at='rift', size=140, color='white'); E(TL['enter'], 'boom', at='rift', size=90, color='white'); E(TL['enter'], 'shock', kind='sky', size=700, color='white')
E(TL['enter'] + 0.1, 'shock', kind='ground', size=300, color='violet')
# the rift hangs open; it breathes and builds
for t, s in hits:
    if TL['enter'] + 1 < t < TL['blast'] - 0.3 and s >= 0.15: E(t, 'pulse', s=s)
E(TL['build'] + 0.5, 'bolt', s=0.3, color='violet'); E(TL['build'] + 2.4, 'bolt', s=0.4, color='violet'); E(TL['blast'] - 1.2, 'bolt', s=0.6, color='white')
# the rift blast
E(TL['blast'] - 0.1, 'flash', at='rift', size=200, color='white'); E(TL['blast'], 'boom', at='rift', size=120, color='violet'); E(TL['blast'], 'shock', kind='sky', size=900, color='white')
E(TL['blast'] + 0.15, 'shock', kind='ground', size=420, color='violet'); E(TL['blast'] + 0.3, 'boom', at='pad', size=60, color='violet')
for t, s in hits:
    if TL['blast'] + 0.5 < t < TL['climax'] and s >= 0.15:
        E(t, 'pulse', s=s)
        if s >= 0.2: E(t, 'bolt', s=s, color='violet' if s < 0.3 else 'white')
    if TL['climax'] <= t < TL['close'] and s >= 0.15:
        E(t, 'pulse', s=s)
        E(t, 'bolt', s=s, color='white')
        if s >= 0.3: E(t, 'shock', kind='sky' if int(t * 10) % 2 else 'ground', size=300 + 400 * s, color='white')
# the finale: the biggest hit, then the rift collapses
E(TL['final'] - 0.1, 'flash', at='rift', size=260, color='white'); E(TL['final'], 'boom', at='rift', size=160, color='white'); E(TL['final'], 'shock', kind='sky', size=1100, color='white')
E(TL['final'] + 0.1, 'shock', kind='ground', size=520, color='white')
E(TL['close'], 'flash', at='rift', size=300, color='white'); E(TL['close'], 'shock', kind='sky', size=1300, color='violet'); E(TL['close'] + 0.1, 'boom', at='rift', size=150, color='violet'); E(TL['close'] + 0.2, 'shock', kind='ground', size=600, color='violet')
E(TL['close'] + 1.0, 'flash', at='rift', size=120, color='violet')
ev.sort(key=lambda d: d['t'])
# the arena the plugin fits onto the Forest's real floor: the viewer's spot, the pad and a ring around it
PAD = [0, 110]; pts = [[0, 0], PAD] + [[round(PAD[0] + 48 * math.cos(a * 0.7854)), round(PAD[1] + 48 * math.sin(a * 0.7854))] for a in range(8)]
json.dump({'tl': TL, 'events': ev, 'windows': [], 'arena': {'pts': pts, 'margin': 26}}, open(os.path.join(OUT, 'cues.json'), 'w'), separators=(',', ':'))
bounds = [0, 6.5, 10.4, 24, 34.9, 51.6, 54, 73, 79.5, 83, 107.4, 118.67, 120.3, 122] + [DUR]
json.dump({'id': 'rocketlaunch', 'name': 'Rocket Launch (2018)', 'kind': 'rocket', 'music': 'rocket.mp3', 'lead': 0, 'length': DUR, 'bounds': bounds,
           'names': ['Countdown', 'Ignition', 'Lift-off', 'The Climb', 'The Sky Cracks', 'Into the Rift', 'The Rift Hangs Open', 'Build-up', 'The Rift Blast', 'Lightning and Quakes', 'Climax', 'The Final Hit', 'The Rift Collapses', 'Aftermath'],
           'cues': 'cues.json', 'plateau': 0.0, 'actors': [{'id': 'rocket', 'glb': 'rocket.glb', 'height': 150, 'ankle': 5.0}]}, open(os.path.join(OUT, 'event.json'), 'w'), indent=1)
print(len(ev), 'events;', len(hits), 'hits; rocket.glb', os.path.getsize(os.path.join(OUT, 'rocket.glb')) // 1024, 'KB')
