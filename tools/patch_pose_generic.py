p = r'C:\Users\drago\Documents\AstronomicalEventMod\tools\pose.py'
s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert a in s, a[:70]
    s = s.replace(a, b, 1)
rep("""        for i, w in enumerate(self.SPINE_W):
            self.local('spine_0%d' % (i + 1), m3mul(m3mul(rx(bend * w), ry(twist * w)), rz(lean * w)))""",
"""        bones = [('spine_0%d' % (i + 1), w) for i, w in enumerate(self.SPINE_W) if 'spine_0%d' % (i + 1) in s_name]      # other rigs have fewer spine bones
        tot = sum(w for _, w in bones)
        for nm, w in bones:
            w /= tot; self.local(nm, m3mul(m3mul(rx(bend * w), ry(twist * w)), rz(lean * w)))""")
rep("""        for nm, w in (('neck_01', 0.25), ('neck_02', 0.25), ('head', 0.5)):
            self.local(nm, m3mul(m3mul(rx(pitch * w), ry(yaw * w)), rz(roll * w)))""",
"""        necks = [n for n in ('neck_01', 'neck_02', 'neck_03') if n in s_name]
        for nm, w in [(n, 0.5 / len(necks)) for n in necks] + [('head', 0.5)]:
            self.local(nm, m3mul(m3mul(rx(pitch * w), ry(yaw * w)), rz(roll * w)))""")
open(p, 'w', encoding='utf-8').write(s)
p = r'C:\Users\drago\Documents\AstronomicalEventMod\tools\render_sheet.py'
s = open(p, encoding='utf-8').read()
rep("hb = [b for b in arm.pose.bones if b.name == 'head' or (b.name.startswith('head') and b.name[4:].lstrip('_').replace('_', '').isdigit())][0]",
    "hb = [b for b in arm.pose.bones if b.name == 'head' or (b.name.startswith('head') and 'end' not in b.name and 'offset_end' not in b.name)][0]")
rep("sc_, loc, rot = cfg[VIEW]", "K = float(os.environ.get('RS_K', '1'))\nsc_, loc, rot = cfg[VIEW]\nsc_ *= K; loc = tuple(x * K for x in loc)")
rep("cam.data.type = 'ORTHO'; cam.data.clip_end = 1e6", "cam.data.type = 'ORTHO'; cam.data.clip_end = 1e6")
rep("    if VIEW in ('face', 'upper'): cam.location.z = loc[2] + (hp.z - 14900)", "    if VIEW in ('face', 'upper'): cam.location.z = loc[2] + (hp.z - 14900 * K)")
open(p, 'w', encoding='utf-8').write(s)
print('ok')
