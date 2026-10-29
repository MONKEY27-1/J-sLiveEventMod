using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "The End" (Fortnite Chapter 1 finale, 13 Oct 2019): the rocket at the pad launches and tears the first rift in the sky, the other rifts open and seven rockets fly around the island,
    // all of them fly into one rift again and again while it gets unstable, the frozen meteor is sent through it, a rift opens high over the exposed Zero Point, six rockets and the
    // meteor dive down, the meteor hits the Zero Point (shockwave), a black hole pulls everything in (and you), the screen goes black, and a black hole is all that is left.
    // Every moment below is pinned to the soundtrack with the times the user read off the replay; everything is a pure function of the music time, so seeks and late joins land correctly.
    // Static models (rocket, meteor) + procedural effects. The map is never moved; the shaking / pulling is controller rumble + the game's own knockback (Knockback setting).
    internal sealed class AstronomicalEnd : IEventScene, IDebug
    {
        sealed class Cue
        {
            public float T;
            public string Type;
            public JNode N;
        }

        sealed class Bolt
        {
            public LineRenderer Core,
                Glow;
            public float Age = 9f,
                Life = 0.45f;
            public Light L;
        }

        sealed class Ring
        {
            public LineRenderer L;
            public Vector3 C,
                U,
                V;
            public float Age = 9f,
                Life = 1.6f,
                R;
            public Color Col;
        }

        sealed class Leg
        {
            public float T0,
                T1;
            public int Kind,
                Ease;
            public Vector3 A,
                B,
                Ctl;
            public bool In,
                Out;
            public float Cx,
                Cz,
                Rad,
                Hgt,
                A0,
                W;
            public Leg Ref;
            public Vector3 Off;
        }

        sealed class Flyer
        {
            public Transform T;
            public GameObject G;
            public List<Leg> Legs = new List<Leg>();
            public bool Hold,
                Vis = true;
            public Vector3 HoldPos,
                LastDir = Vector3.up;
            public float AccA,
                AccB;
            public float Half,
                K;
        }

        sealed class Rift
        {
            public string Name;
            public Vector3 Pos;
            public float[] Tab;
            public Transform T,
                Tear,
                Halo;
            public Material HaloMat;
            public LineRenderer[] Arcs;
            public float Open;
            public float Size = 300f;
            public bool On;
        }

        // ---- the timeline (seconds of the music), from the replay
        const float T_IGN = 20f,
            T_LIFT = 24f,
            T_RIFT1 = 55f,
            T_ENTER1 = 58f,
            T_SEVEN = 88f,
            T_SEVEN_IN = 132f,
            T_AGAIN = 146f,
            T_METEOR = 169f,
            T_RIFT_GONE = 175f,
            T_HIGH = 186f,
            T_DIVE1 = 193f,
            T_SKY = 195f,
            T_DIVE6 = 208f,
            T_HIT = 237f,
            T_BACK = 241f,
            T_PULL = 250f,
            T_BLACK = 277f,
            T_BH = 296f;
        static readonly float[] SHAKE =
        {
            0,
            0,
            18.5f,
            0,
            20.2f,
            0.5f,
            24,
            0.9f,
            32,
            0.45f,
            54,
            0.15f,
            55,
            0.5f,
            58,
            0.2f,
            87.5f,
            0.1f,
            88,
            0.4f,
            100,
            0.2f,
            131,
            0.25f,
            132,
            0.7f,
            135,
            0.2f,
            146,
            0.3f,
            169,
            0.85f,
            175,
            0.9f,
            176,
            0.1f,
            186,
            0.4f,
            195,
            0.85f,
            208,
            0.9f,
            237,
            1f,
            245,
            1f,
            250,
            0.8f,
            276.8f,
            1f,
            277.2f,
            0.1f,
            295,
            0.1f,
            296,
            0.3f,
            308,
            0.15f,
            400,
            0,
        };
        static readonly float[] ENGINE =
        {
            0,
            0,
            19.8f,
            0,
            20.2f,
            0.6f,
            24,
            1f,
            56,
            1f,
            58.5f,
            0,
            87.5f,
            0,
            88.5f,
            0.35f,
            132,
            0.35f,
            134,
            0,
            145.5f,
            0,
            146.5f,
            0.35f,
            165,
            0.35f,
            167,
            0,
            192.5f,
            0,
            193.5f,
            0.35f,
            237.5f,
            0.35f,
            238.5f,
            0,
            400,
            0,
        };
        static readonly float[] ENTER = { 0, 0, 193.5f, 0, 196, 0.9f, 237, 0.9f, 237.4f, 1.4f, 242, 0.2f, 248, 0, 400, 0 };
        static readonly float[] DARK = { 0, 0, 241, 0, 250, 0.6f, 277, 0.97f, 400, 0.97f };
        static readonly float[] BHR = { 0, 0, 239, 0, 250, 38, 277, 60, 400, 60 };

        static float Curve(float[] k, float t)
        {
            if (t <= k[0])
                return k[1];
            for (int i = 2; i < k.Length; i += 2)
                if (t <= k[i])
                {
                    float a = k[i - 2],
                        b = k[i];
                    return Mathf.Lerp(k[i - 1], k[i + 1], b > a ? (t - a) / (b - a) : 1f);
                }
            return k[k.Length - 1];
        }

        static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }

        static float Smooth(float a, float b, float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        }

        // layout in local units (x right, y up, z forward; the viewer's spot is the origin)
        static readonly Vector3 PAD = new Vector3(-45f, 0f, 115f),
            ZP = new Vector3(30f, 0f, 150f),
            C0 = new Vector3(10f, 0f, 150f);
        const float ZP_H = 75f; // the Zero Point floats this high above the ground
        static readonly Vector3 ZPC = ZP + new Vector3(0f, ZP_H, 0f),
            METEOR0 = ZP + new Vector3(0f, ZP_H + 130f, 0f);
        const float TOWER_H = 80f,
            ARM_LEN = 10f,
            ROCKET_H = 150f,
            METEOR_D = 70f;
        static readonly int[] RIFT_AZ = { -40, 40, 110, 180, -110, -170 };
        static readonly float[] RIFT_Y = { 250, 290, 270, 300, 260, 280 };
        static readonly float[] RIFT_OPEN = { 62f, 70f, 75.5f, 81f, 84.5f, 86.5f };

        static float DiveArrive(int i)
        {
            return i == 0 ? T_DIVE1 + 9f : T_DIVE6 + 8f + 1.1f * (i - 1);
        }

        GameObject root,
            dome,
            voidObj,
            bhBall;
        Transform plateauT,
            shakeT,
            haloBh,
            zpOrb,
            zpDome,
            beam;
        Transform[] arms = new Transform[4];
        float[] armSign = new float[4];
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Cue> events = new List<Cue>();
        readonly List<Flyer> rockets = new List<Flyer>();
        Flyer meteor;
        readonly List<Rift> rifts = new List<Rift>();
        float rocketK = 1f,
            meteorK = 1f;
        GltfModel rocketModel,
            meteorModel;
        Material matSprite,
            matGlow,
            matLine,
            padLightMat,
            beaconMat,
            tearMat,
            matGlowTop,
            matLineTop,
            voidMat,
            orbMat,
            domeMat,
            beamMat,
            bhHaloMat,
            bhBallMat;
        Texture2D glowTex,
            lineTex;
        ParticleSystem fx,
            smoke;
        ParticleSystemRenderer fxRend;
        Light boomLight,
            engineLight,
            padLight,
            riftLight,
            zpLight,
            bhLight,
            meteorLight;
        Transform[] boomSphere;
        Material[] boomMat;
        float[] boomAge,
            boomSize;
        int boomNext;
        Bolt[] bolts = new Bolt[4];
        int boltNext;
        Ring[] rings = new Ring[6];
        int ringNext;
        LineRenderer[] bhRings = new LineRenderer[3];
        Mesh skyMesh,
            tearMesh;
        Color[] skyCols,
            skyBase;
        Vector3[] skyDirs;
        int skyGrad;
        float skyT = -9f,
            arcT;
        AudioSource sfx,
            engineSrc;
        float reactCool,
            rumbleT,
            nudgeT;
        float lastT = -1f,
            pulse,
            shakeBump,
            zpPulse,
            shatterT = -9f,
            accA,
            accC,
            accD,
            accE,
            accF;
        int cueIdx;
        bool paused,
            voidMode,
            voidSet;
        Vector3 voidPos;
        float scale = 1f;
        Vector3 ambientSave;
        bool ambientSaved;
        AstronomicalEventDef def;
        static bool fitOk;
        static Vector3 fitO;
        static float fitY,
            fitS = 1f;

        // ------------------------------------------------------------------ helpers
        Material Mat(Color c, Texture tex = null, int queue = -1)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh == null)
                sh = Shader.Find("Unlit/Color");
            var m = new Material(sh) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            if (queue > 0)
                m.renderQueue = queue;
            owned.Add(m);
            return m;
        }

        // drawn over everything (ignores depth): the black screen and what is left in it
        Material TopMat(Color c, Texture tex, int queue)
        {
            var sh = Shader.Find("UI/Default");
            if (sh == null)
                return Mat(c, tex, queue);
            var m = new Material(sh) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            m.SetInt("unity_GUIZTestMode", 8);
            m.renderQueue = queue;
            owned.Add(m);
            return m;
        }

        static Color Col(string n, float a = 1f)
        {
            switch (n)
            {
                case "violet":
                    return new Color(0.62f, 0.38f, 1f, a);
                case "cyan":
                    return new Color(0.4f, 0.9f, 1f, a);
                case "orange":
                    return new Color(1f, 0.55f, 0.15f, a);
                case "green":
                    return new Color(0.35f, 1f, 0.35f, a);
                default:
                    return new Color(1f, 1f, 1f, a);
            }
        }

        Light AddLight(Transform parent, Color c, float range, float intensity)
        {
            var g = new GameObject("EndLight");
            g.transform.SetParent(parent, false);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        Vector3 W(Vector3 local)
        {
            return plateauT.TransformPoint(local);
        }

        float U
        {
            get { return scale; }
        }

        static float Rnd(float a, float b)
        {
            return UnityEngine.Random.Range(a, b);
        }

        static int Take(ref float acc, float rate, float dt)
        {
            acc += rate * dt;
            int n = (int)acc;
            acc -= n;
            return n;
        }

        float Parts
        {
            get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); }
        }
        float Lit
        {
            get { return AstronomicalSettings.Lighting.Value; }
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

        Texture2D MakeLineTex()
        {
            var t = new Texture2D(4, 32, TextureFormat.RGBA32, false);
            var px = new Color[4 * 32];
            for (int y = 0; y < 32; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / 16f - 1f);
                float a = Mathf.Pow(1f - v, 1.6f);
                for (int x = 0; x < 4; x++)
                    px[y * 4 + x] = new Color(1, 1, 1, a);
            }
            t.SetPixels(px);
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static float[] OpenTab(float open, float close)
        {
            return new[] { 0f, 0f, open, 0f, open + 1.5f, 0.5f, close, 0.5f, close + 0.8f, 0f, 500f, 0f };
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
            bool sl = AstronomicalShowdown.lastFit;
            Vector3 so = AstronomicalShowdown.fitOrigin;
            float sy = AstronomicalShowdown.fitYaw,
                ss = AstronomicalShowdown.fitScale;
            try
            {
                AstronomicalShowdown.FitArena(ref origin, ref yaw, ref scale, def.Cues != null ? def.Cues["arena"] : null);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Arena fit failed: " + e.Message);
            }
            AstronomicalShowdown.lastFit = sl;
            AstronomicalShowdown.fitOrigin = so;
            AstronomicalShowdown.fitYaw = sy;
            AstronomicalShowdown.fitScale = ss;
            fitOk = true;
            fitO = origin;
            fitY = yaw;
            fitS = scale;
            root = new GameObject("TheEnd");
            root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * scale;
            var pl = new GameObject("Plateau");
            plateauT = pl.transform;
            plateauT.SetParent(root.transform, false);
            plateauT.localPosition = new Vector3(0, AstronomicalSettings.ShowPlateau.Value, 0);
            glowTex = MakeGlow();
            lineTex = MakeLineTex();
            owned.Add(glowTex);
            owned.Add(lineTex);
            matSprite = Mat(Color.white);
            matGlow = Mat(Color.white, glowTex);
            matLine = Mat(Color.white, lineTex);
            matGlowTop = TopMat(Color.white, glowTex, 4002);
            matLineTop = TopMat(Color.white, lineTex, 4003);
            BuildFx();
            BuildPad();
            BuildRifts();
            BuildSky();
            BuildPools();
            BuildAudio();
            BuildZeroPoint();
            BuildBlackHole();
            BuildGroundCracks();
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b);
            ambientSaved = true;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.36f);
            var moon = new GameObject("EndMoon");
            moon.transform.SetParent(root.transform, false);
            moon.transform.rotation = Quaternion.Euler(40f, 150f + yaw, 0);
            var ml = moon.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.color = new Color(0.7f, 0.8f, 1f);
            ml.intensity = 1.1f;
            ml.shadows = LightShadows.None;
            // models: the rocket (seven copies) and the meteor
            rocketModel = def.ActorModels.Count > 0 ? def.ActorModels[0] : null;
            meteorModel = def.ActorModels.Count > 1 ? def.ActorModels[1] : null;
            if (rocketModel != null && rocketModel.Pivot != null)
            {
                var b = rocketModel.RendererBounds.size;
                rocketK = ROCKET_H / Mathf.Max(0.01f, Mathf.Max(b.x, b.y, b.z));
                for (int i = 0; i < 7; i++)
                {
                    var f = new Flyer();
                    f.G = i == 0 ? rocketModel.Pivot : UnityEngine.Object.Instantiate(rocketModel.Pivot);
                    f.T = f.G.transform;
                    f.T.SetParent(plateauT, false);
                    f.T.localScale = Vector3.one * rocketK;
                    f.G.SetActive(i == 0);
                    foreach (var r in f.G.GetComponentsInChildren<Renderer>(true))
                    {
                        r.allowOcclusionWhenDynamic = false;
                        r.enabled = true;
                    }
                    f.Vis = i == 0;
                    f.Half = ROCKET_H * 0.5f;
                    rockets.Add(f);
                }
            }
            else
                AstronomicalEventMod.Log.LogWarning("The End: the rocket model did not load");
            if (meteorModel != null && meteorModel.Pivot != null)
            {
                var b = meteorModel.RendererBounds.size;
                meteorK = METEOR_D / Mathf.Max(0.01f, Mathf.Max(b.x, b.y, b.z));
                meteor = new Flyer();
                meteor.G = meteorModel.Pivot;
                meteor.T = meteor.G.transform;
                meteor.T.SetParent(plateauT, false);
                meteor.T.localScale = Vector3.one * meteorK;
                meteor.G.SetActive(true);
                foreach (var r in meteor.G.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                }
                meteor.Half = b.y * meteorK * 0.5f;
                meteor.Hold = true;
                meteor.HoldPos = METEOR0;
                meteorLight = AddLight(plateauT, new Color(0.5f, 0.75f, 1f), 260f * scale, 0f);
            }
            else
                AstronomicalEventMod.Log.LogWarning("The End: the meteor model did not load");
            BuildProps();
            BuildVault();
            BuildFlights();
            ParseCues(def.Cues);
            lastT = -1f;
            cueIdx = 0;
            pulse = 0f;
            shakeBump = 0f;
            zpPulse = 0f;
            shatterT = -9f;
            voidSet = false;
            voidMode = false;
            AstronomicalEventMod.Log.LogInfo(
                "The End built: origin "
                    + origin
                    + ", scale "
                    + scale
                    + ", rockets "
                    + rockets.Count
                    + ", meteor "
                    + (meteor != null)
                    + ", rifts "
                    + rifts.Count
                    + ", cues "
                    + events.Count
                    + ", top shader "
                    + (matGlowTop.shader != null ? matGlowTop.shader.name : "none")
            );
        }

        void BuildProps()
        {
            zpModel = def.Prop("zeropoint");
            bhModel = def.Prop("blackhole");
            if (zpModel != null && zpModel.Pivot != null)
            {
                zpModel.Pivot.transform.SetParent(plateauT, false);
                zpModel.Pivot.SetActive(true);
                zpHalf = zpModel.RendererBounds.size.y * 0.5f;
                foreach (var r in zpModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 3000)
                        r.sharedMaterial.renderQueue =
                            r.name == "Glow" ? 2999
                            : r.name == "Shell" ? 3001
                            : r.name == "Shell2" ? 3002
                            : r.name == "Burst" ? 3003
                            : r.sharedMaterial.renderQueue;
                }
                foreach (var t in zpModel.Pivot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Bubble")
                        zpBubble = t.gameObject;
                zpOrb.GetComponent<Renderer>().enabled = false;
                zpDome.GetComponent<Renderer>().enabled = false;
            }
            if (bhModel != null && bhModel.Pivot != null)
            {
                bhModel.Pivot.transform.SetParent(root.transform, false);
                bhModel.Pivot.SetActive(false);
                bhHalf = bhModel.RendererBounds.size.y * 0.5f;
                var rl = new List<Renderer>();
                foreach (var r in bhModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    rl.Add(r);
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !bhMats.Contains(m))
                            bhMats.Add(m);
                    var mf = r.GetComponent<MeshFilter>();
                    string n = r.name;
                    if (mf != null && mf.sharedMesh != null && (n.Contains("Outer") || n.Contains("Inner")))
                    {
                        var us = new List<Vector2>();
                        mf.sharedMesh.GetUVs(0, us);
                        scrolls.Add(
                            new UvScroll
                            {
                                M = mf.sharedMesh,
                                Base = us.ToArray(),
                                Speed = n.Contains("Inner") ? 0.16f : 0.06f,
                            }
                        );
                    }
                }
                bhRend = rl.ToArray();
                bhHalo(false);
                haloBh.gameObject.SetActive(false);
                bhBall.SetActive(false);
            }
        }

        void BuildVault()
        {
            var vm = def.Prop("vault");
            if (vm == null || vm.Pivot == null)
                return;
            vm.Pivot.transform.SetParent(plateauT, false);
            vm.Pivot.SetActive(true);
            const float k = 14f;
            vm.Pivot.transform.localScale = Vector3.one * k;
            vm.Pivot.transform.localPosition = new Vector3(ZP.x, 0.3f, ZP.z);
            vaultModel = vm;
            foreach (var r in vm.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;
                if (r.name == "ShaftGlow")
                    r.sharedMaterial.renderQueue = 3000;
            }
        }

        GltfModel vaultModel;

        void bhHalo(bool top)
        {
            // draw order without a depth buffer: names are BH_<digit>...; the black screen is queue 4000, so on top of it everything is 4001+
            foreach (var r in bhRend)
            {
                var m = r.sharedMaterial;
                if (m == null)
                    continue;
                int d = 0;
                var n = r.name;
                if (n.StartsWith("BH_") && n.Length > 3 && char.IsDigit(n[3]))
                    d = n[3] - '0';
                if (top)
                {
                    if (!bhShader.ContainsKey(m))
                        bhShader[m] = m.shader;
                    var ui = Shader.Find("UI/Default");
                    if (ui != null)
                    {
                        var tex = m.mainTexture;
                        var col = m.color;
                        m.shader = ui;
                        m.mainTexture = tex;
                        m.color = col;
                        m.SetInt("unity_GUIZTestMode", 8);
                    }
                    m.renderQueue = 4001 + d;
                }
                else
                {
                    Shader sh;
                    if (bhShader.TryGetValue(m, out sh) && sh != null)
                    {
                        var tex = m.mainTexture;
                        var col = m.color;
                        m.shader = sh;
                        m.mainTexture = tex;
                        m.color = col;
                        bhShader.Remove(m);
                    }
                    m.renderQueue = 3000 + d * 3;
                }
            }
            bhTop = top;
        }

        // the animated black hole model: turned to face the viewer, swirl scrolled, faded with alpha
        void BhModelStep(float t, Vector3 pos, float rW, float alpha, bool on, bool inVoid, Camera cam)
        {
            var pv = bhModel.Pivot;
            if (pv.activeSelf != on)
                pv.SetActive(on);
            if (!on)
                return;
            if (inVoid != bhTop)
                bhHalo(inVoid);
            Vector3 f = cam != null ? cam.transform.position - pos : Vector3.back;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f)
                f = -root.transform.forward;
            pv.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            pv.transform.localScale = Vector3.one * (rW / U);
            pv.transform.position = pos - pv.transform.up * (bhHalf * rW);
            if (bhModel.Clips.Count > 0)
                bhModel.Sample(bhModel.Clips[0], t);
            var c = new Color(1f, 1f, 1f, alpha);
            foreach (var m in bhMats)
                m.color = c;
            foreach (var sc in scrolls)
            {
                sc.Tmp.Clear();
                float o = t * sc.Speed;
                foreach (var uv in sc.Base)
                    sc.Tmp.Add(new Vector2(uv.x + o, uv.y));
                sc.M.SetUVs(0, sc.Tmp);
            }
        }

        void ParseCues(JNode c)
        {
            events.Clear();
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
            events.Sort((a, b) => a.T.CompareTo(b.T));
        }

        void BuildFx()
        {
            var g = new GameObject("EndFx");
            g.transform.SetParent(root.transform, false);
            fx = g.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = fx.main;
            m.maxParticles = 6000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.playOnAwake = false;
            m.startLifetime = 2f;
            m.gravityModifier = 0.05f;
            var e = fx.emission;
            e.enabled = false;
            fxRend = g.GetComponent<ParticleSystemRenderer>();
            fxRend.sharedMaterial = matGlow;
            fx.Play();
            var g2 = new GameObject("EndSmoke");
            g2.transform.SetParent(root.transform, false);
            smoke = g2.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = smoke.main;
            m2.maxParticles = 3000;
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            m2.playOnAwake = false;
            m2.startLifetime = 12f;
            m2.gravityModifier = -0.01f;
            var e2 = smoke.emission;
            e2.enabled = false;
            g2.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;
            var sol = smoke.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.9f));
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            gr.alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            };
            col.color = gr;
            smoke.Play();
            boomLight = AddLight(root.transform, Color.white, 700f * scale, 0f);
        }

        void Emit(ParticleSystem ps, Vector3 p, Vector3 v, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = p;
            ep.velocity = v;
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = life;
            ps.Emit(ep, 1);
        }

        void Burst(Vector3 p, int n, Color c, float speed, float up, float size, float life)
        {
            for (int k = 0; k < n; k++)
                Emit(
                    fx,
                    p + UnityEngine.Random.insideUnitSphere * size * 0.4f,
                    UnityEngine.Random.onUnitSphere * Rnd(0.3f, 1f) * speed + Vector3.up * up,
                    c,
                    size * Rnd(0.6f, 1.5f),
                    life * Rnd(0.6f, 1.3f)
                );
        }

        // pad, two gantry towers with swing arms, ring of lights, beacons (as in Rocket Launch)
        static void Box(List<Vector3> v, List<int> tr, List<Color> c, Vector3 ctr, Vector3 size, Color col)
        {
            int b = v.Count;
            Vector3 h = size * 0.5f;
            for (int k = 0; k < 8; k++)
            {
                v.Add(ctr + new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z));
                c.Add(col);
            }
            int[] f = { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };
            foreach (int i in f)
                tr.Add(b + i);
        }

        GameObject MeshObj(string name, Transform parent, List<Vector3> v, List<int> tr, List<Color> c, Material m)
        {
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(tr, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.allowOcclusionWhenDynamic = false;
            return g;
        }

        void Disc(string name, float r, float h, float y, Color col)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            d.name = name;
            d.transform.SetParent(shakeT, false);
            d.transform.localPosition = new Vector3(PAD.x, y, PAD.z);
            d.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            d.GetComponent<Renderer>().sharedMaterial = Mat(col);
        }

        void BuildPad()
        {
            shakeT = new GameObject("Pad").transform;
            shakeT.SetParent(plateauT, false);
            Disc("PadBase", 30f, 1.2f, 0.6f, new Color(0.10f, 0.10f, 0.12f));
            Disc("PadRing", 26f, 0.2f, 1.25f, new Color(0.22f, 0.22f, 0.26f));
            Disc("PadCore", 11f, 0.2f, 1.35f, new Color(0.05f, 0.05f, 0.06f));
            var v = new List<Vector3>();
            var tr = new List<int>();
            var c = new List<Color>();
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Box(v, tr, c, PAD + new Vector3(Mathf.Cos(a) * 28f, 1.6f, Mathf.Sin(a) * 28f), new Vector3(2.4f, 1.2f, 2.4f), Color.white);
            }
            padLightMat = Mat(new Color(1f, 0.7f, 0.2f, 1f));
            MeshObj("PadLights", shakeT, v, tr, c, padLightMat);
            var vb = new List<Vector3>();
            var tb = new List<int>();
            var cb = new List<Color>();
            int ai = 0;
            for (int s = -1; s <= 1; s += 2)
            {
                var v2 = new List<Vector3>();
                var t2 = new List<int>();
                var c2 = new List<Color>();
                Color steel = new Color(0.30f, 0.31f, 0.37f);
                Vector3 cen = new Vector3(PAD.x + s * 26f, 0f, PAD.z);
                for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    Box(v2, t2, c2, cen + new Vector3(px * 4f, TOWER_H * 0.5f, pz * 4f), new Vector3(1.6f, TOWER_H, 1.6f), steel);
                for (float y = 6f; y <= TOWER_H; y += 12f)
                {
                    for (int pz = -1; pz <= 1; pz += 2)
                        Box(v2, t2, c2, cen + new Vector3(0, y, pz * 4f), new Vector3(8f, 0.9f, 0.9f), steel);
                    for (int px = -1; px <= 1; px += 2)
                        Box(v2, t2, c2, cen + new Vector3(px * 4f, y, 0), new Vector3(0.9f, 0.9f, 8f), steel);
                }
                MeshObj("Tower" + s, shakeT, v2, t2, c2, matSprite);
                Box(vb, tb, cb, cen + new Vector3(0, TOWER_H + 1.6f, 0), new Vector3(2.6f, 2.6f, 2.6f), Color.white);
                foreach (float ay in new[] { 60f, 40f })
                {
                    var arm = new GameObject("Arm");
                    arm.transform.SetParent(shakeT, false);
                    arm.transform.localPosition = new Vector3(PAD.x + s * 22f, ay, PAD.z);
                    var av = new List<Vector3>();
                    var at = new List<int>();
                    var ac = new List<Color>();
                    Box(
                        av,
                        at,
                        ac,
                        new Vector3(-s * ARM_LEN * 0.5f, 0f, 0f),
                        new Vector3(ARM_LEN, 2.2f, 2.2f),
                        new Color(0.42f, 0.43f, 0.5f)
                    );
                    Box(
                        av,
                        at,
                        ac,
                        new Vector3(-s * (ARM_LEN - 0.5f), 0f, 0f),
                        new Vector3(1.4f, 3.4f, 3.4f),
                        new Color(0.9f, 0.5f, 0.15f)
                    );
                    MeshObj("ArmMesh", arm.transform, av, at, ac, matSprite);
                    arms[ai] = arm.transform;
                    armSign[ai] = s;
                    ai++;
                }
            }
            beaconMat = Mat(new Color(1f, 0.15f, 0.1f, 1f));
            MeshObj("Beacons", shakeT, vb, tb, cb, beaconMat);
            padLight = AddLight(plateauT, new Color(1f, 0.92f, 0.8f), 220f * scale, 0f);
            padLight.transform.localPosition = PAD + new Vector3(0, 60f, -40f);
            engineLight = AddLight(plateauT, new Color(1f, 0.6f, 0.25f), 320f * scale, 0f);
        }

        // rifts: the main one (the rocket's), the one high over the Zero Point and six around the island
        Mesh MakeTear()
        {
            int n = 30;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var tri = new List<int>();
            var rnd = new System.Random(21);
            v.Add(Vector3.zero);
            c.Add(new Color(0.0f, 0.0f, 0.02f, 1f));
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.4f);
                c.Add(new Color(0.05f, 0.01f, 0.12f, 1f));
            }
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                float sp = (k % 2 == 1) ? 1f + 0.1f + (float)rnd.NextDouble() * 0.22f : 1f;
                v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f * sp);
                c.Add(new Color(0.85f, 0.7f, 1f, 1f));
            }
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                v.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.78f);
                c.Add(new Color(0.55f, 0.25f, 1f, 0f));
            }
            for (int k = 0; k < n; k++)
            {
                int k2 = (k + 1) % n,
                    ia = 1 + k,
                    ib = 1 + k2,
                    oa = 1 + n + k,
                    ob = 1 + n + k2,
                    ga = 1 + 2 * n + k,
                    gb = 1 + 2 * n + k2;
                tri.AddRange(new[] { 0, ia, ib, ia, oa, ob, ia, ob, ib, oa, ga, gb, oa, gb, ob });
            }
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        void AddRift(string name, Vector3 pos, float[] tab, int arcs, bool main)
        {
            var r = new Rift
            {
                Name = name,
                Pos = pos,
                Tab = tab,
                Arcs = new LineRenderer[arcs],
            };
            var h = new GameObject("Rift" + name);
            r.T = h.transform;
            r.T.SetParent(plateauT, false);
            r.T.localPosition = pos;
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.name = "Halo";
            r.Halo = halo.transform;
            r.Halo.SetParent(r.T, false);
            r.HaloMat = Mat(new Color(0.55f, 0.3f, 1f, 0f), glowTex, 2999);
            halo.GetComponent<Renderer>().sharedMaterial = r.HaloMat;
            var tear = new GameObject("Tear");
            r.Tear = tear.transform;
            r.Tear.SetParent(r.T, false);
            tear.AddComponent<MeshFilter>().sharedMesh = tearMesh;
            var mr = tear.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tearMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.allowOcclusionWhenDynamic = false;
            for (int k = 0; k < arcs; k++)
            {
                var g = new GameObject("Arc");
                g.transform.SetParent(r.T, false);
                var l = g.AddComponent<LineRenderer>();
                l.useWorldSpace = false;
                l.positionCount = 7;
                l.sharedMaterial = arcMat;
                l.shadowCastingMode = ShadowCastingMode.Off;
                l.numCapVertices = 3;
                l.enabled = false;
                r.Arcs[k] = l;
            }
            r.T.gameObject.SetActive(false);
            rifts.Add(r);
        }

        Material arcMat;

        void BuildRifts()
        {
            tearMesh = MakeTear();
            tearMat = Mat(Color.white, null, 3000);
            arcMat = Mat(new Color(0.9f, 0.82f, 1f, 1f), lineTex, 3002);
            Vector3 rm = new Vector3(0f, 380f, 300f),
                rh = new Vector3(30f, 470f, 240f);
            AddRift(
                "main",
                rm,
                new[]
                {
                    0f,
                    0f,
                    T_RIFT1 - 0.4f,
                    0f,
                    T_RIFT1,
                    0.22f,
                    T_ENTER1,
                    0.5f,
                    66f,
                    0.45f,
                    130f,
                    0.55f,
                    138f,
                    0.75f,
                    T_AGAIN,
                    0.9f,
                    T_METEOR,
                    1.15f,
                    T_METEOR + 3f,
                    1.6f,
                    T_RIFT_GONE - 0.4f,
                    1.5f,
                    T_RIFT_GONE + 0.4f,
                    0f,
                    500f,
                    0f,
                },
                8,
                true
            );
            AddRift(
                "high",
                rh,
                new[]
                {
                    0f,
                    0f,
                    T_HIGH - 0.5f,
                    0f,
                    T_HIGH + 0.5f,
                    0.35f,
                    T_DIVE1 - 1f,
                    0.5f,
                    T_SKY - 0.2f,
                    0.6f,
                    T_SKY + 0.6f,
                    1.9f,
                    T_HIT,
                    1.9f,
                    T_HIT + 0.6f,
                    2.6f,
                    T_BACK,
                    0f,
                    500f,
                    0f,
                },
                8,
                true
            );
            for (int k = 0; k < 6; k++)
            {
                float a = RIFT_AZ[k] * Mathf.Deg2Rad;
                AddRift(
                    "r" + (k + 2),
                    new Vector3(C0.x + 420f * Mathf.Sin(a), RIFT_Y[k], C0.z + 420f * Mathf.Cos(a)),
                    OpenTab(RIFT_OPEN[k], T_RIFT_GONE),
                    5,
                    false
                );
            }
            riftLight = AddLight(plateauT, new Color(0.62f, 0.4f, 1f), 700f * scale, 0f);
        }

        // night sky dome (drawn first, follows the camera); it glows around every open rift, floods white when "we enter the sky" and goes black with the black hole
        void BuildSky()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int lat = 14,
                lon = 28;
            var rnd = new System.Random(5);
            var dirs = new List<Vector3>();
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = i * Mathf.PI * 0.55f / lat,
                    ph = j * Mathf.PI * 2f / lon;
                float s = Mathf.Sin(th);
                var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph));
                dirs.Add(d);
                verts.Add(d * 420f);
                float uu = (float)i / lat;
                cols.Add(Color.Lerp(new Color(0.012f, 0.02f, 0.08f, 1), new Color(0.12f, 0.09f, 0.26f, 1), Mathf.Pow(uu, 1.8f)));
            }
            skyGrad = verts.Count;
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
            for (int k = 0; k < 300; k++)
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
            skyMesh = new Mesh();
            skyMesh.SetVertices(verts);
            skyMesh.SetColors(cols);
            skyMesh.SetTriangles(tris, 0);
            skyMesh.RecalculateBounds();
            owned.Add(skyMesh);
            skyCols = cols.ToArray();
            skyBase = cols.ToArray();
            skyDirs = dirs.ToArray();
            dome = new GameObject("EndSky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(matSprite);
            dm.renderQueue = 1000;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void UpdateSky(float t)
        {
            if (dome == null || skyMesh == null)
                return;
            float en = Curve(ENTER, t) + pulse * 0.1f,
                dark = Curve(DARK, t);
            Vector3 dp = dome.transform.position;
            var rd = new Vector3[rifts.Count];
            var op = new float[rifts.Count];
            for (int q = 0; q < rifts.Count; q++)
            {
                rd[q] = (rifts[q].T.position - dp).normalized;
                op[q] = Mathf.Min(1.2f, rifts[q].Open) + (rifts[q].Open > 0.05f ? pulse * 0.2f : 0f);
            }
            for (int k = 0; k < skyGrad; k++)
            {
                float sum = 0f;
                for (int q = 0; q < rd.Length; q++)
                {
                    if (op[q] < 0.02f)
                        continue;
                    float d = Vector3.Dot(skyDirs[k], rd[q]);
                    sum += Mathf.Pow(Mathf.Clamp01((d - 0.4f) / 0.6f), 2.2f) * op[q];
                }
                sum = Mathf.Min(sum, 1.6f);
                var b = skyBase[k];
                float dk = (1f - 0.3f * Mathf.Min(1f, sum)) * (1f - dark);
                float up = 0.4f + 0.6f * Mathf.Clamp01(skyDirs[k].y);
                skyCols[k] = new Color(
                    (b.r * dk + 0.34f * sum + 0.45f * en * up) * (1f - dark),
                    (b.g * dk + 0.12f * sum + 0.5f * en * up) * (1f - dark),
                    (b.b * dk + 0.62f * sum + 0.85f * en * up) * (1f - dark),
                    1f
                );
            }
            skyMesh.colors = skyCols;
        }

        void BuildPools()
        {
            boomSphere = new Transform[8];
            boomMat = new Material[8];
            boomAge = new float[8];
            boomSize = new float[8];
            for (int k = 0; k < 8; k++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(root.transform, false);
                boomMat[k] = Mat(Color.white, glowTex);
                s.GetComponent<Renderer>().sharedMaterial = boomMat[k];
                s.SetActive(false);
                boomSphere[k] = s.transform;
                boomAge[k] = 9f;
            }
            for (int k = 0; k < bolts.Length; k++)
            {
                var b = new Bolt();
                b.Glow = NewLine("BoltGlow", matLine, 8f);
                b.Core = NewLine("BoltCore", matSprite, 1.6f);
                b.L = AddLight(root.transform, Color.white, 260f * scale, 0f);
                bolts[k] = b;
            }
            for (int k = 0; k < rings.Length; k++)
            {
                var r = new Ring { L = NewLine("Ring", matLine, 10f) };
                r.L.positionCount = 64;
                rings[k] = r;
            }
            for (int k = 0; k < bhRings.Length; k++)
            {
                bhRings[k] = NewLine("BhRing", matLine, 4f);
                bhRings[k].positionCount = 72;
            }
        }

        LineRenderer NewLine(string n, Material m, float w)
        {
            var g = new GameObject(n);
            g.transform.SetParent(root.transform, false);
            var l = g.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.sharedMaterial = new Material(m);
            owned.Add(l.sharedMaterial);
            l.widthMultiplier = w * scale;
            l.numCapVertices = 3;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.enabled = false;
            l.positionCount = 2;
            l.sharedMaterial.renderQueue = 3003;
            return l;
        }

        // the Zero Point (a glowing orb under a flickering bubble, a beam of light), the meteor's light
        void BuildZeroPoint()
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
            o.name = "ZeroPoint";
            zpOrb = o.transform;
            zpOrb.SetParent(plateauT, false);
            zpOrb.localPosition = ZPC;
            orbMat = Mat(new Color(0.55f, 0.9f, 1f, 0.8f), glowTex);
            o.GetComponent<Renderer>().sharedMaterial = orbMat;
            var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(d.GetComponent<Collider>());
            d.name = "ZpBubble";
            zpDome = d.transform;
            zpDome.SetParent(plateauT, false);
            zpDome.localPosition = ZPC;
            zpDome.localScale = Vector3.one * 90f;
            domeMat = Mat(new Color(0.4f, 0.8f, 1f, 0.12f), glowTex);
            d.GetComponent<Renderer>().sharedMaterial = domeMat;
            beamMat = Mat(new Color(0.5f, 0.9f, 1f, 0.6f), lineTex, 3001);
            var bg = new GameObject("ZpBeam");
            bg.transform.SetParent(plateauT, false);
            var bl = bg.AddComponent<LineRenderer>();
            bl.useWorldSpace = false;
            bl.positionCount = 2;
            bl.sharedMaterial = beamMat;
            bl.SetPosition(0, ZP);
            bl.SetPosition(1, ZP + new Vector3(0, 500f, 0));
            bl.widthMultiplier = 14f;
            bl.shadowCastingMode = ShadowCastingMode.Off;
            beam = bg.transform;
            zpLight = AddLight(plateauT, new Color(0.5f, 0.85f, 1f), 280f * scale, 0f);
            zpLight.transform.localPosition = ZPC + new Vector3(0, 15f, 0);
        }

        // the black hole: a black ball with a bright rim and accretion rings; in the black screen it is drawn on top of everything
        void BuildBlackHole()
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = "BhBall";
            bhBall = b;
            b.transform.SetParent(root.transform, false);
            bhBallMat = Mat(new Color(0f, 0f, 0f, 1f), null, 3001);
            b.GetComponent<Renderer>().sharedMaterial = bhBallMat;
            b.SetActive(false);
            var h = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(h.GetComponent<Collider>());
            h.name = "BhHalo";
            haloBh = h.transform;
            haloBh.SetParent(root.transform, false);
            bhHaloMat = Mat(new Color(0.75f, 0.85f, 1f, 0f), glowTex, 2998);
            h.GetComponent<Renderer>().sharedMaterial = bhHaloMat;
            h.SetActive(false);
            bhLight = AddLight(root.transform, new Color(0.7f, 0.85f, 1f), 600f * scale, 0f);
            voidObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(voidObj.GetComponent<Collider>());
            voidObj.name = "BlackScreen";
            voidObj.transform.SetParent(root.transform, false);
            voidObj.transform.localScale = Vector3.one * 5f;
            voidMat = TopMat(new Color(0f, 0f, 0f, 0f), null, 4000);
            voidObj.GetComponent<Renderer>().sharedMaterial = voidMat;
            voidObj.SetActive(false);
        }

        // the ground cracks open around the Zero Point (one runs towards the viewer) and chunks of the island are pulled into the black hole
        sealed class GCrack
        {
            public LineRenderer L;
            public Vector3[] P;
            public int Shown = -1;
        }

        readonly List<GCrack> gcracks = new List<GCrack>();

        void BuildGroundCracks()
        {
            var rnd = new System.Random(31);
            int n = 14;
            float toViewer = Mathf.Atan2(-ZP.z, -ZP.x);
            for (int c = 0; c < n; c++)
            {
                float a = c == 0 ? toViewer : c * Mathf.PI * 2f / n + ((float)rnd.NextDouble() - 0.5f) * 0.3f,
                    len = c == 0 ? 190f : 90f + (float)rnd.NextDouble() * 100f;
                int segs = 22;
                var pts = new Vector3[segs + 1];
                Vector3 p = ZP;
                for (int k = 0; k <= segs; k++)
                {
                    a += ((float)rnd.NextDouble() - 0.5f) * 0.35f;
                    RaycastHit h;
                    var w = W(p + Vector3.up * 40f);
                    p.y = Physics.Raycast(w, Vector3.down, out h, 120f, ~0, QueryTriggerInteraction.Ignore)
                        ? plateauT.InverseTransformPoint(h.point).y + 0.4f
                        : 0.4f;
                    pts[k] = p;
                    p += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (len / segs);
                }
                var l = NewLine("GroundCrack", matLine, 7f);
                l.useWorldSpace = false;
                l.transform.SetParent(plateauT, false);
                l.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
                gcracks.Add(new GCrack { L = l, P = pts });
            }
        }

        void GroundCracksStep(float t, float dt)
        {
            float p = Smooth(T_BACK, T_PULL + 12f, t) * (t < T_BLACK ? 1f : 0f);
            Color col = Color.Lerp(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.95f, 0.8f, 0.9f), Smooth(T_PULL, T_BLACK, t));
            foreach (var g in gcracks)
            {
                int n = p <= 0.001f ? 0 : Mathf.Clamp(Mathf.CeilToInt(p * (g.P.Length - 1)) + 1, 2, g.P.Length);
                bool on = n >= 2;
                if (g.L.enabled != on)
                    g.L.enabled = on;
                if (!on)
                {
                    g.Shown = -1;
                    continue;
                }
                g.L.startColor = col;
                g.L.endColor = col;
                if (n != g.Shown)
                {
                    g.Shown = n;
                    g.L.positionCount = n;
                    for (int k = 0; k < n; k++)
                        g.L.SetPosition(k, g.P[k]);
                }
            }
            if (paused || p <= 0.01f || gcracks.Count == 0)
                return;
            int c = Take(ref accE, 40f * p * Parts, dt);
            Vector3 bh = W(ZPC + new Vector3(0f, 35f, 0f));
            for (int q = 0; q < c; q++)
            {
                var g = gcracks[UnityEngine.Random.Range(0, gcracks.Count)];
                int k = UnityEngine.Random.Range(0, Mathf.Max(1, Mathf.Min(g.Shown, g.P.Length) - 1));
                Vector3 w = W(g.P[k]);
                Emit(
                    fx,
                    w,
                    (bh - w) / Rnd(2.5f, 4f) + Vector3.up * Rnd(10f, 30f) * U,
                    new Color(0.32f, 0.28f, 0.25f, 0.9f),
                    Rnd(2f, 7f) * U,
                    3.2f
                );
            }
        }

        void BuildAudio()
        {
            if (AstronomicalShowdown.boomClip == null)
                AstronomicalShowdown.MakeSfx();
            var g = new GameObject("EndSfx");
            g.transform.SetParent(root.transform, false);
            sfx = g.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;
            sfx.playOnAwake = false;
            sfx.priority = 0;
            var e = new GameObject("EndEngine");
            e.transform.SetParent(root.transform, false);
            engineSrc = e.AddComponent<AudioSource>();
            engineSrc.spatialBlend = 0f;
            engineSrc.playOnAwake = false;
            engineSrc.loop = true;
            engineSrc.volume = 0f;
            engineSrc.priority = 0;
            int n = 44100 * 4;
            var d = new float[n];
            var rnd = new System.Random(8);
            float lp = 0f,
                lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / 44100f;
                float w = (float)(rnd.NextDouble() * 2 - 1);
                lp += (w - lp) * 0.06f;
                lp2 += (lp - lp2) * 0.2f;
                d[i] =
                    Mathf.Clamp(
                        0.34f * Mathf.Sin(6.2832f * 31f * t)
                            + 0.22f * Mathf.Sin(6.2832f * 47f * t + 1f)
                            + 0.18f * Mathf.Sin(6.2832f * 71f * t + 2f)
                            + lp2 * 1.5f
                            + lp * 0.35f,
                        -1f,
                        1f
                    ) * 0.8f;
            }
            int rp = 0;
            var clip = AudioClip.Create(
                "EndEngine",
                n,
                1,
                44100,
                false,
                buf =>
                {
                    for (int q = 0; q < buf.Length; q++)
                    {
                        buf[q] = d[rp];
                        rp = (rp + 1) % n;
                    }
                }
            );
            owned.Add(clip);
            engineSrc.clip = clip;
        }

        // ------------------------------------------------------------------ flights
        Vector3 Orbit(int i, float t)
        {
            float a = i * Mathf.PI * 2f / 7f + 0.34f * (t - T_SEVEN);
            return new Vector3(
                C0.x + Mathf.Cos(a) * (280f + 25f * (i % 3)),
                230f + 35f * (i % 4) + 18f * Mathf.Sin(a * 2.1f + i),
                C0.z + Mathf.Sin(a) * (280f + 25f * (i % 3))
            );
        }

        Leg Bez(float t0, float t1, Vector3 a, Vector3 b, int ease, bool inn, bool outt, float lift)
        {
            var mid = (a + b) * 0.5f;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up) * ((b - a).magnitude * 0.18f);
            return new Leg
            {
                T0 = t0,
                T1 = t1,
                Kind = 0,
                Ease = ease,
                A = a,
                B = b,
                Ctl = mid + side + Vector3.up * lift,
                In = inn,
                Out = outt,
            };
        }

        void BuildFlights()
        {
            Vector3 rm = rifts[0].Pos,
                rh = rifts[1].Pos;
            var padC = PAD + new Vector3(0f, 1.2f + ROCKET_H * 0.5f, 0f);
            Leg mDive = Bez(T_DIVE6, T_HIT, rh, ZPC, 2, true, false, 0f);
            for (int i = 0; i < rockets.Count; i++)
            {
                var f = rockets[i];
                var L = f.Legs;
                int k = i % 6;
                if (i == 0)
                {
                    f.Hold = true;
                    f.HoldPos = padC;
                    L.Add(
                        new Leg
                        {
                            T0 = T_LIFT,
                            T1 = T_ENTER1 - 2f,
                            Kind = 0,
                            Ease = 3,
                            A = padC,
                            Ctl = new Vector3(PAD.x, 300f, PAD.z + 60f),
                            B = rm,
                            Out = true,
                        }
                    );
                    L.Add(Bez(68f, 73f, rifts[2].Pos, rifts[3].Pos, 1, true, true, 40f)); // reappears from one rift, flies to the next
                    L.Add(Bez(76f, 86f, rifts[4].Pos, Orbit(0, 86f), 1, true, false, 30f));
                    L.Add(
                        new Leg
                        {
                            T0 = 86f,
                            T1 = 126f,
                            Kind = 1,
                            A0 = 0,
                        }
                    );
                }
                else
                {
                    float ta = T_SEVEN + 0.45f * (i - 1),
                        tb = ta + 4.5f;
                    L.Add(Bez(ta, tb, rifts[2 + (i - 1) % 6].Pos, Orbit(i, tb), 1, true, false, 40f));
                    L.Add(
                        new Leg
                        {
                            T0 = tb,
                            T1 = 126f,
                            Kind = 1,
                            A0 = i,
                        }
                    );
                }
                L.Add(Bez(126f, T_SEVEN_IN - 0.8f + 0.3f * i, Orbit(i, 126f), rm, 1, false, true, 60f)); // all seven fly into the main rift at 2:12
                L.Add(Bez(T_AGAIN + 0.55f * i, T_AGAIN + 4.5f + 1.5f * i + 0.55f * i, rifts[2 + k].Pos, rm, 1, true, true, 50f)); // and again, one after the other, while the rift gets unstable
                if (i == 0)
                    L.Add(Bez(T_DIVE1, T_DIVE1 + 6f, rh, ZPC, 2, true, true, 0f));
                else
                { // the other six escort the meteor down (flying round it), then peel away and leave the sky
                    float a0 = (i - 1) * Mathf.PI / 3f;
                    var off = new Vector3(Mathf.Cos(a0) * 70f, Rnd(-15f, 25f), Mathf.Sin(a0) * 70f);
                    float t0 = T_DIVE6 + 0.3f * (i - 1),
                        t1 = T_HIT - 8f;
                    var esc = new Leg
                    {
                        T0 = t0,
                        T1 = t1,
                        Kind = 2,
                        Ref = mDive,
                        Off = off,
                        In = true,
                    };
                    L.Add(esc);
                    Vector3 away = new Vector3(off.x, 0f, off.z).normalized;
                    L.Add(Bez(t1, t1 + 11f, Eval(esc, t1), ZP + away * 650f + new Vector3(0f, 520f, 0f), 2, false, true, 0f));
                }
                L.Sort((x, y) => x.T0.CompareTo(y.T0));
            }
            if (meteor != null)
            {
                meteor.Legs.Add(Bez(T_METEOR, T_RIFT_GONE - 0.8f, METEOR0, rm, 2, false, true, 0f)); // frozen above the Zero Point until 2:49, then it moves into the rift
                meteor.Legs.Add(mDive); // and dives down at 3:28, hitting the Zero Point at 3:57
            }
        }

        float Ease(int k, float u, float dur)
        {
            switch (k)
            {
                case 1:
                    return Mathf.SmoothStep(0f, 1f, u);
                case 2:
                    return u * u;
                case 3:
                {
                    const float t0 = 4.5f;
                    float tau = u * dur;
                    return (tau - t0 * (1f - Mathf.Exp(-tau / t0))) / (dur - t0 * (1f - Mathf.Exp(-dur / t0)));
                }
                default:
                    return u;
            }
        }

        Vector3 Eval(Leg g, float t)
        {
            float u = Mathf.Clamp01((t - g.T0) / Mathf.Max(0.01f, g.T1 - g.T0));
            if (g.Kind == 1)
                return Orbit((int)g.A0, t);
            if (g.Kind == 2)
                return Eval(g.Ref, Mathf.Min(t, g.Ref.T1)) + g.Off;
            float e = Ease(g.Ease, u, g.T1 - g.T0);
            return Vector3.Lerp(Vector3.Lerp(g.A, g.Ctl, e), Vector3.Lerp(g.Ctl, g.B, e), e);
        }

        // centre, heading and size factor of a flyer at time t; false = not there
        bool Place(Flyer f, float t, out Vector3 pos, out Vector3 dir, out float k)
        {
            pos = Vector3.zero;
            dir = f.LastDir;
            k = 1f;
            Leg g = null;
            foreach (var l in f.Legs)
                if (t >= l.T0 && t <= l.T1)
                {
                    g = l;
                    break;
                }
            if (g == null)
            {
                if (f.Hold && f.Legs.Count > 0 && t < f.Legs[0].T0)
                {
                    pos = f.HoldPos;
                    dir = Vector3.up;
                    return true;
                }
                return false;
            }
            pos = Eval(g, t);
            Vector3 d = Eval(g, Mathf.Min(t + 0.08f, g.T1)) - Eval(g, Mathf.Max(t - 0.08f, g.T0));
            if (d.sqrMagnitude > 1e-6f)
                dir = d.normalized;
            f.LastDir = dir;
            if (g.In)
                k = Mathf.Min(k, Smooth(g.T0, g.T0 + 1.6f, t));
            if (g.Out)
                k = Mathf.Min(k, 1f - Smooth(g.T1 - 1.6f, g.T1, t));
            return k > 0.01f;
        }

        // ------------------------------------------------------------------ viewpoint: the spot at the gazebo floor, facing the middle of the island
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
            else if (fitOk)
            {
                o = fitO;
                y = fitY;
                sc = fitS;
            }
            else
                return false;
            Vector3 d = Quaternion.Euler(0, y, 0) * new Vector3(C0.x * sc, 0f, C0.z * sc);
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pos = o + Vector3.up * 1.2f;
            return true;
        }

        // ------------------------------------------------------------------ reactions
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
                float reach = Mathf.Max(80f, size * 6f * U);
                float k = Mathf.Clamp01(1f - d / reach);
                if (k < 0.03f)
                    return;
                float amp = Mathf.Clamp01(k * (kind == 2 ? 0.6f : 1.1f) * Mathf.Clamp(size / 40f, 0.25f, 1.2f));
                if (kind == 2)
                    reactCool = Time.unscaledTime + 0.3f;
                try
                {
                    var gt = GorillaTagger.Instance;
                    if (gt != null)
                    {
                        float dur = kind == 2 ? 0.16f : 0.4f;
                        gt.StartVibration(true, amp, dur);
                        gt.StartVibration(false, amp, dur);
                    }
                }
                catch { }
                if (AstronomicalShowdown.boomClip == null)
                    AstronomicalShowdown.MakeSfx();
                sfx.PlayOneShot(
                    kind == 2 ? AstronomicalShowdown.thumpClip : AstronomicalShowdown.boomClip,
                    Mathf.Clamp01(amp * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f))
                );
            }
            catch { }
        }

        void Shake(float power, float dt, float t)
        {
            var cam = Camera.main;
            if (cam == null || dt <= 0f)
                return;
            Vector3 focus = t < T_ENTER1 ? W(PAD) : W(ZP);
            float d = Vector3.Distance(cam.transform.position, focus);
            float k = Mathf.Clamp01(1f - d / Mathf.Max(100f, 900f * U));
            float amp = power * (0.2f + 0.8f * k) * (k > 0.02f ? 1f : 0f);
            rumbleT += dt;
            if (rumbleT >= 0.12f)
            {
                rumbleT = 0f;
                if (amp > 0.04f)
                {
                    try
                    {
                        var gt = GorillaTagger.Instance;
                        if (gt != null)
                        {
                            gt.StartVibration(true, Mathf.Clamp01(amp * 0.8f), 0.14f);
                            gt.StartVibration(false, Mathf.Clamp01(amp * 0.8f), 0.14f);
                        }
                    }
                    catch { }
                }
            }
            nudgeT += dt;
            if (nudgeT >= 0.5f)
            {
                nudgeT = 0f;
                if (power > 0.5f && k > 0.25f && !(t >= T_PULL && t < T_BLACK))
                {
                    var off = UnityEngine.Random.insideUnitCircle.normalized * 2f;
                    AstronomicalHazards.Blast(cam.transform.position + new Vector3(off.x, 0f, off.y), 40f, 1.1f + 2.6f * power * k, 3.2f);
                }
            }
            if (power > 0.3f && !paused && t < T_BLACK)
            {
                int n = Take(ref accE, 22f * power * k * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        cam.transform.position + UnityEngine.Random.insideUnitSphere * 7f + Vector3.up * 4f,
                        Vector3.down * Rnd(0.4f, 2f),
                        new Color(0.55f, 0.5f, 0.45f, 0.55f),
                        Rnd(0.05f, 0.2f),
                        2.2f
                    );
            }
            if (engineSrc != null)
            {
                float vol =
                    (Curve(ENGINE, t) * 0.55f + power * 0.3f)
                    * (0.25f + 0.75f * k)
                    * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f)
                    * 0.55f;
                if (t >= T_BLACK)
                {
                    vol =
                        (0.22f + 0.1f * Mathf.Sin(t * 0.7f))
                        * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f)
                        * (1f - Smooth(305f, 309.5f, t));
                    engineSrc.pitch = 0.55f;
                }
                else
                    engineSrc.pitch = 1f; // a low rumble in the black
                engineSrc.volume = Mathf.Clamp01(vol);
                if (vol > 0.01f && !engineSrc.isPlaying && !paused)
                    engineSrc.Play();
                else if (vol <= 0.01f && engineSrc.isPlaying)
                    engineSrc.Pause();
            }
        }

        // ------------------------------------------------------------------ per frame
        public void SetPaused(bool p)
        {
            paused = p;
            if (fx != null)
            {
                if (p)
                {
                    fx.Pause(true);
                    smoke.Pause(true);
                }
                else
                {
                    fx.Play(true);
                    smoke.Play(true);
                }
            }
            if (engineSrc != null && p)
                engineSrc.Pause();
        }

        public void Apply(float t, int i, float u, AstronomicalAudioManager au)
        {
            if (root == null)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            bool back = t < lastT - 0.4f;
            if (back || lastT < 0f)
            {
                cueIdx = 0;
                foreach (var b in bolts)
                    b.Age = 9f;
                foreach (var r in rings)
                    r.Age = 9f;
                skyT = -9f;
                voidSet = false;
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null && dome != null)
                dome.transform.position = cam.transform.position;
            pulse = Mathf.Max(0f, pulse - dt * 3.2f);
            shakeBump = Mathf.Max(0f, shakeBump - dt * 2.5f);
            zpPulse = Mathf.Max(0f, zpPulse - dt * 1.5f);
            float power = Mathf.Clamp01(Curve(SHAKE, t) + shakeBump);
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
            try
            {
                FlyersStep(t, dt, power);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Flyers: " + e.Message);
            }
            PadStep(t, dt, power);
            RiftsStep(t, dt, cam);
            ZeroPointStep(t, dt);
            GroundCracksStep(t, dt);
            try
            {
                BlackHoleStep(t, dt, cam);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Black hole: " + e.Message);
            }
            if (t - skyT > 0.08f || t < skyT)
            {
                skyT = t;
                UpdateSky(t);
            }
            float dk = Curve(DARK, t);
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.36f) * (1f - dk);
            UpdateBolts(dt);
            UpdateRings(dt);
            UpdateBooms(dt);
            if (t >= T_BACK && t < T_BACK + 4f)
                AstronomicalHazards.Blast(W(ZPC), 520f * U, 14f, 14f); // "you get pulled back farther" at 4:01
            Shake(power, dt, t);
            boomI = Mathf.Max(0f, boomI - dt * 14f);
            boomLight.intensity = boomI * Lit;
        }

        float boomI;

        // detailed props (Blender-made, animated): the Zero Point and the black hole
        GltfModel zpModel,
            bhModel;
        GameObject zpBubble;
        float zpHalf,
            bhHalf;
        Renderer[] bhRend = new Renderer[0];
        readonly List<Material> bhMats = new List<Material>();
        readonly Dictionary<Material, Shader> bhShader = new Dictionary<Material, Shader>();

        sealed class UvScroll
        {
            public Mesh M;
            public Vector2[] Base;
            public float Speed;
            public List<Vector2> Tmp = new List<Vector2>();
        }

        readonly List<UvScroll> scrolls = new List<UvScroll>();
        bool bhTop;

        void FlyersStep(float t, float dt, float power)
        {
            float tilt = power * 0.8f;
            for (int i = 0; i < rockets.Count; i++)
            {
                var f = rockets[i];
                Vector3 c,
                    dir;
                float k;
                bool vis = Place(f, t, out c, out dir, out k);
                if (vis != f.Vis)
                {
                    f.Vis = vis;
                    f.G.SetActive(vis);
                }
                if (i == 0)
                {
                    engineLight.intensity = 0f;
                }
                if (!vis)
                    continue;
                float thrust =
                    (i == 0 && t < T_LIFT) ? Curve(ENGINE, t)
                    : (i > 0 && t >= T_HIT - 8.5f && t <= T_HIT + 5f) ? 2.6f * k
                    : k; // the six fire their thrusters as they peel away from the meteor
                float sh = (i == 0 && t < T_LIFT + 12f) ? power : power * 0.15f;
                c += new Vector3(Noise(t * 31f + i, 0.3f), Noise(t * 27f + i, 5.1f) * 0.4f, Noise(t * 29f + i, 9.7f)) * 1.1f * sh;
                Vector3 b = c - dir * (f.Half * k);
                f.T.localPosition = b;
                f.T.localRotation =
                    Quaternion.FromToRotation(Vector3.up, dir)
                    * Quaternion.Euler(Noise(t * 23f + i, 2f) * 0.8f * sh, 0f, Noise(t * 19f + i, 7f) * 0.8f * sh);
                f.T.localScale = Vector3.one * rocketK * k;
                Vector3 bw = f.T.position,
                    down = -f.T.up;
                if (i == 0)
                {
                    engineLight.transform.position = bw + down * 6f * U;
                    engineLight.intensity = thrust * (7f + 2.5f * Mathf.PerlinNoise(t * 20f, 3f)) * Lit;
                }
                if (paused || t >= T_BLACK)
                    continue;
                if (thrust > 0.02f)
                {
                    int n = Take(ref f.AccA, (i == 0 ? 160f : 70f) * thrust * Parts, dt);
                    for (int q = 0; q < n; q++)
                    {
                        Vector3 src =
                            bw
                            + f.T.right * (UnityEngine.Random.value < 0.5f ? -2.7f : 2.7f) * U * k
                            + down * Rnd(0f, 6f) * U * k
                            + UnityEngine.Random.insideUnitSphere * 2.2f * U * k;
                        Color col = Color.Lerp(
                            new Color(1f, 0.95f, 0.8f, 0.95f),
                            new Color(1f, 0.45f, 0.1f, 0.8f),
                            UnityEngine.Random.value
                        );
                        Emit(
                            fx,
                            src,
                            down * Rnd(60f, 110f) * U * thrust + UnityEngine.Random.insideUnitSphere * 14f * U,
                            col,
                            Rnd(5f, 10f) * U * (0.6f + 0.4f * thrust) * k,
                            Rnd(0.3f, 0.6f)
                        );
                    }
                    int m = Take(ref f.AccB, (i == 0 ? 55f : 16f) * thrust * Parts, dt);
                    for (int q = 0; q < m; q++)
                        Emit(
                            smoke,
                            bw + down * 8f * U * k + UnityEngine.Random.insideUnitSphere * 3f * U,
                            down * Rnd(4f, 10f) * U + UnityEngine.Random.insideUnitSphere * 5f * U,
                            new Color(0.7f, 0.7f, 0.74f, 0.5f),
                            Rnd(10f, 18f) * U * k,
                            Rnd(6f, 12f)
                        );
                }
                if (i == 0 && t > T_IGN - 0.3f && t < T_LIFT + 6f) // steam rolling out over the pad
                {
                    int n = Take(ref accC, 90f * Mathf.Clamp01((t - T_IGN + 0.3f) * 2f) * Parts, dt);
                    for (int q = 0; q < n; q++)
                    {
                        float a = Rnd(0f, 6.2832f);
                        Vector3 d2 = plateauT.right * Mathf.Cos(a) + plateauT.forward * Mathf.Sin(a);
                        Emit(
                            smoke,
                            W(PAD + new Vector3(0f, 2f, 0f)) + d2 * Rnd(4f, 14f) * U,
                            d2 * Rnd(18f, 50f) * U + Vector3.up * Rnd(1f, 6f) * U,
                            new Color(0.88f, 0.9f, 0.94f, 0.5f),
                            Rnd(12f, 26f) * U,
                            Rnd(4f, 6.5f)
                        );
                    }
                }
            }
            // the meteor: frozen over the Zero Point (it trembles before it is sent into the rift), then the dive
            if (meteor == null)
                return;
            Vector3 mc,
                md;
            float mk;
            bool mv = Place(meteor, t, out mc, out md, out mk);
            if (mv != meteor.Vis)
            {
                meteor.Vis = mv;
                meteor.G.SetActive(mv);
            }
            if (!mv)
            {
                if (meteorLight != null)
                    meteorLight.intensity = 0f;
                return;
            }
            bool frozen = t < T_METEOR;
            Vector3 wob = frozen ? new Vector3(Mathf.Sin(t * 0.4f) * 3f, Mathf.Sin(t * 0.7f) * 4f, Mathf.Cos(t * 0.5f) * 3f) : Vector3.zero;
            float trem = Mathf.Clamp01((t - 160f) / 9f) * (frozen ? 1f : 0f);
            wob += new Vector3(Noise(t * 40f, 1f), Noise(t * 37f, 4f), Noise(t * 41f, 8f)) * 1.8f * trem;
            var rot = Quaternion.Euler(t * 3f, t * 5f + 20f, t * 2f);
            var mp = mc + wob - rot * (Vector3.up * meteor.Half * mk);
            meteor.T.localPosition = mp;
            meteor.T.localRotation = rot;
            meteor.T.localScale = Vector3.one * meteorK * mk;
            Vector3 mw = W(mc + wob);
            if (meteorLight != null)
            {
                meteorLight.transform.position = mw;
                meteorLight.intensity = (frozen ? 2.5f : 6f) * mk * Lit;
            }
            if (!paused && t < T_BLACK)
            {
                int n = Take(ref meteor.AccA, (frozen ? 14f : 120f) * mk * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        mw + UnityEngine.Random.onUnitSphere * METEOR_D * 0.45f * U,
                        frozen
                            ? UnityEngine.Random.insideUnitSphere * 3f * U
                            : plateauT.TransformDirection(-md) * Rnd(20f, 60f) * U + UnityEngine.Random.insideUnitSphere * 12f * U,
                        frozen
                            ? new Color(0.5f, 0.8f, 1f, 0.8f)
                            : Color.Lerp(new Color(1f, 0.9f, 0.7f, 0.95f), new Color(1f, 0.4f, 0.1f, 0.8f), UnityEngine.Random.value),
                        Rnd(3f, 9f) * U * mk,
                        frozen ? 2.5f : 1.2f
                    );
                if (!frozen)
                {
                    int m = Take(ref meteor.AccB, 30f * Parts, dt);
                    for (int q = 0; q < m; q++)
                        Emit(
                            smoke,
                            mw + UnityEngine.Random.insideUnitSphere * 10f * U,
                            plateauT.TransformDirection(-md) * Rnd(5f, 20f) * U,
                            new Color(0.6f, 0.55f, 0.52f, 0.5f),
                            Rnd(14f, 26f) * U,
                            Rnd(6f, 11f)
                        );
                }
            }
        }

        void PadStep(float t, float dt, float power)
        {
            shakeT.localPosition =
                new Vector3(Noise(t * 33f, 1.3f), Noise(t * 29f, 4.4f) * 0.4f, Noise(t * 37f, 8.8f)) * 0.7f * (t < 60f ? power : 0f);
            if (t < T_IGN)
                padLightMat.color = new Color(1f, 0.7f, 0.2f, (t % 1f) < 0.5f ? 1f : 0.12f);
            else
                padLightMat.color = new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(1f - (t - T_LIFT - 6f) / 8f));
            beaconMat.color = new Color(1f, 0.15f, 0.1f, t < 60f ? ((t * 2f) % 1f < 0.5f ? 1f : 0.2f) : 0.35f);
            float ang = 85f * Smooth(T_IGN + 0.8f, T_LIFT - 0.2f, t);
            for (int k = 0; k < arms.Length; k++)
                if (arms[k] != null)
                    arms[k].localRotation = Quaternion.Euler(0f, armSign[k] * ang, 0f);
            padLight.intensity = 2.6f * Lit * (1f - Smooth(T_LIFT, T_LIFT + 14f, t));
        }

        void RiftsStep(float t, float dt, Camera cam)
        {
            Rift lead = null;
            float lo = 0f;
            for (int q = 0; q < rifts.Count; q++)
            {
                var r = rifts[q];
                float open = Curve(r.Tab, t);
                bool main = q < 2;
                if (open > 0.01f)
                {
                    open *= 1f + 0.04f * Mathf.Sin(t * 1.7f + q) + 0.02f * Mathf.Sin(t * 5.3f);
                    if (q == 0)
                    {
                        float inst = Smooth(138f, T_METEOR, t);
                        open *= 1f + inst * (0.22f * Mathf.Sin(t * 13f) + 0.14f * Mathf.Sin(t * 31f + 1f));
                    } // "increasingly unstable"
                    open += pulse * 0.1f;
                }
                r.Open = open;
                bool on = open > 0.004f;
                if (r.T.gameObject.activeSelf != on)
                    r.T.gameObject.SetActive(on);
                if (!on)
                    continue;
                if (open > lo)
                {
                    lo = open;
                    lead = r;
                }
                if (cam != null)
                    r.T.rotation =
                        Quaternion.LookRotation(r.T.position - cam.transform.position, Vector3.up) * Quaternion.Euler(0f, 0f, 9f);
                float H = r.Size * open,
                    Wd = H * 0.34f;
                r.Tear.localScale = new Vector3(Wd, H, 1f);
                r.Halo.localScale = new Vector3(H * 1.9f, H * 2.3f, 1f);
                r.Halo.localPosition = new Vector3(0f, 0f, 2f);
                r.HaloMat.color = new Color(0.55f, 0.3f, 1f, Mathf.Clamp01(open * 0.7f + pulse * 0.25f));
                if (t - arcT > 0.07f || t < arcT)
                {
                    int live = Mathf.Min(r.Arcs.Length, (int)((2 + open * 6f) * Mathf.Min(1f, Parts)));
                    for (int k = 0; k < r.Arcs.Length; k++)
                    {
                        var l = r.Arcs[k];
                        if (k >= live || UnityEngine.Random.value < 0.25f)
                        {
                            l.enabled = false;
                            continue;
                        }
                        float a = Rnd(0f, 6.2832f);
                        Vector3 ctr = new Vector3(Mathf.Cos(a) * 0.5f * Wd, Mathf.Sin(a) * 0.5f * H, 0f),
                            dir = new Vector3(Mathf.Cos(a) * Wd, Mathf.Sin(a) * H, 0f).normalized;
                        l.enabled = true;
                        l.widthMultiplier = Rnd(1.2f, 3.2f) * (0.5f + open);
                        Vector3 p = ctr;
                        l.SetPosition(0, p);
                        for (int w = 1; w < 7; w++)
                        {
                            p += dir * Rnd(7f, 15f) * (0.6f + open) + new Vector3(Rnd(-6f, 6f), Rnd(-6f, 6f), 0f);
                            l.SetPosition(w, p);
                        }
                    }
                }
                if (!paused && t < T_BLACK)
                {
                    int n = Take(ref accD, (q < 2 ? 1f : 0.35f) * (30f + 100f * Mathf.Clamp01(open)) * Parts, dt);
                    for (int w = 0; w < n; w++)
                    {
                        float a = Rnd(0f, 6.2832f),
                            rho = Rnd(0.55f, 1.5f);
                        Vector3 lp = new Vector3(Mathf.Cos(a) * 0.5f * Wd * rho, Mathf.Sin(a) * 0.5f * H * rho, Rnd(-4f, 4f));
                        Vector3 wp = r.T.TransformPoint(lp),
                            wc = r.T.position;
                        Emit(
                            fx,
                            wp,
                            (wc - wp) / 1.2f,
                            Color.Lerp(new Color(0.7f, 0.5f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.9f), UnityEngine.Random.value),
                            Rnd(2f, 6f) * U,
                            1.2f
                        );
                    }
                }
            }
            if (t - arcT > 0.07f || t < arcT)
                arcT = t;
            if (lead != null)
            {
                riftLight.transform.position = lead.T.position;
                riftLight.intensity = Mathf.Clamp01(lo) * (3.5f + 3f * pulse + 0.8f * Mathf.Sin(t * 3f)) * Lit;
            }
            else
                riftLight.intensity = 0f;
        }

        void ZeroPointStep(float t, float dt)
        {
            if (zpModel != null && zpModel.Pivot != null)
            {
                bool zv = t < T_HIT + 0.2f;
                if (zpModel.Pivot.activeSelf != zv)
                    zpModel.Pivot.SetActive(zv);
                if (zpBubble != null && zpBubble.activeSelf != (t < T_HIGH))
                    zpBubble.SetActive(t < T_HIGH);
                float kk = (13f + 4f * (t >= T_HIGH ? 1f : 0f)) * (1f + 0.04f * Mathf.Sin(t * 3f) + 0.3f * zpPulse);
                var zc = Camera.main;
                if (zc != null)
                {
                    Vector3 fz = zc.transform.position - W(ZPC);
                    fz.y = 0f;
                    if (fz.sqrMagnitude > 0.01f)
                        zpModel.Pivot.transform.rotation = Quaternion.LookRotation(fz.normalized, Vector3.up);
                }
                zpModel.Pivot.transform.localScale = Vector3.one * kk;
                zpModel.Pivot.transform.position = W(ZPC) - zpModel.Pivot.transform.up * (zpHalf * kk * U);
                if (zpModel.Clips.Count > 0)
                    zpModel.Sample(zpModel.Clips[0], t);
                if (vaultModel != null)
                {
                    if (vaultModel.Clips.Count > 0)
                        vaultModel.Sample(vaultModel.Clips[0], t);
                    if (vaultModel.Pivot.activeSelf != (t < T_BLACK))
                        vaultModel.Pivot.SetActive(t < T_BLACK);
                }
                bool bm2 = t >= T_HIGH && t < T_HIT;
                if (beam.gameObject.activeSelf != bm2)
                    beam.gameObject.SetActive(bm2);
                zpLight.intensity = (zv ? (2.5f + 2f * (t >= T_HIGH ? 1f : 0f) + 6f * zpPulse + 0.6f * Mathf.Sin(t * 3f)) : 0f) * Lit;
                if (!paused && t < T_BLACK && zv)
                {
                    int n = Take(ref accF, 18f * Parts, dt);
                    for (int q = 0; q < n; q++)
                        Emit(
                            fx,
                            W(ZPC + new Vector3(Rnd(-8f, 8f), 0f, Rnd(-8f, 8f))),
                            new Vector3(Rnd(-3f, 3f), Rnd(8f, 22f), Rnd(-3f, 3f)) * U,
                            new Color(0.6f, 0.95f, 1f, 0.85f),
                            Rnd(1.5f, 4f) * U,
                            2.2f
                        );
                }
                return;
            }
            bool bubble = t < T_HIGH && t < T_HIT;
            float shat = Mathf.Clamp01((t - shatterT) / 2f);
            if (zpDome.gameObject.activeSelf != bubble)
                zpDome.gameObject.SetActive(bubble);
            domeMat.color = new Color(0.4f, 0.8f, 1f, 0.1f + 0.05f * Mathf.Sin(t * 2f) + 0.06f * pulse);
            float expose = t >= T_HIGH ? 1f : 0f;
            float vis = t < T_HIT + 0.2f ? 1f : 0f;
            if (zpOrb.gameObject.activeSelf != (vis > 0f))
                zpOrb.gameObject.SetActive(vis > 0f);
            float r = (9f + 5f * expose) * (1f + 0.08f * Mathf.Sin(t * 3f) + 0.35f * zpPulse);
            zpOrb.localScale = Vector3.one * r * 2f;
            orbMat.color = new Color(0.55f, 0.9f, 1f, 0.75f + 0.25f * zpPulse);
            bool bm = t >= T_HIGH && t < T_HIT;
            if (beam.gameObject.activeSelf != bm)
                beam.gameObject.SetActive(bm);
            zpLight.intensity = (vis > 0f ? (2.5f + 2f * expose + 6f * zpPulse + 0.6f * Mathf.Sin(t * 3f)) : 0f) * Lit;
            if (!paused && t < T_BLACK && vis > 0f)
            {
                int n = Take(ref accF, 18f * (1f + expose) * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        W(ZPC + new Vector3(Rnd(-8f, 8f), 0f, Rnd(-8f, 8f))),
                        new Vector3(Rnd(-3f, 3f), Rnd(8f, 22f), Rnd(-3f, 3f)) * U,
                        new Color(0.6f, 0.95f, 1f, 0.85f),
                        Rnd(1.5f, 4f) * U,
                        2.2f
                    );
            }
        }

        // black hole: grows over the impact point, pulls things in (and you); at 4:37 the screen goes black; at 4:56 a black hole is all there is
        void BlackHoleStep(float t, float dt, Camera cam)
        {
            bool inVoid = t >= T_BLACK;
            if (bhModel != null && !inVoid && bhTop)
                bhHalo(false);
            if (inVoid != voidMode)
            {
                voidMode = inVoid;
                fxRend.sharedMaterial = voidMode ? matGlowTop : matGlow;
                foreach (var l in bhRings)
                    l.sharedMaterial.shader = (voidMode ? matLineTop : matLine).shader;
                foreach (var l in bhRings)
                {
                    l.sharedMaterial.renderQueue = voidMode ? 4003 : 3003;
                    if (voidMode)
                        l.sharedMaterial.SetInt("unity_GUIZTestMode", 8);
                }
                bhHaloMat.shader = (voidMode ? matGlowTop : matGlow).shader;
                bhHaloMat.renderQueue = voidMode ? 4001 : 2998;
                if (voidMode)
                    bhHaloMat.SetInt("unity_GUIZTestMode", 8);
            }
            // the black screen
            float sa = Mathf.Clamp01((t - T_BLACK) / 0.6f) * (1f - Mathf.Clamp01((t - 307.6f) / 1.9f));
            bool so = sa > 0.001f;
            if (voidObj.activeSelf != so)
                voidObj.SetActive(so);
            if (so)
            {
                voidMat.color = new Color(0f, 0f, 0f, sa);
                if (cam != null)
                    voidObj.transform.position = cam.transform.position;
            }
            float rW,
                alpha;
            Vector3 pos;
            if (!inVoid)
            {
                rW = Curve(BHR, t) * U;
                alpha = Smooth(239f, 246f, t);
                pos = W(ZPC + new Vector3(0f, 35f, 0f));
                if (bhModel == null && bhBall.activeSelf != (rW > 0.5f))
                    bhBall.SetActive(rW > 0.5f);
                bhBall.transform.position = pos;
                bhBall.transform.localScale = Vector3.one * rW * 2f;
                if (t >= T_PULL && !paused)
                    AstronomicalHazards.Pull(pos, 1400f * U, 5f + 28f * Smooth(T_PULL, T_BLACK, t), 24f);
            }
            else
            {
                if (bhBall.activeSelf)
                    bhBall.SetActive(false);
                if (!voidSet && cam != null)
                {
                    Vector3 f = cam.transform.forward;
                    f.y = 0f;
                    if (f.sqrMagnitude < 0.01f)
                        f = root.transform.forward;
                    voidPos = cam.transform.position + f.normalized * 150f * U + Vector3.up * 25f * U;
                    voidSet = true;
                }
                rW = 60f * U * (0.6f + 0.4f * Smooth(T_BH, T_BH + 6f, t));
                pos = voidPos;
                alpha = Smooth(T_BH, T_BH + 4f, t) * (1f - Smooth(306.5f, 308.3f, t));
            }
            bool on = alpha > 0.003f;
            bhLight.intensity = on && !inVoid ? 4f * alpha * Lit : 0f;
            bhLight.transform.position = pos;
            if (bhModel != null)
            {
                BhModelStep(t, pos, rW, alpha, on, inVoid, cam);
                if (!on)
                    return;
            }
            else
            {
                if (haloBh.gameObject.activeSelf != on)
                    haloBh.gameObject.SetActive(on);
                foreach (var l in bhRings)
                    if (l.enabled != on)
                        l.enabled = on;
                if (!on)
                    return;
                if (cam != null)
                    haloBh.rotation = Quaternion.LookRotation(haloBh.position - cam.transform.position, Vector3.up);
                haloBh.position = pos;
                haloBh.localScale = Vector3.one * rW * 4.4f;
                bhHaloMat.color = new Color(0.75f, 0.85f, 1f, alpha * (0.55f + 0.1f * Mathf.Sin(t * 4f)));
                for (int j = 0; j < bhRings.Length; j++)
                {
                    var l = bhRings[j];
                    float rr = rW * (1.45f + 0.5f * j);
                    float ph = t * (0.9f - 0.25f * j) + j * 2f;
                    Vector3 axis = Quaternion.Euler(70f - 12f * j, j * 55f, 0f) * Vector3.up,
                        a1 = Vector3.Cross(axis, Vector3.forward).normalized,
                        a2 = Vector3.Cross(axis, a1);
                    Color c =
                        j == 0 ? new Color(1f, 0.75f, 0.4f, alpha)
                        : j == 1 ? new Color(1f, 1f, 1f, alpha)
                        : new Color(0.5f, 0.75f, 1f, alpha * 0.8f);
                    l.startColor = c;
                    l.endColor = c;
                    l.widthMultiplier = rW * 0.14f * (1f - 0.2f * j);
                    for (int k = 0; k < 72; k++)
                    {
                        float a = k * Mathf.PI * 2f / 71f + ph;
                        l.SetPosition(k, pos + (a1 * Mathf.Cos(a) + a2 * Mathf.Sin(a)) * rr);
                    }
                }
            }
            if (paused)
                return;
            if (!inVoid && t >= T_PULL)
            { // everything is pushed in: dust, rock and light spiral into the hole
                int n = Take(ref accA, (60f + 220f * Smooth(T_PULL, T_BLACK, t)) * Parts, dt);
                for (int q = 0; q < n; q++)
                {
                    float a = Rnd(0f, 6.2832f),
                        rd = Rnd(90f, 300f) * U;
                    Vector3 p = W(ZP) + new Vector3(Mathf.Cos(a) * rd, Rnd(0f, 90f) * U, Mathf.Sin(a) * rd);
                    Vector3 to = (pos - p),
                        tang = Vector3.Cross(to.normalized, Vector3.up) * rd * 0.35f;
                    Emit(
                        fx,
                        p,
                        to / 2.4f + tang / 2.4f,
                        Color.Lerp(new Color(0.45f, 0.4f, 0.35f, 0.7f), new Color(0.7f, 0.85f, 1f, 0.9f), UnityEngine.Random.value),
                        Rnd(2f, 7f) * U,
                        2.4f
                    );
                }
            }
            else if (inVoid && t >= T_BH)
            {
                int n = Take(ref accA, 130f * alpha * Parts, dt);
                for (int q = 0; q < n; q++)
                {
                    float a = Rnd(0f, 6.2832f),
                        rd = Rnd(1.6f, 3.2f) * rW;
                    Vector3 p = pos + new Vector3(Mathf.Cos(a) * rd, Rnd(-0.25f, 0.25f) * rd, Mathf.Sin(a) * rd);
                    Vector3 to = pos - p,
                        tang = Vector3.Cross(to.normalized, Vector3.up) * rd * 0.5f;
                    Emit(
                        fx,
                        p,
                        to / 2.8f + tang / 2.8f,
                        Color.Lerp(new Color(1f, 0.8f, 0.5f, 0.9f), new Color(0.6f, 0.8f, 1f, 0.9f), UnityEngine.Random.value),
                        Rnd(1.5f, 4f) * U,
                        2.8f
                    );
                }
            }
        }

        // ------------------------------------------------------------------ cues
        Vector3 At(JNode n)
        {
            string s = (string)n;
            if (s == null)
                return n != null && n.Count >= 3 ? W(new Vector3((float)n[0], (float)n[1], (float)n[2])) : W(PAD);
            if (s == "rift")
                return rifts[0].T.position;
            if (s == "rifth")
                return rifts[1].T.position;
            if (s == "zp")
                return W(ZPC);
            if (s == "meteor")
                return W(METEOR0);
            if (s == "bh")
                return W(ZPC + new Vector3(0f, 35f, 0f));
            if (s.Length == 2 && s[0] == 'r' && s[1] >= '2' && s[1] <= '7')
                return rifts[s[1] - '0'].T.position;
            return W(PAD + new Vector3(0f, 4f, 0f));
        }

        void RunEvent(Cue c, float t)
        {
            var n = c.N;
            float size = n["size"] != null ? (float)n["size"] : 10f;
            Color col = Col(n["color"] != null ? (string)n["color"] : "white");
            switch (c.Type)
            {
                case "flash":
                    Flash(At(n["at"]), size, col);
                    break;
                case "boom":
                {
                    Vector3 p = At(n["at"]);
                    Boom(p, size, col, true);
                    if (size >= 20f)
                        React(p, size, 0);
                    break;
                }
                case "dust":
                    Dust(At(n["at"]), size);
                    break;
                case "shock":
                    Shock((string)n["kind"], (string)n["at"], size, col);
                    break;
                case "bolt":
                    Strike(c.T, n["s"] != null ? (float)n["s"] : 0.3f, col, (string)n["from"]);
                    break;
                case "zphit":
                    zpPulse = 1f;
                    Flash(W(ZPC), size, col);
                    break;
                case "shatter":
                    shatterT = t;
                    Burst(W(ZPC), (int)(90 * Parts), new Color(0.5f, 0.9f, 1f, 0.8f), 60f * U, 10f * U, 6f * U, 2.4f);
                    Flash(W(ZP), 120f, col);
                    break;
                case "pulse":
                {
                    float s = n["s"] != null ? (float)n["s"] : 0.2f;
                    pulse = Mathf.Max(pulse, Mathf.Clamp01(s * 2.4f));
                    shakeBump = Mathf.Max(shakeBump, 0.1f + s * 0.4f);
                    boomI = Mathf.Max(boomI, 2f + s * 10f);
                    boomLight.transform.position = t < T_HIGH ? rifts[0].T.position : rifts[1].T.position;
                    boomLight.color = new Color(0.7f, 0.5f, 1f);
                    React(W(PAD), 20f + s * 80f, 2);
                    break;
                }
            }
        }

        void Flash(Vector3 p, float size, Color c)
        {
            boomI = Mathf.Max(boomI, 10f + size * 0.2f);
            boomLight.transform.position = p;
            boomLight.color = c;
            Burst(p, 40, c, size * 0.5f * U, 0f, size * 0.12f * U, 1.2f);
        }

        void Dust(Vector3 p, float size)
        {
            Burst(
                new Vector3(p.x, plateauT.position.y + 2f * U, p.z),
                (int)(size * 14f * Parts),
                new Color(0.45f, 0.4f, 0.35f, 0.55f),
                size * 3.2f * U,
                size * 0.6f * U,
                size * 1.6f * U,
                4.5f
            );
        }

        void Boom(Vector3 p, float size, Color c, bool knock)
        {
            int k = boomNext;
            boomNext = (boomNext + 1) % boomSphere.Length;
            boomAge[k] = 0f;
            boomSize[k] = size;
            boomSphere[k].position = p;
            boomSphere[k].gameObject.SetActive(true);
            boomMat[k].color = c;
            Burst(p, (int)(Mathf.Clamp(size * 3f, 20f, 200f) * Parts), c, size * 0.9f * U, size * 0.4f * U, size * 0.12f * U, 2.4f);
            boomI = Mathf.Max(boomI, 6f + size * 0.25f);
            boomLight.transform.position = p + Vector3.up * size * 0.2f * U;
            boomLight.color = c;
            if (knock)
                AstronomicalHazards.Blast(p, size * 2.4f * U, 4f + size * 0.4f);
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
                c.a = (1f - f) * (1f - f) * 0.8f;
                boomMat[k].color = c;
            }
        }

        void Shock(string kind, string at, float size, Color c)
        {
            var r = rings[ringNext];
            ringNext = (ringNext + 1) % rings.Length;
            r.Age = 0f;
            r.Col = c;
            r.R = size * U;
            r.L.enabled = true;
            if (kind == "sky")
            {
                var rf =
                    at == "rifth" ? rifts[1]
                    : at == "main" || at == "rift" ? rifts[0]
                    : (rifts[1].Open > rifts[0].Open ? rifts[1] : rifts[0]);
                r.C = rf.T.position;
                r.U = rf.T.right;
                r.V = rf.T.up;
                r.Life = 2.4f;
            }
            else
            {
                Vector3 ctr = at == "zp" ? ZP : PAD;
                r.C = W(ctr + new Vector3(0f, 1.5f, 0f));
                r.U = plateauT.right;
                r.V = plateauT.forward;
                r.Life = 1.7f;
                Burst(r.C, (int)(40 * Parts), new Color(0.6f, 0.55f, 0.5f, 0.5f), size * 0.5f * U, 2f * U, 6f * U, 3f);
            }
            React(r.C, Mathf.Min(size, 160f) * 0.6f, 2);
        }

        void UpdateRings(float dt)
        {
            foreach (var r in rings)
            {
                if (!r.L.enabled)
                    continue;
                r.Age += dt;
                float f = r.Age / r.Life;
                if (f >= 1f)
                {
                    r.L.enabled = false;
                    continue;
                }
                float rad = r.R * (1f - (1f - f) * (1f - f));
                var col = new Color(r.Col.r, r.Col.g, r.Col.b, (1f - f) * 0.9f);
                r.L.startColor = col;
                r.L.endColor = col;
                r.L.widthMultiplier = (4f + r.R * 0.03f / U) * U * (1f - f * 0.7f);
                for (int k = 0; k < 64; k++)
                {
                    float a = k * Mathf.PI * 2f / 63f;
                    r.L.SetPosition(k, r.C + (r.U * Mathf.Cos(a) + r.V * Mathf.Sin(a)) * rad);
                }
            }
        }

        // lightning from an open rift to the ground around the Zero Point
        void Strike(float seed, float s, Color col, string from)
        {
            Rift rf =
                from == "rifth" ? rifts[1]
                : from == "main" ? rifts[0]
                : (rifts[1].Open > rifts[0].Open ? rifts[1] : rifts[0]);
            if (!rf.T.gameObject.activeSelf)
                return;
            var b = bolts[boltNext];
            boltNext = (boltNext + 1) % bolts.Length;
            var rnd = new System.Random((int)(seed * 100f) + 7);
            Vector3 a =
                rf.T.position
                + rf.T.right * ((float)rnd.NextDouble() - 0.5f) * 60f * U
                + rf.T.up * ((float)rnd.NextDouble() - 0.5f) * 120f * U;
            float ga = (float)rnd.NextDouble() * 6.2832f,
                gr = 22f + (float)rnd.NextDouble() * 52f;
            Vector3 g = Snap(W(new Vector3(ZP.x + Mathf.Cos(ga) * gr, 0f, ZP.z + Mathf.Sin(ga) * gr)));
            int N = 18;
            Vector3 axis = (g - a).normalized,
                p1 = Vector3.Cross(axis, Vector3.up).normalized,
                p2 = Vector3.Cross(axis, p1).normalized;
            b.Core.positionCount = N + 1;
            b.Glow.positionCount = N + 1;
            for (int k = 0; k <= N; k++)
            {
                float f = k / (float)N,
                    amp = Mathf.Sin(f * Mathf.PI) * 30f * U;
                Vector3 p =
                    Vector3.Lerp(a, g, f)
                    + (
                        k == 0 || k == N
                            ? Vector3.zero
                            : p1 * ((float)rnd.NextDouble() * 2f - 1f) * amp + p2 * ((float)rnd.NextDouble() * 2f - 1f) * amp
                    );
                b.Core.SetPosition(k, p);
                b.Glow.SetPosition(k, p);
            }
            b.Age = 0f;
            b.Life = 0.3f + 0.4f * s;
            b.Core.enabled = b.Glow.enabled = true;
            b.Core.startColor = b.Core.endColor = Color.white;
            Color gc = new Color(col.r, col.g, col.b, 0.7f);
            b.Glow.startColor = b.Glow.endColor = gc;
            b.L.transform.position = g + Vector3.up * 8f * U;
            b.L.color = col;
            b.L.intensity = 8f * Lit;
            float size = 14f + s * 40f;
            Boom(g, size, col, true);
            Dust(g, 3f + s * 6f);
            React(g, size * 1.4f, 0);
        }

        // lightning ends on whatever is really there (tree tops, rocks, the floor): ray from high above down to the first collider
        Vector3 Snap(Vector3 w)
        {
            RaycastHit h;
            return Physics.Raycast(w + Vector3.up * 200f * U, Vector3.down, out h, 400f * U, ~0, QueryTriggerInteraction.Ignore)
                ? h.point
                : w;
        }

        public string DebugInfo()
        {
            int fl = 0,
                ro = 0,
                bo = 0;
            foreach (var f in rockets)
                if (f.Vis)
                    fl++;
            foreach (var r in rifts)
                if (r.Open > 0.004f)
                    ro++;
            foreach (var b in bolts)
                if (b.Core.enabled)
                    bo++;
            return "rockets "
                + fl
                + "  meteor "
                + (meteor != null && meteor.Vis ? "on" : "off")
                + "  rifts "
                + ro
                + "  bolts "
                + bo
                + "  void "
                + voidMode
                + "\nparticles "
                + (fx != null ? fx.particleCount : 0)
                + "+"
                + (smoke != null ? smoke.particleCount : 0)
                + "  shake "
                + Curve(SHAKE, lastT).ToString("0.00")
                + "  props "
                + (zpModel != null)
                + "/"
                + (bhModel != null)
                + "/"
                + (vaultModel != null);
        }

        void UpdateBolts(float dt)
        {
            foreach (var b in bolts)
            {
                if (!b.Core.enabled)
                    continue;
                b.Age += dt;
                float f = b.Age / b.Life;
                if (f >= 1f)
                {
                    b.Core.enabled = b.Glow.enabled = false;
                    b.L.intensity = 0f;
                    continue;
                }
                float fl = (Mathf.Sin(b.Age * 90f) > -0.3f ? 1f : 0.35f) * (1f - f);
                b.Core.widthMultiplier = 1.8f * U * fl + 0.2f;
                b.Glow.widthMultiplier = 9f * U * fl + 0.5f;
                b.L.intensity = 8f * fl * Lit;
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
            try
            {
                for (int i = 0; i < rockets.Count; i++)
                {
                    var f = rockets[i];
                    if (f.G == null)
                        continue;
                    if (i == 0)
                    {
                        f.T.SetParent(null, false);
                        f.G.SetActive(false);
                        AstronomicalEventMod.ParkModel(f.G);
                    }
                    else
                        UnityEngine.Object.Destroy(f.G);
                }
                if (zpModel != null && zpModel.Pivot != null)
                {
                    zpModel.Pivot.transform.SetParent(null, false);
                    zpModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(zpModel.Pivot);
                }
                if (vaultModel != null && vaultModel.Pivot != null)
                {
                    vaultModel.Pivot.transform.SetParent(null, false);
                    vaultModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(vaultModel.Pivot);
                }
                if (bhModel != null && bhModel.Pivot != null)
                {
                    bhHalo(false);
                    bhModel.Pivot.transform.SetParent(null, false);
                    bhModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(bhModel.Pivot);
                }
                if (meteor != null && meteor.G != null)
                {
                    meteor.T.SetParent(null, false);
                    meteor.G.SetActive(false);
                    AstronomicalEventMod.ParkModel(meteor.G);
                }
            }
            catch { }
            rockets.Clear();
            meteor = null;
            rifts.Clear();
            gcracks.Clear();
            scrolls.Clear();
            bhMats.Clear();
            bhShader.Clear();
            zpModel = null;
            bhModel = null;
            vaultModel = null;
            zpBubble = null;
            bhTop = false;
            try
            {
                if (engineSrc != null)
                    engineSrc.Stop();
            }
            catch { }
            if (dome != null)
                UnityEngine.Object.Destroy(dome);
            dome = null;
            if (root != null)
                UnityEngine.Object.Destroy(root);
            root = null;
            foreach (var o in owned)
                if (o != null)
                    UnityEngine.Object.Destroy(o);
            owned.Clear();
            events.Clear();
            lastT = -1f;
            skyMesh = null;
            engineSrc = null;
            sfx = null;
            fx = null;
            smoke = null;
        }
    }
}
