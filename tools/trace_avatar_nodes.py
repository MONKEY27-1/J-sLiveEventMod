import json, struct

with open(r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))

nodes = j["nodes"]

def trace_down(idx, depth=0):
    n = nodes[idx]
    print("  " * depth + f"Node {idx} ({n.get('name')}): scale={n.get('scale')} trans={n.get('translation')} matrix={n.get('matrix')}")
    for c in n.get("children", []):
        trace_down(c, depth + 1)

trace_down(297)
