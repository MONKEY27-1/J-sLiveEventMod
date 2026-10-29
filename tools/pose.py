"""Pose builder (authoring frame: faces +Z, +Y up, +X = the character's LEFT). Angles in degrees, positions in mesh units.

Everything is expressed as world rotations-from-bind (P). Limbs use two-bone IK so feet and hands go exactly where asked."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from body import *

SIDES = ('l', 'r')
CLAMPS = {'leg': 0, 'arm': 0}          # how many times an IK target was out of reach (feet would slide if legs ever clamp)
def sgn(side): return 1.0 if side == 'l' else -1.0

def rmin(a, c):
    """Minimal rotation taking unit vector a to unit vector c."""
    a = vnorm(a); c = vnorm(c); d = max(-1.0, min(1.0, vdot(a, c)))
    if d > 0.999999: return [r[:] for r in I3]
    if d < -0.999999:
        ax = vcross(a, [1, 0, 0] if abs(a[0]) < 0.9 else [0, 1, 0]); return r_axis(ax, 180)
    return r_axis(vcross(a, c), math.degrees(math.acos(d)))

def _bdir(a, c): return vnorm(vsub(BP[N(c)], BP[N(a)]))
LEN = {}
for _s in SIDES:
    LEN['th' + _s] = vlen(vsub(BP[N('calf_' + _s)], BP[N('thigh_' + _s)])); LEN['sh' + _s] = vlen(vsub(BP[N('foot_' + _s)], BP[N('calf_' + _s)]))
    LEN['ua' + _s] = vlen(vsub(BP[N('lowerarm_' + _s)], BP[N('upperarm_' + _s)])); LEN['fa' + _s] = vlen(vsub(BP[N('hand_' + _s)], BP[N('lowerarm_' + _s)]))
ANKLE_REST_Y = BP[N('foot_l')][1]          # ankle height when the foot is flat on the ground

class Pose:
    def __init__(self, base=None):
        self.P = copy_pose(base or BASE_P); self.pel = list(BASE_DPEL); self.tr = {}
    def pel_abs(self): return vadd(PEL_B, self.pel)
    def pos(self, name):
        j = N(name); path = []
        while j != PEL:
            path.append(j); j = s_par[j]
        p = self.pel_abs(); par = PEL
        for c in reversed(path):
            p = vadd(p, mv(self.P[par], vsub(BP[c], BP[par]))); par = c
        return p
    def set_world(self, bone, R_target):
        j = N(bone); D = m3mul(R_target, m3T(self.P[j]))
        for s in subtree[j]: self.P[s] = m3mul(D, self.P[s])
    def local(self, bone, R_local): apply_local(self.P, bone, R_local)
    # ---- torso ----
    def pelvis(self, yaw=0, pitch=0, roll=0, dx=0, dy=0, dz=0):
        D = m3mul(m3mul(ry(yaw), rx(pitch)), rz(roll)); apply_world(self.P, 'pelvis', D)
        self.pel = [self.pel[0] + dx * BODY_H, self.pel[1] + dy * BODY_H, self.pel[2] + dz * BODY_H]
    SPINE_W = (0.12, 0.18, 0.24, 0.26, 0.20)
    def spine(self, bend=0, twist=0, lean=0):
        bones = []; seen = set()
        for i, w in enumerate(self.SPINE_W):
            nm = 'spine_0%d' % (i + 1)
            if nm in s_name and s_name[nm] not in seen: seen.add(s_name[nm]); bones.append((nm, w))      # other rigs have fewer spine bones (aliases are skipped)
        tot = sum(w for _, w in bones)
        for nm, w in bones:
            w /= tot; self.local(nm, m3mul(m3mul(rx(bend * w), ry(twist * w)), rz(lean * w)))
    def head(self, pitch=0, yaw=0, roll=0):
        necks = [n for n in ('neck_01', 'neck_02', 'neck_03') if n in s_name]
        for nm, w in [(n, 0.5 / len(necks)) for n in necks] + [('head', 0.5)]:
            self.local(nm, m3mul(m3mul(rx(pitch * w), ry(yaw * w)), rz(roll * w)))
    def shoulder(self, side, up=0, fwd=0):
        s = sgn(side); self.local('clavicle_' + side, m3mul(rz(s * up), ry(-s * fwd)))
    # ---- legs (2-bone IK) ----
    def leg(self, side, ankle, foot_yaw=0, foot_pitch=0, knee_out=8.0, ball_pitch=None, follow=0.0, plant=0.0, toe=0.0):
        H = self.pos('thigh_' + side); A = list(ankle); L1 = LEN['th' + side]; L2 = LEN['sh' + side]
        v = vsub(A, H); d = vlen(v); reach = (L1 + L2) * 0.9995
        if d > reach:
            CLAMPS['leg'] += 1; CLAMPS.setdefault('over', []).append(d - reach); A = vadd(H, vmul(vnorm(v), reach)); v = vsub(A, H); d = reach
        u = vnorm(v)
        fwd = mv(self.P[PEL], [0, 0, 1]); fwd = vnorm(vadd(fwd, vmul(mv(self.P[PEL], [sgn(side), 0, 0]), math.tan(math.radians(knee_out)))))
        a_ = (L1 * L1 - L2 * L2 + d * d) / (2 * d); h_ = math.sqrt(max(L1 * L1 - a_ * a_, 0.0))
        nrm = vnorm(vsub(fwd, vmul(u, vdot(fwd, u))))
        K = vadd(H, vadd(vmul(u, a_), vmul(nrm, h_)))
        d1 = vnorm(vsub(K, H)); d2 = vnorm(vsub(A, K))
        d1b = _bdir('thigh_' + side, 'calf_' + side); d2b = _bdir('calf_' + side, 'foot_' + side); hint_b = [0, 0, 1]
        self.set_world('thigh_' + side, m3mul(frame(d1, nrm), m3T(frame(d1b, hint_b))))
        self.set_world('calf_' + side, m3mul(frame(d2, nrm), m3T(frame(d2b, hint_b))))
        Rf = m3mul(ry(foot_yaw), rx(foot_pitch))
        if follow > 0:                                    # airborne / seated: the foot hangs from the shin with a relaxed ankle (toes down) instead of staying flat to the world
            Rf = slerp_m(Rf, m3mul(self.P[N('calf_' + side)], rx(plant)), follow)
        self.set_world('foot_' + side, Rf)
        if 'ball_' + side in s_name:
            Rb = m3mul(ry(foot_yaw), rx(foot_pitch if ball_pitch is None else ball_pitch))
            if follow > 0: Rb = slerp_m(Rb, m3mul(Rf, rx(toe)), follow)
            self.set_world('ball_' + side, Rb)
    def ankle_ground(self, x, z, lift=0.0): return [x, ANKLE_REST_Y + lift, z]
    # ---- arms (2-bone IK) ----
    def arm(self, side, target, elbow_hint=None):
        S = self.pos('upperarm_' + side); L1 = LEN['ua' + side]; L2 = LEN['fa' + side]; T = list(target)
        v = vsub(T, S); d = vlen(v); reach = (L1 + L2) * 0.998
        if d > reach: T = vadd(S, vmul(vnorm(v), reach)); v = vsub(T, S); d = reach
        if d < 0.35 * (L1 + L2): T = vadd(S, vmul(vnorm(v), 0.35 * (L1 + L2))); v = vsub(T, S); d = vlen(v)
        u = vnorm(v)
        hint = vnorm(elbow_hint or [sgn(side) * 0.5, -1.0, -0.8])      # elbows point down, back and a little out
        nrm = vnorm(vsub(hint, vmul(u, vdot(hint, u))))
        a_ = (L1 * L1 - L2 * L2 + d * d) / (2 * d); h_ = math.sqrt(max(L1 * L1 - a_ * a_, 0.0))
        E = vadd(S, vadd(vmul(u, a_), vmul(nrm, h_)))
        d1 = vnorm(vsub(E, S)); d2 = vnorm(vsub(T, E))
        d1b = _bdir('upperarm_' + side, 'lowerarm_' + side); d2b = _bdir('lowerarm_' + side, 'hand_' + side)
        self.set_world('upperarm_' + side, m3mul(frame(d1, nrm), m3T(frame(d1b, [0, 0, -1]))))
        self.set_world('lowerarm_' + side, m3mul(frame(d2, nrm), m3T(frame(d2b, [0, 0, -1]))))
    # ---- hands ----
    FINGERS = ('index', 'middle', 'ring', 'pinky')
    def hand(self, side, curl=0.3, thumb=0.3, point=False):
        d = _bdir('lowerarm_' + side, 'hand_' + side); axis = vnorm(vcross(d, [0, -1, 0]))
        for f in self.FINGERS:
            c = 0.0 if (point and f == 'index') else curl
            for i, ang in ((1, 62), (2, 80), (3, 50)):
                nm = '%s_0%d_%s' % (f, i, side)
                if nm in s_name: self.local(nm, r_axis(axis, ang * c))
        for i, ang in ((1, 25), (2, 40), (3, 30)):
            nm = 'thumb_0%d_%s' % (i, side)
            if nm in s_name: self.local(nm, r_axis(axis, ang * thumb))
    # ---- face ----
    def face(self, jaw=0.0, smile=0.0, blink=0.0, brow=0.0, eye_yaw=0.0, eye_pitch=0.0):
        """jaw in degrees (0-24); blink 0..1 (1 = shut); smile 0..2.5 (lip corners up); brow -1..2.2 (raise); eyes in degrees. Values calibrated against Blender renders."""
        if jaw: self.local('C_jaw', rx(jaw))
        t = self.tr
        for sd in SIDES:
            S_ = sd.upper()
            t['%s_eye_lid_upper_mid' % S_] = [0, 1.7 * blink, 0]
            t['%s_brow_outer' % S_] = [0, 0.8 * brow, 0]
            t['%s_lip_corner' % S_] = [0, -1.0 * smile, 0]
            if eye_yaw or eye_pitch:
                nm = '%s_eye' % S_
                if nm in s_name: self.local(nm, m3mul(ry(eye_yaw), rx(eye_pitch)))
        t['C_brow_mid'] = [0, 0.8 * brow, 0]
    def frame_tuple(self): return (self.P, list(self.pel), dict(self.tr))      # pel is already an offset from the bind pelvis

def stand_legs(ps, width=1.0, yaw_l=0.0, yaw_r=0.0, dz_l=0.0, dz_r=0.0):
    """Both feet flat on the ground under the hips (width scales the stance)."""
    ps.leg('l', ps.ankle_ground(BP[N('foot_l')][0] * width, BP[N('foot_l')][2] + dz_l), yaw_l)
    ps.leg('r', ps.ankle_ground(BP[N('foot_r')][0] * width, BP[N('foot_r')][2] + dz_r), yaw_r)
