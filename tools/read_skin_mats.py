import json
import struct

with open(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))
    print("Materials:", json.dumps(j.get("materials", []), indent=2))
    print("Textures:", json.dumps(j.get("textures", []), indent=2))
    print("Images:", json.dumps(j.get("images", []), indent=2))
