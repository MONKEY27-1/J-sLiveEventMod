p='rig.py'; s=open(p,encoding='utf-8').read()
a="    s_G0 = _global_rest(sg, s_order, s_par)"
assert a in s
s=s.replace(a,a+'''
    # repair the zeroed inverse-bind matrices of unweighted joints (some importers choke on them) so the exported file is clean
    _acc = sg['accessors'][ssk['inverseBindMatrices']]; _bv = sg['bufferViews'][_acc['bufferView']]; _off = _bv.get('byteOffset', 0) + _acc.get('byteOffset', 0)
    for _k, _ji in enumerate(ssk['joints']):
        if not _ibm_ok(_ji):
            _M = [[RB[_ji][r][c] for c in range(3)] + [BP[_ji][r]] for r in range(3)] + [[0, 0, 0, 1]]
            _I = inv4(_M); s_ibm[_k] = _I
            struct.pack_into('<16f', sb, _off + _k * 64, *[_I[r][c] for c in range(4) for r in range(4)])''',1)
open(p,'w',encoding='utf-8').write(s)
