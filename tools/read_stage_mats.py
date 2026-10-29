import json
import struct

with open(r"C:\Users\drago\Downloads\stage_3.glb", "rb") as f:
    f.seek(12)
    l, t = struct.unpack("<II", f.read(8))
    j = json.loads(f.read(l).decode("utf-8"))
    print(json.dumps(j.get("materials", []), indent=2))
