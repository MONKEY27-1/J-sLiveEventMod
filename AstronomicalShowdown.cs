using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // What an event scene has to do for the manager.
    internal interface IDebug
    {
        string DebugInfo();
    }

    internal interface IEventScene
    {
        void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yaw);
        void SetPaused(bool p);
        void Cleanup();
        void Apply(float t, int i, float u, AstronomicalAudioManager au);
    }

    // "The Final Showdown": the monster and the robot are baked clips (Blender-checked, tools/show_*.py) played against the soundtrack; this class adds everything
    // around them: terrain they stand on, night sky and light, footsteps, the beam, missiles, explosions, the energy orb, the sword, the torn-off arm, jets.
    // The whole show lives in one root (origin = the Forest's gazebo if found, else the stage spot) that can be scaled / raised in the config. Local only.
    internal sealed class AstronomicalShowdown : IEventScene
    {
        sealed class Actor
        {
            public string Id;
            public GltfModel Model;
            public GltfModel.Clip Clip;
            public float Height,
                Ankle;
            public Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public float BaseScale = 1f;
            public bool Visible = true;
            public float[] LiftT = { -9f, -9f };
            public Light Eye,
                Glow;
        }

        sealed class Missile
        {
            public Transform T;
            public Vector3 Start;
            public Transform Target;
            public Vector3 Off;
            public float T0,
                Fly;
            public bool Live;
            public TrailRenderer Trail;
            public Light L;
        }

        sealed class Cue
        {
            public float T;
            public string Type;
            public JNode N;
        }

        sealed class Win
        {
            public float T0,
                T1;
            public string Type;
            public JNode N;
        }

        GameObject root,
            plateau,
            dome,
            sky;
        Transform plateauT;
        readonly List<Actor> actors = new List<Actor>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Cue> events = new List<Cue>();
        readonly List<Win> wins = new List<Win>();
        Material matSprite,
            matGlow;
        Texture2D glowTex;
        ParticleSystem fx;
        Light boomLight;
        float boomI;
        Transform[] boomSphere;
        Material[] boomMat;
        float[] boomAge,
            boomSize;
        int boomNext;
        Missile[] missiles = new Missile[14];
        List<Missile> pending = new List<Missile>();
        LineRenderer beamCore,
            beamGlow;
        Light beamLight;
        GltfModel sword;
        Light swordLight;
        Transform armProp;
        float armT0 = -1f;
        Transform orb;
        Material orbMat;
        Light orbLight;
        GltfModel zpModel,
            vaultModel;
        float zpHalf;
        Material zpShell2Mat,
            zpGlowMat;
        float lastT = -1f;
        int cueIdx;
        bool paused,
            eyesOn,
            eyeOut,
            hidRobot;
        string beamCol = "green";
        float scale = 1f;
        Vector3 ambientSave;
        bool ambientSaved;
        Color fogSave;
        bool fogSaved;
        public static string CurrentMove = "";
        AstronomicalEventDef def;

        // ------------------------------------------------------------------ helpers
        Material Sprite(Color c, Texture tex = null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            owned.Add(m);
            return m;
        }

        static Color Col(string n, float a = 1f)
        {
            switch (n)
            {
                case "green":
                    return new Color(0.35f, 1f, 0.35f, a);
                case "cyan":
                    return new Color(0.4f, 0.9f, 1f, a);
                case "orange":
                    return new Color(1f, 0.55f, 0.15f, a);
                case "white":
                    return new Color(1f, 1f, 1f, a);
                default:
                    return new Color(1f, 0.7f, 0.3f, a);
            }
        }

        Light AddLight(Transform parent, Color c, float range, float intensity)
        {
            var g = new GameObject("ShowLight");
            g.transform.SetParent(parent, false);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static Transform FindBone(GltfModel m, string baseName)
        {
            Transform best = null;
            int bl = 9999;
            foreach (var j in m.Joints)
            {
                if (j == null)
                    continue;
                string n = j.name;
                if (!n.StartsWith(baseName, StringComparison.Ordinal))
                    continue;
                bool ok = true;
                for (int k = baseName.Length; k < n.Length; k++)
                {
                    char c = n[k];
                    if (c != '_' && !char.IsDigit(c))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok && n.Length < bl)
                {
                    best = j;
                    bl = n.Length;
                }
            }
            return best;
        }

        Actor ActorOf(string id)
        {
            foreach (var a in actors)
                if (a.Id == id)
                    return a;
            return null;
        }

        Transform Bone(Actor a, string name)
        {
            if (a == null)
                return null;
            Transform t;
            if (!a.Bones.TryGetValue(name, out t))
            {
                t = FindBone(a.Model, name);
                a.Bones[name] = t;
            }
            return t;
        }

        // "monster:spine_03" -> that bone's world position; [x,y,z] -> a point in the plateau frame
        bool Resolve(JNode n, out Vector3 p)
        {
            p = Vector3.zero;
            if (n == null)
                return false;
            string s = (string)n;
            if (s != null)
            {
                int c = s.IndexOf(':');
                if (c < 0)
                    return false;
                var a = ActorOf(s.Substring(0, c));
                var b = Bone(a, s.Substring(c + 1));
                if (b == null)
                    return false;
                p = b.position;
                return true;
            }
            if (n.Count >= 3)
            {
                p = plateauT.TransformPoint(new Vector3((float)n[0], (float)n[1], (float)n[2]));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ build
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yaw)
        {
            def = AstronomicalEvents.Selected;
            scale = Mathf.Max(0.2f, AstronomicalSettings.ShowScale.Value);
            Vector3 origin = anchorPos;
            try
            {
                origin = AstronomicalCountdown.FindGazeboBase(anchorPos);
            }
            catch { }
            try
            {
                FitArena(ref origin, ref yaw, ref scale, def.Cues != null ? def.Cues["arena"] : null);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Arena fit failed: " + e.Message);
            }
            root = new GameObject("FinalShowdown");
            root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * scale;
            plateau = new GameObject("Plateau");
            plateauT = plateau.transform;
            plateauT.SetParent(root.transform, false);
            float plat = AstronomicalSettings.ShowPlateau.Value;
            plateauT.localPosition = new Vector3(0, plat, 0);
            glowTex = MakeGlow();
            owned.Add(glowTex);
            matSprite = Sprite(Color.white);
            matGlow = Sprite(Color.white, glowTex);
            BuildSky();
            BuildFx();
            BuildBeam();
            BuildPools();
            // atmosphere: night, moonlight
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b);
            ambientSaved = true;
            RenderSettings.ambientLight = new Color(0.22f, 0.25f, 0.4f);
            var moon = new GameObject("ShowMoon");
            moon.transform.SetParent(root.transform, false);
            moon.transform.rotation = Quaternion.Euler(38f, 160f + yaw, 0);
            var ml = moon.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.color = new Color(0.75f, 0.85f, 1f);
            ml.intensity = 1.15f;
            ml.shadows = LightShadows.None;
            // actors
            for (int k = 0; k < def.ActorModels.Count; k++)
            {
                var m = def.ActorModels[k];
                if (m == null || m.Pivot == null)
                    continue;
                var a = new Actor
                {
                    Id = def.ActorIds[k],
                    Model = m,
                    Height = def.ActorHeights[k],
                    Ankle = def.ActorAnkle[k],
                };
                m.Pivot.transform.SetParent(plateauT, false);
                m.Pivot.transform.localPosition = Vector3.zero;
                m.Pivot.transform.localRotation = Quaternion.identity;
                float src = m.BoneHeight > 0.01f ? m.BoneHeight : 1.8f;
                a.BaseScale = a.Height / src;
                m.Pivot.transform.localScale = Vector3.one * a.BaseScale;
                m.Pivot.SetActive(true);
                foreach (var smr in m.Pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.allowOcclusionWhenDynamic = false;
                    smr.enabled = true;
                } // the maps' baked occlusion data culled body parts that overlap walls / terrain; bounds follow the animated body
                a.Clip = m.Clips.Count > 0 ? m.Clips[0] : null;
                if (a.Clip != null)
                    m.Sample(a.Clip, 0f);
                actors.Add(a);
            }
            var mon = ActorOf("monster");
            var rob = ActorOf("robot");
            if (mon != null)
            {
                var e = Bone(mon, "eye");
                if (e != null)
                {
                    mon.Eye = AddLight(e, new Color(1f, 0.8f, 0.3f), 90f, 4f);
                }
            }
            if (rob != null)
            {
                var h = Bone(rob, "head_offset");
                if (h != null)
                {
                    rob.Eye = AddLight(h, new Color(0.4f, 0.95f, 1f), 120f, 0f);
                    rob.Eye.transform.localPosition = new Vector3(0, 0, 0.15f);
                }
                var c = Bone(rob, "spine_03");
                if (c != null)
                    rob.Glow = AddLight(c, new Color(1f, 0.6f, 0.2f), 150f, 0f);
            }
            // props
            sword = def.Sword;
            if (sword != null && sword.Pivot != null)
            {
                sword.Pivot.transform.SetParent(plateauT, false);
                sword.Pivot.SetActive(false);
                swordLight = AddLight(plateauT, new Color(0.4f, 0.9f, 1f), 70f, 0f);
            }
            zpModel = def.Prop("zeropoint");
            if (zpModel != null && zpModel.Pivot != null)
            {
                zpModel.Pivot.transform.SetParent(plateauT, false);
                zpModel.Pivot.SetActive(false);
                zpHalf = zpModel.RendererBounds.size.y * 0.5f;
                foreach (var r in zpModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.sharedMaterial != null)
                        r.sharedMaterial.renderQueue =
                            r.name == "Glow" ? 2999
                            : r.name == "Shell" ? 3001
                            : r.name == "Shell2" ? 3002
                            : r.name == "Burst" ? 3003
                            : r.sharedMaterial.renderQueue;
                }
            }
            if (zpModel != null)
                foreach (var r in zpModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "Shell2")
                        zpShell2Mat = r.sharedMaterial;
                    else if (r.name == "Glow")
                        zpGlowMat = r.sharedMaterial;
                }
            vaultModel = def.Prop("vault");
            if (vaultModel != null && vaultModel.Pivot != null)
            {
                vaultModel.Pivot.transform.SetParent(plateauT, false);
                vaultModel.Pivot.SetActive(false);
                foreach (var r in vaultModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.name == "ShaftGlow")
                        r.sharedMaterial.renderQueue = 3000;
                }
            }
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.name = "EnergyOrb";
            orb = o.transform;
            orb.SetParent(plateauT, false);
            orbMat = Sprite(new Color(0.5f, 1f, 1f, 0.7f), glowTex);
            o.GetComponent<Renderer>().sharedMaterial = orbMat;
            o.SetActive(false);
            orbLight = AddLight(orb, new Color(0.5f, 1f, 1f), 160f, 0f);
            // cues
            ParseCues(def.Cues);
            lastT = -1f;
            cueIdx = 0;
            eyesOn = false;
            eyeOut = false;
            hidRobot = false;
            armT0 = -1f;
            AstronomicalEventMod.Log.LogInfo(
                "Final Showdown built: origin "
                    + origin
                    + ", scale "
                    + scale
                    + ", plateau "
                    + plat
                    + " m, actors "
                    + actors.Count
                    + ", cues "
                    + events.Count
                    + "/"
                    + wins.Count
            );
        }

        // The show is authored in a ~250 m arena; the Forest is much smaller. Sample where there is walkable floor around the gazebo, then pick the largest scale (and the turn / shift of the
        // arena) for which every point the giants walk through, plus a margin for their bodies, stands on floor. cues.json "arena": {pts: [[x, z]...], margin}.
        internal static bool lastFit;
        internal static Vector3 fitOrigin;
        internal static float fitYaw,
            fitScale = 1f;

        // A spot at the gazebo floor facing the middle of the arena (used by the menu button "Event Viewpoint").
        public bool TryViewpoint(out Vector3 pos, out float yaw)
        {
            pos = Vector3.zero;
            yaw = 0f;
            Vector3 o;
            float y,
                sc;
            if (root != null)
            {
                o = root.transform.position;
                y = root.transform.eulerAngles.y;
                sc = scale;
            }
            else if (lastFit)
            {
                o = fitOrigin;
                y = fitYaw;
                sc = fitScale;
            }
            else
                return false;
            Vector3 centre = o + Quaternion.Euler(0, y, 0) * new Vector3(-20f * sc, 0f, 130f * sc);
            Vector3 d = centre - o;
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pos = o + Vector3.up * 1.2f;
            return true;
        }

        internal static void FitArena(ref Vector3 origin, ref float yaw, ref float scale, JNode arena)
        {
            if (arena == null || arena["pts"] == null)
                return;
            Vector3 org = origin;
            const float step = 10f;
            const int n = 44;
            float half = n * step / 2f;
            var ok = new bool[n * n];
            int cnt = 0;
            for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
            {
                var o = new Vector3(org.x - half + i * step, org.y + 60f, org.z - half + k * step);
                RaycastHit h;
                if (Physics.Raycast(o, Vector3.down, out h, 160f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.point.y - org.y) < 80f)
                {
                    ok[i * n + k] = true;
                    cnt++;
                }
            }
            Func<float, float, bool> floor = (wx, wz) =>
            {
                int i = Mathf.FloorToInt((wx - (org.x - half)) / step),
                    k = Mathf.FloorToInt((wz - (org.z - half)) / step);
                return i >= 0 && k >= 0 && i < n && k < n && ok[i * n + k];
            };
            var pts = new List<Vector2>();
            for (int q = 0; q < arena["pts"].Count; q++)
                pts.Add(new Vector2((float)arena["pts"][q][0], (float)arena["pts"][q][1]));
            float margin = (arena["margin"] != null ? (float)arena["margin"] : 40f) * 0.8f;
            float baseScale = scale;
            float[] scales = { 1f, 0.85f, 0.7f, 0.6f, 0.5f, 0.42f, 0.35f, 0.3f, 0.25f, 0.2f, 0.17f, 0.14f };
            if (cnt < 40)
            {
                AstronomicalEventMod.Log.LogInfo("Arena fit: too little floor found (" + cnt + " cells), using scale " + baseScale * 0.6f);
                scale = baseScale * 0.6f;
                return;
            }
            float bestScore = float.MinValue;
            float bs = scales[scales.Length - 1],
                by = yaw;
            Vector2 bo = Vector2.zero;
            bool found = false;
            foreach (float fs in scales)
            {
                float sc = baseScale * fs;
                for (int yo = 0; yo < 360; yo += 15)
                for (int ox = -90; ox <= 90; ox += 30)
                for (int oz = -90; oz <= 90; oz += 30)
                {
                    float a = (yaw + yo) * Mathf.Deg2Rad,
                        ca = Mathf.Cos(a),
                        sa = Mathf.Sin(a);
                    int bad = 0,
                        tot = pts.Count * 5;
                    for (int q = 0; q < pts.Count; q++)
                    for (int d = 0; d < 5; d++)
                    {
                        float lx =
                                pts[q].x
                                + (
                                    d == 1 ? margin
                                    : d == 2 ? -margin
                                    : 0f
                                ),
                            lz =
                                pts[q].y
                                + (
                                    d == 3 ? margin
                                    : d == 4 ? -margin
                                    : 0f
                                );
                        float wx = org.x + ox + (lx * ca + lz * sa) * sc,
                            wz = org.z + oz + (-lx * sa + lz * ca) * sc; // unity: heading rotates x toward z
                        if (!floor(wx, wz))
                            bad++;
                    }
                    if (bad > tot * 0.03f)
                        continue; // 97% of the footprint (+ body margin) on floor
                    float score = -bad * 0.5f - Mathf.Abs(yo > 180 ? 360 - yo : yo) * 0.05f - Mathf.Sqrt(ox * ox + oz * oz) * 0.1f;
                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bs = sc;
                        by = yaw + yo;
                        bo = new Vector2(ox, oz);
                    }
                }
                if (found)
                    break;
            }
            AstronomicalEventMod.Log.LogInfo(
                "Arena fit: floor cells " + cnt + "/" + (n * n) + ", found " + found + ", scale " + bs + ", yaw " + by + ", shift " + bo
            );
            scale = bs;
            yaw = by;
            origin += new Vector3(bo.x, 0f, bo.y);
            lastFit = true;
            fitOrigin = origin;
            fitYaw = yaw;
            fitScale = scale;
        }

        // the Forest floor is not level: keep each giant standing on the real ground under its pelvis (smoothed)
        void GroundFollow(Actor a, float dt)
        {
            try
            {
                var pel = Bone(a, "pelvis");
                if (pel == null || a.Model.Pivot == null)
                    return;
                RaycastHit h;
                var o = new Vector3(pel.position.x, plateauT.position.y + 150f * scale, pel.position.z);
                if (!Physics.Raycast(o, Vector3.down, out h, 400f * scale, ~0, QueryTriggerInteraction.Ignore))
                    return;
                float dy = h.point.y - plateauT.position.y;
                if (Mathf.Abs(dy) > 60f)
                    return;
                var lp = a.Model.Pivot.transform.localPosition;
                float target = dy / Mathf.Max(0.01f, scale);
                lp.y = Mathf.Lerp(lp.y, target, 1f - Mathf.Exp(-3f * dt));
                a.Model.Pivot.transform.localPosition = lp;
            }
            catch { }
        }

        // ------------------------------------------------------------------ player reactions: controller rumble + a procedural thump / roar, both scaled by distance to the event
        internal static AudioClip boomClip,
            roarClip,
            thumpClip;
        AudioSource sfx;
        float reactCool;

        static AudioClip MakeClip(string name, float len, Func<float, float, float> f)
        {
            int n = (int)(44100 * len);
            var d = new float[n];
            for (int i = 0; i < n; i++)
                d[i] = Mathf.Clamp(f(i / 44100f, len), -1f, 1f);
            int pos = 0;
            var c = AudioClip.Create(
                name,
                n,
                1,
                44100,
                false,
                buf =>
                {
                    int m = Math.Min(buf.Length, d.Length - pos);
                    if (m > 0)
                        Array.Copy(d, pos, buf, 0, m);
                    pos += buf.Length;
                }
            );
            return c;
        }

        internal static void MakeSfx()
        {
            var rnd = new System.Random(3);
            float lp = 0f,
                ph = 0f,
                ph2 = 0f,
                ph3 = 0f,
                lp2 = 0f;
            boomClip = MakeClip(
                "ShowBoom",
                1.8f,
                (t, L) =>
                {
                    float fr = 95f * Mathf.Exp(-2.2f * t) + 22f;
                    ph += fr / 44100f;
                    float n = (float)(rnd.NextDouble() * 2 - 1);
                    lp += (n - lp) * 0.08f;
                    return (float)Math.Tanh(1.6 * (Mathf.Sin(ph * 6.2832f) * Mathf.Exp(-2.4f * t) + lp * 0.9f * Mathf.Exp(-7f * t)))
                        * Mathf.Min(1f, t * 400f)
                        * 0.9f;
                }
            );
            thumpClip = MakeClip(
                "ShowThump",
                0.7f,
                (t, L) =>
                {
                    float fr = 60f * Mathf.Exp(-6f * t) + 30f;
                    ph2 += fr / 44100f;
                    return Mathf.Sin(ph2 * 6.2832f) * Mathf.Exp(-6.5f * t) * Mathf.Min(1f, t * 300f) * 0.9f;
                }
            );
            roarClip = MakeClip(
                "ShowRoar",
                2.4f,
                (t, L) =>
                {
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / L)), 0.6f);
                    float fr = (78f - 26f * (t / L)) * (1f + 0.04f * Mathf.Sin(t * 38f));
                    ph3 += fr / 44100f;
                    float saw = (ph3 % 1f) * 2f - 1f;
                    float n = (float)(rnd.NextDouble() * 2 - 1);
                    lp2 += (n - lp2) * 0.22f;
                    return (float)Math.Tanh(2.2 * (saw * 0.55f + lp2 * 0.9f)) * env * 0.85f;
                }
            );
        }

        // kind: 0 explosion / impact, 1 roar, 2 footstep.  size in metres (loudness).
        void React(Vector3 at, float size, int kind)
        {
            try
            {
                if (kind == 2 && Time.unscaledTime < reactCool)
                    return;
                var cam = Camera.main;
                if (cam == null || root == null)
                    return;
                float d = Vector3.Distance(cam.transform.position, at);
                float reach = Mathf.Max(60f, size * 7f * scale);
                float k = Mathf.Clamp01(1f - d / reach);
                if (k < 0.03f)
                    return;
                float amp = Mathf.Clamp01(k * (kind == 2 ? 0.7f : 1.1f) * Mathf.Clamp(size / 40f, 0.25f, 1.2f));
                if (kind == 2)
                    reactCool = Time.unscaledTime + 0.35f;
                try
                {
                    var gt = GorillaTagger.Instance;
                    if (gt != null)
                    {
                        float dur =
                            kind == 1 ? 0.7f
                            : kind == 2 ? 0.18f
                            : 0.35f;
                        gt.StartVibration(true, amp, dur);
                        gt.StartVibration(false, amp, dur);
                    }
                }
                catch { }
                if (boomClip == null)
                    MakeSfx();
                if (sfx == null)
                {
                    var g = new GameObject("ShowdownSfx");
                    g.transform.SetParent(root.transform, false);
                    sfx = g.AddComponent<AudioSource>();
                    sfx.spatialBlend = 0f;
                    sfx.playOnAwake = false;
                    sfx.priority = 0;
                }
                sfx.PlayOneShot(
                    kind == 1 ? roarClip
                        : kind == 2 ? thumpClip
                        : boomClip,
                    Mathf.Clamp01(amp * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f))
                );
            }
            catch { }
        }

        // the monster dies: green energy pours out of the eye, its body crumbles to dust and shrinks away
        void Decay(Win w, float t)
        {
            var a = ActorOf((string)w.N["actor"]);
            if (a == null)
                return;
            float f = Mathf.InverseLerp(w.T0, w.T1, t);
            var eye = Bone(a, "eye");
            if (eye != null && UnityEngine.Random.value < 0.9f)
                Emit(eye.position, 4, new Color(0.45f, 1f, 0.5f, 0.9f), 12f * scale, 14f, 3.2f * scale, 2.2f);
            string[] bs = { "pelvis", "spine_03", "head", "hand_l", "hand_r", "foot_l", "foot_r" };
            var b = Bone(a, bs[UnityEngine.Random.Range(0, bs.Length)]);
            if (b != null)
                Emit(b.position, (int)(2 + 10 * f), new Color(0.40f, 0.36f, 0.32f, 0.55f), 8f * scale, 3f, 7f * scale, 2.8f);
            float g = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, f));
            a.Model.Pivot.transform.localScale = Vector3.one * a.BaseScale * (1f - g);
        }

        // a glowing light trail behind a flying actor (the robot leaving for the Ice Moon)
        void Trail(Win w, float t)
        {
            var a = ActorOf((string)w.N["actor"]);
            if (a == null || !a.Visible)
                return;
            float f = Mathf.InverseLerp(w.T0, w.T1, t);
            foreach (var bn in new[] { "pelvis", "foot_l", "foot_r", "spine_03" })
            {
                var b = Bone(a, bn);
                if (b != null)
                    Emit(
                        b.position,
                        5,
                        Color.Lerp(new Color(0.6f, 1f, 1f, 0.9f), new Color(1f, 0.8f, 0.4f, 0.8f), f),
                        2f * scale,
                        0f,
                        (3.5f + 6f * f) * scale,
                        4.5f
                    );
            }
        }

        void ParseCues(JNode c)
        {
            events.Clear();
            wins.Clear();
            if (c == null)
                return;
            var ev = c["events"];
            if (ev != null)
                for (int k = 0; k < ev.Count; k++)
                {
                    var n = ev[k];
                    events.Add(
                        new Cue
                        {
                            T = (float)n["t"],
                            Type = (string)n["type"],
                            N = n,
                        }
                    );
                }
            var wi = c["windows"];
            if (wi != null)
                for (int k = 0; k < wi.Count; k++)
                {
                    var n = wi[k];
                    wins.Add(
                        new Win
                        {
                            T0 = (float)n["t0"],
                            T1 = (float)n["t1"],
                            Type = (string)n["type"],
                            N = n,
                        }
                    );
                }
            events.Sort((a, b) => a.T.CompareTo(b.T));
        }

        Texture2D MakeGlow()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                float a = Mathf.Clamp01(1f - d);
                px[y * 64 + x] = new Color(1, 1, 1, a * a);
            }
            t.SetPixels(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        void BuildSky()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int lat = 14,
                lon = 28;
            var rnd = new System.Random(5);
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = i * Mathf.PI * 0.55f / lat,
                    ph = j * Mathf.PI * 2f / lon;
                float s = Mathf.Sin(th);
                verts.Add(new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)) * 420f);
                float u = (float)i / lat;
                cols.Add(Color.Lerp(new Color(0.015f, 0.025f, 0.09f, 1), new Color(0.16f, 0.1f, 0.3f, 1), Mathf.Pow(u, 1.8f)));
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j,
                    b = a + 1,
                    c = a + lon + 1,
                    d = c + 1;
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
                tris.Add(b);
                tris.Add(d);
                tris.Add(c);
            }
            for (int k = 0; k < 260; k++) // stars
            {
                float th = (float)rnd.NextDouble() * Mathf.PI * 0.5f,
                    ph = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var c = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 415f;
                var r1 = Vector3.Cross(c, Vector3.up).normalized * 0.9f;
                var r2 = Vector3.Cross(c, r1).normalized * 0.9f;
                int b0 = verts.Count;
                float br = 0.6f + (float)rnd.NextDouble() * 0.4f;
                verts.Add(c + r1);
                verts.Add(c - r1 + r2);
                verts.Add(c - r1 - r2);
                for (int q = 0; q < 3; q++)
                    cols.Add(new Color(br, br, br, 1));
                tris.Add(b0);
                tris.Add(b0 + 1);
                tris.Add(b0 + 2);
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            dome = new GameObject("ShowSky");
            dome.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(matSprite);
            dm.renderQueue = 1000;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false; // drawn first, so it can never paint over a giant mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildFx()
        {
            var g = new GameObject("ShowFx");
            g.transform.SetParent(root.transform, false);
            fx = g.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = fx.main;
            m.maxParticles = 6000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.playOnAwake = false;
            m.startLifetime = 2f;
            m.gravityModifier = 0.15f;
            var e = fx.emission;
            e.enabled = false;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;
            fx.Play();
            boomLight = AddLight(root.transform, Color.white, 300f, 0f);
        }

        void Emit(Vector3 p, int n, Color c, float speed, float up, float size, float life = 2f)
        {
            if (fx == null)
                return;
            var ep = new ParticleSystem.EmitParams();
            for (int k = 0; k < n; k++)
            {
                ep.position = p + UnityEngine.Random.insideUnitSphere * size * 0.4f;
                ep.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.3f, 1f) * speed + Vector3.up * up;
                ep.startColor = c;
                ep.startSize = size * UnityEngine.Random.Range(0.6f, 1.5f);
                ep.startLifetime = life * UnityEngine.Random.Range(0.6f, 1.3f);
                fx.Emit(ep, 1);
            }
        }

        void BuildBeam()
        {
            beamCore = NewLine("BeamCore", Color.white);
            beamGlow = NewLine("BeamGlow", new Color(0.3f, 1f, 0.35f, 0.55f));
            beamLight = AddLight(root.transform, new Color(0.4f, 1f, 0.4f), 150f, 0f);
        }

        LineRenderer NewLine(string n, Color c)
        {
            var g = new GameObject(n);
            g.transform.SetParent(root.transform, false);
            var l = g.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.positionCount = 2;
            l.sharedMaterial = Sprite(c, glowTex);
            l.startColor = c;
            l.endColor = c;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.enabled = false;
            l.numCapVertices = 6;
            return l;
        }

        void BuildPools()
        {
            boomSphere = new Transform[10];
            boomMat = new Material[10];
            boomAge = new float[10];
            boomSize = new float[10];
            for (int k = 0; k < 10; k++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(root.transform, false);
                boomMat[k] = Sprite(Color.white, glowTex);
                s.GetComponent<Renderer>().sharedMaterial = boomMat[k];
                s.SetActive(false);
                boomSphere[k] = s.transform;
                boomAge[k] = 9f;
            }
            for (int k = 0; k < missiles.Length; k++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(root.transform, false);
                s.transform.localScale = new Vector3(1.4f, 3.4f, 1.4f);
                s.GetComponent<Renderer>().sharedMaterial = Sprite(new Color(1f, 0.85f, 0.5f, 1f));
                var tr = s.AddComponent<TrailRenderer>();
                tr.time = 1.1f;
                tr.widthMultiplier = 2.6f;
                tr.sharedMaterial = Sprite(new Color(1f, 0.6f, 0.2f, 0.8f), glowTex);
                tr.startColor = new Color(1f, 0.8f, 0.4f, 0.9f);
                tr.endColor = new Color(1f, 0.3f, 0.1f, 0f);
                tr.emitting = false;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                var m = new Missile
                {
                    T = s.transform,
                    Trail = tr,
                    L = AddLight(s.transform, new Color(1f, 0.6f, 0.2f), 60f, 0f),
                };
                s.SetActive(false);
                missiles[k] = m;
            }
        }

        // ------------------------------------------------------------------ effects
        void Boom(Vector3 p, float size, Color c, bool knock = true)
        {
            int k = boomNext;
            boomNext = (boomNext + 1) % boomSphere.Length;
            boomAge[k] = 0;
            boomSize[k] = size;
            boomSphere[k].position = p;
            boomSphere[k].gameObject.SetActive(true);
            boomMat[k].color = c;
            Emit(
                p,
                (int)(Mathf.Clamp(size * 5f, 20, 260) * Mathf.Max(0.3f, AstronomicalSettings.Particles.Value)),
                c,
                size * 0.9f,
                size * 0.5f,
                size * 0.22f,
                2.4f
            );
            Emit(p, (int)(size * 2f), new Color(0.35f, 0.3f, 0.28f, 0.7f), size * 0.5f, size * 0.3f, size * 0.5f, 3.5f);
            boomI = Mathf.Max(boomI, 6f + size * 0.25f);
            boomLight.transform.position = p + Vector3.up * size * 0.3f;
            boomLight.color = c;
            if (knock)
                AstronomicalHazards.Blast(p, size * 2.6f * scale, 4f + size * 0.45f);
        }

        void Dust(Vector3 p, float size)
        {
            Emit(
                p,
                (int)(size * 16 * Mathf.Max(0.4f, AstronomicalSettings.Particles.Value)),
                new Color(0.42f, 0.38f, 0.33f, 0.55f),
                size * 3.2f,
                size * 0.6f,
                size * 1.4f,
                4f
            );
        }

        void Flash(Vector3 p, float size, Color c)
        {
            boomI = Mathf.Max(boomI, 10f + size * 0.2f);
            boomLight.transform.position = p;
            boomLight.color = c;
            Emit(p, 40, c, size * 0.5f, 0, size * 0.25f, 1.2f);
        }

        // ------------------------------------------------------------------ per frame
        public void SetPaused(bool p)
        {
            paused = p;
            if (fx != null)
            {
                if (p)
                    fx.Pause(true);
                else
                    fx.Play(true);
            }
        }

        public void Apply(float t, int i, float u, AstronomicalAudioManager au)
        {
            if (root == null)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            bool back = t < lastT - 0.4f;
            bool jump = lastT < 0f || t - lastT > 1.0f;
            if (back || lastT < 0f)
            {
                cueIdx = 0;
                foreach (var m in missiles)
                {
                    m.Live = false;
                    m.T.gameObject.SetActive(false);
                    m.Trail.emitting = false;
                }
                pending.Clear();
                eyeOut = false;
                eyesOn = false;
                armT0 = -1f;
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null && dome != null)
                dome.transform.position = cam.transform.position;
            // the clips
            foreach (var a in actors)
                if (a.Clip != null)
                    a.Model.Sample(a.Clip, Mathf.Min(t, a.Clip.Duration - 0.01f));
            foreach (var a in actors)
                GroundFollow(a, dt);
            var rob = ActorOf("robot");
            var mon = ActorOf("monster");
            if (rob != null)
            {
                bool vis = t >= 80f && t < 296.2;
                if (vis != rob.Model.Pivot.activeSelf)
                    rob.Model.Pivot.SetActive(vis);
                rob.Visible = vis;
            }
            // events due now (stale ones are skipped after a seek)
            while (cueIdx < events.Count && events[cueIdx].T <= t)
            {
                var c = events[cueIdx++];
                if (t - c.T < 1.5f && !paused)
                {
                    try
                    {
                        RunEvent(c, t);
                    }
                    catch (Exception e)
                    {
                        AstronomicalEventMod.Log.LogWarning("Cue " + c.Type + ": " + e.Message);
                    }
                }
            }
            // continuous windows
            bool beamOn = false,
                orbOn = false,
                swordPlanted = false,
                swordHeld = false;
            foreach (var w in wins)
            {
                if (t < w.T0 || t > w.T1)
                    continue;
                try
                {
                    switch (w.Type)
                    {
                        case "beam":
                            if (Beam(w, t, dt))
                                beamOn = true;
                            break;
                        case "orb":
                            orbOn = true;
                            Orb(w, t);
                            break;
                        case "absorb":
                            Absorb(w, t, dt);
                            break;
                        case "jets":
                            Jets(w, t);
                            break;
                        case "aura":
                            Aura(w, t);
                            break;
                        case "smoke":
                            Smoke(w);
                            break;
                        case "decay":
                            Decay(w, t);
                            break;
                        case "trail":
                            Trail(w, t);
                            break;
                        case "sword":
                            if ((string)w.N["mode"] == "planted")
                            {
                                swordPlanted = true;
                                SwordPlanted(w, t);
                            }
                            else
                            {
                                swordHeld = true;
                                SwordHeld(w);
                            }
                            break;
                    }
                }
                catch (Exception e)
                {
                    AstronomicalEventMod.Log.LogWarning("Window " + w.Type + ": " + e.Message);
                }
            }
            if (!beamOn)
            {
                beamCore.enabled = false;
                beamGlow.enabled = false;
                beamLight.intensity = 0;
            }
            if (!orbOn && orb != null)
            {
                orb.gameObject.SetActive(false);
                orbLight.intensity = 0;
                if (zpModel != null && zpModel.Pivot != null && zpModel.Pivot.activeSelf)
                    zpModel.Pivot.SetActive(false);
                if (vaultModel != null && vaultModel.Pivot != null && vaultModel.Pivot.activeSelf)
                    vaultModel.Pivot.SetActive(false);
            }
            if (sword != null && sword.Pivot != null)
            {
                bool on = swordPlanted || swordHeld;
                if (sword.Pivot.activeSelf != on)
                    sword.Pivot.SetActive(on);
                if (swordLight != null && !on)
                    swordLight.intensity = 0;
            }
            if (rob != null && rob.Glow != null && t < C_FLASH)
                rob.Glow.intensity = 0;
            if (rob != null && rob.Eye != null)
                rob.Eye.intensity = t >= 90.2f ? (6f + 2f * Mathf.Sin(t * 5f)) * Mathf.Clamp01((296f - t) / 8f) : 0f;
            if (mon != null && mon.Eye != null)
                mon.Eye.intensity = eyeOut ? 0f : 4f + 1.5f * Mathf.Sin(t * 3f);
            // footsteps and the torn-off arm
            if (!paused)
            {
                foreach (var a in actors)
                    Steps(a, t);
                ArmProp(t, dt);
            }
            UpdateBooms(dt);
            UpdateMissiles(t, dt);
            boomI = Mathf.Max(0f, boomI - dt * 14f);
            boomLight.intensity = boomI * AstronomicalSettings.Lighting.Value;
        }

        const float C_FLASH = 205.3f;

        void RunEvent(Cue c, float t)
        {
            var n = c.N;
            float size = n["size"] != null ? (float)n["size"] : 10f;
            Vector3 p;
            switch (c.Type)
            {
                case "roar":
                {
                    var a = ActorOf((string)n["a"]);
                    var j = Bone(a, "jaw_01") ?? Bone(a, "head");
                    if (j != null)
                    {
                        React(j.position, a.Height * 0.5f, 1);
                        Ring(j.position, a.Height * 0.9f);
                        Flash(j.position, 20f, new Color(1f, 0.7f, 0.4f));
                        Dust(plateauT.TransformPoint(new Vector3(j.position.x, 0, j.position.z)), 4f);
                        AstronomicalHazards.Blast(j.position, a.Height * 2.2f * scale, 5f);
                    }
                    break;
                }
                case "boom":
                    if (Resolve(n["at"], out p))
                    {
                        Boom(p, size, Col((string)n["color"]));
                        if (size >= 20f)
                            React(p, size, 0);
                    }
                    break;
                case "sonic":
                    if (Resolve(n["at"], out p))
                    {
                        Ring(p, size);
                        Ring(p + Vector3.up * 4f * scale, size * 1.6f);
                        Flash(p, size, new Color(0.8f, 0.95f, 1f));
                        AstronomicalHazards.Blast(p, size * 4f * scale, 12f);
                        React(p, size * 1.5f, 0);
                    }
                    break;
                case "dust":
                    if (Resolve(n["at"], out p))
                        Dust(p.y > plateauT.position.y + 3f ? new Vector3(p.x, plateauT.position.y, p.z) : p, size);
                    break;
                case "flash":
                    if (Resolve(n["at"], out p))
                        Flash(p, size, Col((string)n["color"]));
                    break;
                case "missiles":
                    Launch(n, t);
                    break;
                case "armtear":
                    armT0 = t;
                    break;
                case "eyes":
                    eyesOn = true;
                    break;
                case "eyeout":
                    eyeOut = true;
                    break;
                case "hide":
                {
                    var a = ActorOf((string)n["a"]);
                    if (a != null && a.Model.Pivot.activeSelf)
                        a.Model.Pivot.SetActive(false);
                    break;
                }
                case "show":
                {
                    var a = ActorOf((string)n["a"]);
                    if (a != null && !a.Model.Pivot.activeSelf)
                        a.Model.Pivot.SetActive(true);
                    break;
                }
            }
        }

        void Ring(Vector3 p, float r)
        {
            Emit(p, 90, new Color(1f, 0.85f, 0.6f, 0.5f), r * 1.6f, 0f, r * 0.12f, 1.4f);
        }

        void Steps(Actor a, float t)
        {
            if (!a.Visible && a.Id == "robot")
                return;
            for (int s = 0; s < 2; s++)
            {
                var f = Bone(a, s == 0 ? "foot_l" : "foot_r");
                if (f == null)
                    continue;
                float y = plateauT.InverseTransformPoint(f.position).y;
                if (y > a.Ankle * 2.2f)
                    a.LiftT[s] = t;
                else if (y < a.Ankle * 1.35f && t - a.LiftT[s] < 1.8f && a.LiftT[s] > 0f)
                {
                    a.LiftT[s] = -9f;
                    var gp = new Vector3(f.position.x, plateauT.position.y, f.position.z);
                    Dust(gp, a.Height * 0.045f);
                    AstronomicalHazards.Blast(gp, a.Height * 1.3f * scale, 4f + a.Height * 0.09f);
                    boomI = Mathf.Max(boomI, 3f);
                    React(gp, a.Height * 0.12f, 2);
                }
            }
        }

        // ------------------------------------------------------------------ beam / missiles / orb / sword / arm / jets / aura
        bool Beam(Win w, float t, float dt)
        {
            Vector3 src,
                target;
            if (w.N["frmpos"] != null)
            {
                var fp = w.N["frmpos"];
                src = plateauT.TransformPoint(new Vector3((float)fp[0], (float)fp[1], (float)fp[2]));
            } // erupts from a point in the ground instead of a mouth
            else if (w.N["frmb"] != null)
            {
                if (!Resolve(w.N["frmb"], out src))
                    return false;
            } // or from a bone ("robot:hand_l")
            else
            {
                var m = ActorOf((string)w.N["frm"]);
                var mouth = Bone(m, "FX_mouthLaser") ?? Bone(m, "jaw_01");
                if (mouth == null)
                    return false;
                src = mouth.position;
            }
            if (w.N["to"] != null)
            {
                if (!Resolve(w.N["to"], out target))
                    return false;
            }
            else
            {
                var pts = w.N["pts"];
                int n = pts.Count;
                var a = pts[0];
                var b = pts[n - 1];
                target = Vector3.zero;
                for (int k = 0; k < n - 1; k++)
                {
                    var p0 = pts[k];
                    var p1 = pts[k + 1];
                    if (t >= (float)p0[0] && t <= (float)p1[0])
                    {
                        float f = Mathf.InverseLerp((float)p0[0], (float)p1[0], t);
                        target = plateauT.TransformPoint(
                            Vector3.Lerp(
                                new Vector3((float)p0[1], (float)p0[2], (float)p0[3]),
                                new Vector3((float)p1[1], (float)p1[2], (float)p1[3]),
                                f
                            )
                        );
                        break;
                    }
                    target = plateauT.TransformPoint(new Vector3((float)p1[1], (float)p1[2], (float)p1[3]));
                }
            }
            string cn = w.N["color"] != null ? (string)w.N["color"] : "green";
            if (cn != beamCol)
            {
                beamCol = cn;
                var bc = Col(cn);
                beamGlow.sharedMaterial.color = new Color(bc.r, bc.g, bc.b, 0.55f);
                beamLight.color = new Color(bc.r, bc.g, bc.b);
            }
            float wd = (w.N["width"] != null ? (float)w.N["width"] : 1f) * 3.2f * scale * (0.9f + 0.1f * Mathf.Sin(t * 40f));
            beamCore.enabled = beamGlow.enabled = true;
            beamCore.SetPosition(0, src);
            beamCore.SetPosition(1, target);
            beamGlow.SetPosition(0, src);
            beamGlow.SetPosition(1, target);
            beamCore.widthMultiplier = wd * 0.45f;
            beamGlow.widthMultiplier = wd * 1.6f;
            beamLight.transform.position = target + Vector3.up * 4f;
            beamLight.intensity = 8f * AstronomicalSettings.Lighting.Value;
            if (UnityEngine.Random.value < 0.9f)
            {
                Emit(target, 6, new Color(0.5f, 1f, 0.5f, 0.9f), 14f * scale, 8f, 2.4f * wd / 3.2f, 1.2f);
                Emit(target, 2, new Color(0.4f, 0.35f, 0.3f, 0.6f), 8f, 4f, 5f * wd / 3.2f, 2.5f);
            }
            Emit(src, 2, new Color(0.6f, 1f, 0.6f, 0.9f), 6f, 0f, 3f, 0.6f);
            AstronomicalHazards.Blast(target, 26f * scale, 7f);
            return true;
        }

        void Launch(JNode n, float t)
        {
            var rob = ActorOf("robot");
            var mon = ActorOf("monster");
            if (rob == null || mon == null)
                return;
            string tb = ((string)n["to"]);
            int cnt = n["n"] != null ? (int)n["n"] : 4;
            float fly = n["fly"] != null ? (float)n["fly"] : 1f;
            var tgt = Bone(mon, tb.Substring(tb.IndexOf(':') + 1));
            if (tgt == null)
                return;
            for (int k = 0; k < cnt; k++)
            {
                string sdn = k % 2 == 0 ? "l" : "r";
                string bn = "dyn_sldr_mssl_0" + (1 + k % 4) + "_" + sdn;
                var src = Bone(rob, bn) ?? Bone(rob, "dyn_shldr_02_" + sdn) ?? Bone(rob, "spine_05");
                if (src == null)
                    continue;
                Missile m = null;
                foreach (var q in missiles)
                    if (!q.Live)
                    {
                        m = q;
                        break;
                    }
                if (m == null)
                    return;
                m.Live = true;
                m.Start = src.position;
                m.Target = tgt;
                m.Off =
                    new Vector3(
                        UnityEngine.Random.Range(-14f, 14f),
                        UnityEngine.Random.Range(-10f, 10f),
                        UnityEngine.Random.Range(-14f, 14f)
                    ) * scale;
                m.T0 = t + k * 0.13f;
                m.Fly = fly;
                m.T.gameObject.SetActive(false);
                pending.Add(m);
            }
        }

        void UpdateMissiles(float t, float dt)
        {
            foreach (var m in missiles)
            {
                if (!m.Live)
                    continue;
                if (t < m.T0)
                    continue;
                float u = (t - m.T0) / m.Fly;
                if (!m.T.gameObject.activeSelf)
                {
                    m.T.gameObject.SetActive(true);
                    m.Trail.Clear();
                    m.Trail.emitting = true;
                    m.L.intensity = 4f;
                }
                Vector3 end = m.Target.position + m.Off;
                Vector3 pos =
                    Vector3.Lerp(m.Start, end, u)
                    + Vector3.up * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 24f * scale
                    + Vector3.Cross(end - m.Start, Vector3.up).normalized * Mathf.Sin(u * 9f + m.T0) * 3f * scale * (1f - u);
                Vector3 dir = pos - m.T.position;
                m.T.position = pos;
                if (dir.sqrMagnitude > 0.0001f)
                    m.T.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                if (u >= 1f)
                {
                    m.Live = false;
                    m.T.gameObject.SetActive(false);
                    m.Trail.emitting = false;
                    m.L.intensity = 0;
                    Boom(end, 15f, new Color(1f, 0.6f, 0.2f), true);
                }
            }
        }

        void UpdateBooms(float dt)
        {
            for (int k = 0; k < boomSphere.Length; k++)
            {
                if (boomAge[k] > 1.1f)
                {
                    if (boomSphere[k].gameObject.activeSelf)
                        boomSphere[k].gameObject.SetActive(false);
                    continue;
                }
                boomAge[k] += dt;
                float f = boomAge[k] / 1.1f;
                boomSphere[k].localScale = Vector3.one * boomSize[k] * (1f - Mathf.Exp(-boomAge[k] * 6f)) * 2.2f;
                var c = boomMat[k].color;
                c.a = (1f - f) * (1f - f) * 0.85f;
                boomMat[k].color = c;
            }
        }

        void Orb(Win w, float t)
        {
            orb.gameObject.SetActive(true);
            Vector3 p = new Vector3((float)w.N["pos"][0], (float)w.N["pos"][1], (float)w.N["pos"][2]);
            orb.localPosition = p;
            float r = (float)w.N["radius"];
            float fade = Mathf.SmoothStep(0, 1, (t - w.T0) / 4f);
            float pulse = 1f + 0.08f * Mathf.Sin(t * 3f) + 0.04f * Mathf.Sin(t * 11f);
            float shrink = t > 198f ? Mathf.Lerp(1f, 0.45f, (t - 198f) / 7.3f) : 1f;
            orb.localScale = Vector3.one * r * 2f * fade * pulse * shrink;
            if (zpModel != null && zpModel.Pivot != null)
            { // the detailed, animated Zero Point instead of the plain glowing ball (the ball stays as an invisible anchor for the light / the absorb effect)
                float kk = r * fade * pulse * shrink;
                zpModel.Pivot.SetActive(fade > 0.01f);
                var zcam = Camera.main;
                if (zcam != null)
                {
                    Vector3 fz = zcam.transform.position - plateauT.TransformPoint(p);
                    fz.y = 0f;
                    if (fz.sqrMagnitude > 0.01f)
                        zpModel.Pivot.transform.rotation = Quaternion.LookRotation(fz.normalized, Vector3.up);
                }
                zpModel.Pivot.transform.localScale = Vector3.one * kk;
                zpModel.Pivot.transform.position = plateauT.TransformPoint(p) - zpModel.Pivot.transform.up * (zpHalf * kk * scale);
                if (zpModel.Clips.Count > 0)
                    zpModel.Sample(zpModel.Clips[0], t);
                orb.GetComponent<Renderer>().enabled = false;
                // the reveal: the vault rises with it; as the robot drains it the cracks light up more and more (stage 1 -> 3) and the glow swells
                float stage = Mathf.Clamp01((t - w.T0) / Mathf.Max(1f, w.T1 - w.T0));
                if (zpShell2Mat != null)
                    zpShell2Mat.color = new Color(1f, 1f, 1f, 0.25f + 0.75f * stage);
                if (zpGlowMat != null)
                    zpGlowMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.7f + 0.3f * stage + 0.15f * Mathf.Sin(t * (3f + 10f * stage))));
                if (vaultModel != null && vaultModel.Pivot != null)
                {
                    if (!vaultModel.Pivot.activeSelf)
                        vaultModel.Pivot.SetActive(true);
                    vaultModel.Pivot.transform.localScale = Vector3.one * 9f * Mathf.Max(0.01f, fade);
                    vaultModel.Pivot.transform.localPosition = new Vector3(p.x, 0.3f, p.z);
                    if (vaultModel.Clips.Count > 0)
                        vaultModel.Sample(vaultModel.Clips[0], t);
                }
            }
            var c = orbMat.color;
            c.a = 0.75f * fade;
            orbMat.color = c;
            orbLight.intensity = (4f + 4f * Mathf.Sin(t * 2f)) * fade * AstronomicalSettings.Lighting.Value;
            if (UnityEngine.Random.value < 0.4f)
                Emit(orb.position, 1, new Color(0.6f, 1f, 1f, 0.7f), 4f, 0f, r * 0.12f, 1.5f);
        }

        void Absorb(Win w, float t, float dt)
        {
            Vector3 hand;
            if (!Resolve(w.N["to"], out hand))
                return;
            float k = Mathf.InverseLerp(w.T0, w.T1, t);
            int n = 10 + (int)(20 * k);
            for (int q = 0; q < n; q++)
            {
                Vector3 src = orb.position + UnityEngine.Random.onUnitSphere * orb.localScale.x * 0.45f * scale;
                var ep = new ParticleSystem.EmitParams();
                ep.position = src;
                ep.velocity = (hand - src) / 0.9f + UnityEngine.Random.insideUnitSphere * 6f;
                ep.startColor = Color.Lerp(new Color(0.5f, 1f, 1f, 0.9f), new Color(1f, 0.8f, 0.4f, 0.9f), k);
                ep.startSize = 2.2f * scale;
                ep.startLifetime = 0.9f;
                fx.Emit(ep, 1);
            }
            var rob = ActorOf("robot");
            if (rob != null && rob.Glow != null)
                rob.Glow.intensity = 3f + 5f * k;
        }

        void Aura(Win w, float t)
        {
            var rob = ActorOf("robot");
            if (rob == null || !rob.Visible)
                return;
            var c = Bone(rob, "spine_03");
            if (c == null)
                return;
            float f = Mathf.Clamp01(1f - (t - w.T0) / 40f) * 0.6f + 0.4f;
            if (rob.Glow != null)
                rob.Glow.intensity = (5f + Mathf.Sin(t * 4f)) * f * AstronomicalSettings.Lighting.Value;
            if (UnityEngine.Random.value < 0.6f)
                Emit(
                    c.position + UnityEngine.Random.insideUnitSphere * rob.Height * 0.15f,
                    1,
                    new Color(1f, 0.6f, 0.2f, 0.7f),
                    4f,
                    6f,
                    rob.Height * 0.03f,
                    1.4f
                );
        }

        void Smoke(Win w)
        {
            var a = ActorOf((string)w.N["actor"]);
            if (a == null || !a.Visible && a.Id == "robot")
                return;
            var b = Bone(a, (string)w.N["bone"]);
            if (b == null)
                return;
            float sz = (float)w.N["size"];
            if (UnityEngine.Random.value < 0.5f)
                Emit(
                    b.position + UnityEngine.Random.insideUnitSphere * sz * 2f,
                    1,
                    new Color(0.25f, 0.24f, 0.24f, 0.45f),
                    sz * 0.5f,
                    sz * 1.6f,
                    sz * 1.2f,
                    4.5f
                );
        }

        void Jets(Win w, float t)
        {
            var a = ActorOf((string)w.N["actor"]);
            if (a == null || !a.Visible)
                return;
            float pw = w.N["power"] != null ? (float)w.N["power"] : 1f;
            var bn = w.N["bones"];
            for (int k = 0; k < bn.Count; k++)
            {
                var b = Bone(a, (string)bn[k]);
                if (b == null)
                    continue;
                Vector3 dir = (string)bn[k] == "spine_03" ? -b.forward : Vector3.down;
                for (int q = 0; q < 2; q++)
                {
                    var ep = new ParticleSystem.EmitParams();
                    ep.position = b.position;
                    ep.velocity = (dir + UnityEngine.Random.insideUnitSphere * 0.25f).normalized * a.Height * 0.5f * pw;
                    ep.startColor = Color.Lerp(new Color(0.5f, 0.9f, 1f, 0.9f), new Color(1f, 0.6f, 0.2f, 0.8f), UnityEngine.Random.value);
                    ep.startSize = a.Height * 0.05f;
                    ep.startLifetime = 0.7f;
                    fx.Emit(ep, 1);
                }
            }
        }

        void SwordPlanted(Win w, float t)
        {
            float rise = Mathf.SmoothStep(0, 1, (t - w.T0) / 5f);
            var p = new Vector3((float)w.N["pos"][0], (float)w.N["pos"][1] - 16f - (1f - rise) * 110f, (float)w.N["pos"][2]);
            sword.Pivot.transform.localPosition = p; // it rises out of the ground over 5 s sword.Pivot.transform.localRotation = Quaternion.Euler(0, 20f, 6f); sword.Pivot.transform.localScale = Vector3.one;
            float f = Mathf.SmoothStep(0, 1, (t - w.T0) / 5f);
            if (swordLight != null)
            {
                swordLight.transform.localPosition = p + new Vector3(0, 60f, 0);
                swordLight.intensity = (3f + Mathf.Sin(t * 4f)) * f * AstronomicalSettings.Lighting.Value;
            }
            if (UnityEngine.Random.value < 0.5f * f)
                Emit(
                    sword.Pivot.transform.position
                        + new Vector3(
                            UnityEngine.Random.Range(-4f, 4f),
                            UnityEngine.Random.Range(8f, 60f),
                            UnityEngine.Random.Range(-2f, 2f)
                        ) * scale,
                    1,
                    new Color(0.5f, 0.95f, 1f, 0.8f),
                    3f,
                    5f,
                    1.6f,
                    1.6f
                );
        }

        void SwordHeld(Win w)
        {
            var rob = ActorOf("robot");
            if (rob == null)
                return;
            string bn = (string)w.N["bone"],
                fa = (string)w.N["forearm"];
            var hand = Bone(rob, bn.Substring(bn.IndexOf(':') + 1));
            var fore = Bone(rob, fa.Substring(fa.IndexOf(':') + 1));
            if (hand == null || fore == null)
                return;
            Vector3 dir = (hand.position - fore.position).normalized;
            if (dir.sqrMagnitude < 0.01f)
                return;
            sword.Pivot.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0, 90f, 0);
            sword.Pivot.transform.localScale = Vector3.one;
            sword.Pivot.transform.position = hand.position - dir * 5f * scale;
            if (swordLight != null)
            {
                swordLight.transform.position = hand.position + dir * 30f * scale;
                swordLight.intensity = 5f * AstronomicalSettings.Lighting.Value;
            }
            if (UnityEngine.Random.value < 0.5f)
                Emit(
                    hand.position + dir * UnityEngine.Random.Range(10f, 60f) * scale,
                    1,
                    new Color(0.5f, 0.95f, 1f, 0.8f),
                    3f,
                    2f,
                    1.4f,
                    1.0f
                );
        }

        void ArmProp(float t, float dt)
        {
            if (armT0 < 0f)
            {
                if (armProp != null)
                    armProp.gameObject.SetActive(false);
                return;
            }
            var mon = ActorOf("monster");
            var hand = Bone(mon, "hand_r");
            if (hand == null)
                return;
            if (armProp == null)
            {
                var g = new GameObject("TornArm");
                g.transform.SetParent(root.transform, false);
                armProp = g.transform;
                var mat = Sprite(new Color(0.12f, 0.12f, 0.16f, 1f));
                var up = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.Object.DestroyImmediate(up.GetComponent<Collider>());
                up.transform.SetParent(armProp, false);
                up.transform.localScale = new Vector3(7f, 12f, 7f);
                up.transform.localPosition = new Vector3(0, 10f, 0);
                up.GetComponent<Renderer>().sharedMaterial = mat;
                var lo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                UnityEngine.Object.DestroyImmediate(lo.GetComponent<Collider>());
                lo.transform.SetParent(armProp, false);
                lo.transform.localScale = new Vector3(6f, 10f, 6f);
                lo.transform.localPosition = new Vector3(2f, -8f, 0);
                lo.GetComponent<Renderer>().sharedMaterial = mat;
                lo.transform.localRotation = Quaternion.Euler(0, 0, 14f);
                var pl = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(pl.GetComponent<Collider>());
                pl.transform.SetParent(armProp, false);
                pl.transform.localScale = new Vector3(11f, 4f, 7f);
                pl.transform.localPosition = new Vector3(0, 21f, 0);
                pl.GetComponent<Renderer>().sharedMaterial = Sprite(new Color(0.8f, 0.2f, 0.5f, 1f));
            }
            armProp.gameObject.SetActive(true);
            float age = t - armT0;
            if (age < 5f)
            {
                armProp.position = hand.position;
                armProp.rotation = hand.rotation;
                if (UnityEngine.Random.value < 0.5f)
                    Emit(armProp.position, 1, new Color(1f, 0.9f, 0.4f, 0.9f), 6f, 4f, 1.6f, 0.8f);
            }
            else
            { // thrown aside
                float f = age - 5f;
                Vector3 p = armProp.position;
                float gy = plateauT.position.y + 3f;
                if (p.y > gy)
                {
                    p += new Vector3(8f, 6f - 22f * f, 3f) * dt * scale;
                    p.y = Mathf.Max(p.y, gy);
                    armProp.position = p;
                    armProp.Rotate(60f * dt, 20f * dt, 0f);
                }
            }
        }

        // ------------------------------------------------------------------ cleanup
        public void Cleanup()
        {
            AstronomicalHazards.Reset();
            if (ambientSaved)
            {
                RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z);
                ambientSaved = false;
            }
            foreach (var a in actors)
            {
                try
                {
                    if (a.Model != null && a.Model.Pivot != null)
                    {
                        a.Model.Pivot.transform.SetParent(null, false);
                        a.Model.Pivot.SetActive(false);
                        AstronomicalEventMod.ParkModel(a.Model.Pivot);
                    }
                }
                catch { }
            }
            try
            {
                if (vaultModel != null && vaultModel.Pivot != null)
                {
                    vaultModel.Pivot.transform.SetParent(null, false);
                    vaultModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(vaultModel.Pivot);
                }
            }
            catch { }
            try
            {
                if (zpModel != null && zpModel.Pivot != null)
                {
                    zpModel.Pivot.transform.SetParent(null, false);
                    zpModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(zpModel.Pivot);
                }
            }
            catch { }
            try
            {
                if (sword != null && sword.Pivot != null)
                {
                    sword.Pivot.transform.SetParent(null, false);
                    sword.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(sword.Pivot);
                }
            }
            catch { }
            actors.Clear();
            sword = null;
            zpModel = null;
            vaultModel = null;
            zpShell2Mat = zpGlowMat = null;
            if (dome != null)
                UnityEngine.Object.Destroy(dome);
            dome = null;
            if (root != null)
                UnityEngine.Object.Destroy(root);
            root = null;
            plateau = null;
            foreach (var o in owned)
                if (o != null)
                    UnityEngine.Object.Destroy(o);
            owned.Clear();
            events.Clear();
            wins.Clear();
            pending.Clear();
            armProp = null;
            orb = null;
            lastT = -1f;
            CurrentMove = "";
        }
    }
}
