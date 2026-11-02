using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // THE GALACTUS EVENT (Fortnite Chapter 2 Season 4 finale, 1 Dec 2020), rebuilt from the user's recording (tools/gx_ref has the frames, tools/gx_frames.py extracts them).
    // 0:00 you stand on the Helicarrier with a countdown (the warning at 0:10), 0:55 Galactus walks out of the clouds, 2:35 lightning, 2:55 his hand sweeps the deck, 3:00 you fall and Iron Man arrives, 3:35 red-purple energy
    // converges under his hand, 4:30 white flash, 4:30 - 7:00 you ride the Stark Battle Bus in a fleet of buses to his chest, 7:00 - 8:25 the bus flies through his red-lit insides, 8:30 space, 8:50 Iron Man, 9:05 the fleet
    // goes for his face, 9:35 the green payload blows him apart, 9:45 a cosmic explosion, 10:05 Agent Jones on an office floor, 10:20 "In Season Five". An OVERLAY at 800 m over the spawn (the Forest's own geometry is not
    // there); you walk on the Helicarrier's deck, then you are carried (colliders off, gravity cancelled) on the bus roof. Every frame is a pure function of the music time t; one-shots fire when t passes them.
    internal sealed partial class AstronomicalGalactus : IEventScene, IDebug
    {
        enum Sc { Deck, Arrive, Reach, Fall, BusIn, Assault, Inside, Space, Mouth, Blast, Dark, Office, Promo }
        sealed class Seg { public float T0, T1; public Sc S; }
        static Seg G(float t0, float t1, Sc s) { return new Seg { T0 = t0, T1 = t1, S = s }; }
        static readonly Seg[] SEGS = { G(0f, 55f, Sc.Deck), G(55f, 120f, Sc.Arrive), G(120f, 180f, Sc.Reach), G(180f, 270f, Sc.Fall), G(270f, 330f, Sc.BusIn), G(330f, 420f, Sc.Assault), G(420f, 510f, Sc.Inside), G(510f, 535f, Sc.Space), G(535f, 575f, Sc.Mouth),
            G(575f, 600f, Sc.Blast), G(600f, 605f, Sc.Dark), G(605f, 620f, Sc.Office), G(620f, 700f, Sc.Promo) };
        const float T_END = 650.9f, IY = 800f;
        static readonly float[] CUTS = { 180f, 270f, 330f, 351f, 397f, 420f, 510f, 535f, 575f, 600f, 605f };
        static float Sm(float a, float b, float x) { return AstronomicalCube.Sm(a, b, x); }
        static Seg SegAt(float t) { Seg r = SEGS[0]; foreach (var g in SEGS) if (t >= g.T0) r = g; return r; }
        static Seg SegBefore(Seg s) { Seg r = SEGS[0]; foreach (var g in SEGS) { if (g == s) break; r = g; } return r; }

        AstronomicalEventDef def;
        GameObject root, skyGo, flashGo, tintGo;
        WorldKit kit;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        float skyT = -9f, lastT = -1f, yaw, shake, farSave = -1f, rumbleT;
        Vector3 originW, homeHead;
        bool built, paused, ambientSaved, fogSave, haveHome;
        Color ambientSave;
        int floorLayer, locoMask;
        Texture2D glowTex, lineTex;
        Material flashMat, tintMat;
        ParticleSystem fx, dust, smoke, clouds;
        Light sun, flashLamp, redLamp;
        readonly List<GltfModel> models = new List<GltfModel>();
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<GameObject> clones = new List<GameObject>();

        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }
        Vector3 Lc(Vector3 w) { return root.transform.InverseTransformPoint(w); }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        Vector3 HeadNow() { try { return GTPlayer.Instance.headCollider.transform.position; } catch { } return originW + Vector3.up * 1.2f; }
        Vector3 HandNow()
        {
            try { var gt = GorillaTagger.Instance; if (gt != null && gt.rightHandTransform != null) return gt.rightHandTransform.position; } catch { }
            return HeadNow() + Vector3.down * 0.4f;
        }
        public bool TryViewpoint(out Vector3 pos, out float y) { pos = originW + Vector3.up * 1.2f; y = yaw; return root != null; }
        public string DebugInfo() { var g = SegAt(lastT); return "t " + lastT.ToString("0.0") + "  scene " + g.S + "  actors " + acts.Count; }
        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { fx, dust, smoke }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }

        // ---------------------------------------------------------------- small builders
        Transform Quad(string n, Material mat)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(root.transform, false);
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform;
        }
        Transform Prim(PrimitiveType pt, string n, Material mat)
        {
            var g = GameObject.CreatePrimitive(pt); UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(root.transform, false);
            var r = g.GetComponent<Renderer>(); if (mat != null) r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform;
        }
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
        static float Hash(int i, int k) { return WorldKit.Hash(i, k, 23); }
        Font textFont; Material textMat;
        TextMesh MakeText(Transform parent, float size, Color c, int queue = 3500)
        {
            var g = new GameObject("t"); g.transform.SetParent(parent, false); var tm = g.AddComponent<TextMesh>(); tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontSize = 100; tm.characterSize = size; tm.fontStyle = FontStyle.Bold; tm.color = c;
            if (textFont == null) { try { textFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } if (textFont == null) { try { textFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } } }
            if (textFont != null) { tm.font = textFont; var r = g.GetComponent<MeshRenderer>(); if (r != null && textFont.material != null) { if (textMat == null) { textMat = new Material(textFont.material); textMat.renderQueue = queue; owned.Add(textMat); } r.sharedMaterial = textMat; r.shadowCastingMode = ShadowCastingMode.Off; } }
            return tm;
        }

        // ---------------------------------------------------------------- build
        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false;
            Vector3 head = anchorPos;
            try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            locoMask = LocoMask(); floorLayer = 0; for (int i = 0; i < 32; i++) if ((locoMask & (1 << i)) != 0) { floorLayer = i; break; }
            RaycastHit h; originW = head; Vector3 org = head;
            try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, locoMask, QueryTriggerInteraction.Ignore)) originW = h.point; else originW = new Vector3(org.x, head.y - 1.2f, org.z);
            root = new GameObject("AstronomicalGalactus"); root.transform.position = originW; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            homeHead = head; haveHome = true; if (homeHead.y > originW.y + 150f) homeHead = originW + Vector3.up * 1.5f;
            AstronomicalDebug.L2W = v => W(v); AstronomicalDebug.Anchors.Clear();
            BuildSky(); BuildEffects(); SfxInit(); BuildModels(); BuildContent();
            var fg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(fg.GetComponent<Collider>()); fg.name = "AstronomicalGalactusFlash"; fg.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3500); var fr = fg.GetComponent<Renderer>(); fr.sharedMaterial = flashMat; fr.shadowCastingMode = ShadowCastingMode.Off; fr.allowOcclusionWhenDynamic = false; flashGo = fg; flashGo.SetActive(false);
            var tg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(tg.GetComponent<Collider>()); tg.name = "AstronomicalGalactusTint"; tg.transform.localScale = Vector3.one * 2.2f;
            tintMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3400); var tr = tg.GetComponent<Renderer>(); tr.sharedMaterial = tintMat; tr.shadowCastingMode = ShadowCastingMode.Off; tr.allowOcclusionWhenDynamic = false; tintGo = tg; tintGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight; ambientSaved = true; fogSave = RenderSettings.fog; RenderSettings.fog = false;
            skyboxSave = RenderSettings.skybox; RenderSettings.skybox = null;
            try { if (cam != null) { farSave = cam.farClipPlane; cam.farClipPlane = Mathf.Max(farSave, 7000f); } } catch { }
            lastT = -1f; built = true; try { FindSkyRenderers(); } catch { }
            JsLiveEventsMod.Log.LogInfo("Galactus built at " + originW + ": actors " + acts.Count);
        }

        Material skyboxSave; readonly List<Renderer> skyRs = new List<Renderer>();
        void FindSkyRenderers()
        {
            skyRs.Clear(); foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>()) { if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.gameObject == skyGo || (root != null && r.transform.IsChildOf(root.transform))) continue; var m = r.sharedMaterial; string sn = m != null && m.shader != null ? m.shader.name : ""; string nm = r.gameObject.name.ToLowerInvariant(); if ((sn.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0 && sn.IndexOf("WindTunnel", StringComparison.OrdinalIgnoreCase) < 0) || nm == "standard sky" || nm.Contains("skydome") || nm.Contains("sky dome")) skyRs.Add(r); }
        }
        readonly List<GameObject> hiddenGos = new List<GameObject>();
        void HideSkies(bool hide)
        {
            if (hide) { foreach (var r in skyRs) if (r != null && r.gameObject.activeSelf) { hiddenGos.Add(r.gameObject); r.gameObject.SetActive(false); } }
            else { foreach (var g in hiddenGos) if (g != null) g.SetActive(true); hiddenGos.Clear(); }
        }
        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 450f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray();
            skyGo = new GameObject("AstronomicalGalactusSky"); skyGo.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = skyGo.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 1500)); dm.mainTexture = null; dm.renderQueue = 1500; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
            // stars: 900 tiny quads on a sphere, shown from 8:30
            var sv = new List<Vector3>(); var st = new List<int>(); var sc = new List<Color>(); var rr = new System.Random(5);
            for (int i = 0; i < 900; i++)
            {
                Vector3 d = new Vector3((float)rr.NextDouble() * 2f - 1f, (float)rr.NextDouble() * 2f - 1f, (float)rr.NextDouble() * 2f - 1f); if (d.sqrMagnitude < 0.01f) continue; d.Normalize(); Vector3 u = Vector3.Cross(d, Vector3.up).normalized, v = Vector3.Cross(d, u); float sz = 0.45f + 1.4f * (float)rr.NextDouble() * (float)rr.NextDouble(); int b0 = sv.Count;
                sv.Add(d * 430f + (-u - v) * sz); sv.Add(d * 430f + (u - v) * sz); sv.Add(d * 430f + (u + v) * sz); sv.Add(d * 430f + (-u + v) * sz); st.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 }); float br = 0.5f + 0.5f * (float)rr.NextDouble(); for (int k = 0; k < 4; k++) sc.Add(new Color(br, br, 1f, 1f));
            }
            var smesh = new Mesh(); smesh.SetVertices(sv); smesh.SetColors(sc); smesh.SetTriangles(st, 0); owned.Add(smesh); starGo = new GameObject("AstronomicalGalactusStars"); starGo.AddComponent<MeshFilter>().sharedMesh = smesh; var smr = starGo.AddComponent<MeshRenderer>();
            var sm = new Material(kit.Glow(Color.white, null, 1501)); owned.Add(sm); smr.sharedMaterial = sm; smr.shadowCastingMode = ShadowCastingMode.Off; smr.allowOcclusionWhenDynamic = false; starGo.SetActive(false);
        }
        GameObject starGo;
        // sky colours (top, middle, horizon) of a scene; each fades in from the one before over 4 s
        static void SkyOf(Seg g, float t, out Color top, out Color mid, out Color hor)
        {
            top = mid = hor = Color.black;
            switch (g.S)
            {
                case Sc.Deck: top = new Color(0.72f, 0.74f, 0.88f); mid = new Color(0.88f, 0.86f, 0.92f); hor = new Color(1f, 0.96f, 0.95f); break;
                case Sc.Arrive: top = new Color(0.7f, 0.7f, 0.86f); mid = new Color(0.9f, 0.82f, 0.9f); hor = new Color(1f, 0.9f, 0.92f); break;
                case Sc.Reach: { float k = Sm(120f, 180f, t); top = Color.Lerp(new Color(0.7f, 0.7f, 0.86f), new Color(0.55f, 0.4f, 0.7f), k); mid = Color.Lerp(new Color(0.9f, 0.82f, 0.9f), new Color(0.95f, 0.6f, 0.7f), k); hor = Color.Lerp(new Color(1f, 0.9f, 0.92f), new Color(1f, 0.7f, 0.65f), k); break; }
                case Sc.Fall: { float k = Sm(195f, 240f, t); top = Color.Lerp(new Color(0.42f, 0.52f, 0.68f), new Color(0.03f, 0.05f, 0.3f), k); mid = Color.Lerp(new Color(0.65f, 0.68f, 0.8f), new Color(0.1f, 0.15f, 0.5f), k); hor = Color.Lerp(new Color(0.85f, 0.8f, 0.9f), new Color(0.35f, 0.3f, 0.75f), k); break; }
                case Sc.BusIn: top = new Color(0.8f, 0.88f, 0.96f); mid = new Color(0.9f, 0.94f, 1f); hor = new Color(0.97f, 0.98f, 1f); break;
                case Sc.Assault: { float k = Sm(330f, 400f, t); top = Color.Lerp(new Color(0.8f, 0.88f, 0.96f), new Color(0.75f, 0.6f, 0.8f), k); mid = Color.Lerp(new Color(0.9f, 0.94f, 1f), new Color(0.95f, 0.7f, 0.8f), k); hor = Color.Lerp(new Color(0.97f, 0.98f, 1f), new Color(1f, 0.8f, 0.8f), k); break; }
                case Sc.Inside: top = new Color(0.12f, 0.02f, 0.06f); mid = new Color(0.3f, 0.05f, 0.08f); hor = new Color(0.5f, 0.1f, 0.1f); break;
                case Sc.Space: case Sc.Mouth: top = new Color(0.01f, 0.01f, 0.07f); mid = new Color(0.03f, 0.04f, 0.2f); hor = new Color(0.1f, 0.08f, 0.32f); break;
                case Sc.Blast: { float k = Sm(580f, 592f, t); top = Color.Lerp(new Color(0.01f, 0.01f, 0.07f), new Color(0.2f, 0.5f, 1f), k); mid = Color.Lerp(new Color(0.03f, 0.04f, 0.2f), new Color(0.5f, 0.8f, 1f), k); hor = Color.Lerp(new Color(0.1f, 0.08f, 0.32f), new Color(0.9f, 0.97f, 1f), k); break; }
            }
        }
        void UpdateSky(float t, Seg g)
        {
            if (skyMesh == null) return; Color top, mid, hor; SkyOf(g, t, out top, out mid, out hor);
            float k = Sm(g.T0, g.T0 + 4f, t); if (k < 1f) { Color t2, m2, h2; SkyOf(SegBefore(g), t, out t2, out m2, out h2); top = Color.Lerp(t2, top, k); mid = Color.Lerp(m2, mid, k); hor = Color.Lerp(h2, hor, k); }
            for (int i = 0; i < skyDirs.Length; i++) { var d = skyDirs[i]; float up = Mathf.Clamp01(d.y); var c = up < 0.3f ? Color.Lerp(hor, mid, up / 0.3f) : Color.Lerp(mid, top, (up - 0.3f) / 0.7f); c.a = 1f; skyCols[i] = c; }
            skyMesh.colors = skyCols;
        }
        void BuildEffects()
        {
            fx = MakePS("GFx", 8000, 0.0f, kit.Glow(Color.white, glowTex, 3004)); dust = MakePS("GDust", 3000, 0f, kit.Glow(new Color(1f, 0.9f, 0.9f, 0.5f), glowTex, 3001)); smoke = MakePS("GSmoke", 3000, -0.05f, kit.Glow(new Color(0.3f, 0.2f, 0.3f, 0.5f), glowTex, 3000));
            clouds = MakePS("GClouds", 400, 0f, kit.Glow(new Color(1f, 1f, 1f, 0.5f), glowTex, 2999));
            sun = new GameObject("GSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0f; sun.transform.rotation = Quaternion.Euler(35f, yaw + 160f, 0f);
            flashLamp = new GameObject("GFlash").AddComponent<Light>(); flashLamp.transform.SetParent(root.transform, false); flashLamp.type = LightType.Point; flashLamp.range = 260f; flashLamp.intensity = 0f; flashLamp.shadows = LightShadows.None;
            redLamp = new GameObject("GRed").AddComponent<Light>(); redLamp.transform.SetParent(root.transform, false); redLamp.type = LightType.Point; redLamp.range = 90f; redLamp.intensity = 0f; redLamp.shadows = LightShadows.None; redLamp.color = new Color(1f, 0.25f, 0.15f);
            var rr = new System.Random(9); for (int i = 0; i < 260; i++) { float a = (float)rr.NextDouble() * 6.2832f, r = 120f + 900f * Mathf.Pow((float)rr.NextDouble(), 0.7f), y = (float)(rr.NextDouble() - 0.5) * 260f; Emit(clouds, W(new Vector3(Mathf.Sin(a) * r, IY + y, Mathf.Cos(a) * r)), Vector3.zero, new Color(1f, 1f, 1f, 0.35f + 0.25f * (float)rr.NextDouble()), 90f + 240f * (float)rr.NextDouble(), 1e6f); }
        }
        void Boom(Vector3 wp, float size, float power, Color? tint = null)
        {
            Color a = tint ?? new Color(1f, 0.6f, 0.2f);
            Sfx(size > 14f ? sBig : sBoom, wp, Mathf.Clamp(0.22f + size / 22f, 0.22f, 1f), Mathf.Clamp(1.25f - size * 0.025f, 0.55f, 1.2f), size > 14f ? 0.25f : 0.06f);
            for (int k = 0; k < (int)(80 * Parts + 20); k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, wp, d * UnityEngine.Random.Range(4f, 24f) * size * 0.3f, Color.Lerp(a, new Color(1f, 0.95f, 0.8f), UnityEngine.Random.value), size * UnityEngine.Random.Range(0.4f, 1.2f), UnityEngine.Random.Range(0.8f, 1.8f)); }
            flashLamp.transform.position = wp; flashLamp.color = Color.Lerp(a, Color.white, 0.3f); flashLamp.intensity = Lit * 8f * power; shake = Mathf.Max(shake, 0.25f * power);
        }
        void Overlay(GameObject go, Material mt, float a, Color c, Camera cam) { if (go.activeSelf != a > 0.003f) go.SetActive(a > 0.003f); if (a > 0.003f) { mt.color = new Color(c.r, c.g, c.b, a); go.transform.position = cam != null ? cam.transform.position : originW; } }

        // ---------------------------------------------------------------- carrying you (colliders off, the game's teleport, hold with gravity cancelled); the Deck and the Office are walked
        readonly List<Collider> noclipped = new List<Collider>(); float noclipT = -9f, lastReseat = -9f, dbgT = -9f; bool noclipHold, carryErr; int reseatTries; Seg curSeg;
        void NoClip(bool on)
        {
            if (on) { noclipped.Clear(); noclipT = Time.unscaledTime; foreach (var c in UnityEngine.Object.FindObjectsOfType<MeshCollider>()) { if (c == null || !c.enabled || c.isTrigger || (root != null && c.transform.IsChildOf(root.transform))) continue; c.enabled = false; noclipped.Add(c); } }
            else { foreach (var c in noclipped) if (c != null) c.enabled = true; noclipped.Clear(); noclipT = -9f; }
        }
        void Reseat(Vector3 headWorld)
        {
            try
            {
                var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Vector3 d = headWorld - pl.headCollider.transform.position;
                if (d.magnitude > 3f) { try { pl.TeleportTo(pl.transform.position + d, pl.transform.rotation, false, false); } catch (Exception e) { JsLiveEventsMod.Log.LogWarning("Galactus: TeleportTo failed: " + e.Message); } d = headWorld - pl.headCollider.transform.position; }
                rb.position += d; rb.linearVelocity = Vector3.zero; lastReseat = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("Galactus: moved you to " + headWorld + " (" + d.magnitude.ToString("0") + " m)");
            }
            catch (Exception e) { JsLiveEventsMod.Log.LogWarning("Galactus: could not move you: " + e.Message); }
        }
        static MethodInfo turnM; static bool turnTried;
        void FaceDir(Vector3 worldDir)
        {
            try
            {
                var cam = Camera.main; if (cam == null) return; worldDir.y = 0f; if (worldDir.sqrMagnitude < 0.01f) return; Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) return;
                float d = Mathf.DeltaAngle(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg);
                if (!turnTried) { turnTried = true; turnM = typeof(GTPlayer).GetMethod("Turn", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null); }
                if (turnM != null && Mathf.Abs(d) > 2f) turnM.Invoke(GTPlayer.Instance, new object[] { d });
            }
            catch { }
        }
        void Carry(Vector3 anchor, float dt, float hx, float zMin, float zMax)
        {
            try
            {
                var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Quaternion q = root.transform.rotation; Vector3 local = Quaternion.Inverse(q) * (pl.headCollider.transform.position - anchor);
                Vector3 c = new Vector3(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.y, -0.3f, 0.5f), Mathf.Clamp(local.z, zMin, zMax)); if ((c - local).sqrMagnitude > 1e-6f) rb.position += q * (c - local);
                rb.linearVelocity = (rb.linearVelocity - Physics.gravity * dt) * (1f - Mathf.Min(0.6f, 2f * dt));
            }
            catch (Exception e) { if (!carryErr) { carryErr = true; JsLiveEventsMod.Log.LogWarning("Galactus carry: " + e.Message); } }
        }

        // ---------------------------------------------------------------- the frame
        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (!built || root == null) return;
            var cam = Camera.main; float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f); bool back = t < lastT - 0.4f; float tp = lastT < 0f ? t : lastT; lastT = t;
            Seg g = SegAt(t);
            if (noclipped.Count > 0 && !noclipHold && Time.unscaledTime - noclipT > 0.6f) NoClip(false);
            bool wantHold = Carried(g.S); if (wantHold && !noclipHold) { noclipHold = true; NoClip(true); } else if (!wantHold && noclipHold) { noclipHold = false; NoClip(false); }
            if (cam != null) { skyGo.transform.position = cam.transform.position; starGo.transform.position = cam.transform.position; } HideSkies(true);
            bool stars = g.S >= Sc.Space || (g.S == Sc.Fall && t >= 232f); if (starGo.activeSelf != stars) starGo.SetActive(stars);
            if (t - skyT > 0.15f || t < skyT || (curSeg != null && curSeg != g)) { skyT = t; UpdateSky(t, g); }
            if (!paused)
            {
                if (g != curSeg || back) OnSeg(curSeg, g, t); curSeg = g; Vector3 an; float hx, z0, z1;
                if (Anchor(t, g, out an, out hx, out z0, out z1)) HoldYou(an, hx, z0, z1, dt, g, t); else FreeCheck(t, g);
            }
            else curSeg = g;
            Content(t, tp, dt, g, cam);
            Color bcol; float bk = Fade(t, g, out bcol); Overlay(flashGo, flashMat, bk, bcol, cam);
            Color tc; float ta = TintOf(t, g, out tc); Overlay(tintGo, tintMat, ta, tc, cam);
            flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 20f * Lit);
            if (!paused && dt > 0f) { rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float v = Mathf.Max(shake * 0.4f, 0f); if (v > 0.05f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(v), 0.14f); gt.StartVibration(false, Mathf.Clamp01(v), 0.14f); } } catch { } } } }
            shake = Mathf.Max(0f, shake - dt * 1.4f);
        }
        static bool Carried(Sc s) { return s >= Sc.Fall && s <= Sc.Blast; }
        void HoldYou(Vector3 an, float hx, float z0, float z1, float dt, Seg g, float t)
        {
            Vector3 aw = W(an); float dd = Vector3.Distance(HeadNow(), aw);
            if ((dd > 16f || (dd > 9f && HeadNow().y < aw.y - 2.2f)) && reseatTries < 6 && Time.unscaledTime - lastReseat > 0.7f) { reseatTries++; Reseat(aw); }
            else if (dt > 0f && dd < 16f) Carry(aw, dt, hx, z0, z1);
            if (dd > 6f && Time.unscaledTime - dbgT > 1f) { dbgT = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("Galactus: " + g.S + " t=" + t.ToString("0.0") + " you are " + dd.ToString("0.0") + " m from your hold point"); }
        }
        float farT;
        void FreeCheck(float t, Seg g)
        {
            Vector3 c, face; float rad; if (!Station(g.S, out c, out face, out rad)) return; Vector3 cw = W(c + Vector3.up * IY); float d = Vector3.Distance(new Vector3(HeadNow().x, 0f, HeadNow().z), new Vector3(cw.x, 0f, cw.z)); float dy = Mathf.Abs(HeadNow().y - cw.y);
            if (d > rad * 1.6f || dy > 30f) { farT += Time.unscaledDeltaTime; if (farT > (dy > 30f ? 1.5f : 4f) && reseatTries < 8 && Time.unscaledTime - lastReseat > 2f) { reseatTries++; farT = 0f; Reseat(cw + Vector3.up * 1.6f); } } else farT = 0f;
        }
        void OnSeg(Seg from, Seg to, float t)
        {
            JsLiveEventsMod.Log.LogInfo("Galactus: scene " + (from != null ? from.S.ToString() : "-") + " -> " + to.S + " at t=" + t.ToString("0.0") + " head " + HeadNow()); reseatTries = 0; farT = 0f;
            Vector3 an; float hx, z0, z1; Vector3 c, face; float rad;
            if (Anchor(t, to, out an, out hx, out z0, out z1)) { Reseat(W(an)); FaceDir(root.transform.rotation * Vector3.forward); }
            else if (Station(to.S, out c, out face, out rad)) { Reseat(W(c + Vector3.up * IY) + Vector3.up * 1.6f); FaceDir(root.transform.rotation * face); }
        }
        // free scenes: the Helicarrier's deck and the office
        bool Station(Sc s, out Vector3 c, out Vector3 face, out float rad)
        {
            c = Vector3.zero; face = Vector3.forward; rad = 40f;
            switch (s)
            {
                case Sc.Deck: case Sc.Arrive: case Sc.Reach: c = new Vector3(0f, DeckY, -30f); rad = 28f; return true;
                case Sc.Office: case Sc.Promo: c = new Vector3(0f, 0.15f, OfficeZ + 1f); face = Vector3.Normalize(new Vector3(0f, 0f, 1f)); rad = 6f; return true;
            }
            return false;
        }
        float Eas(float x) { return x * x * (3f - 2f * x); }
        // carried scenes: falling from the deck, then standing on the bus roof for the rest
        bool Anchor(float t, Seg g, out Vector3 a, out float hx, out float zMin, out float zMax)
        {
            a = Vector3.zero; hx = 1.6f; zMin = -1.6f; zMax = 1.6f;
            switch (g.S)
            {
                case Sc.Fall: { float k = Mathf.Clamp01((t - 180f) / 90f); a = FallPos(t) ; return true; }
                case Sc.BusIn: { a = BusPos + new Vector3(0f, AstronomicalDebug.Tweak[4] != 0f ? AstronomicalDebug.Tweak[4] : BusEye, 3f); hx = 1.7f; zMin = -4f; zMax = 3f; return true; }   // 4:30 - 5:30 you look out of the bus' windows
                case Sc.Assault: { a = BusPos + BusRoof + Vector3.up * 3f; hx = 2.4f; zMin = -3.2f; zMax = 3.2f; return true; }
                case Sc.Inside: { a = BusPos + new Vector3(0f, 9f, -36f); hx = 4f; zMin = -4f; zMax = 4f; return true; }   // 7:00 - 8:30 the recording follows the bus from behind
                case Sc.Space: case Sc.Mouth: case Sc.Blast: { a = BusPos + new Vector3(-17f, 7f, -33f); hx = 4f; zMin = -4f; zMax = 4f; return true; }
            }
            return false;
        }
        Vector3 FallPos(float t) { float k = Mathf.Clamp01((t - 180f) / 90f); return new Vector3(8f * Mathf.Sin(k * 3f), IY + DeckY + 6f - 330f * (k * k * 0.6f + k * 0.4f), -30f + 40f * k) + Vector3.up * 1.6f; }
        // the dips to white / black between scenes
        float Fade(float t, Seg g, out Color col)
        {
            col = Color.white; float a = 0f;
            if (t < 2.5f) a = 1f - Sm(0f, 2.5f, t);
            foreach (float c in CUTS) { float len = c == 270f ? 3.2f : c == 600f ? 1.5f : 1.0f; float dip = Sm(c - 0.5f, c, t) * (1f - Sm(c + 0.2f, c + len, t)); if (dip > a) { a = dip; col = c == 575f ? new Color(0.85f, 1f, 0.9f) : c == 600f || c == 605f ? Color.black : Color.white; } }
            if (t >= 586f && t < 600f) { float w = Sm(586f, 588f, t) * (1f - Sm(594f, 600f, t) * 0.0f); if (w > a) { a = w; col = Color.Lerp(Color.white, new Color(0.7f, 0.85f, 1f), Sm(590f, 598f, t)); } }
            if (t >= 598f && t < 605f) { a = Mathf.Max(a, Sm(598f, 600f, t)); col = Color.black; }
            if (t >= 605f && t < 607f) { a = Mathf.Max(a * 0f, 1f - Sm(605f, 607f, t)); col = Color.black; }
            if (t > T_END - 3f) { a = Mathf.Max(a, Sm(T_END - 3f, T_END - 0.3f, t)); col = Color.black; }
            return a;
        }
        float TintOf(float t, Seg g, out Color c)
        {
            float a = TintBase(t, g, out c); float k2 = Sm(g.T0, g.T0 + 3f, t); Seg b = SegBefore(g);
            if (k2 < 1f && b != g) { Color pc; float pa = TintBase(g.T0 - 0.01f, b, out pc); c = Color.Lerp(pc, c, k2); a = Mathf.Lerp(pa, a, k2); }
            return a;
        }
        float TintBase(float t, Seg g, out Color c)
        {
            c = Color.black; float a = 0f;
            switch (g.S)
            {
                case Sc.Deck: case Sc.Arrive: c = new Color(1f, 0.95f, 0.9f); a = 0.1f; break;
                case Sc.Reach: c = new Color(1f, 0.55f, 0.55f); a = 0.06f + 0.1f * Sm(120f, 180f, t); break;
                case Sc.Fall: c = Color.Lerp(new Color(0.6f, 0.7f, 1f), new Color(0.7f, 0.45f, 1f), Sm(200f, 250f, t)); a = 0.1f; break;
                case Sc.BusIn: c = Color.white; a = 0.16f; break;
                case Sc.Assault: c = t >= 351f && t < 397f ? new Color(0.45f, 0.4f, 1f) : new Color(0.82f, 0.9f, 1f); a = t >= 351f && t < 397f ? 0.16f : 0.34f * (1f - 0.5f * Sm(405f, 418f, t)); break;
                case Sc.Inside: c = new Color(1f, 0.15f, 0.1f); a = 0.2f; break;
                case Sc.Space: c = new Color(0.3f, 0.4f, 1f); a = 0.08f; break;
                case Sc.Mouth: c = Color.Lerp(new Color(0.3f, 0.4f, 1f), new Color(0.3f, 1f, 0.5f), Sm(555f, 575f, t)); a = 0.08f + 0.12f * Sm(555f, 575f, t); break;
                case Sc.Blast: c = new Color(0.5f, 0.8f, 1f); a = 0.15f; break;
                case Sc.Office: c = new Color(1f, 0.2f, 0.12f); a = 0.22f; break;
            }
            return a;
        }

        public void Cleanup()
        {
            try { if (noclipped.Count > 0) NoClip(false); noclipHold = false; if (haveHome && root != null && Vector3.Distance(HeadNow(), homeHead) > 6f) Reseat(homeHead); } catch { }
            curSeg = null; try { ContentCleanup(); } catch { } SfxCleanup0(); try { HideSkies(false); } catch { } skyRs.Clear();
            if (ambientSaved) { RenderSettings.ambientLight = ambientSave; RenderSettings.fog = fogSave; RenderSettings.skybox = skyboxSave; skyboxSave = null; ambientSaved = false; }
            try { if (farSave > 0f && Camera.main != null) Camera.main.farClipPlane = farSave; } catch { }
            farSave = -1f; foreach (var kv in origMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; origMats.Clear();
            foreach (var g in clones) if (g != null) UnityEngine.Object.Destroy(g); clones.Clear();
            foreach (var mdl in models) if (mdl != null && mdl.Pivot != null) { foreach (var bc in mdl.Pivot.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(bc); mdl.Pivot.transform.SetParent(null, false); mdl.Pivot.SetActive(false); JsLiveEventsMod.ParkModel(mdl.Pivot); }
            models.Clear();
            if (flashGo != null) UnityEngine.Object.Destroy(flashGo); flashGo = null; if (tintGo != null) UnityEngine.Object.Destroy(tintGo); tintGo = null; if (skyGo != null) UnityEngine.Object.Destroy(skyGo); skyGo = null; if (starGo != null) UnityEngine.Object.Destroy(starGo); starGo = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null; textMat = null;
        }
    }
}
