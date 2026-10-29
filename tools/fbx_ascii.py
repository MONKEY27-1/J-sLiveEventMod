"""Minimal ASCII FBX reader. Returns a tree of (name, [props], [children]). Arrays (*N { a: ... }) become python lists of floats."""
import re, sys

def parse(text):
    pos = 0
    n = len(text)
    num_re = re.compile(r'-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?')

    def skip_ws():
        nonlocal pos
        while pos < n:
            c = text[pos]
            if c in ' \t\r\n':
                pos += 1
            elif c == ';':
                while pos < n and text[pos] != '\n':
                    pos += 1
            else:
                break

    def read_value():
        nonlocal pos
        skip_ws()
        c = text[pos]
        if c == '"':
            end = text.index('"', pos + 1)
            v = text[pos + 1:end]
            pos = end + 1
            return v
        if c == '*':
            m = re.compile(r'\*\d+\s*\{\s*a:\s*').match(text, pos)
            end = text.index('}', m.end())
            body = text[m.end():end]
            pos = end + 1
            return [float(x) for x in num_re.findall(body)]
        m = re.compile(r'[^,\s{}]+').match(text, pos)
        s = m.group(0)
        pos = m.end()
        try:
            return float(s) if ('.' in s or 'e' in s.lower()) else int(s)
        except ValueError:
            return s

    def read_nodes(until_brace):
        nonlocal pos
        out = []
        while True:
            skip_ws()
            if pos >= n:
                return out
            if text[pos] == '}':
                pos += 1
                return out
            m = re.compile(r'([A-Za-z0-9_]+)\s*:').match(text, pos)
            if not m:
                pos += 1
                continue
            name = m.group(1)
            pos = m.end()
            props = []
            children = []
            while True:
                skip_ws()
                if pos >= n:
                    break
                c = text[pos]
                if c == '{':
                    pos += 1
                    children = read_nodes(True)
                    break
                if c == ',':
                    pos += 1
                    continue
                # property or end of line? a new key starts a new node: detect "word:" at line start
                if re.compile(r'[A-Za-z0-9_]+\s*:').match(text, pos) and (text[pos - 1] == '\n' or text[:pos].rstrip(' \t').endswith('\n')):
                    break
                if c == '}':
                    break
                props.append(read_value())
            out.append((name, props, children))
        return out

    return read_nodes(False)

def find(nodes, name):
    return [x for x in nodes if x[0] == name]

if __name__ == '__main__':
    path = sys.argv[1]
    tree = parse(open(path, encoding='utf-8', errors='replace').read())
    objs = find([c for t in find(tree, 'Objects') for c in t[2]], 'Model')
    print('top-level:', [t[0] for t in tree])
    for t in find(tree, 'Objects'):
        for o in t[2]:
            extra = ''
            if o[0] == 'Geometry':
                v = find(o[2], 'Vertices')
                extra = 'verts=%d' % (len(v[0][1][0]) // 3 if v and v[0][1] else 0)
            if o[0] == 'Model':
                p70 = find(o[2], 'Properties70')
                tr = {}
                if p70:
                    for p in find(p70[0][2], 'P'):
                        if p[1] and p[1][0] in ('Lcl Translation', 'Lcl Rotation', 'Lcl Scaling'):
                            tr[p[1][0]] = p[1][4:7]
                extra = str(tr)
            print(o[0], o[1][:2], extra)
    for t in find(tree, 'Connections'):
        for c in t[2][:80]:
            print('C', c[1])
