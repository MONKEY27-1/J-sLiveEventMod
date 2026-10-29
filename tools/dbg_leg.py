import sys,os; sys.path.insert(0,'.'); os.environ['ACTOR']='robot'
from gcore import *
for n in ('pelvis','thigh_l','calf_l','foot_l'): print(n,[round(x,1) for x in BP[N(n)]])
print('REL_KNEE',REL_KNEE,'ANKLE_REST_Y',ANKLE_REST_Y,'BODY_H',BODY_H, 'thigh len',vlen(vsub(BP[N('calf_l')],BP[N('thigh_l')])),'calf len',vlen(vsub(BP[N('foot_l')],BP[N('calf_l')])))
