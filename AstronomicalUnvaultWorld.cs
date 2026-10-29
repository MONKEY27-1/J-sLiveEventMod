using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // The Unvaulting: the world pieces (see AstronomicalUnvault.cs for the show). Two kinds of places:
    //  - in the real Forest (0:15 - 0:55 the Vault site, 4:26 - 5:40 the volcano, the falling rocks, Tilted Towers and Retail Row): props standing on the Forest's real floor, in a
    //    scaled arena fitted onto the floor (design units: x right, z forward, 1 unit = `sc` metres), the volcano and mountains far away at full size;
    //  - the voting area (0:55 - 4:26): the only place that is not in the map. The Forest is switched off there and a glass world with its own colliders stands round the player (metres).
    // The set pieces are Blender models (tools/make_site.py, make_buildings.py, make_rim.py, make_volcano.py): named objects inside one glb each, cloned by name.
    internal sealed partial class AstronomicalUnvault
    {
        // ---- design layout (arena units)
        static readonly Vector3 SITE = new Vector3(0f, 0f, 62f), TILTED = new Vector3(0f, 0f, 130f), RETAIL = new Vector3(-110f, 0f, 110f), ROCK_A = new Vector3(-140f, 0f, 200f);
        const string ARENA_JSON = "{\"pts\":[[0,0],[0,62],[36,62],[-36,62],[0,98],[0,26],[50,62],[-50,62],[0,112],[0,12],[0,130],[60,130],[-60,130],[0,190],[-110,110],[-74,110],[-146,110],[-110,146],[-110,74],[-140,200]],\"margin\":16}";
        // the volcano and the mountains stand far away, in full-size metres round the arena origin; the volcano model is R = 120, rim H = 105 and is scaled up
        static readonly Vector3 VOLC = new Vector3(50f, 0f, 290f); const float VOLC_SCALE = 1.4f, VOLC_H = 98f * VOLC_SCALE;
        // glass world (metres), centred on where the player stood when the voting started
        static readonly Vector3 DC = new Vector3(0f, 0f, 18f); const float RING = 26f, ZP_R = 7f;

        WorldKit kit; Transform farT; GameObject gV, gW, gI, mountains, volcano; Light volcLight, vaultLight; GltfModel vault, volcModel; readonly List<Material> lavaMats = new List<Material>();
        sealed class Building { public Transform T; public Vector3 Size; public float FallT = 9999f; public float Lean; public BoxCollider Col; public float Y0; }
        readonly List<Building> buildings = new List<Building>();
        float sc = 1f;                                  // metres per design unit in the Forest phases

        static float Sm(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

        // the layer mask the player's hands / body collide with
        static int LocoMask() { try { return GorillaLocomotion.GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        static int FirstLayer(int mask) { for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i; return 0; }
        // height of the real floor under a design point, in the arena's own coordinates
        float GroundY(Vector3 local)
        {
            RaycastHit h; var w = root.transform.TransformPoint(new Vector3(local.x, 0f, local.z)); w.y = origin.y + 6f;          // start just above the floor level of the arena: tree tops / roofs higher up are not the floor
            if (Physics.Raycast(w, Vector3.down, out h, 60f, LocoMask(), QueryTriggerInteraction.Ignore)) return root.transform.InverseTransformPoint(h.point).y; return 0f;
        }
        Texture2D lineTexCache; Texture2D LineTex() { if (lineTexCache == null) lineTexCache = kit.Tex(4, 32, (x, y) => { float v = Mathf.Abs((y + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp); return lineTexCache; }

        // a clone of a named object of a Blender prop file, stood on the floor at `pos` (arena units) with a yaw; colliders from the mesh bounds (box) or the mesh itself
        GameObject CloneNode(string propId, string node, Transform parent, Vector3 pos, float yaw, float scale, bool box, bool meshCollider)
        {
            var m = def.Prop(propId); if (m == null || m.Pivot == null) return null; Transform src = null;
            foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == node) { src = t; break; }
            if (src == null) return null;
            var g = UnityEngine.Object.Instantiate(src.gameObject); g.name = node; g.SetActive(true); g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, yaw, 0) * src.localRotation; g.transform.localScale = src.localScale * scale;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = kit.Layer;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            if (meshCollider) { foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; } }
            else if (box) { var b = LocalBounds(g.transform); if (b.size.sqrMagnitude > 0f) { var bc = g.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; } }
            return g;
        }
        static Bounds LocalBounds(Transform root0)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in root0.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue; var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++) { var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1)); var p = root0.InverseTransformPoint(mf.transform.TransformPoint(c)); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            }
            return b;
        }
        // a prop clone on the real floor: `at` is a design point (y ignored)
        GameObject Put(string propId, string node, Transform parent, Vector3 at, float yaw, float scale = 1f, bool box = true)
        {
            var g = CloneNode(propId, node, parent, new Vector3(at.x, 0f, at.z), yaw, scale, box, false); if (g != null) g.transform.localPosition = new Vector3(at.x, GroundY(at) - 0.05f, at.z); return g;
        }

        // ---------------------------------------------------------------- 0:15 - 0:55: the Vault site, in the Forest
        void BuildVaultWorld()
        {
            gV = new GameObject("UV_Vault"); gV.transform.SetParent(root.transform, false);
            var vm = def.Prop("vault"); float vk = 4.7f; float gy = GroundY(SITE); Vector3 vp = new Vector3(SITE.x, gy, SITE.z);
            if (vm != null && vm.Pivot != null)
            {
                vault = vm; vm.Pivot.transform.SetParent(gV.transform, false); vm.Pivot.SetActive(true); vm.Pivot.transform.localScale = Vector3.one * vk; vm.Pivot.transform.localPosition = vp;
                foreach (var r in vm.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; if (r.name == "ShaftGlow") r.sharedMaterial.renderQueue = 3000; }
                var rim = new GameObject("VaultCollider"); rim.layer = kit.Layer; rim.transform.SetParent(gV.transform, false); rim.transform.localPosition = vp + new Vector3(0, vk * 0.5f, 0); var rc = rim.AddComponent<BoxCollider>(); rc.size = new Vector3(vk * 5.6f, vk, vk * 5.6f);
            }
            var bg = new GameObject("VaultBeam"); bg.transform.SetParent(gV.transform, false); var bl = bg.AddComponent<LineRenderer>(); bl.useWorldSpace = false; bl.positionCount = 2; bl.SetPosition(0, vp + Vector3.up * 8f); bl.SetPosition(1, vp + Vector3.up * 420f); bl.widthMultiplier = 9f;
            bl.sharedMaterial = kit.Glow(new Color(0.6f, 0.95f, 1f, 0.85f), LineTex(), 3001); bl.shadowCastingMode = ShadowCastingMode.Off; var bg2 = new GameObject("VaultBeamGlow"); bg2.transform.SetParent(gV.transform, false); var bl2 = bg2.AddComponent<LineRenderer>(); bl2.useWorldSpace = false; bl2.positionCount = 2; bl2.SetPosition(0, vp); bl2.SetPosition(1, vp + Vector3.up * 420f); bl2.widthMultiplier = 34f; bl2.sharedMaterial = kit.Glow(new Color(0.4f, 0.8f, 1f, 0.28f), LineTex(), 3000); bl2.shadowCastingMode = ShadowCastingMode.Off;
            vaultLight = new GameObject("VaultLight").AddComponent<Light>(); vaultLight.transform.SetParent(gV.transform, false); vaultLight.transform.localPosition = vp + Vector3.up * 14f; vaultLight.type = LightType.Point; vaultLight.color = new Color(0.55f, 0.9f, 1f); vaultLight.range = 90f * sc + 25f; vaultLight.intensity = 5f; vaultLight.shadows = LightShadows.None;
            // the pit: a terraced cliff ring with three ramps (Blender model), solid
            var rimG = CloneNode("rim", "PitRim", gV.transform, new Vector3(SITE.x, gy - 0.05f, SITE.z), 0f, 0.72f, false, true);
            // the site: containers, bulldozers, a truck, the cyan dome building, boulders (with teal crystals), poles, barrels, crates, cones (all Blender models)
            string[] cn = { "ContainerRed", "ContainerBlue", "ContainerGrey", "ContainerOrange" };
            Vector3[] cp = { new Vector3(-22, 0, 52), new Vector3(-22, 0, 62), new Vector3(-24, 0, 72), new Vector3(24, 0, 50), new Vector3(23, 0, 80), new Vector3(0, 0, 36), new Vector3(0, 0, 90), new Vector3(52, 0, 36), new Vector3(60, 0, 36), new Vector3(-58, 0, 92), new Vector3(-52, 0, 96) };
            float[] cy = { 10, 80, -20, 95, 65, 3, 170, 20, 24, -12, 40 };
            for (int k = 0; k < cp.Length; k++) Put("site", cn[k % 4], gV.transform, cp[k], cy[k]);
            var st = Put("site", "ContainerRed", gV.transform, new Vector3(52, 0, 36), 20f); if (st != null) { var up2 = Put("site", "ContainerGrey", gV.transform, new Vector3(52, 0, 36), 20f); if (up2 != null) up2.transform.localPosition += Vector3.up * 2.6f; }
            Put("site", "Bulldozer", gV.transform, new Vector3(-14, 0, 78), 70f); Put("site", "Bulldozer", gV.transform, new Vector3(14, 0, 82), -40f); Put("site", "Bulldozer", gV.transform, new Vector3(10, 0, 44), 160f); Put("site", "Bulldozer", gV.transform, new Vector3(-30, 0, 90), 20f);
            Put("site", "DumpTruck", gV.transform, new Vector3(36, 0, 66), 100f); Put("site", "DumpTruck", gV.transform, new Vector3(-36, 0, 58), -80f);
            Put("site", "DomeBuilding", gV.transform, new Vector3(-62, 0, 52), 90f);
            var rnd = new System.Random(5);
            for (int k = 0; k < 14; k++) { float a = (float)rnd.NextDouble() * 6.2832f, d = 14f + (float)rnd.NextDouble() * 52f; var p = new Vector3(SITE.x + Mathf.Cos(a) * d, 0, SITE.z + Mathf.Sin(a) * d); if (d < 18f) continue; Put("site", "Boulder" + (k % 4), gV.transform, p, (float)rnd.NextDouble() * 360f, 0.8f + (float)rnd.NextDouble() * 0.9f); }
            for (int k = 0; k < 7; k++) { float a = k * 0.9f; Put("site", "Pole", gV.transform, new Vector3(SITE.x + Mathf.Cos(a) * 30f, 0, SITE.z + Mathf.Sin(a) * 30f), a * 57f, 1f, false); }
            for (int k = 0; k < 10; k++) { var p = new Vector3(SITE.x + (float)(rnd.NextDouble() - 0.5) * 50f, 0, SITE.z + (float)(rnd.NextDouble() - 0.5) * 50f); Put("site", k % 3 == 0 ? "Crate" : k % 3 == 1 ? "Barrel" : "Cone", gV.transform, p, (float)rnd.NextDouble() * 360f); }
        }

        // ---------------------------------------------------------------- 0:55 - 4:26: the voting area (the only place that is not in the map)
        sealed class Pillar { public Transform T, Slab, ItemHolder; public GameObject Item; public Material SlabMat, CoreMat, LightMat; public Transform ShardRoot; public Transform[] Shards; public Vector3[] ShardP, ShardV, ShardSpin; public Quaternion[] ShardR; public LineRenderer[] Cracks; public Vector3[][] CrackPts; public float Flash; public Vector3 ItemPos; public float Scale; public Quaternion Align; }
        readonly Pillar[] pillars = new Pillar[6]; GameObject rootW; readonly GltfModel[] weaponModels = new GltfModel[6]; Transform zpAnchor;
        static readonly string[] NAMES = { "ZAPOTRON", "INFINITY BLADE", "SCAR", "X-4 STORMWING", "TACTICAL SMG", "DRUM GUN" };

        void BuildVoteWorld()
        {
            rootW = new GameObject("UvGlass"); gW = rootW; var wt = rootW.transform;
            var tile = kit.Tex(128, 128, (x, y) => { float lx = Mathf.Min(x, 127 - x) / 127f, ly = Mathf.Min(y, 127 - y) / 127f; float edge = (lx < 0.02f || ly < 0.02f) ? 0.78f : 1f; float n = WorldKit.Hash(x / 16, y / 16, 3) * 0.05f; return new Color((0.84f + n) * edge, (0.82f + n) * edge, 0.97f * edge, 1f); });
            kit.Obj("GlassFloor", wt, kit.Box(new Vector3(260f, 1f, 260f), 8f), kit.Lit(Color.white, tile, 0.7f), new Vector3(0, -1f, 60f), Quaternion.identity, Vector3.one);
            kit.BoxCol("FloorCollider", wt, new Vector3(0, -1.5f, 60f), new Vector3(260f, 3f, 260f), Quaternion.identity);
            // the Zero Point is the sphere in the middle (see BuildZeroPoint), on a low glowing dais
            kit.Obj("Dais", wt, kit.Frustum(12f, 11f, 0.6f, 40), kit.Lit(new Color(0.78f, 0.84f, 1f), null, 0.6f), DC, Quaternion.identity, Vector3.one);
            var za = new GameObject("ZpAnchor"); za.transform.SetParent(wt, false); za.transform.localPosition = DC + new Vector3(0f, ZP_R + 3.5f, 0f); zpAnchor = za.transform; var zc = za.AddComponent<SphereCollider>(); zc.radius = ZP_R * 0.95f;
            var slabMesh = kit.Box(new Vector3(4f, 22f, 4f), 4f); var coreMesh = kit.Box(new Vector3(3.2f, 21f, 3.2f), 4f);
            var gradTex = kit.Tex(4, 64, (x, y) => { float v = y / 63f; return Color.Lerp(new Color(1f, 0.55f, 0.95f, 0.55f), new Color(0.65f, 0.8f, 1f, 0.22f), Mathf.Pow(v, 0.6f)); }, TextureWrapMode.Clamp);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad; Vector3 pp = DC + new Vector3(Mathf.Sin(a) * RING, 0f, Mathf.Cos(a) * RING); var P = new Pillar(); var go = new GameObject("Pillar" + i); go.transform.SetParent(wt, false); go.transform.localPosition = pp; go.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg + 180f, 0); P.T = go.transform;
                if (!MakePillarModel(P, go.transform, i))
                {   // fallback if the model is missing: plain glass boxes
                    P.SlabMat = kit.Glow(Color.white, gradTex, 3001); P.Slab = kit.Obj("Slab", go.transform, slabMesh, P.SlabMat, Vector3.zero, Quaternion.identity, Vector3.one).transform;
                    P.CoreMat = kit.Glow(new Color(1f, 0.7f, 1f, 0.16f), null, 3002); kit.Obj("Core", go.transform, coreMesh, P.CoreMat, new Vector3(0, 0.3f, 0), Quaternion.identity, Vector3.one);
                }
                var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 11f, 0); col.size = new Vector3(4f, 22f, 4f);
                P.ItemPos = pp + new Vector3(0, 2.5f, 0); P.Cracks = new LineRenderer[7]; P.CrackPts = new Vector3[7][]; var rnd = new System.Random(100 + i);
                for (int c = 0; c < 7; c++)
                {
                    var pts = new Vector3[9]; int face = c % 4; float h0 = 1.2f + (float)rnd.NextDouble() * 2.6f; Vector3 o, u; switch (face) { case 0: o = new Vector3(-2.05f, 0, -1.9f); u = new Vector3(0, 0, 1); break; case 1: o = new Vector3(2.05f, 0, -1.9f); u = new Vector3(0, 0, 1); break; case 2: o = new Vector3(-1.9f, 0, -2.05f); u = new Vector3(1, 0, 0); break; default: o = new Vector3(-1.9f, 0, 2.05f); u = new Vector3(1, 0, 0); break; }
                    for (int k = 0; k < 9; k++) pts[k] = o + u * (k * 3.8f / 8f) + new Vector3(0, h0 + (float)(rnd.NextDouble() - 0.5) * 1.8f + k * 0.12f * ((c & 1) == 0 ? 1 : -1), 0);
                    P.CrackPts[c] = pts; var lg = new GameObject("Crack"); lg.transform.SetParent(go.transform, false); var lr = lg.AddComponent<LineRenderer>(); lr.useWorldSpace = false; lr.positionCount = 2; lr.widthMultiplier = 0.09f; lr.sharedMaterial = kit.Glow(new Color(1f, 0.95f, 1f, 0.95f), null, 3003); lr.shadowCastingMode = ShadowCastingMode.Off; lr.enabled = false; P.Cracks[c] = lr;
                }
                var wm = def.Prop("w" + i);
                if (wm != null && wm.Pivot != null)
                {
                    var b = wm.RendererBounds.size; float k = 2.9f / Mathf.Max(0.01f, Mathf.Max(b.x, b.y, b.z)); int ax = b.x >= b.y && b.x >= b.z ? 0 : b.y >= b.z ? 1 : 2; P.Align = i == 1 ? Quaternion.identity : ax == 2 ? Quaternion.Euler(0, 90f, 0) : ax == 1 ? Quaternion.Euler(0, 0, 90f) : Quaternion.identity; P.Scale = k;
                    var h = new GameObject("ItemHolder" + i); h.transform.SetParent(wt, false); h.transform.localPosition = P.ItemPos; P.ItemHolder = h.transform; wm.Pivot.transform.SetParent(h.transform, false); wm.Pivot.transform.localRotation = P.Align; wm.Pivot.transform.localScale = Vector3.one * k;
                    wm.Pivot.transform.localPosition = -(P.Align * Vector3.up) * (b.y * 0.5f * k); wm.Pivot.SetActive(true); P.Item = wm.Pivot; weaponModels[i] = wm;
                    foreach (var r in wm.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; }
                }
                pillars[i] = P;
            }
        }

        // the glass pillar (Blender model, tools/make_pillar.py): own material instances per pillar so each one can flash / fade / shatter on its own
        bool MakePillarModel(Pillar P, Transform parent, int idx)
        {
            var pm = def.Prop("pillar"); if (pm == null || pm.Pivot == null) return false; Transform src = null;
            foreach (var t in pm.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == "Pillar") { src = t; break; }
            if (src == null) return false;
            var g = UnityEngine.Object.Instantiate(src.gameObject); g.name = "PillarModel"; g.SetActive(true); g.transform.SetParent(parent, false); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; var m = r.material; kit.Owned.Add(m);
                if (r.name == "Glass") { P.SlabMat = m; m.renderQueue = 3001; } else if (r.name == "GlassInner") { P.CoreMat = m; m.renderQueue = 3002; } else if (r.name == "Light") { P.LightMat = m; m.renderQueue = 3000; } else if (r.name.StartsWith("Shard")) m.renderQueue = 3001;
            }
            P.Slab = null; foreach (var t in g.GetComponentsInChildren<Transform>(true)) { if (t.name == "GlassGroup") P.Slab = t; if (t.name == "Shards") P.ShardRoot = t; }
            if (P.ShardRoot != null)
            {
                int n = P.ShardRoot.childCount; P.Shards = new Transform[n]; P.ShardP = new Vector3[n]; P.ShardV = new Vector3[n]; P.ShardSpin = new Vector3[n]; P.ShardR = new Quaternion[n]; var rnd = new System.Random(300 + idx);
                for (int k = 0; k < n; k++)
                {
                    var c = P.ShardRoot.GetChild(k); P.Shards[k] = c; P.ShardP[k] = c.localPosition; P.ShardR[k] = c.localRotation; Vector3 outd = new Vector3(c.localPosition.x, 0f, c.localPosition.z); if (outd.sqrMagnitude < 0.01f) outd = Vector3.right; outd.Normalize();
                    P.ShardV[k] = outd * (5f + (float)rnd.NextDouble() * 9f) + Vector3.up * (2f + (float)rnd.NextDouble() * 9f); P.ShardSpin[k] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 540f;
                }
                P.ShardRoot.gameObject.SetActive(false);
            }
            return P.Slab != null && P.SlabMat != null;
        }

        // ---------------------------------------------------------------- 4:26 - 5:40: Tilted Towers and Retail Row, in the Forest (Blender buildings)
        void AddBuilding(GameObject g)
        {
            if (g == null) return; var b = LocalBounds(g.transform); var col = g.GetComponent<BoxCollider>(); buildings.Add(new Building { T = g.transform, Size = b.size, Col = col, Y0 = g.transform.localPosition.y });
        }
        void BuildIslandWorld()
        {
            gI = new GameObject("UV_Island"); gI.transform.SetParent(root.transform, false); var rnd = new System.Random(77); int bi = 0;
            for (int ix = -2; ix <= 2; ix++) for (int iz = -2; iz <= 1; iz++)
                {
                    if (rnd.NextDouble() < 0.12) continue; var pos = new Vector3(TILTED.x + ix * 28f + (float)(rnd.NextDouble() - 0.5) * 3f, 0f, TILTED.z + iz * 28f + 14f + (float)(rnd.NextDouble() - 0.5) * 3f);
                    int v = (ix == 0 && iz == 0) ? 4 : (ix * 3 + iz * 5 + 20 + bi++) % 6; AddBuilding(Put("buildings", "Bld" + v, gI.transform, pos, 90f * ((ix + iz + 8) % 4)));
                }
            for (int k = 0; k < 7; k++) { var pos = new Vector3(RETAIL.x - 36f + k * 12f, 0f, RETAIL.z + ((k % 2) * 22f - 11f)); AddBuilding(Put("buildings", "Shop" + (k % 3), gI.transform, pos, (k % 2) * 180f)); }
        }

        // ---------------------------------------------------------------- mountains and the volcano: far away, full size (Blender model)
        void BuildMountainsAndVolcano()
        {
            mountains = new GameObject("UV_Mountains"); mountains.transform.SetParent(farT, false); var parts = new List<CombineInstance>(); var rnd = new System.Random(5);
            for (int k = 0; k < 11; k++)
            {
                float a = (k / 11f) * 6.2832f + (float)rnd.NextDouble() * 0.3f, r = 360f + (float)rnd.NextDouble() * 40f, h = 40f + (float)rnd.NextDouble() * 60f, w = 100f + (float)rnd.NextDouble() * 60f; var m = kit.Frustum(w, w * 0.12f, h, 14, 0.35f, k + 3, 6);
                parts.Add(WorldKit.Part(m, new Vector3(Mathf.Sin(a) * r, -6f, Mathf.Cos(a) * r), Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0), Vector3.one));
            }
            var mt = kit.Tex(16, 64, (x, y) => { float v = y / 63f; Color low = new Color(0.30f, 0.42f, 0.34f), mid = new Color(0.42f, 0.40f, 0.52f), snow = new Color(0.92f, 0.92f, 1f); return v < 0.5f ? Color.Lerp(low, mid, v * 2f) : Color.Lerp(mid, snow, Mathf.Clamp01((v - 0.7f) * 4f)); }, TextureWrapMode.Clamp);
            kit.Obj("Mountains", mountains.transform, kit.Combine(parts), kit.Lit(Color.white, mt, 0.02f), Vector3.zero, Quaternion.identity, Vector3.one);
            volcano = new GameObject("UV_Volcano"); volcano.transform.SetParent(farT, false); volcano.transform.localPosition = VOLC;
            Vector3 toOrigin = -VOLC; toOrigin.y = 0f; volcano.transform.localRotation = Quaternion.LookRotation(toOrigin.normalized, Vector3.up);          // the canyon (model +Z) faces the arena
            volcModel = def.Prop("volcano");
            if (volcModel != null && volcModel.Pivot != null)
            {
                volcModel.Pivot.transform.SetParent(volcano.transform, false); volcModel.Pivot.SetActive(true); volcModel.Pivot.transform.localScale = Vector3.one * VOLC_SCALE; volcModel.Pivot.transform.localPosition = new Vector3(0, -1f, 0);
                foreach (var r in volcModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.name.StartsWith("Lava") || r.name.StartsWith("Crater")) { r.sharedMaterial.renderQueue = 3000; if (!lavaMats.Contains(r.sharedMaterial)) lavaMats.Add(r.sharedMaterial); }
                }
            }
            else { volcano.transform.localPosition = VOLC; kit.Obj("VolcanoCone", volcano.transform, kit.Frustum(160f, 26f, 150f, 44, 0.22f, 9, 16), kit.Lit(new Color(0.3f, 0.25f, 0.25f), null, 0.02f), Vector3.zero, Quaternion.identity, Vector3.one); }
            volcLight = new GameObject("VolcanoLight").AddComponent<Light>(); volcLight.transform.SetParent(volcano.transform, false); volcLight.transform.localPosition = new Vector3(0, VOLC_H + 20f, 0); volcLight.type = LightType.Point; volcLight.color = new Color(1f, 0.5f, 0.2f); volcLight.range = 380f; volcLight.intensity = 0f; volcLight.shadows = LightShadows.None;
        }
    }
}
