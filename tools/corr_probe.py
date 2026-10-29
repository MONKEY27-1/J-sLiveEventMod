import sys, math, json, bisect
sys.path.insert(0,'.')
from glbmath import *
g,b=load_glb("C:/Users/drago/Downloads/cattus_-_event_animations_-_fortnite.glb")
nm={i:n['name'] for i,n in enumerate(g['nodes'])}
order=['Cattus_Act1A','Cattus_Act1B','Cattus_Act1_C','Cattus_Act2','Cattus_Act3_A','Cattus_Act3_B']
anims={a['name'].split('|')[1]:a for a in g['animations']}
bones=('spine_02','spine_03','spine_04','neck_01','neck_02','head_0','jaw_01','clavicle_l','clavicle_r','upperarm_l','upperarm_r','lowerarm_l','lowerarm_r','thigh_l','thigh_r','calf_l','calf_r','pelvis')
def bone_ids(p): return [i for i,n in nm.items() if n.startswith(p+'_') or n==p]
DT=0.1
act=[]; starts=[]; t0=0.0
for name in order:
    a=anims[name]; dur=max(g['accessors'][s['input']].get('max',[0])[0] for s in a['samplers']); starts.append(round(t0,2))
    chans={}
    for ch in a['channels']:
        if ch['target']['path']!='rotation': continue
        nn=nm[ch['target']['node']]
        for bp in bones:
            if nn.startswith(bp+'_') and not nn.endswith('end') and 'twist' not in nn and 'dyn' not in nn and 'reverse' not in nn:
                s=a['samplers'][ch['sampler']]; chans[nn]=([x[0] for x in read_acc(g,b,s['input'])], read_acc(g,b,s['output']))
    n=int(dur/DT); prev=None; arr=[]
    for k in range(n+1):
        t=k*DT; qs=[]
        for nn,(ts,vs) in chans.items():
            i=min(len(ts)-1,bisect.bisect_left(ts,t)); qs.append(vs[i])
        if prev is not None:
            e=0
            for q,p in zip(qs,prev):
                d=abs(q[0]*p[0]+q[1]*p[1]+q[2]*p[2]+q[3]*p[3]); e+=2*math.degrees(math.acos(min(1,d)))
            arr.append(e)
        else: arr.append(0)
        prev=qs
    act+=arr; t0+=dur
    print(name, 'dur',round(dur,1),'bones',len(chans),'mean act',round(sum(arr)/len(arr),1))
json.dump({'dt':DT,'act':act,'starts':starts,'order':order},open('cache/showdown/monster_activity.json','w'))
# correlate with audio rms
a=json.load(open('cache/showdown/audio_analysis.json')); rms=a['rms']; fps=a['fps']
aud=[sum(rms[int(i*DT*fps):int((i+1)*DT*fps)+1])/max(1,len(rms[int(i*DT*fps):int((i+1)*DT*fps)+1])) for i in range(int(a['duration']/DT))]
def norm(x):
    m=sum(x)/len(x); s=(sum((v-m)**2 for v in x)/len(x))**0.5 or 1; return [(v-m)/s for v in x]
def smooth(x,n): 
    return [sum(x[max(0,i-n):i+n+1])/len(x[max(0,i-n):i+n+1]) for i in range(len(x))]
A=norm(smooth(aud,5)); M=norm(smooth(act,5)); best=[]
for lag in range(-600,600,2):                      # audio time = monster time + lag*DT
    s=0;n=0
    for i in range(len(A)):
        j=i-lag
        if 0<=j<len(M): s+=A[i]*M[j]; n+=1
    best.append((s/max(1,n),lag*DT))
best.sort(reverse=True); print('best offsets (audio t = monster t + off):',[(round(c,3),round(o,1)) for c,o in best[:6]])
