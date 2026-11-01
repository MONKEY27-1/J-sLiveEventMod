using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // OPERATION: SKY FIRE (Fortnite Chapter 2 Season 7 mission, 12 Sep 2021), 10:09, rebuilt from the user's recording (tools/skyfire_ref has the frames I checked it with).
    // 0:05 the squad is lifted into the Mothership by a blue beam; 0:25 - 4:05 they run through the ship (cells, a hall, a gantry, two transport tubes); 4:05 the viewing deck over the island;
    // 4:45 a dark purple chamber; 6:50 the dormant Cube is rebooted (it turns blue); 8:30 the purple crystal hall with the saucers while every backpack counts down; 9:11 the backpacks go off and the squad falls
    // back to the island while the Mothership blows up; 10:04 the title card. An OVERLAY on the current map: the interior blocks float 800 m above you, nothing is loaded or replaced, you are carried
    // through them (the same method as THE END's rooms). Everything is a pure function of the music time t; one-shots fire when t passes them.
    internal sealed partial class AstronomicalSkyFire : IEventScene, IDebug
    {
        enum Sc { Black, Abduct, Cells, Hall, Walk, Tube, Rooms, View, Purple, Blue, Lift, Crystal, Fall }
        sealed class Seg { public float T0, T1, Tw; public Sc S; public string Blk; }
        static Seg G(float t0, float t1, Sc s, string blk, float tw) { return new Seg { T0 = t0, T1 = t1, S = s, Blk = blk, Tw = tw }; }
        // time windows from the user's recording (mm:ss -> s); Tw = how long the walk along the block takes (then you stand at its end)
        static readonly Seg[] SEGS = {
            G(0f, 5f, Sc.Black, null, 0f), G(5f, 25f, Sc.Abduct, null, 0f), G(25f, 65f, Sc.Cells, "CELLS", 0f), G(65f, 115f, Sc.Hall, "HALL", 0f), G(115f, 165f, Sc.Walk, "WALK", 0f), G(165f, 180f, Sc.Tube, "TUBE", 15f),
            G(180f, 225f, Sc.Rooms, "ROOMS", 0f), G(225f, 245f, Sc.Tube, "TUBE", 20f), G(245f, 285f, Sc.View, "VIEW", 12f), G(285f, 360f, Sc.Purple, "PURPLE", 15f), G(360f, 410f, Sc.View, "VIEW", 12f), G(410f, 495f, Sc.Blue, "BLUE", 15f),
            G(495f, 510f, Sc.Lift, "BLUE", 0f), G(510f, 570f, Sc.Crystal, "CUBES", 0f), G(570f, 609.5f, Sc.Fall, null, 0f) };
        const float MK = 1.5f, T_REBOOT = 426f, T_ZERO = 551f, T_FALL = 570f, T_LAND = 600f, T_TITLE = 604f, T_END = 609.3f, Y_INT = 800f, VIEW_Y = 55f, VIEW_Z = -40f, FALL_TOP = 380f;
        static float Sm(float a, float b, float x) { return AstronomicalCube.Sm(a, b, x); }
        static Seg SegAt(float t) { Seg r = SEGS[0]; foreach (var g in SEGS) if (t >= g.T0) r = g; return r; }
        static bool Inside(Sc s) { return s != Sc.Black && s != Sc.Abduct && s != Sc.View && s != Sc.Fall; }

        AstronomicalEventDef def;
        GameObject root, skyGo, flashGo;
        WorldKit kit;
        Mesh skyMesh;
        Color[] skyCols;
        Vector3[] skyDirs;
        float skyT = -9f, lastT = -1f, yaw, shake, farSave = -1f, rumbleT;
        Vector3 originW, homeHead;
        bool built, paused, ambientSaved, fogSave, haveHome;
        Color ambientSave;
        int floorLayer;
        Texture2D glowTex, lineTex;
        Material flashMat;
        ParticleSystem fx, dust;
        Light sun, flashLamp, facLight, facLight2;
        readonly List<GltfModel> models = new List<GltfModel>();
        readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<GameObject> clones = new List<GameObject>();
        GltfModel inM, viewM, propM, ufoM, shipM, jonM, fouM, grdM;

        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        Vector3 HeadNow() { try { return GTPlayer.Instance.headCollider.transform.position; } catch { } return originW + Vector3.up * 1.2f; }

        public bool TryViewpoint(out Vector3 pos, out float y) { pos = originW + Vector3.up * 1.2f; y = yaw; return root != null; }
        public string DebugInfo() { var g = SegAt(lastT); return "t " + lastT.ToString("0.0") + "  scene " + g.S + "  squad " + squad.Count; }
        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { fx, dust }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }

        // ---------------------------------------------------------------- small builders (the same as THE END's)
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
        static readonly HashSet<string> GLOWS = new HashSet<string> { "Pink", "Purp", "Cyan", "Strip", "White", "BlueGlow", "Teal", "Crystal", "Swirl", "Red", "Orange", "Belt", "Gold", "CubeGlow", "Hazard" };
        // room models: glowing materials are unlit (full colour), the rest are lit by the lights that follow you; characters / props: lit
        void Brighten(GltfModel mdl, float k, bool rooms)
        {
            foreach (var r in mdl.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; var src = r.sharedMaterials; origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src)); var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : ""; if (n.StartsWith("SF_")) n = n.Substring(3); bool alpha = n.StartsWith("A_") || (rooms && src[i] != null && src[i].shader != null && src[i].shader.name == "Sprites/Default"), glowy = rooms && GLOWS.Contains(n);
                    Material mt = alpha ? kit.Glow(new Color(0.6f, 0.95f, 1f, 0.35f), null, 3000) : glowy ? kit.Flat(Color.white * k) : kit.Lit(Color.white * Mathf.Min(k, 1.25f), null, 0.28f);
                    try { mt.SetFloat("_Cull", 0f); mt.doubleSidedGI = true; } catch { }
                    try { if (src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; else if (src[i] != null && !alpha) mt.color = src[i].color * k; } catch { }
                    nw[i] = mt;
                }
                r.sharedMaterials = nw;
            }
        }
        GltfModel Take(string id, float bright, bool rooms)
        {
            var m = def.Prop(id); if (m == null || m.Pivot == null) { JsLiveEventsMod.Log.LogWarning("SkyFire: prop '" + id + "' missing"); return null; }
            m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false); Brighten(m, bright, rooms); models.Add(m); return m;
        }
        static void PlaceByFile(GltfModel m, Vector3 localTarget)
        {
            if (m == null || m.Pivot == null || m.Model == null) return;
            var root0 = m.Pivot.transform.parent; if (root0 == null) return; Vector3 cur = root0.InverseTransformPoint(m.Model.transform.position); m.Pivot.transform.localPosition += localTarget - cur;
        }
        static GltfModel.Clip FindClip(GltfModel m, string n) { if (m == null) return null; var c = m.FindClipExact(n); return c ?? (m.Clips.Count > 0 ? m.Clips[0] : null); }
        static void PlayClip(CRig rig, GltfModel m, string a, float ta, string b = null, float tb = 0f, float w = 0f)
        {
            var ca = FindClip(m, a); if (ca == null) return; var cb = b != null && w > 0.001f ? FindClip(m, b) : null;
            if (cb != null && cb.Channels.Count == ca.Channels.Count) rig.Pose(ca, ta, cb, tb, w); else rig.Pose(ca, ta);
        }
        static float Hash(int i, int k) { return WorldKit.Hash(i, k, 11); }
        Transform Sub(GltfModel m, string name) { if (m == null) return null; foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
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
            int m = LocoMask(); floorLayer = 0; for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) { floorLayer = i; break; }
            RaycastHit h; originW = head; Vector3 org = head;
            try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, m, QueryTriggerInteraction.Ignore)) originW = h.point; else originW = new Vector3(org.x, head.y - 1.2f, org.z);
            {   // face the longest free line of sight (the deck looks that way over the island)
                float best = -1f; Vector3 eye = originW + Vector3.up * 2.2f;
                for (int k = 0; k < 24; k++) { float a = k * 15f; Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward; float free = 90f; RaycastHit hh; if (Physics.Raycast(eye, d, out hh, 90f, ~0, QueryTriggerInteraction.Ignore)) free = hh.distance; Vector3 d2 = Quaternion.Euler(0f, a + 8f, 0f) * Vector3.forward; if (Physics.Raycast(eye + Vector3.up * 3f, d2, out hh, 90f, ~0, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, hh.distance + 15f); if (free > best) { best = free; yaw = a; } }
                JsLiveEventsMod.Log.LogInfo("SkyFire: open direction " + yaw + " deg, free " + best.ToString("0") + " m");
            }
            root = new GameObject("AstronomicalSkyFire"); root.transform.position = originW; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            homeHead = head; haveHome = true; if (homeHead.y > originW.y + 150f) homeHead = originW + Vector3.up * 1.5f;
            AstronomicalDebug.L2W = v => W(v); AstronomicalDebug.Anchors.Clear();
            BuildSky(); BuildEffects(); SfxInit(); BuildModels(); BuildContent();
            var fg = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(fg.GetComponent<Collider>()); fg.name = "AstronomicalSkyFireFlash"; fg.transform.localScale = Vector3.one * 2.4f;
            flashMat = kit.Glow(new Color(0f, 0f, 0f, 0f), null, 3500); var fr = fg.GetComponent<Renderer>(); fr.sharedMaterial = flashMat; fr.shadowCastingMode = ShadowCastingMode.Off; fr.allowOcclusionWhenDynamic = false; flashGo = fg; flashGo.SetActive(false);
            ambientSave = RenderSettings.ambientLight; ambientSaved = true; fogSave = RenderSettings.fog; RenderSettings.fog = false;
            skyboxSave = RenderSettings.skybox; RenderSettings.skybox = null;      // the desktop camera draws the map's GTSkybox over our sky dome (it is drawn last, wherever nothing else is); without it the camera shows our dome
            try { if (cam != null) { farSave = cam.farClipPlane; cam.farClipPlane = Mathf.Max(farSave, 6000f); } } catch { }
            lastT = -1f; built = true; try { FindSkyRenderers(); } catch { }
            JsLiveEventsMod.Log.LogInfo("SkyFire built at " + originW + ": interior " + (inM != null) + ", deck " + (viewM != null) + ", props " + (propM != null) + ", ufo " + (ufoM != null) + ", ship " + (shipM != null) + ", squad " + squad.Count);
        }

        Material skyboxSave; Renderer mapSky; readonly List<Renderer> skyRs = new List<Renderer>();
        void FindSkyRenderers()
        {
            skyRs.Clear(); foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>()) { if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.gameObject == skyGo || (root != null && r.transform.IsChildOf(root.transform))) continue; var m = r.sharedMaterial; string sn = m != null && m.shader != null ? m.shader.name : ""; string nm = r.gameObject.name.ToLowerInvariant(); if ((sn.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0 && sn.IndexOf("WindTunnel", StringComparison.OrdinalIgnoreCase) < 0) || nm == "standard sky" || nm.Contains("skydome") || nm.Contains("sky dome")) { skyRs.Add(r); JsLiveEventsMod.Log.LogInfo("SkyFire sky renderer: " + r.gameObject.name + " (" + (r.transform.parent != null ? r.transform.parent.name : "-") + ") enabled " + r.enabled + " active " + r.gameObject.activeInHierarchy + " size " + r.bounds.size + " shader " + sn); } }
        }
        // the map's sky dome objects are switched OFF (the whole object: the game re-enables a renderer, not a hidden object) while the event runs and back on in Cleanup
        readonly List<GameObject> hiddenGos = new List<GameObject>();
        void HideSkies(bool hide)
        {
            if (hide) { foreach (var r in skyRs) if (r != null && r.gameObject.activeSelf) { hiddenGos.Add(r.gameObject); r.gameObject.SetActive(false); } }
            else { foreach (var g in hiddenGos) if (g != null) g.SetActive(true); hiddenGos.Clear(); }
        }
        // the map's own sky dome (a 700 m "Standard Sky" mesh) is opaque and hides our sky dome, so it is switched off while the event runs and switched back on in Cleanup
        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 450f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray();
            skyGo = new GameObject("AstronomicalSkyFireSky"); skyGo.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = skyGo.AddComponent<MeshRenderer>();
            var dm = new Material(kit.Glow(Color.white, glowTex, 1500)); dm.mainTexture = null; dm.renderQueue = 1500; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
        }
        // the sky colours (top, middle, horizon) of a moment: blue over the beam, purple-pink over the deck, burning orange in the fall; black inside the ship
        void SkyAt(float t, Seg g, out Color top, out Color mid, out Color hor)
        {
            top = mid = hor = Color.black;
            if (g.S == Sc.Abduct) { float k = Sm(5f, 22f, t); top = Color.Lerp(new Color(0.05f, 0.18f, 0.42f), new Color(0.2f, 0.7f, 1f), k); mid = Color.Lerp(new Color(0.1f, 0.4f, 0.75f), new Color(0.6f, 0.95f, 1f), k); hor = Color.Lerp(new Color(0.35f, 0.8f, 1f), Color.white, k); }
            else if (g.S == Sc.View) { top = new Color(0.16f, 0.07f, 0.4f); mid = new Color(0.55f, 0.18f, 0.7f); hor = new Color(1f, 0.5f, 0.82f); if (t > 360f && t < 410f) { top = new Color(0.22f, 0.07f, 0.3f); mid = new Color(0.7f, 0.2f, 0.5f); hor = new Color(1f, 0.45f, 0.5f); } }
            else if (g.S == Sc.Fall) { float k = Sm(T_FALL, T_FALL + 6f, t); top = Color.Lerp(Color.black, new Color(0.28f, 0.08f, 0.2f), k); mid = Color.Lerp(Color.black, new Color(0.85f, 0.3f, 0.2f), k); hor = Color.Lerp(Color.black, new Color(1f, 0.65f, 0.3f), k); }
        }
        void UpdateSky(float t, Seg g)
        {
            if (skyMesh == null) return; Color top, mid, hor; SkyAt(t, g, out top, out mid, out hor);
            for (int k = 0; k < skyDirs.Length; k++) { var d = skyDirs[k]; float up = Mathf.Clamp01(d.y); var c = up < 0.3f ? Color.Lerp(hor, mid, up / 0.3f) : Color.Lerp(mid, top, (up - 0.3f) / 0.7f); c.a = 1f; skyCols[k] = c; }
            skyMesh.colors = skyCols; if (g.S != lastSkyScene) { lastSkyScene = g.S; var mr = skyGo.GetComponent<MeshRenderer>(); JsLiveEventsMod.Log.LogInfo("SkyFire sky: " + g.S + " mid " + mid + " top " + top + " renderer on " + mr.enabled + " active " + skyGo.activeInHierarchy + " pos " + skyGo.transform.position + " cam " + (Camera.main != null ? Camera.main.transform.position.ToString() : "-") + " far " + (Camera.main != null ? Camera.main.farClipPlane : 0f) + " cull " + (Camera.main != null ? Camera.main.cullingMask.ToString() : "-") + " layer " + skyGo.layer); }
        }
        Sc lastSkyScene = Sc.Fall;
        void BuildEffects()
        {
            fx = MakePS("SFx", 6000, 0.2f, kit.Glow(Color.white, glowTex, 3004)); dust = MakePS("SDust", 2500, -0.02f, kit.Glow(new Color(0.8f, 0.6f, 0.55f, 0.5f), glowTex, 3001));
            sun = new GameObject("SSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0.8f; sun.transform.rotation = Quaternion.Euler(30f, yaw + 160f, 0f);
            flashLamp = new GameObject("SFlash").AddComponent<Light>(); flashLamp.transform.SetParent(root.transform, false); flashLamp.type = LightType.Point; flashLamp.range = 140f; flashLamp.intensity = 0f; flashLamp.shadows = LightShadows.None;
            facLight = new GameObject("SLight1").AddComponent<Light>(); facLight.transform.SetParent(root.transform, false); facLight.type = LightType.Point; facLight.shadows = LightShadows.None; facLight.range = 70f; facLight.intensity = 0f;
            facLight2 = new GameObject("SLight2").AddComponent<Light>(); facLight2.transform.SetParent(root.transform, false); facLight2.type = LightType.Point; facLight2.shadows = LightShadows.None; facLight2.range = 70f; facLight2.intensity = 0f;
        }
        void Boom(Vector3 wp, float size, float power, Color? tint = null, bool blast = true)
        {
            Color a = tint ?? new Color(1f, 0.55f, 0.15f);
            Sfx(size > 14f ? sBig : sBoom, wp, Mathf.Clamp(0.22f + size / 22f, 0.22f, 1f), Mathf.Clamp(1.25f - size * 0.025f, 0.55f, 1.2f), size > 14f ? 0.25f : 0.06f);
            for (int k = 0; k < (int)(70 * Parts + 20); k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, wp, d * UnityEngine.Random.Range(4f, 24f) * size * 0.3f, Color.Lerp(a, new Color(1f, 0.9f, 0.5f), UnityEngine.Random.value), size * UnityEngine.Random.Range(0.4f, 1.2f), UnityEngine.Random.Range(0.8f, 1.8f)); }
            for (int k = 0; k < 16; k++) Emit(dust, wp + UnityEngine.Random.insideUnitSphere * size, new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(2f, 9f), UnityEngine.Random.Range(-3f, 3f)), new Color(0.3f, 0.22f, 0.2f, 0.6f), size * 2.4f, UnityEngine.Random.Range(2f, 4f));
            flashLamp.transform.position = wp; flashLamp.color = Color.Lerp(a, Color.white, 0.3f); flashLamp.intensity = Lit * 8f * power;
            if (blast) AstronomicalHazards.Blast(wp, 40f, 4f * power, 7f * power); shake = Mathf.Max(shake, 0.25f * power);
        }
        void Black(float a, Camera cam, Color c) { if (flashGo.activeSelf != a > 0.003f) flashGo.SetActive(a > 0.003f); if (a > 0.003f) { flashMat.color = new Color(c.r, c.g, c.b, a); flashGo.transform.position = cam != null ? cam.transform.position : originW; } }

        // ---------------------------------------------------------------- carrying you (the same as THE END: J CLIENT's NoClip for a moment, the game's teleport, hold with gravity cancelled)
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
                if (!noclipHold) NoClip(true); var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Vector3 d = headWorld - pl.headCollider.transform.position;
                if (d.magnitude > 3f) { try { pl.TeleportTo(pl.transform.position + d, pl.transform.rotation, false, false); } catch (Exception e) { JsLiveEventsMod.Log.LogWarning("SkyFire: TeleportTo failed: " + e.Message); } d = headWorld - pl.headCollider.transform.position; }
                rb.position += d; rb.linearVelocity = Vector3.zero; lastReseat = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("SkyFire: moved you to " + headWorld + " (" + d.magnitude.ToString("0") + " m)");
            }
            catch (Exception e) { JsLiveEventsMod.Log.LogWarning("SkyFire: could not move you: " + e.Message); }
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
        void Carry(Vector3 anchor, float yawDeg, float dt, float hx, float zMin, float zMax)
        {
            try
            {
                var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Quaternion q = root.transform.rotation * Quaternion.Euler(0f, yawDeg, 0f); Vector3 local = Quaternion.Inverse(q) * (pl.headCollider.transform.position - anchor);
                Vector3 c = new Vector3(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.y, -0.3f, 0.5f), Mathf.Clamp(local.z, zMin, zMax)); if ((c - local).sqrMagnitude > 1e-6f) rb.position += q * (c - local);
                rb.linearVelocity = (rb.linearVelocity - Physics.gravity * dt) * (1f - Mathf.Min(0.6f, 2f * dt));
            }
            catch (Exception e) { if (!carryErr) { carryErr = true; JsLiveEventsMod.Log.LogWarning("SkyFire carry: " + e.Message + " " + e.StackTrace); } }
        }

        // ---------------------------------------------------------------- the frame
        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (!built || root == null) return;
            var cam = Camera.main; float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f); bool back = t < lastT - 0.4f; float tp = lastT < 0f ? t : lastT; lastT = t;
            Seg g = SegAt(t); if (back) BackReset();
            if (noclipped.Count > 0 && !noclipHold && Time.unscaledTime - noclipT > 0.6f) NoClip(false);
            // the colliders stay off while you rise through the map's roof (0:05 - 0:25) and fall back through it (9:30 - 10:00)
            bool wantHold = g.S == Sc.Abduct || (g.S == Sc.Fall && t < T_LAND); if (wantHold && !noclipHold) { noclipHold = true; NoClip(true); } else if (!wantHold && noclipHold) { noclipHold = false; NoClip(false); }
            if (cam != null) skyGo.transform.position = cam.transform.position; HideSkies(true);
            if (t - skyT > 0.15f || t < skyT || (curSeg != null && curSeg != g)) { skyT = t; UpdateSky(t, g); }
            // ---- you
            if (!paused) { Vector3 an; float ayaw, hx, z0, z1; if (g != curSeg) OnSeg(curSeg, g, t); curSeg = g; if (Anchor(t, g, out an, out ayaw, out hx, out z0, out z1)) HoldYou(an, ayaw, hx, z0, z1, dt, g, t); }
            else curSeg = g;
            // ---- the world
            Content(t, tp, dt, g, cam);
            Color bcol; float bk = Fade(t, g, out bcol); Black(bk, cam, bcol);
            flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 20f * Lit);
            if (!paused && dt > 0f) { rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float v = Mathf.Max(shake * 0.4f, 0f); if (v > 0.05f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(v), 0.14f); gt.StartVibration(false, Mathf.Clamp01(v), 0.14f); } } catch { } } } }
            shake = Mathf.Max(0f, shake - dt * 1.4f);
        }
        void HoldYou(Vector3 an, float ayaw, float hx, float z0, float z1, float dt, Seg g, float t)
        {
            Vector3 aw = W(an); float dd = Vector3.Distance(HeadNow(), aw);
            if ((dd > 16f || (dd > 9f && HeadNow().y < aw.y - 2.2f)) && reseatTries < 6 && Time.unscaledTime - lastReseat > 0.7f) { reseatTries++; Reseat(aw); }
            else if (dt > 0f && dd < 16f) Carry(aw, ayaw, dt, hx, z0, z1);
            if (dd > 6f && Time.unscaledTime - dbgT > 1f) { dbgT = Time.unscaledTime; JsLiveEventsMod.Log.LogInfo("SkyFire: " + g.S + " t=" + t.ToString("0.0") + " you are " + dd.ToString("0.0") + " m from your hold point: head " + HeadNow() + " hold " + aw); }
        }
        void OnSeg(Seg from, Seg to, float t)
        {
            JsLiveEventsMod.Log.LogInfo("SkyFire: scene " + (from != null ? from.S.ToString() : "-") + " -> " + to.S + " at t=" + t.ToString("0.0") + " head " + HeadNow()); reseatTries = 0;
            Vector3 an; float ayaw, hx, z0, z1;
            if (Anchor(t, to, out an, out ayaw, out hx, out z0, out z1)) { Reseat(W(an)); FaceDir(root.transform.rotation * Quaternion.Euler(0f, ayaw, 0f) * Vector3.forward); }
            else if (to.S == Sc.Black) { }
        }
        // where your head is held in each scene (root-local), the frame's yaw and the half width / back / front limits of the free box round it
        bool Anchor(float t, Seg g, out Vector3 a, out float yawDeg, out float hx, out float zMin, out float zMax)
        {
            a = Vector3.zero; yawDeg = 0f; hx = 1.2f; zMin = -1f; zMax = 1f;
            switch (g.S)
            {
                case Sc.Black: a = new Vector3(0f, 1.5f, 0f); hx = 3f; zMin = -3f; zMax = 3f; return t < 4.9f;
                case Sc.Abduct: { float k = Sm(5f, 23f, t); a = new Vector3(0f, 1.5f + 190f * k, 0f); hx = 1.2f; zMin = -1.2f; zMax = 1.2f; return true; }
                case Sc.Fall:
                    { if (t >= T_LAND) return false; float k = Mathf.Clamp01((t - T_FALL) / (T_LAND - T_FALL)); a = new Vector3(0f, 1.5f + FALL_TOP * Mathf.Pow(1f - k, 1.5f), 0f); hx = 1.5f; zMin = -1.5f; zMax = 1.5f; return true; }
                default:
                    {
                        Vector3 s, e; if (!Route(g, out s, out e)) return false; Vector3 p; float rx, rz;
                        if (RouteXZ(g, t, out rx, out rz)) p = s + new Vector3(rx, 0f, rz);
                        else { float x = g.Tw <= 0f ? 1f : Mathf.Clamp01((t - g.T0) / g.Tw); float k = g.Tw > 0f && g.Tw <= 20f ? Sm(0f, 1f, x) : x; p = Vector3.Lerp(s, e, k); if (g.Tw <= 2f && g.Tw > 0f) p = e; }
                        a = p + Vector3.up * 1.6f; bool stand = g.S == Sc.View || g.S == Sc.Purple || g.S == Sc.Blue || g.S == Sc.Lift; hx = stand ? 3.5f : g.S == Sc.Tube ? 0.9f : 1.2f; zMin = stand ? -2.5f : -1.2f; zMax = stand ? 3.5f : 1.0f; return true;
                    }
            }
        }
        // time -> where the scene's walk starts / ends (root-local floor points from the models' S_ / E_ markers)
        readonly Dictionary<string, Vector3> bS = new Dictionary<string, Vector3>(), bE = new Dictionary<string, Vector3>();
        bool Route(Seg g, out Vector3 s, out Vector3 e) { s = e = Vector3.zero; if (g.Blk == null || !bS.TryGetValue(g.Blk, out s) || !bE.TryGetValue(g.Blk, out e)) return false; return true; }
        // the dips to black / white between scenes (0.5 s into the cut, 0.8 s out), the first fade-in and the white flash at the very end of the fall
        float Fade(float t, Seg g, out Color col)
        {
            col = Color.black; float a = 0f;
            if (t < 4.2f) a = 1f; else if (t < 5f) a = 1f - Sm(4.2f, 5f, t);
            for (int i = 1; i < SEGS.Length; i++)
            {
                float c = SEGS[i].T0; if (c < 6f) continue; float dip = Sm(c - 0.5f, c, t) * (1f - Sm(c + 0.15f, c + 0.9f, t));
                if (dip > a) { a = dip; bool white = SEGS[i].S == Sc.Tube || SEGS[i].S == Sc.Cells || SEGS[i].S == Sc.Crystal || SEGS[i - 1].S == Sc.Tube || SEGS[i].S == Sc.Fall; col = white ? new Color(0.85f, 0.97f, 1f) : Color.black; }
            }
            if (t > T_TITLE - 1.2f && t < T_TITLE) { a = Mathf.Max(a, Sm(T_TITLE - 1.2f, T_TITLE, t)); col = Color.black; } else if (t >= T_TITLE) { a = Mathf.Max(a, 0.9f); col = Color.black; }
            if (t >= T_ZERO && t < T_ZERO + 2.5f) { float w = Sm(T_ZERO + 0.2f, T_ZERO + 0.6f, t) * (1f - Sm(T_ZERO + 0.8f, T_ZERO + 2.5f, t)); if (w > a) { a = w * 0.95f; col = new Color(1f, 0.9f, 0.7f); } }
            return a;
        }

        public void Cleanup()
        {
            try { if (noclipped.Count > 0) NoClip(false); noclipHold = false; if (curSeg != null && (Inside(curSeg.S) || curSeg.S == Sc.View || curSeg.S == Sc.Abduct || (curSeg.S == Sc.Fall && lastT < T_LAND + 0.5f))) Reseat(homeHead); } catch { }
            curSeg = null; try { ContentCleanup(); } catch { } SfxCleanup0(); try { HideSkies(false); } catch { } mapSky = null; skyRs.Clear();
            if (ambientSaved) { RenderSettings.ambientLight = ambientSave; RenderSettings.fog = fogSave; RenderSettings.skybox = skyboxSave; skyboxSave = null; ambientSaved = false; }
            try { if (farSave > 0f && Camera.main != null) Camera.main.farClipPlane = farSave; } catch { }
            farSave = -1f; foreach (var kv in origMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; origMats.Clear();
            foreach (var g in clones) if (g != null) UnityEngine.Object.Destroy(g); clones.Clear(); bS.Clear(); bE.Clear();
            foreach (var mdl in models) if (mdl != null && mdl.Pivot != null) { foreach (var bc in mdl.Pivot.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(bc); mdl.Pivot.transform.SetParent(null, false); mdl.Pivot.SetActive(false); JsLiveEventsMod.ParkModel(mdl.Pivot); }
            models.Clear(); inM = viewM = propM = ufoM = shipM = jonM = fouM = grdM = cubeM = debM = null;
            if (flashGo != null) UnityEngine.Object.Destroy(flashGo); flashGo = null; if (skyGo != null) UnityEngine.Object.Destroy(skyGo); skyGo = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null; textMat = null;
        }
    }
}
