import json, struct

with open(r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))

nodes = j["nodes"]
scenes = j.get("scenes", [])
print("Scenes count:", len(scenes))
for si, s in enumerate(scenes):
    print(f"Scene {si}: nodes={s.get('nodes')}")

def find_in_tree(root_idx):
    found = []
    stack = [root_idx]
    while stack:
        curr = stack.pop()
        n = nodes[curr]
        if n.get("mesh") is not None:
            found.append(f"Mesh {n.get('mesh')} on node {curr} ({n.get('name')})")
        if n.get("skin") is not None:
            found.append(f"Skin {n.get('skin')} on node {curr} ({n.get('name')})")
        for c in n.get("children", []):
            stack.append(c)
    return found

for sni in scenes[0].get("nodes", []):
    print(f"\nRoot node {sni} ({nodes[sni].get('name')}) contains:")
    for item in find_in_tree(sni):
        print("  ", item)
