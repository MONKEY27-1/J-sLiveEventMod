import sys, os, math, collections
sys.path.insert(0,'.')
from glbmath import *
path=sys.argv[1]; g,b=load_glb(path); sk=g['skins'][0]; J=sk['joints']
ibm=[from_colmajor(m) for m in read_acc(g,b,sk['inverseBindMatrices'])]
bad=set(k for k in range(len(J)) if abs(math.sqrt(sum(inv4(ibm[k])[r][0]**2 for r in range(3)))-1)>0.05)
for me in g['meshes']:
    for pr in me['primitives']:
        at=pr['attributes']
        if 'JOINTS_0' not in at: continue
        jo=read_acc(g,b,at['JOINTS_0']); we=read_acc(g,b,at['WEIGHTS_0'])
        c=collections.Counter(); tot=0; badw=0
        for ja,wa in zip(jo,we):
            for jj,ww in zip(ja,wa):
                if ww>0: c[int(jj)]+=ww; tot+=ww; badw+= ww if int(jj) in bad else 0
        top=c.most_common(6)
        print(me['name'][-24:], 'weight on bad-IBM joints: %.1f%%'%(100*badw/tot), [(g['nodes'][J[k]]['name'][:22],round(w)) for k,w in top])
