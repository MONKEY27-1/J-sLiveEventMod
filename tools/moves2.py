"""Travel, airborne (levitation / seated on a planet), underwater and finale moves. Same conventions as moves.py."""
from moves import *

def stance_off(sd):
    """Default foot offset from the pelvis in the ground plane (mesh units)."""
    return BP[N('foot_' + sd)][0] - PEL_B[0], BP[N('foot_' + sd)][2] - PEL_B[2]

# ------------------------------------------------------------------ travel on the ground
def m_walk(S, t0, rng, dx=None, dz=None, style=None):
    """Real steps: pelvis travels, feet leapfrog, every foot lands where the hips will be. Ends with a closing step."""
    if dx is None: dx = rng.choice([-1, 1]) * rng.uniform(0.18, 0.42)
    if dz is None: dz = rng.uniform(-0.25, 0.25)
    nb = rng.choice([4, 6, 6, 8]); style = style or rng.choice(['swagger', 'plain', 'rap', 'point'])
    T = beat_times(S, t0, nb + 2); ts = T[0]
    p0 = (S.get('home_x', ts), S.get('home_z', ts)); L = math.hypot(dx, dz) or 1.0; ux, uz = dx / L, dz / L
    per = (dx / nb, dz / nb)
    hold_feet(S, ts, ts)
    for sd in SIDES: S.K('ft_w_' + sd, ts, 1.0)
    for i in range(nb + 1):
        sd = 'l' if i % 2 == 0 else 'r'; a, b = T[i], T[i + 1]
        last = (i >= nb)
        px = p0[0] + per[0] * min(i + 1, nb); pz = p0[1] + per[1] * min(i + 1, nb)
        ox, oz = stance_off(sd)
        lead = 0.0 if last else 1.0
        nx = PEL_B[0] + px * BODY_H + ox + per[0] * BODY_H * lead
        nz = PEL_B[2] + pz * BODY_H + oz + per[1] * BODY_H * lead
        step_foot(S, sd, a, a + (b - a) * 0.85, nx, nz, lift=rng.uniform(900, 1500))
        # pelvis keys: smooth travel plus a small bob at every contact
        S.K('home_x', a, p0[0] + per[0] * min(i, nb)); S.K('home_z', a, p0[1] + per[1] * min(i, nb))
        S.K('pel_dy', a, -0.014); S.K('pel_dy', (a + b) / 2, 0.004)
        s_ = 1 if sd == 'l' else -1
        S.K('sp_twist', a, s_ * 7); S.K('pel_yaw', a, -s_ * 5); S.K('pel_roll', a, s_ * 2.5); S.K('hd_pitch', a, rng.uniform(0, 5))
        a0 = a + (b - a) * 0.3 if i == 0 else a
        if style == 'swagger':
            for sdd in SIDES:
                S.K('arm_w_' + sdd, a0, 1.0); S.K('arm_x_' + sdd, a, 0.17); S.K('arm_y_' + sdd, a, -0.12 + 0.03 * (s_ * sgn(sdd))); S.K('arm_z_' + sdd, a, 0.04 - 0.05 * s_ * sgn(sdd)); S.K('curl_' + sdd, a, 0.8)
        elif style == 'rap':
            S.K('arm_w_r', a, 1.0); S.K('arm_x_r', a, -0.085); S.K('arm_y_r', a, 0.08); S.K('arm_z_r', a, 0.095); S.K('curl_r', a, 0.95); S.K('arm_elb_r', a, 0.8)
            S.K('arm_w_l', a, 1.0); S.K('arm_x_l', a, 0.2); S.K('arm_y_l', a, -0.08 + 0.04 * s_); S.K('arm_z_l', a, 0.1); S.K('curl_l', a, 0.2)
        elif style == 'point':
            S.K('arm_w_r', a, 1.0); S.K('arm_x_r', a, 0.06); S.K('arm_y_r', a, 0.04 + 0.03 * s_); S.K('arm_z_r', a, 0.27); S.K('curl_r', a, 0.9); S.K('point_r', a, 1.0)
    te = T[nb + 1]
    for k in ('pel_dy', 'sp_twist', 'pel_yaw', 'pel_roll', 'hd_pitch'): S.K(k, te, 0.0)
    S.K('home_x', te, p0[0] + dx); S.K('home_z', te, p0[1] + dz)
    for sdd in SIDES: S.K('arm_w_' + sdd, te, 0.0); S.K('point_' + sdd, te, 0.0)
    S.log.append((ts, te, 'walk_' + style, '%.2f,%.2f' % (dx, dz)))
    return te

def pelvis_xz(S, t): return S.get('home_x', t), S.get('home_z', t)

def recentre(S, t0, rng, within=0.55):
    """Walk back toward the middle when the clip has drifted too far from the starting spot."""
    px, pz = pelvis_xz(S, t0)
    if math.hypot(px, pz) < within: return t0
    return m_walk(S, t0, rng, dx=-px * rng.uniform(0.6, 0.9), dz=-pz * rng.uniform(0.6, 0.9), style=rng.choice(['swagger', 'plain']))

# ------------------------------------------------------------------ transitions into / out of the air
def lift_off(S, t0, h, dur=0.9, feet='hang'):
    """Feet leave the ground: ft_w -> 0, pelvis rises h (body heights)."""
    S.tr['lift'].trim(t0)
    for sd in SIDES:
        S.K('ft_w_' + sd, t0, 1.0); S.K('ft_w_' + sd, t0 + dur * 0.55, 0.0)
        for k in ('ft_wx_', 'ft_wz_', 'ft_yaw_', 'ft_lift_', 'ft_pitch_'): S.K(k + sd, t0, S.get(k + sd, t0)); S.K(k + sd, t0 + dur, S.get(k + sd, t0))
    S.K('lift', t0, S.get('lift', t0)); S.K('lift', t0 + dur * 0.15, S.get('lift', t0) - 0.04); S.K('lift', t0 + dur, h)
    return t0 + dur

def land(S, t0, dur=0.9):
    """Back to the ground: planted again under the hips."""
    S.tr['lift'].trim(t0)
    pxz = pelvis_xz(S, t0 + dur)
    for sd in SIDES:
        ox, oz = stance_off(sd)
        wx, wz = PEL_B[0] + pxz[0] * BODY_H + ox, PEL_B[2] + pxz[1] * BODY_H + oz
        S.K('ft_wx_' + sd, t0, wx); S.K('ft_wx_' + sd, t0 + dur, wx); S.K('ft_wz_' + sd, t0, wz); S.K('ft_wz_' + sd, t0 + dur, wz)
        S.K('ft_yaw_' + sd, t0, 0.0); S.K('ft_pitch_' + sd, t0, 0.0); S.K('ft_lift_' + sd, t0, 0.0)
        S.K('ft_w_' + sd, t0 + dur * 0.35, 0.0); S.K('ft_w_' + sd, t0 + dur * 0.95, 1.0)
        for k, d in (('ft_rx_', None), ('ft_ry_', None), ('ft_rz_', None)):
            S.K(k + sd, t0, S.get(k + sd, t0)); S.K(k + sd, t0 + dur, S.tr[k + sd].default)
        S.K('knee_out_' + sd, t0, S.get('knee_out_' + sd, t0)); S.K('knee_out_' + sd, t0 + dur, 8.0)
    for k in ('pel_pitch', 'pel_yaw', 'pel_roll', 'sp_bend', 'sp_twist', 'sp_lean', 'hd_pitch', 'hd_yaw', 'hd_roll'):
        S.K(k, t0, S.get(k, t0)); S.K(k, t0 + dur, 0.0)
    S.K('lift', t0, S.get('lift', t0)); S.K('lift', t0 + dur * 0.85, 0.0)
    S.K('pel_dy', t0 + dur * 0.8, 0.0); S.K('pel_dy', t0 + dur, -0.035); S.K('pel_dy', t0 + dur + 0.3, 0.0)
    for sd in SIDES: S.K('arm_w_' + sd, t0 + dur, 0.0)
    return t0 + dur + 0.3

def stand_rel(sd, x=0.0, y=-0.49, z=0.0): return {'ft_rx_' + sd: x, 'ft_ry_' + sd: y, 'ft_rz_' + sd: z}

def leg_pose(l, r, knee=8.0):
    d = {}
    for sd, p in (('l', l), ('r', r)):
        d.update(stand_rel(sd, *p)); d['knee_out_' + sd] = knee
    return d

# ------------------------------------------------------------------ AIR (levitation)
def m_float_hang(S, t0, rng):
    nb = rng.choice([4, 6, 8]); keys = []; w = rng.uniform(0.18, 0.28); sway = rng.uniform(3, 7)
    for i in range(nb):
        s = 1 if i % 2 == 0 else -1
        d = {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': w + 0.02 * s, 'arm_x_r': w - 0.02 * s, 'arm_y_l': 0.05 + 0.04 * s, 'arm_y_r': 0.05 - 0.04 * s, 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.0, 'curl_r': 0.0, 'arm_elb_l': 0.2, 'arm_elb_r': 0.2,
             'pel_roll': s * sway, 'sp_lean': -s * sway, 'hd_roll': s * 4, 'hd_pitch': rng.uniform(-12, -4), 'sp_bend': -6, 'pel_dy': 0.02 * s}
        d.update(leg_pose((0.08 + 0.02 * s, -0.49, 0.03 * s), (-0.08 + 0.02 * s, -0.49, -0.03 * s)))
        keys.append((i, d))
    return place(S, 'float_hang', t0, nb, keys, rng, sig='%.2f' % w)

def m_superman(S, t0, rng):
    """Flying forward: body pitched down, legs trailing, arms forward or swept back."""
    nb = rng.choice([4, 6]); keys = []; pitch = rng.uniform(55, 80); fwd = rng.random() < 0.6; roll = rng.uniform(-8, 8)
    for i in range(nb):
        k = 1.0 if i % 2 == 0 else 0.7
        d = {'pel_pitch': pitch * k + pitch * 0.3 * (1 - k) + 12, 'pel_roll': roll * (1 if i % 2 else -1), 'hd_pitch': -pitch * 0.55, 'sp_bend': -10, 'hd_yaw': rng.uniform(-10, 10), 'pel_yaw': rng.uniform(-12, 12)}
        if fwd: d.update({'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.07, 'arm_x_r': 0.07, 'arm_y_l': 0.27, 'arm_y_r': 0.27, 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.8, 'curl_r': 0.8, 'arm_elb_l': 0.1, 'arm_elb_r': 0.1})
        else: d.update({'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12, 'arm_x_r': 0.12, 'arm_y_l': -0.25, 'arm_y_r': -0.25, 'arm_z_l': -0.05, 'arm_z_r': -0.05, 'curl_l': 0.0, 'curl_r': 0.0})
        d.update(leg_pose((0.07, -0.49, -0.02 * i), (-0.07, -0.49, 0.02 * i), knee=5))
        keys.append((i, d))
    return place(S, 'superman', t0, nb, keys, rng, sig='%.0f%s' % (pitch, 'f' if fwd else 'b'))

def m_float_spin(S, t0, rng):
    nb = rng.choice([4, 6]); turns = rng.choice([1, 1, 2]) * rng.choice([-1, 1]); y0 = S.get('pel_yaw', t0)
    keys = []
    for i in range(2 * nb + 1):                          # keys every half beat so no step ever reaches 180 deg (the shortest-way unwrap would flip)
        d = {'pel_yaw': y0 + 360.0 * turns * i / (2 * nb), 'pel_roll': rng.uniform(-6, 6), 'sp_bend': -5, 'hd_pitch': -6,
             'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.26, 'arm_x_r': 0.26, 'arm_y_l': 0.06, 'arm_y_r': 0.06, 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.0, 'curl_r': 0.0}
        d.update(leg_pose((0.08, -0.46, 0.02), (-0.08, -0.46, -0.02)))
        keys.append((i / 2.0, d))
    return place(S, 'float_spin', t0, nb, keys, rng, mirror=False, sig=str(turns))

def m_reach_sky(S, t0, rng):
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb):
        sd = 'l' if i % 2 == 0 else 'r'; o = 'r' if sd == 'l' else 'l'
        d = {'arm_w_' + sd: 1, 'arm_x_' + sd: 0.07, 'arm_y_' + sd: rng.uniform(0.25, 0.29), 'arm_z_' + sd: rng.uniform(0.0, 0.09), 'curl_' + sd: 0.0, 'arm_elb_' + sd: 0.15,
             'arm_w_' + o: 1, 'arm_x_' + o: 0.2, 'arm_y_' + o: 0.0, 'arm_z_' + o: 0.0, 'curl_' + o: 0.2,
             'hd_pitch': -22, 'sp_bend': -12, 'pel_pitch': -6, 'sp_lean': 5 if sd == 'l' else -5, 'pel_roll': -4 if sd == 'l' else 4}
        d.update(leg_pose((0.09, -0.48, 0.04), (-0.09, -0.48, -0.03)))
        keys.append((i, d))
    return place(S, 'reach_sky', t0, nb, keys, rng)

def m_float_tuck(S, t0, rng):
    """Curl up into a ball and roll once, then open."""
    nb = 4; keys = []; y0 = S.get('pel_pitch', t0)
    for i in range(nb + 1):
        u = i / nb; tuck = math.sin(math.pi * u)
        d = {'pel_pitch': y0 + 360.0 * u * rng.choice([1]) , 'sp_bend': 40 * tuck, 'hd_pitch': 30 * tuck,
             'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.1 - 0.05 * tuck, 'arm_x_r': 0.1 - 0.05 * tuck, 'arm_y_l': -0.05, 'arm_y_r': -0.05, 'arm_z_l': 0.12 * tuck, 'arm_z_r': 0.12 * tuck, 'curl_l': 0.9, 'curl_r': 0.9}
        d.update(leg_pose((0.09, -0.49 + 0.26 * tuck, 0.0 + 0.18 * tuck), (-0.09, -0.49 + 0.26 * tuck, 0.0 + 0.18 * tuck), knee=10))
        keys.append((i, d))
    return place(S, 'float_tuck', t0, nb, keys, rng, mirror=False)

def m_seat_cross(S, t0, rng):
    """Cross-legged meditation float: hands rest on the knees, palms up, slow breathing."""
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb):
        d = {'sp_bend': 3 + 2 * (i % 2), 'hd_pitch': 2 - 5 * (i % 2), 'hd_yaw': rng.uniform(-10, 10), 'pel_pitch': -4,
             'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.17, 'arm_x_r': 0.17, 'arm_y_l': -0.17, 'arm_y_r': -0.17, 'arm_z_l': 0.16, 'arm_z_r': 0.16, 'curl_l': 0.3, 'curl_r': 0.3, 'arm_elb_l': 0.5, 'arm_elb_r': 0.5}
        d.update(leg_pose((-0.05, -0.20, 0.20), (0.05, -0.20, 0.20), knee=45))
        keys.append((i, d))
    return place(S, 'seat_cross', t0, nb, keys, rng, mirror=False)

def m_seat_rap(S, t0, rng):
    """Seated on the planet: one leg dangles and swings, mic hand at the mouth, other hand gestures, torso leans in."""
    nb = rng.choice([4, 6, 8]); keys = []; free = rng.choice(['open', 'point', 'rest', 'fist'])
    for i in range(nb):
        sw = math.sin(i * 1.7 + rng.uniform(0, 1))
        d = {'sp_bend': rng.uniform(8, 20), 'hd_pitch': rng.uniform(2, 10), 'hd_yaw': rng.uniform(-14, 14), 'pel_pitch': -8, 'sp_twist': rng.uniform(-8, 8),
             'arm_w_r': 1, 'arm_x_r': jit(rng, -0.085, 0.2), 'arm_y_r': jit(rng, 0.08, 0.2), 'arm_z_r': jit(rng, 0.095, 0.2), 'curl_r': 0.95, 'arm_elb_r': 0.8, 'thumb_r': 0.7}
        if free == 'open': d.update({'arm_w_l': 1, 'arm_x_l': 0.2, 'arm_y_l': rng.uniform(-0.05, 0.06), 'arm_z_l': 0.12, 'curl_l': 0.1, 'arm_elb_l': 0.5})
        elif free == 'point': d.update({'arm_w_l': 1, 'arm_x_l': 0.08, 'arm_y_l': 0.02, 'arm_z_l': 0.27, 'curl_l': 0.9, 'point_l': 1})
        elif free == 'fist': d.update({'arm_w_l': 1, 'arm_x_l': 0.12, 'arm_y_l': rng.uniform(0.0, 0.2), 'arm_z_l': 0.07, 'curl_l': 1.0})
        else: d.update({'arm_w_l': 1, 'arm_x_l': 0.13, 'arm_y_l': -0.16, 'arm_z_l': 0.15, 'curl_l': 0.3})
        d.update(leg_pose((0.09, -0.33, 0.24), (-0.09, -0.33 - 0.07 * sw, 0.22 - 0.1 * sw), knee=10))
        keys.append((i, d))
    return place(S, 'seat_rap_' + free, t0, nb, keys, rng, sig=free)

def m_seat_conduct(S, t0, rng):
    nb = rng.choice([6, 8]); keys = []; ph = rng.uniform(0, 6)
    for i in range(nb):
        s = math.sin(ph + i * 1.3); c = math.cos(ph + i * 1.3)
        d = {'sp_bend': 6, 'hd_pitch': -8, 'sp_twist': 10 * s, 'pel_pitch': -6, 'hd_yaw': 12 * s, 'arm_w_l': 1, 'arm_w_r': 1,
             'arm_x_l': 0.2 + 0.1 * s, 'arm_x_r': 0.2 - 0.1 * s, 'arm_y_l': 0.08 + 0.12 * c, 'arm_y_r': 0.08 - 0.12 * c, 'arm_z_l': 0.1, 'arm_z_r': 0.1, 'curl_l': 0.1, 'curl_r': 0.1}
        d.update(leg_pose((0.09, -0.33, 0.24), (-0.09, -0.33, 0.24), knee=10))
        keys.append((i, d))
    return place(S, 'seat_conduct', t0, nb, keys, rng)

def leave_seat(S, t0, dur=1.0):
    for sd in SIDES:
        for k, v in (('ft_rx_', 0.08 * sgn(sd)), ('ft_ry_', -0.49), ('ft_rz_', 0.0)):
            S.K(k + sd, t0, S.get(k + sd, t0)); S.K(k + sd, t0 + dur, v)
        S.K('knee_out_' + sd, t0, S.get('knee_out_' + sd, t0)); S.K('knee_out_' + sd, t0 + dur, 8.0)
    return t0 + dur

def enter_seat(S, t0, dur=1.2):
    """Settle from floating into the seat: legs fold forward, pelvis tips back slightly."""
    for sd in SIDES:
        S.K('ft_w_' + sd, t0, S.get('ft_w_' + sd, t0)); S.K('ft_w_' + sd, t0 + 0.2, 0.0)
        for k, v in (('ft_rx_', 0.09 * sgn(sd)), ('ft_ry_', -0.33), ('ft_rz_', 0.24)):
            S.K(k + sd, t0, S.get(k + sd, t0)); S.K(k + sd, t0 + dur, v)
    return t0 + dur

# ------------------------------------------------------------------ UNDERWATER
def m_swim_breast(S, t0, rng):
    nb = rng.choice([4, 6, 8]); keys = []; pitch = rng.uniform(58, 78)
    for i in range(nb):
        for ph, (x, y, z, kick) in enumerate(((0.05, 0.27, 0.02, 0.0), (0.22, 0.18, 0.06, 0.0), (0.12, -0.08, 0.1, 1.0))):
            d = {'pel_pitch': pitch + rng.uniform(-4, 4), 'hd_pitch': -pitch * 0.6, 'sp_bend': -8, 'pel_roll': rng.uniform(-3, 3),
                 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': x, 'arm_x_r': x, 'arm_y_l': y, 'arm_y_r': y, 'arm_z_l': z, 'arm_z_r': z, 'curl_l': 0.2, 'curl_r': 0.2, 'arm_elb_l': 0.3, 'arm_elb_r': 0.3}
            d.update(leg_pose((0.07 + 0.05 * kick, -0.49 + 0.25 * (1 - kick) * (ph == 1), -0.05 * kick), (-0.07 - 0.05 * kick, -0.49 + 0.25 * (1 - kick) * (ph == 1), -0.05 * kick)))
            keys.append((i + ph / 3.0, d))
    return place(S, 'swim_breast', t0, nb, keys, rng, mirror=False, sig='%.0f' % pitch)

def m_swim_crawl(S, t0, rng):
    nb = rng.choice([4, 6, 8]); keys = []; pitch = rng.uniform(62, 80); steps = nb * 3
    for i in range(steps + 1):
        phi = i * math.pi / 3.0 * 1.0
        d = {'pel_pitch': pitch, 'hd_pitch': -pitch * 0.6, 'sp_bend': -6, 'sp_twist': 12 * math.sin(phi), 'pel_roll': 7 * math.sin(phi), 'hd_yaw': rng.uniform(-8, 8)}
        for sd, off in (('l', 0.0), ('r', math.pi)):
            d.update({'arm_w_' + sd: 1, 'arm_x_' + sd: 0.07 + 0.03 * math.sin(phi + off), 'arm_y_' + sd: 0.1 + 0.17 * math.cos(phi + off), 'arm_z_' + sd: 0.07 - 0.1 * math.sin(phi + off), 'curl_' + sd: 0.2, 'arm_elb_' + sd: 0.4})
        d.update(leg_pose((0.07, -0.49, 0.05 * math.sin(phi * 3)), (-0.07, -0.49, -0.05 * math.sin(phi * 3))))
        keys.append((i / 3.0, d))
    return place(S, 'swim_crawl', t0, nb, keys, rng, mirror=False, sig='%.0f' % pitch)

def m_swim_tread(S, t0, rng):
    """Upright, sculling hands and a slow bicycle kick."""
    nb = rng.choice([4, 6]); keys = []
    for i in range(nb * 2 + 1):
        s = math.sin(i * math.pi / 2); c = math.cos(i * math.pi / 2)
        d = {'sp_bend': 2, 'hd_pitch': -4 + 3 * c, 'hd_yaw': 10 * s, 'pel_roll': 4 * s, 'sp_lean': -4 * s,
             'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.16 + 0.05 * s, 'arm_x_r': 0.16 - 0.05 * s, 'arm_y_l': -0.12 + 0.03 * c, 'arm_y_r': -0.12 - 0.03 * c, 'arm_z_l': 0.1, 'arm_z_r': 0.1, 'curl_l': 0.2, 'curl_r': 0.2}
        d.update(leg_pose((0.08, -0.45 + 0.07 * s, 0.07 * c), (-0.08, -0.45 - 0.07 * s, -0.07 * c)))
        keys.append((i / 2.0, d))
    return place(S, 'swim_tread', t0, nb, keys, rng, mirror=False)

def m_swim_drift(S, t0, rng):
    """Weightless drift: slow roll with arms trailing, head tilting, legs bending lazily."""
    nb = rng.choice([4, 6, 8]); keys = []; roll = rng.choice([-1, 1]) * rng.uniform(40, 150); pit = rng.uniform(20, 60)
    for i in range(nb + 1):
        u = i / nb
        d = {'pel_roll': roll * u, 'pel_pitch': pit + 12 * math.sin(i * 1.9), 'hd_pitch': -pit * 0.4, 'sp_bend': 4,
             'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.2 + 0.05 * math.sin(i * 1.3), 'arm_x_r': 0.2 + 0.05 * math.cos(i * 1.1), 'arm_y_l': 0.05 * math.sin(i), 'arm_y_r': 0.05 * math.cos(i), 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.1, 'curl_r': 0.1}
        d.update(leg_pose((0.08, -0.4 + 0.1 * math.sin(i * 1.7), 0.08 * math.cos(i * 1.3)), (-0.08, -0.4 + 0.1 * math.cos(i * 1.7), -0.04 * math.sin(i))))
        keys.append((i, d))
    return place(S, 'swim_drift', t0, nb, keys, rng, mirror=False, sig='%.0f' % roll)

def m_swim_dive(S, t0, rng):
    nb = 4; keys = []
    for i in range(nb + 1):
        u = i / nb
        d = {'pel_pitch': 100 * math.sin(math.pi * 0.5 * u) , 'hd_pitch': -30 * u, 'sp_bend': -4, 'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.05, 'arm_x_r': 0.05, 'arm_y_l': 0.27, 'arm_y_r': 0.27, 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 1.0, 'curl_r': 1.0, 'pel_dy': -0.1 * u}
        d.update(leg_pose((0.05, -0.49, -0.04 * u), (-0.05, -0.49, -0.04 * u), knee=5))
        keys.append((i, d))
    return place(S, 'swim_dive', t0, nb, keys, rng, mirror=False)

# ------------------------------------------------------------------ FINALE
def m_fin_charge(S, t0, rng):
    """Crouch with trembling fists: energy gathering."""
    nb = rng.choice([4, 6]); keys = []; depth = rng.uniform(0.13, 0.2)
    for i in range(nb):
        u = i / max(1, nb - 1)
        keys.append((i, {'pel_dy': -depth * (0.6 + 0.4 * u), 'sp_bend': 14 + 12 * u, 'hd_pitch': 10 + 12 * u, 'shk': 0.4 + 0.6 * u, 'pel_pitch': 5,
                         'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.1, 'arm_x_r': 0.1, 'arm_y_l': -0.15 - 0.03 * u, 'arm_y_r': -0.15 - 0.03 * u, 'arm_z_l': 0.1, 'arm_z_r': 0.1, 'curl_l': 1.0, 'curl_r': 1.0, 'arm_elb_l': 0.8, 'arm_elb_r': 0.8}))
    keys.append((nb, {'shk': 0.0}))
    return place(S, 'fin_charge', t0, nb, keys, rng, mirror=False, sig='%.2f' % depth)

def m_fin_roar(S, t0, rng):
    nb = rng.choice([2, 4]); keys = []
    for i in range(nb):
        keys.append((i, {'pel_dy': -0.04, 'sp_bend': -16, 'hd_pitch': -22, 'pel_pitch': -3, 'shk': 0.5,
                         'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.27, 'arm_x_r': 0.27, 'arm_y_l': 0.12, 'arm_y_r': 0.12, 'arm_z_l': 0.0, 'arm_z_r': 0.0, 'curl_l': 0.0, 'curl_r': 0.0, 'thumb_l': 0.0, 'thumb_r': 0.0}))
    keys.append((nb, {'shk': 0.0}))
    return place(S, 'fin_roar', t0, nb, keys, rng, mirror=False)

def m_fin_slam(S, t0, rng):
    """Leap and slam both fists into the ground: crouch, jump, land with a body drop."""
    T = beat_times(S, t0, 3); ts = T[0]; air = min(0.55, (T[2] - T[1]) * 0.6); t_land = T[2] - 0.04; t_take = t_land - air      # lands exactly on the beat
    hold_feet(S, ts, T[3])
    for sd in SIDES:
        S.K('ft_w_' + sd, ts, 1.0); S.K('ft_w_' + sd, t_take - 0.02, 1.0); S.K('ft_w_' + sd, t_take + 0.02, 0.0); S.K('ft_w_' + sd, t_land - 0.05, 0.0); S.K('ft_w_' + sd, t_land, 1.0)
        S.K('ft_ry_' + sd, t_take, S.get('ft_ry_' + sd, ts)); S.K('ft_ry_' + sd, t_take + air * 0.5, S.get('ft_ry_' + sd, ts) + 0.08); S.K('ft_ry_' + sd, t_land - 0.03, S.get('ft_ry_' + sd, ts))
    out = place(S, 'fin_slam', t0, 3, [(0, {'arm_w_l': 1, 'arm_w_r': 1}), (0.6, {'pel_dy': -0.17, 'sp_bend': 20, 'hd_pitch': 10, 'arm_x_l': 0.12, 'arm_x_r': 0.12, 'arm_y_l': -0.2, 'arm_y_r': -0.2, 'arm_z_l': -0.04, 'arm_z_r': -0.04, 'curl_l': 1.0, 'curl_r': 1.0})], rng, mirror=False)
    for t_, dy in ((t_take - 0.05, -0.17), (t_take + air * 0.5, 0.2), (t_land - 0.02, 0.0), (t_land + 0.1, -0.12), (T[3], 0.0)): S.K('pel_dy', t_, dy)
    for sd in SIDES:
        S.K('arm_y_' + sd, t_take + 0.02, 0.27); S.K('arm_x_' + sd, t_take + 0.02, 0.12); S.K('arm_z_' + sd, t_take + 0.02, 0.0)
        S.K('arm_y_' + sd, t_land - 0.05, -0.1); S.K('arm_z_' + sd, t_land - 0.05, 0.22); S.K('arm_x_' + sd, t_land - 0.05, 0.1)
        S.K('arm_y_' + sd, t_land + 0.35, -0.22); S.K('arm_w_' + sd, T[3] + 0.4, 0.0)
    S.K('sp_bend', t_take, -8); S.K('sp_bend', t_land - 0.03, 26); S.K('sp_bend', T[3], 0.0); S.K('hd_pitch', t_land - 0.03, 18)
    S.K('shk', t_land, 0.8); S.K('shk', t_land + 0.5, 0.0)
    S.log[-1] = (ts, T[3], 'fin_slam', '')
    return T[3]

def m_fin_pose(S, t0, rng):
    """Final victory pose: arms up, head back, held."""
    nb = rng.choice([2, 4]); keys = []
    for i in range(nb):
        keys.append((i, {'arm_w_l': 1, 'arm_w_r': 1, 'arm_x_l': 0.12, 'arm_x_r': 0.12, 'arm_y_l': 0.27, 'arm_y_r': 0.27, 'arm_z_l': 0.04, 'arm_z_r': 0.04, 'curl_l': 1.0, 'curl_r': 1.0, 'sp_bend': -14, 'hd_pitch': -18, 'pel_dy': -0.02 * (i % 2)}))
    return place(S, 'fin_pose', t0, nb, keys, rng, mirror=False)

AIR = {'float_hang': (m_float_hang, 3), 'superman': (m_superman, 2), 'float_spin': (m_float_spin, 1), 'reach_sky': (m_reach_sky, 3), 'float_tuck': (m_float_tuck, 1)}
SEAT = {'seat_cross': (m_seat_cross, 1), 'seat_rap': (m_seat_rap, 4), 'seat_conduct': (m_seat_conduct, 2)}
SWIM = {'swim_breast': (m_swim_breast, 3), 'swim_crawl': (m_swim_crawl, 3), 'swim_tread': (m_swim_tread, 2), 'swim_drift': (m_swim_drift, 3), 'swim_dive': (m_swim_dive, 1)}
FINALE = {'fin_charge': (m_fin_charge, 3), 'fin_roar': (m_fin_roar, 2), 'fin_slam': (m_fin_slam, 2), 'fin_pose': (m_fin_pose, 1)}
