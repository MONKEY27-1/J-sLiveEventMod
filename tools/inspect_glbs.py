import json
import struct
import sys

def inspect_glb(path):
    print(f"=== {path} ===")
    try:
        with open(path, "rb") as f:
            magic, version, length = struct.unpack("<4sII", f.read(12))
            if magic != b"glTF":
                print("Not a GLTF")
                return
            chunk_len, chunk_type = struct.unpack("<II", f.read(8))
            json_data = json.loads(f.read(chunk_len).decode("utf-8"))
            print("Nodes:", len(json_data.get("nodes", [])))
            print("Meshes:", len(json_data.get("meshes", [])))
            for mi, m in enumerate(json_data.get("meshes", [])):
                print(f"  Mesh {mi}: {m.get('name')}, primitives: {len(m.get('primitives', []))}")
            print("Skins:", len(json_data.get("skins", [])))
            for si, s in enumerate(json_data.get("skins", [])):
                print(f"  Skin {si}: {s.get('name')}, joints: {len(s.get('joints', []))}")
            print("Materials:", len(json_data.get("materials", [])))
            for mat_i, mat in enumerate(json_data.get("materials", [])):
                print(f"  Material {mat_i}: {mat.get('name')}")
            print("Textures:", len(json_data.get("textures", [])))
            print("Images:", len(json_data.get("images", [])))
            anims = json_data.get("animations", [])
            print("Animations:", len(anims))
            for i, a in enumerate(anims):
                print(f"  [{i}] name: {a.get('name', '<no name>')}, channels: {len(a.get('channels', []))}, samplers: {len(a.get('samplers', []))}")
            nodes = json_data.get("nodes", [])
            print(f"Top 20 nodes of {len(nodes)}:")
            for i, n in enumerate(nodes[:20]):
                print(f"  Node {i}: {n.get('name')}")
    except Exception as e:
        print(f"Error inspecting {path}: {e}")

if __name__ == "__main__":
    inspect_glb(r"C:\Users\drago\Downloads\travis_scott_fortnite_skin.glb")
    print()
    inspect_glb(r"C:\Users\drago\Downloads\stage_3.glb")
    print()
    inspect_glb(r"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\BepInEx\plugins\JsLiveEventsMod\AstronomicalAssets\travis_avatar.glb")
