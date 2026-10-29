import json, struct, sys
def load(p):
    d=open(p,'rb').read(); jl=struct.unpack('<I',d[12:16])[0]; g=json.loads(d[20:20+jl]); return g
for p in sys.argv[1:]:
    g=load(p); print('=====',p)
    print('nodes',len(g['nodes']),'meshes',len(g.get('meshes',[])),'skins',len(g.get('skins',[])),'anims',len(g.get('animations',[])),'mats',len(g.get('materials',[])),'tex',len(g.get('images',[])))
    for s in g.get('skins',[]): print(' skin joints',len(s['joints']), 'name', s.get('name'))
    for a in g.get('animations',[]): print(' anim',a.get('name'),len(a['channels']))
    # scene roots
    sc=g['scenes'][g.get('scene',0)]
    def walk(i,d,maxd):
        n=g['nodes'][i]
        kids=n.get('children',[])
        if d<=maxd: print('  '*d+str(i),n.get('name'),'mesh' if 'mesh' in n else '','skin' if 'skin' in n else '', 'T',[round(x,2) for x in n.get('translation',[])] , 'S',[round(x,3) for x in n.get('scale',[])] if 'scale' in n else '', 'kids',len(kids))
        for k in kids: walk(k,d+1,maxd)
    for r in sc['nodes']: walk(r,0,3)
    for m in g.get('meshes',[])[:8]:
        print(' mesh',m.get('name'),[ (p['attributes'].keys().__len__(), p.get('material')) for p in m['primitives']][:4])
