import json,struct,sys
p=r"C:\Users\drago\Downloads\fortnite_mecha_team_leader__fortnite_event_skin.glb"
d=open(p,'rb').read(); l,t=struct.unpack_from('<II',d,12); g=json.loads(d[20:20+l])
print('nodes',len(g['nodes']),'skins',len(g.get('skins',[])),'meshes',len(g['meshes']),'anims',len(g.get('animations',[])),'mats',len(g.get('materials',[])),'images',len(g.get('images',[])))
for s in g.get('skins',[]): print('skin joints',len(s['joints']))
names=[n.get('name') for n in g['nodes']]
sk=g['skins'][0]['joints']
print([names[j] for j in sk][:80])
for a in g.get('animations',[]): print('anim',a.get('name'),len(a['channels']))
print([ (n.get('name'),n.get('scale'),n.get('rotation')) for n in g['nodes'] if not n.get('name','').startswith('Object') and n.get('children') and n['name'] in names[:6]])
print(json.dumps(g['materials'][0])[:300]); print(g.get('extensionsUsed'))
