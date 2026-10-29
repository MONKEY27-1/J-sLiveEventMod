import sys; sys.path.insert(0,'.')
import real_monster as M
from rig import *
rows=[]
for j in ssk['joints']:
    G=s_G0[j]; rows.append((node_to_name[j],round(-G[0][3],2),round(G[1][3],2),round(G[2][3],2)))
rows.sort(key=lambda r:-(abs(r[1])+abs(r[3])+abs(r[2])))
for r in rows[:14]: print(r)
print('bind of those:', {r[0]:[round(x,2) for x in BP[N(r[0])]] for r in rows[:8]})
print('parents', {r[0]: node_to_name.get(s_par[N(r[0])]) for r in rows[:8]})
print('rest T', {r[0]: [round(x,3) for x in sg['nodes'][N(r[0])].get('translation',[0,0,0])] for r in rows[:8]})
