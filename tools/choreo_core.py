"""Choreography core: smooth keyframe tracks, the beat grid from the song, noise, and controls -> Pose evaluation.

A Scene owns a set of named Tracks (pelvis, spine, head, arms, hands, feet, face...). Moves (moves.py) add keyframes; evaluate() turns the
track values at a time into a full Pose using the IK pose builder."""
import sys, os, math, json, random, bisect
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pose import *

HERE = os.path.dirname(os.path.abspath(__file__))
FPS = 24.0

# ---------------------------------------------------------------- smooth tracks
class Track:
    """Cubic-Hermite (Catmull-Rom tangents) keyframe curve; holds the first/last value outside the keys. flat=True forces a zero tangent at a key."""
    def __init__(self, default=0.0): self.default = default; self.t = []; self.v = []; self.f = []
    def key(self, t, v, flat=False):
        i = bisect.bisect_left(self.t, t)
        if i < len(self.t) and abs(self.t[i] - t) < 1e-4: self.v[i] = v; self.f[i] = flat; return
        if i > 0 and abs(self.t[i - 1] - t) < 1e-4: self.v[i - 1] = v; self.f[i - 1] = flat; return
        self.t.insert(i, t); self.v.insert(i, v); self.f.insert(i, flat)
    def hold(self, t0, t1, v): self.key(t0, v, True); self.key(t1, v, True)
    def trim(self, t):
        """Drop keys later than t (a transition replaces whatever a previous move had scheduled further ahead)."""
        while self.t and self.t[-1] > t + 1e-4: self.t.pop(); self.v.pop(); self.f.pop()
    def get(self, t):
        n = len(self.t)
        if n == 0: return self.default
        if t <= self.t[0]:
            if self.v[0] == self.default: return self.v[0]
            u = (t - (self.t[0] - 0.6)) / 0.6               # before its first key a control sits at its default, then eases in
            if u <= 0: return self.default
            return self.default + (self.v[0] - self.default) * u * u * (3 - 2 * u)
        if t >= self.t[-1]: return self.v[-1]
        i = bisect.bisect_right(self.t, t) - 1
        t0, t1 = self.t[i], self.t[i + 1]; v0, v1 = self.v[i], self.v[i + 1]; h = t1 - t0; u = (t - t0) / h
        def tangent(j):
            if self.f[j]: return 0.0
            if j <= 0: return (self.v[1] - self.v[0]) / (self.t[1] - self.t[0])
            if j >= n - 1: return (self.v[-1] - self.v[-2]) / (self.t[-1] - self.t[-2])
            return (self.v[j + 1] - self.v[j - 1]) / (self.t[j + 1] - self.t[j - 1])
        m0 = tangent(i) * h * 0.55; m1 = tangent(i + 1) * h * 0.55      # <1: tamed tangents, no wild overshoot
        u2 = u * u; u3 = u2 * u
        return (2 * u3 - 3 * u2 + 1) * v0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * v1 + (u3 - u2) * m1

class Noise:
    """Smooth non-repeating wobble: a few incommensurate sines with random phases."""
    def __init__(self, rng, base_freq=0.35, n=3):
        self.c = [(rng.uniform(0.6, 1.6) * base_freq * (1.0 + 0.77 * k), rng.uniform(0, 6.283), 1.0 / (1 + k * 0.6)) for k in range(n)]
        self.norm = sum(a for _, _, a in self.c)
    def __call__(self, t): return sum(a * math.sin(6.2832 * f * t + p) for f, p, a in self.c) / self.norm

# ---------------------------------------------------------------- the beat grid
class Beats:
    def __init__(self, analysis):
        raw = sorted(analysis['beats']); self.dur = analysis['duration']
        grid = []
        for b in raw:
            if not grid or b - grid[-1] >= 0.28: grid.append(b)
        # fill long gaps with evenly spaced beats at the neighbouring tempo so moves always have something to land on
        out = [grid[0]]
        for b in grid[1:]:
            gap = b - out[-1]
            if gap > 0.95:
                per = out[-1] - out[-2] if len(out) > 1 and 0.3 < out[-1] - out[-2] < 0.95 else 0.46
                n = max(1, int(round(gap / per))); start = out[-1]
                for k in range(1, n): out.append(start + gap * k / n)
            out.append(b)
        self.b = out
        self.analysis = analysis
    def around(self, t, n):
        """n beat times starting at the first beat >= t."""
        i = bisect.bisect_left(self.b, t)
        return self.b[i:i + n]
    def next(self, t):
        i = bisect.bisect_left(self.b, t)
        return self.b[min(i, len(self.b) - 1)]
    def period(self, t):
        i = max(1, min(len(self.b) - 1, bisect.bisect_left(self.b, t)))
        p = self.b[i] - self.b[i - 1]
        return p if 0.25 < p < 1.2 else 0.46
    def bpm(self, t):
        v = 78.0
        for tt, b in self.analysis['tempos']:
            if tt <= t: v = b
        return v
    def move_period(self, t):
        """Seconds per move-beat: the song's pulse folded into 0.5-1.0 s so gestures are never frantic or glacial (tempo estimates are octave-ambiguous)."""
        p = 60.0 / self.bpm(t)
        while p < 0.5: p *= 2
        while p > 1.0: p /= 2
        return p
    def snap(self, t):
        i = bisect.bisect_left(self.b, t)
        c = [self.b[j] for j in (i - 1, i) if 0 <= j < len(self.b)]
        return min(c, key=lambda x: abs(x - t)) if c else t

def load_analysis():
    return json.load(open(os.path.join(HERE, 'audio_analysis.json')))

# ---------------------------------------------------------------- scene = all tracks
CONTROLS = {
    # pelvis offsets in body-height fractions, angles in degrees
    'pel_dx': 0, 'pel_dy': 0, 'pel_dz': 0, 'pel_yaw': 0, 'pel_pitch': 0, 'pel_roll': 0,
    'sp_bend': 0, 'sp_twist': 0, 'sp_lean': 0,
    'hd_pitch': 0, 'hd_yaw': 0, 'hd_roll': 0, 'shk': 0, 'lift': 0, 'root_dy': 0, 'jaw': 0, 'wag': 0, 'home_x': 0, 'home_z': 0,
}
for _s in SIDES:
    for k, d in (('shl_up', 0), ('shl_fwd', 0), ('arm_w', 0), ('arm_x', 0.3), ('arm_y', -0.4), ('arm_z', 0.05), ('arm_elb', 0.5),
                 ('curl', 0.25), ('thumb', 0.25), ('point', 0),
                 ('ft_wx', None), ('ft_wz', 0), ('ft_yaw', 0), ('ft_lift', 0), ('ft_pitch', 0),
                 ('ft_rx', None), ('ft_ry', -0.44), ('ft_rz', 0), ('ft_w', 1.0), ('knee_out', 8)):
        CONTROLS['%s_%s' % (k, _s)] = d
FACE_CONTROLS = {'jaw': 0, 'smile': 0, 'blink': 0, 'brow': 0}

class Scene:
    def __init__(self, seed=1, analysis=None):
        self.rng = random.Random(seed); self.analysis = analysis or load_analysis(); self.beats = Beats(self.analysis)
        self.tr = {}
        for k, d in CONTROLS.items():
            sd = k[-1]
            if k.startswith('ft_wx'): d = BP[N('foot_' + sd)][0]                     # planted under the hips, mesh units
            elif k.startswith('ft_wz'): d = BP[N('foot_' + sd)][2]
            elif k.startswith('ft_rx'): d = (BP[N('foot_' + sd)][0] - PEL_B[0]) / BODY_H   # pelvis-relative defaults, body-height fractions
            elif k.startswith('ft_ry'): d = (ANKLE_REST_Y - PEL_B[1]) / BODY_H
            elif k.startswith('ft_rz'): d = (BP[N('foot_' + sd)][2] - PEL_B[2]) / BODY_H
            self.tr[k] = Track(d)
        self.noise = {}
        self.tr['live'] = Track(1.0)
        self.log = []                     # (t0, t1, move name, signature) for the uniqueness report
        self.fixed = {'pel_base_y': 0.0}
    ROT = ('pel_pitch', 'pel_yaw', 'pel_roll')
    def K(self, name, t, v, flat=None):
        if flat is None: flat = name.startswith('ft_')           # feet-related tracks are always flat-tangent (no sliding between equal keys)
        tr = self.tr[name]
        if name in self.ROT and tr.t: v += 360.0 * round((tr.get(t) - v) / 360.0)      # whole-body spins never unwind the long way round
        tr.key(t, v, flat)
    def H(self, name, t0, t1, v): self.tr[name].hold(t0, t1, v)
    def get(self, name, t): return self.tr[name].get(t)
    def n(self, key, t, freq=0.35, amp=1.0):
        if key not in self.noise: self.noise[key] = Noise(self.rng, freq)
        return amp * self.noise[key](t)

# ---------------------------------------------------------------- controls -> Pose
def chest_basis(ps):
    R = ps.P[N('spine_05')]; return R

NOISE = {'pel_dx': (0.006, 0.31), 'pel_dz': (0.004, 0.23), 'pel_roll': (1.2, 0.27), 'pel_yaw': (1.6, 0.22), 'sp_twist': (1.6, 0.29), 'sp_lean': (1.3, 0.23),
         'sp_bend': (0.9, 0.37), 'hd_pitch': (2.2, 0.41), 'hd_yaw': (3.0, 0.19), 'hd_roll': (1.6, 0.33), 'pel_dy': (0.004, 0.45)}
for _sd in SIDES:
    NOISE.update({'shl_up_' + _sd: (1.2, 0.5), 'arm_x_' + _sd: (0.012, 0.37), 'arm_y_' + _sd: (0.012, 0.41), 'arm_z_' + _sd: (0.012, 0.33), 'curl_' + _sd: (0.05, 0.6)})

FOOT_LEN = vlen(vsub(BP[N('ball_l')], BP[N('foot_l')])) if 'ball_l' in s_name else 2500.0
REL_KNEE = 0.02        # whole body sits 2% of a body height lower than a locked-straight stance: knees stay slightly soft so every planted foot is reachable
SHAKE = {'pel_dx': 0.012, 'pel_dy': 0.01, 'pel_roll': 2.0, 'sp_lean': 2.5, 'sp_bend': 2.5, 'hd_pitch': 4.0, 'hd_yaw': 4.0, 'hd_roll': 3.0}

def build_pose(S, t, face=None):
    live = S.get('live', t); shk = S.get('shk', t)
    def g(k):
        v = S.get(k, t)
        if k in NOISE and live > 0.001: a, f = NOISE[k]; v += S.n(k, t, f, a) * live
        if shk > 0.001 and k in SHAKE: v += S.n('shk_' + k, t, 7.0, SHAKE[k]) * shk
        return v
    ps = Pose()
    ps.pelvis(g('pel_yaw'), g('pel_pitch'), g('pel_roll'), g('pel_dx') + g('home_x'), g('pel_dy') + g('lift') + g('root_dy') - REL_KNEE, g('pel_dz') + g('home_z'))
    ps.spine(g('sp_bend'), g('sp_twist'), g('sp_lean'))
    ps.head(g('hd_pitch'), g('hd_yaw'), g('hd_roll'))
    for sd in SIDES:
        ps.shoulder(sd, g('shl_up_' + sd), g('shl_fwd_' + sd))
    # arms: relaxed idle arms blended with IK gesture poses
    relaxed = {j: [r[:] for r in ps.P[j]] for sub in [subtree[N('clavicle_l')], subtree[N('clavicle_r')]] for j in sub}
    chest = ps.P[N('spine_05')]
    for sd in SIDES:
        w = g('arm_w_' + sd)
        if w > 0.002:
            Sh = ps.pos('upperarm_' + sd); s = sgn(sd)
            off = [s * g('arm_x_' + sd) * BODY_H, g('arm_y_' + sd) * BODY_H, g('arm_z_' + sd) * BODY_H]
            target = vadd(Sh, mv(chest, off))
            e = g('arm_elb_' + sd)
            hint = mv(chest, [s * e, -1.0, -0.7])
            ps.arm(sd, target, hint)
            if w < 0.998:
                for j in subtree[N('upperarm_' + sd)]:
                    ps.P[j] = slerp_m(relaxed[j], ps.P[j], smooth(w) if False else w)
    for sd in SIDES:
        ps.hand(sd, g('curl_' + sd), g('thumb_' + sd), g('point_' + sd) > 0.5)
    # legs: planted (world) <-> pelvis-relative (airborne / seated) targets blended per foot
    pel = ps.pel_abs(); Rp = ps.P[PEL]
    for sd in SIDES:
        wgt = max(0.0, min(1.0, g('ft_w_' + sd)))
        world = [g('ft_wx_' + sd), ANKLE_REST_Y + g('ft_lift_' + sd) + g('root_dy') * BODY_H, g('ft_wz_' + sd)]
        rel = vadd(pel, mv(Rp, [g('ft_rx_' + sd) * BODY_H, g('ft_ry_' + sd) * BODY_H, g('ft_rz_' + sd) * BODY_H]))
        A = [world[i] * wgt + rel[i] * (1 - wgt) for i in range(3)]
        fy = g('ft_yaw_' + sd) + (1 - wgt) * g('pel_yaw'); fp = g('ft_pitch_' + sd) + (1 - wgt) * 0.9 * g('pel_pitch')      # airborne feet follow the body's tilt
        L = g('ft_lift_' + sd); grounded = wgt > 0.99 and L < 60
        heel = 0.0
        if grounded:                                                                   # heels pop up and settle: weight shifts from heel to toe, shoes never frozen
            heel = max(0.0, S.n('heel_' + sd, t, 1.1, 1.0) - 0.05) * (0.0 if GENERIC else 820.0)
            world[1] += heel; A = [world[i] * wgt + rel[i] * (1 - wgt) for i in range(3)]
        hp = math.degrees(math.asin(min(0.6, heel / FOOT_LEN)))
        plant = 26.0 + 7.0 * S.n('toe_' + sd, t, 0.6, 1.0)                               # relaxed ankle when the foot hangs: toes down, slowly swaying
        toe = min(38.0, L / 1800.0 * 38.0)
        ps.leg(sd, A, fy, fp + hp, g('knee_out_' + sd), fp + toe, follow=(1 - wgt), plant=plant, toe=8.0 + toe)
    if face: ps.face(**face)
    return ps

def sample_scene(S, t0, t1, fps=FPS, face_fn=None):
    n = int((t1 - t0) * fps) + 1
    return [build_pose(S, t0 + k / fps, face_fn(t0 + k / fps) if face_fn else None).frame_tuple() for k in range(n)]

