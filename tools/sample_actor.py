"""ACTOR=monster|robot python sample_actor.py <script module> out.png view cols t1 t2 ...  -> contact sheet of the scripted poses at those times"""
import os, sys, subprocess, importlib
mod = sys.argv[1]; out, view, cols = sys.argv[2:5]; times = [float(x) for x in sys.argv[5:]]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
m = importlib.import_module(mod)
from gcore import *
frames = []
for t in times:
    ps = build_pose(m.S, t)
    if m.S.get('jaw', t) > 0.01 and 'jaw_01' in s_name: ps.local('jaw_01', rx(m.S.get('jaw', t)))
    P = fix_reverse(ps.P) if ACTOR == 'monster' else ps.P
    frames.append((P, ps.frame_tuple()[1], ps.tr))
tmp = r"C:\Users\drago\AppData\Local\Temp\rp\sample_%s.glb" % ACTOR
write_preview(tmp, frames, fps=1.0, name='s')
env = dict(os.environ); env['RS_K'] = str(1.0 / 150.0 if ACTOR == 'monster' else 1.0 / 200.0)
blender = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
r = subprocess.run([blender, '-b', '-P', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'render_sheet.py'), '--', tmp, out, 's', view, cols] + [str(i) for i in range(len(times))], capture_output=True, text=True, env=env)
print([l for l in r.stdout.splitlines() if 'sheet' in l or 'Error' in l or 'Traceback' in l][:4])
