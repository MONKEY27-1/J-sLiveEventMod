import re, sys

TOK = re.compile(r'"((?:[^"\\]|\\.)*)"|(\*\d+)|([A-Za-z_][A-Za-z0-9_]*)|(-?\d+\.?\d*(?:[eE][-+]?\d+)?)|([{}:,])|;[^\n]*')


class Node:
    def __init__(s, name):
        s.name = name; s.vals = []; s.kids = []

    def get(s, name):
        for k in s.kids:
            if k.name == name:
                return k

    def all(s, name):
        return [k for k in s.kids if k.name == name]


def tokenize(text):
    out = []
    for m in TOK.finditer(text):
        if m.group(1) is not None: out.append(('S', m.group(1)))
        elif m.group(2): out.append(('A', int(m.group(2)[1:])))
        elif m.group(3): out.append(('I', m.group(3)))
        elif m.group(4): out.append(('N', float(m.group(4))))
        elif m.group(5): out.append(('P', m.group(5)))
    return out


def parse(path):
    toks = tokenize(open(path, 'r', encoding='utf-8', errors='replace').read())
    pos = 0
    root = Node('root')

    def block(parent):
        nonlocal pos
        while pos < len(toks):
            t = toks[pos]
            if t == ('P', '}'):
                pos += 1; return
            if t[0] == 'I' and pos + 1 < len(toks) and toks[pos + 1] == ('P', ':'):
                n = Node(t[1]); pos += 2
                while pos < len(toks):
                    t = toks[pos]
                    if t == ('P', '{'):
                        pos += 1; block(n); break
                    if t[0] == 'I' and pos + 1 < len(toks) and toks[pos + 1] == ('P', ':'): break
                    if t == ('P', '}'): break
                    if t[0] == 'A' or t == ('P', ','):
                        pos += 1; continue
                    n.vals.append(t[1]); pos += 1
                parent.kids.append(n)
            else:
                pos += 1

    block(root)
    return root


if __name__ == '__main__':
    r = parse(sys.argv[1])
    ob = r.get('Objects')
    for n in ob.kids:
        if n.name in ('Geometry', 'Model', 'Material', 'Texture'):
            extra = ''
            if n.name == 'Geometry' and n.get('Vertices'):
                v = n.get('Vertices').get('a').vals
                extra = 'verts=%d polyidx=%d' % (len(v) // 3, len(n.get('PolygonVertexIndex').get('a').vals))
            if n.name == 'Model':
                p = n.get('Properties70')
                extra = ' '.join('%s=%s' % (k.vals[0], k.vals[4:7]) for k in (p.all('P') if p else []) if k.vals and k.vals[0] in ('Lcl Translation', 'Lcl Rotation', 'Lcl Scaling'))
            print(n.name, [str(x)[:50] for x in n.vals[:3]], extra)
    gs = r.get('GlobalSettings')
    if gs:
        for k in gs.get('Properties70').all('P'):
            if k.vals[0] in ('UpAxis', 'FrontAxis', 'UnitScaleFactor', 'CoordAxis'): print('GS', k.vals[0], k.vals[4:])
    print('CONN')
    for c in r.get('Connections').all('C')[:60]: print(c.vals)
