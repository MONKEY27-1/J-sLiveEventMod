import sys; sys.path.insert(0,'.')
import real_monster as M
from rig import *
def show(j,d=0,maxd=6):
    n=node_to_name[j]; ch=[c for c in s_joint_list if s_par[c]==j]
    print('  '*d+n, ('[rot]' if j in M.rc._acts['Cattus_Act2']['rot'] else ''), [round(x,2) for x in BP[j]] if j in BP else '')
    if d<maxd:
        for c in ch:
            nm=node_to_name[c]
            if 'index' in nm or 'middle' in nm or 'ring' in nm or 'pinky' in nm or 'thumb' in nm: continue
            show(c,d+1,maxd)
show(N('clavicle_l'),0,5); show(N('thigh_l'),0,6)
