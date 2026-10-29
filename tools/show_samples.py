"""python show_samples.py out.png view cols t1 t2 ...  -> poses of the baked choreography at those event times, as a contact sheet (needs cache/choreo_scene.pkl)."""
import sys, os, pickle, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from choreo_core import *
out, view, cols = sys.argv[1], sys.argv[2], sys.argv[3]; times = [float(x) for x in sys.argv[4:]]
S = pickle.load(open(os.path.join(HERE, 'cache', 'choreo_scene.pkl'), 'rb'))
frames = [build_pose(S, t).frame_tuple() for t in times]
tmp = r"C:\Users\drago\AppData\Local\Temp\rp\show_samples.glb"
write_preview(tmp, frames, fps=1.0, name='show')
blender = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
r = subprocess.run([blender, '-b', '-P', os.path.join(HERE, 'render_sheet.py'), '--', tmp, out, 'show', view, cols] + [str(i) for i in range(len(times))], capture_output=True, text=True)
print([l for l in r.stdout.splitlines() if 'sheet' in l or 'Error' in l or 'Traceback' in l][:5])
