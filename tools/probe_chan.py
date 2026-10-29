import sys; sys.path.insert(0,'.')
import real_clip as rc
from rig import *
import collections
for a in sg['animations']:
    c=collections.Counter(); tn=[]
    for ch in a['channels']:
        c[ch['target']['path']]+=1
        if ch['target']['path']=='translation': tn.append(node_to_name.get(ch['target']['node'],ch['target']['node']))
        if ch['target']['path']=='scale': tn.append('S:'+str(node_to_name.get(ch['target']['node'],ch['target']['node'])))
    print(a['name'],dict(c),tn[:12])
print('joints without rot channel in Act2:',[node_to_name[j] for j in s_joint_list if j not in rc._acts['Cattus_Act2']['rot']][:40])
print('static joints',[node_to_name[j] for j in static_joints])
print('PEL parent chain',[node_to_name.get(p) for p in (s_par[PEL],s_par[s_par[PEL]])])
