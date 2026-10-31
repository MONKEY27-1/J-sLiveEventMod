using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // THE END (Fortnite Chapter 2 finale, 4 Dec 2021), milestone 1: 0:00 - 4:36 on the island around you (overlay on the CURRENT map, nothing is loaded or replaced).
    // 0:04 the blue cube at Guava Fort, 0:52 the loopers defend it, 1:45 the Cube Queen's beam opens the rift over her pyramid, 2:08 the Last Reality fleet (motherships + the Cube's Cradle) arrives,
    // 2:00-3:40 cube monsters and caretakers march on the fort (explosions + knockback), 3:45 abductors, 4:15 they attack the cube's shield, 4:36 the cube is destroyed, then black (the I.O. facility scenes come in milestone 2).
    // Everything is a pure function of the music time t; one-shots (explosions) fire when t passes them and are reset when you scrub back.
    internal sealed partial class AstronomicalEnd21 : IEventScene, IDebug
    {
        const float T_RIFT = 95f, T_FLEET = 125f, T_MON = 138f, T_CARE = 195f, T_ABD = 225f, T_SHIELD = 255f, T_BREAK = 260f, T_CUBE_DIE = 265f, T_BLACK = 270f;
        static float Sm(float a, float b, float x) { return AstronomicalCube.Sm(a, b, x); }
        struct Mon { public float T0, X0, Z0, Tx, Tz, Speed, T1, Seed, Scale, Die; public bool Big; public GameObject Go; public CRig Rig; public bool Boomed; public float Hp, Killed, KX, KZ, VX, VZ, HitT; public int Hits; public Vector3 KillP; }
        const float SHIELD_R = 32f;
        bool seated; float hitGlow; readonly List<float> hitTimes = new List<float>();

        AstronomicalEventDef def;
        GameObject root, skyGo, flashGo;
        WorldKit kit;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        float skyT = -9f, lastT = -1f, yaw, shake, rumbleT, farSave = -1f;
        Vector3 originW;
        bool built, paused, ambientSaved, fogSave;
        Color ambientSave;
        int floorLayer;
        Texture2D glowTex, lineTex;
        Material flashMat;
        ParticleSystem fx, dust;
        Light sun, flashLamp, riftLamp;
        GltfModel cubeM, careM, queenM, shipM, abdM, cradleM, pyrM;
        readonly List<GltfModel> models = new List<GltfModel>();
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Mon> mons = new List<Mon>();
        readonly List<GameObject> ships = new List<GameObject>(), abds = new List<GameObject>(), clones = new List<GameObject>();
        readonly List<LineRenderer> beams = new List<LineRenderer>();
        LineRenderer riftRing, riftRing2, mainBeam, bolt;
        Transform riftDisc, orb, shield, cubeT;
        GameObject queenGo;
        CRig queenRig;
        bool cubeBoom, shieldBoom, skyLogged;
        float lastBolt;

        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }

        public bool TryViewpoint(out Vector3 pos, out float y) { pos = originW + Vector3.up * 1.2f; y = yaw; return root != null; }
        public string DebugInfo() { return "t " + lastT.ToString("0.0") + "  monsters " + mons.Count + "  caretakers " ; }

        Transform Quad(string n, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(root.transform, false);
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform;
        }
        void Face(Transform t, Vector3 pos) { t.position = pos; var cam = Camera.main; if (cam != null) t.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up); }
        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams(); ep.position = pos; ep.velocity = vel; ep.startColor = c; ep.startSize = size; ep.startLifetime = life; ps.Emit(ep, 1);
        }
        ParticleSystem MakePS(string n, int max, float grav, Material mat)
        {
            var ps = new GameObject(n).AddComponent<ParticleSystem>(); ps.transform.SetParent(root.transform, false); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pm = ps.main; pm.maxParticles = max; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 2f; pm.gravityModifier = grav;
            var em = ps.emission; em.enabled = false; ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; ps.Play(); return ps;
        }
        LineRenderer MakeLine(string n, Material m, int pts, float width)
        {
            var g = new GameObject(n); g.transform.SetParent(root.transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = pts; l.enabled = false; l.shadowCastingMode = ShadowCastingMode.Off;
            l.sharedMaterial = m; l.widthMultiplier = width; return l;
        }
        // unlit copies of the model's own textures (A_ materials stay see-through); originals are put back before the model is parked
        void Brighten(GltfModel mdl, float k, bool lit = false)
        {
            foreach (var r in mdl.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; var src = r.sharedMaterials; origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src)); var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    bool alpha = src[i] != null && src[i].name.StartsWith("A_");
                    Material mt = alpha ? kit.Glow(new Color(1f, 1f, 1f, 0.5f), null, 3000) : lit ? kit.Lit(Color.white * Mathf.Min(k, 1.25f), null, 0.28f) : kit.Flat(Color.white * k);
                    try { mt.SetFloat("_Cull", 0f); mt.doubleSidedGI = true; } catch { }
                    try { if (src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; else if (src[i] != null && !alpha) mt.color = src[i].color * k; } catch { }     // untextured models (the pyramid) keep their base colours
                    nw[i] = mt;
                }
                r.sharedMaterials = nw;
            }
        }
        float GroundY(float x, float z)
        {
            RaycastHit h; Vector3 p = W(new Vector3(x, 300f, z));
            if (Physics.Raycast(p, Vector3.down, out h, 700f, ~0, QueryTriggerInteraction.Ignore) && h.point.y > originW.y - 40f) return h.point.y - originW.y;
            return 0f;
        }
        GltfModel Take(string id, float bright, bool lit = false)
        {
            var m = def.Prop(id); if (m == null || m.Pivot == null) { AstronomicalEventMod.Log.LogWarning("End21: prop '" + id + "' missing"); return null; }
            m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false); Brighten(m, bright, lit); models.Add(m); return m;
        }
        GameObject Clone(GltfModel m, string name)
        {
            var g = UnityEngine.Object.Instantiate(m.Pivot, root.transform); g.name = name; g.SetActive(false); clones.Add(g); return g;
        }
        static GltfModel.Clip FindClip(GltfModel m, string n) { if (m == null) return null; var c = m.FindClipExact(n); return c ?? (m.Clips.Count > 0 ? m.Clips[0] : null); }
        static float Hash(int i, int k) { return WorldKit.Hash(i, k, 7); }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false; mons.Clear();
            Vector3 head = anchorPos;
            try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            int m = LocoMask(); floorLayer = 0; for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) { floorLayer = i; break; }
            RaycastHit h; originW = head; Vector3 org = head;   // the Forest has no gazebo any more (its spawn is the stump tree room): the force field is centred on where you start. (yaw is re-chosen below: the most open direction from the gazebo)
            try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, m, QueryTriggerInteraction.Ignore)) originW = h.point; else originW = new Vector3(org.x, head.y - 1.2f, org.z);
            {   // face the longest free line of sight (the Forest's spawn is a tree house: the camera there looks at a wall)
                float best = -1f; Vector3 eye = originW + Vector3.up * 2.2f;
                for (int k = 0; k < 24; k++) { float a = k * 15f; Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward; float free = 90f; RaycastHit hh; if (Physics.Raycast(eye, d, out hh, 90f, ~0, QueryTriggerInteraction.Ignore)) free = hh.distance; Vector3 d2 = Quaternion.Euler(0f, a + 8f, 0f) * Vector3.forward; if (Physics.Raycast(eye + Vector3.up * 3f, d2, out hh, 90f, ~0, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, hh.distance + 15f); if (free > best) { best = free; yaw = a; } }
                AstronomicalEventMod.Log.LogInfo("End21: open direction " + yaw + " deg, free " + best.ToString("0") + " m");
            }
            root = new GameObject("AstronomicalEnd21"); root.transform.position = originW; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            AstronomicalDebug.L2W = v => W(v); AstronomicalDebug.Anchors.Clear(); AstronomicalDebug.Anchors["cube"] = () => W(new Vector3(0f, 4f, 9f)); AstronomicalDebug.Anchors["pyr"] = () => pyrM != null && pyrM.Pivot != null ? pyrM.Pivot.transform.position + Vector3.up * 120f : Vector3.zero; AstronomicalDebug.Anchors["queen"] = () => queenGo != null ? queenGo.transform.position : Vector3.zero; AstronomicalDebug.Anchors["ship"] = () => ships.Count > 0 && ships[0] != null ? ships[0].transform.position : Vector3.zero; AstronomicalDebug.Anchors["mon"] = () => { foreach (var m in mons) if (m.Go != null && m.Go.activeSelf) return m.Go.transform.position + Vector3.up * 3f; return W(new Vector3(0f, 3f, 60f)); };
            BuildSky(); BuildEffects(); BuildModels(); P2Start(); AstronomicalDebug.FireHook = (o, d) => Shoot(o, d);
            var fg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(fg.GetComponent<Collider>()); fg.name = "AstronomicalEnd21Flash"; fg.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3500); var fr = fg.GetComponent<Renderer>(); fr.sharedMaterial = flashMat; fr.shadowCastingMode = ShadowCastingMode.Off; fr.allowOcclusionWhenDynamic = false; flashGo = fg; flashGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight; ambientSaved = true; fogSave = RenderSettings.fog; RenderSettings.fog = false;       // the Forest's green fog hides everything beyond ~100 m (pyramid, fleet)
            try { if (cam != null) { farSave = cam.farClipPlane; cam.farClipPlane = Mathf.Max(farSave, 6000f); } } catch { }
            lastT = -1f; built = true;
            AstronomicalEventMod.Log.LogInfo("End21 built at " + originW + ": cube " + (cubeM != null) +  ", caretaker " + (careM != null) + ", ship " + (shipM != null) + ", abductor " + (abdM != null) + ", cradle " + (cradleM != null) + ", pyramid " + (pyrM != null) + ", queen " + (queenM != null));
        }

        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI * 0.55f / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 2800f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray();
            skyGo = new GameObject("AstronomicalEnd21Sky"); skyGo.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = skyGo.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 2501)); dm.mainTexture = null; dm.renderQueue = 2501; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        // sunset over the island that darkens to dusk from 0:50; from the ring (1:35) the sky bleeds red and purple; black inside the rooms; twilight and then a bright blue day (after the 10:03 flash) in the ocean
        void UpdateSky(float t)
        {
            if (skyMesh == null) return; Sc sc = SceneAt(t);
            float dusk = Sm(50f, 95f, t), rift = Sm(T_RIFT, T_RIFT + 40f, t), dark = Sm(T_ABD - 20f, T_CUBE_DIE, t) * 0.5f;
            Color top = Color.Lerp(new Color(0.22f, 0.12f, 0.38f), new Color(0.1f, 0.06f, 0.22f), dusk), mid = Color.Lerp(new Color(0.85f, 0.4f, 0.33f), new Color(0.4f, 0.16f, 0.3f), dusk), hor = Color.Lerp(new Color(1f, 0.62f, 0.3f), new Color(0.72f, 0.3f, 0.22f), dusk);
            top = Color.Lerp(top, new Color(0.25f, 0.04f, 0.14f), rift); mid = Color.Lerp(mid, new Color(0.75f, 0.2f, 0.14f), rift); hor = Color.Lerp(hor, new Color(1f, 0.38f, 0.1f), rift);
            float black = IsRoom(sc) ? 1f : 0f; if (t < T_FAC) black = Sm(T_CUBE_DIE, T_BLACK, t); if (sc == Sc.Base && t >= T_CHAM - 3f) black = 0.05f;      // the chamber's glass roof shows the red sky
            if (sc == Sc.Ocean) { float blue = Sm(T_END_VIS - 0.2f, T_END_VIS + 1.5f, t); top = Color.Lerp(new Color(0.07f, 0.05f, 0.18f), new Color(0.2f, 0.45f, 0.95f), blue); mid = Color.Lerp(new Color(0.3f, 0.16f, 0.35f), new Color(0.5f, 0.75f, 1f), blue); hor = Color.Lerp(new Color(0.62f, 0.3f, 0.3f), new Color(0.86f, 0.94f, 1f), blue); dark = 0f; }
            for (int k = 0; k < skyDirs.Length; k++)
            {
                var d = skyDirs[k]; float up = Mathf.Clamp01(d.y); var c = up < 0.3f ? Color.Lerp(hor, mid, up / 0.3f) : Color.Lerp(mid, top, (up - 0.3f) / 0.7f);
                c = Color.Lerp(c, Color.black, dark * 0.5f); c = Color.Lerp(c, Color.black, black); c.a = 1f; skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        void BuildEffects()
        {
            fx = MakePS("EFx", 5000, 0.25f, kit.Glow(Color.white, glowTex, 3004)); dust = MakePS("EDust", 2000, -0.01f, kit.Glow(new Color(0.9f, 0.7f, 0.55f, 0.5f), glowTex, 3001));
            sun = new GameObject("ESun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 1f; sun.color = new Color(1f, 0.7f, 0.45f); sun.transform.rotation = Quaternion.Euler(14f, yaw + 160f, 0f);
            flashLamp = new GameObject("EFlash").AddComponent<Light>(); flashLamp.transform.SetParent(root.transform, false); flashLamp.type = LightType.Point; flashLamp.range = 140f; flashLamp.intensity = 0f; flashLamp.shadows = LightShadows.None;
            riftLamp = new GameObject("ERiftLight").AddComponent<Light>(); riftLamp.transform.SetParent(root.transform, false); riftLamp.type = LightType.Point; riftLamp.range = 700f; riftLamp.color = new Color(1f, 0.45f, 0.15f); riftLamp.intensity = 0f; riftLamp.shadows = LightShadows.None;
            var add = kit.Glow(new Color(1f, 0.55f, 0.2f, 1f), lineTex, 3001); riftRing = MakeLine("ERift", add, 90, 14f); riftRing2 = MakeLine("ERift2", kit.Glow(new Color(1f, 0.9f, 0.7f, 1f), lineTex, 3002), 90, 5f);
            mainBeam = MakeLine("EBeam", kit.Glow(new Color(1f, 0.75f, 0.4f, 1f), lineTex, 3001), 2, 30f); bolt = MakeLine("EBolt", kit.Glow(new Color(1f, 0.85f, 0.6f, 1f), lineTex, 3002), 12, 4f);
            riftDisc = Quad("ERiftDisc", kit.Glow(new Color(1f, 0.4f, 0.1f, 0.0f), glowTex, 3000)); riftDisc.gameObject.SetActive(false);
            orb = Quad("EOrb", kit.Glow(new Color(1f, 0.6f, 0.2f, 0f), glowTex, 3003)); orb.gameObject.SetActive(false);
            shield = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; UnityEngine.Object.Destroy(shield.GetComponent<Collider>()); shield.name = "EShield"; shield.SetParent(root.transform, false);
            var sr = shield.GetComponent<Renderer>(); sr.sharedMaterial = kit.Glow(new Color(0.35f, 0.75f, 1f, 0.22f), null, 3002); sr.shadowCastingMode = ShadowCastingMode.Off; shield.gameObject.SetActive(false);
        }

        void BuildModels()
        {
            cubeM = Take("bluecube", 1.2f); careM = Take("caretaker", 1.25f, true); queenM = Take("cube_queen", 1.25f, true); shipM = Take("mothership", 1.2f); abdM = Take("abductor", 1.5f); cradleM = Take("cradle", 1.2f); pyrM = Take("pyramid", 1.2f);
            if (cubeM != null) { cubeT = cubeM.Pivot.transform; // cyan cube: a procedural digital-circuit texture (the loaded one is deep blue, and tinting a blue texture stays blue)
                var cy = kit.Tex(64, 64, (x, y) => { float n = Mathf.Abs(Mathf.Sin(x * 0.7f + y * 0.31f)) * Mathf.Abs(Mathf.Cos(y * 0.9f - x * 0.2f)); bool grid = (x % 16 < 1) || (y % 16 < 1); float b = 0.55f + 0.3f * n + (grid ? 0.25f : 0f); return new Color(0.1f * b, Mathf.Min(1f, 0.95f * b), Mathf.Min(1f, 1.05f * b), 1f); });
                foreach (var r in cubeM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var mt in r.sharedMaterials) if (mt != null) { mt.mainTexture = cy; mt.color = new Color(1f, 1f, 1f, 1f); } }
            if (pyrM != null) foreach (var r in pyrM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var mt in r.sharedMaterials) if (mt != null && mt.mainTexture == null) { Color c = mt.color; mt.color = c.g > 0.5f ? new Color(1f, 0.55f, 0.12f, 1f) : new Color(0.38f, 0.06f, 0.12f, 1f); }      // the Queen's pyramid: dark crimson frame, glowing orange cap (the file's colours are flat magenta / yellow)
            if (cradleM != null)      // the Cube's Cradle with its own materials: a dark steel shell (lit, so its shape reads against the sky), the magenta digital-rain interior and the gold cube glow (unlit, bright)
                foreach (var kv in origMats)
                {
                    var r = kv.Key; if (r == null || !r.transform.IsChildOf(cradleM.Pivot.transform)) continue; var src = kv.Value; var nw = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string n = src[i] != null ? src[i].name : ""; bool glow = n.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0;
                        Material mt = glow ? kit.Flat(Color.white * 1.9f) : kit.Lit(new Color(0.24f, 0.22f, 0.32f), null, 0.45f); try { mt.SetFloat("_Cull", 0f); } catch { }      // double-sided: the x-mirror of the loader turns the shell's faces inside out, culling would show only its far inner wall
                        if (glow && src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; nw[i] = mt;
                    }
                    r.sharedMaterials = nw;
                }
            if (careM != null)
            {
                // the waves: 26 small caretakers arrive from 2:46 on, 6 big ones (10 m) from 3:15; they walk / run to the force field, hit it, then die
                for (int i = 0; i < 32; i++)
                {
                    bool big = i >= 26; int k = big ? i - 26 : i; float arrive = big ? 195f + k * 7.5f : 166f + k * 3.3f + Hash(i, 1) * 1.6f; float a0 = (Hash(i, 2) - 0.5f) * 2.4f; float r0 = 120f + Hash(i, 3) * 90f;
                    float x0 = Mathf.Sin(a0 * 0.9f) * r0, z0 = Mathf.Cos(a0 * 0.9f) * r0, ta = a0 * 0.45f, tx = Mathf.Sin(ta) * (SHIELD_R + 3f), tz = Mathf.Cos(ta) * (SHIELD_R + 3f); float sp = big ? 4.8f : 6.0f + Hash(i, 6) * 2.0f; float dist = Mathf.Sqrt((x0 - tx) * (x0 - tx) + (z0 - tz) * (z0 - tz));
                    float t1 = arrive, t0 = t1 - dist / sp, die = t1 + (big ? 6f : 3f + Hash(i, 9) * 2f);
                    var g = Clone(careM, "ECare" + i); mons.Add(new Mon { T0 = t0, X0 = x0, Z0 = z0, Tx = tx, Tz = tz, Speed = sp, T1 = t1, Die = die, Big = big, Scale = (big ? 10f : 6f) / 1.8f, Seed = Hash(i, 8) * 9f, Go = g, Rig = CRig.Of(careM, g), Hp = big ? 10f : 3f, Killed = -1f, HitT = -9f });
                }
            }
            if (shipM != null) for (int i = 0; i < 7; i++) ships.Add(i == 0 ? shipM.Pivot : Clone(shipM, "EShip" + i));
            if (abdM != null) for (int i = 0; i < 10; i++) abds.Add(i == 0 ? abdM.Pivot : Clone(abdM, "EAbd" + i));
            for (int i = 0; i < 10; i++) beams.Add(MakeLine("EAbdBeam" + i, kit.Glow(new Color(1f, 0.3f, 0.9f, 1f), lineTex, 3001), 2, 3f));
            if (queenM != null) { queenGo = queenM.Pivot; queenRig = CRig.Of(queenM, queenGo); }
        }
        // one clip per time window, crossfaded (clip names come from tools/anim_human2.py: H_ + name)
        static void PlayClip(CRig rig, GltfModel m, string a, float ta, string b = null, float tb = 0f, float w = 0f)
        {
            var ca = FindClip(m, a); if (ca == null) return; var cb = b != null && w > 0.001f ? FindClip(m, b) : null;
            if (cb != null && cb.Channels.Count == ca.Channels.Count) rig.Pose(ca, ta, cb, tb, w); else rig.Pose(ca, ta);
        }
        // the Cube Queen's schedule: floats, then the casting / taunting / raging moments of the fight
        string QueenClip(float t, out float lt)
        {
            lt = t;
            if (t < 88f) return "H_Float"; if (t < 104f) { lt = t - 88f; return "H_Cast"; } if (t < 126f) return "H_Float"; if (t < 138f) { lt = t - 126f; return "H_Taunt"; } if (t < 195f) return "H_Float"; if (t < 205f) { lt = t - 195f; return "H_Point"; }
            if (t < 209f) return "H_Float"; if (t < 219f) { lt = t - 209f; return "H_Rage"; } if (t < 252f) return "H_Float"; if (t < 258f) { lt = t - 252f; return "H_Cast"; } if (t < 266f) { lt = t - 258f; return "H_Retreat"; }
            if (t > 405f && t < 415f) { lt = t - 405f; return "H_Taunt"; } return "H_Float";
        }
        // teleports you into the force field (once), and pushes you back whenever you reach its wall
        void KeepInside(float t, float cx = 0f, float cz = 0f, float rad = -1f)
        {
            try
            {
                var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Vector3 head = pl.headCollider.transform.position;
                if (!seated) { if (Vector3.Distance(head, originW) < 3f) { seated = true; return; } Vector3 target = originW + Vector3.up * 1.2f; rb.position += target - head; rb.linearVelocity = Vector3.zero; seated = true;  AstronomicalEventMod.Log.LogInfo("End21: you are inside the force field at " + target); return; }
                Vector3 cen = root.transform.TransformPoint(new Vector3(cx, 0f, cz)); Vector3 flat = head - cen; flat.y = 0f; float lim = (rad > 0f ? rad : SHIELD_R) - 1.6f;
                if (flat.magnitude > lim) { Vector3 inward = -flat.normalized; rb.position += inward * (flat.magnitude - lim); Vector3 v = rb.linearVelocity; float outv = Vector3.Dot(v, -inward); if (outv > 0f) rb.linearVelocity = v + inward * outv; hitGlow = Mathf.Max(hitGlow, 0.5f); }
            }
            catch { }
        }
        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { fx, dust }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }

        void Boom(Vector3 wp, float size, float power, Color? tint = null)
        {
            Color a = tint ?? new Color(1f, 0.55f, 0.15f);
            for (int k = 0; k < (int)(70 * Parts + 20); k++) { Vector3 d = UnityEngine.Random.onUnitSphere; d.y = Mathf.Abs(d.y); Emit(fx, wp, d * UnityEngine.Random.Range(4f, 24f) * size * 0.3f, Color.Lerp(a, new Color(1f, 0.9f, 0.5f), UnityEngine.Random.value), size * UnityEngine.Random.Range(0.4f, 1.2f), UnityEngine.Random.Range(0.8f, 1.8f)); }
            for (int k = 0; k < 16; k++) Emit(dust, wp + UnityEngine.Random.insideUnitSphere * size, new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(2f, 9f), UnityEngine.Random.Range(-3f, 3f)), new Color(0.35f, 0.3f, 0.28f, 0.6f), size * 2.4f, UnityEngine.Random.Range(2f, 4f));
            flashLamp.transform.position = wp; flashLamp.color = Color.Lerp(a, Color.white, 0.3f); flashLamp.intensity = Lit * 8f * power;
            AstronomicalHazards.Blast(wp, 70f, 5f * power, 8f * power); shake = Mathf.Max(shake, 0.2f * power);
        }
        Vector3 HeadNow() { try { return GTPlayer.Instance.headCollider.transform.position; } catch { } return originW + Vector3.up * 1.2f; }
        void Black(float a, Camera cam, Color c) { if (flashGo.activeSelf != a > 0.003f) flashGo.SetActive(a > 0.003f); if (a > 0.003f) { flashMat.color = new Color(c.r, c.g, c.b, a); flashGo.transform.position = cam != null ? cam.transform.position : originW; } }

        static void PlaceOn(GltfModel m, Vector3 pos, float scale, float yawDeg, bool on)
        {
            if (m == null || m.Pivot == null) return; if (m.Pivot.activeSelf != on) m.Pivot.SetActive(on); if (!on) return;
            var tr = m.Pivot.transform; tr.localPosition = pos; tr.localRotation = Quaternion.Euler(0f, yawDeg, 0f); tr.localScale = Vector3.one * scale;
        }
        static void PlaceGo(GameObject g, Vector3 pos, float scale, Quaternion rot, bool on)
        {
            if (g == null) return; if (g.activeSelf != on) g.SetActive(on); if (!on) return; var tr = g.transform; tr.localPosition = pos; tr.localRotation = rot; tr.localScale = Vector3.one * scale;
        }

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (!built || root == null) return;
            var cam = Camera.main; float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f); bool back = t < lastT - 0.4f; float tp = lastT < 0f ? t : lastT; lastT = t;
            Sc sc = SceneAt(t); if (back) { seated = false; glassGone = false; cubeBoom = shieldBoom = false; for (int i = 0; i < mons.Count; i++) { var m = mons[i]; m.Boomed = false; m.Killed = -1f; m.Hp = m.Big ? 10f : 3f; m.Hits = 0; m.HitT = -9f; m.KX = m.KZ = m.VX = m.VZ = 0f; mons[i] = m; } }
            if (cam != null) skyGo.transform.position = cam.transform.position;
            if (t - skyT > 0.15f || t < skyT) { skyT = t; UpdateSky(t); if (!skyLogged) { skyLogged = true; var smr = skyGo.GetComponent<MeshRenderer>(); AstronomicalEventMod.Log.LogInfo("End21 sky: active " + skyGo.activeInHierarchy + " renderer " + smr.enabled + " pos " + skyGo.transform.position + " col " + skyCols[skyCols.Length / 2] + " shader " + smr.sharedMaterial.shader.name + " queue " + smr.sharedMaterial.renderQueue + " far " + (cam != null ? cam.farClipPlane : 0f)); } }
            float rift = Sm(T_RIFT, T_RIFT + 14f, t), fleetK = Sm(T_FLEET, T_FLEET + 8f, t), alive = 1f - Sm(T_CUBE_DIE, T_CUBE_DIE + 0.5f, t), black = Sm(T_CUBE_DIE + 1.5f, T_BLACK, t);
            float ambK = t < T_FAC ? Mathf.Max(0.05f, 1f - black) : 0.55f, sunK = t < T_FAC ? 1f - black : 0.55f;
            if (sc == Sc.Ocean) { float bl = Sm(T_END_VIS - 0.2f, T_END_VIS + 1.5f, t); ambK = Mathf.Lerp(0.4f, 1.15f, bl); sunK = Mathf.Lerp(0.3f, 1.15f, bl); }
            RenderSettings.ambientLight = Color.Lerp(new Color(0.75f, 0.55f, 0.5f), new Color(0.45f, 0.2f, 0.25f), rift) * ambK; if (sc == Sc.Ocean && t >= T_END_VIS) RenderSettings.ambientLight = new Color(0.7f, 0.8f, 0.95f) * ambK;
            sun.color = Color.Lerp(new Color(1f, 0.7f, 0.45f), new Color(1f, 0.4f, 0.25f), rift); if (sc == Sc.Ocean && t >= T_END_VIS) sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = sunK;
            // ----- the cube (0:04) in the middle of the force field that covers the gazebo
            Vector3 cubeP = new Vector3(0f, GroundY(0f, 9f), 9f);
            if (cubeM != null && cubeM.Pivot != null)
            {
                float scale = 7f / Mathf.Max(0.2f, cubeM.RendererBounds.size.y); float spawn = Sm(0.5f, 3.5f, t);
                PlaceOn(cubeM, cubeP + Vector3.up * (3.6f + 0.3f * Mathf.Sin(t * 0.8f)), scale * Mathf.Max(0.01f, spawn), t * 6f, t < T_CUBE_DIE);
            }
            Vector3 cubeC = cubeP + Vector3.up * 3.6f;
            // the force field: a dome (radius SHIELD_R) over the gazebo; you cannot walk out of it; the monsters beat on it (it glows where they hit), the abductors break it at 4:30
            bool shOn = t > 0.5f && t < T_BREAK + 0.2f;
            if (shield.gameObject.activeSelf != shOn) shield.gameObject.SetActive(shOn);
            hitGlow = Mathf.Max(0f, hitGlow - dt * 2.5f);
            if (shOn) { float hit = Sm(T_SHIELD, T_BREAK, t); shield.localPosition = new Vector3(0f, 0f, 0f); shield.localScale = Vector3.one * (SHIELD_R * 2f * (1f + 0.004f * Mathf.Sin(t * 3f))); var smat = shield.GetComponent<Renderer>().sharedMaterial; float g = Mathf.Clamp01(hit * (0.6f + 0.4f * Mathf.Sin(t * 30f)) + hitGlow); smat.color = new Color(Mathf.Lerp(0.2f, 1f, g), Mathf.Lerp(0.9f, 0.35f, g), 1f - 0.4f * g, 0.08f + 0.06f * Sm(228f, 236f, t) + 0.3f * g); }
            if (!shieldBoom && t >= T_BREAK && !paused) { shieldBoom = true; Boom(W(cubeC + Vector3.up * 14f), 22f, 1.6f, new Color(0.4f, 0.9f, 1f)); }
            if (!cubeBoom && t >= T_CUBE_DIE && !paused) { cubeBoom = true; Boom(W(cubeC), 30f, 3.2f, new Color(0.3f, 0.9f, 1f)); for (int k = 0; k < 300; k++) { var d = UnityEngine.Random.onUnitSphere; Emit(fx, W(cubeC) + d * 3f, d * UnityEngine.Random.Range(10f, 50f), new Color(0.2f + 0.3f * UnityEngine.Random.value, 0.95f, 1f, 1f), UnityEngine.Random.Range(0.6f, 3f), UnityEngine.Random.Range(1.5f, 3.5f)); } shake = 3f; flashLamp.intensity = Lit * 30f; }
            // you are brought into the force field at 0:00 and kept inside until it breaks
            if (t < T_BREAK && !paused) KeepInside(t); else if (sc == Sc.Surface && !paused) { seated = true; KeepInside(t, stageX, stageZ, 16f); }
            if (!paused && tp < 210f && t >= 210f) { flashLamp.transform.position = W(cubeC + Vector3.up * 4f); flashLamp.color = new Color(0.4f, 0.8f, 1f); flashLamp.intensity = Lit * 22f; hitGlow = 1f; shake = Mathf.Max(shake, 1f); for (int k = 0; k < 160; k++) { float a2 = k / 160f * 6.2832f; Emit(fx, W(cubeC), new Vector3(Mathf.Cos(a2), 0.15f * Mathf.Sin(k), Mathf.Sin(a2)) * UnityEngine.Random.Range(14f, 30f), new Color(0.3f, 0.9f, 1f, 1f), UnityEngine.Random.Range(0.6f, 1.6f), UnityEngine.Random.Range(1.2f, 2.4f)); } }      // 3:30: a bright blue energy burst round the cube
            // ----- the Cube Queen's pyramid (always in the distance), her energy orb on top, the rift above it
            bool oc = sc == Sc.Ocean; Vector3 pyrP = oc ? new Vector3(OC_PX, OC_PY, OC_PZ) : new Vector3(40f, GroundY(40f, 650f), 650f); float pyrS = oc ? OC_S : 1.3f, apex = 150f * pyrS;      // in the ocean the Queen's tower rises straight out of the sea (the reference: a huge spire under a burning ring)
            PlaceOn(pyrM, pyrP, pyrS, 20f, t < T_END_VIS); PlaceOn(spireM, pyrP, pyrS, 0f, false);      // the same pyramid in the sea (the Queen's tower, she stands in its orb the whole event)
            Vector3 orbP = pyrP + Vector3.up * (apex + 35f), riftC = pyrP + (oc ? new Vector3(0f, apex + 90f, -120f) : new Vector3(0f, 700f, -120f));
            bool orbOn = t > T_RIFT - 8f && t < T_END_VIS; if (orb.gameObject.activeSelf != orbOn) orb.gameObject.SetActive(orbOn);
            if (orbOn) { Face(orb, orbP); float os = (oc ? 260f : 95f) * Sm(T_RIFT - 8f, T_RIFT + 4f, t) * (1f + 0.1f * Mathf.Sin(t * 9f)); orb.localScale = new Vector3(os, os, 1f); var om = orb.GetComponent<Renderer>().sharedMaterial; om.color = new Color(1f, 0.6f, 0.25f, Mathf.Clamp01(rift + 0.4f)); }
            bool riftOn = t > T_RIFT && t < T_END_VIS; bool beamPre = t > 90f && t <= T_RIFT && sc == Sc.Island; riftRing.enabled = riftRing2.enabled = riftOn; mainBeam.enabled = riftOn || beamPre; if (riftDisc.gameObject.activeSelf != riftOn) riftDisc.gameObject.SetActive(riftOn);
            riftLamp.transform.position = W(riftC + new Vector3(0f, -100f, -200f)); riftLamp.intensity = riftOn ? Lit * 6f * rift : 0f;
            if (riftOn)
            {
                float R = (oc ? 640f : 330f) * rift * (1f + 0.03f * Mathf.Sin(t * 2f)); Vector3 cp = W(riftC); Vector3 right = root.transform.right, tilt = Quaternion.AngleAxis(oc ? -78f : -35f, right) * Vector3.up;
                for (int k = 0; k < 90; k++) { float a = k / 89f * Mathf.PI * 2f + t * 0.3f; var ofs = right * Mathf.Cos(a) * R + Vector3.Cross(right, tilt).normalized * Mathf.Sin(a) * R * 0.5f + tilt * Mathf.Sin(a * 3f + t * 2f) * 6f; riftRing.SetPosition(k, cp + ofs); riftRing2.SetPosition(k, cp + ofs * 0.96f); }
                Face(riftDisc, cp); riftDisc.localScale = new Vector3(R * 2f, R * 1.1f, 1f); riftDisc.GetComponent<Renderer>().sharedMaterial.color = new Color(1f, 0.42f, 0.12f, 0.42f * rift);
                mainBeam.SetPosition(0, W(orbP)); mainBeam.SetPosition(1, cp); mainBeam.widthMultiplier = 26f * rift * (1f + 0.12f * Mathf.Sin(t * 14f));
                if (oc) { mainBeam.SetPosition(0, W(new Vector3(-1500f, WY + 70f, 600f))); mainBeam.SetPosition(1, W(pyrP + Vector3.up * (apex * 0.42f))); mainBeam.widthMultiplier = 14f * (1f + 0.15f * Mathf.Sin(t * 11f)); }      // the red beam streaking in from the left
                if (t - lastBolt > 0.18f && !paused) { lastBolt = t; bolt.enabled = true; Vector3 a0 = cp + UnityEngine.Random.insideUnitSphere * R * 0.6f, b0 = a0 + new Vector3(UnityEngine.Random.Range(-300f, 300f), -UnityEngine.Random.Range(250f, 520f), UnityEngine.Random.Range(-300f, 300f)); for (int k = 0; k < 12; k++) bolt.SetPosition(k, Vector3.Lerp(a0, b0, k / 11f) + (k > 0 && k < 11 ? UnityEngine.Random.insideUnitSphere * 22f : Vector3.zero)); }
            }
            else if (beamPre) { mainBeam.SetPosition(0, W(orbP)); mainBeam.SetPosition(1, W(orbP + Vector3.up * 1800f)); mainBeam.widthMultiplier = 22f * Sm(90f, 94f, t) * (1f + 0.1f * Mathf.Sin(t * 14f)); bolt.enabled = false; }
            else bolt.enabled = false;
            if (t > 85f && t < T_RIFT && sc == Sc.Island && !paused && t - lastBolt > 0.55f + 0.5f * Hash((int)(t * 3f), 4)) { lastBolt = t; bolt.enabled = true; Vector3 a0 = pyrP + new Vector3(UnityEngine.Random.Range(-260f, 260f), 1100f, UnityEngine.Random.Range(-260f, 260f)), b0 = pyrP + new Vector3(UnityEngine.Random.Range(-120f, 120f), 0f, UnityEngine.Random.Range(-120f, 120f)); for (int k = 0; k < 12; k++) bolt.SetPosition(k, W(Vector3.Lerp(a0, b0, k / 11f) + (k > 0 && k < 11 ? UnityEngine.Random.insideUnitSphere * 40f : Vector3.zero))); flashLamp.transform.position = W(b0 + Vector3.up * 40f); flashLamp.color = new Color(0.8f, 0.85f, 1f); flashLamp.intensity = Lit * 14f; }
            // ----- the Cube Queen: floats above her pyramid from the rift opening to the end
            bool qOn = queenM != null && t > T_RIFT - 8f && t < T_END_VIS && sc != Sc.Ocean;
            if (queenM != null && sc != Sc.Ocean) { PlaceGo(queenGo, orbP + Vector3.up * (-14f + 1.5f * Mathf.Sin(t * 0.7f)), 28f / 1.8f * Sm(T_RIFT + 6f, T_RIFT + 16f, t), Quaternion.Euler(0f, yaw * 0f + 180f + 8f * Mathf.Sin(t * 0.3f), 0f), qOn); if (qOn) { float lt; string qc = QueenClip(t, out lt); PlayClip(queenRig, queenM, qc, lt); } }
            // ----- the Last Reality fleet: the Cube's Cradle and the motherships
            bool crOn = cradleM != null && t > T_FLEET - 4f && t < T_END_VIS && !IsRoom(sc) && sc != Sc.Ocean;
            float cTilt = -38f, cYaw = 8f * Mathf.Sin(t * 0.05f), cRoll = 0f; if (AstronomicalDebug.Tweak[4] > 0.5f) { cTilt = AstronomicalDebug.Tweak[5]; cYaw = AstronomicalDebug.Tweak[6]; cRoll = AstronomicalDebug.Tweak[7]; }      // (test console: tweak 4 1 / 5 tilt / 6 yaw / 7 roll)
            if (cradleM != null) PlaceGo(cradleM.Pivot, new Vector3(-60f, 520f, 1500f), 0.12f * Sm(T_FLEET - 4f, T_FLEET + 6f, t), Quaternion.Euler(cTilt, cYaw, cRoll), crOn);      // its open side faces you (tilted down a little) so the magenta digital interior and the gold cube show
            for (int i = 0; i < ships.Count; i++)
            {
                float arrive = T_FLEET + i * 1.6f; float k = Sm(arrive, arrive + 9f, t); bool on = t > arrive && t < T_END_VIS && !IsRoom(sc); float ang = (i / (float)ships.Count) * Mathf.PI * 1.1f - 0.55f * Mathf.PI * 1.1f + 0.1f * Mathf.Sin(t * 0.2f + i);
                Vector3 hover = new Vector3(Mathf.Sin(ang) * 760f, 430f + 80f * (i % 3) + 12f * Mathf.Sin(t * 0.4f + i), 520f + Mathf.Cos(ang) * 420f - 150f + (i % 2) * 120f);
                if (oc) { float aa = i / (float)ships.Count * 6.2832f + 0.4f; hover = riftC + new Vector3(Mathf.Cos(aa) * 520f, Mathf.Sin(aa) * 140f + 230f, -60f + Mathf.Sin(aa) * 60f); }      // the saucers hang in and around the burning ring
                Vector3 pos = Vector3.Lerp(riftC, hover, oc ? 1f : k) + (i == 0 ? Vector3.up * 1500f * Mathf.Pow(Sm(424f, 430f, t), 2f) : Vector3.zero);
                PlaceGo(ships[i], pos, (oc ? 0.5f : 0.62f) * k, Quaternion.Euler(Mathf.Sin(t * 0.5f + i) * 3f, t * 2.5f + i * 40f, Mathf.Cos(t * 0.45f + i) * 3f), on);
            }
            // ----- the caretaker waves: walk / run to the force field, beat on it (it flashes), then die; the big ones slam it
            for (int i = 0; i < mons.Count; i++)
            {
                var mo = mons[i]; bool shotDead = mo.Killed >= 0f; float dieT = shotDead ? mo.Killed : mo.Die; bool on = t >= mo.T0 && t < dieT + 7f && t < T_CUBE_DIE; if (mo.Go.activeSelf != on) mo.Go.SetActive(on);
                if (on)
                {
                    float k = Mathf.Clamp01((t - mo.T0) / Mathf.Max(0.1f, mo.T1 - mo.T0)); Vector3 p = new Vector3(Mathf.Lerp(mo.X0, mo.Tx, k), 0f, Mathf.Lerp(mo.Z0, mo.Tz, k)); Vector3 dir = new Vector3(mo.Tx - mo.X0, 0f, mo.Tz - mo.Z0);
                    if (!shotDead && (mo.KX != 0f || mo.KZ != 0f || mo.VX != 0f || mo.VZ != 0f)) { if (dt > 0f && !paused) { mo.KX += mo.VX * dt; mo.KZ += mo.VZ * dt; float dm = Mathf.Exp(-3f * dt), sp = Mathf.Exp(-0.7f * dt); mo.VX *= dm; mo.VZ *= dm; mo.KX *= sp; mo.KZ *= sp; } p.x += mo.KX; p.z += mo.KZ; }      // shot: pushed back, then it walks on
                    p.y = GroundY(p.x, p.z); if (shotDead) p = mo.KillP;
                    var tr = mo.Go.transform; tr.localPosition = p; tr.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f); tr.localScale = Vector3.one * mo.Scale;
                    if (shotDead) PlayClip(mo.Rig, careM, "H_Death", t - mo.Killed);
                    else if (mo.HitT >= 0f && t - mo.HitT >= 0f && t - mo.HitT < 0.5f && (!mo.Big || mo.Hits % 3 == 0)) PlayClip(mo.Rig, careM, "H_Stagger", t - mo.HitT);
                    else if (t < mo.T1) { bool run = !mo.Big && t > mo.T0 + 0.5f * (mo.T1 - mo.T0) && mo.Speed > 6.5f; float ph = (t - mo.T0) * (run ? 1f / 0.9f : 1f / 1.35f) * 1.0f + mo.Seed; PlayClip(mo.Rig, careM, run ? "H_Run" : "H_Walk", ph); }
                    else if (t < mo.Die) { float lt = t - mo.T1; PlayClip(mo.Rig, careM, mo.Big ? "H_Slam" : "H_Attack", mo.Big ? lt % 4.5f : lt % 3f); if (!paused && ((int)(lt * 0.9f + mo.Seed) != (int)(tp * 0.9f + mo.Seed))) { hitGlow = 1f; Boom(W(new Vector3(mo.Tx, p.y + (mo.Big ? 6f : 3f), mo.Tz)), mo.Big ? 6f : 3f, mo.Big ? 0.9f : 0.4f, new Color(0.4f, 0.9f, 1f)); } }
                    else PlayClip(mo.Rig, careM, "H_Death", t - mo.Die);
                }
                if (!mo.Boomed && !shotDead && t >= mo.Die && t < T_CUBE_DIE && !paused && tp < mo.Die + 3f) { mo.Boomed = true; Boom(W(new Vector3(mo.Tx, GroundY(mo.Tx, mo.Tz) + 3f, mo.Tz)), mo.Big ? 10f : 5f, mo.Big ? 1.4f : 0.7f, new Color(1f, 0.55f, 0.2f)); }
                mons[i] = mo;
            }
            // ----- abductors circle the cube from 3:45 and fire at its shield from 4:15
            for (int i = 0; i < abds.Count; i++)
            {
                float arrive = 140f + i * 1.4f; float far = Sm(arrive, arrive + 10f, t), near = Sm(T_ABD + i * 1.0f, T_ABD + i * 1.0f + 8f, t); bool on = t > arrive && t < T_END_VIS && !IsRoom(sc); float ang = t * 0.35f + i * 0.63f; float rad = Mathf.Lerp(Mathf.Lerp(950f, 420f, far), 85f + 12f * (i % 3), near);
                Vector3 pos = cubeC + new Vector3(Mathf.Cos(ang) * rad, Mathf.Lerp(Mathf.Lerp(330f, 200f, far), 55f + 8f * (i % 4), near), Mathf.Sin(ang) * rad * 0.8f - 20f);
                PlaceGo(abds[i], pos, 0.1f * Mathf.Lerp(1.9f, 1f, near), Quaternion.Euler(Mathf.Sin(t + i) * 6f, -ang * Mathf.Rad2Deg, Mathf.Cos(t * 0.8f + i) * 6f), on);
                bool fire = on && t > T_SHIELD - 1f + i * 0.3f && t < T_BREAK + 6f; if (beams[i].enabled != fire) beams[i].enabled = fire;
                if (fire) { Vector3 a = W(pos + Vector3.down * 3f), b = W(cubeC + (pos - cubeC).normalized * 14f); beams[i].SetPosition(0, a); beams[i].SetPosition(1, b); beams[i].widthMultiplier = 2.5f + 1.5f * Mathf.Sin(t * 22f + i); }
                if (fire && !paused && dt > 0f && UnityEngine.Random.value < dt * 10f) Emit(fx, W(cubeC + (pos - cubeC).normalized * 14f), UnityEngine.Random.insideUnitSphere * 8f, new Color(1f, 0.5f, 0.95f, 1f), UnityEngine.Random.Range(0.8f, 2.2f), 0.6f);
            }
            // ----- after the cube is gone: black until the next milestone
            WeaponFrame(t, dt, sc); Color bcol; float pb = P2Black(t, out bcol); if (pb >= 0f) black = pb; else bcol = Color.black; P2Frame(t, dt, tp, cam);
            Black(black, cam, bcol); flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 20f * Lit);
            if (!paused && dt > 0f) { rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float v = Mathf.Max(shake * 0.4f, 0f); if (v > 0.05f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(v), 0.14f); gt.StartVibration(false, Mathf.Clamp01(v), 0.14f); } } catch { } } } }
            shake = Mathf.Max(0f, shake - dt * 1.4f);
        }

        public void Cleanup()
        {
            try { P2Cleanup(); } catch { } AstronomicalDebug.FireHook = null; boltPool.Clear(); boltAge.Clear();
            if (ambientSaved) { RenderSettings.ambientLight = ambientSave; RenderSettings.fog = fogSave; ambientSaved = false; }
            try { if (farSave > 0f && Camera.main != null) Camera.main.farClipPlane = farSave; } catch { }
            farSave = -1f; foreach (var kv in origMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; origMats.Clear();
            foreach (var g in clones) if (g != null) UnityEngine.Object.Destroy(g); clones.Clear(); mons.Clear(); ships.Clear(); abds.Clear(); beams.Clear();
            foreach (var mdl in models) if (mdl != null && mdl.Pivot != null) { foreach (var bc in mdl.Pivot.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(bc); mdl.Pivot.transform.SetParent(null, false); mdl.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(mdl.Pivot); }
            models.Clear(); cubeM = careM = queenM = shipM = abdM = cradleM = pyrM = null;
            if (flashGo != null) UnityEngine.Object.Destroy(flashGo); flashGo = null; if (skyGo != null) UnityEngine.Object.Destroy(skyGo); skyGo = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null;
        }
    }
}
