using System;
using GorillaLocomotion;
using UnityEngine;

namespace AstronomicalEvent
{
    internal enum EventState
    {
        Idle,
        Running,
        Paused,
        Finished,
    }

    // Local-only orchestration. Touches no networking types; everything is created through Unity APIs on this client.
    internal sealed class AstronomicalEventManager
    {
        public readonly AstronomicalEffectsManager Effects = new AstronomicalEffectsManager();
        public readonly AstronomicalShowdown Showdown = new AstronomicalShowdown();
        IEventScene scene;
        public readonly AstronomicalRocket Rocket = new AstronomicalRocket();
        public readonly AstronomicalEnd End = new AstronomicalEnd();
        public readonly AstronomicalUnvault Unvault = new AstronomicalUnvault();
        public readonly AstronomicalConcert Concert = new AstronomicalConcert();
        public readonly AstronomicalDevice Device = new AstronomicalDevice();
        public readonly AstronomicalCube CubeScene = new AstronomicalCube();
        public readonly AstronomicalCubeLake LakeScene = new AstronomicalCubeLake();
        public readonly AstronomicalIceStorm IceScene = new AstronomicalIceStorm();
        public readonly AstronomicalNewYear NewYearScene = new AstronomicalNewYear();
        public readonly AstronomicalButterfly ButterflyScene = new AstronomicalButterfly();
        public readonly AstronomicalCollision CollisionScene = new AstronomicalCollision();

        IEventScene PickScene()
        {
            string k = AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Kind : "";
            if (k == "showdown")
                return Showdown;
            if (k == "rocket")
                return Rocket;
            if (k == "end")
                return End;
            if (k == "unvault")
                return Unvault;
            if (k == "concert")
                return Concert;
            if (k == "device")
                return Device;
            if (k == "cube")
                return CubeScene;
            if (k == "cubelake")
                return LakeScene;
            if (k == "icestorm")
                return IceScene;
            if (k == "newyear")
                return NewYearScene;
            if (k == "butterfly")
                return ButterflyScene;
            if (k == "collision")
                return CollisionScene;
            return Effects;
        }

        public readonly AstronomicalAudioManager Audio = new AstronomicalAudioManager();
        public EventState State = EventState.Idle;
        public float T;
        public string Message = "";
        public bool InForest;
        bool audioStarted;
        float nextCheck,
            nextRestart;

        public readonly AstronomicalAssetLoader Assets;
        bool audioInit;

        public AstronomicalEventManager(AstronomicalAssetLoader assets)
        {
            Assets = assets;
        }

        // Once assets finish loading: use the supplied music (and stretch the timeline to it) or fall back to the synthesized track.
        void InitAudio()
        {
            if (audioInit || !Assets.Done)
                return;
            audioInit = true;
            ApplyDef(AstronomicalEvents.Selected);
        }

        // Makes an event current: its music, performer, choreography and section timings.
        void ApplyDef(AstronomicalEventDef d)
        {
            if (d == null)
                return;
            Assets.Use(d);
            AstronomicalSequence.Configure(d);
            if (d.Music != null)
            {
                Audio.UseClip(d.Music, 0f);
                AstronomicalSequence.Rescale(Mathf.Min(d.Music.length, d.Length));
            }
            else
                Audio.Prepare();
        }

        public AstronomicalEventDef Event
        {
            get { return AstronomicalEvents.Selected; }
        }

        public bool SelectEvent(string id)
        {
            var d = AstronomicalEvents.Find(id);
            if (d == null || State != EventState.Idle || CountingDown)
                return false;
            if (!d.Loaded)
            {
                if (!d.Loading && AstronomicalEventMod.Inst != null)
                    AstronomicalEventMod.Inst.StartCoroutine(LoadThenApply(d));
                Message = "Loading event " + d.Name + "...";
                return false;
            }
            AstronomicalEvents.Selected = d;
            ApplyDef(d);
            return true;
        }

        System.Collections.IEnumerator LoadThenApply(AstronomicalEventDef d)
        {
            yield return Assets.LoadEvent(d);
            if (State == EventState.Idle && !CountingDown)
            {
                AstronomicalEvents.Selected = d;
                ApplyDef(d);
                Message = "";
            }
        }

        // the Forest-only rule does not apply to events made for other maps (the lake event runs on the Beach)
        bool AnyMap
        {
            get
            {
                return AstronomicalEvents.Selected != null
                    && (
                        AstronomicalEvents.Selected.Kind == "cubelake"
                        || AstronomicalEvents.Selected.Kind == "icestorm"
                        || AstronomicalEvents.Selected.Kind == "newyear"
                        || AstronomicalEvents.Selected.Kind == "butterfly"
                        || AstronomicalEvents.Selected.Kind == "collision"
                    );
            }
        }

        public bool CheckForest()
        {
            try
            {
                return ZoneManagement.IsInZone(GTZone.forest);
            }
            catch (Exception e)
            {
                Message = "Zone check failed: " + e.Message;
                return false;
            }
        }

        public bool CountingDown;
        public float CountdownLeft;
        int pendingSeq;
        float pendingOffset;
        public bool SyncStarted,
            BroadcastedStart,
            Spectate;
        readonly AstronomicalCountdown cdText = new AstronomicalCountdown();

        // User start: counts down (if configured), tells other mod users, then runs.
        public void Start(int fromSeq)
        {
            StartCore(fromSeq, AstronomicalSettings.Countdown.Value, 0f, true);
        }

        void StartCore(int fromSeq, float countdown, float offset, bool broadcast)
        {
            if (State != EventState.Idle || CountingDown)
                return;
            if (broadcast)
            {
                BroadcastedStart = false;
                SyncStarted = false;
                if (Assets.Done && AstronomicalSettings.SyncBroadcast.Value)
                {
                    BroadcastedStart = true;
                    AstronomicalNet.Publish("start", Event.Id, countdown > 0.5f ? countdown : 0f, fromSeq);
                }
            }
            else
                SyncStarted = true;
            if (countdown > 0.5f)
            {
                CountingDown = true;
                CountdownLeft = countdown;
                pendingSeq = fromSeq;
                pendingOffset = offset;
                Message = "Event starts in " + Mathf.CeilToInt(countdown) + " s";
                AstronomicalEventMod.Log.LogInfo("Countdown started: " + countdown + " s");
                cdText.Show(StageSpot());
                return;
            }
            StartNow(fromSeq);
            if (State == EventState.Running && offset > 0.5f)
                T = Mathf.Min(T + offset, AstronomicalSequence.Total - 5f);
        }

        // Another mod user (possibly in another lobby) started an event: join it, with the same countdown / at the same point of the show.
        public void StartFromNet(string evId, int seq, float remaining)
        {
            if (!AstronomicalSettings.SyncReceive.Value || State != EventState.Idle || CountingDown || !Assets.Done)
                return;
            if (!CheckForest() && !AstronomicalSettings.BypassForestCheck.Value)
                return;
            var d = AstronomicalEvents.Find(evId);
            if (d == null)
                return;
            if (d != Event && !SelectEvent(evId))
            {
                pendJoin = evId;
                pendSeq = seq;
                pendAt = Time.unscaledTime + remaining;
                Message = "Joining " + d.Name + ": loading it first...";
                return;
            } // not loaded yet: keep the request and retry every frame
            if (remaining > 0.5f)
                StartCore(seq, remaining, 0f, false);
            else if (-remaining < AstronomicalSequence.Total - 10f)
                StartCore(seq, 0f, -remaining, false);
            else
                return;
            Message = "Joined event from another player";
            AstronomicalEventMod.Log.LogInfo("Sync: joined " + evId + " (remaining " + remaining.ToString("0.0") + ")");
        }

        string pendJoin;
        int pendSeq;
        float pendAt;

        // a join that had to wait for the event's files: start it as soon as they are loaded, at the moment the host's clock says we are at now
        void TickPendingJoin()
        {
            if (pendJoin == null)
                return;
            if (Time.unscaledTime - pendAt > AstronomicalSequence.Total + 20f || State != EventState.Idle || CountingDown)
            {
                pendJoin = null;
                return;
            }
            var d = AstronomicalEvents.Find(pendJoin);
            if (d == null || !d.Loaded)
                return;
            string id = pendJoin;
            pendJoin = null;
            StartFromNet(id, pendSeq, pendAt - Time.unscaledTime);
        }

        // votes / the winner announced by other mod users (The Unvaulting)
        public void VoteFromNet(string evId, int item, int count)
        {
            if (State != EventState.Idle && Event != null && string.Equals(Event.Id, evId, StringComparison.OrdinalIgnoreCase))
                AstronomicalUnvault.AddRemote(item, count);
        }

        public void WinnerFromNet(string evId, int item)
        {
            if (!Host() && State != EventState.Idle && Event != null && string.Equals(Event.Id, evId, StringComparison.OrdinalIgnoreCase))
                AstronomicalUnvault.SetWinner(item);
        }

        bool Host()
        {
            return !SyncStarted;
        }

        public void StopFromNet(string evId)
        {
            if (pendJoin != null && string.Equals(pendJoin, evId, StringComparison.OrdinalIgnoreCase))
                pendJoin = null;
            if (!SyncStarted || Event == null || !string.Equals(Event.Id, evId, StringComparison.OrdinalIgnoreCase))
                return;
            Message = "Event stopped by the host";
            Stop();
        }

        // Stop pressed by the user: also stops it for the players who joined through us.
        public void UserStop()
        {
            if (BroadcastedStart && Event != null)
                AstronomicalNet.Publish("stop", Event.Id, 0f, 0);
            Stop();
        }

        Vector3 StageSpot()
        {
            if (HasSavedStage)
                return new Vector3(
                    AstronomicalSettings.FixedSpawnX.Value,
                    AstronomicalSettings.FixedSpawnY.Value,
                    AstronomicalSettings.FixedSpawnZ.Value
                );
            var cam = Camera.main;
            return cam != null
                ? cam.transform.position
                    + new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized * AstronomicalSettings.Distance.Value
                : Vector3.zero;
        }

        void StartNow(int fromSeq)
        {
            if (State != EventState.Idle)
                return; // single instance only
            if (!Assets.Done)
            {
                Message = "Assets still loading: " + Assets.Status;
                return;
            }
            InitAudio();
            InForest = CheckForest();
            AstronomicalEventMod.Log.LogInfo("Forest detected: " + InForest);
            if (!InForest && !AstronomicalSettings.BypassForestCheck.Value && !AnyMap)
            {
                Message = "Forest not detected - refusing to spawn into an unknown area.";
                return;
            }
            try
            {
                Vector3 a;
                float stageYaw;
                // First ever start: remember this spot so every later start is in exactly the same place.
                if (AstronomicalSettings.UseFixedSpawn.Value && !HasSavedStage)
                {
                    SaveStageHere();
                    AstronomicalEventMod.Log.LogInfo("Stage location saved for all future starts: " + AstronomicalApiPos());
                }
                if (
                    AstronomicalSettings.UseFixedSpawn.Value
                    && (AstronomicalSettings.FixedSpawnX.Value != 0 || AstronomicalSettings.FixedSpawnZ.Value != 0)
                )
                {
                    a = new Vector3(
                        AstronomicalSettings.FixedSpawnX.Value,
                        AstronomicalSettings.FixedSpawnY.Value,
                        AstronomicalSettings.FixedSpawnZ.Value
                    );
                    stageYaw = AstronomicalSettings.FixedSpawnYaw.Value;
                }
                else
                {
                    Vector3 p;
                    var player = GTPlayer.Instance;
                    var cam = Camera.main;
                    p = player != null ? player.HeadCenterPosition : (cam != null ? cam.transform.position : Vector3.zero);
                    Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
                    fwd.y = 0;
                    if (fwd.sqrMagnitude < 0.01f)
                        fwd = Vector3.forward;
                    fwd = Quaternion.Euler(0, AstronomicalSettings.Yaw.Value, 0) * fwd.normalized;
                    a = p + fwd * AstronomicalSettings.Distance.Value;
                    float gy = p.y - 1.5f;
                    if (
                        Physics.Raycast(
                            new Vector3(a.x, p.y + 1f, a.z),
                            Vector3.down,
                            out RaycastHit hit,
                            40f,
                            ~0,
                            QueryTriggerInteraction.Ignore
                        )
                    )
                        gy = hit.point.y;
                    a.y = gy + AstronomicalSettings.Height.Value;
                    stageYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                }
                AstronomicalUnvault.Host = !SyncStarted;
                scene = PickScene();
                scene.Build(Assets, a, stageYaw);
                if (Spectate)
                {
                    Spectate = false;
                    GoToViewpoint();
                }
                T = AstronomicalSequence.Start[Mathf.Clamp(fromSeq, 0, AstronomicalSequence.Start.Length - 1)];
                audioStarted = false;
                State = EventState.Running;
                Message = "";
                AstronomicalEventMod.Log.LogInfo("Event started at t=" + T + " anchor=" + a);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogError("Start failed: " + e);
                Message = "Start failed: " + e.Message;
                Stop();
            }
        }

        public void Pause()
        {
            if (State != EventState.Running)
                return;
            State = EventState.Paused;
            if (scene != null)
                scene.SetPaused(true);
            Audio.Pause();
        }

        public void Resume()
        {
            if (State != EventState.Paused)
                return;
            State = EventState.Running;
            if (scene != null)
                scene.SetPaused(false);
            Audio.Resume();
        }

        public void Restart()
        {
            int s = AstronomicalSettings.StartSequence.Value;
            UserStop();
            Start(s);
        }

        public void Stop()
        {
            CountingDown = false;
            SyncStarted = false;
            BroadcastedStart = false;
            Spectate = false;
            cdText.Hide();
            AstronomicalHazards.Release();
            try
            {
                Audio.Stop();
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning(e.Message);
            }
            try
            {
                if (scene != null)
                    scene.Cleanup();
                else
                    Effects.Cleanup();
                scene = null;
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Cleanup: " + e.Message);
            }
            if (State != EventState.Idle)
                AstronomicalEventMod.Log.LogInfo("Event stopped");
            State = EventState.Idle;
            T = 0;
            audioStarted = false;
        }

        public void Shutdown()
        {
            Stop();
            Audio.Dispose();
            Assets.Dispose();
        }

        public void Tick()
        {
            InitAudio();
            TickPendingJoin();
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            Audio.Poll();
            if (State != EventState.Running && State != EventState.Paused)
                AstronomicalHazards.Release(); // swimming must never outlive the event
            if (CountingDown)
            {
                CountdownLeft -= Time.unscaledDeltaTime;
                cdText.Tick(CountdownLeft);
                if (CountdownLeft <= 0f)
                {
                    CountingDown = false;
                    Message = "";
                    cdText.Hide();
                    StartNow(pendingSeq);
                    if (State == EventState.Running && pendingOffset > 0.5f)
                        T = Mathf.Min(T + pendingOffset, AstronomicalSequence.Total - 5f);
                }
                return;
            }
            if (State == EventState.Idle)
                return;
            if (State == EventState.Running)
            {
                T += Time.unscaledDeltaTime * AstronomicalSettings.Speed.Value;
                if (T >= AstronomicalSequence.Total)
                {
                    T = AstronomicalSequence.Total;
                    AstronomicalEventMod.Log.LogInfo("Timeline finished");
                    if (AstronomicalSettings.AutoCleanup.Value)
                    {
                        Stop();
                        return;
                    }
                    State = EventState.Finished;
                    Audio.Stop();
                }
                else if (Audio.Ready && !AstronomicalDebug.NoMusic)
                {
                    if (!audioStarted)
                    {
                        Audio.PlayAt(T);
                        audioStarted = true;
                    }
                    else if (!Audio.IsPlaying)
                    {
                        // music stopped by something outside the mod (voice limit, focus change): resume it, at most twice a second
                        if (Time.unscaledTime >= nextRestart && T < AstronomicalSequence.Total - 1f)
                        {
                            nextRestart = Time.unscaledTime + 0.5f;
                            Audio.PlayAt(T);
                            AstronomicalEventMod.Log.LogWarning("Music had stopped; restarted at t=" + T.ToString("0.0"));
                        }
                    }
                    else if (AstronomicalSettings.Speed.Value == 1f)
                    {
                        // audio clock is the master: follow it instead of ever seeking the music (no audible restarts after frame hitches)
                        float d = Audio.Time - T;
                        if (Mathf.Abs(d) > 0.25f)
                            T = Audio.Time;
                        else
                            T += d * 0.1f;
                    }
                    else if (Mathf.Abs(Audio.Time - T) > 0.6f)
                        Audio.PlayAt(T); // re-sync only when playback speed is not 1
                    Audio.SetVolume();
                }
            }
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                InForest = CheckForest();
                if (!InForest && !AstronomicalSettings.BypassForestCheck.Value && !AnyMap)
                {
                    Message = "Left Forest - event stopped and cleaned up.";
                    Stop();
                    return;
                }
            }
            float u;
            int i = AstronomicalSequence.IndexAt(T, out u);
            if (State == EventState.Running)
                Audio.Analyze(Time.unscaledDeltaTime);
            try
            {
                if (scene != null)
                    scene.Apply(T, i, u, Audio);
            }
            catch (Exception e)
            {
                Message = "Effect error: " + e.Message;
                AstronomicalEventMod.Log.LogError(e);
            }
        }

        public int CurrentSequence
        {
            get
            {
                float u;
                return AstronomicalSequence.IndexAt(T, out u);
            }
        }

        static string AstronomicalApiPos()
        {
            return new Vector3(
                AstronomicalSettings.FixedSpawnX.Value,
                AstronomicalSettings.FixedSpawnY.Value,
                AstronomicalSettings.FixedSpawnZ.Value
            ).ToString("0.0");
        }

        public bool HasSavedStage
        {
            get { return AstronomicalSettings.FixedSpawnX.Value != 0 || AstronomicalSettings.FixedSpawnZ.Value != 0; }
        }

        // Saves the spot Distance metres in front of the player as the permanent event location (used when UseFixedSpawn is on).
        public void SaveStageHere()
        {
            var player = GTPlayer.Instance;
            var cam = Camera.main;
            Vector3 p = player != null ? player.HeadCenterPosition : (cam != null ? cam.transform.position : Vector3.zero);
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            fwd = fwd.normalized;
            Vector3 s = p + fwd * AstronomicalSettings.Distance.Value;
            s.y = Physics.Raycast(
                new Vector3(s.x, p.y + 2f, s.z),
                Vector3.down,
                out RaycastHit hit,
                35f,
                ~0,
                QueryTriggerInteraction.Ignore
            )
                ? hit.point.y
                : p.y - 1.5f;
            AstronomicalSettings.FixedSpawnX.Value = s.x;
            AstronomicalSettings.FixedSpawnY.Value = s.y;
            AstronomicalSettings.FixedSpawnZ.Value = s.z;
            AstronomicalSettings.FixedSpawnYaw.Value = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            AstronomicalSettings.UseFixedSpawn.Value = true;
            AstronomicalSettings.FixedSpawnX.ConfigFile.Save();
            Message = "Saved event location: " + s.ToString("0.0");
        }

        public void ClearSavedStage()
        {
            AstronomicalSettings.FixedSpawnX.Value = 0;
            AstronomicalSettings.FixedSpawnY.Value = 0;
            AstronomicalSettings.FixedSpawnZ.Value = 0;
            AstronomicalSettings.UseFixedSpawn.Value = false;
            AstronomicalSettings.FixedSpawnX.ConfigFile.Save();
        }

        // Only ever called from an explicit user action (button / API call); the event itself never moves the player.
        public void TeleportToStage()
        {
            if (!HasSavedStage)
            {
                Message = "No saved stage location yet.";
                return;
            }
            var dest = new Vector3(
                AstronomicalSettings.FixedSpawnX.Value,
                AstronomicalSettings.FixedSpawnY.Value + 1.2f,
                AstronomicalSettings.FixedSpawnZ.Value
            );
            var player = GTPlayer.Instance;
            if (player != null)
                player.TeleportTo(dest, Quaternion.Euler(0, AstronomicalSettings.FixedSpawnYaw.Value, 0), false, false);
            else if (Camera.main != null)
                Camera.main.transform.position = dest;
        }

        // Moves the player to the gazebo floor facing the middle of the show (only when the user presses the menu button).
        public void GoToViewpoint()
        {
            Vector3 pos;
            float yaw;
            var player = GTPlayer.Instance;
            string kind = AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Kind : "";
            if (
                !(
                    kind == "rocket" ? Rocket.TryViewpoint(out pos, out yaw)
                    : kind == "end" ? End.TryViewpoint(out pos, out yaw)
                    : kind == "unvault" ? Unvault.TryViewpoint(out pos, out yaw)
                    : kind == "concert" ? Concert.TryViewpoint(out pos, out yaw)
                    : kind == "device" ? Device.TryViewpoint(out pos, out yaw)
                    : kind == "cube" ? CubeScene.TryViewpoint(out pos, out yaw)
                    : kind == "cubelake" ? LakeScene.TryViewpoint(out pos, out yaw)
                    : kind == "icestorm" ? IceScene.TryViewpoint(out pos, out yaw)
                    : kind == "newyear" ? NewYearScene.TryViewpoint(out pos, out yaw)
                    : kind == "butterfly" ? ButterflyScene.TryViewpoint(out pos, out yaw)
                    : kind == "collision" ? CollisionScene.TryViewpoint(out pos, out yaw)
                    : Showdown.TryViewpoint(out pos, out yaw)
                )
            )
            {
                try
                {
                    var cam = Camera.main;
                    pos = AstronomicalCountdown.FindGazeboBase(cam != null ? cam.transform.position : Vector3.zero) + Vector3.up * 1.2f;
                    yaw = AstronomicalSettings.FixedSpawnYaw.Value;
                }
                catch
                {
                    Message = "No viewpoint yet: start the event once first.";
                    return;
                }
            }
            if (player != null)
                player.TeleportTo(pos, Quaternion.Euler(0, yaw, 0), false, false);
            else if (Camera.main != null)
                Camera.main.transform.position = pos;
        }

        float fps = 60f;
        public float TimeLeft
        {
            get { return Mathf.Max(0f, AstronomicalSequence.Total - T); }
        }

        // jumps to just before the next big moment (flash / shatter / sonic / boom of size >= 40 in the event's cues); events without cues skip to the next section
        public void SkipToNextHit()
        {
            if (State == EventState.Idle)
                return;
            float next = -1f;
            var d = Event;
            if (d != null && d.Cues != null && d.Cues["events"] != null)
            {
                var ev = d.Cues["events"];
                for (int k = 0; k < ev.Count; k++)
                {
                    var n = ev[k];
                    string ty = (string)n["type"];
                    float t = (float)n["t"];
                    bool key =
                        ty == "flash" || ty == "shatter" || ty == "sonic" || (ty == "boom" && n["size"] != null && (float)n["size"] >= 40f);
                    if (key && t > T + 1.5f && (next < 0f || t < next))
                        next = t;
                }
            }
            if (next < 0f)
            {
                SeekToSequence(Mathf.Min(AstronomicalSequence.Start.Length - 1, CurrentSequence + 1));
                return;
            }
            T = Mathf.Max(0f, next - 0.3f);
            if (audioStarted && Audio.Ready)
                Audio.PlayAt(T);
        }

        public string DebugText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Event != null ? Event.Name : "-").Append('\n');
            int cs = State == EventState.Idle ? -1 : CurrentSequence;
            sb.Append(State)
                .Append("  t=")
                .Append(T.ToString("0.0"))
                .Append('/')
                .Append(AstronomicalSequence.Total.ToString("0"))
                .Append("  ")
                .Append(cs >= 0 ? AstronomicalSequence.Names[cs] : "")
                .Append('\n');
            sb.Append("fps ")
                .Append(fps.ToString("0"))
                .Append("  audio ")
                .Append(Audio.IsPlaying ? Audio.Time.ToString("0.0") : "off")
                .Append("  scale ")
                .Append(AstronomicalSettings.ShowScale.Value.ToString("0.00"));
            var dbg = scene as IDebug;
            if (dbg != null)
            {
                try
                {
                    sb.Append('\n').Append(dbg.DebugInfo());
                }
                catch { }
            }
            return sb.ToString();
        }

        public void SeekToTime(float t)
        {
            if (State == EventState.Idle)
                return;
            T = t;
            if (audioStarted && Audio.Ready)
                Audio.PlayAt(T);
        }

        public void SeekToSequence(int seq)
        {
            if (State == EventState.Idle)
                return;
            T = AstronomicalSequence.Start[Mathf.Clamp(seq, 0, AstronomicalSequence.Start.Length - 1)];
            if (audioStarted && Audio.Ready)
                Audio.PlayAt(T); // the music is the clock: seek it too, or it would pull the show back
        }
    }
}
