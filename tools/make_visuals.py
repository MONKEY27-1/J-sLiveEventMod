"""blender -b -P make_visuals.py -- <out.glb>
Flip-book visuals for the concert's LED screens: six loops (Sheet0..Sheet5), each a 4x4 sheet of 16 seamless frames (256x144 each), carried as the textures of six quads in one glb (the plugin reads the textures).
0 Alone: slow blue / violet waves | 1 neon perspective grid | 2 paint swirls | 3 fire | 4 rainbow plasma | 5 Happier: yellow chevrons + radial burst. Frame 0 is the top-left cell."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1]; setup()
FW, FH = 256, 144
def hsv(h, s, v):
    h = h % 1.0; i = np.floor(h * 6).astype(int); f = h * 6 - i; p = v * (1 - s); q = v * (1 - f * s); t = v * (1 - (1 - f) * s); i = i % 6
    r = np.choose(i, [v, q, p, p, t, v]); g = np.choose(i, [t, v, v, q, p, p]); b = np.choose(i, [p, p, t, v, v, q]); return r, g, b
X, Y = np.meshgrid((np.arange(FW) + 0.5) / FW, (np.arange(FH) + 0.5) / FH); TAU = 2 * np.pi
def f_alone(ph):
    w = np.sin(TAU * (X * 2 + ph)) * 0.5 + np.sin(TAU * (Y * 3 - ph) + X * 4) * 0.5; v = 0.35 + 0.25 * w + 0.4 * np.exp(-((Y - 0.55 - 0.15 * np.sin(TAU * (X + ph))) / 0.12) ** 2)
    return np.clip(0.15 + 0.3 * v, 0, 1), np.clip(0.2 + 0.25 * v * (1 - Y), 0, 1), np.clip(0.6 + 0.4 * v, 0, 1)
def f_grid(ph):
    gx = np.exp(-(np.minimum((X * 12 + ph * 12) % 1, 1 - (X * 12 + ph * 12) % 1) / 0.05) ** 2); gy = np.exp(-(np.minimum(((1 - Y) ** 2 * 10 + ph * 4) % 1, 1 - ((1 - Y) ** 2 * 10 + ph * 4) % 1) / 0.06) ** 2)
    g = np.maximum(gx * (0.3 + 0.7 * Y), gy); pulse = 0.6 + 0.4 * np.sin(TAU * ph); return 0.05 + 0.2 * g * pulse, 0.1 + 0.95 * g, 0.2 + 0.7 * g
def f_paint(ph):
    wx = X + 0.12 * np.sin(TAU * (Y * 3 + ph)); wy = Y + 0.12 * np.sin(TAU * (X * 2 - ph)); h = 0.5 * np.sin(TAU * (wx * 2 + wy * 2)) + 0.5 * np.sin(TAU * (wx * 3 - wy + ph)); return hsv(0.9 + 0.35 * h + ph * 0.0, 0.8, 0.5 + 0.5 * np.abs(h))
def f_fire(ph):
    n = np.sin(TAU * (X * 6 + ph)) * 0.5 + np.sin(TAU * (X * 11 - ph * 2) + Y * 9) * 0.5; flame = np.clip(1.2 * (1 - Y) + 0.28 * n - 0.15 * np.sin(TAU * (X * 3 + ph)) - 0.25, 0, 1); return np.clip(flame * 1.3, 0, 1), np.clip(flame ** 2 * 0.9, 0, 1), np.clip(flame ** 5 * 0.4, 0, 1)
def f_plasma(ph):
    v = np.sin(TAU * (X * 3 + ph)) + np.sin(TAU * (Y * 4 - ph)) + np.sin(TAU * ((X + Y) * 3 + ph * 2)) + np.sin(TAU * (np.hypot(X - 0.5, (Y - 0.5) * 1.78) * 5 - ph)); return hsv(v / 8 + 0.5, 0.9, 0.9)
def f_happier(ph):
    ch = ((X * 8 + np.abs(Y - 0.5) * 6 - ph * 2) % 1 < 0.5); base = np.where(ch, 1.0, 0.1); ang = np.arctan2((Y - 0.5) * 1.78, X - 0.5); ray = 0.5 + 0.5 * np.sin(ang * 12 + ph * TAU * 2); r = np.hypot(X - 0.5, (Y - 0.5) * 1.78)
    glow = np.clip(1 - r * 1.4, 0, 1) * ray; return np.clip(base * 0.95 + glow * 0.5, 0, 1), np.clip(base * 0.72 + glow * 0.4, 0, 1), np.clip(base * 0.05 + glow * 0.1, 0, 1)
FN = [f_alone, f_grid, f_paint, f_fire, f_plasma, f_happier]
bm = []
for si, fn in enumerate(FN):
    sheet = np.zeros((FH * 4, FW * 4, 4), np.float32); sheet[..., 3] = 1
    for k in range(16):
        r, g, b = fn(k / 16.0); r0 = (3 - k // 4) * FH; c0 = (k % 4) * FW; fr = np.stack([r, g, b], -1)[::-1]            # image rows go bottom-up in Blender
        sheet[r0:r0 + FH, c0:c0 + FW, :3] = np.clip(fr, 0, 1)
    m = material('Sheet%dMat' % si, sheet); b = MB([m]); b.quad((si * 3, 0, 0), (si * 3 + 2, 0, 0), (si * 3 + 2, 0, 1.12), (si * 3, 0, 1.12), 0, [(0, 0), (1, 0), (1, 1), (0, 1)]); b.build('Sheet%d' % si)
export(out)
