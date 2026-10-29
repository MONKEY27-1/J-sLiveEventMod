using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // What the Ice Storm leaves behind and what reaches the player: snow lying on the tops of the ground, roofs and ledges, snowfall, the white-out shell and fog.
    // It is static so it can outlive the event scene: when the event ends by itself the snow stays until you leave the map or stop an event.
    internal static class IceSnow
    {
        sealed class Chunk { public GameObject Go; public float Thr; }
        static readonly List<Chunk> chunks = new List<Chunk>();
        sealed class Ren { public MeshRenderer R; public Material[] Orig, Snow; public float Thr; public bool On; }
        static readonly List<Ren> rens = new List<Ren>(); static readonly Dictionary<Material, Material> snowOf = new Dictionary<Material, Material>();
        static readonly string[] skipWords = { "water", "glass", "lava", "sky", "cloud", "window", "ant", "ghost", "candle", "flame", "inflat", "scry", "light", "lamp", "fire", "star", "moon", "sun" };
        static readonly string[] groundWords = { "grass", "ground", "dirt", "terrain", "leaf", "leaves", "moss", "floor", "sand", "mud", "path", "soil", "lawn", "foliage", "bush", "hedge", "rock", "stone" };
        static WorldKit kit; static Material snow, whiteMat; static GameObject white, fallGo; static ParticleSystem fall; static float acc;
        static bool fogSaved, fogEnSave; static Color fogColSave; static float fogDenSave; static FogMode fogModeSave;
        static float curWhite, curFog, startWhite, startFog, fadeLeft; static bool over; static float meltT;      // over = the event ended by itself and only the snow is left
        public static bool Active { get { return chunks.Count > 0 || rens.Count > 0 || white != null || fall != null; } }
        static Transform CamT() { var c = Camera.main; return c != null ? c.transform : null; }

        // Snow lies only on TOPS: rays are dropped from above on a grid round the stage, every hit on a floor-layer collider that faces up (ground, roofs, ledges, rocks) gets a thin snow skin 4 cm above it,
        // in 12 m tiles that rise out of the ground one by one as the storm passes (from the storm's side first). The map's own materials are never touched.
        public static void Collect(Vector3 origin, Vector3 stormDir, int mask)
        {
            Clear(); kit = new WorldKit();
            var tex = kit.Tex(64, 64, (x, y) => { float n = WorldKit.Fbm(x * 0.16f, y * 0.16f, 5) * 0.2f, sp = WorldKit.Hash(x, y, 9) > 0.985f ? 0.06f : 0f, v = 0.84f + n + sp; return new Color(Mathf.Min(1f, v * 0.96f), Mathf.Min(1f, v * 0.99f), Mathf.Min(1f, v * 1.04f), 1f); });
            snow = kit.Lit(new Color(0.9f, 0.94f, 1f), tex, 0.2f);
            const float R = 110f, S = 1.2f; const int TILE = 8; int n2 = (int)(2f * R / S) + 1; var ys = new float[n2, n2]; var ok = new bool[n2, n2]; float top = origin.y + 45f; RaycastHit h; int hits = 0;
            for (int i = 0; i < n2; i++) for (int j = 0; j < n2; j++) if (Physics.Raycast(new Vector3(origin.x - R + i * S, top, origin.z - R + j * S), Vector3.down, out h, 120f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.55f && h.collider.GetComponentInParent<VRRig>() == null) { ys[i, j] = h.point.y; ok[i, j] = true; hits++; } else if (Physics.Raycast(new Vector3(origin.x - R + i * S, origin.y + 6f, origin.z - R + j * S), Vector3.down, out h, 40f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.55f && h.collider.GetComponentInParent<VRRig>() == null) { ys[i, j] = h.point.y; ok[i, j] = true; hits++; }
            Func<float, float, float> lump = (x, z) => 0.035f * Mathf.Sin(x * 1.7f + z * 0.9f) + 0.03f * Mathf.Sin(z * 2.3f - x * 0.7f) + 0.045f;
            for (int ti = 0; ti < n2 - 1; ti += TILE) for (int tj = 0; tj < n2 - 1; tj += TILE)
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
                for (int i = ti; i < Mathf.Min(ti + TILE, n2 - 1); i++) for (int j = tj; j < Mathf.Min(tj + TILE, n2 - 1); j++)
                {
                    if (!(ok[i, j] && ok[i + 1, j] && ok[i, j + 1] && ok[i + 1, j + 1])) continue; float lo = Mathf.Min(Mathf.Min(ys[i, j], ys[i + 1, j]), Mathf.Min(ys[i, j + 1], ys[i + 1, j + 1])), hi = Mathf.Max(Mathf.Max(ys[i, j], ys[i + 1, j]), Mathf.Max(ys[i, j + 1], ys[i + 1, j + 1])); if (hi - lo > 0.9f) continue;
                    int b0 = v.Count; for (int c = 0; c < 4; c++) { int ci = i + (c & 1), cj = j + (c >> 1); float x = origin.x - R + ci * S, z = origin.z - R + cj * S; v.Add(new Vector3(x, ys[ci, cj] + lump(x, z), z)); uv.Add(new Vector2(x * 0.3f, z * 0.3f)); }
                    tr.AddRange(new[] { b0, b0 + 2, b0 + 1, b0 + 1, b0 + 2, b0 + 3 });
                }
                if (v.Count == 0) continue;
                var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tr, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals(); kit.Keep(mesh);
                var go = new GameObject("AstronomicalIceSnow"); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = snow; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; go.SetActive(false);
                Vector3 c0 = mesh.bounds.center - origin; float p = Vector3.Dot(new Vector3(c0.x, 0f, c0.z), stormDir);
                chunks.Add(new Chunk { Go = go, Thr = Mathf.Min(48f, 33f + 14f * Mathf.Clamp01(0.5f - p / 220f) + WorldKit.Hash(ti, tj, 5) * 1.5f) });
            }
            // the map's own grass / ground / leaf textures are swapped for a snow texture (a copy of each material with the snow texture and a pale tint, so the game's shader, lightmaps and colours still work)
            int swapped = 0; var names = new HashSet<string>();
            foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                try
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.name.StartsWith("Astronomical") || r.GetComponentInParent<VRRig>() != null) continue; var mats = r.sharedMaterials; if (mats.Length == 0) continue;
                    float hz = Mathf.Max(r.bounds.size.x, r.bounds.size.z); float hy = r.bounds.size.y; bool flat = (hz > 8f && hy < 0.6f * hz) || (Mathf.Max(hz, hy) > 25f && Mathf.Max(hz, hy) < 150f); /* flat ground, and cliff / rock-wall sized meshes (but not the sky dome) */  var snowMats = new Material[mats.Length]; bool any = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i]; snowMats[i] = m; if (m == null) continue; string nm = (m.name + " " + (m.mainTexture != null ? m.mainTexture.name : "")).ToLowerInvariant(); bool skip = false; foreach (var w in skipWords) if (nm.Contains(w)) { skip = true; break; } if (skip) continue;
                        bool match = flat; if (!match) foreach (var w in groundWords) if (nm.Contains(w)) { match = true; break; } if (!match) continue;
                        Material sm = snow; names.Add(m.name);
                        snowMats[i] = sm; any = true;
                    }
                    if (!any) continue; Vector3 d = r.bounds.center - origin; float pp = Vector3.Dot(new Vector3(d.x, 0f, d.z), stormDir); rens.Add(new Ren { R = r, Orig = mats, Snow = snowMats, Thr = Mathf.Min(48f, 33f + 14f * Mathf.Clamp01(0.5f - pp / 220f) + WorldKit.Hash(rens.Count, 3, 5) * 1.5f) }); swapped++;
                }
                catch { }
            }
            AstronomicalEventMod.Log.LogInfo("Ice storm: " + swapped + " ground / grass meshes get the snow texture; materials: " + string.Join(", ", new List<string>(names).GetRange(0, Mathf.Min(40, names.Count)).ToArray()));
            AstronomicalEventMod.Log.LogInfo("Ice storm: " + hits + " up-facing floor points, " + chunks.Count + " snow tiles");
        }
        // t = event time: tiles whose moment has come rise out of the ground (and sink back when scrubbing earlier)
        public static void Update(float t)
        {
            int budget = 160; foreach (var e in rens) { if (e.R == null) continue; bool want = t >= e.Thr; if (want == e.On) continue; e.R.sharedMaterials = want ? e.Snow : e.Orig; e.On = want; if (--budget <= 0) break; }
            foreach (var c in chunks)
            {
                if (c.Go == null) continue; bool on = t >= c.Thr; if (c.Go.activeSelf != on) c.Go.SetActive(on);
                if (on) c.Go.transform.position = new Vector3(0f, -0.7f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.Thr, c.Thr + 2.5f, t))), 0f);
            }
        }
        public static void SetFog(float d)
        {
            if (!fogSaved) { fogSaved = true; fogEnSave = RenderSettings.fog; fogColSave = RenderSettings.fogColor; fogDenSave = RenderSettings.fogDensity; fogModeSave = RenderSettings.fogMode; }
            curFog = d; RenderSettings.fog = d > 0.0001f || fogEnSave; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = Color.Lerp(fogColSave, new Color(0.82f, 0.9f, 1f), Mathf.Clamp01(d * 60f)); RenderSettings.fogDensity = Mathf.Max(fogDenSave, d);
        }
        static void RestoreFog() { if (!fogSaved) return; RenderSettings.fog = fogEnSave; RenderSettings.fogColor = fogColSave; RenderSettings.fogDensity = fogDenSave; RenderSettings.fogMode = fogModeSave; fogSaved = false; curFog = 0f; }
        public static void SetWhite(float a)
        {
            curWhite = a; if (kit == null) kit = new WorldKit();
            if (white == null && a > 0.003f) { white = GameObject.CreatePrimitive(PrimitiveType.Sphere); white.name = "AstronomicalIceWhite"; UnityEngine.Object.Destroy(white.GetComponent<Collider>()); white.transform.localScale = Vector3.one * 2.4f; whiteMat = kit.Glow(new Color(0.88f, 0.94f, 1f, 0f), null, 3500); var mr = white.GetComponent<Renderer>(); mr.sharedMaterial = whiteMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false; }
            if (white == null) return; whiteMat.color = new Color(0.88f, 0.94f, 1f, a); if (white.activeSelf != a > 0.003f) white.SetActive(a > 0.003f); var c = CamT(); if (c != null) white.transform.position = c.position;
        }
        public static void Fall(float rate, Vector3 wind, float dt)
        {
            var c = CamT(); if (c == null || dt <= 0f) return; if (kit == null) kit = new WorldKit();
            if (fall == null)
            {
                fallGo = new GameObject("AstronomicalIceSnowfall"); fall = fallGo.AddComponent<ParticleSystem>(); fall.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = fall.main; pm.maxParticles = 3000; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 5f; var em = fall.emission; em.enabled = false;
                fallGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, kit.GlowTex(), 3004); fall.Play();
            }
            acc += rate * dt; int n = (int)acc; acc -= n; Vector3 h = c.position;
            for (int i = 0; i < n; i++) { var ep = new ParticleSystem.EmitParams(); ep.position = h + new Vector3(UnityEngine.Random.Range(-32f, 32f), UnityEngine.Random.Range(-2f, 14f), UnityEngine.Random.Range(-32f, 32f)); ep.velocity = wind + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), -UnityEngine.Random.Range(1.5f, 4.5f), UnityEngine.Random.Range(-1.5f, 1.5f)); ep.startSize = UnityEngine.Random.Range(0.12f, 0.38f); ep.startLifetime = UnityEngine.Random.Range(3.5f, 6f); ep.startColor = new Color(1f, 1f, 1f, UnityEngine.Random.Range(0.55f, 1f)); fall.Emit(ep, 1); }
        }
        // the event ended by itself: the snow stays, the white-out and fog fade over three seconds
        public static void BeginFade() { over = true; meltT = 0f; startWhite = curWhite; startFog = curFog; fadeLeft = 3f; Update(999f); }
        public static void Tick(float dt)
        {
            if (!Active || !over) return;
            meltT += dt; if (meltT >= 30f) { Clear(); return; }      // the snow melts 30 s after the event ends
            if (fadeLeft > 0f) { fadeLeft -= dt; float k = Mathf.Clamp01(fadeLeft / 3f); SetWhite(startWhite * k); SetFog(startFog * k); if (fadeLeft <= 0f) { RestoreFog(); if (white != null) UnityEngine.Object.Destroy(white); white = null; } }
            if (chunks.Count > 0 && fadeLeft <= 0f) { Update(999f); Fall(60f, new Vector3(1.5f, 0f, 1f), dt); }
        }
        public static void Clear()
        {
            foreach (var e in rens) if (e.R != null && e.On) e.R.sharedMaterials = e.Orig; rens.Clear(); snowOf.Clear(); foreach (var c in chunks) if (c.Go != null) UnityEngine.Object.Destroy(c.Go); chunks.Clear(); RestoreFog(); fadeLeft = 0f; over = false; curWhite = 0f;
            if (white != null) UnityEngine.Object.Destroy(white); white = null; if (fallGo != null) UnityEngine.Object.Destroy(fallGo); fallGo = null; fall = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); kit = null; snow = null; whiteMat = null;
        }
    }

    // "The Ice Storm" (Fortnite Chapter 1 Season 7, January 2019), from the user's reading of the video. Set in the SAME Gorilla Tag map (nothing is loaded or replaced): an ice orb hangs in the sky and is there from the start (rings pulse from it);
    // 0:04 it cracks and the Ice King appears from inside; 0:07 - 0:11 his big form appears; 0:20 he starts conjuring and the frozen wind appears, growing until 0:27; then it comes closer and at 0:33 it reaches the map, he hits
    // you with knockback and the map starts to get covered (snow); 0:36 the frozen wind blinds you; 0:48 it covers the whole map and you cannot see; 0:50 the event ends. The Ice King plays 18 different Blender-made clips, each exactly once, in order.
    // Everything is a pure function of the music time T (so skipping works); the one-shots (knockback) fire once per pass.
    internal sealed class AstronomicalIceStorm : IEventScene, IDebug
    {
        const float ORB_R = 18f, ORB_Y = 95f, ORB_Z = 62f, KING_H = 150f, FAR = 200f, NEAR = 62f;
        const float T_CRACK = 4f, T_BIG0 = 7f, T_BIG1 = 11f, T_CONJ = 20f, T_GROW = 27f, T_HIT = 33f, T_BLIND = 36f, T_WHITE = 48f;
        static readonly float[] ClipT = { 0f, 4f, 6f, 8f, 11f, 14f, 17f, 20f, 23f, 25f, 27f, 29.5f, 32f, 34.5f, 37f, 40f, 43f, 46f, 60f };
        static readonly string[] ClipN = { "I_Curl", "I_Emerge", "I_Awake", "I_Rise", "I_Stand", "I_Survey", "I_Flex", "I_Conjure1", "I_Conjure2", "I_Gather", "I_Advance", "I_Windup", "I_Strike", "I_Roar", "I_SweepL", "I_SweepR", "I_Slam", "I_Tower" };
        public static void ClearPersistent() { IceSnow.Clear(); }
        public static void PersistTick() { IceSnow.Tick(Time.deltaTime); }
        static float Sm(float a, float b, float x) { return AstronomicalCube.Sm(a, b, x); }

        AstronomicalEventDef def; GameObject root, dome, cubeDummy; WorldKit kit; GltfModel model; Transform pivot, handL, handR, headT, orb, orb2, glowC, glowH1, glowH2, halo, beam, ringQ0, ringQ1, ringQ2, conj;
        Material orbMat, orb2Mat, glowCMat, glowHMat, haloMat, beamMat, conjMat, crackMat; Material[] ringMats = new Material[3]; Light kingLamp, orbLamp, sun; ParticleSystem fx, wind, stars; Mesh skyMesh; Color[] skyCols; Vector3[] skyDirs; int skyN; float skyT = -9f;
        LineRenderer[] cracks = new LineRenderer[10]; readonly List<Material> kingMats = new List<Material>(); readonly List<bool> kingEye = new List<bool>(); readonly List<KeyValuePair<Renderer, Material[]>> origMats = new List<KeyValuePair<Renderer, Material[]>>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>(); Texture2D glowTex, lineTex, ringTex, beamTex, swirlA, swirlB;
        Vector3 origin, stormDir; float yaw, lastT = -1f, endT = 50f, acc, accW, rumbleT, nextGust, flash; int floorLayer; bool built, paused, shattered, hitDone; Color ambientSave; bool ambientSaved; float baseH = 1.67f;
        float Parts { get { return Mathf.Max(0.3f, AstronomicalSettings.Particles.Value); } }
        float Lit { get { return AstronomicalSettings.Lighting.Value; } }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        Vector3 W(Vector3 l) { return root.transform.TransformPoint(l); }

        public void Build(AstronomicalAssetLoader loader, Vector3 anchorPos, float yawIn)
        {
            def = AstronomicalEvents.Selected; built = false; endT = def != null && def.Length > 5f ? def.Length : 50f; IceSnow.Clear();
            Vector3 head = anchorPos; try { var p = GTPlayer.Instance; if (p != null) head = p.headCollider.transform.position; else if (Camera.main != null) head = Camera.main.transform.position; } catch { }
            Vector3 org = head; try { org = AstronomicalCountdown.FindGazeboBase(head); } catch { }
            var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; stormDir = f.normalized;
            floorLayer = 0; int m = LocoMask(); for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) { floorLayer = i; break; } RaycastHit h; Vector3 ground = org; if (Physics.Raycast(org + Vector3.up * 4f, Vector3.down, out h, 14f, m, QueryTriggerInteraction.Ignore)) ground = h.point; else ground.y = head.y - 1.2f;
            origin = ground; root = new GameObject("AstronomicalIceStorm"); root.transform.position = origin; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f); kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex();
            lineTex = kit.Tex(4, 32, (x, yy) => { float v = Mathf.Abs((yy + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp);
            ringTex = kit.Tex(128, 128, (x, y) => { float dx = (x - 63.5f) / 64f, dy = (y - 63.5f) / 64f, r = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.06f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.7f) / 0.1f, 2f)); return new Color(1, 1, 1, r < 1f ? Mathf.Clamp01(a) : 0f); }, TextureWrapMode.Clamp);
            beamTex = kit.Tex(32, 4, (x, y) => { float u = Mathf.Abs((x + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - u, 1.6f)); }, TextureWrapMode.Clamp);
            swirlA = Swirl(new Color(0.15f, 0.45f, 0.85f), new Color(0.5f, 0.85f, 1f), new Color(0.95f, 1f, 1f), 0.1f, 0.9f); swirlB = Swirl(new Color(0.1f, 0.3f, 0.7f), new Color(0.4f, 0.7f, 1f), new Color(0.9f, 0.98f, 1f), 0.6f, 0.75f);
            BuildSky(); BuildOrb(); BuildKing(); BuildStorm();
            sun = new GameObject("IceSun").AddComponent<Light>(); sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional; sun.shadows = LightShadows.None; sun.intensity = 0f;
            ambientSave = RenderSettings.ambientLight; ambientSaved = true; IceSnow.Collect(origin, stormDir, m);
            lastT = -1f; shattered = false; hitDone = false; built = true;
            AstronomicalEventMod.Log.LogInfo("Ice storm built at " + origin + " facing " + yaw.ToString("0") + ", king " + (model != null));
        }
        public bool TryViewpoint(out Vector3 pos, out float y) { pos = Vector3.zero; y = 0f; if (root == null) return false; pos = root.transform.position + Vector3.up * 1.2f; y = yaw; return true; }
        Texture2D Swirl(Color a, Color b, Color c, float seed, float alpha)
        {
            return kit.Tex(128, 128, (x, y) => { float u = x / 128f, v = y / 128f, T = Mathf.PI * 2f; float n = Mathf.Sin(T * (u * 3f + 0.25f * Mathf.Sin(T * (v * 2f + seed)))) + Mathf.Sin(T * (v * 4f + 0.3f * Mathf.Sin(T * u * 3f + seed))) + Mathf.Sin(T * ((u + v) * 2f + 0.4f * Mathf.Sin(T * v * 3f))); n = n / 3f * 0.5f + 0.5f; var col = n < 0.5f ? Color.Lerp(a, b, n * 2f) : Color.Lerp(b, c, (n - 0.5f) * 2f); col.a = alpha * (0.4f + 0.6f * n); return col; });
        }
        Transform Quad(string n, Material mat) { var g = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(root.transform, false); var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform; }
        Transform Ball(string n, Material mat) { var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(root.transform, false); var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.allowOcclusionWhenDynamic = false; return g.transform; }
        void Face(Transform t, Vector3 pos) { t.position = pos; var cam = Camera.main; if (cam != null) t.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up); }

        void BuildSky()
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols = new List<Color>(); var dirs = new List<Vector3>(); int lat = 20, lon = 40;
            for (int i = 0; i <= lat; i++) for (int j = 0; j <= lon; j++) { float th = i * Mathf.PI * 0.52f / lat, ph = j * Mathf.PI * 2f / lon; float s = Mathf.Sin(th); var d = new Vector3(s * Mathf.Cos(ph), Mathf.Cos(th), s * Mathf.Sin(ph)); dirs.Add(d); verts.Add(d * 280f); cols.Add(Color.black); }
            for (int i = 0; i < lat; i++) for (int j = 0; j < lon; j++) { int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1; tris.AddRange(new[] { a, b, c, b, d, c }); }
            skyMesh = new Mesh(); skyMesh.SetVertices(verts); skyMesh.SetColors(cols); skyMesh.SetTriangles(tris, 0); skyMesh.RecalculateBounds(); owned.Add(skyMesh); skyCols = cols.ToArray(); skyDirs = dirs.ToArray(); skyN = skyCols.Length;
            dome = new GameObject("AstronomicalIceSky"); dome.AddComponent<MeshFilter>().sharedMesh = skyMesh; var mr = dome.AddComponent<MeshRenderer>(); var dm = new Material(kit.Glow(Color.white, glowTex, 2501)); dm.mainTexture = null; dm.renderQueue = 2501; owned.Add(dm); mr.sharedMaterial = dm; mr.allowOcclusionWhenDynamic = false; mr.shadowCastingMode = ShadowCastingMode.Off;
            stars = new GameObject("IceStars").AddComponent<ParticleSystem>(); stars.transform.SetParent(dome.transform, false); stars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = stars.main; pm.maxParticles = 800; pm.simulationSpace = ParticleSystemSimulationSpace.Local; pm.playOnAwake = false; pm.startLifetime = 100000f; var em = stars.emission; em.enabled = false; stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004); stars.Play();
            for (int k = 0; k < 520; k++) { var d = UnityEngine.Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.97f + 0.03f; var ep = new ParticleSystem.EmitParams(); ep.position = d.normalized * 270f; ep.velocity = Vector3.zero; ep.startSize = UnityEngine.Random.Range(0.8f, 2.4f); ep.startLifetime = 100000f; float b = UnityEngine.Random.Range(0.5f, 1f); ep.startColor = new Color(b, b, Mathf.Min(1f, b + 0.2f), 1f); stars.Emit(ep, 1); }
        }
        // violet dusk before the orb breaks (like the video), then a deep icy night with green aurora bands, going to a white overcast as the storm arrives
        void UpdateSky(float t)
        {
            if (skyMesh == null) return; float pre = 1f - Sm(4f, 12f, t), storm = Sm(31f, 42f, t), aur = Sm(2f, 8f, t) * (1f - storm);
            Color top = Color.Lerp(new Color(0.015f, 0.04f, 0.17f), new Color(0.08f, 0.07f, 0.3f), pre), hor = Color.Lerp(new Color(0.1f, 0.3f, 0.55f), new Color(0.38f, 0.3f, 0.62f), pre);
            top = Color.Lerp(top, new Color(0.62f, 0.76f, 0.92f), storm); hor = Color.Lerp(hor, new Color(0.85f, 0.92f, 1f), storm);
            for (int k = 0; k < skyN; k++)
            {
                var d = skyDirs[k]; float el = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)), az = Mathf.Atan2(d.z, d.x); var c = Color.Lerp(hor, top, Mathf.Pow(Mathf.Clamp01(d.y), 0.45f));
                float rip = 0.5f + 0.5f * Mathf.Sin(az * 7f + el * 14f + t * 0.5f) * Mathf.Sin(az * 4f - el * 9f + t * 0.3f), band = Mathf.Exp(-((el - 0.75f) * (el - 0.75f)) / 0.12f) * aur * rip;
                c.r += band * 0.1f; c.g += band * 0.75f; c.b += band * 0.45f; float orbGlow = Mathf.Exp(-Mathf.Pow(Vector3.Angle(d, new Vector3(0f, ORB_Y, ORB_Z).normalized) / 34f, 2f)) * (1f - Sm(5f, 12f, t)) * 0.25f; c.g += orbGlow * 0.6f; c.b += orbGlow; c.a = 1f; skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

        void BuildOrb()
        {
            orbMat = kit.Glow(Color.white, swirlA, 2997); orb2Mat = kit.Glow(Color.white, swirlB, 2998); orb = Ball("IceOrb", orbMat); orb2 = Ball("IceOrbInner", orb2Mat);
            glowCMat = kit.Glow(new Color(0.6f, 0.9f, 1f, 0f), glowTex, 3003); glowC = Quad("OrbGlow", glowCMat); haloMat = kit.Glow(new Color(0.4f, 0.8f, 1f, 0f), glowTex, 3000); halo = Quad("KingHalo", haloMat);
            beamMat = kit.Glow(new Color(0.55f, 0.88f, 1f, 0f), beamTex, 2999); beam = Quad("OrbBeam", beamMat); for (int i = 0; i < 3; i++) ringMats[i] = kit.Glow(new Color(0.6f, 0.9f, 1f, 0f), ringTex, 3002); ringQ0 = Quad("Ring0", ringMats[0]); ringQ1 = Quad("Ring1", ringMats[1]); ringQ2 = Quad("Ring2", ringMats[2]);
            crackMat = kit.Glow(Color.white, lineTex, 3002); for (int i = 0; i < cracks.Length; i++) { var g = new GameObject("Crack" + i); g.transform.SetParent(root.transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = 18; l.sharedMaterial = crackMat; l.shadowCastingMode = ShadowCastingMode.Off; l.enabled = false; cracks[i] = l; }
            orbLamp = new GameObject("OrbLight").AddComponent<Light>(); orbLamp.transform.SetParent(root.transform, false); orbLamp.type = LightType.Point; orbLamp.color = new Color(0.55f, 0.85f, 1f); orbLamp.range = 160f; orbLamp.intensity = 0f; orbLamp.shadows = LightShadows.None;
            fx = new GameObject("IceFx").AddComponent<ParticleSystem>(); fx.transform.SetParent(root.transform, false); fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = fx.main; pm.maxParticles = 1500; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 3f; pm.gravityModifier = 0.25f; var em = fx.emission; em.enabled = false; fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3004); fx.Play();
        }
        void BuildKing()
        {
            model = def.Prop("king"); if (model == null || model.Pivot == null) { AstronomicalEventMod.Log.LogWarning("Ice storm: king model missing"); return; }
            pivot = model.Pivot.transform; pivot.SetParent(root.transform, false); model.Pivot.SetActive(true); baseH = Mathf.Max(0.5f, model.RendererBounds.size.y);
            // ghostly: every material becomes a see-through tinted one (the originals are put back before the model is parked)
            foreach (var r in model.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; var src = r.sharedMaterials; origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src)); var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) { bool eye = src[i] != null && src[i].name.ToLowerInvariant().Contains("eye"); var mt = kit.Glow(Color.white, null, eye ? 3001 : 3000); try { if (src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; } catch { } nw[i] = mt; kingMats.Add(mt); kingEye.Add(eye); }
                r.sharedMaterials = nw;
            }
            foreach (var t in model.Pivot.GetComponentsInChildren<Transform>(true)) { string n = t.name.ToLowerInvariant(); if (n.StartsWith("bip_hand_l")) handL = t; else if (n.StartsWith("bip_hand_r")) handR = t; else if (n.StartsWith("bip_head")) headT = t; }
            glowHMat = kit.Glow(new Color(0.7f, 0.95f, 1f, 0f), glowTex, 3003); glowH1 = Quad("HandGlowL", glowHMat); glowH2 = Quad("HandGlowR", glowHMat);
            conjMat = kit.Glow(Color.white, swirlA, 2999); conj = Ball("Conjure", conjMat);
            kingLamp = new GameObject("KingLight").AddComponent<Light>(); kingLamp.transform.SetParent(root.transform, false); kingLamp.type = LightType.Point; kingLamp.color = new Color(0.5f, 0.82f, 1f); kingLamp.range = 260f; kingLamp.intensity = 0f; kingLamp.shadows = LightShadows.None;
        }
        void BuildStorm()
        {
            wind = new GameObject("IceWind").AddComponent<ParticleSystem>(); wind.transform.SetParent(root.transform, false); wind.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var pm = wind.main; pm.maxParticles = 2600; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.playOnAwake = false; pm.startLifetime = 5f; var em = wind.emission; em.enabled = false;
            wind.GetComponent<ParticleSystemRenderer>().sharedMaterial = kit.Glow(Color.white, glowTex, 3002); wind.Play();
        }

        public void SetPaused(bool p) { paused = p; foreach (var ps in new[] { fx, stars, wind }) { if (ps == null) continue; if (p) ps.Pause(true); else ps.Play(true); } }
        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life) { var ep = new ParticleSystem.EmitParams(); ep.position = pos; ep.velocity = vel; ep.startColor = c; ep.startSize = size; ep.startLifetime = life; ps.Emit(ep, 1); }

        // the king: 18 clips, one after the other, each once; the last 0.6 s of a clip fade into the first frame of the next
        void PlayKing(float t)
        {
            if (model == null || model.Clips.Count == 0) return; int k = 0; while (k < ClipN.Length - 1 && t >= ClipT[k + 1]) k++; float tin = t - ClipT[k], len = ClipT[k + 1] - ClipT[k]; var a = model.FindClipExact(ClipN[k]); if (a == null) return;
            const float X = 0.6f; if (k < ClipN.Length - 1 && tin > len - X) { var b = model.FindClipExact(ClipN[k + 1]); if (b != null) { model.SampleBlend(a, tin, b, 0f, Sm(len - X, len, tin)); return; } }
            model.Sample(a, Mathf.Min(tin, a.Duration - 0.01f));
        }
        void PlaceKing(float t)
        {
            if (model == null) return; float e = Sm(T_BIG0, T_BIG1, t), small = Mathf.Lerp(9f, 13f, Sm(T_CRACK, T_BIG0, t));
            float h = Mathf.Exp(Mathf.Lerp(Mathf.Log(small), Mathf.Log(KING_H), e)) * (1f + 0.08f * Sm(T_HIT, 50f, t));
            float z = Mathf.Lerp(ORB_Z, FAR, e); z = Mathf.Lerp(z, NEAR, Sm(T_GROW, T_HIT, t)) - 10f * Sm(T_HIT, 50f, t); float y = Mathf.Lerp(ORB_Y - small * 0.5f, -12f, e);
            pivot.localPosition = new Vector3(0f, y, z); pivot.localRotation = Quaternion.Euler(0f, 180f, 0f); pivot.localScale = Vector3.one * (h / baseH);
            float body = Mathf.Lerp(0.4f, 0.78f, Sm(T_CRACK, T_CRACK + 1.5f, t)) * Mathf.Lerp(1f, 0.72f, e); float fl = 0.92f + 0.08f * Mathf.Sin(t * 3.1f);
            for (int i = 0; i < kingMats.Count; i++) { if (kingEye[i]) kingMats[i].color = new Color(0.55f, 0.97f, 1f, Mathf.Clamp01(0.5f + body)); else kingMats[i].color = new Color(0.66f, 0.9f, 1f, body * fl); }
            Vector3 chest = W(new Vector3(0f, y + h * 0.62f, z)); Face(halo, chest); halo.localScale = Vector3.one * h * 1.25f; haloMat.color = new Color(0.4f, 0.8f, 1f, 0.2f * e + 0.07f * Mathf.Sin(t * 1.3f) * e);
            kingLamp.transform.position = chest + root.transform.forward * -20f; kingLamp.intensity = Lit * (0.5f + 1.2f * e);
            // hands: glow that grows through the conjuring (0:20 - 0:27), pulses with every strike
            float cj = Sm(T_CONJ - 2f, T_GROW, t) * (1f - 0.4f * Sm(T_HIT, T_HIT + 3f, t)); float pu = 0.85f + 0.15f * Mathf.Sin(t * 9f) + flash * 0.8f;
            if (handL != null) { Face(glowH1, handL.position); glowH1.localScale = Vector3.one * h * (0.07f + 0.22f * cj) * pu; }
            if (handR != null) { Face(glowH2, handR.position); glowH2.localScale = Vector3.one * h * (0.07f + 0.22f * cj) * pu; }
            glowHMat.color = new Color(0.7f, 0.95f, 1f, Mathf.Clamp01(0.25f * e + 0.55f * cj));
            // the thing he conjures: a frozen-wind ball between the hands, growing until 0:27, then it leaves his hands as the storm
            bool cOn = t >= T_CONJ - 1f && t < T_HIT && handL != null && handR != null; if (conj.gameObject.activeSelf != cOn) conj.gameObject.SetActive(cOn);
            if (cOn) { Vector3 mid = (handL.position + handR.position) * 0.5f + Vector3.up * h * 0.03f; float r = h * (0.02f + 0.11f * Sm(T_CONJ - 1f, T_GROW, t)) * (1f + 0.5f * Sm(T_GROW, T_HIT, t)); conj.position = mid; conj.localScale = Vector3.one * r * 2f; conj.rotation = Quaternion.Euler(t * 40f, t * 63f, 0f); conjMat.color = new Color(1f, 1f, 1f, 0.8f * (1f - Sm(T_HIT - 2f, T_HIT, t))); }
        }

        float StormZ(float t)
        {   // z (local) of the frozen wind's front: near the king while he conjures, then it comes closer, reaching the map's edge at 0:33 and you at 0:36
            if (t < T_GROW) return FAR - 20f; if (t < T_HIT) return Mathf.Lerp(FAR - 20f, 60f, Sm(T_GROW, T_HIT, t)); return Mathf.Lerp(60f, -5f, Sm(T_HIT, T_BLIND, t));
        }

        float acc2;
        public void Apply(float t, int idx, float u, AstronomicalAudioManager au)
        {
            if (root == null || !built) return;
            float dt = Mathf.Clamp(t - lastT, 0f, 0.1f); bool back = t < lastT - 0.4f; if (back || lastT < 0f) { shattered = false; hitDone = false; nextGust = 0f; } lastT = t; var cam = Camera.main; if (cam != null) dome.transform.position = cam.transform.position;
            // ---- the orb: there from the start, rings pulsing out of it; cracks at 0:04, breaks into shards, the flash
            bool orbOn = t < 6.2f; foreach (var tr in new[] { orb, orb2, beam }) if (tr.gameObject.activeSelf != orbOn) tr.gameObject.SetActive(orbOn);
            Vector3 oc = W(new Vector3(0f, ORB_Y, ORB_Z)); float breakK = Sm(T_CRACK + 0.6f, T_CRACK + 1.8f, t);
            if (orbOn)
            {
                float rr = ORB_R * (1f + 0.02f * Mathf.Sin(t * 2.4f)) * (1f + 0.25f * breakK); orb.position = oc; orb.localScale = Vector3.one * rr * 2f; orb.rotation = Quaternion.Euler(0f, t * 9f, 0f); orb2.position = oc; orb2.localScale = Vector3.one * rr * 1.9f; orb2.rotation = Quaternion.Euler(t * 5f, -t * 14f, 20f);
                float al = (0.8f - 0.8f * breakK) * (0.9f + 0.1f * Mathf.Sin(t * 5f)); orbMat.color = new Color(1f, 1f, 1f, al); orb2Mat.color = new Color(1f, 1f, 1f, al * 0.8f);
                Vector3 camP = cam != null ? cam.transform.position : oc - root.transform.forward * 50f; Vector3 bot = oc + Vector3.down * ORB_R, gnd = new Vector3(oc.x, origin.y, oc.z); Vector3 mid = (bot + gnd) * 0.5f; float bh = bot.y - gnd.y; beam.position = mid; Vector3 toC = camP - mid; toC.y = 0f; if (toC.sqrMagnitude > 0.01f) beam.rotation = Quaternion.LookRotation(-toC, Vector3.up); beam.localScale = new Vector3(ORB_R * 1.1f, bh, 1f); beamMat.color = new Color(0.55f, 0.88f, 1f, (0.22f + 0.05f * Mathf.Sin(t * 3f)) * (1f - breakK));
            }
            // rings: one every 1.7 s until the orb breaks, faster / bigger after
            Transform[] rq = { ringQ0, ringQ1, ringQ2 }; float period = t < T_CRACK ? 1.8f : 1.1f; bool rOn = t < 14f;
            for (int i = 0; i < 3; i++) { if (rq[i].gameObject.activeSelf != rOn) rq[i].gameObject.SetActive(rOn); if (!rOn) continue; float k = Mathf.Repeat(t / (period * 3f) + i / 3f, 1f); Face(rq[i], oc); rq[i].localScale = Vector3.one * (ORB_R * 2.2f + k * ORB_R * 10f); ringMats[i].color = new Color(0.6f, 0.9f, 1f, 0.5f * (1f - k) * (1f - Sm(8f, 14f, t))); }
            // cracks (0:04): jagged lines racing over the sphere's surface
            for (int i = 0; i < cracks.Length; i++)
            {
                float start = T_CRACK + (i % 5) * 0.08f, prog = Mathf.Clamp01((t - start) / 0.6f); bool on = orbOn && prog > 0.01f && t < T_CRACK + 1.9f; if (cracks[i].enabled != on) cracks[i].enabled = on; if (!on) continue;
                Vector3 a = new Vector3(WorldKit.Hash(i, 1, 4) - 0.5f, WorldKit.Hash(i, 2, 4) - 0.5f, WorldKit.Hash(i, 3, 4) - 0.5f).normalized, b = new Vector3(WorldKit.Hash(i, 4, 4) - 0.5f, WorldKit.Hash(i, 5, 4) - 0.5f, WorldKit.Hash(i, 6, 4) - 0.5f).normalized;
                for (int p = 0; p < 18; p++) { float f = p / 17f * prog; Vector3 d = Vector3.Slerp(a, b, f); d += new Vector3(WorldKit.Hash(i, p, 7) - 0.5f, WorldKit.Hash(i, p, 8) - 0.5f, WorldKit.Hash(i, p, 9) - 0.5f) * 0.16f; cracks[i].SetPosition(p, oc + d.normalized * ORB_R * 1.01f); }
                cracks[i].widthMultiplier = 1.1f; cracks[i].startColor = cracks[i].endColor = new Color(0.85f, 1f, 1f, 0.95f * (1f - breakK));
            }
            if (!shattered && t >= T_CRACK + 0.6f && t < T_CRACK + 3f && !paused)
            {
                shattered = true; flash = 1f; for (int k = 0; k < 220; k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, oc + d * ORB_R, d * UnityEngine.Random.Range(14f, 46f) + Vector3.up * 6f, Color.Lerp(new Color(0.6f, 0.9f, 1f, 0.9f), Color.white, UnityEngine.Random.value), UnityEngine.Random.Range(1.5f, 5f), UnityEngine.Random.Range(1.8f, 3.6f)); }
                AstronomicalHazards.Blast(oc, 140f, 9f, 9f);
            }
            float fl = flash * flash; glowC.gameObject.SetActive(t > 2f && t < 9f); if (glowC.gameObject.activeSelf) { Face(glowC, oc); glowC.localScale = Vector3.one * (ORB_R * 3f * (0.8f + 0.2f * Mathf.Sin(t * 4f)) + 110f * fl); glowCMat.color = new Color(0.6f, 0.9f, 1f, 0.22f * (1f - Sm(5f, 9f, t)) * Sm(2f, 4f, t) + 0.8f * fl); }
            // ---- the king
            PlayKing(t); PlaceKing(t);
            // ---- the frozen wind: a wall of white-blue cloud that forms by his hands, grows until 0:27, comes closer, reaches the map at 0:33 (knockback) and blinds you at 0:36
            float grow = Sm(T_CONJ, T_GROW, t), storm = Sm(T_CONJ, T_BLIND, t); float sz = StormZ(t);
            if (!paused && dt > 0f && t >= T_CONJ && t < endT + 1f)
            {
                float rate = (60f + 260f * grow + 220f * Sm(T_GROW, T_HIT, t)) * Parts; accW += rate * dt; int n = (int)accW; accW -= n; float spread = Mathf.Lerp(18f, 150f, grow) * (t >= T_GROW ? 1.2f : 1f); float yc = Mathf.Lerp(80f, 30f, Sm(T_GROW - 1f, T_HIT, t)), yr = Mathf.Lerp(22f, 34f, grow);
                float vz = -Mathf.Lerp(2f, 22f, Sm(T_GROW - 1f, T_GROW + 1f, t)) * (1f - 0.7f * Sm(T_BLIND, T_BLIND + 4f, t));
                for (int k = 0; k < n; k++)
                {
                    Vector3 lp = t < T_HIT + 3f ? new Vector3(UnityEngine.Random.Range(-spread, spread), Mathf.Max(1f, yc + UnityEngine.Random.Range(-yr, yr)), sz + UnityEngine.Random.Range(-14f, 14f)) : new Vector3(UnityEngine.Random.Range(-55f, 55f), UnityEngine.Random.Range(1f, 40f), UnityEngine.Random.Range(-55f, 70f));
                    Emit(wind, W(lp), root.transform.TransformVector(new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-1f, 3f), vz * UnityEngine.Random.Range(0.7f, 1.1f))), new Color(0.82f, 0.93f, 1f, UnityEngine.Random.Range(0.1f, 0.26f)), UnityEngine.Random.Range(16f, 42f) * (0.7f + 0.5f * grow), UnityEngine.Random.Range(4f, 6.5f));
                }
            }
            // the hit at 0:33: knockback from the king's side (it pushes you back, away from him) + a ring of frost, then gusts as long as the storm lasts
            if (!paused && !hitDone && t >= T_HIT && t < T_HIT + 1.5f) { hitDone = true; flash = 1f; AstronomicalHazards.Blast(W(new Vector3(0f, 12f, 62f)), 170f, 22f, 20f); nextGust = t + 2.2f; }
            if (!paused && hitDone && t >= nextGust && t < endT) { nextGust = t + UnityEngine.Random.Range(2.2f, 3.4f); AstronomicalHazards.Blast(W(new Vector3(0f, 10f, 120f)), 220f, 8f, 7f); }
            flash = Mathf.Max(0f, flash - dt * 1.6f);
            // snowfall, white-out, fog, slippery ice, the map turning to snow
            float snowRate = (900f * Sm(24f, 36f, t) + 500f * Sm(36f, 48f, t)) * Parts; Vector3 wv = root.transform.TransformVector(new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, -Mathf.Lerp(0f, 30f, Sm(26f, 38f, t))));
            if (!paused && dt > 0f && snowRate > 1f) IceSnow.Fall(snowRate, wv, dt);
            float white = 0.62f * Sm(T_HIT, T_BLIND, t) + 0.37f * Sm(T_BLIND + 2f, T_WHITE, t); white = Mathf.Min(white, 0.985f); IceSnow.SetWhite(white); IceSnow.SetFog(0.045f * Sm(T_HIT - 1f, T_WHITE, t));
            IceSnow.Update(t); if (!paused && t >= T_HIT) { try { var gp = GTPlayer.Instance; if (gp != null) gp.SetMaximumSlipThisFrame(); } catch { } }
            if (!paused && dt > 0f) { rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float p = (t >= T_HIT && t < T_HIT + 1.2f ? 1f : 0f) + 0.3f * Sm(T_GROW, T_BLIND, t) * (t < endT ? 1f : 0f) + (t >= T_CRACK && t < T_CRACK + 1.2f ? 0.8f : 0f); if (p > 0.05f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(p), 0.14f); gt.StartVibration(false, Mathf.Clamp01(p), 0.14f); } } catch { } } } }
            // atmosphere
            float night = Sm(2f, 8f, t) * (1f - Sm(33f, 44f, t) * 0.5f); RenderSettings.ambientLight = Color.Lerp(ambientSave, Color.Lerp(new Color(0.2f, 0.28f, 0.5f), new Color(0.42f, 0.5f, 0.65f), Sm(33f, 46f, t)), night * 0.85f + 0.15f * Sm(33f, 46f, t)) * (0.9f + 0.6f * flash);
            sun.color = new Color(0.65f, 0.8f, 1f); sun.intensity = 0.45f * night * Mathf.Max(0.4f, Lit); sun.transform.rotation = Quaternion.Euler(55f, yaw + 160f, 0f); orbLamp.transform.position = oc; orbLamp.intensity = Lit * (orbOn ? 2f * (1f - breakK) + 5f * fl : 0f);
            if (t - skyT > 0.1f || t < skyT) { skyT = t; UpdateSky(t); }
        }
        public string DebugInfo() { return "t " + lastT.ToString("0.0") + "  king " + (model != null) + "  origin " + origin + "  hit " + hitDone; }

        public void Cleanup()
        {
            if (ambientSaved) { RenderSettings.ambientLight = ambientSave; ambientSaved = false; }
            bool natural = lastT >= endT - 1.0f; if (natural) { IceSnow.BeginFade(); AstronomicalEventMod.Log.LogInfo("Ice storm over: the snow stays until you leave the map"); } else IceSnow.Clear();
            if (model != null && model.Pivot != null)
            {
                foreach (var kv in origMats) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value; origMats.Clear(); model.Pivot.transform.SetParent(null, false); model.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(model.Pivot);
            }
            kingMats.Clear(); kingEye.Clear(); model = null;
            if (dome != null) UnityEngine.Object.Destroy(dome); dome = null; if (root != null) UnityEngine.Object.Destroy(root); root = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); foreach (var o in owned) if (o != null) UnityEngine.Object.Destroy(o); owned.Clear(); kit = null; built = false; lastT = -1f; skyMesh = null;
        }
    }
}
