using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "The Cube Sinking" (Fortnite Chapter 1 Season 5, 19 September 2018), on the Beach map: the cube sits at the end of a dock; at 0:11 it tips over, rolls off the dock and hits the water at 0:15; from 0:18 to 1:25 it
    // dissolves slowly while the water turns more and more purple; at 1:25 the lake goes normal for a second, then turns purple in a burst of light (fully purple at 1:29) and stays purple and BOUNCY for the rest of the
    // audio and afterwards (until the map is left). The real water of the map is used: the nearest water volume to where you stand when you start (the map's water triggers are switched off while the lake is purple, so you
    // bounce off the surface instead of swimming). Without any water volume a small pond is built.
    internal sealed class AstronomicalCubeLake : IEventScene, IDebug
    {
        const float T_TIP = 11f, T_HIT = 15f, T_DISS0 = 18f, T_DISS1 = 85f, T_NORMAL = 86f, T_FULL = 89f, CUBE = 5f;
        // ---- the persistent purple lake
        static GameObject lakeRoot; static WorldKit lakeKit; static Material overMat; static Mesh overMesh; static Vector2[] uv0, uvb; static Bounds waterB; static float surfY; static readonly List<Collider> volumes = new List<Collider>(); static float persistT, uvT, lastBounce; static bool persisting;
        static Light lakeLight; static ParticleSystem lakeFx; static float acc; static BoxCollider solid;
        public static float Sm(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }
        public static void ClearPersistent()
        {
            persisting = false; foreach (var c in volumes) if (c != null) c.enabled = true; volumes.Clear();
            if (lakeRoot != null) UnityEngine.Object.Destroy(lakeRoot); lakeRoot = null; if (lakeKit != null) foreach (var o in lakeKit.Owned) if (o != null) UnityEngine.Object.Destroy(o); lakeKit = null;
        }
        public static void PersistTick() { if (!persisting || lakeRoot == null) return; persistT += Time.deltaTime; try { LakeUpdate(persistT, Time.deltaTime, false); } catch { } }
        static float Alpha(float t) { float pre = 0.6f * Sm(T_DISS0, T_DISS1, t) * (1f - Sm(T_DISS1, T_NORMAL, t)); float full = 0.9f * Sm(T_NORMAL, T_FULL, t); return Mathf.Max(pre, full); }
        static void LakeUpdate(float t, float dt, bool running)
        {
            float a = Alpha(t); float k = Sm(T_DISS0, T_DISS1, t); bool post = t >= T_NORMAL; float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            Color blue = new Color(0.25f, 0.55f, 0.95f), purple = new Color(0.65f, 0.2f, 1f); Color c = post ? Color.Lerp(new Color(0.9f, 0.7f, 1f), purple, Sm(T_NORMAL, T_FULL + 3f, t)) : Color.Lerp(blue, purple, k);
            overMat.color = new Color(c.r, c.g, c.b, a * (post ? 0.85f + 0.15f * pulse : 1f)); overMat.renderQueue = 2994;
            if (Time.unscaledTime - uvT > 0.08f) { uvT = Time.unscaledTime; for (int i = 0; i < uv0.Length; i++) uvb[i] = uv0[i] + new Vector2(t * 0.03f, t * 0.017f); overMesh.uv = uvb; }
            float burst = t >= T_NORMAL && t < T_NORMAL + 2.5f ? 1f - (t - T_NORMAL) / 2.5f : 0f; lakeLight.intensity = AstronomicalSettings.Lighting.Value * (a * 2.5f * (0.7f + 0.3f * pulse) + 14f * burst);
            if (solid != null) solid.enabled = t >= T_NORMAL + 0.3f;
            if (volumes.Count > 0 && post) foreach (var v in volumes) if (v != null && v.enabled) v.enabled = false;
            if (dt <= 0f) return; float rate = post ? 40f + 160f * burst : 30f * k; acc += rate * AstronomicalSettings.Particles.Value * dt; int n = (int)acc; acc -= n;
            for (int i = 0; i < n; i++) { var ep = new ParticleSystem.EmitParams(); ep.position = new Vector3(UnityEngine.Random.Range(waterB.min.x, waterB.max.x), surfY + 0.2f, UnityEngine.Random.Range(waterB.min.z, waterB.max.z)); if ((ep.position - waterB.center).magnitude > 60f) ep.position = waterB.center + UnityEngine.Random.insideUnitSphere * 40f; ep.position = new Vector3(ep.position.x, surfY + 0.2f, ep.position.z); ep.velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1f, 4f + 8f * burst), UnityEngine.Random.Range(-1f, 1f)); ep.startColor = Color.Lerp(purple, Color.white, UnityEngine.Random.value * 0.5f); ep.startSize = UnityEngine.Random.Range(0.3f, 0.9f); ep.startLifetime = UnityEngine.Random.Range(1f, 2.5f); lakeFx.Emit(ep, 1); }
            if (post && t >= T_NORMAL + 0.5f) BounceStep();
        }
        static void BounceStep()
        {
            if (!AstronomicalSettings.Knockback.Value || Time.time - lastBounce < 0.4f) return; var pl = GTPlayer.Instance; if (pl == null) return; Vector3 hd; try { hd = pl.HeadCenterPosition; } catch { return; }
            float feet = hd.y - 1.1f; if (hd.x < waterB.min.x || hd.x > waterB.max.x || hd.z < waterB.min.z || hd.z > waterB.max.z) return; if (feet > surfY + 0.35f || feet < surfY - 5f) return;
            try { pl.ApplyKnockback(Vector3.up, 9.5f * AstronomicalSettings.KnockbackStrength.Value, true); lastBounce = Time.time; var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, 0.5f, 0.15f); gt.StartVibration(false, 0.5f, 0.15f); } } catch { }
        }
        // the real water: the water volume nearest to the player (found by type name so no assumptions about the game's namespaces), else any big water-named trigger
        static bool FindWater(Vector3 from, out Bounds b, out List<Collider> cols)
        {
            b = new Bounds(); cols = new List<Collider>(); Collider best = null; float bd = 9999f;
            try
            {
                foreach (var comp in Resources.FindObjectsOfTypeAll<Component>())
                {
                    if (comp == null) continue; string tn = comp.GetType().Name; if (tn != "WaterVolume") continue; var c = comp.GetComponent<Collider>(); if (c == null || !c.gameObject.scene.IsValid()) continue;
                    var bb = c.bounds; if (bb.size.x < 6f || bb.size.z < 6f) continue; float d = Vector3.Distance(from, bb.ClosestPoint(from)); if (d < bd) { bd = d; best = c; }
                }
            }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Lake: water search " + e.Message); }
            if (best == null || bd > 250f) return false; b = best.bounds; cols.Add(best); return true;
        }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }

        AstronomicalEventDef def; GameObject root, cubeRoot; WorldKit kit; Transform pivot, shell, glow; GltfModel model; Light lamp; ParticleSystem cfx; Material shellMat, glowMat; bool built, paused; float lastT = -1f, endT = 105f, yaw, flash; Vector3 dockEnd, dir, edge, shoreStand; int floorLayer; bool burstDone; bool pond;
        Texture2D glowTex; Mesh dockMesh; Light sun; Vector3 ambientSave; bool ambientSaved; float rumbleT, splashT = -9f; LineRenderer[] rings = new LineRenderer[3]; float[] ringT = { -9f, -9f, -9f }; int ringNext; bool splashed;
        Vector3 W(Vector3 l) { return l; }
        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { cfx, lakeFx }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false; endT = def != null && def.Length > 5f ? def.Length : 105f; ClearPersistent();
            Vector3 head = anchorPos; try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            floorLayer = 0; int m = LocoMask(); for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) { floorLayer = i; break; } kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lakeKit = new WorldKit { Layer = floorLayer }; Bounds b; List<Collider> cols;
            // the cube is made at the saved stage (when one is set and in use): the water nearest to the stage, the dock from its shore
            Vector3 foot = head - Vector3.up * 1.2f; var sg = new Vector3(AstronomicalSettings.FixedSpawnX.Value, AstronomicalSettings.FixedSpawnY.Value, AstronomicalSettings.FixedSpawnZ.Value); if (AstronomicalSettings.UseFixedSpawn.Value && (sg.x != 0f || sg.z != 0f) && Vector3.Distance(sg, foot) < 60f) foot = sg;      // (a stage saved on another map is far away: then the cube is made where you stand)
            pond = !FindWater(foot + Vector3.up * 1.2f, out b, out cols);
            if (pond)
            {   // no water volume on this map: build a pond on the FLATTEST patch of floor within 45 m (9 sample heights over a 22 m patch), its surface just above the highest of them, with a rim of stone blocks
                Vector3 bestC = foot; float bestS = float.MaxValue, bestTop = foot.y; RaycastHit h;
                for (int gx = -4; gx <= 4; gx++) for (int gz = -4; gz <= 4; gz++)
                {
                    Vector3 c = foot + new Vector3(gx * 10f, 0f, gz * 10f); float lo = 1e9f, hi = -1e9f; bool ok = true;
                    for (int sx = -1; sx <= 1 && ok; sx++) for (int sz = -1; sz <= 1 && ok; sz++) { if (Physics.Raycast(c + new Vector3(sx * 11f, 6f, sz * 11f), Vector3.down, out h, 16f, m, QueryTriggerInteraction.Ignore)) { lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y); } else ok = false; }
                    if (!ok) continue; float score = (hi - lo) + new Vector2(gx * 10f, gz * 10f).magnitude * 0.03f; if (score < bestS) { bestS = score; bestC = c; bestTop = hi; }
                }
                surfY = bestTop + 0.35f; b = new Bounds(new Vector3(bestC.x, surfY, bestC.z), new Vector3(24f, 2f, 24f)); BuildPondRim(b, surfY, m);
                AstronomicalEventMod.Log.LogInfo("Cube lake: pond spot spread " + bestS.ToString("0.0") + " m at " + bestC);
            }
            waterB = b; if (!pond) surfY = b.max.y; foreach (var c in cols) volumes.Add(c);
            // the dock: from the nearest point of the water edge towards the water
            Vector3 hp = new Vector3(Mathf.Clamp(foot.x, b.min.x, b.max.x), surfY, Mathf.Clamp(foot.z, b.min.z, b.max.z)); bool inside = foot.x > b.min.x && foot.x < b.max.x && foot.z > b.min.z && foot.z < b.max.z;
            if (inside) { float dl = foot.x - b.min.x, dr = b.max.x - foot.x, db = foot.z - b.min.z, dt2 = b.max.z - foot.z, mn = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt2)); hp = mn == dl ? new Vector3(b.min.x, surfY, foot.z) : mn == dr ? new Vector3(b.max.x, surfY, foot.z) : mn == db ? new Vector3(foot.x, surfY, b.min.z) : new Vector3(foot.x, surfY, b.max.z); }
            edge = hp; dir = new Vector3(b.center.x - edge.x, 0f, b.center.z - edge.z); if (inside) dir = -(new Vector3(foot.x - edge.x, 0f, foot.z - edge.z)); if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize(); if (!inside && (b.center - edge).magnitude < 1f) dir = Vector3.forward;
            if (!pond)
            {   // the water volume box is much bigger than the lake: walk out from the stage in 24 directions and take the nearest place where the ground drops below the surface
                float bestD = 1e9f; Vector3 bE = edge, bDir = dir; RaycastHit wh;
                for (int k = 0; k < 24; k++)
                {
                    float ang = k * 15f * Mathf.Deg2Rad; Vector3 d = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)); Vector3 last = foot;
                    for (float s = 0f; s <= 120f && s < bestD; s += 2f)
                    {
                        Vector3 p = foot + d * s; bool wet = !Physics.Raycast(new Vector3(p.x, surfY + 25f, p.z), Vector3.down, out wh, 60f, m, QueryTriggerInteraction.Ignore) || wh.point.y < surfY - 0.3f;
                        if (wet) { if (s > 3f) { bestD = s; bE = new Vector3(last.x, surfY, last.z); bDir = d; } break; }
                        last = p;
                    }
                }
                if (bestD < 1e8f) { edge = bE; dir = bDir; inside = false; }
            }
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; shoreStand = edge - dir * 9f; RaycastHit g2; if (Physics.Raycast(shoreStand + Vector3.up * 4f, Vector3.down, out g2, 14f, m, QueryTriggerInteraction.Ignore)) shoreStand = g2.point; else shoreStand.y = foot.y;
            root = new GameObject("AstronomicalCubeLake"); BuildDock(); BuildOverlay(); BuildCube();
            sun = new GameObject("LakeSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0f;
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b); ambientSaved = true; lastT = -1f; burstDone = false; splashed = false; built = true;
            AstronomicalEventMod.Log.LogInfo("Cube lake built: " + (pond ? "pond" : "real water") + ", surface " + surfY.ToString("0.00") + ", bounds " + b.size + ", dock dir " + dir);
        }
        public bool TryViewpoint(out Vector3 pos, out float y) { pos = Vector3.zero; y = 0f; if (root == null) return false; pos = shoreStand + Vector3.up * 1.2f; y = yaw; return true; }
        void BuildPondRim(Bounds b, float sy, int mask)
        {
            var rimM = kit.Lit(new Color(0.55f, 0.5f, 0.42f), null, 0.1f); var box = kit.Box(new Vector3(1f, 1f, 1f), 3f); int n = 30; float r = 12.5f; var holder = new GameObject("PondRim"); owned.Add(holder);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n; Vector3 at = b.center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); RaycastHit h; float gy = sy - 1f; if (Physics.Raycast(new Vector3(at.x, sy + 6f, at.z), Vector3.down, out h, 16f, mask, QueryTriggerInteraction.Ignore)) gy = h.point.y;
                float top = sy + 0.5f, hgt = Mathf.Max(0.6f, top - gy + 0.8f); kit.Obj("Rim" + i, holder.transform, box, rimM, new Vector3(at.x, top - hgt, at.z), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f), new Vector3(2.2f, hgt, 4.4f), true);
            }
        }
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        void BuildDock()
        {
            var wood = kit.Lit(new Color(0.45f, 0.3f, 0.18f), null, 0.1f); var dark = kit.Lit(new Color(0.3f, 0.2f, 0.12f), null, 0.1f); var box = kit.Box(new Vector3(1f, 1f, 1f), 2f); var holder = new GameObject("Dock"); holder.transform.SetParent(root.transform, false);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f); float dy = surfY + 0.45f; float len = 13f, w = 3.4f;
            Vector3 c = edge + dir * (len * 0.5f - 3f); kit.Obj("Deck", holder.transform, box, wood, new Vector3(c.x, dy - 0.15f, c.z), rot, new Vector3(w, 0.3f, len), true);
            for (int i = 0; i < 9; i++) { Vector3 p = edge + dir * (-2.6f + i * 1.3f); kit.Obj("Plank" + i, holder.transform, box, dark, new Vector3(p.x, dy + 0.02f, p.z), rot, new Vector3(w + 0.1f, 0.06f, 0.14f), false); }
            for (int i = 0; i < 5; i++) for (int s = -1; s <= 1; s += 2) { Vector3 p = edge + dir * (i * 2.4f - 1.6f) + Vector3.Cross(Vector3.up, dir) * s * (w * 0.5f - 0.1f); var post = kit.Obj("Post", holder.transform, box, dark, new Vector3(p.x, surfY - 1.4f, p.z), Quaternion.identity, new Vector3(0.28f, 2.6f, 0.28f), false); }
            dockEnd = edge + dir * (len - 3f - 2.2f); dockEnd.y = dy;
        }
        void BuildOverlay()
        {
            lakeRoot = new GameObject("AstronomicalLake"); float R = 130f; Vector3 c = waterB.center; float x0 = Mathf.Max(waterB.min.x, c.x - R), x1 = Mathf.Min(waterB.max.x, c.x + R), z0 = Mathf.Max(waterB.min.z, c.z - R), z1 = Mathf.Min(waterB.max.z, c.z + R); float y = surfY + 0.04f;
            // a grid so the swirl can scroll and the surface can wobble in light (vertex colours give soft edges)
            int nx = 24, nz = 24; var v = new List<Vector3>(); var uv = new List<Vector2>(); var cols = new List<Color>(); var tr = new List<int>();
            for (int j = 0; j <= nz; j++) for (int i = 0; i <= nx; i++) { float fx = i / (float)nx, fz = j / (float)nz; v.Add(new Vector3(Mathf.Lerp(x0, x1, fx), y, Mathf.Lerp(z0, z1, fz))); uv.Add(new Vector2(fx * (x1 - x0) / 14f, fz * (z1 - z0) / 14f)); float e = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fz, 1f - fz)); cols.Add(new Color(1, 1, 1, 1f)); }
            for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) { int a = j * (nx + 1) + i, b2 = a + 1, c2 = a + nx + 1, d = c2 + 1; tr.AddRange(new[] { a, c2, b2, b2, c2, d }); }
            overMesh = new Mesh(); overMesh.SetVertices(v); overMesh.SetUVs(0, uv); overMesh.SetColors(cols); overMesh.SetTriangles(tr, 0); overMesh.RecalculateBounds(); uv0 = overMesh.uv; uvb = new Vector2[uv0.Length]; lakeKit.Keep(overMesh);
            var tex = lakeKit.Tex(128, 128, (x, yy) => { float u = x / 128f, w = yy / 128f, T = Mathf.PI * 2f; float n = Mathf.Sin(T * (u * 3f + 0.25f * Mathf.Sin(T * (w * 2f)))) + Mathf.Sin(T * (w * 4f + 0.3f * Mathf.Sin(T * u * 3f))) + Mathf.Sin(T * ((u + w) * 2f)); n = n / 3f * 0.5f + 0.5f; return Color.Lerp(new Color(0.55f, 0.55f, 0.75f, 0.85f), new Color(1f, 0.95f, 1f, 1f), n * n); });
            overMat = lakeKit.Glow(Color.white, tex, 2994); overMat.color = new Color(0.25f, 0.55f, 0.95f, 0f); var g = new GameObject("LakeOverlay"); g.transform.SetParent(lakeRoot.transform, false); g.AddComponent<MeshFilter>().sharedMesh = overMesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = overMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false;
            lakeLight = new GameObject("LakeLight").AddComponent<Light>(); lakeLight.transform.SetParent(lakeRoot.transform, false); lakeLight.transform.position = new Vector3(c.x, surfY + 4f, c.z); lakeLight.type = LightType.Point; lakeLight.range = 120f; lakeLight.intensity = 0f; lakeLight.color = new Color(0.75f, 0.45f, 1f); lakeLight.shadows = LightShadows.None;
            var fg = new GameObject("LakeFx"); fg.transform.SetParent(lakeRoot.transform, false); lakeFx = fg.AddComponent<ParticleSystem>(); lakeFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = lakeFx.main; pm.maxParticles = 2500; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 2f; pm.gravityModifier = 0.1f; var em = lakeFx.emission; em.enabled = false;
            fg.GetComponent<ParticleSystemRenderer>().sharedMaterial = lakeKit.Glow(Color.white, lakeKit.GlowTex(), 3004); lakeFx.Play();
            var lt = lakeKit.Tex(4, 32, (x, yy) => { float vv = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - vv, 1.6f)); }, TextureWrapMode.Clamp); var soft = lakeKit.Glow(Color.white, lt, 3001);
            for (int i = 0; i < rings.Length; i++) { var rg = new GameObject("Ring" + i); rg.transform.SetParent(lakeRoot.transform, false); var l = rg.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = 64; l.loop = true; l.sharedMaterial = soft; l.shadowCastingMode = ShadowCastingMode.Off; l.enabled = false; rings[i] = l; }
            var sc = new GameObject("LakeSolid"); sc.layer = floorLayer; sc.transform.SetParent(lakeRoot.transform, false); sc.transform.position = new Vector3((x0 + x1) * 0.5f, surfY - 0.25f, (z0 + z1) * 0.5f); solid = sc.AddComponent<BoxCollider>(); solid.size = new Vector3(x1 - x0, 0.5f, z1 - z0); solid.enabled = false;      // the bouncy lake is solid
            persistT = 0f; acc = 0f;
        }
        void BuildCube()
        {
            model = def.Prop("cube"); if (model == null || model.Pivot == null) { AstronomicalEventMod.Log.LogWarning("Cube lake: model missing"); return; }
            cubeRoot = new GameObject("AstronomicalCubeLakeCube"); pivot = model.Pivot.transform; pivot.SetParent(cubeRoot.transform, false); model.Pivot.SetActive(true); float k = CUBE / Mathf.Max(0.01f, model.RendererBounds.size.x); pivot.localScale = Vector3.one * k;
            foreach (var r in model.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; foreach (var mt in r.sharedMaterials) if (mt != null) { mt.DisableKeyword("_EMISSION"); mt.SetColor("_EmissionColor", Color.black); } }
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(sh.GetComponent<Collider>()); sh.transform.SetParent(pivot, false); sh.transform.localPosition = new Vector3(0f, model.RendererBounds.size.y * 0.5f, 0f); sh.transform.localScale = Vector3.one * model.RendererBounds.size.x * 1.06f; shellMat = kit.Glow(new Color(0.75f, 0.4f, 1f, 0.1f), null, 3003); sh.GetComponent<Renderer>().sharedMaterial = shellMat; shell = sh.transform;
            var gq = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(gq.GetComponent<Collider>()); gq.transform.SetParent(cubeRoot.transform, false); glowMat = kit.Glow(new Color(0.85f, 0.55f, 1f, 0.2f), glowTex, 3002); gq.GetComponent<Renderer>().sharedMaterial = glowMat; glow = gq.transform;
            lamp = new GameObject("CubeLamp").AddComponent<Light>(); lamp.transform.SetParent(cubeRoot.transform, false); lamp.type = LightType.Point; lamp.color = new Color(0.75f, 0.45f, 1f); lamp.range = 40f; lamp.intensity = 0f; lamp.shadows = LightShadows.None;
            var fg = new GameObject("CubeFx"); fg.transform.SetParent(cubeRoot.transform, false); cfx = fg.AddComponent<ParticleSystem>(); cfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = cfx.main; pm.maxParticles = 1500; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 2f; pm.gravityModifier = -0.05f; var em = cfx.emission; em.enabled = false; fg.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004); cfx.Play();
        }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built) return; float dt = Mathf.Clamp(t - lastT, 0f, 0.1f); if (t < lastT - 0.4f || lastT < 0f) { burstDone = false; splashed = false; } lastT = t; var cam = Camera.main;
            if (model != null)
            {
                float k = CUBE / Mathf.Max(0.01f, model.RendererBounds.size.x); Vector3 side = Vector3.Cross(Vector3.up, dir); Vector3 pos; Quaternion rot; float sc = 1f; float half = CUBE * 0.5f;
                if (t < T_TIP) { pos = dockEnd + Vector3.up * half; rot = Quaternion.Euler(0f, yaw, 0f); }
                else if (t < T_HIT)
                {
                    float f = (t - T_TIP) / (T_HIT - T_TIP); float tilt = Sm(0f, 0.55f, f) * 90f + Sm(0.55f, 1f, f) * 100f; float fall = Mathf.Clamp01((f - 0.5f) / 0.5f); pos = dockEnd + Vector3.up * half + dir * (Sm(0f, 0.6f, f) * 2.3f + fall * 2.2f) - Vector3.up * (fall * fall * (dockEnd.y - surfY + half * 1.6f));
                    rot = Quaternion.AngleAxis(tilt, -side) * Quaternion.Euler(0f, yaw, 0f);
                }
                else
                {
                    float ds = Sm(T_DISS0, T_DISS1, t); Vector3 landed = dockEnd + dir * 4.5f; float sink = Sm(T_HIT, T_HIT + 3f, t) * 0.7f + ds * 3.0f; pos = new Vector3(landed.x, surfY - half * 0.8f - sink + 0.6f * Mathf.Sin(t * 0.8f) * (1f - ds), landed.z); sc = Mathf.Lerp(1f, 0.25f, ds) * (t >= T_DISS0 ? 1f + 0.03f * Mathf.Sin(t * 30f) : 1f);
                    rot = Quaternion.AngleAxis(190f + t * 8f, side) * Quaternion.Euler(0f, yaw + t * 6f, 0f); if (t >= T_NORMAL - 0.2f) sc = 0f;
                }
                bool vis = sc > 0.01f; if (pivot.gameObject.activeSelf != vis) { pivot.gameObject.SetActive(vis); shell.gameObject.SetActive(vis); }
                // the cube's own origin is its base: lift by the half height so the pose above is for its centre
                pivot.localScale = Vector3.one * k * sc; pivot.rotation = rot; pivot.position = pos - rot * (Vector3.up * model.RendererBounds.size.y * 0.5f * k * sc);
                Vector3 centre = pos; glow.position = centre; glow.localScale = Vector3.one * CUBE * 2.2f * Mathf.Max(0.1f, sc); if (cam != null) glow.rotation = Quaternion.LookRotation(glow.position - cam.transform.position, Vector3.up); bool gl = vis; if (glow.gameObject.activeSelf != gl) glow.gameObject.SetActive(gl);
                float uw = t > T_HIT ? Mathf.Clamp01((surfY - centre.y) / 3f) : 0f; glowMat.color = new Color(0.85f, 0.55f, 1f, (0.12f + 0.2f * Sm(T_DISS0, T_DISS1, t)) * (1f - 0.3f * uw)); shellMat.color = new Color(0.75f, 0.4f, 1f, 0.06f + 0.1f * Sm(T_DISS0, T_DISS1, t)); lamp.transform.position = centre; lamp.intensity = AstronomicalSettings.Lighting.Value * (vis ? 1.5f + 2.5f * Sm(T_DISS0, T_DISS1, t) : 0f);
                if (!paused && dt > 0f && vis) { float rate = t < T_HIT ? 6f : 10f + 40f * Sm(T_DISS0, T_DISS1, t); float par = AstronomicalSettings.Particles.Value; int n = (int)(rate * par * dt + UnityEngine.Random.value); for (int i = 0; i < n; i++) { var ep = new ParticleSystem.EmitParams(); var d = UnityEngine.Random.onUnitSphere; ep.position = centre + d * half * sc; ep.velocity = d * 1.5f + Vector3.up * UnityEngine.Random.Range(1f, 3f); ep.startColor = Color.Lerp(new Color(0.8f, 0.5f, 1f), Color.white, UnityEngine.Random.value * 0.4f); ep.startSize = UnityEngine.Random.Range(0.3f, 0.9f); ep.startLifetime = UnityEngine.Random.Range(1f, 2.4f); cfx.Emit(ep, 1); } }
                if (!splashed && t >= T_HIT && t - T_HIT < 1.5f && !paused) { splashed = true; splashT = t; ringT[ringNext] = t; ringNext = (ringNext + 1) % rings.Length; AstronomicalHazards.Blast(new Vector3(landed(dockEnd, dir).x, surfY, landed(dockEnd, dir).z), 30f, 9f); for (int i = 0; i < 140; i++) { var ep = new ParticleSystem.EmitParams(); ep.position = new Vector3(pos.x, surfY, pos.z) + UnityEngine.Random.insideUnitSphere * 2f; ep.velocity = new Vector3(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(6f, 20f), UnityEngine.Random.Range(-8f, 8f)); ep.startColor = new Color(0.85f, 0.95f, 1f, 0.9f); ep.startSize = UnityEngine.Random.Range(0.4f, 1.2f); ep.startLifetime = UnityEngine.Random.Range(1f, 2f); lakeFx.Emit(ep, 1); } }
            }
            // the burst of light at 1:26 and the purple lake
            if (!paused && !burstDone && t >= T_NORMAL && t - T_NORMAL < 1.5f) { burstDone = true; flash = 1f; ringT[ringNext] = t; ringNext = (ringNext + 1) % rings.Length; AstronomicalHazards.Blast(waterB.center, 40f, 14f, 14f); }
            flash = Mathf.Max(0f, flash - dt * 0.7f); for (int i = 0; i < rings.Length; i++) { float age = t - ringT[i]; bool on = age >= 0f && age < 3f; if (rings[i].enabled != on) rings[i].enabled = on; if (!on) continue; float kk = age / 3f, r = 2f + age * 24f; Vector3 c0 = i == ringNext ? waterB.center : waterB.center; Vector3 cc = t < 40f ? dockEnd + dir * 4.5f : waterB.center; cc.y = surfY; for (int j = 0; j < 64; j++) { float a = j * Mathf.PI * 2f / 64f; rings[i].SetPosition(j, cc + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r)); } rings[i].startColor = rings[i].endColor = new Color(0.85f, 0.7f, 1f, 0.9f * (1f - kk)); rings[i].widthMultiplier = 1.8f * (1f - 0.6f * kk); }
            LakeUpdate(t, dt, true); lakeLight.intensity += AstronomicalSettings.Lighting.Value * 10f * flash;
            float purple = Alpha(t); RenderSettings.ambientLight = Color.Lerp(new Color(ambientSave.x, ambientSave.y, ambientSave.z), new Color(0.45f, 0.3f, 0.7f), purple * 0.6f) * (1f + 0.6f * flash); sun.color = new Color(0.75f, 0.55f, 1f); sun.intensity = 0.5f * purple * Mathf.Max(0.4f, AstronomicalSettings.Lighting.Value); sun.transform.rotation = Quaternion.Euler(60f, yaw, 0f);
            rumbleT += dt; if (!paused && rumbleT > 0.15f) { rumbleT = 0f; float p = t >= T_NORMAL && t < T_NORMAL + 2f ? 0.8f : t >= T_HIT && t < T_HIT + 1f ? 0.5f : 0f; if (p > 0.02f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, p, 0.15f); gt.StartVibration(false, p, 0.15f); } } catch { } } }
        }
        static Vector3 landed(Vector3 d, Vector3 dr) { return d + dr * 4.5f; }
        public string DebugInfo() { return "t " + lastT.ToString("0.0") + "  " + (pond ? "pond" : "real water") + "  surface " + surfY.ToString("0.00") + "  bounce " + (lastT >= T_NORMAL + 0.5f); }

        public void Cleanup()
        {
            if (ambientSaved) { RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z); ambientSaved = false; }
            bool natural = lastT >= endT - 1.0f; if (cubeRoot != null) { if (model != null && model.Pivot != null) { model.Pivot.transform.SetParent(null, false); model.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(model.Pivot); } UnityEngine.Object.Destroy(cubeRoot); cubeRoot = null; }
            if (natural && lakeRoot != null) { persisting = true; persistT = lastT; AstronomicalEventMod.Log.LogInfo("The lake stays purple and bouncy until you leave the map"); }
            else ClearPersistent();
            if (root != null) UnityEngine.Object.Destroy(root); root = null; foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); kit = null; built = false; lastT = -1f;
        }
    }
}
