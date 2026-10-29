p='rig.py'; s=open(p,encoding='utf-8').read()
a="RB_PREFIX = None"
assert a in s
s=s.replace(a,'''if GENERIC:                                                               # make the node rest pose agree with the bind pose the mesh was skinned to
    _static0 = set(); _q = s_par[s_name['pelvis']]
    while _q in s_is_joint: _static0.add(_q); _q = s_par[_q]
    for _j in s_joint_list:
        _p = s_par[_j]
        if _p in BP and _j not in _static0 and _p not in _static0:
            sg['nodes'][_j]['translation'] = mv(m3T(RB[_p]), vsub(BP[_j], BP[_p]))
    s_G0 = _global_rest(sg, s_order, s_par)''',1)
open(p,'w',encoding='utf-8').write(s)
