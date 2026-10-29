import json, struct

with open(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))
    nodes = j.get("nodes", [])
    for i, n in enumerate(nodes):
        if 'pelvis' in str(n.get('name')):
            print(f"Original Skin Node {i} ({n.get('name')}): trans={n.get('translation')}, scale={n.get('scale')}, matrix={n.get('matrix')}")

    scenes = j.get("scenes", [])
    if scenes:
        scene_nodes = scenes[0].get("nodes", [])
        for sni in scene_nodes:
            sn = nodes[sni]
            print(f"Original Scene node {sni} ({sn.get('name')}): children={sn.get('children')}, trans={sn.get('translation')}, scale={sn.get('scale')}")
