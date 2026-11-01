using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // ZERO CRISIS FINALE (Fortnite Chapter 2 Season 5 finale, 16 Mar 2021), rebuilt from the user's recording (tools/zc_ref has the frames checked with tools/zc_frames.py).
    // 0:04 you ride the Battle Bus over the island, Jones hangs from it and drops; 0:36 the meadow; 0:50 the Hunters; 1:28 Jones runs for the Zero Point; 1:58 the Foundation's meteor lands; 2:30 the Foundation and Jones meet;
    // 3:20 you join them; 4:40 the first Reality Wave (Jones becomes a butterfly) and the first of four Rifts; 5:45 the second wave (autumn), 6:30 wolves in the dark, 6:40 a third wave, 7:40 the Zero Point blooms;
    // 8:20 the Spire grows and you are carried up its stair, 9:10 the Foundation overloads the device and is sealed inside the Zero Point; 10:02 the optional Season 6 reel (a line-up of the cast).
    // An OVERLAY on the current map (the island IS the Forest: a pink skin is laid over its floor, the sky is replaced); nothing is loaded or replaced. Every frame is a pure function of the music time t; one-shots fire when t passes them.
    internal sealed partial class AstronomicalZero : IEventScene, IDebug
    {
        enum Sc { Black, Bus, Meadow, Battle, Run, Meteor, Talk, Zero, Raz, Cluck, Wolves, Lara, Bloom, Spire, Top, Reel }
        sealed class Seg { public float T0, T1; public Sc S; }
        static Seg G(float t0, float t1, Sc s) { return new Seg { T0 = t0, T1 = t1, S = s }; }
        static readonly Seg[] SEGS = { G(0f, 4f, Sc.Black), G(4f, 36f, Sc.Bus), G(36f, 50f, Sc.Meadow), G(50f, 88f, Sc.Battle), G(88f, 118f, Sc.Run), G(118f, 150f, Sc.Meteor), G(150f, 200f, Sc.Talk), G(200f, 280f, Sc.Zero), G(280f, 340f, Sc.Raz),
            G(340f, 390f, Sc.Cluck), G(390f, 400f, Sc.Wolves), G(400f, 450f, Sc.Lara), G(450f, 500f, Sc.Bloom), G(500f, 552f, Sc.Spire), G(552f, 602f, Sc.Top), G(602f, 700f, Sc.Reel) };
        const float T_END = 668.7f, T_FLASH = 600f, SPY = 118f, IY = 800f;     // IY: the island floats this high over the spawn (the Forest's own floor is a small ravine)     // the Spire's top deck is SPY m up
        static readonly float[] WAVES = { 280f, 345f, 390f, 400f };
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
        ParticleSystem fx, dust, smoke;
        Light sun, flashLamp;
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
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform;
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
        static float Hash(int i, int k) { return WorldKit.Hash(i, k, 17); }
        Font textFont; Material textMat;
        TextMesh MakeText(Transform parent, float size, Color c, int queue = 3500)
        {
            var g = new GameObject("t"); g.transform.SetParent(parent, false); var tm = g.AddComponent<TextMesh>(); tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontSize = 100; tm.characterSize = size; tm.fontStyle = FontStyle.Bold; tm.color = c;
            if (textFont == null) { try { textFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } if (textFont == null) { try { textFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } } }
            if (textFont != null) { tm.font = textFont; var r = g.GetComponent<MeshRenderer>(); if (r != null && textFont.material != null) { if (textMat == null) { textMat = new Material(textFont.material); textMat.renderQueue = queue; owned.Add(textMat); } r.sharedMaterial = textMat; r.shadowCastingMode = ShadowCastingMode.Off; } }
            return tm;
        }
        // ground height under a world point (the map's own floor)
        float GroundY(Vector3 w, float fallback)
        {
            RaycastHit h; if (Physics.Raycast(w + Vector3.up * 14f, Vector3.down, out h, 60f, locoMask, QueryTriggerInteraction.Ignore)) return h.point.y; return fallback;
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
            {   // face the longest free line of sight: the action runs that way and the Zero Point hangs over it
                float best = -1f; Vector3 eye = originW + Vector3.up * 2.2f;
                for (int k = 0; k < 24; k++) { float a = k * 15f; Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward; float free = 120f; RaycastHit hh; if (Physics.Raycast(eye, d, out hh, 120f, ~0, QueryTriggerInteraction.Ignore)) free = hh.distance; Vector3 d2 = Quaternion.Euler(0f, a + 8f, 0f) * Vector3.forward; if (Physics.Raycast(eye + Vector3.up * 3f, d2, out hh, 120f, ~0, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, hh.distance + 15f); if (free > best) { best = free; yaw = a; } }
                JsLiveEventsMod.Log.LogInfo("ZeroCrisis: open direction " + yaw + " deg, free " + best.ToString("0") + " m");
            }
            try
            {
                Bounds mb = new Bounds(originW, Vector3.zero); int nc = 0; foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>()) { if (c == null || !c.enabled || c.isTrigger || (locoMask & (1 << c.gameObject.layer)) == 0) continue; mb.Encapsulate(c.bounds); nc++; }
                JsLiveEventsMod.Log.LogInfo("ZeroCrisis: walkable colliders " + nc + ", bounds centre " + mb.center + " size " + mb.size + " (spawn " + originW + ")");
                foreach (var qt in UnityEngine.Object.FindObjectsOfType<Transform>()) if (qt != null && qt.name.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0) { var cc = qt.GetComponent<Collider>(); JsLiveEventsMod.Log.LogInfo("ZeroCrisis: quit object " + qt.name + " at " + qt.position + (cc != null ? " bounds " + cc.bounds.center + " size " + cc.bounds.size : "")); }
            }
            catch (Exception e) { JsLiveEventsMod.Log.LogWarning("ZeroCrisis: bounds scan: " + e.Message); }
            root = new GameObject("AstronomicalZeroCrisis"); root.transform.position = originW; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            homeHead = head; haveHome = true; if (homeHead.y > originW.y + 150f) homeHead = originW + Vector3.up * 1.5f;
            AstronomicalDebug.L2W = v => W(v); AstronomicalDebug.Anchors.Clear();
            BuildSky(); BuildEffects(); SfxInit(); BuildModels(); BuildContent();
            var fg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(fg.GetComponent<Collider>()); fg.name = "AstronomicalZeroFlash"; fg.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3500); var fr = fg.GetComponent<Renderer>(); fr.sharedMaterial = flashMat; fr.shadowCastingMode = ShadowCastingMode.Off; fr.allowOcclusionWhenDynamic = false; flashGo = fg; flashGo.SetActive(false);
            var tg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(tg.GetComponent<Collider>()); tg.name = "AstronomicalZeroTint"; tg.transform.localScale = Vector3.one * 2.2f;
            tintMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3400); var tr = tg.GetComponent<Renderer>(); tr.sharedMaterial = tintMat; tr.shadowCastingMode = ShadowCastingMode.Off; tr.allowOcclusionWhenDynamic = false; tintGo = tg; tintGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight; ambientSaved = true; fogSave = RenderSettings.fog; RenderSettings.fog = false;
            skyboxSave = RenderSettings.skybox; RenderSettings.skybox = null;
            try { if (cam != null) { farSave = cam.farClipPlane; cam.farClipPlane = Mathf.Max(farSave, 6000f); } } catch { }
            lastT = -1f; built = true; try { FindSkyRenderers(); } catch { }
            JsLiveEventsMod.Log.LogInfo("ZeroCrisis built at " + originW + ": actors " + acts.Count + " (" + string.Join(",", acts.ConvertAll(a => a.Id).ToArray()) + ")");
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
            skyGo = new GameObject("AstronomicalZeroSky"); skyGo.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = skyGo.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 1500)); dm.mainTexture = null; dm.renderQueue = 1500; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        // the sky colours (top, middle, horizon) of a scene; a scene fades in from the one before over 4 s
        static void SkyOf(Seg g, float t, out Color top, out Color mid, out Color hor)
        {
            top = mid = hor = Color.black;
            switch (g.S)
            {
                case Sc.Bus: top = new Color(0.28f, 0.5f, 0.92f); mid = new Color(0.7f, 0.72f, 0.97f); hor = new Color(1f, 0.74f, 0.92f); break;
                case Sc.Meadow: top = new Color(0.32f, 0.55f, 0.95f); mid = new Color(0.72f, 0.8f, 0.97f); hor = new Color(0.95f, 0.88f, 0.95f); break;
                case Sc.Battle: { float k = Sm(50f, 88f, t); top = Color.Lerp(new Color(0.32f, 0.55f, 0.95f), new Color(0.35f, 0.4f, 0.8f), k); mid = Color.Lerp(new Color(0.72f, 0.8f, 0.97f), new Color(0.85f, 0.65f, 0.9f), k); hor = Color.Lerp(new Color(0.95f, 0.88f, 0.95f), new Color(1f, 0.75f, 0.92f), k); break; }
                case Sc.Run: case Sc.Meteor: case Sc.Talk: top = new Color(0.36f, 0.5f, 0.92f); mid = new Color(0.86f, 0.72f, 0.96f); hor = new Color(1f, 0.82f, 0.96f); break;
                case Sc.Zero: { float k = Sm(215f, 270f, t); top = Color.Lerp(new Color(0.36f, 0.5f, 0.92f), new Color(0.12f, 0.1f, 0.36f), k); mid = Color.Lerp(new Color(0.86f, 0.72f, 0.96f), new Color(0.5f, 0.25f, 0.7f), k); hor = Color.Lerp(new Color(1f, 0.82f, 0.96f), new Color(0.95f, 0.5f, 0.85f), k); break; }
                case Sc.Raz: top = new Color(0.09f, 0.07f, 0.3f); mid = new Color(0.42f, 0.2f, 0.62f); hor = new Color(0.9f, 0.5f, 0.82f); break;
                case Sc.Cluck: top = new Color(0.34f, 0.46f, 0.8f); mid = new Color(0.92f, 0.7f, 0.58f); hor = new Color(1f, 0.6f, 0.3f); break;
                case Sc.Wolves: top = new Color(0.02f, 0.04f, 0.18f); mid = new Color(0.05f, 0.14f, 0.42f); hor = new Color(0.12f, 0.3f, 0.62f); break;
                case Sc.Lara: top = new Color(0.28f, 0.38f, 0.74f); mid = new Color(0.82f, 0.64f, 0.7f); hor = new Color(1f, 0.7f, 0.4f); break;
                case Sc.Bloom: { float k = Sm(466f, 490f, t); top = Color.Lerp(new Color(0.12f, 0.08f, 0.34f), new Color(0.28f, 0.03f, 0.1f), k); mid = Color.Lerp(new Color(0.72f, 0.32f, 0.78f), new Color(0.9f, 0.2f, 0.22f), k); hor = Color.Lerp(new Color(1f, 0.6f, 0.9f), new Color(1f, 0.4f, 0.25f), k); break; }
                case Sc.Spire: top = new Color(0.03f, 0.07f, 0.3f); mid = new Color(0.14f, 0.3f, 0.72f); hor = new Color(0.55f, 0.5f, 0.92f); break;
                case Sc.Top: { float k = Sm(580f, 600f, t); top = Color.Lerp(new Color(0.03f, 0.08f, 0.32f), new Color(0.6f, 0.8f, 1f), k); mid = Color.Lerp(new Color(0.16f, 0.34f, 0.8f), new Color(0.9f, 0.96f, 1f), k); hor = Color.Lerp(new Color(0.6f, 0.55f, 0.95f), Color.white, k); break; }
                case Sc.Reel: top = new Color(0.95f, 0.45f, 0.1f); mid = new Color(1f, 0.62f, 0.25f); hor = new Color(1f, 0.85f, 0.5f); break;
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
            fx = MakePS("ZFx", 6000, 0.2f, kit.Glow(Color.white, glowTex, 3004)); dust = MakePS("ZDust", 3000, -0.02f, kit.Glow(new Color(0.9f, 0.7f, 0.8f, 0.5f), glowTex, 3001)); smoke = MakePS("ZSmoke", 2500, -0.05f, kit.Glow(new Color(0.3f, 0.2f, 0.35f, 0.5f), glowTex, 3000));
            sun = new GameObject("ZSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0.0f; sun.transform.rotation = Quaternion.Euler(35f, yaw + 160f, 0f);
            flashLamp = new GameObject("ZFlash").AddComponent<Light>(); flashLamp.transform.SetParent(root.transform, false); flashLamp.type = LightType.Point; flashLamp.range = 160f; flashLamp.intensity = 0f; flashLamp.shadows = LightShadows.None;
        }
        void Boom(Vector3 wp, float size, float power, Color? tint = null, bool blast = true)
        {
            Color a = tint ?? new Color(1f, 0.6f, 0.2f);
            Sfx(size > 14f ? sBig : sBoom, wp, Mathf.Clamp(0.22f + size / 22f, 0.22f, 1f), Mathf.Clamp(1.25f - size * 0.025f, 0.55f, 1.2f), size > 14f ? 0.25f : 0.06f);
            for (int k = 0; k < (int)(70 * Parts + 20); k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, wp, d * UnityEngine.Random.Range(4f, 24f) * size * 0.3f, Color.Lerp(a, new Color(1f, 0.95f, 0.8f), UnityEngine.Random.value), size * UnityEngine.Random.Range(0.4f, 1.2f), UnityEngine.Random.Range(0.8f, 1.8f)); }
            for (int k = 0; k < 16; k++) Emit(dust, wp + UnityEngine.Random.insideUnitSphere * size, new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(2f, 9f), UnityEngine.Random.Range(-3f, 3f)), new Color(0.85f, 0.65f, 0.8f, 0.6f), size * 2.4f, UnityEngine.Random.Range(2f, 4f));
            flashLamp.transform.position = wp; flashLamp.color = Color.Lerp(a, Color.white, 0.3f); flashLamp.intensity = Lit * 8f * power;
            if (blast) AstronomicalHazards.Blast(wp, 40f, 4f * power, 7f * power); shake = Mathf.Max(shake, 0.25f * power);
        }
        void Overlay(GameObject go, Material mt, float a, Color c, Camera cam) { if (go.activeSelf != a > 0.003f) go.SetActive(a > 0.003f); if (a > 0.003f) { mt.color = new Color(c.r, c.g, c.b, a); go.transform.position = cam != null ? cam.transform.position : originW; } }

        // ---------------------------------------------------------------- carrying you (like THE END / SKY FIRE: colliders off, the game's teleport, hold with gravity cancelled); free scenes only get a re-seat at the cut
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
                if (d.magnitude > 3f) { try { pl.TeleportTo(pl.transform.position + d, pl.transform.rotation, false, false); } catch (Exception e) { JsLiveEventsMod.Log.LogWarning("ZeroCrisis: TeleportTo failed: " + e.Message); } d = headWorld - pl.headCollider.transform.position; }
                rb.position += d; rb.linearVelocity = Vector3.zero; lastReseat = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("ZeroCrisis: moved you to " + headWorld + " (" + d.magnitude.ToString("0") + " m)");
            }
            catch (Exception e) { JsLiveEventsMod.Log.LogWarning("ZeroCrisis: could not move you: " + e.Message); }
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
            catch (Exception e) { if (!carryErr) { carryErr = true; JsLiveEventsMod.Log.LogWarning("ZeroCrisis carry: " + e.Message); } }
        }

        // ---------------------------------------------------------------- the frame
        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (!built || root == null) return;
            var cam = Camera.main; float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f); bool back = t < lastT - 0.4f; float tp = lastT < 0f ? t : lastT; lastT = t;
            Seg g = SegAt(t);
            if (noclipped.Count > 0 && !noclipHold && Time.unscaledTime - noclipT > 0.6f) NoClip(false);
            bool wantHold = g.S == Sc.Bus || g.S == Sc.Spire || g.S == Sc.Top; if (wantHold && !noclipHold) { noclipHold = true; NoClip(true); } else if (!wantHold && noclipHold) { noclipHold = false; NoClip(false); }
            if (cam != null) skyGo.transform.position = cam.transform.position; HideSkies(true);
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
        void HoldYou(Vector3 an, float hx, float z0, float z1, float dt, Seg g, float t)
        {
            Vector3 aw = W(an); float dd = Vector3.Distance(HeadNow(), aw);
            if ((dd > 16f || (dd > 9f && HeadNow().y < aw.y - 2.2f)) && reseatTries < 6 && Time.unscaledTime - lastReseat > 0.7f) { reseatTries++; Reseat(aw); }
            else if (dt > 0f && dd < 16f) Carry(aw, dt, hx, z0, z1);
            if (dd > 6f && Time.unscaledTime - dbgT > 1f) { dbgT = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("ZeroCrisis: " + g.S + " t=" + t.ToString("0.0") + " you are " + dd.ToString("0.0") + " m from your hold point: head " + HeadNow() + " hold " + aw); }
        }
        // free scenes: you roam; if you wander off, the scene's centre pulls you back (after a few seconds)
        float farT;
        void FreeCheck(float t, Seg g)
        {
            Vector3 c, face; float rad; if (!Station(g.S, out c, out face, out rad)) return; float d = Vector3.Distance(new Vector3(HeadNow().x, 0f, HeadNow().z), new Vector3(W(c).x, 0f, W(c).z));
            float dy = Mathf.Abs(HeadNow().y - W(c + Vector3.up * IY).y); if (d > rad * 1.6f || dy > 30f) { farT += Time.unscaledDeltaTime; if (farT > (dy > 30f ? 1.5f : 4f) && reseatTries < 8 && Time.unscaledTime - lastReseat > 2f) { reseatTries++; farT = 0f; Reseat(W(c + Vector3.up * IY) + Vector3.up * 1.6f); } } else farT = 0f;
        }
        void OnSeg(Seg from, Seg to, float t)
        {
            JsLiveEventsMod.Log.LogInfo("ZeroCrisis: scene " + (from != null ? from.S.ToString() : "-") + " -> " + to.S + " at t=" + t.ToString("0.0") + " head " + HeadNow()); reseatTries = 0; farT = 0f;
            Vector3 an; float hx, z0, z1; Vector3 c, face; float rad;
            if (Anchor(t, to, out an, out hx, out z0, out z1)) { Reseat(W(an)); FaceDir(root.transform.rotation * Vector3.forward); }
            else if (Station(to.S, out c, out face, out rad)) { Reseat(W(c + Vector3.up * IY) + Vector3.up * 1.6f); FaceDir(root.transform.rotation * face); }
        }
        // where each free scene starts you (root-local ground point, the way you face and how far you may stray)
        bool Station(Sc s, out Vector3 c, out Vector3 face, out float rad)
        {
            c = Vector3.zero; face = Vector3.forward; rad = 60f;
            switch (s)
            {
                case Sc.Meadow: c = new Vector3(0f, 0f, 0f); rad = 30f; return true;
                case Sc.Battle: c = new Vector3(0f, 0f, -4f); rad = 45f; return true;
                case Sc.Run: c = new Vector3(0f, 0f, 20f); rad = 70f; return false;
                case Sc.Meteor: c = new Vector3(0f, 0f, 82f); rad = 45f; return true;
                case Sc.Talk: c = new Vector3(0f, 0f, 92f); rad = 40f; return true;
                case Sc.Zero: c = new Vector3(0f, 0f, 100f); rad = 60f; return true;
                case Sc.Raz: c = new Vector3(0f, 0f, 118f); face = Vector3.Normalize(P1 - c); rad = 90f; return true;
                case Sc.Cluck: c = new Vector3(30f, 0f, 112f); face = Vector3.Normalize(P2 - c); rad = 90f; return true;
                case Sc.Wolves: c = new Vector3(-28f, 0f, 118f); face = Vector3.Normalize(P3 - c); rad = 90f; return true;
                case Sc.Lara: c = new Vector3(0f, 0f, 128f); face = Vector3.Normalize(P4 - c); rad = 90f; return true;
                case Sc.Bloom: c = new Vector3(0f, 0f, 100f); rad = 70f; return true;
                case Sc.Reel: c = new Vector3(0f, 0f, 0f); rad = 40f; return true;
            }
            return false;
        }
        // carried scenes: the black opening, the Battle Bus, the Spire climb and the top deck
        bool Anchor(float t, Seg g, out Vector3 a, out float hx, out float zMin, out float zMax)
        {
            a = Vector3.zero; hx = 1.2f; zMin = -1.2f; zMax = 1.2f;
            switch (g.S)
            {
                case Sc.Black: a = new Vector3(0f, 1.5f, 0f); hx = 3f; zMin = -3f; zMax = 3f; return true;
                case Sc.Bus: a = BusPos(t) + BusRoof; hx = 1.8f; zMin = -3.2f; zMax = 3.2f; return true;
                case Sc.Spire: { a = ClimbPos(t) + Vector3.up * 1.6f; hx = 2.2f; zMin = -2.2f; zMax = 2.2f; return true; }
                case Sc.Top: { a = SpireTop + new Vector3(0f, spireY + 1.6f, -9.5f); hx = 5f; zMin = -3f; zMax = 3f; return true; }
            }
            return false;
        }
        // the dips to black between cuts (0.5 s in, 0.8 s out), the white wave flashes, the first fade-in and the end of the reel
        float Fade(float t, Seg g, out Color col)
        {
            col = Color.black; float a = 0f;
            if (t < 3.2f) a = 1f; else if (t < 4.2f) a = 1f - Sm(3.2f, 4.2f, t);
            foreach (float c in new[] { 36f, 340f, 500f, 602f }) { float dip = Sm(c - 0.5f, c, t) * (1f - Sm(c + 0.15f, c + 0.9f, t)); if (dip > a) { a = dip; col = c == 500f ? new Color(0.9f, 0.95f, 1f) : Color.black; } }
            foreach (float w in WAVES) { float e = t - w; if (e > -0.05f && e < 3.2f) { float f = Sm(-0.05f, 0.25f, e) * (1f - Sm(0.4f, 3.2f, e)); if (f * 0.95f > a) { a = f * 0.95f; col = new Color(0.95f, 0.88f, 1f); } } }
            foreach (float pc in PORTAL_CLOSE) { float e = t - pc; if (e > -0.02f && e < 1.6f) { float f = Sm(-0.02f, 0.12f, e) * (1f - Sm(0.1f, 1.6f, e)); if (f * 0.9f > a) { a = f * 0.9f; col = new Color(0.9f, 0.97f, 1f); } } }
            if (t >= T_FLASH - 1.2f && t < 603.5f) { float w = Sm(T_FLASH - 1.2f, T_FLASH, t) * (1f - Sm(602f, 603.5f, t)); if (w > a) { a = w; col = Color.white; } }
            if (t > T_END - 3.5f) { a = Mathf.Max(a, Sm(T_END - 3.5f, T_END - 0.5f, t)); col = new Color(1f, 0.85f, 0.6f); }
            if (t >= 280f && t < 281f) { }
            return a;
        }
        // a coloured veil over the whole view (the mood of each reality)
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
                case Sc.Battle: c = new Color(0.9f, 0.45f, 0.8f); a = 0.14f * Sm(52f, 88f, t); break;
                case Sc.Run: case Sc.Meteor: case Sc.Talk: c = new Color(0.85f, 0.5f, 0.85f); a = 0.16f; break;
                case Sc.Zero: c = Color.Lerp(new Color(0.85f, 0.5f, 0.85f), new Color(0.45f, 0.2f, 0.75f), Sm(215f, 270f, t)); a = Mathf.Lerp(0.16f, 0.3f, Sm(215f, 270f, t)); break;
                case Sc.Raz: c = new Color(0.45f, 0.3f, 0.95f); a = 0.26f; break;
                case Sc.Cluck: c = new Color(1f, 0.55f, 0.15f); a = 0.2f; break;
                case Sc.Wolves: c = new Color(0.05f, 0.25f, 0.95f); a = 0.42f; break;
                case Sc.Lara: c = new Color(1f, 0.72f, 0.4f); a = 0.14f; break;
                case Sc.Bloom: { float k = Sm(466f, 490f, t); c = Color.Lerp(new Color(1f, 0.45f, 0.75f), new Color(1f, 0.12f, 0.08f), k); a = Mathf.Lerp(0.16f, 0.3f, k); break; }
                case Sc.Spire: c = new Color(0.15f, 0.4f, 1f); a = 0.18f; break;
                case Sc.Top: c = new Color(0.3f, 0.55f, 1f); a = 0.14f; break;
                case Sc.Reel: c = new Color(1f, 0.5f, 0.1f); a = 0.2f; break;
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
            if (flashGo != null) UnityEngine.Object.Destroy(flashGo); flashGo = null; if (tintGo != null) UnityEngine.Object.Destroy(tintGo); tintGo = null; if (skyGo != null) UnityEngine.Object.Destroy(skyGo); skyGo = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null; textMat = null;
        }
    }
}
