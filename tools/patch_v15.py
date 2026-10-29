R = r'C:\Users\drago\Documents\AstronomicalEventMod' + '\\'
def edit(fn, pairs):
    s = open(R + fn, encoding='utf-8').read()
    for a, b in pairs:
        assert a in s, (fn, a[:90])
        s = s.replace(a, b, 1)
    open(R + fn, 'w', encoding='utf-8').write(s)

edit('AstronomicalSettings.cs', [
 ("public static ConfigEntry<float> Countdown;", "public static ConfigEntry<float> Countdown, ShowScale, ShowPlateau;"),
 ("            // Spawn Location & Scale\n", "            ShowScale = c.Bind(\"Showdown\", \"ShowScale\", 1f, new ConfigDescription(\"Scale of the whole Final Showdown (giants, terrain, effects) around its origin\", new AcceptableValueRange<float>(0.3f, 2f)));\n            ShowPlateau = c.Bind(\"Showdown\", \"PlateauHeight\", 24f, new ConfigDescription(\"Metres above the Forest floor the giants' forested plateau stands (so they are not hidden behind the walls)\", new AcceptableValueRange<float>(0f, 80f)));\n            // Spawn Location & Scale\n"),
])
edit('AstronomicalEventDef.cs', [
 ("        public AudioClip Music; public GltfModel Avatar; public JNode Choreo;", "        public AudioClip Music; public GltfModel Avatar; public JNode Choreo;\n        // showdown-style events: several baked actors + props + a cue list\n        public string Kind = \"travis\", SwordFile, CuesFile; public float Plateau = 24f;\n        public List<string> ActorIds = new List<string>(), ActorFiles = new List<string>(); public List<float> ActorHeights = new List<float>(), ActorAnkle = new List<float>(); public List<GltfModel> ActorModels = new List<GltfModel>();\n        public GltfModel Sword; public JNode Cues;"),
 ("                        d.Length = d.Bounds[7]; d.SeqNames = b.SeqNames;", "                        d.Length = d.Bounds[7]; d.SeqNames = b.SeqNames;\n                        if (j[\"kind\"] != null) d.Kind = (string)j[\"kind\"]; if (j[\"plateau\"] != null) d.Plateau = (float)j[\"plateau\"]; d.SwordFile = (string)j[\"sword\"]; d.CuesFile = (string)j[\"cues\"];\n                        if (j[\"actors\"] != null) for (int k = 0; k < j[\"actors\"].Count; k++) { var an = j[\"actors\"][k]; d.ActorIds.Add((string)an[\"id\"]); d.ActorFiles.Add((string)an[\"glb\"]); d.ActorHeights.Add((float)an[\"height\"]); d.ActorAnkle.Add(an[\"ankle\"] != null ? (float)an[\"ankle\"] : 5f); }"),
 ("if (d.Bounds == null || string.IsNullOrEmpty(d.MusicFile))", "if (d.Bounds == null || (string.IsNullOrEmpty(d.MusicFile) && d.Kind != \"showdown\"))"),
])
edit('AstronomicalAssetLoader.cs', [
 ("            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId);\n            if (!string.IsNullOrEmpty(d.MusicFile))",
  """            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId);
            if (d.Kind == "showdown")
            {
                if (!string.IsNullOrEmpty(d.MusicFile)) yield return LoadMusicTo(Path.Combine(d.Folder, d.MusicFile), c => d.Music = c);
                d.ActorModels.Clear();
                for (int k = 0; k < d.ActorFiles.Count; k++) { GltfModel gm = null; Status = "loading " + d.ActorIds[k]; yield return LoadGlb(Path.Combine(d.Folder, d.ActorFiles[k]), true, true, m => gm = m); d.ActorModels.Add(gm); }
                if (!string.IsNullOrEmpty(d.SwordFile)) yield return LoadGlb(Path.Combine(d.Folder, d.SwordFile), false, false, m => d.Sword = m);
                try { if (!string.IsNullOrEmpty(d.CuesFile)) d.Cues = JNode.Parse(File.ReadAllText(Path.Combine(d.Folder, d.CuesFile))); } catch (Exception e) { Problems.Add("cues " + d.Id + ": " + e.Message); }
                d.Loaded = true; d.Loading = false; Status = "ready"; AstronomicalEventMod.Log.LogInfo("Event loaded: " + d.Id); yield break;
            }
            if (!string.IsNullOrEmpty(d.MusicFile))"""),
])
edit('AstronomicalEventMod.cs', [
 ("        public static AstronomicalEventMod Inst;", "        public static AstronomicalEventMod Inst;\n        public AstronomicalAssetLoader Loader;\n        public static void ParkModel(GameObject g) { if (Inst != null && Inst.Loader != null && g != null) Inst.Loader.Park(g); }"),
 ("            var loader = new AstronomicalAssetLoader();", "            var loader = new AstronomicalAssetLoader(); Loader = loader;"),
])
edit('AstronomicalCountdown.cs', [
 ("        void Build()", """        // The gazebo's footprint on the floor (so the show can be centred on the pit); falls back to the given point.
        public static Vector3 FindGazeboBase(Vector3 fallback)
        {
            Transform best = null; float bd = 120f * 120f;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (t == null || t.name.IndexOf("gazebo", StringComparison.OrdinalIgnoreCase) < 0) continue;
                float d = (new Vector2(t.position.x - fallback.x, t.position.z - fallback.z)).sqrMagnitude; if (d < bd) { bd = d; best = t; }
            }
            if (best == null) return fallback;
            float low = best.position.y; foreach (var r in best.GetComponentsInChildren<Renderer>()) low = Mathf.Min(low, r.bounds.min.y);
            return new Vector3(best.position.x, Mathf.Min(low, fallback.y + 3f), best.position.z);
        }

        void Build()"""),
])
edit('AstronomicalEffectsManager.cs', [
 ("    internal sealed class AstronomicalEffectsManager", "    internal sealed class AstronomicalEffectsManager : IEventScene"),
])
edit('AstronomicalEventManager.cs', [
 ("        public readonly AstronomicalEffectsManager Effects = new AstronomicalEffectsManager();", "        public readonly AstronomicalEffectsManager Effects = new AstronomicalEffectsManager();\n        public readonly AstronomicalShowdown Showdown = new AstronomicalShowdown();\n        IEventScene scene;\n        IEventScene PickScene() { return AstronomicalEvents.Selected != null && AstronomicalEvents.Selected.Kind == \"showdown\" ? (IEventScene)Showdown : Effects; }"),
 ("                Effects.Build(Assets, a, stageYaw);", "                scene = PickScene(); scene.Build(Assets, a, stageYaw);"),
 ("State = EventState.Paused; Effects.SetPaused(true);", "State = EventState.Paused; if (scene != null) scene.SetPaused(true);"),
 ("State = EventState.Running; Effects.SetPaused(false);", "State = EventState.Running; if (scene != null) scene.SetPaused(false);"),
 ("            try { Effects.Cleanup(); }", "            try { if (scene != null) scene.Cleanup(); else Effects.Cleanup(); scene = null; }"),
 ("            try { Effects.Apply(T, i, u, Audio); }", "            try { if (scene != null) scene.Apply(T, i, u, Audio); }"),
])
edit('AstronomicalShowdown.cs', [
 ("rob.Eye.intensity = eyesOn || t >= 90.2f && !jump ? 6f + 2f * Mathf.Sin(t * 5f) : (t >= 90.2f ? 6f : 0f);", "rob.Eye.intensity = t >= 90.2f ? 6f + 2f * Mathf.Sin(t * 5f) : 0f;"),
])
print('ok')
