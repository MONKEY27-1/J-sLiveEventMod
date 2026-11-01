using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // THE END: sound effects. Everything is synthesised at load (no files): gunshot, laser zap, three explosions (small / big / thunder), shattering glass, water splash, a deep rumble for the island. They play from a pool of
    // 3D sources (so a far explosion is quiet and a near one loud), scale with the "Event volume" setting and are throttled so a crowd of guards firing does not turn into noise. The music keeps the front.
    internal sealed partial class AstronomicalEnd21
    {
        AudioClip sShot, sZap, sBoom, sBig, sThunder, sGlass, sSplash, sRumble; AudioSource[] sPool; int sNext; readonly Dictionary<AudioClip, float> sLast = new Dictionary<AudioClip, float>();
        float SfxVol { get { return Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f) * 0.55f; } }

        static AudioClip MakeSfxClip(string name, float len, Func<float, float> f)
        {
            int n = (int)(44100 * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / 44100f), -1f, 1f);
            int rp = 0; return AudioClip.Create(name, n, 1, 44100, false, buf => { for (int q = 0; q < buf.Length; q++) { buf[q] = d[Mathf.Min(rp, n - 1)]; rp++; } }, pos => { rp = pos; });
        }
        void SfxInit()
        {
            var rnd = new System.Random(21); Func<float> N = () => (float)(rnd.NextDouble() * 2.0 - 1.0);
            float lp = 0f, lp2 = 0f;
            sShot = MakeSfxClip("EShot", 0.28f, t => { float w = N(); lp += (w - lp) * 0.5f; return (w * 0.55f + lp * 0.45f) * Mathf.Exp(-t * 26f) * 0.9f + Mathf.Sin(6.2832f * 170f * t) * Mathf.Exp(-t * 38f) * 0.55f; });
            sZap = MakeSfxClip("EZap", 0.3f, t => { float ph = 6.2832f * (1800f * (1f - Mathf.Exp(-8f * t)) / 8f + 320f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 9f) * 0.55f + Mathf.Sin(ph * 2.01f) * Mathf.Exp(-t * 14f) * 0.2f + N() * 0.06f * Mathf.Exp(-t * 25f); });
            lp = 0f; sBoom = MakeSfxClip("EBoom", 1.7f, t => { lp += (N() - lp) * 0.05f; float ph = 6.2832f * (70f * (1f - Mathf.Exp(-1.6f * t)) / 1.6f + 30f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 2.4f) * 0.8f + lp * 2.2f * Mathf.Exp(-t * 3.2f) + N() * 0.25f * Mathf.Exp(-t * 18f); });
            lp = 0f; lp2 = 0f; sBig = MakeSfxClip("EBig", 3.4f, t => { lp += (N() - lp) * 0.025f; lp2 += (lp - lp2) * 0.2f; float ph = 6.2832f * (48f * (1f - Mathf.Exp(-0.9f * t)) / 0.9f + 22f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 1.3f) * 0.85f + lp2 * 5f * Mathf.Exp(-t * 1.6f) + N() * 0.3f * Mathf.Exp(-t * 12f); });
            lp = 0f; sThunder = MakeSfxClip("EThunder", 3.2f, t => { lp += (N() - lp) * 0.018f; float roll = 0.55f + 0.45f * Mathf.Sin(6.2832f * 4.3f * t) * Mathf.Sin(6.2832f * 1.7f * t); return lp * 7f * roll * Mathf.Exp(-t * 0.85f) + N() * 0.5f * Mathf.Exp(-t * 28f); });
            lp = 0f; var hits = new float[7]; for (int i = 0; i < 7; i++) hits[i] = (float)rnd.NextDouble() * 0.35f; var fr = new float[7]; for (int i = 0; i < 7; i++) fr[i] = 2200f + 3200f * (float)rnd.NextDouble();
            sGlass = MakeSfxClip("EGlass", 1.1f, t => { lp += (N() - lp) * 0.35f; float hp = N() - lp, v = 0f; for (int i = 0; i < 7; i++) { float k = t - hits[i]; if (k >= 0f) v += (hp * 0.5f + Mathf.Sin(6.2832f * fr[i] * k) * 0.35f) * Mathf.Exp(-k * 38f); } return v * 0.45f + hp * 0.25f * Mathf.Exp(-t * 6f); });
            lp = 0f; lp2 = 0f; sSplash = MakeSfxClip("ESplash", 1.4f, t => { float w = N(); lp += (w - lp) * 0.3f; lp2 += (lp - lp2) * 0.5f; return (lp - lp2 * 0.5f) * 2f * (1f - Mathf.Exp(-t * 55f)) * Mathf.Exp(-t * 3.2f); });
            lp = 0f; lp2 = 0f; sRumble = MakeSfxClip("ERumble", 4f, t => { lp += (N() - lp) * 0.012f; lp2 += (lp - lp2) * 0.3f; float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 4f)); return lp2 * 9f * env * env + Mathf.Sin(6.2832f * 36f * t) * 0.25f * env; });
            owned.Add(sShot); owned.Add(sZap); owned.Add(sBoom); owned.Add(sBig); owned.Add(sThunder); owned.Add(sGlass); owned.Add(sSplash); owned.Add(sRumble);
            sPool = new AudioSource[12]; for (int i = 0; i < sPool.Length; i++) { var g = new GameObject("ESfx" + i); g.transform.SetParent(root.transform, false); var a = g.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0.85f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 10f; a.maxDistance = 420f; a.priority = 64; sPool[i] = a; }
        }
        // play one effect at a world position; `gap` is the minimum time between two plays of the same clip
        void Sfx(AudioClip c, Vector3 wp, float vol, float pitch = 1f, float gap = 0.05f)
        {
            if (c == null || sPool == null || paused) return; float now = Time.unscaledTime, last; if (sLast.TryGetValue(c, out last) && now - last < gap) return; sLast[c] = now;
            AudioSource a = null; for (int k = 0; k < sPool.Length; k++) { var s = sPool[(sNext + k) % sPool.Length]; if (!s.isPlaying) { a = s; sNext = (sNext + k + 1) % sPool.Length; break; } }
            if (a == null) { a = sPool[sNext]; sNext = (sNext + 1) % sPool.Length; }
            a.transform.position = wp; a.clip = c; a.volume = Mathf.Clamp01(vol * SfxVol); a.pitch = Mathf.Clamp(pitch * UnityEngine.Random.Range(0.94f, 1.06f), 0.4f, 2f); a.Play();
        }

        // ---- scene captions: a small line of text that fades in at the start of each scene (4:30 facility ... 10:03 the island turns)
        static readonly float[] CAP_T = { 271f, 333f, 357f, 401f, 435.5f, 509f, 557f, 606f };
        static readonly string[] CAP_S = { "I.O. FACILITY", "THE HALLS", "ZERO POINT CONTROL", "THE ISLAND", "BENEATH THE ISLAND", "THE ZERO POINT CHAMBER", "THE SEA", "THE ISLAND TURNS" };
        GameObject capGo; TextMesh capTx;
        void CaptionBuild()
        {
            capGo = new GameObject("EEndCaption"); var g = new GameObject("t"); g.transform.SetParent(capGo.transform, false); capTx = g.AddComponent<TextMesh>(); capTx.anchor = TextAnchor.MiddleCenter; capTx.alignment = TextAlignment.Center; capTx.fontSize = 100; capTx.characterSize = 0.012f; capTx.fontStyle = FontStyle.Bold; capTx.color = new Color(1f, 1f, 1f, 0f);
            Font f = null; try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            if (f != null) { capTx.font = f; var r = g.GetComponent<MeshRenderer>(); if (r != null && f.material != null) { r.sharedMaterial = new Material(f.material); r.sharedMaterial.renderQueue = 4000; owned.Add(r.sharedMaterial); } }
            capGo.SetActive(false);
        }
        void CaptionFrame(float t)
        {
            if (capGo == null) return; int k = -1; for (int i = 0; i < CAP_T.Length; i++) if (t >= CAP_T[i] && t < CAP_T[i] + 4.2f) k = i;
            var cam = Camera.main; bool on = k >= 0 && cam != null; if (capGo.activeSelf != on) capGo.SetActive(on); if (!on) return;
            float a = Sm(CAP_T[k], CAP_T[k] + 0.7f, t) * (1f - Sm(CAP_T[k] + 3.2f, CAP_T[k] + 4.2f, t)); capTx.text = CAP_S[k]; capTx.color = new Color(1f, 1f, 1f, a * 0.9f);
            capGo.transform.position = cam.transform.position + cam.transform.forward * 4f - cam.transform.up * 1.15f; capGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }
        void SfxCleanup() { if (capGo != null) UnityEngine.Object.Destroy(capGo); capGo = null; capTx = null; }
        void SfxCleanup0() { sPool = null; sLast.Clear(); sShot = sZap = sBoom = sBig = sThunder = sGlass = sSplash = sRumble = null; }
    }
}
