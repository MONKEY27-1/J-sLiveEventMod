using System;
using UnityEngine;

namespace AstronomicalEvent
{
    public enum AstronomicalState
    {
        Idle,
        Running,
        Paused,
        Finished,
    }

    // Stable public surface for other menus/mods. Everything here is primitives/strings/enums so it also works via reflection:
    //   var t = Type.GetType("AstronomicalEvent.AstronomicalEventApi, AstronomicalEventMod");
    //   t.GetMethod("Start").Invoke(null, new object[] { -1 });
    // Main thread only (call from Update/OnGUI). The event is purely local: nothing here touches networking.
    public static class AstronomicalEventApi
    {
        public const string PluginGuid = "local.astronomical.event";
        public const string ApiVersion = "1.0";

        internal static AstronomicalEventManager Mgr;
        internal static AstronomicalUI Ui;
        static AstronomicalState lastState;
        static int lastSeq = -1;

        // ---- lifecycle ----
        /// <summary>True once the plugin is initialised (the API is safe to call).</summary>
        public static bool IsAvailable
        {
            get { return Mgr != null; }
        }

        /// <summary>True once models/music finished loading. Start() refuses (and sets Message) before this.</summary>
        public static bool AssetsReady
        {
            get { return Mgr != null && Mgr.Assets.Done; }
        }
        public static string AssetStatus
        {
            get { return Mgr != null ? Mgr.Assets.Status : "not loaded"; }
        }

        /// <summary>Start the event. fromSequence = -1 uses the configured preferred start sequence (0..6).</summary>
        public static void Start(int fromSequence = -1)
        {
            if (Mgr != null)
                Mgr.Start(fromSequence < 0 ? AstronomicalSettings.StartSequence.Value : fromSequence);
        }

        public static void Pause()
        {
            if (Mgr != null)
                Mgr.Pause();
        }

        public static void Resume()
        {
            if (Mgr != null)
                Mgr.Resume();
        }

        public static void TogglePause()
        {
            if (Mgr == null)
                return;
            if (Mgr.State == EventState.Running)
                Mgr.Pause();
            else if (Mgr.State == EventState.Paused)
                Mgr.Resume();
        }

        public static void Restart()
        {
            if (Mgr != null)
                Mgr.Restart();
        }

        /// <summary>Stop immediately and restore Forest (lighting, fog, hidden objects).</summary>
        public static void Stop()
        {
            if (Mgr != null)
                Mgr.UserStop();
        }

        /// <summary>Jump to the start of a sequence while the event is running or paused.</summary>
        public static void SeekToSequence(int sequence)
        {
            if (Mgr != null)
                Mgr.SeekToSequence(sequence);
        }

        // ---- status ----
        public static AstronomicalState State
        {
            get { return Mgr == null ? AstronomicalState.Idle : (AstronomicalState)(int)Mgr.State; }
        }
        public static bool IsRunning
        {
            get { return State == AstronomicalState.Running; }
        }
        public static bool IsPaused
        {
            get { return State == AstronomicalState.Paused; }
        }

        /// <summary>Seconds into the timeline.</summary>
        public static float Time
        {
            get { return Mgr != null ? Mgr.T : 0f; }
        }
        public static float TotalTime
        {
            get { return AstronomicalSequence.Total; }
        }
        public static int SequenceCount
        {
            get { return AstronomicalSequence.Names.Length; }
        }
        public static int CurrentSequence
        {
            get { return Mgr != null && Mgr.State != EventState.Idle ? Mgr.CurrentSequence : -1; }
        }
        public static string CurrentSequenceName
        {
            get
            {
                int i = CurrentSequence;
                return i < 0 ? "" : AstronomicalSequence.Names[i];
            }
        }

        public static string GetSequenceName(int i)
        {
            return AstronomicalSequence.Names[Mathf.Clamp(i, 0, SequenceCount - 1)];
        }

        public static float GetSequenceStartTime(int i)
        {
            return AstronomicalSequence.Start[Mathf.Clamp(i, 0, SequenceCount - 1)];
        }

        public static float GetSequenceDuration(int i)
        {
            return AstronomicalSequence.Duration[Mathf.Clamp(i, 0, SequenceCount - 1)];
        }

        /// <summary>Name of the choreography move Travis is performing right now (e.g. "mic_rap_open", "swim_crawl"); empty when idle. Every move in the show is performed once.</summary>
        public static string CurrentMove
        {
            get { return Mgr != null && Mgr.State != EventState.Idle ? AstronomicalEffectsManager.CurrentMove : ""; }
        }
        public static bool InForest
        {
            get { return Mgr != null && Mgr.CheckForest(); }
        }

        /// <summary>Last warning/error text (e.g. "Forest not detected"). Empty when fine.</summary>
        public static string Message
        {
            get { return Mgr != null ? Mgr.Message : ""; }
        }

        public static void ClearMessage()
        {
            if (Mgr != null)
                Mgr.Message = "";
        }

        // ---- events (raised on the main thread from the plugin's Update) ----
        public static event Action<AstronomicalState> StateChanged;
        public static event Action<int> SequenceChanged;

        internal static void Pump()
        {
            try
            {
                var s = State;
                if (s != lastState)
                {
                    lastState = s;
                    var h = StateChanged;
                    if (h != null)
                        h(s);
                }
                int q = CurrentSequence;
                if (q != lastSeq)
                {
                    lastSeq = q;
                    var h = SequenceChanged;
                    if (h != null)
                        h(q);
                }
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("API subscriber threw: " + e.Message);
            }
        }

        // ---- stage location ----
        public static bool HasSavedStage
        {
            get { return Mgr != null && Mgr.HasSavedStage; }
        }

        /// <summary>When true the event always starts at the saved stage instead of in front of the player.</summary>
        public static bool UseSavedStage
        {
            get { return AstronomicalSettings.UseFixedSpawn.Value; }
            set { AstronomicalSettings.UseFixedSpawn.Value = value; }
        }

        /// <summary>Save the spot StageDistance metres in front of the player as the permanent stage location.</summary>
        public static void SaveStageHere()
        {
            if (Mgr != null)
                Mgr.SaveStageHere();
        }

        public static void ClearSavedStage()
        {
            if (Mgr != null)
                Mgr.ClearSavedStage();
        }

        public static Vector3 SavedStagePosition
        {
            get
            {
                return new Vector3(
                    AstronomicalSettings.FixedSpawnX.Value,
                    AstronomicalSettings.FixedSpawnY.Value,
                    AstronomicalSettings.FixedSpawnZ.Value
                );
            }
        }
        public static float SavedStageYaw
        {
            get { return AstronomicalSettings.FixedSpawnYaw.Value; }
        }

        /// <summary>Moves the player to the saved stage. Only runs when YOUR menu calls it; the event never teleports anyone by itself.</summary>
        public static void TeleportToStage()
        {
            if (Mgr != null)
                Mgr.TeleportToStage();
        }

        /// <summary>Moves you to the gazebo floor, facing the middle of the selected event (Final Showdown or Rocket Launch). Only runs when YOUR menu calls it.</summary>
        public static void GoToViewpoint()
        {
            if (Mgr != null)
                Mgr.GoToViewpoint();
        }

        /// <summary>Spectator mode: moves you to the best viewing spot, then starts the selected event (and puts you at the spot again once the show is built). Only runs when YOUR menu calls it.</summary>
        public static void SpectatorStart()
        {
            if (Mgr == null)
                return;
            if (!Mgr.Assets.Done)
            {
                Mgr.Message = "Assets still loading: " + Mgr.Assets.Status;
                return;
            }
            Mgr.GoToViewpoint();
            Mgr.Spectate = true;
            Mgr.Start(AstronomicalSettings.StartSequence.Value);
        }

        /// <summary>Seconds left in the running event.</summary>
        public static float TimeLeft
        {
            get { return Mgr != null ? Mgr.TimeLeft : 0f; }
        }

        /// <summary>Jumps to just before the next big moment of the running event (or the next section if it has no cue list).</summary>
        public static void SkipToNextHit()
        {
            if (Mgr != null)
                Mgr.SkipToNextHit();
        }

        /// <summary>Multi-line text for an on-screen overlay: event, time, section, fps and what the scene has running.</summary>
        public static string DebugText
        {
            get { return Mgr != null ? Mgr.DebugText() : ""; }
        }

        /// <summary>0 = low, 1 = medium, 2 = high: sets particle density, light strength and effects intensity together (for weaker headsets).</summary>
        public static int QualityLevel
        {
            get
            {
                float p = AstronomicalSettings.Particles.Value;
                return p < 0.5f ? 0
                    : p < 0.9f ? 1
                    : 2;
            }
            set
            {
                int v = Mathf.Clamp(value, 0, 2);
                AstronomicalSettings.Particles.Value =
                    v == 0 ? 0.35f
                    : v == 1 ? 0.7f
                    : 1f;
                AstronomicalSettings.Lighting.Value =
                    v == 0 ? 0.5f
                    : v == 1 ? 0.8f
                    : 1f;
                AstronomicalSettings.Effects.Value =
                    v == 0 ? 0.6f
                    : v == 1 ? 0.85f
                    : 1f;
            }
        }
        public static string QualityName
        {
            get
            {
                int q = QualityLevel;
                return q == 0 ? "Low"
                    : q == 1 ? "Medium"
                    : "High";
            }
        }

        // ---- settings (persisted by BepInEx; Volume/Speed apply live, particle density & tree destruction apply on next Start) ----
        /// <summary>0..3. Above 1 adds soft-clipped gain.</summary>
        public static float Volume
        {
            get { return AstronomicalSettings.Volume.Value; }
            set { AstronomicalSettings.Volume.Value = value; }
        }
        public static float EffectsIntensity
        {
            get { return AstronomicalSettings.Effects.Value; }
            set { AstronomicalSettings.Effects.Value = value; }
        }
        public static float LightingIntensity
        {
            get { return AstronomicalSettings.Lighting.Value; }
            set { AstronomicalSettings.Lighting.Value = value; }
        }
        public static float ParticleDensity
        {
            get { return AstronomicalSettings.Particles.Value; }
            set { AstronomicalSettings.Particles.Value = value; }
        }
        public static float PlaybackSpeed
        {
            get { return AstronomicalSettings.Speed.Value; }
            set { AstronomicalSettings.Speed.Value = value; }
        }
        public static float StageScale
        {
            get { return AstronomicalSettings.StageScale.Value; }
            set { AstronomicalSettings.StageScale.Value = value; }
        }
        public static float PerformerHeight
        {
            get { return AstronomicalSettings.TravisScale.Value; }
            set { AstronomicalSettings.TravisScale.Value = value; }
        }

        /// <summary>Explosions, meteors, lightning, Travis's stomps and launch rings push the local player (default on).</summary>
        public static int NewYearYear
        {
            get { return AstronomicalSettings.NewYearYear.Value; }
            set { AstronomicalSettings.NewYearYear.Value = Mathf.Clamp(value, 2019, 2022); }
        }
        public static bool Knockback
        {
            get { return AstronomicalSettings.Knockback.Value; }
            set { AstronomicalSettings.Knockback.Value = value; }
        }
        public static float KnockbackStrength
        {
            get { return AstronomicalSettings.KnockbackStrength.Value; }
            set { AstronomicalSettings.KnockbackStrength.Value = value; }
        }

        /// <summary>Use the game's own swimming during the flood sequence (default on).</summary>
        public static bool SwimInFlood
        {
            get { return AstronomicalSettings.SwimInFlood.Value; }
            set { AstronomicalSettings.SwimInFlood.Value = value; }
        }

        /// <summary>Seconds of countdown between Start and the event beginning (0 = immediate). Saved in the config.</summary>
        public static float CountdownSeconds
        {
            get { return AstronomicalSettings.Countdown.Value; }
            set { AstronomicalSettings.Countdown.Value = value; }
        }
        public static bool IsCountingDown
        {
            get { return Mgr != null && Mgr.CountingDown; }
        }
        public static float CountdownLeft
        {
            get { return Mgr != null && Mgr.CountingDown ? Mgr.CountdownLeft : 0f; }
        }

        /// <summary>Show the 3D stage model (default false).</summary>
        public static bool ShowStage
        {
            get { return AstronomicalSettings.ShowStage.Value; }
            set { AstronomicalSettings.ShowStage.Value = value; }
        }

        // ---- events (more than one show can be installed: AstronomicalAssets/events/<id>/event.json) ----
        public static int EventCount
        {
            get { return AstronomicalEvents.All.Count; }
        }

        public static string GetEventId(int i)
        {
            return AstronomicalEvents.All[Mathf.Clamp(i, 0, EventCount - 1)].Id;
        }

        public static string GetEventName(int i)
        {
            return AstronomicalEvents.All[Mathf.Clamp(i, 0, EventCount - 1)].Name;
        }

        public static string SelectedEventId
        {
            get { return AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Id : ""; }
        }
        public static string SelectedEventName
        {
            get { return AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Name : ""; }
        }

        /// <summary>Makes an event current (only while idle). Extra events load their music/performer first, so it may need a second call.</summary>
        public static bool SelectEvent(string id)
        {
            return Mgr != null && Mgr.SelectEvent(id);
        }

        // ---- cross-lobby sync (other players who have the mod) ----
        public static bool SyncBroadcast
        {
            get { return AstronomicalSettings.SyncBroadcast.Value; }
            set { AstronomicalSettings.SyncBroadcast.Value = value; }
        }
        public static bool SyncReceive
        {
            get { return AstronomicalSettings.SyncReceive.Value; }
            set { AstronomicalSettings.SyncReceive.Value = value; }
        }
        public static string SyncStatus
        {
            get { return AstronomicalNet.Status + " (received " + AstronomicalNet.Received + ")"; }
        }
        public static bool CameraEffects
        {
            get { return AstronomicalSettings.CameraFx.Value; }
            set { AstronomicalSettings.CameraFx.Value = value; }
        }
        public static bool TreeDestruction
        {
            get { return AstronomicalSettings.TreeDestruction.Value; }
            set { AstronomicalSettings.TreeDestruction.Value = value; }
        }
        public static bool CleanupOnFinish
        {
            get { return AstronomicalSettings.AutoCleanup.Value; }
            set { AstronomicalSettings.AutoCleanup.Value = value; }
        }
        public static bool DebugLogging
        {
            get { return AstronomicalSettings.Debug.Value; }
            set { AstronomicalSettings.Debug.Value = value; }
        }
        public static int PreferredStartSequence
        {
            get { return AstronomicalSettings.StartSequence.Value; }
            set { AstronomicalSettings.StartSequence.Value = value; }
        }

        // ---- built-in (P key) menu: turn it off if you ship your own ----
        public static bool BuiltInMenuEnabled
        {
            get { return AstronomicalSettings.BuiltInMenu.Value; }
            set
            {
                AstronomicalSettings.BuiltInMenu.Value = value;
                if (!value && Ui != null)
                    Ui.Close();
            }
        }
        public static bool IsBuiltInMenuOpen
        {
            get { return Ui != null && Ui.IsOpen; }
        }

        public static void OpenBuiltInMenu()
        {
            if (Ui != null && BuiltInMenuEnabled)
                Ui.Open();
        }

        public static void CloseBuiltInMenu()
        {
            if (Ui != null)
                Ui.Close();
        }
    }
}
