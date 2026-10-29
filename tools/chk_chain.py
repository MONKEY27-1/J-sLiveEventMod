import sys, os, math
sys.path.insert(0, os.getcwd())
from glbmath import *
g, b = load_glb(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb"); order, par = order_of(g)
G = {}
for i in order:
    L = node_local(g['nodes'][i]); G[i] = mmul(G[par[i]], L) if par[i] >= 0 else L
sk = g['skins'][0]
for ji in sk['joints'][:4]:
    n = g['nodes'][ji]; print(n['name'], "local t", n.get('translation'), "r", n.get('rotation'), "s", n.get('scale'), "| global pos", [round(G[ji][r][3],1) for r in range(3)], "| parent", g['nodes'][par[ji]]['name'] if par[ji]>=0 else None)
pel = [i for i in sk['joints'] if g['nodes'][i]['name'].startswith('pelvis')][0]
print("pelvis global rest pos", [round(G[pel][r][3],1) for r in range(3)])
m = [i for i,n in enumerate(g['nodes']) if 'skin' in n][0]
print("mesh global", [[round(G[m][r][c],3) for c in range(4)] for r in range(3)])
