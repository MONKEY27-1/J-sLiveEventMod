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
        const float WY = -0.3f, ISLE_Z = 760f, ISLE_R = 258f, ISLE_D = 70f;
        GameObject oceanGo, isle, isleCone, isleDisc, waterGo, titleGo; Material oceanMat, waterMat, coneMat, discMat; Texture2D waveTex, rockTex; TextMesh[] titleTx; Transform c3T;
        struct IsleDraw { public Mesh Mesh; public int Sub; public Material Mat; public Matrix4x4 Rel; }
        readonly List<IsleDraw> isleDraws = new List<IsleDraw>(); bool isleBuilt, mapHidden; int isleSkipped;
        Vector3 floodC;

        // the player's head height in the ocean scene: deep under the water at first, rising to float at the surface by 9:25
        float OceanHeadY(float t) { float rise = Sm(T_OCEAN + 1.5f, T_OCEAN + 11f, t); return Mathf.Lerp(WY - 7.5f, WY + 1.3f, rise) + 0.05f * Mathf.Sin(t * 0.9f); }

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

        void SetupOcean()
        {
            waveTex = kit.Tex(64, 64, (x, y) => { float a = Mathf.Sin(x * 0.4f + y * 0.15f) * 0.5f + 0.5f, b = Mathf.Sin(y * 0.55f - x * 0.2f) * 0.5f + 0.5f, c = Mathf.Sin((x + y) * 0.9f) * 0.5f + 0.5f, w = (a + b + c) / 3f; return new Color(0.82f + 0.18f * w, 0.86f + 0.14f * w, 0.9f + 0.1f * w, 1f); });
            oceanMat = new Material(kit.Flat(new Color(0.03f, 0.09f, 0.2f))); oceanMat.mainTexture = waveTex; try { oceanMat.SetFloat("_Cull", 0f); } catch { } owned.Add(oceanMat);
            oceanGo = new GameObject("EOcean"); oceanGo.transform.SetParent(root.transform, false); oceanGo.AddComponent<MeshFilter>().sharedMesh = MakeDisc(4200f, 72, 22f); var mr = oceanGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = oceanMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; oceanGo.SetActive(false);
            var bc = oceanGo.AddComponent<BoxCollider>(); bc.size = new Vector3(260f, 1f, 260f); bc.center = new Vector3(0f, -0.5f, 0f); oceanGo.layer = floorLayer;
            rockTex = kit.Tex(32, 32, (x, y) => { float n = Mathf.PerlinNoise(x * 0.23f, y * 0.23f) * 0.6f + Mathf.PerlinNoise(x * 0.7f, y * 0.7f) * 0.4f; return new Color(0.55f + 0.3f * n, 0.5f + 0.28f * n, 0.45f + 0.25f * n, 1f); });
            coneMat = new Material(kit.Flat(new Color(0.46f, 0.37f, 0.28f))); coneMat.mainTexture = rockTex; try { coneMat.SetFloat("_Cull", 0f); } catch { } owned.Add(coneMat);
            discMat = new Material(kit.Flat(new Color(0.36f, 0.5f, 0.24f))); discMat.mainTexture = rockTex; try { discMat.SetFloat("_Cull", 0f); } catch { } owned.Add(discMat);
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
            if (isleBuilt) return; isleBuilt = true; isleDraws.Clear();
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
                    if (keep) { isleSkipped++; continue; } list.Add(new KeyValuePair<float, MeshRenderer>(d2, r));
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
            AstronomicalEventMod.Log.LogInfo("End21: the island copy has " + isleDraws.Count + " draws (" + batched + " from static batches, " + plain + " plain, " + isleSkipped + " skipped)");
            // the rock underside and a plain ground disc under everything (so the island has a surface even where the copy is thin)
            isleCone = new GameObject("EIsleCone"); isleCone.transform.SetParent(isle.transform, false); isleCone.AddComponent<MeshFilter>().sharedMesh = MakeCone(ISLE_R, ISLE_D, 50f, 9, 44, 7); var cr = isleCone.AddComponent<MeshRenderer>(); cr.sharedMaterial = coneMat; cr.shadowCastingMode = ShadowCastingMode.Off;
            isleDisc = new GameObject("EIsleDisc"); isleDisc.transform.SetParent(isle.transform, false); isleDisc.transform.localPosition = new Vector3(0f, -0.9f, 0f); isleDisc.AddComponent<MeshFilter>().sharedMesh = MakeDisc(ISLE_R, 48, 40f); var dr = isleDisc.AddComponent<MeshRenderer>(); dr.sharedMaterial = discMat; dr.shadowCastingMode = ShadowCastingMode.Off;
            if (ch3M != null)
            {
                var p = ch3M.Pivot; p.SetActive(true); p.transform.SetParent(isle.transform, false); float sc = 1.9f; p.transform.localScale = Vector3.one * sc; p.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                p.transform.localPosition = new Vector3(0f, -48f, 0f); c3T = p.transform;      // the island's seabed sits inside the cone's narrow end, its mountains reach 50 m past the tip: after the roll it stands on top, its coast (about 27 m above its seabed) is at sea level when h = -75
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
            else { roll = 90f + 90f * Sm(T_END_VIS + 1.5f, T_END_VIS + 25f, t); h = Mathf.Lerp(262f, -75f, Sm(T_END_VIS + 2.5f, T_END_VIS + 52f, t)); }
            float z = ISLE_Z - 330f * Sm(T_END_VIS + 18f, T_END_VIS + 50f, t);        // after the flip the island drifts towards you: Chapter 3 is what stays on the sea
            pos = new Vector3(0f, WY + h, z); rollDeg = roll;
        }
        // the Cube Queen falls out of the sky into the sea (9:32 - 9:38), thrashes as the water takes her and sinks, a last flash at 9:58
        const float QT0 = 572f, QT1 = 578f, QT2 = 600f;
        void QueenInWater(float t, float tp, float dt)
        {
            if (queenM == null || queenGo == null) return; bool on = t > T_OCEAN - 0.5f && t < QT2 + 0.5f; if (!on) { if (queenGo.activeSelf && SceneAt(t) == Sc.Ocean) queenGo.SetActive(false); return; }
            float fall = Mathf.Clamp01((t - QT0) / (QT1 - QT0)), sink = Sm(QT1 + 2f, QT2 - 2f, t); float y = WY + Mathf.Lerp(230f, 0f, fall * fall) - 18f * sink * sink + 0.8f * Mathf.Sin(t * 2.2f) * (1f - sink);
            float tilt = Mathf.Lerp(0f, 55f, sink) + 6f * Mathf.Sin(t * 3f) * (1f - fall), scale = Mathf.Lerp(140f / 1.8f, 10f, Sm(QT0, QT1, t)); Vector3 pos = new Vector3(22f, y, 85f);
            Vector3 orbQ = new Vector3(OC_PX, OC_PY + 150f * OC_S, OC_PZ); if (t < QT1) { float u = Mathf.Clamp01((t - QT0) / (QT1 - QT0)); pos = new Vector3(Mathf.Lerp(orbQ.x, 22f, u), Mathf.Lerp(orbQ.y, WY, u * u) , Mathf.Lerp(orbQ.z, 85f, u)); tilt = 0f; }      // she starts in the tower's orb, as in the rest of the event
            PlaceGo(queenGo, pos, scale, Quaternion.Euler(tilt, 180f + 12f * Mathf.Sin(t * 0.8f), 8f * Mathf.Sin(t * 1.7f) * sink), true);
            string qc = t < QT0 ? "H_Float" : t < QT1 ? "H_Retreat" : (t < QT1 + 8f ? "H_Hit" : "H_Rage"); float lt = t < QT0 ? t : t < QT1 ? Mathf.Min(t - QT0, 3.9f) : (t < QT1 + 8f ? (t - QT1) % 1.6f : (t - QT1 - 8f) % 3f); PlayClip(queenRig, queenM, qc, lt);
            if (paused || dt <= 0f) return; Vector3 wp = W(new Vector3(pos.x, WY, pos.z));
            if (tp < QT1 && t >= QT1) { flashLamp.transform.position = wp + Vector3.up * 6f; flashLamp.color = new Color(0.7f, 0.85f, 1f); flashLamp.intensity = Lit * 18f; shake = Mathf.Max(shake, 1.5f); for (int k = 0; k < 260; k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(0f, 14f); Emit(fx, wp + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r), new Vector3(Mathf.Cos(a) * r * 0.5f, UnityEngine.Random.Range(8f, 34f), Mathf.Sin(a) * r * 0.5f), new Color(0.8f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.6f, 2.2f), UnityEngine.Random.Range(1.2f, 2.6f)); } }
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
            Vector3 cp = cam != null ? cam.transform.position : HeadNow(); oceanGo.transform.position = new Vector3(cp.x, W(new Vector3(0f, WY, 0f)).y, cp.z); oceanGo.transform.rotation = Quaternion.identity;
            oceanMat.color = Color.Lerp(new Color(0.26f, 0.17f, 0.34f), new Color(0.07f, 0.45f, 0.82f), flashK);      // dusk sea: purple, reflecting the sky oceanMat.mainTextureOffset = new Vector2((cp.x + t * 0.8f) / 22f, (cp.z + t * 0.55f) / 22f);
            // the island copy
            if (isle != null)
            {
                Vector3 p; float roll; IslePose(t, out p, out roll); bool vis = t < 661f; isle.SetActive(vis);
                isle.transform.position = W(p); isle.transform.rotation = root.transform.rotation * Quaternion.Euler(0f, 0f, -roll) * Quaternion.Euler(0f, (t - T_OCEAN) * 2.5f, 0f);      // the map turns slowly the whole time
                if (vis) { Matrix4x4 hm = isle.transform.localToWorldMatrix; for (int i = 0; i < isleDraws.Count; i++) { var d = isleDraws[i]; Graphics.DrawMesh(d.Mesh, hm * d.Rel, d.Mat, 0, null, d.Sub); } }
            }
            QueenInWater(t, tp, dt);
            // underwater: bubbles round you until you break the surface (9:25)
            if (t < T_OCEAN + 11f && UnityEngine.Random.value < dt * 80f) { Vector3 hp = HeadNow(); Emit(dust, hp + UnityEngine.Random.insideUnitSphere * 3f, new Vector3(0f, UnityEngine.Random.Range(1.5f, 3.5f), 0f), new Color(0.6f, 0.85f, 1f, 0.5f), UnityEngine.Random.Range(0.06f, 0.2f), UnityEngine.Random.Range(2f, 4f)); }
            if (!paused && tp < T_END_VIS && t >= T_END_VIS) { flashLamp.transform.position = W(new Vector3(0f, 30f, 0f)); flashLamp.color = Color.white; flashLamp.intensity = Lit * 25f; shake = Mathf.Max(shake, 1.5f); }
            if (!paused && tp < T_END_VIS + 2.5f && t >= T_END_VIS + 2.5f) shake = Mathf.Max(shake, 1.2f);
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
