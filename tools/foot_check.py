import sys, json, struct, bisect, math
sys.path.insert(0,'.')
from glbmath import *
def read_glb(p):
    d=open(p,'rb').read(); l,_=struct.unpack_from('<II',d,12); g=json.loads(d[20:20+l]); o=20+l; bl,_=struct.unpack_from('<II',d,o); return g, d[o+8:o+8+bl]
g,b=read_glb(sys.argv[1]); height=float(sys.argv[2])
nodes=g['nodes']; par=parents_of(g); depth=lambda i: 0 if par[i]<0 else 1+depth(par[i]); order=sorted(range(len(nodes)),key=depth)
anim=g['animations'][0]; ch={}
for c in anim['channels']:
    s=anim['samplers'][c['sampler']]; ts=[x[0] for x in read_acc(g,b,s['input'])]; vs=[tuple(x) for x in read_acc(g,b,s['output'])]
    ch[(c['target']['node'],c['target']['path'])]=(ts,vs)
def samp(key,t):
    ts,vs=ch[key]
    if t<=ts[0]: return vs[0]
    if t>=ts[-1]: return vs[-1]
    i=bisect.bisect_right(ts,t)-1; f=(t-ts[i])/(ts[i+1]-ts[i]); 
    if key[1]=='rotation': return slerp(vs[i],vs[i+1],f)
    return tuple(a+(c-a)*f for a,c in zip(vs[i],vs[i+1]))
def world(t=None):
    G={}
    for i in order:
        n=nodes[i]; T=n.get('translation',[0,0,0]); q=n.get('rotation',[0,0,0,1]); S=n.get('scale',[1,1,1])
        if t is not None:
            if (i,'translation') in ch: T=samp((i,'translation'),t)
            if (i,'rotation') in ch: q=samp((i,'rotation'),t)
        L=trs_to_m4(T,q,S); G[i]=mmul(G[par[i]],L) if par[i]>=0 else L
    return G
sk=g['skins'][0]['joints']; G0=world(None)
ys=[G0[j][1][3] for j in sk]; jb=max(ys)-min(ys); miny=min(ys); k=height/jb
def name(i): return nodes[i]['name']
def find(prefix):
    c=[j for j in sk if name(j).startswith(prefix+'_') and name(j)[len(prefix)+1:].replace('_','').isdigit()]; return min(c,key=lambda j:len(name(j)))
fl,fr,pel,hd=find('foot_l'),find('foot_r'),find('pelvis'),find('head_offset')
print('metres per unit',k)
for t in [float(x) for x in sys.argv[3:]]:
    G=world(t); y=lambda j:round(k*(G[j][1][3]-miny),1)
    print('t',t,'foot_l',y(fl),'foot_r',y(fr),'pelvis',y(pel),'head',y(hd))
