s = open('anim_human2.py').read()
assert 'def polish(' not in s
new = '''
# ---- THE END polish: every clip gets a "life layer" (breathing, weight shift, an irregular head / hand drift) and the emotional clips get real motion instead of one held pose
def noise1(t, seed): return (math.sin(2 * math.pi * (t * 0.37 + seed)) + 0.6 * math.sin(2 * math.pi * (t * 0.91 + seed * 2.3)) + 0.35 * math.sin(2 * math.pi * (t * 1.73 + seed * 4.1))) / 1.95
LOCO = {'Walk', 'Run', 'WalkS', 'Climb', 'Sneak', 'RunAim', 'Jog', 'JogAim'}
def polish(p, name, t):
    p = copy.deepcopy(p); loco = name in LOCO; f = 0.5 if loco else 1.0
    p['lean'] += f * (0.009 * sn(t, 0.27) + 0.012 * noise1(t, 0.1)); p['nod'] += f * 0.03 * noise1(t, 0.4); p['turn'] += f * 0.08 * noise1(t, 0.7); p['tilt'] += f * 0.025 * noise1(t, 0.2)
    if not loco:
        o = p['pel']; p['pel'] = (o[0] + 0.014 * noise1(t, 0.5), o[1] + 0.004 * sn(t, 0.27), o[2] + 0.006 * noise1(t, 0.9)); p['proll'] += 0.02 * noise1(t, 0.5); p['roll'] += 0.012 * noise1(t, 0.8)
    for k, sd in (('hl', 1.3), ('hr', 2.9)):
        v = p[k]; p[k] = (v[0] + 0.035 * noise1(t, sd), v[1] + 0.035 * noise1(t, sd + 0.5), v[2] + 0.02 * noise1(t, sd + 0.9), v[3])
    return p
def sback(x): x = max(0.0, min(1.0, x)); c1 = 0.9; return 1 + (c1 + 1) * (x - 1) ** 3 + c1 * (x - 1) ** 2
def c_panic(t):
    a = sn(t, 2.2); fl_ = max(0.0, sn(t, 0.37)); tr = 0.06 * sn(t, 5.3)
    return P(IDLE, hl=(0.5 + 0.2 * a + tr, 0.7 + 0.15 * sn(t, 1.7), 0.45 + 0.12 * sn(t, 2.3), 0.9), hr=(0.5 - 0.2 * a - tr, 0.7 + 0.15 * sn(t, 1.9, 0.3), 0.45 + 0.12 * sn(t, 2.1), 0.9), el=(1.0, 0.1 * sn(t, 1.1), -0.3), er=(1.0, 0.1 * sn(t, 1.3), -0.3),
             lean=-0.2 - 0.12 * fl_ + 0.05 * sn(t, 1.4), nod=-0.25 - 0.1 * fl_, turn=0.6 * sn(t, 0.9) + 0.2 * sn(t, 2.7), pel=(0.03 * a, -0.08 - 0.03 * fl_, -0.06), fl=(FX + 0.08, 0.02 * max(0, sn(t, 1.3)), 0.12 * a), fr=(-FX - 0.08, 0.02 * max(0, -sn(t, 1.3)), -0.12 * a))
def c_plead(t):
    k = ramp(t, 0.3, 1.5); w = sn(t, 1.4); cw = cs(t, 1.4)
    p = P(IDLE, hl=(0.3 + 0.05 * cw, -0.3 + 0.05 * w, 0.9, 0.9), hr=(0.3 - 0.05 * cw, -0.3 - 0.05 * w, 0.9, 0.9), el=(0.9, -0.5, 0.0), er=(0.9, -0.5, 0.0), lean=0.1 + 0.06 * sn(t, 0.5), nod=0.15 + 0.06 * sn(t, 0.5), fl=(FX, 0, 0.28), fr=(-FX, 0, -0.1), pel=(0, -0.03 - 0.02 * sn(t, 0.5), 0.06), turn=0.12 * sn(t, 0.23), tilt=0.1 + 0.05 * sn(t, 0.3))
    return lerp_pose(IDLE, p, k)
def c_stalk(t):
    a = sn(t, 0.5); acc = max(0.0, sn(t, 0.83)); return P(IDLE, hl=(0.2, -0.9, 0.1, 0.95), hr=(0.25 + 0.2 * a + 0.2 * acc, 0.2 + 0.3 * sn(t, 0.7) + 0.2 * acc, 0.85, 0.95), er=(0.8, -0.4 + 0.2 * acc, -0.2), turn=0.25 * sn(t, 0.2) + 0.1 * acc, nod=0.06 * sn(t, 0.6) - 0.1 * acc, lean=0.06 + 0.04 * acc, tilt=0.07 * sn(t, 0.25), pel=(0.02 * sn(t, 0.12), -0.012, 0), fl=(FX + 0.05, 0, 0.05), fr=(-FX - 0.02, 0, -0.1))
def c_talk(t):
    a = sn(t, 0.43); c = sn(t, 0.31, 0.3); acc = max(0.0, sn(t, 0.71)); return P(IDLE, hl=(0.5 + 0.3 * a, -0.15 + 0.4 * c + 0.2 * acc, 0.65, 0.78), hr=(0.5 - 0.3 * c, -0.15 + 0.4 * a + 0.2 * acc, 0.7, 0.78), el=(0.8, -0.5, -0.1), er=(0.8, -0.5, -0.1), turn=0.3 * sn(t, 0.17), nod=0.08 * sn(t, 0.5) - 0.06 * acc, lean=0.03 + 0.03 * acc, pel=(0.015 * sn(t, 0.12), -0.012, 0), fl=(FX + 0.03, 0, 0.05), fr=(-FX - 0.01, 0, -0.06))
'''
s = s.replace("CLIPS = {'Idle': c_idle,", new + "CLIPS = {'Idle': c_idle,", 1)
s = s.replace("t = f / FPS; Wg = solve(fn(t))", "t = f / FPS; Wg = solve(fn(t) if _cs else polish(fn(t), name, t))", 1)
open('anim_human2.py', 'w').write(s); print('ok')
