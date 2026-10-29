"""Audio-driven face: jaw follows the vocal-band envelope (so the mouth moves when he sings / raps), blinks at natural random intervals, eyes drift
and counter-rotate with the head, brows accent the kicks, smile lifts when he is not singing. Returns kwargs for Pose.face()."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from choreo_core import *

def smooth01(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

class Face:
    def __init__(self, analysis, S, seed=5):
        self.a = analysis; self.S = S; self.rng = random.Random(seed); self.fps = analysis['fps']; self.n = len(analysis['rms'])
        self.blinks = []; t = 1.5
        while t < analysis['duration'] + 5:
            self.blinks.append((t, self.rng.uniform(0.13, 0.2)))
            if self.rng.random() < 0.18: self.blinks.append((t + 0.32, 0.13))                      # occasional double blink
            t += self.rng.uniform(2.2, 6.0)
        self.blinks.sort()
        self.jaw = 0.0; self.last_t = None; self.vs = 0.0
        self.gaze = [(0.0, 0.0)]; tt = 0.0; gy = gp = 0.0
        while tt < analysis['duration'] + 5:
            tt += self.rng.uniform(0.7, 2.6); gy = self.rng.uniform(-9, 9); gp = self.rng.uniform(-5, 5); self.gaze.append((tt, gy, gp))
        self.gaze = self.gaze[1:]
    def env(self, key, t, w=0):
        i = min(self.n - 1, max(0, int(t * self.fps))); return self.a[key][i]
    def smoothed(self, key, t, win):
        k = max(1, int(win * self.fps)); i = int(t * self.fps); lo = max(0, i - k); hi = min(self.n - 1, i + k)
        return sum(self.a[key][lo:hi + 1]) / (hi - lo + 1)
    def blink_amount(self, t):
        for bt, d in self.blinks:
            if bt - 0.02 <= t <= bt + d:
                u = (t - bt) / d
                return smooth01(u / 0.35) if u < 0.35 else (1.0 if u < 0.5 else 1.0 - smooth01((u - 0.5) / 0.5))
            if bt > t + 1: break
        return 0.0
    def gaze_at(self, t):
        prev = (0.0, 0.0); cur = (0.0, 0.0)
        for tt, gy, gp in self.gaze:
            if tt > t: break
            prev, cur = cur, (gy, gp)
        return cur
    def __call__(self, t):
        v = self.env('vocal', t); o = self.env('open', t); r = self.smoothed('rms', t, 0.4); kk = self.env('kick', t)
        gate = smooth01((r - 0.10) / 0.22)                                                         # silent / very quiet parts: mouth stays shut
        syl = 0.78 + 0.4 * random.Random(int(t * 9)).random()                                      # every syllable a little different
        target = gate * (smooth01((v - 0.28) / 0.55) ** 1.15) * (9.0 + 14.0 * smooth01((o - 0.45) / 0.4)) * syl
        dt = 0 if self.last_t is None else t - self.last_t; self.last_t = t
        a_up = 1 - math.exp(-dt / 0.035) if dt > 0 else 1; a_dn = 1 - math.exp(-dt / 0.075) if dt > 0 else 1
        self.jaw += (target - self.jaw) * (a_up if target > self.jaw else a_dn)
        sing = smooth01(self.jaw / 9.0)
        brow = max(-0.6, min(2.2, 1.4 * smooth01((kk - 0.45) / 0.5) + 0.9 * (r - 0.35) + 0.5 * sing))
        smile = max(0.0, 0.45 + 1.1 * (1.0 - sing) * smooth01((r - 0.2) / 0.4) - 0.2 * sing)
        gy, gp = self.gaze_at(t); hy = self.S.get('hd_yaw', t); hp = self.S.get('hd_pitch', t)
        return dict(jaw=self.jaw, smile=smile, blink=self.blink_amount(t), brow=brow,
                    eye_yaw=gy - 0.35 * hy, eye_pitch=gp - 0.3 * hp)
