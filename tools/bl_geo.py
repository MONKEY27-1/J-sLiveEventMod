"""Geometry + texture helpers for the Unvaulting set pieces (make_site.py, make_buildings.py, make_rim.py, make_volcano.py). Blender units are metres, Z is up; a named object's origin sits at the centre of its base
(the plugin clones objects by name and stands them on the floor).  MB collects boxes / cylinders / cones / quads (with per-face material and planar uv in metres / tile) into one mesh."""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector, Matrix
from bl_tex import *

class MB:
    def __init__(self, mats): self.v = []; self.f = []; self.uv = []; self.mi = []; self.mats = mats
    def quad(self, a, b, c, d, mat, uvs):
        i = len(self.v); self.v += [tuple(a), tuple(b), tuple(c), tuple(d)]; self.f.append((i, i + 1, i + 2, i + 3)); self.uv.append(uvs); self.mi.append(mat)
    def tri(self, a, b, c, mat, uvs):
        i = len(self.v); self.v += [tuple(a), tuple(b), tuple(c)]; self.f.append((i, i + 1, i + 2)); self.uv.append(uvs); self.mi.append(mat)
    def box(self, c, s, mat, tile=3.0, tile_v=None, top=None, bottom=False):
        """axis aligned box centred at c (x, y, z) with size s; the side faces use `mat`, the top `top` (default mat)"""
        tv = tile_v or tile; x0, y0, z0 = c[0] - s[0] / 2, c[1] - s[1] / 2, c[2] - s[2] / 2; x1, y1, z1 = x0 + s[0], y0 + s[1], z0 + s[2]
        self.quad((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1), mat, [(x0 / tile, z0 / tv), (x1 / tile, z0 / tv), (x1 / tile, z1 / tv), (x0 / tile, z1 / tv)])          # -y
        self.quad((x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1), mat, [(-x1 / tile, z0 / tv), (-x0 / tile, z0 / tv), (-x0 / tile, z1 / tv), (-x1 / tile, z1 / tv)])      # +y
        self.quad((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), mat, [(y0 / tile, z0 / tv), (y1 / tile, z0 / tv), (y1 / tile, z1 / tv), (y0 / tile, z1 / tv)])          # +x
        self.quad((x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1), mat, [(-y1 / tile, z0 / tv), (-y0 / tile, z0 / tv), (-y0 / tile, z1 / tv), (-y1 / tile, z1 / tv)])      # -x
        tm = mat if top is None else top
        self.quad((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), tm, [(x0 / tile, y0 / tile), (x1 / tile, y0 / tile), (x1 / tile, y1 / tile), (x0 / tile, y1 / tile)])     # top
        if bottom: self.quad((x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0), mat, [(x0 / tile, y1 / tile), (x1 / tile, y1 / tile), (x1 / tile, y0 / tile), (x0 / tile, y0 / tile)])
    def cyl(self, c, r, h, mat, seg=14, r2=None, axis='z', tile=2.0):
        """cylinder / cone frustum centred at c along `axis` (x, y or z); r2 = radius at the far end"""
        r2 = r if r2 is None else r2; pts = []
        def P(a, rr, t):
            ca, sa = math.cos(a) * rr, math.sin(a) * rr; t0 = (t - 0.5) * h
            return (c[0] + t0, c[1] + ca, c[2] + sa) if axis == 'x' else (c[0] + ca, c[1] + t0, c[2] + sa) if axis == 'y' else (c[0] + ca, c[1] + sa, c[2] + t0)
        for k in range(seg):
            a0 = 2 * math.pi * k / seg; a1 = 2 * math.pi * (k + 1) / seg; u0 = k / seg * 2 * math.pi * r / tile; u1 = (k + 1) / seg * 2 * math.pi * r / tile
            self.quad(P(a0, r, 0), P(a1, r, 0), P(a1, r2, 1), P(a0, r2, 1), mat, [(u0, 0), (u1, 0), (u1, h / tile), (u0, h / tile)])
            if r2 > 0: self.tri(P(a0, r2, 1), P(a1, r2, 1), P(0, 0, 1), mat, [(0, 0), (0, 0), (0, 0)])
            self.tri(P(a1, r, 0), P(a0, r, 0), P(0, 0, 0), mat, [(0, 0), (0, 0), (0, 0)])
    def build(self, name, parent=None, shade_smooth=False):
        me = bpy.data.meshes.new(name); me.from_pydata(self.v, [], self.f); uv = me.uv_layers.new()
        for pi, p in enumerate(me.polygons):
            p.material_index = self.mi[pi]
            for li, t in zip(p.loop_indices, self.uv[pi]): uv.data[li].uv = t
        for m in self.mats: me.materials.append(m)
        if shade_smooth:
            for p in me.polygons: p.use_smooth = True
        o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); o.parent = parent; return o

def noise2(W, H, base, seed, octv=4): return fbm(W, H, base, seed, octv)
def stripe(U, V, n, width): return np.exp(-(np.minimum((U * n) % 1, 1 - (U * n) % 1) / width) ** 2)

# ---------------------------------------------------------------- textures
def tex_facade(wall, seed, bays=4, floors=2, lit=0.22, glass=(0.12, 0.2, 0.34), frame=(0.82, 0.84, 0.86)):
    W = H = 256; U, V = grid(W, H); n = fbm(W, H, 4, seed); out = np.zeros((H, W, 4), np.float32); out[..., 3] = 1
    base = np.array(wall)[None, None, :] * (0.85 + 0.3 * n[..., None])
    cu = (U * bays) % 1; cv = (V * floors) % 1; win = (cu > 0.2) & (cu < 0.8) & (cv > 0.22) & (cv < 0.8); fr = (cu > 0.15) & (cu < 0.85) & (cv > 0.17) & (cv < 0.85) & ~win
    cell = (np.floor(U * bays) * 7 + np.floor(V * floors) * 13 + seed) % 11 / 11.0; isl = cell < lit
    g = np.array(glass)[None, None, :] * (0.7 + 0.6 * (1 - cv[..., None])); sheen = np.clip(1 - np.abs(cu - cv * 0.9 - 0.1) * 6, 0, 1)[..., None] * 0.18; g = g + sheen
    glow = np.array((1.0, 0.82, 0.45))[None, None, :] * (0.8 + 0.2 * cv[..., None])
    out[..., :3] = base; out[..., :3] = np.where(fr[..., None], np.array(frame), out[..., :3]); out[..., :3] = np.where(win[..., None], np.where(isl[..., None], glow, g), out[..., :3])
    sill = (cv > 0.1) & (cv <= 0.17) & (cu > 0.12) & (cu < 0.88); out[..., :3] = np.where(sill[..., None], np.array(wall)[None, None, :] * 0.7, out[..., :3])
    return out
def tex_concrete(seed, col=(0.62, 0.62, 0.64)):
    W = H = 128; n = fbm(W, H, 5, seed); return rgba(col[0] * (0.8 + 0.4 * n), col[1] * (0.8 + 0.4 * n), col[2] * (0.8 + 0.4 * n), 1.0)
def tex_gravel(seed): W = H = 128; n = fbm(W, H, 8, seed); return rgba(0.3 + 0.25 * n, 0.3 + 0.25 * n, 0.32 + 0.25 * n, 1.0)
def tex_storefront(col, seed):
    W, H = 256, 128; U, V = grid(W, H); n = fbm(W, H, 4, seed); cu = (U * 4) % 1; shop = (cu > 0.08) & (cu < 0.92) & (V > 0.12) & (V < 0.8)
    base = np.array(col)[None, None, :] * (0.8 + 0.3 * n[..., None]); glass = np.array((0.18, 0.28, 0.4))[None, None, :] * (0.6 + 0.8 * (1 - V[..., None])) + (np.clip(1 - np.abs(cu - V + 0.15) * 7, 0, 1)[..., None] * 0.2)
    door = (cu > 0.4) & (cu < 0.62) & (V < 0.8); out = np.where(shop[..., None], glass, base); out = np.where(door[..., None], np.array((0.22, 0.16, 0.12)), out); return rgba(out[..., 0], out[..., 1], out[..., 2], 1.0)
def tex_container(col, seed):
    W, H = 256, 128; U, V = grid(W, H); n = fbm(W, H, 4, seed); rust = np.clip((fbm(W, H, 10, seed + 5) - 0.55) * 4, 0, 1); rib = 0.78 + 0.22 * (np.sin(U * 2 * np.pi * 24) * 0.5 + 0.5)
    base = np.array(col)[None, None, :] * rib[..., None] * (0.8 + 0.3 * n[..., None]); base = base * (1 - 0.4 * rust[..., None]) + np.array((0.45, 0.22, 0.1))[None, None, :] * 0.4 * rust[..., None]
    mark = (V > 0.62) & (V < 0.74) & (U > 0.12) & (U < 0.45); base = np.where(mark[..., None], np.array((0.92, 0.92, 0.9)), base); return rgba(base[..., 0], base[..., 1], base[..., 2], 1.0)
def tex_rock(seed, tint=(1.0, 1.0, 1.0)):
    W = H = 256; n = fbm(W, H, 4, seed); strata = 0.5 + 0.5 * np.sin(V_ := (np.arange(H)[:, None] / H * 2 * np.pi * 9 + n * 5)); col = np.stack([0.42 + 0.22 * strata, 0.30 + 0.16 * strata, 0.22 + 0.12 * strata], -1) * (0.7 + 0.5 * n[..., None]) * np.array(tint)
    return rgba(col[..., 0], col[..., 1], col[..., 2], 1.0)
def tex_solid(r, g, b): return rgba(np.full((4, 4), r), np.full((4, 4), g), np.full((4, 4), b), np.full((4, 4), 1.0))
def tex_yellow(seed):
    W = H = 128; n = fbm(W, H, 5, seed); U, V = grid(W, H); dirt = np.clip((fbm(W, H, 9, seed + 3) - 0.5) * 3, 0, 1); col = np.array((0.96, 0.72, 0.08))[None, None, :] * (0.85 + 0.25 * n[..., None]); col = col * (1 - 0.5 * dirt[..., None]); return rgba(col[..., 0], col[..., 1], col[..., 2], 1.0)
def tex_glass(a=0.45): return rgba(np.full((4, 4), 0.55), np.full((4, 4), 0.95), np.full((4, 4), 1.0), np.full((4, 4), a))
def displace_icosphere(radius, subdiv, seed, rough=0.35, squash=(1, 1, 1)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0, calc_uvs=True); rng = np.random.default_rng(seed)
    for v in bm.verts:
        d = v.co.normalized(); n = 0.0; amp = 0.5; f = 1.6
        for o in range(4): n += amp * (math.sin(d.x * f * 3.1 + seed) * math.cos(d.y * f * 2.7 + o) + math.sin(d.z * f * 3.7 + seed * 0.3)) * 0.5; amp *= 0.5; f *= 2
        v.co = Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])) * radius * (1 + rough * n)
    return bm
