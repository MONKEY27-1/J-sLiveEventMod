import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig
names = sorted(rig.s_name)
print("FACE:", [n for n in names if n.startswith(('C_','L_','R_')) or 'teeth' in n or 'tongue' in n or 'eye' in n.lower() or 'jaw' in n.lower()])
print("DYN:", [n for n in names if n.startswith('dyn_')])
print("OTHER:", [n for n in names if not n.startswith(('C_','L_','R_','dyn_','index','middle','ring','pinky','thumb')) ])

