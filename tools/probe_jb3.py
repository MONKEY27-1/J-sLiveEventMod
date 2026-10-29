import sys; sys.path.insert(0,'.')
import real_monster as M
from rig import *
rows=[(node_to_name[j],round(-s_G0[j][0][3],2),round(s_G0[j][1][3],2),round(s_G0[j][2][3],2)) for j in ssk['joints']]
rows.sort(key=lambda r:r[2]); print(rows[:8]); print(rows[-4:])
