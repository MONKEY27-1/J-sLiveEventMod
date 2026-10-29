"""blender -b -P dance_gen.py -- <which: marsh|bear> <audio.json> <out.glb> [max_seconds]
Generates the concert dances: one long animation per clip that never repeats a move. The song's beat grid (audio.json 'beats') is cut into segments of two beats; every segment is its own
animation: a pose chosen from a vocabulary (arms, legs, spine, head, hips) that no earlier segment used, eased from the previous pose, with a beat bounce on top and amplitudes that follow the song's
loudness. 'marsh' = Marshmello: clip 'DJ' (hands on the decks, now and then a crowd move) and clip 'Dance' (the giant hologram); 'bear' = the Cuddle Team Leader: clips 'BearA' and 'BearB'.
Meshes are hidden while baking (fast); the glTF exporter samples the result at 12 fps. Prints the number of unique segments."""
import bpy, sys, math, json, random, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dance_lib import *
import numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; which, audio, out = a[0], a[1], a[2]; maxs = float(a[3]) if len(a) > 3 else 1e9
FPS = 12
A = json.load(open(audio)); beats = [b for b in A['beats'] if b >= 0]; DUR = min(A['duration'], maxs); rms = np.array(A['rms'], np.float32); rms = rms / (rms.max() + 1e-6)
def energy(t, w=1.5):
    i0 = max(0, int((t - w) * 30)); i1 = min(len(rms), int((t + w) * 30) + 1); return float(np.clip(rms[i0:i1].mean() * 2.2, 0.05, 1.0)) if i1 > i0 else 0.3
if beats[0] > 0.01: beats = [0.0] + beats
dense = []
for x, y in zip(beats[:-1], beats[1:]):                  # beats further apart than 0.9 s are split, so a segment (two beats) is about 1.2 s: hundreds of moves in the song
    k = max(1, int(round((y - x) / 0.6))); dense += [x + (y - x) * q / k for q in range(k)]
beats = dense + [beats[-1]]
if beats[-1] < A['duration']: beats.append(A['duration'])
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
bpy.ops.wm.read_factory_settings(use_empty=True)
path = r"C:\Users\drago\Downloads\fortnite_marshmellow_skin__item_shop_skin.glb" if which == 'marsh' else r"C:\Users\drago\Downloads\cuddle_team_leader_fortnite_royal_battle.glb"
bpy.ops.import_scene.gltf(filepath=path)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][-1]; arm.animation_data_clear()
for ac in list(bpy.data.actions): bpy.data.actions.remove(ac)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name.startswith('Icosphere'): bpy.data.objects.remove(o, do_unlink=True)             # the importer's bone-shape helper
if which == 'marsh':
    n = dict(pelvis='pelvis', neck='neck_01', head='head', uarm_l='upperarm_l', farm_l='lowerarm_l', hand_l='hand_l', uarm_r='upperarm_r', farm_r='lowerarm_r', hand_r='hand_r', thigh_l='thigh_l', calf_l='calf_l', foot_l='foot_l', thigh_r='thigh_r', calf_r='calf_r', foot_r='foot_r')
    rig = Rig(arm, n, (0, 1, 0), (0, 0, 1), (1, 0, 0), 10000.0); chain = ['spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05']
else:
    n = {'pelvis': 'mixamorig:Hips', 'neck': 'mixamorig:Neck', 'head': 'mixamorig:Head', 'uarm_l': 'mixamorig:LeftArm', 'farm_l': 'mixamorig:LeftForeArm', 'hand_l': 'mixamorig:LeftHand', 'uarm_r': 'mixamorig:RightArm', 'farm_r': 'mixamorig:RightForeArm', 'hand_r': 'mixamorig:RightHand',
         'thigh_l': 'mixamorig:LeftUpLeg', 'calf_l': 'mixamorig:LeftLeg', 'foot_l': 'mixamorig:LeftFoot', 'thigh_r': 'mixamorig:RightUpLeg', 'calf_r': 'mixamorig:RightLeg', 'foot_r': 'mixamorig:RightFoot'}
    rig = Rig(arm, n, (0, 0, 1), (0, -1, 0), (1, 0, 0), 1.0); chain = ['mixamorig:Spine', 'mixamorig:Spine1', 'mixamorig:Spine2']
for k in chain: rig.pb[k] = rig.find(k)
rig.n['spine_chain'] = chain
keyed = list(dict.fromkeys([rig.pb[k] for k in rig.pb]))
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_viewport = True; o.hide_render = True

# ---------------------------------------------------------------- vocabulary (directions are (side, forward, up); side is mirrored for the right arm / leg)
ARMS = {'down': ((0.15, 0, -1), (0.1, 0.1, -1)), 'out': ((1, 0, 0.1), (1, 0.1, 0.1)), 'up': ((0.25, 0, 1), (0.1, 0, 1)), 'vup': ((0.1, 0, 1), (0.05, 0, 1)), 'fwd': ((0.2, 1, 0), (0.1, 1, 0.1)), 'diag_up': ((0.8, 0.1, 0.8), (0.8, 0.2, 0.9)),
        'diag_down': ((0.7, 0.1, -0.7), (0.8, 0.1, -0.5)), 'cross': ((-0.5, 0.8, 0.1), (-0.9, 0.6, 0.2)), 'on_head': ((0.4, 0, 0.9), (-0.6, 0.3, 0.6)), 'hip': ((0.8, -0.1, -0.4), (-0.4, 0.3, -0.5)), 'point_up': ((0.5, 0.3, 0.9), (0.4, 0.3, 1)),
        'wave': ((0.7, 0, 0.7), (0.4, 0, 1.0)), 'decks': ((0.1, 0.8, -0.5), (0.1, 1, -0.2)), 'scratch': ((0.1, 0.8, -0.6), (0.1, 0.9, -0.4)), 'chest': ((0.3, 0.5, -0.2), (-0.3, 0.8, 0.3)), 'clap': ((0.2, 0.8, 0.1), (-0.7, 0.9, 0.1)),
        'flex': ((1, 0, 0.2), (0.2, 0, 1)), 'broad': ((1, 0, 0.5), (1, 0, 0.6)), 'reach': ((0.4, 0.8, 0.5), (0.3, 1, 0.5)), 'hook': ((0.9, 0.3, 0.3), (-0.3, 0.7, 0.5)), 'swing_back': ((0.5, -0.8, -0.3), (0.5, -1, -0.1)), 'heart': ((0.5, 0.6, 0.5), (-0.7, 0.7, 0.3))}
CROWD = ['up', 'vup', 'diag_up', 'wave', 'point_up', 'clap', 'broad', 'flex', 'reach', 'heart', 'on_head', 'out']
DECK = ['decks', 'scratch', 'chest', 'fwd', 'decks', 'scratch']
DANCE = list(ARMS.keys())
LEG = [(0.0, 0.0), (0.25, 0.0), (0.5, 0.0), (0.15, 0.35), (0.15, -0.3), (0.35, 0.2), (0.0, 0.5), (0.4, -0.2)]
def pick(rng, vocab, e):
    return rng.choice(vocab)
def make_pose(rng, kind, e, key):
    """a target pose of the given dancer kind; `key` collects the discrete choices so that no two segments are the same"""
    P = {}
    if kind == 'dj':
        crowd = rng.random() < 0.25 + 0.4 * e; pl = pick(rng, CROWD if crowd else DECK, e); pr = pick(rng, CROWD if (crowd and rng.random() < 0.8) else DECK, e); key += [pl, pr]
        leg = rng.randrange(0, 3); P['leg_l'] = (LEG[leg][0] * 0.4, LEG[leg][1] * 0.3); P['leg_r'] = (LEG[(leg + 1) % 3][0] * 0.4, LEG[(leg + 1) % 3][1] * 0.3); key.append(leg)
        P['lean'] = rng.uniform(-0.1, 0.18) * (0.5 + e); P['twist'] = rng.uniform(-0.3, 0.3) * (0.4 + e); P['roll'] = rng.uniform(-0.12, 0.12); P['hip'] = (rng.uniform(-0.04, 0.04), rng.uniform(-0.03, 0.03), 0)
        P['nod'] = rng.uniform(-0.15, 0.3); P['turn'] = rng.uniform(-0.6, 0.6); P['tilt'] = rng.uniform(-0.2, 0.2); P['knee_l'] = rng.uniform(0, 0.15); P['knee_r'] = rng.uniform(0, 0.15)
    else:
        pl = pick(rng, DANCE, e); pr = pick(rng, DANCE, e); key += [pl, pr]; li = rng.randrange(len(LEG)); ri = rng.randrange(len(LEG)); key += [li, ri]
        P['leg_l'] = (LEG[li][0] * (0.6 + 0.5 * e), LEG[li][1]); P['leg_r'] = (LEG[ri][0] * (0.6 + 0.5 * e), LEG[ri][1])
        P['lean'] = rng.uniform(-0.25, 0.45) * (0.4 + e); P['twist'] = rng.uniform(-0.8, 0.8) * (0.3 + e); P['roll'] = rng.uniform(-0.35, 0.35) * (0.4 + e)
        P['hip'] = (rng.uniform(-0.12, 0.12) * (0.4 + e), rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.02) * (0.5 + e)); P['nod'] = rng.uniform(-0.3, 0.5); P['turn'] = rng.uniform(-0.9, 0.9); P['tilt'] = rng.uniform(-0.4, 0.4)
        P['knee_l'] = rng.uniform(0, 0.5) * (0.4 + e); P['knee_r'] = rng.uniform(0, 0.5) * (0.4 + e)
    amp = 1.0 + 0.35 * e; au = ARMS[pl]; bu = ARMS[pr]
    P['arm_l'] = ((au[0][0], au[0][1], au[0][2]), (au[1][0], au[1][1], au[1][2])); P['arm_r'] = ((bu[0][0], bu[0][1], bu[0][2]), (bu[1][0], bu[1][1], bu[1][2]))
    return P
def lerp(a, b, u):
    if isinstance(a, (int, float)): return a + (b - a) * u
    return tuple(lerp(x, y, u) for x, y in zip(a, b))
def lerp_pose(A_, B_, u): return {k: lerp(A_[k], B_[k], u) for k in A_}
def smooth(u): return u * u * (3 - 2 * u)

def bake(name, kind, seed):
    arm.animation_data_create(); arm.animation_data.action = None; rec = []             # no action while posing: with one assigned, every depsgraph update would put the keyed values back over the pose
    rng = random.Random(seed); seen = set(); segs = []; cur = None
    nb = len(beats) - 1; i = 0
    while i < nb:
        j = min(nb, i + 2); t0, t1 = beats[i], beats[j]
        if t1 - t0 < 0.05: i = j; continue
        key = []; e = energy((t0 + t1) / 2)
        for attempt in range(200):
            key = []; P = make_pose(rng, kind, e, key); sig = tuple(key) + (round(P['lean'] * 10),) + (round(P['twist'] * 6), round(P['turn'] * 4), round(P['hip'][0] * 40))
            if sig not in seen: seen.add(sig); break
        segs.append((t0, t1, P, e)); i = j
    print(name, 'segments', len(segs), 'unique', len(seen))
    f_end = int(DUR * FPS); sidx = 0; prev = segs[0][2]; t_start = time.time()
    for f in range(f_end + 1):
        t = f / FPS
        while sidx < len(segs) - 1 and t >= segs[sidx][1]: prev = segs[sidx][2]; sidx += 1
        t0, t1, P, e = segs[sidx]; u = smooth(min(1.0, max(0.0, (t - t0) / max(0.05, (t1 - t0) * 0.7)))); pose_ = lerp_pose(prev, P, u)
        # the beat: a squash on the beat that recovers, a nod
        bi = max(0, min(len(beats) - 2, np.searchsorted(beats, t, side='right') - 1)); ph = (t - beats[bi]) / max(0.05, beats[bi + 1] - beats[bi]); acc = (1 - ph) ** 2
        h = pose_['hip']; hz = h[2] - (0.045 if kind == 'dj' else 0.08) * (0.4 + e) * acc; pose_['hip'] = (h[0] + 0.02 * math.sin(t * 1.3) * (0.3 + e), h[1], hz)
        pose_['nod'] = pose_['nod'] + (0.18 if kind == 'dj' else 0.3) * acc; pose_['knee_l'] = pose_['knee_l'] + 0.15 * acc * (0.4 + e); pose_['knee_r'] = pose_['knee_r'] + 0.15 * acc * (0.4 + e)
        for side in ('arm_l', 'arm_r'): pose_[side] = ((pose_[side][0][0], pose_[side][0][1], pose_[side][0][2] + 0.12 * acc * e), pose_[side][1])
        pose(rig, pose_)
        rec.append([(tuple(pb.rotation_quaternion), tuple(pb.location)) for pb in keyed])
        if f % 300 == 0: print('DBG', f, [tuple(round(x,2) for x in rig.pb[k].rotation_quaternion) for k in ('farm_l','head','uarm_l')], 'hipsloc', tuple(round(x,3) for x in rig.pb['pelvis'].location), flush=True)
        if f % 600 == 0: print(' ', name, 'pose', f, '/', f_end, 'elapsed %.0f s' % (time.time() - t_start), flush=True)
    act = bpy.data.actions.new(name); arm.animation_data.action = act
    for f in range(f_end + 1):
        for pb, (q, l) in zip(keyed, rec[f]): pb.rotation_quaternion = q; pb.location = l; pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f)
        if f % 1200 == 0: print(' ', name, 'key', f, '/', f_end, 'elapsed %.0f s' % (time.time() - t_start), flush=True)
    arm.animation_data.action = None
    return act

acts = []
if which == 'marsh': acts.append(bake('DJ', 'dj', 11)); acts.append(bake('Dance', 'dance', 29))
else: acts.append(bake('BearA', 'dance', 41)); acts.append(bake('BearB', 'dance', 77))
for ac in acts: ac.use_fake_user = True
arm.animation_data.action = acts[0]
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_viewport = False; o.hide_render = False
sc = bpy.context.scene; sc.render.fps = FPS; sc.frame_start = 0; sc.frame_end = int(DUR * FPS)
bpy.ops.object.select_all(action='DESELECT')
# every action becomes an animation of the glb (they sit in NLA tracks of the armature)
for ac in acts:
    tr = arm.animation_data.nla_tracks.new(); tr.name = ac.name; st = tr.strips.new(ac.name, 0, ac); st.name = ac.name
arm.animation_data.action = None
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_animations=True, export_animation_mode='NLA_TRACKS', export_force_sampling=True, export_optimize_animation_size=True, export_image_format='AUTO', export_frame_range=False)
print('wrote', out, os.path.getsize(out) // 1024, 'KB')

