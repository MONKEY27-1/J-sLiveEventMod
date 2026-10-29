import json, struct, sys
p = sys.argv[1]; d = open(p, 'rb').read()
jl = struct.unpack('<I', d[12:16])[0]; g = json.loads(d[20:20 + jl]); bin_off = 20 + jl
rest = d[bin_off:]                                   # BIN chunk header + data
n = 0
for m in g['materials']:
    if 'emissiveFactor' in m: m.pop('emissiveFactor'); n += 1
    m.pop('emissiveTexture', None)
js = json.dumps(g, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + len(rest)) + struct.pack('<II', len(js), 0x4E4F534A) + js + rest
open(p, 'wb').write(out); print('fixed', n, 'materials in', p)
