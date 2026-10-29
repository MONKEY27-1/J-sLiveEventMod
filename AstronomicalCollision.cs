using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Plays a glTF clip on a CLONE of a loaded model (the loader's Sample only drives the original): the channels are re-targeted by hierarchy path.
    internal sealed class CRig
    {
        public GameObject Go;
        public GltfModel M;
        readonly Dictionary<GltfModel.Clip, Transform[]> maps = new Dictionary<GltfModel.Clip, Transform[]>();

        public static CRig Of(GltfModel m, GameObject go)
        {
            return new CRig { M = m, Go = go };
        }

        Dictionary<Transform, Transform> pair;

        // a clone has the same hierarchy in the same order as the original: pair them by position in that order (names are not unique, so name paths can pick the wrong bone)
        Transform[] Map(GltfModel.Clip c)
        {
            Transform[] arr;
            if (maps.TryGetValue(c, out arr))
                return arr;
            arr = new Transform[c.Channels.Count];
            if (pair == null && Go != M.Pivot)
            {
                pair = new Dictionary<Transform, Transform>();
                var o = M.Pivot.GetComponentsInChildren<Transform>(true);
                var cl = Go.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < o.Length && i < cl.Length; i++)
                    pair[o[i]] = cl[i];
            }
            for (int k = 0; k < arr.Length; k++)
            {
                var ch = c.Channels[k];
                if (Go == M.Pivot)
                    arr[k] = ch.T;
                else
                {
                    Transform t;
                    arr[k] = pair.TryGetValue(ch.T, out t) ? t : null;
                }
            }
            maps[c] = arr;
            return arr;
        }

        static void Eval(GltfModel.Channel ch, float t, out Quaternion q, out Vector3 v)
        {
            q = Quaternion.identity;
            v = Vector3.zero;
            var ts = ch.Times;
            int n = ts.Length;
            if (n == 0)
                return;
            int i = Mathf.Clamp(ch.Hint, 0, n - 1);
            while (i > 0 && ts[i] > t)
                i--;
            while (i < n - 1 && ts[i + 1] <= t)
                i++;
            ch.Hint = i;
            int j = Mathf.Min(i + 1, n - 1);
            float f = j == i ? 0f : Mathf.Clamp01((t - ts[i]) / (ts[j] - ts[i]));
            var a = ch.Vals;
            if (ch.Path == 1)
                q = Quaternion.Slerp(
                    new Quaternion(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]),
                    new Quaternion(a[j * 4], a[j * 4 + 1], a[j * 4 + 2], a[j * 4 + 3]),
                    f
                );
            else
                v = Vector3.Lerp(new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]), new Vector3(a[j * 3], a[j * 3 + 1], a[j * 3 + 2]), f);
        }

        public void Pose(GltfModel.Clip a, float ta, GltfModel.Clip b = null, float tb = 0f, float w = 0f)
        {
            if (a == null || a.Duration <= 0f)
                return;
            ta = Mathf.Repeat(ta, a.Duration);
            var ma = Map(a);
            Transform[] mb = null;
            if (b != null && b.Duration > 0f && b.Channels.Count == a.Channels.Count && w > 0.001f)
            {
                tb = Mathf.Repeat(tb, b.Duration);
                mb = Map(b);
            }
            for (int k = 0; k < a.Channels.Count; k++)
            {
                var ca = a.Channels[k];
                var tr = ma[k];
                if (tr == null)
                    continue;
                Quaternion qa,
                    qb;
                Vector3 va,
                    vb;
                Eval(ca, ta, out qa, out va);
                if (mb != null)
                {
                    Eval(b.Channels[k], tb, out qb, out vb);
                    qa = Quaternion.Slerp(qa, qb, w);
                    va = Vector3.Lerp(va, vb, w);
                }
                if (ca.Path == 1)
                    tr.localRotation = qa;
                else if (ca.Path == 0)
                    tr.localPosition = va;
                else
                    tr.localScale = va;
            }
        }
    }

    // "Collision" (Fortnite Chapter 3 Season 2 finale, June 4 2022) - MILESTONE 1 (0:00 - 8:00), version 2. Same Gorilla Tag map: it stays visible and IS the island. You ride in the giant mech's head (a fully enclosed Blender
    // cabin on the mech's head bone; you are carried with it, gravity cancelled, the cabin has colliders). Moon base (0:00 - 1:35, The Origin speaks), launch at 1:35, flight down 1:55 - 3:11 (asteroids, re-entry), landing,
    // the tanks under their orange barriers, B.R.U.T.E.s, the walk across the map with the barrage, the weapon that powers up at 4:08 and hits at 4:11, the animated Collider reached at 7:45, the sword, the fall through the ground at
    // 7:57 and black at 8:00. Every model is a Blender model with clips (mech 18, B.R.U.T.E. 8, tanks, domes, asteroids, the Collider's ring / beam, the cabin's joysticks / radar, the moon base dish).
    // Knockback: tank / brute explosions, the weapon hit, the landing, the collapse. Everything is a pure function of the music time T except one-shot bursts.
    internal sealed partial class AstronomicalCollision : IEventScene, IDebug
    {
        const float T_LAUNCH = 95f,
            T_ENTRY = 115f,
            T_LAND = 191f,
            T_CHARGE = 248f,
            T_HIT = 251f,
            T_SWORD = 445f,
            T_ARRIVE = 465f,
            T_SLASH = 470f,
            T_FALL = 477f,
            T_BLACK = 480f;
        const float MECH_S = 37.5f,
            PAD_Y = 700f,
            COLLIDER_Z = 330f,
            COLLIDER_S = 1.3f,
            LAND_Z = 20f;

        static float Sm(float a, float b, float x)
        {
            return AstronomicalCube.Sm(a, b, x);
        }

        // where the mech walks: (start, end, z0, z1); it stands still between the legs of the walk (firing)
        static readonly float[][] WALK =
        {
            new[] { 215f, 250f, 20f, 70f },
            new[] { 266f, 322f, 70f, 130f },
            new[] { 336f, 392f, 130f, 180f },
            new[] { 402f, 450f, 180f, 225f },
            new[] { 452f, 465f, 225f, 240f },
        };

        static float MechZ(float t)
        {
            float z = LAND_Z;
            foreach (var w in WALK)
            {
                if (t >= w[1])
                    z = w[3];
                else if (t > w[0])
                    return Mathf.Lerp(w[2], w[3], Sm(w[0], w[1], t));
                else
                    break;
            }
            return z;
        }

        static bool Walking(float t)
        {
            foreach (var w in WALK)
                if (t > w[0] && t < w[1])
                    return true;
            return false;
        }

        // mech feet position (local) and pitch (deg, nose down positive)
        static Vector3 MechPos(float t, out float pitch)
        {
            pitch = 0f;
            if (t < T_LAUNCH)
                return new Vector3(0f, PAD_Y, -10f);
            if (t < T_ENTRY)
            {
                float k = Sm(T_LAUNCH, T_ENTRY, t);
                pitch = 25f * k;
                return new Vector3(0f, PAD_Y + 160f * k * k, -10f + 140f * k);
            }
            if (t < T_LAND)
            {
                float k = Mathf.Pow(Sm(T_ENTRY, T_LAND, t), 1.25f);
                pitch = Mathf.Lerp(25f, 4f, k);
                Vector3 a = new Vector3(0f, PAD_Y + 160f, 130f),
                    b = new Vector3(0f, 0f, LAND_Z);
                return Vector3.Lerp(a, b, k) + new Vector3(30f * Mathf.Sin(k * Mathf.PI), 0f, 0f);
            }
            Vector3 p = new Vector3(0f, 0f, MechZ(t));
            if (t >= T_FALL)
            {
                float f = t - T_FALL;
                p.y -= 0.5f * 60f * f * f;
                pitch = 8f * Mathf.Min(1f, f);
            }
            return p;
        }

        AstronomicalEventDef def;
        GameObject root,
            dome,
            flashGo;
        WorldKit kit;
        GltfModel mechM,
            cabM,
            moonM,
            colM,
            tankM,
            domeM,
            bruteM,
            swordM,
            originM,
            rockM,
            islandM;
        CRig mechR,
            cabR,
            moonR,
            colR,
            originR;
        Transform headT,
            handRT,
            handLT;
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        sealed class Seg
        {
            public float T;
            public string Clip;
            public float Speed,
                Off;
            public bool Loop;
        }

        readonly List<Seg> mechSegs = new List<Seg>();

        sealed class Actor
        {
            public GameObject Go;
            public CRig Rig;
            public Vector3 P;
            public float Die;
            public float Off;
            public bool Dead,
                Boomed;
            public int Kind;
            public float Scale;
        }

        readonly List<Actor> tanks = new List<Actor>(),
            brutes = new List<Actor>(),
            rocks = new List<Actor>(),
            domes = new List<Actor>();
        ParticleSystem fx,
            dust,
            stars;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        int skyN;
        float skyT = -9f;
        Texture2D glowTex,
            lineTex,
            beamTex;
        Material flashMat,
            chargeMat;
        Light sun;
        Transform cannonGlow;
        LineRenderer bolt,
            boltCore,
            tracer,
            tracerCore;
        Light flashLamp,
            engineLamp;
        Vector3 originW,
            holdOff,
            cabPrev;
        bool held,
            cabHave,
            built,
            paused,
            hitDone,
            landDone,
            fallDone,
            launchDone;
        float yaw,
            lastT = -1f,
            endT = 480f,
            shake,
            rumbleT,
            sparkAcc,
            tracerT = -9f,
            farSave = -1f;
        Vector3 tracerTo,
            homePos;
        Color ambientSave;
        bool ambientSaved;
        int floorLayer;
        Quaternion cabRot = Quaternion.identity;
        float Parts
        {
            get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); }
        }
        float Lit
        {
            get { return AstronomicalSettings.Lighting.Value; }
        }

        Vector3 W(Vector3 l)
        {
            return root.transform.TransformPoint(l);
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

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected;
            built = false;
            endT = def != null && def.Length > 5f ? def.Length : 480f;
            tanks.Clear();
            brutes.Clear();
            rocks.Clear();
            domes.Clear();
            mechSegs.Clear();
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
            Vector3 org = head;
            try
            {
                org = AstronomicalCountdown.FindGazeboBase(head);
            }
            catch { }
            originW = org;
            if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, m, QueryTriggerInteraction.Ignore))
                originW = h.point;
            else
                originW.y = head.y - 1.2f;
            root = new GameObject("AstronomicalCollision");
            root.transform.position = originW;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer };
            glowTex = kit.GlowTex();
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
            beamTex = kit.Tex(
                32,
                4,
                (x, y) =>
                {
                    float u = Mathf.Abs((x + 0.5f) / 16f - 1f);
                    return new Color(1, 1, 1, Mathf.Pow(1f - u, 1.4f));
                },
                TextureWrapMode.Clamp
            );
            BuildSky();
            BuildModels();
            BuildEffects();
            BuildSegs();
            var fg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fg.name = "AstronomicalCollisionFlash";
            UnityEngine.Object.Destroy(fg.GetComponent<Collider>());
            fg.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3500);
            var fr = fg.GetComponent<Renderer>();
            fr.sharedMaterial = flashMat;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.allowOcclusionWhenDynamic = false;
            flashGo = fg;
            flashGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight;
            ambientSaved = true;
            try
            {
                if (cam != null)
                {
                    farSave = cam.farClipPlane;
                    cam.farClipPlane = Mathf.Max(farSave, 6000f);
                }
            }
            catch { }
            lastT = -1f;
            hitDone = landDone = fallDone = launchDone = false;
            held = false;
            cabHave = false;
            built = true;
            AstronomicalEventMod.Log.LogInfo(
                "Collision M1 v2 built at "
                    + originW
                    + ": mech "
                    + (mechM != null)
                    + ", cabin "
                    + (cabM != null)
                    + ", moon "
                    + (moonM != null)
                    + ", collider "
                    + (colM != null)
                    + ", "
                    + tanks.Count
                    + " tanks, "
                    + brutes.Count
                    + " brutes, "
                    + rocks.Count
                    + " asteroids"
            );
        }

        public bool TryViewpoint(out Vector3 pos, out float y)
        {
            pos = originW + Vector3.up * 1.2f;
            y = yaw;
            return root != null;
        }

        Transform Quad(string n, Material mat, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = n;
            g.transform.SetParent(parent != null ? parent : root.transform, false);
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

        ParticleSystem MakePS(string n, int max, float grav, Material mat)
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

        // every model: bright unlit copies of its own textures (never glitchy lighting), keeping alpha for the A_ materials; originals put back before parking
        void Brighten(GltfModel mdl, float k, bool lit = false, bool forceAlpha = false)
        {
            foreach (var r in mdl.Pivot.GetComponentsInChildren<Renderer>(true))
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
                    bool alpha = forceAlpha || (src[i] != null && src[i].name.StartsWith("A_"));
                    Material mt =
                        alpha ? kit.Glow(new Color(1f, 1f, 1f, forceAlpha ? 1.0f : 0.5f), null, 3000)
                        : lit ? kit.Lit(Color.white * Mathf.Min(k, 1.25f), null, 0.28f)
                        : kit.Flat(Color.white * k);
                    try
                    {
                        mt.SetFloat("_Cull", 0f);
                        mt.doubleSidedGI = true;
                    }
                    catch { }
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
        }

        void Colliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null)
                    mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mf.gameObject.layer = floorLayer;
            }
        }

        // the loader's Pivot is feet-centred (it moves the file's origin): put the FILE origin of a model (the cabin's head point, the pad's top, the Collider's base) exactly where it belongs
        static void PlaceByFileOrigin(GltfModel m, Vector3 world)
        {
            if (m == null || m.Pivot == null || m.Model == null)
                return;
            m.Pivot.transform.position += world - m.Model.transform.position;
        }

        static GltfModel.Clip Clip(GltfModel m, string name)
        {
            if (m == null)
                return null;
            var c = m.FindClipExact(name);
            return c ?? (m.Clips.Count > 0 ? m.Clips[0] : null);
        }

        float GroundY(float x, float z)
        {
            RaycastHit h;
            Vector3 p = W(new Vector3(x, 400f, z));
            if (Physics.Raycast(p, Vector3.down, out h, 800f, ~0, QueryTriggerInteraction.Ignore) && h.point.y > originW.y - 40f)
                return h.point.y - originW.y;
            return -3f;
        }

        void BuildModels()
        {
            cabM = def.Prop("cabin");
            moonM = def.Prop("moonbase");
            colM = def.Prop("collider");
            mechM = def.Prop("mech");
            tankM = def.Prop("tank");
            domeM = def.Prop("dome");
            bruteM = def.Prop("brute");
            swordM = def.Prop("sword");
            originM = def.Prop("origin");
            rockM = def.Prop("asteroid");
            islandM = def.Prop("island");
            foreach (var mm in new[] { cabM, moonM, colM, mechM, tankM, domeM, bruteM, swordM, originM, rockM, islandM })
                if (mm != null && mm.Pivot != null)
                {
                    mm.Pivot.transform.SetParent(root.transform, false);
                    mm.Pivot.SetActive(false);
                }
            if (islandM != null && islandM.Pivot != null)
            {
                Brighten(islandM, 1f);
                islandM.Pivot.SetActive(true);
                PlaceByFileOrigin(islandM, W(new Vector3(0f, -3f, 0f)));
            } // the land: a desert island under the whole route (the Gorilla map stays on top of it where it exists)
            if (moonM != null && moonM.Pivot != null)
            {
                Brighten(moonM, 1.15f);
                Colliders(moonM.Pivot);
                moonM.Pivot.transform.localPosition = new Vector3(0f, PAD_Y, 0f);
                moonM.Pivot.SetActive(true);
                PlaceByFileOrigin(moonM, W(new Vector3(0f, PAD_Y - 0.8f, 0f)));
                moonR = CRig.Of(moonM, moonM.Pivot);
            }
            if (colM != null && colM.Pivot != null)
            {
                Brighten(colM, 1.2f);
                Colliders(colM.Pivot);
                colM.Pivot.transform.localPosition = new Vector3(0f, GroundY(0f, COLLIDER_Z), COLLIDER_Z);
                colM.Pivot.transform.localScale = Vector3.one * COLLIDER_S;
                colM.Pivot.SetActive(true);
                PlaceByFileOrigin(colM, W(new Vector3(0f, GroundY(0f, COLLIDER_Z), COLLIDER_Z)));
                colR = CRig.Of(colM, colM.Pivot);
            }
            if (mechM != null && mechM.Pivot != null)
            {
                Brighten(mechM, 1.2f, true);
                mechM.Pivot.transform.localScale = Vector3.one * MECH_S;
                mechM.Pivot.SetActive(true);
                mechR = CRig.Of(mechM, mechM.Pivot);
                foreach (var t in mechM.Pivot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("head_0130"))
                        headT = t;
                    else if (t.name.StartsWith("hand_r_083"))
                        handRT = t;
                    else if (t.name.StartsWith("hand_l_014"))
                        handLT = t;
                }
                foreach (var r in mechM.Pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    string n = r.name;
                    int vc = r.sharedMesh != null ? r.sharedMesh.vertexCount : 0;
                    if (n == "Object_13" || n == "Object_12" || vc == 3533 || vc == 73)
                        r.enabled = false;
                } // the head shell and the visor are the cabin while you ride
            }
            if (cabM != null && cabM.Pivot != null)
            {
                Brighten(cabM, 1.25f);
                cabM.Pivot.SetActive(true);
                cabR = CRig.Of(cabM, cabM.Pivot);
            }
            if (originM != null && originM.Pivot != null)
            {
                Brighten(originM, 1f);
                foreach (var r in originM.Pivot.GetComponentsInChildren<Renderer>(true))
                foreach (var mt in r.sharedMaterials)
                    if (mt != null)
                        mt.renderQueue = 3000;
                originR = CRig.Of(originM, originM.Pivot);
            }
            if (swordM != null && swordM.Pivot != null)
                Brighten(swordM, 1.3f);
            if (domeM != null && domeM.Pivot != null)
                Brighten(domeM, 1f, false, true); // BEFORE the clones are made: the clones used to keep the loader's material (the domes looked solid)
            if (domeM != null && domeM.Pivot != null)
                foreach (var rr in domeM.Pivot.GetComponentsInChildren<Renderer>(true))
                foreach (var mt in rr.sharedMaterials)
                    AstronomicalEventMod.Log.LogInfo(
                        "Collision dome material: "
                            + (
                                mt != null
                                    ? mt.name + " shader=" + mt.shader.name + " color=" + mt.color + " queue=" + mt.renderQueue
                                    : "null"
                            )
                    );
            // tanks, domes and B.R.U.T.E.s on the REAL map along the mech's route, asteroids in the sky
            if (tankM != null && tankM.Pivot != null)
            {
                Brighten(tankM, 1.15f, true);
                int gcount = 6;
                for (int g = 0; g < gcount; g++)
                {
                    float gz = 60f + g * 48f,
                        gx = (WorldKit.Hash(g, 2, 17) - 0.5f) * 90f;
                    for (int k = 0; k < 6; k++)
                    {
                        float x = gx + (WorldKit.Hash(g, k, 18) - 0.5f) * 36f,
                            z = gz + (WorldKit.Hash(g, k, 19) - 0.5f) * 36f;
                        var go = k == 0 && g == 0 ? tankM.Pivot : UnityEngine.Object.Instantiate(tankM.Pivot, root.transform);
                        go.SetActive(false);
                        go.name = "CTank";
                        var a = new Actor
                        {
                            Go = go,
                            Rig = CRig.Of(tankM, go),
                            P = new Vector3(x, GroundY(x, z), z),
                            Off = WorldKit.Hash(g, k, 22) * 8f,
                            Scale = 1.1f,
                        };
                        float ry = WorldKit.Hash(g, k, 20) * 360f;
                        go.transform.localRotation = Quaternion.Euler(0f, ry, 0f);
                        a.Die = 0f;
                        tanks.Add(a);
                        var bc = go.GetComponent<BoxCollider>();
                        if (bc == null)
                            bc = go.AddComponent<BoxCollider>();
                        bc.center = new Vector3(0f, 1f, 0f);
                        bc.size = new Vector3(3.8f, 2.2f, 6.8f);
                        go.layer = floorLayer;
                    }
                    if (domeM != null && domeM.Pivot != null)
                    {
                        var dg = g == 0 ? domeM.Pivot : UnityEngine.Object.Instantiate(domeM.Pivot, root.transform);
                        dg.SetActive(false);
                        dg.name = "CDome";
                        domes.Add(
                            new Actor
                            {
                                Go = dg,
                                Rig = CRig.Of(domeM, dg),
                                P = new Vector3(gx, GroundY(gx, gz) - 2f, gz),
                                Off = g * 1.7f,
                                Scale = 26f,
                                Die = gz,
                            }
                        );
                    }
                }
                foreach (var a in tanks)
                    a.Die = 46f + WorldKit.Hash(a.Off.GetHashCode(), 1, 3) * 60f;
            }
            if (bruteM != null && bruteM.Pivot != null)
            {
                Brighten(bruteM, 1.2f, true);
                float[] bz =  { 105f, 165f, 215f, 265f },
                    bx =  { -22f, 18f, -12f, 25f };
                for (int i = 0; i < bz.Length; i++)
                {
                    var go = i == 0 ? bruteM.Pivot : UnityEngine.Object.Instantiate(bruteM.Pivot, root.transform);
                    go.SetActive(false);
                    go.name = "CBrute" + i;
                    var a = new Actor
                    {
                        Go = go,
                        Rig = CRig.Of(bruteM, go),
                        P = new Vector3(bx[i], GroundY(bx[i], bz[i]), bz[i]),
                        Off = i * 2.3f,
                        Scale = 1.5f,
                        Die = 50f + i * 8f,
                    };
                    brutes.Add(a);
                    var cc = go.GetComponent<CapsuleCollider>();
                    if (cc == null)
                        cc = go.AddComponent<CapsuleCollider>();
                    cc.center = new Vector3(0f, 3.3f, 0f);
                    cc.radius = 1.8f;
                    cc.height = 6.6f;
                    go.layer = floorLayer;
                }
            }
            if (rockM != null && rockM.Pivot != null)
            {
                AstronomicalDebug.Anchors.Clear();
                AstronomicalDebug.Anchors["cab"] = () => dbgCab;
                AstronomicalDebug.Anchors["cabloc"] = () => Quaternion.Inverse(dbgCabQ) * (HeadNow() - dbgCab);
                AstronomicalDebug.Anchors["hand"] = () => handRT != null ? handRT.position : Vector3.zero;
                AstronomicalDebug.Anchors["handl"] = () => handLT != null ? handLT.TransformPoint(PALM) : Vector3.zero;
                AstronomicalDebug.Anchors["head"] = () => headT != null ? headT.position : Vector3.zero;
                AstronomicalDebug.Anchors["jones"] = () =>
                    jonesM != null && jonesM.Pivot != null ? jonesM.Pivot.transform.position + Vector3.up : Vector3.zero;
                AstronomicalDebug.Anchors["found"] = () =>
                    foundM != null && foundM.Pivot != null ? foundM.Pivot.transform.position + Vector3.up : Vector3.zero;
                AstronomicalDebug.Anchors["tank"] = () => sloneTank != null ? sloneTank.transform.position : Vector3.zero;
                AstronomicalDebug.Anchors["player"] = () => HeadNow();
                AstronomicalDebug.Anchors["sword"] = () =>
                    swordM != null && swordM.Pivot != null ? swordM.Pivot.transform.position : Vector3.zero;
                AstronomicalDebug.Anchors["mech"] = () =>
                    mechM != null && mechM.Pivot != null ? mechM.Pivot.transform.position : Vector3.zero;
                AstronomicalDebug.L2W = v => W(v);
                BuildModels2();
                Brighten(rockM, 1f);
                for (int i = 0; i < 9; i++)
                {
                    var go = i == 0 ? rockM.Pivot : UnityEngine.Object.Instantiate(rockM.Pivot, root.transform);
                    go.SetActive(false);
                    go.name = "CRock";
                    rocks.Add(
                        new Actor
                        {
                            Go = go,
                            Rig = CRig.Of(rockM, go),
                            Off = WorldKit.Hash(i, 3, 5) * 8f,
                            Scale = 8f + WorldKit.Hash(i, 4, 5) * 26f,
                            P = new Vector3((WorldKit.Hash(i, 1, 5) - 0.5f) * 500f, 0f, 40f + WorldKit.Hash(i, 2, 5) * 260f),
                        }
                    );
                }
            }
        }

        // the mech's clips, one after another with crossfades (each one-shot plays from its own start)
        void BuildSegs()
        {
            lifeBones = null;
            Action<float, string, float, float, bool> S = (t, c, sp, off, loop) =>
                mechSegs.Add(
                    new Seg
                    {
                        T = t,
                        Clip = c,
                        Speed = sp,
                        Off = off,
                        Loop = loop,
                    }
                );
            S(0f, "M_Idle2", 1f, 0f, true);
            S(T_LAUNCH, "M_Brace", 1f, 0f, true);
            S(T_LAUNCH + 8f, "M_Hover", 1f, 0f, true);
            S(T_LAND - 8f, "M_Brace", 1f, 0f, true);
            S(T_LAND, "M_Land", 1f, T_LAND, false);
            S(T_LAND + 5f, "M_Rise", 1f, T_LAND + 5f, false);
            S(T_LAND + 14f, "M_Idle2", 1f, 0f, true);
            float tt = 215f;
            bool left = false;
            foreach (var w in WALK)
            {
                S(w[0], "M_WalkA", 0.3f, 0f, true);
                S(w[1], left ? "M_FireL" : "M_Fire", 1f, 0f, true);
                left = !left;
            }
            S(T_HIT, "M_Hit", 1f, T_HIT, false);
            S(T_HIT + 4f, "M_Stagger", 1f, T_HIT + 4f, false);
            S(T_HIT + 8f, "M_Idle2", 1f, 0f, true);
            mechSegs.Sort((a, b) => a.T.CompareTo(b.T));
            // the walk legs after the hit are still scheduled by WALK (the Hit / Stagger / Idle2 above sit inside the pause before 266 s)
            S(T_SWORD, "M_SwordDraw", 1f, T_SWORD, false);
            S(T_SWORD + 8f, "M_SwordRaise", 1f, T_SWORD + 8f, false);
            S(T_SLASH, "M_SwordSlash", 1f, T_SLASH, false);
            S(T_FALL, "M_Fall", 1f, T_FALL, true);
            mechSegs.Sort((a, b) => a.T.CompareTo(b.T));
        }

        void PlayMech(float t)
        {
            if (mechR == null)
                return;
            int k = 0;
            for (int i = 0; i < mechSegs.Count; i++)
                if (mechSegs[i].T <= t)
                    k = i;
            var s = mechSegs[k];
            var a = Clip(mechM, s.Clip);
            float ta = s.Loop ? (t - s.T) * s.Speed + s.Off : t - s.Off;
            float w = 0f;
            GltfModel.Clip pc = null;
            float tpv = 0f;
            if (k > 0)
            {
                var p = mechSegs[k - 1];
                pc = Clip(mechM, p.Clip);
                tpv = p.Loop ? (t - p.T) * p.Speed + p.Off : t - p.Off;
                w = 1f - Sm(0f, 0.8f, t - s.T);
            }
            if (pc != null && w > 0.001f)
                mechR.Pose(a, ta, pc, tpv, w);
            else
                mechR.Pose(a, ta);
            MechAlive(t, true);
        }

        // constant small motion on top of every clip (breathing, servo sway), so the mech is never frozen between clips
        Transform[] lifeBones;

        void MechAlive(float t, bool arms)
        {
            if (mechM == null || mechM.Pivot == null)
                return;
            if (lifeBones == null)
            {
                var l = new List<Transform>();
                foreach (var tr in mechM.Pivot.GetComponentsInChildren<Transform>(true))
                {
                    var n = tr.name;
                    if (
                        n == "spine_01_06"
                        || n == "spine_02_07"
                        || n == "spine_03_08"
                        || n == "spine_04_09"
                        || n == "spine_05_010"
                        || n == "neck_01_0128"
                        || n == "upperarm_l_012"
                        || n == "lowerarm_l_013"
                        || n == "upperarm_r_081"
                        || n == "lowerarm_r_082"
                    )
                        l.Add(tr);
                }
                lifeBones = l.ToArray();
            } // ONLY bones the clips write every frame (extra rotation on others piled up: the mech tweaked out)
            for (int i = 0; i < lifeBones.Length; i++)
            {
                var tr = lifeBones[i];
                if (tr == null)
                    continue;
                bool arm = !tr.name.StartsWith("spine_0") && !tr.name.StartsWith("neck_01");
                if (arm && !arms)
                    continue;
                float am = arm ? 3.0f : 1.4f,
                    ph = i * 1.7f;
                tr.localRotation *= Quaternion.Euler(
                    am * Mathf.Sin(t * 1.3f + ph),
                    am * 0.7f * Mathf.Sin(t * 0.9f + ph * 1.3f),
                    am * Mathf.Sin(t * 1.1f + ph * 0.7f)
                );
            }
        }

        // ---- sky: black with stars in space, blue towards the ground, a warm hazy island sky, orange re-entry glow
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
                float th = i * Mathf.PI * 0.55f / lat,
                    ph = j * Mathf.PI * 2f / lon;
                float s = Mathf.Sin(th);
                var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph));
                dirs.Add(d);
                verts.Add(d * 2800f);
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
            dome = new GameObject("AstronomicalCollisionSky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 2501));
            dm.mainTexture = null;
            dm.renderQueue = 2501;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = new GameObject("CStars").AddComponent<ParticleSystem>();
            stars.transform.SetParent(dome.transform, false);
            stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pm = stars.main;
            pm.maxParticles = 900;
            pm.simulationSpace = ParticleSystemSimulationSpace.Local;
            pm.playOnAwake = false;
            pm.startLifetime = 100000f;
            var em = stars.emission;
            em.enabled = false;
            stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004);
            stars.Play();
            for (int k = 0; k < 700; k++)
            {
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.95f + 0.05f;
                var ep = new ParticleSystem.EmitParams();
                ep.position = d.normalized * 2700f;
                ep.velocity = Vector3.zero;
                ep.startSize = UnityEngine.Random.Range(6f, 18f);
                ep.startLifetime = 100000f;
                float b = UnityEngine.Random.Range(0.55f, 1f);
                ep.startColor = new Color(b, b, b, 1f);
                stars.Emit(ep, 1);
            }
        }

        void UpdateSky(float t)
        {
            if (skyMesh == null)
                return;
            float alt = t < T_LAUNCH ? PAD_Y : MechPosY(t),
                atm = t < T_LAUNCH ? 0f : Mathf.Clamp01(1f - alt / 600f),
                entry = Sm(T_ENTRY, T_ENTRY + 12f, t) * (1f - Sm(T_LAND - 25f, T_LAND, t)),
                ground = Sm(T_LAND - 20f, T_LAND + 5f, t),
                night = Sm(T_FALL - 30f, T_FALL, t);
            Color sky = Color.Lerp(new Color(0.15f, 0.35f, 0.7f), new Color(0.75f, 0.55f, 0.4f), ground),
                hor = Color.Lerp(new Color(0.5f, 0.65f, 0.85f), new Color(0.95f, 0.7f, 0.45f), ground),
                hot = new Color(1f, 0.45f, 0.1f);
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k];
                float up = Mathf.Pow(Mathf.Clamp01(d.y), 0.5f);
                var c = Color.Lerp(Color.Lerp(hor, sky, up), Color.black, Mathf.Clamp01(1f - atm * 1.6f));
                c = Color.Lerp(c, hot, entry * (1f - up) * 0.9f);
                c = Color.Lerp(c, new Color(0.15f, 0.04f, 0.18f), night * 0.8f);
                c.a = 1f;
                skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
            var sr = stars.GetComponent<ParticleSystemRenderer>();
            if (sr != null)
                sr.enabled = atm < 0.6f;
        }

        static float MechPosY(float t)
        {
            float p;
            return MechPos(t, out p).y;
        }

        void BuildEffects()
        {
            fx = MakePS("CFx", 4000, 0.2f, kit.Glow(Color.white, glowTex, 3004));
            dust = MakePS("CDust", 1500, -0.01f, kit.Glow(new Color(1f, 0.85f, 0.65f, 0.5f), glowTex, 3001));
            Func<string, LineRenderer> mk = n =>
            {
                var g = new GameObject(n);
                g.transform.SetParent(root.transform, false);
                var l = g.AddComponent<LineRenderer>();
                l.useWorldSpace = true;
                l.positionCount = 2;
                l.enabled = false;
                l.shadowCastingMode = ShadowCastingMode.Off;
                return l;
            };
            tracer = mk("CTracer");
            tracer.sharedMaterial = kit.Glow(Color.white, lineTex, 3001);
            tracerCore = mk("CTracerCore");
            tracerCore.sharedMaterial = kit.Glow(Color.white, lineTex, 3002);
            bolt = mk("CBolt");
            bolt.sharedMaterial = kit.Glow(Color.white, lineTex, 3001);
            boltCore = mk("CBoltCore");
            boltCore.sharedMaterial = kit.Glow(Color.white, lineTex, 3002);
            chargeMat = kit.Glow(new Color(1f, 0.5f, 0.2f, 0f), glowTex, 3003);
            cannonGlow = Quad("CCannon", chargeMat);
            sun = new GameObject("CSun").AddComponent<Light>();
            sun.transform.SetParent(root.transform, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.None;
            sun.intensity = 1f;
            sun.transform.rotation = Quaternion.Euler(48f, yaw + 35f, 0f);
            flashLamp = new GameObject("CFlashLight").AddComponent<Light>();
            flashLamp.transform.SetParent(root.transform, false);
            flashLamp.type = LightType.Point;
            flashLamp.range = 140f;
            flashLamp.intensity = 0f;
            flashLamp.shadows = LightShadows.None;
            engineLamp = new GameObject("CEngineLight").AddComponent<Light>();
            engineLamp.transform.SetParent(root.transform, false);
            engineLamp.type = LightType.Point;
            engineLamp.range = 80f;
            engineLamp.color = new Color(1f, 0.55f, 0.2f);
            engineLamp.intensity = 0f;
            engineLamp.shadows = LightShadows.None;
        }

        public void SetPaused(bool p)
        {
            paused = p;
            foreach (var ps in new[] { fx, dust, stars })
            {
                if (ps == null)
                    continue;
                if (p)
                    ps.Pause(true);
                else
                    ps.Play(true);
            }
        }

        // J CLIENT's NoClip, the same way: every solid MeshCollider of the map is switched off while you are moved into the cabin (and back on as soon as you sit in it, or when the event ends)
        readonly List<Collider> noclipped = new List<Collider>();
        float noclipT = -9f;

        void NoClip(bool on)
        {
            if (on)
            {
                noclipped.Clear();
                noclipT = Time.unscaledTime;
                foreach (var c in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
                {
                    if (c == null || !c.enabled || c.isTrigger || (root != null && c.transform.IsChildOf(root.transform)))
                        continue;
                    c.enabled = false;
                    noclipped.Add(c);
                }
            }
            else
            {
                foreach (var c in noclipped)
                    if (c != null)
                        c.enabled = true;
                noclipped.Clear();
                noclipT = -9f;
            }
        }

        // you are carried by an anchor (the cabin, a spot on the stairs, the mech's hand): it moves you, you are free to walk about inside a box round it (half width hx, forward z from zMin to zMax, head height yBase +-), gravity cancelled
        Vector3 dbgCab;
        Quaternion dbgCabQ = Quaternion.identity;

        void Carry(Vector3 anchor, Quaternion q, float dt, float hx, float zMin, float zMax, float yBase)
        {
            try
            {
                var pl = GTPlayer.Instance;
                var rb = pl.playerRigidBody;
                if (cabHave)
                {
                    Vector3 d = anchor - cabPrev;
                    if (d.sqrMagnitude < 90000f)
                        rb.position += d;
                }
                cabPrev = anchor;
                cabHave = true;
                Vector3 local = Quaternion.Inverse(q) * (pl.headCollider.transform.position - anchor);
                Vector3 c = new Vector3(
                    Mathf.Clamp(local.x, -hx, hx),
                    Mathf.Clamp(local.y, yBase - 0.3f, yBase + 0.4f),
                    Mathf.Clamp(local.z, zMin, zMax)
                );
                if ((c - local).sqrMagnitude > 1e-6f)
                    rb.position += q * (c - local);
                rb.linearVelocity = (rb.linearVelocity - Physics.gravity * dt) * (1f - Mathf.Min(0.6f, 2f * dt));
            }
            catch { }
        }

        void SetHold(bool on, Vector3 cabinPoint)
        {
            if (on == held)
                return;
            held = on;
            if (on)
            { // you are moved INTO the cabin: your head ends up at the cabin's origin, wherever the cabin is (700 m up on the moon base at the start)
                try
                {
                    NoClip(true);
                    var pl = GTPlayer.Instance;
                    var rb = pl.playerRigidBody;
                    homePos = rb.position;
                    Vector3 delta = cabinPoint - pl.headCollider.transform.position;
                    rb.position += delta;
                    rb.linearVelocity = Vector3.zero;
                    holdOff = rb.position - cabinPoint;
                    cabHave = false;
                    AstronomicalEventMod.Log.LogInfo("Collision: you are in the cabin at " + cabinPoint);
                }
                catch (Exception e)
                {
                    held = false;
                    AstronomicalEventMod.Log.LogWarning("Collision: could not seat you: " + e.Message);
                }
            }
            else
            {
                NoClip(false);
                try
                {
                    var rb = GTPlayer.Instance.playerRigidBody;
                    rb.position = homePos;
                    GTPlayer.Instance.transform.position = homePos;
                    rb.linearVelocity = Vector3.zero;
                }
                catch { }
            }
        }

        void Boom(Vector3 wp, float size, float power)
        {
            for (int k = 0; k < (int)(70 * Parts + 20); k++)
            {
                Vector3 d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y);
                Emit(
                    fx,
                    wp,
                    d * UnityEngine.Random.Range(4f, 24f) * size * 0.3f,
                    Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.5f), UnityEngine.Random.value),
                    size * UnityEngine.Random.Range(0.4f, 1.2f),
                    UnityEngine.Random.Range(0.8f, 1.8f)
                );
            }
            for (int k = 0; k < 16; k++)
                Emit(
                    dust,
                    wp + UnityEngine.Random.insideUnitSphere * size,
                    new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(2f, 9f), UnityEngine.Random.Range(-3f, 3f)),
                    new Color(0.35f, 0.3f, 0.28f, 0.6f),
                    size * 2.4f,
                    UnityEngine.Random.Range(2f, 4f)
                );
            flashLamp.transform.position = wp;
            flashLamp.color = new Color(1f, 0.6f, 0.25f);
            flashLamp.intensity = Lit * 8f * power;
            AstronomicalHazards.Blast(wp, 110f, 7f * power, 9f * power);
            shake = Mathf.Max(shake, 0.2f * power);
        }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built)
                return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f);
            if (t >= T_BLACK)
            {
                ApplyAfter(t, dt);
                return;
            }
            if (underOn)
            {
                ExitUnder();
            }
            bool back = t < lastT - 0.4f;
            if (back || lastT < 0f)
            {
                hitDone = landDone = fallDone = launchDone = false;
                foreach (var a in tanks)
                {
                    a.Dead = false;
                    a.Boomed = false;
                }
                foreach (var a in brutes)
                {
                    a.Dead = false;
                    a.Boomed = false;
                }
            }
            float tp = lastT < 0f ? t : lastT;
            lastT = t;
            var cam = Camera.main;
            if (cam != null)
                dome.transform.position = cam.transform.position;
            // ---- the mech: position, pitch and clip, the cabin on its head bone
            float pitch;
            Vector3 mp = MechPos(t, out pitch);
            bool mechOn = mechM != null && mechM.Pivot != null;
            float sh = shake;
            shake = Mathf.Max(0f, shake - dt * 1.4f);
            if (mechOn)
            {
                var pt = mechM.Pivot.transform;
                pt.localPosition = mp;
                pt.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                pt.localScale = Vector3.one * MECH_S;
                PlayMech(t);
            }
            Vector3 cabPos = mechOn && headT != null ? headT.position : W(mp + new Vector3(0f, 57f, 0f));
            Quaternion cabQ = Quaternion.Euler(pitch * 0.35f, yaw, 0f);
            cabRot = Quaternion.Slerp(cabRot, cabQ, Mathf.Clamp01(dt * 8f + 0.01f));
            if (cabM != null)
            {
                var ct = cabM.Pivot.transform;
                ct.rotation = cabQ;
                ct.position = cabPos;
                PlaceByFileOrigin(cabM, cabPos);
                if (cabM.Clips.Count > 0)
                    cabR.Pose(cabM.Clips[0], t);
            }
            SetHold(t < endT - 0.05f, cabPos);
            if (noclipped.Count > 0 && Time.unscaledTime - noclipT > 0.6f)
                NoClip(false); // seated: the map is solid again
            dbgCab = cabPos;
            dbgCabQ = cabRot;
            if (held && dt > 0f)
                Carry(cabPos, cabRot, dt, 1.9f, -2.0f, 1.2f, 0f); // the SAME smoothed rotation the cabin model uses (the raw one let you poke through the walls while the mech turned)
            // ---- moon base (animated dish) until the launch, The Origin's hologram over the dashboard
            bool moonOn = moonM != null && t < T_ENTRY + 6f;
            if (moonM != null && moonM.Pivot.activeSelf != moonOn)
                moonM.Pivot.SetActive(moonOn);
            if (moonOn && moonM.Clips.Count > 0)
                moonR.Pose(moonM.Clips[0], t);
            bool hol = false; // the Origin hologram is removed if (originM != null && originM.Pivot.activeSelf != hol) originM.Pivot.SetActive(hol);
            if (hol)
            {
                var pt = originM.Pivot.transform;
                pt.position = cabPos + cabRot * new Vector3(0f, -1.68f + 0.04f * Mathf.Sin(t * 1.3f), 1.0f);
                pt.rotation = cabRot * Quaternion.Euler(0f, 180f + 8f * Mathf.Sin(t * 0.4f), 0f);
                float s = 0.95f / Mathf.Max(0.2f, originM.RendererBounds.size.y);
                pt.localScale = Vector3.one * s;
                float a =
                    (0.75f + 0.15f * Mathf.Sin(t * 9f) * (Mathf.Sin(t * 1.3f) > 0.7f ? 1f : 0.2f))
                    * Sm(0f, 3f, t)
                    * (1f - Sm(T_LAUNCH - 3f, T_LAUNCH, t));
                foreach (var r in originM.Pivot.GetComponentsInChildren<Renderer>(true))
                foreach (var mt in r.sharedMaterials)
                    if (mt != null)
                        mt.color = new Color(0.55f, 1f, 1f, a);
            }
            // ---- launch (1:35), re-entry, landing (3:11)
            if (!launchDone && t >= T_LAUNCH && t < T_LAUNCH + 3f && !paused)
            {
                launchDone = true;
                shake = Mathf.Max(shake, 1.2f);
            }
            if (t >= T_LAUNCH && t < T_ENTRY)
                sh = Mathf.Max(sh, 0.4f * Sm(T_LAUNCH, T_LAUNCH + 3f, t));
            float ent = Sm(T_ENTRY, T_ENTRY + 8f, t) * (1f - Sm(T_LAND - 30f, T_LAND - 6f, t));
            bool flame = (t >= T_LAUNCH && t < T_LAND) && mechOn;
            engineLamp.intensity = Lit * (flame ? 3f + ent * 5f : 0f);
            engineLamp.transform.position = mechOn ? mechM.Pivot.transform.position + Vector3.up * 8f : cabPos;
            if (!paused && dt > 0f && flame)
            {
                sparkAcc += 220f * Parts * dt;
                int n = (int)sparkAcc;
                sparkAcc -= n;
                for (int k = 0; k < n; k++)
                {
                    Vector3 fp =
                        mechM.Pivot.transform.position
                        + new Vector3(
                            UnityEngine.Random.Range(-5f, 5f),
                            UnityEngine.Random.Range(0f, 14f),
                            UnityEngine.Random.Range(-5f, 5f)
                        );
                    Emit(
                        fx,
                        fp,
                        new Vector3(0f, UnityEngine.Random.Range(-60f, -140f), 0f),
                        new Color(1f, UnityEngine.Random.Range(0.4f, 0.8f), 0.2f, 0.85f),
                        UnityEngine.Random.Range(1.5f, 4.5f),
                        0.7f
                    );
                }
            }
            if (!landDone && t >= T_LAND && t < T_LAND + 2f && !paused)
            {
                landDone = true;
                shake = Mathf.Max(shake, 1.6f);
                AstronomicalHazards.Blast(W(new Vector3(0f, 0f, 20f)), 200f, 14f, 14f);
                for (int k = 0; k < (int)(260 * Parts); k++)
                {
                    float a = UnityEngine.Random.value * 6.2832f,
                        r = UnityEngine.Random.Range(8f, 50f);
                    Emit(
                        dust,
                        W(new Vector3(Mathf.Cos(a) * r, 1f, LAND_Z + Mathf.Sin(a) * r)),
                        new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * UnityEngine.Random.Range(8f, 28f),
                        new Color(0.9f, 0.75f, 0.55f, 0.55f),
                        UnityEngine.Random.Range(8f, 22f),
                        UnityEngine.Random.Range(5f, 9f)
                    );
                }
            }
            // ---- asteroids drifting past during the flight (animated Blender asteroid)
            for (int i = 0; i < rocks.Count; i++)
            {
                var a = rocks[i];
                bool on = t >= T_LAUNCH + 10f && t < T_LAND - 3f;
                if (a.Go.activeSelf != on)
                    a.Go.SetActive(on);
                if (!on)
                    continue;
                float k = Mathf.Repeat(t * 0.06f + a.Off * 0.125f, 1f);
                Vector3 lp = mp + new Vector3(a.P.x * (0.6f + k), (k - 0.3f) * 600f - 150f, 160f + a.P.z * 0.4f + k * 60f);
                var tr = a.Go.transform;
                tr.localPosition = lp;
                tr.localScale = Vector3.one * a.Scale;
                a.Rig.Pose(Clip(rockM, "Animation"), t + a.Off);
            }
            // ---- tanks (3:11+), orange barriers, B.R.U.T.E.s: all animated, on the real map
            float walked = MechZ(t);
            bool tOn = t >= T_LAND - 6f;
            for (int i = 0; i < tanks.Count; i++)
            {
                var a = tanks[i];
                float rel = a.P.z - walked;
                bool dead = t >= 215f && rel < a.Die && rel > -30f || t >= 215f && rel <= -30f;
                bool want = tOn && !dead;
                if (dead && !a.Boomed && !paused && t >= 215f && rel > -30f)
                {
                    a.Boomed = true;
                    Boom(W(a.P + Vector3.up * 1.2f), 5f, 0.8f);
                    tracerTo = W(a.P + Vector3.up * 1f);
                    tracerT = t;
                }
                if (a.Go.activeSelf != want)
                    a.Go.SetActive(want);
                if (want)
                {
                    var tr = a.Go.transform;
                    tr.localPosition = a.P;
                    tr.localScale = Vector3.one * a.Scale;
                    a.Rig.Pose(Clip(tankM, "Animation"), t + a.Off);
                }
            }
            for (int i = 0; i < domes.Count; i++)
            {
                var a = domes[i];
                float rel = a.Die - walked;
                bool want = t >= T_LAND + 6f && rel > -20f && !(rel < 40f && t >= 215f);
                if (a.Go.activeSelf != want)
                    a.Go.SetActive(want);
                if (want)
                {
                    var tr = a.Go.transform;
                    tr.localPosition = a.P;
                    tr.localScale = Vector3.one * a.Scale * Sm(T_LAND + 6f, T_LAND + 18f, t);
                    a.Rig.Pose(Clip(domeM, "Animation"), t + a.Off);
                }
            }
            for (int i = 0; i < brutes.Count; i++)
            {
                var a = brutes[i];
                float bzNow = a.P.z - Mathf.Clamp((t - 215f) * 0.9f, 0f, 45f);
                float rel = bzNow - walked;
                bool dead = t >= 215f && rel < a.Die;
                bool want = t >= 215f && rel > -25f;
                float dieT = 0f;
                if (dead && !a.Boomed && !paused)
                {
                    a.Boomed = true;
                    Boom(W(a.P + Vector3.up * 5f), 9f, 1.4f);
                }
                if (a.Go.activeSelf != want)
                    a.Go.SetActive(want);
                if (!want)
                    continue;
                var tr = a.Go.transform;
                tr.localPosition = new Vector3(a.P.x, a.P.y, bzNow);
                tr.localRotation = Quaternion.Euler(0f, 180f, 0f);
                tr.localScale = Vector3.one * a.Scale;
                string c =
                    dead ? "B_Death"
                    : rel < 140f ? "B_Fire"
                    : rel < 230f ? "B_Aim"
                    : "B_Alert";
                if (!dead && t >= 215f && t < 260f)
                    c = "B_Walk";
                var clip = Clip(bruteM, c);
                float ct = c == "B_Death" ? Mathf.Max(0f, t - (a.Die > 0f ? tDie(a, tp) : t)) : t + a.Off;
                a.Rig.Pose(clip, c == "B_Death" ? Mathf.Min(ct, 2.6f) : ct);
            }
            // ---- the muzzle tracer for the last kill
            float ta2 = t - tracerT;
            bool trOn = ta2 >= 0f && ta2 < 0.18f && mechOn && handRT != null;
            if (tracer.enabled != trOn)
            {
                tracer.enabled = trOn;
                tracerCore.enabled = trOn;
            }
            if (trOn)
            {
                Vector3 mz = handRT.position;
                tracer.SetPosition(0, mz);
                tracer.SetPosition(1, tracerTo);
                tracerCore.SetPosition(0, mz);
                tracerCore.SetPosition(1, tracerTo);
                tracer.widthMultiplier = 1.8f;
                tracerCore.widthMultiplier = 0.6f;
                tracer.startColor = tracer.endColor = new Color(1f, 0.7f, 0.3f, 0.8f);
                tracerCore.startColor = tracerCore.endColor = new Color(1f, 1f, 0.9f, 1f);
            }
            // ---- the weapon: powers up at 4:08 far ahead, hits the mech at 4:11
            float cw = Sm(T_CHARGE, T_HIT, t);
            bool cOn = t >= T_CHARGE - 1f && t < T_HIT + 1.5f;
            if (cannonGlow.gameObject.activeSelf != cOn)
                cannonGlow.gameObject.SetActive(cOn);
            bolt.enabled = boltCore.enabled = t >= T_HIT && t < T_HIT + 0.6f;
            if (cOn)
            {
                Vector3 cp = W(new Vector3(60f, 40f, MechZ(t) + 260f));
                Face(cannonGlow, cp);
                cannonGlow.localScale = Vector3.one * (6f + 90f * cw * cw);
                chargeMat.color = new Color(1f, 0.5f + 0.4f * cw, 0.2f + 0.5f * cw, 0.2f + 0.7f * cw * (1f - Sm(T_HIT, T_HIT + 1.5f, t)));
                if (bolt.enabled)
                {
                    Vector3 b0 = cp,
                        b1 = cabPos;
                    bolt.SetPosition(0, b0);
                    bolt.SetPosition(1, b1);
                    boltCore.SetPosition(0, b0);
                    boltCore.SetPosition(1, b1);
                    bolt.widthMultiplier = 8f;
                    boltCore.widthMultiplier = 2.5f;
                    bolt.startColor = bolt.endColor = new Color(1f, 0.55f, 0.2f, 0.7f);
                    boltCore.startColor = boltCore.endColor = new Color(1f, 0.95f, 0.8f, 1f);
                }
            }
            if (!hitDone && t >= T_HIT && t < T_HIT + 2f && !paused)
            {
                hitDone = true;
                shake = Mathf.Max(shake, 2.2f);
                AstronomicalHazards.Blast(cabPos + cabRot * Vector3.forward * 6f, 200f, 16f, 16f);
                for (int k = 0; k < 220; k++)
                    Emit(
                        fx,
                        cabPos
                            + cabRot
                                * new Vector3(
                                    UnityEngine.Random.Range(-1.5f, 1.5f),
                                    UnityEngine.Random.Range(-0.5f, 1f),
                                    UnityEngine.Random.Range(1f, 3f)
                                ),
                        UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(2f, 12f),
                        new Color(1f, 0.7f, 0.3f, 0.9f),
                        UnityEngine.Random.Range(0.15f, 0.6f),
                        UnityEngine.Random.Range(0.6f, 1.6f)
                    );
            }
            // ---- the Collider (animated): ring, casing, beam, energy rings; the sword in the mech's hand (7:25 -> 7:57)
            if (colM != null && colM.Clips.Count > 0)
                colR.Pose(colM.Clips[0], t);
            bool swOn = swordM != null && mechOn && handRT != null && t >= T_SWORD && t < T_FALL + 3f;
            if (swordM != null && swordM.Pivot.activeSelf != swOn)
                swordM.Pivot.SetActive(swOn);
            if (swOn)
            { // the sword's hilt sits in the fist: its blade runs along the hand's palm-width axis, its guard along the fingers (hand bone axes measured with tools/mech_palm.py)
                var st = swordM.Pivot.transform;
                float bs = AstronomicalDebug.Tweak[0],
                    gs2 = AstronomicalDebug.Tweak[1];
                Vector3 bdir = handRT.TransformDirection(new Vector3(-0.205f, -0.846f, -0.489f)).normalized * bs,
                    gdir = handRT.TransformDirection(new Vector3(0.988f, 0.013f, 0.152f)).normalized * gs2;
                st.rotation = Quaternion.LookRotation(Vector3.Cross(gdir, bdir), bdir);
                st.localScale = Vector3.one * 3.8f;
                PlaceByFileOrigin(swordM, handRT.position + bdir * 1.2f);
            }
            if (!fallDone && t >= T_FALL && !paused)
            {
                fallDone = true;
                shake = Mathf.Max(shake, 3f);
                for (int k = 0; k < 300; k++)
                    Emit(
                        dust,
                        W(new Vector3(UnityEngine.Random.Range(-60f, 60f), 0f, UnityEngine.Random.Range(150f, 330f))),
                        new Vector3(
                            UnityEngine.Random.Range(-6f, 6f),
                            UnityEngine.Random.Range(10f, 40f),
                            UnityEngine.Random.Range(-6f, 6f)
                        ),
                        new Color(0.6f, 0.45f, 0.35f, 0.8f),
                        UnityEngine.Random.Range(4f, 14f),
                        UnityEngine.Random.Range(3f, 6f)
                    );
                AstronomicalHazards.Blast(cabPos, 200f, 14f, 14f);
            }
            float blk = Sm(T_BLACK - 2.5f, T_BLACK, t);
            if (flashGo.activeSelf != blk > 0.003f)
                flashGo.SetActive(blk > 0.003f);
            if (blk > 0.003f)
            {
                flashMat.color = new Color(0f, 0f, 0f, blk);
                flashGo.transform.position = cam != null ? cam.transform.position : cabPos;
            }
            flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 20f * Lit);
            // camera shake: the whole cabin rattles a little (kept small for comfort)
            if (cabM != null && sh > 0.01f)
                cabM.Pivot.transform.position +=
                    new Vector3(Mathf.Sin(t * 47f) + Mathf.Sin(t * 31f), Mathf.Sin(t * 53f), Mathf.Sin(t * 41f))
                    * 0.04f
                    * Mathf.Min(sh, 2f);
            if (!paused && dt > 0f)
            {
                rumbleT += dt;
                if (rumbleT > 0.12f)
                {
                    rumbleT = 0f;
                    float v = Mathf.Max(sh * 0.4f, (t >= T_LAUNCH && t < T_LAND) ? 0.25f : 0f, Walking(t) ? 0.12f : 0f);
                    if (v > 0.05f)
                    {
                        try
                        {
                            var gt = GorillaTagger.Instance;
                            if (gt != null)
                            {
                                gt.StartVibration(true, Mathf.Clamp01(v), 0.14f);
                                gt.StartVibration(false, Mathf.Clamp01(v), 0.14f);
                            }
                        }
                        catch { }
                    }
                }
            }
            float dayK = Sm(T_LAND - 30f, T_LAND, t),
                dusk = Sm(400f, T_ARRIVE, t);
            sun.color = Color.Lerp(
                new Color(0.75f, 0.82f, 1f),
                Color.Lerp(new Color(1f, 0.93f, 0.8f), new Color(0.85f, 0.6f, 0.9f), dusk),
                dayK
            );
            sun.intensity = Mathf.Lerp(0.9f, 1.15f, dayK) * Mathf.Max(0.5f, Lit);
            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.55f, 0.58f, 0.7f),
                Color.Lerp(new Color(0.85f, 0.8f, 0.75f), new Color(0.45f, 0.3f, 0.5f), dusk),
                dayK
            );
            if (t - skyT > 0.1f || t < skyT)
            {
                skyT = t;
                UpdateSky(t);
            }
        }

        // the moment a brute died = when the mech's walk brought it inside its range (found by scanning the walk)
        float tDie(Actor a, float fallback)
        {
            for (float s = 215f; s < 480f; s += 0.5f)
                if (a.P.z - MechZ(s) < a.Die)
                    return s;
            return fallback;
        }

        public string DebugInfo()
        {
            return "t "
                + lastT.ToString("0.0")
                + "  mech z "
                + MechZ(Mathf.Max(0f, lastT)).ToString("0")
                + "  tanks "
                + tanks.Count
                + "  brutes "
                + brutes.Count;
        }

        public void Cleanup()
        {
            try
            {
                SetHold(false, Vector3.zero);
            }
            catch { }
            try
            {
                CleanupModels2();
            }
            catch { }
            if (ambientSaved)
            {
                RenderSettings.ambientLight = ambientSave;
                ambientSaved = false;
            }
            try
            {
                if (farSave > 0f && Camera.main != null)
                    Camera.main.farClipPlane = farSave;
            }
            catch { }
            farSave = -1f;
            foreach (var kv in origMats)
                if (kv.Key != null)
                    kv.Key.sharedMaterials = kv.Value;
            origMats.Clear();
            var first = new HashSet<GameObject>();
            foreach (var mm in new[] { cabM, moonM, colM, mechM, tankM, domeM, bruteM, swordM, originM, rockM, islandM })
                if (mm != null && mm.Pivot != null)
                    first.Add(mm.Pivot);
            foreach (var lst in new[] { tanks, brutes, rocks, domes })
            foreach (var a in lst)
                if (a.Go != null && !first.Contains(a.Go))
                    UnityEngine.Object.Destroy(a.Go);
            foreach (var mdl in new[] { cabM, moonM, colM, mechM, tankM, domeM, bruteM, swordM, originM, rockM, islandM })
                if (mdl != null && mdl.Pivot != null)
                {
                    foreach (var bc in mdl.Pivot.GetComponentsInChildren<Collider>(true))
                        UnityEngine.Object.Destroy(bc);
                    mdl.Pivot.transform.SetParent(null, false);
                    mdl.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(mdl.Pivot);
                }
            cabM = moonM = colM = mechM = tankM = domeM = bruteM = swordM = originM = rockM = islandM = null;
            tanks.Clear();
            brutes.Clear();
            rocks.Clear();
            domes.Clear();
            if (flashGo != null)
                UnityEngine.Object.Destroy(flashGo);
            flashGo = null;
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
            held = false;
            headT = handRT = null;
        }
    }
}
