using System;
using UnityEngine;

namespace AstronomicalEvent
{
    // THE END: the finale's extras. Five blue-white tethers from the sky pull the island over (10:03 - 10:27), a golden pillar follows the Cube Queen from the moment she is thrown until she dies, a looping sea ambience
    // (wind and swell, muffled under the water at 9:15), and a dozen thunder claps while the sky burns. Everything is a function of the music time like the rest of the event.
    internal sealed partial class AstronomicalEnd21
    {
        LineRenderer[] tethers; LineRenderer deathBeam; AudioSource seaSrc; AudioClip seaClip;
        static readonly float[] THUNDER_T = { 557.5f, 563f, 568.5f, 574f, 579.5f, 585f, 590f, 595f, 599.5f, 602.5f, 611f, 618f };

        void FinaleBuild()
        {
            var tm = kit.Glow(new Color(0.75f, 0.9f, 1f, 1f), lineTex, 3002); tethers = new LineRenderer[5];
            for (int i = 0; i < tethers.Length; i++) tethers[i] = MakeLine("ETether" + i, tm, 2, 5f);
            deathBeam = MakeLine("EQueenBeam", kit.Glow(new Color(1f, 0.78f, 0.35f, 1f), lineTex, 3003), 2, 10f);
            // a loopable 8 s bed: low-passed noise under a slow swell, the ends cross-faded so the loop does not click
            int n = 44100 * 8, xf = 8820; var d = new float[n]; var rnd = new System.Random(55); float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++) { float w = (float)(rnd.NextDouble() * 2.0 - 1.0); lp += (w - lp) * 0.02f; lp2 += (lp - lp2) * 0.1f; float sw = 0.55f + 0.45f * Mathf.Sin(6.2832f * 3f * i / n) * Mathf.Sin(6.2832f * 1f * i / n + 1f); d[i] = Mathf.Clamp(lp2 * 14f * sw, -1f, 1f); }
            for (int i = 0; i < xf; i++) { float k = i / (float)xf; d[i] = d[i] * k + d[n - xf + i] * (1f - k); }
            seaClip = MakeSfxClip("ESea", 8f, t => d[Mathf.Min(n - 1, (int)(t * 44100f))]); owned.Add(seaClip);
            var g = new GameObject("ESeaAmb"); g.transform.SetParent(root.transform, false); seaSrc = g.AddComponent<AudioSource>(); seaSrc.clip = seaClip; seaSrc.loop = true; seaSrc.spatialBlend = 0f; seaSrc.playOnAwake = false; seaSrc.volume = 0f;
        }
        void FinaleFrame(float t, float tp, float dt, bool on)
        {
            if (tethers == null) return;
            // the Seven's tethers
            float k = Sm(T_END_VIS + 0.3f, T_END_VIS + 2f, t) * (1f - Sm(T_END_VIS + 22f, T_END_VIS + 26f, t));
            for (int i = 0; i < tethers.Length; i++)
            {
                bool b = on && k > 0.01f; if (tethers[i].enabled != b) tethers[i].enabled = b; if (!b) continue;
                float a = i * 1.2566f + 0.4f; Vector3 rim = IsleAnchor(t, new Vector3(Mathf.Cos(a) * ISLE_R * 0.9f, -6f, Mathf.Sin(a) * ISLE_R * 0.9f));
                tethers[i].SetPosition(0, W(rim + new Vector3(60f * Mathf.Sin(i * 2.3f), 1500f, 80f))); tethers[i].SetPosition(1, W(rim)); tethers[i].widthMultiplier = 14f * k * (1f + 0.25f * Mathf.Sin(t * 9f + i * 1.7f));
            }
            // the Queen's pillar
            bool qb = on && queenGo != null && t > QD && t < QE;
            if (deathBeam.enabled != qb) deathBeam.enabled = qb;
            if (qb) { Vector3 q = queenGo.transform.localPosition; deathBeam.SetPosition(0, W(q)); deathBeam.SetPosition(1, W(q + Vector3.up * 2200f)); deathBeam.widthMultiplier = Mathf.Lerp(3f, 30f, Sm(QL, QL + 1.5f, t)) * (1f - Sm(QE - 4f, QE - 0.3f, t)) * (1f + 0.15f * Mathf.Sin(t * 13f)); }
            // the sea bed of sound
            if (seaSrc != null)
            {
                float vol = on && !paused ? SfxVol * 1.1f * Sm(T_OCEAN, T_OCEAN + 4f, t) * (1f - Sm(659f, 662f, t)) : 0f;
                seaSrc.volume = vol; seaSrc.pitch = Mathf.Lerp(0.55f, 1f, Sm(T_OCEAN + 2f, T_OCEAN + 10f, t));      // muffled and low while you are still under the water
                if (vol > 0.001f && !seaSrc.isPlaying) seaSrc.Play(); else if (vol <= 0.001f && seaSrc.isPlaying) seaSrc.Pause();
            }
            if (on && !paused && dt > 0f) for (int i = 0; i < THUNDER_T.Length; i++) if (tp < THUNDER_T[i] && t >= THUNDER_T[i]) { Sfx(sThunder, W(new Vector3(UnityEngine.Random.Range(-300f, 300f), 400f, UnityEngine.Random.Range(200f, 900f))), 0.8f, UnityEngine.Random.Range(0.75f, 1f), 0.2f); shake = Mathf.Max(shake, 0.4f); }
        }
    }
}
