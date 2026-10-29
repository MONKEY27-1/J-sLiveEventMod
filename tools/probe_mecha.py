import os, sys
os.environ.update(RIG_SKIN=r"C:\Users\drago\Downloads\fortnite_mecha_team_leader__fortnite_event_skin.glb", RIG_STRIP='1', RIG_ALIAS='head=head_offset')
sys.path.insert(0,'.')
import rig
from rig import *
print('BODY_H default', BODY_H, 'pelvis', [round(x,2) for x in PEL_B], 'head', [round(x,2) for x in BP[N('head')]])
for n in ('spine_05','neck_01','thigh_l','calf_l','foot_l','ball_l','upperarm_l','lowerarm_l','hand_l','clavicle_l','jaw_01'):
    print(n, n in s_name)
print([n for n in s_name if any(k in n for k in ('weapon','socket','head','eye','pod','missile','jet','wing','gun','cannon'))])
xs=[];ys=[];zs=[]
for j in ssk['joints']:
    G=s_G0[j]; xs.append(-G[0][3]); ys.append(G[1][3]); zs.append(G[2][3])
print('jb y [%.2f,%.2f] x [%.2f,%.2f] z[%.2f,%.2f]'%(min(ys),max(ys),min(xs),max(xs),min(zs),max(zs)))
print('rest rot of root chain', [sg['nodes'][s_par[PEL]].get('rotation')])
rows=sorted(((node_to_name[j],round(-s_G0[j][0][3]),round(s_G0[j][1][3]),round(s_G0[j][2][3])) for j in ssk['joints']), key=lambda r:-abs(r[1]))
print(rows[:6]); print('head',[round(x) for x in BP[N('head')]],'pelvis',[round(x) for x in PEL_B],'foot',[round(x) for x in BP[N('foot_l')]])
G=s_G0[PEL]; print('G0 pelvis pos',[round(G[r][3],1) for r in range(3)],'PEL_B',[round(x,1) for x in PEL_B],'s_f',s_f)
print('mesh node scale', sg['nodes'][s_mesh].get('scale'), 'G0 mesh scale', _col_scale(s_G0[s_mesh]) if '_col_scale' in dir() else None)
