using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 7: the beats of the new recording that need their own effects, keyed on the REAL music time (tReal):
    //  - the bus COCKPIT shots (6:04 - 6:14 and 7:52 - 8:10: you sit in the bus, the face fills the windshield, Iron Man outside),
    //  - the convoy's explosions / green energy burst (4:35 - 6:04), the Gamma Cell beam (6:20), Wolverine's claw swipes (7:30 - 7:35),
    //  - the dimensional rift (8:48 - 9:00): a blue core and a web of cracks that closes to a point.
    internal sealed partial class AstronomicalGalactus
    {
        float tRealPrev = -1f;
        bool CockpitNow() { return (tReal >= 364f && tReal < 376f) || (tReal >= 472f && tReal < 491f); }
        // ---- Gamma Cell beam + rift: lines and glows built once
        LineRenderer gammaBeam; Transform gammaGlow, riftCore; readonly List<LineRenderer> riftLines = new List<LineRenderer>(); readonly List<Vector3> riftDirs = new List<Vector3>(); Material beamMat, riftMat; bool moreBuilt;
        void BuildMore()
        {
            moreBuilt = true; beamMat = kit.Glow(new Color(0.4f, 1f, 0.5f, 0.9f), lineTex, 3002); gammaBeam = MakeLine("GGammaBeam", beamMat, 2, 3f); gammaBeam.enabled = false;
            gammaGlow = Quad("GGammaGlow", kit.Glow(new Color(0.4f, 1f, 0.5f, 1f), glowTex, 3003)); gammaGlow.gameObject.SetActive(false);
            riftMat = kit.Glow(new Color(0.55f, 0.8f, 1f, 0.95f), lineTex, 3002); riftCore = Quad("GRiftCore", kit.Glow(new Color(0.6f, 0.85f, 1f, 1f), glowTex, 3003)); riftCore.gameObject.SetActive(false);
            for (int i = 0; i < 30; i++) { var l = MakeLine("GRift" + i, riftMat, 12, 2.2f); l.enabled = false; riftLines.Add(l); var d = new Vector3(Hash(i, 61) - 0.5f, (Hash(i, 62) - 0.5f) * 0.8f, Hash(i, 63) - 0.5f); riftDirs.Add(d.sqrMagnitude < 0.01f ? Vector3.up : d.normalized); }
        }
        void MoreCleanup() { moreBuilt = false; gammaBeam = null; gammaGlow = null; riftCore = null; riftLines.Clear(); riftDirs.Clear(); }
        void MoreFrame(float t, float dt, Camera cam)
        {
            if (!moreBuilt) BuildMore(); float tp = tRealPrev < 0f ? tReal : tRealPrev; tRealPrev = tReal; float T = tReal;
            // 4:35 - 6:04 the convoy under fire: a giant hand sweeps near the buses (5:00, 5:20), the energy builds (5:05, 5:15), the green burst 5:40, the big burst 5:45
            if (!paused && dt > 0f)
            {
                Func<float, bool> at = x => tp < x && T >= x; Vector3 ahead = BusPos + new Vector3(0f, 0f, 70f);
                foreach (float bt in new[] { 300f, 305f, 315f, 320f, 322f, 330f, 335f }) if (at(bt)) { Boom(W(ahead + new Vector3(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-4f, 14f), UnityEngine.Random.Range(-20f, 30f))), 16f, 0.8f, new Color(1f, 0.55f, 0.35f)); }
                if (at(340f)) { Boom(W(ahead + new Vector3(0f, 6f, 20f)), 46f, 1.5f, new Color(0.4f, 1f, 0.5f)); for (int k = 0; k < 90; k++) Emit(fx, W(ahead + new Vector3(0f, 6f, 20f)) + UnityEngine.Random.insideUnitSphere * 12f, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(30f, 140f), new Color(0.45f, 1f, 0.55f, 0.95f), UnityEngine.Random.Range(2f, 6f), 2f); }
                if (at(345f)) { Boom(W(ahead + new Vector3(0f, 4f, 40f)), 70f, 2.2f, new Color(0.7f, 0.9f, 1f)); flashLamp.intensity = Mathf.Max(flashLamp.intensity, 6f * Lit); }
                // 7:30 - 7:35 Wolverine clears the drones with his claws: a swipe every 0.4 s, sparks and small explosions at the claws
                if (T >= 449.5f && T < 455f) { int n0 = (int)(tp * 2.5f), n1 = (int)(T * 2.5f); if (n1 != n0) { Act wv; if (actBy.TryGetValue("wolv", out wv) && wv != null && wv.Go.activeSelf) { Vector3 c = wv.Go.transform.position + Vector3.up * 1.4f + wv.Go.transform.forward * 1.2f; for (int k = 0; k < 18; k++) Emit(fx, c + UnityEngine.Random.insideUnitSphere * 0.8f, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 22f), Color.Lerp(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.4f, 0.3f, 1f), UnityEngine.Random.value), UnityEngine.Random.Range(0.3f, 0.9f), 0.7f); Boom(c + wv.Go.transform.forward * 5f + new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(0f, 5f), UnityEngine.Random.Range(0f, 8f)), 5f, 0.5f, new Color(1f, 0.6f, 0.3f)); } } }
            }
            // 6:18 - 6:26 the Gamma Cell fires a green beam from the roof at the giant's arm
            bool gb = T >= 377f && T < 388f; if (gammaBeam != null) { if (gammaBeam.enabled != gb) gammaBeam.enabled = gb; if (gammaGlow.gameObject.activeSelf != gb) gammaGlow.gameObject.SetActive(gb); }
            if (gb && gammaBeam != null)
            {
                Vector3 o = BusPos + new Vector3(0f, 1.8f, -7.5f), e = BusPos + new Vector3(UnityEngine.Random.Range(-3f, 3f), 14f, 380f); float k = Sm(377f, 379f, T) * (1f - Sm(386f, 388f, T)); gammaBeam.SetPosition(0, W(o)); gammaBeam.SetPosition(1, W(e)); gammaBeam.widthMultiplier = (2.5f + 2f * Mathf.Sin(T * 30f)) * k; beamMat.color = new Color(0.4f, 1f, 0.5f, 0.9f * k);
                if (cam != null) Glow(gammaGlow, W(o), 16f * k, new Color(0.45f, 1f, 0.55f, 0.9f * k), cam, true);
            }
            // 8:48 - 9:00 the dimensional rift: a blue core, cracks spreading in a web, then everything closes to a point
            bool rf = T >= 526f && T < 541f; if (riftCore != null && riftCore.gameObject.activeSelf != rf) riftCore.gameObject.SetActive(rf);
            for (int i = 0; i < riftLines.Count; i++) if (riftLines[i].enabled != rf) riftLines[i].enabled = rf;
            if (rf)
            {
                Vector3 c = BusPos + new Vector3(0f, 6f, 60f); float grow = Sm(526f, 532f, T), shrink = Sm(536f, 541f, T), L = 260f * grow * (1f - 0.97f * shrink);
                for (int i = 0; i < riftLines.Count; i++)
                {
                    var l = riftLines[i]; int n = l.positionCount; Vector3 d = riftDirs[i], side = Vector3.Cross(d, Vector3.up); if (side.sqrMagnitude < 1e-3f) side = Vector3.right; side.Normalize();
                    for (int p = 0; p < n; p++) { float s = p / (float)(n - 1); Vector3 q = c + d * (L * s) + (side * Mathf.Sin(s * 13f + i * 1.7f + T * 3f) + Vector3.Cross(d, side) * Mathf.Cos(s * 9f + i)) * (9f * s * grow); l.SetPosition(p, W(q)); }
                    l.widthMultiplier = Mathf.Lerp(3.2f, 0.6f, (float)(i % 5) / 4f) * (1f - 0.7f * shrink);
                }
                riftMat.color = new Color(0.55f, 0.82f, 1f, 0.95f * (1f - 0.5f * shrink));
                if (cam != null) Glow(riftCore, W(c), (30f + 120f * grow * (1f - shrink) + 8f * Mathf.Sin(T * 9f)) , new Color(0.6f, 0.85f, 1f, 1f), cam, true);
            }
        }
    }
}
