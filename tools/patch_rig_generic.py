p = r'C:\Users\drago\Documents\AstronomicalEventMod\tools\rig.py'
s = open(p, encoding='utf-8').read()
def rep(a, b, cnt=1):
    global s
    assert a in s, a[:70]
    s = s.replace(a, b, cnt)

rep('SKIN = r"C:\\Users\\drago\\Downloads\\travis_scott_fortnite_skin.glb"\nANIM = r"C:\\Users\\drago\\Downloads\\travis_scott_fortnite_event_animation_1.glb"',
    '''SKIN = os.environ.get('RIG_SKIN', r"C:\\Users\\drago\\Downloads\\travis_scott_fortnite_skin.glb")
GENERIC = 'RIG_SKIN' in os.environ                       # another character (robot / monster): no retarget clip, bind data rebuilt from the node tree
ANIM = None if GENERIC else r"C:\\Users\\drago\\Downloads\\travis_scott_fortnite_event_animation_1.glb"
STRIP = int(os.environ.get('RIG_STRIP', '2'))''')
rep("for ji in ssk['joints']: s_name.setdefault(base(sg['nodes'][ji]['name'], 2), ji)\nnode_to_name = {ji: base(sg['nodes'][ji]['name'], 2) for ji in ssk['joints']}",
    '''for ji in ssk['joints']: s_name.setdefault(base(sg['nodes'][ji]['name'], STRIP), ji)
node_to_name = {ji: base(sg['nodes'][ji]['name'], STRIP) for ji in ssk['joints']}
for _al in filter(None, os.environ.get('RIG_ALIAS', '').split(',')):       # e.g. head=head_offset
    _a, _b = _al.split('=')
    if _b in s_name: s_name[_a] = s_name[_b]''')
# bind data: rebuild when IBMs are degenerate
rep("""BP = {j: bind_pos(j) for j in s_joint_list}                                # bind positions (mesh space)
RB = {j: m3T(rot_of(s_ibm[s_joint_index[j]])) for j in s_joint_list}       # bind world rotation of each joint = rot(inv IBM)""",
"""def _ibm_ok(j): return j in s_joint_index and abs(_col_scale(inv4(s_ibm[s_joint_index[j]])) - 1.0) < 0.05
BP = {}; RB = {}
for _j in s_joint_list:
    if _ibm_ok(_j) or not GENERIC:
        BP[_j] = bind_pos(_j); RB[_j] = m3T(rot_of(s_ibm[s_joint_index[_j]]))
    else:                                                                     # unweighted bones of the exported models carry a zeroed inverse-bind matrix: rebuild from the parent
        _n = sg['nodes'][_j]; _p = s_par[_j]; _q = _n.get('rotation', [0, 0, 0, 1])
        if _p in BP:
            BP[_j] = vadd(BP[_p], mv(RB[_p], _n.get('translation', [0, 0, 0]))); RB[_j] = m3mul(RB[_p], quat_to_m3(_q))
        else: BP[_j] = [0, 0, 0]; RB[_j] = I3
RB_PREFIX = None""")
rep("BODY_H = vlen(vsub(BP[s_name['head']], PEL_B)) * 2.9", "BODY_H = float(os.environ['RIG_BODYH']) if 'RIG_BODYH' in os.environ else vlen(vsub(BP[s_name['head']], PEL_B)) * 2.9")
rep("    return {sj: (rot_of(s_G0[sj]) if sj in static_joints else m3mul(m3mul(S_RM, P[sj]), m3T(rot_of(s_ibm[s_joint_index[sj]])))) for sj in s_joint_list}",
    "    return {sj: (rot_of(s_G0[sj]) if sj in static_joints else m3mul(m3mul(S_RM, P[sj]), m3T(RB[sj]))) for sj in s_joint_list}")
# anim section only when there is an anim file
a = s.index("ag, ab = load_glb(ANIM)")
b = s.index("# ---------- GLB writer ----------")
block = s[a:b]
block = "if ANIM:\n" + "".join(("    " + l if l.strip() else l) for l in block.splitlines(True))
s = s[:a] + block + "\n" + s[b:]
open(p, 'w', encoding='utf-8').write(s)

p = r'C:\Users\drago\Documents\AstronomicalEventMod\tools\body.py'
s = open(p, encoding='utf-8').read()
rep("BASE_P, BASE_DPEL = _base_idle()", "if GENERIC: BASE_P, BASE_DPEL = {j: [r[:] for r in I3] for j in s_joint_list}, [0.0, 0.0, 0.0]      # bind pose is the base for the robot / monster\nelse: BASE_P, BASE_DPEL = _base_idle()")
rep("from rig import _anim_tracks, _anim_globals, _pose_from_anim, _sample", "if not GENERIC: from rig import _anim_tracks, _anim_globals, _pose_from_anim, _sample")
open(p, 'w', encoding='utf-8').write(s)
print('ok')
