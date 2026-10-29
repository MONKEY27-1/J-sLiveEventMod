"""Body-level authoring on top of rig.py: base pose, face/hand controls, and pose-building helpers."""
import sys, os, math, pickle
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig import *
if not GENERIC: from rig import _anim_tracks, _anim_globals, _pose_from_anim, _sample

CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cache'); os.makedirs(CACHE, exist_ok=True)

def _base_idle():
    path = os.path.join(CACHE, 'base_idle.pkl')
    if os.path.exists(path): return pickle.load(open(path, 'rb'))
    clip = ag['animations'][1]; tracks = _anim_tracks(clip)
    AG = _anim_globals(0.0, tracks); P = _pose_from_anim(AG)
    pa = [x / a_f for x in [AG[a_name['pelvis']][r][3] for r in range(3)]]
    pel = [PEL_B[r] + K_UNIT * (pa[r] - a_bind_pelvis[r]) for r in range(3)]
    th = hip_yaw(P, pel); Rc = ry(-math.degrees(th))
    P = {j: m3mul(Rc, P[j]) for j in s_joint_list}
    res = (P, [pel[0] - PEL_B[0], pel[1] - PEL_B[1], pel[2] - PEL_B[2]])
    pickle.dump(res, open(path, 'wb')); return res
if GENERIC: BASE_P, BASE_DPEL = {j: [r[:] for r in I3] for j in s_joint_list}, [0.0, 0.0, 0.0]      # bind pose is the base for the robot / monster
else: BASE_P, BASE_DPEL = _base_idle()

def copy_pose(P): return {j: [r[:] for r in R] for j, R in P.items()}
def rebuild_children(P, root, Lp_override=None):
    """Re-derive the subtree below `root` after P[root] changed, keeping each descendant's rotation RELATIVE to its parent."""
    pass

def apply_local(P, bone, R_local):
    """Rotate `bone` (and everything below it) by R_local expressed in the PARENT's frame: P[bone] <- P[parent] * R_local * (P[parent]^T P[bone])."""
    j = N(bone); p = jpar(j); old = P[j]
    new = m3mul(m3mul(P[p], R_local), mmT(P[p], old)) if p else m3mul(R_local, old)
    D = m3mul(new, m3T(old))                       # world-frame delta applied to this bone
    for s in subtree[j]: P[s] = m3mul(D, P[s])
    return P

def apply_world(P, bone, D):
    """Left-multiply a world-frame rotation onto `bone` and its subtree (pivot at the bone's joint)."""
    for s in subtree[N(bone)]: P[s] = m3mul(D, P[s])
    return P

def flip_side(name): return name.replace('_l', '_r') if name.endswith('_l') or '_l_' in name else name.replace('_r', '_l')

def write_preview(path, frames, fps=30.0, name='preview'):
    clip = bake_clip(name, frames, fps); write_glb(path, [clip])
