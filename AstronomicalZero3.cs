using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Zero Crisis, part 3: the scenery and the effects of each scene (bus, meteor, Zero Point + tendrils + bloom, Rifts and Reality Waves, wolves, the Spire and its deck, the Foundation's sacrifice, captions)
    internal sealed partial class AstronomicalZero
    {
        GameObject busGo, spireGo, metGo, zpGo, bflyGo, chamGo, lotGo; float busS = 1f, spireS = 1f, metS = 1f, zpS = 105f, bflyS = 1f; const float spireY = IY;
        readonly List<GameObject> cryGos = new List<GameObject>(), wolfGos = new List<GameObject>(), slabGos = new List<GameObject>(), rockGos = new List<GameObject>();
        readonly List<Vector3> cryPos = new List<Vector3>(); readonly List<float> cryAt = new List<float>(), cryS = new List<float>(), cryYaw = new List<float>();
        sealed class Rift { public GameObject Go; public Transform[] Rings; public Transform Disc, Beam; public Transform[] Shards; public Vector3 P; public float Yaw; public float Ground; }
        readonly List<Rift> rifts = new List<Rift>();
        readonly List<Transform[]> petals = new List<Transform[]>(); readonly List<Material> lotMats = new List<Material>();
        readonly List<LineRenderer> tends = new List<LineRenderer>(), ropes = new List<LineRenderer>(), bolts = new List<LineRenderer>(); LineRenderer devBeam; Material tendMat, boltMat;
        TextMesh capTm, subTm; Transform ball; Material ballMat; Vector3[] tendDir; float[] tendLen, tendPh;
        float bflyYaw; Vector3 bflyW; bool bflyInit; float metGround, chamGround;

        void BuildContent()
        {
            BuildActors(); BuildIsland();
            if (busM != null) { busGo = busM.Pivot; busGo.SetActive(false); var s = busM.RendererBounds.size; busS = 16f / Mathf.Max(0.1f, Mathf.Max(s.x, s.z)); BusRoof = new Vector3(0f, (-0.5f + 0.3f) * s.y * busS, 0f); }
            if (spireM != null) { spireGo = spireM.Pivot; spireGo.SetActive(false); { var ss = spireM.RendererBounds.size; spireS = (SPY + 6f) / Mathf.Max(1f, Mathf.Max(ss.x, ss.y, ss.z)); } }
            if (metM != null) { metGo = metM.Pivot; metGo.SetActive(false); { var ms = metM.RendererBounds.size; metS = 18f / Mathf.Max(0.05f, Mathf.Max(ms.x, ms.y, ms.z)); } }
            if (zpM != null) { zpGo = zpM.Pivot; zpGo.SetActive(false); }
            if (bflyM != null) { bflyGo = bflyM.Pivot; bflyGo.SetActive(false); bflyS = 0.9f / Mathf.Max(0.05f, Mathf.Max(bflyM.RendererBounds.size.x, bflyM.RendererBounds.size.y)); }
            if (chamM != null) { chamGo = chamM.Pivot; chamGo.SetActive(false); }
            if (lotM != null) { lotGo = lotM.Pivot; lotGo.SetActive(false); for (int r = 0; r < 3; r++) { var l = new List<Transform>(); for (int k = 0; k < 24; k++) { var p = Sub(lotM, "Petal" + r + "_" + k); if (p != null) l.Add(p); } petals.Add(l.ToArray()); } foreach (var rr in lotGo.GetComponentsInChildren<Renderer>(true)) foreach (var m in rr.sharedMaterials) if (m != null && !lotMats.Contains(m)) lotMats.Add(m); }
            // crystals and corrupted rocks of the pink island
            if (cryM != null)
            {
                string[] kinds = { "CryA", "CryB", "CryC", "CryD", "CryE", "CryF", "Spike", "DarkA", "DarkB", "DarkC" };
                for (int i = 0; i < 46; i++)
                {
                    string kn = kinds[i < 6 ? i : (i % 10)]; var src = Sub(cryM, kn); if (src == null) continue; float a = Hash(i, 1) * 6.2832f, r = 20f + 105f * Mathf.Pow(Hash(i, 2), 0.8f); float x = Mathf.Sin(a) * r, z = 70f + Mathf.Cos(a) * r * 0.95f;
                    if (Mathf.Abs(x) < 12f && z > -6f && z < 215f) x += (x < 0 ? -1f : 1f) * 14f; if (z > 215f && Mathf.Abs(x) < 50f && z < 320f) continue;
                    var go = UnityEngine.Object.Instantiate(src.gameObject, root.transform); go.name = "ZCry" + i; go.SetActive(false); clones.Add(go); cryGos.Add(go); cryPos.Add(new Vector3(x, IY - 0.25f, z)); cryAt.Add(58f + 40f * Hash(i, 3));
                    cryS.Add(kn == "Spike" ? 1.1f + 1.2f * Hash(i, 4) : kn.StartsWith("Dark") ? 1.1f + 0.9f * Hash(i, 4) : 1.5f + 1.9f * Hash(i, 4)); cryYaw.Add(Hash(i, 5) * 360f);
                }
            }
            // four Rifts
            Vector3[] rp = { P1, P2, P3, P4 };
            for (int i = 0; i < 4 && riftM != null; i++)
            {
                var go = i == 0 ? riftM.Pivot : UnityEngine.Object.Instantiate(riftM.Pivot, root.transform); go.name = "ZRift" + i; if (i > 0) clones.Add(go); go.SetActive(false);
                var rf = new Rift { Go = go }; var rg = new List<Transform>(); for (int k = 0; k < 4; k++) { var tr = SubStart(go, "Ring" + k); if (tr != null) rg.Add(tr); } rf.Rings = rg.ToArray(); rf.Disc = SubStart(go, "Disc"); rf.Beam = SubStart(go, "Beam"); var sh = new List<Transform>(); for (int k = 0; k < 12; k++) { var tr = SubStart(go, "Shard" + k); if (tr != null) sh.Add(tr); } rf.Shards = sh.ToArray();
                rf.P = new Vector3(rp[i].x, IY, rp[i].z); Vector3 c; Vector3 f; float rad; Sc[] sc = { Sc.Raz, Sc.Cluck, Sc.Wolves, Sc.Lara }; Station(sc[i], out c, out f, out rad); rf.Yaw = Mathf.Atan2(c.x - rp[i].x, c.z - rp[i].z) * Mathf.Rad2Deg; rifts.Add(rf);
            }
            // wolves, steps and floating rocks of the Spire
            for (int i = 0; i < 10 && wolfM != null; i++) { var go = i == 0 ? wolfM.Pivot : UnityEngine.Object.Instantiate(wolfM.Pivot, root.transform); go.name = "ZWolf" + i; if (i > 0) clones.Add(go); go.SetActive(false); wolfGos.Add(go); }
            for (int i = 0; i < 44 && rockM != null; i++) { var src = Sub(rockM, "Slab" + (i % 3)); if (src == null) break; var go = UnityEngine.Object.Instantiate(src.gameObject, root.transform); go.name = "ZSlab" + i; go.SetActive(false); clones.Add(go); slabGos.Add(go); }
            for (int i = 0; i < 40 && rockM != null; i++) { var src = Sub(rockM, "Rock" + (i % 6)); if (src == null) break; var go = UnityEngine.Object.Instantiate(src.gameObject, root.transform); go.name = "ZRock" + i; go.SetActive(false); clones.Add(go); rockGos.Add(go); }
            // lines: tendrils from the Zero Point, bus ropes, the device beam, energy bolts
            tendMat = kit.Glow(new Color(1f, 0.6f, 0.95f, 0.8f), lineTex, 3002); boltMat = kit.Glow(new Color(0.7f, 0.95f, 1f, 1f), lineTex, 3003);
            int nt = 30; tendDir = new Vector3[nt]; tendLen = new float[nt]; tendPh = new float[nt];
            for (int i = 0; i < nt; i++) { float az = (Hash(i, 11) - 0.5f) * 2.4f, el = -0.15f + 1.2f * Hash(i, 12); tendDir[i] = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), -Mathf.Cos(az) * Mathf.Cos(el)); tendLen[i] = 260f + 700f * Hash(i, 13); tendPh[i] = Hash(i, 14) * 6.28f; tends.Add(MakeLine("ZTend" + i, tendMat, 22, 7f)); }
            for (int i = 0; i < 2; i++) ropes.Add(MakeLine("ZRope" + i, kit.Glow(new Color(0.8f, 0.8f, 0.85f, 1f), null, 3000), 6, 0.06f)); devBeam = MakeLine("ZDevBeam", boltMat, 10, 0.35f);
            for (int i = 0; i < 6; i++) bolts.Add(MakeLine("ZBolt" + i, boltMat, 12, 0.5f));
            capTm = MakeText(root.transform, 0.045f, new Color(1f, 1f, 1f, 0f), 3600); subTm = MakeText(root.transform, 0.02f, new Color(1f, 0.9f, 1f, 0f), 3600);
            ball = Prim(PrimitiveType.Sphere, "ZBall", null); ballMat = kit.Glow(new Color(0.65f, 0.9f, 1f, 0.5f), null, 3001); ball.GetComponent<Renderer>().sharedMaterial = ballMat; ball.gameObject.SetActive(false);
            AstronomicalDebug.Anchors["head"] = () => HeadNow(); AstronomicalDebug.Anchors["zp"] = () => W(ZPOS); AstronomicalDebug.Anchors["bus"] = () => busGo != null ? busGo.transform.position : Vector3.zero;
            AstronomicalDebug.Anchors["jones"] = () => actBy.ContainsKey("jones") ? actBy["jones"].Go.transform.position + Vector3.up * 1.2f : Vector3.zero; AstronomicalDebug.Anchors["found"] = () => actBy.ContainsKey("found") ? actBy["found"].Go.transform.position + Vector3.up * 1.2f : Vector3.zero;
            AstronomicalDebug.Anchors["spire"] = () => W(SpireC + Vector3.up * (spireY + 40f)); AstronomicalDebug.Anchors["top"] = () => W(SpireTop + Vector3.up * (spireY + 3f)); AstronomicalDebug.Anchors["rift1"] = () => rifts.Count > 0 ? W(rifts[0].P + Vector3.up * 5f) : Vector3.zero;
            AstronomicalDebug.Anchors["meteor"] = () => W(new Vector3(0f, IY + 4f, 118f)); AstronomicalDebug.Anchors["arena"] = () => W(new Vector3(0f, IY + 2f, 26f)); AstronomicalDebug.Anchors["zone"] = () => W(new Vector3(0f, IY + 2f, 100f));
        }
        void ContentCleanup()
        {
            acts.Clear(); actBy.Clear(); charM.Clear(); cryGos.Clear(); cryPos.Clear(); cryAt.Clear(); cryS.Clear(); cryYaw.Clear(); wolfGos.Clear(); slabGos.Clear(); rockGos.Clear(); rifts.Clear(); petals.Clear(); lotMats.Clear(); tends.Clear(); ropes.Clear(); bolts.Clear();
            busGo = spireGo = metGo = zpGo = bflyGo = chamGo = lotGo = devGo = null; devGlow = null; peelyM = null; busM = spireM = wolfM = devM = metM = zpM = bflyM = cryM = lotM = riftM = rockM = chamM = null; isGo = null; isMesh = null; treeM = null; treeGos.Clear(); treePos.Clear(); treeMats.Clear(); devBeam = null; capTm = subTm = null; ball = null; bflyInit = false; isT = -9f;
        }

        // ---------------------------------------------------------------- the frame
        void Content(float t, float tp, float dt, Seg g, Camera cam)
        {
            ActorsFrame(t, dt); PeelyFrame(t); IslandFrame(t, g); CrystalFrame(t, g); BusFrame(t, tp, dt); MeteorFrame(t, tp, dt); ZeroFrame(t, tp, dt, cam); RiftFrame(t, tp, dt, cam); WaveFrame(t, tp, cam); ButterflyFrame(t, dt, cam); WolfFrame(t, dt);
            SpireFrame(t, tp, dt); AmbientFrame(t, tp, dt, g, cam); CaptionFrame(t, cam);
            // light and mood
            Color amb = Color.Lerp(ambientSave, MoodAmbient(g, t), 0.75f); RenderSettings.ambientLight = amb; sun.intensity = 0.55f * Lit; sun.color = Color.Lerp(Color.white, MoodAmbient(g, t), 0.5f);
        }
        Color MoodAmbient(Seg g, float t)
        {
            switch (g.S) { case Sc.Raz: return new Color(0.55f, 0.45f, 0.9f); case Sc.Cluck: return new Color(1f, 0.7f, 0.45f); case Sc.Wolves: return new Color(0.2f, 0.4f, 0.9f); case Sc.Lara: return new Color(1f, 0.8f, 0.6f); case Sc.Bloom: return Color.Lerp(new Color(1f, 0.6f, 0.85f), new Color(1f, 0.3f, 0.25f), Sm(466f, 490f, t)); case Sc.Spire: case Sc.Top: return new Color(0.4f, 0.6f, 1f); case Sc.Reel: return new Color(1f, 0.7f, 0.4f); default: return new Color(0.95f, 0.75f, 0.95f); }
        }
        void CrystalFrame(float t, Seg g)
        {
            bool pink = (t >= 58f && t < 340f && g.S != Sc.Cluck) || (t >= 450f && t < 500f);
            for (int i = 0; i < cryGos.Count; i++)
            {
                var go = cryGos[i]; if (!pink) { if (go.activeSelf) go.SetActive(false); continue; }
                float grow = Sm(cryAt[i], cryAt[i] + 5f, t) * (t >= 450f ? 1f - Sm(493f, 500f, t) : 1f) * (t >= 450f ? Sm(450f, 458f, t) : 1f); if (grow < 0.01f) { if (go.activeSelf) go.SetActive(false); continue; }
                PlaceGo(go, cryPos[i], cryS[i] * grow, Quaternion.Euler(0f, cryYaw[i], 0f), true);
            }
        }
        // Peely (a banana with no skeleton) hops about at the side lines in the fight (0:50 - 1:40) and in the reel
        void PeelyFrame(float t)
        {
            if (peelyM == null) return; var go = peelyM.Pivot; bool on = (t >= 50f && t < 100f) || (t >= 602f && t < T_END + 2f); if (go.activeSelf != on) go.SetActive(on); if (!on) return;
            float ph = t * 3.4f, hop = Mathf.Abs(Mathf.Sin(ph)); float sq = 1f + 0.18f * Mathf.Cos(ph * 2f); Vector3 p = t < 100f ? new Vector3(25f + 2f * Mathf.Sin(t * 0.7f), IY + hop * 1.3f, 12f + 3f * Mathf.Cos(t * 0.5f)) : new Vector3(-6f, IY + hop * 1.1f, 28f);
            float s0 = Fit(peelyM, 1.9f); go.transform.localPosition = p; go.transform.localRotation = Quaternion.Euler(8f * Mathf.Sin(ph), 200f + 30f * Mathf.Sin(t * 0.9f), 6f * Mathf.Cos(ph)); go.transform.localScale = new Vector3(s0 / sq, s0 * sq, s0 / sq);
        }
        // ---------------------------------------------------------------- 0:04 the Battle Bus
        void BusFrame(float t, float tp, float dt)
        {
            bool on = t >= 0f && t < 36.5f; if (busGo == null) return; if (busGo.activeSelf != on) busGo.SetActive(on); if (!on) return;
            Vector3 p = BusPos(t); float roll = 2.5f * Mathf.Sin(t * 0.7f), pitch = 1.5f * Mathf.Sin(t * 0.5f + 1f);
            float by = BusYaw(t); Quaternion bq = Quaternion.Euler(0f, by, 0f); busGo.transform.localPosition = p; busGo.transform.localRotation = bq * Quaternion.Euler(pitch, 0f, roll); busGo.transform.localScale = Vector3.one * busS;
            Act j; bool hang = t < 29.5f && actBy.TryGetValue("jones", out j);
            for (int i = 0; i < ropes.Count; i++) { var l = ropes[i]; if (l.enabled != hang) l.enabled = hang; if (!hang) continue; Vector3 top = W(p + BusRoof + bq * new Vector3(i == 0 ? 0.9f : 1.9f, 3.4f, -7.4f)); Vector3 hand = W(p + bq * new Vector3(1.4f + (i == 0 ? -0.3f : 0.3f), BusRoof.y - 2.4f, -7.6f)); for (int k = 0; k < l.positionCount; k++) l.SetPosition(k, Vector3.Lerp(top, hand, k / (float)(l.positionCount - 1))); }
            if (!paused && dt > 0f && t < 36f) { Vector3 tail = W(p + bq * new Vector3(0f, BusRoof.y, -8f)); if (UnityEngine.Random.value < 0.6f) Emit(dust, tail, new Vector3(0f, 0f, -6f), new Color(1f, 0.9f, 1f, 0.35f), 4f, 3f); }
        }
        // ---------------------------------------------------------------- 1:52 the Foundation's meteor falls and lands (2:00), smoulders, and opens at 2:20
        void MeteorFrame(float t, float tp, float dt)
        {
            if (metGo == null) return; bool on = t >= 108f && t < 345f; if (metGo.activeSelf != on) metGo.SetActive(on); if (!on) return;
            Vector3 land = new Vector3(0f, IY + 1.4f, 120f);
            float f = Mathf.Clamp01((t - 108f) / 11f); Vector3 start = land + new Vector3(70f, 300f, 220f); Vector3 p = Vector3.Lerp(start, land, f * f * (0.4f + 0.6f * f)); bool flying = f < 1f;
            metGo.transform.localPosition = p; metGo.transform.localScale = Vector3.one * metS * (flying ? 1.2f : 1f); metGo.transform.localRotation = flying ? Quaternion.Euler(t * 40f, t * 25f, 0f) : Quaternion.Euler(14f, 205f, 8f);
            if (!paused && dt > 0f)
            {
                if (flying) { Vector3 wp = W(p); for (int k = 0; k < (int)(10 * Parts + 2); k++) { Emit(fx, wp + UnityEngine.Random.insideUnitSphere * 2f, Vector3.up * 4f + UnityEngine.Random.insideUnitSphere * 5f, new Color(1f, UnityEngine.Random.Range(0.4f, 0.8f), 0.15f, 0.9f), UnityEngine.Random.Range(3f, 7f), UnityEngine.Random.Range(0.8f, 1.6f)); } Emit(smoke, wp, Vector3.up * 3f, new Color(0.5f, 0.4f, 0.55f, 0.35f), UnityEngine.Random.Range(8f, 14f), 3.5f); }
                else if (t < 200f && UnityEngine.Random.value < 0.14f) { Vector3 wp = W(land + new Vector3(UnityEngine.Random.Range(-2f, 2f), 3f, UnityEngine.Random.Range(-2f, 2f))); Emit(smoke, wp, Vector3.up * 3f, new Color(0.62f, 0.5f, 0.68f, 0.22f), UnityEngine.Random.Range(5f, 9f), 3.2f); if (UnityEngine.Random.value < 0.4f) Emit(fx, wp, Vector3.up * 8f + UnityEngine.Random.insideUnitSphere * 3f, new Color(1f, 0.5f, 0.2f, 0.9f), 0.8f, 1.2f); }
                if (tp < 119f && t >= 119f) { Boom(W(land), 16f, 1.1f, new Color(1f, 0.6f, 0.3f), true); for (int k = 0; k < 220; k++) { float a = k / 220f * 6.2832f; Emit(dust, W(land) + Vector3.up * 0.5f, new Vector3(Mathf.Sin(a), 0.05f, Mathf.Cos(a)) * UnityEngine.Random.Range(14f, 34f), new Color(1f, 0.8f, 0.9f, 0.55f), 5f, 3.5f); } Sfx(sThunder, W(land), 1f, 0.8f); }
                if (tp < 141f && t >= 141f) { flashLamp.transform.position = W(land + Vector3.up * 3f); flashLamp.color = new Color(0.6f, 0.85f, 1f); flashLamp.intensity = Lit * 10f; Sfx(sZap, W(land), 0.9f, 0.6f); for (int k = 0; k < 90; k++) Emit(fx, W(land + Vector3.up * 2f), UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 22f), new Color(0.6f, 0.9f, 1f, 1f), 1.2f, 1.6f); }
            }
        }
        // ---------------------------------------------------------------- the Zero Point: a huge cracked sphere over the island, pink tendrils from 3:30, the bloom from 4:25
        void ZeroFrame(float t, float tp, float dt, Camera cam)
        {
            bool zpOn = t >= 84f && t < 500f; Vector3 zw = W(ZPOS); Vector3 view = cam != null ? cam.transform.position : originW;
            if (zpGo != null)
            {
                if (zpGo.activeSelf != zpOn) zpGo.SetActive(zpOn);
                if (zpOn) { float grow = Sm(84f, 108f, t) * (0.85f + 0.15f * Mathf.Sin(t * 1.3f)) * (t > 466f ? 1f + 0.4f * Sm(466f, 495f, t) : 1f); zpGo.transform.position = zw; zpGo.transform.rotation = Quaternion.LookRotation(zw - view, Vector3.up); zpGo.transform.localScale = Vector3.one * zpS * Mathf.Max(0.01f, grow) * (t > 230f ? 1f + 0.05f * Mathf.Sin(t * 7f) : 1f); Bounds zb = new Bounds(zpGo.transform.position, Vector3.zero); bool zfirst = true; foreach (var zr in zpGo.GetComponentsInChildren<Renderer>()) { if (zfirst) { zb = zr.bounds; zfirst = false; } else zb.Encapsulate(zr.bounds); } if (!zfirst) zpGo.transform.position += zw - zb.center; }
            }
            // tendrils
            float amt = Sm(205f, 262f, t) * (t < 500f ? 1f : 0f); int show = (int)(tends.Count * amt); bool red = t > 466f; Color tc = Color.Lerp(new Color(1f, 0.55f, 0.95f, 0.85f), new Color(1f, 0.25f, 0.2f, 0.9f), Sm(466f, 490f, t)); if (tendMat != null) tendMat.color = tc;
            for (int i = 0; i < tends.Count; i++)
            {
                var l = tends[i]; bool on = i < show && t >= 205f; if (l.enabled != on) l.enabled = on; if (!on) continue; float grow = Mathf.Clamp01(amt * 1.3f - i * 0.01f) * (1f + 0.25f * Sm(450f, 495f, t)); Vector3 d = tendDir[i], p1 = Vector3.Cross(d, Vector3.up).normalized, p2 = Vector3.Cross(d, p1).normalized; int n = l.positionCount;
                for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); float amp = 60f * s * (1f + 0.5f * Mathf.Sin(t * 0.4f + tendPh[i])); Vector3 pt = zw + d * (tendLen[i] * s * grow) + p1 * Mathf.Sin(s * 6f + t * 0.7f + tendPh[i]) * amp + p2 * Mathf.Cos(s * 5f - t * 0.5f + tendPh[i]) * amp; l.SetPosition(k, pt); }
                l.widthMultiplier = 11f + 5f * Mathf.Sin(t * 1.1f + tendPh[i]) + 8f * Sm(450f, 495f, t);
            }
            // the bloom
            if (lotGo != null)
            {
                bool lo = t >= 262f && t < 500f; if (lotGo.activeSelf != lo) lotGo.SetActive(lo);
                if (lo)
                {
                    float open = Mathf.Clamp01(Sm(262f, 300f, t) * 0.32f + Sm(380f, 462f, t) * 0.68f); float S = Mathf.Lerp(20f, 360f, Sm(262f, 470f, t)) * (1f - Sm(494f, 500f, t)); Vector3 toV = (Lc(view) - ZPOS).normalized;
                    lotGo.transform.localPosition = ZPOS; lotGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, toV) * Quaternion.Euler(0f, t * 2f, 0f); lotGo.transform.localScale = Vector3.one * S;
                    for (int r = 0; r < petals.Count; r++) { var ps = petals[r]; for (int k = 0; k < ps.Length; k++) { float az = 360f * (k + 0.5f * r) / ps.Length; float tilt = Mathf.Lerp(6f + 3f * r, 88f - r * 12f, open) + 3f * Mathf.Sin(t * 1.2f + k); ps[k].localRotation = Quaternion.Euler(0f, az, 0f) * Quaternion.Euler(tilt, 0f, 0f); } }
                    Color pc = Color.Lerp(Color.white, new Color(1f, 0.28f, 0.22f), Sm(466f, 490f, t)); foreach (var m in lotMats) { if (m != null) m.color = new Color(pc.r, pc.g, pc.b, 0.85f); }
                }
            }
            // the Foundation holds it together: streams of light from him to the Zero Point
            Act f; bool hold = actBy.TryGetValue("found", out f) && f.Vis && ((t >= 246f && t < 339f) || (t >= 409f && t < 500f));
            for (int i = 0; i < 3 && i < bolts.Count; i++)
            {
                var l = bolts[i]; if (l.enabled != hold) l.enabled = hold; if (!hold) continue; Vector3 a = f.Go.transform.position + Vector3.up * 1.7f + (i - 1) * f.Go.transform.right * 0.6f; Vector3 b = zw; int n = l.positionCount;
                for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); Vector3 pt = Vector3.Lerp(a, b, s * s * (3f - 2f * s)); pt += new Vector3(Mathf.Sin(s * 11f + t * 9f + i), Mathf.Cos(s * 9f + t * 8f), Mathf.Sin(s * 7f - t * 7f)) * (6f * Mathf.Sin(s * Mathf.PI)); l.SetPosition(k, pt); }
                l.widthMultiplier = 0.8f + 0.5f * Mathf.Sin(t * 12f + i);
            }
            // flares at 3:50 - 4:20 (the Zero Point flares and throws light across the island)
            if (!paused && dt > 0f) foreach (float ft in new[] { 240f, 258f, 330f }) if (tp < ft && t >= ft) { flashLamp.transform.position = W(new Vector3(0f, IY + 40f, 60f)); flashLamp.color = new Color(0.8f, 0.85f, 1f); flashLamp.intensity = Lit * 12f; Sfx(sThunder, W(new Vector3(0f, IY + 40f, 80f)), 1f, 0.7f); shake = Mathf.Max(shake, 0.3f); }
            if (!paused && dt > 0f && t > 210f && t < 500f && UnityEngine.Random.value < 0.5f) { var p = W(new Vector3(UnityEngine.Random.Range(-60f, 60f), IY + UnityEngine.Random.Range(30f, 120f), UnityEngine.Random.Range(40f, 200f))); Emit(fx, p, UnityEngine.Random.insideUnitSphere * 2f, Color.Lerp(new Color(1f, 0.6f, 0.95f), new Color(0.6f, 0.9f, 1f), UnityEngine.Random.value), 1.1f, 3f); }
        }
        // ---------------------------------------------------------------- the four Rifts: a ring of light with a beam to the sky; you close each by walking up to it
        static readonly float[] RF_ON = { 268f, 326f, 326f, 326f };
        void RiftFrame(float t, float tp, float dt, Camera cam)
        {
            for (int i = 0; i < rifts.Count; i++)
            {
                var rf = rifts[i]; float close = PORTAL_CLOSE[i]; bool win = i == 0 ? t >= RF_ON[0] && t < close + 6f : i == 1 ? t >= RF_ON[1] && t < close + 6f : i == 2 ? ((t >= 326f && t < 340f) || (t >= 388f && t < close + 6f)) : ((t >= 326f && t < 340f) || (t >= 400f && t < close + 6f));
                if (!win) { if (rf.Go.activeSelf) rf.Go.SetActive(false); continue; }
                float closing = Sm(close - 5.5f, close, t), ap = Sm(i == 0 ? RF_ON[0] : (t < 345f ? 326f : (i == 2 ? 388f : 400f)), (i == 0 ? RF_ON[0] : (t < 345f ? 326f : (i == 2 ? 388f : 400f))) + 3f, t); float after = t >= close ? 1f : 0f; float s = ap * (1f - closing * 0.92f) * (1f - after); bool far = t < 340f && i > 0; if (far) s *= 0.55f;
                if (s < 0.01f) { if (rf.Go.activeSelf) rf.Go.SetActive(false); continue; }
                if (!rf.Go.activeSelf) rf.Go.SetActive(true); var tr = rf.Go.transform; tr.localPosition = rf.P + Vector3.up * 9.6f * s; tr.localRotation = Quaternion.Euler(0f, rf.Yaw, 0f); tr.localScale = new Vector3(8f * s, 9.5f * s, 8f * s);
                for (int k = 0; k < rf.Rings.Length; k++) rf.Rings[k].localRotation = Quaternion.Euler(0f, 0f, t * (24f + 14f * k) * (k % 2 == 0 ? 1f : -1f) * (1f + 3f * closing));
                if (rf.Disc != null) rf.Disc.localRotation = Quaternion.Euler(0f, 0f, -t * (40f + 200f * closing));
                if (rf.Beam != null) { rf.Beam.localScale = new Vector3(0.8f, 380f / Mathf.Max(0.2f, s * 9.5f) * Mathf.Lerp(1f, 0.25f, closing), 0.8f); rf.Beam.localPosition = new Vector3(0f, -0.95f, 0f); }
                for (int k = 0; k < rf.Shards.Length; k++) { float a = t * (0.6f + 0.15f * (k % 3)) + k * 0.52f; float rr = 1.05f + 0.25f * Mathf.Sin(t + k); rf.Shards[k].localPosition = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 1.1f, 0.05f * Mathf.Sin(k)); rf.Shards[k].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f); }
                if (!paused && dt > 0f)
                {
                    Vector3 cw = W(rf.P + Vector3.up * 9f); if (UnityEngine.Random.value < 0.6f) { float a = UnityEngine.Random.value * 6.2832f; Emit(fx, cw + new Vector3(Mathf.Sin(a), Mathf.Cos(a) * 1.1f, 0f) * 8f * s, UnityEngine.Random.insideUnitSphere * 2f + Vector3.up, Color.Lerp(new Color(0.8f, 0.4f, 1f), new Color(0.5f, 0.9f, 1f), UnityEngine.Random.value), 0.9f, 1.6f); }
                    if (tp < close && t >= close) { Boom(cw, 12f, 0.8f, new Color(0.7f, 0.9f, 1f), false); Sfx(sBig, cw, 1f, 1.1f); for (int k = 0; k < 160; k++) { float a = k / 160f * 6.2832f; Emit(fx, cw, new Vector3(Mathf.Sin(a), UnityEngine.Random.Range(-0.2f, 0.4f), Mathf.Cos(a)) * UnityEngine.Random.Range(10f, 30f), Color.Lerp(Color.white, new Color(0.7f, 0.85f, 1f), UnityEngine.Random.value), 1.4f, 1.8f); } ShowCap("RIFT CLOSED  " + (i + 1) + " / 4", "", 5.5f, close); }
                }
            }
            // the beam from your hand (or the butterfly's light) into the Rift that is closing
            int ci = -1; for (int i = 0; i < PORTAL_CLOSE.Length; i++) if (t >= PORTAL_CLOSE[i] - 12f && t < PORTAL_CLOSE[i]) ci = i; bool beamOn = ci >= 0 && ci < rifts.Count && rifts[ci].Go.activeSelf && !(ci > 0 && t < 340f);
            if (devBeam != null) { if (devBeam.enabled != beamOn) devBeam.enabled = beamOn; if (beamOn) { Vector3 tgt = W(rifts[ci].P + Vector3.up * 6f); Vector3 src = Vector3.Distance(HeadNow(), tgt) < 70f ? HandNow() : (bflyGo != null && bflyGo.activeSelf ? bflyGo.transform.position : HandNow()); int n = devBeam.positionCount; float ph = Sm(PORTAL_CLOSE[ci] - 12f, PORTAL_CLOSE[ci] - 9f, t); for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); Vector3 pt = Vector3.Lerp(src, tgt, s * ph); pt += new Vector3(Mathf.Sin(s * 14f + t * 20f), Mathf.Cos(s * 12f + t * 17f), Mathf.Sin(s * 9f - t * 13f)) * 0.25f * Mathf.Sin(s * Mathf.PI); devBeam.SetPosition(k, pt); } devBeam.widthMultiplier = 0.25f + 0.2f * Mathf.Sin(t * 30f) + 0.25f * Sm(PORTAL_CLOSE[ci] - 6f, PORTAL_CLOSE[ci], t); if (!paused && Vector3.Distance(HeadNow(), tgt) < 70f) shake = Mathf.Max(shake, 0.12f); } }
        }
        // a Reality Wave: a ring of light sweeps out from you across the ground, the sky flashes and the world changes
        void WaveFrame(float t, float tp, Camera cam)
        {
            if (paused) return; foreach (float w in WAVES) if (tp < w && t >= w)
                {
                    Vector3 c = HeadNow(); c.y = originW.y + 0.3f; try { RaycastHit hh; if (Physics.Raycast(HeadNow(), Vector3.down, out hh, 20f, locoMask, QueryTriggerInteraction.Ignore)) c.y = hh.point.y + 0.3f; } catch { }
                    Color a = w == 280f ? new Color(0.7f, 0.5f, 1f) : w == 345f ? new Color(1f, 0.65f, 0.2f) : w == 390f ? new Color(0.3f, 0.6f, 1f) : new Color(0.5f, 1f, 0.5f);
                    for (int k = 0; k < 420; k++) { float ang = k / 420f * 6.2832f; Emit(fx, c, new Vector3(Mathf.Sin(ang), UnityEngine.Random.Range(0f, 0.12f), Mathf.Cos(ang)) * UnityEngine.Random.Range(30f, 46f), Color.Lerp(a, Color.white, UnityEngine.Random.value * 0.7f), 2.4f, 4f); }
                    Sfx(sBig, c, 1f, 0.9f); Sfx(sWhoosh, c, 0.9f, 0.7f); shake = Mathf.Max(shake, 0.5f); flashLamp.transform.position = c + Vector3.up * 3f; flashLamp.color = a; flashLamp.intensity = Lit * 10f;
                    ShowCap("REALITY WAVE", w == 280f ? "GLYPH MASTER RAZ" : w == 345f ? "CLUCK" : w == 390f ? "NIGHTFALL" : "LARA CROFT", 5.5f, w);
                }
        }
        // Jones as a Rift Butterfly: hovers beside you from the first wave to the top of the Spire
        void ButterflyFrame(float t, float dt, Camera cam)
        {
            if (bflyGo == null) return; bool on = t >= 281f && t < 600f; if (bflyGo.activeSelf != on) bflyGo.SetActive(on); if (!on) { bflyInit = false; return; }
            Vector3 head = HeadNow(); Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize(); Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 tgt = head + fwd * 1.5f + right * 0.9f + Vector3.up * (0.1f + 0.15f * Mathf.Sin(t * 1.7f)) + right * 0.25f * Mathf.Sin(t * 0.9f);
            if (!bflyInit || dt <= 0f || Vector3.Distance(bflyW, tgt) > 8f) { bflyW = tgt; bflyInit = true; } else bflyW = Vector3.Lerp(bflyW, tgt, 1f - Mathf.Exp(-3f * dt));
            Vector3 dir = bflyW - head; dir.y = 0f; if (dir.sqrMagnitude > 0.001f) bflyYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            float flap = 1f + 0.18f * Mathf.Sin(t * 22f); bflyGo.transform.position = bflyW; bflyGo.transform.rotation = Quaternion.Euler(8f * Mathf.Sin(t * 1.3f), bflyYaw, 10f * Mathf.Sin(t * 0.9f)); bflyGo.transform.localScale = new Vector3(bflyS * flap, bflyS, bflyS);
        }
        // 6:30: wolves run through the dark blue reality
        void WolfFrame(float t, float dt)
        {
            bool on = t >= 389f && t < 402f; for (int i = 0; i < wolfGos.Count; i++)
            {
                var go = wolfGos[i]; if (!on) { if (go.activeSelf) go.SetActive(false); continue; }
                float u = Sm(389f, 392f, t) * (1f - Sm(399f, 402f, t)); float a = Hash(i, 21) * 6.2832f + t * (0.38f + 0.1f * Hash(i, 22)) * (i % 2 == 0 ? 1f : -1f), r = 9f + 26f * Hash(i, 23); Vector3 ctr = new Vector3(-30f, IY, 124f); Vector3 p = ctr + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                p.y = IY + Mathf.Abs(Mathf.Sin(t * 9f + i)) * 0.35f; float tang = (a * Mathf.Rad2Deg + (i % 2 == 0 ? 90f : -90f)) + 180f;
                PlaceGo(go, p, Fit(wolfM, 3.4f, true) * u, Quaternion.Euler(6f * Mathf.Sin(t * 9f + i), tang, 0f), u > 0.02f);
            }
        }
        // ---------------------------------------------------------------- 8:20 the Spire grows, floating rocks and steps carry you up, the deck on top, the Foundation's sacrifice
        void SpireFrame(float t, float tp, float dt)
        {
            bool on = t >= 500f && t < 602.5f; Vector3 baseP = SpireC + Vector3.up * spireY;
            if (spireGo != null)
            {
                if (spireGo.activeSelf != on) spireGo.SetActive(on);
                if (on) { float gy = Sm(500f, 520f, t), gx = 0.5f + 0.5f * Sm(500f, 512f, t); float shk = t < 520f ? 0.4f * (1f - gy) : 0f; spireGo.transform.localPosition = baseP + new Vector3(Mathf.Sin(t * 41f), 0f, Mathf.Cos(t * 37f)) * shk; spireGo.transform.localRotation = Quaternion.Euler(0f, 20f, 0f); spireGo.transform.localScale = new Vector3(spireS * gx, spireS * Mathf.Max(0.02f, gy), spireS * gx); }
            }
            for (int i = 0; i < slabGos.Count; i++) { var go = slabGos[i]; bool vis = on && t >= 500.5f + 0.12f * i; if (!vis) { if (go.activeSelf) go.SetActive(false); continue; } float ti = 508f + i * 44f / (slabGos.Count - 1); Vector3 p = ClimbPos(ti) - Vector3.up * 0.6f; float pop = Sm(500.5f + 0.12f * i, 502f + 0.12f * i, t); Vector3 d = ClimbPos(ti + 0.5f) - ClimbPos(ti - 0.5f); float yw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; PlaceGo(go, p + Vector3.up * (1f - pop) * -12f + new Vector3(0f, 0.3f * Mathf.Sin(t * 0.9f + i), 0f), (1.15f + 0.25f * Hash(i, 31)) * pop, Quaternion.Euler(0f, yw, 0f), true); }
            for (int i = 0; i < rockGos.Count; i++) { var go = rockGos[i]; if (!on) { if (go.activeSelf) go.SetActive(false); continue; } float a = Hash(i, 41) * 6.2832f + t * 0.05f * (i % 2 == 0 ? 1f : -1f), r = 34f + 60f * Hash(i, 42); float y = (10f + 130f * Hash(i, 43) + 3f * Mathf.Sin(t * 0.3f + i)) * Sm(500f, 515f, t); float s = 2.2f + 6f * Hash(i, 44); PlaceGo(go, SpireC + Vector3.up * spireY + new Vector3(Mathf.Sin(a) * r, y + 4f, Mathf.Cos(a) * r), s, Quaternion.Euler(t * 3f * Hash(i, 45), t * 5f * Hash(i, 46), 10f * i), Sm(500f, 506f, t) > 0.01f); }
            if (chamGo != null)
            {
                bool co = t >= 524f && t < 603f; if (chamGo.activeSelf != co) chamGo.SetActive(co);
                if (co) { float cs = Sm(524f, 548f, t); chamGo.transform.localPosition = SpireTop + Vector3.up * (spireY - (1f - cs) * 30f); chamGo.transform.localRotation = Quaternion.identity; chamGo.transform.localScale = Vector3.one * Mathf.Max(0.05f, cs); }
            }
            // the energy sphere (9:40 - 9:50) and the final blast at 10:00
            bool bo = t >= 578f && t < 603f; if (ball != null)
            {
                if (ball.gameObject.activeSelf != bo) ball.gameObject.SetActive(bo);
                if (bo) { float r = 1f + 11f * Sm(580f, 592f, t) + 80f * Sm(598f, 601.5f, t); ball.localPosition = SpireTop + Vector3.up * (spireY + 4.5f); ball.localScale = Vector3.one * r * 2f * (1f + 0.03f * Mathf.Sin(t * 14f)); ballMat.color = new Color(0.62f + 0.3f * Sm(596f, 601f, t), 0.88f + 0.1f * Sm(596f, 601f, t), 1f, 0.42f + 0.4f * Sm(596f, 601f, t)); }
            }
            for (int i = 3; i < bolts.Count; i++) { var l = bolts[i]; bool lo = bo && t > 582f; if (l.enabled != lo) l.enabled = lo; if (!lo) continue; Vector3 c = W(SpireTop + Vector3.up * (spireY + 4.5f)); float r = 1f + 11f * Sm(580f, 592f, t); Vector3 d = UnityEngine.Random.onUnitSphere; int n = l.positionCount; for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); l.SetPosition(k, c + (Quaternion.Euler(0f, i * 120f + t * 40f, 0f) * new Vector3(Mathf.Sin(s * 5f + t * 10f), Mathf.Cos(s * 4f + t * 9f + i), Mathf.Sin(s * 6f - t * 11f))) * r * (0.2f + 0.9f * s)); } l.widthMultiplier = 0.4f + 0.3f * Mathf.Sin(t * 20f + i); }
            if (!paused && dt > 0f)
            {
                if (tp < 500f && t >= 500f) { Boom(W(SpireC), 22f, 1f, new Color(0.5f, 0.7f, 1f), false); Sfx(sBig, W(SpireC), 1f, 0.7f); ShowCap("THE SPIRE", "", 6f, 500f); }
                if (t >= 500f && t < 520f && UnityEngine.Random.value < 0.9f) { Vector3 b = W(baseP + new Vector3(UnityEngine.Random.Range(-30f, 30f), 1f, UnityEngine.Random.Range(-30f, 30f))); Emit(dust, b, Vector3.up * UnityEngine.Random.Range(4f, 14f), new Color(0.7f, 0.8f, 1f, 0.5f), UnityEngine.Random.Range(6f, 14f), 4f); }
                if (tp < 600f && t >= 600f) { Boom(W(SpireTop + Vector3.up * (spireY + 4f)), 30f, 1.2f, new Color(0.8f, 0.92f, 1f), false); Sfx(sBig, W(SpireTop), 1f, 0.6f); ShowCap("THE FOUNDATION IS SEALED IN THE ZERO POINT", "", 6f, 600f); }
            }
        }
        // dust, embers and smoke over the island, a few captions
        void AmbientFrame(float t, float tp, float dt, Seg g, Camera cam)
        {
            if (paused || dt <= 0f) return; Vector3 h = HeadNow();
            bool burn = (t >= 56f && t < 340f) || (t >= 450f && t < 500f); if (burn)
            {
                for (int k = 0; k < (int)(2 * Parts + 1); k++) Emit(dust, h + new Vector3(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(0.5f, 12f), UnityEngine.Random.Range(-30f, 30f)), new Vector3(2f, 1f, 1f), new Color(1f, 0.8f, 0.9f, 0.3f), UnityEngine.Random.Range(0.4f, 1.1f), 5f);
                if (UnityEngine.Random.value < 0.35f) { Vector3 p = W(new Vector3(UnityEngine.Random.Range(-70f, 70f), IY + 0.5f, UnityEngine.Random.Range(30f, 170f))); Emit(smoke, p, Vector3.up * UnityEngine.Random.Range(2f, 5f), new Color(0.55f, 0.4f, 0.6f, 0.3f), UnityEngine.Random.Range(6f, 12f), 4.5f); }
            }
            if (g.S == Sc.Wolves && UnityEngine.Random.value < 0.8f) Emit(dust, h + new Vector3(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(0.3f, 4f), UnityEngine.Random.Range(-25f, 25f)), Vector3.up * 0.3f, new Color(0.5f, 0.7f, 1f, 0.25f), 6f, 6f);
            if (g.S == Sc.Cluck && UnityEngine.Random.value < 0.6f) Emit(dust, h + new Vector3(UnityEngine.Random.Range(-25f, 25f), UnityEngine.Random.Range(4f, 14f), UnityEngine.Random.Range(-25f, 25f)), new Vector3(-1f, -1.4f, 0f), new Color(1f, 0.55f, 0.1f, 0.9f), 0.35f, 6f);
            if (g.S == Sc.Reel && UnityEngine.Random.value < 0.8f) Emit(fx, h + new Vector3(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(0f, 8f), UnityEngine.Random.Range(0f, 26f)), Vector3.up * 2f, new Color(1f, 0.7f, 0.2f, 0.9f), 0.5f, 4f);
        }
        float capT = -99f, capDur; string capA = "", capB = "";
        void ShowCap(string a, string b, float dur, float at) { capA = a; capB = b; capDur = dur; capT = at; }
        void CaptionFrame(float t, Camera cam)
        {
            if (capTm == null) return;
            // scheduled titles
            if (t >= 4f && t < 11f) { capA = "ZERO CRISIS FINALE"; capB = "FORTNITE  CHAPTER 2  SEASON 5"; capDur = 7f; capT = 4f; }
            else if (t >= 602.5f && t < 609f) { capA = "ZERO CRISIS FINALE"; capB = ""; capDur = 6.5f; capT = 602.5f; }
            else if (t >= 612f && t < 620f) { capA = "CHAPTER 2  SEASON 6"; capB = "PRIMAL"; capDur = 8f; capT = 612f; }
            else if (t >= 640f && t < 650f) { capA = "BATTLE PASS"; capB = ""; capDur = 10f; capT = 640f; }
            float e = t - capT; float a = e < 0f || e > capDur ? 0f : Sm(0f, 0.6f, e) * (1f - Sm(capDur - 1.2f, capDur, e));
            if (a < 0.01f) { if (capTm.gameObject.activeSelf) capTm.gameObject.SetActive(false); if (subTm.gameObject.activeSelf) subTm.gameObject.SetActive(false); return; }
            if (!capTm.gameObject.activeSelf) capTm.gameObject.SetActive(true); capTm.text = capA; capTm.color = new Color(1f, 1f, 1f, a); subTm.gameObject.SetActive(capB.Length > 0); subTm.text = capB; subTm.color = new Color(1f, 0.85f, 1f, a);
            if (cam != null) { Vector3 p = cam.transform.position + cam.transform.forward * 3.0f + cam.transform.up * 0.35f; capTm.transform.position = p; capTm.transform.rotation = Quaternion.LookRotation(p - cam.transform.position, cam.transform.up); subTm.transform.position = p - cam.transform.up * 0.34f; subTm.transform.rotation = capTm.transform.rotation; }
        }
    }
}
