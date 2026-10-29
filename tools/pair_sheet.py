"""python pair_sheet.py <out.png> <ortho_m> <az> t1 t2 ...  (keywords tear / punch / stab / blast / ambush also work): both giants in the show frame, tile centred between the monster's chest and the robot's chest"""
import json, os, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, 'cache', 'showdown'); sys.path.insert(0, HERE)
import monster_events as ME
out, scale, az = sys.argv[1], sys.argv[2], sys.argv[3]; items = sys.argv[4:]
KW = {'tear': 162.2, 'punch': 245.6, 'stab': 260.0, 'ambush': 160.8, 'kick': 181.2}
rt = json.load(open(os.path.join(OUT, 'robot_trace.json'))); RB = rt['b']
def rob(bone, t):
    i = max(0.0, min(len(RB[bone]) - 1.001, t / rt['dt'])); i0 = int(i); f = i - i0; a = RB[bone][i0]; b = RB[bone][i0 + 1]; return [a[k] + (b[k] - a[k]) * f for k in range(3)]
specs = []
for it in items:
    t = KW[it] if it in KW else float(it)
    m = ME.at('spine_03', t); r = rob('spine_03', t); f = [(m[k] + r[k]) / 2 for k in range(3)]
    specs.append('%.2f:%.1f:%.1f:%.1f:%s' % (t, f[0], f[1], f[2], az))
blender = r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
cmd = [blender, '-b', '-P', os.path.join(HERE, 'render_pair.py'), '--', os.path.join(OUT, 'monster_slim.glb'), os.path.join(OUT, 'robot.glb'), out, '4', scale] + specs
r = subprocess.run(cmd, capture_output=True, text=True); print('\n'.join(l for l in r.stdout.splitlines() if 'fit' in l or 'sheet' in l)); print(r.stderr[-600:])
