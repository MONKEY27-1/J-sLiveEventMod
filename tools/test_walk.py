import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gcore import *
rng = random.Random(3); S = new_scene(3)
set_root(S, 0, 40, 160, 200)
plant_all(S, 0)
end = walk(S, rng, 0, 24, [(0, 40, 160), (24, 15, 110)], step=(2.6, 3.6))
plant_all(S, 26)
bake(S, 0, 26, sys.argv[1], name='t', sim=(len(sys.argv) < 3))
