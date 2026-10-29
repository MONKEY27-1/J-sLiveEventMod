using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "New Year" (Fortnite's New Year's Eve countdown, 2019 - 2022; the year is a setting), from the user's reading of the 2021 video. Same Gorilla Tag map, nothing loaded or replaced.
    // 0:05 a rift opens in the sky; 0:09 the disco ball (a crystal ball in 2022) appears; 0:18 the 10 second countdown starts; 0:28 it hits zero, fireworks start and everyone dances (confetti / lights / rumble);
    // 0:38 the fireworks stop and the ball and the fireworks turn to confetti, one big firework launches and explodes at 0:42 and writes the year in firework bursts, which starts to fade and is gone at 1:03.
    // Everything is a pure function of the music time T, except the fireworks bursts, which are fired once as T passes them.
    internal sealed class AstronomicalNewYear : IEventScene, IDebug
    {
        const float T_RIFT = 5f,
            T_BALL = 9f,
            T_COUNT = 18f,
            T_ZERO = 28f,
            T_CONF = 38f,
            T_BOOM = 42f,
            T_GONE = 63f,
            BALL_R = 13f,
            BALL_Y = 100f,
            BALL_Z = 55f,
            TEXT_Y = 100f,
            TEXT_Z = 115f,
            CELL = 4.6f;
        static readonly string[][] FONT =
        {
            new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
            new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
            new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" },
        };

        static float Sm(float a, float b, float x)
        {
            return AstronomicalCube.Sm(a, b, x);
        }

        static Color Hsv(float h, float s = 0.65f, float v = 1f)
        {
            return Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);
        }

        AstronomicalEventDef def;
        GameObject root,
            dome;
        WorldKit kit;
        GltfModel model;
        Transform pivot,
            rift1,
            rift2,
            riftGlow,
            ballGlow,
            num0,
            num1,
            numGlow;
        Material riftMat,
            riftMat2,
            ballGlowMat,
            numMat0,
            numMat1,
            numGlowMat;
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform[] beams = new Transform[12],
            spots = new Transform[12];
        Material[] beamMats = new Material[12],
            spotMats = new Material[12];
        Texture2D[] digits = new Texture2D[10];
        Texture2D glowTex,
            beamTex,
            confTex;
        ParticleSystem fx,
            conf,
            yearFx,
            stars;
        Light ballLamp,
            flashLamp;
        Mesh skyMesh,
            riftMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        int skyN;
        float skyT = -9f;

        struct Cell
        {
            public Vector3 P;
            public float T;
            public Color C;
        }

        readonly List<Cell> cells = new List<Cell>();
        int year = 2021;
        float hue0;
        Vector3 origin;
        float yaw,
            lastT = -1f,
            endT = 120f,
            flash,
            accRain,
            rumbleT,
            sparkAcc;
        int floorLayer;
        bool built,
            paused,
            zeroDone,
            confDone,
            boomDone;
        int cellIdx;
        Color ambientSave;
        bool ambientSaved;
        float Parts
        {
            get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); }
        }
        float Lit
        {
            get { return AstronomicalSettings.Lighting.Value; }
        }

        static int LocoMask()
        {
            try
            {
                return GTPlayer.LocomotionEnabledLayers.value;
            }
            catch { }
            return ~0;
        }

        Vector3 W(Vector3 l)
        {
            return root.transform.TransformPoint(l);
        }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected;
            built = false;
            endT = def != null && def.Length > 5f ? def.Length : 120f;
            year = Mathf.Clamp(AstronomicalSettings.NewYearYear.Value, 2019, 2022);
            hue0 =
                year == 2019 ? 0.88f
                : year == 2020 ? 0.11f
                : year == 2021 ? 0.5f
                : 0.6f;
            Vector3 head = anchorPos;
            try
            {
                var p = GTPlayer.Instance;
                if (p != null)
                    head = p.headCollider.transform.position;
                else if (Camera.main != null)
                    head = Camera.main.transform.position;
            }
            catch { }
            Vector3 org = head;
            try
            {
                org = AstronomicalCountdown.FindGazeboBase(head);
            }
            catch { }
            var cam = Camera.main;
            Vector3 f = cam != null ? cam.transform.forward : Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f)
                f = Vector3.forward;
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            int m = LocoMask();
            floorLayer = 0;
            for (int i = 0; i < 32; i++)
                if ((m & (1 << i)) != 0)
                {
                    floorLayer = i;
                    break;
                }
            RaycastHit h;
            Vector3 ground = org;
            if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, m, QueryTriggerInteraction.Ignore))
                ground = h.point;
            else
                ground.y = head.y - 1.2f;
            origin = ground;
            root = new GameObject("AstronomicalNewYear");
            root.transform.position = origin;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer };
            glowTex = kit.GlowTex();
            beamTex = kit.Tex(
                32,
                4,
                (x, y) =>
                {
                    float u = Mathf.Abs((x + 0.5f) / 16f - 1f);
                    return new Color(1, 1, 1, Mathf.Pow(1f - u, 1.5f));
                },
                TextureWrapMode.Clamp
            );
            confTex = kit.Tex(8, 4, (x, y) => Color.white, TextureWrapMode.Clamp);
            for (int d = 0; d < 10; d++)
            {
                int dd = d;
                digits[d] = kit.Tex(
                    20,
                    28,
                    (x, y) =>
                    {
                        int cx = x / 4,
                            cy = 6 - y / 4;
                        return FONT[dd][cy][cx] == '1' ? Color.white : new Color(1, 1, 1, 0);
                    },
                    TextureWrapMode.Clamp
                );
                digits[d].filterMode = FilterMode.Bilinear;
            }
            BuildSky();
            BuildRift();
            BuildBall();
            BuildBeams();
            BuildFx();
            BuildYear();
            ambientSave = RenderSettings.ambientLight;
            ambientSaved = true;
            lastT = -1f;
            zeroDone = confDone = boomDone = false;
            cellIdx = 0;
            built = true;
            AstronomicalEventMod.Log.LogInfo(
                "New Year " + year + " built at " + origin + ", ball " + (model != null) + ", " + cells.Count + " year particles"
            );
        }

        public bool TryViewpoint(out Vector3 pos, out float y)
        {
            pos = Vector3.zero;
            y = 0f;
            if (root == null)
                return false;
            pos = root.transform.position + Vector3.up * 1.2f;
            y = yaw;
            return true;
        }

        Transform Quad(string n, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = n;
            g.transform.SetParent(root.transform, false);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.allowOcclusionWhenDynamic = false;
            return g.transform;
        }

        void Face(Transform t, Vector3 pos)
        {
            t.position = pos;
            var cam = Camera.main;
            if (cam != null)
                t.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = pos;
            ep.velocity = vel;
            ep.startColor = c;
            ep.startSize = size;
            ep.startLifetime = life;
            ps.Emit(ep, 1);
        }

        ParticleSystem MakePS(string n, int max, float grav, Material mat, bool local = false)
        {
            var ps = new GameObject(n).AddComponent<ParticleSystem>();
            ps.transform.SetParent(root.transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pm = ps.main;
            pm.maxParticles = max;
            pm.simulationSpace = ParticleSystemSimulationSpace.World;
            pm.playOnAwake = false;
            pm.startLifetime = 2f;
            pm.gravityModifier = grav;
            var em = ps.emission;
            em.enabled = false;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        void BuildSky()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cols = new List<Color>();
            var dirs = new List<Vector3>();
            int lat = 20,
                lon = 40;
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = i * Mathf.PI * 0.52f / lat,
                    ph = j * Mathf.PI * 2f / lon;
                float s = Mathf.Sin(th);
                var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph));
                dirs.Add(d);
                verts.Add(d * 280f);
                cols.Add(Color.black);
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j,
                    b = a + 1,
                    c = a + lon + 1,
                    d = c + 1;
                tris.AddRange(new[] { a, b, c, b, d, c });
            }
            skyMesh = new Mesh();
            skyMesh.SetVertices(verts);
            skyMesh.SetColors(cols);
            skyMesh.SetTriangles(tris, 0);
            skyMesh.RecalculateBounds();
            owned.Add(skyMesh);
            skyCols = cols.ToArray();
            skyDirs = dirs.ToArray();
            skyN = skyCols.Length;
            dome = new GameObject("AstronomicalNewYearSky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 2501));
            dm.mainTexture = null;
            dm.renderQueue = 2501;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = new GameObject("NYStars").AddComponent<ParticleSystem>();
            stars.transform.SetParent(dome.transform, false);
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pm = stars.main;
            pm.maxParticles = 800;
            pm.simulationSpace = ParticleSystemSimulationSpace.Local;
            pm.playOnAwake = false;
            pm.startLifetime = 100000f;
            var em = stars.emission;
            em.enabled = false;
            stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004);
            stars.Play();
            for (int k = 0; k < 520; k++)
            {
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.97f + 0.03f;
                var ep = new ParticleSystem.EmitParams();
                ep.position = d.normalized * 270f;
                ep.velocity = Vector3.zero;
                ep.startSize = UnityEngine.Random.Range(0.8f, 2.4f);
                ep.startLifetime = 100000f;
                float b = UnityEngine.Random.Range(0.5f, 1f);
                ep.startColor = new Color(b, b, Mathf.Min(1f, b + 0.2f), 1f);
                stars.Emit(ep, 1);
            }
        }

        // deep night, a coloured glow round the ball, whitened by every firework
        void UpdateSky(float t)
        {
            if (skyMesh == null)
                return;
            float night = Sm(0f, 4f, t);
            Color top = Color.Lerp(new Color(0.03f, 0.05f, 0.14f), new Color(0.01f, 0.01f, 0.07f), night),
                hor = Color.Lerp(new Color(0.12f, 0.2f, 0.35f), new Color(0.1f, 0.05f, 0.22f), night);
            Vector3 bd = new Vector3(0f, BALL_Y, BALL_Z).normalized;
            Color glow = Hsv(hue0 + t * 0.02f, 0.7f, 1f);
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k];
                var c = Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.5f));
                float bg =
                    Mathf.Exp(-Mathf.Pow(Vector3.Angle(d, bd) / 40f, 2f))
                        * Sm(T_BALL, T_BALL + 3f, t)
                        * (1f - Sm(T_CONF, T_CONF + 4f, t))
                        * 0.3f
                    + flash * 0.12f;
                c.r += glow.r * bg;
                c.g += glow.g * bg;
                c.b += glow.b * bg;
                c.a = 1f;
                skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        Texture2D Swirl(Color a, Color b, Color c, float seed)
        {
            return kit.Tex(
                128,
                128,
                (x, y) =>
                {
                    float u = x / 128f,
                        v = y / 128f,
                        T = Mathf.PI * 2f;
                    float n =
                        Mathf.Sin(T * (u * 3f + 0.25f * Mathf.Sin(T * (v * 2f + seed))))
                        + Mathf.Sin(T * (v * 4f + 0.3f * Mathf.Sin(T * u * 3f + seed)))
                        + Mathf.Sin(T * ((u + v) * 2f + 0.4f * Mathf.Sin(T * v * 3f)));
                    n = n / 3f * 0.5f + 0.5f;
                    var col = n < 0.5f ? Color.Lerp(a, b, n * 2f) : Color.Lerp(b, c, (n - 0.5f) * 2f);
                    col.a = 0.9f * (0.35f + 0.65f * n);
                    return col;
                }
            );
        }

        void BuildRift()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var cols = new List<Color>();
            var tr = new List<int>();
            int seg = 64,
                rings = 10;
            for (int k = 0; k <= rings; k++)
            for (int j = 0; j <= seg; j++)
            {
                float r = k / (float)rings,
                    a = j * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * r * 0.8f, 0.5f + Mathf.Sin(a) * r * 0.8f));
                cols.Add(new Color(1, 1, 1, Mathf.Clamp01((1f - r * r) * 1.6f)));
            }
            for (int k = 0; k < rings; k++)
            for (int j = 0; j < seg; j++)
            {
                int a = k * (seg + 1) + j,
                    b = a + 1,
                    c = a + seg + 1,
                    d = c + 1;
                tr.AddRange(new[] { a, c, b, b, c, d });
            }
            riftMesh = new Mesh();
            riftMesh.SetVertices(v);
            riftMesh.SetUVs(0, uv);
            riftMesh.SetColors(cols);
            riftMesh.SetTriangles(tr, 0);
            riftMesh.RecalculateBounds();
            owned.Add(riftMesh);
            riftMat = kit.Glow(Color.white, Swirl(Hsv(hue0, 0.9f, 0.5f), Hsv(hue0 + 0.15f, 0.7f, 0.9f), Color.white, 0.1f), 2995);
            riftMat2 = kit.Glow(Color.white, Swirl(Hsv(hue0 + 0.3f, 0.9f, 0.4f), Hsv(hue0 + 0.45f, 0.7f, 0.9f), Color.white, 0.6f), 2996);
            Func<string, Material, Transform> mk = (n, mat) =>
            {
                var g = new GameObject(n);
                g.transform.SetParent(root.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = riftMesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.allowOcclusionWhenDynamic = false;
                return g.transform;
            };
            rift1 = mk("NYRift1", riftMat);
            rift2 = mk("NYRift2", riftMat2);
            riftGlow = Quad("NYRiftGlow", kit.Glow(Hsv(hue0 + 0.1f, 0.5f, 1f) * new Color(1, 1, 1, 0.4f), glowTex, 3000));
        }

        void BuildBall()
        {
            model = def.Prop(year == 2022 ? "crystal" : "disco");
            if (model == null || model.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("New Year: ball model missing");
                return;
            }
            pivot = model.Pivot.transform;
            pivot.SetParent(root.transform, false);
            model.Pivot.SetActive(true);
            foreach (var r in model.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var src = r.sharedMaterials;
                origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src));
                var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var mt = kit.Flat(Color.white * 1.2f);
                    try
                    {
                        if (src[i] != null && src[i].mainTexture != null)
                            mt.mainTexture = src[i].mainTexture;
                    }
                    catch { }
                    nw[i] = mt;
                }
                r.sharedMaterials = nw;
            }
            ballGlowMat = kit.Glow(Hsv(hue0, 0.4f, 1f) * new Color(1, 1, 1, 0.3f), glowTex, 3000);
            ballGlow = Quad("NYBallGlow", ballGlowMat);
            ballLamp = new GameObject("NYBallLight").AddComponent<Light>();
            ballLamp.transform.SetParent(root.transform, false);
            ballLamp.type = LightType.Point;
            ballLamp.range = 220f;
            ballLamp.intensity = 0f;
            ballLamp.shadows = LightShadows.None;
            flashLamp = new GameObject("NYFlashLight").AddComponent<Light>();
            flashLamp.transform.SetParent(root.transform, false);
            flashLamp.type = LightType.Point;
            flashLamp.range = 200f;
            flashLamp.intensity = 0f;
            flashLamp.shadows = LightShadows.None;
            numMat0 = kit.Glow(Color.white, digits[0], 3003);
            numMat1 = kit.Glow(Color.white, digits[0], 3003);
            num0 = Quad("NYNum0", numMat0);
            num1 = Quad("NYNum1", numMat1);
            numGlowMat = kit.Glow(Color.white, glowTex, 3001);
            numGlow = Quad("NYNumGlow", numGlowMat);
        }

        void BuildBeams()
        {
            for (int i = 0; i < 12; i++)
            {
                beamMats[i] = kit.Glow(Color.white, beamTex, 2999);
                beams[i] = Quad("NYBeam" + i, beamMats[i]);
                spotMats[i] = kit.Glow(Color.white, glowTex, 2998);
                spots[i] = Quad("NYSpot" + i, spotMats[i]);
            }
        }

        void BuildFx()
        {
            fx = MakePS("NYFireworks", 4000, 0.35f, kit.Glow(Color.white, glowTex, 3004));
            conf = MakePS("NYConfetti", 3500, 0.06f, kit.Glow(Color.white, confTex, 3002));
            var pm = conf.main;
            pm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var rot = conf.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        }

        // the year, written in the sky in firework bursts: a 5x7 pixel font; every cell becomes four sparks that pop in left to right from 0:42.3 and fade out so they are gone at 1:03
        void BuildYear()
        {
            yearFx = MakePS("NYYear", 1500, 0.012f, kit.Glow(Color.white, glowTex, 3004));
            var col = yearFx.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            GradientColorKey[] ck = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            GradientAlphaKey[] ak = { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.92f, 0.3f), new GradientAlphaKey(0f, 1f) };
            g.colorKeys = ck;
            g.alphaKeys = ak;
            col.color = g;
            string ys = year.ToString();
            var list = new List<Cell>();
            for (int d = 0; d < 4; d++)
            {
                int dig = ys[d] - '0';
                for (int c = 0; c < 5; c++)
                for (int r = 0; r < 7; r++)
                    if (FONT[dig][r][c] == '1')
                        list.Add(
                            new Cell
                            {
                                P = new Vector3((d * 6 + c - 11f) * CELL, TEXT_Y + (3f - r) * CELL, TEXT_Z),
                                C = Hsv(hue0 + d * 0.09f + r * 0.012f, 0.55f, 1f),
                            }
                        );
            }
            list.Sort((a, b) => a.P.x.CompareTo(b.P.x));
            for (int k = 0; k < list.Count; k++)
            {
                var c = list[k];
                c.T = T_BOOM + 0.3f + k * 0.012f;
                list[k] = c;
            }
            cells.AddRange(list);
        }

        public void SetPaused(bool p)
        {
            paused = p;
            foreach (var ps in new[] { fx, conf, yearFx, stars })
            {
                if (ps == null)
                    continue;
                if (p)
                    ps.Pause(true);
                else
                    ps.Play(true);
            }
        }

        void Burst(Vector3 lp, Color c, int n, float speed, float size)
        {
            Vector3 wp = W(lp);
            for (int k = 0; k < n; k++)
            {
                Vector3 d = UnityEngine.Random.onUnitSphere;
                Emit(
                    fx,
                    wp,
                    root.transform.TransformVector(d * speed * UnityEngine.Random.Range(0.6f, 1f)),
                    Color.Lerp(c, Color.white, UnityEngine.Random.value * 0.4f),
                    size * UnityEngine.Random.Range(0.7f, 1.3f),
                    UnityEngine.Random.Range(1.4f, 2.6f)
                );
            }
            flash = Mathf.Max(flash, 0.5f);
            flashLamp.transform.position = wp;
            flashLamp.color = c;
            flashLamp.intensity = Lit * 6f;
        }

        // shell i of the finale: launched every 0.3 s from 0:28 to 0:38, climbs for its fuse and bursts
        static float ShellT(int i)
        {
            return T_ZERO + 0.3f * i;
        }

        Vector3 ShellTarget(int i)
        {
            float a = WorldKit.Hash(i, 1, 41) * 6.2832f,
                r = 40f + WorldKit.Hash(i, 2, 41) * 110f;
            return new Vector3(Mathf.Cos(a) * r * 0.8f, 65f + WorldKit.Hash(i, 3, 41) * 70f, BALL_Z + Mathf.Sin(a) * r * 0.9f);
        }

        float Fuse(int i)
        {
            return 1.1f + WorldKit.Hash(i, 4, 41) * 0.6f;
        }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            bool back = t < lastT - 0.4f;
            if (back || lastT < 0f)
            {
                zeroDone = confDone = boomDone = false;
                cellIdx = 0;
                while (cellIdx < cells.Count && cells[cellIdx].T < t)
                    cellIdx++;
            }
            float tp = lastT < 0f ? t : lastT;
            lastT = t;
            var cam = Camera.main;
            if (cam != null)
                dome.transform.position = cam.transform.position;
            Vector3 bc = new Vector3(0f, BALL_Y, BALL_Z);
            float app = Sm(T_BALL, T_BALL + 2.5f, t),
                gone = t >= T_CONF ? 0f : 1f;
            // ---- the rift (0:05 - 0:13)
            float R = 55f * Sm(T_RIFT, T_BALL - 0.5f, t) * (1f - Sm(T_BALL + 3f, T_BALL + 7f, t));
            bool rOn = R > 0.5f;
            foreach (var tr in new[] { rift1, rift2, riftGlow })
                if (tr.gameObject.activeSelf != rOn)
                    tr.gameObject.SetActive(rOn);
            if (rOn)
            {
                Vector3 rc = new Vector3(0f, 150f, BALL_Z);
                rift1.localPosition = rc;
                rift1.localScale = new Vector3(R, 1f, R);
                rift1.localRotation = Quaternion.Euler(0f, t * 17f, 0f);
                rift2.localPosition = rc + Vector3.down * 3f;
                rift2.localScale = new Vector3(R * 0.8f, 1f, R * 0.8f);
                rift2.localRotation = Quaternion.Euler(0f, -t * 26f, 0f);
                Face(riftGlow, W(rc + Vector3.down * 8f));
                riftGlow.localScale = Vector3.one * R * 1.3f;
            }
            // ---- the ball: drops out of the rift from 0:09, spins, sparkles; at 0:38 it bursts into confetti
            bool bOn = model != null && t >= T_BALL && t < T_CONF;
            if (model != null && model.Pivot.activeSelf != bOn)
                model.Pivot.SetActive(bOn);
            float bY = Mathf.Lerp(150f, BALL_Y, Sm(T_BALL, T_BALL + 5f, t)) + 1.2f * Mathf.Sin(t * 0.8f);
            Vector3 bpos = W(new Vector3(0f, bY, BALL_Z));
            float cOff = year == 2022 ? 1.6f : 1f;
            if (bOn)
            {
                pivot.position = bpos - Vector3.up * cOff * BALL_R * app;
                pivot.rotation = Quaternion.Euler(0f, t * 22f, 0f);
                pivot.localScale = Vector3.one * BALL_R * app;
            }
            float show = bOn ? 1f : 0f;
            bool gOn = bOn;
            if (ballGlow.gameObject.activeSelf != gOn)
                ballGlow.gameObject.SetActive(gOn);
            if (gOn)
            {
                Face(ballGlow, bpos);
                ballGlow.localScale = Vector3.one * BALL_R * (4.5f + 1.5f * Mathf.Sin(t * 3f)) * app;
                ballGlowMat.color = Hsv(hue0 + t * 0.04f, 0.45f, 1f) * new Color(1, 1, 1, 0.22f + 0.3f * Sm(T_ZERO, T_ZERO + 1f, t));
            }
            ballLamp.transform.position = bpos;
            ballLamp.color = Hsv(hue0 + t * 0.12f, 0.7f, 1f);
            ballLamp.intensity = Lit * show * app * (3f + 4f * Sm(T_ZERO, T_ZERO + 1f, t));
            if (!paused && dt > 0f && bOn)
            {
                sparkAcc += (14f + 30f * Sm(T_ZERO, T_ZERO + 2f, t)) * Parts * dt;
                int n = (int)sparkAcc;
                sparkAcc -= n;
                for (int k = 0; k < n; k++)
                {
                    Vector3 d = UnityEngine.Random.onUnitSphere;
                    Emit(
                        fx,
                        bpos + d * BALL_R * app * 1.05f,
                        d * 1.5f,
                        Color.Lerp(Color.white, Hsv(hue0 + UnityEngine.Random.value * 0.5f), 0.4f),
                        UnityEngine.Random.Range(1f, 2.4f),
                        UnityEngine.Random.Range(0.3f, 0.7f)
                    );
                }
            }
            // ---- disco beams and floor spots (from the ball's arrival, wilder after zero)
            float lights = show * Sm(T_BALL + 2f, T_BALL + 5f, t) * (0.55f + 0.45f * Sm(T_ZERO, T_ZERO + 1f, t));
            for (int i = 0; i < 12; i++)
            {
                bool on = lights > 0.02f;
                if (beams[i].gameObject.activeSelf != on)
                {
                    beams[i].gameObject.SetActive(on);
                    spots[i].gameObject.SetActive(on);
                }
                if (!on)
                    continue;
                float a = i * Mathf.PI * 2f / 12f + t * (0.35f + 0.5f * Sm(T_ZERO, T_ZERO + 2f, t)) * (i % 2 == 0 ? 1f : -1f),
                    rad = 22f + 40f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f + i * 1.7f));
                Vector3 end = W(new Vector3(Mathf.Cos(a) * rad, 0f, BALL_Z + Mathf.Sin(a) * rad));
                RaycastHit h;
                end.y = Physics.Raycast(end + Vector3.up * 70f, Vector3.down, out h, 160f, ~0, QueryTriggerInteraction.Ignore)
                    ? h.point.y + 0.15f
                    : origin.y;
                Color c = Hsv(hue0 + i / 12f * 0.8f + t * 0.1f, 0.75f, 1f);
                Vector3 L = (end - bpos),
                    mid = (end + bpos) * 0.5f;
                float len = L.magnitude;
                Vector3 Ln = L / Mathf.Max(0.01f, len);
                Vector3 toCam = (cam != null ? cam.transform.position : bpos - Vector3.forward * 50f) - mid;
                Vector3 perp = toCam - Ln * Vector3.Dot(toCam, Ln);
                if (perp.sqrMagnitude < 0.01f)
                    perp = Vector3.right;
                beams[i].position = mid;
                beams[i].rotation = Quaternion.LookRotation(-perp, Ln);
                beams[i].localScale = new Vector3(5f + 2f * Mathf.Sin(t * 2f + i), len, 1f);
                beamMats[i].color = new Color(c.r, c.g, c.b, 0.2f * lights);
                spots[i].position = end;
                spots[i].rotation = Quaternion.Euler(90f, 0f, 0f);
                spots[i].localScale = Vector3.one * (12f + 4f * Mathf.Sin(t * 3f + i));
                spotMats[i].color = new Color(c.r, c.g, c.b, 0.55f * lights);
            }
            // ---- the countdown (0:18 - 0:28): 10 ... 1, then 0
            int n0 = -1;
            float fracT = 0f;
            if (t >= T_COUNT && t < T_ZERO + 2f)
            {
                n0 = Mathf.Max(0, Mathf.CeilToInt(T_ZERO - t));
                fracT = t - Mathf.Floor(t);
                if (t >= T_ZERO)
                {
                    n0 = 0;
                    fracT = t - T_ZERO;
                }
            }
            bool nOn = n0 >= 0;
            foreach (var tr in new[] { num0, num1, numGlow })
                if (tr != null && tr.gameObject.activeSelf != nOn)
                    tr.gameObject.SetActive(nOn);
            if (nOn)
            {
                float pulse = 1f + 0.35f * Mathf.Exp(-fracT * 5f),
                    fade = t >= T_ZERO ? 1f - Sm(T_ZERO + 0.6f, T_ZERO + 2f, t) : 1f;
                float sc = 34f * pulse * (t >= T_ZERO ? 1.4f : 1f);
                Color c = Hsv(hue0 + n0 * 0.07f, 0.45f, 1f);
                c.a = fade;
                Vector3 np = new Vector3(0f, 62f, BALL_Z - 18f);
                if (n0 >= 10)
                {
                    numMat0.mainTexture = digits[1];
                    numMat1.mainTexture = digits[0];
                    num0.gameObject.SetActive(true);
                    num1.gameObject.SetActive(true);
                    Face(num0, W(np + new Vector3(-0.3f * sc, 0f, 0f)));
                    Face(num1, W(np + new Vector3(0.3f * sc, 0f, 0f)));
                }
                else
                {
                    numMat0.mainTexture = digits[n0];
                    num0.gameObject.SetActive(true);
                    num1.gameObject.SetActive(false);
                    Face(num0, W(np));
                }
                num0.localScale = new Vector3(sc * 0.55f, sc * 0.77f, 1f);
                num1.localScale = num0.localScale;
                numMat0.color = c;
                numMat1.color = c;
                Face(numGlow, W(np + Vector3.forward * 2f));
                numGlow.localScale = Vector3.one * sc * 2.2f;
                numGlowMat.color = new Color(c.r, c.g, c.b, 0.18f * fade);
            }
            // ---- zero (0:28): flash, a burst of colour + confetti, rumble; the party starts
            if (!paused && !zeroDone && t >= T_ZERO && t < T_ZERO + 1.5f)
            {
                zeroDone = true;
                Burst(bc, Hsv(hue0, 0.6f, 1f), 160, 55f, 4f);
                for (int k = 0; k < 160; k++)
                    Emit(
                        conf,
                        bpos,
                        UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(8f, 30f),
                        Hsv(UnityEngine.Random.value, 0.8f, 1f),
                        UnityEngine.Random.Range(0.6f, 1.2f),
                        UnityEngine.Random.Range(5f, 9f)
                    );
                flash = 1f;
            }
            // ---- the finale fireworks (0:28 - 0:38): a shell every 0.3 s
            if (!paused && dt > 0f)
                for (int i = 0; i < 34; i++)
                {
                    float ts = ShellT(i),
                        te = ts + Fuse(i);
                    if (ts >= T_CONF)
                        break;
                    Vector3 tg = ShellTarget(i),
                        st = new Vector3(tg.x * 0.9f, 0f, tg.z);
                    Vector3 gp = W(st);
                    gp.y = origin.y;
                    if (t >= ts && t < te)
                    {
                        float k = (t - ts) / (te - ts);
                        Vector3 p = Vector3.Lerp(gp, W(tg), 1f - (1f - k) * (1f - k));
                        for (int q = 0; q < 2; q++)
                            Emit(fx, p, UnityEngine.Random.insideUnitSphere * 2f, new Color(1f, 0.85f, 0.5f, 0.8f), 1.1f, 0.5f);
                    }
                    if (te > tp && te <= t)
                        Burst(tg, Hsv(hue0 + WorldKit.Hash(i, 5, 41) * 0.7f, 0.7f, 1f), (int)(70 * Parts + 20), 34f, 3f);
                }
            // ---- 0:38: the fireworks stop, the ball and the fireworks turn to confetti
            if (!paused && !confDone && t >= T_CONF && t < T_CONF + 1.5f)
            {
                confDone = true;
                for (int k = 0; k < 700; k++)
                    Emit(
                        conf,
                        bpos + UnityEngine.Random.onUnitSphere * BALL_R * 0.8f,
                        UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(4f, 26f),
                        Hsv(UnityEngine.Random.value, 0.75f, 1f),
                        UnityEngine.Random.Range(0.7f, 1.4f),
                        UnityEngine.Random.Range(8f, 14f)
                    );
                flash = 1f;
            }
            if (!paused && dt > 0f && t >= T_CONF && t < 52f && cam != null)
            {
                accRain += (160f * (1f - Sm(46f, 52f, t))) * Parts * dt;
                int n = (int)accRain;
                accRain -= n;
                for (int k = 0; k < n; k++)
                    Emit(
                        conf,
                        cam.transform.position
                            + new Vector3(
                                UnityEngine.Random.Range(-60f, 60f),
                                UnityEngine.Random.Range(15f, 60f),
                                UnityEngine.Random.Range(-60f, 60f)
                            ),
                        new Vector3(
                            UnityEngine.Random.Range(-2f, 2f),
                            -UnityEngine.Random.Range(1.5f, 4f),
                            UnityEngine.Random.Range(-2f, 2f)
                        ),
                        Hsv(UnityEngine.Random.value, 0.75f, 1f),
                        UnityEngine.Random.Range(0.6f, 1.2f),
                        UnityEngine.Random.Range(9f, 14f)
                    );
            }
            // ---- the big firework: leaves the ground at 0:38, explodes at 0:42 and writes the year
            if (!paused && dt > 0f && t >= T_CONF && t < T_BOOM)
            {
                float k = (t - T_CONF) / (T_BOOM - T_CONF);
                Vector3 p = W(new Vector3(0f, Mathf.Lerp(0f, TEXT_Y, 1f - (1f - k) * (1f - k)), TEXT_Z));
                p.y += origin.y - W(Vector3.zero).y;
                for (int q = 0; q < 6; q++)
                    Emit(
                        fx,
                        p,
                        UnityEngine.Random.insideUnitSphere * 4f + Vector3.down * 4f,
                        new Color(1f, 0.8f, 0.4f, 0.9f),
                        UnityEngine.Random.Range(1.5f, 3.2f),
                        UnityEngine.Random.Range(0.6f, 1.2f)
                    );
            }
            if (!paused && !boomDone && t >= T_BOOM && t < T_BOOM + 1.5f)
            {
                boomDone = true;
                Burst(new Vector3(0f, TEXT_Y, TEXT_Z), new Color(1f, 0.85f, 0.45f), 420, 70f, 4.5f);
                flash = 1f;
            }
            if (!paused)
                while (cellIdx < cells.Count && cells[cellIdx].T <= t)
                {
                    var c = cells[cellIdx];
                    cellIdx++;
                    if (c.T < tp - 0.01f && !back)
                        continue;
                    float life = Mathf.Max(1f, T_GONE - c.T + 0.2f);
                    for (int s = 0; s < 4; s++)
                        Emit(
                            yearFx,
                            W(c.P + new Vector3((s & 1) - 0.5f, (s >> 1) - 0.5f, 0f) * CELL * 0.45f),
                            Vector3.zero,
                            c.C,
                            CELL * 0.8f,
                            life
                        );
                    if ((cellIdx & 3) == 0)
                        Burst(c.P, c.C, 6, 18f, 2f);
                }
            flash = Mathf.Max(0f, flash - dt * 1.8f);
            flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 18f * Lit);
            if (!paused && dt > 0f)
            {
                rumbleT += dt;
                if (rumbleT > 0.25f)
                {
                    rumbleT = 0f;
                    float p =
                        (t >= T_ZERO && t < T_CONF)
                            ? 0.25f + 0.2f * Mathf.Sin(t * 12.57f)
                            : (t >= T_BOOM && t < T_BOOM + 0.8f ? 0.8f : (t >= T_COUNT && t < T_ZERO ? 0.12f : 0f));
                    if (p > 0.05f)
                    {
                        try
                        {
                            var gt = GorillaTagger.Instance;
                            if (gt != null)
                            {
                                gt.StartVibration(true, Mathf.Clamp01(p), 0.12f);
                                gt.StartVibration(false, Mathf.Clamp01(p), 0.12f);
                            }
                        }
                        catch { }
                    }
                }
            }
            // atmosphere
            RenderSettings.ambientLight =
                Color.Lerp(ambientSave, Hsv(hue0, 0.5f, 0.35f), Sm(2f, 8f, t) * (1f - Sm(T_GONE, T_GONE + 8f, t))) * (0.9f + 0.5f * flash);
            if (t - skyT > 0.1f || t < skyT)
            {
                skyT = t;
                UpdateSky(t);
            }
        }

        public string DebugInfo()
        {
            return "t " + lastT.ToString("0.0") + "  year " + year + "  ball " + (model != null) + "  origin " + origin;
        }

        public void Cleanup()
        {
            if (ambientSaved)
            {
                RenderSettings.ambientLight = ambientSave;
                ambientSaved = false;
            }
            if (model != null && model.Pivot != null)
            {
                foreach (var kv in origMats)
                    if (kv.Key != null)
                        kv.Key.sharedMaterials = kv.Value;
                origMats.Clear();
                model.Pivot.transform.SetParent(null, false);
                model.Pivot.SetActive(false);
                AstronomicalEventMod.ParkModel(model.Pivot);
            }
            model = null;
            cells.Clear();
            if (dome != null)
                UnityEngine.Object.Destroy(dome);
            dome = null;
            if (root != null)
                UnityEngine.Object.Destroy(root);
            root = null;
            if (kit != null)
                foreach (var o in kit.Owned)
                    if (o != null)
                        UnityEngine.Object.Destroy(o);
            foreach (var o in owned)
                if (o != null)
                    UnityEngine.Object.Destroy(o);
            owned.Clear();
            kit = null;
            built = false;
            lastT = -1f;
            skyMesh = null;
        }
    }
}
