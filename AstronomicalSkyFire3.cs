using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Sky Fire, part 3 (second pass): waypoint routes with stops, the animated parts of the ship (energy barriers, blast doors, pistons, fans, rings, conveyor, lift shaft), the beveled Cube model
    // (hero cubes + hundreds of merged cubes in the rack towers + the golden cube + falling cubes), the abduction from the Mothership, and the squad's clip cues.
    internal sealed partial class AstronomicalSkyFire
    {
        GltfModel cubeM;

        // ---------------------------------------------------------------- routes (floor positions relative to the block's S_ marker, with stops so the run has beats)
        sealed class Leg { public float T, X, Z; public bool Lin; }
        static Leg L(float t, float x, float z, bool lin = false) { return new Leg { T = t, X = x, Z = z, Lin = lin }; }
        readonly Dictionary<float, Leg[]> routes = new Dictionary<float, Leg[]>();
        readonly float[] cellRelX = new float[6];
        void BuildRoutes()
        {
            routes.Clear(); float c2 = cellRelX[2] / MK, cx = c2 - 0.9f;
            routes[25f] = new[] { L(25f, cx, -4.6f), L(36.4f, cx, -4.6f), L(39.4f, cx * 0.4f, 6f), L(41.6f, 0f, 13f), L(64f, 0f, 56f) };
            routes[65f] = new[] { L(65f, 0f, 0f), L(70f, 0f, 25f), L(74.2f, 0f, 26.5f), L(81f, 0f, 57f), L(86f, 0f, 58.5f), L(106f, 0f, 90f), L(115f, 0f, 90f) };
            routes[115f] = new[] { L(115f, 0f, 0f), L(118f, 0f, 0f), L(133f, 0f, 44f, true), L(143f, 0f, 66f), L(150f, 0f, 66f), L(165f, 0f, 100f) };
            routes[180f] = new[] { L(180f, 0f, 0f), L(183f, 0f, 0f), L(192f, 0f, 18f), L(203.5f, 0f, 46f), L(207.8f, 0f, 49f), L(214f, 0f, 62f), L(225f, 0f, 64f) };
            routes[510f] = new[] { L(510f, 0f, 0f), L(516f, 0f, 0f), L(540f, 0f, 40f), L(551f, 0f, 44f) };
            foreach (var kv in routes) foreach (var lg in kv.Value) { lg.X *= MK; lg.Z *= MK; }
        }
        bool RouteXZ(Seg g, float t, out float x, out float z)
        {
            x = z = 0f; Leg[] legs; if (!routes.TryGetValue(g.T0, out legs)) return false; int k = 0; for (int i = 0; i < legs.Length; i++) if (legs[i].T <= t) k = i;
            if (t <= legs[0].T) { x = legs[0].X; z = legs[0].Z; return true; }
            if (k + 1 >= legs.Length) { x = legs[k].X; z = legs[k].Z; return true; }
            float u = Mathf.Clamp01((t - legs[k].T) / (legs[k + 1].T - legs[k].T)); float e = legs[k + 1].Lin ? u : u * u * (3f - 2f * u); x = Mathf.Lerp(legs[k].X, legs[k + 1].X, e); z = Mathf.Lerp(legs[k].Z, legs[k + 1].Z, e); return true;
        }
        // the player's floor position (root-local) in this scene, whichever way it is defined
        Vector3 PlayerFloor(Seg g, float t)
        {
            Vector3 an; float a1, a2, a3, a4; if (Anchor(t, g, out an, out a1, out a2, out a3, out a4)) return an - Vector3.up * 1.6f; return new Vector3(0f, 0f, 0f);
        }

        // ---------------------------------------------------------------- animated parts
        readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>(); readonly Dictionary<string, Vector3> partBase = new Dictionary<string, Vector3>(); readonly Dictionary<string, Quaternion> partRot = new Dictionary<string, Quaternion>();
        readonly List<Vector3> steamPts = new List<Vector3>(); readonly List<Material> scrollMats = new List<Material>(); readonly Material[] barMats = new Material[6];
        static string PartBlock(string n)
        {
            if (n.StartsWith("Barrier")) return "CELLS"; if (n.StartsWith("BlastDoor") || n.StartsWith("Piston") || n.StartsWith("Fan")) return "HALL"; if (n == "Belt") return "WALK"; if (n.StartsWith("PDoor") || n.StartsWith("Ring")) return "ROOMS"; if (n == "Shaft") return "BLUE"; return null;
        }
        void RegisterParts()
        {
            parts.Clear(); partBase.Clear(); partRot.Clear(); steamPts.Clear();
            var barTex = kit.Tex(32, 64, (x, y) => { float s = 0.5f + 0.5f * Mathf.Sin(y * 1.1f); float hx = Mathf.Abs(Mathf.Sin(x * 0.6f + y * 0.15f)); return new Color(1f, 1f, 1f, Mathf.Clamp01(0.25f + 0.45f * s + 0.2f * hx)); });
            for (int i = 0; i < 6; i++) barMats[i] = kit.Glow(new Color(1f, 0.4f, 0.9f, 0.6f), barTex, 3002);
            foreach (var t in inM.Pivot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("STEAM")) { steamPts.Add(root.transform.InverseTransformPoint(t.position)); continue; }
                string b = PartBlock(t.name); if (b == null || parts.ContainsKey(t.name)) continue;
                parts[t.name] = t; partBase[t.name] = root.transform.InverseTransformPoint(t.position); partRot[t.name] = Quaternion.Inverse(root.transform.rotation) * t.rotation; t.gameObject.SetActive(false);
                if (t.name.StartsWith("Barrier")) { int bi = t.name[7] - '0'; foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = barMats[Mathf.Clamp(bi, 0, 5)]; cellRelX[Mathf.Clamp(bi, 0, 5)] = partBase[t.name].x - bS["CELLS"].x; }
            }
            scrollMats.Clear(); foreach (var kv in origMats) if (kv.Key != null && kv.Key.transform.IsChildOf(inM.Pivot.transform)) { var now = kv.Key.sharedMaterials; for (int i = 0; i < kv.Value.Length && i < now.Length; i++) if (kv.Value[i] != null && (kv.Value[i].name == "SF_Belt") && !scrollMats.Contains(now[i])) scrollMats.Add(now[i]); }
            BuildRoutes();
        }
        float BarrierDrop(int i) { return 36.4f + 0.3f * Mathf.Abs(i - 2); }
        void SetPartPos(string n, Vector3 offRootLocal) { Transform tr; if (parts.TryGetValue(n, out tr)) tr.position = root.transform.TransformPoint(partBase[n] + offRootLocal); }
        void PartsFrame(float t, float tp, float dt, Seg g, Vector3 headW)
        {
            // which parts show: the ones of the current block (the energy barriers vanish when they drop)
            foreach (var kv in parts)
            {
                bool on = PartBlock(kv.Key) == g.Blk; if (on && kv.Key.StartsWith("Barrier")) on = t < BarrierDrop(kv.Key[7] - '0') + 0.45f;
                if (on && kv.Key == "Shaft" && g.S != Sc.Blue && g.S != Sc.Lift) on = false; if (kv.Value.gameObject.activeSelf != on) kv.Value.gameObject.SetActive(on);
            }
            foreach (var m in scrollMats) if (m != null) m.mainTextureOffset = new Vector2(0f, -t * 0.7f);
            if (g.S == Sc.Cells)
            {
                for (int i = 0; i < 6; i++)
                {
                    float d = BarrierDrop(i); float a = (0.5f + 0.12f * Mathf.Sin(t * 9f + i)) * (1f - Sm(d, d + 0.4f, t)); barMats[i].color = new Color(1f, 0.4f + 0.2f * Mathf.Sin(t * 3f + i), 0.9f, a); barMats[i].mainTextureOffset = new Vector2(0f, -t * 0.8f);
                    if (tp < d && t >= d && !paused) { Vector3 bp = W(partBase["Barrier" + i] + Vector3.up * 0.2f); Sfx(sZap, bp, 0.9f, 0.8f + 0.1f * i, 0.05f); for (int k = 0; k < 50; k++) Emit(fx, bp + UnityEngine.Random.insideUnitSphere * 1.6f, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 9f), new Color(1f, 0.5f, 0.95f, 1f), UnityEngine.Random.Range(0.1f, 0.4f), UnityEngine.Random.Range(0.4f, 1.2f)); if (i == 2) { shake = Mathf.Max(shake, 0.6f); flashLamp.transform.position = bp; flashLamp.color = new Color(1f, 0.5f, 0.95f); flashLamp.intensity = Lit * 10f; } }
                }
                if (t > 25f && t < 37f && !paused && dt > 0f && UnityEngine.Random.value < dt * 5f) Emit(fx, W(partBase["Barrier" + UnityEngine.Random.Range(0, 6)] + new Vector3(UnityEngine.Random.Range(-1.8f, 1.8f), UnityEngine.Random.Range(-1.4f, 1.4f), 0f)), Vector3.zero, new Color(1f, 0.6f, 1f, 0.9f), 0.5f, 0.15f);
            }
            if (g.S == Sc.Hall)
            {
                float o30 = Sm(70.4f, 73.8f, t) * (1f - Sm(76.4f, 76.9f, t)), o62 = Sm(82.2f, 85.4f, t) * (1f - Sm(89.2f, 89.7f, t));
                DoorPair("BlastDoor30", "HALL", o30, 3.5f * MK); DoorPair("BlastDoor62", "HALL", o62, 3.5f * MK);
                if (!paused && ((tp < 76.4f && t >= 76.4f) || (tp < 89.2f && t >= 89.2f))) { Sfx(sBoom, headW + root.transform.forward * -8f * MK, 0.9f, 0.6f, 0.5f); shake = Mathf.Max(shake, 0.9f); for (int k = 0; k < 30; k++) Emit(dust, headW + root.transform.forward * -8f * MK + UnityEngine.Random.insideUnitSphere * 3f, Vector3.up * 2f, new Color(0.7f, 0.7f, 0.9f, 0.4f), 2f, 2f); }
                if (!paused && ((tp < 70.4f && t >= 70.4f) || (tp < 82.2f && t >= 82.2f))) Sfx(sWhoosh, headW + root.transform.forward * 8f * MK, 0.8f, 0.6f, 0.5f);
                for (int k = 0; k < 6; k++) { float ph = Mathf.Abs(Mathf.Sin(t * 2.4f + k * 1.3f)); SetPartPos("Piston" + k, new Vector3(0f, 1.6f * MK * ph, 0f)); if (ph > 0.97f && !paused && dt > 0f && UnityEngine.Random.value < 0.4f) Emit(fx, W(partBase["Piston" + k] + Vector3.down * 2.6f * MK), UnityEngine.Random.onUnitSphere * 2f, new Color(1f, 0.8f, 0.4f, 1f), 0.2f, 0.3f); }
                for (int k = 0; k < 4; k++) { Transform tr; if (parts.TryGetValue("Fan" + k, out tr)) tr.rotation = root.transform.rotation * Quaternion.Euler(t * (k % 2 == 0 ? 420f : -380f), 0f, 0f) * partRot["Fan" + k]; }
                for (int k = 0; k < steamPts.Count; k++) { float ph = Mathf.Repeat(t * 0.32f + k * 0.27f, 1f); if (ph < 0.12f && !paused && dt > 0f) for (int q = 0; q < 3; q++) Emit(dust, W(steamPts[k]) + UnityEngine.Random.insideUnitSphere * 0.3f, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(3f, 7f), UnityEngine.Random.Range(-1f, 1f)), new Color(0.85f, 0.92f, 1f, 0.45f), UnityEngine.Random.Range(1.2f, 2.4f), UnityEngine.Random.Range(0.8f, 1.6f)); }
            }
            if (g.S == Sc.Rooms)
            {
                DoorPair("PDoor", "ROOMS", Sm(203.8f, 207.3f, t), 4.5f * MK);
                if (!paused && tp < 203.8f && t >= 203.8f) Sfx(sWhoosh, headW + root.transform.forward * 6f, 0.9f, 0.7f, 0.5f);
                for (int k = 0; k < 5; k++) { Transform tr; if (parts.TryGetValue("Ring" + k, out tr)) tr.rotation = root.transform.rotation * Quaternion.Euler(0f, t * (22f + 11f * k) * (k % 2 == 0 ? 1f : -1f), 0f) * partRot["Ring" + k]; }
            }
            if (g.S == Sc.Lift)
            {
                float lk = Mathf.Clamp01((t - 495f) / 14.5f); float rise = 170f * MK * (lk * lk * (3f - 2f * lk)); SetPartPos("Shaft", new Vector3(0f, -rise, 0f));
            }
            else if (g.S == Sc.Blue) SetPartPos("Shaft", Vector3.zero);
        }
        // a sliding double door: parts NAME + "L" / "R" move outwards (away from the block's middle) by `open` * travel metres
        void DoorPair(string name, string blk, float open, float travel)
        {
            float cxm = bS[blk].x;
            foreach (var side in new[] { "L", "R" }) { string n = name + side; if (!partBase.ContainsKey(n)) continue; float sg = partBase[n].x >= cxm ? 1f : -1f; SetPartPos(n, new Vector3(sg * travel * open, 0f, 0f)); }
        }

        // ---------------------------------------------------------------- the Cube: textures, hero cubes, hundreds of merged cubes, falling cubes
        Texture2D runeTex; Material rackFrame, rackFace, fallGlow; readonly Material[] rackGlow = new Material[5];
        readonly List<GameObject> rackGos = new List<GameObject>(), floaters = new List<GameObject>(), fallCubes = new List<GameObject>(); int rackCount;
        readonly List<Vector4> floatBase = new List<Vector4>();   // x, y, z (block-local, relative to the hall's S), w = size
        struct CPart { public Mesh Mesh; public int Sub; public Matrix4x4 Rel; public int Cat; }
        readonly List<CPart> cubeParts = new List<CPart>();
        Texture2D MakeRuneTex()
        {
            return kit.Tex(128, 128, (x, y) =>
            {
                int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16; int bits = (int)(WorldKit.Hash(cx, cy, 91) * 63f); bool on = false;
                if ((bits & 1) != 0 && ly >= 3 && ly <= 4 && lx >= 3 && lx <= 12) on = true; if ((bits & 2) != 0 && ly >= 11 && ly <= 12 && lx >= 3 && lx <= 12) on = true;
                if ((bits & 4) != 0 && lx >= 3 && lx <= 4 && ly >= 3 && ly <= 12) on = true; if ((bits & 8) != 0 && lx >= 11 && lx <= 12 && ly >= 3 && ly <= 12) on = true;
                if ((bits & 16) != 0 && Mathf.Abs(lx - ly) <= 1 && lx >= 4 && lx <= 11) on = true; if ((bits & 32) != 0 && lx >= 7 && lx <= 8 && ly >= 3 && ly <= 12) on = true;
                bool border = x < 3 || y < 3 || x > 124 || y > 124; float v = on ? 1f : (border ? 0.7f : 0.06f); v = Mathf.Clamp01(v + 0.08f * WorldKit.Hash(x / 3, y / 3, 17)); return new Color(v, v, v, 1f);
            });
        }
        string[] OrigNames(Renderer r) { foreach (var kv in origMats) if (kv.Key == r) { var a = new string[kv.Value.Length]; for (int i = 0; i < a.Length; i++) a[i] = kv.Value[i] != null ? kv.Value[i].name : ""; return a; } return new string[0]; }
        static int CubeCat(string n) { return n.Contains("Glow") ? 2 : n.Contains("Face") ? 1 : 0; }
        void GatherCubeParts()
        {
            cubeParts.Clear(); if (cubeM == null) return; var pv = cubeM.Pivot.transform;
            foreach (var mf in cubeM.Pivot.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); if (mf.sharedMesh == null || mr == null) continue; var names = OrigNames(mr); if (names.Length == 0) continue; var rel = pv.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) cubeParts.Add(new CPart { Mesh = mf.sharedMesh, Sub = s, Rel = rel, Cat = CubeCat(names[Mathf.Min(s, names.Length - 1)]) });
            }
        }
        // a hero cube: its own materials (frame / face / glow) so it can be dormant grey, charged purple, rebooted blue or golden
        sealed class CubeObj { public Transform Tr; public readonly List<Material> Frame = new List<Material>(), Face = new List<Material>(), Glow = new List<Material>(); }
        CubeObj MakeCube(string n)
        {
            var co = new CubeObj();
            if (cubeM == null || cubeM.Pivot == null) { co.Tr = new GameObject(n).transform; co.Tr.SetParent(root.transform, false); return co; }
            var go = UnityEngine.Object.Instantiate(cubeM.Pivot, root.transform); go.name = n; go.SetActive(true); clones.Add(go); co.Tr = go.transform;
            var orig = cubeM.Pivot.GetComponentsInChildren<Renderer>(true); var cl = go.GetComponentsInChildren<Renderer>(true);
            for (int ri = 0; ri < cl.Length && ri < orig.Length; ri++)
            {
                var names = OrigNames(orig[ri]); var ms = cl[ri].sharedMaterials;
                for (int s = 0; s < ms.Length; s++) { var m = new Material(ms[s]); owned.Add(m); ms[s] = m; int cat = CubeCat(names[Mathf.Min(s, names.Length - 1)]); if (cat == 2) { m.mainTexture = runeTex; co.Glow.Add(m); } else if (cat == 1) co.Face.Add(m); else co.Frame.Add(m); }
                cl[ri].sharedMaterials = ms;
            }
            return co;
        }
        void SetCube(CubeObj c, Vector3 pos, Vector3 size, float rotY, Color core, Color aura, bool on)
        {
            if (c.Tr.gameObject.activeSelf != on) c.Tr.gameObject.SetActive(on); if (!on) return;
            float unit = cubeM != null ? Mathf.Max(0.1f, cubeM.RendererBounds.size.x) : 2f; c.Tr.localPosition = pos; c.Tr.localScale = size / unit; c.Tr.localRotation = Quaternion.Euler(0f, rotY, 0f);
            foreach (var m in c.Frame) m.color = core * 0.9f; foreach (var m in c.Face) m.color = Color.Lerp(core, aura, 0.35f) * 1.05f;
            Color gl = new Color(aura.r, aura.g, aura.b, 1f) * (0.3f + 1.5f * aura.a); foreach (var m in c.Glow) m.color = gl;
        }
        void BuildCubeHall()
        {
            runeTex = MakeRuneTex(); GatherCubeParts(); if (cubeM == null || cubeParts.Count == 0 || !bS.ContainsKey("CUBES")) { JsLiveEventsMod.Log.LogWarning("SkyFire: no Cube model (" + (cubeM != null) + ", parts " + cubeParts.Count + ")"); return; }
            rackFrame = kit.Lit(new Color(0.32f, 0.28f, 0.42f), null, 0.4f); rackFace = kit.Lit(new Color(0.4f, 0.3f, 0.6f), runeTex, 0.3f); fallGlow = kit.Flat(new Color(1f, 0.55f, 0.15f)); fallGlow.mainTexture = runeTex;
            for (int i = 0; i < 5; i++) { rackGlow[i] = kit.Flat(new Color(0.6f, 0.25f, 1f)); rackGlow[i].mainTexture = runeTex; }
            Vector3 s = bS["CUBES"]; float unit = Mathf.Max(0.1f, cubeM.RendererBounds.size.x);
            // hundreds of cubes in the rack towers: 3 columns x 6 tiers x 15 bays x 2 sides, ~12 % of the sockets empty
            var frame = new List<CombineInstance>(); var face = new List<CombineInstance>(); var glow = new List<CombineInstance>[5]; for (int i = 0; i < 5; i++) glow[i] = new List<CombineInstance>();
            int n = 0; float[] xs = { 12.75f, 19.25f, 25.75f };
            for (int side = -1; side <= 1; side += 2) for (int xi = 0; xi < 3; xi++) for (int tier = 0; tier < 6; tier++) for (int bay = 0; bay < 15; bay++)
            {
                int key = (side + 1) * 1000 + xi * 100 + tier * 20 + bay; if (WorldKit.Hash(key, 3, 5) < 0.12f) continue;
                float size = 3.0f + 1.5f * WorldKit.Hash(key, 7, 5); float y = 2.2f + tier * 6f + size * 0.5f; float z = 11f + bay * 6f; float yaw = (WorldKit.Hash(key, 11, 5) - 0.5f) * 0.6f * Mathf.Rad2Deg;
                Vector3 p = s + new Vector3(side * xs[xi], y, z - 4f) * MK; var mtx = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * (size * MK / unit)); int grp = Mathf.Clamp(bay / 3, 0, 4); n++;
                foreach (var cp in cubeParts) { var ci = new CombineInstance { mesh = cp.Mesh, subMeshIndex = cp.Sub, transform = mtx * cp.Rel }; if (cp.Cat == 0) frame.Add(ci); else if (cp.Cat == 1) face.Add(ci); else glow[grp].Add(ci); }
            }
            rackCount = n; rackGos.Clear();
            rackGos.Add(MergeGo("SRackFrame", frame, rackFrame)); rackGos.Add(MergeGo("SRackFace", face, rackFace)); for (int i = 0; i < 5; i++) rackGos.Add(MergeGo("SRackGlow" + i, glow[i], rackGlow[i]));
            foreach (var go in rackGos) go.SetActive(false);
            // floating cubes in the aisle (they move), falling cubes for the end of the fall
            floaters.Clear(); floatBase.Clear();
            for (int i = 0; i < 28; i++) { floaters.Add(CubeClone("SFloat" + i, rackFrame, rackFace, rackGlow[i % 5])); floatBase.Add(new Vector4(Mathf.Lerp(-7f, 7f, WorldKit.Hash(i, 1, 9)) * MK, Mathf.Lerp(8f, 36f, WorldKit.Hash(i, 2, 9)) * MK, Mathf.Lerp(14f, 98f, WorldKit.Hash(i, 3, 9)) * MK, (1.6f + 1.8f * WorldKit.Hash(i, 4, 9)) * MK)); }
            fallCubes.Clear(); for (int i = 0; i < 70; i++) fallCubes.Add(CubeClone("SFall" + i, rackFrame, rackFace, fallGlow));
            JsLiveEventsMod.Log.LogInfo("SkyFire: Cube hall built: " + n + " cubes in the racks, " + floaters.Count + " floating, " + fallCubes.Count + " for the fall (cube size " + unit.ToString("0.00") + ", parts " + cubeParts.Count + ")");
        }
        GameObject MergeGo(string name, List<CombineInstance> list, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; if (list.Count > 0) mesh.CombineMeshes(list.ToArray(), true, true); owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false; return go;
        }
        GameObject CubeClone(string n, Material frame, Material face, Material glow)
        {
            var go = UnityEngine.Object.Instantiate(cubeM.Pivot, root.transform); go.name = n; go.SetActive(false); clones.Add(go); var orig = cubeM.Pivot.GetComponentsInChildren<Renderer>(true); var cl = go.GetComponentsInChildren<Renderer>(true);
            for (int ri = 0; ri < cl.Length && ri < orig.Length; ri++) { var names = OrigNames(orig[ri]); var ms = cl[ri].sharedMaterials; for (int s = 0; s < ms.Length; s++) { int cat = CubeCat(names[Mathf.Min(s, names.Length - 1)]); ms[s] = cat == 2 ? glow : cat == 1 ? face : frame; } cl[ri].sharedMaterials = ms; }
            return go;
        }
        void CubePose(GameObject go, Vector3 pos, float size, Quaternion rot, bool on)
        {
            if (go.activeSelf != on) go.SetActive(on); if (!on) return; float unit = Mathf.Max(0.1f, cubeM.RendererBounds.size.x); var tr = go.transform; tr.localPosition = pos; tr.localRotation = rot; tr.localScale = Vector3.one * (size / unit);
        }

        // ---------------------------------------------------------------- the Cube hall frame (8:30 - 9:30): racks wake up, cubes float, the golden cube hangs over its dais, the backpacks go off, the hall tears apart
        void CubesFx(float t, float tp, float dt, Seg g, Vector3 headW, Vector3 floorP, Vector3 dir)
        {
            Vector3 s = bS["CUBES"]; bool boom = t >= T_ZERO; float e = Mathf.Max(0f, t - T_ZERO); HallDebris(t, dt, headW);
            foreach (var go in rackGos) if (go.activeSelf != true) go.SetActive(true);
            // the racks wake up in a wave from the dock to the far end; hot orange after the blast
            float hot = Sm(T_ZERO, T_ZERO + 7f, t);
            for (int i = 0; i < 5; i++)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(t * 1.4f - i * 1.1f); float awake = Sm(516f + i * 2.5f, 520f + i * 2.5f, t); if (t > 541f && !boom) wave = 0.5f + 0.5f * Mathf.Sin(t * 8f - i);
                Color purple = new Color(0.55f, 0.2f, 0.95f) * (0.35f + 0.5f * awake * wave); Color orange = new Color(1f, 0.45f, 0.1f) * (0.9f + 0.4f * Mathf.PerlinNoise(t * 6f, i)); rackGlow[i].color = Color.Lerp(purple, orange, hot);
            }
            // the golden cube over its dais at the far end (it can be seen from the dock, small and bright)
            float gk = Sm(511f, 516f, t); Vector3 gp = s + new Vector3(0f, 9f + 1.4f * Mathf.Sin(t * 0.9f), 100f) * MK;
            SetCube(cubeU, gp, Vector3.one * (9f * MK * Mathf.Max(0.001f, gk)), t * 14f, new Color(0.45f, 0.32f, 0.1f), new Color(1f, 0.82f, 0.3f, 0.8f + 0.2f * Mathf.Sin(t * 3f)), gk > 0.01f);
            if (!paused && dt > 0f && gk > 0.5f) for (int q = 0; q < 2; q++) Emit(fx, W(gp) + UnityEngine.Random.insideUnitSphere * 7f, Vector3.up * UnityEngine.Random.Range(1f, 4f), new Color(1f, 0.85f, 0.35f, 0.9f), UnityEngine.Random.Range(0.2f, 0.7f), UnityEngine.Random.Range(1.2f, 2.5f));
            // floating cubes drift through the aisle; the blast pulls them down
            for (int i = 0; i < floaters.Count; i++)
            {
                var fb = floatBase[i]; Vector3 p = s + new Vector3(fb.x + 2.2f * MK * Mathf.Sin(t * 0.2f + i), fb.y + 1.5f * MK * Mathf.Sin(t * 0.33f + i * 2f), fb.z - 4f * MK + 3f * MK * Mathf.Cos(t * 0.17f + i)); float fall = boom ? Mathf.Min(e * e * 6f, Mathf.Max(0f, p.y - s.y - fb.w * 0.5f)) : 0f; p.y -= fall;
                CubePose(floaters[i], p, fb.w, Quaternion.Euler(t * (7f + i % 5 * 3f) + i * 20f, t * (11f + i % 4 * 4f), t * 5f + i * 9f), true);
            }
            // the saucers drifting under the roof
            float usc = ufoM != null ? 8f / Mathf.Max(0.5f, Mathf.Max(ufoM.RendererBounds.size.x, ufoM.RendererBounds.size.z)) : 1f;
            for (int i = 0; i < saucers.Count; i++) { var u = saucers[i]; Vector3 p = s + new Vector3(Mathf.Sin(t * 0.13f + i * 1.3f) * 7f, 30f + 3f * Mathf.Sin(i + t * 0.5f), 14f + i * 14f + 6f * Mathf.Sin(t * 0.2f + i)) * MK; PlaceGo(u, p, usc * 1.4f * MK, Quaternion.Euler(4f * Mathf.Sin(t + i), t * 30f + i * 60f, 4f * Mathf.Cos(t * 0.8f + i)), true); if (!paused && dt > 0f) Emit(fx, W(p + Vector3.down * 1.4f), Vector3.zero, new Color(0.85f, 0.4f, 1f, 0.9f), 6f, 0.06f); }
            // the dock: steam and a flash when the lift arrives (8:30)
            if (t >= 510f && t < 516f && !paused && dt > 0f) for (int q = 0; q < 3; q++) Emit(dust, W(s + new Vector3(UnityEngine.Random.Range(-6f, 6f), 0.3f, UnityEngine.Random.Range(-2f, 8f)) * MK), new Vector3(0f, UnityEngine.Random.Range(2f, 5f), 0f), new Color(0.8f, 0.7f, 1f, 0.4f), UnityEngine.Random.Range(1.5f, 3f), 1.5f);
            if (tp < 510.1f && t >= 510.1f && !paused) { Sfx(sBig, headW, 0.8f, 0.7f, 1f); shake = Mathf.Max(shake, 0.7f); }
            if (t >= 524f && !paused && dt > 0f) { float k = Sm(524f, 548f, t); for (int q = 0; q < (int)(1 + 8 * k); q++) Emit(fx, headW + new Vector3(UnityEngine.Random.Range(-22f, 22f), UnityEngine.Random.Range(-1.5f, 30f), UnityEngine.Random.Range(-12f, 60f)), new Vector3(0f, UnityEngine.Random.Range(0.4f, 2.5f), 0f), new Color(0.85f, 0.4f, 1f, 0.85f), UnityEngine.Random.Range(0.12f, 0.45f), UnityEngine.Random.Range(1.5f, 3.5f)); }
            if (!boom) { if (t > 541f && !paused && Mathf.FloorToInt(t) != Mathf.FloorToInt(tp)) shake = Mathf.Max(shake, 0.12f); return; }
            // 9:11: the backpacks go off, the racks burn and rain debris until the white-out at 9:30
            shake = Mathf.Max(shake, 0.5f + 0.4f * Sm(2f, 8f, e));
            float[] bt = { 554f, 556.5f, 558.5f, 560.5f, 562.5f, 564.5f, 566.5f, 568.2f };
            for (int i = 0; i < bt.Length; i++) if (tp < bt[i] && t >= bt[i] && !paused) { Vector3 p = headW + new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(1f, 16f), UnityEngine.Random.Range(4f, 40f)); Boom(p, 12f + 1.5f * i, 1.2f, new Color(1f, 0.5f, 0.18f), true); Sfx(sBig, p, 0.9f, 1f, 0.3f); }
            if (!paused && dt > 0f) for (int q = 0; q < (int)(7 * Sm(T_ZERO, T_ZERO + 6f, t) + 1); q++) Emit(fx, headW + new Vector3(UnityEngine.Random.Range(-24f, 24f), UnityEngine.Random.Range(0f, 30f), UnityEngine.Random.Range(-4f, 60f)), new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(4f, 14f), UnityEngine.Random.Range(-3f, 3f)), new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(1.5f, 5f), UnityEngine.Random.Range(1f, 2.4f));
            if (!paused && dt > 0f && UnityEngine.Random.value < dt * 10f) Emit(dust, headW + UnityEngine.Random.insideUnitSphere * 18f, Vector3.up * 3f, new Color(0.25f, 0.18f, 0.2f, 0.6f), UnityEngine.Random.Range(6f, 14f), 4f);
        }
        // the Mothership explodes and the Cubes fall with you toward the island
        void FallCubes(float t, float tp, float dt, Vector3 an)
        {
            bool on = t >= T_FALL && t < T_LAND && fallCubes.Count > 0; float e = t - T_FALL;
            for (int i = 0; i < fallCubes.Count; i++)
            {
                if (!on) { if (fallCubes[i].activeSelf) fallCubes[i].SetActive(false); continue; }
                float h1 = Hash(i, 21), h2 = Hash(i, 22), h3 = Hash(i, 23), h4 = Hash(i, 24); float a = h1 * 6.2832f, r = 9f + 120f * h2 * h2; float rel = Mathf.Repeat(h3 * 160f - e * (10f + 12f * h4), 160f) - 70f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, an.y + rel, Mathf.Sin(a) * r + 20f); float k = Sm(0f, 2f, e); CubePose(fallCubes[i], p, (2.4f + 3.2f * h4) * k, Quaternion.Euler(e * (40f + 90f * h1) + i * 30f, e * (30f + 80f * h2), e * 25f), true);
                if (!paused && dt > 0f && i % 3 == 0 && r < 90f) { Emit(fx, W(p), Vector3.up * UnityEngine.Random.Range(6f, 16f), new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(1.2f, 3.5f), UnityEngine.Random.Range(0.6f, 1.4f)); }
            }
        }

        // ---------------------------------------------------------------- the abduction (0:05 - 0:25): the Mothership comes down over the island, its beam lifts the squad into its underside
        void AbductFx(float t, float tp, float dt, Vector3 an, Vector3 headW)
        {
            if (shipM != null)
            {
                float sz = Mathf.Max(1f, shipM.RendererBounds.size.x); float sc2 = 300f / sz; float k = Sm(5f, 15f, t); float shipY = 215f; Vector3 sp = Vector3.Lerp(new Vector3(140f, 520f, 420f), new Vector3(0f, shipY, 0f), k);
                PlaceGo(shipM.Pivot, sp, sc2, Quaternion.Euler(10f * (1f - k), t * 6f, 4f * Mathf.Sin(t * 0.7f) * (1f - k)), true);
                beam.localPosition = new Vector3(0f, shipY * 0.5f, 0f); beam.localScale = new Vector3(18f + 4f * Mathf.Sin(t * 5f), shipY * 0.5f, 18f + 4f * Mathf.Sin(t * 5f)); beamMat.color = new Color(0.35f, 0.85f, 1f, 0.55f * Sm(9f, 12f, t));
                float usc = ufoM != null ? 22f / Mathf.Max(0.5f, Mathf.Max(ufoM.RendererBounds.size.x, ufoM.RendererBounds.size.z)) : 1f;
                for (int i = 0; i < saucers.Count; i++) { float a = t * 0.35f + i * 1.0472f; Vector3 p = sp + new Vector3(Mathf.Cos(a) * 190f, 12f * Mathf.Sin(t + i), Mathf.Sin(a) * 190f); PlaceGo(saucers[i], p, usc, Quaternion.Euler(6f * Mathf.Sin(t + i), t * 40f, 0f), true); if (!paused && dt > 0f && i % 2 == 0) Emit(fx, W(p + Vector3.down * 3f), Vector3.zero, new Color(0.5f, 0.95f, 1f, 0.9f), 9f, 0.06f); }
                if (!paused && dt > 0f && t > 11f) { Emit(fx, W(new Vector3(UnityEngine.Random.Range(-9f, 9f), shipY - 2f, UnityEngine.Random.Range(-9f, 9f))), Vector3.down * UnityEngine.Random.Range(8f, 22f), new Color(0.6f, 1f, 1f, 0.9f), UnityEngine.Random.Range(0.4f, 1.1f), 0.9f); }
                if (tp < 8f && t >= 8f && !paused) Sfx(sBig, W(sp), 1f, 0.5f, 1f); if (tp < 12f && t >= 12f && !paused) Sfx(sWhoosh, W(Vector3.up * 40f), 1f, 0.5f, 1f);
            }
        }

        // ---------------------------------------------------------------- the squad, second pass: stop-and-go routes, cells, cues
        static readonly string[] CELL_CLIPS = { "Shove", "Panic", "Beckon", "Look", "Shove", "Plead" };
        void SquadFrame(float t, float tp, float dt, Seg g, Vector3 an, Vector3 floorP, Vector3 dir)
        {
            Sc sc = g.S; bool show = sc != Sc.Black && !(sc == Sc.Fall && t >= T_LAND);
            float rx, rz, rx2, rz2; bool routed = RouteXZ(g, t, out rx, out rz); RouteXZ(g, t - 0.2f, out rx2, out rz2); float sp = routed ? Mathf.Sqrt((rx - rx2) * (rx - rx2) + (rz - rz2) * (rz - rz2)) / 0.2f : 0f;
            Vector3 hd = dir; if (routed && sp > 0.3f) { hd = new Vector3(rx - rx2, 0f, rz - rz2).normalized; } Vector3 right = Vector3.Cross(Vector3.up, hd).normalized; Vector3 S0 = g.Blk != null && bS.ContainsKey(g.Blk) ? bS[g.Blk] : Vector3.zero;
            bool walking = routed ? sp > 0.35f : (g.Tw > 2f && (t - g.T0) < g.Tw); float speed = routed ? sp : (walking ? Vector3.Distance(bS.ContainsKey(g.Blk ?? "") ? bS[g.Blk] : Vector3.zero, bE.ContainsKey(g.Blk ?? "") ? bE[g.Blk] : Vector3.zero) / Mathf.Max(1f, g.Tw) : 0f);
            bool belt = sc == Sc.Walk && t > 118f && t < 133f;
            for (int i = 0; i < squad.Count; i++)
            {
                var m = squad[i]; if (m.Go.activeSelf != show) m.Go.SetActive(show); if (!show) { m.LastT = -9f; continue; }
                Vector3 pos; float yawD = Mathf.Atan2(hd.x, hd.z) * Mathf.Rad2Deg; string clip = "Idle"; float ct = t + m.Seed; bool tumble = false;
                if (sc == Sc.Abduct) { float a = i * 1.05f + t * 0.45f, r = 3.2f + 0.7f * (i % 3); pos = new Vector3(Mathf.Cos(a) * r, an.y - 1.7f + 0.5f * Mathf.Sin(t * 1.3f + i), Mathf.Sin(a) * r); yawD = a * Mathf.Rad2Deg + 90f; clip = "Fall"; ct = t * 0.8f + m.Seed; tumble = true; }
                else if (sc == Sc.Fall) { float a = i * 1.05f + t * 0.35f, r = 4.5f + 1.6f * (i % 3); pos = new Vector3(Mathf.Cos(a) * r, an.y - 1.2f + 1.8f * Mathf.Sin(t * 0.9f + i * 1.7f), Mathf.Sin(a) * r); yawD = i * 55f + t * 25f; clip = i % 2 == 0 ? "Fall" : (i % 4 == 1 ? "FallBumpR" : "FallBumpL"); ct = t * 0.9f + m.Seed; tumble = true; }
                else if (sc == Sc.Tube) { pos = floorP + hd * m.Ahead + right * m.Lat * 0.4f + Vector3.up * (0.2f + 0.3f * Mathf.Sin(t * 1.6f + i)); clip = "Fall"; ct = t * 0.7f + m.Seed; }
                else
                {
                    pos = floorP + hd * m.Ahead + right * m.Lat; if (sc == Sc.View || sc == Sc.Purple || sc == Sc.Blue || sc == Sc.Lift) pos += hd * 1.0f;
                    bool inCell = sc == Sc.Cells && t < 36.4f + 0.3f * Mathf.Abs(i - 2) + 0.2f;
                    if (sc == Sc.Cells)
                    {
                        Vector3 cellP = S0 + new Vector3(cellRelX[Mathf.Clamp(i, 0, 5)] + (i == 2 ? 0.9f : 0f), 0f, (-5.0f + 0.5f * (i % 2)) * MK); float rel = BarrierDrop(i);
                        if (t < rel) { pos = cellP; yawD = 8f * Mathf.Sin(i * 3f + t * 0.5f); clip = CELL_CLIPS[i]; ct = t * 0.9f + m.Seed; }
                        else { float u = Mathf.Clamp01((t - rel - 0.2f) / 2.6f); pos = Vector3.Lerp(cellP, pos, u * u * (3f - 2f * u)); Vector3 dv = pos - cellP; if (u < 1f) { yawD = Mathf.Atan2(dv.x, Mathf.Max(0.5f, dv.z)) * Mathf.Rad2Deg; clip = "Run"; float per, str; GaitOf("Run", out per, out str); ct = (t * 3.4f + i * 0.7f) / str * per; } }
                    }
                    if (inCell) { }
                    else if (belt) { clip = i % 2 == 0 ? "Idle" : "Look"; ct = t + m.Seed; yawD += 8f * Mathf.Sin(t * 0.4f + i); }
                    else if (walking) { float v = Mathf.Max(0.6f, speed); clip = v > 5.5f ? "Run" : v > 2.8f ? "Jog" : "Walk"; float per, str; GaitOf(clip, out per, out str); ct = (v * t + i * 0.7f) / str * per; }
                    else { Cue(i, t, sc, out clip, out ct); ct += m.Seed; yawD += 8f * Mathf.Sin(t * 0.25f + i); }
                    if (sc == Sc.Crystal && t >= T_ZERO + 0.25f * i)
                    {
                        float e = t - (T_ZERO + 0.25f * i); Vector3 v0 = new Vector3(Mathf.Sin(i * 2.1f) * 5f, 7f, -3.5f - 0.6f * i); float em = Mathf.Min(e, 1.4f); pos += v0 * em - Vector3.up * 4.9f * em * em; pos.y = Mathf.Max(floorP.y, pos.y); clip = e < 1.4f ? "Stagger" : "Panic"; ct = e;
                    }
                }
                if (oneShot.Contains(clip)) { var cc = FindClip(m.M, "H_" + clip); if (cc != null) ct = Mathf.Clamp(ct, 0f, cc.Duration - 0.05f); }
                bool scrub = Mathf.Abs(t - m.LastT) > 0.5f || dt <= 0f || m.LastT < -8f;
                if (m.Cur != clip) { if (m.Cur != null && !scrub) { m.Prev = m.Cur; m.PrevT = m.CurT; m.Blend = 0f; } else { m.Prev = null; m.Blend = 1f; } m.Cur = clip; }
                if (m.Prev != null) { m.PrevT += dt; m.Blend = Mathf.Min(1f, m.Blend + dt / 0.28f); if (m.Blend >= 1f) m.Prev = null; }
                m.CurT = ct; m.LastT = t;
                if (m.Prev != null) PlayClip(m.Rig, m.M, "H_" + m.Prev, m.PrevT, "H_" + clip, ct, Sm(0f, 1f, m.Blend)); else PlayClip(m.Rig, m.M, "H_" + clip, ct);
                var rot = Quaternion.Euler(tumble ? 18f * Mathf.Sin(t * 0.8f + i * 2f) : 0f, yawD, tumble ? 14f * Mathf.Cos(t * 0.7f + i) : 0f);
                var tr = m.Go.transform; tr.localPosition = pos; tr.localRotation = rot; tr.localScale = Vector3.one;
                if (m.Timer != null)
                {
                    bool on = t >= TT[0] - 0.5f && t < T_ZERO + 0.25f * i + 0.5f; string txt = on ? TimerText(t) : ""; if (m.Timer.text != txt) m.Timer.text = txt;
                    if (m.Pack != null && t >= T_ZERO + 0.25f * i && t < T_ZERO + 0.25f * i + 0.12f && !paused && tp < T_ZERO + 0.25f * i) Boom(m.Pack.transform.position, 6f, 0.9f, new Color(1f, 0.5f, 0.2f), true);
                }
            }
        }
        // what the squad does while standing (a different action for each member, tied to the scene)
        void Cue(int i, float t, Sc sc, out string clip, out float ct)
        {
            clip = "Idle"; ct = t;
            switch (sc)
            {
                case Sc.Cells: clip = i % 2 == 0 ? "Look" : "Talk"; break;
                case Sc.Hall: clip = (t > 70f && t < 75f) || (t > 81f && t < 87f) ? (i % 2 == 0 ? "Look" : "Point") : (t > 106f ? (i % 3 == 0 ? "Cheer" : "Look") : "Look"); break;
                case Sc.Walk: clip = t > 143f ? (i % 2 == 0 ? "Point" : (i % 3 == 1 ? "Talk" : "Look")) : "Idle"; break;
                case Sc.Rooms: clip = t < 184f ? "Look" : (t > 203f && t < 209f ? (i % 2 == 0 ? "Beckon" : "Look") : (i % 2 == 0 ? "Point" : "Look")); break;
                case Sc.View: { string[] c = { "Look", "Point", "Talk", "Shrug", "Look", "Talk" }; clip = c[i % 6]; if (t > 360f && t < 410f && i % 3 == 0) clip = "Brace"; break; }
                case Sc.Purple: clip = t < 330f ? (i % 2 == 0 ? "Look" : "Idle") : (t < 356f ? (i % 2 == 0 ? "Brace" : "Shield") : "Panic"); break;
                case Sc.Blue: clip = t >= 423f && t < 428f ? (i == 0 ? "Lever" : "Look") : (t >= 428f && t < 440f ? (i % 2 == 0 ? "Cheer" : "Fistbump") : (t > 480f ? "Brace" : "Look")); break;
                case Sc.Lift: clip = i % 2 == 0 ? "Brace" : "Crouch"; break;
                case Sc.Crystal: clip = t < 524f ? (i % 2 == 0 ? "Look" : "Point") : (t < 541f ? (i % 3 == 0 ? "Panic" : "Look") : "Panic"); ct = t - 541f; break;
            }
        }
    }
}
