using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JClient
{
    // More Utilities: route recording, lap timing, mod presets, power-up style mod tools, screenshots and comfort settings.
    public static class UtilitiesMore
    {
        static readonly string[] MoveCats = { "Movement", "Flight", "Parkour", "Teleport", "Experimental" };

        // Whole-game volume for you only; two modules share it (Game Volume and Mute In Background)
        static float volume = 1f;
        static bool muted;

        static void ApplyVolume() => AudioListener.volume = volume * (muted ? 0f : 1f);

        public static void Register()
        {
            const string c = "Utilities";

            var route = new List<Vector3>();
            float recTick = 0f;
            Mod recorder = null;
            recorder = Mods.T(
                    c,
                    "Route Recorder",
                    () =>
                    {
                        if ((recTick -= Time.deltaTime) > 0f || route.Count >= 6000) // 10 minutes at 10 points a second
                            return;
                        recTick = 0.1f;
                        route.Add(Game.P.transform.position);
                    },
                    () =>
                    {
                        route.Clear();
                        recTick = 0f;
                    },
                    () => Hud.Toast("Route saved: " + (route.Count / 10f).ToString("0") + " s")
                )
                .Live(() => "Route Recorder" + (recorder.On ? " (recording " + (route.Count / 10f).ToString("0") + " s)" : ""))
                .Info("While on, records the path you move along (up to 10 minutes) for Route Autopilot");

            float playT = 0f;
            Mods.T(
                    c,
                    "Route Autopilot",
                    () =>
                    {
                        if (route.Count < 2)
                            return;
                        playT += Time.deltaTime;
                        float f = playT / 0.1f;
                        int i = Mathf.Min((int)f, route.Count - 2);
                        var target = Vector3.Lerp(route[i], route[i + 1], Mathf.Clamp01(f - i));
                        var pos = Game.P.transform.position;
                        Game.Body.linearVelocity = Vector3.ClampMagnitude((target - pos) / MoveKit.Dt, 40f * MoveKit.S);
                        Movement.AntiGrav(1f);
                        if (f >= route.Count - 1)
                        {
                            Hud.Toast("Route finished");
                            Mods.Get("Route Autopilot").Set(false);
                        }
                    },
                    () =>
                    {
                        if (route.Count < 2)
                        {
                            Hud.Toast("Record a route first (Route Recorder)");
                            return;
                        }
                        // start from the recorded point nearest to you, at the speed it was recorded
                        var pos = Game.P.transform.position;
                        int best = 0;
                        for (int i = 1; i < route.Count; i++)
                            if ((route[i] - pos).sqrMagnitude < (route[best] - pos).sqrMagnitude)
                                best = i;
                        playT = best * 0.1f;
                    },
                    () => Game.Body.linearVelocity = Vector3.zero
                )
                .Info("Replays your recorded route: you move along it by yourself, at the speed you recorded it");

            Vector3? gate = null;
            GameObject gatePad = null;
            float lapStart = -1f,
                bestLap = -1f;
            bool wasNear = false;
            Mod lap = null;
            lap = Mods.T(
                    c,
                    "Lap Timer",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            gate = p;
                            lapStart = -1f;
                            if (gatePad == null)
                                gatePad = Fx.Prim(PrimitiveType.Cylinder, new Vector3(3f, 0.02f, 3f) * s, new Color(1f, 1f, 1f, 0.35f));
                            gatePad.transform.position = p + Vector3.up * 0.02f;
                            Hud.Toast("Start/finish pad set - go!");
                        }
                        if (gate == null)
                            return;
                        var me = Game.P.transform.position;
                        bool near = MoveKit.Flat(me - gate.Value).magnitude < 1.5f * s && Mathf.Abs(me.y - gate.Value.y) < 3f * s;
                        if (near && !wasNear)
                        {
                            if (lapStart >= 0f)
                            {
                                float t = Time.time - lapStart;
                                bool record = bestLap < 0f || t < bestLap;
                                if (record)
                                    bestLap = t;
                                Hud.Toast(
                                    "Lap "
                                        + t.ToString("0.00")
                                        + " s"
                                        + (record ? "  NEW BEST" : "  (best " + bestLap.ToString("0.00") + " s)")
                                );
                            }
                            lapStart = Time.time;
                        }
                        wasNear = near;
                    },
                    null,
                    () =>
                    {
                        gate = null;
                        Fx.Kill(gatePad);
                        gatePad = null;
                        lapStart = -1f;
                    }
                )
                .Live(() => "Lap Timer" + (lap.On && bestLap > 0f ? " (best " + bestLap.ToString("0.00") + " s)" : ""))
                .Info("Fire the gun to set a start/finish pad; each time you come back through it you get your lap time and best lap");

            var preset = Plugin.I.Config.Bind("Presets", "Slot1", "", "Mods switched on by Load Preset (names separated by |)");
            Mods.B(
                    c,
                    "Save Preset",
                    () =>
                    {
                        var on = Mods
                            .All.Where(m => m.Toggle && m.On && m.Cat != "Settings" && m.Cat != "Keyboard")
                            .Select(m => m.Name)
                            .ToList();
                        preset.Value = string.Join("|", on);
                        Hud.Toast("Preset saved: " + on.Count + " mods");
                    }
                )
                .Info("Remembers which mods are on right now (kept after restarting the game)");
            Mods.B(
                    c,
                    "Load Preset",
                    () =>
                    {
                        if (string.IsNullOrEmpty(preset.Value))
                        {
                            Hud.Toast("Save a preset first");
                            return;
                        }
                        var want = new HashSet<string>(preset.Value.Split('|'));
                        foreach (var m in Mods.All)
                            if (m.Toggle && m.Cat != "Settings" && m.Cat != "Keyboard")
                                m.Set(want.Contains(m.Name), quiet: true);
                        Hud.Toast("Preset loaded: " + want.Count + " mods");
                    }
                )
                .Info("Turns on exactly the mods in your saved preset and turns every other mod off");

            // modules that could get you stuck or confused if they switched on by surprise
            var surpriseUnsafe = new HashSet<string>
            {
                "NoClip",
                "Disable Body Collider",
                "Disable Head Collider",
                "Disable Water",
                "Disable Wind Barriers",
                "Anchor",
                "Ghost Monkey",
                "Punch Mod",
                "Solid Players",
                "Air Swim",
                "Size Changer",
                "Size By Speed",
                "Crouch Shrink",
                "Sky Floor",
                "Edge Guard",
                "Speed Limit",
            };
            Mod pick = null,
                roulette = null;
            float nextSpin = 0f;
            roulette = Mods.T(
                    c,
                    "Mod Roulette",
                    () =>
                    {
                        if (Time.time < nextSpin)
                            return;
                        nextSpin = Time.time + 30f;
                        if (pick != null)
                            pick.Set(false, quiet: true);
                        var pool = Mods
                            .All.Where(m => m.Toggle && !m.On && MoveCats.Contains(m.Cat) && !surpriseUnsafe.Contains(m.Name))
                            .ToList();
                        if (pool.Count == 0)
                            return;
                        pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                        pick.Set(true, quiet: true);
                        Hud.Toast("Roulette: " + pick.Name + " for 30 s");
                    },
                    () => nextSpin = 0f,
                    () =>
                    {
                        if (pick != null)
                            pick.Set(false, quiet: true);
                        pick = null;
                    }
                )
                .Live(() => "Mod Roulette" + (roulette.On && pick != null ? ": " + pick.Name : ""))
                .Info("Every 30 seconds a random movement mod switches on (and the previous one off) - surprise yourself");

            Mods.B(
                    c,
                    "Movement Panic",
                    () =>
                    {
                        int n = 0;
                        foreach (var m in Mods.All)
                            if (m.Toggle && m.On && MoveCats.Contains(m.Cat))
                            {
                                m.Set(false, quiet: true);
                                n++;
                            }
                        Game.Body.linearVelocity = Vector3.zero;
                        Hud.Toast("Movement Panic: " + n + " mods off");
                    }
                )
                .Info("Turns off every Movement, Flight, Parkour, Teleport and Experimental mod at once and stops you dead");

            var timed = new Dictionary<Mod, float>();
            var already = new HashSet<Mod>();
            float timedSince = 0f;
            Mod timer = null;
            timer = Mods.T(
                    c,
                    "Timed Mods",
                    () =>
                    {
                        bool settling = Time.time - timedSince < 0.5f; // mods restored when the game starts are not power-ups
                        foreach (var m in Mods.All)
                        {
                            if (!m.Toggle || m == timer || m.Cat == "Settings" || m.Cat == "Keyboard")
                                continue;
                            if (!m.On)
                            {
                                timed.Remove(m);
                                already.Remove(m);
                            }
                            else if (settling)
                                already.Add(m);
                            else if (!already.Contains(m) && !timed.ContainsKey(m))
                                timed[m] = Time.time;
                        }
                        foreach (var m in timed.Where(kv => Time.time - kv.Value > 20f).Select(kv => kv.Key).ToList())
                        {
                            timed.Remove(m);
                            m.Set(false);
                        }
                    },
                    () =>
                    {
                        timed.Clear();
                        already.Clear();
                        timedSince = Time.time;
                    },
                    () => timed.Clear()
                )
                .Info("Power-up mode: any mod you switch on while this is on turns itself off again after 20 seconds");

            Mod lapse = null;
            float nextShot = 0f;
            int shots = 0;
            string lapseDir = null;
            lapse = Mods.T(
                    c,
                    "Timelapse",
                    () =>
                    {
                        if (lapseDir == null || Time.unscaledTime < nextShot)
                            return;
                        nextShot = Time.unscaledTime + new[] { 2f, 5f, 15f }[lapse.Opt];
                        ScreenCapture.CaptureScreenshot(Path.Combine(lapseDir, "frame_" + (shots++).ToString("00000") + ".png"));
                    },
                    () =>
                    {
                        lapseDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "J CLIENT",
                            "Timelapse " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")
                        );
                        Directory.CreateDirectory(lapseDir);
                        shots = 0;
                        nextShot = 0f;
                        Hud.Toast("Timelapse saving to Pictures\\J CLIENT");
                    }
                )
                .Opts("Every", "2 s", "5 s", "15 s")
                .Def(1)
                .Live(() => "Timelapse" + (lapse.On ? " (" + shots + " frames)" : ""))
                .Info("Saves a screenshot every few seconds into its own folder, ready to turn into a timelapse video");

            Mods.B(
                    c,
                    "Backup Settings",
                    () =>
                    {
                        var cfg = Plugin.I.Config;
                        cfg.Save();
                        var dst = cfg.ConfigFilePath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        File.Copy(cfg.ConfigFilePath, dst, true);
                        Hud.Toast("Backed up to " + Path.GetFileName(dst));
                    }
                )
                .Info("Copies your J CLIENT settings file (every mod, keybind and favourite) to a dated backup next to it");

            float idle = 0f;
            int savedRate = 0;
            bool capped = false;
            Mods.T(
                    c,
                    "Idle FPS Saver",
                    () =>
                    {
                        bool active =
                            Inp.LGrip.Held
                            || Inp.RGrip.Held
                            || Inp.LTrig.Held
                            || Inp.RTrig.Held
                            || Inp.RA.Held
                            || Inp.RB.Held
                            || Inp.LX.Held
                            || Inp.LY.Held
                            || Inp.LStick.sqrMagnitude > 0.01f
                            || Inp.RStick.sqrMagnitude > 0.01f
                            || Game.Body.linearVelocity.sqrMagnitude > 0.04f
                            || MoveKit.HeadVel.sqrMagnitude > 0.01f
                            || Inp.Ms != null && Inp.Ms.delta.ReadValue().sqrMagnitude > 0f
                            || Board.DesktopOpen;
                        idle = active ? 0f : idle + Time.unscaledDeltaTime;
                        if (!capped && idle > 30f)
                        {
                            savedRate = Application.targetFrameRate;
                            Application.targetFrameRate = 30;
                            capped = true;
                            Hud.Toast("Idle - frame rate capped to save power");
                        }
                        else if (capped && active)
                        {
                            Application.targetFrameRate = savedRate;
                            capped = false;
                        }
                    },
                    null,
                    () =>
                    {
                        if (capped)
                            Application.targetFrameRate = savedRate;
                        capped = false;
                        idle = 0f;
                    }
                )
                .Info(
                    "After 30 s without moving or pressing anything, caps the frame rate to 30 to save power and heat; moving restores it"
                );

            Mod vol = null;
            vol = Mods.T(
                    c,
                    "Game Volume",
                    () =>
                    {
                        volume = new[] { 0.75f, 0.5f, 0.25f, 0.1f }[vol.Opt];
                        ApplyVolume();
                    },
                    null,
                    () =>
                    {
                        volume = 1f;
                        ApplyVolume();
                    }
                )
                .Opts("Volume", "75%", "50%", "25%", "10%")
                .Def(1)
                .Info("Turns the whole game's sound down for you: voices, music and effects");

            Mods.T(
                    c,
                    "Mute In Background",
                    () =>
                    {
                        muted = !Application.isFocused;
                        ApplyVolume();
                    },
                    null,
                    () =>
                    {
                        muted = false;
                        ApplyVolume();
                    }
                )
                .Info("Silences the game while its window isn't focused (desktop alt-tab)");
        }
    }
}
