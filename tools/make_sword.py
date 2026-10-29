"""blender -b -P make_sword.py -- out.glb   the mech's energy sword: hilt centred on the origin, blade along +Z (Blender; +Y in the glb), ~10 m long (the plugin scales it)."""
import bpy, sys, math, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bl_geo import *
out = sys.argv[sys.argv.index('--') + 1:][0]; setup(24, 1)
STEEL = solid('Steel', 0.72, 0.78, 0.9); EDGE = solid('Edge', 0.35, 1.0, 1.0); GOLD = solid('Gold', 0.95, 0.7, 0.18); DARK = solid('Dark', 0.1, 0.1, 0.15); RED = solid('Gem', 1.0, 0.2, 0.3)
b = MB([STEEL, EDGE, GOLD, DARK, RED]); S, E, G, D, R = range(5)
b.cyl((0, 0, -0.35), 0.17, 2.1, D, 10); [b.cyl((0, 0, -1.3 + k * 0.45), 0.2, 0.08, G, 10) for k in range(4)]            # grip with gold rings
b.cyl((0, 0, -1.5), 0.33, 0.35, G, 10); b.box((0, 0, 0.85), (3.0, 0.5, 0.35), G, 1.0); b.box((0, 0, 0.85), (0.5, 0.62, 0.5), R, 1.0)                  # pommel, crossguard, gem
for sg in (-1, 1): b.box((sg * 1.7, 0, 0.7), (0.35, 0.4, 0.9), G, 1.0)
b.box((0, 0, 4.9), (0.95, 0.26, 8.3), S, 1.0); b.box((0, 0, 4.9), (0.28, 0.34, 8.3), E, 1.0)                                                          # blade + glowing fuller
for sg in (-1, 1): b.box((sg * 0.5, 0, 4.9), (0.06, 0.28, 8.3), E, 1.0)                                                                                # glowing edges
b.tri((-0.475, -0.13, 9.05), (0.475, -0.13, 9.05), (0, 0, 10.4), S, [(0, 0), (1, 0), (0.5, 1)]); b.tri((0.475, 0.13, 9.05), (-0.475, 0.13, 9.05), (0, 0, 10.4), S, [(0, 0), (1, 0), (0.5, 1)])
b.tri((0.475, -0.13, 9.05), (0.475, 0.13, 9.05), (0, 0, 10.4), E, [(0, 0), (1, 0), (0.5, 1)]); b.tri((-0.475, 0.13, 9.05), (-0.475, -0.13, 9.05), (0, 0, 10.4), E, [(0, 0), (1, 0), (0.5, 1)])
o = b.build('Sword'); keys(o, 'rotation_euler', lambda t: (0, 0, 0), 24, 1, 1); export(out)
