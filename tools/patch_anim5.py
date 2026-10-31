s = open('anim_human2.py').read()
assert 'def c_punch2' not in s
new = '''
# ---- THE END fight pass: a punch with a wind-up, a step and a turn of the hips; a two-handed shove with a lunge; a death that starts with a hit reaction (clutch the chest, the other arm thrown back), buckles to the knees and falls
B0 = P(READY, gun=BACK, gl=0.0, gr=0.0)
def c_punch(t):
    G = P(B0, hl=(0.1, 0.3, 0.9, 0.55), hr=(0.1, 0.3, 0.9, 0.55), el=(0.5, -0.9, -0.1), er=(0.5, -0.9, -0.1), lean=0.12, pel=(0, -0.06, 0.0), fl=(FX + 0.02, 0, 0.28), fr=(-FX - 0.02, 0, -0.28), twist=0.1, nod=0.08)
    Wd = P(G, twist=0.65, lean=0.05, pel=(0, -0.08, -0.06), hr=(0.3, -0.9, 0.1, 0.38), er=(0.3, 0.1, -1.0), hl=(0.2, 0.4, 0.9, 0.7), pyaw=0.2)
    S = P(G, twist=-0.6, lean=0.35, pel=(0, -0.1, 0.16), fl=(FX, 0, 0.5), fr=(-FX, 0, -0.38), hr=(0.0, 0.05, 1.0, 1.0), er=(0.5, -0.9, 0.0), hl=(0.35, -0.2, -0.4, 0.55), el=(0.3, 0.4, -1.0), pyaw=-0.25, nod=0.1)
    return kf([(0.0, c_ready(0)), (0.3, G), (0.62, Wd), (0.74, S), (1.3, S), (2.2, G), (3.2, c_ready(0))], t)
def c_shove(t):
    idle = c_ready(0)
    chest = P(B0, hl=(0.3, 0.2, 0.75, 0.45), hr=(0.3, 0.2, 0.75, 0.45), el=(0.8, -0.5, -0.2), er=(0.8, -0.5, -0.2), lean=-0.05, pel=(0, -0.1, -0.06), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.25), nod=0.1)
    push = P(chest, hl=(0.1, 0.1, 1.0, 1.0), hr=(0.1, 0.1, 1.0, 1.0), el=(0.5, -0.7, 0.1), er=(0.5, -0.7, 0.1), lean=0.4, pel=(0, -0.1, 0.2), fl=(FX, 0, 0.55), fr=(-FX, 0, -0.35))
    return kf([(0.0, idle), (0.4, chest), (0.6, push), (1.5, push), (2.6, idle)], t)
def c_death(t):
    impact = P(B0, lean=-0.32, nod=-0.3, pel=(0.02, -0.04, -0.1), twist=0.25, roll=0.1, fl=(FX, 0, 0.3), fr=(-FX, 0, -0.3), hl=(0.3, -0.1, 0.85, 0.5), hr=(0.55, 0.25, -0.6, 0.95), el=(0.7, -0.5, 0.2), er=(0.9, 0.3, -0.2))
    stag = P(B0, lean=-0.1, nod=0.15, pel=(0, -0.28, -0.18), roll=0.1, fl=(FX, 0, 0.2), fr=(-FX, 0.08, -0.4), hl=(0.25, -0.7, 0.6, 0.55), hr=(0.25, -0.6, 0.5, 0.6), el=(0.8, -0.6, 0.0), er=(0.8, -0.6, 0.0))
    kneel = P(B0, lean=0.45, nod=0.4, pel=(0, -0.55, -0.1), fl=(FX, 0.0, 0.3), fr=(-FX, 0.2, -0.25), hl=(0.2, -0.9, 0.3, 0.7), hr=(0.2, -0.9, 0.3, 0.7), el=(0.8, -0.6, -0.2), er=(0.8, -0.6, -0.2))
    dn = P(B0, pel=(0, -0.8, -0.9), ppitch=-1.4, lean=-0.1, fl=(FX, 0.03, -0.06), fr=(-FX + 0.05, 0.03, -0.5), kr=(0.2, 1.0, 0.1), hl=(0.4, -0.9, -0.12, 0.97), hr=(-0.37, -0.88, 0.3, 0.62), el=(0.8, -0.3, -0.5), er=(0.9, -0.4, 0.0), nod=-0.1, turn=0.45, tilt=0.12, roll=0.0)
    return kf([(0.0, c_ready(0)), (0.15, impact), (0.7, stag), (1.3, kneel), (2.3, dn), (12.0, dn)], t)
'''
a = "CLIPS = {'Idle': c_idle,"
assert a in s
s = s.replace(a, new + a, 1)
open('anim_human2.py', 'w').write(s); print('ok')
