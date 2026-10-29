import sys, math
sys.path.insert(0,'.')
from glbmath import *
g,b=load_glb("C:/Users/drago/Downloads/cattus_-_event_animations_-_fortnite.glb")
nm={i:n['name'] for i,n in enumerate(g['nodes'])}
def find(prefix): return [i for i,n in nm.items() if n.startswith(prefix)]
for a in g['animations']:
    print('=====',a['name'])
    for pref in ('root_02','pelvis_03','ik_foot_root','jaw_01','head_0'):
        ids=find(pref)
        for ch in a['channels']:
            if ch['target']['node'] in ids and ch['target']['path']=='translation' and pref in ('root_02','pelvis_03','ik_foot_root'):
                s=a['samplers'][ch['sampler']]; t=read_acc(g,b,s['input']); v=read_acc(g,b,s['output'])
                n=len(v); 
                print(pref,'T keys',n,'first',[round(x,2) for x in v[0]],'mid',[round(x,2) for x in v[n//2]],'last',[round(x,2) for x in v[-1]])
            if ch['target']['node'] in ids and ch['target']['path']=='rotation' and pref in ('jaw_01',):
                s=a['samplers'][ch['sampler']]; t=[x[0] for x in read_acc(g,b,s['input'])]; v=read_acc(g,b,s['output'])
                ang=[2*math.degrees(math.acos(min(1,abs(q[3])))) for q in v]
                # report times where jaw angle high relative to median
                med=sorted(ang)[len(ang)//2]; pk=[(round(tt,1),round(aa)) for tt,aa in zip(t,ang) if aa>med+20][:60:3]
                print('jaw med %.0f max %.0f'%(med,max(ang)), pk[:14])
