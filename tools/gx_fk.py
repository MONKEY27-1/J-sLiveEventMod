"""gx_fk.py - forward / two-bone-IK solver for the new Galactus skeleton (used inside Blender by gx_anim.py and gx_preview.py).
A pose is a dict:  root = np3 (hips translation), rot = {bone: (rx, ry, rz) deg},  aim = {bone: (dir, frame_bone|None)},  twist = {bone: deg},  ik = {upper_bone: dict(target=np3, pole=np3)}.
'rot' is an ALIGNED delta (identity = rest pose, axes of the model: +x = his left, -y = front, +z = up; +rx = bend forward, +rz = turn towards his left, +ry = lean towards his left... (right-handed)), composed down the chain.
'aim' points a bone's own axis along a direction (in model space, or in the frame of an already solved bone).  'ik' puts the end of a 2-bone limb on a target (model space) with the middle joint pushed to the pole side."""
import numpy as np, math
def rx(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
def ry(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
def rz(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])
def eul(v): return rz(v[2]) @ ry(v[1]) @ rx(v[0])
def unit(v): v = np.asarray(v, float); n = np.linalg.norm(v); return v / n if n > 1e-9 else v
def axis_rot(ax, deg):
    ax = unit(ax); a = math.radians(deg); K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]]); return np.eye(3) + math.sin(a) * K + (1 - math.cos(a)) * (K @ K)
def from_two(a, b):
    a = unit(a); b = unit(b); v = np.cross(a, b); c = float(np.dot(a, b))
    if c < -0.999999:
        p = np.cross(a, [1, 0, 0]);
        if np.linalg.norm(p) < 1e-3: p = np.cross(a, [0, 1, 0])
        return axis_rot(p, 180.0)
    K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]]); return np.eye(3) + K + (K @ K) / (1 + c)
class Rig:
    def __init__(self, arm):
        self.names = [b.name for b in arm.data.bones]; self.parent = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
        self.rest = {b.name: np.array(b.matrix_local) for b in arm.data.bones}; self.head = {n: self.rest[n][:3, 3].copy() for n in self.names}
        self.y = {n: unit(self.rest[n][:3, 1]) for n in self.names}; self.len = {b.name: b.length for b in arm.data.bones}
        self.child = {}
        for n in self.names:
            p = self.parent[n]
            if p and p not in self.child: self.child[p] = n
        self.order = self.names
    def solve(self, spec):
        D, P, M = {}, {}, {}; root = np.asarray(spec.get('root', (0, 0, 0)), float); rot = spec.get('rot', {}); aim = spec.get('aim', {}); tw = spec.get('twist', {}); ik = spec.get('ik', {}); over = {}
        for nm in self.order:
            p = self.parent[nm]
            if p is None: Dp = np.eye(3); pos = self.head[nm] + root
            else: Dp = D[p]; pos = P[p] + Dp @ (self.head[nm] - self.head[p])
            d = None
            if nm in over: d = over[nm]
            elif nm in ik:
                c = self.child[nm]; la, lb = self.len[nm], self.len[c]; tg = np.asarray(ik[nm]['target'], float); v = tg - pos; dist = np.linalg.norm(v); dirv = unit(v); dist = min(dist, la + lb - 1e-3); dist = max(dist, abs(la - lb) + 1e-3)
                cosA = (la * la + dist * dist - lb * lb) / (2 * la * dist); sinA = math.sqrt(max(0.0, 1 - cosA * cosA)); pole = np.asarray(ik[nm]['pole'], float); perp = pole - dirv * np.dot(pole, dirv)
                perp = unit(perp) if np.linalg.norm(perp) > 1e-6 else unit(np.cross(dirv, [0, 0, 1]))
                el = pos + dirv * la * cosA + perp * la * sinA; d = unit(el - pos); over[c] = unit(tg - el) if np.linalg.norm(tg - el) > 1e-6 else d
            elif nm in aim:
                dv, fr = aim[nm]; d = unit(D[fr] @ np.asarray(dv, float)) if fr else unit(dv)
            if d is not None:
                Di = from_two(Dp @ self.y[nm], d) @ Dp
                if nm in tw: Di = axis_rot(d, tw[nm]) @ Di
            else: Di = Dp @ eul(rot.get(nm, (0, 0, 0)))
            D[nm] = Di; P[nm] = pos; Mi = np.eye(4); Mi[:3, :3] = Di @ self.rest[nm][:3, :3]; Mi[:3, 3] = pos; M[nm] = Mi
        out = {}
        for nm in self.order:
            p = self.parent[nm]; Bm = np.linalg.inv(self.rest[nm]) @ (np.eye(4) if p is None else self.rest[p] @ np.linalg.inv(M[p])) @ M[nm]; out[nm] = Bm
        self.last = (D, P); return out
def apply(arm, basis, frame=None):
    """write the matrices to the pose bones (and key them when frame is given)"""
    import mathutils
    for nm, Bm in basis.items():
        pb = arm.pose.bones[nm]; m = mathutils.Matrix(Bm.tolist()); pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = m.to_quaternion(); pb.location = m.to_translation()
        if frame is not None:
            pb.keyframe_insert('rotation_quaternion', frame=frame)
            if nm == 'Hips' or pb.parent is None: pb.keyframe_insert('location', frame=frame)
