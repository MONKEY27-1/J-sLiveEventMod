import json, struct, sys
d=open(sys.argv[1],'rb').read(); jl=struct.unpack('<I',d[12:16])[0]; g=json.loads(d[20:20+jl])
sk=g['skins'][0]; J=sk['joints']; par={}
for i,n in enumerate(g['nodes']):
    for c in n.get('children',[]): par[c]=i
js=set(J)
def depth(j):
    k=0
    while j in par and par[j] in js: j=par[j]; k+=1
    return k
for j in J[:int(sys.argv[2])]:
    n=g['nodes'][j]; print('  '*depth(j)+n['name'], [round(x,2) for x in n.get('translation',[])], [round(x,2) for x in n.get('scale',[])] if 'scale' in n else '')
