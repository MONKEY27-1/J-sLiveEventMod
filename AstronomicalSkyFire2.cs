using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Sky Fire, part 2: the models, the squad that runs with you, and what happens in each scene
    internal sealed partial class AstronomicalSkyFire
    {
        sealed class Mem
        {
            public GameObject Go, Pack; public CRig Rig; public GltfModel M; public float Ahead, Lat, Seed; public TextMesh Timer; public string Cur, Prev; public float CurT, PrevT, Blend = 1f, LastT = -9f;
        }
        readonly List<Mem> squad = new List<Mem>();
        readonly Dictionary<string, Transform> blocks = new Dictionary<string, Transform>();
        readonly List<GameObject> rocks = new List<GameObject>(), saucers = new List<GameObject>();
        readonly List<Material> swirlMats = new List<Material>();
        readonly List<LineRenderer> bolts = new List<LineRenderer>();
        Transform beam, vortex, poolT; Material beamMat, vortexMat; CubeObj cubeP, cubeB, cubeU; TextMesh promptTm, titleTm; Texture2D cubeTex, edgeTex; float boltT; GameObject shipGo;
        static readonly float[] AHEAD = { 2.6f, 4.2f, 3.4f, 5.8f, 4.9f, 1.4f }, LAT = { -1.7f, 1.5f, 0.1f, -0.6f, 2.1f, -2.3f };

        void BuildModels()
        {
            inM = Take("interior", 1.0f, true); viewM = Take("view", 1.0f, true); propM = Take("props", 1.0f, true); ufoM = Take("ufo", 1.2f, false); cubeM = Take("cube", 1.0f, true); shipM = Take("mothership", 1.2f, false); jonM = Take("jones", 1.25f, false); fouM = Take("foundation", 1.25f, false); grdM = Take("guard", 1.25f, false);
        }
        void BuildContent()
        {
            squad.Clear(); blocks.Clear(); bS.Clear(); bE.Clear();
            if (inM != null)
            {
                inM.Pivot.SetActive(true); PlaceByFile(inM, new Vector3(0f, Y_INT, 0f));
                foreach (var t in inM.Pivot.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("S_")) bS[t.name.Substring(2)] = root.transform.InverseTransformPoint(t.position); else if (t.name.StartsWith("E_")) bE[t.name.Substring(2)] = root.transform.InverseTransformPoint(t.position);
                    else if (!blocks.ContainsKey(t.name) && System.Array.IndexOf(new[] { "CELLS", "HALL", "WALK", "TUBE", "ROOMS", "PURPLE", "POOL", "BLUE", "CUBES" }, t.name) >= 0) { blocks[t.name] = t; t.gameObject.SetActive(false); }
                }
                foreach (var kv in origMats) if (kv.Key != null && kv.Key.transform.IsChildOf(inM.Pivot.transform)) { var now = kv.Key.sharedMaterials; for (int i = 0; i < kv.Value.Length && i < now.Length; i++) if (kv.Value[i] != null && kv.Value[i].name == "SF_Swirl" && !swirlMats.Contains(now[i])) swirlMats.Add(now[i]); }
                poolT = blocks.ContainsKey("POOL") ? blocks["POOL"] : null; RegisterParts();
            }
            if (viewM != null)
            {
                viewM.Pivot.SetActive(false); PlaceByFile(viewM, new Vector3(0f, VIEW_Y, VIEW_Z));
                foreach (var t in viewM.Pivot.GetComponentsInChildren<Transform>(true)) { if (t.name == "S_VIEW") bS["VIEW"] = root.transform.InverseTransformPoint(t.position); else if (t.name == "E_VIEW") bE["VIEW"] = root.transform.InverseTransformPoint(t.position); }
            }
            BuildCubeHall();
            JsLiveEventsMod.Log.LogInfo("SkyFire: blocks " + blocks.Count + ", routes " + bS.Count + ", swirl mats " + swirlMats.Count);
            // props: the backpack, rocks and saucers are cloned from the props / ufo files
            var packSrc = Sub(propM, "Backpack");
            GltfModel[] ms = { jonM, fouM, grdM, jonM, fouM, grdM };
            for (int i = 0; i < 6; i++)
            {
                var m = ms[i]; if (m == null) continue; GameObject go = (i < 3) ? m.Pivot : UnityEngine.Object.Instantiate(m.Pivot, root.transform); if (i >= 3) { go.name = "SSquad" + i; clones.Add(go); } go.SetActive(false);
                var mem = new Mem { Go = go, M = m, Rig = CRig.Of(m, go), Ahead = AHEAD[i], Lat = LAT[i], Seed = Hash(i, 3) * 6f };
                if (packSrc != null) { mem.Pack = UnityEngine.Object.Instantiate(packSrc.gameObject, go.transform); mem.Pack.name = "SPack"; mem.Pack.SetActive(true); mem.Pack.transform.localPosition = new Vector3(0f, 1.24f, -0.2f); mem.Pack.transform.localRotation = Quaternion.identity; mem.Pack.transform.localScale = Vector3.one; mem.Timer = MakeText(mem.Pack.transform, 0.009f, new Color(1f, 0.25f, 0.2f, 1f)); mem.Timer.transform.localPosition = new Vector3(0f, 0f, -0.145f); mem.Timer.text = ""; }
                squad.Add(mem); int ii = i; AstronomicalDebug.Anchors["squad" + i] = () => mem.Go != null ? mem.Go.transform.position + Vector3.up * 1.2f : Vector3.zero;
            }
            for (int i = 0; i < 36; i++) { var src = Sub(propM, "Rock" + (i % 3 + 1)); if (src == null) break; var r = UnityEngine.Object.Instantiate(src.gameObject, root.transform); r.name = "SRock" + i; r.SetActive(false); rocks.Add(r); clones.Add(r); }
            for (int i = 0; i < 6; i++) { if (ufoM == null) break; var u = i == 0 ? ufoM.Pivot : UnityEngine.Object.Instantiate(ufoM.Pivot, root.transform); u.name = "SUfo" + i; u.SetActive(false); saucers.Add(u); if (i > 0) clones.Add(u); }
            // beam, vortex, cubes, lightning, prompt, title
            var beamTex = kit.Tex(32, 64, (x, y) => { float e = Mathf.Abs((x + 0.5f) / 16f - 1f); float a = Mathf.Pow(1f - e, 1.4f) * (0.55f + 0.45f * Mathf.Sin(y * 0.8f + x * 0.3f)); return new Color(1f, 1f, 1f, Mathf.Clamp01(a)); }, TextureWrapMode.Repeat);
            beamMat = kit.Glow(new Color(0.35f, 0.85f, 1f, 0.5f), beamTex, 3001); beam = Prim(PrimitiveType.Cylinder, "SBeam", beamMat); beam.gameObject.SetActive(false);
            vortexMat = kit.Glow(new Color(0.7f, 1f, 1f, 0.95f), glowTex, 3003); vortex = Quad("SVortex", vortexMat); vortex.gameObject.SetActive(false);
            cubeTex = kit.Tex(64, 64, (x, y) => { bool l = x % 16 < 1 || y % 16 < 1; float n = WorldKit.Hash(x / 4, y / 4, 5) * 0.12f; float b = 0.36f + n + (l ? 0.22f : 0f); return new Color(b, b, b * 1.1f, 1f); });
            edgeTex = kit.Tex(64, 64, (x, y) => { float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f, e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); float a = Mathf.Clamp01(1f - e * 11f); return new Color(1f, 1f, 1f, 0.16f + 0.84f * a * a); }, TextureWrapMode.Clamp);
            cubeP = MakeCube("SCubeP"); cubeB = MakeCube("SCubeB"); cubeU = MakeCube("SCubeU"); cubeP.Tr.gameObject.SetActive(false); cubeB.Tr.gameObject.SetActive(false); cubeU.Tr.gameObject.SetActive(false);
            var lm = kit.Glow(new Color(0.85f, 0.6f, 1f, 1f), lineTex, 3002); for (int i = 0; i < 6; i++) bolts.Add(MakeLine("SBolt" + i, lm, 14, 0.7f));
            promptTm = MakeText(root.transform, 0.02f, new Color(1f, 1f, 1f, 0f)); promptTm.text = "[E]  REBOOT"; promptTm.gameObject.SetActive(false);
            titleTm = MakeText(root.transform, 0.05f, new Color(1f, 1f, 1f, 0f), 3600); titleTm.text = "OPERATION: SKY FIRE"; titleTm.gameObject.SetActive(false);
            AstronomicalDebug.Anchors["head"] = () => HeadNow(); AstronomicalDebug.Anchors["cubeP"] = () => cubeP.Tr.position; AstronomicalDebug.Anchors["cubeB"] = () => cubeB.Tr.position; AstronomicalDebug.Anchors["ship"] = () => shipM != null && shipM.Pivot != null ? shipM.Pivot.transform.position : Vector3.zero;
            AstronomicalDebug.Anchors["saucer"] = () => saucers.Count > 0 ? saucers[0].transform.position : Vector3.zero;
        }
        static void PlaceGo(GameObject g, Vector3 pos, float scale, Quaternion rot, bool on)
        {
            if (g == null) return; if (g.activeSelf != on) g.SetActive(on); if (!on) return; var tr = g.transform; tr.localPosition = pos; tr.localRotation = rot; tr.localScale = Vector3.one * scale;
        }
        void BackReset() { }
        void ContentCleanup()
        {
            parts.Clear(); partBase.Clear(); partRot.Clear(); steamPts.Clear(); scrollMats.Clear(); rackGos.Clear(); floaters.Clear(); fallCubes.Clear(); floatBase.Clear(); cubeParts.Clear(); routes.Clear(); runeTex = null; rackFrame = rackFace = fallGlow = null;
            squad.Clear(); blocks.Clear(); rocks.Clear(); saucers.Clear(); swirlMats.Clear(); bolts.Clear(); poolT = null; beam = vortex = null; cubeP = cubeB = cubeU = null; promptTm = titleTm = null; cubeTex = edgeTex = null; shipGo = null; textFont = null;
        }

        // ---------------------------------------------------------------- sound (synthesised at load, played from a pool of 3D sources)
        AudioClip sBoom, sBig, sThunder, sWhoosh, sZap; AudioSource[] sPool; int sNext; readonly Dictionary<AudioClip, float> sLast = new Dictionary<AudioClip, float>();
        float SfxVol { get { return Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f) * 0.55f; } }
        static AudioClip MakeSfxClip(string name, float len, Func<float, float> f)
        {
            int n = (int)(44100 * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / 44100f), -1f, 1f);
            int rp = 0; return AudioClip.Create(name, n, 1, 44100, false, buf => { for (int q = 0; q < buf.Length; q++) { buf[q] = d[Mathf.Min(rp, n - 1)]; rp++; } }, pos => { rp = pos; });
        }
        void SfxInit()
        {
            var rnd = new System.Random(12); Func<float> N = () => (float)(rnd.NextDouble() * 2.0 - 1.0); float lp = 0f, lp2 = 0f;
            sBoom = MakeSfxClip("SBoom", 1.7f, t => { lp += (N() - lp) * 0.05f; float ph = 6.2832f * (70f * (1f - Mathf.Exp(-1.6f * t)) / 1.6f + 30f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 2.4f) * 0.8f + lp * 2.2f * Mathf.Exp(-t * 3.2f) + N() * 0.25f * Mathf.Exp(-t * 18f); });
            lp = 0f; lp2 = 0f; sBig = MakeSfxClip("SBig", 3.4f, t => { lp += (N() - lp) * 0.025f; lp2 += (lp - lp2) * 0.2f; float ph = 6.2832f * (48f * (1f - Mathf.Exp(-0.9f * t)) / 0.9f + 22f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 1.3f) * 0.85f + lp2 * 5f * Mathf.Exp(-t * 1.6f) + N() * 0.3f * Mathf.Exp(-t * 12f); });
            lp = 0f; sThunder = MakeSfxClip("SThunder", 3.2f, t => { lp += (N() - lp) * 0.018f; float roll = 0.55f + 0.45f * Mathf.Sin(6.2832f * 4.3f * t) * Mathf.Sin(6.2832f * 1.7f * t); return lp * 7f * roll * Mathf.Exp(-t * 0.85f) + N() * 0.5f * Mathf.Exp(-t * 28f); });
            lp = 0f; lp2 = 0f; sWhoosh = MakeSfxClip("SWhoosh", 1.6f, t => { lp += (N() - lp) * 0.12f; lp2 += (lp - lp2) * 0.3f; float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)); return lp2 * 4f * env + Mathf.Sin(6.2832f * (180f + 400f * t) * t) * 0.15f * env; });
            sZap = MakeSfxClip("SZap", 0.35f, t => { float ph = 6.2832f * (1800f * (1f - Mathf.Exp(-8f * t)) / 8f + 320f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 9f) * 0.55f + Mathf.Sin(ph * 2.01f) * Mathf.Exp(-t * 14f) * 0.2f + N() * 0.1f * Mathf.Exp(-t * 25f); });
            owned.Add(sBoom); owned.Add(sBig); owned.Add(sThunder); owned.Add(sWhoosh); owned.Add(sZap);
            sPool = new AudioSource[10]; for (int i = 0; i < sPool.Length; i++) { var g = new GameObject("SSfx" + i); g.transform.SetParent(root.transform, false); var a = g.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0.85f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 10f; a.maxDistance = 420f; a.priority = 64; sPool[i] = a; }
        }
        void Sfx(AudioClip c, Vector3 wp, float vol, float pitch = 1f, float gap = 0.05f)
        {
            if (c == null || sPool == null || paused) return; float now = Time.unscaledTime, last; if (sLast.TryGetValue(c, out last) && now - last < gap) return; sLast[c] = now;
            AudioSource a = null; for (int k = 0; k < sPool.Length; k++) { var s = sPool[(sNext + k) % sPool.Length]; if (!s.isPlaying) { a = s; sNext = (sNext + k + 1) % sPool.Length; break; } }
            if (a == null) { a = sPool[sNext]; sNext = (sNext + 1) % sPool.Length; }
            a.transform.position = wp; a.clip = c; a.volume = Mathf.Clamp01(vol * SfxVol); a.pitch = Mathf.Clamp(pitch * UnityEngine.Random.Range(0.94f, 1.06f), 0.4f, 2f); a.Play();
        }
        void SfxCleanup0() { sPool = null; sLast.Clear(); sBoom = sBig = sThunder = sWhoosh = sZap = null; }

        // ---------------------------------------------------------------- the countdown on the backpacks (read from the recording: 2:12 at 7:01, 0:30 at 8:42, 0:10 at 9:01, 0:00 at 9:11)
        static readonly float[] TT = { 419f, 421f, 522f, 541f, 551f }, TV = { 134f, 132f, 30f, 10f, 0f };
        static float Timer(float t) { if (t <= TT[0]) return TV[0]; for (int i = 0; i + 1 < TT.Length; i++) if (t < TT[i + 1]) return Mathf.Lerp(TV[i], TV[i + 1], (t - TT[i]) / (TT[i + 1] - TT[i])); return 0f; }
        static string TimerText(float t) { int s = Mathf.Max(0, Mathf.RoundToInt(Timer(t))); return (s / 60) + ":" + (s % 60).ToString("00"); }
        static void GaitOf(string clip, out float period, out float stride)
        {
            switch (clip) { case "Run": period = 0.75f; stride = 3.2f; break; case "Jog": period = 0.85f; stride = 2.3f; break; default: period = 1.0f; stride = 1.5f; break; }
        }
        static readonly HashSet<string> oneShot = new HashSet<string>(new[] { "Death", "Stagger", "Fall", "Panic", "Land", "Hit" });
        Color RoomLight(float t, Seg g, out Color c2, out float i1, out float i2, out Color amb, out float sunI, out Color sunC)
        {
            Color c1 = Color.black; c2 = Color.black; i1 = i2 = 0f; amb = new Color(0.3f, 0.3f, 0.4f); sunI = 0f; sunC = Color.white;
            switch (g.S)
            {
                case Sc.Abduct: c1 = new Color(0.4f, 0.85f, 1f); c2 = new Color(0.5f, 0.8f, 1f); i1 = 2.4f; i2 = 1f; amb = new Color(0.45f, 0.62f, 0.85f); sunI = 1.0f; sunC = new Color(0.6f, 0.85f, 1f); break;
                case Sc.Cells: { c1 = new Color(1f, 0.5f, 0.9f); c2 = new Color(0.4f, 0.6f, 1f); i1 = 2.0f; i2 = 1.5f; amb = new Color(0.42f, 0.32f, 0.6f); if (t < 38f) { float p = Mathf.Max(0f, Mathf.Sin(t * 7f)); c2 = Color.Lerp(c2, new Color(1f, 0.1f, 0.1f), p); i2 += 2.4f * p; } break; }
                case Sc.Hall: c1 = new Color(0.5f, 0.7f, 1f); c2 = new Color(0.9f, 0.5f, 1f); i1 = 2.4f; i2 = 1.6f; amb = new Color(0.32f, 0.42f, 0.68f); break;
                case Sc.Walk: c1 = new Color(0.7f, 0.5f, 1f); c2 = new Color(0.5f, 0.8f, 1f); i1 = 2.2f; i2 = 1.6f; amb = new Color(0.36f, 0.32f, 0.62f); break;
                case Sc.Tube: c1 = new Color(0.4f, 0.95f, 1f); c2 = new Color(0.4f, 0.7f, 1f); i1 = 3.5f; i2 = 2f; amb = new Color(0.3f, 0.62f, 0.72f); break;
                case Sc.Rooms: c1 = new Color(0.4f, 0.9f, 0.9f); c2 = new Color(0.8f, 0.5f, 1f); i1 = 2.2f; i2 = 1.4f; amb = new Color(0.3f, 0.46f, 0.58f); break;
                case Sc.View: c1 = new Color(1f, 0.6f, 0.9f); c2 = new Color(0.7f, 0.5f, 1f); i1 = 1.2f; i2 = 0.8f; amb = new Color(0.58f, 0.42f, 0.72f); sunI = 1.0f; sunC = new Color(1f, 0.72f, 0.9f); break;
                case Sc.Purple: { float k = Sm(285f, 300f, t); c1 = new Color(0.8f, 0.3f, 1f); c2 = new Color(0.5f, 0.25f, 0.9f); i1 = 2.4f * k * (0.8f + 0.2f * Mathf.Sin(t * 5f)); i2 = 1.2f * k; amb = new Color(0.16f, 0.09f, 0.26f) * (0.4f + 0.6f * k); break; }
                case Sc.Blue: case Sc.Lift: { float k = Sm(T_REBOOT, T_REBOOT + 8f, t); if (t < 410f) k = 0f; c1 = Color.Lerp(new Color(0.6f, 0.5f, 0.8f), new Color(0.3f, 0.65f, 1f), k); c2 = new Color(0.3f, 0.5f, 1f); i1 = 2.0f + 1.5f * k; i2 = 1.2f; amb = Color.Lerp(new Color(0.12f, 0.1f, 0.2f), new Color(0.12f, 0.2f, 0.4f), k); break; }
                case Sc.Crystal:
                    {
                        c1 = new Color(0.7f, 0.3f, 1f); c2 = new Color(0.4f, 0.3f, 0.9f); i1 = 2.4f; i2 = 1.4f; amb = new Color(0.22f, 0.12f, 0.35f);
                        if (t > 541f && t < T_ZERO) { float p = Mathf.Max(0f, Mathf.Sin(t * 6.2832f * (1f + (t - 541f) * 0.35f))); c2 = Color.Lerp(c2, new Color(1f, 0.12f, 0.1f), p); i2 = 1.4f + 2.4f * p; }
                        if (t >= T_ZERO) { float fire = Sm(T_ZERO, T_ZERO + 8f, t); float fl = 0.75f + 0.25f * Mathf.PerlinNoise(t * 9f, 2f); c1 = Color.Lerp(c1, new Color(1f, 0.5f, 0.15f), fire); c2 = Color.Lerp(c2, new Color(1f, 0.3f, 0.1f), fire); i1 = Mathf.Lerp(2.4f, 5.5f, fire) * fl; i2 = Mathf.Lerp(1.4f, 3.5f, fire); amb = Color.Lerp(amb, new Color(0.7f, 0.3f, 0.15f), fire); }
                        break;
                    }
                case Sc.Fall: { float k = Sm(T_FALL, T_FALL + 5f, t); c1 = new Color(1f, 0.55f, 0.25f); c2 = new Color(1f, 0.4f, 0.3f); i1 = 2.2f * k; i2 = 1.2f * k; amb = new Color(0.62f, 0.38f, 0.34f) * (0.5f + 0.5f * k); sunI = 1.1f * k; sunC = new Color(1f, 0.62f, 0.38f); break; }
            }
            return c1;
        }

        // ---------------------------------------------------------------- one frame of content
        void Content(float t, float tp, float dt, Seg g, Camera cam)
        {
            Sc sc = g.S; Vector3 fwd = root.transform.forward; Vector3 an; float ayaw, hx, z0, z1; bool have = Anchor(t, g, out an, out ayaw, out hx, out z0, out z1);
            if (!have) an = new Vector3(0f, 1.5f, 0f);
            Vector3 floorP = an - Vector3.up * 1.6f; Vector3 dir = Vector3.forward; Vector3 rs, re; if (Route(g, out rs, out re)) { Vector3 d = re - rs; d.y = 0f; if (d.sqrMagnitude > 0.01f) dir = d.normalized; }
            Vector3 headW = have ? W(an) : HeadNow(); Color c2; float i1, i2, sunI; Color amb, sunC; Color c1 = RoomLight(t, g, out c2, out i1, out i2, out amb, out sunI, out sunC);
            // ---- the blocks of the ship (one at a time) and the deck
            foreach (var kv in blocks) { bool on = kv.Key == g.Blk || (kv.Key == "POOL" && g.S == Sc.Purple); if (kv.Value.gameObject.activeSelf != on) kv.Value.gameObject.SetActive(on); }
            if (viewM != null && viewM.Pivot.activeSelf != (sc == Sc.View)) viewM.Pivot.SetActive(sc == Sc.View);
            PartsFrame(t, tp, dt, g, HeadNow());
            if (poolT != null && sc == Sc.Purple) { float pk = Sm(285f, 298f, t); poolT.localScale = Vector3.one * Mathf.Max(0.001f, pk); }
            // ---- lights (they follow you) and the sky light
            facLight.transform.position = headW + Vector3.up * 2.4f + root.transform.rotation * Quaternion.Euler(0f, ayaw, 0f) * Vector3.forward * 3.5f; facLight2.transform.position = headW + Vector3.up * 1.6f - root.transform.rotation * Quaternion.Euler(0f, ayaw, 0f) * Vector3.forward * 4f;
            if (sc == Sc.Black) { i1 = i2 = 0f; amb = Color.black; }
            facLight.color = c1; facLight2.color = c2; facLight.intensity = Lit * i1; facLight2.intensity = Lit * i2; sun.intensity = sunI; sun.color = sunC; if (sc != Sc.Black && sc != Sc.Abduct && sc != Sc.View && sc != Sc.Fall) { } RenderSettings.ambientLight = amb;
            if (shake > 0.4f && Inside(sc)) { float fl = Mathf.PerlinNoise(t * 24f, 3f) > 0.4f ? 1f : 0.25f; facLight.intensity *= fl; facLight2.intensity *= fl; }
            // ---- the beam and the vortex (0:05 - 0:25)
            bool ab = sc == Sc.Abduct; if (beam != null && beam.gameObject.activeSelf != ab) { beam.gameObject.SetActive(ab); vortex.gameObject.SetActive(ab); }
            if (ab)
            {
                AbductFx(t, tp, dt, an, headW);
                float vk = Sm(18f, 25f, t); Vector3 vp = new Vector3(0f, Mathf.Lerp(150f, 208f, vk), 0f); Face(vortex, W(vp)); float vs = Mathf.Lerp(18f, 130f, vk * vk); vortex.localScale = new Vector3(vs, vs, 1f) * (1f + 0.04f * Mathf.Sin(t * 12f)); vortexMat.color = new Color(0.7f, 1f, 1f, 0.35f + 0.6f * vk);
                if (!paused && dt > 0f) for (int k = 0; k < 3; k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(1f, 9f); Emit(fx, W(new Vector3(Mathf.Cos(a) * r, an.y - 6f + UnityEngine.Random.Range(-10f, 22f), Mathf.Sin(a) * r)), Vector3.up * UnityEngine.Random.Range(4f, 14f), new Color(0.5f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.2f, 0.7f), UnityEngine.Random.Range(0.8f, 1.8f)); }
            }
            // ---- floating rocks: the debris round the beam, the islands round the deck
            RocksFrame(t, sc, an);
            // ---- the squad
            SquadFrame(t, tp, dt, g, an, floorP, dir);
            // ---- each scene's own effects
            switch (sc)
            {
                case Sc.Tube: TubeFx(t, tp, dt, headW, ayaw); break;
                case Sc.Purple: PurpleFx(t, tp, dt, g, headW); break;
                case Sc.Blue: case Sc.Lift: BlueFx(t, tp, dt, g, headW, cam); break;
                case Sc.Crystal: CubesFx(t, tp, dt, g, headW, floorP, dir); break;
                case Sc.Fall: FallFx(t, tp, dt, g, an, cam); break;
            }
            if (sc != Sc.Purple && cubeP != null && cubeP.Tr.gameObject.activeSelf) cubeP.Tr.gameObject.SetActive(false);
            if (sc != Sc.Blue && sc != Sc.Lift && cubeB != null && cubeB.Tr.gameObject.activeSelf) cubeB.Tr.gameObject.SetActive(false);
            if (sc != Sc.Crystal && cubeU != null && cubeU.Tr.gameObject.activeSelf) cubeU.Tr.gameObject.SetActive(false);
            if (sc != Sc.Purple) foreach (var b in bolts) if (b.enabled) b.enabled = false;
            if (promptTm != null && sc != Sc.Blue && promptTm.gameObject.activeSelf) promptTm.gameObject.SetActive(false);
            if (sc != Sc.Crystal) { foreach (var go in rackGos) if (go != null && go.activeSelf) go.SetActive(false); foreach (var go in floaters) if (go != null && go.activeSelf) go.SetActive(false); }
            if (sc != Sc.Fall) foreach (var go in fallCubes) if (go != null && go.activeSelf) go.SetActive(false);
            if (sc != Sc.Crystal && sc != Sc.Fall && sc != Sc.Abduct) for (int i = 0; i < saucers.Count; i++) if (saucers[i] != null && saucers[i].activeSelf) saucers[i].SetActive(false);
            if (sc != Sc.Fall && sc != Sc.Abduct && shipM != null && shipM.Pivot.activeSelf) shipM.Pivot.SetActive(false);
            if (sc == Sc.Fall && t >= T_TITLE && titleTm != null && cam != null)
            {
                if (!titleTm.gameObject.activeSelf) titleTm.gameObject.SetActive(true); float a = Sm(T_TITLE, T_TITLE + 1.5f, t) * (1f - Sm(T_END - 0.7f, T_END, t)); titleTm.color = new Color(1f, 1f, 1f, a); titleTm.transform.position = cam.transform.position + cam.transform.forward * 4f; titleTm.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            }
            else if (titleTm != null && titleTm.gameObject.activeSelf) titleTm.gameObject.SetActive(false);
            // dust motes inside the ship
            if (Inside(sc) && !paused && dt > 0f && UnityEngine.Random.value < dt * 6f) Emit(dust, headW + UnityEngine.Random.insideUnitSphere * 5f, UnityEngine.Random.insideUnitSphere * 0.4f, new Color(0.7f, 0.6f, 1f, 0.35f), 0.25f, 4f);
        }

        // ---------------------------------------------------------------- floating rocks
        void RocksFrame(float t, Sc sc, Vector3 an)
        {
            int n = 0; Vector3 c = Vector3.zero; bool deck = sc == Sc.View, beamRocks = sc == Sc.Abduct;
            if (deck) n = t < 285f ? 12 : t < 410f ? 24 : 36; else if (beamRocks) n = 14;
            for (int i = 0; i < rocks.Count; i++)
            {
                var r = rocks[i]; if (i >= n) { if (r.activeSelf) r.SetActive(false); continue; }
                float h1 = Hash(i, 1), h2 = Hash(i, 2), h3 = Hash(i, 3), h4 = Hash(i, 4); Vector3 pos; float s;
                if (beamRocks) { float a = i * 0.9f + t * (0.35f + 0.1f * h1), rad = 9f + (i % 4) * 4f; pos = new Vector3(Mathf.Cos(a) * rad, an.y - 30f + Mathf.Repeat(i * 17f + (t - 5f) * 7f, 90f), Mathf.Sin(a) * rad); s = 1.2f + 2.6f * h2; }
                else { float dist = 70f + 330f * h1, ang = (h2 - 0.5f) * 2.4f; pos = new Vector3(Mathf.Sin(ang) * dist, VIEW_Y + (h3 - 0.45f) * 130f + 4f * Mathf.Sin(t * 0.3f + i), VIEW_Z + 30f + Mathf.Cos(ang) * dist); s = i < 6 ? 28f + 40f * h4 : 4f + 18f * h4; }
                PlaceGo(r, pos, s, Quaternion.Euler(0f, t * (3f + 6f * h4) + i * 40f, 4f * Mathf.Sin(t * 0.2f + i)), true);
            }
            if (deck && t >= 360f && !paused && UnityEngine.Random.value < 0.35f) { int i = UnityEngine.Random.Range(0, n); var r = rocks[i]; Emit(dust, r.transform.position, Vector3.up * UnityEngine.Random.Range(3f, 9f), new Color(0.3f, 0.2f, 0.25f, 0.5f), UnityEngine.Random.Range(14f, 30f), 5f); if (t >= 475f) Emit(fx, r.transform.position + UnityEngine.Random.insideUnitSphere * 4f, Vector3.up * 5f, new Color(1f, 0.5f, 0.2f, 1f), UnityEngine.Random.Range(2f, 6f), 1.4f); }
        }

        // ---------------------------------------------------------------- scene effects
        void TubeFx(float t, float tp, float dt, Vector3 headW, float ayaw)
        {
            foreach (var m in swirlMats) if (m != null) m.mainTextureOffset = new Vector2(t * 0.9f, t * 0.25f);
            Vector3 f = root.transform.rotation * Vector3.forward;
            if (!paused && dt > 0f) for (int k = 0; k < 3; k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(1.2f, 3.1f); Vector3 p = headW + f * UnityEngine.Random.Range(8f, 40f) + root.transform.right * Mathf.Cos(a) * r + Vector3.up * (Mathf.Sin(a) * r - 0.9f); Emit(fx, p, -f * 40f, new Color(0.5f, 1f, 1f, 1f), 0.18f, 0.5f); }
            Seg g = SegAt(t); if (tp < g.T0 + 0.6f && t >= g.T0 + 0.6f) Sfx(sWhoosh, headW, 0.9f, 1f, 1f);
        }
        void PurpleFx(float t, float tp, float dt, Seg g, Vector3 headW)
        {
            Vector3 s = bS["PURPLE"], c = s + new Vector3(0f, 0f, 28f); float grow = Sm(305f, 326f, t), hover = 7f + 1.0f * Mathf.Sin(t * 0.8f); float size = 12f * grow;
            float charge = Sm(326f, 352f, t), flare = Sm(348f, 355f, t) * (1f - Sm(357f, 360f, t));
            Color core = Color.Lerp(new Color(0.42f, 0.4f, 0.48f), new Color(0.3f, 0.12f, 0.45f), charge); Color aura = Color.Lerp(new Color(0.65f, 0.35f, 0.95f, 0.12f), new Color(0.85f, 0.4f, 1f, 0.75f), charge); aura = Color.Lerp(aura, new Color(1f, 0.9f, 1f, 1f), flare);
            SetCube(cubeP, c + Vector3.up * (hover + size * 0.5f), new Vector3(size, size * 0.9f, size) * 0.9f, t * 6f + 20f, core, aura, grow > 0.01f);
            if (t >= 330f && t < 360f && !paused)
            {
                boltT -= dt; if (boltT <= 0f)
                {
                    boltT = Mathf.Lerp(0.2f, 0.09f, charge); Vector3 from = W(c + Vector3.up * (hover + size * 0.5f)); bool zapped = false;
                    for (int b = 0; b < bolts.Count; b++)
                    {
                        var l = bolts[b]; bool on = UnityEngine.Random.value < 0.35f + 0.5f * charge; l.enabled = on; if (!on) continue; float a = UnityEngine.Random.value * 6.2832f; Vector3 to = W(c + new Vector3(Mathf.Sin(a) * 25f, UnityEngine.Random.Range(0.5f, 20f), Mathf.Cos(a) * 25f));
                        for (int k = 0; k < l.positionCount; k++) { float u = k / (l.positionCount - 1f); Vector3 p = Vector3.Lerp(from, to, u); if (k > 0 && k < l.positionCount - 1) p += UnityEngine.Random.insideUnitSphere * 2.2f * Mathf.Sin(u * 3.14f); l.SetPosition(k, p); }
                        l.widthMultiplier = 0.5f + UnityEngine.Random.value * 0.9f; if (!zapped) { zapped = true; Sfx(sZap, to, 0.5f, UnityEngine.Random.Range(0.7f, 1.2f), 0.12f); flashLamp.transform.position = to; flashLamp.color = new Color(0.85f, 0.6f, 1f); flashLamp.intensity = Lit * 12f; }
                    }
                    if (UnityEngine.Random.value < 0.18f) Sfx(sThunder, headW + UnityEngine.Random.insideUnitSphere * 40f, 0.6f, 1f, 1.2f);
                }
            }
            else foreach (var b in bolts) if (b.enabled) b.enabled = false;
            if (!paused && dt > 0f && t >= 295f) for (int k = 0; k < (int)(2 + 5 * charge); k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(0f, 11f); Emit(fx, W(c + new Vector3(Mathf.Sin(a) * r, 0.2f, Mathf.Cos(a) * r)), Vector3.up * UnityEngine.Random.Range(2f, 9f), new Color(0.85f, 0.4f, 1f, 0.9f), UnityEngine.Random.Range(0.2f, 0.6f), UnityEngine.Random.Range(1f, 2.4f)); }
            if (flare > 0.01f) { flashLamp.transform.position = W(c + Vector3.up * 14f); flashLamp.color = new Color(0.95f, 0.8f, 1f); flashLamp.intensity = Lit * 10f * flare; }
        }
        void BlueFx(float t, float tp, float dt, Seg g, Vector3 headW, Camera cam)
        {
            if (g.S == Sc.Lift) { shake = Mathf.Max(shake, 0.3f + 0.3f * Sm(495f, 500f, t)); if (!paused && dt > 0f) { if (UnityEngine.Random.value < dt * 8f) Emit(dust, headW + UnityEngine.Random.insideUnitSphere * 6f + Vector3.up * 6f, Vector3.down * 2f, new Color(0.6f, 0.6f, 0.8f, 0.4f), 1.5f, 1.5f); if (Mathf.FloorToInt(t / 3f) != Mathf.FloorToInt(tp / 3f)) Sfx(sBig, headW, 0.5f, 0.5f, 1f); } }
            Vector3 c = bS["BLUE"] + new Vector3(0f, 0f, 38f); float rb = Sm(T_REBOOT, T_REBOOT + 6f, t), rise = Sm(T_REBOOT + 4f, T_REBOOT + 24f, t); if (t >= 495f) { rb = 1f; rise = 1f; }
            Color core = Color.Lerp(new Color(0.42f, 0.4f, 0.46f), new Color(0.3f, 0.68f, 1f), rb), aura = Color.Lerp(new Color(0.55f, 0.5f, 0.75f, 0.12f), new Color(0.45f, 0.88f, 1f, 0.7f), rb);
            float h = Mathf.Lerp(7f, 9f, rise); Vector3 cc = c + new Vector3(0f, 3.4f + h * 0.5f + 0.4f * Mathf.Sin(t * 1.1f), 0f); SetCube(cubeB, cc, new Vector3(h, h, h), t * 9f + 20f, core, aura, true);
            bool pr = t > 418f && t < 428f && promptTm != null; if (promptTm != null && promptTm.gameObject.activeSelf != pr) promptTm.gameObject.SetActive(pr);
            if (pr) { Vector3 pp = W(c + new Vector3(0f, 2.3f, -7.5f)); promptTm.transform.position = pp; if (cam != null) { Vector3 d = pp - cam.transform.position; d.y = 0f; if (d.sqrMagnitude > 0.01f) promptTm.transform.rotation = Quaternion.LookRotation(d, Vector3.up); } promptTm.color = new Color(1f, 1f, 1f, Sm(418f, 419f, t) * (1f - Sm(T_REBOOT, T_REBOOT + 1.5f, t) * 0.7f)); }
            if (tp < T_REBOOT && t >= T_REBOOT && !paused) { Boom(W(cc), 9f, 0.6f, new Color(0.4f, 0.85f, 1f), false); flashLamp.intensity = Lit * 14f; for (int k = 0; k < 240; k++) { var d = UnityEngine.Random.onUnitSphere; Emit(fx, W(cc), d * UnityEngine.Random.Range(6f, 26f), new Color(0.4f, 0.9f, 1f, 1f), UnityEngine.Random.Range(0.3f, 1.4f), UnityEngine.Random.Range(0.8f, 2.2f)); } }
            if (rb > 0.3f && !paused && dt > 0f) for (int k = 0; k < 3; k++) Emit(fx, W(cc + UnityEngine.Random.onUnitSphere * h * 0.75f), Vector3.up * UnityEngine.Random.Range(0.5f, 3f), new Color(0.6f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.12f, 0.4f), UnityEngine.Random.Range(1f, 2.4f));
        }
        void FallFx(float t, float tp, float dt, Seg g, Vector3 an, Camera cam)
        {
            FallCubes(t, tp, dt, an);
            // the Mothership far above and ahead: it burns and tears apart (9:50 - 10:09 is mostly this view)
            if (shipM != null)
            {
                float sz = Mathf.Max(1f, shipM.RendererBounds.size.x); float sc2 = 380f / sz; Vector3 sp = new Vector3(30f, 470f, 330f); bool on = t < T_END + 1f;
                PlaceGo(shipM.Pivot, sp + Vector3.up * (-10f * Sm(T_FALL, T_END, t)), sc2, Quaternion.Euler(8f + 6f * Mathf.Sin(t * 0.3f), 20f + (t - T_FALL) * 1.2f, 10f * Mathf.Sin(t * 0.2f)), on);
                if (!paused && dt > 0f) for (int q = 0; q < (int)(6 * Parts + 3); q++) { Vector3 o = UnityEngine.Random.insideUnitSphere; o.y *= 0.45f; Emit(fx, W(sp + o * 170f), o.normalized * UnityEngine.Random.Range(6f, 26f) + Vector3.up * 4f, new Color(1f, UnityEngine.Random.Range(0.35f, 0.75f), 0.15f, 0.95f), UnityEngine.Random.Range(10f, 30f), UnityEngine.Random.Range(1.5f, 3.2f)); }
                float[] bt = { 570.4f, 573f, 576f, 579f, 582f, 585f, 588f, 591f, 594f, 597f, 600.5f, 604f, 607f };
                for (int i = 0; i < bt.Length; i++) if (tp < bt[i] && t >= bt[i] && !paused) { Vector3 o = UnityEngine.Random.insideUnitSphere; o.y *= 0.4f; Boom(W(sp + o * 140f), 36f + 6f * (i % 3), 0.9f, new Color(1f, 0.55f, 0.2f), false); }
            }
            // wind streaks past you
            if (!paused && dt > 0f && t < T_LAND) for (int k = 0; k < 4; k++) { Vector3 p = W(an) + UnityEngine.Random.insideUnitSphere * 14f; Emit(fx, p, Vector3.up * UnityEngine.Random.Range(40f, 80f), new Color(1f, 0.85f, 0.7f, 0.5f), 0.16f, 0.5f); }
            if (!paused && dt > 0f && UnityEngine.Random.value < dt * 8f) Emit(dust, W(an) + UnityEngine.Random.insideUnitSphere * 40f, Vector3.up * 6f, new Color(0.35f, 0.2f, 0.2f, 0.5f), UnityEngine.Random.Range(8f, 20f), 5f);
            if (tp < T_FALL + 0.1f && t >= T_FALL + 0.1f && !paused) { shake = Mathf.Max(shake, 1.0f); }
            if (t >= T_LAND - 3f && tp < T_LAND - 3f && !paused) Sfx(sBig, W(an) + Vector3.up * 30f, 0.5f, 0.8f, 1f);
        }
    }
}
