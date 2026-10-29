"""Run inside Blender (it bundles numpy + FFmpeg decoding): blender -b -P analyze_audio.py -- in.mp3 out.json
Produces beat grid, section boundaries, energy curve, vocal-band envelope and vowel-ish spectral shape at 30 fps."""
import sys, json
import numpy as np
import aud
src, out = sys.argv[sys.argv.index('--') + 1:][:2]
snd = aud.Sound(src)
rate = snd.specs[0]
data = snd.data()                      # (N, ch) float32
mono = data.mean(axis=1) if data.ndim > 1 else data
print("decoded", len(mono) / rate, "s @", rate, "Hz")
FPS = 30
hop = int(rate / FPS)
win = 2048
n_frames = (len(mono) - win) // hop
w = np.hanning(win)
freqs = np.fft.rfftfreq(win, 1.0 / rate)
def band(lo, hi): return (freqs >= lo) & (freqs < hi)
bands = {'sub': band(30, 120), 'low': band(120, 300), 'lowmid': band(300, 900), 'mid': band(900, 2500), 'high': band(2500, 6000), 'air': band(6000, 14000)}
B = {k: np.zeros(n_frames, np.float32) for k in bands}
flux = np.zeros(n_frames, np.float32)
prev = None
mel = []      # coarse spectral shape for section novelty
edges = np.geomspace(120, 12000, 25)
for i in range(n_frames):
    seg = mono[i * hop:i * hop + win] * w
    sp = np.abs(np.fft.rfft(seg))
    for k, m in bands.items(): B[k][i] = np.sqrt(np.mean(sp[m] ** 2))
    if prev is not None: flux[i] = np.sum(np.maximum(sp - prev, 0))
    prev = sp
    mel.append([float(np.log1p(sp[(freqs >= edges[j]) & (freqs < edges[j + 1])].mean())) if np.any((freqs >= edges[j]) & (freqs < edges[j + 1])) else 0.0 for j in range(24)])
mel = np.array(mel)
rms = np.sqrt(sum(B[k] ** 2 for k in B))
def smooth(x, n): return np.convolve(x, np.ones(n) / n, mode='same')
# ---- tempo / beats (global + per 16 s window) ----
def tempo_of(f, lo=70, hi=190):
    f = f - smooth(f, 15); f = np.maximum(f, 0)
    ac = np.correlate(f, f, 'full')[len(f) - 1:]
    lags = np.arange(len(ac)); bpm = 60.0 * FPS / np.maximum(lags, 1)
    sel = (bpm >= lo) & (bpm <= hi)
    return bpm[sel][np.argmax(ac[sel])]
kick = B['sub'] + B['low']
onset = flux / (flux.max() + 1e-9)
glob_bpm = tempo_of(onset)
tempos = []
for t0 in range(0, n_frames - FPS * 16, FPS * 8):
    tempos.append((t0 / FPS, float(tempo_of(onset[t0:t0 + FPS * 16]))))
# beat times via kick-onset peaks snapped to a grid of the global tempo
kd = np.maximum(np.diff(kick, prepend=kick[0]), 0); kd = kd / (kd.max() + 1e-9)
peaks = [i for i in range(2, n_frames - 2) if kd[i] > 0.25 and kd[i] >= kd[i - 1] and kd[i] > kd[i + 1]]
beats = []
last = -99
for i in peaks:
    if i - last >= FPS * 0.25: beats.append(i / FPS); last = i
# ---- sections: novelty on the coarse spectrum with a checkerboard kernel (~10 s) ----
m = (mel - mel.mean(0)) / (mel.std(0) + 1e-6)
ms = np.array([smooth(m[:, j], 15) for j in range(m.shape[1])]).T
step = 15                                   # 0.5 s grid
M = ms[::step]
Lk = 10                                     # +-5 s
nov = np.zeros(len(M))
for i in range(Lk, len(M) - Lk):
    a = M[i - Lk:i].mean(0); b = M[i:i + Lk].mean(0); nov[i] = np.linalg.norm(a - b)
nov = smooth(nov, 3)
cands = [i for i in range(2, len(nov) - 2) if nov[i] >= nov[i - 1] and nov[i] > nov[i + 1] and nov[i] > nov.mean()]
cands.sort(key=lambda i: -nov[i])
chosen = []
for i in cands:
    if all(abs(i - j) * step / FPS > 12 for j in chosen): chosen.append(i)
sections = sorted([float(i * step / FPS) for i in chosen[:14]])
# ---- vocals: mid-band envelope normalised, vowel openness (lowmid vs mid) ----
voc = smooth(B['lowmid'] + B['mid'], 3)
voc = voc / (np.percentile(voc, 97) + 1e-9)
openness = B['lowmid'] / (B['lowmid'] + B['mid'] + B['high'] + 1e-9)
res = {
    'fps': FPS, 'duration': len(mono) / rate, 'global_bpm': float(glob_bpm), 'tempos': tempos,
    'beats': [round(b, 3) for b in beats], 'sections': [round(s, 2) for s in sections],
    'rms': [round(float(x), 4) for x in (rms / np.percentile(rms, 98))], 'vocal': [round(float(x), 4) for x in voc],
    'open': [round(float(x), 4) for x in openness], 'kick': [round(float(x), 4) for x in (kick / (np.percentile(kick, 98) + 1e-9))],
    'high': [round(float(x), 4) for x in (B['high'] / (np.percentile(B['high'], 98) + 1e-9))],
}
json.dump(res, open(out, 'w'))
print("global bpm %.1f; beats %d; sections:" % (glob_bpm, len(beats)), res['sections'])
print("tempo track:", [(round(a), round(b)) for a, b in tempos][::4])
print("loudness per 10 s:", [round(float(np.mean(res['rms'][i:i + 300])), 2) for i in range(0, len(res['rms']), 300)])
print("vocal per 10 s:", [round(float(np.mean(res['vocal'][i:i + 300])), 2) for i in range(0, len(res['vocal']), 300)])
