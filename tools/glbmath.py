"""Tiny pure-python glTF + 3D math helpers (no numpy). Matrices are row-major lists of lists."""
import json, struct, math

def load_glb(path):
    b = open(path, 'rb').read()
    jl = struct.unpack_from('<I', b, 12)[0]
    j = json.loads(b[20:20 + jl])
    off = 20 + jl + 8
    bl = struct.unpack_from('<I', b, 20 + jl)[0]
    return j, bytearray(b[off:off + bl])

CT = {5126: ('f', 4), 5125: ('I', 4), 5123: ('H', 2), 5121: ('B', 1), 5122: ('h', 2), 5120: ('b', 1)}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def read_acc(g, binb, i):
    a = g['accessors'][i]; bv = g['bufferViews'][a['bufferView']]
    n = NC[a['type']]; fmt, sz = CT[a['componentType']]
    start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride', n * sz)
    out = []
    for k in range(a['count']):
        out.append(struct.unpack_from('<%d%s' % (n, fmt), binb, start + k * stride))
    return out

def ident():
    return [[1.0 if r == c else 0.0 for c in range(4)] for r in range(4)]

def mmul(A, B):
    return [[sum(A[r][k] * B[k][c] for k in range(4)) for c in range(4)] for r in range(4)]

def from_colmajor(m):
    return [[m[c * 4 + r] for c in range(4)] for r in range(4)]

def inv4(M):
    A = [M[r][:] + [1.0 if r == c else 0.0 for c in range(4)] for r in range(4)]
    for c in range(4):
        p = max(range(c, 4), key=lambda r: abs(A[r][c])); A[c], A[p] = A[p], A[c]
        d = A[c][c]; A[c] = [x / d for x in A[c]]
        for r in range(4):
            if r != c:
                f = A[r][c]; A[r] = [x - f * y for x, y in zip(A[r], A[c])]
    return [row[4:] for row in A]

def quat_to_m3(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]

def m3_to_quat(R):
    t = R[0][0] + R[1][1] + R[2][2]
    if t > 0:
        s = math.sqrt(t + 1) * 2; w = 0.25 * s
        x = (R[2][1] - R[1][2]) / s; y = (R[0][2] - R[2][0]) / s; z = (R[1][0] - R[0][1]) / s
    elif R[0][0] > R[1][1] and R[0][0] > R[2][2]:
        s = math.sqrt(1 + R[0][0] - R[1][1] - R[2][2]) * 2; w = (R[2][1] - R[1][2]) / s
        x = 0.25 * s; y = (R[0][1] + R[1][0]) / s; z = (R[0][2] + R[2][0]) / s
    elif R[1][1] > R[2][2]:
        s = math.sqrt(1 + R[1][1] - R[0][0] - R[2][2]) * 2; w = (R[0][2] - R[2][0]) / s
        x = (R[0][1] + R[1][0]) / s; y = 0.25 * s; z = (R[1][2] + R[2][1]) / s
    else:
        s = math.sqrt(1 + R[2][2] - R[0][0] - R[1][1]) * 2; w = (R[1][0] - R[0][1]) / s
        x = (R[0][2] + R[2][0]) / s; y = (R[1][2] + R[2][1]) / s; z = 0.25 * s
    l = math.sqrt(x * x + y * y + z * z + w * w)
    return (x / l, y / l, z / l, w / l)

def m3mul(A, B):
    return [[sum(A[r][k] * B[k][c] for k in range(3)) for c in range(3)] for r in range(3)]

def m3T(A):
    return [[A[c][r] for c in range(3)] for r in range(3)]

def trs_to_m4(t, q, s):
    R = quat_to_m3(q)
    return [[R[r][c] * s[c] for c in range(3)] + [t[r]] for r in range(3)] + [[0, 0, 0, 1.0]]

def rot_of(M):
    """Rotation part of an affine matrix with (positive) scale: normalise columns."""
    cols = []
    for c in range(3):
        v = [M[r][c] for r in range(3)]; l = math.sqrt(sum(x * x for x in v)) or 1.0
        cols.append([x / l for x in v])
    return [[cols[c][r] for c in range(3)] for r in range(3)]

def det3(R):
    return (R[0][0] * (R[1][1] * R[2][2] - R[1][2] * R[2][1]) - R[0][1] * (R[1][0] * R[2][2] - R[1][2] * R[2][0]) + R[0][2] * (R[1][0] * R[2][1] - R[1][1] * R[2][0]))

def slerp(a, b, t):
    d = sum(x * y for x, y in zip(a, b))
    if d < 0: b = tuple(-x for x in b); d = -d
    if d > 0.9995:
        r = [x + (y - x) * t for x, y in zip(a, b)]
    else:
        th = math.acos(d); s = math.sin(th)
        r = [(math.sin((1 - t) * th) * x + math.sin(t * th) * y) / s for x, y in zip(a, b)]
    l = math.sqrt(sum(x * x for x in r)); return tuple(x / l for x in r)

def node_local(n):
    if 'matrix' in n:
        return from_colmajor(n['matrix'])
    return trs_to_m4(n.get('translation', [0, 0, 0]), n.get('rotation', [0, 0, 0, 1]), n.get('scale', [1, 1, 1]))

def parents_of(g):
    p = [-1] * len(g['nodes'])
    for i, n in enumerate(g['nodes']):
        for c in n.get('children', []): p[c] = i
    return p

def order_of(g):
    par = parents_of(g); out = []; seen = set()
    def visit(i):
        if i in seen: return
        if par[i] >= 0: visit(par[i])
        seen.add(i); out.append(i)
    for i in range(len(g['nodes'])): visit(i)
    return out, par
