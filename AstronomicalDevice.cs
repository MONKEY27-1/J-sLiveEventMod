using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "The Device" (Fortnite Chapter 2 Season 2 finale, 15 June 2020), recreated in the Forest from the user's reading of the replay. Five pylons rise round the Agency (all up by 0:50), the Agency blows up (1:11),
    // the Doomsday Device rises out of it on a telescoping tower (full height 1:47) and its orb rises above (1:51, Midas inside); from 2:10 lightning runs between the orb and the pylons and from 2:20 hits the storm,
    // which is pushed back (2:27 - 2:40); office cutscene 2:40 - 2:56; the island becomes unstable at 3:13, the lightning returns at 3:38 and 3:49, the storm fights back (3:51 - 4:16, orange from 4:04), pylons and
    // device retract (4:48 / 4:59), office cutscene 5:02 - 5:28 and the water storm, office cutscene 6:03 - 6:25, white until 6:38, then the destroyed Agency surrounded by the water storm.
    // Everything is a pure function of the music time. The device stands on the gazebo; the Forest stays (it is only hidden during the office cutscenes, like the voting area of The Unvaulting).
    internal sealed class AstronomicalDevice : IEventScene, IDebug
    {
        const float PR = 60f,
            NEAR = 70f,
            MID = 100f,
            FAR = 240f,
            TOWER_K = 0.8f,
            PYL_H = 42f;
        const string ARENA_JSON = "{\"pts\":[[0,0],[76,0],[-76,0],[0,76],[0,-76],[54,54],[-54,54],[54,-54],[-54,-54]],\"margin\":14}";
        static readonly float[] PYL_START = { 9f, 24f, 4f, 33f, 16f };
        static readonly int[] PYL_ORDER = { 2, 0, 4, 1, 3 };

        sealed class Bolt
        {
            public LineRenderer Glow,
                Core;
        }

        sealed class Chunk
        {
            public Transform T;
            public Vector3 V;
            public float Spin,
                Life;
        }

        AstronomicalEventDef def;
        GameObject root,
            dome,
            office;
        WorldKit kit;
        Vector3 origin;
        float yaw,
            sc = 1f;
        int floorLayer;
        bool built,
            paused;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<GltfModel> models = new List<GltfModel>();
        readonly List<GameObject> clones = new List<GameObject>();
        float[] beats = new float[0],
            energy = new float[0];
        float endT = 402f;

        // the show
        float gy;
        GltfModel agency,
            deviceM,
            towerM,
            orbMidas,
            jonesM;
        Transform decoT,
            agencyT,
            ruinsT,
            rubbleT,
            deviceT,
            towerP,
            orbT,
            orbShell,
            glowQuad;
        Transform[] seg = new Transform[6];
        Transform cap;
        Transform[] pylon = new Transform[5];
        CapsuleCollider[] pylonCol = new CapsuleCollider[5];
        GltfModel.Clip midasClip,
            jonesClip;
        readonly List<GltfModel.Clip> midasCalm = new List<GltfModel.Clip>(),
            midasHot = new List<GltfModel.Clip>(),
            jonesClips = new List<GltfModel.Clip>();
        float midasFace,
            jonesFace;
        Material orbMat,
            glowMat,
            stormMat,
            waterMat,
            fireMat;
        Mesh stormMesh,
            waterMesh;
        Vector2[] stormUv0,
            stormUv;
        Vector3 deviceTop;
        Bolt[] bolts = new Bolt[48];
        int boltN;
        LineRenderer[] rings = new LineRenderer[4];
        float[] ringT = { -99f, -99f, -99f, -99f };
        int ringNext;
        Transform fireball;
        Material fireballMat;
        readonly List<Chunk> chunks = new List<Chunk>();
        ParticleSystem fx,
            smoke;
        Material matGlow;
        Texture2D glowTex,
            lineTex;
        Light orbLight,
            boomLight;
        Light sun;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        int skyN;
        float skyT = -9f;

        // state
        float lastT = -1f,
            flashV,
            rumbleT,
            accSm,
            accDust;
        int curMode = -1;
        bool exploded,
            ambientSaved,
            hiderOn;
        Vector3 ambientSave;
        GameObject flashSphere;
        Material topWhite;
        Vector3 officePos;
        float officeYaw;
        bool officePlaced;
        Vector3 glassHead;

        static float Sm(float a, float b, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        }

        static float Rnd(float a, float b)
        {
            return UnityEngine.Random.Range(a, b);
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

        Vector3 WV(Vector3 l)
        {
            return root.transform.TransformVector(l);
        }

        float Parts
        {
            get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); }
        }
        float Lit
        {
            get { return AstronomicalSettings.Lighting.Value; }
        }

        static Vector3? Head()
        {
            try
            {
                var p = GTPlayer.Instance;
                if (p != null)
                    return p.HeadCenterPosition;
            }
            catch { }
            return null;
        }

        float PylonAng(int i)
        {
            return (i * 72f + 18f) * Mathf.Deg2Rad;
        }

        Vector3 PylonBase(int i)
        {
            float a = PylonAng(i);
            return new Vector3(Mathf.Cos(a) * PR, gy, Mathf.Sin(a) * PR);
        }

        // ------------------------------------------------------------------ the timeline
        float PylonE(int i, float t)
        {
            return Sm(PYL_START[i], PYL_START[i] + 12f, t) * (1f - Sm(288f + 0.8f * i, 294f + 0.8f * i, t));
        }

        float TowerE(float t)
        {
            return Sm(85f, 107f, t) * (1f - Sm(289f, 298f, t));
        }

        float DeviceY(float t)
        {
            return t < 71f ? -999f : -16f * (1f - Sm(71f, 95f, t)) - 16f * Sm(295f, 299f, t);
        }

        float OrbRise(float t)
        {
            return Sm(111f, 126f, t) * (1f - Sm(288f, 296f, t));
        }

        bool OrbOn(float t)
        {
            return t >= 111f && t < 298f;
        }

        float StormR(float t)
        {
            if (t < 147f)
                return NEAR;
            if (t < 160f)
                return Mathf.Lerp(NEAR, FAR, Sm(147f, 160f, t));
            if (t < 193f)
                return FAR;
            if (t < 231f)
                return Mathf.Lerp(FAR, NEAR, Sm(193f, 231f, t)) + 6f * Mathf.Sin(t * 1.7f) * Sm(200f, 231f, t);
            if (t < 241f)
                return Mathf.Lerp(NEAR, MID, Sm(231f, 241f, t)) + 5f * Mathf.Sin(t * 2.3f);
            if (t < 244f)
                return Mathf.Lerp(MID, NEAR * 1.05f, Sm(241f, 244f, t));
            if (t < 247f)
                return NEAR * 1.05f;
            return Mathf.Lerp(NEAR * 1.05f, FAR * 1.1f, Sm(247f, 256f, t));
        }

        float StormA(float t)
        {
            if (t < 2f)
                return 0f;
            return Sm(2f, 8f, t) * (1f - Sm(256f, 268f, t));
        }

        float Orange(float t)
        {
            return Sm(244f, 247f, t);
        }

        float WaterR(float t)
        {
            return Mathf.Lerp(FAR, 125f, Sm(328f, 402f, t));
        }

        float WaterA(float t)
        {
            return Sm(328f, 336f, t);
        }

        bool InOffice(float t)
        {
            return (t >= 160f && t < 176f) || (t >= 302f && t < 328f) || (t >= 363f && t < 385f);
        }

        bool Whiteout(float t)
        {
            return t >= 385f && t < 398f;
        }

        float Unstable(float t)
        {
            return Sm(193f, 205f, t) * (1f - Sm(250f, 258f, t));
        }

        // how many pylons carry lightning to the orb (and to the storm) at t
        int OrbBolts(float t)
        {
            if (t < 130f)
                return 0;
            if (t < 138f)
                return 1 + (int)(Sm(130f, 138f, t) * 3.99f);
            if (t < 193f)
                return 5;
            if (t < 218f)
                return 0;
            if (t < 222f)
                return 1;
            if (t < 229f)
                return 3;
            if (t < 288f)
                return 5;
            return t < 292f ? 5 : 0;
        }

        bool StormBolts(float t)
        {
            return (t >= 140f && t < 193f) || (t >= 231f && t < 256f);
        }

        // ------------------------------------------------------------------ build
        Texture2D Swirl(Color a, Color b, Color c, float seed, float alphaMul)
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
                    col.a *= alphaMul * (0.45f + 0.55f * n);
                    return col;
                }
            );
        }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected;
            built = false;
            ReadCues();
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
            floorLayer = 0;
            for (int i = 0; i < 32; i++)
                if ((LocoMask() & (1 << i)) != 0)
                {
                    floorLayer = i;
                    break;
                }
            RaycastHit h;
            if (Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out h, 8f, LocoMask(), QueryTriggerInteraction.Ignore))
                floorLayer = h.collider.gameObject.layer;
            root = new GameObject("AstronomicalDevice");
            root.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, yaw, 0));
            root.transform.localScale = Vector3.one * sc;
            kit = new WorldKit { Layer = floorLayer };
            gy = 0f;
            if (Physics.Raycast(origin + Vector3.up * 6f, Vector3.down, out h, 40f, LocoMask(), QueryTriggerInteraction.Ignore))
                gy = root.transform.InverseTransformPoint(h.point).y;
            glowTex = kit.GlowTex();
            matGlow = kit.Glow(Color.white, glowTex, 3000);
            lineTex = kit.Tex(
                4,
                32,
                (x, yy) =>
                {
                    float v = Mathf.Abs((yy + 0.5f) / 16f - 1f);
                    return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f));
                },
                TextureWrapMode.Clamp
            );
            BuildGround();
            BuildDevice();
            BuildPylons();
            BuildOrb();
            BuildStorm();
            BuildFx();
            BuildSky();
            BuildOffice();
            BuildFlash();
            BuildExtras();
            sun = new GameObject("DeviceSun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.intensity = 0f;
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b);
            ambientSaved = true;
            lastT = -1f;
            curMode = -1;
            exploded = false;
            flashV = 0f;
            hiderOn = false;
            officePlaced = false;
            built = true;
            AstronomicalEventMod.Log.LogInfo(
                "The Device built: origin "
                    + origin
                    + " scale "
                    + sc.ToString("0.00")
                    + " yaw "
                    + yaw.ToString("0")
                    + ", layer "
                    + floorLayer
                    + ", ground "
                    + gy.ToString("0.0")
                    + ", agency "
                    + (agency != null)
                    + ", device "
                    + (deviceM != null)
                    + ", tower "
                    + (towerM != null)
                    + ", pylons "
                    + (pylon[0] != null)
                    + ", midas "
                    + (orbMidas != null)
                    + ", jones "
                    + (jonesM != null)
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
                pos = root.transform.position + Vector3.up * 1.2f - root.transform.forward * 20f * sc;
                y = yaw;
                return true;
            }
            if (fitOk)
            {
                pos = fitO + Vector3.up * 1.2f - Quaternion.Euler(0, fitY, 0) * Vector3.forward * 20f;
                y = fitY;
                return true;
            }
            return false;
        }

        void ReadCues()
        {
            var c = def != null ? def.Cues : null;
            endT = def != null && def.Length > 10f ? def.Length : 402f;
            if (c == null || c["beats"] == null)
            {
                var b = new List<float>();
                for (float t = 0f; t < endT; t += 0.6f)
                    b.Add(t);
                beats = b.ToArray();
                energy = new float[] { 0.3f };
                return;
            }
            beats = c["beats"].Floats();
            energy = c["energy"] != null ? c["energy"].Floats() : new float[] { 0.3f };
        }

        float Energy(float t)
        {
            if (energy.Length < 2)
                return energy.Length == 1 ? energy[0] : 0.3f;
            float f = Mathf.Clamp(t * 2f, 0f, energy.Length - 1.001f);
            int i = (int)f;
            return Mathf.Lerp(energy[i], energy[i + 1], f - i);
        }

        int BeatIdx(float t)
        {
            if (beats.Length < 2)
                return 0;
            int i = Array.BinarySearch(beats, t);
            if (i < 0)
                i = ~i - 1;
            return Mathf.Clamp(i, 0, beats.Length - 2);
        }

        Transform Node(GltfModel m, string n)
        {
            foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true))
                if (t.name == n)
                    return t;
            return null;
        }

        void Prep(GltfModel m, Transform parent, bool collide)
        {
            models.Add(m);
            m.Pivot.transform.SetParent(parent, false);
            m.Pivot.SetActive(true);
            foreach (var r in m.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var sk = r as SkinnedMeshRenderer;
                if (sk != null)
                    sk.updateWhenOffscreen = true;
            }
            foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = floorLayer;
        }

        void MeshCol(Transform n)
        {
            if (n == null)
                return;
            foreach (var mf in n.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    owned.Add(mc);
                }
        }

        void BuildGround()
        {
            agency = def.Prop("agency");
            if (agency == null || agency.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("Device: agency model missing");
                agency = null;
                return;
            }
            Prep(agency, root.transform, false);
            agency.Pivot.transform.localPosition = new Vector3(0f, gy, 0f);
            agency.Pivot.transform.localRotation = Quaternion.identity;
            decoT = Node(agency, "AgencyDeco");
            agencyT = Node(agency, "Agency");
            ruinsT = Node(agency, "Ruins");
            rubbleT = Node(agency, "RuinsRubble");
            MeshCol(agencyT);
            if (ruinsT != null)
                ruinsT.gameObject.SetActive(false);
            if (rubbleT != null)
                rubbleT.gameObject.SetActive(false);
        }

        void BuildDevice()
        {
            deviceM = def.Prop("device");
            if (deviceM != null && deviceM.Pivot != null)
            {
                Prep(deviceM, root.transform, false);
                deviceT = deviceM.Pivot.transform;
                var b = deviceM.RendererBounds.size;
                float k = 22f / Mathf.Max(0.01f, Mathf.Max(b.x, b.z));
                deviceT.localScale = Vector3.one * k;
                deviceT.localPosition = new Vector3(0f, gy - 999f, 0f);
                deviceTop = new Vector3(0f, gy + b.y * k * 0.55f, 0f);
                var bc = deviceT.gameObject.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, b.y * 0.5f, 0f);
                bc.size = new Vector3(b.x * 0.9f, b.y, b.z * 0.9f);
                owned.Add(bc);
            }
            towerM = def.Prop("tower");
            if (towerM != null && towerM.Pivot != null)
            {
                Prep(towerM, root.transform, false);
                towerP = towerM.Pivot.transform;
                towerP.localScale = Vector3.one * TOWER_K;
                towerP.localPosition = new Vector3(0f, gy + 3f, 0f);
                for (int k = 0; k < 6; k++)
                    seg[k] = Node(towerM, "Seg" + k);
                cap = Node(towerM, "Cap");
            }
        }

        void BuildPylons()
        {
            var m = def.Prop("tentacle");
            if (m == null || m.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("Device: pylon model missing");
                return;
            }
            Prep(m, root.transform, false);
            float k = PYL_H / Mathf.Max(0.01f, m.RendererBounds.size.y);
            pylon[0] = m.Pivot.transform;
            for (int i = 1; i < 5; i++)
            {
                var c = UnityEngine.Object.Instantiate(m.Pivot);
                c.transform.SetParent(root.transform, false);
                c.SetActive(true);
                clones.Add(c);
                pylon[i] = c.transform;
            }
            for (int i = 0; i < 5; i++)
            {
                pylon[i].localScale = Vector3.one * k;
                pylon[i].localPosition = PylonBase(i) + Vector3.down * 999f;
                foreach (var t in pylon[i].GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = floorLayer;
                var cc = pylon[i].gameObject.AddComponent<CapsuleCollider>();
                cc.direction = 1;
                cc.radius = m.RendererBounds.size.x * 0.5f * 1.2f;
                cc.height = m.RendererBounds.size.y;
                cc.center = new Vector3(0f, m.RendererBounds.size.y * 0.5f, 0f);
                pylonCol[i] = cc;
                owned.Add(cc);
            }
        }

        void BuildOrb()
        {
            var shellTex = Swirl(new Color(0.35f, 0.1f, 0.5f), new Color(0.9f, 0.35f, 0.55f), new Color(1f, 0.85f, 0.7f), 0.3f, 0.95f);
            orbMat = kit.Glow(Color.white, shellTex, 3004);
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
            s.name = "Orb";
            s.transform.SetParent(root.transform, false);
            s.GetComponent<Renderer>().sharedMaterial = orbMat;
            s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            orbT = s.transform;
            orbShell = s.transform;
            var gq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(gq.GetComponent<Collider>());
            gq.name = "OrbGlow";
            gq.transform.SetParent(root.transform, false);
            glowMat = kit.Glow(new Color(1f, 0.75f, 0.85f, 0.7f), glowTex, 3003);
            gq.GetComponent<Renderer>().sharedMaterial = glowMat;
            glowQuad = gq.transform;
            gq.SetActive(false);
            s.SetActive(false);
            orbMidas = def.Prop("cyclo");
            if (orbMidas != null && orbMidas.Pivot != null)
            {
                Prep(orbMidas, root.transform, false);
                orbMidas.Pivot.SetActive(false);
                midasClip = orbMidas.FindClipExact("C_Hover") ?? (orbMidas.Clips.Count > 0 ? orbMidas.Clips[0] : null);
                foreach (var c in orbMidas.Clips)
                {
                    if (!c.Name.StartsWith("C_"))
                        continue;
                    string n = c.Name.Substring(2);
                    if (n == "Hover" || n == "Float" || n == "Cross" || n == "Wings" || n == "OrbHold" || n == "Look")
                        midasCalm.Add(c);
                    else
                        midasHot.Add(c);
                }
                var f = AstronomicalConcert.Facing(orbMidas);
                midasFace = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                // the storm materials are black in the file (they are animated energy in the game): replaced with moving energy textures
                var armTex = Swirl(new Color(0.1f, 0.1f, 0.55f), new Color(0.4f, 0.55f, 1f), new Color(1f, 1f, 1f), 0.6f, 0.95f);
                var headTex = Swirl(new Color(0.25f, 0.05f, 0.45f), new Color(0.7f, 0.3f, 0.95f), new Color(1f, 0.8f, 1f), 0.9f, 1f);
                foreach (var r in orbMidas.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.gameObject.name;
                    Material m = null;
                    if (n.Contains("Storm_Arm") || n.Contains("EnergyArms"))
                        m = kit.Glow(Color.white, armTex, 3005);
                    else if (n.Contains("Storm_Head"))
                        m = kit.Glow(Color.white, headTex, 3005);
                    if (m != null)
                    {
                        var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int k = 0; k < arr.Length; k++)
                            arr[k] = m;
                        r.sharedMaterials = arr;
                    }
                }
            }
            orbLight = new GameObject("OrbLight").AddComponent<Light>();
            orbLight.transform.SetParent(root.transform, false);
            orbLight.type = LightType.Point;
            orbLight.range = 160f * sc + 30f;
            orbLight.intensity = 0f;
            orbLight.color = new Color(1f, 0.7f, 0.85f);
            orbLight.shadows = LightShadows.None;
            boomLight = new GameObject("BoomLight").AddComponent<Light>();
            boomLight.transform.SetParent(root.transform, false);
            boomLight.type = LightType.Point;
            boomLight.range = 300f * sc + 30f;
            boomLight.intensity = 0f;
            boomLight.color = new Color(1f, 0.6f, 0.25f);
            boomLight.shadows = LightShadows.None;
        }

        // the storm: an open cylinder (radius 1, height 120, scaled to the storm radius) whose uv scrolls; the water storm is the same kind of wall, tinted blue, with a sea outside it
        Mesh RingMesh(int lon, int rings, float h, bool wall)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var cols = new List<Color>();
            var tr = new List<int>();
            for (int k = 0; k <= rings; k++)
            for (int j = 0; j <= lon; j++)
            {
                float a = j * Mathf.PI * 2f / lon,
                    f = k / (float)rings;
                v.Add(new Vector3(Mathf.Cos(a), f * h, Mathf.Sin(a)));
                uv.Add(new Vector2(j * 6f / lon, f * 5f));
                cols.Add(new Color(1, 1, 1, Mathf.Clamp01(Mathf.Min(f * 6f, (1f - f) * 3f))));
            }
            for (int k = 0; k < rings; k++)
            for (int j = 0; j < lon; j++)
            {
                int a = k * (lon + 1) + j,
                    b = a + 1,
                    c = a + lon + 1,
                    d = c + 1;
                tr.AddRange(new[] { a, b, c, b, d, c });
            }
            var me = new Mesh();
            me.SetVertices(v);
            me.SetUVs(0, uv);
            me.SetColors(cols);
            me.SetTriangles(tr, 0);
            me.RecalculateBounds();
            kit.Keep(me);
            return me;
        }

        GameObject stormG,
            waterG,
            seaG;

        void BuildStorm()
        {
            stormMesh = RingMesh(72, 10, 320f, true);
            stormUv0 = stormMesh.uv;
            stormUv = new Vector2[stormUv0.Length];
            stormMat = kit.Glow(
                Color.white,
                Swirl(new Color(0.25f, 0.1f, 0.5f), new Color(0.6f, 0.3f, 1f), new Color(1f, 0.75f, 1f), 0.2f, 0.9f),
                2995
            );
            stormG = new GameObject("Storm");
            stormG.transform.SetParent(root.transform, false);
            stormG.AddComponent<MeshFilter>().sharedMesh = stormMesh;
            var mr = stormG.AddComponent<MeshRenderer>();
            mr.sharedMaterial = stormMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.allowOcclusionWhenDynamic = false;
            stormG.SetActive(false);
            waterMat = kit.Glow(
                Color.white,
                Swirl(new Color(0.05f, 0.3f, 0.6f), new Color(0.2f, 0.6f, 0.9f), new Color(0.85f, 1f, 1f), 0.5f, 0.95f),
                2995
            );
            waterG = new GameObject("WaterStorm");
            waterG.transform.SetParent(root.transform, false);
            waterG.AddComponent<MeshFilter>().sharedMesh = stormMesh;
            var mr2 = waterG.AddComponent<MeshRenderer>();
            mr2.sharedMaterial = waterMat;
            mr2.shadowCastingMode = ShadowCastingMode.Off;
            mr2.allowOcclusionWhenDynamic = false;
            waterG.SetActive(false);
            // the sea outside the water wall: a flat annulus from radius 1 to 4 (scaled), seen as one big ring
            var vv = new List<Vector3>();
            var tt = new List<int>();
            var uu = new List<Vector2>();
            int seg2 = 64;
            for (int j = 0; j <= seg2; j++)
            {
                float a = j * Mathf.PI * 2f / seg2;
                vv.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                vv.Add(new Vector3(Mathf.Cos(a) * 4f, 0f, Mathf.Sin(a) * 4f));
                uu.Add(new Vector2(j * 0.5f, 0f));
                uu.Add(new Vector2(j * 0.5f, 4f));
            }
            for (int j = 0; j < seg2; j++)
            {
                int a = j * 2;
                tt.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            waterMesh = new Mesh();
            waterMesh.SetVertices(vv);
            waterMesh.SetUVs(0, uu);
            waterMesh.SetTriangles(tt, 0);
            waterMesh.RecalculateBounds();
            kit.Keep(waterMesh);
            seaG = new GameObject("Sea");
            seaG.transform.SetParent(root.transform, false);
            seaG.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            var mr3 = seaG.AddComponent<MeshRenderer>();
            mr3.sharedMaterial = waterMat;
            mr3.shadowCastingMode = ShadowCastingMode.Off;
            seaG.SetActive(false);
        }

        ParticleSystem MakePS(string name, Material mat, int max, float grav, float life)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.maxParticles = max;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.playOnAwake = false;
            m.startLifetime = life;
            m.gravityModifier = grav;
            var e = ps.emission;
            e.enabled = false;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        void Emit(ParticleSystem ps, Vector3 lp, Vector3 lv, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams();
            ep.position = W(lp);
            ep.velocity = WV(lv);
            ep.startColor = c;
            ep.startSize = size * sc;
            ep.startLifetime = life;
            ps.Emit(ep, 1);
        }

        void BuildFx()
        {
            fx = MakePS("DeviceFx", matGlow, 5000, 0.12f, 2f);
            smoke = MakePS("DeviceSmoke", matGlow, 3000, -0.01f, 10f);
            var sol = smoke.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.0f));
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var gr = new Gradient();
            gr.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            gr.alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.5f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            };
            col.color = gr;
            for (int i = 0; i < bolts.Length; i++)
            {
                var b = new Bolt();
                var g = new GameObject("Bolt" + i);
                g.transform.SetParent(root.transform, false);
                b.Glow = g.AddComponent<LineRenderer>();
                b.Glow.useWorldSpace = true;
                b.Glow.positionCount = 14;
                b.Glow.sharedMaterial = kit.Glow(Color.white, lineTex, 3001);
                b.Glow.shadowCastingMode = ShadowCastingMode.Off;
                b.Glow.enabled = false;
                var g2 = new GameObject("BoltCore");
                g2.transform.SetParent(g.transform, false);
                b.Core = g2.AddComponent<LineRenderer>();
                b.Core.useWorldSpace = true;
                b.Core.positionCount = 14;
                b.Core.sharedMaterial = kit.Glow(Color.white, lineTex, 3002);
                b.Core.shadowCastingMode = ShadowCastingMode.Off;
                b.Core.enabled = false;
                bolts[i] = b;
            }
            var soft = kit.Glow(Color.white, lineTex, 3001);
            for (int i = 0; i < rings.Length; i++)
            {
                var g = new GameObject("Shock" + i);
                g.transform.SetParent(root.transform, false);
                var l = g.AddComponent<LineRenderer>();
                l.useWorldSpace = true;
                l.positionCount = 64;
                l.loop = true;
                l.sharedMaterial = soft;
                l.shadowCastingMode = ShadowCastingMode.Off;
                l.enabled = false;
                rings[i] = l;
            }
            var fb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(fb.GetComponent<Collider>());
            fb.name = "Fireball";
            fb.transform.SetParent(root.transform, false);
            fireballMat = kit.Glow(new Color(1f, 0.6f, 0.2f, 0.9f), glowTex, 3003);
            fb.GetComponent<Renderer>().sharedMaterial = fireballMat;
            fb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            fireball = fb.transform;
            fb.SetActive(false);
            var rock = kit.Box(new Vector3(1f, 1f, 1f), 1f);
            var rm = kit.Lit(new Color(0.35f, 0.34f, 0.36f), null, 0.1f);
            var rnd = new System.Random(3);
            for (int i = 0; i < 60; i++)
            {
                var o = kit.Obj(
                    "Chunk" + i,
                    root.transform,
                    rock,
                    rm,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one * (float)(0.8 + rnd.NextDouble() * 2.6),
                    false
                ).transform;
                o.gameObject.SetActive(false);
                float ang = (float)(rnd.NextDouble() * 6.283),
                    sp = (float)(10 + rnd.NextDouble() * 34),
                    up = (float)(18 + rnd.NextDouble() * 40);
                chunks.Add(
                    new Chunk
                    {
                        T = o,
                        V = new Vector3(Mathf.Cos(ang) * sp, up, Mathf.Sin(ang) * sp),
                        Spin = (float)(rnd.NextDouble() * 300 - 150),
                        Life = (float)(4 + rnd.NextDouble() * 2),
                    }
                );
            }
        }

        void BuildSky()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cols = new List<Color>();
            var dirs = new List<Vector3>();
            int lat = 18,
                lon = 36;
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
            dome = new GameObject("AstronomicalDeviceSky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(matGlow);
            dm.mainTexture = null;
            dm.renderQueue = 2501;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // the sky: deep blue-violet with moving cloud bands; orange while the storm turns orange, blue once it is gone, teal for the water storm
        void UpdateSky(float t, float lightning)
        {
            if (skyMesh == null)
                return;
            float orange = Orange(t) * (1f - Sm(256f, 262f, t)),
                calm = Sm(256f, 270f, t),
                water = Sm(328f, 340f, t);
            Color top = Color.Lerp(
                Color.Lerp(new Color(0.10f, 0.07f, 0.42f), new Color(0.32f, 0.10f, 0.16f), orange),
                Color.Lerp(new Color(0.12f, 0.27f, 0.66f), new Color(0.06f, 0.38f, 0.52f), water),
                calm
            );
            Color hor = Color.Lerp(
                Color.Lerp(new Color(0.50f, 0.32f, 0.90f), new Color(1f, 0.52f, 0.2f), orange),
                Color.Lerp(new Color(0.45f, 0.6f, 1f), new Color(0.3f, 0.8f, 0.85f), water),
                calm
            );
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k];
                float el = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)),
                    az = Mathf.Atan2(d.z, d.x);
                var c = Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.5f));
                float cl =
                    0.5f
                    + 0.5f
                        * Mathf.Sin(az * 5f + t * 0.2f + Mathf.Sin(el * 9f + t * 0.35f) * 1.4f)
                        * Mathf.Sin(el * 7f - t * 0.1f + az * 2f);
                cl = cl * cl * 0.35f;
                c.r += cl * 0.5f + lightning * 0.15f;
                c.g += cl * 0.35f + lightning * 0.1f;
                c.b += cl * 0.7f + lightning * 0.2f;
                c.a = 1f;
                skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        // ------------------------------------------------------------------ the office (the cutscenes): a closed room that stands where the player is, Forest unloaded round it
        GltfModel jonesModel;
        Transform jonesT;
        Material screenMat;
        Texture2D screenTex;
        GltfModel officeM;
        Material[] officeGlow;

        void BuildOffice()
        {
            office = new GameObject("AstronomicalOffice");
            var T = office.transform;
            officeM = def.Prop("office");
            if (officeM != null && officeM.Pivot != null)
            {
                models.Add(officeM);
                officeM.Pivot.transform.SetParent(T, false);
                officeM.Pivot.SetActive(true);
                officeM.Pivot.transform.localPosition = Vector3.zero;
                officeM.Pivot.transform.localRotation = Quaternion.identity;
                foreach (var r in officeM.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                foreach (var n in new[] { "Shell", "Furniture" })
                    MeshCol(Node(officeM, n));
                var gl = Node(officeM, "Glow");
                if (gl != null)
                {
                    var r = gl.GetComponentInChildren<Renderer>(true);
                    if (r != null)
                        officeGlow = r.sharedMaterials;
                } // glow materials in slot order: 0 / 1 screens, 2 window, 3 ceiling panels, 4 lamp
            }
            else
                AstronomicalEventMod.Log.LogWarning("Device: office model missing");
            foreach (var p in new[] { new Vector3(0f, 2.7f, 1.6f), new Vector3(-2f, 2.7f, -1.2f), new Vector3(2f, 2.7f, -1.2f) })
            {
                var l = new GameObject("OfficeLight").AddComponent<Light>();
                l.transform.SetParent(T, false);
                l.transform.localPosition = p;
                l.type = LightType.Point;
                l.range = 10f;
                l.intensity = 1.5f * Mathf.Max(0.5f, Lit);
                l.color = new Color(1f, 0.96f, 0.88f);
                l.shadows = LightShadows.None;
            }
            officeFlash = new GameObject("OfficeStormLight").AddComponent<Light>();
            officeFlash.transform.SetParent(T, false);
            officeFlash.transform.localPosition = new Vector3(3.9f, 1.5f, 0.4f);
            officeFlash.type = LightType.Point;
            officeFlash.range = 8f;
            officeFlash.color = new Color(0.6f, 0.5f, 1f);
            officeFlash.intensity = 0.5f;
            officeFlash.shadows = LightShadows.None;
            jonesM = def.Prop("jones");
            if (jonesM != null && jonesM.Pivot != null)
            {
                models.Add(jonesM);
                jonesM.Pivot.transform.SetParent(T, false);
                jonesM.Pivot.SetActive(true);
                jonesT = jonesM.Pivot.transform;
                float k = 1.8f / Mathf.Max(0.01f, jonesM.RendererBounds.size.y);
                var f = AstronomicalConcert.Facing(jonesM);
                jonesFace = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg + 180f;
                jonesT.localScale = Vector3.one * k;
                jonesT.localPosition = new Vector3(0f, 0.03f, 2.3f);
                jonesT.localRotation = Quaternion.Euler(0f, jonesFace, 0f);
                jonesClip = jonesM.FindClipExact("J_Idle") ?? (jonesM.Clips.Count > 0 ? jonesM.Clips[0] : null);
                foreach (var c in jonesM.Clips)
                    if (c.Name.StartsWith("J_"))
                        jonesClips.Add(c);
                foreach (var r in jonesM.Pivot.GetComponentsInChildren<Renderer>(true))
                {
                    r.allowOcclusionWhenDynamic = false;
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    var sk = r as SkinnedMeshRenderer;
                    if (sk != null)
                        sk.updateWhenOffscreen = true;
                }
            }
            office.SetActive(false);
        }

        void BuildFlash()
        {
            flashSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(flashSphere.GetComponent<Collider>());
            flashSphere.name = "AstronomicalDeviceFlash";
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

        float WhiteAlpha(float t)
        {
            float a = 0f;
            foreach (float s in new[] { 160f, 302f, 363f })
            {
                a = Mathf.Max(a, t < s ? Sm(s - 0.8f, s, t) : 1f - Sm(s, s + 1.2f, t));
            }
            foreach (float e in new[] { 176f, 328f })
            {
                a = Mathf.Max(a, t < e ? Sm(e - 0.8f, e, t) : 1f - Sm(e, e + 1.2f, t));
            }
            if (t >= 385f)
                a = Mathf.Max(a, t < 398f ? 1f : 1f - Sm(398f, 402.5f, t));
            else
                a = Mathf.Max(a, Sm(384.2f, 385f, t) * (t >= 363f ? 1f : 0f));
            return a;
        }

        void EnterOffice()
        {
            Vector3 head = origin + Vector3.up * 1.4f;
            var cam = Camera.main;
            try
            {
                var p = GTPlayer.Instance;
                if (p != null)
                    head = p.headCollider.transform.position;
            }
            catch { }
            float fy = head.y - 1.4f;
            RaycastHit h;
            if (Physics.Raycast(head, Vector3.down, out h, 10f, LocoMask(), QueryTriggerInteraction.Ignore))
                fy = h.point.y;
            float oy = yaw;
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.01f)
                    oy = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
            var rot = Quaternion.Euler(0, oy, 0);
            office.transform.SetPositionAndRotation(new Vector3(head.x, fy, head.z) - rot * new Vector3(0f, 0f, -1.4f), rot);
            glassHead = head;
            foreach (var c in office.GetComponentsInChildren<Collider>(true))
                c.gameObject.layer = floorLayer;
            AstronomicalForestHider.Hide(
                new Vector3(head.x, fy, head.z),
                300f,
                tr =>
                    tr == office.transform
                    || tr.IsChildOf(office.transform)
                    || tr == root.transform
                    || tr.IsChildOf(root.transform)
                    || tr == dome.transform
            );
            hiderOn = true;
        }

        void ExitOffice()
        {
            if (hiderOn)
            {
                AstronomicalForestHider.Restore();
                hiderOn = false;
            }
        }

        void SetMode(int mode)
        {
            if (mode == curMode)
                return;
            int prev = curMode;
            curMode = mode; // 0 island, 1 office, 2 white (nothing)
            root.SetActive(mode == 0);
            dome.SetActive(mode == 0);
            office.SetActive(mode == 1);
            if (mode == 1)
                EnterOffice();
            else if (prev == 1)
                ExitOffice();
        }

        // ------------------------------------------------------------------ per frame
        public void SetPaused(bool p)
        {
            paused = p;
            foreach (var ps in new[] { fx, smoke })
            {
                if (ps == null)
                    continue;
                if (p)
                    ps.Pause(true);
                else
                    ps.Play(true);
            }
        }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            bool back = t < lastT - 0.4f;
            if (back || lastT < 0f)
            {
                exploded = false;
                skyT = -9f;
                for (int i = 0; i < ringT.Length; i++)
                    ringT[i] = -99f;
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null)
            {
                dome.transform.position = cam.transform.position;
            }
            int mode =
                InOffice(t) ? 1
                : Whiteout(t) ? 2
                : 0;
            SetMode(mode);
            float wa = WhiteAlpha(t);
            bool wOn = wa > 0.003f;
            if (flashSphere.activeSelf != wOn)
                flashSphere.SetActive(wOn);
            if (wOn)
            {
                topWhite.color = new Color(1f, 1f, 1f, wa);
                if (cam != null)
                    flashSphere.transform.position = cam.transform.position;
            }
            if (mode != 0)
                AstronomicalHazards.Swim(false, Vector3.zero);
            if (mode == 1)
            {
                OfficeStep(t);
                return;
            }
            if (mode == 2)
            {
                Atmosphere(t, 0f);
                return;
            }
            float en = Energy(t);
            int bi = BeatIdx(t);
            float span = beats.Length > 1 ? Mathf.Max(0.05f, beats[bi + 1] - beats[bi]) : 0.6f,
                ph = Mathf.Clamp01((t - (beats.Length > 1 ? beats[bi] : 0f)) / span),
                pulse = Mathf.Pow(1f - ph, 3f);
            Events(t, dt);
            Machine(t, dt, pulse, en);
            StormStep(t);
            BoltStep(t, pulse, en);
            Explosion(t, dt);
            RingStep(t);
            Atmosphere(t, en);
            Extras(t, dt, pulse);
            SwimStep(t);
            float lit = Mathf.Clamp01(boltN / 8f);
            if (t - skyT > 0.1f || t < skyT)
            {
                skyT = t;
                UpdateSky(t, lit);
            }
            Rumble(t, dt, pulse);
        }

        // ---- many clips: a new one every few seconds, crossfaded (all clips share one channel layout). Jonesy picks from his office clips; Midas picks calm ones until the lightning starts, then energetic ones.
        static GltfModel.Clip PickFrom(List<GltfModel.Clip> list, int seg)
        {
            if (list.Count == 0)
                return null;
            int i = (int)(WorldKit.Hash(seg, 7, 31) * list.Count) % list.Count;
            if (list.Count > 1)
            {
                int pv = (int)(WorldKit.Hash(seg - 1, 7, 31) * list.Count) % list.Count;
                if (pv == i)
                    i = (i + 1) % list.Count;
            }
            return list[i];
        }

        GltfModel.Clip PickMidas(int seg)
        {
            return PickFrom(
                OrbBolts(seg * 5f + 0.5f) > 0 && midasHot.Count > 0 ? midasHot
                    : midasCalm.Count > 0 ? midasCalm
                    : midasHot,
                seg
            );
        }

        void PlayMidas(float t)
        {
            const float L = 5f,
                X = 0.7f;
            int seg = (int)(t / L);
            float tin = t - seg * L;
            var a = PickMidas(seg);
            if (a == null)
            {
                if (midasClip != null)
                    orbMidas.Sample(midasClip, t);
                return;
            }
            if (tin > L - X)
            {
                var b2 = PickMidas(seg + 1);
                if (b2 != null && b2 != a)
                {
                    orbMidas.SampleBlend(a, t, b2, t, Sm(L - X, L, tin));
                    return;
                }
            }
            orbMidas.Sample(a, t);
        }

        void PlayJones(float t)
        {
            const float L = 6f,
                X = 0.7f;
            int seg = (int)(t / L);
            float tin = t - seg * L;
            var a = PickFrom(jonesClips, seg);
            if (a == null)
            {
                if (jonesClip != null)
                    jonesM.Sample(jonesClip, t);
                return;
            }
            if (tin > L - X)
            {
                var b2 = PickFrom(jonesClips, seg + 1);
                if (b2 != null && b2 != a)
                {
                    jonesM.SampleBlend(a, t, b2, t, Sm(L - X, L, tin));
                    return;
                }
            }
            jonesM.Sample(a, t);
        }

        // the one-off moments
        float prevT = -1f;

        void Events(float t, float dt)
        {
            if (prevT < 0f || t < prevT - 0.4f)
                prevT = t - 0.0001f;
            float a = prevT,
                b = t;
            prevT = t;
            if (paused)
                return;
            Func<float, bool> cross = x => a < x && b >= x && b - x < 1.5f;
            if (cross(71f))
            {
                exploded = true;
                Ring(71f);
                flashV = 1f;
                AstronomicalHazards.Blast(W(new Vector3(0f, gy + 2f, 0f)), 110f * sc, 20f, 16f);
            }
            if (cross(111f))
            {
                Ring(111f);
                AstronomicalHazards.Blast(W(new Vector3(0f, gy + 2f, 0f)), 70f * sc, 10f);
            }
            if (cross(138f))
            {
                Ring(138f);
                flashV = Mathf.Max(flashV, 0.7f);
                AstronomicalHazards.Blast(W(new Vector3(0f, gy + 2f, 0f)), 90f * sc, 12f);
            }
            if (cross(229f))
            {
                Ring(229f);
                flashV = Mathf.Max(flashV, 0.5f);
            }
            if (cross(288f) || cross(299f))
            {
                Ring(b);
                AstronomicalHazards.Blast(W(new Vector3(0f, gy + 2f, 0f)), 90f * sc, 12f);
            }
            if (t >= 71f && !exploded)
                exploded = true;
        }

        void Ring(float t)
        {
            ringT[ringNext] = t;
            ringNext = (ringNext + 1) % rings.Length;
        }

        void RingStep(float t)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var l = rings[i];
                float age = t - ringT[i],
                    life = 3f;
                bool on = age >= 0f && age < life;
                if (l.enabled != on)
                    l.enabled = on;
                if (!on)
                    continue;
                float k = age / life,
                    r = 4f + age * 60f;
                for (int j = 0; j < 64; j++)
                {
                    float a = j * Mathf.PI * 2f / 64f;
                    l.SetPosition(j, W(new Vector3(Mathf.Cos(a) * r, gy + 0.4f, Mathf.Sin(a) * r)));
                }
                l.startColor = l.endColor = new Color(1f, 0.85f, 0.7f, 0.9f * (1f - k) * (1f - k));
                l.widthMultiplier = 3f * (1f - 0.6f * k) * sc;
            }
        }

        void Explosion(float t, float dt)
        {
            bool ex = t >= 71f;
            if (agencyT != null)
            {
                bool on = !ex;
                if (agencyT.gameObject.activeSelf != on)
                    agencyT.gameObject.SetActive(on);
            }
            if (decoT != null && decoT.gameObject.activeSelf == ex)
                decoT.gameObject.SetActive(!ex);
            if (ruinsT != null && ruinsT.gameObject.activeSelf != ex)
                ruinsT.gameObject.SetActive(ex);
            if (rubbleT != null && rubbleT.gameObject.activeSelf != ex)
                rubbleT.gameObject.SetActive(ex);
            float age = t - 71f;
            bool fb = age >= 0f && age < 2f;
            if (fireball.gameObject.activeSelf != fb)
                fireball.gameObject.SetActive(fb);
            if (fb)
            {
                float k = age / 2f;
                fireball.localPosition = new Vector3(0f, gy + 10f + 8f * k, 0f);
                fireball.localScale = Vector3.one * (20f + 90f * Mathf.Sqrt(k));
                fireballMat.color = new Color(1f, 0.5f + 0.4f * k, 0.15f + 0.4f * k, 0.9f * (1f - k));
                boomLight.intensity = Lit * 8f * (1f - k);
                boomLight.transform.localPosition = new Vector3(0f, gy + 20f, 0f);
            }
            else
                boomLight.intensity = 0f;
            foreach (var c in chunks)
            {
                bool on = age >= 0f && age < c.Life;
                if (c.T.gameObject.activeSelf != on)
                    c.T.gameObject.SetActive(on);
                if (!on)
                    continue;
                var p = new Vector3(c.V.x * age, gy + 6f + c.V.y * age - 0.5f * 28f * age * age, c.V.z * age);
                if (p.y < gy + 0.5f)
                {
                    p.y = gy + 0.5f;
                    p.x = c.V.x * Mathf.Min(age, 2.5f) * 0.8f;
                    p.z = c.V.z * Mathf.Min(age, 2.5f) * 0.8f;
                }
                c.T.localPosition = p;
                c.T.localRotation = Quaternion.Euler(c.Spin * age, c.Spin * 0.6f * age, c.Spin * 0.3f * age);
            }
            if (paused || dt <= 0f)
                return;
            if (age >= 0f && age < 20f)
                for (int k = Take(ref accSm, 40f * Parts * (1f - age / 20f), dt); k > 0; k--)
                    Emit(
                        smoke,
                        new Vector3(Rnd(-20f, 20f), gy + 2f, Rnd(-16f, 16f)),
                        new Vector3(Rnd(-2f, 2f), Rnd(4f, 12f), Rnd(-2f, 2f)),
                        new Color(0.22f, 0.2f, 0.2f, 0.7f),
                        Rnd(10f, 22f),
                        Rnd(7f, 10f)
                    );
            if (age >= 0f && age < 0.6f)
                for (int k = 0; k < (int)(150 * Parts); k++)
                    Emit(
                        fx,
                        new Vector3(Rnd(-12f, 12f), gy + 6f, Rnd(-10f, 10f)),
                        UnityEngine.Random.onUnitSphere * Rnd(15f, 60f),
                        new Color(1f, Rnd(0.5f, 0.9f), 0.2f),
                        Rnd(2f, 5f),
                        Rnd(1f, 2.5f)
                    );
            if (ex && t < 395f)
                for (int k = Take(ref accDust, 6f * Parts, dt); k > 0; k--)
                    Emit(
                        fx,
                        new Vector3(Rnd(-22f, 22f), gy + 1f, Rnd(-18f, 18f)),
                        new Vector3(Rnd(-1f, 1f), Rnd(6f, 14f), Rnd(-1f, 1f)),
                        new Color(1f, 0.5f, 0.15f, 0.8f),
                        Rnd(0.8f, 1.6f),
                        Rnd(1f, 2f)
                    ); // the ruins keep burning
            float uns = Unstable(t);
            if (uns > 0.05f)
                for (int k = Take(ref accSm, 14f * uns * Parts, dt); k > 0; k--)
                    Emit(
                        smoke,
                        new Vector3(Rnd(-60f, 60f), gy + 0.5f, Rnd(-60f, 60f)),
                        new Vector3(0f, Rnd(3f, 8f), 0f),
                        new Color(0.5f, 0.45f, 0.4f, 0.35f),
                        Rnd(5f, 10f),
                        Rnd(5f, 8f)
                    ); // dust shaken up
        }

        static int Take(ref float acc, float rate, float dt)
        {
            acc += rate * dt;
            int n = (int)acc;
            acc -= n;
            return n;
        }

        // the Device, the tower and the orb
        void Machine(float t, float dt, float pulse, float en)
        {
            float dy = DeviceY(t);
            if (deviceT != null)
            {
                bool on = dy > -900f;
                if (deviceT.gameObject.activeSelf != on)
                    deviceT.gameObject.SetActive(on);
                if (on)
                    deviceT.localPosition = new Vector3(0f, gy + dy, 0f);
            }
            float e = TowerE(t);
            if (towerP != null)
            {
                bool on = t >= 71f && e > 0.001f;
                if (towerP.gameObject.activeSelf != on)
                    towerP.gameObject.SetActive(on);
                towerP.localPosition = new Vector3(0f, gy + 3f + (deviceT != null ? dy : 0f), 0f);
            }
            float topY = gy + 3f + (deviceT != null ? dy : 0f);
            if (towerP != null)
            {
                for (int k = 0; k < 6; k++)
                    if (seg[k] != null)
                        seg[k].localPosition = new Vector3(0f, e * k * 12f, 0f);
                if (cap != null)
                    cap.localPosition = new Vector3(0f, e * 72f, 0f);
                topY += (e * 72f + 8f) * TOWER_K;
            }
            // the orb rises out of the cap
            bool orb = OrbOn(t);
            float rise = OrbRise(t);
            if (orbT.gameObject.activeSelf != orb)
            {
                orbT.gameObject.SetActive(orb);
                glowQuad.gameObject.SetActive(orb);
            }
            Vector3 oc = new Vector3(0f, topY + 8f + 22f * rise, 0f);
            float orad = 9f * Mathf.Clamp01(Sm(111f, 114f, t) + 0.0f) * (1f - Sm(296f, 298f, t)) * (1f + 0.04f * pulse);
            if (orb)
            {
                orbT.localPosition = oc;
                orbT.localScale = Vector3.one * (2f * Mathf.Max(0.01f, orad));
                orbT.localRotation = Quaternion.Euler(t * 9f, t * 14f, t * 5f);
                glowMat.color = new Color(1f, 0.75f, 0.85f, 0.45f + 0.35f * pulse + 0.2f * flashV);
                var cam = Camera.main;
                glowQuad.localPosition = oc;
                glowQuad.localScale = Vector3.one * (70f * (0.7f + 0.3f * pulse));
                if (cam != null)
                    glowQuad.rotation = Quaternion.LookRotation(glowQuad.position - cam.transform.position, Vector3.up);
                float act = Mathf.Clamp01(OrbBolts(t) / 3f + 0.3f);
                orbLight.transform.localPosition = oc;
                orbLight.intensity = Lit * (2.5f + 5f * act + 4f * pulse * act + 5f * flashV);
                orbMat.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.6f), Orange(t) * (1f - Sm(256f, 262f, t)));
            }
            else
                orbLight.intensity = 0f;
            if (orbMidas != null && orbMidas.Pivot != null)
            {
                bool on = orb && orad > 1f;
                if (orbMidas.Pivot.activeSelf != on)
                    orbMidas.Pivot.SetActive(on);
                if (on)
                {
                    PlayMidas(t);
                    float k = 10f * orad / 9f / Mathf.Max(0.01f, orbMidas.RendererBounds.size.y);
                    var mp = orbMidas.Pivot.transform;
                    mp.localScale = Vector3.one * k;
                    mp.localPosition = oc + new Vector3(0f, -4.6f * orad / 9f, 0f);
                    var cam = Camera.main;
                    float toCam = 0f;
                    if (cam != null)
                    {
                        var d = root.transform.InverseTransformPoint(cam.transform.position) - oc;
                        toCam = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    }
                    mp.localRotation = Quaternion.Euler(0f, toCam + midasFace, 0f);
                }
            }
            // pylons
            float uns = Unstable(t);
            for (int i = 0; i < 5; i++)
            {
                if (pylon[i] == null)
                    continue;
                float pe = PylonE(i, t);
                bool on = pe > 0.001f;
                if (pylon[i].gameObject.activeSelf != on)
                    pylon[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                Vector3 b = PylonBase(i);
                pylon[i].localPosition = b + Vector3.down * (PYL_H * 1.05f * (1f - pe));
                pylon[i].localRotation = Quaternion.Euler(
                    uns * 2.2f * Mathf.Sin(t * 3.1f + i),
                    0f,
                    uns * 2.2f * Mathf.Cos(t * 2.7f + i * 2f)
                );
            }
        }

        Vector3 PylonTip(int i, float t)
        {
            return PylonBase(i) + Vector3.down * (PYL_H * 1.05f * (1f - PylonE(i, t))) + Vector3.up * (PYL_H * 0.98f);
        }

        Vector3 OrbCentre(float t)
        {
            float e = TowerE(t);
            float dy = DeviceY(t);
            return new Vector3(0f, gy + 3f + (deviceT != null ? dy : 0f) + (e * 72f + 8f) * TOWER_K + 8f + 22f * OrbRise(t), 0f);
        }

        // the storm and the water storm
        void StormStep(float t)
        {
            float a = StormA(t);
            bool on = a > 0.003f;
            if (stormG.activeSelf != on)
                stormG.SetActive(on);
            if (on)
            {
                float r = StormR(t);
                stormG.transform.localPosition = new Vector3(0f, gy - 10f, 0f);
                stormG.transform.localScale = new Vector3(r, 1f, r);
                float o = Orange(t),
                    flick = 1f - 0.3f * Unstable(t) * (0.5f + 0.5f * Mathf.Sin(t * 13f) * Mathf.Sin(t * 5.3f));
                var c = Color.Lerp(new Color(0.65f, 0.4f, 1f), new Color(1f, 0.55f, 0.2f), o);
                stormMat.color = new Color(c.r, c.g, c.b, 0.85f * a * flick);
            }
            float wa = WaterA(t);
            bool won = wa > 0.003f;
            if (waterG.activeSelf != won)
            {
                waterG.SetActive(won);
                seaG.SetActive(won);
                discG.SetActive(won);
            }
            if (won)
            {
                float r = WaterR(t);
                waterG.transform.localPosition = new Vector3(0f, gy - 10f, 0f);
                waterG.transform.localScale = new Vector3(r, 1f, r);
                float wl = WaterLevel(t);
                seaG.transform.localPosition = new Vector3(0f, gy + wl, 0f);
                seaG.transform.localScale = new Vector3(r, 1f, r);
                discG.transform.localPosition = new Vector3(0f, gy + wl, 0f);
                discG.transform.localScale = new Vector3(r, 1f, r);
                surfMat.color = new Color(0.6f, 0.9f, 1f, 0.42f * wa);
                waterMat.color = new Color(0.7f, 0.95f, 1f, 0.8f * wa);
            }
            if (on || won)
                ScrollUv(stormMesh, t * 0.05f, t * 0.01f);
        }

        float uvT = -9f;

        void ScrollUv(Mesh m, float du, float dv)
        {
            if (Time.unscaledTime - uvT < 0.08f)
                return;
            uvT = Time.unscaledTime;
            for (int i = 0; i < stormUv.Length; i++)
                stormUv[i] = new Vector2(stormUv0[i].x + du, stormUv0[i].y + dv);
            m.uv = stormUv;
        }

        // lightning
        float BoltRnd(int a, int b, float tq)
        {
            return WorldKit.Hash(a, b, (int)(tq * 18f));
        }

        void ClearBolts()
        {
            for (int i = 0; i < boltN; i++)
            {
                bolts[i].Glow.enabled = false;
                bolts[i].Core.enabled = false;
            }
            boltN = 0;
        }

        void AddBolt(Vector3 a, Vector3 b, Color c, float width, float t, int seed, bool fork = true)
        {
            if (boltN >= bolts.Length)
                return;
            var bo = bolts[boltN++];
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 0.1f)
                return;
            var side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;
            side.Normalize();
            var side2 = Vector3.Cross(dir.normalized, side);
            int n = bo.Glow.positionCount;
            float amp = Mathf.Min(len * 0.07f, 9f);
            for (int k = 0; k < n; k++)
            {
                float f = k / (float)(n - 1),
                    env = Mathf.Sin(f * Mathf.PI);
                var p =
                    Vector3.Lerp(a, b, f)
                    + (side * (BoltRnd(seed, k, t) - 0.5f) + side2 * (BoltRnd(seed, k + 50, t) - 0.5f)) * 2f * amp * env;
                var wp = W(p);
                bo.Glow.SetPosition(k, wp);
                bo.Core.SetPosition(k, wp);
            }
            bo.Glow.enabled = true;
            bo.Core.enabled = true;
            bo.Glow.widthMultiplier = width * 2.6f * sc;
            bo.Core.widthMultiplier = width * 0.7f * sc;
            bo.Glow.startColor = bo.Glow.endColor = new Color(c.r, c.g, c.b, 0.75f);
            bo.Core.startColor = bo.Core.endColor = new Color(1f, 1f, 1f, 0.95f);
            if (fork && width > 0.9f && boltN < bolts.Length)
            {
                var mid = Vector3.Lerp(a, b, 0.35f + 0.3f * BoltRnd(seed, 5, t));
                var end =
                    mid
                    + (side * (BoltRnd(seed, 6, t) - 0.5f) + side2 * (BoltRnd(seed, 7, t) - 0.5f)) * len * 0.5f
                    + dir.normalized * len * 0.08f;
                AddBolt(mid, end, c, width * 0.45f, t, seed + 999, false);
            }
        }

        void BoltStep(float t, float pulse, float en)
        {
            ClearBolts();
            impactN = 0;
            int n = OrbBolts(t);
            bool toStorm = StormBolts(t);
            float fade = t >= 288f ? 1f - Sm(288f, 294f, t) : 1f;
            Color pink = Color.Lerp(new Color(1f, 0.5f, 0.85f), new Color(1f, 0.7f, 0.35f), Orange(t) * (1f - Sm(256f, 262f, t))),
                cyan = new Color(0.5f, 0.85f, 1f);
            if (t >= 218f && t < 222f)
            {
                for (int k = 0; k < 3; k++)
                    AddBolt(
                        new Vector3(Mathf.Cos(k * 2.1f) * 90f, gy + 90f, Mathf.Sin(k * 2.1f) * 90f),
                        OrbCentre(t),
                        cyan,
                        1.2f,
                        t,
                        40 + k
                    );
            } // 3:38 the lightning hits the orb again
            for (int k = 0; k < n; k++)
            {
                int i = PYL_ORDER[k];
                if (PylonE(i, t) < 0.5f)
                    continue;
                Vector3 tip = PylonTip(i, t);
                AddBolt(tip, OrbCentre(t), (k & 1) == 0 ? pink : cyan, (1.2f + 0.8f * pulse) * fade, t, i * 7 + 1);
                if (toStorm && (t < 193f || t >= 231f))
                {
                    float a = PylonAng(i),
                        r = StormR(t);
                    var tg = new Vector3(Mathf.Cos(a) * r, gy + 80f + 40f * Mathf.Sin(t * 1.3f + i), Mathf.Sin(a) * r);
                    AddBolt(tip, tg, pink, 1.5f, t, i * 7 + 3);
                    if (impactN < impactPts.Length)
                        impactPts[impactN++] = tg;
                    if (i % 2 == 0)
                        AddBolt(tip, new Vector3(Mathf.Cos(a + 0.5f) * r, gy + 150f, Mathf.Sin(a + 0.5f) * r), cyan, 0.9f, t, i * 7 + 5);
                }
            }
            if (OrbOn(t))
            {
                float orad = 9f * Sm(111f, 114f, t) * (1f - Sm(296f, 298f, t));
                var oc = OrbCentre(t);
                int pk = 2 + (int)(3f * Mathf.Clamp01(n / 5f));
                for (int k = 0; k < pk; k++)
                    AddBolt(
                        oc,
                        oc
                            + new Vector3(
                                BoltRnd(300 + k, 1, t) - 0.5f,
                                BoltRnd(300 + k, 2, t) - 0.5f,
                                BoltRnd(300 + k, 3, t) - 0.5f
                            ).normalized * orad,
                        pink,
                        0.7f,
                        t,
                        300 + k,
                        false
                    );
            } // the plasma inside the orb
        }

        void Atmosphere(float t, float en)
        {
            if (curMode == 1)
            {
                RenderSettings.ambientLight = new Color(0.62f, 0.6f, 0.56f);
                sun.intensity = 0f;
                return;
            }
            float orange = Orange(t) * (1f - Sm(256f, 262f, t)),
                calm = Sm(256f, 270f, t);
            var amb = Color.Lerp(
                Color.Lerp(new Color(0.28f, 0.22f, 0.5f), new Color(0.5f, 0.28f, 0.2f), orange),
                new Color(0.38f, 0.45f, 0.65f),
                calm
            );
            RenderSettings.ambientLight = amb * (0.85f + 0.4f * flashV);
            sun.color = Color.Lerp(new Color(0.65f, 0.55f, 1f), new Color(1f, 0.6f, 0.35f), orange);
            sun.intensity = 0.7f * Mathf.Max(0.4f, Lit);
            sun.transform.rotation = Quaternion.Euler(52f, yaw + 30f, 0f);
            flashV = Mathf.Max(0f, flashV - Time.deltaTime * 1.8f);
        }

        void Rumble(float t, float dt, float pulse)
        {
            if (dt <= 0f || paused)
                return;
            rumbleT += dt;
            if (rumbleT < 0.12f)
                return;
            rumbleT = 0f;
            float p = Mathf.Max(
                Unstable(t) * 0.45f,
                (t >= 71f && t < 80f) ? 0.9f : 0f,
                (t >= 55f && t < 71f) ? 0.1f + 0.5f * Sm(55f, 71f, t) : 0f,
                (t >= 85f && t < 107f) ? 0.35f : 0f,
                (t >= 288f && t < 299f) ? 0.5f : 0f,
                boltN > 4 ? 0.12f * pulse : 0f
            );
            if (p < 0.05f)
                return;
            try
            {
                var gt = GorillaTagger.Instance;
                if (gt != null)
                {
                    gt.StartVibration(true, Mathf.Clamp01(p), 0.14f);
                    gt.StartVibration(false, Mathf.Clamp01(p), 0.14f);
                }
            }
            catch { }
        }

        void OfficeStep(float t)
        {
            PlayJones(t);
            Atmosphere(t, 0f);
            if (officeFlash != null)
            {
                float f =
                    Mathf.Pow(Mathf.Clamp01(Mathf.Sin(t * 2.3f) * Mathf.Sin(t * 5.1f + 1f)), 6f) * 2f
                    + ((t * 0.31f) % 1f < 0.05f ? 4f : 0f);
                officeFlash.intensity = (0.5f + f) * Mathf.Max(0.5f, Lit);
            }
            if (officeGlow != null && officeGlow.Length >= 2)
            {
                float sf = 0.82f + 0.18f * Mathf.Sin(t * 23f) * Mathf.Sin(t * 3.1f);
                if (officeGlow[0] != null)
                    officeGlow[0].color = new Color(sf, sf, sf, 1f);
                float wf = 0.75f + 0.25f * Mathf.Sin(t * 0.7f) + ((t * 0.31f) % 1f < 0.05f ? 0.6f : 0f);
                if (officeGlow[1] != null)
                    officeGlow[1].color = new Color(wf, wf, wf, 1f);
            } // glow slots: 0 CRT screen, 1 the storm behind the blinds (then ceiling panels, lamp, blinds)
        }

        // ------------------------------------------------------------------ extra detail: orb halos + beam, pylon tip glows, ground rings, energy rings down the tower, floating rocks, storm impact glows, Agency alarm beacons, rain
        LineRenderer MakeLoop(string name, Material mat, int n, bool loop)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            var l = g.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.positionCount = n;
            l.loop = loop;
            l.sharedMaterial = mat;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.enabled = false;
            return l;
        }

        Transform GlowQuad(string name, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(root.transform, false);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            q.SetActive(false);
            return q.transform;
        }

        void SetLoop(LineRenderer l, Vector3 c, float r, Quaternion q)
        {
            int n = l.positionCount;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                l.SetPosition(k, W(c + q * new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)));
            }
        }

        void Face(Transform q)
        {
            var cam = Camera.main;
            if (cam != null)
                q.rotation = Quaternion.LookRotation(q.position - cam.transform.position, Vector3.up);
        }

        LineRenderer[] halos = new LineRenderer[3],
            baseRings = new LineRenderer[6],
            towerRings = new LineRenderer[3];
        LineRenderer beam;
        Transform[] tipGlow = new Transform[5],
            impact = new Transform[6],
            alarm = new Transform[4],
            rocks = new Transform[28];
        Vector3[] impactPts = new Vector3[6],
            rockBase = new Vector3[28];
        float[] rockH = new float[28];
        int impactN;
        Material tipMat,
            impactMat,
            alarmMat,
            surfMat;
        Light alarmLight,
            officeFlash;
        GameObject discG;
        Mesh discMesh;
        float accRain,
            accDust2;

        void BuildExtras()
        {
            var soft = kit.Glow(Color.white, lineTex, 3001);
            for (int i = 0; i < halos.Length; i++)
                halos[i] = MakeLoop("OrbHalo" + i, soft, 72, true);
            for (int i = 0; i < baseRings.Length; i++)
                baseRings[i] = MakeLoop("BaseRing" + i, soft, 48, true);
            for (int i = 0; i < towerRings.Length; i++)
                towerRings[i] = MakeLoop("TowerRing" + i, soft, 48, true);
            beam = MakeLoop("OrbBeam", soft, 2, false);
            tipMat = kit.Glow(new Color(1f, 0.75f, 0.9f, 0.9f), glowTex, 3003);
            impactMat = kit.Glow(new Color(1f, 0.7f, 0.95f, 0.85f), glowTex, 3003);
            alarmMat = kit.Glow(new Color(1f, 0.15f, 0.1f, 0.9f), glowTex, 3003);
            for (int i = 0; i < tipGlow.Length; i++)
                tipGlow[i] = GlowQuad("TipGlow" + i, tipMat);
            for (int i = 0; i < impact.Length; i++)
                impact[i] = GlowQuad("Impact" + i, impactMat);
            for (int i = 0; i < alarm.Length; i++)
                alarm[i] = GlowQuad("Alarm" + i, alarmMat);
            alarmLight = new GameObject("AlarmLight").AddComponent<Light>();
            alarmLight.transform.SetParent(root.transform, false);
            alarmLight.type = LightType.Point;
            alarmLight.color = new Color(1f, 0.15f, 0.1f);
            alarmLight.range = 80f * sc + 20f;
            alarmLight.intensity = 0f;
            alarmLight.shadows = LightShadows.None;
            alarmLight.transform.localPosition = new Vector3(0f, gy + 22f, 0f);
            var rm = kit.Lit(new Color(0.30f, 0.27f, 0.30f), null, 0.1f);
            for (int i = 0; i < rocks.Length; i++)
            {
                float ang = WorldKit.Hash(i, 1, 5) * 6.2832f,
                    rad = 14f + WorldKit.Hash(i, 2, 5) * 46f,
                    s = 1.4f + WorldKit.Hash(i, 3, 5) * 3.6f;
                rockH[i] = WorldKit.Hash(i, 4, 5);
                rockBase[i] = new Vector3(Mathf.Cos(ang) * rad, gy + 0.5f, Mathf.Sin(ang) * rad);
                var o = kit.Obj(
                    "FloatRock" + i,
                    root.transform,
                    kit.Blob(1f, 0.7f, 0.9f, 11 + i, 0.3f, 8, 12),
                    rm,
                    rockBase[i],
                    Quaternion.identity,
                    Vector3.one * s,
                    false
                ).transform;
                o.gameObject.SetActive(false);
                rocks[i] = o;
            }
            // the flooded island: a flat disc inside the water wall (the sea outside it is the annulus)
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tr = new List<int>();
            v.Add(Vector3.zero);
            uv.Add(Vector2.zero);
            int seg = 48;
            for (int j = 0; j <= seg; j++)
            {
                float a = j * Mathf.PI * 2f / seg;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                uv.Add(new Vector2(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 2f));
            }
            for (int j = 1; j <= seg; j++)
            {
                tr.Add(0);
                tr.Add(j + 1);
                tr.Add(j);
            }
            discMesh = new Mesh();
            discMesh.SetVertices(v);
            discMesh.SetUVs(0, uv);
            discMesh.SetTriangles(tr, 0);
            discMesh.RecalculateBounds();
            kit.Keep(discMesh);
            surfMat = kit.Glow(Color.white, waterMat.mainTexture as Texture2D, 2994);
            discG = new GameObject("WaterSurface");
            discG.transform.SetParent(root.transform, false);
            discG.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var mr = discG.AddComponent<MeshRenderer>();
            mr.sharedMaterial = surfMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            discG.SetActive(false);
        }

        float WaterLevel(float t)
        {
            return (0.15f + 1.45f * Sm(340f, 398f, t)) / sc;
        } // the flood is 1.6 m deep (real metres) at the end: deep enough to swim in

        void SwimStep(float t)
        {
            if (paused)
                return;
            bool want = false;
            var hp = Head();
            if (hp.HasValue && WaterA(t) > 0.3f)
            {
                float sy = W(new Vector3(0f, gy + WaterLevel(t), 0f)).y;
                var c = W(Vector3.zero);
                float dx = hp.Value.x - c.x,
                    dz = hp.Value.z - c.z;
                want = hp.Value.y < sy && Mathf.Sqrt(dx * dx + dz * dz) < WaterR(t) * sc * 1.2f;
            }
            AstronomicalHazards.Swim(want, W(Vector3.zero));
        }

        void Extras(float t, float dt, float pulse)
        {
            bool orb = OrbOn(t);
            float act = orb ? Mathf.Clamp01(OrbBolts(t) / 5f) : 0f;
            Vector3 oc = orb ? OrbCentre(t) : Vector3.zero;
            float e = TowerE(t);
            for (int i = 0; i < halos.Length; i++)
            {
                var h = halos[i];
                bool on = orb && Sm(111f, 116f, t) > 0.05f;
                if (h.enabled != on)
                    h.enabled = on;
                if (!on)
                    continue;
                var q = Quaternion.Euler(
                    70f * Mathf.Sin(t * 0.35f + i * 2.1f),
                    t * (22f + 14f * i) * (i % 2 == 0 ? 1f : -1f),
                    35f * Mathf.Cos(t * 0.3f + i)
                );
                SetLoop(h, oc, (15f + 6f * i) * (1f + 0.05f * pulse), q);
                var c =
                    i == 0 ? new Color(1f, 0.6f, 0.85f)
                    : i == 1 ? new Color(0.6f, 0.85f, 1f)
                    : Color.white;
                h.startColor = h.endColor = new Color(c.r, c.g, c.b, (0.25f + 0.55f * act) * (1f - Sm(294f, 298f, t)));
                h.widthMultiplier = (0.5f + 0.5f * pulse) * sc;
            }
            bool bon = orb && act > 0.01f;
            if (beam.enabled != bon)
                beam.enabled = bon;
            if (bon)
            {
                beam.SetPosition(0, W(oc));
                beam.SetPosition(1, W(oc + Vector3.up * 450f));
                beam.widthMultiplier = (5f + 4f * act + 3f * pulse) * sc;
                beam.startColor = beam.endColor = new Color(1f, 0.8f, 0.95f, 0.22f + 0.2f * act);
            }
            for (int i = 0; i < tipGlow.Length; i++)
            {
                bool on = PylonE(i, t) > 0.5f;
                if (tipGlow[i].gameObject.activeSelf != on)
                    tipGlow[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                bool live = Array.IndexOf(PYL_ORDER, i) < OrbBolts(t);
                tipGlow[i].position = W(PylonTip(i, t));
                tipGlow[i].localScale = Vector3.one * ((live ? 16f + 8f * pulse : 6f) * sc);
                Face(tipGlow[i]);
            }
            for (int i = 0; i < baseRings.Length; i++)
            {
                var r = baseRings[i];
                bool up = i < 5 ? PylonE(i, t) > 0.5f : DeviceY(t) > -8f;
                bool on = up && act > 0.01f;
                if (r.enabled != on)
                    r.enabled = on;
                if (!on)
                    continue;
                SetLoop(
                    r,
                    (i < 5 ? PylonBase(i) : new Vector3(0f, gy, 0f)) + Vector3.up * 0.4f,
                    (i < 5 ? 6f : 13f) * (1f + 0.15f * pulse),
                    Quaternion.identity
                );
                var c = i % 2 == 0 ? new Color(1f, 0.6f, 0.85f) : new Color(0.6f, 0.85f, 1f);
                r.startColor = r.endColor = new Color(c.r, c.g, c.b, 0.8f * act);
                r.widthMultiplier = 1.1f * sc;
            }
            float tower = e * 72f * TOWER_K;
            for (int k = 0; k < towerRings.Length; k++)
            {
                var r = towerRings[k];
                bool on = orb && e > 0.5f && act > 0.01f;
                if (r.enabled != on)
                    r.enabled = on;
                if (!on)
                    continue;
                float f = ((t * 0.5f + k / 3f) % 1f);
                float y = gy + 3f + tower * (1f - f);
                SetLoop(r, new Vector3(0f, y, 0f), 4.2f * TOWER_K + 0.6f, Quaternion.identity);
                r.startColor = r.endColor = new Color(1f, 0.65f, 0.9f, 0.85f * act * Mathf.Sin(f * Mathf.PI));
                r.widthMultiplier = 0.9f * sc;
            }
            float lift = Sm(195f, 215f, t) * (1f - Sm(256f, 262f, t));
            bool ron = lift > 0.001f;
            for (int i = 0; i < rocks.Length; i++)
            {
                if (rocks[i].gameObject.activeSelf != ron)
                    rocks[i].gameObject.SetActive(ron);
                if (!ron)
                    continue;
                rocks[i].localPosition = rockBase[i] + Vector3.up * (lift * (7f + 18f * rockH[i]) + 1.2f * lift * Mathf.Sin(t * 0.8f + i));
                rocks[i].localRotation = Quaternion.Euler(t * 9f * (rockH[i] - 0.4f), t * 14f * rockH[i], t * 6f * (0.5f - rockH[i]));
            }
            for (int i = 0; i < impact.Length; i++)
            {
                bool on = i < impactN;
                if (impact[i].gameObject.activeSelf != on)
                    impact[i].gameObject.SetActive(on);
                if (!on)
                    continue;
                impact[i].position = W(impactPts[i]);
                impact[i].localScale = Vector3.one * ((26f + 12f * pulse) * sc);
                Face(impact[i]);
            }
            bool al = t >= 30f && t < 71f && agencyT != null;
            float fl = al ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 6f)), 2f) : 0f;
            alarmLight.intensity = Lit * 4f * fl;
            for (int i = 0; i < alarm.Length; i++)
            {
                if (alarm[i].gameObject.activeSelf != al)
                    alarm[i].gameObject.SetActive(al);
                if (!al)
                    continue;
                alarm[i].localPosition = new Vector3((i & 1) == 0 ? -12f : 12f, gy + 20.5f, (i & 2) == 0 ? -7f : 7f);
                alarm[i].localScale = Vector3.one * ((6f + 10f * fl) * sc);
                Face(alarm[i]);
            }
            if (paused || dt <= 0f)
                return;
            if (t >= 55f && t < 71f)
                for (int k = Take(ref accDust2, 18f * Parts * Sm(55f, 71f, t), dt); k > 0; k--)
                    Emit(
                        smoke,
                        new Vector3(Rnd(-22f, 22f), gy + 1f, Rnd(-18f, 18f)),
                        new Vector3(Rnd(-1f, 1f), Rnd(3f, 7f), Rnd(-1f, 1f)),
                        new Color(0.4f, 0.38f, 0.36f, 0.5f),
                        Rnd(5f, 9f),
                        Rnd(5f, 7f)
                    );
            if (t >= 330f && t < 400f)
                for (int k = Take(ref accRain, 260f * Parts, dt); k > 0; k--)
                    Emit(
                        fx,
                        new Vector3(Rnd(-70f, 70f), gy + Rnd(25f, 45f), Rnd(-70f, 70f)),
                        new Vector3(Rnd(-3f, 3f), -Rnd(55f, 70f), Rnd(-3f, 3f)),
                        new Color(0.75f, 0.9f, 1f, 0.5f),
                        0.35f,
                        0.55f
                    );
        }

        public string DebugInfo()
        {
            return "t "
                + lastT.ToString("0.0")
                + "  mode "
                + curMode
                + "  scale "
                + sc.ToString("0.00")
                + "  storm R "
                + (lastT >= 0f ? StormR(lastT).ToString("0") : "-")
                + "  bolts "
                + boltN
                + "  orb "
                + (lastT >= 0f ? OrbRise(lastT).ToString("0.00") : "-")
                + "  forest "
                + (AstronomicalForestHider.Hidden ? "unloaded" : "on");
        }

        // ------------------------------------------------------------------ cleanup
        public void Cleanup()
        {
            AstronomicalHazards.Reset();
            try
            {
                ExitOffice();
            }
            catch { }
            if (ambientSaved)
            {
                RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z);
                ambientSaved = false;
            }
            try
            {
                foreach (var m in models)
                    if (m != null && m.Pivot != null)
                    {
                        m.Pivot.transform.SetParent(null, false);
                        m.Pivot.SetActive(false);
                        AstronomicalEventMod.ParkModel(m.Pivot);
                    }
            }
            catch { }
            foreach (var c in clones)
                if (c != null)
                    UnityEngine.Object.Destroy(c);
            clones.Clear();
            models.Clear();
            chunks.Clear();
            if (flashSphere != null)
                UnityEngine.Object.Destroy(flashSphere);
            flashSphere = null;
            if (office != null)
                UnityEngine.Object.Destroy(office);
            office = null;
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
            prevT = -1f;
            curMode = -1;
            skyMesh = null;
            fx = smoke = null;
            agency = deviceM = towerM = orbMidas = jonesM = null;
            decoT = agencyT = ruinsT = rubbleT = deviceT = towerP = orbT = null;
            Array.Clear(halos, 0, halos.Length);
            Array.Clear(baseRings, 0, baseRings.Length);
            Array.Clear(towerRings, 0, towerRings.Length);
            Array.Clear(tipGlow, 0, tipGlow.Length);
            Array.Clear(impact, 0, impact.Length);
            Array.Clear(alarm, 0, alarm.Length);
            Array.Clear(rocks, 0, rocks.Length);
            beam = null;
            discG = null;
            officeFlash = null;
            alarmLight = null;
            Array.Clear(pylon, 0, 5);
            Array.Clear(seg, 0, 6);
            Array.Clear(bolts, 0, bolts.Length);
            Array.Clear(rings, 0, rings.Length);
            boltN = 0;
            cap = null;
            midasClip = jonesClip = null;
            officeM = null;
            officeGlow = null;
            midasCalm.Clear();
            midasHot.Clear();
            jonesClips.Clear();
        }
    }
}
