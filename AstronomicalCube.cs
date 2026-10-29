using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // "Kevin the Cube" mini event (Fortnite Chapter 1 Season 5, August 2018), from the user's reading of the video: the sky rift opens when the video starts, the first lightning hits the ground at 0:10 and does not stop
    // until 0:25, the cube starts to appear at 0:23 and is complete at 0:25, the rift disappears at 0:33. The cube then stays (floating, glowing, throwing sparks) until the user stops it. Pure function of the music time.
    internal sealed class CubePart
    {
        public const float CUBE_SIZE = 4f;
        public GameObject Root; public GltfModel Model; public Transform Pivot, Shell, Glow; public Light Lamp; public ParticleSystem Fx; public WorldKit Kit; public BoxCollider Col; public Material GlowMat, ShellMat, ArcMat; public LineRenderer Arc, ArcCore; public Texture2D GlowTex, LineTex;
        float lastRing = -9f; public float T0; int arcSeed;
        public static CubePart Create(GltfModel m, Vector3 at, int layer)
        {
            var c = new CubePart { Model = m, Kit = new WorldKit { Layer = layer } }; c.GlowTex = c.Kit.GlowTex(); c.LineTex = c.Kit.Tex(4, 32, (x, y) => { float v = Mathf.Abs((y + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            c.Root = new GameObject("AstronomicalKevin"); c.Root.transform.position = at; c.Pivot = m.Pivot.transform; c.Pivot.SetParent(c.Root.transform, false); m.Pivot.SetActive(true); c.Pivot.localPosition = Vector3.zero;
            float k = CUBE_SIZE / Mathf.Max(0.01f, m.RendererBounds.size.x); c.Pivot.localScale = Vector3.one * k;
            foreach (var r in m.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; foreach (var mt in r.sharedMaterials) if (mt != null) { mt.DisableKeyword("_EMISSION"); mt.SetColor("_EmissionColor", Color.black); } }          // the file's emissive factor is white: left on, the whole cube is a white block
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(sh.GetComponent<Collider>()); sh.name = "KevinShell"; sh.transform.SetParent(c.Pivot, false); sh.transform.localPosition = new Vector3(0f, m.RendererBounds.size.y * 0.5f, 0f); sh.transform.localScale = Vector3.one * m.RendererBounds.size.x * 1.07f;
            c.ShellMat = c.Kit.Glow(new Color(0.75f, 0.4f, 1f, 0.12f), null, 3003); sh.GetComponent<Renderer>().sharedMaterial = c.ShellMat; sh.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; c.Shell = sh.transform;
            var gq = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(gq.GetComponent<Collider>()); gq.name = "KevinGlow"; gq.transform.SetParent(c.Root.transform, false); c.GlowMat = c.Kit.Glow(new Color(0.85f, 0.55f, 1f, 0.6f), c.GlowTex, 3002); gq.GetComponent<Renderer>().sharedMaterial = c.GlowMat; gq.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; c.Glow = gq.transform;
            var lg = new GameObject("KevinLight"); lg.transform.SetParent(c.Root.transform, false); c.Lamp = lg.AddComponent<Light>(); c.Lamp.type = LightType.Point; c.Lamp.color = new Color(0.75f, 0.45f, 1f); c.Lamp.range = 70f; c.Lamp.intensity = 0f; c.Lamp.shadows = LightShadows.None;
            var fg = new GameObject("KevinFx"); fg.transform.SetParent(c.Root.transform, false); c.Fx = fg.AddComponent<ParticleSystem>(); c.Fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = c.Fx.main; pm.maxParticles = 1500; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 2f; pm.gravityModifier = 0.05f;
            var em = c.Fx.emission; em.enabled = false; fg.GetComponent<ParticleSystemRenderer>().sharedMaterial = c.Kit.Glow(Color.white, c.GlowTex, 3004); c.Fx.Play();
            var ap = c.Kit.Glow(Color.white, c.LineTex, 3001); var ag = new GameObject("KevinArc"); ag.transform.SetParent(c.Root.transform, false); c.Arc = ag.AddComponent<LineRenderer>(); c.Arc.useWorldSpace = true; c.Arc.positionCount = 14; c.Arc.sharedMaterial = ap; c.Arc.enabled = false;
            var ag2 = new GameObject("KevinArcCore"); ag2.transform.SetParent(ag.transform, false); c.ArcCore = ag2.AddComponent<LineRenderer>(); c.ArcCore.useWorldSpace = true; c.ArcCore.positionCount = 14; c.ArcCore.sharedMaterial = c.Kit.Glow(Color.white, c.LineTex, 3002); c.ArcCore.enabled = false;
            var cg = new GameObject("KevinCollider"); cg.layer = layer; cg.transform.SetParent(c.Pivot, false); c.Col = cg.AddComponent<BoxCollider>(); c.Col.center = new Vector3(0f, m.RendererBounds.size.y * 0.5f, 0f); c.Col.size = m.RendererBounds.size * 0.98f;
            return c;
        }
        // t = seconds of the event (the same function runs while the event plays and, afterwards, as the persistent cube)
        public void Update(float t, Vector3 ground, bool eventRunning, float dt)
        {
            float app = AstronomicalCube.Sm(23f, 25f, t), rise = AstronomicalCube.Sm(25f, 31f, t); bool vis = t >= 23f; if (Root.activeSelf != true) Root.SetActive(true); Pivot.gameObject.SetActive(vis); Shell.gameObject.SetActive(vis); Glow.gameObject.SetActive(t >= 22f);
            float flick = t < 25f ? 1f + 0.04f * Mathf.Sin(t * 70f) * (1f - app) : 1f; float s = (0.12f + 0.88f * app) * flick; float bob = Mathf.Sin(t * 0.7f) * 0.8f * rise;
            float hover = 0.2f + 2.5f * rise + bob * 0.3f; Pivot.position = ground + Vector3.up * hover; Pivot.localScale = Vector3.one * (CUBE_SIZE / Mathf.Max(0.01f, Model.RendererBounds.size.x)) * s;
            float spin = t * 5f + (t < 25f ? (25f - t) * (25f - t) * 6f : 0f); Pivot.rotation = Quaternion.Euler(Mathf.Sin(t * 0.35f) * 4f, spin, Mathf.Sin(t * 0.27f + 1f) * 4f);
            float cy = hover + CUBE_SIZE * 0.5f * s; Vector3 centre = ground + Vector3.up * cy; Glow.position = centre; var cam = Camera.main; if (cam != null) Glow.rotation = Quaternion.LookRotation(Glow.position - cam.transform.position, Vector3.up);
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.6f), fl = t >= 25f && t < 25.6f ? 1f - (t - 25f) / 0.6f : 0f; Glow.localScale = Vector3.one * (14f * Mathf.Max(0.05f, s) * (0.85f + 0.3f * pulse) + 24f * fl); GlowMat.color = new Color(0.85f, 0.55f, 1f, (0.12f + 0.1f * pulse) * Mathf.Clamp01((t - 22f) / 3f) + 0.4f * fl);
            ShellMat.color = new Color(0.75f, 0.4f, 1f, 0.05f + 0.07f * pulse + 0.3f * fl); Lamp.transform.position = centre; Lamp.intensity = Mathf.Max(0f, (t - 22f)) < 3f ? Mathf.Clamp01((t - 22f) / 3f) * 4f * AstronomicalSettings.Lighting.Value : (3f + 1.5f * pulse + 8f * fl) * AstronomicalSettings.Lighting.Value;
            Col.enabled = vis && app > 0.9f;
            if (dt <= 0f) return;
            float par = AstronomicalSettings.Particles.Value; int n = (int)(vis ? (6 + 14 * app) * par * dt * 60f / 60f * 60f * 0.05f * 10f : 0); n = vis ? Mathf.Min(8, (int)(Mathf.Max(0.5f, 6f * par) * dt * 60f * 0.2f + UnityEngine.Random.value)) : 0;
            for (int k = 0; k < n; k++) { var ep = new ParticleSystem.EmitParams(); Vector3 dir = UnityEngine.Random.onUnitSphere; ep.position = centre + dir * CUBE_SIZE * 0.5f * s; ep.velocity = dir * UnityEngine.Random.Range(1f, 4f) + Vector3.up * 1f; ep.startColor = Color.Lerp(new Color(0.8f, 0.5f, 1f), Color.white, UnityEngine.Random.value * 0.5f); ep.startSize = UnityEngine.Random.Range(0.5f, 1.4f); ep.startLifetime = UnityEngine.Random.Range(1.2f, 2.6f); Fx.Emit(ep, 1); }
            // little arcs from the cube to the ground (and, while the rift is open, up to it) every second or so
            float arcPh = (t * 1.1f) % 1f; bool arc = vis && t >= 26f && arcPh < 0.3f; if (Arc.enabled != arc) { Arc.enabled = arc; ArcCore.enabled = arc; }
            if (arc) { int seed = (int)(t * 1.1f) * 17 + 3; Vector3 a = centre + AstronomicalCube.Dir(seed, 1) * 2f, b = (seed & 1) == 0 ? ground + AstronomicalCube.Dir(seed, 2) * 7f : centre + AstronomicalCube.Dir(seed, 3) * 10f + Vector3.up * 5f; if ((seed & 1) == 0) b.y = ground.y; AstronomicalCube.Jag(Arc, ArcCore, a, b, 0.7f, t, seed, new Color(0.8f, 0.55f, 1f, 0.8f)); }
        }
        public void Destroy()
        {
            if (Model != null && Model.Pivot != null) { Model.Pivot.transform.SetParent(null, false); Model.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(Model.Pivot); }
            if (Root != null) UnityEngine.Object.Destroy(Root); if (Kit != null) foreach (var o in Kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); Root = null;
        }
    }

    internal sealed class AstronomicalCube : IEventScene, IDebug
    {
        const float RIFT_H = 240f, RIFT_R = 95f, T_LIGHT = 10f, T_FULL = 25f, T_OFF = 33f;
        static CubePart persisted; static float persistT;
        public static float Sm(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }
        public static Vector3 Dir(int seed, int k) { return new Vector3(WorldKit.Hash(seed, k, 1) - 0.5f, WorldKit.Hash(seed, k, 2) * 0.3f, WorldKit.Hash(seed, k, 3) - 0.5f).normalized; }
        public static void Jag(LineRenderer glow, LineRenderer core, Vector3 a, Vector3 b, float width, float t, int seed, Color c)
        {
            var d = b - a; float len = d.magnitude; if (len < 0.1f) return; var dn = d / len; var s1 = Vector3.Cross(dn, Vector3.up); if (s1.sqrMagnitude < 0.01f) s1 = Vector3.right; s1.Normalize(); var s2 = Vector3.Cross(dn, s1); int n = glow.positionCount; float amp = Mathf.Min(len * 0.06f, 8f); int q = (int)(t * 24f);
            for (int k = 0; k < n; k++) { float f = k / (float)(n - 1), env = Mathf.Sin(f * Mathf.PI); var p = Vector3.Lerp(a, b, f) + (s1 * (WorldKit.Hash(seed + q, k, 7) - 0.5f) + s2 * (WorldKit.Hash(seed + q, k + 40, 7) - 0.5f)) * 2f * amp * env; glow.SetPosition(k, p); core.SetPosition(k, p); }
            glow.widthMultiplier = width * 2.6f; core.widthMultiplier = width * 0.7f; glow.startColor = glow.endColor = new Color(c.r, c.g, c.b, 0.75f); core.startColor = core.endColor = new Color(1f, 1f, 1f, 0.95f);
        }
        public static void ClearPersistent() { if (persisted != null) { persisted.Destroy(); persisted = null; } }
        public static void PersistTick() { if (persisted == null) return; persistT += Time.deltaTime; try { persisted.Update(persistT, persistedGround, false, Time.deltaTime); } catch { } }
        static Vector3 persistedGround;

        AstronomicalEventDef def; GameObject root, dome; WorldKit kit; Vector3 origin; float yaw; int floorLayer; bool built, paused; CubePart cube; readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Transform rift1, rift2, riftGlow, impactGlow; Material riftMat, riftMat2, impactMat, scorchMat; Mesh riftMesh, skyMesh; Color[] skyCols; Vector3[] skyDirs; int skyN; float skyT = -9f; Texture2D glowTex, lineTex, swirlA, swirlB;
        LineRenderer bolt, boltCore; LineRenderer[] forks = new LineRenderer[4], forkCores = new LineRenderer[4], crack = new LineRenderer[8], crackCore = new LineRenderer[8], rings = new LineRenderer[3]; float[] ringT = { -9f, -9f, -9f }; int ringNext; float lastRing = -9f;
        ParticleSystem fx, stars; Light strike, sun; Vector3 ambientSave; bool ambientSaved; float lastT = -1f, rumbleT, flash, endT = 51f; Transform scorch; GameObject viewDummy; bool blasted25;
        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false; endT = def != null && def.Length > 5f ? def.Length : 51f; ClearPersistent();
            Vector3 head = anchorPos; try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            Vector3 org = head; try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            floorLayer = 0; for (int i = 0; i < 32; i++) if ((LocoMask() & (1 << i)) != 0) { floorLayer = i; break; } RaycastHit h; Vector3 ground = org; if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, LocoMask(), QueryTriggerInteraction.Ignore)) { ground = h.point; floorLayer = h.collider.gameObject.layer; } else ground.y = org.y;
            origin = ground; root = new GameObject("AstronomicalCubeEvent"); root.transform.position = origin; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f); kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            swirlA = Swirl(new Color(0.35f, 0.12f, 0.7f), new Color(0.8f, 0.45f, 1f), new Color(1f, 0.95f, 1f), 0.15f, 1f); swirlB = Swirl(new Color(0.2f, 0.25f, 0.8f), new Color(0.55f, 0.55f, 1f), new Color(0.95f, 0.95f, 1f), 0.6f, 0.85f);
            BuildRift(); BuildBolts(); BuildSky();
            var m = def.Prop("cube"); if (m != null && m.Pivot != null) cube = CubePart.Create(m, origin, floorLayer); else AstronomicalEventMod.Log.LogWarning("Cube: model missing");
            sun = new GameObject("CubeSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0f;
            ambientSave = new Vector3(RenderSettings.ambientLight.r, RenderSettings.ambientLight.g, RenderSettings.ambientLight.b); ambientSaved = true; lastT = -1f; blasted25 = false; built = true;
            AstronomicalEventMod.Log.LogInfo("Kevin the Cube built at " + origin + " layer " + floorLayer + " cube " + (cube != null));
        }
        static bool fitOk; static Vector3 fitO; static float fitY;
        public bool TryViewpoint(out Vector3 pos, out float y)
        {
            pos = Vector3.zero; y = 0f; if (root != null) { pos = root.transform.position - root.transform.forward * 22f + Vector3.up * 1.2f; y = yaw; return true; } return false;
        }
        Texture2D Swirl(Color a, Color b, Color c, float seed, float alpha)
        {
            return kit.Tex(128, 128, (x, y) => { float u = x / 128f, v = y / 128f, T = Mathf.PI * 2f; float n = Mathf.Sin(T * (u * 3f + 0.25f * Mathf.Sin(T * (v * 2f + seed)))) + Mathf.Sin(T * (v * 4f + 0.3f * Mathf.Sin(T * u * 3f + seed))) + Mathf.Sin(T * ((u + v) * 2f + 0.4f * Mathf.Sin(T * v * 3f))); n = n / 3f * 0.5f + 0.5f; var col = n < 0.5f ? Color.Lerp(a, b, n * 2f) : Color.Lerp(b, c, (n - 0.5f) * 2f); col.a = alpha * (0.35f + 0.65f * n); return col; });
        }
        Mesh DiscMesh(int seg, int rings)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var cols = new List<Color>(); var tr = new List<int>();
            for (int k = 0; k <= rings; k++) for (int j = 0; j <= seg; j++) { float r = k / (float)rings, a = j * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * r * 0.5f * 1.6f, 0.5f + Mathf.Sin(a) * r * 0.5f * 1.6f)); cols.Add(new Color(1, 1, 1, Mathf.Clamp01((1f - r * r) * 1.6f) * (r < 0.12f ? r / 0.12f * 0.4f + 0.6f : 1f))); }
            for (int k = 0; k < rings; k++) for (int j = 0; j < seg; j++) { int a = k * (seg + 1) + j, b = a + 1, c = a + seg + 1, d = c + 1; tr.AddRange(new[] { a, c, b, b, c, d }); }
            var me = new Mesh(); me.SetVertices(v); me.SetUVs(0, uv); me.SetColors(cols); me.SetTriangles(tr, 0); me.RecalculateBounds(); owned.Add(me); return me;
        }
        void BuildRift()
        {
            riftMesh = DiscMesh(64, 10); riftMat = kit.Glow(Color.white, swirlA, 2995); riftMat2 = kit.Glow(Color.white, swirlB, 2996);
            Func<string, Material, Transform> mk = (n, mat) => { var g = new GameObject(n); g.transform.SetParent(root.transform, false); g.AddComponent<MeshFilter>().sharedMesh = riftMesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false; return g.transform; };
            rift1 = mk("Rift1", riftMat); rift2 = mk("Rift2", riftMat2); var gq = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(gq.GetComponent<Collider>()); gq.name = "RiftGlow"; gq.transform.SetParent(root.transform, false); impactMat = kit.Glow(new Color(0.9f, 0.7f, 1f, 0.6f), glowTex, 3000); gq.GetComponent<Renderer>().sharedMaterial = impactMat; riftGlow = gq.transform;
            var ig = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(ig.GetComponent<Collider>()); ig.name = "ImpactGlow"; ig.transform.SetParent(root.transform, false); ig.GetComponent<Renderer>().sharedMaterial = kit.Glow(new Color(1f, 0.85f, 1f, 0.9f), glowTex, 3003); impactGlow = ig.transform;
            var sg = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(sg.GetComponent<Collider>()); sg.name = "Scorch"; sg.transform.SetParent(root.transform, false); sg.transform.localPosition = new Vector3(0f, 0.08f, 0f); sg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); scorchMat = kit.Glow(new Color(0.05f, 0.02f, 0.08f, 0f), glowTex, 2990); sg.GetComponent<Renderer>().sharedMaterial = scorchMat; scorch = sg.transform;
        }
        void BuildBolts()
        {
            var soft = kit.Glow(Color.white, lineTex, 3001); var core = kit.Glow(Color.white, lineTex, 3002);
            Func<string, int, LineRenderer> mk = (n, pts) => { var g = new GameObject(n); g.transform.SetParent(root.transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = pts; l.shadowCastingMode = ShadowCastingMode.Off; l.enabled = false; return l; };
            bolt = mk("Bolt", 40); bolt.sharedMaterial = soft; boltCore = mk("BoltCore", 40); boltCore.sharedMaterial = core;
            for (int i = 0; i < forks.Length; i++) { forks[i] = mk("Fork" + i, 14); forks[i].sharedMaterial = soft; forkCores[i] = mk("ForkCore" + i, 14); forkCores[i].sharedMaterial = core; }
            for (int i = 0; i < crack.Length; i++) { crack[i] = mk("Crack" + i, 14); crack[i].sharedMaterial = soft; crackCore[i] = mk("CrackCore" + i, 14); crackCore[i].sharedMaterial = core; }
            for (int i = 0; i < rings.Length; i++) { var l = mk("Shock" + i, 64); l.loop = true; l.sharedMaterial = soft; rings[i] = l; }
            fx = new GameObject("CubeFx").AddComponent<ParticleSystem>(); fx.transform.SetParent(root.transform, false); fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = fx.main; pm.maxParticles = 4000; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 1.5f; pm.gravityModifier = 0.3f; var em = fx.emission; em.enabled = false; fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004); fx.Play();
            strike = new GameObject("StrikeLight").AddComponent<Light>(); strike.transform.SetParent(root.transform, false); strike.transform.localPosition = new Vector3(0f, 8f, 0f); strike.type = LightType.Point; strike.color = new Color(0.8f, 0.6f, 1f); strike.range = 90f; strike.intensity = 0f; strike.shadows = LightShadows.None;
        }
        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI * 0.52f / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 280f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray(); skyN = skyCols.Length;
            dome = new GameObject("AstronomicalCubeSky"); dome.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = dome.AddComponent<MeshRenderer>(); var dm = new Material(kit.Glow(Color.white, glowTex, 2501)); dm.mainTexture = null; dm.renderQueue = 2501; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = new GameObject("CubeStars").AddComponent<ParticleSystem>(); stars.transform.SetParent(dome.transform, false); stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = stars.main; pm.maxParticles = 800; pm.simulationSpace = ParticleSystemSimulationSpace.Local; pm.playOnAwake = false; pm.startLifetime = 100000f; var em = stars.emission; em.enabled = false; stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004); stars.Play();
            for (int k = 0; k < 520; k++) { var d = UnityEngine.Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.97f + 0.03f; var ep = new ParticleSystem.EmitParams(); ep.position = d.normalized * 270f; ep.velocity = Vector3.zero; ep.startSize = UnityEngine.Random.Range(0.8f, 2.4f); ep.startLifetime = 100000f; float b = UnityEngine.Random.Range(0.5f, 1f); ep.startColor = new Color(b, b, Mathf.Min(1f, b + 0.2f), 1f); stars.Emit(ep, 1); }
        }
        // dusk: deep violet overhead, pink-violet at the horizon, rippled blue-white cloud bands round the rift (like the video), whitened by the lightning
        void UpdateSky(float t, float lightning)
        {
            if (skyMesh == null) return; float dusk = AstronomicalCube.Sm(0f, 5f, t) * (1f - AstronomicalCube.Sm(33f, 44f, t) * 0.65f);
            Color top = Color.Lerp(new Color(0.04f, 0.05f, 0.14f), new Color(0.2f, 0.08f, 0.45f), dusk), hor = Color.Lerp(new Color(0.07f, 0.09f, 0.2f), new Color(0.85f, 0.4f, 0.75f), dusk);
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k]; float el = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)), az = Mathf.Atan2(d.z, d.x); var c = Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.45f));
                float rip = 0.5f + 0.5f * Mathf.Sin(az * 14f + el * 22f + t * 0.8f) * Mathf.Sin(az * 9f - el * 17f + t * 0.5f), band = Mathf.Exp(-((el - 0.95f) * (el - 0.95f)) / 0.18f) * dusk; band *= rip;
                c.r += band * 0.35f + lightning * 0.15f; c.g += band * 0.3f + lightning * 0.1f; c.b += band * 0.75f + lightning * 0.25f; float wisp = Mathf.Clamp01(Mathf.Sin(az * 4f + t * 0.1f) * Mathf.Sin(el * 11f + az * 2f)) * 0.18f * dusk; c.r += wisp * 0.9f; c.g += wisp * 0.4f; c.b += wisp * 0.9f; c.a = 1f; skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { fx, stars }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }
        void Emit(Vector3 lp, Vector3 lv, Color c, float size, float life) { var ep = new ParticleSystem.EmitParams(); ep.position = W(lp); ep.velocity = root.transform.TransformVector(lv); ep.startColor = c; ep.startSize = size; ep.startLifetime = life; fx.Emit(ep, 1); }
        float acc;

        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built) return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f); bool back = t < lastT - 0.4f; if (back || lastT < 0f) { blasted25 = false; skyT = -9f; lastRing = -9f; } lastT = t; var cam = Camera.main; if (cam != null) dome.transform.position = cam.transform.position;
            // the rift: grows from the start, shrinks and goes at 0:33
            float R = RIFT_R * AstronomicalCube.Sm(0f, 7f, t) * (1f - AstronomicalCube.Sm(30f, T_OFF, t)) * (1f + 0.03f * Mathf.Sin(t * 3f)); bool riftOn = R > 0.5f; foreach (var tr in new[] { rift1, rift2, riftGlow }) if (tr.gameObject.activeSelf != riftOn) tr.gameObject.SetActive(riftOn);
            Vector3 top = new Vector3(0f, RIFT_H, 0f);
            if (riftOn) { rift1.localPosition = top; rift1.localScale = new Vector3(R, 1f, R); rift1.localRotation = Quaternion.Euler(0f, t * 14f, 0f); rift2.localPosition = top + Vector3.down * 3f; rift2.localScale = new Vector3(R * 0.8f, 1f, R * 0.8f); rift2.localRotation = Quaternion.Euler(0f, -t * 23f, 0f); riftGlow.position = W(top + Vector3.down * 6f); riftGlow.localScale = Vector3.one * R * 1.1f; if (cam != null) riftGlow.rotation = Quaternion.LookRotation(riftGlow.position - cam.transform.position, Vector3.up); impactMat.color = new Color(0.9f, 0.7f, 1f, 0.35f + 0.25f * Mathf.Sin(t * 5f) + 0.3f * flash); }
            // the lightning: 0:10 - 0:25, thicker and brighter the longer it goes; forks and cracks round the rift
            bool strikeOn = t >= T_LIGHT && t < T_FULL + 0.3f; float inten = AstronomicalCube.Sm(T_LIGHT, T_FULL, t); float onoff = t < T_LIGHT + 2f ? (Mathf.Sin(t * 29f) > -0.2f ? 1f : 0f) : 1f;
            if (bolt.enabled != (strikeOn && onoff > 0f)) { bolt.enabled = boltCore.enabled = strikeOn && onoff > 0f; foreach (var f in forks) f.enabled = strikeOn && onoff > 0f; foreach (var f in forkCores) f.enabled = strikeOn && onoff > 0f; }
            bool ground = strikeOn && onoff > 0f; Vector3 gp = W(Vector3.zero), tp = W(top);
            if (ground)
            {
                var c = Color.Lerp(new Color(0.75f, 0.45f, 1f), Color.white, 0.3f + 0.4f * inten); AstronomicalCube.Jag(bolt, boltCore, tp, gp, 1.3f + 3.2f * inten, t, 11, c);
                for (int i = 0; i < forks.Length; i++) { float f = 0.2f + 0.2f * i; Vector3 a = Vector3.Lerp(tp, gp, f), b = a + (AstronomicalCube.Dir(i * 5 + (int)(t * 12f), i) * 55f + Vector3.down * 25f) * (1f + inten); b.y = Mathf.Max(b.y, gp.y); AstronomicalCube.Jag(forks[i], forkCores[i], a, b, 0.7f + 1.2f * inten, t, 30 + i, c); }
            }
            bool cr = t >= 5f && t < 31f; for (int i = 0; i < crack.Length; i++)
            {
                bool on = cr && riftOn && (WorldKit.Hash(i, (int)(t * 8f), 5) < 0.5f + 0.4f * inten); if (crack[i].enabled != on) { crack[i].enabled = on; crackCore[i].enabled = on; if (!on) continue; } if (!on) continue;
                float a0 = WorldKit.Hash(i, (int)(t * 8f), 9) * 6.2832f; Vector3 p0 = tp + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * R * 0.85f, p1 = tp + new Vector3(Mathf.Cos(a0 + 0.35f), -0.15f, Mathf.Sin(a0 + 0.35f)) * R * 0.45f + Vector3.down * 12f; AstronomicalCube.Jag(crack[i], crackCore[i], W(p0), W(p1), 1.1f, t, 70 + i, new Color(0.8f, 0.6f, 1f));
            }
            // impact: glow, shock rings (they push the player), sparks, scorch, light, flashes, rumble
            float glow = ground ? (0.5f + 0.5f * inten) * (0.8f + 0.2f * Mathf.Sin(t * 40f)) : 0f; bool gOn = glow > 0.01f; if (impactGlow.gameObject.activeSelf != gOn) impactGlow.gameObject.SetActive(gOn);
            if (gOn) { impactGlow.position = gp + Vector3.up * 4f; impactGlow.localScale = Vector3.one * (16f + 26f * inten) * (0.9f + 0.2f * Mathf.Sin(t * 33f)); if (cam != null) impactGlow.rotation = Quaternion.LookRotation(impactGlow.position - cam.transform.position, Vector3.up); }
            scorchMat.color = new Color(0.03f, 0.01f, 0.06f, 0.85f * AstronomicalCube.Sm(T_LIGHT, T_FULL, t)); scorch.localScale = Vector3.one * (10f + 14f * AstronomicalCube.Sm(T_LIGHT, 28f, t));
            if (!paused && ground && t - lastRing > 1.4f) { lastRing = t; ringT[ringNext] = t; ringNext = (ringNext + 1) % rings.Length; AstronomicalHazards.Blast(gp, 38f, 9f + 5f * inten); flash = Mathf.Max(flash, 0.5f + 0.4f * inten); }
            for (int i = 0; i < rings.Length; i++) { float age = t - ringT[i]; bool on = age >= 0f && age < 2.4f; if (rings[i].enabled != on) rings[i].enabled = on; if (!on) continue; float k = age / 2.4f, r = 3f + age * 34f; for (int j = 0; j < 64; j++) { float a = j * Mathf.PI * 2f / 64f; rings[i].SetPosition(j, gp + new Vector3(Mathf.Cos(a) * r, 0.4f, Mathf.Sin(a) * r)); } rings[i].startColor = rings[i].endColor = new Color(0.85f, 0.6f, 1f, 0.9f * (1f - k) * (1f - k)); rings[i].widthMultiplier = 2.4f * (1f - 0.6f * k); }
            if (!paused && t >= T_FULL && !blasted25 && t - T_FULL < 1.5f) { blasted25 = true; flash = 1f; AstronomicalHazards.Blast(gp, 85f, 16f, 16f); ringT[ringNext] = t; ringNext = (ringNext + 1) % rings.Length; }
            flash = Mathf.Max(0f, flash - dt * 2.2f); strike.intensity = Lit * (ground ? (3f + 9f * inten) * (0.7f + 0.5f * onoff) : 0f) + Lit * 10f * flash; strike.transform.position = gp + Vector3.up * 8f;
            if (!paused && dt > 0f)
            {
                if (ground) { acc += (60f + 160f * inten) * Parts * dt; int n = (int)acc; acc -= n; for (int k = 0; k < n; k++) Emit(new Vector3(UnityEngine.Random.Range(-3f, 3f), 1f, UnityEngine.Random.Range(-3f, 3f)), new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(8f, 34f), UnityEngine.Random.Range(-14f, 14f)), Color.Lerp(new Color(0.8f, 0.5f, 1f), Color.white, UnityEngine.Random.value), UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(0.8f, 1.6f)); }
                rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float p = ground ? 0.1f + 0.5f * inten : t >= T_FULL && t < T_FULL + 2f ? 0.9f : 0f; if (p > 0.02f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(p), 0.14f); gt.StartVibration(false, Mathf.Clamp01(p), 0.14f); } } catch { } } }
            }
            // atmosphere
            float dusk = AstronomicalCube.Sm(0f, 5f, t) * (1f - AstronomicalCube.Sm(33f, 44f, t) * 0.5f); RenderSettings.ambientLight = Color.Lerp(new Color(0.3f, 0.3f, 0.35f), new Color(0.3f, 0.2f, 0.5f), dusk) * (0.85f + 0.5f * flash);
            sun.color = new Color(0.7f, 0.5f, 1f); sun.intensity = 0.6f * dusk * Mathf.Max(0.4f, Lit); sun.transform.rotation = Quaternion.Euler(70f, yaw, 0f);
            if (t - skyT > 0.1f || t < skyT) { skyT = t; UpdateSky(t, Mathf.Clamp01(ground ? 0.5f + inten : 0f) + flash); }
            if (cube != null) { cube.Update(t, origin, true, dt); }
        }
        public string DebugInfo() { return "t " + lastT.ToString("0.0") + "  cube " + (cube != null) + "  origin " + origin; }

        public void Cleanup()
        {
            if (ambientSaved) { RenderSettings.ambientLight = new Color(ambientSave.x, ambientSave.y, ambientSave.z); ambientSaved = false; }
            bool natural = lastT >= endT - 1.0f && cube != null;
            if (natural) { persisted = cube; persistedGround = origin; persistT = lastT; cube = null; AstronomicalEventMod.Log.LogInfo("Kevin the Cube stays here until you leave the map"); }
            else if (cube != null) { cube.Destroy(); cube = null; }
            if (dome != null) UnityEngine.Object.Destroy(dome); dome = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null;
        }
    }
}
