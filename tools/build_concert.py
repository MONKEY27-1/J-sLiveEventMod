"""python build_concert.py -> cache/marsh/{cues.json,event.json}; python install_concert.py installs events/marshmello.
Song times are the ones the user read off the replay: Alone 0:00, 2:11, 2:55, 4:37, 5:16, Happier 6:58, the end with the mp3 (617.3 s). Everything else comes from the audio analysis (analyze_audio.py ->
cache/marsh/audio.json): the beat grid (drives bounces, light chases, lasers, the screens' frame changes), the loud hits (pyro / confetti / giant-hologram flashes), the loud passages (the giant Marshmello
hologram is up), a drop inside each song (where the loudness rises most)."""
import json, os, math
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'marsh')
A = json.load(open(os.path.join(OUT, 'audio.json'))); DUR = round(A['duration'], 2); r = np.array(A['rms'], np.float32); r = r / r.max()
SONGS = [(0.0, 'Alone'), (131.0, "Poppin'"), (175.0, 'Check This Out'), (277.0, 'Chasing Colors'), (316.0, 'Flashbacks / Everyday / Fly'), (418.0, 'Happier')]
PAL = [0, 1, 2, 3, 4, 5]; SHEET = [0, 1, 2, 3, 4, 5]; LASER = [0, 1, 2, 3, 4, 5]
def env(t, w=1.0): i0 = max(0, int((t - w) * 30)); i1 = min(len(r), int((t + w) * 30) + 1); return float(r[i0:i1].mean())
# beats: the analysed beats, densified so that no gap is longer than about 0.9 s
beats = [b for b in A['beats'] if b >= 0]
if beats[0] > 0.01: beats = [0.0] + beats
dense = []
for x, y in zip(beats[:-1], beats[1:]):
    k = max(1, int(round((y - x) / 0.6))); dense += [x + (y - x) * q / k for q in range(k)]
beats = dense + [beats[-1]]
# hits: rms rises (like the other events)
rr = r; hits = []
for i in range(6, len(rr) - 3):
    rise = rr[i + 2] - min(rr[i - 6:i])
    if rise > 0.12 and rr[i] >= max(rr[i - 3:i + 4]): hits.append((i / 30.0, float(rise)))
m = []
for h in hits:
    if m and h[0] - m[-1][0] < 0.8:
        if h[1] > m[-1][1]: m[-1] = h
    else: m.append(h)
hits = [(round(t, 2), round(s, 2)) for t, s in m]
# the loud passages: smoothed loudness above 0.30 until it falls under 0.20, at least 8 s long -> the giant hologram is up
holo = []; on = None
for i in range(0, len(r), 15):
    t = i / 30.0; v = env(t, 2.0)
    if v > 0.30 and on is None: on = t
    if (v <= 0.20 or i + 15 >= len(r)) and on is not None:
        if t - on >= 8: holo.append([round(on, 1), round(t, 1)])
        on = None
# the drops: the beat that comes back after a quiet break (loudness 1 s before < 0.15, 2 s later > 0.15 higher), at least 20 s apart
drops = []
for t in np.arange(8, DUR - 6, 0.5):
    if env(t - 1, 1.0) < 0.15 and env(t + 2, 1.0) - env(t - 1, 1.0) > 0.15 and (not drops or t - drops[-1][0] > 20):
        k = max(j for j, (t0, _) in enumerate(SONGS) if t0 <= t); drops.append((round(float(t), 1), SONGS[k][1]))
drops = [(t, n) for t, n in drops if all(abs(t - t0) > 12 for t0, _ in SONGS)]
# sections: each song, and the drop inside it
secs = [(t0, name) for t0, name in SONGS] + [(t, name + ' - drop') for t, name in drops]
secs.sort(); bounds = [round(t, 2) for t, _ in secs] + [DUR]; names = [n if not n.endswith('drop') else n for _, n in secs]
if len(names) < 7: raise SystemExit('need at least 7 sections: ' + str(names))
songs = [{'t': t0, 'name': name, 'pal': PAL[i], 'sheet': SHEET[i], 'laser': LASER[i]} for i, (t0, name) in enumerate(SONGS)]
json.dump({'songs': songs, 'beats': [round(b, 3) for b in beats], 'hits': [{'t': t, 's': s} for t, s in hits], 'holo': holo, 'drops': [{'t': t, 'name': n} for t, n in drops], 'end': DUR,
           'energy': [round(env(t, 1.0), 2) for t in np.arange(0, DUR, 0.5)]}, open(os.path.join(OUT, 'cues.json'), 'w'), separators=(',', ':'))
json.dump({'id': 'marshmello', 'name': 'Marshmello Showtime (2019)', 'kind': 'concert', 'music': 'marshmello.mp3', 'lead': 0, 'length': DUR, 'bounds': bounds, 'names': names, 'cues': 'cues.json',
           'props': [{'id': 'stage', 'glb': 'stage.glb', 'lit': True}, {'id': 'visuals', 'glb': 'visuals.glb'}, {'id': 'marsh', 'glb': 'marsh_dj.glb', 'lit': True}, {'id': 'holo', 'glb': 'marsh_dance.glb', 'lit': True}, {'id': 'bearL', 'glb': 'bear_a.glb', 'lit': True}, {'id': 'bearR', 'glb': 'bear_b.glb', 'lit': True}]},
          open(os.path.join(OUT, 'event.json'), 'w'), indent=1)
print(len(beats), 'beats;', len(hits), 'hits;', len(holo), 'hologram windows', holo, '; sections', list(zip(bounds, names)))
