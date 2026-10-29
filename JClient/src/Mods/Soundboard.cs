using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace JClient
{
    // Plays built-in synth sounds and files from plugins/JClient/Sounds, locally and (optionally) through your mic.
    public static class Soundboard
    {
        static AudioSource src;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static Mod mic, loop, volume;
        static Coroutine revert;
        public static AudioClip Last;
        public static string Folder => Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "Sounds");

        // ---- synth ----
        const int Rate = 44100;
        static AudioClip Synth(string name, float seconds, Func<float, float> wave)
        {
            int n = (int)(Rate * seconds); var data = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; data[i] = Mathf.Clamp(wave(t) * Mathf.Clamp01((seconds - t) * 20f), -1f, 1f); } // short fade-out avoids clicks
            int pos = 0; // fill through the reader callback: SetData's Span overload doesn't resolve against net472
            return AudioClip.Create(name, n, 1, Rate, false, buf => { for (int i = 0; i < buf.Length; i++) buf[i] = pos < n ? data[pos++] : 0f; }, p => pos = p);
        }
        static float Sq(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t) > 0 ? 0.5f : -0.5f;
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);

        static readonly Dictionary<string, Func<AudioClip>> synths = new Dictionary<string, Func<AudioClip>>
        {
            { "Airhorn", () => Synth("Airhorn", 1.6f, t => (Sq(415, t) + Sq(523, t) + Sq(622, t)) * 0.3f * (t % 0.55f < 0.45f ? 1 : 0)) },
            { "Beep", () => Synth("Beep", 0.25f, t => Sin(1000, t) * 0.6f) },
            { "Laser", () => Synth("Laser", 0.4f, t => Sq(1800 - t * 3500, t) * 0.5f) },
            { "Drum Kick", () => Synth("Kick", 0.4f, t => Sin(120 * Mathf.Exp(-t * 12f) + 40, t) * Mathf.Exp(-t * 7f)) },
            { "Siren", () => Synth("Siren", 2f, t => Sq(600 + 300 * Mathf.Sin(t * 6f), t) * 0.4f) },
            { "Coin", () => Synth("Coin", 0.35f, t => Sq(t < 0.08f ? 988 : 1319, t) * 0.4f) },
            { "Explosion", () => Synth("Explosion", 1.2f, t => Noise() * Mathf.Exp(-t * 3f)) },
            { "Power Up", () => Synth("PowerUp", 0.7f, t => Sq(300 + t * 900 + (t * 30 % 1) * 200, t) * 0.4f) },
            { "Error", () => Synth("Error", 0.5f, t => Sq(t < 0.2f ? 200 : 150, t) * 0.5f) },
            { "Victory", () => Synth("Victory", 1.2f, t => Sq(new[] { 523f, 659f, 784f, 1047f }[Mathf.Min(3, (int)(t / 0.2f))], t) * 0.4f) },
            { "Bonk", () => Synth("Bonk", 0.3f, t => Sin(300 * Mathf.Exp(-t * 6f) + 100, t) * Mathf.Exp(-t * 10f)) },
            { "Monke Ooh", () => Synth("Ooh", 0.8f, t => (Sin(250 + 80 * Mathf.Sin(t * 8f), t) + 0.4f * Sin(500, t)) * 0.4f) },
        };

        static AudioSource Src
        {
            get
            {
                if (src == null) { var g = new GameObject("JClientSound"); UnityEngine.Object.DontDestroyOnLoad(g); src = g.AddComponent<AudioSource>(); src.spatialBlend = 0f; }
                return src;
            }
        }

        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            Last = clip;
            var s = Src; s.Stop(); s.clip = clip; s.loop = loop.On; s.volume = new[] { 0.25f, 0.5f, 0.75f, 1f }[volume.Opt]; s.Play();
            if (mic.On && Game.InRoom) ToMic(clip);
        }

        static Recorder Rec => NetworkSystem.Instance?.VoiceConnection?.PrimaryRecorder;

        static void ToMic(AudioClip clip)
        {
            var r = Rec; if (r == null) { Hud.Toast("Voice recorder not ready"); return; }
            r.SourceType = Recorder.InputSourceType.AudioClip; r.AudioClip = clip; r.LoopAudioClip = loop.On; r.RestartRecording(true);
            if (revert != null) Plugin.I.StopCoroutine(revert);
            if (!loop.On) revert = Plugin.I.StartCoroutine(BackToMic(clip.length + 0.2f));
        }

        static IEnumerator BackToMic(float after) { yield return new WaitForSeconds(after); MicBack(); }
        static void MicBack()
        {
            var r = Rec; if (r == null || r.SourceType == Recorder.InputSourceType.Microphone) return;
            r.SourceType = Recorder.InputSourceType.Microphone; r.RestartRecording(true);
        }

        public static void Stop() { if (src != null) src.Stop(); if (revert != null) Plugin.I.StopCoroutine(revert); MicBack(); }

        static void PlayFile(string path)
        {
            if (cache.TryGetValue(path, out var c)) { Play(c); return; }
            Plugin.I.StartCoroutine(Load(path));
        }

        static IEnumerator Load(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var type = ext == ".mp3" ? AudioType.MPEG : ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV;
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace('\\', '/'), type))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Hud.Toast("Could not load " + Path.GetFileName(path)); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(req); clip.name = Path.GetFileNameWithoutExtension(path);
                cache[path] = clip; Play(clip);
            }
        }

        public static void Register()
        {
            const string c = "Soundboard";
            mic = Mods.T(c, "Play Through Mic").Info("Sounds also go out through your voice chat so others hear them");
            loop = Mods.T(c, "Loop Sound").Info("Repeat the sound until you press Stop Sound");
            volume = Mods.C(c, "Sound Volume", "25%", "50%", "75%", "100%").Def(2).Info("How loud sounds play for you");
            Mods.B(c, "Stop Sound", Stop).Info("Stops the current sound and gives your mic back");
            Mods.B(c, "Random Sound", () =>
            {
                var keys = synths.Keys.ToList(); var files = Files();
                int i = UnityEngine.Random.Range(0, keys.Count + files.Count);
                if (i < keys.Count) Play(Synth(keys[i])); else PlayFile(files[i - keys.Count]);
            }).Info("Plays a random sound");
            Mods.B(c, "Open Sounds Folder", () => { Directory.CreateDirectory(Folder); Application.OpenURL("file:///" + Folder.Replace('\\', '/')); })
                .Info("Opens the folder: drop .mp3/.wav/.ogg files in it and restart the game to get a button for each");

            foreach (var kv in synths) { Mods.B(c, "Sound: " + kv.Key, () => Play(Synth(kv.Key))).Info("Built-in " + kv.Key.ToLowerInvariant() + " sound"); }
            foreach (var f in Files()) { var path = f; Mods.B(c, "Sound: " + Path.GetFileNameWithoutExtension(f), () => PlayFile(path)).Info("Sound file from the Sounds folder"); }
        }

        static AudioClip Synth(string key) { if (!cache.TryGetValue("synth:" + key, out var c)) cache["synth:" + key] = c = synths[key](); return c; }

        static List<string> Files()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                return Directory.GetFiles(Folder).Where(f => new[] { ".mp3", ".wav", ".ogg" }.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f).ToList();
            }
            catch { return new List<string>(); }
        }
    }
}
