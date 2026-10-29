import re
p='choreo_core.py'; s=open(p,encoding='utf-8').read()
def rep(a,b):
    global s
    assert a in s, a[:80]; s=s.replace(a,b,1)
rep("'shk': 0, 'lift': 0,","'shk': 0, 'lift': 0, 'root_dy': 0, 'jaw': 0, 'wag': 0,")
rep("g('pel_dy') + g('lift') - REL_KNEE,","g('pel_dy') + g('lift') + g('root_dy') - REL_KNEE,")
rep("world = [g('ft_wx_' + sd), ANKLE_REST_Y + g('ft_lift_' + sd), g('ft_wz_' + sd)]","world = [g('ft_wx_' + sd), ANKLE_REST_Y + g('ft_lift_' + sd) + g('root_dy') * BODY_H, g('ft_wz_' + sd)]")
open(p,'w',encoding='utf-8').write(s)
# ClipBaker scales
p='rig.py'; s=open(p,encoding='utf-8').read()
rep("self.pel = array('f'); self.prev = {}; self.trans = {}; self.n = 0","self.pel = array('f'); self.prev = {}; self.trans = {}; self.scales = {}; self.n = 0")
rep("    def add(self, P, pp, tr):","    def add(self, P, pp, tr, sc=None):")
rep("        self.n += 1\n    def finish","        sc = sc or {}\n        for bone in sc:\n            j = N(bone)\n            if j not in self.scales: self.scales[j] = array('f', [1.0, 1.0, 1.0] * self.n)\n        for j, arr in self.scales.items(): arr.extend(sc.get(node_to_name[j], (1.0, 1.0, 1.0)))\n        self.n += 1\n    def finish")
rep("'rots': self.rots, 'pelvis': self.pel, 'trans': self.trans}","'rots': self.rots, 'pelvis': self.pel, 'trans': self.trans, 'scales': self.scales}")
rep("        anims.append({'name': c['name'], 'samplers': samplers, 'channels': channels})","        for tj, vals in c.get('scales', {}).items():\n            ai = add(struct.pack('<%df' % len(vals), *vals), 'VEC3', n)\n            samplers.append({'input': ti, 'output': ai, 'interpolation': 'LINEAR'}); channels.append({'sampler': len(samplers) - 1, 'target': {'node': tj, 'path': 'scale'}})\n        anims.append({'name': c['name'], 'samplers': samplers, 'channels': channels})")
open(p,'w',encoding='utf-8').write(s)
print('ok')
