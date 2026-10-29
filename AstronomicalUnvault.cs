using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "The Unvaulting" (Fortnite Season 8 finale, 4 May 2019). In the real Forest: 0:15 the Vault site (the vault hatch with its beam of light and the Zero Point over it), then 4:26 the volcano
    // explodes and rocks hit the map (the user's times: 4:41, 5:08, 5:19 -> 5:28, 5:30) and Tilted Towers falls. In between, 0:55 - 4:26, the voting area, the only place that is not in the map: the
    // Forest is switched off there (AstronomicalForestHider) and a glass world with its own colliders stands round the player: six pillars, each with a vaulted weapon floating at punching height;
    // hit a pillar with your fists to vote (votes are shared with everyone who has the mod through the same relay as the event start), 4:08 the winning pillar shatters.
    // No text anywhere: the glass cracks as votes come in. Every state is a pure function of the music time except the votes themselves.
    internal sealed partial class AstronomicalUnvault : IEventScene, IDebug
    {
        // ---- the timeline (seconds of the music), from the user's reading of the replay
        const float T_START = 15f,
            T_JUMP = 55f,
            T_WIN = 240f,
            T_BREAK = 248f,
            T_BACK = 266f,
            T_ROCK_A = 281f,
            T_END = 340f;

        sealed class Cue
        {
            public float T;
            public int Type;
            public Vector3 P;
            public float S;
            public Color C;
            public bool World;
            public float V;
        } // types: 0 flash, 1 boom, 2 shock ring, 3 dust, 4 split, 5 launch puff, 6 knockback (S = radius m, V = push speed)

        sealed class RockF
        {
            public float T0,
                T1;
            public Vector3 A,
                B;
            public float Arc,
                Size;
            public int Kind;
            public Transform G;
            public Transform Glow;
            public Material Mat;
            public float AccA,
                AccB;
        }

        sealed class Impact
        {
            public float T;
            public Vector3 P;
            public float R;
        }

        sealed class Ring
        {
            public LineRenderer L;
            public Vector3 C;
            public float Age = 9f,
                Life = 1.8f,
                R;
            public Color Col;
        }

        static readonly float[] SHAKE =
        {
            0,
            0,
            14.5f,
            0,
            15,
            0.4f,
            17,
            0.05f,
            54.5f,
            0.05f,
            55,
            0.7f,
            58,
            0.08f,
            247.5f,
            0.1f,
            248,
            1f,
            251,
            0.25f,
            265.5f,
            0.1f,
            266,
            1f,
            272,
            0.55f,
            280,
            0.35f,
            400,
            0.3f,
        };

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

        public static bool Host = true; // set by the manager: true when this client started the event (it announces the winner)
        static AstronomicalUnvault current;
        AstronomicalEventDef def;
        GameObject root,
            dome;
        Vector3 origin;
        float yaw;
        int floorLayer;
        bool built;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Cue> cues = new List<Cue>();
        readonly List<RockF> rocks = new List<RockF>();
        readonly List<Impact> impacts = new List<Impact>();
        Material matGlow,
            topWhite;
        Texture2D glowTex;
        ParticleSystem fx,
            smoke;
        Light sun,
            boomLight;
        float boomI;
        Transform[] boomSphere;
        Material[] boomMat;
        float[] boomAge,
            boomSize;
        int boomNext;
        readonly Ring[] rings = new Ring[5];
        int ringNext;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        int skyGrad;
        float skyT = -9f;
        GameObject flashSphere;
        AudioSource sfx;
        float reactCool,
            rumbleT,
            lastT = -1f,
            shakeBump;
        int cueIdx,
            curPhase = -1;
        bool paused,
            shattered,
            absorbed;
        float accA,
            accB,
            accC,
            accD,
            accE;
        Vector3 ambientSave;
        bool ambientSaved;
        bool hiderOn;
        Vector3 glassOrigin;
        float glassYaw;
        bool haveGlass;
        GltfModel zpModel;
        float zpHalf;

        // ---- votes
        readonly int[] local = new int[6],
            remote = new int[6],
            pend = new int[6];
        int winner = -1;
        bool winnerSent;
        float nextSend;
        readonly Vector3[] handPrev = new Vector3[2];
        readonly bool[] handInit = new bool[2];
        readonly float[] handCool = new float[2];

        public static void AddRemote(int item, int count)
        {
            var c = current;
            if (c != null && item >= 0 && item < 6 && count > 0 && count < 500)
                c.remote[item] += count;
        }

        public static void SetWinner(int item)
        {
            var c = current;
            if (c != null && item >= 0 && item < 6 && c.lastT < T_BREAK - 0.5f)
                c.winner = item;
        }

        int Total(int i)
        {
            return local[i] + remote[i];
        }

        int Decide()
        {
            int best = 5,
                bv = -1;
            for (int i = 0; i < 6; i++)
            {
                int v = Total(i);
                if (v > bv || (v == bv && WorldKit.Hash(i, bv, 99) > 0.5f))
                {
                    bv = v;
                    best = i;
                }
            }
            return bv <= 0 ? 5 : best;
        } // no votes at all: the Drum Gun, like the real event

        // ------------------------------------------------------------------ helpers
        Light AddLight(Transform parent, Color c, float range, float intensity)
        {
            var g = new GameObject("UvLight");
            g.transform.SetParent(parent, false);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        Vector3 W(Vector3 localPos)
        {
            return root.transform.TransformPoint(localPos);
        } // a design point of the Forest phases -> world

        Vector3 WW(Vector3 localPos)
        {
            return rootW.transform.TransformPoint(localPos);
        } // a point of the glass world -> world

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
        float K
        {
            get { return Mathf.Clamp(sc * 1.4f, 0.3f, 1f); }
        } // size of arena effects relative to their design size

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

        // ------------------------------------------------------------------ build
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected;
            current = this;
            built = false;
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
            // the Forest phases stand on the real floor: fit the arena onto it like the other events do
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
            float y = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            sc = Mathf.Max(0.2f, AstronomicalSettings.ShowScale.Value);
            bool sl = AstronomicalShowdown.lastFit;
            Vector3 so = AstronomicalShowdown.fitOrigin;
            float sy = AstronomicalShowdown.fitYaw,
                ss = AstronomicalShowdown.fitScale;
            try
            {
                AstronomicalShowdown.FitArena(ref org, ref y, ref sc, JNode.Parse(ARENA_JSON));
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Arena fit failed: " + e.Message);
            }
            AstronomicalShowdown.lastFit = sl;
            AstronomicalShowdown.fitOrigin = so;
            AstronomicalShowdown.fitYaw = sy;
            AstronomicalShowdown.fitScale = ss;
            origin = org;
            yaw = y;
            fitOk = true;
            fitO = origin;
            fitY = yaw;
            // the physics layer the player's hands and body collide with: taken from the real floor, so that our colliders are solid for them too
            floorLayer = FirstLayer(LocoMask());
            RaycastHit h;
            if (Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out h, 8f, LocoMask(), QueryTriggerInteraction.Ignore))
                floorLayer = h.collider.gameObject.layer;
            root = new GameObject("TheUnvaulting");
            root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * sc;
            kit = new WorldKit { Layer = floorLayer };
            var far = new GameObject("UvFar");
            far.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            farT = far.transform;
            glowTex = kit.GlowTex();
            matGlow = kit.Glow(Color.white, glowTex, 3000);
            BuildFx();
            BuildSky();
            BuildPools();
            BuildAudio();
            BuildMountainsAndVolcano();
            BuildVaultWorld();
            BuildVoteWorld();
            BuildZeroPoint();
            BuildIslandWorld();
            BuildFlash();
            BuildRocks();
            BuildCues();
            sun = new GameObject("UvSun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.intensity = 0f;
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b);
            ambientSaved = true;
            foreach (var g in new[] { gV, gW, gI, mountains, volcano, dome })
                if (g != null)
                    g.SetActive(false);
            Array.Clear(local, 0, 6);
            Array.Clear(remote, 0, 6);
            Array.Clear(pend, 0, 6);
            winner = -1;
            winnerSent = false;
            shattered = false;
            lastT = -1f;
            cueIdx = 0;
            curPhase = -1;
            hiderOn = false;
            haveGlass = false;
            built = true;
            AstronomicalEventMod.Log.LogInfo(
                "The Unvaulting built: origin "
                    + origin
                    + " scale "
                    + sc.ToString("0.00")
                    + " yaw "
                    + yaw.ToString("0")
                    + ", collider layer "
                    + floorLayer
                    + ", buildings "
                    + buildings.Count
                    + ", items "
                    + (weaponModels[0] != null)
                    + (weaponModels[1] != null)
                    + (weaponModels[2] != null)
                    + (weaponModels[3] != null)
                    + (weaponModels[4] != null)
                    + (weaponModels[5] != null)
                    + ", zero point "
                    + (zpModel != null)
                    + ", cues "
                    + cues.Count
            );
        }

        static bool fitOk;
        static Vector3 fitO;
        static float fitY;

        public bool TryViewpoint(out Vector3 pos, out float y)
        {
            pos = Vector3.zero;
            y = 0f;
            if (root != null)
            {
                pos = root.transform.position + Vector3.up * 1.2f;
                y = yaw;
                return true;
            }
            if (fitOk)
            {
                pos = fitO + Vector3.up * 1.2f;
                y = fitY;
                return true;
            }
            return false;
        }

        void BuildFx()
        {
            var g = new GameObject("UvFx");
            g.transform.SetParent(root.transform, false);
            fx = g.AddComponent<ParticleSystem>();
            fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = fx.main;
            m.maxParticles = 6000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.playOnAwake = false;
            m.startLifetime = 2f;
            m.gravityModifier = 0.1f;
            var e = fx.emission;
            e.enabled = false;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;
            fx.Play();
            var g2 = new GameObject("UvSmoke");
            g2.transform.SetParent(root.transform, false);
            smoke = g2.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m2 = smoke.main;
            m2.maxParticles = 3500;
            m2.simulationSpace = ParticleSystemSimulationSpace.World;
            m2.playOnAwake = false;
            m2.startLifetime = 12f;
            m2.gravityModifier = -0.008f;
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
            boomLight = AddLight(root.transform, Color.white, 600f, 0f);
        }

        void BuildSky()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            var dirs = new List<Vector3>();
            int lat = 16,
                lon = 28;
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = i * Mathf.PI * 0.52f / lat,
                    ph = j * Mathf.PI * 2f / lon;
                float s = Mathf.Sin(th);
                var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph));
                dirs.Add(d);
                verts.Add(d * 420f);
                cols.Add(Color.white);
            }
            skyGrad = verts.Count;
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
            dome = new GameObject("UvSky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(matGlow);
            dm.renderQueue = 1000;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        Vector3 CraterW()
        {
            return farT.TransformPoint(VOLC + Vector3.up * VOLC_H);
        }

        void UpdateSky(float t, int ph)
        {
            if (skyMesh == null)
                return;
            Color top,
                hor,
                gl;
            Vector3 gd;
            if (ph == 1)
            {
                top = new Color(0.34f, 0.24f, 0.60f);
                hor = new Color(1f, 0.62f, 0.60f);
                gl = new Color(1f, 0.55f, 0.25f);
                gd = (Quaternion.Euler(0, yaw, 0) * new Vector3(-0.6f, 0.12f, 0.8f)).normalized;
            }
            else if (ph == 2)
            {
                top = new Color(0.66f, 0.62f, 0.96f);
                hor = new Color(0.96f, 0.94f, 1f);
                gl = new Color(1f, 1f, 1f);
                gd = Vector3.up;
            }
            else
            {
                float a = Sm(T_BACK, T_BACK + 40f, t);
                top = Color.Lerp(new Color(0.30f, 0.17f, 0.42f), new Color(0.13f, 0.07f, 0.10f), a);
                hor = Color.Lerp(new Color(0.95f, 0.5f, 0.3f), new Color(0.65f, 0.2f, 0.08f), a);
                gl = new Color(1f, 0.45f, 0.12f);
                gd = (CraterW() - dome.transform.position).normalized;
            }
            for (int k = 0; k < skyGrad; k++)
            {
                float y = Mathf.Clamp01(skyDirs[k].y);
                var c = Color.Lerp(hor, top, Mathf.Pow(y, 0.55f));
                float g = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(skyDirs[k], gd)), ph == 2 ? 20f : 5f) * (ph == 2 ? 0.1f : 0.55f);
                skyCols[k] = new Color(c.r + gl.r * g, c.g + gl.g * g, c.b + gl.b * g, 1f);
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
                s.transform.SetParent(farT, false);
                boomMat[k] = kit.Glow(Color.white, glowTex);
                s.GetComponent<Renderer>().sharedMaterial = boomMat[k];
                s.SetActive(false);
                boomSphere[k] = s.transform;
                boomAge[k] = 9f;
            }
            var lt = kit.Tex(
                4,
                32,
                (x, y) =>
                {
                    float v = Mathf.Abs((y + 0.5f) / 16f - 1f);
                    return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f));
                },
                TextureWrapMode.Clamp
            );
            for (int k = 0; k < rings.Length; k++)
            {
                var g = new GameObject("Ring");
                g.transform.SetParent(farT, false);
                var l = g.AddComponent<LineRenderer>();
                l.useWorldSpace = true;
                l.positionCount = 64;
                l.sharedMaterial = kit.Glow(Color.white, lt, 3003);
                l.shadowCastingMode = ShadowCastingMode.Off;
                l.enabled = false;
                rings[k] = new Ring { L = l };
            }
        }

        void BuildAudio()
        {
            if (AstronomicalShowdown.boomClip == null)
                AstronomicalShowdown.MakeSfx();
            var g = new GameObject("UvSfx");
            g.transform.SetParent(farT, false);
            sfx = g.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;
            sfx.playOnAwake = false;
            sfx.priority = 0;
        }

        void BuildFlash()
        { // a white sphere round the head, drawn over everything: it hides the swaps between the places
            flashSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(flashSphere.GetComponent<Collider>());
            flashSphere.name = "UvFlash";
            flashSphere.transform.SetParent(farT, false);
            flashSphere.transform.localScale = Vector3.one * 5f;
            var sh = Shader.Find("UI/Default");
            Material m;
            if (sh != null)
            {
                m = new Material(sh) { color = new Color(1, 1, 1, 0) };
                m.SetInt("unity_GUIZTestMode", 8);
                m.renderQueue = 4000;
                owned.Add(m);
            }
            else
                m = kit.Glow(new Color(1, 1, 1, 0), null, 4000);
            topWhite = m;
            flashSphere.GetComponent<Renderer>().sharedMaterial = m;
            flashSphere.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flashSphere.SetActive(false);
        }

        // the Zero Point (the model made earlier): the big sphere in the middle of the voting area
        void BuildZeroPoint()
        {
            zpModel = def.Prop("zeropoint");
            if (zpModel == null || zpModel.Pivot == null)
            {
                zpModel = null;
                return;
            }
            zpModel.Pivot.transform.SetParent(rootW.transform, false);
            zpModel.Pivot.SetActive(true);
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

        // ------------------------------------------------------------------ rocks and the cue list (every time the user named a rock it is a different rock that hits at that time)
        void BuildRocks()
        {
            var rock = kit.Blob(1f, 0.8f, 0.9f, 17, 0.35f, 12, 16);
            var tex = kit.Tex(
                64,
                64,
                (x, y) =>
                {
                    float n = WorldKit.Fbm(x * 0.12f, y * 0.12f, 81);
                    float cr = Mathf.Clamp01((WorldKit.Fbm(x * 0.2f, y * 0.2f, 91) - 0.55f) * 8f);
                    return Color.Lerp(new Color(0.16f, 0.1f, 0.09f) * (0.6f + n), new Color(1f, 0.45f, 0.08f), cr);
                }
            );
            Vector3 crater = CraterW();
            float rs = Mathf.Max(0.5f, sc * 1.5f);
            Vector3 splitP = W(new Vector3(-10f, 175f, 150f));
            Func<float, float, float, Vector3, Vector3, float, int, RockF> mk = (t0, t1, size, a, b, arc, kind) =>
            {
                var r = new RockF
                {
                    T0 = t0,
                    T1 = t1,
                    A = a,
                    B = b,
                    Arc = arc,
                    Size = size * rs,
                    Kind = kind,
                };
                var g = new GameObject("Rock" + rocks.Count);
                g.transform.SetParent(farT, false);
                g.AddComponent<MeshFilter>().sharedMesh = rock;
                var mr = g.AddComponent<MeshRenderer>();
                r.Mat = kit.Lit(Color.white, tex, 0.1f);
                mr.sharedMaterial = r.Mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.allowOcclusionWhenDynamic = false;
                g.transform.localScale = Vector3.one * r.Size;
                r.G = g.transform;
                var gl = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(gl.GetComponent<Collider>());
                gl.transform.SetParent(g.transform, false);
                gl.transform.localScale = Vector3.one * 2.8f;
                gl.GetComponent<Renderer>().sharedMaterial = kit.Glow(new Color(1f, 0.5f, 0.12f, 0.55f), glowTex, 2999);
                r.Glow = gl.transform;
                g.SetActive(false);
                rocks.Add(r);
                return r;
            };
            mk(T_BACK + 1f, T_ROCK_A, 9f, crater, W(ROCK_A), 160f, 0); // 4:41 lands on the map
            mk(288f, 308f, 10f, crater, W(RETAIL), 180f, 1); // 5:08 a different one lands on Retail Row
            mk(300f, 319f, 11f, crater, splitP, 70f, 2); // 5:19 one explodes into three in the air ...
            Vector3[] tp = { TILTED + new Vector3(-18f, 0f, -6f), TILTED + new Vector3(14f, 0f, 10f), TILTED + new Vector3(-2f, 0f, -16f) };
            for (int k = 0; k < 3; k++)
                mk(319f, 328f + 0.45f * k, 5.5f, splitP, W(tp[k]), 30f, 3); // ... and the pieces hit Tilted Towers at 5:28
            mk(308f, 330f, 15f, crater, W(TILTED), 200f, 4); // 5:30 one big one hits Tilted again
            impacts.Add(
                new Impact
                {
                    T = 308f,
                    P = W(RETAIL),
                    R = 62f * sc,
                }
            );
            for (int k = 0; k < 3; k++)
                impacts.Add(
                    new Impact
                    {
                        T = 328f + 0.45f * k,
                        P = W(tp[k]),
                        R = 34f * sc,
                    }
                );
            impacts.Add(
                new Impact
                {
                    T = 330f,
                    P = W(TILTED),
                    R = 95f * sc,
                }
            );
            foreach (var b in buildings)
            {
                var p = b.T.position;
                foreach (var im in impacts)
                {
                    float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(im.P.x, 0, im.P.z));
                    if (d < im.R)
                        b.FallT = Mathf.Min(b.FallT, im.T + 0.3f + 0.9f * d / im.R);
                }
                b.Lean = (WorldKit.Hash((int)(p.x * 3f), (int)(p.z * 3f), 7) - 0.5f) * 14f;
            }
        }

        void BuildCues()
        {
            Action<float, int, Vector3, float, Color, bool> C = (t, ty, p, s, col, wd) =>
                cues.Add(
                    new Cue
                    {
                        T = t,
                        Type = ty,
                        P = p,
                        S = s,
                        C = col,
                        World = wd,
                    }
                );
            Color wh = Color.white,
                or = new Color(1f, 0.55f, 0.15f);
            Vector3 cr = CraterW();
            C(T_BACK, 1, cr, 260f, or, true);
            C(T_BACK, 2, new Vector3(cr.x, origin.y, cr.z), 400f, or, true);
            C(T_BACK + 0.3f, 2, new Vector3(cr.x, origin.y, cr.z), 600f, wh, true);
            C(T_BACK + 1f, 3, new Vector3(cr.x, origin.y, cr.z), 30f, or, true);
            foreach (var r in rocks)
            {
                if (r.Kind != 3)
                    C(r.T0, 5, r.A, 30f, or, true);
                if (r.Kind == 2)
                {
                    C(r.T1, 4, r.B, 70f, or, true);
                    continue;
                }
                float kr = r.Kind == 4 ? 150f
                    : r.Kind == 3 ? 90f
                    : 110f,
                    kv = r.Kind == 4 ? 20f : 14f; // every rock that lands throws the player back: a hard push at the hit and a second one just after
                cues.Add(
                    new Cue
                    {
                        T = r.T1,
                        Type = 6,
                        P = r.B,
                        S = kr,
                        C = or,
                        World = true,
                        V = kv,
                    }
                );
                cues.Add(
                    new Cue
                    {
                        T = r.T1 + 0.3f,
                        Type = 6,
                        P = r.B,
                        S = kr * 1.15f,
                        C = or,
                        World = true,
                        V = kv * 0.8f,
                    }
                );
                C(
                    r.T1,
                    1,
                    r.B,
                    r.Kind == 4 ? 190f
                        : r.Kind == 3 ? 100f
                        : 140f,
                    or,
                    true
                );
                C(r.T1, 2, r.B, r.Kind == 4 ? 520f : 300f, wh, true);
                C(r.T1, 3, r.B, r.Kind == 4 ? 26f : 16f, or, true);
            }
            cues.Sort((a, b) => a.T.CompareTo(b.T));
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
        }

        const float R_MAX = 470f;

        void SetPhase(int ph)
        {
            int prev = curPhase;
            curPhase = ph;
            bool forestPhase = ph == 1 || ph == 3;
            gV.SetActive(ph == 1);
            gI.SetActive(ph == 3);
            rootW.SetActive(ph == 2);
            mountains.SetActive(forestPhase);
            volcano.SetActive(forestPhase);
            dome.SetActive(ph >= 1);
            if (ph == 2)
                EnterGlass();
            else if (hiderOn)
            {
                AstronomicalForestHider.Restore();
                hiderOn = false;
                if (prev == 2 && ph == 3)
                    ToStage();
            }
            skyT = -9f;
        }

        // when the voting area is left the player is put at the stage position (the saved stage spot; without one, the start of the arena)
        void ToStage()
        {
            try
            {
                var p = GTPlayer.Instance;
                if (p == null)
                    return;
                Vector3 pos;
                Quaternion rot;
                if (AstronomicalSettings.FixedSpawnX.Value != 0 || AstronomicalSettings.FixedSpawnZ.Value != 0)
                {
                    pos = new Vector3(
                        AstronomicalSettings.FixedSpawnX.Value,
                        AstronomicalSettings.FixedSpawnY.Value + 1.2f,
                        AstronomicalSettings.FixedSpawnZ.Value
                    );
                    rot = Quaternion.Euler(0, AstronomicalSettings.FixedSpawnYaw.Value, 0);
                }
                else
                {
                    pos = origin + Vector3.up * 1.2f;
                    rot = Quaternion.Euler(0, yaw, 0);
                }
                p.TeleportTo(pos, rot, false, false);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Stage teleport: " + e.Message);
            }
        }

        // the voting area stands where the player is right now, level with the real floor under their feet; the Forest is switched off around it
        void EnterGlass()
        {
            Vector3 head = origin + Vector3.up * 1.4f;
            float gy = origin.y;
            int layer = floorLayer;
            float gyaw = yaw;
            var cam = Camera.main;
            try
            {
                var p = GTPlayer.Instance;
                if (p != null)
                    head = p.headCollider.transform.position;
            }
            catch { }
            RaycastHit h;
            if (Physics.Raycast(head, Vector3.down, out h, 10f, LocoMask(), QueryTriggerInteraction.Ignore))
            {
                gy = h.point.y;
                layer = h.collider.gameObject.layer;
            }
            else
                gy = head.y - 1.4f;
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.01f)
                    gyaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
            glassOrigin = new Vector3(head.x, gy, head.z);
            glassYaw = gyaw;
            haveGlass = true;
            rootW.transform.SetPositionAndRotation(glassOrigin, Quaternion.Euler(0, gyaw, 0));
            foreach (var c in rootW.GetComponentsInChildren<Collider>(true))
                c.gameObject.layer = layer;
            AstronomicalForestHider.Hide(
                glassOrigin,
                R_MAX,
                tr =>
                    tr == root.transform
                    || tr.IsChildOf(root.transform)
                    || tr == farT
                    || tr.IsChildOf(farT)
                    || tr == rootW.transform
                    || tr.IsChildOf(rootW.transform)
                    || tr == dome.transform
                    || tr.IsChildOf(dome.transform)
            );
            hiderOn = true;
        }

        public void Apply(float t, int i, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            bool back = t < lastT - 0.4f;
            if (back || lastT < 0f)
            {
                cueIdx = 0;
                shattered = false;
                absorbed = false;
                skyT = -9f;
                foreach (var r in rings)
                    r.Age = 9f;
                if (back)
                {
                    winner = -1;
                    winnerSent = false;
                }
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null)
                dome.transform.position = cam.transform.position;
            int ph =
                t < T_START ? 0
                : t < T_JUMP ? 1
                : t < T_BACK ? 2
                : 3;
            if (ph != curPhase)
                SetPhase(ph);
            FlashStep(t, cam);
            while (cueIdx < cues.Count && cues[cueIdx].T <= t)
            {
                var c = cues[cueIdx++];
                if (t - c.T < 1.5f && !paused)
                {
                    try
                    {
                        RunCue(c, t);
                    }
                    catch (Exception e)
                    {
                        AstronomicalEventMod.Log.LogWarning("Cue: " + e.Message);
                    }
                }
            }
            if (ph >= 1)
            {
                Atmosphere(t, ph);
                if (t - skyT > 0.1f || t < skyT)
                {
                    skyT = t;
                    UpdateSky(t, ph);
                }
                if (ph == 1)
                    VaultStep(t, dt, cam);
                else if (ph == 2)
                    VoteStep(t, dt, cam);
                if (ph != 2)
                    VolcanoStep(t, dt, ph);
                if (ph == 3)
                {
                    RocksStep(t, dt);
                    BuildingsStep(t, dt);
                }
            }
            UpdateBooms(dt);
            UpdateRings(dt);
            boomI = Mathf.Max(0f, boomI - dt * 14f);
            boomLight.intensity = boomI * Lit;
            Shake(Mathf.Clamp01(Curve(SHAKE, t) + shakeBump), dt, t);
            shakeBump = Mathf.Max(0f, shakeBump - dt * 2.5f);
        }

        void FlashStep(float t, Camera cam)
        {
            float a = 0f;
            foreach (float tc in new[] { T_START, T_JUMP, T_BACK })
            {
                float v = t < tc ? Sm(tc - 0.7f, tc, t) : 1f - Sm(tc, tc + 1.8f, t);
                if (t < tc - 0.7f)
                    v = 0f;
                a = Mathf.Max(a, v);
            }
            bool on = a > 0.003f;
            if (flashSphere.activeSelf != on)
                flashSphere.SetActive(on);
            if (!on)
                return;
            topWhite.color = new Color(1f, 1f, 1f, a);
            if (cam != null)
                flashSphere.transform.position = cam.transform.position;
        }

        void Atmosphere(float t, int ph)
        {
            Color amb,
                sc2;
            float si;
            Quaternion rot;
            if (ph == 1)
            {
                amb = new Color(0.50f, 0.42f, 0.58f);
                sc2 = new Color(1f, 0.72f, 0.52f);
                si = 1.15f;
                rot = Quaternion.Euler(22f, yaw - 40f, 0);
            }
            else if (ph == 2)
            {
                amb = new Color(0.78f, 0.78f, 0.92f);
                sc2 = new Color(0.96f, 0.95f, 1f);
                si = 1.0f;
                rot = Quaternion.Euler(60f, yaw + 20f, 0);
            }
            else
            {
                float a = Sm(T_BACK, T_BACK + 40f, t);
                amb = Color.Lerp(new Color(0.42f, 0.30f, 0.38f), new Color(0.28f, 0.14f, 0.12f), a);
                sc2 = Color.Lerp(new Color(1f, 0.62f, 0.4f), new Color(0.9f, 0.35f, 0.18f), a);
                si = Mathf.Lerp(1.0f, 0.8f, a);
                rot = Quaternion.Euler(28f, yaw + 150f, 0);
            }
            RenderSettings.ambientLight = amb;
            sun.color = sc2;
            sun.intensity = si;
            sun.transform.rotation = rot;
        }

        // ---- 0:15 - 0:55: the Vault site with the Zero Point over the hatch
        void VaultStep(float t, float dt, Camera cam) // (the cam parameter is kept for symmetry)
        {
            if (vault != null && vault.Clips.Count > 0)
                vault.Sample(vault.Clips[0], t);
            vaultLight.intensity = (4.5f + 1.2f * Mathf.Sin(t * 2f)) * Lit;
            if (!paused)
            {
                int n = Take(ref accA, 14f * Parts, dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        W(new Vector3(SITE.x, 6f, SITE.z)) + UnityEngine.Random.insideUnitSphere * 4f * sc,
                        new Vector3(Rnd(-2f, 2f), Rnd(10f, 30f), Rnd(-2f, 2f)) * sc,
                        new Color(0.7f, 0.95f, 1f, 0.8f),
                        Rnd(0.8f, 2f) * Mathf.Max(0.4f, sc),
                        3.5f
                    );
            }
        }

        // ---- 0:55 - 4:26: the voting area
        void VoteStep(float t, float dt, Camera cam)
        {
            bool voting = t < T_BREAK;
            if (!paused)
            { // the ink above the dome and sparkles in the air
                int n = Take(ref accB, 22f * Parts, dt);
                for (int q = 0; q < n; q++)
                {
                    float a = Rnd(0f, 6.2832f),
                        r = Rnd(0f, 5f);
                    Emit(
                        smoke,
                        WW(DC + new Vector3(Mathf.Cos(a) * r, 2f * ZP_R + 6f, Mathf.Sin(a) * r)),
                        new Vector3(Mathf.Cos(a + 1.5f) * Rnd(1f, 5f), Rnd(4f, 12f), Mathf.Sin(a + 1.5f) * Rnd(1f, 5f)),
                        new Color(0.12f, 0.08f, 0.30f, 0.6f),
                        Rnd(5f, 10f),
                        4.5f
                    );
                }
                int m = Take(ref accC, 30f * Parts, dt);
                for (int q = 0; q < m; q++)
                    Emit(
                        fx,
                        WW(new Vector3(Rnd(-45f, 45f), Rnd(0.5f, 25f), Rnd(-25f, 70f))),
                        new Vector3(Rnd(-0.5f, 0.5f), Rnd(-1.5f, 0.5f), Rnd(-0.5f, 0.5f)),
                        new Color(0.85f, 0.95f, 1f, 0.9f),
                        Rnd(0.12f, 0.35f),
                        3f
                    );
            }
            if (zpModel != null && zpAnchor != null)
            {
                float kk =
                    ZP_R
                    * (
                        1f
                        + 0.03f * Mathf.Sin(t * 2.3f)
                        + (t >= T_BREAK ? 0.12f * Mathf.Sin(t * 9f) * (1f - Sm(T_BREAK, T_BREAK + 3f, t)) : 0f)
                        + (t >= T_BREAK + 6f ? 0.3f * Mathf.Exp(-(t - T_BREAK - 6f) * 2.5f) : 0f)
                    );
                Vector3 zc = zpAnchor.position;
                if (cam != null)
                {
                    Vector3 fz = cam.transform.position - zc;
                    fz.y = 0f;
                    if (fz.sqrMagnitude > 0.01f)
                        zpModel.Pivot.transform.rotation = Quaternion.LookRotation(fz.normalized, Vector3.up);
                }
                zpModel.Pivot.transform.localScale = Vector3.one * kk;
                zpModel.Pivot.transform.position = zc - zpModel.Pivot.transform.up * (zpHalf * kk);
                if (zpModel.Clips.Count > 0)
                    zpModel.Sample(zpModel.Clips[0], t);
            }
            if (voting)
            {
                HandStep(t, dt);
                Network(t);
                if (t >= T_WIN && winner < 0)
                    winner = Decide();
                if (t >= T_WIN && !winnerSent && winner >= 0)
                {
                    winnerSent = true;
                    if (Host)
                        AstronomicalNet.Publish("winner", def.Id, 0f, winner);
                }
            }
            else if (winner < 0)
                winner = Decide();
            float bp = Sm(T_BREAK, T_BREAK + 6f, t);
            int maxTot = 1;
            for (int i = 0; i < 6; i++)
                maxTot = Mathf.Max(maxTot, Total(i));
            for (int i = 0; i < 6; i++)
            {
                var P = pillars[i];
                if (P == null)
                    continue;
                bool win = i == winner;
                P.Flash = Mathf.Max(0f, P.Flash - dt * 3f);
                int tot = Total(i);
                float vis =
                    t < T_BREAK ? 1f
                    : win ? 0f
                    : 1f - Sm(T_BREAK, T_BREAK + 4f, t) * 0.92f;
                float share = tot / (float)maxTot;
                P.SlabMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(vis * (0.85f + 0.35f * P.Flash)));
                if (P.CoreMat != null)
                    P.CoreMat.color = new Color(
                        1f,
                        0.85f + 0.15f * P.Flash,
                        1f,
                        Mathf.Clamp01(vis * (0.55f + 0.5f * share + 0.7f * P.Flash))
                    );
                if (P.LightMat != null)
                    P.LightMat.color = new Color(
                        1f,
                        1f,
                        1f,
                        Mathf.Clamp01(0.55f + 0.45f * share + 0.5f * P.Flash)
                            * (t >= T_BREAK && !win ? 1f - Sm(T_BREAK, T_BREAK + 4f, t) : 1f)
                    );
                P.Slab.gameObject.SetActive(vis > 0.02f);
                if (P.Shards != null)
                {
                    bool sh = win && t >= T_BREAK && t < T_BREAK + 5f;
                    if (P.ShardRoot.gameObject.activeSelf != sh)
                        P.ShardRoot.gameObject.SetActive(sh);
                    if (sh)
                        ShardsStep(P, t - T_BREAK);
                }
                // the glass cracks as the votes come in (more hits, more cracks): this is the only vote meter, there is no text
                float prog = t >= T_BREAK && win ? 1f : Mathf.Clamp01(tot / 70f);
                int shown = Mathf.CeilToInt(prog * P.Cracks.Length);
                for (int c = 0; c < P.Cracks.Length; c++)
                {
                    bool on = c < shown && vis > 0.05f;
                    if (P.Cracks[c].enabled != on)
                        P.Cracks[c].enabled = on;
                    if (on && P.Cracks[c].positionCount != P.CrackPts[c].Length)
                    {
                        P.Cracks[c].positionCount = P.CrackPts[c].Length;
                        P.Cracks[c].SetPositions(P.CrackPts[c]);
                    }
                }
                if (P.ItemHolder != null)
                {
                    float spin = t * 40f + i * 60f;
                    Vector3 pos = P.ItemPos + new Vector3(0, 0.25f * Mathf.Sin(t * 1.3f + i), 0);
                    float scl = 1f;
                    if (t >= T_BREAK)
                    {
                        if (win)
                        {
                            Vector3 zl = zpAnchor != null ? zpAnchor.localPosition : DC + new Vector3(0, ZP_R + 3.5f, 0);
                            float fly = Sm(T_BREAK + 0.5f, T_BREAK + 6f, t);
                            pos = Vector3.Lerp(P.ItemPos, zl, fly) + Vector3.up * (Mathf.Sin(fly * Mathf.PI) * 6f);
                            scl = Mathf.Lerp(1f, 2.0f, Mathf.Min(1f, fly * 2f)) * (1f - Sm(0.72f, 1f, fly));
                        }
                        else
                            scl = 1f - Sm(T_BREAK, T_BREAK + 3f, t);
                    }
                    P.ItemHolder.localPosition = pos;
                    P.ItemHolder.localRotation = Quaternion.Euler(0, spin, 0);
                    P.ItemHolder.localScale = Vector3.one * Mathf.Max(0.001f, scl);
                    if (weaponModels[i] != null && weaponModels[i].Clips.Count > 0)
                        weaponModels[i].Sample(weaponModels[i].Clips[0], t);
                }
            }
            if (t >= T_BREAK + 6f && winner >= 0 && !absorbed && !paused && zpAnchor != null)
            {
                absorbed = true;
                Vector3 zc2 = zpAnchor.position;
                Burst(zc2, (int)(120 * Parts), new Color(0.8f, 0.9f, 1f, 0.95f), 22f, 4f, 1.4f, 2.2f);
                boomI = Mathf.Max(boomI, 16f);
                boomLight.transform.position = zc2;
                boomLight.color = new Color(0.7f, 0.85f, 1f);
                React(zc2, 70f, 0);
            }
            if (t >= T_BREAK && winner >= 0 && !shattered && !paused)
            {
                shattered = true;
                var P = pillars[winner];
                Burst(
                    WW(P.T.localPosition + new Vector3(0, 6, 0)),
                    (int)(180 * Parts),
                    new Color(0.85f, 0.75f, 1f, 0.95f),
                    28f,
                    6f,
                    1.6f,
                    3f
                );
                Burst(WW(P.T.localPosition + new Vector3(0, 2.5f, 0)), (int)(80 * Parts), Color.white, 20f, 4f, 0.9f, 2.5f);
                boomI = Mathf.Max(boomI, 14f);
                boomLight.transform.position = WW(DC + new Vector3(0, ZP_R + 3, 0));
                boomLight.color = new Color(0.8f, 0.7f, 1f);
                React(WW(DC), 90f, 0);
            }
        }

        // the shards of the glass thrown out of the winning pillar when it breaks (a pure function of the time since the break)
        void ShardsStep(Pillar P, float tau)
        {
            for (int k = 0; k < P.Shards.Length; k++)
            {
                var c = P.Shards[k];
                c.localPosition = P.ShardP[k] + P.ShardV[k] * tau + Vector3.down * (0.5f * 20f * tau * tau);
                if (c.localPosition.y < 0.15f)
                    c.localPosition = new Vector3(c.localPosition.x, 0.15f, c.localPosition.z);
                c.localRotation = P.ShardR[k] * Quaternion.Euler(P.ShardSpin[k] * Mathf.Min(tau, 1.6f));
                c.localScale = Vector3.one * Mathf.Clamp01((5f - tau) / 1.5f);
            }
        }

        // fists: a hand moving fast into a pillar's glass counts as a vote
        void HandStep(float t, float dt)
        {
            if (dt <= 0f || t < T_JUMP + 1.5f)
                return;
            var gt = GorillaTagger.Instance;
            if (gt == null)
                return;
            for (int h = 0; h < 2; h++)
            {
                Transform ht = h == 0 ? gt.leftHandTransform : gt.rightHandTransform;
                if (ht == null)
                    continue;
                Vector3 p = ht.position;
                if (!handInit[h])
                {
                    handInit[h] = true;
                    handPrev[h] = p;
                    continue;
                }
                Vector3 v = (p - handPrev[h]) / dt;
                handPrev[h] = p;
                float sp = v.magnitude;
                if (sp < 1.6f || Time.unscaledTime < handCool[h])
                    continue;
                for (int i = 0; i < 6; i++)
                {
                    var P = pillars[i];
                    if (P == null)
                        continue;
                    Vector3 lp = P.T.InverseTransformPoint(p);
                    if (
                        Mathf.Abs(lp.x) < 2.4f
                        && Mathf.Abs(lp.z) < 2.4f
                        && lp.y > -0.3f
                        && lp.y < 5.5f
                        && Vector3.Dot(v, P.T.position + Vector3.up * lp.y - p) > 0f
                    )
                    {
                        Vote(i, p, sp, h);
                        handCool[h] = Time.unscaledTime + 0.18f;
                        break;
                    }
                }
            }
        }

        void Vote(int i, Vector3 p, float speed, int hand)
        {
            local[i]++;
            pend[i]++;
            pillars[i].Flash = 1f;
            shakeBump = Mathf.Max(shakeBump, 0.12f);
            try
            {
                var gt = GorillaTagger.Instance;
                if (gt != null)
                    gt.StartVibration(hand == 0, Mathf.Clamp01(0.4f + speed * 0.05f), 0.08f);
            }
            catch { }
            Burst(p, (int)(10 * Parts), new Color(1f, 0.9f, 1f, 0.9f), 6f, 1f, 0.35f, 0.7f);
            try
            {
                if (AstronomicalShowdown.thumpClip == null)
                    AstronomicalShowdown.MakeSfx();
                sfx.PlayOneShot(AstronomicalShowdown.thumpClip, 0.4f * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f));
            }
            catch { }
        }

        // tell the other mod users about our new hits (one message every 2.5 s at most, the item with the most new hits first)
        void Network(float t)
        {
            if (Time.unscaledTime < nextSend)
                return;
            int best = -1,
                bv = 0;
            for (int i = 0; i < 6; i++)
                if (pend[i] > bv)
                {
                    bv = pend[i];
                    best = i;
                }
            if (best < 0)
                return;
            nextSend = Time.unscaledTime + 2.5f;
            AstronomicalNet.Publish("vote", def.Id, bv, best);
            pend[best] = 0;
        }

        // ---- the volcano, falling rocks, buildings
        void VolcanoStep(float t, float dt, int ph)
        {
            bool erupt = t >= T_BACK;
            float k = erupt ? Mathf.Clamp01(1f - (t - T_BACK) / 120f) * 0.5f + 0.5f : 0f;
            Vector3 cr = CraterW();
            float lv = erupt ? 1f : 0.82f;
            foreach (var lm in lavaMats)
                lm.color = new Color(1f, 1f, 1f, Mathf.Clamp01(lv * (0.88f + 0.12f * Mathf.Sin(t * (erupt ? 5f : 1.6f)))));
            volcLight.intensity = (erupt ? 6f + 2f * Mathf.Sin(t * 4f) : 1.2f) * Lit;
            volcLight.transform.position = cr + Vector3.up * 20f;
            if (paused)
                return;
            int n = Take(ref accD, (erupt ? 150f * k : 16f) * Parts, dt);
            for (int q = 0; q < n; q++)
                Emit(
                    smoke,
                    cr + UnityEngine.Random.insideUnitSphere * 14f,
                    new Vector3(Rnd(-10f, 10f), Rnd(erupt ? 40f : 10f, erupt ? 110f : 22f), Rnd(-10f, 10f)),
                    erupt ? new Color(0.22f, 0.18f, 0.17f, 0.7f) : new Color(0.55f, 0.5f, 0.5f, 0.4f),
                    Rnd(25f, 55f),
                    Rnd(9f, 14f)
                );
            if (erupt)
            {
                int m = Take(ref accE, 90f * k * Parts, dt);
                for (int q = 0; q < m; q++)
                    Emit(
                        fx,
                        cr + UnityEngine.Random.insideUnitSphere * 8f,
                        new Vector3(Rnd(-35f, 35f), Rnd(60f, 150f), Rnd(-35f, 35f)),
                        Color.Lerp(new Color(1f, 0.85f, 0.4f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), UnityEngine.Random.value),
                        Rnd(5f, 12f),
                        Rnd(2f, 4f)
                    );
            }
        }

        Vector3 RockPos(RockF r, float t)
        {
            float u = Mathf.Clamp01((t - r.T0) / (r.T1 - r.T0));
            return r.A + (r.B - r.A) * u + Vector3.up * r.Arc * 4f * u * (1f - u);
        }

        void RocksStep(float t, float dt)
        {
            var cam = Camera.main;
            foreach (var r in rocks)
            {
                bool on = t >= r.T0 && t <= r.T1;
                if (r.G.gameObject.activeSelf != on)
                    r.G.gameObject.SetActive(on);
                if (!on)
                    continue;
                Vector3 p = RockPos(r, t),
                    p2 = RockPos(r, Mathf.Min(t + 0.1f, r.T1));
                r.G.position = p;
                r.G.rotation = Quaternion.Euler(t * 20f + r.T0, t * 35f, t * 12f);
                if (cam != null)
                    r.Glow.rotation = Quaternion.LookRotation(r.Glow.position - cam.transform.position);
                if (paused)
                    continue;
                Vector3 dir = (p2 - p).normalized;
                int n = Take(ref r.AccA, 110f * Parts * (r.Size / 10f), dt);
                for (int q = 0; q < n; q++)
                    Emit(
                        fx,
                        p + UnityEngine.Random.insideUnitSphere * r.Size * 0.6f,
                        -dir * Rnd(10f, 40f) + UnityEngine.Random.insideUnitSphere * 8f,
                        Color.Lerp(new Color(1f, 0.9f, 0.5f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), UnityEngine.Random.value),
                        Rnd(0.8f, 1.5f) * r.Size,
                        Rnd(0.8f, 1.6f)
                    );
                int m = Take(ref r.AccB, 30f * Parts, dt);
                for (int q = 0; q < m; q++)
                    Emit(
                        smoke,
                        p + UnityEngine.Random.insideUnitSphere * r.Size * 0.5f,
                        -dir * Rnd(2f, 8f),
                        new Color(0.2f, 0.17f, 0.16f, 0.6f),
                        Rnd(1.6f, 2.8f) * r.Size,
                        Rnd(7f, 11f)
                    );
            }
        }

        void BuildingsStep(float t, float dt)
        {
            foreach (var b in buildings)
            {
                float p = Mathf.Clamp01((t - b.FallT) / 3.2f);
                var tr = b.T;
                if (p <= 0.001f)
                {
                    if (tr.localPosition.y != b.Y0 || tr.localRotation != Quaternion.identity)
                    {
                        tr.localPosition = new Vector3(tr.localPosition.x, b.Y0, tr.localPosition.z);
                        tr.localRotation = Quaternion.identity;
                        tr.localScale = Vector3.one;
                    }
                    if (b.Col != null && !b.Col.enabled)
                        b.Col.enabled = true;
                    continue;
                }
                float e = p * p;
                float shake = (1f - p) * Mathf.Sin(t * 40f + b.Lean) * 0.5f * Mathf.Min(1f, p * 8f);
                tr.localPosition = new Vector3(tr.localPosition.x, b.Y0 - b.Size.y * 0.8f * e + shake * 0.3f, tr.localPosition.z);
                tr.localRotation = Quaternion.Euler(b.Lean * e, 0f, -b.Lean * e * 0.6f);
                tr.localScale = new Vector3(1f, 1f - 0.12f * e, 1f);
                if (b.Col != null && b.Col.enabled && p > 0.35f)
                    b.Col.enabled = false;
                if (!paused && p < 0.95f)
                {
                    int n = Take(ref accA, 12f * Parts, dt * 0.2f);
                    for (int q = 0; q < n; q++)
                        Emit(
                            smoke,
                            W(
                                tr.localPosition
                                    + new Vector3(Rnd(-b.Size.x, b.Size.x) * 0.5f, Rnd(0f, b.Size.y), Rnd(-b.Size.z, b.Size.z) * 0.5f)
                            ),
                            new Vector3(Rnd(-4f, 4f), Rnd(0f, 6f), Rnd(-4f, 4f)) * Mathf.Max(0.4f, sc),
                            new Color(0.5f, 0.46f, 0.43f, 0.6f),
                            Rnd(6f, 14f) * Mathf.Max(0.4f, sc),
                            Rnd(3f, 6f)
                        );
                }
            }
        }

        // ------------------------------------------------------------------ cues, booms, rings
        void RunCue(Cue c, float t)
        {
            Vector3 p = c.World ? c.P : W(c.P);
            float k = c.World && c.Type != 1 && c.Type != 2 && c.Type != 3 && c.Type != 4 ? 1f : (c.World ? Mathf.Max(0.3f, K) : K);
            if (c.World && c.P.y > origin.y + 60f && c.Type == 1)
                k = 1f;
            float S = c.S * k;
            switch (c.Type)
            {
                case 0:
                    boomI = Mathf.Max(boomI, 10f + S * 0.2f);
                    boomLight.transform.position = p;
                    boomLight.color = c.C;
                    break;
                case 1:
                    Boom(p, S, c.C, S >= 70f);
                    React(p, S, 0);
                    break;
                case 2:
                {
                    var r = rings[ringNext];
                    ringNext = (ringNext + 1) % rings.Length;
                    r.Age = 0f;
                    r.C = new Vector3(p.x, p.y + 1f, p.z);
                    r.R = S;
                    r.Col = c.C;
                    r.L.enabled = true;
                    r.Life = 1.8f + S * 0.002f;
                    React(r.C, Mathf.Min(S, 160f) * 0.6f, 2);
                    break;
                }
                case 3:
                    Burst(p, (int)(S * 12f * Parts), new Color(0.45f, 0.4f, 0.35f, 0.6f), S * 3f, S * 0.6f, S * 1.3f, 5f);
                    break;
                case 4:
                    Boom(p, S, c.C, false);
                    Burst(p, (int)(120 * Parts), c.C, 50f * k, 10f, 4f * Mathf.Max(0.5f, K), 2f);
                    React(p, 90f, 0);
                    break;
                case 5:
                    Burst(p, (int)(40 * Parts), c.C, 30f, 30f, 6f, 2.5f);
                    break;
                case 6:
                    AstronomicalHazards.Blast(p, c.S, c.V, 20f);
                    break;
            }
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
            Burst(p, (int)(Mathf.Clamp(size * 2f, 20f, 240f) * Parts), c, size * 0.9f, size * 0.4f, size * 0.12f, 2.6f);
            boomI = Mathf.Max(boomI, 6f + size * 0.25f);
            boomLight.transform.position = p + Vector3.up * size * 0.2f;
            boomLight.color = c;
            var cam = Camera.main;
            if (knock && cam != null && Vector3.Distance(cam.transform.position, p) < 400f)
                AstronomicalHazards.Blast(p, Mathf.Min(300f, size * 2.4f), 4f + size * 0.12f);
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
                r.L.widthMultiplier = (1f + r.R * 0.03f) * (1f - f * 0.7f);
                for (int k = 0; k < 64; k++)
                {
                    float a = k * Mathf.PI * 2f / 63f;
                    r.L.SetPosition(k, r.C + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rad);
                }
            }
        }

        void React(Vector3 at, float size, int kind)
        {
            try
            {
                if (kind == 2 && Time.unscaledTime < reactCool)
                    return;
                var cam = Camera.main;
                if (cam == null)
                    return;
                float d = Vector3.Distance(cam.transform.position, at);
                float reach = Mathf.Max(80f, size * 7f);
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
            if (dt <= 0f)
                return;
            rumbleT += dt;
            if (rumbleT < 0.12f)
                return;
            rumbleT = 0f;
            if (power < 0.05f)
                return;
            try
            {
                var gt = GorillaTagger.Instance;
                if (gt != null)
                {
                    gt.StartVibration(true, Mathf.Clamp01(power * 0.7f), 0.14f);
                    gt.StartVibration(false, Mathf.Clamp01(power * 0.7f), 0.14f);
                }
            }
            catch { }
        }

        public string DebugInfo()
        {
            int fall = 0;
            foreach (var b in buildings)
                if (lastT >= b.FallT)
                    fall++;
            int act = 0;
            foreach (var r in rocks)
                if (r.G != null && r.G.gameObject.activeSelf)
                    act++;
            return "phase "
                + curPhase
                + "  forest "
                + (AstronomicalForestHider.Hidden ? "unloaded" : "on")
                + "  layer "
                + floorLayer
                + "  scale "
                + sc.ToString("0.00")
                + "  votes "
                + Total(0)
                + "/"
                + Total(1)
                + "/"
                + Total(2)
                + "/"
                + Total(3)
                + "/"
                + Total(4)
                + "/"
                + Total(5)
                + "  winner "
                + winner
                + "\nbuildings down "
                + fall
                + "/"
                + buildings.Count
                + "  rocks "
                + act
                + "  particles "
                + (fx != null ? fx.particleCount : 0)
                + "+"
                + (smoke != null ? smoke.particleCount : 0);
        }

        // ------------------------------------------------------------------ cleanup: the map comes back; a player left away from where the glass world stood is put back
        public void Cleanup()
        {
            AstronomicalHazards.Reset();
            current = null;
            try
            {
                bool wasGlass = hiderOn;
                AstronomicalForestHider.Restore();
                hiderOn = false;
                if (wasGlass)
                    ToStage();
                var p = GTPlayer.Instance;
                if (p != null && haveGlass)
                {
                    Vector3 head = p.headCollider.transform.position;
                    if (head.y < glassOrigin.y - 1.2f)
                        p.TeleportTo(glassOrigin + Vector3.up * 1.2f, Quaternion.Euler(0, glassYaw, 0), false, false);
                }
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Restore: " + e.Message);
            }
            if (ambientSaved)
            {
                RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z);
                ambientSaved = false;
            }
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var m = weaponModels[i];
                    if (m != null && m.Pivot != null)
                    {
                        m.Pivot.transform.SetParent(null, false);
                        m.Pivot.SetActive(false);
                        AstronomicalEventMod.ParkModel(m.Pivot);
                    }
                    weaponModels[i] = null;
                }
                if (vault != null && vault.Pivot != null)
                {
                    vault.Pivot.transform.SetParent(null, false);
                    vault.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(vault.Pivot);
                }
                if (volcModel != null && volcModel.Pivot != null)
                {
                    volcModel.Pivot.transform.SetParent(null, false);
                    volcModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(volcModel.Pivot);
                }
                if (zpModel != null && zpModel.Pivot != null)
                {
                    zpModel.Pivot.transform.SetParent(null, false);
                    zpModel.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(zpModel.Pivot);
                }
            }
            catch { }
            vault = null;
            zpModel = null;
            volcModel = null;
            lavaMats.Clear();
            buildings.Clear();
            rocks.Clear();
            impacts.Clear();
            cues.Clear();
            Array.Clear(pillars, 0, 6);
            if (dome != null)
                UnityEngine.Object.Destroy(dome);
            dome = null;
            if (root != null)
                UnityEngine.Object.Destroy(root);
            root = null;
            if (farT != null)
                UnityEngine.Object.Destroy(farT.gameObject);
            farT = null;
            if (rootW != null)
                UnityEngine.Object.Destroy(rootW);
            rootW = null;
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
            curPhase = -1;
            skyMesh = null;
            sfx = null;
            fx = null;
            smoke = null;
        }
    }
}
