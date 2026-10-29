"""Authoring of extra Travis clips on top of the real retargeted event dance / idle.

Each spec = {name, src (0 = event dance, 1 = idle), dur (seconds, None = use the source length), fn(t, T) -> (offsets, pelvis_delta)}.
offsets: {bone name: 3x3 rotation} applied in MESH SPACE (character faces -Z, +X is his left, +Y up) on top of the base pose, to the
bone AND all of its descendants, so limbs stay connected. pelvis_delta = (dx, dy, dz) as fractions of body height.
Axes: Rx(+) swings an arm/leg FORWARD (nose-down lean is Rx(-)), Rz(+) lifts the LEFT arm outward/up (use Rz(-) for the right arm), Ry twists.
"""
import math

def rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == 'x': return [[1, 0, 0], [0, c, -s], [0, s, c]]
    if axis == 'y': return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]

def sm(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

def keys(t, kf):
    """Smoothstep interpolation through (time, value) keyframes."""
    if t <= kf[0][0]: return kf[0][1]
    for (t0, v0), (t1, v1) in zip(kf, kf[1:]):
        if t <= t1: return v0 + (v1 - v0) * sm((t - t0) / (t1 - t0))
    return kf[-1][1]

def LR(deg_left, deg_right_mirror=None):
    pass

def hype(t, T):
    ph = 2 * math.pi * t / (T / 20.0); b = math.sin(ph)
    return ({'upperarm_l': rot('z', -(25 + 22 * max(0, b))), 'upperarm_r': rot('z', 25 + 22 * max(0, -b)),
             'neck_01': rot('x', -14 * abs(b)), 'spine_01': rot('x', -6 * abs(b))}, (0, -0.02 * abs(b), 0))

def levitate(t, T):
    w = 2 * math.pi * t / T
    return ({'upperarm_l': rot('z', -(80 + 6 * math.sin(2 * w))), 'upperarm_r': rot('z', 80 + 6 * math.sin(2 * w + 1)),
             'spine_01': mm(rot('y', 6 * math.sin(w)), rot('x', 3 * math.sin(2 * w))), 'neck_01': rot('x', -4)}, (0, 0.012 * math.sin(w), 0))

def mm(A, B):
    return [[sum(A[r][k] * B[k][c] for k in range(3)) for c in range(3)] for r in range(3)]

def swim(t, T):
    w = 2 * math.pi * t / T
    return ({'spine_01': rot('x', -32 + 5 * math.sin(w)), 'neck_01': rot('x', 26),
             'clavicle_l': rot('x', 0.25 * (60 + 60 * math.sin(2 * w))), 'clavicle_r': rot('x', 0.25 * (60 + 60 * math.sin(2 * w + math.pi))),
             'upperarm_l': mm(rot('x', 0.75 * (60 + 60 * math.sin(2 * w))), rot('z', -10)), 'upperarm_r': mm(rot('x', 0.75 * (60 + 60 * math.sin(2 * w + math.pi))), rot('z', 10)),
             'thigh_l': rot('x', 22 * math.sin(4 * w)), 'thigh_r': rot('x', -22 * math.sin(4 * w)),
             'calf_l': rot('x', -10 - 10 * math.sin(4 * w)), 'calf_r': rot('x', -10 + 10 * math.sin(4 * w))}, (0, 0, 0))

def slam(t, T):
    arm = keys(t, [(0, 0), (1.6, 170), (2.3, 170), (2.6, 0), (6, 0)])
    lean = keys(t, [(0, 0), (1.6, 12), (2.3, 14), (2.6, -38), (3.6, -30), (5.5, 0), (6, 0)])
    dy = keys(t, [(0, 0), (2.3, 0.01), (2.6, -0.06), (3.6, -0.05), (5.5, 0), (6, 0)])
    crouch = sm(-dy / 0.06) if dy < 0 else 0.0
    return ({'clavicle_l': rot('x', arm * 0.28), 'clavicle_r': rot('x', arm * 0.28), 'upperarm_l': rot('x', arm * 0.72), 'upperarm_r': rot('x', arm * 0.72), 'spine_01': rot('x', lean),
             'thigh_l': rot('x', 25 * crouch), 'thigh_r': rot('x', 25 * crouch), 'calf_l': rot('x', -45 * crouch), 'calf_r': rot('x', -45 * crouch)}, (0, dy, 0))

def stride(t, T):
    p = 2 * math.pi * t / 1.0; s = math.sin(p)
    return ({'thigh_l': rot('x', 28 * s), 'thigh_r': rot('x', -28 * s),
             'calf_l': rot('x', -(25 + 25 * math.sin(p - 1.2))), 'calf_r': rot('x', -(25 + 25 * math.sin(p + math.pi - 1.2))),
             'upperarm_l': rot('x', -24 * s), 'upperarm_r': rot('x', 24 * s),
             'lowerarm_l': rot('x', 20), 'lowerarm_r': rot('x', 20), 'spine_01': rot('y', 7 * s)}, (0.012 * math.sin(p), 0.012 * abs(math.cos(p)), 0))

def headbang(t, T):
    p = 2 * math.pi * t / 0.5; s = math.sin(p)
    return ({'neck_01': rot('x', -10 - 25 * s), 'spine_05': rot('x', -8 - 10 * math.sin(p + 0.6)),
             'upperarm_l': rot('x', 70), 'upperarm_r': rot('x', 60 + 10 * s),
             'thigh_l': rot('x', 18), 'thigh_r': rot('x', 18), 'calf_l': rot('x', -34), 'calf_r': rot('x', -34)}, (0, -0.04 + 0.01 * s, 0))

def wave(t, T):
    return ({'upperarm_r': rot('z', 112), 'lowerarm_r': rot('z', 10 + 30 * math.sin(2 * math.pi * t / 0.6)),
             'upperarm_l': rot('z', -8), 'neck_01': rot('z', 5)}, (0, 0, 0))

BODY_HEIGHT_FRAC = 1.0
SPECS = [
    {'name': 'track1_7473-8320_PRC', 'src': 0, 'dur': None, 'fn': None},
    {'name': 'Travis_IntroIdle', 'src': 1, 'dur': None, 'fn': None},
    {'name': 'Travis_Hype', 'src': 0, 'dur': None, 'fn': hype},
    {'name': 'Travis_Levitate', 'src': 1, 'dur': 8.0, 'fn': levitate},
    {'name': 'Travis_Swim', 'src': 1, 'dur': 6.0, 'fn': swim},
    {'name': 'Travis_Slam', 'src': 1, 'dur': 6.0, 'fn': slam},
    {'name': 'Travis_Stride', 'src': 1, 'dur': 4.0, 'fn': stride},
    {'name': 'Travis_Headbang', 'src': 1, 'dur': 4.0, 'fn': headbang},
    {'name': 'Travis_Wave', 'src': 1, 'dur': 5.0, 'fn': wave},
]




