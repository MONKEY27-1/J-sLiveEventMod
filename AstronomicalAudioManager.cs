using System;
using System.Threading.Tasks;
using UnityEngine;

namespace AstronomicalEvent
{
    // Local-only 2D audio. No event audio exists in the 12.41 paks, so the track is synthesized off-thread.
    // Post-gain on the event AudioSource only (audio thread); soft-clips so boosts above 1.0 do not crackle.
    internal sealed class GainFilter : MonoBehaviour
    {
        public volatile float Gain = 1f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            float g = Gain;
            if (g <= 1.001f)
                return;
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i] * g;
                data[i] = Mathf.Abs(x) < 0.8f ? x : (float)Math.Tanh(x);
            }
        }
    }

    internal sealed class AstronomicalAudioManager
    {
        const int Rate = 22050;
        volatile float[] pcm;
        Task job;
        AudioClip clip;
        GameObject go;
        AudioSource src;
        GainFilter gain;

        void ApplyVol()
        {
            float v = AstronomicalSettings.Volume.Value;
            src.volume = Mathf.Min(1f, v);
            gain.Gain = Mathf.Max(1f, v);
        }

        public string Status = "not started";

        public void Prepare()
        {
            if (job != null || clip != null || external)
                return;
            Status = "synthesizing";
            job = Task.Run(() =>
            {
                try
                {
                    pcm = Synth();
                }
                catch (Exception e)
                {
                    Status = "failed: " + e.Message;
                }
            });
        }

        // Called each frame; creates the clip on the main thread once the worker finishes.
        public void Poll()
        {
            if (clip != null || pcm == null)
                return;
            try
            {
                var data = pcm;
                int pos = 0; // callback form avoids the Span overload of SetData (not in net472 refs)
                clip = AudioClip.Create(
                    "AstronomicalLocalTrack",
                    data.Length,
                    1,
                    Rate,
                    false,
                    buf =>
                    {
                        Array.Copy(data, pos, buf, 0, Math.Min(buf.Length, data.Length - pos));
                        pos += buf.Length;
                    }
                );
                pcm = null;
                Status = "ready";
            }
            catch (Exception e)
            {
                Status = "failed: " + e.Message;
            }
        }

        public bool Ready => clip != null;
        bool external;
        public float Offset; // seconds into the file where the event starts (file is the full video audio) => clip != null ? clip.length : 0f;

        // Real music supplied by the user; owned by the asset loader, not destroyed here.
        public void UseClip(AudioClip c, float offset)
        {
            if (src != null && src.clip != c)
            {
                src.Stop();
                src.clip = c;
            }
            clip = c;
            external = true;
            Offset = offset;
            Status = "music file (" + c.length.ToString("0") + "s)";
        }

        void EnsureSource()
        {
            if (src != null)
                return;
            go = new GameObject("AstronomicalAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.loop = false;
            src.clip = clip;
            src.priority = 0;
            src.ignoreListenerPause = true; // highest priority so the game's many other sounds never virtualise (silence) the music
            gain = go.AddComponent<GainFilter>();
        }

        public void PlayAt(float t)
        {
            if (clip == null)
                return;
            try
            {
                EnsureSource();
                if (src.clip != clip)
                {
                    src.Stop();
                    src.clip = clip;
                } // the source keeps the clip it was created with: a different event's music never played
                ApplyVol();
                src.time = Mathf.Clamp(t + Offset, 0f, clip.length - 0.05f);
                src.Play();
            }
            catch (Exception e)
            {
                Status = "play failed: " + e.Message;
            }
        }

        // Live levels (0..1, self-normalised) from the event's own AudioSource, used to drive visuals.
        public float Bass,
            Mid,
            High,
            Kick;
        public bool KickHit,
            Active;
        readonly float[] spec = new float[1024];
        float bassAvg,
            peakB = 0.001f,
            peakM = 0.001f,
            peakH = 0.001f,
            cooldown;

        public void Analyze(float dt)
        {
            KickHit = false;
            Active = src != null && src.isPlaying;
            Kick = Mathf.Max(0f, Kick - dt * 4f);
            if (!Active)
            {
                Bass = Mid = High = 0;
                return;
            }
            src.GetSpectrumData(spec, 0, FFTWindow.BlackmanHarris);
            float b = 0,
                m = 0,
                h = 0;
            for (int i = 1; i <= 5; i++)
                b += spec[i];
            for (int i = 6; i <= 80; i++)
                m += spec[i];
            for (int i = 81; i <= 400; i++)
                h += spec[i];
            m /= 15f;
            h /= 64f;
            peakB = Mathf.Max(peakB * 0.998f, b);
            peakM = Mathf.Max(peakM * 0.998f, m);
            peakH = Mathf.Max(peakH * 0.998f, h);
            float k = 1f - Mathf.Exp(-dt * 20f);
            Bass = Mathf.Lerp(Bass, b / peakB, k);
            Mid = Mathf.Lerp(Mid, m / peakM, k);
            High = Mathf.Lerp(High, h / peakH, k);
            cooldown -= dt;
            if (cooldown <= 0f && b > bassAvg * 1.45f && b / peakB > 0.35f)
            {
                KickHit = true;
                Kick = 1f;
                cooldown = 0.3f;
            } // <=3.3 Hz
            bassAvg = Mathf.Lerp(bassAvg, b, 1f - Mathf.Exp(-dt * 2f));
        }

        public bool IsPlaying => src != null && src.isPlaying;

        public void Pause()
        {
            if (src != null)
                src.Pause();
        }

        public void Resume()
        {
            if (src != null)
                src.UnPause();
        }

        public void SetVolume()
        {
            if (src != null)
                ApplyVol();
        }

        public float Time => src != null ? src.time - Offset : 0f;

        public void Stop()
        {
            if (src != null)
                src.Stop();
        }

        public void Dispose()
        {
            Stop();
            if (go != null)
                UnityEngine.Object.Destroy(go);
            if (clip != null && !external)
                UnityEngine.Object.Destroy(clip);
            go = null;
            src = null;
            clip = null;
        }

        static float[] Synth()
        {
            float total = AstronomicalSequence.Total;
            int n = (int)(total * Rate) + Rate;
            var d = new float[n];
            var rng = new System.Random(12041);
            const float beat = 60f / 76f;
            const double tau = Math.PI * 2;
            float lp = 0;
            float[] arp = { 220f, 261.6f, 329.6f, 392f, 493.9f, 392f, 329.6f, 261.6f };
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                int idx = AstronomicalSequence.IndexAt((float)t, out float u);
                if (t >= total)
                    idx = -1;
                double s =
                    Math.Sin(tau * 55 * t) * 0.22
                    + Math.Sin(tau * 82.4 * t) * 0.09
                    + Math.Sin(tau * 110 * t * (1 + 0.002 * Math.Sin(0.3 * t))) * 0.06;
                double noise = rng.NextDouble() * 2 - 1;
                double bt = t % beat;
                double kickAmp =
                    idx <= 0 ? 0
                    : idx == 1 ? u * 0.8
                    : idx == 5 ? 0.5
                    : 1;
                if (kickAmp > 0)
                    s += Math.Sin(tau * (45 * bt + 60 * (1 - Math.Exp(-30 * bt)) / 30)) * Math.Exp(-bt * 9) * 0.6 * kickAmp;
                if (idx >= 2 && idx != 5)
                    s += noise * Math.Exp(-((t + beat / 2) % beat) * 60) * 0.1;
                if (idx == 4 || idx == 6)
                {
                    double nt = t % (beat / 2);
                    s += Math.Sin(tau * arp[(int)(t / (beat / 2)) % 8] * t) * Math.Exp(-nt * 6) * 0.1;
                }
                if (idx >= 2)
                    s +=
                        (Math.Sin(tau * 220 * t) + Math.Sin(tau * 329.6 * t) + Math.Sin(tau * 493.9 * t * (1 + 0.003 * Math.Sin(t))))
                        * 0.025;
                if (idx == 0)
                    s += noise * u * 0.08 + Math.Sin(tau * (200 + u * 1500) * t) * u * 0.04;
                if (idx == 5)
                {
                    double ts = t * 8,
                        fr = ts - Math.Floor(ts);
                    double h = Math.Abs(Math.Sin(Math.Floor(ts) * 12.9898) * 43758.5453) % 1;
                    if (h > 0.65)
                        s += Math.Sin(tau * (600 + h * 800) * t) * Math.Exp(-fr * 12) * 0.08;
                }
                if (idx == 6)
                {
                    double ft = t - AstronomicalSequence.Start[6];
                    s += noise * Math.Exp(-ft * 1.5) * 0.25;
                    if (u > 0.75)
                        s *= 1 - (u - 0.75) / 0.25;
                }
                lp += ((float)s - lp) * 0.08f;
                double m = 0;
                if (idx == 5)
                    m = Math.Min(1, Math.Min(u * 15, (1 - u) * 15));
                double outS = s * (1 - m) + lp * m;
                if (idx < 0)
                    outS = 0;
                d[i] = (float)(Math.Tanh(outS * 1.2) * 0.8);
            }
            return d;
        }
    }
}
