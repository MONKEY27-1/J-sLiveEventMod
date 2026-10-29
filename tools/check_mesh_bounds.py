import json, struct

with open(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))
    
nodes = j["nodes"]
print("Total nodes:", len(nodes))
# Let's inspect mesh bounds in accessors
for m in j.get("meshes", []):
    print("Mesh:", m.get("name"))
    for p in m.get("primitives", []):
        pos_acc_idx = p["attributes"]["POSITION"]
        pos_acc = j["accessors"][pos_acc_idx]
        print("  Pos accessor min:", pos_acc.get("min"))
        print("  Pos accessor max:", pos_acc.get("max"))
