s = open('anim_human2.py').read()
def rep(a, b, c=1):
    global s
    assert a in s, a[:70]
    s = s.replace(a, b, c)
new = '''
# ---- THE END part 2 additions: a reclined captive (the scanner chair), a crouched sneak-walk with the rifle low, a held pose for pushing / pinning, and a two-handed shove
def c_recline(t):
    return P(IDLE, gun=BACK, pel=(0, -0.46, -0.24), fl=(FX, 0, 0.5), fr=(-FX, 0, 0.5), lean=-0.38 + 0.012 * sn(t, 0.25), nod=-0.12 + 0.04 * sn(t, 0.18), turn=0.14 * sn(t, 0.07), tilt=0.05 * sn(t, 0.11),
             hl=(0.55, -0.55, 0.45, 0.85), hr=(0.55, -0.55, 0.45, 0.85), el=(0.8, -0.2, 0.3), er=(0.8, -0.2, 0.3))
def c_sneak(t):
    p = gait(t, 1.35, 1.0, 0.66, 0.08, 0.018, 0.34, 0.0, LOW); p['pel'] = (p['pel'][0], p['pel'][1] - 0.30, p['pel'][2] + 0.03); p['nod'] = 0.1 + 0.03 * sn(t, 0.74); p['turn'] = 0.25 * sn(t, 0.37); return p
def c_pin(t):   # holds someone against a wall: both arms forward and up, weight leaning in
    return P(IDLE, gun=BACK, hl=(0.3, 0.55, 0.6, 1.0), hr=(0.3, 0.55, 0.6, 1.0), el=(0.7, 0.2, 0.0), er=(0.7, 0.2, 0.0), lean=0.3, pel=(0, -0.06, 0.1), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.3), nod=0.1)
def c_run_aim(t):   # running with the rifle up at the shoulder (the Seven's rescue, the underground evacuation)
    p = gait(t, 0.75, 3.0, 0.40, 0.16, 0.05, 0.22, 0.0, HIGH, True); return p
'''
rep("CLIPS = {'Idle': c_idle,", new + "CLIPS = {'Idle': c_idle,")
rep("'Sit': c_sit,", "'Recline': c_recline, 'Sneak': c_sneak, 'Pin': c_pin, 'RunAim': c_run_aim, 'Sit': c_sit,")
open('anim_human2.py', 'w').write(s)
print('ok')
