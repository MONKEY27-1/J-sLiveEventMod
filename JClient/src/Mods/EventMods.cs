using System;
using System.Linq;
using AstronomicalEvent;
using Api = AstronomicalEvent.AstronomicalEventApi;

namespace JClient
{
    // Drives the separate Astronomical Event plugin through its public API (its own P menu is turned off).
    public static class EventMods
    {
        static float Next(float cur, params float[] vals)
        {
            foreach (var v in vals)
                if (v > cur + 0.001f)
                    return v;
            return vals[0];
        }

        static int skip;

        static string Pct(float v) => (v * 100f).ToString("0") + "%";

        static void Say()
        {
            if (!string.IsNullOrEmpty(Api.Message))
            {
                Hud.Toast(Api.Message);
                Api.ClearMessage();
            }
        }

        static void Setting(string name, Func<string> value, Action step, string info) =>
            Mods.B(
                    "Settings",
                    name,
                    () =>
                    {
                        step();
                        Hud.Toast(name + ": " + value());
                    }
                )
                .Live(() => name + ": " + value())
                .Info(info);

        public static void Register()
        {
            Api.BuiltInMenuEnabled = false;
            const string c = "Events";

            Mods.B(c, "Event Status", Say)
                .Live(() =>
                {
                    if (!Api.IsAvailable)
                        return "Event: not loaded";
                    if (!Api.AssetsReady)
                        return "Event: loading assets...";
                    if (Api.IsCountingDown)
                        return "Event starts in " + UnityEngine.Mathf.CeilToInt(Api.CountdownLeft) + "s";
                    return Api.State == AstronomicalState.Idle
                        ? "Event: ready" + (Api.InForest ? "" : " (go to Forest)")
                        : Api.State
                            + " "
                            + Api.Time.ToString("0")
                            + "/"
                            + Api.TotalTime.ToString("0")
                            + "s "
                            + Api.CurrentSequenceName
                            + (string.IsNullOrEmpty(Api.CurrentMove) ? "" : " | " + Api.CurrentMove);
                })
                .Info("Live event status. Press to show the last event message");
            Mods.B(
                    c,
                    "Start Event",
                    () =>
                    {
                        Api.Start();
                        Say();
                    }
                )
                .Info("Starts the Astronomical Event (Forest only)");
            Mods.B(c, "Pause / Resume", Api.TogglePause).Info("Pauses or resumes the show");
            Mods.B(
                    c,
                    "Restart Event",
                    () =>
                    {
                        Api.Restart();
                        Say();
                    }
                )
                .Info("Restarts from the start sequence");
            Mods.B(c, "Stop Event", Api.Stop).Info("Stops and cleans up everything the event spawned");
            Mods.B(c, "Previous Sequence", () => Api.SeekToSequence(Math.Max(0, Api.CurrentSequence - 1))).Info("Jumps back one sequence");
            Mods.B(c, "Next Sequence", () => Api.SeekToSequence(Math.Min(Api.SequenceCount - 1, Api.CurrentSequence + 1)))
                .Info("Skips to the next sequence");
            Mods.B(
                    c,
                    "Set Stage Here",
                    () =>
                    {
                        Api.SaveStageHere();
                        Hud.Toast("Stage saved here");
                    }
                )
                .Info("Saves your current spot as the stage location");
            Mods.B(
                    c,
                    "Spectator Mode",
                    () =>
                    {
                        Api.SpectatorStart();
                        Say();
                    }
                )
                .Info("Moves you to the best viewing spot and starts the selected event");
            Mods.B(
                    c,
                    "Skip To Section",
                    () =>
                    {
                        if (Api.State != AstronomicalState.Idle)
                            Api.SeekToSequence(Math.Min(skip, Api.SequenceCount - 1));
                        Say();
                    }
                )
                .Live(() => "Skip To: " + Api.GetSequenceName(Math.Min(skip, Api.SequenceCount - 1)))
                .Info("Jumps the running event to the section chosen with 'Skip Section'");
            Mods.B(
                    c,
                    "Skip Section",
                    () =>
                    {
                        skip = (skip + 1) % Api.SequenceCount;
                        Hud.Toast("Skip to: " + Api.GetSequenceName(skip));
                    }
                )
                .Live(() => "Skip Section: " + Api.GetSequenceName(Math.Min(skip, Api.SequenceCount - 1)))
                .Info("Chooses which section 'Skip To Section' jumps to (names come from the selected event)");
            Mods.B(c, "Time Left", Say)
                .Live(() =>
                    Api.State == AstronomicalState.Idle
                        ? "Time Left: -"
                        : "Time Left: " + ((int)Api.TimeLeft / 60) + ":" + ((int)Api.TimeLeft % 60).ToString("00")
                )
                .Info("Time left in the running event");
            Mods.B(
                    c,
                    "Skip To Next Hit",
                    () =>
                    {
                        if (Api.State != AstronomicalState.Idle)
                            Api.SkipToNextHit();
                        Say();
                    }
                )
                .Info("Jumps to just before the next big moment of the running event");
            UnityEngine.GameObject dbg = null;
            Mods.T(
                    c,
                    "Event Debug Overlay",
                    () =>
                    {
                        if (dbg == null)
                        {
                            dbg = new UnityEngine.GameObject("JClientEventDebug");
                            Fx.Spawned.Add(dbg);
                            Fx.Text(dbg.transform, "", 0.03f, Fx.Accent);
                        }
                        var f = Game.Head.forward;
                        f.y = 0;
                        f.Normalize();
                        dbg.transform.position = Game.Head.position + f * 1.6f + UnityEngine.Vector3.down * 0.45f;
                        dbg.transform.rotation = UnityEngine.Quaternion.LookRotation(dbg.transform.position - Game.Head.position);
                        var t = dbg.GetComponentInChildren<UnityEngine.TextMesh>();
                        t.text = Api.DebugText;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(dbg);
                        dbg = null;
                    }
                )
                .Info("Text in front of you with the event time, section, fps and what the show has running (to report problems)");
            Mods.B(c, "Event Viewpoint", Api.GoToViewpoint)
                .Info(
                    "Teleports you to the gazebo floor facing the middle of the selected event (Final Showdown / Rocket Launch / The End) for the best view"
                );
            Mods.B(c, "Teleport To Stage", Api.TeleportToStage).Info("Teleports you to the saved stage");
            Mods.B(
                    c,
                    "Clear Saved Stage",
                    () =>
                    {
                        Api.ClearSavedStage();
                        Hud.Toast("Saved stage cleared");
                    }
                )
                .Info("Forgets the saved stage location");
            Mods.B(c, "Use Saved Stage", () => Api.UseSavedStage = !Api.UseSavedStage)
                .Live(() => "Use Saved Stage: " + (Api.UseSavedStage ? "ON" : "OFF"))
                .Info("Start at the saved stage instead of in front of you");

            UnityEngine.GameObject sky = null;
            Mods.T(
                    c,
                    "Event Countdown Sky",
                    () =>
                    {
                        if (sky == null)
                        {
                            sky = new UnityEngine.GameObject("JClientCountdown");
                            Fx.Spawned.Add(sky);
                            Fx.Text(sky.transform, "", 0.6f, Fx.Accent);
                        }
                        var f = Game.Head.forward;
                        f.y = 0;
                        f.Normalize();
                        sky.transform.position = Game.Head.position + f * 30f + UnityEngine.Vector3.up * 14f;
                        sky.transform.rotation = UnityEngine.Quaternion.LookRotation(sky.transform.position - Game.Head.position);
                        var t = sky.GetComponentInChildren<UnityEngine.TextMesh>();
                        t.color = Fx.Accent;
                        int cur = Api.CurrentSequence;
                        if (Api.State == AstronomicalState.Idle || cur < 0)
                            t.text = Api.AssetsReady ? "EVENT READY" : "LOADING...";
                        else if (cur + 1 >= Api.SequenceCount)
                            t.text = "FINALE\n" + UnityEngine.Mathf.CeilToInt(Api.TotalTime - Api.Time) + "s";
                        else
                            t.text =
                                Api.GetSequenceName(cur + 1).ToUpperInvariant()
                                + "\nIN "
                                + UnityEngine.Mathf.CeilToInt(Api.GetSequenceStartTime(cur + 1) - Api.Time)
                                + "s";
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(sky);
                        sky = null;
                    }
                )
                .Info("Huge countdown in the sky to the next event sequence");

            // Event settings live in the Settings category; values are stored by the event plugin's own config.
            Setting(
                "Event Selected",
                () => Api.SelectedEventName,
                () =>
                {
                    int n = Api.EventCount;
                    for (int k = 0; k < n; k++)
                        if (Api.GetEventId(k) == Api.SelectedEventId)
                        {
                            Api.SelectEvent(Api.GetEventId((k + 1) % n));
                            break;
                        }
                },
                "Which installed event Start runs (more events can be added as folders)"
            );
            Setting(
                "New Year Year",
                () => Api.NewYearYear.ToString(),
                () => Api.NewYearYear = Api.NewYearYear >= 2022 ? 2019 : Api.NewYearYear + 1,
                "Year the New Year event counts down to (2022 uses the crystal ball). Applies on next start"
            );
            Setting(
                "Event Tell Others",
                () => Api.SyncBroadcast ? "ON" : "OFF",
                () => Api.SyncBroadcast = !Api.SyncBroadcast,
                "Starting/stopping the event also starts/stops it for other mod users in any lobby"
            );
            Setting(
                "Event Join Others",
                () => Api.SyncReceive ? "ON" : "OFF",
                () => Api.SyncReceive = !Api.SyncReceive,
                "Join events that other mod users start (Forest only)"
            );
            Setting(
                "Event Countdown",
                () => Api.CountdownSeconds <= 0 ? "OFF" : Api.CountdownSeconds.ToString("0") + "s",
                () => Api.CountdownSeconds = Next(Api.CountdownSeconds, 0f, 5f, 10f, 15f, 30f, 60f, 120f, 180f, 300f, 600f),
                "Countdown between pressing Start and the event beginning (0 = off). Shown in Event Status and the sky countdown"
            );
            Setting(
                "Event Stage",
                () => Api.ShowStage ? "ON" : "OFF",
                () => Api.ShowStage = !Api.ShowStage,
                "Show the 3D stage (off = open arena; applies on next start)"
            );
            Setting(
                "Event Knockback",
                () => Api.Knockback ? "ON" : "OFF",
                () => Api.Knockback = !Api.Knockback,
                "Explosions, meteors, stomps and rings push you"
            );
            Setting(
                "Event Swim In Flood",
                () => Api.SwimInFlood ? "ON" : "OFF",
                () => Api.SwimInFlood = !Api.SwimInFlood,
                "Use the game's swimming in the underwater sequence"
            );
            Setting(
                "Event Quality",
                () => Api.QualityName,
                () => Api.QualityLevel = (Api.QualityLevel + 1) % 3,
                "Low / Medium / High: particle amount, light strength and effects together (Low for weaker headsets)"
            );
            Setting(
                "Event Volume",
                () => Api.Volume.ToString("0.0"),
                () => Api.Volume = Next(Api.Volume, 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f),
                "Music volume 0-3 (above 1 = boosted)"
            );
            Setting(
                "Event Effects",
                () => Pct(Api.EffectsIntensity),
                () => Api.EffectsIntensity = Next(Api.EffectsIntensity, 0.25f, 0.5f, 0.75f, 1f),
                "Overall effects intensity"
            );
            Setting(
                "Event Lighting",
                () => Pct(Api.LightingIntensity),
                () => Api.LightingIntensity = Next(Api.LightingIntensity, 0f, 0.25f, 0.5f, 0.75f, 1f),
                "Lights, fog and sky tint strength"
            );
            Setting(
                "Event Particles",
                () => Pct(Api.ParticleDensity),
                () => Api.ParticleDensity = Next(Api.ParticleDensity, 0.25f, 0.5f, 0.75f, 1f),
                "Particle amount (applies on next start)"
            );
            Setting(
                "Event Camera FX",
                () => Api.CameraEffects ? "ON" : "OFF",
                () => Api.CameraEffects = !Api.CameraEffects,
                "Screen tint for underwater/flashes"
            );
            Setting(
                "Event Speed",
                () => Api.PlaybackSpeed.ToString("0.00") + "x",
                () => Api.PlaybackSpeed = Next(Api.PlaybackSpeed, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f),
                "Timeline playback speed"
            );
            Setting(
                "Event Stage Scale",
                () => Api.StageScale.ToString("0.0") + "x",
                () => Api.StageScale = Next(Api.StageScale, 0.2f, 0.5f, 1f, 1.5f, 2f, 3f, 4f),
                "Size of the stage"
            );
            Setting(
                "Event Performer Height",
                () => Api.PerformerHeight.ToString("0.#") + "m",
                () => Api.PerformerHeight = Next(Api.PerformerHeight, 2f, 3.8f, 6f, 9f, 15f, 22f, 30f),
                "Height of the performer on stage"
            );
            Setting(
                "Event Tree Destruction",
                () => Api.TreeDestruction ? "ON" : "OFF",
                () => Api.TreeDestruction = !Api.TreeDestruction,
                "Visual-only exploding trees near the stage"
            );
            Setting(
                "Event Cleanup On Finish",
                () => Api.CleanupOnFinish ? "ON" : "OFF",
                () => Api.CleanupOnFinish = !Api.CleanupOnFinish,
                "Remove everything when the show ends"
            );
            Setting(
                "Event Start Sequence",
                () => Api.GetSequenceName(Api.PreferredStartSequence),
                () => Api.PreferredStartSequence = (Api.PreferredStartSequence + 1) % Api.SequenceCount,
                "Which sequence Start begins at"
            );
            Setting(
                "Event Debug Log",
                () => Api.DebugLogging ? "ON" : "OFF",
                () => Api.DebugLogging = !Api.DebugLogging,
                "Verbose event logging"
            );
        }
    }
}
