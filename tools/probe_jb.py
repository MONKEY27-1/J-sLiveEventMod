import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
js=ssk['joints']; ys=[];xs=[];zs=[]
for j in js:
    G=s_G0[j]; xs.append(-G[0][3]); ys.append(G[1][3]); zs.append(G[2][3])
print(len(js),'x',min(xs),max(xs),'y',min(ys),max(ys),'z',min(zs),max(zs))
sx=max(xs)-min(xs); sy=max(ys)-min(ys); print('size y',sy,'scale for 110m',110/sy,'center x',(max(xs)+min(xs))/2,'min y',min(ys),'center z',(max(zs)+min(zs))/2)
import collections
order=sorted(range(len(js)), key=lambda i: ys[i])
print('lowest',[(node_to_name.get(js[i]),round(ys[i],2)) for i in order[:5]]); print('highest',[(node_to_name.get(js[i]),round(ys[i],2)) for i in order[-5:]])
