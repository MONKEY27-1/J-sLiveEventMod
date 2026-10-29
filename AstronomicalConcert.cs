using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "Marshmello Showtime" (Fortnite, 2 Feb 2019), recreated in the Forest: the textured black-truss stage with yellow / black LED screens (colour visuals that change with every song), the DJ booth
    // with Marshmello playing, two giant holographic Cuddle Team Leaders dancing beside the stage, a giant holographic Marshmello that rises over the roof on the drops, beat-chasing light strips,
    // lasers, pyro, confetti, paint splashes on the floor and an aurora sky. Every dance move is a clip baked in Blender (tools/dance_gen.py, one unique pose per two beats, nothing repeats);
    // everything else is a pure function of the music time, with the beats, hits, drops and the six songs coming from tools/build_concert.py (cues.json). Local only.
    internal sealed class AstronomicalConcert : IEventScene, IDebug
    {
        const float SCALE_DOWN = 0.55f;                        // the fitted arena scale is the biggest that fits the floor; the show is built smaller than that
        const float STAGE_Z = 48f;                           // centre of the stage in design metres (the audience stands in front of it, round z = 0)
        const string ARENA_JSON = "{\"pts\":[[0,0],[0,26],[30,26],[-30,26],[0,48],[36,48],[-36,48],[0,68],[36,68],[-36,68],[52,48],[-52,48]],\"margin\":14}";
        static readonly Color[][] PAL = {
            new[] { new Color(0.25f, 0.45f, 1f), new Color(0.65f, 0.35f, 1f), new Color(0.3f, 0.95f, 1f) },
            new[] { new Color(1f, 0.2f, 0.8f), new Color(0.2f, 1f, 0.9f), new Color(0.6f, 1f, 0.2f) },
            new[] { new Color(1f, 0.3f, 0.4f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.6f, 1f) },
            new[] { new Color(1f, 0.45f, 0.1f), new Color(1f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.3f) },
            new[] { Color.red, Color.green, Color.blue },
            new[] { new Color(1f, 0.85f, 0.1f), new Color(1f, 1f, 1f), new Color(1f, 0.45f, 0.75f) } };
        sealed class Splash { public float T0, Size; public Transform T; public Material M; public Color C; }
        sealed class Laser { public LineRenderer Soft, Core; public Vector3 O; }
        sealed class Screen { public MeshFilter MF; public Mesh Orig, Mine; public Vector2[] Base, Buf; }
        sealed class Dancer { public GltfModel M; public GltfModel.Clip Clip; public float K = 1f; public Transform Pivot; public float Face; }

        AstronomicalEventDef def; GameObject root, dome; WorldKit kit; Vector3 origin; float yaw, sc = 1f; int floorLayer; bool built, paused;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>(); readonly List<KeyValuePair<Renderer, Material[]>> restoreMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<GltfModel> models = new List<GltfModel>();
        // data from cues.json
        float[] beats = new float[0], energy = new float[0], hitT = new float[0], hitS = new float[0], dropT = new float[0], songT = { 0f }; float[][] holo = new float[0][]; int[] songSheet = new int[6]; float endT = 617f;
        // stage
        Transform stageT; Dictionary<string, Transform> nodes = new Dictionary<string, Transform>(); readonly List<Screen> screens = new List<Screen>(); Material ledMat; readonly Texture[] sheets = new Texture[6];
        Material[] stripMat = new Material[66]; Vector3[] stripPos = new Vector3[66]; Vector3[] flameSpots = new Vector3[8]; Vector3 stageC; float deckTop, archTop, deckFront, stageHalfW;
        Dancer dj, giant, bearL, bearR; Material holoBear, holoGiant; Vector3 giantRest; float holoAmt;
        // effects
        ParticleSystem fx, flame, confetti, stars; Material matGlow, matFlat; Texture2D glowTex, lineTex, splatTex; Laser[] lasers = new Laser[13]; readonly Splash[] splashes = new Splash[40]; int splashNext;
        // extras: floor shockwaves, halos round the giant, searchlights, light pillars, ground fog, drifting orbs, equaliser bars, fireworks
        sealed class Rocket { public float T0, Rise; public Vector3 A, B; public Color C; public bool Burst; public int Kind; }
        readonly List<Rocket> fireworks = new List<Rocket>(); LineRenderer[] waves = new LineRenderer[4], halos = new LineRenderer[3], search = new LineRenderer[8], pillars = new LineRenderer[6]; float[] waveT = { -99f, -99f, -99f, -99f }; Color[] waveC = new Color[4]; bool[] waveHit = new bool[4]; int waveNext;
        Vector3[] searchO = new Vector3[8], pillO = new Vector3[6]; Transform[] bars = new Transform[38]; Material[] barMat = new Material[3]; ParticleSystem fog, orbs; float floorY, accFog, accOrb; Vector3 waveAt;
        Light[] lights = new Light[8]; Light sun; Mesh skyMesh; Color[] skyCols; Vector3[] skyDirs; int skyN; float skyT = -9f;
        // state
        float lastT = -1f, flashV, lightKick, rumbleT; int hitIdx, dropIdx, curSong = -1, lastBeat = -1, song; Vector3 ambientSave; bool ambientSaved; float screenFlash;
        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        static float Sm(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }
        static float Rnd(float a, float b) { return UnityEngine.Random.Range(a, b); }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }
        Vector3 WV(Vector3 l) { return root.transform.TransformVector(l); }
        Color Pc(int k, float t) { if (song == 4) return Color.HSVToRGB(Mathf.Repeat(t * 0.08f + k * 0.33f, 1f), 0.85f, 1f); return PAL[Mathf.Clamp(song, 0, 5)][k % 3]; }

        // ------------------------------------------------------------------ helpers
        static Bounds LB(Transform rootT, Transform node)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in node.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue; var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++) { var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1)); var p = rootT.InverseTransformPoint(mf.transform.TransformPoint(c)); if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p); }
            }
            return b;
        }
        Transform Node(string n) { Transform t; return nodes.TryGetValue(n, out t) ? t : null; }
        void SetActive(string n, bool on) { var t = Node(n); if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on); }
        Light AddLight(Vector3 localPos, Color c, float range, float intensity) { var g = new GameObject("ConcertLight"); g.transform.SetParent(root.transform, false); g.transform.localPosition = localPos; var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = Mathf.Max(8f, range * sc); l.intensity = intensity; l.shadows = LightShadows.None; return l; }
        ParticleSystem MakePS(string name, Material mat, int max, float grav, float life, Transform parent = null, bool local = false)
        {
            var g = new GameObject(name); g.transform.SetParent(parent != null ? parent : root.transform, false); var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.maxParticles = max; m.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World; m.playOnAwake = false; m.startLifetime = life; m.gravityModifier = grav;
            var e = ps.emission; e.enabled = false; g.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; ps.Play(); return ps;
        }
        // positions / velocities given in design (root-local) units, sizes in design metres
        void Emit(ParticleSystem ps, Vector3 lp, Vector3 lv, Color c, float size, float life, float rot = 0f)
        {
            var ep = new ParticleSystem.EmitParams(); ep.position = W(lp); ep.velocity = WV(lv); ep.startColor = c; ep.startSize = size * sc; ep.startLifetime = life; ep.rotation = rot; ps.Emit(ep, 1);
        }
        internal static Vector3 Facing(GltfModel m)
        {
            try
            {
                // the hips: the character's left thigh minus its right one, turned a quarter about up, is the way it faces (the toe bones of the Epic rig point every which way)
                Transform tl = null, tr = null;
                foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant(); if (n.Contains("twist")) continue;
                    if (tl == null && (n.StartsWith("thigh_l") || n.Contains("leftupleg"))) tl = t; else if (tr == null && (n.StartsWith("thigh_r") || n.Contains("rightupleg"))) tr = t;
                }
                if (tl != null && tr != null) { var d = m.Pivot.transform.InverseTransformDirection(tl.position - tr.position); d.y = 0f; if (d.sqrMagnitude > 1e-6f) return Vector3.Cross(Vector3.up, d).normalized; }
            }
            catch { }
            try
            {
                Transform foot = null, toe = null;
                foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant(); if (n.Contains("end") || n.Contains("_ik") || n.Contains("ik_")) continue;
                    if (toe == null && (n.Contains("toe") || n.StartsWith("ball") || n.Contains(":ball"))) toe = t;
                    else if (foot == null && n.Contains("foot") && !n.Contains("twist")) foot = t;
                }
                if (foot != null && toe != null) { var d = m.Pivot.transform.InverseTransformDirection(toe.position - foot.position); d.y = 0f; if (d.sqrMagnitude > 1e-6f) return d.normalized; }
            }
            catch { }
            return Vector3.forward;
        }
        Dancer MakeDancer(string propId, string clipName, Transform parent, Vector3 pos, float height, Color? holo)
        {
            var m = def.Prop(propId); if (m == null || m.Pivot == null) { AstronomicalEventMod.Log.LogWarning("Concert: model '" + propId + "' missing"); return null; }
            models.Add(m); var d = new Dancer { M = m, Pivot = m.Pivot.transform }; var f = Facing(m); float face = 180f - Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            d.Clip = m.FindClipExact(clipName); if (d.Clip == null && m.Clips.Count > 0) d.Clip = m.Clips[0];
            d.K = height / Mathf.Max(0.01f, m.RendererBounds.size.y); d.Face = face;
            d.Pivot.SetParent(parent, false); d.Pivot.localPosition = pos; d.Pivot.localRotation = Quaternion.Euler(0f, face, 0f); d.Pivot.localScale = Vector3.one * d.K; m.Pivot.SetActive(true);
            foreach (var r in m.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var sk = r as SkinnedMeshRenderer; if (sk != null) sk.updateWhenOffscreen = true;
            }
            AstronomicalEventMod.Log.LogInfo("Concert: " + propId + " height " + height.ToString("0.0") + " scale " + d.K.ToString("0.000") + " face " + face.ToString("0") + " clip " + (d.Clip != null ? d.Clip.Name + " " + d.Clip.Duration.ToString("0") + "s" : "none"));
            return d;
        }
        void MakeHolo(Dancer d, Material mat)
        {
            if (d == null) return;
            foreach (var r in d.M.Pivot.GetComponentsInChildren<Renderer>(true)) { var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)]; for (int k = 0; k < arr.Length; k++) arr[k] = mat; r.sharedMaterials = arr; }
        }

        // ------------------------------------------------------------------ build
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false; ReadCues();
            Vector3 head = anchorPos; try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            Vector3 org = head; try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; float y = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; sc = Mathf.Max(0.2f, AstronomicalSettings.ShowScale.Value);
            bool sl = AstronomicalShowdown.lastFit; Vector3 so = AstronomicalShowdown.fitOrigin; float sy = AstronomicalShowdown.fitYaw, ss = AstronomicalShowdown.fitScale;
            try { AstronomicalShowdown.FitArena(ref org, ref y, ref sc, JNode.Parse(ARENA_JSON)); } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Arena fit failed: " + e.Message); }
            AstronomicalShowdown.lastFit = sl; AstronomicalShowdown.fitOrigin = so; AstronomicalShowdown.fitYaw = sy; AstronomicalShowdown.fitScale = ss;
            sc *= SCALE_DOWN; origin = org; yaw = y; fitOk = true; fitO = origin; fitY = yaw;
            floorLayer = 0; for (int i = 0; i < 32; i++) if ((LocoMask() & (1 << i)) != 0) { floorLayer = i; break; }
            RaycastHit h; if (Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out h, 8f, LocoMask(), QueryTriggerInteraction.Ignore)) floorLayer = h.collider.gameObject.layer;
            root = new GameObject("MarshmelloShowtime"); root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0)); root.transform.localScale = Vector3.one * sc; kit = new WorldKit { Layer = floorLayer };
            glowTex = kit.GlowTex(); matGlow = kit.Glow(Color.white, glowTex, 3000); matFlat = kit.Glow(Color.white, null, 3000);
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            BuildStage(); BuildDancers(); BuildEffects(); BuildSky(); BuildExtras();
            sun = new GameObject("ConcertSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0f;
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b); ambientSaved = true;
            lastT = -1f; hitIdx = dropIdx = 0; curSong = -1; lastBeat = -1; flashV = 0f; built = true;
            AstronomicalEventMod.Log.LogInfo("Concert built: origin " + origin + " scale " + sc.ToString("0.00") + " yaw " + yaw.ToString("0") + ", collider layer " + floorLayer + ", stage " + (stageT != null) + ", screens " + screens.Count + ", dancers " + (dj != null) + (giant != null) + (bearL != null) + (bearR != null) + ", beats " + beats.Length + ", hits " + hitT.Length);
        }
        static bool fitOk; static Vector3 fitO; static float fitY;
        public bool TryViewpoint(out Vector3 pos, out float y)
        {
            pos = Vector3.zero; y = 0f; if (root != null) { pos = root.transform.position + Vector3.up * 1.2f; y = yaw; return true; }
            if (fitOk) { pos = fitO + Vector3.up * 1.2f; y = fitY; return true; } return false;
        }

        void ReadCues()
        {
            var c = def != null ? def.Cues : null; endT = def != null && def.Length > 10f ? def.Length : 617f;
            if (c == null || c["beats"] == null) { AstronomicalEventMod.Log.LogWarning("Concert: cues.json missing - a plain 100 bpm grid is used"); var b = new List<float>(); for (float t = 0f; t < endT; t += 0.6f) b.Add(t); beats = b.ToArray(); energy = new float[] { 0.3f }; hitT = hitS = dropT = new float[0]; holo = new float[0][]; songT = new[] { 0f }; return; }
            beats = c["beats"].Floats(); energy = c["energy"] != null ? c["energy"].Floats() : new float[] { 0.3f };
            int n = c["hits"] != null ? c["hits"].Count : 0; hitT = new float[n]; hitS = new float[n]; for (int i = 0; i < n; i++) { hitT[i] = (float)c["hits"][i]["t"]; hitS[i] = (float)c["hits"][i]["s"]; }
            n = c["drops"] != null ? c["drops"].Count : 0; dropT = new float[n]; for (int i = 0; i < n; i++) dropT[i] = (float)c["drops"][i]["t"];
            n = c["holo"] != null ? c["holo"].Count : 0; var hl = new List<float[]>(); for (int i = 0; i < n; i++) hl.Add(c["holo"][i].Floats()); hl.Add(new[] { endT - 20f, endT + 5f }); foreach (float d in dropT) hl.Add(new[] { d - 0.5f, d + 14f }); holo = hl.ToArray();
            n = c["songs"] != null ? c["songs"].Count : 1; songT = new float[n]; songSheet = new int[Mathf.Max(6, n)]; for (int i = 0; i < n; i++) { songT[i] = (float)c["songs"][i]["t"]; songSheet[i] = c["songs"][i]["sheet"] != null ? (int)c["songs"][i]["sheet"] : i; }
            if (c["end"] != null) endT = (float)c["end"];
        }

        void BuildStage()
        {
            var sm = def.Prop("stage"); if (sm == null || sm.Pivot == null) { AstronomicalEventMod.Log.LogWarning("Concert: stage model missing"); return; }
            models.Add(sm); stageT = sm.Pivot.transform; stageT.SetParent(root.transform, false);
            float gy = -9999f; foreach (var dz in new[] { 0f, -20f, 20f }) foreach (var dx in new[] { 0f, -30f, 30f }) { RaycastHit h; var w = W(new Vector3(dx, 6f, STAGE_Z + dz)); if (Physics.Raycast(w, Vector3.down, out h, 40f, LocoMask(), QueryTriggerInteraction.Ignore)) gy = Mathf.Max(gy, root.transform.InverseTransformPoint(h.point).y); }
            if (gy < -9000f) gy = 0f;
            stageT.localPosition = new Vector3(0f, gy - 0.3f, STAGE_Z); stageT.localRotation = Quaternion.Euler(0f, 180f, 0f); stageT.localScale = Vector3.one; sm.Pivot.SetActive(true);
            nodes.Clear(); foreach (var t in sm.Pivot.GetComponentsInChildren<Transform>(true)) if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;
            foreach (var r in sm.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            foreach (string n in new[] { "StageDeck", "StageWall", "StageArch", "StageMetal", "DjBooth" })
            {
                var t = Node(n); if (t == null) continue;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) { mf.gameObject.layer = floorLayer; var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; owned.Add(mc); }
            }
            Bounds deck = Node("StageDeck") != null ? LB(root.transform, Node("StageDeck")) : new Bounds(new Vector3(0, 3, STAGE_Z), new Vector3(60, 1, 36)), arch = Node("StageArch") != null ? LB(root.transform, Node("StageArch")) : deck;
            stageC = deck.center; deckTop = deck.max.y; deckFront = deck.min.z; archTop = arch.max.y; stageHalfW = deck.extents.x;
            // the LED screens: one shared material, each screen's mesh gets its own uv set so the flip-book can move the frame
            foreach (string n in new[] { "LEDBack", "LEDSideL", "LEDSideR" })
            {
                var t = Node(n); if (t == null) continue; var mf = t.GetComponentInChildren<MeshFilter>(true); var r = t.GetComponentInChildren<Renderer>(true); if (mf == null || r == null) continue;
                if (ledMat == null) ledMat = r.sharedMaterial; else r.sharedMaterial = ledMat;
                var s = new Screen { MF = mf, Orig = mf.sharedMesh, Mine = UnityEngine.Object.Instantiate(mf.sharedMesh) }; s.Base = s.Orig.uv; s.Buf = new Vector2[s.Base.Length]; mf.sharedMesh = s.Mine; owned.Add(s.Mine); screens.Add(s);
            }
            var vm = def.Prop("visuals");
            if (vm != null && vm.Pivot != null) { models.Add(vm); for (int k = 0; k < 6; k++) foreach (var t in vm.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == "Sheet" + k) { var r = t.GetComponentInChildren<Renderer>(true); if (r != null && r.sharedMaterial != null) { sheets[k] = r.sharedMaterial.mainTexture; if (sheets[k] != null) sheets[k].wrapMode = TextureWrapMode.Clamp; } break; } }
            // the light strips get a material each (restored afterwards)
            for (int i = 0; i < 66; i++)
            {
                var t = Node("Strip" + i); if (t == null) continue; var r = t.GetComponentInChildren<Renderer>(true); if (r == null) continue;
                restoreMats.Add(new KeyValuePair<Renderer, Material[]>(r, r.sharedMaterials)); var m = new Material(r.sharedMaterial); owned.Add(m); r.sharedMaterial = m; stripMat[i] = m; stripPos[i] = LB(root.transform, t).center;
            }
            for (int k = 0; k < 8; k++) { int si = Mathf.Clamp(1 + k * 3, 0, 27); flameSpots[k] = stripMat[si] != null ? new Vector3(stripPos[si].x, deckTop + 0.2f, deckFront + 0.6f) : new Vector3(stageC.x + (k - 3.5f) * 7f, deckTop + 0.2f, deckFront + 0.6f); }
            foreach (string n in new[] { "TextAlone", "TextWant", "TextHappier", "TextMello" }) SetActive(n, false);
        }

        void BuildDancers()
        {
            Bounds booth = Node("DjBooth") != null ? LB(root.transform, Node("DjBooth")) : new Bounds(new Vector3(stageC.x, deckTop + 0.5f, STAGE_Z), new Vector3(10, 1, 6));
            dj = MakeDancer("marsh", "DJ", root.transform, new Vector3(booth.center.x, deckTop, booth.max.z + 1.1f), 3.6f, null);
            float side = stageHalfW + 17f;
            bearL = MakeDancer("bearL", "BearA", root.transform, new Vector3(stageC.x - side, GroundAt(stageC.x - side, STAGE_Z), STAGE_Z), 22f, null);
            bearR = MakeDancer("bearR", "BearB", root.transform, new Vector3(stageC.x + side, GroundAt(stageC.x + side, STAGE_Z), STAGE_Z), 22f, null);
            giantRest = new Vector3(stageC.x, archTop + 0.4f, STAGE_Z + 2f);
            giant = MakeDancer("holo", "Dance", root.transform, giantRest, 20f, null);
            holoBear = new Material(matFlat) { color = new Color(0.25f, 0.62f, 1f, 0.5f) }; holoBear.renderQueue = 3002; owned.Add(holoBear);
            holoGiant = new Material(matFlat) { color = new Color(0.85f, 0.95f, 1f, 0.5f) }; holoGiant.renderQueue = 3003; owned.Add(holoGiant);
            MakeHolo(bearL, holoBear); MakeHolo(bearR, holoBear); MakeHolo(giant, holoGiant);
            if (giant != null) giant.M.Pivot.SetActive(false);
        }
        float GroundAt(float x, float z)
        {
            RaycastHit h; var w = W(new Vector3(x, 6f, z)); if (Physics.Raycast(w, Vector3.down, out h, 40f, LocoMask(), QueryTriggerInteraction.Ignore)) return root.transform.InverseTransformPoint(h.point).y; return 0f;
        }

        void BuildEffects()
        {
            fx = MakePS("ConcertFx", matGlow, 4000, 0.1f, 2f);
            flame = MakePS("ConcertFlame", matGlow, 3000, -0.05f, 1.4f);
            var col = flame.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), new GradientColorKey(new Color(0.35f, 0.1f, 0.05f), 1f) };
            gr.alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }; col.color = gr;
            var sol = flame.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.7f));
            confetti = MakePS("ConcertConfetti", matFlat, 5000, 0.12f, 9f);
            for (int i = 0; i < lasers.Length; i++)
            {
                var l = new Laser(); var g = new GameObject("Laser" + i); g.transform.SetParent(root.transform, false);
                l.Soft = g.AddComponent<LineRenderer>(); l.Soft.useWorldSpace = true; l.Soft.positionCount = 2; l.Soft.sharedMaterial = kit.Glow(Color.white, lineTex, 3001); l.Soft.shadowCastingMode = ShadowCastingMode.Off; l.Soft.enabled = false;
                var g2 = new GameObject("LaserCore" + i); g2.transform.SetParent(g.transform, false); l.Core = g2.AddComponent<LineRenderer>(); l.Core.useWorldSpace = true; l.Core.positionCount = 2; l.Core.sharedMaterial = kit.Glow(Color.white, lineTex, 3002); l.Core.shadowCastingMode = ShadowCastingMode.Off; l.Core.enabled = false;
                int si = i < 11 ? 28 + i * 2 : (i == 11 ? 64 : 65); l.O = stripMat[si] != null ? stripPos[si] + new Vector3(0f, 0.5f, 0f) : new Vector3(stageC.x + (i - 6) * 5f, archTop - 6f, STAGE_Z);
                lasers[i] = l;
            }
            splatTex = kit.Tex(128, 128, (x, y) =>
            {
                float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float edge = 0.42f + 0.22f * (WorldKit.Fbm(Mathf.Cos(a) * 1.7f + 3f, Mathf.Sin(a) * 1.7f + 3f, 5, 3) - 0.5f) * 2f + 0.1f * Mathf.Abs(Mathf.Sin(a * 7f)); float al = 1f - Mathf.Clamp01((r - edge) / 0.04f);
                for (int k = 0; k < 10; k++) { float ak = WorldKit.Hash(k, 1, 77) * 6.2832f, dk = 0.55f + 0.38f * WorldKit.Hash(k, 2, 77), rk = 0.03f + 0.05f * WorldKit.Hash(k, 3, 77); float ex = dx - Mathf.Cos(ak) * dk, ey = dy - Mathf.Sin(ak) * dk; al = Mathf.Max(al, 1f - Mathf.Clamp01((Mathf.Sqrt(ex * ex + ey * ey) - rk) / 0.03f)); }
                float sh = 0.82f + 0.18f * WorldKit.Fbm(x * 0.08f, y * 0.08f, 9); return new Color(sh, sh, sh, al * 0.92f);
            }, TextureWrapMode.Clamp);
            for (int i = 0; i < splashes.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>()); q.name = "Splash" + i; q.transform.SetParent(root.transform, false);
                var s = new Splash { T = q.transform, M = kit.Glow(Color.white, splatTex, 2990), T0 = -999f }; q.GetComponent<Renderer>().sharedMaterial = s.M; q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; q.SetActive(false); splashes[i] = s;
            }
            for (int i = 0; i < lights.Length; i++)
            {
                Vector3 p = i < 4 ? new Vector3(stageC.x + (i - 1.5f) * stageHalfW * 0.55f, archTop - 8f, deckFront + 2f) : i < 6 ? new Vector3(stageC.x + (i == 4 ? -1 : 1) * (stageHalfW + 4f), 9f, deckFront + 2f) : new Vector3(stageC.x + (i == 6 ? -1 : 1) * 14f, 6f, deckFront - 14f);
                lights[i] = AddLight(p, Color.white, 70f, 0f);
            }
        }

        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI * 0.52f / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 280f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray(); skyN = skyCols.Length;
            dome = new GameObject("ConcertSky"); dome.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = dome.AddComponent<MeshRenderer>(); var dm = new Material(matGlow); dm.mainTexture = null; dm.renderQueue = 2501;          // (the glow texture's corner pixel is transparent: with it the dome could not be seen) owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = MakePS("ConcertStars", matGlow, 800, 0f, 100000f, dome.transform, true);
            for (int k = 0; k < 520; k++) { var d = UnityEngine.Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.97f + 0.03f; var ep = new ParticleSystem.EmitParams(); ep.position = d.normalized * 270f; ep.velocity = Vector3.zero; ep.startSize = Rnd(0.8f, 2.4f); ep.startLifetime = 100000f; float b = Rnd(0.5f, 1f); ep.startColor = new Color(b, b, Mathf.Min(1f, b + 0.15f), 1f); stars.Emit(ep, 1); }
        }
        // night with an aurora: three curtains whose height and brightness drift with time, coloured from the song's palette
        void UpdateSky(float t, float en)
        {
            if (skyMesh == null) return; float gain = 0.55f + 0.9f * Mathf.Clamp01(en * 1.6f) + flashV * 0.5f;
            Color c0 = Pc(0, t), c1 = Pc(1, t), c2 = Pc(2, t);
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k]; float el = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)), az = Mathf.Atan2(d.z, d.x); float yy = Mathf.Clamp01(d.y);
                var c = Color.Lerp(new Color(0.07f, 0.09f, 0.20f), new Color(0.008f, 0.01f, 0.06f), Mathf.Pow(yy, 0.5f));
                for (int b = 0; b < 3; b++)
                {
                    float centre = 0.5f + 0.2f * b + 0.09f * Mathf.Sin(az * (2 + b) + t * (0.15f + 0.07f * b)), w = 0.2f; float band = Mathf.Exp(-((el - centre) / w) * ((el - centre) / w));
                    float rays = 0.55f + 0.45f * Mathf.Sin(az * (9 + 3 * b) + t * (0.6f + 0.2f * b) + Mathf.Sin(az * 3f + t * 0.3f) * 2f), amp = band * rays * (0.6f + 0.4f * Mathf.Sin(az * 1.7f + b * 2f + t * 0.2f)) * gain * 0.55f;
                    var pc = b == 0 ? c0 : b == 1 ? c1 : c2; c.r += pc.r * amp; c.g += pc.g * amp; c.b += pc.b * amp;
                }
                c.a = 1f; skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        // ------------------------------------------------------------------ per frame
        public void SetPaused(bool p)
        {
            paused = p; foreach (var ps in new[] { fx, flame, confetti, fog, orbs }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); }
        }
        int SongAt(float t) { int s = 0; for (int i = 0; i < songT.Length; i++) if (t >= songT[i]) s = i; return s; }
        int BeatIdx(float t) { if (beats.Length < 2) return 0; int i = Array.BinarySearch(beats, t); if (i < 0) i = ~i - 1; return Mathf.Clamp(i, 0, beats.Length - 2); }
        float Energy(float t) { if (energy.Length < 2) return energy.Length == 1 ? energy[0] : 0.3f; float f = Mathf.Clamp(t * 2f, 0f, energy.Length - 1.001f); int i = (int)f; return Mathf.Lerp(energy[i], energy[i + 1], f - i); }
        float HoloAt(float t) { float a = 0f; foreach (var w in holo) { float v = Sm(w[0], w[0] + 2.5f, t) * (1f - Sm(w[1] - 0.5f, w[1] + 3f, t)); if (v > a) a = v; } return a; }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built) return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f); bool back = t < lastT - 0.4f;
            if (back || lastT < 0f) { hitIdx = dropIdx = 0; lastBeat = -1; skyT = -9f; fireworks.Clear(); }
            lastT = t; var cam = Camera.main; if (cam != null) dome.transform.position = cam.transform.position;
            int si = SongAt(t); if (si != curSong) SetSong(si, t);
            int bi = BeatIdx(t); float span = beats.Length > 1 ? Mathf.Max(0.05f, beats[bi + 1] - beats[bi]) : 0.6f, ph = Mathf.Clamp01((t - (beats.Length > 1 ? beats[bi] : 0f)) / span), pulse = Mathf.Pow(1f - ph, 3f), en = Energy(t);
            if (bi != lastBeat) { lastBeat = bi; if (!paused && en > 0.22f && bi % 4 == 0) Confetti(30, 0.7f); if (!paused && en > 0.3f && bi % 8 == 0) Wave(t); if (!paused && t > endT - 22f && bi % 3 == 0) Firework(t, bi & 1); }
            while (hitIdx < hitT.Length && hitT[hitIdx] <= t) { int k = hitIdx++; if (t - hitT[k] < 1.2f && !paused) Hit(hitS[k], t); }
            while (dropIdx < dropT.Length && dropT[dropIdx] <= t) { int k = dropIdx++; if (t - dropT[k] < 1.5f && !paused) Drop(t); }
            flashV = Mathf.Max(0f, flashV - dt * 2.2f); lightKick = Mathf.Max(0f, lightKick - dt * 5f);
            Atmosphere(t, en);
            if (t - skyT > 0.1f || t < skyT) { skyT = t; UpdateSky(t, en); }
            Screens(t, bi, ph, pulse, en); Strips(t, bi, ph, pulse, en); Texts(t, bi, pulse); LaserStep(t, bi, ph, pulse, en); LightStep(t, pulse, en); Dancers(t, dt, pulse); Splashes(t); Extras(t, dt, bi, pulse, en);
            if (en > 0.2f && !paused) { Confetti(Mathf.Min(8, Take(en * 14f * Parts, dt)), 0.6f); }
            Rumble(en, pulse, dt);
        }
        float accT; int Take(float rate, float dt) { accT += rate * dt; int n = (int)accT; accT -= n; return n; }
        void Rumble(float en, float pulse, float dt)
        {
            if (dt <= 0f || paused) return; rumbleT += dt; if (rumbleT < 0.12f || en < 0.15f) return; rumbleT = 0f;
            try { var gt = GorillaTagger.Instance; if (gt != null) { float a = Mathf.Clamp01(0.02f + 0.12f * pulse * en); gt.StartVibration(true, a, 0.1f); gt.StartVibration(false, a, 0.1f); } } catch { }
        }

        void SetSong(int s, float t)
        {
            curSong = s; song = Mathf.Min(s, 5); flashV = 1f; screenFlash = 1f; skyT = -9f; if (!paused && t > 1f) { Wave(t); for (int k = 0; k < 3; k++) Firework(t + 0.3f * k, k & 1); }
            if (ledMat != null) { int k = Mathf.Clamp(songSheet[Mathf.Min(s, songSheet.Length - 1)], 0, 5); if (sheets[k] != null) ledMat.mainTexture = sheets[k]; }
            SetActive("TextAlone", s == 0); SetActive("TextWant", s == 5); SetActive("TextHappier", s == 5);
        }
        void Atmosphere(float t, float en)
        {
            RenderSettings.ambientLight = Color.Lerp(new Color(0.16f, 0.17f, 0.30f), new Color(0.28f, 0.30f, 0.46f), Mathf.Clamp01(en * 2f));
            sun.color = new Color(0.55f, 0.6f, 0.95f); sun.intensity = 0.55f * Mathf.Max(0.4f, Lit); sun.transform.rotation = Quaternion.Euler(50f, yaw + 180f, 0f);
        }
        void Screens(float t, int bi, float ph, float pulse, float en)
        {
            if (screens.Count == 0 || ledMat == null) return;
            int f = (bi * 4 + (int)(ph * 4f)) & 15; int c = f & 3, r = f >> 2; float u0 = c * 0.25f, v0 = (3 - r) * 0.25f;
            foreach (var s in screens) { for (int i = 0; i < s.Base.Length; i++) s.Buf[i] = new Vector2(u0 + (0.01f + 0.98f * s.Base[i].x) * 0.25f, v0 + (0.01f + 0.98f * s.Base[i].y) * 0.25f); s.Mine.uv = s.Buf; }
            screenFlash = Mathf.Max(0f, screenFlash - Time.deltaTime * 2.5f); float b = en < 0.08f ? 0.45f : 0.72f + 0.28f * pulse; ledMat.color = Color.Lerp(new Color(b, b, b, 1f), Color.white, Mathf.Max(screenFlash, flashV * 0.5f));
        }
        void Strips(float t, int bi, float ph, float pulse, float en)
        {
            float bt = bi + ph;
            for (int i = 0; i < 66; i++)
            {
                var m = stripMat[i]; if (m == null) continue; float v; Color col;
                if (i < 28) { v = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(i * 0.45f - bt * 3.1f), 3f); col = Pc((i / 4 + bi) % 3, t); }
                else if (i < 50) { v = 0.25f + 0.75f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(i * 0.3f + bt * 2.4f), 2f); col = Pc((i + bi / 2) % 3, t); }
                else { v = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((i - 50) * 0.8f - bt * 3.1f), 2f); col = Pc(i % 3, t); }
                v = Mathf.Clamp01(0.12f + v * (0.5f + 0.5f * pulse) * (en < 0.08f ? 0.4f : 1f) + flashV * 0.5f); col = Color.Lerp(col, Color.white, flashV * 0.5f); m.color = new Color(col.r * v, col.g * v, col.b * v, 1f);
            }
        }
        void Texts(float t, int bi, float pulse)
        {
            float k = 1f + 0.04f * pulse + 0.06f * flashV;
            foreach (string n in new[] { "TextAlone", "TextWant", "TextHappier", "TextMello", "LogoF" }) { var tr = Node(n); if (tr != null) tr.localScale = Vector3.one * k; }
            SetActive("TextMello", song >= 1 && song <= 4 && ((bi / 8) & 1) == 0);
        }
        void LaserStep(float t, int bi, float ph, float pulse, float en)
        {
            bool on = en > 0.12f || flashV > 0.1f; int N = lasers.Length; float intensity = on ? Mathf.Clamp01(0.45f + 0.55f * pulse + flashV) : 0f; float len = 170f, wS = 1.6f * (0.7f + 0.5f * pulse), wC = 0.35f; int sgn = (bi & 1) == 0 ? 1 : -1;
            for (int i = 0; i < N; i++)
            {
                var l = lasers[i]; if (l == null) continue; if (intensity <= 0.01f) { l.Soft.enabled = false; l.Core.enabled = false; continue; }
                float yw, pt, fi = i / (float)(N - 1), a = i * 6.2832f / N;
                switch (song)
                {
                    case 0: yw = Mathf.Sin(t * 0.5f + i * 0.35f) * 0.7f; pt = 0.35f + 0.15f * Mathf.Sin(t * 0.7f + i); break;
                    case 1: yw = (i % 2 == 0 ? 1f : -1f) * Mathf.Sin(t * 1.1f) * 0.9f; pt = 0.3f + 0.25f * Mathf.Abs(Mathf.Sin(t * 0.8f + i)); break;
                    case 2: yw = Mathf.Cos(t * 1.5f + a) * 0.5f; pt = 0.55f + Mathf.Sin(t * 1.5f + a) * 0.4f; break;
                    case 3: yw = (fi - 0.5f) * 1.6f; pt = 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(t * 0.9f + i * 0.2f)); break;
                    case 4: yw = Mathf.Sin(t * 0.8f) * 1f + (i - N / 2) * 0.12f; pt = 0.4f + 0.3f * Mathf.Sin(t * 1.3f + i * 0.5f); break;
                    default: yw = (fi - 0.5f) * 2.2f * (sgn > 0 ? 1f : -0.6f); pt = 0.45f + 0.35f * Mathf.Sin(t * 2f + i); break;
                }
                var dir = Quaternion.Euler(pt * Mathf.Rad2Deg, yw * Mathf.Rad2Deg, 0f) * Vector3.back; var p0 = l.O; var p1 = p0 + dir * len;
                Color c = song == 4 ? Color.HSVToRGB(Mathf.Repeat(i / (float)N + t * 0.1f, 1f), 0.9f, 1f) : Pc(i % 3, t); c = Color.Lerp(c, Color.white, flashV);
                l.Soft.enabled = true; l.Core.enabled = true; l.Soft.SetPosition(0, W(p0)); l.Soft.SetPosition(1, W(p1)); l.Core.SetPosition(0, W(p0)); l.Core.SetPosition(1, W(p1)); l.Soft.widthMultiplier = wS * sc; l.Core.widthMultiplier = wC * sc;
                l.Soft.startColor = l.Soft.endColor = new Color(c.r, c.g, c.b, 0.55f * intensity); l.Core.startColor = l.Core.endColor = new Color(1f, 1f, 1f, 0.9f * intensity);
            }
        }
        void LightStep(float t, float pulse, float en)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i]; if (l == null) continue; var c = Color.Lerp(Pc(i % 3, t), Color.white, flashV * 0.7f);
                l.color = c; l.intensity = Lit * (0.6f + 2.6f * pulse * Mathf.Clamp01(en * 2.5f) + 2.2f * lightKick + 4f * flashV);
            }
        }
        void Dancers(float t, float dt, float pulse)
        {
            if (dj != null && dj.Clip != null) dj.M.Sample(dj.Clip, t);
            if (bearL != null && bearL.Clip != null) bearL.M.Sample(bearL.Clip, t);
            if (bearR != null && bearR.Clip != null) bearR.M.Sample(bearR.Clip, t);
            var bc = Pc(2, t); bc = Color.Lerp(new Color(0.25f, 0.62f, 1f), bc, song == 0 ? 0.15f : 0.35f); if (holoBear != null) holoBear.color = new Color(bc.r, bc.g, bc.b, 0.38f + 0.22f * pulse);
            holoAmt = HoloAt(t);
            if (giant != null)
            {
                bool vis = holoAmt > 0.01f; if (giant.M.Pivot.activeSelf != vis) giant.M.Pivot.SetActive(vis);
                if (vis)
                {
                    if (giant.Clip != null) giant.M.Sample(giant.Clip, t);
                    giant.Pivot.localPosition = giantRest + Vector3.up * (-26f * (1f - holoAmt)); giant.Pivot.localScale = Vector3.one * giant.K * (1f + 0.035f * pulse + 0.05f * flashV);
                    var gc = Color.Lerp(new Color(0.85f, 0.95f, 1f), Pc(0, t), 0.3f); holoGiant.color = new Color(gc.r, gc.g, gc.b, (0.28f + 0.3f * pulse) * Mathf.Clamp01(holoAmt * 1.5f));
                }
            }
        }
        void Splashes(float t)
        {
            foreach (var s in splashes)
            {
                if (s == null) continue; float age = t - s.T0; bool on = age >= 0f && age < 48f; if (s.T.gameObject.activeSelf != on) s.T.gameObject.SetActive(on); if (!on) continue;
                float e = Mathf.Clamp01(age / 0.25f); float k = e < 1f ? 1f + 0.35f * Mathf.Sin(e * Mathf.PI) * (1f - e) + (e - 1f) : 1f; k = Mathf.Lerp(0.1f, 1f, 1f - (1f - e) * (1f - e)); s.T.localScale = Vector3.one * s.Size * k; s.M.color = new Color(s.C.r, s.C.g, s.C.b, 1f - Sm(34f, 48f, age));
            }
        }


        // ------------------------------------------------------------------ the extras
        LineRenderer MakeLine(string name, Material mat, int n, bool loop)
        {
            var g = new GameObject(name); g.transform.SetParent(root.transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = n; l.loop = loop; l.sharedMaterial = mat; l.shadowCastingMode = ShadowCastingMode.Off; l.enabled = false; return l;
        }
        void SetRing(LineRenderer l, Vector3 c, float r, Quaternion q)
        {
            int n = l.positionCount; for (int k = 0; k < n; k++) { float a = k * Mathf.PI * 2f / n; l.SetPosition(k, W(c + q * new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r))); }
        }
        void BuildExtras()
        {
            var soft = kit.Glow(Color.white, lineTex, 3001); float zb = deckFront - 2.4f; floorY = GroundAt(stageC.x, deckFront - 3f);
            for (int i = 0; i < waves.Length; i++) waves[i] = MakeLine("Wave" + i, soft, 64, true);
            for (int i = 0; i < halos.Length; i++) halos[i] = MakeLine("Halo" + i, soft, 72, true);
            for (int i = 0; i < search.Length; i++) { search[i] = MakeLine("Search" + i, soft, 2, false); float x = stageC.x + (i - 3.5f) * (stageHalfW * 2f / 8f); searchO[i] = new Vector3(x, GroundAt(x, 3f), 3f); }
            float[] px = { -(stageHalfW + 3f), stageHalfW + 3f, -(stageHalfW + 3f), stageHalfW + 3f, -(stageHalfW + 17f), stageHalfW + 17f }, pz = { deckFront + 2f, deckFront + 2f, deckFront + 30f, deckFront + 30f, STAGE_Z - 14f, STAGE_Z - 14f };
            for (int i = 0; i < pillars.Length; i++) { pillars[i] = MakeLine("Pillar" + i, soft, 2, false); float x = stageC.x + px[i]; pillO[i] = new Vector3(x, GroundAt(x, pz[i]), pz[i]); }
            fog = MakePS("ConcertFog", matGlow, 1500, 0f, 9f); orbs = MakePS("ConcertOrbs", matGlow, 700, 0f, 9f);
            for (int k = 0; k < 3; k++) barMat[k] = kit.Glow(Color.white, null, 3001);
            var box = kit.Box(new Vector3(1f, 1f, 1f), 1f);
            for (int i = 0; i < bars.Length; i++) { float x = stageC.x + (i / (float)(bars.Length - 1) - 0.5f) * (stageHalfW * 2f - 2f); bars[i] = kit.Obj("Bar" + i, root.transform, box, barMat[i % 3], new Vector3(x, GroundAt(x, zb) + 0.05f, zb), Quaternion.identity, new Vector3(1.5f, 0.5f, 0.9f), false).transform; }
        }
        void Wave(float t)
        {
            waveT[waveNext] = t; waveHit[waveNext] = false; waveC[waveNext] = Pc(UnityEngine.Random.Range(0, 3), t); waveNext = (waveNext + 1) % waves.Length; waveAt = new Vector3(stageC.x, floorY + 0.3f, deckFront - 3f);
        }
        void Firework(float t0, int kind)
        {
            if (fireworks.Count > 24) return; var f = new Rocket { T0 = t0, Rise = Rnd(1.3f, 1.9f), Kind = kind };
            f.A = new Vector3(stageC.x + Rnd(-1f, 1f) * stageHalfW, deckTop, STAGE_Z + Rnd(8f, 16f)); f.B = f.A + new Vector3(Rnd(-25f, 25f), Rnd(55f, 90f), Rnd(-5f, 20f));
            f.C = song == 4 ? Color.HSVToRGB(Rnd(0f, 1f), 0.8f, 1f) : Pc(UnityEngine.Random.Range(0, 3), t0); fireworks.Add(f);
        }
        static int TakeA(ref float acc, float rate, float dt) { acc += rate * dt; int n = (int)acc; acc -= n; return n; }
        void Extras(float t, float dt, int bi, float pulse, float en)
        {
            bool live = en > 0.1f || flashV > 0.1f;
            // floor shockwaves from the stage front
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i]; if (w == null) continue; float age = t - waveT[i], life = 2.2f; bool on = age >= 0f && age < life; if (w.enabled != on) w.enabled = on; if (!on) continue;
                if (!waveHit[i] && !paused)          // the ring pushes the player once as it passes them, unless they jumped over it
                {
                    var hp = Head(); if (hp.HasValue) { var c0 = W(waveAt); float dx = hp.Value.x - c0.x, dz = hp.Value.z - c0.z, d = Mathf.Sqrt(dx * dx + dz * dz), r = (3f + age * 30f) * sc; if (Mathf.Abs(d - r) < 1.6f * sc + 0.7f && hp.Value.y - c0.y < 2.2f) { waveHit[i] = true; AstronomicalHazards.Blast(new Vector3(c0.x, hp.Value.y, c0.z), Mathf.Max(6f, d * 5f), 12f, 9f); } }
                }
                float k = age / life; SetRing(w, waveAt, 3f + age * 30f, Quaternion.identity); var c = waveC[i]; w.startColor = w.endColor = new Color(c.r, c.g, c.b, 0.9f * (1f - k) * (1f - k)); w.widthMultiplier = (2.6f * (1f - 0.7f * k)) * sc;
            }
            // three spinning halos round the giant hologram
            float ha = Mathf.Clamp01(holoAmt * 1.5f); Vector3 mid = giantRest + Vector3.up * (-26f * (1f - holoAmt) + 10f);
            for (int i = 0; i < halos.Length; i++)
            {
                var h = halos[i]; if (h == null) continue; bool on = ha > 0.02f; if (h.enabled != on) h.enabled = on; if (!on) continue;
                var q = Quaternion.Euler(60f * Mathf.Sin(t * 0.4f + i * 2f), t * (30f + 25f * i) * (i % 2 == 0 ? 1f : -1f), 40f * Mathf.Cos(t * 0.3f + i)); SetRing(h, mid, (9f + 4f * i) * (1f + 0.08f * pulse), q);
                var c = Pc(i, t); h.startColor = h.endColor = new Color(c.r, c.g, c.b, 0.85f * ha); h.widthMultiplier = (0.5f + 0.4f * pulse) * sc;
            }
            // searchlights sweeping over the crowd, pillars of light at the stage corners and beside the bears
            for (int i = 0; i < search.Length; i++)
            {
                var s = search[i]; if (s == null) continue; if (s.enabled != live) s.enabled = live; if (!live) continue;
                var d = new Vector3(Mathf.Sin(t * 0.6f + i * 0.8f) * 0.55f, 1f, 0.35f + 0.25f * Mathf.Sin(t * 0.45f + i * 1.3f)).normalized; var c = Pc(i % 3, t);
                s.SetPosition(0, W(searchO[i])); s.SetPosition(1, W(searchO[i] + d * 160f)); s.widthMultiplier = (5f + 2f * pulse) * sc; s.startColor = s.endColor = new Color(c.r, c.g, c.b, 0.2f + 0.12f * pulse);
            }
            for (int i = 0; i < pillars.Length; i++)
            {
                var s = pillars[i]; if (s == null) continue; s.enabled = true; var c = Color.Lerp(Pc(i % 3, t), Color.white, flashV * 0.6f);
                s.SetPosition(0, W(pillO[i])); s.SetPosition(1, W(pillO[i] + Vector3.up * 130f)); s.widthMultiplier = (2.2f + 1.5f * pulse) * sc; s.startColor = s.endColor = new Color(c.r, c.g, c.b, (live ? 0.25f + 0.5f * pulse : 0.12f) + 0.3f * flashV);
            }
            // the equaliser wall along the stage front
            for (int k = 0; k < 3; k++) { var c = Pc(k, t); float b = 0.65f + 0.35f * pulse; barMat[k].color = new Color(c.r * b, c.g * b, c.b * b, 1f); }
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] == null) continue; float h = 0.4f + Mathf.Clamp01(en * 1.8f) * (3f + 9f * Mathf.Abs(Mathf.Sin(i * 0.55f + t * 4.5f + bi))) + 4f * pulse * Mathf.Abs(Mathf.Sin(i * 1.3f)); bars[i].localScale = new Vector3(1.5f, h, 0.9f);
            }
            Fireworks(t);
            if (paused || dt <= 0f) return;
            // ground fog (coloured by the lights) and orbs drifting up out of the crowd
            var fc = Pc(UnityEngine.Random.Range(0, 3), t);
            for (int k = TakeA(ref accFog, (6f + 24f * en) * Parts, dt); k > 0; k--) Emit(fog, new Vector3(stageC.x + Rnd(-1f, 1f) * stageHalfW, floorY + 0.3f, deckFront + Rnd(-2f, 3f)), new Vector3(Rnd(-0.6f, 0.6f), 0.15f, -Rnd(1.2f, 2.2f)), new Color(fc.r, fc.g, fc.b, Rnd(0.07f, 0.14f)), Rnd(7f, 12f), Rnd(7f, 10f));
            for (int k = TakeA(ref accOrb, (5f + 10f * en) * Parts, dt); k > 0; k--) Emit(orbs, new Vector3(stageC.x + Rnd(-1f, 1f) * (stageHalfW + 10f), floorY + 0.5f, Rnd(2f, Mathf.Max(4f, deckFront - 3f))), new Vector3(Rnd(-0.6f, 0.6f), Rnd(1.2f, 3f), Rnd(-0.6f, 0.6f)), Color.HSVToRGB(Rnd(0f, 1f), 0.6f, 1f), Rnd(0.5f, 1.1f), Rnd(7f, 10f));
        }
        void Fireworks(float t)
        {
            for (int i = fireworks.Count - 1; i >= 0; i--)
            {
                var f = fireworks[i]; float age = t - f.T0; if (age < 0f) continue; if (age > f.Rise + 3f) { fireworks.RemoveAt(i); continue; }
                if (age < f.Rise)
                {
                    float u = 1f - (1f - age / f.Rise) * (1f - age / f.Rise); var p = Vector3.Lerp(f.A, f.B, u);
                    if (!paused) for (int k = 0; k < 3; k++) Emit(fx, p, new Vector3(Rnd(-1.5f, 1.5f), Rnd(-4f, -1f), Rnd(-1.5f, 1.5f)), new Color(1f, 0.85f, 0.5f), 0.7f, Rnd(0.4f, 0.8f));
                }
                else if (!f.Burst && !paused)
                {
                    f.Burst = true; int n = (int)(160 * Parts);
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 d; if (f.Kind == 1) { float a = Rnd(0f, 6.2832f); d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), Rnd(-0.12f, 0.12f)); } else d = UnityEngine.Random.onUnitSphere;
                        Emit(fx, f.B, d * Rnd(12f, 22f), Color.Lerp(f.C, Color.white, Rnd(0f, 0.4f)), 1.1f * Rnd(0.6f, 1.4f), Rnd(1.6f, 2.6f));
                    }
                    lightKick = Mathf.Max(lightKick, 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ effects on the hits and the drops
        void Confetti(int n, float speed)
        {
            if (confetti == null) return; n = (int)(n * Parts);
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(stageC.x + Rnd(-1f, 1f) * (stageHalfW + 10f), Rnd(18f, 32f), Rnd(deckFront - 24f, deckFront + 6f)); var c = Color.HSVToRGB(Rnd(0f, 1f), 0.8f, 1f);
                Emit(confetti, p, new Vector3(Rnd(-3f, 3f), Rnd(-1f, 3f), Rnd(-4f, 2f)) * speed, c, Rnd(0.35f, 0.7f), Rnd(6f, 10f), Rnd(0f, 360f));
            }
        }
        void Flames(float power, int count)
        {
            if (flame == null) return;
            for (int s = 0; s < 8; s++)
            {
                if (count < 8 && UnityEngine.Random.value > count / 8f) continue; int n = (int)((18 + 26 * power) * Parts);
                for (int k = 0; k < n; k++) Emit(flame, flameSpots[s] + new Vector3(Rnd(-0.5f, 0.5f), 0f, Rnd(-0.5f, 0.5f)), new Vector3(Rnd(-2f, 2f), Rnd(10f, 18f + 14f * power), Rnd(-3f, 1f)), Color.white, Rnd(2.2f, 3.8f) * (0.7f + 0.5f * power), Rnd(0.8f, 1.5f));
            }
            if (power > 0.5f) for (int s = 0; s < 8; s++) for (int k = 0; k < (int)(10 * Parts); k++) Emit(fx, flameSpots[s], new Vector3(Rnd(-6f, 6f), Rnd(14f, 26f), Rnd(-8f, 3f)), new Color(1f, 0.9f, 0.55f), Rnd(0.3f, 0.6f), Rnd(1f, 1.8f));        // cold sparks
            lightKick = Mathf.Max(lightKick, 0.6f + power * 0.4f);
            var hp = Head(); if (hp.HasValue && power > 0.7f)                  // the jet nearest to the player throws them back
            {
                int best = 0; float bd = float.MaxValue; for (int s = 0; s < 8; s++) { float d = (W(flameSpots[s]) - hp.Value).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
                AstronomicalHazards.Blast(W(flameSpots[best]), (9f + 7f * power) * sc, 8f + 6f * power);
            }
        }
        static Vector3? Head() { try { var p = GTPlayer.Instance; if (p != null) return p.HeadCenterPosition; } catch { } return null; }
        void Splat(float strength, float t)
        {
            var s = splashes[splashNext]; splashNext = (splashNext + 1) % splashes.Length; var lp = new Vector3(stageC.x + Rnd(-0.9f, 0.9f) * (stageHalfW + 8f), 0f, Rnd(2f, deckFront - 3f));
            RaycastHit h; var w = W(lp + Vector3.up * 5f); Vector3 pos; if (Physics.Raycast(w, Vector3.down, out h, 30f, LocoMask(), QueryTriggerInteraction.Ignore)) pos = h.point + Vector3.up * 0.04f; else pos = W(lp);
            s.T.position = pos; s.T.rotation = Quaternion.Euler(90f, root.transform.eulerAngles.y + Rnd(0f, 360f), 0f); s.Size = Rnd(5f, 9f + 8f * strength) * sc; s.T0 = t; s.C = Color.Lerp(Pc(UnityEngine.Random.Range(0, 3), t), Color.HSVToRGB(Rnd(0f, 1f), 0.8f, 1f), 0.4f);
        }
        void Hit(float s, float t)
        {
            if (s > 0.22f) Flames(Mathf.Clamp01(s * 1.6f), 3 + (int)(s * 8f));
            if (s > 0.3f) Confetti((int)(120 + 300 * s), 1f);
            if (s > 0.18f) Splat(s, t);
            if (s > 0.25f) Wave(t); if (s > 0.55f) Firework(t, 0);
            flashV = Mathf.Max(flashV, Mathf.Clamp01(s * 1.4f)); lightKick = Mathf.Max(lightKick, 1f);
            if (s > 0.3f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(s), 0.25f); gt.StartVibration(false, Mathf.Clamp01(s), 0.25f); } } catch { } }
        }
        void Drop(float t)
        {
            Flames(1f, 8); Confetti(700, 1.4f); for (int k = 0; k < 4; k++) Splat(1f, t); flashV = 1f; screenFlash = 1f; lightKick = 1.5f; Wave(t); Wave(t + 0.3f); for (int k = 0; k < 6; k++) Firework(t + 0.25f * k, k & 1);
            AstronomicalHazards.Blast(W(new Vector3(stageC.x, deckTop, deckFront)), 45f * sc, 15f, 15f);          // the drop blows everyone near the stage back
        }

        public string DebugInfo()
        {
            int act = 0; foreach (var s in splashes) if (s != null && s.T.gameObject.activeSelf) act++;
            return "song " + curSong + "  beat " + lastBeat + "/" + beats.Length + "  hologram " + holoAmt.ToString("0.00") + "  scale " + sc.ToString("0.00") + "  layer " + floorLayer + "\nsplashes " + act + "  particles " + (fx != null ? fx.particleCount : 0) + "+" + (flame != null ? flame.particleCount : 0) + "+" + (confetti != null ? confetti.particleCount : 0);
        }

        // ------------------------------------------------------------------ cleanup: every model is parked again and its materials / meshes put back
        public void Cleanup()
        {
            if (ambientSaved) { RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z); ambientSaved = false; }
            try { foreach (var kv in restoreMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; foreach (var s in screens) if (s.MF != null) s.MF.sharedMesh = s.Orig; if (ledMat != null) { ledMat.color = Color.white; ledMat.mainTexture = null; } } catch { }
            try
            {
                foreach (var m in models) if (m != null && m.Pivot != null) { m.Pivot.transform.SetParent(null, false); m.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(m.Pivot); }
                foreach (var kv in nodes) if (kv.Value != null && kv.Value.name.StartsWith("Text", StringComparison.Ordinal)) kv.Value.gameObject.SetActive(true);
            }
            catch { }
            restoreMats.Clear(); screens.Clear(); models.Clear(); nodes.Clear(); ledMat = null; stageT = null; dj = giant = bearL = bearR = null;
            if (dome != null) UnityEngine.Object.Destroy(dome); dome = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o);
            foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o);
            owned.Clear(); kit = null; built = false; lastT = -1f; curSong = -1; skyMesh = null; fx = flame = confetti = stars = null; Array.Clear(splashes, 0, splashes.Length); Array.Clear(lasers, 0, lasers.Length); Array.Clear(stripMat, 0, stripMat.Length); Array.Clear(lights, 0, lights.Length); fireworks.Clear(); Array.Clear(waves, 0, waves.Length); Array.Clear(halos, 0, halos.Length); Array.Clear(search, 0, search.Length); Array.Clear(pillars, 0, pillars.Length); Array.Clear(bars, 0, bars.Length); Array.Clear(barMat, 0, barMat.Length); fog = orbs = null; waveT = new[] { -99f, -99f, -99f, -99f };
        }
    }
}
