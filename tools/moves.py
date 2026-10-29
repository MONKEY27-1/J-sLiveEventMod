"""Move library. Each move writes keyposes (on the song's beats) onto the Scene tracks and returns the time it ends.

Conventions: pel_d* in body-height fractions; angles in degrees; arm_x/y/z = hand target relative to the shoulder in the chest frame
(x = outward toward that side, y = up, z = forward) in body-height fractions. 'l'/'r' = the character's left/right."""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from choreo_core import *

ROTC = ('pel_pitch', 'pel_yaw', 'pel_roll'); ROT_RATE = 220.0      # degrees per second a whole-body rotation may change at
NO_RELEASE = ('live', 'home_x', 'home_z', 'lift')      # persistent state, only transitions move these
SIGNED = ('pel_dx', 'pel_yaw', 'pel_roll', 'sp_twist', 'sp_lean', 'hd_yaw', 'hd_roll')      # negated when a move is mirrored
FEET = ('ft_',)

def beat_times(S, t0, nb):
    """nb+1 move-beat times starting on the first detected beat at or after t0: regular steps of the folded tempo, each pulled onto a real beat when one is near."""
    per = S.beats.move_period(t0); T = [S.beats.next(t0)]
    if T[0] - t0 > per: T = [t0]
    for k in range(1, nb + 1):
        want = T[0] + k * per; near = S.beats.snap(want)
        T.append(near if abs(near - want) < 0.2 * per and near > T[-1] + 0.3 * per else want)
    return T
def tf(T, frac):
    i = int(math.floor(frac)); f = frac - i
    if i >= len(T) - 1: return T[-1]
    return T[i] + (T[i + 1] - T[i]) * f

def flipname(c):
    if c.endswith('_l'): return c[:-2] + '_r'
    if c.endswith('_r'): return c[:-2] + '_l'
    return c

def place(S, name, t0, nb, keys, rng, mirror=None, release=0.45, sig=''):
    """keys: [(beat_fraction, {control: value})]. Returns the end time (last beat)."""
    T = beat_times(S, t0, nb); ts = T[0]
    if mirror is None: mirror = rng.random() < 0.5
    flat = []
    for frac, d in keys:
        dd = {}
        for c, v in d.items():
            if mirror:
                if c in SIGNED or c.startswith(('ft_rx', 'ft_yaw')): v = -v
                c = flipname(c)
            dd[c] = v
        flat.append((frac, dd))
    used = set(c for _, d in flat for c in d)
    for c in used:
        S.K(c, ts, S.get(c, ts))                         # anchor: continue from wherever the previous move left it
    beat = (T[1] - T[0]) if len(T) > 1 else 0.6
    arrive = {}                                          # a big whole-body rotation change (swimming -> upright) gets time to happen
    for frac, d in flat:
        if frac == 0:
            for c, v in d.items():
                need = abs(v - S.get(c, ts)) / ROT_RATE if c in ROTC else 0.0
                if need > 0.3: arrive[c] = ts + min(need * 1.3, 0.95 * beat)
    for frac, d in flat:
        t = tf(T, frac)
        for c, v in d.items():
            if c in arrive:
                if frac == 0: S.K(c, arrive[c], v); continue
                if t < arrive[c] + 0.05: continue
            S.K(c, t, v)
    for c, tr in S.tr.items():                           # controls this move does not use drift back to neutral
        if c in used or c in NO_RELEASE or c.startswith('ft_'): continue
        cur = S.get(c, ts)
        if abs(cur - tr.default) > 1e-4:
            if c in ROTC: gap = abs((cur - tr.default + 180.0) % 360.0 - 180.0)       # whole turns already spun don't count
            elif c.startswith(('sp_', 'hd_')): gap = abs(cur - tr.default)
            else: gap = 0.0
            dur = min(release + gap / 150.0, 0.9 * (T[-1] - ts))                          # never longer than the move itself
            S.K(c, ts, cur); S.K(c, ts + dur, tr.default)
    S.log.append((ts, T[-1], name, sig + ('M' if mirror else '')))
    return T[-1]

def jit(rng, v, f=0.15): return v * (1.0 + rng.uniform(-f, f))

# ------------------------------------------------------------------ foot helpers
def stance_now(S, t):
    return {sd: (S.get('ft_wx_' + sd, t), S.get('ft_wz_' + sd, t), S.get('ft_yaw_' + sd, t)) for sd in SIDES}
def hold_feet(S, t0, t1):
    for sd in SIDES:
        for k in ('ft_wx_', 'ft_wz_', 'ft_yaw_', 'ft_pitch_', 'ft_w_', 'ft_lift_'):
            v = S.get(k + sd, t0); S.K(k + sd, t0, v); S.K(k + sd, t1, v)
def step_foot(S, sd, t0, t1, nx, nz, yaw=None, lift=1800):
    """Lift the foot, swing it to (nx, nz) (mesh units) and plant it again."""
    ox, oz, oy = S.get('ft_wx_' + sd, t0), S.get('ft_wz_' + sd, t0), S.get('ft_yaw_' + sd, t0)
    tm = (t0 + t1) / 2
    for k, v0, v1, vm in (('ft_wx_', ox, nx, (ox + nx) / 2), ('ft_wz_', oz, nz, (oz + nz) / 2)):
        S.K(k + sd, t0, v0); S.K(k + sd, tm, vm); S.K(k + sd, t1, v1)
    S.K('ft_lift_' + sd, t0, S.get('ft_lift_' + sd, t0)); S.K('ft_lift_' + sd, tm, lift); S.K('ft_lift_' + sd, t1, 0.0)
    S.K('ft_pitch_' + sd, t0, 0.0); S.K('ft_pitch_' + sd, tm, -14.0); S.K('ft_pitch_' + sd, t1, 0.0)
    if yaw is not None: S.K('ft_yaw_' + sd, t0, oy); S.K('ft_yaw_' + sd, t1, yaw)

# ================================================================== GROUND MOVES (planted feet)
def m_breathe(S, t0, rng):
    nb = rng.choice([4, 4, 6]); keys = []
    for i in range(nb):
        keys += [(i, {'sp_bend': jit(rng, 1.5), 'shl_up_l': jit(rng, 2.5), 'shl_up_r': jit(rng, 2.5), 'hd_pitch': jit(rng, -2, 0.5), 'pel_dy': 0.0}),
                 (i + 0.6, {'sp_bend': -0.5, 'shl_up_l': 0.0, 'shl_up_r': 0.0, 'hd_pitch': 1.0, 'pel_dy': -0.004})]
    keys.append((nb, {'sp_bend': 0.0, 'hd_yaw': rng.uniform(-14, 14)}))
    return place(S, 'breathe', t0, nb, keys, rng, sig='%.2f' % keys[0][1]['sp_bend'])

def m_bounce(S, t0, rng):
    nb = rng.choice([4, 4, 8]); a = rng.uniform(0.022, 0.05); keys = []
    for i in range(nb):
        k = jit(rng, 1.0, 0.25)
        keys += [(i, {'pel_dy': -a * k, 'sp_bend': 3.5 * k, 'hd_pitch': 5.0 * k, 'pel_pitch': 2.0 * k, 'shl_up_l': -1.5, 'shl_up_r': -1.5}),
                 (i + 0.55, {'pel_dy': 0.12 * a, 'sp_bend': 0.2, 'hd_pitch': -1.5, 'pel_pitch': 0.0, 'shl_up_l': 1.0, 'shl_up_r': 1.0})]
    return place(S, 'bounce', t0, nb, keys, rng, sig='%.3f' % a)

def m_bounce_arms(S, t0, rng):
    """Bounce with the arms swinging out of phase (hip-hop bounce)."""
    nb = 4; a = rng.uniform(0.03, 0.055); keys = []
    sw = rng.uniform(0.10, 0.17)
    for i in range(nb):
        s = 1 if i % 2 == 0 else -1
        keys += [(i, {'pel_dy': -a, 'sp_bend': 4.0, 'hd_pitch': 6.0, 'arm_w_l': 1, 'arm_w_r': 1,
                      'arm_x_l': 0.10, 'arm_y_l': -0.05 + s * 0.06, 'arm_z_l': 0.10 + s * 0.06, 'arm_x_r': 0.10, 'arm_y_r': -0.05 - s * 0.06, 'arm_z_r': 0.10 - s * 0.06,
                      'curl_l': 0.6, 'curl_r': 0.6, 'arm_elb_l': 0.6, 'arm_elb_r': 0.6}),
                 (i + 0.55, {'pel_dy': 0.1 * a, 'sp_bend': 0.5, 'hd_pitch': -1.0})]
    keys.append((nb, {'arm_w_l': 0, 'arm_w_r': 0}))
    return place(S, 'bounce_arms', t0, nb, keys, rng, sig='%.3f' % a)

def m_rock(S, t0, rng):
    nb = rng.choice([4, 4, 6]); keys = []; a = rng.uniform(0.03, 0.055); r = rng.uniform(3, 7)
    for i in range(nb):
        s = 1 if i % 2 == 0 else -1
        keys.append((i, {'pel_dx': s * a, 'pel_roll': -s * r, 'sp_lean': s * r * 1.2, 'hd_roll': -s * 5, 'pel_dy': -0.012, 'hd_yaw': s * rng.uniform(4, 12),
                         'shl_up_l': 2.0 * s, 'shl_up_r': -2.0 * s}))
    return place(S, 'rock', t0, nb, keys, rng, sig='%.3f' % a)

def m_mic_rap(S, t0, rng):
    """One hand holds the mic at the mouth; the other gestures. Head and chest nod to the flow."""
    nb = rng.choice([4, 6, 8]); keys = []
    free = rng.choice(['rest', 'open', 'point', 'fist', 'chop'])
    for i in range(nb):
        nod = rng.uniform(4, 9); lean = rng.uniform(3, 9)
        d = {'arm_w_r': 1, 'arm_x_r': jit(rng, -0.085, 0.2), 'arm_y_r': jit(rng, 0.08, 0.2), 'arm_z_r': jit(rng, 0.095, 0.2), 'curl_r': 0.95, 'thumb_r': 0.7, 'arm_elb_r': 0.8,
             'hd_pitch': nod, 'sp_bend': lean, 'pel_dy': -rng.uniform(0.008, 0.02), 'hd_yaw': rng.uniform(-8, 8), 'sp_twist': rng.uniform(-6, 6)}
        if free == 'open': d.update({'arm_w_l': 1, 'arm_x_l': jit(rng, 0.2), 'arm_y_l': jit(rng, -0.02, 0.8), 'arm_z_l': jit(rng, 0.12), 'curl_l': 0.1, 'arm_elb_l': 0.5})
        elif free == 'point': d.update({'arm_w_l': 1, 'arm_x_l': jit(rng, 0.07), 'arm_y_l': jit(rng, 0.02, 1.5), 'arm_z_l': 0.27, 'curl_l': 0.9, 'point_l': 1, 'arm_elb_l': 0.3})
        elif free == 'fist': d.update({'arm_w_l': 1, 'arm_x_l': 0.1, 'arm_y_l': jit(rng, 0.14, 0.4), 'arm_z_l': 0.08, 'curl_l': 1.0, 'arm_elb_l': 0.6})
        elif free == 'chop': d.update({'arm_w_l': 1, 'arm_x_l': 0.06, 'arm_y_l': jit(rng, -0.1, 0.5), 'arm_z_l': 0.22, 'curl_l': 0.0, 'arm_elb_l': 0.4})
        keys += [(i, d), (i + 0.6, {'hd_pitch': nod * 0.35, 'sp_bend': lean * 0.4, 'pel_dy': 0.0})]
    return place(S, 'mic_rap_' + free, t0, nb, keys, rng, sig=free)

def m_point_crowd(S, t0, rng):
    nb = rng.choice([4, 6]); keys = []; x0 = rng.uniform(-0.12, 0.0); x1 = rng.uniform(0.04, 0.16)
    for i in range(nb):
        u = i / max(1, nb - 1)
        keys.append((i, {'arm_w_r': 1, 'arm_x_r': x0 + (x1 - x0) * u, 'arm_y_r': jit(rng, 0.02, 1.5), 'arm_z_r': 0.28, 'curl_r': 0.9, 'point_r': 1, 'arm_elb_r': 0.3,
                         'arm_w_l': 1, 'arm_x_l': -0.08, 'arm_y_l': 0.03, 'arm_z_l': 0.1, 'curl_l': 1.0, 'arm_elb_l': 0.7,
                         'sp_twist': -(x0 + (x1 - x0) * u) * 80, 'hd_yaw': -(x0 + (x1 - x0) * u) * 110, 'sp_bend': rng.uniform(4, 10), 'pel_dy': -0.012, 'hd_pitch': rng.uniform(-2, 4)}))
    return place(S, 'point_crowd', t0, nb, keys, rng, sig='%.2f' % x0)

def m_fist_pump(S, t0, rng):
    nb = rng.choice([4, 4, 6]); keys = []; hi = rng.uniform(0.16, 0.27)
    for i in range(nb):
        up = (i % 2 == 0)
        keys += [(i, {'arm_w_r': 1, 'arm_x_r': 0.09, 'arm_y_r': hi if up else 0.02, 'arm_z_r': 0.07 if up else 0.12, 'curl_r': 1.0, 'arm_elb_r': 0.7,
                      'pel_dy': -0.025 if not up else 0.0, 'sp_bend': 5.0 if not up else -3.0, 'hd_pitch': 6.0 if not up else -2.0, 'hd_yaw': rng.uniform(-10, 10),
                      'arm_w_l': 1 if rng.random() < 0.4 else 0, 'arm_x_l': 0.1, 'arm_y_l': 0.03, 'arm_z_l': 0.1, 'curl_l': 0.9})]
    return place(S, 'fist_pump', t0, nb, keys, rng, sig='%.2f' % hi)

def m_arms_wide(S, t0, rng):
    nb = rng.choice([4, 6]); keys = []; hgt = rng.uniform(-0.02, 0.12)
    for i in range(nb):
        k = 1.0 if i % 2 == 0 else 0.8
        keys.append((i, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.27 * k, 'arm_x_r': 0.27 * k, 'arm_y_l': hgt + rng.uniform(-0.03, 0.04), 'arm_y_r': hgt + rng.uniform(-0.03, 0.04),
                         'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.0, 'curl_r': 0.0, 'thumb_l': 0.0, 'thumb_r': 0.0, 'arm_elb_l': 0.2, 'arm_elb_r': 0.2,
                         'sp_bend': -8, 'hd_pitch': -9, 'pel_dy': -0.01 * (i % 2), 'shl_up_l': -2, 'shl_up_r': -2}))
    return place(S, 'arms_wide', t0, nb, keys, rng, sig='%.2f' % hgt)

def m_crouch_hype(S, t0, rng):
    nb = rng.choice([4, 4, 6]); keys = []; depth = rng.uniform(0.10, 0.19)
    for i in range(nb):
        up = i % 2 == 1
        keys.append((i, {'pel_dy': -depth * (0.5 if up else 1.0), 'sp_bend': 14 if not up else 8, 'hd_pitch': -6 + (6 if not up else 0), 'pel_pitch': 4,
                         'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12, 'arm_x_r': 0.12, 'arm_y_l': -0.12 if not up else 0.05, 'arm_y_r': -0.12 if not up else 0.05, 'arm_z_l': 0.1, 'arm_z_r': 0.1,
                         'curl_l': 1.0, 'curl_r': 1.0, 'arm_elb_l': 0.7, 'arm_elb_r': 0.7}))
    return place(S, 'crouch_hype', t0, nb, keys, rng, sig='%.2f' % depth)

def m_head_bang(S, t0, rng):
    nb = rng.choice([4, 6, 8]); keys = []
    for i in range(nb):
        keys += [(i, {'hd_pitch': rng.uniform(22, 34), 'sp_bend': rng.uniform(10, 18), 'pel_dy': -0.03, 'hd_yaw': rng.uniform(-14, 14), 'hd_roll': rng.uniform(-8, 8),
                      'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12, 'arm_x_r': 0.12, 'arm_y_l': 0.0, 'arm_y_r': 0.0, 'arm_z_l': 0.12, 'arm_z_r': 0.12, 'curl_l': 1.0, 'curl_r': 1.0}),
                 (i + 0.55, {'hd_pitch': -8, 'sp_bend': 1, 'pel_dy': 0.0})]
    return place(S, 'head_bang', t0, nb, keys, rng)

def m_chest_beat(S, t0, rng):
    nb = rng.choice([4, 4]); keys = []
    for i in range(nb):
        sd = 'r' if i % 2 == 0 else 'l'; o = 'l' if sd == 'r' else 'r'
        keys += [(i, {'arm_w_' + sd: 1, 'arm_x_' + sd: -0.04, 'arm_y_' + sd: -0.06, 'arm_z_' + sd: 0.1, 'curl_' + sd: 1.0, 'arm_elb_' + sd: 0.9,
                      'arm_w_' + o: 1, 'arm_x_' + o: 0.15, 'arm_y_' + o: 0.02, 'arm_z_' + o: 0.02, 'curl_' + o: 0.4,
                      'sp_bend': -5, 'hd_pitch': -6, 'pel_dy': -0.02}),
                 (i + 0.45, {'arm_x_' + sd: -0.01, 'arm_z_' + sd: 0.07, 'sp_bend': 2, 'hd_pitch': 1})]
    return place(S, 'chest_beat', t0, nb, keys, rng)

def m_clap_overhead(S, t0, rng):
    nb = rng.choice([4, 4]); keys = []
    for i in range(nb):
        keys += [(i, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.02, 'arm_x_r': 0.02, 'arm_y_l': 0.27, 'arm_y_r': 0.27, 'arm_z_l': 0.05, 'arm_z_r': 0.05, 'curl_l': 0.0, 'curl_r': 0.0, 'arm_elb_l': 0.2, 'arm_elb_r': 0.2,
                      'sp_bend': -7, 'hd_pitch': -8, 'pel_dy': 0.0}),
                 (i + 0.5, {'arm_x_l': 0.2, 'arm_x_r': 0.2, 'arm_y_l': 0.22, 'arm_y_r': 0.22, 'pel_dy': -0.03})]
    return place(S, 'clap_overhead', t0, nb, keys, rng)

def m_lean_back_rap(S, t0, rng):
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb):
        keys.append((i, {'sp_bend': -rng.uniform(10, 17), 'pel_pitch': -3, 'hd_pitch': -rng.uniform(4, 12), 'pel_dz': -0.02, 'pel_dy': -0.02,
                         'arm_w_r': 1, 'arm_x_r': -0.08, 'arm_y_r': 0.1, 'arm_z_r': 0.09, 'curl_r': 0.95, 'arm_elb_r': 0.8,
                         'arm_w_l': 1, 'arm_x_l': 0.26, 'arm_y_l': jit(rng, 0.12, 0.5), 'arm_z_l': 0.04, 'curl_l': 0.0, 'hd_yaw': rng.uniform(-12, 12)}))
    return place(S, 'lean_back_rap', t0, nb, keys, rng)

def m_star_grab(S, t0, rng):
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb):
        sd = 'l' if i % 2 == 0 else 'r'; o = flipname('_' + sd)[1:]
        keys += [(i, {'arm_w_' + sd: 1, 'arm_x_' + sd: 0.08, 'arm_y_' + sd: rng.uniform(0.25, 0.29), 'arm_z_' + sd: rng.uniform(0.02, 0.1), 'curl_' + sd: 0.0, 'arm_elb_' + sd: 0.2,
                      'sp_bend': -10, 'hd_pitch': -16, 'sp_lean': -4 if sd == 'l' else 4}),
                 (i + 0.55, {'curl_' + sd: 1.0, 'arm_y_' + sd: rng.uniform(0.2, 0.25)})]
    return place(S, 'star_grab', t0, nb, keys, rng)

def m_light_sweep(S, t0, rng):
    """Both arms sweep across the sky like conducting waves of light."""
    nb = rng.choice([6, 8]); keys = []; ph = rng.uniform(0, 3.14)
    for i in range(nb):
        s = math.sin(ph + i * math.pi / 2)
        keys.append((i, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12 + 0.12 * s, 'arm_x_r': 0.12 - 0.12 * s, 'arm_y_l': 0.13 + 0.07 * math.cos(ph + i), 'arm_y_r': 0.13 - 0.07 * math.cos(ph + i),
                         'arm_z_l': 0.1, 'arm_z_r': 0.1, 'curl_l': 0.1, 'curl_r': 0.1, 'sp_twist': 12 * s, 'sp_lean': -6 * s, 'hd_yaw': 10 * s, 'hd_pitch': -6, 'pel_dx': 0.02 * s, 'pel_dy': -0.01}))
    return place(S, 'light_sweep', t0, nb, keys, rng)

def m_hands_head(S, t0, rng):
    nb = 4; keys = []
    for i in range(nb):
        keys.append((i, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.03, 'arm_x_r': 0.03, 'arm_y_l': 0.12 + rng.uniform(0, 0.04), 'arm_y_r': 0.12 + rng.uniform(0, 0.04), 'arm_z_l': 0.04, 'arm_z_r': 0.04,
                         'curl_l': 0.5, 'curl_r': 0.5, 'arm_elb_l': 1.0, 'arm_elb_r': 1.0, 'hd_pitch': -6 + 8 * (i % 2), 'sp_bend': 8 * (i % 2), 'pel_dy': -0.02 * (i % 2)}))
    return place(S, 'hands_head', t0, nb, keys, rng)

def m_jump(S, t0, rng):
    """Crouch on a beat, launch on the next, land on the one after: feet leave the ground and re-plant."""
    T = beat_times(S, t0, 3); ts = T[0]
    h = rng.uniform(0.08, 0.15); crouch = rng.uniform(0.07, 0.12); air = (T[2] - T[1]) * 0.55
    t_take = T[1]; t_land = T[1] + air
    hold_feet(S, ts, T[3])
    pos_x = {sd: S.get('ft_wx_' + sd, ts) for sd in SIDES}; pos_z = {sd: S.get('ft_wz_' + sd, ts) for sd in SIDES}
    for sd in SIDES:
        S.K('ft_w_' + sd, ts, 1.0); S.K('ft_w_' + sd, t_take - 0.02, 1.0); S.K('ft_w_' + sd, t_take + 0.02, 0.0)
        S.K('ft_w_' + sd, t_land - 0.06, 0.0); S.K('ft_w_' + sd, t_land, 1.0)
        S.K('ft_ry_' + sd, t_take, S.get('ft_ry_' + sd, ts)); S.K('ft_ry_' + sd, t_take + air * 0.5, S.get('ft_ry_' + sd, ts) + 0.07); S.K('ft_ry_' + sd, t_land - 0.03, S.get('ft_ry_' + sd, ts))
        S.K('ft_rz_' + sd, t_take, S.get('ft_rz_' + sd, ts)); S.K('ft_rz_' + sd, t_take + air * 0.5, S.get('ft_rz_' + sd, ts) - 0.03)
        S.K('ft_wx_' + sd, t_land - 0.1, pos_x[sd]); S.K('ft_wz_' + sd, t_land - 0.1, pos_z[sd])
    keys = [(0, {'pel_dy': 0.0}), ((T[0] - T[0]) + 0.6, {'pel_dy': -crouch, 'sp_bend': 14, 'hd_pitch': 6, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.1, 'arm_x_r': 0.1, 'arm_y_l': -0.17, 'arm_y_r': -0.17, 'arm_z_l': -0.05, 'arm_z_r': -0.05, 'curl_l': 0.8, 'curl_r': 0.8})]
    out = place(S, 'jump', t0, 3, keys, rng, mirror=False)
    # vertical arc and arm throw by absolute time
    for t_, dy in ((t_take - 0.05, -crouch), (t_take + air * 0.5, h), (t_land - 0.02, 0.0), (t_land + 0.12, -crouch * 0.7), (T[3], 0.0)):
        S.K('pel_dy', t_, dy)
    for sd in SIDES:
        S.K('arm_y_' + sd, t_take + 0.02, 0.24); S.K('arm_x_' + sd, t_take + 0.02, 0.18); S.K('arm_z_' + sd, t_take + 0.02, 0.0); S.K('curl_' + sd, t_take + 0.02, 0.0)
        S.K('arm_y_' + sd, t_land + 0.1, -0.1); S.K('arm_w_' + sd, T[3], 0.0)
    S.K('sp_bend', t_take, -6); S.K('sp_bend', t_land + 0.1, 12); S.K('sp_bend', T[3], 0.0)
    S.log[-1] = (ts, T[3], 'jump', '%.2f' % h)
    return T[3]

def m_step_touch(S, t0, rng):
    nb = rng.choice([4, 4, 6]); T = beat_times(S, t0, nb); ts = T[0]
    hold_feet(S, ts, T[nb])
    sd = rng.choice(['l', 'r']); dirn = 1 if sd == 'l' else -1; wide = rng.uniform(0.07, 0.12) * BODY_H
    x0 = {k: S.get('ft_wx_' + k, ts) for k in SIDES}
    keys = []
    for i in range(nb):
        out = (i % 2 == 0)
        a, b = T[i], T[min(i + 1, nb)]
        if out: step_foot(S, sd, a, a + (b - a) * 0.8, x0[sd] + dirn * wide, S.get('ft_wz_' + sd, ts))
        else: step_foot(S, sd, a, a + (b - a) * 0.8, x0[sd], S.get('ft_wz_' + sd, ts))
        keys.append((i, {'pel_dx': dirn * 0.04 * (1 if out else 0.3), 'pel_roll': -dirn * 4 * (1 if out else 0), 'pel_dy': -0.025, 'sp_lean': dirn * 5 * (1 if out else 0), 'hd_pitch': 5,
                         'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12 + (0.1 if out else 0), 'arm_x_r': 0.12 + (0.1 if out else 0), 'arm_y_l': -0.05, 'arm_y_r': -0.05, 'arm_z_l': 0.1, 'arm_z_r': 0.1, 'curl_l': 0.6, 'curl_r': 0.6}))
    keys.append((nb, {'arm_w_l': 0, 'arm_w_r': 0}))
    return place(S, 'step_touch', t0, nb, keys, rng, mirror=False, sig=sd)

def m_stomp(S, t0, rng):
    """Raise a foot high and stomp it down on the beat; shoulders shake, big body drop."""
    T = beat_times(S, t0, 2); ts = T[0]; sd = rng.choice(['l', 'r']); other = 'r' if sd == 'l' else 'l'
    hold_feet(S, ts, T[2]); up = T[1] - (T[1] - T[0]) * 0.15
    S.K('ft_lift_' + sd, ts, 0.0); S.K('ft_lift_' + sd, up - (T[1] - T[0]) * 0.35, rng.uniform(3500, 5200)); S.K('ft_lift_' + sd, T[1], 0.0)
    S.K('ft_pitch_' + sd, ts, 0.0); S.K('ft_pitch_' + sd, up - (T[1] - T[0]) * 0.35, -12.0); S.K('ft_pitch_' + sd, T[1], 4.0); S.K('ft_pitch_' + sd, T[1] + 0.2, 0.0)
    s_ = 1 if sd == 'l' else -1
    keys = [(0, {'pel_dx': -s_ * 0.03, 'pel_roll': s_ * 4, 'sp_lean': -s_ * 4, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.22, 'arm_x_r': 0.22, 'arm_y_l': 0.1, 'arm_y_r': 0.1, 'arm_z_l': 0.05, 'arm_z_r': 0.05, 'curl_l': 0.8, 'curl_r': 0.8, 'sp_bend': -4}),
            (0.65, {'pel_dy': 0.01, 'sp_bend': -8, 'hd_pitch': -8}),
            (1.0, {'pel_dy': -0.07, 'sp_bend': 16, 'hd_pitch': 12, 'shl_up_l': 4, 'shl_up_r': 4, 'arm_y_l': -0.06, 'arm_y_r': -0.06, 'pel_dx': 0.0, 'pel_roll': 0}),
            (1.5, {'pel_dy': -0.02, 'sp_bend': 5, 'hd_pitch': 3, 'shl_up_l': -2, 'shl_up_r': -2}), (2.0, {'arm_w_l': 0, 'arm_w_r': 0})]
    return place(S, 'stomp', t0, 2, keys, rng, mirror=False, sig=sd)

def m_kick_step(S, t0, rng):
    T = beat_times(S, t0, 2); ts = T[0]; sd = rng.choice(['l', 'r']); hold_feet(S, ts, T[2])
    S.K('ft_wz_' + sd, ts, S.get('ft_wz_' + sd, ts)); S.K('ft_wz_' + sd, T[0] + (T[1] - T[0]) * 0.5, S.get('ft_wz_' + sd, ts) + rng.uniform(2500, 4200)); S.K('ft_wz_' + sd, T[1], S.get('ft_wz_' + sd, ts))
    S.K('ft_lift_' + sd, ts, 0.0); S.K('ft_lift_' + sd, T[0] + (T[1] - T[0]) * 0.5, rng.uniform(1800, 3200)); S.K('ft_lift_' + sd, T[1], 0.0)
    keys = [(0, {'sp_bend': 3}), (0.5, {'sp_bend': -10, 'hd_pitch': -6, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.22, 'arm_x_r': 0.22, 'arm_y_l': 0.02, 'arm_y_r': 0.02, 'curl_l': 0.2, 'curl_r': 0.2}),
            (1.0, {'sp_bend': 6, 'pel_dy': -0.03}), (2.0, {'arm_w_l': 0, 'arm_w_r': 0})]
    return place(S, 'kick_step', t0, 2, keys, rng, mirror=False, sig=sd)

def m_look_around(S, t0, rng):
    """Intro: slow, curious scan; hands hang, shoulders roll."""
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb):
        keys.append((i, {'hd_yaw': rng.uniform(-35, 35), 'hd_pitch': rng.uniform(-12, 8), 'hd_roll': rng.uniform(-6, 6), 'sp_twist': rng.uniform(-8, 8), 'shl_up_l': rng.uniform(-2, 4), 'shl_up_r': rng.uniform(-2, 4), 'pel_dx': rng.uniform(-0.02, 0.02)}))
    return place(S, 'look_around', t0, nb, keys, rng)

def m_open_arms_slow(S, t0, rng):
    """Intro: arms rise slowly, palms open, chest lifts, head tips up."""
    nb = rng.choice([4, 6]); keys = [(0, {'arm_w_l': 0, 'arm_w_r': 0})]
    top = rng.uniform(0.02, 0.1)
    keys.append((nb * 0.55, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.24, 'arm_x_r': 0.24, 'arm_y_l': top, 'arm_y_r': top, 'arm_z_l': 0.03, 'arm_z_r': 0.03, 'curl_l': 0.0, 'curl_r': 0.0, 'thumb_l': 0.0, 'thumb_r': 0.0,
                                'sp_bend': -9, 'hd_pitch': -10, 'arm_elb_l': 0.2, 'arm_elb_r': 0.2}))
    keys.append((nb, {'sp_bend': -6, 'hd_pitch': -6, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.22, 'arm_x_r': 0.22, 'arm_y_l': top - 0.02, 'arm_y_r': top - 0.02}))
    return place(S, 'open_arms_slow', t0, nb, keys, rng, mirror=False)

def m_fist_clench(S, t0, rng):
    nb = 4; keys = []
    for i in range(nb):
        keys.append((i, {'curl_l': 1.0 if i % 2 == 0 else 0.25, 'curl_r': 1.0 if i % 2 == 1 else 0.25, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.11, 'arm_x_r': 0.11, 'arm_y_l': -0.12, 'arm_y_r': -0.12, 'arm_z_l': 0.09, 'arm_z_r': 0.09,
                         'sp_bend': 3 + 2 * (i % 2), 'hd_pitch': rng.uniform(-4, 6), 'hd_yaw': rng.uniform(-10, 10)}))
    keys.append((nb, {'arm_w_l': 0, 'arm_w_r': 0}))
    return place(S, 'fist_clench', t0, nb, keys, rng)

GROUND = {'bounce': (m_bounce, 3), 'bounce_arms': (m_bounce_arms, 3), 'rock': (m_rock, 2), 'mic_rap': (m_mic_rap, 3), 'point_crowd': (m_point_crowd, 2), 'fist_pump': (m_fist_pump, 3),
          'arms_wide': (m_arms_wide, 2), 'crouch_hype': (m_crouch_hype, 3), 'head_bang': (m_head_bang, 3), 'chest_beat': (m_chest_beat, 2), 'clap_overhead': (m_clap_overhead, 2),
          'lean_back_rap': (m_lean_back_rap, 2), 'star_grab': (m_star_grab, 2), 'light_sweep': (m_light_sweep, 2), 'hands_head': (m_hands_head, 2), 'jump': (m_jump, 3),
          'step_touch': (m_step_touch, 2), 'stomp': (m_stomp, 3), 'kick_step': (m_kick_step, 2), 'breathe': (m_breathe, 1), 'look_around': (m_look_around, 1),
          'open_arms_slow': (m_open_arms_slow, 1), 'fist_clench': (m_fist_clench, 1)}
