using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "The Butterfly Event" (Fortnite Chapter 1 Season 6, November 4 2018), from the user's reading of the video. Same Gorilla Tag map, nothing loaded or replaced, no knockback.
    // The cube hangs in the sky and spins faster and faster; at 1:00 it explodes (white flash) and it is gone for good. The In-Between (a pale grey void with floating white orbs and a white floor) takes the place of the map around you.
    // 1:48 the butterfly appears and flies round you; 2:15 it settles on your hand and stays until 2:17; the white flash sends you back to the map (it is put back exactly as it was); the event ends at 2:20.
    // The map is only hidden (never moved or destroyed) and you are never teleported: the white floor is where the real floor was.
    internal sealed class AstronomicalButterfly : IEventScene, IDebug
    {
        const float T_BOOM = 60f,
            T_VOID = 60.25f,
            T_BUT = 108f,
            T_FLY = 130f,
            T_LAND = 135f,
            T_BACK = 137f,
            CUBE = 9f;

        static float Sm(float a, float b, float x)
        {
            return AstronomicalCube.Sm(a, b, x);
        }

        AstronomicalEventDef def;
        GameObject root,
            dome,
            flashGo;
        WorldKit kit;
        GltfModel cubeM,
            butM,
            inbM;
        Transform cubePv,
            inbPv,
            butPv,
            cubeGlow,
            butGlow;
        Material cubeGlowMat,
            butGlowMat,
            butGlow2Mat,
            flashMat;
        Vector3 voidCenter,
            holdPos;
        bool held;
        Transform butGlow2;
        Light cubeLamp,
            butLamp;
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        LineRenderer arc,
            arcCore;
        ParticleSystem fx,
            motes,
            stars;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        int skyN;
        float skyT = -9f;
        Texture2D glowTex,
            lineTex;
        Vector3 origin;
        float yaw,
            lastT = -1f,
            endT = 140f,
            flapT,
            motesAcc,
            sparkAcc,
            rumbleT,
            butAcc;
        int floorLayer;
        bool built,
            paused,
            boomDone,
            butDone,
            voidOn;
        Color ambientSave;
        bool ambientSaved;
        Quaternion butRot = Quaternion.identity;
        bool butRotInit;
        Vector3 lastBut;
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
            endT = def != null && def.Length > 5f ? def.Length : 140f;
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
            root = new GameObject("AstronomicalButterfly");
            root.transform.position = origin;
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
            BuildSky();
            BuildCube();
            BuildVoid();
            BuildButterfly();
            fx = MakePS("BFx", 3000, 0.15f, kit.Glow(Color.white, glowTex, 3004));
            motes = MakePS("BMotes", 600, 0f, kit.Glow(Color.white, glowTex, 3002));
            var fg = new GameObject("BFlash");
            fg.transform.SetParent(root.transform, false);
            flashGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flashGo.name = "AstronomicalButterflyFlash";
            UnityEngine.Object.Destroy(flashGo.GetComponent<Collider>());
            flashGo.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(1f, 1f, 1f, 0f), null, 3500);
            var fr = flashGo.GetComponent<Renderer>();
            fr.sharedMaterial = flashMat;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.allowOcclusionWhenDynamic = false;
            flashGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight;
            ambientSaved = true;
            lastT = -1f;
            boomDone = butDone = voidOn = false;
            built = true;
            AstronomicalEventMod.Log.LogInfo(
                "Butterfly event built at "
                    + origin
                    + ": cube "
                    + (cubeM != null)
                    + ", in-between "
                    + (inbM != null)
                    + ", butterfly "
                    + (butM != null)
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

        // the model's own materials are swapped for bright unlit copies of its textures (put back before it is parked)
        void Brighten(GltfModel mdl, float k)
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
                    var mt = kit.Flat(Color.white * k);
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
            dome = new GameObject("AstronomicalButterflySky");
            dome.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var mr = dome.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 2501));
            dm.mainTexture = null;
            dm.renderQueue = 2501;
            owned.Add(dm);
            mr.sharedMaterial = dm;
            mr.allowOcclusionWhenDynamic = false;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = new GameObject("BStars").AddComponent<ParticleSystem>();
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
            for (int k = 0; k < 420; k++)
            {
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.97f + 0.03f;
                var ep = new ParticleSystem.EmitParams();
                ep.position = d.normalized * 270f;
                ep.velocity = Vector3.zero;
                ep.startSize = UnityEngine.Random.Range(0.8f, 2.2f);
                ep.startLifetime = 100000f;
                float b = UnityEngine.Random.Range(0.5f, 1f);
                ep.startColor = new Color(b, b, Mathf.Min(1f, b + 0.2f), 1f);
                stars.Emit(ep, 1);
            }
        }

        // dusk that darkens and turns violet as the cube spins up
        void UpdateSky(float t)
        {
            if (skyMesh == null)
                return;
            float p = Sm(0f, 60f, t);
            Color top = Color.Lerp(new Color(0.08f, 0.07f, 0.22f), new Color(0.04f, 0.0f, 0.12f), p),
                hor = Color.Lerp(new Color(0.45f, 0.3f, 0.6f), new Color(0.3f, 0.1f, 0.45f), p);
            Vector3 cd = new Vector3(0f, 30f, 45f).normalized;
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k];
                var c = Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.5f));
                float g = Mathf.Exp(-Mathf.Pow(Vector3.Angle(d, cd) / 30f, 2f)) * p * 0.5f;
                c.r += g * 0.7f;
                c.g += g * 0.3f;
                c.b += g;
                c.a = 1f;
                skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        void BuildCube()
        {
            cubeM = def.Prop("cube");
            if (cubeM == null || cubeM.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("Butterfly: cube model missing");
                return;
            }
            cubePv = cubeM.Pivot.transform;
            cubePv.SetParent(root.transform, false);
            cubeM.Pivot.SetActive(true);
            foreach (var r in cubeM.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                foreach (var mt in r.sharedMaterials)
                    if (mt != null)
                    {
                        mt.DisableKeyword("_EMISSION");
                        mt.SetColor("_EmissionColor", Color.black);
                    }
            }
            cubeGlowMat = kit.Glow(new Color(0.85f, 0.55f, 1f, 0f), glowTex, 3002);
            cubeGlow = Quad("BCubeGlow", cubeGlowMat);
            cubeLamp = new GameObject("BCubeLight").AddComponent<Light>();
            cubeLamp.transform.SetParent(root.transform, false);
            cubeLamp.type = LightType.Point;
            cubeLamp.color = new Color(0.75f, 0.45f, 1f);
            cubeLamp.range = 160f;
            cubeLamp.intensity = 0f;
            cubeLamp.shadows = LightShadows.None;
            var g1 = new GameObject("BArc");
            g1.transform.SetParent(root.transform, false);
            arc = g1.AddComponent<LineRenderer>();
            arc.useWorldSpace = true;
            arc.positionCount = 14;
            arc.sharedMaterial = kit.Glow(Color.white, lineTex, 3001);
            arc.enabled = false;
            var g2 = new GameObject("BArcCore");
            g2.transform.SetParent(root.transform, false);
            arcCore = g2.AddComponent<LineRenderer>();
            arcCore.useWorldSpace = true;
            arcCore.positionCount = 14;
            arcCore.sharedMaterial = kit.Glow(Color.white, lineTex, 3002);
            arcCore.enabled = false;
        }

        // the In-Between: bright unlit copy of the Blender model (pale grey dome, floating white orbs), a white floor with a collider where the real floor is
        void BuildVoid()
        {
            inbM = def.Prop("inbetween");
            if (inbM == null || inbM.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("Butterfly: In-Between model missing");
                return;
            }
            inbPv = inbM.Pivot.transform;
            inbPv.SetParent(root.transform, false);
            Brighten(inbM, 1.05f);
            inbM.Pivot.SetActive(false);
        }

        void BuildButterfly()
        {
            butM = def.Prop("butterfly");
            if (butM == null || butM.Pivot == null)
            {
                AstronomicalEventMod.Log.LogWarning("Butterfly: butterfly model missing");
                return;
            }
            butPv = butM.Pivot.transform;
            butPv.SetParent(root.transform, false);
            foreach (var r in butM.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false;
                r.enabled = true;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            butM.Pivot.SetActive(false);
            butGlowMat = kit.Glow(new Color(0.6f, 0.85f, 1f, 0f), glowTex, 3000);
            butGlow = Quad("BButGlow", butGlowMat);
            butGlow2Mat = kit.Glow(new Color(0.6f, 0.45f, 1f, 0f), glowTex, 2999);
            butGlow2 = Quad("BButAura", butGlow2Mat);
            butLamp = new GameObject("BButLight").AddComponent<Light>();
            butLamp.transform.SetParent(root.transform, false);
            butLamp.type = LightType.Point;
            butLamp.color = new Color(0.6f, 0.85f, 1f);
            butLamp.range = 14f;
            butLamp.intensity = 0f;
            butLamp.shadows = LightShadows.None;
        }

        public void SetPaused(bool p)
        {
            paused = p;
            foreach (var ps in new[] { fx, motes, stars })
            {
                if (ps == null)
                    continue;
                if (p)
                    ps.Pause(true);
                else
                    ps.Play(true);
            }
        }

        static Vector3 HeadPos()
        {
            try
            {
                var p = GTPlayer.Instance;
                if (p != null)
                    return p.headCollider.transform.position;
            }
            catch { }
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        static Vector3 HandPos(Vector3 fallback)
        {
            try
            {
                var gt = GorillaTagger.Instance;
                if (gt != null && gt.rightHandTransform != null)
                    return gt.rightHandTransform.position;
            }
            catch { }
            return fallback;
        }

        void SetVoid(bool on)
        {
            if (on == voidOn)
                return;
            voidOn = on;
            if (on)
            { // it opens where you are: the dome is centred on your head and you are held up in the air (gravity cancelled, drift damped); the real map is only switched off
                voidCenter = HeadPos();
                try
                {
                    var pl = GTPlayer.Instance;
                    if (pl != null)
                    {
                        holdPos = pl.playerRigidBody.position;
                        held = true;
                    }
                }
                catch
                {
                    held = false;
                }
                if (inbM != null)
                    inbM.Pivot.SetActive(true);
                AstronomicalForestHider.Hide(voidCenter, 500f, null);
            }
            else
            {
                AstronomicalForestHider.Restore();
                if (inbM != null)
                    inbM.Pivot.SetActive(false);
                if (held)
                {
                    try
                    {
                        var rb = GTPlayer.Instance.playerRigidBody;
                        if ((rb.position - holdPos).sqrMagnitude > 0.25f)
                        {
                            rb.position = holdPos;
                            GTPlayer.Instance.transform.position = holdPos;
                        }
                        rb.linearVelocity = Vector3.zero;
                    }
                    catch { }
                    held = false;
                } // the flash sends you back to where you were
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
                boomDone = false;
                butDone = false;
            }
            lastT = t;
            var cam = Camera.main;
            if (cam != null)
                dome.transform.position = cam.transform.position;
            Vector3 head = HeadPos();
            // ---- the cube: spins faster and faster, shakes, sparks and arcs; explodes at 1:00 and is gone for good
            float p = Mathf.Clamp01(t / T_BOOM),
                p4 = p * p * p * p;
            bool cOn = cubeM != null && t < T_BOOM;
            if (cubeM != null && cubeM.Pivot.activeSelf != cOn)
                cubeM.Pivot.SetActive(cOn);
            cubeGlow.gameObject.SetActive(cOn);
            Vector3 cc = W(new Vector3(0f, 24f + 10f * p + 0.8f * Mathf.Sin(t * 0.7f), 45f));
            int q = (int)(t * 30f);
            cc += new Vector3(WorldKit.Hash(q, 1, 3) - 0.5f, WorldKit.Hash(q, 2, 3) - 0.5f, WorldKit.Hash(q, 3, 3) - 0.5f) * 0.9f * p4;
            if (cOn)
            {
                float k = CUBE / Mathf.Max(0.01f, cubeM.RendererBounds.size.x);
                cubePv.position = cc - Vector3.up * cubeM.RendererBounds.size.y * 0.5f * k;
                cubePv.localScale = Vector3.one * k;
                cubePv.rotation = Quaternion.Euler(Mathf.Sin(t * 0.4f) * 8f, 40f * t + 10500f * p4, Mathf.Sin(t * 0.3f) * 8f);
                Face(cubeGlow, cc);
                cubeGlow.localScale = Vector3.one * (16f + 36f * p4 + 3f * Mathf.Sin(t * 5f));
                cubeGlowMat.color = new Color(0.85f, 0.55f + 0.4f * p4, 1f, 0.12f + 0.55f * p4);
                cubeLamp.transform.position = cc;
                cubeLamp.intensity = Lit * (2f + 10f * p4);
                float rate = 1f + 3f * p;
                float ph = Mathf.Repeat(t * rate, 1f);
                bool ao = ph < 0.3f;
                if (arc.enabled != ao)
                {
                    arc.enabled = ao;
                    arcCore.enabled = ao;
                }
                if (ao)
                {
                    int seed = (int)(t * rate) * 13 + 5;
                    Vector3 a = cc + AstronomicalCube.Dir(seed, 1) * 3f,
                        b = W(new Vector3(0f, 0f, 45f)) + AstronomicalCube.Dir(seed, 2) * 14f;
                    b.y = origin.y;
                    AstronomicalCube.Jag(arc, arcCore, a, b, 0.6f + 1.4f * p, t, seed, new Color(0.8f, 0.55f, 1f, 0.8f));
                }
                if (!paused && dt > 0f)
                {
                    sparkAcc += (6f + 70f * p) * Parts * dt;
                    int n = (int)sparkAcc;
                    sparkAcc -= n;
                    for (int s = 0; s < n; s++)
                    {
                        Vector3 d = UnityEngine.Random.onUnitSphere;
                        Emit(
                            fx,
                            cc + d * CUBE * 0.5f,
                            d * UnityEngine.Random.Range(2f, 8f),
                            Color.Lerp(new Color(0.8f, 0.5f, 1f), Color.white, UnityEngine.Random.value * 0.6f),
                            UnityEngine.Random.Range(0.6f, 1.8f),
                            UnityEngine.Random.Range(0.8f, 1.8f)
                        );
                    }
                }
            }
            else if (arc.enabled)
            {
                arc.enabled = false;
                arcCore.enabled = false;
            }
            if (!paused && !boomDone && t >= T_BOOM && t < T_BOOM + 1.5f)
            {
                boomDone = true;
                Vector3 bc = W(new Vector3(0f, 34f, 45f));
                for (int s = 0; s < 700; s++)
                {
                    Vector3 d = UnityEngine.Random.onUnitSphere;
                    Emit(
                        fx,
                        bc,
                        d * UnityEngine.Random.Range(14f, 70f),
                        Color.Lerp(new Color(0.8f, 0.5f, 1f), Color.white, UnityEngine.Random.value),
                        UnityEngine.Random.Range(1.2f, 4f),
                        UnityEngine.Random.Range(1.5f, 3.5f)
                    );
                }
            }
            // ---- the In-Between (1:00:25 - 2:17): white floor + pale dome + floating orbs; the real map is only switched off meanwhile
            SetVoid(t >= T_VOID && t < T_BACK);
            if (voidOn && inbM != null)
            {
                inbPv.position = voidCenter + Vector3.down * 20f + Vector3.up * (1.2f * Mathf.Sin(t * 0.25f));
                inbPv.rotation = Quaternion.Euler(0f, yaw + t * 1.5f, 0f);
            }
            if (voidOn && held && dt > 0f)
            {
                try
                {
                    var rb = GTPlayer.Instance.playerRigidBody;
                    rb.linearVelocity = (rb.linearVelocity - Physics.gravity * dt) * (1f - Mathf.Min(0.9f, 0.7f * dt));
                }
                catch { }
            } // gravity cancelled every frame + a little drag: you hang in the air
            if (voidOn && !paused && dt > 0f)
            {
                motesAcc += 22f * Parts * dt;
                int n = (int)motesAcc;
                motesAcc -= n;
                for (int s = 0; s < n; s++)
                    Emit(
                        motes,
                        head
                            + new Vector3(
                                UnityEngine.Random.Range(-30f, 30f),
                                UnityEngine.Random.Range(-3f, 18f),
                                UnityEngine.Random.Range(-30f, 30f)
                            ),
                        new Vector3(
                            UnityEngine.Random.Range(-0.3f, 0.3f),
                            UnityEngine.Random.Range(0.2f, 0.7f),
                            UnityEngine.Random.Range(-0.3f, 0.3f)
                        ),
                        new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.3f, 0.7f)),
                        UnityEngine.Random.Range(0.3f, 1.1f),
                        UnityEngine.Random.Range(8f, 13f)
                    );
            }
            dome.SetActive(!voidOn);
            // ---- the butterfly: shard burst at 1:48, flies round you, comes to your right hand at 2:15 and stays there until 2:17
            bool bOn = butM != null && t >= T_BUT && t < T_BACK;
            if (butM != null && butM.Pivot.activeSelf != bOn)
                butM.Pivot.SetActive(bOn);
            butGlow.gameObject.SetActive(bOn);
            butGlow2.gameObject.SetActive(bOn);
            if (bOn)
            {
                float ang = (t - T_BUT) * 1.0f + yaw * Mathf.Deg2Rad,
                    R = Mathf.Lerp(5f, 2.4f, Sm(T_BUT, T_FLY - 4f, t)) + 0.6f * Mathf.Sin(t * 0.7f),
                    hh = 0.2f + 0.9f * Mathf.Sin(t * 0.9f) + 0.45f * Mathf.Sin(t * 2.1f);
                Vector3 orbit = head + new Vector3(Mathf.Cos(ang) * R, hh, Mathf.Sin(ang) * R),
                    hand = HandPos(head + Vector3.down * 0.4f) + Vector3.up * 0.06f;
                float w = Sm(T_FLY, T_LAND, t);
                Vector3 pos = Vector3.Lerp(orbit, hand, w);
                if (t >= T_LAND)
                    pos = hand + Vector3.up * 0.01f * Mathf.Sin(t * 3f);
                float grow = Sm(T_BUT, T_BUT + 2f, t),
                    size = Mathf.Lerp(0.55f, 0.17f, w) * grow;
                Vector3 vel = pos - lastBut;
                if (!butRotInit || back)
                {
                    butRot = Quaternion.identity;
                    butRotInit = true;
                    lastBut = pos;
                    vel = Vector3.zero;
                }
                Quaternion want =
                    vel.sqrMagnitude > 1e-6f && t < T_LAND
                        ? Quaternion.LookRotation(vel.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 0f)
                        : Quaternion.Euler(0f, cam != null ? cam.transform.eulerAngles.y : yaw, 0f);
                butRot = Quaternion.Slerp(butRot, want, Mathf.Clamp01(dt * 6f + 0.02f));
                lastBut = pos;
                butPv.position = pos;
                butPv.rotation = butRot;
                butPv.localScale = Vector3.one * size;
                flapT += dt * Mathf.Lerp(1.4f, 0.45f, w);
                if (butM.Clips.Count > 0)
                    butM.Sample(butM.Clips[0], flapT);
                Face(butGlow, pos);
                butGlow.localScale = Vector3.one * Mathf.Lerp(3.2f, 0.9f, w) * grow;
                butGlowMat.color = new Color(0.6f, 0.9f, 1f, 0.5f * grow);
                Face(butGlow2, pos);
                butGlow2.localScale = Vector3.one * Mathf.Lerp(7f, 1.8f, w) * grow * (1f + 0.08f * Mathf.Sin(t * 2.2f));
                butGlow2Mat.color = new Color(0.62f, 0.45f, 1f, 0.3f * grow);
                butLamp.transform.position = pos;
                butLamp.intensity = Lit * 2.5f * grow;
                if (!paused && !butDone && t >= T_BUT && t < T_BUT + 1.5f)
                {
                    butDone = true;
                    for (int s = 0; s < 160; s++)
                    {
                        Vector3 d = UnityEngine.Random.onUnitSphere;
                        Emit(
                            fx,
                            pos + d * 1.5f,
                            -d * 2f + UnityEngine.Random.insideUnitSphere,
                            Color.Lerp(new Color(0.5f, 0.8f, 1f), Color.white, UnityEngine.Random.value),
                            UnityEngine.Random.Range(0.3f, 0.9f),
                            UnityEngine.Random.Range(1f, 2f)
                        );
                    }
                }
                if (!paused && dt > 0f)
                {
                    butAcc += 26f * Parts * dt;
                    int n = (int)butAcc;
                    butAcc -= n;
                    for (int s = 0; s < n; s++)
                        Emit(
                            fx,
                            pos + UnityEngine.Random.insideUnitSphere * 0.3f,
                            UnityEngine.Random.insideUnitSphere * 0.4f,
                            Color.Lerp(new Color(0.5f, 0.8f, 1f), new Color(0.9f, 0.6f, 1f), UnityEngine.Random.value),
                            UnityEngine.Random.Range(0.12f, 0.35f),
                            UnityEngine.Random.Range(0.8f, 1.6f)
                        );
                }
            }
            // ---- the white flashes: the explosion (1:00) and the one that sends you back (2:17)
            float fa = Mathf.Max(
                Sm(T_BOOM - 0.4f, T_BOOM, t) * (1f - Sm(T_BOOM + 0.8f, T_BOOM + 3f, t)),
                Sm(T_BACK - 0.35f, T_BACK, t) * (1f - Sm(T_BACK + 0.5f, T_BACK + 3f, t))
            );
            if (flashGo.activeSelf != fa > 0.003f)
                flashGo.SetActive(fa > 0.003f);
            if (fa > 0.003f)
            {
                flashMat.color = new Color(1f, 1f, 1f, fa);
                flashGo.transform.position = cam != null ? cam.transform.position : head;
            }
            if (!paused && dt > 0f)
            {
                rumbleT += dt;
                if (rumbleT > 0.15f)
                {
                    rumbleT = 0f;
                    float v = t < T_BOOM ? 0.05f + 0.55f * p4 : (t < T_BOOM + 1.2f ? 0.9f : 0f);
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
            // atmosphere: violet while the cube spins, bright and pale in the In-Between
            RenderSettings.ambientLight = voidOn
                ? new Color(0.8f, 0.82f, 0.9f)
                : Color.Lerp(ambientSave, new Color(0.3f, 0.2f, 0.45f), p) * (1f + 0.5f * p4);
            if (t - skyT > 0.1f || t < skyT)
            {
                skyT = t;
                UpdateSky(t);
            }
        }

        public string DebugInfo()
        {
            return "t " + lastT.ToString("0.0") + "  void " + voidOn + "  cube " + (cubeM != null) + "  butterfly " + (butM != null);
        }

        public void Cleanup()
        {
            try
            {
                SetVoid(false);
            }
            catch { }
            AstronomicalForestHider.Restore();
            if (ambientSaved)
            {
                RenderSettings.ambientLight = ambientSave;
                ambientSaved = false;
            }
            foreach (var kv in origMats)
                if (kv.Key != null)
                    kv.Key.sharedMaterials = kv.Value;
            origMats.Clear();
            foreach (var mdl in new[] { cubeM, butM, inbM })
                if (mdl != null && mdl.Pivot != null)
                {
                    mdl.Pivot.transform.SetParent(null, false);
                    mdl.Pivot.SetActive(false);
                    AstronomicalEventMod.ParkModel(mdl.Pivot);
                }
            cubeM = butM = inbM = null;
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
            voidOn = false;
            butRotInit = false;
        }
    }
}
