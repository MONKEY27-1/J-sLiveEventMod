import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
# scale + translation variation per act
for a in sg['animations']:
    nm=a['name'].split('|')[-1]; nvt=0; nvs=0; mt=0; ms=0; worst=[]
    for ch in a['channels']:
        p=ch['target']['path']
        if p=='rotation': continue
        s=a['samplers'][ch['sampler']]; v=[tuple(x) for x in read_acc(sg,sb,s['output'])]; n=ch['target']['node']
        if p=='translation':
            rest=tuple(sg['nodes'][n].get('translation',[0,0,0])) if True else None
            dev=max(max(abs(x[i]-v[0][i]) for i in range(3)) for x in v)
            if n!=PEL and dev>1e-4: nvt+=1; mt=max(mt,dev); worst.append((node_to_name[n],round(dev,3)))
        else:
            dev=max(max(abs(x[i]-v[0][i]) for i in range(3)) for x in v)
            if dev>1e-4: nvs+=1; ms=max(ms,dev)
    print(nm,'varying trans',nvt,'max',round(mt,3),'varying scales',nvs,'max',round(ms,3),sorted(worst,key=lambda w:-w[1])[:6])
