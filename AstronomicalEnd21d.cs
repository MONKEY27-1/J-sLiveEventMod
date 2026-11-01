using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // THE END, the finale (9:15 - 11:04): the Zero Point chamber floods and you wake in the ocean (underwater, rising to the surface at 9:25). The real map is hidden and a COPY of it (every mesh within 270 m, drawn with
    // Graphics.DrawMesh so statically batched meshes work too) floats in the distance under the Cube Queen's ring. At 10:03 a flash: the sky turns blue, the island stands on its edge, then rolls over (10:05 - 10:28) and sinks;
    // its underside (a rock cone) carries the Chapter 3 island, which is what is left on the sea (10:30). 11:00 the title card, 11:04 the end.
    internal sealed partial class AstronomicalEnd21
    {
        const float OC_PX = -150f, OC_PY = -12.3f, OC_PZ = 1150f, OC_S = 5.2f;
        const float C3S = 2.58f; // Chapter 3 (prepared to 200 m across) scaled to the old island's 516 m diameter
        const float WY = -0.3f, ISLE_Z = 520f, ISLE_R = 258f, ISLE_D = 18f, ISLE_S = 1.45f;      // the old island: radius 258 m, a 150 m crystal rock slab under it, shown 1.45x bigger and 520 m away so it towers over you (reference screenshot)
        GameObject oceanGo, isle, isleCone, isleDisc, waterGo, titleGo; Material oceanMat, waterMat, coneMat, discMat; Texture2D waveTex, rockTex; TextMesh[] titleTx; Transform c3T;
        struct IsleDraw { public Mesh Mesh; public int Sub; public Material Mat; public Matrix4x4 Rel; }
        Vector3 isleCenterSum; int isleCenterN; readonly List<IsleDraw> isleDraws = new List<IsleDraw>(); bool isleBuilt, mapHidden; int isleSkipped;
        Vector3 floodC;

        // the player's head height in the ocean scene: deep under the water at first, rising to float at the surface by 9:25
        float OceanHeadY(float t) { float rise = Sm(T_OCEAN + 1.5f, T_OCEAN + 11f, t); float gx, gz; float heave = SeaSample(0f, 0f, t, 0f, out gx, out gz); return Mathf.Lerp(WY - 7.5f, WY + 1.3f, rise) + heave * rise; }      // you float: the head rides the wave height

        Mesh MakeDisc(float r, int seg, float uvScale)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); v.Add(Vector3.zero); uv.Add(Vector2.zero);
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); v.Add(p); uv.Add(new Vector2(p.x, p.z) / uvScale); }
            for (int i = 1; i <= seg; i++) { tr.Add(0); tr.Add(i + 1); tr.Add(i); tr.Add(0); tr.Add(i); tr.Add(i + 1); }     // both faces: you see the surface from above and from below
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateBounds(); owned.Add(m); return m;
        }
        // the rock underside: a rough frustum from the island's rim (radius R at the top) down to a blunt tip, with the Chapter 3 island hung on its lower end
        Mesh MakeCone(float R, float D, float rBot, int rings, int segs, int seed)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>(); var rnd = new System.Random(seed);
            float[,] n = new float[rings + 1, segs + 1]; for (int i = 0; i <= rings; i++) for (int j = 0; j < segs; j++) n[i, j] = (float)(rnd.NextDouble() - 0.5); for (int i = 0; i <= rings; i++) n[i, segs] = n[i, 0];
            for (int i = 0; i <= rings; i++)
            {
                float f = i / (float)rings, r = Mathf.Lerp(R, rBot, Mathf.Pow(f, 0.85f)), y = -2f - D * f;
                for (int j = 0; j <= segs; j++) { float a = j * Mathf.PI * 2f / segs, jag = 1f + (i == 0 ? 0f : 0.14f * n[i, j]); v.Add(new Vector3(Mathf.Cos(a) * r * jag, y + (i == 0 ? 0f : 9f * n[i, j]), Mathf.Sin(a) * r * jag)); uv.Add(new Vector2(j / (float)segs * 6f, f * 2f)); }
            }
            for (int i = 0; i < rings; i++) for (int j = 0; j < segs; j++) { int a = i * (segs + 1) + j, b = a + 1, c = a + segs + 1, d = c + 1; tr.Add(a); tr.Add(c); tr.Add(b); tr.Add(b); tr.Add(c); tr.Add(d); tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(b); tr.Add(d); tr.Add(c); }
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m); return m;
        }


        // the rock slab under the island: layered strata, big crystal facets (flat shaded: every face has its own three vertices) and glowing crystal spikes; reference: a vertical slab of blue-violet crystal rock with moss on the rim
        Mesh MakeRock(float R, float D, float rBot, int rings, int segs, int seed, out Mesh spikes)
        {
            var pts = new Vector3[rings + 1, segs]; float so = seed * 3.7f;
            for (int i = 0; i <= rings; i++)
            {
                float f = i / (float)rings, baseR = R * (1f + 0.05f * Mathf.Sin(Mathf.PI * f) - 0.02f * f * f * f);
                for (int j = 0; j < segs; j++)
                {
                    float a = j * Mathf.PI * 2f / segs; float n1 = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + so, Mathf.Sin(a) * 1.6f + f * 2.2f) * 2f - 1f, n2 = Mathf.PerlinNoise(Mathf.Cos(a) * 4.2f + 9f + so, Mathf.Sin(a) * 4.2f + f * 5.5f) * 2f - 1f;
                    float terr = 1f + 0.035f * Mathf.Sin(f * 46f + n1 * 3f) + 0.05f * Mathf.Sign(Mathf.Sin(f * 17f + a * 3f)), env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, f * 1.1f)); float rr = i == 0 ? R : baseR * terr * (1f + 0.14f * n1 * env + 0.08f * n2 * env);
                    pts[i, j] = new Vector3(Mathf.Cos(a) * rr, -2f - D * f + (i == 0 ? 0f : 9f * n2 * env), Mathf.Sin(a) * rr);
                }
            }
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            Action<Vector3, Vector3, Vector3, Vector2, Vector2, Vector2> tri = (p0, p1, p2, u0, u1, u2) =>
            {
                Vector3 nrm = Vector3.Cross(p1 - p0, p2 - p0); Vector3 c = (p0 + p1 + p2) / 3f; Vector3 outd = new Vector3(c.x, 0f, c.z); if (Vector3.Dot(nrm, outd) < 0f) { var t = p1; p1 = p2; p2 = t; var tu = u1; u1 = u2; u2 = tu; }
                int b = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); uv.Add(u0); uv.Add(u1); uv.Add(u2); tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
            };
            for (int i = 0; i < rings; i++) for (int j = 0; j < segs; j++)
            {
                int j2 = (j + 1) % segs; Vector3 a = pts[i, j], b = pts[i, j2], c = pts[i + 1, j], e = pts[i + 1, j2]; float u0 = j / (float)segs * 5f, u1 = (j + 1) / (float)segs * 5f, v0 = i / (float)rings * 3f, v1 = (i + 1) / (float)rings * 3f;
                if (((i * 7 + j * 3) & 1) == 0) { tri(a, c, b, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v0)); tri(b, c, e, new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1)); }
                else { tri(a, c, e, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1)); tri(a, e, b, new Vector2(u0, v0), new Vector2(u1, v1), new Vector2(u1, v0)); }
            }
            Vector3 tip = new Vector3(0f, -2f - D, 0f);      // flat bottom face (Chapter 3 sits on it)
             for (int j = 0; j < segs; j++) tri(pts[rings, j], pts[rings, (j + 1) % segs], tip, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1f));
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m);
            // glowing crystal spikes jutting from the face
            var sv = new List<Vector3>(); var sc = new List<Color>(); var st = new List<int>(); var rnd = new System.Random(seed + 5);
            for (int k = 0; k < 90; k++)
            {
                int i = rnd.Next(3, rings), j = rnd.Next(segs); Vector3 bp = pts[i, j]; Vector3 outd = new Vector3(bp.x, 0f, bp.z).normalized; Vector3 dir = (outd * 0.9f + new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() * 0.9f - 0.1f, (float)rnd.NextDouble() - 0.5f) * 0.7f).normalized;
                float len = 10f + 34f * (float)rnd.NextDouble() * (float)rnd.NextDouble(), wd = len * (0.10f + 0.07f * (float)rnd.NextDouble()); Vector3 s1 = Vector3.Cross(dir, Vector3.up).normalized, s2 = Vector3.Cross(dir, s1);
                Color cb = Color.Lerp(new Color(0.25f, 0.45f, 1f, 0.95f), new Color(0.55f, 0.95f, 1f, 0.95f), (float)rnd.NextDouble()), ct = new Color(0.9f, 1f, 1f, 1f); int b0 = sv.Count;
                sv.Add(bp + s1 * wd); sv.Add(bp + s2 * wd); sv.Add(bp - s1 * wd); sv.Add(bp - s2 * wd); sv.Add(bp + dir * len); sc.Add(cb); sc.Add(cb * 0.8f); sc.Add(cb); sc.Add(cb * 0.8f); sc.Add(ct);
                for (int q = 0; q < 4; q++) { st.Add(b0 + q); st.Add(b0 + (q + 1) % 4); st.Add(b0 + 4); }
            }
            spikes = new Mesh(); spikes.SetVertices(sv); spikes.SetColors(sc); spikes.SetTriangles(st, 0); spikes.RecalculateBounds(); owned.Add(spikes); return m;
        }
        Texture2D MakeRockTex()
        {
            return kit.Tex(128, 128, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.07f + 4f, y * 0.07f) * 0.6f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f + 8f) * 0.4f, band = 0.5f + 0.5f * Mathf.Sin(y * 0.33f + n * 4f);
                Color c = Color.Lerp(new Color(0.15f, 0.12f, 0.22f), new Color(0.46f, 0.5f, 0.66f), Mathf.Clamp01(n * 1.3f - 0.25f + 0.3f * band)); c = Color.Lerp(c, new Color(0.34f, 0.2f, 0.42f), 0.25f * Mathf.PerlinNoise(x * 0.4f, y * 0.04f)); float vein = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(x * 0.19f + n * 6f)) * 5f), 2f) * (n > 0.55f ? 1f : 0f); c = Color.Lerp(c, new Color(0.55f, 0.95f, 1f), vein * 0.6f); c.a = 1f; return c;
            });
        }
        void SetupOcean()
        {
            waveTex = kit.Tex(64, 64, (x, y) => { float a = Mathf.Sin(x * 0.4f + y * 0.15f) * 0.5f + 0.5f, b = Mathf.Sin(y * 0.55f - x * 0.2f) * 0.5f + 0.5f, c = Mathf.Sin((x + y) * 0.9f) * 0.5f + 0.5f, w = (a + b + c) / 3f; return new Color(0.82f + 0.18f * w, 0.86f + 0.14f * w, 0.9f + 0.1f * w, 1f); });
            oceanMat = new Material(kit.Flat(new Color(0.03f, 0.09f, 0.2f))); oceanMat.mainTexture = waveTex; try { oceanMat.SetFloat("_Cull", 0f); } catch { } owned.Add(oceanMat);
            oceanGo = new GameObject("EOcean"); oceanGo.transform.SetParent(root.transform, false); oceanGo.AddComponent<MeshFilter>().sharedMesh = MakeDisc(4200f, 72, 22f); var mr = oceanGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = oceanMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; oceanGo.SetActive(false);
            SeaBuild();
            // (no collider on the sea: rising from underwater through a solid plane threw the rig about)
            rockTex = kit.Tex(32, 32, (x, y) => { float n = Mathf.PerlinNoise(x * 0.23f, y * 0.23f) * 0.6f + Mathf.PerlinNoise(x * 0.7f, y * 0.7f) * 0.4f; return new Color(0.55f + 0.3f * n, 0.5f + 0.28f * n, 0.45f + 0.25f * n, 1f); });
            coneMat = kit.Lit(Color.white, MakeRockTex(), 0.55f);
            discMat = new Material(kit.Flat(Color.white)); discMat.mainTexture = kit.Tex(128, 128, (x, y) => { float n = Mathf.PerlinNoise(x * 0.05f + 3f, y * 0.05f) * 0.55f + Mathf.PerlinNoise(x * 0.22f, y * 0.22f + 5f) * 0.45f; Color c = Color.Lerp(new Color(0.16f, 0.2f, 0.1f), new Color(0.42f, 0.3f, 0.12f), n); c = Color.Lerp(c, new Color(0.3f, 0.42f, 0.18f), Mathf.Clamp01((n - 0.55f) * 3f)); c.a = 1f; return c; }); try { discMat.SetFloat("_Cull", 0f); } catch { } owned.Add(discMat);
            // the title card (11:00): three lines of text, drawn over everything
            titleGo = new GameObject("EEndTitle"); titleTx = new TextMesh[3]; string[] lines = { "FORTNITE", "CHAPTER 2 FINALE", "THE END" }; float[] sz = { 0.018f, 0.025f, 0.07f }; float[] yy = { 0.55f, 0.2f, -0.45f };
            Font f = null; try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            for (int i = 0; i < 3; i++)
            {
                var g = new GameObject("l" + i); g.transform.SetParent(titleGo.transform, false); g.transform.localPosition = new Vector3(0f, yy[i], 0f); var tm = g.AddComponent<TextMesh>(); tm.text = lines[i]; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontSize = 100; tm.characterSize = sz[i]; tm.fontStyle = FontStyle.Bold; tm.color = new Color(1f, 1f, 1f, 0f);
                if (f != null) { tm.font = f; var tr2 = g.GetComponent<MeshRenderer>(); if (tr2 != null && f.material != null) { tr2.sharedMaterial = new Material(f.material); tr2.sharedMaterial.renderQueue = 4000; owned.Add(tr2.sharedMaterial); } }
                titleTx[i] = tm;
            }
            titleGo.SetActive(false);
        }
        void OceanCleanup()
        {
            try { if (mapHidden) { AstronomicalForestHider.Restore(); mapHidden = false; } } catch { }
            DestroyIsle(); if (titleGo != null) UnityEngine.Object.Destroy(titleGo); titleGo = null; titleTx = null; oceanGo = null; waterGo = null; c3T = null; isleBuilt = false;
        }
        void DestroyIsle() { if (isle != null) UnityEngine.Object.Destroy(isle); isle = null; isleCone = null; isleDisc = null; isleDraws.Clear(); isleBuilt = false; if (ch3M != null && ch3M.Pivot != null) { ch3M.Pivot.transform.SetParent(root != null ? root.transform : null, false); ch3M.Pivot.SetActive(false); } }

        // copy the map: every mesh renderer within 270 m (static-batched ones too: their combined mesh holds world-space vertices, drawn with an identity matrix and the sub-mesh range of the renderer)
        void BuildIsland()
        {
            if (isleBuilt) return; isleBuilt = true; isleDraws.Clear(); isleCenterSum = Vector3.zero; isleCenterN = 0;
            isle = new GameObject("EIsle"); isle.transform.SetParent(root.transform, false); isle.transform.position = originW; isle.transform.rotation = root.transform.rotation;
            Matrix4x4 w2h = isle.transform.worldToLocalMatrix; int batched = 0, plain = 0; var seen = new HashSet<long>();
            try
            {
                var skip = new HashSet<Transform>();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<GTPlayer>()) skip.Add(p.transform); foreach (var p in UnityEngine.Object.FindObjectsOfType<VRRig>()) skip.Add(p.transform); foreach (var p in UnityEngine.Object.FindObjectsOfType<GorillaTagger>()) skip.Add(p.transform); foreach (var p in UnityEngine.Object.FindObjectsOfType<Camera>()) skip.Add(p.transform);
                float r2 = 270f * 270f; var list = new List<KeyValuePair<float, MeshRenderer>>();
                foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue; var b = r.bounds; if (b.size.sqrMagnitude > 1000000f) continue; float d2 = (b.center - originW).sqrMagnitude; if (d2 > r2) continue;
                    bool keep = false; for (var q = r.transform; q != null; q = q.parent) { if (skip.Contains(q)) { keep = true; break; } string n = q.name; if (n.StartsWith("JClient", StringComparison.Ordinal) || n.StartsWith("Astronomical", StringComparison.Ordinal) || n.StartsWith("BepInEx", StringComparison.Ordinal)) { keep = true; break; } }
                    if (keep) { isleSkipped++; continue; } list.Add(new KeyValuePair<float, MeshRenderer>(d2, r)); isleCenterSum += b.center - originW; isleCenterN++;
                }
                list.Sort((x, y) => x.Key.CompareTo(y.Key));
                foreach (var kv in list)
                {
                    var r = kv.Value; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; var mesh = mf.sharedMesh; var mats = r.sharedMaterials; bool sb = r.isPartOfStaticBatch; int start = sb ? r.subMeshStartIndex : 0; Matrix4x4 m = sb ? Matrix4x4.identity : r.transform.localToWorldMatrix;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        int sub = start + i; if (sub >= mesh.subMeshCount || mats[i] == null) continue;
                        if (sb) { long key = ((long)mesh.GetInstanceID() * 73856093L) ^ ((long)sub * 19349663L) ^ ((long)mats[i].GetInstanceID() * 83492791L); if (!seen.Add(key)) continue; }
                        if (isleDraws.Count >= 6000) break; isleDraws.Add(new IsleDraw { Mesh = mesh, Sub = sub, Mat = mats[i], Rel = w2h * m }); if (sb) batched++; else plain++;
                    }
                }
            }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("End21 island copy: " + e.Message); }
            if (isleCenterN > 0) { Vector3 ctr = isleCenterSum / isleCenterN; ctr.y = 0f; Vector3 lc = isle.transform.InverseTransformVector(ctr); Matrix4x4 sh = Matrix4x4.Translate(new Vector3(-lc.x, 0f, -lc.z)); for (int i = 0; i < isleDraws.Count; i++) { var dd = isleDraws[i]; dd.Rel = sh * dd.Rel; isleDraws[i] = dd; } AstronomicalEventMod.Log.LogInfo("End21: the map copy was " + lc.magnitude.ToString("0") + " m off the island's axis: re-centred"); }
            AstronomicalEventMod.Log.LogInfo("End21: the island copy has " + isleDraws.Count + " draws (" + batched + " from static batches, " + plain + " plain, " + isleSkipped + " skipped)");
            // the rock underside and a plain ground disc under everything (so the island has a surface even where the copy is thin)
            isleCone = new GameObject("EIsleCone"); isleCone.transform.SetParent(isle.transform, false); Mesh spk; isleCone.AddComponent<MeshFilter>().sharedMesh = MakeRock(ISLE_R, ISLE_D, 70f, 26, 96, 7, out spk); var spo = new GameObject("EIsleSpikes"); spo.transform.SetParent(isle.transform, false); spo.AddComponent<MeshFilter>().sharedMesh = spk; var spr = spo.AddComponent<MeshRenderer>(); spr.sharedMaterial = kit.Glow(Color.white, null, 3004); spr.shadowCastingMode = ShadowCastingMode.Off; var cr = isleCone.AddComponent<MeshRenderer>(); cr.sharedMaterial = coneMat; cr.shadowCastingMode = ShadowCastingMode.Off;
            isleDisc = new GameObject("EIsleDisc"); isleDisc.transform.SetParent(isle.transform, false); isleDisc.transform.localPosition = new Vector3(0f, -0.9f, 0f); isleDisc.AddComponent<MeshFilter>().sharedMesh = MakeDisc(ISLE_R, 48, 40f); var dr = isleDisc.AddComponent<MeshRenderer>(); dr.sharedMaterial = discMat; dr.shadowCastingMode = ShadowCastingMode.Off;
            if (ch3M != null)
            {
                var p = ch3M.Pivot; p.SetActive(true); p.transform.SetParent(isle.transform, false); float sc = C3S; p.transform.localScale = Vector3.one * sc; p.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                p.transform.localPosition = new Vector3(0f, -2f - ISLE_D + 0.5f, 0f); c3T = p.transform;      // now a SOLID chunk (tools/prep_chapter3.py closes the sheet with a 16 m rock skirt and a flat bottom): its flat bottom (the pivot, its lowest point) is pressed onto the slab's bottom face; the land grows away from it      // Chapter 3 is a relief SHEET (0 - 27 m above its lowest vertex, median 14 m): sink it 14 m into the slab so the low half is buried in the rock and the land grows out of the bottom face      // the model's pivot is at its LOWEST point (feet-centred loader): that point sits on the slab's bottom face and the island grows away from it (flipped 180 with the island it ends upright)      // the island's seabed sits inside the cone's narrow end, its mountains reach 50 m past the tip: after the roll it stands on top, its coast (about 27 m above its seabed) is at sea level when h = -75
            }
        }
        // the map goes while the ocean scene runs (and comes back if you scrub out of it)
        void OceanSwitch(bool entering, float t)
        {
            if (entering)
            {
                BuildIsland(); try { if (!mapHidden) { AstronomicalForestHider.Hide(originW, 1500f, tr => root != null && tr.IsChildOf(root.transform)); mapHidden = true; } } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("End21 hide map: " + e.Message); }
            }
            else { try { if (mapHidden) { AstronomicalForestHider.Restore(); mapHidden = false; } } catch { } DestroyIsle(); if (oceanGo != null) oceanGo.SetActive(false); if (titleGo != null) titleGo.SetActive(false); }
        }

        // the island's pose: upright and calm until the flash, then on its edge high over the sea, rolling over and sinking
        void IslePose(float t, out Vector3 pos, out float rollDeg)
        {
            float flash = T_END_VIS + 0.2f; float h, roll;
            if (t < flash) { h = 0.5f; roll = 0f; }
            else { roll = 90f + 90f * Sm(T_END_VIS + 1.5f, T_END_VIS + 25f, t); h = Mathf.Lerp(ISLE_R * ISLE_S + 12f, ISLE_S * (-(2f + ISLE_D) - 2.7f * C3S), Sm(T_END_VIS + 2.5f, T_END_VIS + 50f, t)); }
            float z = ISLE_Z + 20f * Sm(T_END_VIS + 18f, T_END_VIS + 50f, t);        // after the flip the island drifts towards you: Chapter 3 is what stays on the sea
            pos = new Vector3(0f, WY + h, z); rollDeg = roll;
        }
        // the Cube Queen falls out of the sky into the sea (9:32 - 9:38), thrashes as the water takes her and sinks, a last flash at 9:58
        const float QT0 = 572f, QT1 = 578f, QT2 = 600f;
        void QueenInWater(float t, float tp, float dt)
        {
            if (queenM == null || queenGo == null) return; bool on = t > T_OCEAN - 0.5f && t < QT2 + 0.5f; if (!on) { if (queenGo.activeSelf && SceneAt(t) == Sc.Ocean) queenGo.SetActive(false); return; }
            float fall = Mathf.Clamp01((t - QT0) / (QT1 - QT0)), sink = Sm(QT1 + 2f, QT2 - 2f, t); float sgx, sgz; float y = WY + Mathf.Lerp(230f, 0f, fall * fall) - 18f * sink * sink + SeaSample(22f, 85f, t, 60f, out sgx, out sgz) * (1f - sink) + 0.8f * Mathf.Sin(t * 2.2f) * (1f - sink);
            float tilt = Mathf.Lerp(0f, 55f, sink) + 6f * Mathf.Sin(t * 3f) * (1f - fall), scale = Mathf.Lerp(140f / 1.8f, 10f, Sm(QT0, QT1, t)); Vector3 pos = new Vector3(22f, y, 85f);
            Vector3 orbQ = new Vector3(OC_PX, OC_PY + 150f * OC_S, OC_PZ); if (t < QT1) { float u = Mathf.Clamp01((t - QT0) / (QT1 - QT0)); pos = new Vector3(Mathf.Lerp(orbQ.x, 22f, u), Mathf.Lerp(orbQ.y, WY, u * u) , Mathf.Lerp(orbQ.z, 85f, u)); tilt = 0f; }      // she starts in the tower's orb, as in the rest of the event
            PlaceGo(queenGo, pos, scale, Quaternion.Euler(tilt, 180f + 12f * Mathf.Sin(t * 0.8f), 8f * Mathf.Sin(t * 1.7f) * sink), true);
            string qc = t < QT0 ? "H_Float" : t < QT1 ? "H_Retreat" : (t < QT1 + 8f ? "H_Hit" : "H_Rage"); float lt = t < QT0 ? t : t < QT1 ? Mathf.Min(t - QT0, 3.9f) : (t < QT1 + 8f ? (t - QT1) % 1.6f : (t - QT1 - 8f) % 3f); PlayClip(queenRig, queenM, qc, lt);
            if (paused || dt <= 0f) return; Vector3 wp = W(new Vector3(pos.x, WY, pos.z));
            if (tp < QT1 && t >= QT1) { Sfx(sSplash, wp, 1f, 0.7f, 0.2f); Sfx(sBoom, wp, 0.8f, 0.8f, 0.2f); AddRipple(22f, 85f, QT1, 2.4f, 16f, 22f, 24f, 45f); AddRipple(22f, 85f, QT1 + 0.4f, 1.1f, 11f, 11f, 14f, 30f); flashLamp.transform.position = wp + Vector3.up * 6f; flashLamp.color = new Color(0.7f, 0.85f, 1f); flashLamp.intensity = Lit * 18f; shake = Mathf.Max(shake, 1.5f); for (int k = 0; k < 260; k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(0f, 14f); Emit(fx, wp + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r), new Vector3(Mathf.Cos(a) * r * 0.5f, UnityEngine.Random.Range(8f, 34f), Mathf.Sin(a) * r * 0.5f), new Color(0.8f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.6f, 2.2f), UnityEngine.Random.Range(1.2f, 2.6f)); } }
            if (t > QT1 && t < QT2 - 2f && UnityEngine.Random.value < dt * 40f) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(2f, 11f); Emit(fx, wp + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r), new Vector3(0f, UnityEngine.Random.Range(3f, 12f), 0f), new Color(0.6f, 0.9f, 1f, 0.8f), UnityEngine.Random.Range(0.3f, 1.2f), UnityEngine.Random.Range(0.6f, 1.4f)); if (UnityEngine.Random.value < 0.4f) Emit(fx, wp + Vector3.up * 3f, UnityEngine.Random.onUnitSphere * 8f, new Color(1f, 0.6f, 0.95f, 1f), UnityEngine.Random.Range(0.3f, 0.9f), 0.8f); }
            if (tp < QT2 - 1f && t >= QT2 - 1f) { flashLamp.transform.position = wp + Vector3.up * 2f; flashLamp.color = new Color(1f, 0.7f, 0.95f); flashLamp.intensity = Lit * 25f; shake = Mathf.Max(shake, 1.8f); for (int k = 0; k < 300; k++) { var v = UnityEngine.Random.onUnitSphere; v.y = Mathf.Abs(v.y); Emit(fx, wp + Vector3.up * 1.5f, v * UnityEngine.Random.Range(6f, 36f), new Color(1f, UnityEngine.Random.Range(0.5f, 0.9f), 1f, 1f), UnityEngine.Random.Range(0.5f, 1.8f), UnityEngine.Random.Range(1f, 2.4f)); } }
        }
        void OceanFx(float t, float tp, float dt, Sc sc, Camera cam)
        {
            bool on = sc == Sc.Ocean; if (oceanGo == null) return; if (oceanGo.activeSelf != on) oceanGo.SetActive(on);
            if (titleGo != null && titleGo.activeSelf != (on && t > 659.5f)) titleGo.SetActive(on && t > 659.5f);
            if (!on) return;
            float flashK = Sm(T_END_VIS - 0.3f, T_END_VIS + 1.2f, t);
            // the sea follows you (it is a big flat disc); its colour opens up after the flash
            Vector3 cp = cam != null ? cam.transform.position : HeadNow(); SeaFrame(t, cp);
            if (!paused && tp < T_END_VIS + 2.5f && t >= T_END_VIS + 2.5f) AddRipple(0f, ISLE_Z, T_END_VIS + 2.5f, 5.5f, 34f, 85f, 90f, 70f);
            if (!paused && tp < T_END_VIS + 30f && t >= T_END_VIS + 30f) AddRipple(0f, ISLE_Z + 20f, T_END_VIS + 30f, 4f, 28f, 60f, 80f, 70f);
 oceanMat.mainTextureOffset = new Vector2((cp.x + t * 0.8f) / 22f, (cp.z + t * 0.55f) / 22f);
            // the island copy
            if (isle != null)
            {
                Vector3 p; float roll; IslePose(t, out p, out roll); bool vis = t < 661f; isle.SetActive(vis);
                isle.transform.localScale = Vector3.one * ISLE_S; isle.transform.position = W(p); isle.transform.rotation = root.transform.rotation * Quaternion.Euler(0f, 0f, -roll) * Quaternion.Euler(0f, (t - T_OCEAN) * 2.5f, 0f);      // the map turns slowly the whole time
                if (vis) { Matrix4x4 hm = isle.transform.localToWorldMatrix; for (int i = 0; i < isleDraws.Count; i++) { var d = isleDraws[i]; Graphics.DrawMesh(d.Mesh, hm * d.Rel, d.Mat, 0, null, d.Sub); } }
            }
            if (isle != null && isle.activeSelf && t > T_END_VIS && t < T_END_VIS + 52f && !paused && dt > 0f)      // water streaming off the rock slab and the rim
            {
                int nn = (int)(220f * dt * Parts) + 1;
                for (int k = 0; k < nn; k++) { float a = UnityEngine.Random.value * 6.2832f, rr = UnityEngine.Random.Range(0.55f, 1f) * ISLE_R, dep = UnityEngine.Random.value; Vector3 lp = new Vector3(Mathf.Cos(a) * rr * (1f - 0.6f * dep), -2f - ISLE_D * dep, Mathf.Sin(a) * rr * (1f - 0.6f * dep)); Vector3 wp = isle.transform.TransformPoint(lp); if (wp.y < root.transform.position.y + WY + 3f) continue; Emit(fx, wp, new Vector3(UnityEngine.Random.Range(-1f, 1f), -UnityEngine.Random.Range(6f, 22f), UnityEngine.Random.Range(-1f, 1f)), new Color(0.85f, 0.95f, 1f, 0.7f), UnityEngine.Random.Range(0.8f, 3.2f), UnityEngine.Random.Range(1.2f, 2.6f)); }
            }
            QueenInWater(t, tp, dt);
            // underwater: bubbles round you until you break the surface (9:25)
            if (t < T_OCEAN + 11f && UnityEngine.Random.value < dt * 80f) { Vector3 hp = HeadNow(); Emit(dust, hp + UnityEngine.Random.insideUnitSphere * 3f, new Vector3(0f, UnityEngine.Random.Range(1.5f, 3.5f), 0f), new Color(0.6f, 0.85f, 1f, 0.5f), UnityEngine.Random.Range(0.06f, 0.2f), UnityEngine.Random.Range(2f, 4f)); }
            if (!paused && tp < T_END_VIS && t >= T_END_VIS) { Sfx(sThunder, W(new Vector3(0f, 200f, ISLE_Z)), 1f, 0.8f, 0.5f); Sfx(sBig, W(new Vector3(0f, 100f, ISLE_Z)), 0.9f, 0.7f, 0.5f); flashLamp.transform.position = W(new Vector3(0f, 30f, 0f)); flashLamp.color = Color.white; flashLamp.intensity = Lit * 25f; shake = Mathf.Max(shake, 1.5f); }
            if (!paused && tp < T_END_VIS + 2.5f && t >= T_END_VIS + 2.5f) { shake = Mathf.Max(shake, 1.2f); Sfx(sRumble, W(new Vector3(0f, 150f, ISLE_Z)), 1f, 0.85f, 1f); Sfx(sSplash, W(new Vector3(0f, 3f, ISLE_Z - 200f)), 1f, 0.6f, 0.5f); }
            if (!paused && tp < T_END_VIS + 26f && t >= T_END_VIS + 26f) { Sfx(sSplash, W(new Vector3(0f, 3f, ISLE_Z)), 1f, 0.55f, 0.5f); Sfx(sBig, W(new Vector3(0f, 20f, ISLE_Z)), 0.8f, 0.8f, 0.5f); }
            if (t > T_END_VIS + 3f && t < T_END_VIS + 52f) shake = Mathf.Max(shake, 0.35f * (1f - Sm(T_END_VIS + 3f, T_END_VIS + 52f, t)));      // the whole island moving: a faint tremor in the water
            // the title card follows your view
            if (titleGo != null && titleGo.activeSelf && cam != null)
            {
                titleGo.transform.position = cam.transform.position + cam.transform.forward * 5f; titleGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up); titleGo.transform.localScale = Vector3.one;
                float[] a0 = { 659.8f, 660.6f, 661.8f }; for (int i = 0; i < 3 && titleTx != null; i++) titleTx[i].color = new Color(1f, 1f, 1f, Sm(a0[i], a0[i] + 1.2f, t) * (1f - Sm(663.2f, 664f, t)));
            }
        }
    }
}
