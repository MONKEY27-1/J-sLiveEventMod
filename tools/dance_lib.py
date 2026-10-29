"""Pose helpers for the concert dancers (Marshmello DJ + Cuddle Team Leader bears): find the bones of a Sketchfab rig, put limbs along target directions (aim a bone so that the vector to its child joint
points where we want, keeping the rest of the chain), bend the spine, turn the head, bob the hips.  Works on the evaluated pose with pb.matrix, so it does not depend on bone roll / axis conventions.
All directions are given in the CHARACTER frame: x = the character's left (+) / right (-)... see Rig.dir(): (side, forward, up) with side > 0 = the character's own left, forward > 0 = the way it faces, up > 0 = up."""
import bpy, math
from mathutils import Vector, Quaternion, Matrix

class Rig:
    def __init__(self, arm, names, up_axis, fwd_axis, left_axis, scale):
        self.arm = arm; self.n = names; self.up = Vector(up_axis).normalized(); self.fwd = Vector(fwd_axis).normalized(); self.left = Vector(left_axis).normalized(); self.scale = scale
        self.pb = {k: self.find(v) for k, v in names.items() if v}
        self.rest = {k: self.arm.matrix_world @ self.pb[k].matrix for k in self.pb}
        self.rest_pos = {k: (self.arm.matrix_world @ self.pb[k].matrix).translation.copy() for k in self.pb}
        self.rest_head = {k: self.arm.pose.bones[self.pb[k].name].matrix.copy() for k in self.pb}
    def find(self, prefix):
        for pb in self.arm.pose.bones:
            if pb.name == prefix or pb.name.startswith(prefix + '_') or pb.name.startswith(prefix):
                if 'twist' in pb.name or 'end' in pb.name: continue
                return pb
        raise KeyError(prefix)
    def dir(self, side, fwd, up):
        """a world direction from (side: character-left +, forward, up)"""
        return (self.left * side + self.fwd * fwd + self.up * up).normalized()
    def pos(self, k): return (self.arm.matrix_world @ self.pb[k].matrix).translation
    def update(self): bpy.context.view_layer.update()
    def reset(self):
        for pb in self.arm.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)           # scale too: pb.matrix = ... leaks tiny scales that pile up frame after frame until the bone stops answering
        self.update()
    def rotate_about_head(self, k, q_world):
        """rotate bone k about its own head by the world-space rotation q (the children follow)"""
        pb = self.pb[k]; M = self.arm.matrix_world @ pb.matrix; head = M.translation.copy(); R = q_world.to_matrix() @ M.to_3x3()
        new = Matrix.Translation(head) @ R.to_4x4(); pb.matrix = self.arm.matrix_world.inverted() @ new; self.update()
    def aim(self, k, child, target):
        """bone k: turn it about its head so that the vector head(k) -> head(child) points along `target` (world)"""
        cur = self.pos(child) - self.pos(k)
        if cur.length < 1e-6: return
        self.rotate_about_head(k, cur.normalized().rotation_difference(Vector(target).normalized()))
    def move(self, k, delta_world):
        pb = self.pb[k]; M = self.arm.matrix_world @ pb.matrix; M.translation += delta_world; pb.matrix = self.arm.matrix_world.inverted() @ M; self.update()
    def twist(self, k, axis_world, angle):
        self.rotate_about_head(k, Quaternion(axis_world, angle))

def pose(rig, p):
    """p: dict of the pose parameters (see dance_gen.py) -> sets the whole body.  Order: hips, spine, head, legs, arms."""
    rig.reset(); S = rig.scale
    hips = p.get('hip', (0, 0, 0))                               # (side, forward, up) offset in metres
    rig.move('pelvis', rig.dir(1, 0, 0) * hips[0] * S + rig.dir(0, 1, 0) * hips[1] * S + rig.dir(0, 0, 1) * hips[2] * S)
    lean = p.get('lean', 0.0); twist = p.get('twist', 0.0); roll = p.get('roll', 0.0)               # radians: forward lean, twist about up, roll to the character's left
    chain = rig.n.get('spine_chain', [])
    for k in chain:
        rig.rotate_about_head(k, Quaternion(rig.left, lean / len(chain)) @ Quaternion(rig.fwd, roll / len(chain)) @ Quaternion(rig.up, twist / len(chain)))
    if 'neck' in rig.pb: rig.rotate_about_head('neck', Quaternion(rig.left, p.get('nod', 0.0) * 0.4) @ Quaternion(rig.up, p.get('turn', 0.0) * 0.4))
    rig.rotate_about_head('head', Quaternion(rig.left, p.get('nod', 0.0) * 0.6) @ Quaternion(rig.up, p.get('turn', 0.0) * 0.6) @ Quaternion(rig.fwd, p.get('tilt', 0.0)))
    for s, sg in (('l', 1), ('r', -1)):
        th, ca = 'thigh_' + s, 'calf_' + s
        if th in rig.pb:
            lk = p.get('leg_' + s, (0.0, 0.0)); knee = p.get('knee_' + s, 0.0)
            rig.aim(th, ca, rig.dir(sg * (0.05 + lk[0]), lk[1], -1.0))
            ft = 'foot_' + s; rig.aim(ca, ft, rig.dir(sg * 0.02, -knee, -1.0))
        up, lo = 'uarm_' + s, 'farm_' + s
        a = p.get('arm_' + s)
        if a is not None:
            u = a[0]; f = a[1]
            rig.aim(up, lo, rig.dir(sg * u[0], u[1], u[2])); rig.aim(lo, 'hand_' + s, rig.dir(sg * f[0], f[1], f[2]))
    rig.update()
