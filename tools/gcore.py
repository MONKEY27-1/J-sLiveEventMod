"""Shared machinery for the two Final Showdown giants (ACTOR=robot | monster). Positions in scripts are UNITY-LOCAL METRES (x right, y up, z forward of the show
origin); this module converts them to the authoring frame of the rig (x = ux, z = -uz, yaw = 180 - unity heading) and to body-height fractions."""
import os, sys, math, json, random
ACTOR = os.environ.get('ACTOR', 'robot')
_CFG = {
    'robot': dict(skin=r'C:\Users\drago\Downloads\fortnite_mecha_team_leader__fortnite_event_skin.glb', strip='1', alias='head=head_offset', bodyh='150', height=80.0),
    'monster': dict(skin=r'C:\Users\drago\Downloads\cattus_monsterfortnite.glb', strip='2', alias='spine_05=spine_04', bodyh='160', height=110.0),
}[ACTOR]
os.environ['RIG_SKIN'] = _CFG['skin']; os.environ['RIG_STRIP'] = _CFG['strip']; os.environ['RIG_ALIAS'] = _CFG['alias']; os.environ['RIG_BODYH'] = _CFG['bodyh']
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from choreo_core import *
from hair_sim import HairSim
import hair_sim

if ACTOR == 'robot':                                    # the viewer in the plugin treats emissiveFactor as a flat glow: drop it (the colours are in the base textures)
    for _m in sg.get('materials', []): _m.pop('emissiveFactor', None); _m.pop('emissiveTexture', None)
HEIGHT_M = _CFG['height']
KM = HEIGHT_M / BODY_H                     # metres per mesh unit
FPSG = 20.0
HERE_G = os.path.dirname(os.path.abspath(__file__))

DUMMY = {'beats': [0.5, 1.0], 'duration': 400.0, 'tempos': [[0, 60]], 'fps': 30, 'rms': [0.0], 'vocal': [0.0], 'open': [0.0], 'kick': [0.0], 'high': [0.0], 'sections': []}

# ------------------------------------------------------------------ coordinates
def to_home(ux, uz): return ux / (KM * BODY_H), -uz / (KM * BODY_H)          # unity metres -> home_x, home_z (body-height fractions, authoring frame)
def from_home(hx, hz): return hx * KM * BODY_H, -hz * KM * BODY_H
def yaw_of(heading_deg): return 180.0 - heading_deg                           # unity heading (0 = +Z, 90 = +X) -> authoring pel_yaw
def heading_to(ax, az, bx, bz): return math.degrees(math.atan2(bx - ax, bz - az))
def frac(m): return m / (KM * BODY_H)                                         # metres -> body-height fraction
def wrap_to(prev, ang): return ang + 360.0 * round((prev - ang) / 360.0)

# ------------------------------------------------------------------ rig fixes
REV = (('reverse_wrist_%s', 'hand_%s'), ('reverse_lowerarm_%s', 'upperarm_%s'), ('reverse_upperarm_mid_%s', 'upperarm_%s'), ('reverse_upperarm_%s', 'upperarm_%s'),
       ('reverse_lowerarm_twist_01_%s', 'lowerarm_%s'))
def fix_reverse(P):
    """The monster's arm mesh is skinned to a reversed chain (wrist -> elbow -> shoulder) hanging under the forearm; make it follow the arm it duplicates."""
    for sd in SIDES:
        for rv, src in REV:
            a, b = rv % sd, src % sd
            if a in s_name and b in s_name:
                P[s_name[a]] = [r[:] for r in P[s_name[b]]]
    return P

class GiantHairSim(HairSim):
    """Only the loose parts (cables, tentacles) swing; the 'offset' / plate bones that carry armour and limb meshes stay rigid."""
    KEYS = ('cable', 'tentacle')
    def __init__(self):
        HairSim.__init__(self, 1.0)
        inv = self.inv; keep = [j for j in self.dyn if any(k in inv[j] for k in self.KEYS)]
        self.dyn = keep; ds = set(keep)
        self.L = {j: v for j, v in self.L.items() if j in ds}; self.child = {j: c for j, c in self.child.items() if j in ds and c in ds}
        self.root = {j: v for j, v in self.root.items() if j in ds}; self.anchor_bone = {j: v for j, v in self.anchor_bone.items() if j in ds}
        for j in keep: self.prm[j] = dict(k=0.07 if 'tentacle' in inv[j] else 0.10, damp=0.965, g=1.0, dev=0.16 * BODY_H)
        self.g = 1.5 * BODY_H
        self.par = {j: s_par[j] for j in keep}

# ------------------------------------------------------------------ scene
def new_scene(seed):
    S = Scene(seed=seed, analysis=DUMMY)
    # arm default targets = the bind pose hands, relative to the chest (so 'arm_w = 1' with no keys holds the rest pose)
    ps = Pose(); chest = ps.P[N('spine_05') if 'spine_05' in s_name else N('spine_04')]
    for sd in SIDES:
        sh = ps.pos('upperarm_' + sd); hd = ps.pos('hand_' + sd); s_ = sgn(sd)
        off = vsub(hd, sh); off = mv(m3T(chest), off)
        S.tr['arm_x_' + sd].default = s_ * off[0] / BODY_H; S.tr['arm_y_' + sd].default = off[1] / BODY_H; S.tr['arm_z_' + sd].default = off[2] / BODY_H
        S.tr['arm_w_' + sd].default = 1.0
        S.tr['arm_elb_' + sd].default = 0.5
        if ACTOR == 'robot':          # this skin's bind pose is a T-pose: rest = arms hanging at the sides (slightly out and forward), not the T
            L = vlen(off); S.tr['arm_x_' + sd].default = 0.16 * L / BODY_H; S.tr['arm_y_' + sd].default = -0.92 * L / BODY_H; S.tr['arm_z_' + sd].default = 0.10 * L / BODY_H
    return S

# ------------------------------------------------------------------ small helpers for scripts
def K(S, t, **kw):
    for k, v in kw.items(): S.K(k, t, v)

def kseq(S, name, pts):
    for t, v in pts: S.K(name, t, v)

def smooth_path(waypoints):
    """[(t, x, z)] -> function t -> (x, z, heading_deg) with ease-in/out between waypoints."""
    wp = sorted(waypoints)
    def f(t):
        if t <= wp[0][0]: a = b = wp[0]; u = 0
        elif t >= wp[-1][0]: a = b = wp[-1]; u = 0
        else:
            i = max(k for k in range(len(wp) - 1) if wp[k][0] <= t); a, b = wp[i], wp[i + 1]; u = (t - a[0]) / (b[0] - a[0]); u = u * u * (3 - 2 * u) * 0.5 + u * 0.5
        x = a[1] + (b[1] - a[1]) * u; z = a[2] + (b[2] - a[2]) * u
        return x, z
    return f

def set_root(S, t, ux, uz, heading=None, lift_m=None):
    hx, hz = to_home(ux, uz); S.K('home_x', t, hx); S.K('home_z', t, hz)
    if heading is not None:
        prev = S.tr['pel_yaw'].v[-1] if S.tr['pel_yaw'].v else 0.0
        S.K('pel_yaw', t, wrap_to(prev, yaw_of(heading)))
    if lift_m is not None: S.K('lift', t, frac(lift_m))

def stance_off(sd, widen=1.0):
    return (BP[N('foot_' + sd)][0] - PEL_B[0]) * widen, BP[N('foot_' + sd)][2] - PEL_B[2]

def foot_world(S, sd, t, px, pz, yaw_deg, lead_dir, lead_m, widen=1.0):
    """Where a foot should be when planted while the pelvis is at authoring-unit position (px, pz) facing yaw."""
    ox, oz = stance_off(sd, widen); a = math.radians(yaw_deg)
    wx = px + ox * math.cos(a) + oz * math.sin(a); wz = pz - ox * math.sin(a) + oz * math.cos(a)
    return wx + lead_dir[0] * lead_m / KM, wz + lead_dir[1] * lead_m / KM

def walk(S, rng, t0, t1, waypoints, step=(1.7, 2.4), lift=0.055, bob=0.018, swing=0.09, lean=6.0, widen=1.0, face_travel=True, arms=True, landings=None, sway=1.0, skip=(), close=True):
    """Footsteps along a waypoint path (t, ux, uz) in unity metres. Every step differs: its length, lift, timing, arm swing and sway are drawn at random.
    landings = explicit landing times (lets a stomp coincide with a beat of the soundtrack)."""
    path = smooth_path(waypoints)
    kt = t0; prev_h = None
    while kt <= t1 + 1e-6:
        x, z = path(kt); x2, z2 = path(kt + 0.3)
        h = heading_to(x, z, x2, z2) if (abs(x2 - x) + abs(z2 - z)) > 1e-3 else prev_h
        if h is None: h = heading_to(x, z, *path(t1))
        prev_h = h
        set_root(S, kt, x, z, prev_h if face_travel else None)
        kt += 0.4
    if landings is None:
        landings = []; t = t0; sd_n = 0
        while t < t1 - 0.5:
            t += rng.uniform(*step); landings.append(min(t, t1))
    cur = {s_: (S.get('ft_wx_' + s_, t0), S.get('ft_wz_' + s_, t0)) for s_ in SIDES}
    sd = 'l' if rng.random() < 0.5 else 'r'; prev_l = t0
    for n, tl in enumerate(landings):
        gap = max(0.6, tl - prev_l); sw = min(gap * rng.uniform(0.85, 1.0) + 0.2, 3.0); ts = max(t0, tl - sw)
        px, pz = S.get('home_x', tl) * BODY_H + PEL_B[0], S.get('home_z', tl) * BODY_H + PEL_B[2]
        yaw = S.get('pel_yaw', tl); x1, z1 = path(tl + 0.2); x0, z0 = path(tl - 0.2)
        dirv = (x1 - x0, -(z1 - z0)); dl = math.hypot(*dirv) or 1.0; dirv = (dirv[0] / dl, dirv[1] / dl); speed = dl / 0.4
        lead = speed * gap * 0.5 * rng.uniform(0.9, 1.1)
        nx, nz = foot_world(S, sd, tl, px, pz, yaw, dirv, lead, widen)
        ox, oz = cur[sd]; tm = (ts + tl) / 2; h = lift * BODY_H * rng.uniform(0.75, 1.25)
        for k, v0, vm, v1 in (('ft_wx_', ox, (ox + nx) / 2, nx), ('ft_wz_', oz, (oz + nz) / 2, nz)):
            S.K(k + sd, ts, v0); S.K(k + sd, tm, vm); S.K(k + sd, tl, v1)
        S.K('ft_lift_' + sd, ts, 0.0); S.K('ft_lift_' + sd, tm, h); S.K('ft_lift_' + sd, tl, 0.0)
        S.K('ft_pitch_' + sd, ts, 0.0); S.K('ft_pitch_' + sd, tm, -rng.uniform(8, 16)); S.K('ft_pitch_' + sd, tl, 4.0); S.K('ft_pitch_' + sd, tl + 0.25, 0.0)
        S.K('ft_yaw_' + sd, ts, S.get('ft_yaw_' + sd, ts)); S.K('ft_yaw_' + sd, tl, yaw)
        cur[sd] = (nx, nz); sgs = 1 if sd == 'l' else -1
        S.K('pel_dy', max(t0, tl - gap * 0.5), 0.004); S.K('pel_dy', tl, -bob * rng.uniform(0.7, 1.3)); S.K('pel_dy', tl + gap * 0.4, 0.004)
        quiet = any(a <= tl <= b for a, b in skip)
        if not quiet:
            S.K('pel_roll', tl, sgs * rng.uniform(1.0, 2.6) * sway); S.K('sp_twist', tl, -sgs * rng.uniform(3, 9) * sway)
            S.K('sp_bend', tl, lean + rng.uniform(-1.5, 2.0)); S.K('hd_pitch', tl, -lean * 0.5 + rng.uniform(-2, 2))
        if arms and not quiet:
            for ad in SIDES:
                sa = 1 if ad == sd else -1
                S.K('arm_w_' + ad, tl, 1.0); S.K('arm_z_' + ad, tl, S.tr['arm_z_' + ad].default + sa * swing * rng.uniform(0.8, 1.2))
                S.K('arm_x_' + ad, tl, S.tr['arm_x_' + ad].default + rng.uniform(-0.01, 0.015))
        sd = 'r' if sd == 'l' else 'l'; prev_l = tl; last_gap = gap
    if close and landings:                                   # the other foot closes the stance so the actor ends planted
        tl = landings[-1] + min(last_gap, 2.6); ts = landings[-1] + 0.3
        px, pz = S.get('home_x', tl) * BODY_H + PEL_B[0], S.get('home_z', tl) * BODY_H + PEL_B[2]; yaw = S.get('pel_yaw', tl)
        nx, nz = foot_world(S, sd, tl, px, pz, yaw, (0, 0), 0.0, widen); ox, oz = cur[sd]; tm = (ts + tl) / 2
        for k, v0, vm, v1 in (('ft_wx_', ox, (ox + nx) / 2, nx), ('ft_wz_', oz, (oz + nz) / 2, nz)): S.K(k + sd, ts, v0); S.K(k + sd, tm, vm); S.K(k + sd, tl, v1)
        S.K('ft_lift_' + sd, ts, 0.0); S.K('ft_lift_' + sd, tm, lift * BODY_H * 0.6); S.K('ft_lift_' + sd, tl, 0.0); S.K('ft_yaw_' + sd, tl, yaw)
        return tl
    return landings[-1] if landings else t0

def plant_all(S, t, widen=1.0, at=None):
    """Feet end side by side under the pelvis at time t (no sliding: the last swing has finished)."""
    px, pz = S.get('home_x', t) * BODY_H + PEL_B[0], S.get('home_z', t) * BODY_H + PEL_B[2]; yaw = S.get('pel_yaw', t)
    for sd in SIDES:
        nx, nz = foot_world(S, sd, t, px, pz, yaw, (0, 0), 0.0, widen)
        S.K('ft_wx_' + sd, t, nx); S.K('ft_wz_' + sd, t, nz); S.K('ft_yaw_' + sd, t, yaw); S.K('ft_lift_' + sd, t, 0.0)

def hold(S, t0, t1, *names):
    for nm in names:
        v = S.get(nm, t0); S.K(nm, t0, v); S.K(nm, t1, v)

def arm_to(S, t, sd, x, y, z, elb=0.5, w=1.0, curl=0.3):
    K(S, t, **{'arm_w_' + sd: w, 'arm_x_' + sd: x, 'arm_y_' + sd: y, 'arm_z_' + sd: z, 'arm_elb_' + sd: elb, 'curl_' + sd: curl})

def arm_rest(S, t, sd, w=1.0):
    K(S, t, **{'arm_w_' + sd: w, 'arm_x_' + sd: S.tr['arm_x_' + sd].default, 'arm_y_' + sd: S.tr['arm_y_' + sd].default, 'arm_z_' + sd: S.tr['arm_z_' + sd].default, 'arm_elb_' + sd: 0.5})

def lying_pose(S, t, pitch=88, roll=0, dy=-0.30):
    """Body flat on the ground, legs trailing (feet follow the pelvis)."""
    K(S, t, pel_pitch=pitch, pel_roll=roll, pel_dy=dy, ft_w_l=0.0, ft_w_r=0.0)

def standing_from_air(S, t, dur):
    for sd in SIDES:
        S.K('ft_w_' + sd, t, 0.0); S.K('ft_w_' + sd, t + dur, 1.0)

# ------------------------------------------------------------------ baking
def bake(S, t0, t1, out, post=None, sim=True, scale_fn=None, name='Show', hide_fn=None):
    """Evaluate the scene at FPSG, apply per-frame fix-ups, and write one clip."""
    baker = ClipBaker(); hs = GiantHairSim() if sim else None
    n = int((t1 - t0) * FPSG) + 1
    for k in range(n):
        t = t0 + k / FPSG
        ps = build_pose(S, t)
        j = S.get('jaw', t)
        if j > 0.01 and 'jaw_01' in s_name: ps.local('jaw_01', rx(j))
        if post: post(ps, S, t)
        P = fix_reverse(ps.P) if ACTOR == 'monster' else ps.P
        P, pel, tr = P, ps.frame_tuple()[1], ps.tr
        if hs: hs.apply(P, vadd(PEL_B, pel))
        baker.add(P, pel, tr, scale_fn(t) if scale_fn else None)
        if k % 400 == 0: print('frame', k, '/', n, flush=True)
    write_glb(out, [baker.finish(name, FPSG)])

# ------------------------------------------------------------------ aiming helpers (solve for the control values that point a part at a world target)
class Ov:
    """Scene view with some controls overridden (for probing poses without keying them)."""
    def __init__(self, S, ov): self.S = S; self.ov = ov
    def get(self, k, t): return self.ov[k] if k in self.ov else self.S.get(k, t)
    def n(self, *a, **k): return self.S.n(*a, **k)

def to_auth(ux, uy, uz): return [ux / KM, uy / KM, -uz / KM]
def to_unity(p): return (p[0] * KM, p[1] * KM, -p[2] * KM)

def solve_head(S, t, target_u, iters=4):
    """hd_yaw / hd_pitch (degrees, relative to the body at time t) that point the head's forward axis at target (unity metres)."""
    T = to_auth(*target_u); y = S.get('hd_yaw', t); p = S.get('hd_pitch', t)
    def fwd_dir(yy, pp):
        ps = build_pose(Ov(S, {'hd_yaw': yy, 'hd_pitch': pp}), t); hp = ps.pos('head')
        f = vnorm(mv(ps.P[N('head')], [0, 0, 1])); d = vnorm(vsub(T, hp)); return f, d
    for _ in range(iters):
        f, d = fwd_dir(y, p); f1, _ = fwd_dir(y + 2.0, p); f2, _ = fwd_dir(y, p + 2.0)
        ey = vsub(f1, f); ep = vsub(f2, f); err = vsub(d, f)
        a = [[vdot(ey, ey), vdot(ey, ep)], [vdot(ey, ep), vdot(ep, ep)]]; b = [vdot(ey, err), vdot(ep, err)]
        det = a[0][0] * a[1][1] - a[0][1] * a[1][0]
        if abs(det) < 1e-12: break
        dy = (b[0] * a[1][1] - b[1] * a[0][1]) / det; dp = (a[0][0] * b[1] - a[1][0] * b[0]) / det
        y += 2.0 * dy; p += 2.0 * dp
    return max(-75, min(75, y)), max(-60, min(60, p))

def aim_head(S, t, target_u, w=1.0):
    y, p = solve_head(S, t, target_u); S.K('hd_yaw', t, y * w); S.K('hd_pitch', t, p * w)

def reach_world(S, t, sd, target_u, elb=0.5, w=1.0, curl=0.3):
    """Key the arm IK controls so the hand goes to a world point (unity metres), given the pose at time t."""
    ps = build_pose(S, t); chest = ps.P[N('spine_05')]; sh = ps.pos('upperarm_' + sd); s_ = sgn(sd)
    off = mv(m3T(chest), vsub(to_auth(*target_u), sh))
    K(S, t, **{'arm_w_' + sd: w, 'arm_x_' + sd: s_ * off[0] / BODY_H, 'arm_y_' + sd: off[1] / BODY_H, 'arm_z_' + sd: off[2] / BODY_H, 'arm_elb_' + sd: elb, 'curl_' + sd: curl})

def bone_world(S, t, bone):
    """Unity-metre position of a bone of THIS actor at time t."""
    ps = build_pose(S, t); return to_unity(ps.pos(bone))

def rel_head_unity(ux, uz): return 0

# ------------------------------------------------------------------ trace of the other actor (written by the monster bake, read by the robot script)
TRACE_BONES = ('head', 'eye', 'jaw_01', 'hand_l', 'hand_r', 'spine_03', 'pelvis', 'upperarm_r', 'upperarm_l', 'weapon_l', 'weapon_r')
_trace = None
def load_trace(actor):
    global _trace
    pth = os.path.join(HERE_G, 'cache', 'showdown', actor + '_trace.json')
    _trace = json.load(open(pth)) if os.path.exists(pth) else None
def other_pos(bone, t, fallback):
    if not _trace or bone not in _trace['b']: return fallback
    i = t / _trace['dt']; i0 = max(0, min(len(_trace['b'][bone]) - 1, int(i))); i1 = min(len(_trace['b'][bone]) - 1, i0 + 1); f = i - int(i)
    a = _trace['b'][bone][i0]; b = _trace['b'][bone][i1]; return tuple(a[k] + (b[k] - a[k]) * f for k in range(3))
def write_trace(S, t0, t1, out, dt=0.1):
    tr = {'dt': dt, 'b': {b: [] for b in TRACE_BONES if b in s_name}}
    t = t0
    while t <= t1:
        ps = build_pose(S, t)
        for b in tr['b']: tr['b'][b].append([round(v, 2) for v in to_unity(ps.pos(b))])
        t += dt
    json.dump(tr, open(out, 'w'))
