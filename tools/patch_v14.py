R = r'C:\Users\drago\Documents\AstronomicalEventMod' + '\\'
def edit(fn, pairs, base=R):
    s = open(base + fn, encoding='utf-8').read()
    for a, b in pairs:
        assert a in s, (fn, a[:90])
        s = s.replace(a, b, 1)
    open(base + fn, 'w', encoding='utf-8').write(s)

edit('AstronomicalSettings.cs', [
 ("public static ConfigEntry<float> Countdown;", "public static ConfigEntry<float> Countdown;\n        public static ConfigEntry<bool> SyncBroadcast, SyncReceive;\n        public static ConfigEntry<string> SyncServer, SyncTopic;"),
 ("            // Spawn Location & Scale\n", "            SyncBroadcast = c.Bind(\"Sync\", \"Broadcast\", true, \"When you start/stop the event, tell other players who have this mod (any lobby) through the relay\");\n            SyncReceive = c.Bind(\"Sync\", \"Receive\", true, \"Join events other mod users start (only while you are in Forest and idle)\");\n            SyncServer = c.Bind(\"Sync\", \"Server\", \"https://ntfy.sh\", \"Public pub/sub relay used to reach other lobbies (ntfy-compatible). Only 'start/stop event X in N s' is sent\");\n            SyncTopic = c.Bind(\"Sync\", \"Topic\", \"astro-event-jclient-7c41e9\", \"Relay channel. Everyone who should see each other's events must use the same topic\");\n            // Spawn Location & Scale\n"),
])
edit('AstronomicalHazards.cs', [
 ("""            var pl = Player(); if (pl == null) return;
            bool on = want && AstronomicalSettings.SwimInFlood.Value && (pl.HeadCenterPosition - anchor).sqrMagnitude < 90f * 90f;
            if (on == swimOn) return;
            try { pl.forcedUnderwater = on; swimOn = on; AstronomicalEventMod.Log.LogInfo("Flood swimming " + (on ? "on" : "off")); }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Swim toggle failed: " + e.Message); swimOn = false; }
        }

        public static void Reset() { Hits = 0; if (swimOn) { var pl = Player(); try { if (pl != null) pl.forcedUnderwater = false; } catch { } swimOn = false; } }""",
  """            if (!want && !swimOn) return;
            var pl = Player(); if (pl == null) return;
            bool on = want && AstronomicalSettings.SwimInFlood.Value && (pl.HeadCenterPosition - anchor).sqrMagnitude < 90f * 90f;
            try
            {
                if (pl.forcedUnderwater != on) { pl.forcedUnderwater = on; AstronomicalEventMod.Log.LogInfo("Flood swimming " + (on ? "on" : "off")); }
                swimOn = on;
            }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Swim toggle failed: " + e.Message); }
        }

        // Called every frame while no event is running (and on Stop): if we ever switched swimming on, make sure it really goes off; retries until it does.
        public static void Release()
        {
            if (!swimOn) return;
            var pl = Player(); if (pl == null) return;
            try { pl.forcedUnderwater = false; swimOn = false; AstronomicalEventMod.Log.LogInfo("Flood swimming released"); }
            catch (Exception e) { Warn2(e.Message); }
        }
        static void Warn2(string m) { if (Time.time - lastWarn > 5f) { lastWarn = Time.time; AstronomicalEventMod.Log.LogWarning("Swim release failed: " + m); } }

        public static void Reset() { Hits = 0; Release(); }"""),
])
edit('AstronomicalAssetLoader.cs', [
 ("        public JNode Choreo;", "        public void Use(AstronomicalEventDef d) { Music = d.Music; Avatar = d.Avatar; Choreo = d.Choreo; }\n        public JNode Choreo;"),
 ("            yield return LoadMusic(Path.Combine(Dir, \"TravisScottConcert.mp3\"));", "            yield return LoadMusicTo(Path.Combine(Dir, \"TravisScottConcert.mp3\"), c => Music = c);"),
 ("            Done = true; Status = \"ready (music \"", "            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId); if (bd != null) { bd.Music = Music; bd.Avatar = Avatar; bd.Choreo = Choreo; bd.Loaded = true; }\n            Done = true; Status = \"ready (music \""),
 ("        IEnumerator LoadMusic(string path)\n        {", """        // Loads the music / performer / choreography of an extra event (the built-in one is loaded by LoadAll).
        public IEnumerator LoadEvent(AstronomicalEventDef d)
        {
            if (d.Loaded || d.Loading) yield break;
            d.Loading = true;
            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId);
            if (!string.IsNullOrEmpty(d.MusicFile)) yield return LoadMusicTo(Path.Combine(d.Folder, d.MusicFile), c => d.Music = c);
            if (!string.IsNullOrEmpty(d.AvatarFile)) yield return LoadGlb(Path.Combine(d.Folder, d.AvatarFile), true, true, m => d.Avatar = m);
            if (d.Avatar == null && bd != null) d.Avatar = bd.Avatar;
            try { if (!string.IsNullOrEmpty(d.ChoreoFile)) { var cp = Path.Combine(d.Folder, d.ChoreoFile); if (File.Exists(cp)) d.Choreo = JNode.Parse(File.ReadAllText(cp)); } }
            catch (Exception e) { Problems.Add("choreo " + d.Id + ": " + e.Message); }
            if (d.Choreo == null && d.Avatar == (bd != null ? bd.Avatar : null) && bd != null) d.Choreo = bd.Choreo;
            d.Loaded = true; d.Loading = false;
            AstronomicalEventMod.Log.LogInfo("Event loaded: " + d.Id);
        }

        IEnumerator LoadMusicTo(string path, Action<AudioClip> done)
        {"""),
 ("            Music = DownloadHandlerAudioClip.GetContent(req);\n            req.Dispose();\n            if (Music != null) { Music.name = \"TravisScottConcert\"; UnityEngine.Object.DontDestroyOnLoad(Music); AstronomicalEventMod.Log.LogInfo(\"Music loaded, \" + Music.length + \"s\"); }",
  "            var clip = DownloadHandlerAudioClip.GetContent(req);\n            req.Dispose();\n            if (clip != null) { clip.name = Path.GetFileNameWithoutExtension(path); UnityEngine.Object.DontDestroyOnLoad(clip); AstronomicalEventMod.Log.LogInfo(\"Music loaded, \" + clip.length + \"s\"); }\n            done(clip);"),
])
edit('AstronomicalEventMod.cs', [
 ("        void Awake()\n        {", "        public static AstronomicalEventMod Inst;\n        void Awake()\n        {\n            Inst = this;"),
 ("            var loader = new AstronomicalAssetLoader();", "            AstronomicalEvents.Scan(AstronomicalAssetLoader.Dir);\n            var loader = new AstronomicalAssetLoader();"),
 ("            ui = new AstronomicalUI(mgr);", "            ui = new AstronomicalUI(mgr);\n            StartCoroutine(AstronomicalNet.Poll(mgr));"),
 ("""            if (mgr != null && mgr.CountingDown)
            {
                if (cdStyle == null) cdStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                cdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 100), "EVENT STARTS IN " + Mathf.CeilToInt(mgr.CountdownLeft), cdStyle);
            }
""", ""),
 ("        GUIStyle cdStyle;\n", ""),
])
edit('AstronomicalEventManager.cs', [
 ("""            audioInit = true;
            if (Assets.Music != null)
            {
                // Event = 0:00 .. 8:49 of the supplied audio (trimmed file plays from 0; a longer file is cut at 529 s by the timeline length).
                float len = Assets.Music.length; float start = 0f, end = Mathf.Min(len, 529f);
                Audio.UseClip(Assets.Music, start);
                if (State == EventState.Idle) AstronomicalSequence.Rescale(Mathf.Min(Assets.Music.length, end) - start);
            }
            else Audio.Prepare();
        }""", """            audioInit = true;
            ApplyDef(AstronomicalEvents.Selected);
        }

        // Makes an event current: its music, performer, choreography and section timings.
        void ApplyDef(AstronomicalEventDef d)
        {
            if (d == null) return;
            Assets.Use(d); AstronomicalSequence.Configure(d);
            if (d.Music != null) { Audio.UseClip(d.Music, 0f); AstronomicalSequence.Rescale(Mathf.Min(d.Music.length, d.Length)); }
            else Audio.Prepare();
        }

        public AstronomicalEventDef Event { get { return AstronomicalEvents.Selected; } }

        public bool SelectEvent(string id)
        {
            var d = AstronomicalEvents.Find(id);
            if (d == null || State != EventState.Idle || CountingDown) return false;
            if (!d.Loaded)
            {
                if (!d.Loading && AstronomicalEventMod.Inst != null) AstronomicalEventMod.Inst.StartCoroutine(LoadThenApply(d));
                Message = "Loading event " + d.Name + "...";
                return false;
            }
            AstronomicalEvents.Selected = d; ApplyDef(d); return true;
        }

        System.Collections.IEnumerator LoadThenApply(AstronomicalEventDef d)
        {
            yield return Assets.LoadEvent(d);
            if (State == EventState.Idle && !CountingDown) { AstronomicalEvents.Selected = d; ApplyDef(d); Message = ""; }
        }"""),
 ("""        public bool CountingDown; public float CountdownLeft; int pendingSeq;

        public void Start(int fromSeq)
        {
            if (State != EventState.Idle || CountingDown) return;
            float c = AstronomicalSettings.Countdown.Value;
            if (c > 0.5f) { CountingDown = true; CountdownLeft = c; pendingSeq = fromSeq; Message = "Event starts in " + Mathf.CeilToInt(c) + " s"; AstronomicalEventMod.Log.LogInfo("Countdown started: " + c + " s"); return; }
            StartNow(fromSeq);
        }
""", """        public bool CountingDown; public float CountdownLeft; int pendingSeq; float pendingOffset;
        public bool SyncStarted, BroadcastedStart;
        readonly AstronomicalCountdown cdText = new AstronomicalCountdown();

        // User start: counts down (if configured), tells other mod users, then runs.
        public void Start(int fromSeq) { StartCore(fromSeq, AstronomicalSettings.Countdown.Value, 0f, true); }

        void StartCore(int fromSeq, float countdown, float offset, bool broadcast)
        {
            if (State != EventState.Idle || CountingDown) return;
            if (broadcast) { BroadcastedStart = false; SyncStarted = false; if (Assets.Done && AstronomicalSettings.SyncBroadcast.Value) { BroadcastedStart = true; AstronomicalNet.Publish("start", Event.Id, countdown > 0.5f ? countdown : 0f, fromSeq); } }
            else SyncStarted = true;
            if (countdown > 0.5f)
            {
                CountingDown = true; CountdownLeft = countdown; pendingSeq = fromSeq; pendingOffset = offset; Message = "Event starts in " + Mathf.CeilToInt(countdown) + " s";
                AstronomicalEventMod.Log.LogInfo("Countdown started: " + countdown + " s");
                cdText.Show(StageSpot());
                return;
            }
            StartNow(fromSeq);
            if (State == EventState.Running && offset > 0.5f) T = Mathf.Min(T + offset, AstronomicalSequence.Total - 5f);
        }

        // Another mod user (possibly in another lobby) started an event: join it, with the same countdown / at the same point of the show.
        public void StartFromNet(string evId, int seq, float remaining)
        {
            if (!AstronomicalSettings.SyncReceive.Value || State != EventState.Idle || CountingDown || !Assets.Done) return;
            if (!CheckForest() && !AstronomicalSettings.BypassForestCheck.Value) return;
            var d = AstronomicalEvents.Find(evId); if (d == null) return;
            if (d != Event && !SelectEvent(evId)) return;
            if (remaining > 0.5f) StartCore(seq, remaining, 0f, false);
            else if (-remaining < AstronomicalSequence.Total - 10f) StartCore(seq, 0f, -remaining, false);
            else return;
            Message = "Joined event from another player";
            AstronomicalEventMod.Log.LogInfo("Sync: joined " + evId + " (remaining " + remaining.ToString("0.0") + ")");
        }

        public void StopFromNet(string evId)
        {
            if (!SyncStarted || Event == null || !string.Equals(Event.Id, evId, StringComparison.OrdinalIgnoreCase)) return;
            Message = "Event stopped by the host"; Stop();
        }

        // Stop pressed by the user: also stops it for the players who joined through us.
        public void UserStop()
        {
            if (BroadcastedStart && Event != null) AstronomicalNet.Publish("stop", Event.Id, 0f, 0);
            Stop();
        }

        Vector3 StageSpot()
        {
            if (HasSavedStage) return new Vector3(AstronomicalSettings.FixedSpawnX.Value, AstronomicalSettings.FixedSpawnY.Value, AstronomicalSettings.FixedSpawnZ.Value);
            var cam = Camera.main; return cam != null ? cam.transform.position + new Vector3(cam.transform.forward.x, 0, cam.transform.forward.z).normalized * AstronomicalSettings.Distance.Value : Vector3.zero;
        }
"""),
 ("        public void Stop()\n        {\n            CountingDown = false;", "        public void Stop()\n        {\n            CountingDown = false; SyncStarted = false; BroadcastedStart = false; cdText.Hide();\n            AstronomicalHazards.Release();"),
 ("""            Audio.Poll();
            if (CountingDown)
            {
                CountdownLeft -= Time.unscaledDeltaTime;
                if (CountdownLeft <= 0f) { CountingDown = false; Message = ""; StartNow(pendingSeq); }
                return;
            }""", """            Audio.Poll();
            if (State != EventState.Running && State != EventState.Paused) AstronomicalHazards.Release();      // swimming must never outlive the event
            if (CountingDown)
            {
                CountdownLeft -= Time.unscaledDeltaTime;
                cdText.Tick(CountdownLeft);
                if (CountdownLeft <= 0f)
                {
                    CountingDown = false; Message = ""; cdText.Hide(); StartNow(pendingSeq);
                    if (State == EventState.Running && pendingOffset > 0.5f) T = Mathf.Min(T + pendingOffset, AstronomicalSequence.Total - 5f);
                }
                return;
            }"""),
 ("            int s = AstronomicalSettings.StartSequence.Value;\n            Stop(); Start(s);", "            int s = AstronomicalSettings.StartSequence.Value;\n            UserStop(); Start(s);"),
])
edit('AstronomicalEventApi.cs', [
 ("public static void Stop() { if (Mgr != null) Mgr.Stop(); }", "public static void Stop() { if (Mgr != null) Mgr.UserStop(); }"),
 ("        public static bool CameraEffects {", """        // ---- events (more than one show can be installed: AstronomicalAssets/events/<id>/event.json) ----
        public static int EventCount { get { return AstronomicalEvents.All.Count; } }
        public static string GetEventId(int i) { return AstronomicalEvents.All[Mathf.Clamp(i, 0, EventCount - 1)].Id; }
        public static string GetEventName(int i) { return AstronomicalEvents.All[Mathf.Clamp(i, 0, EventCount - 1)].Name; }
        public static string SelectedEventId { get { return AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Id : ""; } }
        public static string SelectedEventName { get { return AstronomicalEvents.Selected != null ? AstronomicalEvents.Selected.Name : ""; } }
        /// <summary>Makes an event current (only while idle). Extra events load their music/performer first, so it may need a second call.</summary>
        public static bool SelectEvent(string id) { return Mgr != null && Mgr.SelectEvent(id); }
        // ---- cross-lobby sync (other players who have the mod) ----
        public static bool SyncBroadcast { get { return AstronomicalSettings.SyncBroadcast.Value; } set { AstronomicalSettings.SyncBroadcast.Value = value; } }
        public static bool SyncReceive { get { return AstronomicalSettings.SyncReceive.Value; } set { AstronomicalSettings.SyncReceive.Value = value; } }
        public static string SyncStatus { get { return AstronomicalNet.Status + " (received " + AstronomicalNet.Received + ")"; } }
        public static bool CameraEffects {"""),
])
edit('AstronomicalUI.cs', [
 ("            AstronomicalSettings.Knockback.Value = GUILayout.Toggle(", "            AstronomicalSettings.SyncBroadcast.Value = GUILayout.Toggle(AstronomicalSettings.SyncBroadcast.Value, \" Sync: tell other mod users when I start/stop\");\n            AstronomicalSettings.SyncReceive.Value = GUILayout.Toggle(AstronomicalSettings.SyncReceive.Value, \" Sync: join events other mod users start\");\n            AstronomicalSettings.Knockback.Value = GUILayout.Toggle("),
])
J = r'C:\Users\drago\OneDrive\Documents\J CLIENT\src\Mods' + '\\'
edit('EventMods.cs', [
 ("                if (Api.IsCountingDown) t.text = \"EVENT STARTS IN\\n\" + UnityEngine.Mathf.CeilToInt(Api.CountdownLeft) + \"s\";\n                else if (Api.State", "                if (Api.State"),
 ("            Setting(\"Event Countdown\"", "            Setting(\"Event Selected\", () => Api.SelectedEventName, () => { int n = Api.EventCount; for (int k = 0; k < n; k++) if (Api.GetEventId(k) == Api.SelectedEventId) { Api.SelectEvent(Api.GetEventId((k + 1) % n)); break; } }, \"Which installed event Start runs (more events can be added as folders)\");\n            Setting(\"Event Tell Others\", () => Api.SyncBroadcast ? \"ON\" : \"OFF\", () => Api.SyncBroadcast = !Api.SyncBroadcast, \"Starting/stopping the event also starts/stops it for other mod users in any lobby\");\n            Setting(\"Event Join Others\", () => Api.SyncReceive ? \"ON\" : \"OFF\", () => Api.SyncReceive = !Api.SyncReceive, \"Join events that other mod users start (Forest only)\");\n            Setting(\"Event Countdown\""),
], base=J)
print('ok')
