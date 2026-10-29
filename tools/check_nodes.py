import json, struct

with open(r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))
    nodes = j.get("nodes", [])
    skins = j.get("skins", [])
    print(f"Total nodes: {len(nodes)}, Skins: {len(skins)}")
    if skins:
        skin0 = skins[0]
        joints = skin0.get("joints", [])
        print(f"Skin joints: {len(joints)}")
        for ji in joints[:15]:
            jn = nodes[ji]
            print(f"  Joint {ji} ({jn.get('name')}): trans={jn.get('translation')}, scale={jn.get('scale')}, matrix={jn.get('matrix')}")

    print("\nRoot scene nodes:")
    scenes = j.get("scenes", [])
    if scenes:
        scene_nodes = scenes[0].get("nodes", [])
        for sni in scene_nodes:
            sn = nodes[sni]
            print(f"  Scene node {sni} ({sn.get('name')}): children={sn.get('children')}, trans={sn.get('translation')}, scale={sn.get('scale')}")
