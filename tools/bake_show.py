"""python bake_show.py out.glb [t0 t1]  -> bakes the whole choreography (body + face + hair/necklace/strap simulation) into ONE 24 fps clip."""
import sys, os, pickle, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from face import *
from hair_sim import HairSim
out = sys.argv[1]; t0 = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0; t1 = float(sys.argv[3]) if len(sys.argv) > 3 else 500.952
S = pickle.load(open(os.path.join(HERE, 'cache', 'choreo_scene.pkl'), 'rb'))
face = Face(S.analysis, S); sim = HairSim(); baker = ClipBaker()
n = int((t1 - t0) * FPS) + 1; st = time.time()
for k in range(n):
    t = t0 + k / FPS
    ps = build_pose(S, t, face(t)); P, pel, tr = ps.frame_tuple()
    sim.apply(P, vadd(PEL_B, pel))
    baker.add(P, pel, tr)
    if k % 480 == 0: print('frame', k, 'of', n, 'elapsed %.0fs' % (time.time() - st), flush=True)
write_glb(out, [baker.finish('Travis_Show', FPS)])
print('done %.0fs' % (time.time() - st)); print('leg IK clamps (unreachable foot targets):', CLAMPS['leg'])
