"""Director: fills the whole song with moves. Section boundaries come from the audio analysis; every move is drawn from a pool for the current
situation (ground / floating / seated / underwater / finale), never the same move twice in a row, never the same one within the last few, and
every instance is randomised so no two are identical. Run: python director.py  -> writes cache/choreo_scene.pkl + choreo_log.json"""
import sys, os, json, math, random, pickle
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from moves2 import *

SEQ = [0.0, 27.5, 92.0, 162.5, 228.0, 348.5, 449.5, 500.952]      # Intro, Stage, Energy, Moon rise, Cosmic, Underwater, Finale
T_EXPLODE = SEQ[6] + 0.86 * (SEQ[7] - SEQ[6])                   # when the plugin blows the finale up (493.7 s)

LIB = {}
for _d in (GROUND, AIR, SEAT, SWIM, FINALE): LIB.update(_d)
UPPER = ('mic_rap', 'light_sweep', 'star_grab', 'arms_wide', 'point_crowd', 'clap_overhead', 'chest_beat', 'hands_head', 'fist_clench', 'open_arms_slow', 'look_around', 'breathe', 'lean_back_rap')

def W(**kw): return list(kw.items())
POOL = {
    'intro': W(breathe=2, look_around=3, open_arms_slow=2.5, fist_clench=1.5, hands_head=1, lean_back_rap=1, star_grab=1, rock=1, walk=1.2, mic_rap=1),
    'stage': W(mic_rap=4, bounce=2, rock=2, point_crowd=2, arms_wide=1.5, step_touch=1.6, chest_beat=1.2, lean_back_rap=1.5, fist_pump=1.2, bounce_arms=1.6, walk=2.6, clap_overhead=0.6, head_bang=0.5, stomp=0.5),
    'surge_a': W(mic_rap=3, bounce_arms=2.5, fist_pump=2.5, point_crowd=2, step_touch=1.5, crouch_hype=1.5, chest_beat=1.5, rock=1.5, jump=1.5, walk=2.2, arms_wide=1, head_bang=1, stomp=1),
    'surge_b': W(jump=3, head_bang=2.2, stomp=2.2, crouch_hype=2, fist_pump=2.5, clap_overhead=1.5, bounce_arms=2.5, kick_step=1.6, point_crowd=1.4, chest_beat=1.4, step_touch=1.4, mic_rap=1.5, arms_wide=1, walk=1.8, light_sweep=0.5, rock=1),
    'air': W(float_hang=3, reach_sky=3, superman=2.4, float_spin=1.1, float_tuck=0.8, mic_rap=1.5, light_sweep=2.2, star_grab=2, arms_wide=1.5, point_crowd=1, clap_overhead=0.5, chest_beat=0.5),
    'seat': W(seat_rap=5, seat_conduct=2.5, seat_cross=1.5),
    'water_slow': W(swim_drift=3, swim_breast=3, swim_tread=2, float_hang=1.4, swim_crawl=1.5, seat_cross=0.8, light_sweep=0.8),
    'water_fast': W(swim_crawl=3, swim_breast=2.5, swim_dive=1.2, swim_tread=1.4, swim_drift=1.4, superman=1.4, float_spin=0.8, reach_sky=1),
    'finale': W(jump=3, head_bang=2.5, stomp=2.4, crouch_hype=2, fist_pump=2.5, clap_overhead=1.5, bounce_arms=2, kick_step=1.4, point_crowd=1, chest_beat=1.4, mic_rap=1.2, arms_wide=1.5, step_touch=1, walk=0.8),
}

class CapRng(random.Random):
    """rng.choice over beat counts never picks a move longer than `cap` beats (keeps slow-tempo moves from dragging on)."""
    cap = 99
    def choice(self, seq):
        if all(isinstance(x, int) for x in seq):
            ok = [x for x in seq if x <= self.cap] or [min(seq)]; return random.Random.choice(self, ok)
        return random.Random.choice(self, seq)

class Director:
    def __init__(self, seed=2024):
        self.S = Scene(seed=seed); self.rng = CapRng(seed * 7 + 1); self.recent = []; self.t = 0.0; self.state = 'ground'; self.lift = 0.0
        self.events = []                                   # (t0, t1, kind) for the plugin: seat / water / air windows
    # ------------------------------------------------------------ helpers
    def est(self, name, t):
        nb = LIB[name][1] if name in LIB else 4
        per = self.S.beats.move_period(t)
        return per * {'jump': 3, 'stomp': 2, 'kick_step': 2, 'walk': 7, 'float_spin': 5, 'breathe': 4}.get(name, 4.5)
    def pick(self, pool, t, end, forbid=()):
        rem = end - t
        cands = []
        for name, w in pool:
            if name in forbid or name in self.recent[-2:]: continue
            n_recent = self.recent[-8:].count(name)
            w = w / (1.0 + 2.2 * n_recent)
            d = self.est(name, t)
            if d > rem + 1.6: w *= 0.08                  # would overrun the section badly
            cands.append((name, w))
        tot = sum(w for _, w in cands); r = self.rng.uniform(0, tot); acc = 0
        for name, w in cands:
            acc += w
            if r <= acc: return name
        return cands[-1][0]
    def run_move(self, name, end=None):
        S = self.S; t = self.t
        self.rng.cap = max(2, int(5.0 / S.beats.move_period(t)))
        if name == 'walk': nt = m_walk(S, t, self.rng)
        else: nt = LIB[name][0](S, t, self.rng)
        if nt <= t + 0.2: nt = t + 0.6
        self.recent.append(name); self.t = nt
    def fill(self, pool_name, end, allow=None, until=None):
        pool = POOL[pool_name]
        if allow is not None: pool = [(n, w) for n, w in pool if n in allow]
        stop = until if until is not None else end - 0.9
        while self.t < stop:
            if self.state == 'ground' and math.hypot(*pelvis_xz(self.S, self.t)) > 0.5 and self.rng.random() < 0.8:
                self.t = recentre(self.S, self.t, self.rng); self.recent.append('walk'); continue
            name = self.pick(pool, self.t, end)
            self.run_move(name, end)
            if self.state in ('air', 'seat') and self.rng.random() < 0.35: self.drift_lift()
    def drift_lift(self):
        h = min(0.85, max(0.3, self.lift + self.rng.uniform(-0.18, 0.2)))
        self.S.K('lift', self.t, self.S.get('lift', self.t)); self.S.K('lift', self.t + 1.6, h); self.lift = h
    # ------------------------------------------------------------ transitions
    def go_air(self, h=0.55, dur=1.0):
        self.t = lift_off(self.S, self.t, h, dur); self.lift = h; self.state = 'air'
    def go_ground(self):
        self.t = land(self.S, self.t, 1.0); self.lift = 0.0; self.state = 'ground'
    def go_seat(self):
        t0 = self.t; self.t = enter_seat(self.S, self.t, 1.2); self.state = 'seat'; self._seat0 = t0
    def leave_seat(self):
        self.t = leave_seat(self.S, self.t, 1.0); self.state = 'air'; self.events.append((self._seat0, self.t, 'seat'))

    # ------------------------------------------------------------ the show
    def run(self):
        S = self.S
        # 0 INTRO: calm, one performer waking up
        self.fill('intro', SEQ[1])
        # 1 STAGE: rap on the beat, walk the stage
        self.t = max(self.t, SEQ[1]); self.fill('stage', SEQ[2])
        # 2 ENERGY SURGE: first half still mid-tempo, then the drop
        self.fill('surge_a', SEQ[2] + 36)
        self.fill('surge_b', SEQ[3])
        # 3 MOON RISE: lift off and float
        self.go_air(0.6, 1.1); self.events.append((self.t - 1.1, SEQ[4], 'air'))
        self.fill('air', SEQ[4])
        # 4 COSMIC: seated on the planet raps, floating in between
        end = SEQ[5]; block = 0
        while self.t < end - 6:
            seat_len = self.rng.uniform(26, 40) if block % 2 == 0 else self.rng.uniform(18, 28)
            if self.t + seat_len > end - 10: seat_len = max(10, end - 10 - self.t)
            self.go_seat(); self.fill('seat', end, until=min(end - 1.2, self.t + seat_len)); self.leave_seat()
            if self.t >= end - 6: break
            self.fill('air', end, until=min(end - 1.2, self.t + self.rng.uniform(14, 24)))
            block += 1
        # 5 UNDERWATER: slow drift, then the faster second half
        self.events.append((self.t, SEQ[6], 'water'))
        self.fill('water_slow', 416.0)
        self.fill('water_fast', SEQ[6])
        # 6 FINALE: land, hype, charge, roar, slam on the explosion
        self.go_ground()
        t_charge, t_slam = self.finale_times()
        self.fill('finale', t_charge, until=t_charge - 1.0)
        self.t = max(self.t, t_charge - 0.3)
        self.run_move('fin_charge')
        while t_slam - self.t > 3.2:
            self.run_move(self.pick(W(fin_roar=2, fin_charge=2, crouch_hype=1, chest_beat=1, head_bang=1), self.t, t_slam))
        self.t = max(self.t, t_slam)
        self.run_move('fin_slam')
        end_t = SEQ[7] + 1.5
        while self.t < end_t:
            self.run_move(self.pick(W(fin_pose=1, breathe=1, look_around=1), self.t, end_t))
        return S

    def finale_times(self):
        """(start of the charge, take-off beat for the slam) chosen from the beat grid so the landing falls on the explosion."""
        B = [b for b in self.S.beats.b if T_EXPLODE - 4.0 < b < T_EXPLODE]
        best = None
        for b in self.S.beats.b:
            if not (T_EXPLODE - 3.5 < b < T_EXPLODE + 0.5): continue
            T = beat_times(self.S, b, 3)
            land_t = T[2] - 0.04
            if best is None or abs(land_t - (T_EXPLODE - 0.1)) < best[0]: best = (abs(land_t - (T_EXPLODE - 0.1)), b)
        t_slam = best[1]
        return t_slam - 11.0, t_slam

def content_hashes(S):
    ctl = ['pel_dx', 'pel_dy', 'pel_dz', 'pel_yaw', 'pel_pitch', 'pel_roll', 'sp_bend', 'sp_twist', 'sp_lean', 'hd_pitch', 'hd_yaw', 'hd_roll', 'arm_x_l', 'arm_y_l', 'arm_z_l', 'arm_x_r', 'arm_y_r', 'arm_z_r', 'ft_wx_l', 'ft_wx_r', 'ft_wz_l', 'ft_wz_r', 'ft_lift_l', 'ft_lift_r', 'curl_l', 'curl_r']
    out = []
    for a, b, n, s_ in S.log:
        out.append((n, tuple(round(S.get(c, a + (b - a) * k / 7.0), 2) for c in ctl for k in range(8))))
    return out

def uniqueness_report(log):
    names = [e[2].split('_')[0] if e[2].startswith(('mic_rap', 'seat_rap', 'walk')) else e[2] for e in log]
    sig = [(e[2], e[3]) for e in log]
    dup = len(sig) - len(set(sig))
    back2back = sum(1 for a, b in zip(log, log[1:]) if a[2] == b[2])
    return {'moves': len(log), 'distinct_names': len(set(e[2] for e in log)), 'duplicate_signatures': dup, 'back_to_back_repeats': back2back,
            'longest_run_without_repeat_name': None}

if __name__ == '__main__':
    d = Director(); S = d.run()
    os.makedirs(os.path.join(HERE, 'cache'), exist_ok=True)
    with open(os.path.join(HERE, 'cache', 'choreo_scene.pkl'), 'wb') as f: pickle.dump(S, f)
    log = [(round(a, 2), round(b, 2), n, s) for a, b, n, s in S.log]
    json.dump({'log': log, 'events': [(round(a, 2), round(b, 2), k) for a, b, k in d.events]}, open(os.path.join(HERE, 'cache', 'choreo_log.json'), 'w'))
    H = content_hashes(S); identical = len(H) - len(set(H))
    print('identical move instances (content hash):', identical)
    print(uniqueness_report(S.log), 'end', round(d.t, 1), 'slam land target', round(T_EXPLODE, 2))
    from collections import Counter
    c = Counter(e[2].split('_')[0] if e[2].startswith(('mic', 'seat_rap', 'walk')) else e[2] for e in S.log)
    print(sorted(c.items(), key=lambda x: -x[1]))
