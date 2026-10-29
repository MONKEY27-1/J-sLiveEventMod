import sys, os
sys.path.insert(0, os.getcwd())
from glbmath import *
g, b = load_glb(r"C:\Users\drago\Downloads\travis_scott_fortnite_event_animation_1.glb")
sk = g['skins'][0]; ibm = [from_colmajor(m) for m in read_acc(g, b, sk['inverseBindMatrices'])]
pel = [ji for ji in sk['joints'] if g['nodes'][ji]['name'].startswith('pelvis')][0]
for ai in (0, 1):
    clip = g['animations'][ai]; tr = {}
    for ch in clip['channels']:
        if ch['target'].get('node') == pel:
            s = clip['samplers'][ch['sampler']]; tr[ch['target']['path']] = ([x[0] for x in read_acc(g, b, s['input'])], read_acc(g, b, s['output']))
    print(clip['name'], "pelvis tracks:", list(tr))
    if 'translation' in tr:
        ts, vs = tr['translation']; n = len(ts)
        for k in range(0, n, max(1, n // 10)): print("  t=%.1f translation (local, parent frame) %s" % (ts[k], [round(x, 3) for x in vs[k]]))
        xs = [v[0] for v in vs]; ys = [v[1] for v in vs]; zs = [v[2] for v in vs]
        print("  range x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
