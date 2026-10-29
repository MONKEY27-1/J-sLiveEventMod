using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Builds and drives every local, event-owned object: audio-reactive nebula sky, beams, shockwaves, lights, particles, moon.
    // Everything is procedural except the optional moon model supplied by the user.
    internal sealed class AstronomicalEffectsManager : IEventScene
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        readonly List<Light> lights = new List<Light>();
        readonly List<float> lightBase = new List<float>();
        public string ShaderUsed = "?";

        GameObject root,
            stage,
            planets,
            orb;
        Transform ring;
        Transform[] stageBeams,
            skyBeams;
        Transform[] festivalPylons,
            festivalHalo;
        Material[] festivalMats;
        Transform rainbowDancer;
        Transform[] dancerLines,
            dancerJoints;
        Material[] dancerMats;
        Transform domeA,
            domeB;
        Material starMat,
            bubbleMat,
            glowMat,
            overlayMat,
            stageBeamMat,
            skyBeamMat,
            domeMatA,
            domeMatB,
            neonMat;
        Material[] ringMats;
        Transform[] ringT;
        float[] ringAge;
        ParticleSystem stars,
            dust,
            sparks,
            streaks,
            bubbles,
            fireworks;
        Light[] roam;
        Light[] stageLights;
        Camera cam;
        AstronomicalAssetLoader assets;
        AstronomicalDestruction destr;
        AstronomicalSpectacle spec;
        public AstronomicalDestruction Destruction => destr;
        GltfModel world,
            stageModel,
            avatarModel;
        GameObject performerRoot;
        Light travisLight;
        Transform vocalMic;
        bool avatarZUp;
        GltfModel.Clip showClip;
        Vector3 perfPos;
        float perfH,
            perfYaw;
        bool perfInit; // showClip = Travis_Show, the one baked 500 s choreography (sampled by event time, never looped or switched)
        float[] moveT0,
            moveT1;
        string[] moveName;
        float[] seatT0,
            seatT1;
        int moveIdx;
        Transform seatOrb;
        Material seatMat;
        Transform pelvisJoint;
        AstronomicalLaunchRings rings;
        float[] shockT,
            shockSize;
        int shockIdx;
        public static string CurrentMove = "";
        Transform coaster,
            coasterCart;
        bool coasterModels;
        readonly List<Transform> coasterTrack = new List<Transform>();
        float lastT,
            nextBurst,
            nextBeat,
            hueAcc;
        float sOmega,
            sTheta,
            sAmp,
            sFreq;
        bool paused;
        int lastIdx = -2,
            ringNext;
        bool megaDone;
        float megaFlash;

        struct Atmos
        {
            public bool fog;
            public Color fogColor,
                ambient;
            public FogMode mode;
            public float density,
                start,
                end;
        }

        Atmos orig;
        bool origSaved;

        public bool Built => root != null;
        public int OwnedCount => owned.Count;

        static readonly string[] opaqueChain = { "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default" };
        static readonly string[] alphaChain = { "Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "Unlit/Transparent" };

        Material Mat(Color c, Texture tex, bool alpha)
        {
            Shader sh = null;
            foreach (var n in alpha ? alphaChain : opaqueChain)
            {
                sh = Shader.Find(n);
                if (sh != null)
                {
                    ShaderUsed = n;
                    break;
                }
            }
            if (sh == null)
                throw new Exception("No usable unlit shader found");
            var m = new Material(sh) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            owned.Add(m);
            return m;
        }

        GameObject Prim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); // event props must never block the player
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localScale = ls;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        Light AddLight(Transform parent, Vector3 lp, Color c, float range, float intensity)
        {
            var g = new GameObject("AstLight");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            lights.Add(l);
            lightBase.Add(intensity);
            return l;
        }

        Texture2D MakeTex(int w, int h, Func<int, int, Color> f, bool repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
            t.SetPixels32(px);
            t.Apply(true, true);
            owned.Add(t);
            return t;
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + s * 1274126177;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }

        static float VNoise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x),
                yi = Mathf.FloorToInt(y);
            float fx = x - xi,
                fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(xi, yi, s), Hash(xi + 1, yi, s), fx),
                Mathf.Lerp(Hash(xi, yi + 1, s), Hash(xi + 1, yi + 1, s), fx),
                fy
            );
        }

        // Tileable-in-X nebula: fBm clouds, brightness in RGB, density in alpha. Hue comes from the material tint.
        Texture2D Nebula(int seed)
        {
            const int W = 256,
                H = 128;
            return MakeTex(
                W,
                H,
                (x, y) =>
                {
                    float v = 0,
                        a = 0.5f,
                        fr = 4f;
                    for (int o = 0; o < 4; o++)
                    {
                        float ang = (x / (float)W) * Mathf.PI * 2;
                        v += a * VNoise(Mathf.Cos(ang) * fr + 50, Mathf.Sin(ang) * fr + y / (float)H * fr * 0.7f, seed + o);
                        a *= 0.5f;
                        fr *= 2f;
                    }
                    float d = Mathf.Clamp01((v - 0.35f) * 2.2f);
                    float star = Hash(x, y, seed + 99) > 0.995f ? 1f : 0f;
                    float al = Mathf.Clamp01(d * d + star);
                    float br = Mathf.Lerp(0.55f, 1f, d);
                    return new Color(br, br, br, al);
                },
                true
            );
        }

        Texture2D GlowTex()
        {
            return MakeTex(
                32,
                32,
                (x, y) =>
                {
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 16f);
                    return new Color(1, 1, 1, d * d);
                },
                false
            );
        }

        Texture2D RingTex()
        {
            return MakeTex(
                128,
                128,
                (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
                    float q = (d - 0.88f) / 0.07f;
                    return new Color(1, 1, 1, Mathf.Exp(-q * q) * (d < 1 ? 1 : 0));
                },
                false
            );
        }

        ParticleSystem MakePS(string name, Transform parent, Material m, int max, float size, bool world = true)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.startSize = size;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            var r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            systems.Add(ps);
            return ps;
        }

        Transform[] MakeBeams(Transform parent, int n, float radius, float len, float width, Material m)
        {
            var arr = new Transform[n];
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2 / n;
                var p = new GameObject("beam").transform;
                p.SetParent(parent, false);
                p.localPosition = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
                for (int c = 0; c < 2; c++)
                {
                    var q = Prim(PrimitiveType.Quad, p, new Vector3(0, len / 2, 0), new Vector3(width, len, 1), m);
                    q.transform.localRotation = Quaternion.Euler(0, c * 90, 0);
                }
                arr[k] = p;
            }
            return arr;
        }

        static Color Col(float h, float s, float v)
        {
            return Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        static void Tint(Material m, Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            m.color = c;
        }

        // Move log + seat windows written next to the show clip when it was baked (travis_choreo.json). Optional: without it only the seat orb and the move label are missing.
        void LoadChoreoWindows(JNode ch)
        {
            moveT0 = moveT1 = seatT0 = seatT1 = null;
            moveName = null;
            moveIdx = 0;
            CurrentMove = "";
            if (ch == null)
                return;
            try
            {
                var log = ch["log"];
                int n = log != null ? log.Count : 0;
                moveT0 = new float[n];
                moveT1 = new float[n];
                moveName = new string[n];
                for (int k = 0; k < n; k++)
                {
                    var e = log[k];
                    moveT0[k] = (float)e[0];
                    moveT1[k] = (float)e[1];
                    moveName[k] = (string)e[2];
                }
                var ev = ch["events"];
                var a0 = new List<float>();
                var a1 = new List<float>();
                if (ev != null)
                    for (int k = 0; k < ev.Count; k++)
                    {
                        var e = ev[k];
                        if ((string)e[2] == "seat")
                        {
                            a0.Add((float)e[0]);
                            a1.Add((float)e[1]);
                        }
                    }
                seatT0 = a0.ToArray();
                seatT1 = a1.ToArray();
                var st = new List<float>();
                var ss = new List<float>(); // footfalls that shake the ground: where each stomp / jump / slam lands inside its move
                for (int k = 0; k < n; k++)
                {
                    string nm = moveName[k];
                    float f =
                        nm == "stomp" ? 0.5f
                        : nm == "jump" ? 0.66f
                        : nm == "fin_slam" ? 0.66f
                        : -1f;
                    if (f < 0)
                        continue;
                    st.Add(moveT0[k] + f * (moveT1[k] - moveT0[k]));
                    ss.Add(
                        nm == "fin_slam" ? 1.6f
                        : nm == "jump" ? 0.7f
                        : 0.5f
                    );
                }
                shockT = st.ToArray();
                shockSize = ss.ToArray();
                shockIdx = 0;
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Choreography log unreadable: " + e.Message);
                moveName = null;
                seatT0 = seatT1 = null;
            }
        }

        void TickMoveLog(float tc)
        {
            if (moveName == null || moveName.Length == 0)
                return;
            if (moveIdx >= moveName.Length || tc < moveT0[Mathf.Clamp(moveIdx, 0, moveName.Length - 1)])
                moveIdx = 0;
            while (moveIdx < moveName.Length - 1 && tc >= moveT1[moveIdx])
                moveIdx++;
            CurrentMove = tc >= moveT0[moveIdx] ? moveName[moveIdx] : "";
        }

        // While the choreography has him seated, a small glowing planetoid appears under the pelvis (the pose itself is baked: thighs level, calves hanging).
        void TickSeat(float tc, float h)
        {
            if (seatOrb == null)
                return;
            bool seated = false;
            if (seatT0 != null)
                for (int k = 0; k < seatT0.Length; k++)
                    if (tc >= seatT0[k] + 0.9f && tc <= seatT1[k] - 0.2f)
                    {
                        seated = true;
                        break;
                    }
            if (seated != seatOrb.gameObject.activeSelf)
                seatOrb.gameObject.SetActive(seated);
            if (!seated || pelvisJoint == null)
                return;
            float r = 0.17f * h;
            seatOrb.position = pelvisJoint.position + Vector3.down * (0.23f * h) + performerRoot.transform.forward * (0.02f * h); // his front is local -Z, so +forward is behind him: the seat sits under the sit-bones
            seatOrb.localScale = Vector3.one * (2f * r);
        }

        // The loader normalises the avatar so its skeleton is 1.8 units tall at scale 1: scale = desired height in metres / 1.8.
        float PerformerUnitScale(float heightMetres)
        {
            float src = avatarModel != null && avatarModel.BoneHeight > 0.01f ? avatarModel.BoneHeight : 1.8f;
            return heightMetres / src;
        }

        // Yaw that makes the performer (whose front is local -Z) face the stage centre from pos.
        static float FaceCenter(Vector3 pos)
        {
            return Mathf.Atan2(pos.x, pos.z) * Mathf.Rad2Deg;
        }

        // A non-colliding, local scenic coaster.  It is deliberately procedural so it never
        // changes Gorilla Tag geometry or network state.
        Vector3[] coasterPts,
            coasterUp;

        void BuildCoaster(AstronomicalAssetLoader l)
        {
            coaster = new GameObject("AstronomicalCoaster").transform;
            coaster.SetParent(root.transform, false);
            coasterPts = coasterUp = null;
            if (
                l != null
                && l.CoasterTrack != null
                && l.CoasterTrack.Pivot != null
                && l.CoasterCart != null
                && l.CoasterCart.Pivot != null
                && l.CoasterPath != null
            )
            {
                try
                {
                    var cp = l.CoasterPath;
                    int n = (int)cp["count"];
                    var pts = new Vector3[n];
                    var ups = new Vector3[n];
                    for (int k = 0; k < n; k++)
                    {
                        var p = cp["points"][k];
                        var u = cp["up"][k];
                        pts[k] = new Vector3((float)p[0], (float)p[1], (float)p[2]);
                        ups[k] = new Vector3((float)u[0], (float)u[1], (float)u[2]);
                    }
                    var o = cp["origin"];
                    var org = new Vector3((float)o[0], (float)o[1], (float)o[2]);
                    var tp = l.CoasterTrack.Pivot.transform;
                    tp.SetParent(coaster, false);
                    tp.localPosition = org;
                    tp.localRotation = Quaternion.identity;
                    tp.localScale = Vector3.one;
                    l.CoasterTrack.Pivot.SetActive(true); // the loader re-centres models on their bounds: org undoes that
                    coasterCart = l.CoasterCart.Pivot.transform;
                    coasterCart.SetParent(coaster, false);
                    coasterCart.localScale = Vector3.one;
                    l.CoasterCart.Pivot.SetActive(true);
                    coasterPts = pts;
                    coasterUp = ups;
                    coasterModels = true;
                    return;
                }
                catch (Exception ex)
                {
                    AstronomicalEventMod.Log.LogWarning("Coaster model failed, using the simple one: " + ex.Message);
                    coasterPts = coasterUp = null;
                }
            }
            var railMat = Mat(new Color(1f, 0.12f, 0.5f), null, false);
            const int count = 96;
            for (int n = 0; n < count; n++)
            {
                float a = n * Mathf.PI * 2f / count;
                // A forest-wide visual route, not a small ring around the stage.
                float r = 28f + Mathf.Sin(a * 3f) * 6f;
                float y = 5.5f + Mathf.Sin(a * 2f) * 4.5f + Mathf.Sin(a * 5f) * 1.75f;
                var p = new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
                coasterTrack.Add(Prim(PrimitiveType.Sphere, coaster, p, Vector3.one * 0.8f, railMat).transform);
            }
            var cartMat = Mat(new Color(0.1f, 0.9f, 1f), null, false);
            coasterCart = Prim(PrimitiveType.Cube, coaster, coasterTrack[0].localPosition, new Vector3(3.2f, 1.5f, 2f), cartMat).transform;
            coasterCart.name = "AstronomicalCoasterCart";
        }

        void TickCoaster(float t, float kick)
        {
            if (coasterPts != null && coasterCart != null)
            {
                int n = coasterPts.Length;
                float uu = Mathf.Repeat(t * (0.022f + kick * 0.01f), 1f) * n;
                int ia = Mathf.FloorToInt(uu) % n,
                    ib = (ia + 1) % n;
                float fr = uu - Mathf.Floor(uu);
                Vector3 cpos = Vector3.Lerp(coasterPts[ia], coasterPts[ib], fr),
                    fwd = (coasterPts[ib] - coasterPts[ia]).normalized,
                    up = Vector3.Slerp(coasterUp[ia], coasterUp[ib], fr);
                coasterCart.localPosition = cpos + up * 0.13f;
                if (fwd.sqrMagnitude > 0.001f)
                    coasterCart.localRotation = Quaternion.LookRotation(fwd, up);
                return;
            }
            if (coasterCart == null || coasterTrack.Count < 2)
                return;
            float u = Mathf.Repeat(t * (0.025f + kick * 0.012f), 1f) * coasterTrack.Count;
            int a = Mathf.FloorToInt(u) % coasterTrack.Count,
                b = (a + 1) % coasterTrack.Count;
            float f = u - Mathf.Floor(u);
            Vector3 pos = Vector3.Lerp(coasterTrack[a].localPosition, coasterTrack[b].localPosition, f);
            Vector3 forward = (coasterTrack[b].localPosition - coasterTrack[a].localPosition).normalized;
            coasterCart.localPosition = pos;
            if (forward.sqrMagnitude > 0.001f)
                coasterCart.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            coasterCart.localScale = new Vector3(1f, 1f + kick * 0.35f, 1f);
        }

        // A permanent, non-colliding concert district that reacts to each section of the show.
        void BuildFestivalCity()
        {
            var city = new GameObject("AstronomicalFestivalCity").transform;
            city.SetParent(root.transform, false);
            festivalMats = new[]
            {
                Mat(new Color(1f, 0.08f, 0.5f), null, false),
                Mat(new Color(0.1f, 0.8f, 1f), null, false),
                Mat(new Color(0.7f, 0.18f, 1f), null, false),
            };
            festivalPylons = new Transform[12];
            for (int k = 0; k < festivalPylons.Length; k++)
            {
                float a = k * Mathf.PI * 2f / festivalPylons.Length;
                var pylon = new GameObject("FestivalLightTower").transform;
                pylon.SetParent(city, false);
                pylon.localPosition = new Vector3(Mathf.Sin(a) * 25f, 0, Mathf.Cos(a) * 25f);
                Prim(PrimitiveType.Cylinder, pylon, Vector3.up * 5f, new Vector3(0.7f, 5f, 0.7f), festivalMats[k % festivalMats.Length]);
                Prim(PrimitiveType.Sphere, pylon, Vector3.up * 10.5f, Vector3.one * 1.8f, festivalMats[(k + 1) % festivalMats.Length]);
                festivalPylons[k] = pylon;
            }
            festivalHalo = new Transform[36];
            for (int k = 0; k < festivalHalo.Length; k++)
            {
                var node = Prim(PrimitiveType.Sphere, city, Vector3.zero, Vector3.one * 0.8f, festivalMats[k % festivalMats.Length]);
                node.name = "OrbitingFestivalLight";
                festivalHalo[k] = node.transform;
            }
        }

        void TickFestivalCity(float t, float hue, float kick, float fx)
        {
            if (festivalPylons == null || festivalHalo == null)
                return;
            for (int k = 0; k < festivalPylons.Length; k++)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(t * 2.5f + k * 0.8f);
                festivalPylons[k].localScale = new Vector3(1f, 0.65f + wave * (0.65f + kick), 1f);
            }
            for (int k = 0; k < festivalHalo.Length; k++)
            {
                float a = k * Mathf.PI * 2f / festivalHalo.Length + t * (0.12f + kick * 0.08f);
                float r = 17f + Mathf.Sin(t * 1.7f + k) * (2f + kick * 3f);
                festivalHalo[k].localPosition = new Vector3(Mathf.Sin(a) * r, 13f + Mathf.Sin(t * 2.2f + k) * 3f, Mathf.Cos(a) * r);
                festivalHalo[k].localScale = Vector3.one * (0.55f + kick * 1.1f);
            }
            for (int k = 0; k < festivalMats.Length; k++)
                festivalMats[k].color = Col(hue + k * 0.2f, 0.8f, 0.55f + 0.45f * kick);
        }

        // A model-free neon figure: every body segment is a bright line between animated joints.
        void BuildRainbowDancer()
        {
            rainbowDancer = new GameObject("AstronomicalRainbowDancer").transform;
            rainbowDancer.SetParent(root.transform, false);
            dancerMats = new Material[7];
            for (int k = 0; k < dancerMats.Length; k++)
                dancerMats[k] = Mat(Col(k / 7f, 0.9f, 1f), null, false);
            // head/neck, torso, shoulders, two arms, and two legs
            dancerLines = new Transform[11];
            for (int k = 0; k < dancerLines.Length; k++)
            {
                dancerLines[k] = Prim(
                    PrimitiveType.Cylinder,
                    rainbowDancer,
                    Vector3.zero,
                    Vector3.one,
                    dancerMats[k % dancerMats.Length]
                ).transform;
                dancerLines[k].name = "RainbowDancerLine";
            }
            dancerJoints = new Transform[13];
            for (int k = 0; k < dancerJoints.Length; k++)
            {
                dancerJoints[k] = Prim(
                    PrimitiveType.Sphere,
                    rainbowDancer,
                    Vector3.zero,
                    Vector3.one * 0.7f,
                    dancerMats[(k + 2) % dancerMats.Length]
                ).transform;
                dancerJoints[k].name = "RainbowDancerJoint";
            }
        }

        static void SetDancerLine(Transform line, Vector3 a, Vector3 b, float width)
        {
            Vector3 d = b - a;
            line.localPosition = (a + b) * 0.5f;
            line.localRotation = d.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            // Unity's primitive cylinder is two units tall at scale one.
            line.localScale = new Vector3(width, d.magnitude * 0.5f, width);
        }

        void TickRainbowDancer(float t, int sequence, float u, float hue, float kick, float bass)
        {
            if (rainbowDancer == null)
                return;
            float beat = t * 5.5f;
            float bounce = Mathf.Abs(Mathf.Sin(beat)) * (0.35f + kick * 0.7f);
            int routine = Mathf.FloorToInt(t / 7f) % 6;
            Vector3 motion = Vector3.zero;
            float giant = 1f;
            float bodyYaw = 0f,
                bodyPitch = 0f,
                bodyRoll = 0f;
            switch (sequence)
            {
                case 0:
                    motion = new Vector3(Mathf.Sin(t * 0.35f) * 9f, 0, Mathf.Sin(t * 0.7f) * 6f);
                    bodyYaw = Mathf.Sin(t * 0.35f) * 35f;
                    break;
                case 1: // Repeated figure-eight runs across the whole festival floor.
                    float s = t * 0.34f;
                    motion = new Vector3(Mathf.Sin(s) * 30f, 0, Mathf.Sin(s * 2f) * 19f);
                    bodyYaw = Mathf.Atan2(Mathf.Cos(s) * 30f, Mathf.Cos(s * 2f) * 38f) * Mathf.Rad2Deg;
                    break;
                case 2:
                    float a = t * 0.46f;
                    float r = 44f + Mathf.Sin(t * 0.19f) * 14f;
                    motion = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                    bodyYaw = (a + Mathf.PI * 0.5f) * Mathf.Rad2Deg;
                    giant = 1.35f;
                    break;
                case 3:
                    motion = new Vector3(Mathf.Sin(t * 0.25f) * 20f, 14f + Smooth(u) * 18f, 7f + Mathf.Cos(t * 0.25f) * 12f);
                    bodyYaw = t * 28f;
                    giant = 1.5f;
                    break;
                case 4:
                    float spiral = 33f + Mathf.Sin(t * 0.22f) * 12f;
                    motion = new Vector3(Mathf.Sin(t * 0.2f) * spiral, 27f + Mathf.Sin(t * 0.6f) * 5f, 25f + Mathf.Cos(t * 0.2f) * spiral);
                    bodyYaw = t * 45f;
                    giant = 1.8f;
                    break;
                case 5:
                    motion = new Vector3(Mathf.Sin(t * 0.42f) * 27f, 11f + Mathf.Sin(t * 0.7f) * 5f, 12f + Mathf.Sin(t * 0.84f) * 17f);
                    bodyYaw = t * 36f;
                    giant = 1.25f;
                    break;
                case 6:
                    motion = new Vector3(Mathf.Sin(t * 0.4f) * 14f, 0, 2f + Mathf.Cos(t * 0.4f) * 10f);
                    bodyYaw = t * 65f;
                    giant = Mathf.Lerp(1.5f, 2.6f, Smooth(u));
                    break;
            }

            // Six choreography phrases rotate every seven seconds: rave, shuffle, robot,
            // windmill, jump-up, and a low spinning break-dance transition.
            switch (routine)
            {
                case 0:
                    bodyRoll = Mathf.Sin(beat) * 8f;
                    break;
                case 1:
                    bodyYaw += Mathf.Sin(beat * 0.5f) * 24f;
                    break;
                case 2:
                    bodyPitch = Mathf.Sign(Mathf.Sin(beat * 0.5f)) * 13f;
                    bodyRoll = Mathf.Sign(Mathf.Cos(beat)) * 9f;
                    break;
                case 3:
                    bodyRoll = Mathf.Sin(beat * 0.5f) * 18f;
                    break;
                case 4:
                    bodyPitch = -8f - Mathf.Abs(Mathf.Sin(beat)) * 10f;
                    break;
                case 5:
                    bodyPitch = 20f;
                    bodyRoll = t * 190f;
                    break;
            }
            rainbowDancer.localRotation = Quaternion.Euler(bodyPitch, bodyYaw, bodyRoll);
            rainbowDancer.localPosition = motion;
            rainbowDancer.localScale = Vector3.one * giant * (1f + kick * 0.12f);

            // Local skeleton points: head, neck, hip, shoulders, elbows, hands, knees, ankles.
            var head = new Vector3(0, 14.2f + bounce * 0.25f, 0);
            var neck = new Vector3(0, 12.3f + bounce * 0.18f, 0);
            var hip = new Vector3(0, 7.0f + bounce, 0);
            var sl = new Vector3(-3.1f, 11.8f + bounce * 0.2f, 0);
            var sr = new Vector3(3.1f, 11.8f + bounce * 0.2f, 0);
            var el = new Vector3(-5.8f, 9.6f + Mathf.Sin(beat + 0.6f) * 2.1f, Mathf.Cos(beat) * 1.5f);
            var er = new Vector3(5.8f, 9.6f + Mathf.Sin(beat + 3.7f) * 2.1f, Mathf.Cos(beat + 3f) * 1.5f);
            var hl = new Vector3(-7.7f, 12.5f + Mathf.Sin(beat * 1.2f) * 3.5f, Mathf.Sin(beat) * 3f);
            var hr = new Vector3(7.7f, 12.5f + Mathf.Cos(beat * 1.15f) * 3.5f, Mathf.Cos(beat) * 3f);
            var kl = new Vector3(-2.5f, 3.5f - bounce * 0.4f, Mathf.Sin(beat) * 1.4f);
            var kr = new Vector3(2.5f, 3.5f - bounce * 0.4f, -Mathf.Sin(beat) * 1.4f);
            var al = new Vector3(-3.2f, 0, 1.0f + Mathf.Sin(beat) * 1.3f);
            var ar = new Vector3(3.2f, 0, -1.0f - Mathf.Sin(beat) * 1.3f);
            switch (routine)
            {
                case 0: // rave hands
                    hl.y += Mathf.Abs(Mathf.Sin(beat)) * 5f;
                    hr.y += Mathf.Abs(Mathf.Cos(beat)) * 5f;
                    break;
                case 1: // shuffle, with wide alternating feet
                    al.x -= 2.3f * Mathf.Sin(beat);
                    ar.x += 2.3f * Mathf.Sin(beat);
                    kl.x -= 1.4f * Mathf.Sin(beat);
                    kr.x += 1.4f * Mathf.Sin(beat);
                    break;
                case 2: // robot locks
                    el.y += Mathf.Sign(Mathf.Sin(beat)) * 3f;
                    er.y -= Mathf.Sign(Mathf.Sin(beat)) * 3f;
                    hl.z += Mathf.Sign(Mathf.Sin(beat * 0.5f)) * 4f;
                    hr.z -= Mathf.Sign(Mathf.Sin(beat * 0.5f)) * 4f;
                    break;
                case 3: // windmills
                    hl = new Vector3(-7f, 11f + Mathf.Sin(beat) * 6f, Mathf.Cos(beat) * 6f);
                    hr = new Vector3(7f, 11f + Mathf.Sin(beat + Mathf.PI) * 6f, Mathf.Cos(beat + Mathf.PI) * 6f);
                    break;
                case 4: // jump-up / hands overhead
                    hip.y += Mathf.Abs(Mathf.Sin(beat)) * 2.5f;
                    head.y += Mathf.Abs(Mathf.Sin(beat)) * 2.5f;
                    hl.y += 8f;
                    hr.y += 8f;
                    al.y += Mathf.Abs(Mathf.Sin(beat)) * 2f;
                    ar.y += Mathf.Abs(Mathf.Sin(beat)) * 2f;
                    break;
                case 5: // compact low spin
                    hip.y -= 3f;
                    head.y -= 3f;
                    hl.y -= 3f;
                    hr.y -= 3f;
                    al.y -= 1f;
                    ar.y -= 1f;
                    break;
            }
            if (sequence == 3 || sequence == 4)
            {
                hl.y += 4f;
                hr.y += 4f;
                al.y += 2f;
                ar.y += 2f;
            }
            if (sequence == 6)
            {
                hl.y += 7f * Smooth(u);
                hr.y += 7f * Smooth(u);
            }

            Vector3[] joints = { head, neck, hip, sl, sr, el, er, hl, hr, kl, kr, al, ar };
            for (int k = 0; k < dancerJoints.Length; k++)
                dancerJoints[k].localPosition = joints[k];
            SetDancerLine(dancerLines[0], head, neck, 0.34f);
            SetDancerLine(dancerLines[1], neck, hip, 0.42f);
            SetDancerLine(dancerLines[2], sl, sr, 0.30f);
            SetDancerLine(dancerLines[3], sl, el, 0.28f);
            SetDancerLine(dancerLines[4], el, hl, 0.24f);
            SetDancerLine(dancerLines[5], sr, er, 0.28f);
            SetDancerLine(dancerLines[6], er, hr, 0.24f);
            SetDancerLine(dancerLines[7], hip, kl, 0.36f);
            SetDancerLine(dancerLines[8], kl, al, 0.32f);
            SetDancerLine(dancerLines[9], hip, kr, 0.36f);
            SetDancerLine(dancerLines[10], kr, ar, 0.32f);
            for (int k = 0; k < dancerMats.Length; k++)
                dancerMats[k].color = Col(hue + k / 7f + t * 0.03f, 0.95f, 0.65f + 0.35f * kick);
        }

        // anchorPos is ground level; yaw orients the stage front toward the player.
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yaw)
        {
            Cleanup();
            assets = loader;
            cam = Camera.main;
            SaveAtmos();
            root = new GameObject("AstronomicalEventRoot");
            root.transform.SetPositionAndRotation(anchorPos, Quaternion.Euler(0, yaw, 0));
            owned.Add(root);
            var glowTex = GlowTex();
            float dens = AstronomicalSettings.Particles.Value;
            BuildCoaster(loader);
            BuildFestivalCity();

            // sky domes (follow the camera; Sprites/Default is double-sided so the inside is visible)
            domeMatA = Mat(new Color(1, 1, 1, 0), Nebula(7), true);
            domeMatB = Mat(new Color(1, 1, 1, 0), Nebula(31), true);
            domeA = Prim(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 240f, domeMatA).transform;
            domeB = Prim(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one * 225f, domeMatB).transform;

            // stage
            var dark = Mat(new Color(0.05f, 0.04f, 0.08f), null, false);
            neonMat = Mat(new Color(1f, 0.45f, 0.1f), null, false);
            var violet = Mat(new Color(0.5f, 0.2f, 0.9f), null, false);
            stage = new GameObject("Stage");
            stage.transform.SetParent(root.transform, false);

            if (!AstronomicalSettings.ShowStage.Value) { } // open arena: no stage model and no stand-in stage
            else if (loader != null && loader.Stage != null && loader.Stage.Pivot != null)
            {
                stageModel = loader.Stage;
                stageModel.Pivot.transform.SetParent(stage.transform, false);
                stageModel.Pivot.transform.localPosition = Vector3.zero;
                // The model's front (steps) faces +Z; the audience is at -Z. Scale comes from the parent 'stage' transform (set per frame).
                stageModel.Pivot.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                stageModel.Pivot.transform.localScale = Vector3.one;
                stageModel.Pivot.SetActive(true);
            }
            else
            {
                Prim(PrimitiveType.Cylinder, stage.transform, new Vector3(0, 0.25f, 0), new Vector3(9, 0.25f, 9), dark);
                ring = Prim(
                    PrimitiveType.Cylinder,
                    stage.transform,
                    new Vector3(0, 0.52f, 0),
                    new Vector3(8.4f, 0.02f, 8.4f),
                    neonMat
                ).transform;
                for (int k = -1; k <= 1; k += 2)
                {
                    Prim(PrimitiveType.Cube, stage.transform, new Vector3(k * 7f, 3.5f, 2f), new Vector3(0.8f, 7f, 0.8f), dark);
                    Prim(PrimitiveType.Cube, stage.transform, new Vector3(k * 7f, 3.5f, 1.58f), new Vector3(0.2f, 6.5f, 0.1f), violet);
                }
                Prim(
                    PrimitiveType.Quad,
                    stage.transform,
                    new Vector3(0, 5f, 4f),
                    new Vector3(16, 9, 1),
                    Mat(new Color(0.2f, 0.06f, 0.4f), null, false)
                );
                try
                {
                    var tg = new GameObject("Title");
                    tg.transform.SetParent(stage.transform, false);
                    var tm = tg.AddComponent<TextMesh>();
                    tg.transform.localPosition = new Vector3(0, 5f, 3.9f);
                    tm.text = "ASTRONOMICAL";
                    tm.fontSize = 120;
                    tm.characterSize = 0.18f;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.color = new Color(1, 0.7f, 0.3f);
                    var font = Font.CreateDynamicFontFromOSFont("Arial", 64);
                    owned.Add(font);
                    tm.font = font;
                    tg.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
                catch (Exception e)
                {
                    AstronomicalEventMod.Log.LogWarning("Title text failed: " + e.Message);
                }
            }

            // performer: Travis Scott (skinned model + the retargeted Fortnite event dance); shown/positioned by the timeline
            performerRoot = new GameObject("TravisPerformer");
            performerRoot.transform.SetParent(root.transform, false);
            perfInit = false;
            if (loader != null && loader.Avatar != null && loader.Avatar.Pivot != null)
            {
                avatarModel = loader.Avatar;
                avatarModel.Pivot.transform.SetParent(performerRoot.transform, false);
                avatarModel.Pivot.transform.localPosition = Vector3.zero;
                avatarModel.Pivot.transform.localRotation = Quaternion.identity;
                avatarModel.Pivot.transform.localScale = Vector3.one * PerformerUnitScale(AstronomicalSettings.TravisScale.Value);
                avatarModel.Pivot.SetActive(true);
                foreach (var smr in avatarModel.Pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.enabled = true;
                }
                showClip = avatarModel.FindClipExact("Travis_Show");
                if (showClip == null)
                    AstronomicalEventMod.Log.LogWarning("Travis_Show clip missing; clips in file: " + avatarModel.Clips.Count);
                else
                    avatarModel.Sample(showClip, 0f);
                pelvisJoint = null;
                foreach (var jt in avatarModel.Joints)
                    if (jt != null && jt.name.StartsWith("pelvis", StringComparison.OrdinalIgnoreCase))
                    {
                        pelvisJoint = jt;
                        break;
                    }
                LoadChoreoWindows(loader.Choreo);
                seatMat = Mat(new Color(0.55f, 0.75f, 1f, 1f), glowTex, true);
                seatOrb = Prim(PrimitiveType.Sphere, root.transform, Vector3.zero, Vector3.one, seatMat).transform;
                seatOrb.name = "TravisSeat";
                seatOrb.gameObject.SetActive(false);
                AstronomicalEventMod.Log.LogInfo(
                    "Travis performer ready: joints="
                        + avatarModel.Joints.Length
                        + ", show="
                        + (showClip != null ? showClip.Duration.ToString("0.0") + "s" : "missing")
                        + ", moves="
                        + (moveName != null ? moveName.Length : 0)
                        + ", height="
                        + AstronomicalSettings.TravisScale.Value
                        + " m"
                );
            }
            performerRoot.SetActive(false);
            vocalMic = null;
            travisLight = null;
            // beams: stage fan + outer cage leaning inward
            stageBeamMat = Mat(new Color(0.9f, 0.5f, 1f, 0), glowTex, true);
            skyBeamMat = Mat(new Color(0.4f, 0.8f, 1f, 0), glowTex, true);
            var bg = new GameObject("Beams");
            bg.transform.SetParent(stage.transform, false);
            bg.transform.localPosition = new Vector3(0, 0.6f, 0);
            stageBeams = MakeBeams(bg.transform, 16, 6.5f, 45f, 1.2f, stageBeamMat);
            var sg = new GameObject("SkyBeams");
            sg.transform.SetParent(root.transform, false);
            skyBeams = MakeBeams(sg.transform, 12, 32f, 90f, 2.2f, skyBeamMat);

            // lights: 3 stage + 4 roaming (kept small for VR)
            stageLights = new Light[3];
            stageLights[0] = AddLight(stage.transform, new Vector3(0, 4, -2), Color.white, 20f, 2.5f);
            stageLights[1] = AddLight(stage.transform, new Vector3(-6, 5, 0), Color.white, 20f, 2f);
            stageLights[2] = AddLight(stage.transform, new Vector3(6, 5, 0), Color.white, 20f, 2f);
            roam = new Light[4];
            for (int k = 0; k < 4; k++)
                roam[k] = AddLight(root.transform, Vector3.up * 5, Color.white, 24f, 2.2f);
            if (avatarModel != null)
                travisLight = AddLight(performerRoot.transform, new Vector3(0, 6f, -8f), Color.white, 40f, 6f); // keeps stage/roam light indices intact

            // core orb
            glowMat = Mat(new Color(1f, 0.6f, 0.9f, 0.8f), glowTex, true);
            orb = Prim(PrimitiveType.Quad, root.transform, new Vector3(0, 9f, 0), new Vector3(10, 10, 1), glowMat);

            // shockwave ring pool
            var rt = RingTex();
            ringMats = new Material[5];
            ringT = new Transform[5];
            ringAge = new float[5];
            for (int k = 0; k < 5; k++)
            {
                ringMats[k] = Mat(new Color(1, 1, 1, 0), rt, true);
                var q = Prim(PrimitiveType.Quad, root.transform, new Vector3(0, 0.12f, 0), Vector3.one, ringMats[k]);
                q.transform.localRotation = Quaternion.Euler(90, 0, 0);
                ringT[k] = q.transform;
                ringAge[k] = 99f;
                q.SetActive(false);
            }

            // planets: user-supplied moon/world model, else procedural spheres
            planets = new GameObject("Planets");
            planets.transform.SetParent(root.transform, false);
            if (loader != null && loader.World != null && loader.World.Pivot != null)
            {
                world = loader.World;
                world.Pivot.transform.SetParent(planets.transform, false);
                world.Pivot.SetActive(true);
            }
            else
            {
                Color[] pc = { new Color(0.9f, 0.5f, 0.2f), new Color(0.3f, 0.5f, 1f), new Color(0.8f, 0.3f, 0.7f) };
                for (int k = 0; k < 3; k++)
                    Prim(
                        PrimitiveType.Sphere,
                        planets.transform,
                        new Vector3(-40 + k * 40, 45 + k * 8, 60 + k * 10),
                        Vector3.one * (12 + k * 4),
                        Mat(pc[k], null, false)
                    );
            }

            // particles
            int starN = Mathf.Max(50, (int)(700 * dens));
            starMat = Mat(new Color(1, 1, 1, 0), glowTex, true);
            stars = MakePS("Stars", root.transform, starMat, starN, 0.5f);
            var sh = stars.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 60;
            sh.radiusThickness = 0.05f;
            var sm = stars.main;
            sm.startLifetime = 100000f;
            sm.startSpeed = 0f;
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.8f, 1f), Color.white);
            var se = stars.emission;
            se.rateOverTime = 0;
            se.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)starN) });
            stars.transform.localPosition = new Vector3(0, 20, 0);

            var pm = Mat(Color.white, glowTex, true);
            dust = MakePS("Dust", root.transform, pm, Mathf.Max(100, (int)(900 * dens)), 0.35f, false);
            var ds = dust.shape;
            ds.shapeType = ParticleSystemShapeType.Circle;
            ds.radius = 22;
            ds.rotation = new Vector3(-90, 0, 0);
            var dm = dust.main;
            dm.startLifetime = 9f;
            dm.startSpeed = 0f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            var dv = dust.velocityOverLifetime;
            dv.enabled = true;
            dv.space = ParticleSystemSimulationSpace.Local;
            dv.orbitalY = 0.5f;
            dv.y = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            var de = dust.emission;
            de.rateOverTime = 0;

            sparks = MakePS("Sparks", stage.transform, pm, Mathf.Max(100, (int)(600 * dens)), 0.25f);
            var ss = sparks.shape;
            ss.shapeType = ParticleSystemShapeType.Circle;
            ss.radius = 8;
            ss.rotation = new Vector3(-90, 0, 0);
            var sp = sparks.main;
            sp.startLifetime = 2.2f;
            sp.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
            sp.gravityModifier = -0.1f;
            var spe = sparks.emission;
            spe.rateOverTime = 0;

            var stm = Mat(Color.white, glowTex, true);
            streaks = MakePS("Streaks", root.transform, stm, Mathf.Max(100, (int)(500 * dens)), 0.25f);
            streaks.transform.localPosition = new Vector3(0, 14, 50);
            streaks.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var ssh = streaks.shape;
            ssh.shapeType = ParticleSystemShapeType.Box;
            ssh.scale = new Vector3(70, 35, 1);
            var stmain = streaks.main;
            stmain.startLifetime = 2.2f;
            stmain.startSpeed = 60f;
            stmain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            var stre = streaks.emission;
            stre.rateOverTime = 0;
            var srr = streaks.GetComponent<ParticleSystemRenderer>();
            srr.renderMode = ParticleSystemRenderMode.Stretch;
            srr.velocityScale = 0.08f;
            srr.lengthScale = 2f;

            bubbleMat = Mat(new Color(0.7f, 0.9f, 1f, 0), glowTex, true);
            bubbles = MakePS("Bubbles", root.transform, bubbleMat, Mathf.Max(50, (int)(500 * dens)), 0.25f);
            var bs = bubbles.shape;
            bs.shapeType = ParticleSystemShapeType.Box;
            bs.scale = new Vector3(40, 1, 40);
            var bm = bubbles.main;
            bm.startLifetime = 9f;
            bm.startSpeed = 0f;
            bm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            var bv = bubbles.velocityOverLifetime;
            bv.enabled = true;
            bv.space = ParticleSystemSimulationSpace.World;
            bv.y = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            var be = bubbles.emission;
            be.rateOverTime = 70 * dens;

            fireworks = MakePS("Fireworks", root.transform, Mat(Color.white, glowTex, true), 4000, 0.6f);
            var fm = fireworks.main;
            fm.gravityModifier = 0.15f;
            fm.startLifetime = 2.2f;
            var fe = fireworks.emission;
            fe.enabled = false;

            // Screen tint overlay parented to the camera; destroyed in Cleanup.
            overlayMat = Mat(new Color(0.1f, 0.4f, 0.7f, 0), null, true);
            if (cam != null)
            {
                var q = Prim(PrimitiveType.Quad, cam.transform, new Vector3(0, 0, 0.3f), new Vector3(3, 3, 1), overlayMat);
                q.name = "AstOverlay";
                owned.Add(q);
            }

            foreach (var ps in systems)
                ps.Play();
            if (AstronomicalSettings.TreeDestruction.Value)
            {
                try
                {
                    destr = new AstronomicalDestruction();
                    owned.Add(null);
                    destr.Init(
                        anchorPos,
                        root.transform,
                        pm,
                        Mat(Color.white, null, true),
                        AstronomicalSettings.DestructionRadius.Value,
                        loader != null ? loader.Tree : null
                    );
                    owned[owned.Count - 1] = destr.Root;
                }
                catch (Exception ex)
                {
                    AstronomicalEventMod.Log.LogError("Destruction init failed: " + ex);
                    if (destr != null)
                        destr.Dispose();
                    destr = null;
                }
            }
            try
            {
                spec = new AstronomicalSpectacle();
                owned.Add(null);
                spec.Init(anchorPos, Mat(Color.white, null, true), glowMat, ringMats[0].mainTexture, domeMatA.mainTexture, destr);
                owned[owned.Count - 1] = spec.Root;
            }
            catch (Exception ex)
            {
                AstronomicalEventMod.Log.LogError("Spectacle init failed: " + ex);
                if (spec != null)
                    spec.Dispose();
                spec = null;
            }
            try
            {
                rings = new AstronomicalLaunchRings();
                rings.Build(root.transform, anchorPos, ringMats[0]);
            }
            catch (Exception ex)
            {
                AstronomicalEventMod.Log.LogWarning("Launch rings failed: " + ex.Message);
                rings = null;
            }
            stage.SetActive(false);
            planets.SetActive(false);
        }

        void SaveAtmos()
        {
            orig = new Atmos
            {
                fog = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                ambient = RenderSettings.ambientLight,
                mode = RenderSettings.fogMode,
                density = RenderSettings.fogDensity,
                start = RenderSettings.fogStartDistance,
                end = RenderSettings.fogEndDistance,
            };
            origSaved = true;
        }

        void RestoreAtmos()
        {
            if (!origSaved)
                return;
            RenderSettings.fog = orig.fog;
            RenderSettings.fogColor = orig.fogColor;
            RenderSettings.ambientLight = orig.ambient;
            RenderSettings.fogMode = orig.mode;
            RenderSettings.fogDensity = orig.density;
            RenderSettings.fogStartDistance = orig.start;
            RenderSettings.fogEndDistance = orig.end;
            origSaved = false;
        }

        static float hue0(int i, float t)
        {
            return HueBase[i] + 0.06f * Mathf.Sin(t * 0.4f);
        }

        // The big finale explosion: giant fireballs, every standing tree blows apart, lightning, white flash.
        void MegaBoom(float hue)
        {
            Vector3 c = root.transform.position;
            if (spec != null)
                spec.Mega(c, hue);
            AstronomicalHazards.Blast(c, 170f, 45f, 22f);
            if (destr != null)
                destr.ExplodeAll(c);
            megaFlash = 1f;
            AstronomicalEventMod.Log.LogInfo("Finale explosion");
        }

        public void SetPaused(bool p)
        {
            paused = p;
            foreach (var ps in systems)
            {
                if (ps == null)
                    continue;
                if (p)
                    ps.Pause(true);
                else
                    ps.Play(true);
            }
        }

        static void SetBeam(Transform p, float phi, float theta)
        {
            var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
            p.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        //                          intro  stage  surge  moon   cosmic water  finale
        static readonly float[] BOmega = { 0.1f, 0.3f, 0.8f, 0.4f, 1.2f, 0.15f, 2.0f };
        static readonly float[] BTheta = { 0.1f, 0.25f, 0.4f, 0.6f, 0.7f, 0.15f, 0.9f };
        static readonly float[] BAmp = { 0.05f, 0.1f, 0.3f, 0.2f, 0.5f, 0.1f, 0.7f };
        static readonly float[] BFreq = { 1f, 1f, 2f, 1.2f, 2.5f, 0.8f, 4f };
        static readonly float[] HueBase = { 0.72f, 0.8f, 0.03f, 0.6f, 0.0f, 0.5f, 0.9f };
        static readonly float[] HueSpeed = { 0.01f, 0.02f, 0.03f, 0.03f, 0.05f, 0.01f, 0.12f };

        public void Apply(float t, int i, float u, AstronomicalAudioManager au)
        {
            if (root == null)
                return;
            float fx = AstronomicalSettings.Effects.Value,
                li = AstronomicalSettings.Lighting.Value,
                dens = AstronomicalSettings.Particles.Value;
            float fade = i == 6 ? 1f - Smooth((u - 0.9f) / 0.1f) : 1f; // finale: everything fades back to the normal map
            fx *= fade;
            li *= fade;
            if (cam == null)
                cam = Camera.main;
            bool back = t < lastT - 0.25f;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            lastT = t;
            if (dt == 0 && !paused)
            { /* seek or first frame */
            }
            float kick = au.Kick,
                bass = au.Bass,
                mid = au.Mid,
                high = au.High;
            bool hit = au.KickHit;
            if (!au.Active && !paused && t >= nextBeat)
            {
                hit = true;
                kick = 1f;
                nextBeat = t + 0.66f;
            } // metronome when no audio

            if (back)
                megaDone = false;
            if (i == 6 && u >= 0.86f && !megaDone && !paused)
            {
                megaDone = true;
                MegaBoom(hue0(i, t));
            }
            if (i != 6 || u < 0.85f)
                megaDone = false;
            megaFlash = Mathf.Max(0f, megaFlash - dt * 0.9f);
            bool stageOn = i >= 1 && !(i == 6 && u >= 0.86f);
            if (stage.activeSelf != stageOn)
                stage.SetActive(stageOn);
            if (i != lastIdx)
            {
                lastIdx = i;
                if (AstronomicalSettings.Debug.Value)
                    AstronomicalEventMod.Log.LogInfo("Sequence -> " + AstronomicalSequence.Names[i]);
            }

            float cosmic =
                i < 2 ? 0
                : i == 2 ? Smooth(u) * 0.4f
                : i == 3 ? 0.4f + 0.3f * u
                : i == 4 ? 0.7f + 0.3f * Smooth(u * 4)
                : i == 5 ? 1 - Smooth(u * 3)
                : 0.8f;
            float water = i == 5 ? Smooth(u * 5) * (1 - Smooth((u - 0.85f) / 0.15f)) : 0;
            float dark = i == 0 ? Smooth(u) : 1;
            if (!paused)
                hueAcc += dt * HueSpeed[i];
            float hue = HueBase[i] + hueAcc + 0.06f * Mathf.Sin(t * 0.4f);
            Color c1 = Col(hue, 0.8f, 1f),
                c2 = Col(hue + 0.33f, 0.8f, 1f),
                c3 = Col(hue + 0.66f, 0.7f, 1f);
            if (!paused)
                AstronomicalHazards.Swim(water > 0.3f, root.transform.position); // the game's own swimming while the arena is flooded
            if (rings != null && !paused)
                rings.Tick(dt, t, i, kick, hue);

            // smooth beam pattern params
            float k = 1f - Mathf.Exp(-dt * 2f);
            sOmega = Mathf.Lerp(sOmega, BOmega[i], k);
            sTheta = Mathf.Lerp(sTheta, BTheta[i], k);
            sAmp = Mathf.Lerp(sAmp, BAmp[i], k);
            sFreq = Mathf.Lerp(sFreq, BFreq[i], k);

            // stage
            float curStageScale = AstronomicalSettings.StageScale.Value;
            stage.transform.localScale =
                i == 1
                    ? new Vector3(curStageScale, Mathf.Max(0.001f, Smooth(u * 1.4f)) * curStageScale, curStageScale)
                    : Vector3.one * curStageScale;
            float rs = 8.4f + kick * 0.5f;
            if (ring != null)
                ring.localScale = new Vector3(rs, 0.02f, rs);
            if (neonMat != null)
                Tint(neonMat, c1, 1f);

            // beams
            float beamA = (i >= 1 ? 1f : 0f) * (0.16f + 0.22f * kick + 0.12f * mid) * fx;
            Tint(stageBeamMat, c2, beamA);
            Tint(skyBeamMat, c3, (i >= 2 ? 1f : 0f) * (0.10f + 0.2f * kick + 0.1f * high) * fx);
            for (int b = 0; b < stageBeams.Length; b++)
            {
                float phi = b * Mathf.PI * 2 / stageBeams.Length + t * sOmega * (b % 2 == 0 ? 1 : -1);
                float th = sTheta + sAmp * Mathf.Sin(t * sFreq + b * 0.5f) + kick * 0.12f;
                SetBeam(stageBeams[b], phi, Mathf.Max(0.02f, th));
            }
            for (int b = 0; b < skyBeams.Length; b++)
            {
                float a = b * Mathf.PI * 2 / skyBeams.Length;
                float th = 0.35f + 0.25f * Mathf.Sin(t * (0.6f + sFreq * 0.2f) + b); // lean inward over the arena
                SetBeam(skyBeams[b], a + Mathf.PI + Mathf.Sin(t * 0.3f + b) * 0.3f, Mathf.Clamp(th, 0.05f, 1f));
            }

            // sky domes: nebula changes colour, drifts, breathes with the music
            float domeA_ =
                (
                    i == 0 ? 0.3f * Smooth(u)
                    : i == 1 ? 0.35f
                    : i == 2 ? 0.5f + 0.3f * kick
                    : i == 3 ? 0.65f
                    : i == 4 ? 0.9f
                    : i == 5 ? 0.7f
                    : 0.8f + 0.2f * kick
                ) * fx;
            Color nebula = water > 0.01f ? Color.Lerp(c1, Col(0.5f, 0.7f, 1f), water) : c1;
            Tint(domeMatA, nebula, domeA_);
            Tint(domeMatB, water > 0.01f ? Col(0.46f, 0.6f, 1f) : c2, domeA_ * 0.7f * (0.6f + 0.4f * mid));
            if (cam != null)
            {
                domeA.position = cam.transform.position;
                domeB.position = cam.transform.position;
            }
            domeA.rotation = Quaternion.Euler(0, t * 1.5f, t * 0.4f);
            domeB.rotation = Quaternion.Euler(t * 0.5f, -t * 2.2f, 0);
            domeMatA.mainTextureOffset = new Vector2(t * 0.01f, 0);
            domeMatB.mainTextureOffset = new Vector2(-t * 0.016f, 0);

            // core orb
            float charge = i == 6 ? Smooth((u - 0.72f) / 0.14f) : 0f; // the core swells before the big explosion
            orb.SetActive(!(i == 6 && u >= 0.86f));
            float os = (i <= 1 ? 6f : 10f) * (1 + 0.15f * Mathf.Sin(t * 4f) + 0.5f * kick) * (1f + 5f * charge);
            orb.transform.localScale = Vector3.one * os * (0.3f + 0.7f * dark);
            orb.transform.localPosition = new Vector3(0, 9f + Mathf.Sin(t * 0.7f) * 1.5f, 0);
            if (cam != null)
                orb.transform.rotation = Quaternion.LookRotation(orb.transform.position - cam.transform.position);
            Tint(glowMat, c3, (0.55f + 0.4f * kick) * fx * (i == 5 ? 0.5f : 1f));

            // shockwave rings on every kick
            if (hit && i >= 1 && !paused)
            {
                ringAge[ringNext] = 0;
                ringT[ringNext].gameObject.SetActive(true);
                ringNext = (ringNext + 1) % ringT.Length;
            }
            for (int r = 0; r < ringT.Length; r++)
            {
                if (ringAge[r] > 1.6f)
                {
                    if (ringT[r].gameObject.activeSelf)
                        ringT[r].gameObject.SetActive(false);
                    continue;
                }
                if (!paused)
                    ringAge[r] += dt;
                float rad = 3f + ringAge[r] * 28f;
                float rf = 1f - ringAge[r] / 1.6f;
                ringT[r].localScale = new Vector3(rad * 2, rad * 2, 1);
                Tint(ringMats[r], Col(hue + r * 0.17f, 0.8f, 1f), rf * rf * 0.85f * fx);
            }

            // moon / planets
            float mf =
                (
                    i == 3 ? 0.3f + 1.3f * Smooth(u)
                    : i == 4 ? 1.6f - 0.3f * Smooth(u)
                    : i == 6 ? 1.3f + 0.4f * Mathf.Sin(t * 0.5f)
                    : 0f
                ) * fade;
            bool showPlanets = mf > 0.01f;
            if (planets.activeSelf != showPlanets)
                planets.SetActive(showPlanets);
            if (showPlanets)
            {
                if (world != null)
                {
                    var sz = world.RendererBounds.size;
                    float ext = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
                    float baseS = ext > 0.01f ? 90f / ext : 1f;
                    var wt = world.Pivot.transform;
                    wt.localScale = Vector3.one * baseS * mf * (1 + 0.04f * kick);
                    wt.localPosition = new Vector3(
                        Mathf.Sin(t * 0.05f) * 30f,
                        55f + Mathf.Sin(t * 0.2f) * 6f,
                        Mathf.Lerp(190f, 125f, Smooth(mf / 1.6f))
                    );
                    wt.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.1f) * 10f, t * 3f, 0);
                }
                else
                    planets.transform.localRotation = Quaternion.Euler(0, t * 1.5f, 0);
            }

            // particles
            Tint(starMat, Color.white, cosmic * fx);
            Tint(bubbleMat, Color.white, water * fx);
            SetPS(dust, c2, (i >= 1 ? (25f + 120f * mid) * dens * fx : 0f));
            SetPS(sparks, c1, (i >= 1 ? (10f + 260f * kick + 60f * bass) * dens * fx : 0f));
            SetPS(streaks, c3, ((i == 4 || i == 6) ? (60f + 200f * high) * dens * fx : (i == 2 ? 30f * dens * fx : 0f)));
            if (!paused && (i == 6 || (i == 4 && u > 0.7f)) && ((hit && i == 6) || t >= nextBurst))
            {
                Burst(dens, hue);
                nextBurst = t + (i == 6 ? 0.9f : 1.6f);
            }
            if (i != 6 && i != 4)
                nextBurst = 0;

            bool hitS = hit && !(i == 6 && u >= 0.9f); // no new pyro after the big explosion
            if (destr != null)
            {
                if (back)
                    destr.Reset();
                if (!paused)
                    destr.Tick(dt, i, u, hitS, water, hue, dens);
            }
            if (spec != null)
            {
                if (back)
                    spec.Reset();
                if (!paused)
                    spec.Tick(dt, t, i, u, hitS, hue, dens, fx, cosmic, kick, bass, water);
            }

            // Travis Scott: one baked 500 s choreography (body, mouth, blinks, dreads/necklace physics) sampled at the event time. Nothing loops, switches or repeats.
            if (avatarModel != null && performerRoot != null)
            {
                float st = AstronomicalSettings.StageScale.Value,
                    H0 = AstronomicalSettings.TravisScale.Value;
                float deckGrow = i == 0 ? 0f : (i == 1 ? Smooth(u * 1.4f) : 1f); // the stage rises out of the ground during sequence 1
                Vector3 deck = new Vector3(
                    0,
                    (stageModel != null ? 5.85f : 0f) * st * (stageModel != null ? deckGrow : 1f),
                    (stageModel != null ? 2.5f : 0f) * st
                );
                float height = H0,
                    yaw = AstronomicalSettings.PerformerYaw.Value,
                    glow = 1f;
                Vector3 pos = deck;
                bool visible = true;
                float tExplode = AstronomicalSequence.Start[6] + 0.86f * AstronomicalSequence.Duration[6];
                // The clip carries its own root motion (walking, hovering, swimming strokes) in body-heights, so the performer's size is capped: even at full size
                // that motion stays inside the playable area around the stage.
                const float MaxH = 14f;
                switch (i)
                {
                    case 0: // Intro: rises out of the stage floor, then breathes, looks around, opens his arms
                        visible = u > 0.3f;

                        {
                            float em = Smooth((u - 0.3f) / 0.5f);
                            pos = deck + Vector3.down * (H0 * 1.3f * (1f - em));
                        }
                        break;
                    case 1: // Stage: raps and walks the deck
                        break;
                    case 2: // Energy Surge: grows into a giant on the deck
                        {
                            float g = Smooth(u * 1.6f);
                            height = Mathf.Lerp(H0, Mathf.Min(MaxH, H0 * 3f), g);
                            glow = 1.4f;
                        }
                        break;
                    case 3: // Moon Rise: levitates skyward (the clip lifts him; the root climbs with it) and turns toward the moon
                        height = Mathf.Min(MaxH, H0 * 3f);
                        pos = deck + new Vector3(Mathf.Sin(t * 0.2f) * 4f, 16f * Smooth(u * 1.5f), 4f * Smooth(u));
                        yaw += 110f * Smooth(Mathf.Min(1f, u * 2.5f));
                        break;
                    case 4: // Cosmic: colossal, floating among the stars, seated on small planets between floats
                        height = Mathf.Min(MaxH, H0 * 3.5f);
                        pos = deck + new Vector3(Mathf.Sin(t * 0.12f) * 8f, 20f + Mathf.Sin(t * 0.3f) * 3f, Mathf.Cos(t * 0.12f) * 4f);
                        yaw += 110f - 90f * Smooth(u * 3f) + Mathf.Sin(t * 0.2f) * 20f;
                        break;
                    case 5: // Underwater: swimming drift
                        height = Mathf.Min(MaxH * 0.85f, H0 * 2.5f);
                        pos = deck + new Vector3(Mathf.Sin(t * 0.25f) * 6f, 9f + Mathf.Sin(t * 0.4f) * 2.5f, 5f);
                        yaw += Mathf.Sin(t * 0.2f) * 40f;
                        glow = 0.7f;
                        break;
                    default: // Finale: lands back on the stage, swells, and the slam hits exactly on the explosion
                        {
                            float ch = Smooth((u - 0.4f) / 0.4f);
                            height = Mathf.Lerp(Mathf.Min(MaxH * 0.85f, H0 * 2.5f), Mathf.Min(MaxH, H0 * 3.5f), ch);
                            float sinceStart = t - AstronomicalSequence.Start[6];
                            pos = deck + new Vector3(0, 9f * (1f - Smooth(sinceStart / 2.2f)), 4f * Smooth(sinceStart / 2.2f));
                            glow = 1f + 1.5f * ch;
                            visible = u < 0.86f;
                        }
                        break;
                }
                {
                    Vector2 hp = new Vector2(pos.x, pos.z);
                    if (hp.magnitude > 16f)
                    {
                        hp = hp.normalized * 16f;
                        pos.x = hp.x;
                        pos.z = hp.y;
                    }
                } // never leave the playable area around the stage
                if (!perfInit || back)
                {
                    perfPos = pos;
                    perfH = height;
                    perfYaw = yaw;
                    perfInit = true;
                }
                float ks = 1f - Mathf.Exp(-dt * 2.5f);
                perfPos = Vector3.Lerp(perfPos, pos, ks);
                perfH = Mathf.Lerp(perfH, height, ks);
                perfYaw = Mathf.LerpAngle(perfYaw, yaw, ks);
                if (performerRoot.activeSelf != visible)
                    performerRoot.SetActive(visible);
                float tc = AstronomicalSequence.ChoreoTime(t);
                TickMoveLog(tc);
                if (shockT != null)
                {
                    if (shockIdx > 0 && shockIdx <= shockT.Length && tc < shockT[shockIdx - 1] - 0.3f)
                        shockIdx = 0; // seek backwards
                    while (shockIdx < shockT.Length && tc >= shockT[shockIdx])
                    {
                        if (visible && !paused && tc - shockT[shockIdx] < 0.5f)
                        {
                            Vector3 foot = performerRoot.transform.position;
                            float sz = shockSize[shockIdx];
                            AstronomicalHazards.Blast(foot, perfH * (1.2f + sz), 5f + perfH * 0.35f * sz, 11f);
                            if (spec != null)
                                spec.Boom(foot, Mathf.Min(14f, perfH * 0.35f * sz + 2f), hue);
                        }
                        shockIdx++;
                    }
                }
                if (visible)
                {
                    performerRoot.transform.localPosition = perfPos;
                    performerRoot.transform.localRotation = Quaternion.Euler(0, perfYaw, 0);
                    avatarModel.Pivot.transform.localScale = Vector3.one * PerformerUnitScale(perfH * (1f + 0.03f * kick));
                    if (showClip != null)
                        avatarModel.Sample(showClip, tc);
                    TickSeat(tc, perfH);
                    if (travisLight != null)
                    {
                        travisLight.transform.localPosition = new Vector3(0, perfH * 0.8f, -perfH * 0.9f);
                        travisLight.range = Mathf.Max(20f, perfH * 3f);
                        travisLight.color = i == 5 ? new Color(0.3f, 0.8f, 1f) : Color.Lerp(Color.white, c1, 0.5f);
                        travisLight.intensity = (3f + 9f * kick) * li * glow;
                    }
                }
                else if (seatOrb != null && seatOrb.gameObject.activeSelf)
                    seatOrb.gameObject.SetActive(false);
            }
            TickFestivalCity(t, hue, kick, fx);
            TickCoaster(t, kick);

            // lights: roaming colour lights orbit the arena, all pulse with the music
            float lp = (0.55f + 0.9f * kick + 0.3f * bass) * li;
            for (int r = 0; r < roam.Length; r++)
            {
                float a = t * (0.4f + r * 0.17f) + r * 1.57f,
                    rad = 10f + 6f * Mathf.Sin(t * 0.3f + r);
                roam[r].transform.localPosition = new Vector3(
                    Mathf.Cos(a) * rad,
                    3f + 4f * (0.5f + 0.5f * Mathf.Sin(t * 0.8f + r * 2)),
                    Mathf.Sin(a) * rad
                );
                roam[r].color = Col(hue + r * 0.25f, 0.85f, 1f);
                roam[r].intensity = lightBase[3 + r] * lp * (i >= 1 ? 1 : 0.3f);
            }
            for (int r = 0; r < 3; r++)
            {
                stageLights[r].color =
                    r == 0 ? c1
                    : r == 1 ? c2
                    : c3;
                stageLights[r].intensity = lightBase[r] * lp * (i >= 1 ? 1 : 0);
            }

            ApplyAtmos(dark, cosmic, water, i, li, fx, hue, kick);
        }

        static void SetPS(ParticleSystem ps, Color c, float rate)
        {
            var e = ps.emission;
            e.rateOverTime = rate;
            var m = ps.main;
            m.startColor = c;
        }

        void Burst(float density, float hue)
        {
            var ep = new ParticleSystem.EmitParams();
            Vector3 c = root.transform.TransformPoint(
                new Vector3(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(14f, 35f), UnityEngine.Random.Range(-4f, 24f))
            );
            Color col = Col(hue + UnityEngine.Random.value, 0.7f, 1f);
            int n = (int)(90 * density);
            for (int k = 0; k < n; k++)
            {
                ep.position = c;
                ep.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(5f, 11f);
                ep.startColor = col;
                ep.startSize = 0.6f;
                ep.startLifetime = 2.4f;
                fireworks.Emit(ep, 1);
            }
        }

        void ApplyAtmos(float dark, float cosmic, float water, int i, float li, float fx, float hue, float kick)
        {
            Color tint =
                water > 0.01f
                    ? Color.Lerp(Col(hue, 0.8f, 0.35f), new Color(0.05f, 0.35f, 0.6f), water)
                    : Col(hue, 0.85f, 0.3f + 0.15f * kick);
            float a = Mathf.Max(i == 0 ? dark * 0.5f : 0.5f, cosmic * 0.7f, water * 0.85f) * li;
            RenderSettings.fog = orig.fog || a > 0.01f;
            RenderSettings.fogColor = Color.Lerp(orig.fogColor, tint, a);
            RenderSettings.ambientLight = Color.Lerp(orig.ambient, Col(hue + 0.1f, 0.7f, 0.55f), a * 0.8f);
            if (water > 0.01f)
            {
                if (orig.mode == FogMode.Linear)
                    RenderSettings.fogEndDistance = Mathf.Lerp(orig.end, Mathf.Max(25f, orig.end * 0.4f), water * li);
                else
                    RenderSettings.fogDensity = orig.density + water * li * 0.02f;
            }
            else
            {
                RenderSettings.fogEndDistance = orig.end;
                RenderSettings.fogDensity = orig.density;
            }
            Color oc =
                megaFlash > 0.02f ? Color.white
                : water > 0.01f ? new Color(0.1f, 0.45f, 0.65f)
                : tint;
            float camA = AstronomicalSettings.CameraFx.Value ? Mathf.Max(water * 0.3f, 0.05f * kick * (i >= 1 ? 1 : 0)) * fx : 0; // pulse capped at ~3 Hz, low alpha
            if (AstronomicalSettings.CameraFx.Value)
                camA = Mathf.Max(camA, megaFlash * 0.85f); // single flash, decays over ~1 s
            Tint(overlayMat, oc, camA);
        }

        // Destroys only objects this class created and restores the RenderSettings values it captured.
        public void Cleanup()
        {
            if (rings != null)
            {
                try
                {
                    rings.Dispose();
                }
                catch { }
                rings = null;
            }
            AstronomicalHazards.Reset();
            shockT = null;
            shockSize = null;
            shockIdx = 0;
            if (spec != null)
            {
                try
                {
                    spec.Dispose();
                }
                catch (Exception ex)
                {
                    AstronomicalEventMod.Log.LogError("Spectacle cleanup: " + ex);
                }
                spec = null;
            }
            if (destr != null)
            {
                try
                {
                    destr.Dispose();
                }
                catch (Exception ex)
                {
                    AstronomicalEventMod.Log.LogError("Destruction cleanup: " + ex);
                }
                destr = null;
            }
            RestoreAtmos();
            // The shared models must survive the root's destruction: hand them back to the loader first.
            if (assets != null)
            {
                if (world != null && world.Pivot != null)
                    assets.Park(world.Pivot);
                if (stageModel != null && stageModel.Pivot != null)
                    assets.Park(stageModel.Pivot);
                if (avatarModel != null && avatarModel.Pivot != null)
                    assets.Park(avatarModel.Pivot);
                if (coasterModels)
                {
                    if (assets.CoasterTrack != null && assets.CoasterTrack.Pivot != null)
                        assets.Park(assets.CoasterTrack.Pivot);
                    if (assets.CoasterCart != null && assets.CoasterCart.Pivot != null)
                        assets.Park(assets.CoasterCart.Pivot);
                }
            }
            world = null;
            stageModel = null;
            avatarModel = null;
            assets = null;
            performerRoot = null;
            travisLight = null;
            vocalMic = null;
            avatarZUp = false;
            showClip = null;
            seatOrb = null;
            pelvisJoint = null;
            CurrentMove = "";
            coaster = coasterCart = null;
            coasterTrack.Clear();
            coasterPts = coasterUp = null;
            coasterModels = false;
            for (int k = owned.Count - 1; k >= 0; k--)
                if (owned[k] != null)
                    UnityEngine.Object.Destroy(owned[k]);
            owned.Clear();
            systems.Clear();
            lights.Clear();
            lightBase.Clear();
            root = stage = planets = orb = null;
            ring = null;
            lastIdx = -2;
            nextBurst = 0;
            nextBeat = 0;
            paused = false;
            hueAcc = 0;
            ringNext = 0;
            megaDone = false;
            megaFlash = 0;
        }
    }
}
