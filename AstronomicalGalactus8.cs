using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 8: the WORLD around the beats of the recording - two suns and a warm haze over a cloud sea in the opening, the island far below during the fall,
    // a cloud sea and Galactus' cable-like tendrils round the convoy (4:35 - 6:20), the mouth opening dark and glowing (8:15 - 8:27), and the Astronomical planet behind-left in space.
    internal sealed partial class AstronomicalGalactus
    {
        GltfModel islandM, mawM; readonly List<Renderer> airHid = new List<Renderer>(); GameObject planetGo; Transform sunA, sunB; readonly List<Transform> cloudQ = new List<Transform>(); readonly List<Vector3> cloudP = new List<Vector3>(); readonly List<float> cloudS = new List<float>();
        readonly List<LineRenderer> tendrils = new List<LineRenderer>(); readonly List<Vector3> tendrilDir = new List<Vector3>(); Material tendrilMat; bool envBuilt; AstronomicalAssetLoader ldr;
        void BuildEnv()
        {
            envBuilt = true;
            sunA = Quad("GSunA", kit.Glow(new Color(1f, 0.93f, 0.78f, 1f), glowTex, 3001)); sunB = Quad("GSunB", kit.Glow(new Color(1f, 0.88f, 0.7f, 1f), glowTex, 3001)); sunA.gameObject.SetActive(false); sunB.gameObject.SetActive(false);
            for (int i = 0; i < 90; i++) { var q = Quad("GCloud" + i, kit.Glow(new Color(1f, 0.97f, 0.97f, 0.5f), glowTex, 1600)); q.gameObject.SetActive(false); cloudQ.Add(q); float a = Hash(i, 71) * 6.283f, r = 90f + 650f * Mathf.Pow(Hash(i, 72), 0.7f); cloudP.Add(new Vector3(Mathf.Cos(a) * r, (Hash(i, 73) - 0.5f) * 40f, Mathf.Sin(a) * r)); cloudS.Add(140f + 260f * Hash(i, 74)); }
            tendrilMat = kit.Glow(new Color(0.95f, 0.7f, 1f, 0.55f), lineTex, 3002);
            for (int i = 0; i < 16; i++) { var l = MakeLine("GTendril" + i, tendrilMat, 18, 1.6f); l.enabled = false; tendrils.Add(l); var d = new Vector3(Hash(i, 81) - 0.5f, (Hash(i, 82) - 0.5f) * 1.2f, 0.3f + Hash(i, 83) * 0.8f); tendrilDir.Add(d.normalized); }
            if (mawM != null) mawM.Pivot.SetActive(false); ClearAir();
            if (islandM != null) islandM.Pivot.SetActive(false);
            if (ldr != null && ldr.World != null && ldr.World.Pivot != null) { planetGo = UnityEngine.Object.Instantiate(ldr.World.Pivot, root.transform); planetGo.name = "GAstroPlanet"; planetGo.SetActive(false); clones.Add(planetGo); }
        }
        // The deck is only ~100 m up now, where the real map still has tall things: the Sky Jungle trunk and its cloud base stand right over the forest spawn (they were the "clouds in the way").
        // Hide every real-map renderer that reaches into the event's airspace (top more than 60 m above the ground point, within 450 m), and put them back when the event ends.
        void ClearAir()
        {
            try
            {
                Vector3 o = root.transform.position;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (r == null || !r.enabled || r is ParticleSystemRenderer || r.transform.IsChildOf(root.transform)) continue; var b = r.bounds; if (b.max.y < o.y + 60f || b.size.y < 10f || b.size.x > 700f || b.size.z > 700f) continue;   // not ground-level things, the player's own rig, or sky domes
                    float dx = Mathf.Max(0f, Mathf.Abs(b.center.x - o.x) - b.extents.x), dz = Mathf.Max(0f, Mathf.Abs(b.center.z - o.z) - b.extents.z); if (dx * dx + dz * dz > 450f * 450f) continue;
                    r.enabled = false; airHid.Add(r); JsLiveEventsMod.Log.LogInfo("Galactus: hiding '" + r.name + "' (top " + b.max.y.ToString("0") + ", size " + b.size.ToString("0") + ") - it stands in the event's airspace");
                }
            }
            catch (Exception e) { JsLiveEventsMod.Log.LogWarning("Galactus: ClearAir " + e.Message); }
        }
        void EnvCleanup() { foreach (var r in airHid) if (r != null) r.enabled = true; airHid.Clear(); envBuilt = false; cloudQ.Clear(); cloudP.Clear(); cloudS.Clear(); tendrils.Clear(); tendrilDir.Clear(); planetGo = null; sunA = sunB = null; }
        void EnvFrame(float t, float dt, Camera cam)
        {
            if (!envBuilt) BuildEnv(); if (cam == null) return; float T = tReal; Vector3 cp = cam.transform.position;
            // ---- the opening: two suns low over the haze (0:00 - 1:05), a cloud sea round the carrier (until the strike) and again under the convoy (4:35 - 6:20)
            float sunK = (1f - Sm(48f, 66f, T)); bool sunOn = T < 66f && sunK > 0.01f;
            if (sunA != null) { if (sunA.gameObject.activeSelf != sunOn) { sunA.gameObject.SetActive(sunOn); sunB.gameObject.SetActive(sunOn); } if (sunOn) { Vector3 dA = root.transform.TransformDirection(new Vector3(0.42f, 0.36f, 0.83f).normalized), dB = root.transform.TransformDirection(new Vector3(0.5f, 0.1f, 0.86f).normalized); Glow(sunA, cp + dA * 420f, 120f, new Color(1f, 0.93f, 0.78f, 0.9f * sunK), cam, true); Glow(sunB, cp + dB * 420f, 85f, new Color(1f, 0.88f, 0.7f, 0.85f * sunK), cam, true); } }
            bool cloudsDeck = false, cloudsBus = false; /* the real map is the ground now */ bool cl = cloudsDeck || cloudsBus; float cy = cloudsDeck ? IY - 135f : BusPos.y - 70f; Vector3 cc = cloudsDeck ? W(new Vector3(0f, cy, 0f)) : W(new Vector3(BusPos.x, cy, BusPos.z));
            float flow = cloudsBus ? -(T - 270f) * 30f : t * 1.5f;
            for (int i = 0; i < cloudQ.Count; i++)
            {
                var q = cloudQ[i]; if (q.gameObject.activeSelf != cl) q.gameObject.SetActive(cl); if (!cl) continue; Vector3 p = cloudP[i]; float zz = Mathf.Repeat(p.z + flow + 700f, 1400f) - 700f; Vector3 wp = cc + new Vector3(p.x, p.y, zz); float al = 0.28f * (1f - Mathf.Abs(zz) / 760f) * (cloudsDeck ? 1f - Sm(150f, 176f, t) : Sm(270f, 278f, T) * (1f - Sm(372f, 380f, T)));
                Glow(q, wp, cloudS[i], new Color(1f, 0.96f, 0.96f, Mathf.Max(0f, al)), cam, al > 0.01f);
            }
            // ---- Iron Man's jetpack (3:00 - 3:35): twin thrust flames under you while you fall; they trail downwards
            if (!paused && dt > 0f && t >= 196f && t < 272f) { float kj = Sm(196f, 204f, t); for (int q = 0; q < 3; q++) { Vector3 sd = cam.transform.right * ((q % 2 == 0 ? -1f : 1f) * 0.22f); Vector3 fp = cp + Vector3.down * 1.05f + sd + UnityEngine.Random.insideUnitSphere * 0.06f; Emit(fx, fp, Vector3.down * UnityEngine.Random.Range(10f, 16f), Color.Lerp(new Color(1f, 0.55f, 0.15f, 0.9f * kj), new Color(0.6f, 0.8f, 1f, 0.9f * kj), UnityEngine.Random.value), UnityEngine.Random.Range(0.25f, 0.5f), UnityEngine.Random.Range(0.4f, 0.8f)); } }
            // ---- the island far below during the fall (2:08 - 3:34)
            if (islandM != null) { bool on = false; var go = islandM.Pivot; if (go.activeSelf != on) go.SetActive(on); if (on) { go.transform.localPosition = new Vector3(0f, IY - 1900f, 260f); go.transform.localRotation = Quaternion.Euler(0f, 25f, 0f); go.transform.localScale = Vector3.one; } }
            // ---- Galactus' tendrils round the convoy (4:40 - 6:20): thin glowing cables from his body, drifting
            bool tn = T >= 278f && T < 382f; float tk = Sm(278f, 292f, T) * (1f - Sm(372f, 382f, T));
            for (int i = 0; i < tendrils.Count; i++)
            {
                var l = tendrils[i]; if (l.enabled != tn) l.enabled = tn; if (!tn || gxChestP == null) continue; Vector3 o = gxChestP.position + root.transform.TransformDirection(new Vector3((Hash(i, 84) - 0.5f) * 140f, (Hash(i, 85) - 0.5f) * 120f, 0f)), d = root.transform.TransformDirection(tendrilDir[i] * new Vector3(1f, 1f, -1f).magnitude); d = root.transform.TransformDirection(new Vector3(tendrilDir[i].x, tendrilDir[i].y, -Mathf.Abs(tendrilDir[i].z)));
                Vector3 side = Vector3.Cross(d, Vector3.up).normalized; int n = l.positionCount; for (int p = 0; p < n; p++) { float s = p / (float)(n - 1); l.SetPosition(p, o + d * (520f * s) + (side * Mathf.Sin(s * 6f + i + T * 0.6f) + Vector3.up * Mathf.Cos(s * 5f + i * 1.3f + T * 0.5f)) * (60f * s)); }
                l.widthMultiplier = 2.6f * tk;
            }
            tendrilMat.color = new Color(0.95f, 0.7f, 1f, 0.5f * tk);
            // ---- the mouth opens (8:15 - 8:27): a dark opening growing over the mouth, the glow of the core behind it is GxFx's mouth quad
            bool md = T >= 492f && T < 509f && gxMouthP != null && mawM != null; if (mawM != null && mawM.Pivot.activeSelf != md) mawM.Pivot.SetActive(md);
            if (md) { float k = Sm(493f, 500f, T) * (1f - Sm(506f, 509f, T)); Vector3 mp = gxMouthP.position; Vector3 toCam = (cp - mp).normalized; var tr = mawM.Pivot.transform; float u = gxS / Mathf.Max(0.05f, mawM.RendererBounds.size.x); tr.position = mp + toCam * (gxS * 0.45f); tr.rotation = Quaternion.LookRotation(toCam, root.transform.up); tr.localScale = new Vector3(6.2f * k * u, 4.6f * k * u, 2f * u); }
            // ---- space: the Astronomical planet, behind you to your left (the bus flies +z), turning slowly
            bool sp = t >= 508f && t < 604f; if (planetGo != null) { if (planetGo.activeSelf != sp) planetGo.SetActive(sp); if (sp) { planetGo.transform.localPosition = BusPos + new Vector3(-430f, 120f, -430f); planetGo.transform.localRotation = Quaternion.Euler(12f, t * 1.5f, 6f); float ps = 900f / 90f; planetGo.transform.localScale = Vector3.one * ps; } }
        }
    }
}
