"""gx_clips.py - the authored Galactus animations.  Each clip is a function of time (seconds) returning a gx_fk pose spec; gx_anim.py samples them at 24 fps, keys them into Blender actions, renders contact sheets and exports the glb.
Units: the model is 60 tall, origin at the belt, +x = his left, -y = front, +z = up.  Angles in degrees."""
import numpy as np, math
from gx_fk import rx, ry, rz
PI = math.pi
def sm(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
def env(a, b, c, d, t): return sm(a, b, t) * (1 - sm(c, d, t))
def lerp(a, b, k): return a + (b - a) * k
def ease(u): u = min(1.0, max(0.0, u)); return u * u * (3 - 2 * u)
def keys(t, pts):
    """smooth (cosine-eased) interpolation of [(time, value), ...] - values are floats or tuples"""
    if t <= pts[0][0]: return pts[0][1]
    for i in range(len(pts) - 1):
        t0, v0 = pts[i]; t1, v1 = pts[i + 1]
        if t <= t1:
            k = ease((t - t0) / (t1 - t0)); a, b = np.asarray(v0, float), np.asarray(v1, float); r = a + (b - a) * k; return r if r.ndim else float(r)
    return pts[-1][1]
class C:   # constants of the rig
    pass
def setup(rig):
    C.rig = rig; h = rig.head; C.hipx = abs(h['ThighL'][0] - h['ThighR'][0]) / 2; C.hipy = h['Hips'][1]; C.ank = {'L': h['FootL'].copy(), 'R': h['FootR'].copy()}; C.zA = float((h['FootL'][2] + h['FootR'][2]) / 2)
    C.footdir = {'L': rig.y['FootL'].copy(), 'R': rig.y['FootR'].copy()}; C.upl = rig.len['UpperArmL']; C.lowl = rig.len['ForeArmL']
def adir(side, out, fwd):
    """upper-arm direction from 'out' (degrees away from the body, 0 = hanging) and 'fwd' (degrees swung to the front, 0 = in the body plane), in the chest frame"""
    s = 1.0 if side == 'L' else -1.0; o, f = math.radians(out), math.radians(fwd)
    v = np.array([s * math.sin(o) * math.cos(f), -math.sin(f) * math.cos(o) * 1.0, -math.cos(o) * math.cos(f)]); return v / np.linalg.norm(v)
def pose(hips=(0, 0, 0), hrot=(0, 0, 0), spine=(0, 0, 0), head=(0, 0, 0), shrug=(0, 0), armL=(14, 4, 25), armR=(14, 4, 25), wristL=(0, 0, 0), wristR=(0, 0, 0), footL=None, footR=None, fpL=0.0, fpR=0.0, fyawL=0.0, fyawR=0.0, spread=(0, 0), armframe='Chest', handsfree=True):
    """one full pose.  hips = translation, hrot = (pitch, roll, yaw), spine/head = (pitch, roll, yaw) totals split over the chain, shrug = clavicle lift L,R,
    armX = (out, fwd, elbow flexion) deg, foot targets absolute (None = neutral stance), fp = toe pitch, fyaw = foot yaw"""
    sp = {'rot': {}, 'aim': {}, 'ik': {}, 'twist': {}}; sp['root'] = np.array(hips, float); R = sp['rot']
    R['Hips'] = hrot; R['Spine'] = tuple(0.4 * x for x in spine); R['Chest'] = tuple(0.6 * x for x in spine); R['Neck'] = tuple(0.4 * x for x in head); R['Head'] = tuple(0.6 * x for x in head)
    R['ClavL'] = (0, -shrug[0], 0); R['ClavR'] = (0, shrug[1], 0)
    for side, a, w, spr in (('L', armL, wristL, spread[0]), ('R', armR, wristR, spread[1])):
        up = adir(side, a[0], a[1]); sgn = 1.0 if side == 'L' else -1.0; upf = a[3] if len(a) > 3 else 0.0; fore = (rx(-a[2] * (1 - upf)) @ ry(-sgn * a[2] * upf)) @ up; fore = rz(sgn * spr) @ fore
        sp['aim']['UpperArm' + side] = (up, armframe); sp['aim']['ForeArm' + side] = (fore, armframe); R['Wrist' + side] = w
    for side in 'LR':
        s = 1.0 if side == 'L' else -1.0; tg = footL if side == 'L' else footR
        if tg is None: tg = np.array([s * C.hipx * 1.5, C.hipy, C.zA])
        sp['ik']['Thigh' + side] = {'target': np.asarray(tg, float), 'pole': (s * 0.12, -1.0, 0.15)}
        fp = fpL if side == 'L' else fpR; fy = fyawL if side == 'L' else fyawR; sp['aim']['Foot' + side] = (rz(fy) @ rx(fp) @ C.footdir[side], None)
    return sp
def ank(side, dx=0.0, dy=0.0, dz=0.0, wide=1.5):
    s = 1.0 if side == 'L' else -1.0; return np.array([s * C.hipx * wide + dx, C.hipy + dy, C.zA + dz])
# ------------------------------------------------------------------ clips
def idle(t, dur=6.0):
    w = 2 * PI * t / dur; br = math.sin(2 * w)
    return pose(hips=(0.5 * math.sin(w), 0.3 * math.sin(w + 1), -1.0 + 0.12 * br), hrot=(0.5, 1.4 * math.sin(w), 1.6 * math.sin(w + 1)), spine=(1.0 + 0.9 * br, -0.8 * math.sin(w), 1.8 * math.sin(w + 2)), head=(2.0 + 1.4 * math.sin(2 * w + 1), 0.8 * math.sin(w), 6 * math.sin(w + 0.5)),
        shrug=(0.5 + 0.5 * br, 0.5 + 0.5 * br), armL=(15 + 2 * br, 4 + 3 * math.sin(w + 1), 28 + 4 * math.sin(w)), armR=(15 + 2 * br, 4 + 3 * math.sin(w + 2.4), 28 + 4 * math.sin(w + 2)), wristL=(6, 0, 0), wristR=(6, 0, 0),
        footL=ank('L', wide=1.55), footR=ank('R', wide=1.55))
def walk(t, dur=6.0, S=14.0, lift=4.2):
    ph = (t / dur) % 1.0; sp = {}
    def foot(p):
        if p < 0.6: y = -S / 2 + S * (p / 0.6); z = 0.0
        else: u = (p - 0.6) / 0.4; y = S / 2 - S * ease(u); z = lift * math.sin(PI * u) ** 1.2
        return y, z
    yl, zl = foot(ph); yr, zr = foot((ph + 0.5) % 1.0); c = math.cos(2 * PI * ph)
    bob = 0.55 * math.cos(4 * PI * ph + 0.4)
    return pose(hips=(0.9 * math.sin(2 * PI * ph + PI), 0, -1.3 + bob), hrot=(1.0, 2.0 * math.sin(2 * PI * ph), -4.5 * c), spine=(3.0, 1.2 * math.sin(2 * PI * ph), 5.5 * c), head=(1.0, -1.0 * math.sin(2 * PI * ph), -4.0 * c),
        shrug=(0.4, 0.4), armL=(13, -22 * c + 2, 24 + 18 * max(0, c)), armR=(13, 22 * c + 2, 24 + 18 * max(0, -c)), wristL=(6, 0, 0), wristR=(6, 0, 0),
        footL=ank('L', dy=yl, dz=zl, wide=1.35), footR=ank('R', dy=yr, dz=zr, wide=1.35), fpL=(-14 * sm(0.5, 0.6, ph) * (1 - sm(0.6, 0.72, ph)) + 22 * sm(0.6, 0.85, ph) * (1 - sm(0.9, 1.0, ph))) if False else (0.0 + (-25 if 0.5 < ph < 0.62 else 0.0)),
        fpR=0.0)
def roar(t, dur=7.0):
    e = env(0.8, 1.8, 5.0, 7.0, t); r = env(1.8, 2.5, 4.4, 5.2, t); sh = math.sin(t * 37) * r + 0.6 * math.sin(t * 23 + 1) * r
    return pose(hips=(0, 0.6 * r, -1.0 - 1.2 * e), hrot=(-1.0 * e, 0, 0), spine=(1.0 - 12 * e - 4 * r + 0.5 * sh, 0, 2 * sh), head=(-6 * e - 24 * r, 0, 2 * math.sin(t * 5) * r), shrug=(2.5 * e, 2.5 * e),
        armL=(15 + 62 * e + 10 * r, 4 + 18 * e, 28 + 24 * e - 10 * r), armR=(15 + 62 * e + 10 * r, 4 + 18 * e, 28 + 24 * e - 10 * r), wristL=(6 - 18 * e, 0, 0), wristR=(6 - 18 * e, 0, 0),
        footL=ank('L', wide=1.55 + 0.45 * e), footR=ank('R', wide=1.55 + 0.45 * e))
def backhand(t, dur=12.0, T=6.0):
    cw = sm(0.8, 5.0, t); st = sm(5.0, 6.35, t); fl = sm(6.35, 8.0, t); rc = sm(8.5, 11.8, t)
    yaw = (-34 * cw + 66 * st - 0 * fl + 10 * fl) * (1 - rc) + 0 * rc; hy = (-18 * cw + 33 * st) * (1 - rc) * 0.6
    cro = 2.2 * math.sin(t * 1.4) * (1 - cw)
    pitchS = (6 * cw + 14 * st) * (1 - rc); lunge = (-1.0 * cw - 3.0 * st) * (1 - rc)
    rarm = (lerp(lerp(15, 78, cw), 92, st) * (1 - rc) + 15 * rc, lerp(lerp(4, -26, cw), 74, st) * (1 - rc) + 4 * rc + 14 * fl * (1 - rc), lerp(lerp(28, 85, cw), 6, st) * (1 - rc) + 28 * rc)
    larm = (lerp(lerp(15, 20, cw), 18, st) * (1 - rc) + 15 * rc, lerp(lerp(4, 38, cw), -30, st) * (1 - rc) + 4 * rc, lerp(lerp(28, 95, cw), 110, st) * (1 - rc) + 28 * rc)
    return pose(hips=(0.8 * cw - 1.2 * st, lunge, -1.0 - 3.2 * cw * (1 - st) - 1.2 * st), hrot=(2 * st, -3 * cw + 5 * st, -20 * cw + 36 * st - 4 * fl) , spine=(pitchS, 0, yaw * 0.7), head=(3 - 4 * st, 0, -hy * 0 + (14 * cw - 30 * st) * (1 - rc)), shrug=(0.5 + 3 * cw, 0.5 + 4 * st), armL=larm, armR=rarm, wristL=(8, 0, 0), wristR=(-10 * st, 0, 0),
        footL=ank('L', dy=-2.0 * st - 1.0 * cw, wide=1.7 + 0.4 * cw), footR=ank('R', dy=2.5 * cw + 1.5 * st, dz=0.0 + 0.0, wide=1.7 + 0.5 * cw), fyawL=-8 * st, fyawR=10 * st)
def stoop(t, dur=8.0):
    w = 2 * PI * t / dur; br = math.sin(2 * w)
    return pose(hips=(0.3 * math.sin(w), 1.0, -3.4 + 0.1 * br), hrot=(6, 0.5 * math.sin(w), 2 * math.sin(w + 1)), spine=(26 + 1.2 * br, 0, 3 * math.sin(w)), head=(-34 + 3 * math.sin(2 * w + 1), 0, 10 * math.sin(w)), shrug=(2, 2),
        armL=(18, 36 + 5 * math.sin(w), 34), armR=(14, 55, 12), wristL=(10, 0, 0), wristR=(-6, 0, 0), footL=ank('L', dy=1.5, wide=2.0), footR=ank('R', dy=-2.0, wide=2.0), fyawL=6, fyawR=-6)
def brace(t, dur=6.0):
    w = 2 * PI * t / dur; br = math.sin(2 * w)
    return pose(hips=(0, 1.2, -4.3 + 0.1 * br), hrot=(6, 0, 4), spine=(14 + 0.8 * br, 2 * math.sin(w), -10 + 1.5 * math.sin(w)), head=(-8, 0, 8 + 3 * math.sin(w)), shrug=(1.5, 3.5),
        armL=(58, -14 + 3 * math.sin(w), 42), armR=(80, 60, 8), wristL=(0, 0, 0), wristR=(0, 0, 0), footL=ank('L', dy=2.0, wide=2.3), footR=ank('R', dy=-2.5, wide=2.4), fyawL=10, fyawR=-6)
def drift(t, dur=10.0):
    w = 2 * PI * t / dur
    return pose(hips=(0, 0, 3.0 + 1.0 * math.sin(w)), hrot=(12 * math.sin(w) + 6, 6 * math.sin(w + 1), 8 * math.sin(w + 2)), spine=(10 + 5 * math.sin(w + 0.5), 4 * math.sin(w + 2), 9 * math.sin(w)), head=(-8 + 3 * math.sin(2 * w), 3 * math.sin(w), 12 * math.sin(w + 1.3)), shrug=(3, 3),
        armL=(44 + 10 * math.sin(w), 34 + 12 * math.sin(w + 1), 38 + 10 * math.sin(w + 0.5)), armR=(44 + 10 * math.sin(w + 2), 34 + 12 * math.sin(w + 3), 38 + 10 * math.sin(w + 2.5)), wristL=(14, 0, 0), wristR=(14, 0, 0),
        footL=ank('L', dy=5.0 + 2 * math.sin(w), dz=7.0 + 2.5 * math.sin(2 * w), wide=1.2), footR=ank('R', dy=3.0 + 2 * math.sin(w + 2), dz=8.5 + 2.5 * math.sin(2 * w + 1), wide=1.2))
CLIPS = {'Idle': (idle, 6.0), 'Walk': (walk, 6.0), 'Roar': (roar, 7.0), 'Backhand': (backhand, 12.0), 'Stoop': (stoop, 8.0), 'Brace': (brace, 6.0), 'Drift': (drift, 10.0)}

def prelude(t, dur=60.4):
    """tau 111 - 171.4 (new 1:05 - 2:06): the gestures of the recording: his LEFT hand reaches out (1:10), both arms rise and open (1:14 - 1:30), forearms up (1:36), FISTS RAISED and a roar (1:40 - 1:44), arms wide (1:50), hands sweep forward over the carrier (2:00)"""
    K = lambda pts: keys(t, pts); w = 2 * PI * t / 6.0
    aL = K([(0, (15, 4, 28, 0)), (4, (15, 4, 28, 0)), (7, (46, 56, 30, 0)), (11, (54, 50, 24, 0)), (14, (26, 16, 32, 0)), (18, (56, 20, 40, 0)), (24, (86, 10, 46, 0)), (30, (102, 0, 40, 0)), (34, (82, 5, 115, 1)), (38, (94, 12, 136, 1)), (42, (96, 8, 128, 1)), (46, (90, 10, 36, 0)), (52, (42, 70, 40, 0)), (56, (30, 76, 46, 0)), (60.4, (18, 22, 40, 0))])
    aR = K([(0, (15, 4, 28, 0)), (7, (15, 4, 28, 0)), (15, (18, 6, 30, 0)), (18, (56, 20, 40, 0)), (24, (86, 10, 46, 0)), (30, (102, 0, 40, 0)), (34, (82, 5, 115, 1)), (38, (94, 12, 136, 1)), (42, (96, 8, 128, 1)), (46, (90, 10, 36, 0)), (52, (42, 70, 40, 0)), (56, (38, 44, 70, 0)), (60.4, (18, 10, 30, 0))])
    sp = K([(0, (1, 0, 0)), (18, (-3, 0, 0)), (30, (-4, 0, 3)), (38, (-7, 0, 0)), (46, (-4, 0, 0)), (52, (10, 0, 0)), (56, (12, 0, 0)), (60.4, (6, 0, 0))]); hd = K([(0, (2, 0, 0)), (7, (0, 0, -12)), (14, (0, 0, 0)), (24, (-6, 0, 4)), (38, (-14, 0, 0)), (46, (-4, 0, 0)), (52, (14, 0, 0)), (60.4, (3, 0, 0))])
    sh = K([(0, (0.5, 0.5)), (30, (3, 3)), (38, (4, 4)), (46, (2, 2)), (60.4, (0.5, 0.5))]); wide = K([(0, 1.55), (34, 1.55), (38, 2.0), (46, 2.0), (52, 1.7), (60.4, 1.6)]); dz = K([(0, -1.0), (34, -1.0), (38, -2.6), (46, -2.2), (52, -1.4), (60.4, -1.0)])
    return pose(hips=(0.4 * math.sin(w), 0.2 * math.sin(w + 1), dz + 0.1 * math.sin(2 * w)), hrot=(1.0, 1.2 * math.sin(w), 1.5 * math.sin(w + 1)), spine=tuple(sp[i] + (0.8 * math.sin(2 * w) if i == 0 else 0) for i in range(3)), head=tuple(hd[i] + (3 * math.sin(w + 0.5) if i == 2 else 0) for i in range(3)), shrug=tuple(sh),
        armL=tuple(aL), armR=tuple(aR), wristL=(6, 0, 0), wristR=(6, 0, 0), footL=ank('L', wide=wide), footR=ank('R', wide=wide))
CLIPS['Prelude'] = (prelude, 60.4)
