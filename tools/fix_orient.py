"""python fix_orient.py in.glb out.glb   the two Sketchfab files of the Doomsday Device model are Z-up with no root rotation (they lie on their side in a Y-up scene): turns the root node by -90 about x."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import slim_glb
g, b = slim_glb.read_glb(sys.argv[1]); r = next(n for n in g['nodes'] if n['name'] == 'Sketchfab_model'); r['rotation'] = [-0.70710678, 0, 0, 0.70710678]
g, b = slim_glb.slim(g, b); slim_glb.write_glb(sys.argv[2], g, b); print(sys.argv[2], os.path.getsize(sys.argv[2]) // 1024, 'KB')
