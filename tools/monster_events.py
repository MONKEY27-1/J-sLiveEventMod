"""Reads the baked monster (monster_trace.json / monster_beam.json written by real_monster.py) and derives what the soundtrack-synchronised cues need:
roars (jaw rising edges), beam windows (mouth open and not pointing at the sky -> where the ray meets the ground), the underground hide / show times, step / strike positions."""
import json, math, os
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'showdown')
TR = json.load(open(os.path.join(OUT, 'monster_trace.json'))); BM = json.load(open(os.path.join(OUT, 'monster_beam.json')))
DT = TR['dt']; B = TR['b']; N_ = len(B['pelvis'])

def at(bone, t):
    i = max(0.0, min(N_ - 1.001, t / DT)); i0 = int(i); f = i - i0; a = B[bone][i0]; b = B[bone][i0 + 1]
    return [a[k] + (b[k] - a[k]) * f for k in range(3)]
def xz(t): p = at('pelvis', t); return p[0], p[2]
def jaw(t): i = max(0, min(N_ - 1, int(round(t / DT)))); return BM['jaw'][i]

def hidden_spans(thresh=-4.0):
    """intervals where every traced body part is below the ground (y < thresh metres)"""
    spans = []; start = None
    for i in range(N_):
        low = max(B[b][i][1] for b in B if b != 'FX_mouthLaser') < thresh
        if low and start is None: start = i * DT
        if not low and start is not None: spans.append((start, i * DT)); start = None
    if start is not None: spans.append((start, N_ * DT))
    return spans

def roars(min_deg=22.0, rest=9.0, gap=2.0):
    """times the jaw swings open (after having been closed for `gap` s)"""
    out = []; last_closed = -99.0
    for i in range(N_):
        t = i * DT; j = BM['jaw'][i]
        if j < rest: last_closed = t
        elif j > min_deg and t - last_closed < 0.6 and (not out or t - out[-1] > gap) and last_closed > -50: out.append(round(t, 2))
    return out

def mouth_ray(i):
    m = BM['mouth'][i]; d = BM['dir'][i]; return m, d
def ground_hit(i, far=450.0):
    m, d = mouth_ray(i)
    if d[1] < -0.04:
        s = -m[1] / d[1]; s = min(s, far * 1.5); return [m[0] + d[0] * s, 0.0, m[2] + d[2] * s], True
    return [m[0] + d[0] * far, m[1] + d[1] * far, m[2] + d[2] * far], False

def beam_windows(t0, t1, jaw_min=24.0, step=0.5, min_len=1.2):
    """(start, end, pts) windows in [t0, t1] where the mouth is wide open and the head points at / above the ground, pts = [[t,x,y,z]..] every `step`"""
    wins = []; cur = None
    for i in range(int(t0 / DT), min(N_, int(t1 / DT))):
        t = i * DT; ok = BM['jaw'][i] > jaw_min and BM['dir'][i][1] < 0.35
        if ok and cur is None: cur = [t, t]
        elif ok: cur[1] = t
        elif cur is not None and t - cur[1] > 0.6:
            if cur[1] - cur[0] >= min_len: wins.append(tuple(cur))
            cur = None
    if cur is not None and cur[1] - cur[0] >= min_len: wins.append(tuple(cur))
    out = []
    for a, b in wins:
        pts = []; t = a
        while t <= b + 1e-6:
            g, hit = ground_hit(int(round(t / DT))); pts.append([round(t, 2), round(g[0], 1), round(g[1], 1), round(g[2], 1)]); t += step
        out.append((a, b, pts))
    return out

if __name__ == '__main__':
    print('hidden', [(round(a, 1), round(b, 1)) for a, b in hidden_spans()])
    print('roars', roars())
    for a, b, pts in beam_windows(40.0, 92.0): print('beam %.1f-%.1f' % (a, b), pts[:2], '...', pts[-1])
    for t in (0, 19, 40, 56, 74, 90, 104, 120, 124, 129, 156, 161, 176, 183, 206, 240, 248, 260, 280, 292, 303): print(t, [round(v) for v in at('pelvis', t)], 'head', [round(v) for v in at('head', t)], 'eye', [round(v) for v in at('eye', t)])
