# exec'd by zc_anim.py inside the module (P, add, kf, lerp_pose, ramp, sn, cs, gait, gait_free, sway, FX, IDLE, READY, B0 ... exist). Extra clips for ZERO CRISIS (every clip is a pose function of time, 'H_' prefix on output).
LOOK0 = P(IDLE, gl=0.0, gr=0.0)
def c_hang(t):   # hangs by both hands from a bar overhead, legs dangling and swinging a little
    s = sn(t, 0.35); return P(IDLE, hl=(0.1, 1.0, 0.12, 1.0), hr=(0.1, 1.0, 0.12, 1.0), el=(0.4, 0.2, -0.3), er=(0.4, 0.2, -0.3), pel=(0, 0.02 * sn(t, 0.7), 0.0), lean=0.05 * s, twist=0.06 * sn(t, 0.2), nod=-0.1,
             fl=(FX * 0.8, 0.08 + 0.05 * s, 0.1 + 0.18 * s), fr=(-FX * 0.8, 0.06 - 0.05 * s, 0.05 - 0.18 * s), flp=0.4, frp=0.4)
def c_device(t):   # holds the Portal Device out in front of him at chest height
    return P(IDLE, hr=(0.05, 0.25 + 0.02 * sn(t, 0.6), 1.0, 0.82), er=(0.6, -0.7, -0.2), hl=(0.25, -1.0, 0.1, 0.96), lean=0.05, nod=0.12, turn=-0.1, twist=0.12, fl=(FX + 0.03, 0, 0.1), fr=(-FX - 0.02, 0, -0.1))
def c_deviceup(t):   # raises the device overhead and holds it there
    k = ramp(t, 0.2, 1.0); up = P(IDLE, hr=(0.05, 1.0, 0.45, 1.0), er=(0.5, 0.2, -0.4), hl=(0.25, -1.0, 0.1, 0.96), lean=-0.1, nod=-0.3, fl=(FX + 0.04, 0, 0.1), fr=(-FX - 0.02, 0, -0.1)); return lerp_pose(c_device(0), up, k)
def c_fight(t):   # a fighting stance: wide, knees bent, both fists up, bouncing
    b = 0.5 + 0.5 * sn(t, 1.6); return P(IDLE, hl=(0.1, 0.35, 0.9, 0.5), hr=(0.1, 0.4, 0.95, 0.55), el=(0.5, -0.9, -0.1), er=(0.5, -0.9, -0.1), pel=(0, -0.13 - 0.03 * b, 0.0), lean=0.12, twist=0.2, nod=0.1,
             fl=(FX + 0.06, 0.0, 0.3), fr=(-FX - 0.06, 0.0, -0.28), pyaw=0.15, kl=(0.2, 0.0, 1.0), kr=(0.2, 0.0, 1.0))
def c_block(t):   # arms crossed in front of the face
    return P(IDLE, hl=(-0.25, 0.45, 0.9, 0.5), hr=(-0.25, 0.45, 0.9, 0.5), el=(0.4, 0.5, -0.2), er=(0.4, 0.5, -0.2), pel=(0, -0.1, -0.03), lean=0.18, nod=0.15, fl=(FX + 0.05, 0, 0.2), fr=(-FX - 0.05, 0, -0.2))
def c_kick(t):   # wind up, a high front kick, recover
    G = c_fight(0); wind = P(G, pel=(0, -0.1, -0.1), lean=-0.1, fr=(-FX, 0.1, -0.35), frp=0.0); hit = P(G, pel=(0, -0.05, -0.1), lean=-0.25, ppitch=-0.1, fr=(-FX * 0.4, 0.78, 0.62), frp=-0.3, hl=(0.5, 0.4, 0.6, 0.6), hr=(0.5, 0.1, 0.6, 0.6))
    return kf([(0.0, G), (0.3, wind), (0.5, hit), (0.85, hit), (1.4, G), (2.2, G)], t)
def c_roar(t):   # head thrown back, arms flung out and up
    r = P(IDLE, hl=(0.8, 0.6, 0.2, 1.0), hr=(0.8, 0.6, 0.2, 1.0), el=(0.8, 0.0, -0.1), er=(0.8, 0.0, -0.1), lean=-0.35, nod=-0.6, pel=(0, -0.05, -0.05), fl=(FX + 0.12, 0, 0.1), fr=(-FX - 0.12, 0, -0.1))
    s = sn(t, 3.0) * 0.04; return kf([(0.0, c_fight(0)), (0.5, add(r, lean=0.3, nod=0.2)), (0.9, r), (3.0, add(r, nod=-0.1, tilt=s)), (4.5, c_fight(0))], t)
def c_sprint(t):
    p = gait_free(t, 0.6, 4.0, 0.38, 0.22, 0.06, 0.3, 1.5, True); p['nod'] = 0.12; return p
def c_lunge(t):   # a lunging attack: step in with a straight punch
    G = c_fight(0); hit = P(G, pel=(0, -0.28, 0.35), fl=(FX, 0, 0.75), fr=(-FX, 0.0, -0.3), lean=0.45, twist=-0.5, hr=(0.0, 0.05, 1.0, 1.0), er=(0.5, -0.9, 0.0), hl=(0.4, -0.2, -0.3, 0.5))
    return kf([(0.0, G), (0.35, add(G, twist=0.6, pel=(0, -0.05, -0.1))), (0.55, hit), (1.1, hit), (1.9, G), (3.0, G)], t)
def c_reach(t):   # both arms stretched out towards something big (the Zero Point), trembling
    tr = 0.02 * sn(t, 6.0); return P(IDLE, hl=(0.12 + tr, 0.55, 0.85, 1.0), hr=(0.12 - tr, 0.55, 0.85, 1.0), el=(0.6, -0.2, -0.2), er=(0.6, -0.2, -0.2), lean=-0.08, nod=-0.25, fl=(FX + 0.05, 0, 0.1), fr=(-FX - 0.05, 0, -0.12), pel=(0, -0.02, 0.0))
def c_channel(t):   # arms spread wide in a V, chest out, head back: pouring energy
    p = 0.5 + 0.5 * sn(t, 0.8); return P(IDLE, hl=(0.75, 0.65 + 0.1 * p, 0.12, 1.0), hr=(0.75, 0.65 + 0.1 * p, 0.12, 1.0), el=(0.8, 0.2, -0.1), er=(0.8, 0.2, -0.1), lean=-0.2 - 0.05 * p, nod=-0.4, pel=(0, 0.02 + 0.02 * p, 0.0), fl=(FX + 0.1, 0, 0.05), fr=(-FX - 0.1, 0, -0.05))
def c_lookup(t):   # arms by the sides, head tipped right back: staring up at the sky
    k = ramp(t, 0.3, 1.4); return lerp_pose(IDLE, P(IDLE, nod=-0.65, lean=-0.14, turn=0.05 * sn(t, 0.2), hl=(0.2, -1.0, 0.0, 0.96), hr=(0.2, -1.0, 0.0, 0.96), pel=(0, -0.012, -0.03), fl=(FX + 0.04, 0, 0.0), fr=(-FX - 0.04, 0, -0.1)), k)
def c_knockdown(t):   # struck: staggers back then falls flat
    ko = P(IDLE, lean=-0.4, nod=-0.3, pel=(0, -0.06, -0.15), fl=(FX, 0, 0.3), fr=(-FX, 0, -0.35), hl=(0.8, 0.4, 0.3, 0.9), hr=(0.8, 0.4, 0.3, 0.9), el=(0.8, 0, 0), er=(0.8, 0, 0))
    dn = P(IDLE, pel=(0, -0.8, -0.95), ppitch=-1.45, lean=-0.1, fl=(FX, 0.03, -0.06), fr=(-FX + 0.05, 0.03, -0.5), kr=(0.2, 1.0, 0.1), hl=(0.7, -0.4, -0.2, 0.9), hr=(0.7, -0.4, -0.2, 0.9), el=(0.8, -0.3, -0.5), er=(0.9, -0.4, 0.0), nod=-0.1)
    return kf([(0.0, IDLE), (0.18, ko), (0.9, dn), (8.0, dn)], t)
def c_getup(t):   # from lying on the back: roll up, one knee, stand
    dn = c_knockdown(8.0); knee = P(IDLE, pel=(0, -0.55, 0.12), lean=0.5, fl=(FX, 0, 0.3), fr=(-FX, -0.28, -0.1), frp=0.9, hl=(0.3, -0.7, 0.6, 0.8), hr=(0.2, -0.9, 0.5, 0.8), nod=0.2)
    return kf([(0.0, dn), (0.9, add(dn, ppitch=0.55, pel=(0, 0.25, 0.3), lean=0.4, nod=0.2)), (1.7, knee), (2.6, P(IDLE, pel=(0, -0.2, 0.05), lean=0.25)), (3.4, IDLE)], t)
def c_wave(t):   # a friendly wave
    w = sn(t, 1.6); return P(IDLE, hr=(0.35 + 0.1 * w, 0.8, 0.3, 1.0), er=(0.9, 0.0, -0.3), lean=-0.04, turn=0.1, nod=0.0, fl=(FX + 0.03, 0, 0.0), fr=(-FX - 0.02, 0, -0.05))
def c_victory(t):   # both fists up, bouncing on the spot
    b = abs(sn(t, 1.2)); return P(IDLE, hl=(0.5, 0.95, 0.1, 1.0), hr=(0.5, 0.95, 0.1, 1.0), el=(0.8, 0.0, -0.2), er=(0.8, 0.0, -0.2), pel=(0, 0.0 + 0.08 * b, 0), lean=-0.1, nod=-0.3, fl=(FX + 0.05, 0.1 * b, 0), fr=(-FX - 0.05, 0.1 * b, 0))
def c_float(t):   # lifted off the ground: arms drift out, legs hang, slow turn
    s = sn(t, 0.25); return P(IDLE, hl=(0.55, -0.5 + 0.2 * s, 0.1, 1.0), hr=(0.55, -0.5 - 0.2 * s, 0.1, 1.0), el=(0.8, -0.2, -0.2), er=(0.8, -0.2, -0.2), lean=-0.05 + 0.04 * s, nod=-0.1, fl=(FX * 0.7, 0.1 + 0.05 * s, 0.05), fr=(-FX * 0.7, 0.08 - 0.05 * s, -0.05), flp=0.5, frp=0.5, turn=0.2 * sn(t, 0.12))
def c_think(t):   # one hand to the chin
    return P(IDLE, hr=(0.0, 0.5, 0.55, 0.62), er=(0.8, -0.6, 0.0), hl=(0.3, -0.5, 0.5, 0.8), el=(0.8, -0.6, -0.2), nod=0.2, tilt=0.12, lean=0.04)
def c_flex(t):   # a bicep pose
    return P(IDLE, hl=(0.95, 0.45, 0.0, 0.85), hr=(0.95, 0.45, 0.0, 0.85), el=(0.9, 0.9, 0.0), er=(0.9, 0.9, 0.0), pel=(0, -0.03, 0), nod=-0.15, lean=-0.05, fl=(FX + 0.12, 0, 0.0), fr=(-FX - 0.12, 0, 0.0))
def c_dance(t):   # a silly little dance: arms alternate, hips swing
    a = sn(t, 1.0); return P(IDLE, hl=(0.6, 0.5 + 0.4 * a, 0.4, 1.0), hr=(0.6, 0.5 - 0.4 * a, 0.4, 1.0), el=(0.9, 0.0, -0.1), er=(0.9, 0.0, -0.1), pel=(0.1 * a, -0.04 + 0.03 * abs(sn(t, 2.0)), 0.0), proll=0.1 * a, twist=0.25 * a, nod=0.1 * sn(t, 2.0), fl=(FX + 0.06, 0.06 * max(0, a), 0), fr=(-FX - 0.06, 0.06 * max(0, -a), 0))
def c_taunt(t):   # come on, then: one hand beckoning, weight on the back foot
    w = sn(t, 1.5); return P(IDLE, hr=(0.2, 0.15 + 0.2 * w, 0.95, 0.9), er=(0.6, -0.8, -0.1), hl=(0.2, -1.0, 0.1, 0.96), lean=-0.08, turn=0.0, pel=(0, -0.05, -0.04), fl=(FX + 0.04, 0, 0.3), fr=(-FX - 0.04, 0, -0.1))
def c_pray(t):   # kneeling, hands together, head down
    return P(IDLE, pel=(0, -0.62, 0.0), fl=(FX * 0.6, 0.0, 0.3), fr=(-FX * 0.6, -0.3, -0.1), frp=0.9, lean=0.25, nod=0.4, hl=(0.1, 0.1, 0.8, 0.5), hr=(0.1, 0.1, 0.8, 0.5), el=(0.5, -0.8, -0.2), er=(0.5, -0.8, -0.2))
def c_drag(t):   # leans into a pull: both hands forward, bracing
    return P(IDLE, hl=(0.2, 0.25, 1.0, 0.9), hr=(0.2, 0.25, 1.0, 0.9), el=(0.5, -0.8, -0.1), er=(0.5, -0.8, -0.1), lean=0.4, pel=(0, -0.12, 0.12), fl=(FX, 0, 0.45), fr=(-FX, 0, -0.4), nod=0.1)
def c_hover(t):   # floating upright: arms a little out, legs together, a slow bob
    b = sn(t, 0.3); return P(IDLE, hl=(0.45, -0.85, 0.05, 1.0), hr=(0.45, -0.85, 0.05, 1.0), el=(0.8, -0.3, -0.3), er=(0.8, -0.3, -0.3), pel=(0, 0.02 * b, 0), fl=(FX * 0.45, 0.03, 0.0), fr=(-FX * 0.45, 0.0, 0.0), nod=0.05, lean=0.03 * b)
def c_flypose(t):   # the body is tipped forward by the plugin: arms straight back along it, legs together and straight
    return P(IDLE, hl=(0.12, -1.0, -0.35, 1.0), hr=(0.12, -1.0, -0.35, 1.0), el=(0.5, 0.0, -1.0), er=(0.5, 0.0, -1.0), fl=(FX * 0.35, 0.0, -0.05), fr=(-FX * 0.35, 0.0, -0.05), nod=-0.25, lean=-0.05)
def c_repulsor(t):   # one palm thrust forward (firing), the other arm back
    k = 0.5 + 0.5 * sn(t, 2.0); return P(IDLE, hr=(0.05, 0.1, 1.0, 1.0), er=(0.5, -0.9, 0.0), hl=(0.35, -0.8, -0.4, 1.0), el=(0.7, -0.3, -0.5), lean=0.1 + 0.03 * k, twist=-0.15, fl=(FX + 0.04, 0, 0.1), fr=(-FX - 0.02, 0, -0.18))
CLIPS.update({'Hover': c_hover, 'FlyPose': c_flypose, 'Repulsor': c_repulsor, 'Hang': c_hang, 'Device': c_device, 'DeviceUp': c_deviceup, 'Fight': c_fight, 'Block': c_block, 'Kick': c_kick, 'Roar': c_roar, 'Sprint': c_sprint, 'Lunge': c_lunge, 'Reach': c_reach, 'Channel': c_channel, 'LookUp': c_lookup,
              'KnockDown': c_knockdown, 'GetUp': c_getup, 'Wave': c_wave, 'Victory': c_victory, 'Float': c_float, 'Think': c_think, 'Flex': c_flex, 'Dance': c_dance, 'Taunt': c_taunt, 'Pray': c_pray, 'Drag': c_drag})
LOCO.add('Sprint')
