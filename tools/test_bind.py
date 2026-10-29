import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
fr=[]
p=Pose(); fr.append(p.frame_tuple())
p=Pose(); p.pelvis(dy=-0.0); p.local('spine_01', rx(20)); fr.append(p.frame_tuple())
p=Pose(); p.local('spine_04', rx(35)); fr.append(p.frame_tuple())
p=Pose(); p.local('clavicle_l', rz(30)); p.local('upperarm_l', rz(40)); fr.append(p.frame_tuple())
write_preview(sys.argv[1], fr, fps=1.0, name='t')
