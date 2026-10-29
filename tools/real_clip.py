"""Real Fortnite event animations of the Devourer (cattus_-_event_animations_-_fortnite.glb) as poses in my rig's P-space.
act_pose(act, t) -> (P, pelvis_world_translation_raw). Import with the RIG_* environment set by this module."""
import os, sys, math, bisect
os.environ.update(RIG_SKIN=r'C:\Users\drago\Downloads\cattus_-_event_animations_-_fortnite.glb', RIG_STRIP='1', RIG_BODYH='1.6', RIG_KEEP_T='1', RIG_ALIAS='spine_05=spine_04')
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig import *

ORDER = ['Cattus_Act1A', 'Cattus_Act1B', 'Cattus_Act1_C', 'Cattus_Act2', 'Cattus_Act3_A', 'Cattus_Act3_B']
_acts = {}
for _a in sg['animations']:
    _name = _a['name'].split('|')[-1]; rot = {}; tr = {}; dur = 0.0
    for _ch in _a['channels']:
        _s = _a['samplers'][_ch['sampler']]; _p = _ch['target']['path']; _n = _ch['target']['node']
        if _p not in ('rotation', 'translation'): continue
        if _n not in s_joint_list and _p == 'rotation': continue
        _t = [x[0] for x in read_acc(sg, sb, _s['input'])]; _v = [tuple(x) for x in read_acc(sg, sb, _s['output'])]
        (rot if _p == 'rotation' else tr)[_n] = (_t, _v); dur = max(dur, _t[-1])
    _acts[_name] = {'rot': rot, 'tr': tr, 'dur': dur}
DUR = {k: v['dur'] for k, v in _acts.items()}
_rest = {j: tuple(sg['nodes'][j].get('rotation', [0, 0, 0, 1])) for j in s_joint_list}
_PELN = PEL

def _samp(track, t, is_rot):
    ts, vs = track
    if t <= ts[0]: return vs[0]
    if t >= ts[-1]: return vs[-1]
    i = bisect.bisect_right(ts, t) - 1; f = (t - ts[i]) / (ts[i + 1] - ts[i])
    if is_rot: return slerp(vs[i], vs[i + 1], f)
    return tuple(x + (y - x) * f for x, y in zip(vs[i], vs[i + 1]))

def act_pose(act, t):
    A = _acts[act]; R = {}; P = {}
    for j in s_joint_list:
        par = s_par[j]
        if j in static_joints: R[j] = rot_of(s_G0[j]); P[j] = [r[:] for r in I3]; continue
        Rp = R[par] if par in R else rot_of(s_G0[par]) if par >= 0 else I3
        q = _samp(A['rot'][j], t, True) if j in A['rot'] else _rest[j]
        R[j] = m3mul(Rp, quat_to_m3(q)); P[j] = m3mul(R[j], m3T(RB[j]))
    pel = _samp(A['tr'][PEL], t, False) if PEL in A['tr'] else tuple(sg['nodes'][PEL].get('translation', [0, 0, 0]))
    return P, pel
