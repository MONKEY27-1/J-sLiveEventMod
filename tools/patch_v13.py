R = r'C:\Users\drago\Documents\AstronomicalEventMod' + '\\'
def edit(fn, pairs, base=R):
    s = open(base + fn, encoding='utf-8').read()
    for a, b in pairs:
        assert a in s, (fn, a[:80])
        s = s.replace(a, b, 1)
    open(base + fn, 'w', encoding='utf-8').write(s)

edit('AstronomicalSettings.cs', [
 ("public static ConfigEntry<bool> Knockback, SwimInFlood;", "public static ConfigEntry<bool> Knockback, SwimInFlood, ShowStage;\n        public static ConfigEntry<float> Countdown;"),
 ("            // Spawn Location & Scale\n", "            Countdown = c.Bind(\"Timeline\", \"CountdownSeconds\", 0f, new ConfigDescription(\"Seconds of countdown after Start before the event begins (0 = start immediately)\", new AcceptableValueRange<float>(0f, 600f)));\n            ShowStage = c.Bind(\"Visual\", \"ShowStage\", false, \"Show the 3D stage model (off by default: Travis performs in the open arena)\");\n            // Spawn Location & Scale\n"),
])
edit('AstronomicalEventManager.cs', [
 ("        public void Start(int fromSeq)\n        {\n            if (State != EventState.Idle) return; // single instance only",
  "        public bool CountingDown; public float CountdownLeft; int pendingSeq;\n\n        public void Start(int fromSeq)\n        {\n            if (State != EventState.Idle || CountingDown) return;\n            float c = AstronomicalSettings.Countdown.Value;\n            if (c > 0.5f) { CountingDown = true; CountdownLeft = c; pendingSeq = fromSeq; Message = \"Event starts in \" + Mathf.CeilToInt(c) + \" s\"; AstronomicalEventMod.Log.LogInfo(\"Countdown started: \" + c + \" s\"); return; }\n            StartNow(fromSeq);\n        }\n\n        void StartNow(int fromSeq)\n        {\n            if (State != EventState.Idle) return; // single instance only"),
 ("        public void Stop()\n        {", "        public void Stop()\n        {\n            CountingDown = false;"),
 ("            Audio.Poll();\n            if (State == EventState.Idle) return;", "            Audio.Poll();\n            if (CountingDown)\n            {\n                CountdownLeft -= Time.unscaledDeltaTime;\n                if (CountdownLeft <= 0f) { CountingDown = false; Message = \"\"; StartNow(pendingSeq); }\n                return;\n            }\n            if (State == EventState.Idle) return;"),
])
edit('AstronomicalEventMod.cs', [
 ("        void OnGUI() { if (AstronomicalSettings.BuiltInMenu.Value) ui.Draw(); }",
  "        GUIStyle cdStyle;\n        void OnGUI()\n        {\n            if (AstronomicalSettings.BuiltInMenu.Value) ui.Draw();\n            if (mgr != null && mgr.CountingDown)\n            {\n                if (cdStyle == null) cdStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };\n                cdStyle.normal.textColor = Color.white;\n                GUI.Label(new Rect(0, Screen.height * 0.12f, Screen.width, 100), \"EVENT STARTS IN \" + Mathf.CeilToInt(mgr.CountdownLeft), cdStyle);\n            }\n        }"),
])
edit('AstronomicalEventApi.cs', [
 ("        public static bool CameraEffects {", "        /// <summary>Seconds of countdown between Start and the event beginning (0 = immediate). Saved in the config.</summary>\n        public static float CountdownSeconds { get { return AstronomicalSettings.Countdown.Value; } set { AstronomicalSettings.Countdown.Value = value; } }\n        public static bool IsCountingDown { get { return Mgr != null && Mgr.CountingDown; } }\n        public static float CountdownLeft { get { return Mgr != null && Mgr.CountingDown ? Mgr.CountdownLeft : 0f; } }\n        /// <summary>Show the 3D stage model (default false).</summary>\n        public static bool ShowStage { get { return AstronomicalSettings.ShowStage.Value; } set { AstronomicalSettings.ShowStage.Value = value; } }\n        public static bool CameraEffects {"),
])
edit('AstronomicalUI.cs', [
 ("            AstronomicalSettings.Knockback.Value = GUILayout.Toggle(", "            AstronomicalSettings.Countdown.Value = Slider(\"Countdown (s)\", AstronomicalSettings.Countdown.Value, 0, 120);\n            AstronomicalSettings.ShowStage.Value = GUILayout.Toggle(AstronomicalSettings.ShowStage.Value, \" Show stage model (next Start)\");\n            AstronomicalSettings.Knockback.Value = GUILayout.Toggle("),
])
# stage off + deck on the ground
edit('AstronomicalEffectsManager.cs', [
 ("            if (loader != null && loader.Stage != null && loader.Stage.Pivot != null)\n            {\n                stageModel = loader.Stage;",
  "            if (!AstronomicalSettings.ShowStage.Value) { }      // open arena: no stage model and no stand-in stage\n            else if (loader != null && loader.Stage != null && loader.Stage.Pivot != null)\n            {\n                stageModel = loader.Stage;"),
 ("(stageModel != null ? 5.85f : 0.5f) * st * deckGrow", "(stageModel != null ? 5.85f : 0f) * st * (stageModel != null ? deckGrow : 1f)"),
 ("            BuildCoaster();", "            BuildCoaster(loader);"),
 ("        void BuildCoaster()\n        {\n            coaster = new GameObject(\"AstronomicalCoaster\").transform;\n            coaster.SetParent(root.transform, false);",
  """        Vector3[] coasterPts, coasterUp;
        void BuildCoaster(AstronomicalAssetLoader l)
        {
            coaster = new GameObject("AstronomicalCoaster").transform;
            coaster.SetParent(root.transform, false);
            coasterPts = coasterUp = null;
            if (l != null && l.CoasterTrack != null && l.CoasterTrack.Pivot != null && l.CoasterCart != null && l.CoasterCart.Pivot != null && l.CoasterPath != null)
            {
                try
                {
                    var cp = l.CoasterPath; int n = (int)cp["count"]; var pts = new Vector3[n]; var ups = new Vector3[n];
                    for (int k = 0; k < n; k++)
                    {
                        var p = cp["points"][k]; var u = cp["up"][k];
                        pts[k] = new Vector3((float)p[0], (float)p[1], (float)p[2]); ups[k] = new Vector3((float)u[0], (float)u[1], (float)u[2]);
                    }
                    var o = cp["origin"]; var org = new Vector3((float)o[0], (float)o[1], (float)o[2]);
                    var tp = l.CoasterTrack.Pivot.transform; tp.SetParent(coaster, false); tp.localPosition = org; tp.localRotation = Quaternion.identity; tp.localScale = Vector3.one; l.CoasterTrack.Pivot.SetActive(true);   // the loader re-centres models on their bounds: org undoes that
                    coasterCart = l.CoasterCart.Pivot.transform; coasterCart.SetParent(coaster, false); coasterCart.localScale = Vector3.one; l.CoasterCart.Pivot.SetActive(true);
                    coasterPts = pts; coasterUp = ups; coasterModels = true;
                    return;
                }
                catch (Exception ex) { AstronomicalEventMod.Log.LogWarning("Coaster model failed, using the simple one: " + ex.Message); coasterPts = coasterUp = null; }
            }"""),
 ("            if (coasterCart == null || coasterTrack.Count < 2) return;",
  """            if (coasterPts != null && coasterCart != null)
            {
                int n = coasterPts.Length; float uu = Mathf.Repeat(t * (0.022f + kick * 0.01f), 1f) * n;
                int a = Mathf.FloorToInt(uu) % n, b = (a + 1) % n; float f = uu - Mathf.Floor(uu);
                Vector3 pos = Vector3.Lerp(coasterPts[a], coasterPts[b], f), fwd = (coasterPts[b] - coasterPts[a]).normalized, up = Vector3.Slerp(coasterUp[a], coasterUp[b], f);
                coasterCart.localPosition = pos + up * 0.13f;
                if (fwd.sqrMagnitude > 0.001f) coasterCart.localRotation = Quaternion.LookRotation(fwd, up);
                return;
            }
            if (coasterCart == null || coasterTrack.Count < 2) return;"""),
 ("        Transform coaster, coasterCart;", "        Transform coaster, coasterCart; bool coasterModels;"),
 ("                if (avatarModel != null && avatarModel.Pivot != null) assets.Park(avatarModel.Pivot);", "                if (avatarModel != null && avatarModel.Pivot != null) assets.Park(avatarModel.Pivot);\n                if (coasterModels) { if (assets.CoasterTrack != null && assets.CoasterTrack.Pivot != null) assets.Park(assets.CoasterTrack.Pivot); if (assets.CoasterCart != null && assets.CoasterCart.Pivot != null) assets.Park(assets.CoasterCart.Pivot); }"),
 ("            coaster = coasterCart = null; coasterTrack.Clear();", "            coaster = coasterCart = null; coasterTrack.Clear(); coasterPts = coasterUp = null; coasterModels = false;"),
])
edit('AstronomicalAssetLoader.cs', [
 ("        public JNode Choreo;", "        public GltfModel CoasterTrack, CoasterCart; public JNode CoasterPath;     // Blender-built roller coaster (tools/make_coaster.py)\n        public JNode Choreo;"),
 ("            try { var cp = Path.Combine(Dir, \"travis_choreo.json\");", "            Status = \"loading coaster\";\n            yield return LoadGlb(Path.Combine(Dir, \"coaster_track.glb\"), false, false, m => CoasterTrack = m);\n            yield return LoadGlb(Path.Combine(Dir, \"coaster_cart.glb\"), false, false, m => CoasterCart = m);\n            try { var pp = Path.Combine(Dir, \"coaster_path.json\"); if (File.Exists(pp)) CoasterPath = JNode.Parse(File.ReadAllText(pp)); } catch (Exception e) { Problems.Add(\"coaster path: \" + e.Message); }\n            try { var cp = Path.Combine(Dir, \"travis_choreo.json\");"),
])
# J CLIENT
J = r'C:\Users\drago\OneDrive\Documents\J CLIENT\src\Mods' + '\\'
edit('EventMods.cs', [
 ("                if (!Api.AssetsReady) return \"Event: loading assets...\";", "                if (!Api.AssetsReady) return \"Event: loading assets...\";\n                if (Api.IsCountingDown) return \"Event starts in \" + UnityEngine.Mathf.CeilToInt(Api.CountdownLeft) + \"s\";"),
 ("                int cur = Api.CurrentSequence;\n                if (Api.State", "                int cur = Api.CurrentSequence;\n                if (Api.IsCountingDown) t.text = \"EVENT STARTS IN\\n\" + UnityEngine.Mathf.CeilToInt(Api.CountdownLeft) + \"s\";\n                else if (Api.State"),
 ("            Setting(\"Event Volume\"", "            Setting(\"Event Countdown\", () => Api.CountdownSeconds <= 0 ? \"OFF\" : Api.CountdownSeconds.ToString(\"0\") + \"s\", () => Api.CountdownSeconds = Next(Api.CountdownSeconds, 0f, 5f, 10f, 15f, 30f, 60f, 120f, 180f, 300f, 600f), \"Countdown between pressing Start and the event beginning (0 = off). Shown in Event Status and the sky countdown\");\n            Setting(\"Event Stage\", () => Api.ShowStage ? \"ON\" : \"OFF\", () => Api.ShowStage = !Api.ShowStage, \"Show the 3D stage (off = open arena; applies on next start)\");\n            Setting(\"Event Knockback\", () => Api.Knockback ? \"ON\" : \"OFF\", () => Api.Knockback = !Api.Knockback, \"Explosions, meteors, stomps and rings push you\");\n            Setting(\"Event Swim In Flood\", () => Api.SwimInFlood ? \"ON\" : \"OFF\", () => Api.SwimInFlood = !Api.SwimInFlood, \"Use the game's swimming in the underwater sequence\");\n            Setting(\"Event Volume\""),
], base=J)
print('ok')
