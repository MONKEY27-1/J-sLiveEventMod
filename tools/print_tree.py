import json, struct

with open(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))

nodes = j["nodes"]

def print_tree(idx, depth=0):
    n = nodes[idx]
    prefix = "  " * depth
    info = f"{prefix}Node {idx}: {n.get('name')}"
    if n.get("scale"):
        info += f" scale={n.get('scale')}"
    if n.get("matrix"):
        info += f" matrix={n.get('matrix')}"
    if n.get("translation"):
        info += f" trans={n.get('translation')}"
    if n.get("mesh") is not None:
        info += f" [MESH {n.get('mesh')}]"
    if n.get("skin") is not None:
        info += f" [SKIN {n.get('skin')}]"
    print(info)
    for c in n.get("children", []):
        print_tree(c, depth + 1)

print_tree(0)
