using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "Rocket Launch (2018)": a large rocket on a launch pad lights up, lifts off and climbs into the night sky; the sky cracks, a rift tears open and swallows it, the rift
    // swells to the beat of the soundtrack, lightning rains on the map and everything shakes, until the rift collapses. A static model plus procedural effects (no baked
    // animation): every state is a pure function of the music time, so a late joiner or a seek lands in the right moment. cues.json = timeline anchors ("tl"), one-shot
    // cues (flash / boom / dust / shock / bolt / pulse, generated from the audio hits by tools/build_rocket.py) and the arena points that are fitted onto the Forest's floor.
    // The map itself is never moved: "the map shakes" is controller rumble + small nudges (the existing Knockback setting) + a shaking pad / rocket + dust falling around you.
    internal sealed class AstronomicalRocket : IEventScene, IDebug
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

        sealed class Crack
        {
            public LineRenderer Core,
                Glow;
            public Vector3[] P;
            public float Speed;
            public int Shown = -1;
        }

        // timeline anchors (seconds of the music); cues.json "tl" can move them
        float tIgn = 7.5f,
            tLift = 10.4f,
            tCrack = 34.9f,
            tEnter = 51.6f,
            tBlast = 79.5f,
            tClose = 120.3f,
            tEnd = 124f;

        // piecewise-linear curves of the music time: (t, value) pairs
        static readonly float[] SHAKE =
        {
            0,
            0,
            6.4f,
            0,
            7.5f,
            0.55f,
            10.4f,
            0.9f,
            14,
            0.65f,
            24,
            0.35f,
            34.5f,
            0.2f,
            34.9f,
            0.55f,
            42,
            0.35f,
            51.2f,
            0.5f,
            51.6f,
            0.95f,
            54,
            0.12f,
            72,
            0.12f,
            77,
            0.6f,
            79.5f,
            1f,
            83,
            0.5f,
            106,
            0.4f,
            107.4f,
            0.6f,
            118.6f,
            0.95f,
            118.67f,
            1f,
            120.8f,
            1f,
            123,
            0.15f,
            126,
            0f,
            300,
            0,
        };
        static readonly float[] RIFT =
        {
            0,
            0,
            34.9f,
            0,
            36,
            0.05f,
            51.4f,
            0.15f,
            51.6f,
            0.18f,
            53.2f,
            0.5f,
            73,
            0.52f,
            79.4f,
            0.75f,
            79.6f,
            0.85f,
            81.5f,
            1f,
            107.3f,
            1f,
            107.4f,
            1.05f,
            118.6f,
            1.25f,
            118.7f,
            1.5f,
            120.2f,
            1.4f,
            121.4f,
            0f,
            300,
            0,
        };
        static readonly float[] CRACK =
        {
            0,
            0,
            34.9f,
            0,
            51.5f,
            0.5f,
            56,
            0.62f,
            73,
            0.68f,
            79.5f,
            0.72f,
            82,
            1f,
            120.3f,
            1f,
            122.6f,
            0f,
            300,
            0,
        };
        static readonly float[] SKYGLOW =
        {
            0,
            0,
            34.9f,
            0,
            51.6f,
            0.35f,
            56,
            0.5f,
            73,
            0.55f,
            79.5f,
            0.9f,
            82,
            0.8f,
            107,
            0.9f,
            118.6f,
            1.2f,
            120.3f,
            1f,
            124,
            0f,
            300,
            0,
        };
        static readonly float[] ENGINE = { 0, 0, 7.4f, 0, 7.7f, 0.6f, 10.4f, 1f, 51.6f, 1f, 53.5f, 0f, 300, 0 };

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

        static float Smooth(float a, float b, float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
        }

        static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }

        // layout, in the show's local units (x right, y up, z forward; the viewer's spot is the origin)
        static readonly Vector3 PAD = new Vector3(0f, 0f, 110f),
            RIFT_C = new Vector3(0f, 330f, 400f);
        const float TOWER_H = 80f,
            ARM_LEN = 10f;

        GameObject root,
            dome;
        Transform plateauT,
            shakeT,
            riftT,
            tearT,
            haloT,
            crackRoot;
        Transform[] arms = new Transform[4];
        float[] armSign = new float[4];
        GltfModel rocketModel;
        float rocketK = 1f,
            rocketH = 150f;
        bool rocketVis = true;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Cue> events = new List<Cue>();
        Material matSprite,
            matGlow,
            matLine,
            haloMat,
            padLightMat,
            beaconMat,
            coreMat,
            glowLineMat,
            tearMat;
        Texture2D glowTex,
            lineTex;
        ParticleSystem fx,
            smoke;
        Light boomLight,
            engineLight,
            padLight,
            riftLight;
        float boomI;
        Transform[] boomSphere;
        Material[] boomMat;
        float[] boomAge,
            boomSize;
        int boomNext;
        Bolt[] bolts = new Bolt[4];
        int boltNext;
        Ring[] rings = new Ring[6];
        int ringNext;
        readonly List<Crack> cracks = new List<Crack>();
        LineRenderer[] arcs = new LineRenderer[10];
        float arcT;
        Mesh skyMesh;
        Color[] skyCols,
            skyBase;
        Vector3[] skyDirs;
        int skyGrad;
        float skyT = -9f;
        AudioSource sfx,
            engineSrc;
        float reactCool,
            rumbleT,
            nudgeT;

        // rocket path: a Bezier from the pad to the rift, driven by a smooth acceleration
        Vector3[] pathP = new Vector3[81];
        float[] pathS = new float[81];
        float pathL,
            vmax,
            tau0 = 4.5f;
        Vector3 p0,
            p3,
            endDir;
        float lastT = -1f,
            pulse,
            shakeBump,
            accA,
            accB,
            accC,
            accD,
            accE;
        int cueIdx;
        bool paused;
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
            var g = new GameObject("RocketLight");
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

        // soft across the line, constant along it
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

        // ------------------------------------------------------------------ build
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yaw)
        {
            def = AstronomicalEvents.Selected;
            scale = Mathf.Max(0.2f, AstronomicalSettings.ShowScale.Value);
            var tl = def.Cues != null ? def.Cues["tl"] : null;
            if (tl != null)
            {
                Rd(tl, "ign", ref tIgn);
                Rd(tl, "lift", ref tLift);
                Rd(tl, "crack", ref tCrack);
                Rd(tl, "enter", ref tEnter);
                Rd(tl, "blast", ref tBlast);
                Rd(tl, "close", ref tClose);
                Rd(tl, "end", ref tEnd);
            }
            Vector3 origin = anchorPos;
            try
            {
                origin = AstronomicalCountdown.FindGazeboBase(anchorPos);
            }
            catch { }
            // FitArena also remembers the fit for the Final Showdown's viewpoint: put that back afterwards
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
            root = new GameObject("RocketLaunch");
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
            BuildFx();
            BuildPad();
            BuildRift();
            BuildSky();
            BuildCracks();
            BuildPools();
            BuildAudio();
            BuildGroundCracks();
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b);
            ambientSaved = true;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.36f);
            var moon = new GameObject("RocketMoon");
            moon.transform.SetParent(root.transform, false);
            moon.transform.rotation = Quaternion.Euler(40f, 150f + yaw, 0);
            var ml = moon.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.color = new Color(0.7f, 0.8f, 1f);
            ml.intensity = 1.1f;
            ml.shadows = LightShadows.None;
            // the rocket
            rocketModel = def.ActorModels.Count > 0 ? def.ActorModels[0] : null;
            if (def.ActorHeights.Count > 0)
                rocketH = def.ActorHeights[0];
            if (rocketModel != null && rocketModel.Pivot != null)
            {
                var pv = rocketModel.Pivot.transform;
                pv.SetParent(plateauT, false);
                pv.localPosition = new Vector3(PAD.x, 1.2f, PAD.z);
                pv.localRotation = Quaternion.identity;
                rocketK = rocketH / Mathf.Max(0.01f, rocketModel.RendererBounds.size.y);
                pv.localScale = Vector3.one * rocketK;
                rocketModel.Pivot.SetActive(true);
                foreach (var r in rocketModel.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                }
            }
            else
                AstronomicalEventMod.Log.LogWarning("Rocket Launch: the rocket model did not load");
            BuildPath();
            ParseCues(def.Cues);
            lastT = -1f;
            cueIdx = 0;
            pulse = 0f;
            shakeBump = 0f;
            rocketVis = true;
            AstronomicalEventMod.Log.LogInfo(
                "Rocket Launch built: origin "
                    + origin
                    + ", scale "
                    + scale
                    + ", rocket "
                    + rocketH
                    + " units (model x"
                    + rocketK.ToString("0.000")
                    + "), path "
                    + pathL.ToString("0")
                    + " units, cues "
                    + events.Count
            );
        }

        static void Rd(JNode n, string k, ref float v)
        {
            if (n[k] != null)
                v = (float)n[k];
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
            var g = new GameObject("RocketFx");
            g.transform.SetParent(root.transform, false);
            fx = g.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = fx.main;
            m.maxParticles = 5000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.playOnAwake = false;
            m.startLifetime = 2f;
            m.gravityModifier = 0.05f;
            var e = fx.emission;
            e.enabled = false;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;
            fx.Play();
            var g2 = new GameObject("RocketSmoke");
            g2.transform.SetParent(root.transform, false);
            smoke = g2.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = smoke.main;
            m2.maxParticles = 2500;
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
            boomLight = AddLight(root.transform, Color.white, 600f * scale, 0f);
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

        // pad, two gantry towers with swing arms, ring of lights, beacons
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
                Vector3 cen = new Vector3(s * 26f, 0f, PAD.z);
                for (int px = -1; px <= 1; px += 2)
                for (int pz = -1; pz <= 1; pz += 2)
                    Box(v2, t2, c2, cen + new Vector3(px * 4f, TOWER_H * 0.5f, pz * 4f), new Vector3(1.6f, TOWER_H, 1.6f), steel);
                for (float y = 6f; y <= TOWER_H; y += 12f)
                {
                    for (int pz = -1; pz <= 1; pz += 2)
                        Box(v2, t2, c2, cen + new Vector3(0, y, pz * 4f), new Vector3(8f, 0.9f, 0.9f), steel);
                    for (int px = -1; px <= 1; px += 2)
                        Box(v2, t2, c2, cen + new Vector3(px * 4f, y, 0), new Vector3(0.9f, 0.9f, 8f), steel);
                    Box(v2, t2, c2, cen + new Vector3(0, y + 6f, 4f), new Vector3(0.7f, 12.5f, 0.7f), steel * 0.9f);
                }
                MeshObj("Tower" + s, shakeT, v2, t2, c2, matSprite);
                Box(vb, tb, cb, cen + new Vector3(0, TOWER_H + 1.6f, 0), new Vector3(2.6f, 2.6f, 2.6f), Color.white);
                foreach (float ay in new[] { 60f, 40f })
                {
                    var arm = new GameObject("Arm");
                    arm.transform.SetParent(shakeT, false);
                    arm.transform.localPosition = new Vector3(s * 22f, ay, PAD.z);
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

        // the rift: a billboarded tear (dark void, bright jagged rim), a halo, arcs of lightning on its rim and a light
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

        void BuildRift()
        {
            var h = new GameObject("Rift");
            riftT = h.transform;
            riftT.SetParent(plateauT, false);
            riftT.localPosition = RIFT_C;
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.name = "Halo";
            haloT = halo.transform;
            haloT.SetParent(riftT, false);
            haloMat = Mat(new Color(0.55f, 0.3f, 1f, 0f), glowTex, 2999);
            halo.GetComponent<Renderer>().sharedMaterial = haloMat;
            var tear = new GameObject("Tear");
            tearT = tear.transform;
            tearT.SetParent(riftT, false);
            tear.AddComponent<MeshFilter>().sharedMesh = MakeTear();
            var mr = tear.AddComponent<MeshRenderer>();
            tearMat = Mat(Color.white, null, 3000);
            mr.sharedMaterial = tearMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.allowOcclusionWhenDynamic = false;
            var arcMat = Mat(new Color(0.9f, 0.82f, 1f, 1f), lineTex, 3002);
            for (int k = 0; k < arcs.Length; k++)
            {
                var g = new GameObject("Arc");
                g.transform.SetParent(riftT, false);
                var l = g.AddComponent<LineRenderer>();
                l.useWorldSpace = false;
                l.positionCount = 7;
                l.sharedMaterial = arcMat;
                l.shadowCastingMode = ShadowCastingMode.Off;
                l.numCapVertices = 3;
                l.enabled = false;
                arcs[k] = l;
            }
            riftLight = AddLight(riftT, new Color(0.62f, 0.4f, 1f), 700f * scale, 0f);
            riftT.gameObject.SetActive(false);
        }

        // night sky dome (drawn first, follows the camera) whose colours glow around the rift
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
            dome = new GameObject("RocketSky");
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
            var cam = Camera.main;
            if (dome == null || skyMesh == null)
                return;
            Vector3 rd = (riftT.position - dome.transform.position).normalized;
            float glow = Curve(SKYGLOW, t) + pulse * 0.25f;
            for (int k = 0; k < skyGrad; k++)
            {
                float d = Vector3.Dot(skyDirs[k], rd);
                float f = Mathf.Pow(Mathf.Clamp01((d - 0.3f) / 0.7f), 2.2f) * glow;
                var b = skyBase[k];
                float dark = 1f - 0.3f * Mathf.Clamp01(glow);
                skyCols[k] = new Color(b.r * dark + 0.34f * f, b.g * dark + 0.12f * f, b.b * dark + 0.62f * f, 1f);
            }
            skyMesh.colors = skyCols;
        }

        // cracks across the sky: random walks on the dome starting at the rift's direction, drawn out as the show progresses
        void BuildCracks()
        {
            crackRoot = new GameObject("Cracks").transform;
            crackRoot.SetParent(dome.transform, false);
            coreMat = Mat(new Color(1f, 1f, 1f, 1f), null, 3001);
            glowLineMat = Mat(new Color(0.65f, 0.4f, 1f, 0.5f), lineTex, 3000);
            var rnd = new System.Random(11);
            const float R = 395f;
            int mains = 14;
            Func<float, float, Vector3> dirOf = (th, ph) =>
                new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th)) * R;
            for (int m = 0; m < mains; m++)
            {
                float ph = Mathf.PI * 2f * m / mains + ((float)rnd.NextDouble() - 0.5f) * 0.4f,
                    thMax = 0.5f + (float)rnd.NextDouble() * 1.15f,
                    th = 0.02f;
                var pts = new List<Vector3>();
                var phs = new List<float>();
                var ths = new List<float>();
                while (th < thMax)
                {
                    pts.Add(dirOf(th, ph));
                    phs.Add(ph);
                    ths.Add(th);
                    th += 0.04f + (float)rnd.NextDouble() * 0.05f;
                    ph += ((float)rnd.NextDouble() - 0.5f) * 0.32f;
                }
                AddCrack(pts.ToArray(), 0.6f + (float)rnd.NextDouble() * 0.9f, 1f);
                for (int b = 0; b < 2; b++)
                {
                    int at = 3 + rnd.Next(Mathf.Max(1, pts.Count - 6));
                    if (at >= pts.Count)
                        continue;
                    float bph = phs[at] + (b == 0 ? 1f : -1f) * (0.35f + (float)rnd.NextDouble() * 0.4f),
                        bth = ths[at];
                    var bp = new List<Vector3> { pts[at] };
                    int len = 5 + rnd.Next(8);
                    for (int q = 0; q < len; q++)
                    {
                        bth += 0.03f + (float)rnd.NextDouble() * 0.03f;
                        bph += ((float)rnd.NextDouble() - 0.5f) * 0.4f;
                        bp.Add(dirOf(bth, bph));
                    }
                    AddCrack(bp.ToArray(), 0.5f + (float)rnd.NextDouble() * 0.8f, 0.55f);
                }
            }
        }

        void AddCrack(Vector3[] pts, float speed, float width)
        {
            var c = new Crack { P = pts, Speed = speed };
            c.Glow = CrackLine("CrackGlow", glowLineMat, 14f * width);
            c.Core = CrackLine("Crack", coreMat, 2.6f * width);
            cracks.Add(c);
        }

        LineRenderer CrackLine(string n, Material m, float w)
        {
            var g = new GameObject(n);
            g.transform.SetParent(crackRoot, false);
            var l = g.AddComponent<LineRenderer>();
            l.useWorldSpace = false;
            l.sharedMaterial = m;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.numCapVertices = 2;
            l.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
            l.widthMultiplier = w;
            l.positionCount = 2;
            l.enabled = false;
            return l;
        }

        void UpdateCracks(float t)
        {
            float p = Curve(CRACK, t);
            var rd = (riftT.position - dome.transform.position).normalized;
            crackRoot.rotation = Quaternion.FromToRotation(Vector3.forward, rd);
            float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f) + pulse * 0.4f;
            coreMat.color = new Color(1f, 0.95f, 1f, Mathf.Clamp01(flick));
            glowLineMat.color = new Color(0.65f, 0.4f, 1f, Mathf.Clamp01(0.35f + 0.25f * Curve(SKYGLOW, t) + pulse * 0.4f));
            foreach (var c in cracks)
            {
                float f = Mathf.Clamp01(p * c.Speed * 1.15f);
                int n = f <= 0.001f ? 0 : Mathf.Clamp(Mathf.CeilToInt(f * (c.P.Length - 1)) + 1, 2, c.P.Length);
                if (n == c.Shown)
                    continue;
                c.Shown = n;
                bool on = n >= 2;
                c.Core.enabled = on;
                c.Glow.enabled = on;
                if (!on)
                    continue;
                c.Core.positionCount = n;
                c.Glow.positionCount = n;
                for (int k = 0; k < n; k++)
                {
                    c.Core.SetPosition(k, c.P[k]);
                    c.Glow.SetPosition(k, c.P[k]);
                }
            }
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

        void BuildAudio()
        {
            if (AstronomicalShowdown.boomClip == null)
                AstronomicalShowdown.MakeSfx();
            var g = new GameObject("RocketSfx");
            g.transform.SetParent(root.transform, false);
            sfx = g.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;
            sfx.playOnAwake = false;
            sfx.priority = 0;
            var e = new GameObject("RocketEngine");
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
                "RocketEngine",
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

        // ------------------------------------------------------------------ path
        void BuildPath()
        {
            endDir = new Vector3(0f, 0.75f, 0.66f).normalized;
            p0 = new Vector3(PAD.x, 1.2f, PAD.z);
            p3 = RIFT_C - endDir * (0.9f * rocketH);
            Vector3 p1 = p0 + new Vector3(0f, 130f, 2f),
                p2 = p3 - endDir * 180f;
            for (int k = 0; k <= 80; k++)
            {
                float u = k / 80f,
                    v = 1f - u;
                pathP[k] = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
                pathS[k] = k == 0 ? 0f : pathS[k - 1] + Vector3.Distance(pathP[k], pathP[k - 1]);
            }
            pathL = pathS[80];
            float te = Mathf.Max(5f, tEnter - tLift);
            vmax = pathL / (te - tau0 * (1f - Mathf.Exp(-te / tau0)));
        }

        void PathAt(float s, out Vector3 pos, out Vector3 tan)
        {
            if (s <= 0f)
            {
                pos = p0;
                tan = Vector3.up;
                return;
            }
            if (s >= pathL)
            {
                pos = p3 + endDir * (s - pathL);
                tan = endDir;
                return;
            }
            int lo = 0,
                hi = 80;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (pathS[mid] <= s)
                    lo = mid;
                else
                    hi = mid;
            }
            float f = (s - pathS[lo]) / Mathf.Max(0.0001f, pathS[hi] - pathS[lo]);
            pos = Vector3.Lerp(pathP[lo], pathP[hi], f);
            tan = (pathP[hi] - pathP[lo]).normalized;
        }

        // ------------------------------------------------------------------ viewpoint: the spot at the gazebo floor, facing the pad
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
            Vector3 d = Quaternion.Euler(0, y, 0) * new Vector3(0f, 0f, PAD.z * sc);
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            pos = o + Vector3.up * 1.2f;
            return true;
        }

        // ------------------------------------------------------------------ reactions: rumble + thump / boom, scaled by distance
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

        // continuous shaking of the player's surroundings: haptics, small nudges (Knockback setting), dust falling around them
        void Shake(float power, float dt, float t)
        {
            var cam = Camera.main;
            if (cam == null || dt <= 0f)
                return;
            Vector3 focus = t < tEnter ? W(PAD) : riftT.position;
            float d = Vector3.Distance(cam.transform.position, focus);
            float k = Mathf.Clamp01(1f - d / Mathf.Max(100f, 800f * U));
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
                if (power > 0.5f && k > 0.25f)
                {
                    var off = UnityEngine.Random.insideUnitCircle.normalized * 2f;
                    AstronomicalHazards.Blast(cam.transform.position + new Vector3(off.x, 0f, off.y), 40f, 1.1f + 2.6f * power * k, 3.2f);
                }
            }
            if (power > 0.3f && !paused)
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
                foreach (var c in cracks)
                    c.Shown = -1;
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null && dome != null)
                dome.transform.position = cam.transform.position;
            pulse = Mathf.Max(0f, pulse - dt * 3.2f);
            shakeBump = Mathf.Max(0f, shakeBump - dt * 2.5f);
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
                RocketStep(t, dt, power);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Rocket: " + e.Message);
            }
            PadStep(t, dt, power);
            RiftStep(t, dt, cam);
            if (t - skyT > 0.08f || t < skyT)
            {
                skyT = t;
                UpdateSky(t);
            }
            UpdateCracks(t);
            Weather(t, dt);
            GroundCracksStep(t, dt);
            UpdateBolts(dt);
            UpdateRings(dt);
            UpdateBooms(dt);
            Shake(power, dt, t);
            boomI = Mathf.Max(0f, boomI - dt * 14f);
            boomLight.intensity = boomI * Lit;
        }

        void RocketStep(float t, float dt, float power)
        {
            if (rocketModel == null || rocketModel.Pivot == null)
                return;
            float thrust = Curve(ENGINE, t),
                tau = t - tLift;
            float s = tau <= 0f ? 0f : vmax * (tau - tau0 * (1f - Mathf.Exp(-tau / tau0)));
            Vector3 pos,
                tan;
            PathAt(s, out pos, out tan);
            float kk = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tEnter - 0.3f, tEnter + 2.2f, t));
            float sh = (tau <= 0f ? 1f : Mathf.Exp(-tau / 8f) * 0.6f + 0.15f) * power;
            pos += new Vector3(Noise(t * 31f, 0.3f), Noise(t * 27f, 5.1f) * 0.4f, Noise(t * 29f, 9.7f)) * 1.1f * sh;
            if (kk < 1f)
                pos = RIFT_C + (pos - RIFT_C) * kk;
            bool vis = kk > 0.01f;
            if (vis != rocketVis)
            {
                rocketVis = vis;
                rocketModel.Pivot.SetActive(vis);
            }
            var pv = rocketModel.Pivot.transform;
            pv.localPosition = pos;
            pv.localRotation =
                Quaternion.FromToRotation(Vector3.up, tan)
                * Quaternion.Euler(Noise(t * 23f, 2f) * 0.8f * sh, 0f, Noise(t * 19f, 7f) * 0.8f * sh);
            pv.localScale = Vector3.one * rocketK * kk;
            Vector3 b = pv.position,
                down = -pv.up;
            engineLight.transform.position = b + down * 6f * U;
            engineLight.intensity = vis ? thrust * (7f + 2.5f * Mathf.PerlinNoise(t * 20f, 3f)) * Lit : 0f;
            if (paused || !vis)
                return;
            if (thrust > 0.02f)
            {
                int n = Take(ref accA, 160f * thrust * Parts, dt);
                for (int q = 0; q < n; q++)
                {
                    Vector3 src =
                        b
                        + pv.right * (UnityEngine.Random.value < 0.5f ? -2.7f : 2.7f) * U
                        + down * Rnd(0f, 6f) * U
                        + UnityEngine.Random.insideUnitSphere * 2.2f * U;
                    Color c = Color.Lerp(new Color(1f, 0.95f, 0.8f, 0.95f), new Color(1f, 0.45f, 0.1f, 0.8f), UnityEngine.Random.value);
                    Emit(
                        fx,
                        src,
                        down * Rnd(60f, 110f) * U * thrust + UnityEngine.Random.insideUnitSphere * 14f * U,
                        c,
                        Rnd(5f, 10f) * U * (0.6f + 0.4f * thrust),
                        Rnd(0.3f, 0.6f)
                    );
                }
                int m = Take(ref accB, 55f * thrust * Parts, dt);
                for (int q = 0; q < m; q++)
                    Emit(
                        smoke,
                        b + down * 8f * U + UnityEngine.Random.insideUnitSphere * 3f * U,
                        down * Rnd(4f, 10f) * U + UnityEngine.Random.insideUnitSphere * 5f * U,
                        new Color(0.7f, 0.7f, 0.74f, 0.5f),
                        Rnd(10f, 18f) * U,
                        Rnd(10f, 16f)
                    );
            }
            // steam rolling out over the pad while it stands in its own fire
            if (t > tIgn - 0.3f && t < tLift + 6f)
            {
                int n = Take(ref accC, 90f * Mathf.Clamp01((t - tIgn + 0.3f) * 2f) * Parts, dt);
                for (int q = 0; q < n; q++)
                {
                    float a = Rnd(0f, 6.2832f);
                    Vector3 dir = plateauT.right * Mathf.Cos(a) + plateauT.forward * Mathf.Sin(a);
                    Emit(
                        smoke,
                        W(PAD + new Vector3(0f, 2f, 0f)) + dir * Rnd(4f, 14f) * U,
                        dir * Rnd(18f, 50f) * U + Vector3.up * Rnd(1f, 6f) * U,
                        new Color(0.88f, 0.9f, 0.94f, 0.5f),
                        Rnd(12f, 26f) * U,
                        Rnd(4f, 6.5f)
                    );
                }
            }
        }

        void PadStep(float t, float dt, float power)
        {
            float pw = power * U;
            shakeT.localPosition = new Vector3(Noise(t * 33f, 1.3f), Noise(t * 29f, 4.4f) * 0.4f, Noise(t * 37f, 8.8f)) * 0.7f * power;
            if (t < tIgn)
                padLightMat.color = new Color(1f, 0.7f, 0.2f, (t % 1f) < 0.5f ? 1f : 0.12f);
            else
                padLightMat.color = new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(1f - (t - tLift - 6f) / 8f));
            beaconMat.color = new Color(1f, 0.15f, 0.1f, t < tEnter + 2f ? ((t * 2f) % 1f < 0.5f ? 1f : 0.2f) : 0.35f);
            float ang = 85f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tIgn + 0.8f, tLift - 0.2f, t));
            for (int k = 0; k < arms.Length; k++)
                if (arms[k] != null)
                    arms[k].localRotation = Quaternion.Euler(0f, armSign[k] * ang, 0f);
            padLight.intensity = 2.6f * Lit * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tLift, tLift + 14f, t)));
        }

        void RiftStep(float t, float dt, Camera cam)
        {
            float open = Curve(RIFT, t);
            if (open > 0.01f)
                open += pulse * 0.1f;
            bool on = open > 0.004f;
            if (riftT.gameObject.activeSelf != on)
                riftT.gameObject.SetActive(on);
            if (!on)
                return;
            if (cam != null)
                riftT.rotation =
                    Quaternion.LookRotation(riftT.position - cam.transform.position, Vector3.up) * Quaternion.Euler(0f, 0f, 9f);
            float breathe = 1f + 0.04f * Mathf.Sin(t * 1.7f) + 0.02f * Mathf.Sin(t * 5.3f);
            float H = 300f * open * breathe,
                Wd = H * 0.34f * (0.9f + 0.1f * Mathf.Sin(t * 0.9f));
            tearT.localScale = new Vector3(Wd, H, 1f);
            haloT.localScale = new Vector3(H * 1.9f, H * 2.3f, 1f);
            haloT.localPosition = new Vector3(0f, 0f, 2f);
            haloMat.color = new Color(0.55f, 0.3f, 1f, Mathf.Clamp01(open * 0.7f + pulse * 0.25f));
            riftLight.intensity = Mathf.Clamp01(open) * (3.5f + 3f * pulse + 0.8f * Mathf.Sin(t * 3f)) * Lit;
            if (t - arcT > 0.07f || t < arcT)
            {
                arcT = t;
                int live = Mathf.Min(arcs.Length, (int)((3 + open * 8f) * Mathf.Min(1f, Parts)));
                for (int k = 0; k < arcs.Length; k++)
                {
                    var l = arcs[k];
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
                    for (int q = 1; q < 7; q++)
                    {
                        p += dir * Rnd(7f, 15f) * (0.6f + open) + new Vector3(Rnd(-6f, 6f), Rnd(-6f, 6f), 0f);
                        l.SetPosition(q, p);
                    }
                }
            }
            if (paused)
                return;
            int n = Take(ref accD, (35f + 120f * Mathf.Clamp01(open)) * Parts, dt);
            for (int q = 0; q < n; q++)
            {
                float a = Rnd(0f, 6.2832f),
                    rho = Rnd(0.55f, 1.5f);
                Vector3 lp = new Vector3(Mathf.Cos(a) * 0.5f * Wd * rho, Mathf.Sin(a) * 0.5f * H * rho, Rnd(-4f, 4f));
                Vector3 wp = riftT.TransformPoint(lp),
                    wc = riftT.position;
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

        // falling shards while the rift rages, embers once it has closed
        void Weather(float t, float dt)
        {
            if (paused)
                return;
            if (t > tBlast && t < tClose)
            {
                int n = Take(ref accA, 7f * Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(tBlast, tClose, t)) * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        W(new Vector3(Rnd(-260f, 260f), Rnd(240f, 380f), PAD.z + Rnd(-260f, 260f))),
                        Vector3.down * Rnd(40f, 90f) * U,
                        Color.Lerp(new Color(0.65f, 0.4f, 1f, 0.9f), Color.white, UnityEngine.Random.value),
                        Rnd(2f, 5f) * U,
                        3f
                    );
            }
            if (t > tClose + 0.5f && t < tEnd + 10f)
            {
                int n = Take(ref accC, 12f * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        W(new Vector3(Rnd(-70f, 70f), Rnd(0f, 20f), PAD.z + Rnd(-70f, 70f))),
                        new Vector3(Rnd(-3f, 3f), Rnd(2f, 7f), Rnd(-3f, 3f)) * U,
                        new Color(1f, 0.6f, 0.25f, 0.8f),
                        Rnd(0.8f, 1.8f) * U,
                        4f
                    );
            }
        }

        // after the rift blast the ground around the pad cracks open (glowing seams, chunks of floor lifted towards the rift) and heals after the collapse
        sealed class GCrack
        {
            public LineRenderer L;
            public Vector3[] P;
            public int Shown = -1;
        }

        readonly List<GCrack> gcracks = new List<GCrack>();
        float accG;

        void BuildGroundCracks()
        {
            var rnd = new System.Random(31);
            int n = 12;
            for (int c = 0; c < n; c++)
            {
                float a = c * Mathf.PI * 2f / n + ((float)rnd.NextDouble() - 0.5f) * 0.3f,
                    len = 70f + (float)rnd.NextDouble() * 90f;
                int segs = 20;
                var pts = new Vector3[segs + 1];
                Vector3 p = PAD;
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
            float p = Smooth(tBlast, tBlast + 20f, t) * (1f - Smooth(tClose, tClose + 3f, t));
            Color col = Color.Lerp(new Color(0.65f, 0.4f, 1f, 0.9f), new Color(1f, 0.95f, 1f, 0.9f), Smooth(tBlast + 20f, tClose, t));
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
            int c = Take(ref accG, 25f * p * Parts, dt);
            for (int q = 0; q < c; q++)
            {
                var g = gcracks[UnityEngine.Random.Range(0, gcracks.Count)];
                int k = UnityEngine.Random.Range(0, Mathf.Max(1, Mathf.Min(g.Shown, g.P.Length) - 1));
                Vector3 w = W(g.P[k]);
                Emit(
                    fx,
                    w,
                    (riftT.position - w).normalized * Rnd(10f, 30f) * U + Vector3.up * Rnd(8f, 24f) * U,
                    new Color(0.3f, 0.27f, 0.25f, 0.9f),
                    Rnd(1.5f, 5f) * U,
                    3f
                );
            }
        }

        public string DebugInfo()
        {
            int bo = 0;
            foreach (var b in bolts)
                if (b.Core.enabled)
                    bo++;
            return "rocket "
                + (rocketVis ? "on" : "off")
                + "  rift "
                + (riftT != null && riftT.gameObject.activeSelf ? "open" : "closed")
                + "  bolts "
                + bo
                + "  gcracks "
                + gcracks.Count
                + "\nparticles "
                + (fx != null ? fx.particleCount : 0)
                + "+"
                + (smoke != null ? smoke.particleCount : 0)
                + "  power "
                + Curve(SHAKE, lastT).ToString("0.00");
        }

        // ------------------------------------------------------------------ cues
        Vector3 At(JNode n)
        {
            string s = (string)n;
            if (s == "rift")
                return riftT.position;
            if (s == "pad" || n == null)
                return W(PAD + new Vector3(0f, 4f, 0f));
            return n.Count >= 3 ? W(new Vector3((float)n[0], (float)n[1], (float)n[2])) : W(PAD);
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
                    Shock((string)n["kind"], size, col);
                    break;
                case "bolt":
                    Strike(c.T, n["s"] != null ? (float)n["s"] : 0.3f, col);
                    break;
                case "pulse":
                {
                    float s = n["s"] != null ? (float)n["s"] : 0.2f;
                    pulse = Mathf.Max(pulse, Mathf.Clamp01(s * 2.4f));
                    shakeBump = Mathf.Max(shakeBump, 0.12f + s * 0.5f);
                    boomI = Mathf.Max(boomI, 2f + s * 10f);
                    boomLight.transform.position = riftT.position;
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

        // an expanding ring: on the ground around the pad, or across the sky in the plane of the rift
        void Shock(string kind, float size, Color c)
        {
            var r = rings[ringNext];
            ringNext = (ringNext + 1) % rings.Length;
            r.Age = 0f;
            r.Col = c;
            r.R = size * U;
            r.L.enabled = true;
            if (kind == "sky")
            {
                r.C = riftT.position;
                r.U = riftT.right;
                r.V = riftT.up;
                r.Life = 2.4f;
            }
            else
            {
                r.C = W(PAD + new Vector3(0f, 1.5f, 0f));
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

        // lightning from the rift to the ground around the pad
        void Strike(float seed, float s, Color col)
        {
            if (!riftT.gameObject.activeSelf)
                return;
            var b = bolts[boltNext];
            boltNext = (boltNext + 1) % bolts.Length;
            var rnd = new System.Random((int)(seed * 100f) + 7);
            Vector3 a =
                riftT.position
                + riftT.right * ((float)rnd.NextDouble() - 0.5f) * 60f * U
                + riftT.up * ((float)rnd.NextDouble() - 0.5f) * 120f * U;
            float ga = (float)rnd.NextDouble() * 6.2832f,
                gr = 22f + (float)rnd.NextDouble() * 52f;
            Vector3 g = Snap(W(new Vector3(Mathf.Cos(ga) * gr, 0f, PAD.z + Mathf.Sin(ga) * gr))); // inside the part of the arena that was fitted onto the floor
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
                if (rocketModel != null && rocketModel.Pivot != null)
                {
                    rocketModel.Pivot.transform.SetParent(null, false);
                    rocketModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(rocketModel.Pivot);
                }
            }
            catch { }
            rocketModel = null;
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
            cracks.Clear();
            gcracks.Clear();
            lastT = -1f;
            skyMesh = null;
            engineSrc = null;
            sfx = null;
            fx = null;
            smoke = null;
        }
    }
}
