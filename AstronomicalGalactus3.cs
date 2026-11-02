using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Galactus, part 3: the scenery and effects of each scene
    internal sealed partial class AstronomicalGalactus
    {
        readonly List<GameObject> fleet = new List<GameObject>(), halls = new List<GameObject>(), beams = new List<GameObject>(); readonly List<float> fleetX = new List<float>(), fleetY = new List<float>(), fleetZ = new List<float>();
        readonly List<LineRenderer> bolts = new List<LineRenderer>(), conv = new List<LineRenderer>(); Transform eyeL, eyeR, coreQ, mouthQ, orb, orbGlow, ball, planet, hitPt, flameQ; Material orbMat, ballMat, boltMat, convMat; TextMesh cdTm, capTm, subTm; Transform warnQ;
        float busS = 1f, heliS = 1f; float capT = -99f, capDur; string capA = "", capB = ""; Texture2D warnTex;
        readonly List<Transform> cracks = new List<Transform>(); Material crackMat;
        // 3:00 the carrier burns and sinks away under the falling player (the glowing cracks stay on the deck)
        static float HeliSink(float t) { float e = Mathf.Max(0f, t - 180f); return 9f * e + 0.9f * e * e; }
        // the arm run: the bus flies INTO his arm and through a lit tunnel to the shoulder (gate frames, conduits), a swarm of drones, the bus' lasers
        Transform tunnel, tunEnd; Material tunMat, condMat, trimMat; readonly List<Transform> gates = new List<Transform>(); readonly List<LineRenderer> conduits = new List<LineRenderer>();
        readonly List<Transform> pylons = new List<Transform>(), pylonGl = new List<Transform>(), drones = new List<Transform>(), droneGl = new List<Transform>(); readonly List<LineRenderer> lasers = new List<LineRenderer>(); Material laserMat; readonly float[] droneDz = new float[24];
        static readonly float[] BOOMS = { 385f, 392f, 399f, 406f, 412f, 450f, 457f, 460f, 466f, 473f, 485f, 489f, 496f, 500f, 540f };

        void BuildContent()
        {
            if (heliM != null) { heliGo = heliM.Pivot; heliGo.SetActive(false); var s = heliM.RendererBounds.size; JsLiveEventsMod.Log.LogInfo("Galactus: heli bounds " + s + ", bus bounds " + (sbusM != null ? sbusM.RendererBounds.size.ToString() : "-") + ", galactus bounds " + (galM != null ? galM.RendererBounds.size.ToString() : "-")); heliS = 170f / Mathf.Max(0.1f, Mathf.Max(s.x, s.z)); }
            if (sbusM != null) { busGo = sbusM.Pivot; busGo.SetActive(false); var s = sbusM.RendererBounds.size; busS = 22f / Mathf.Max(0.1f, Mathf.Max(s.x, s.z)); BusRoof = new Vector3(0f, 0.4f * s.y * busS + 2.4f, 3.5f); }
            BuildActors();
            // deck floor and a high invisible fence round it
            kit.BoxCol("GDeck", root.transform, new Vector3(0f, IY + DeckY - 1f, -10f), new Vector3(86f, 2f, 150f), Quaternion.identity);
            foreach (var w in new[] { new Vector3(0f, 0f, 66f), new Vector3(0f, 0f, -86f) }) kit.BoxCol("GRailZ", root.transform, new Vector3(w.x, IY + DeckY + 30f, w.z), new Vector3(90f, 62f, 2f), Quaternion.identity);
            foreach (var sx in new[] { -44f, 44f }) kit.BoxCol("GRailX", root.transform, new Vector3(sx, IY + DeckY + 30f, -10f), new Vector3(2f, 62f, 156f), Quaternion.identity);
            if (offM != null) { offM.Pivot.SetActive(false); }
            kit.BoxCol("GOfficeFloor", root.transform, new Vector3(0f, IY - 0.2f, OfficeZ), new Vector3(14f, 0.4f, 12f), Quaternion.identity);
            foreach (var wl in new[] { new Vector4(0f, 6.1f, 14.4f, 0.4f), new Vector4(0f, -6.1f, 14.4f, 0.4f), new Vector4(7.1f, 0f, 0.4f, 12f), new Vector4(-7.1f, 0f, 0.4f, 12f) }) kit.BoxCol("GOfficeWall", root.transform, new Vector3(wl.x, IY + 2f, OfficeZ + wl.y), new Vector3(wl.z, 4.4f, wl.w), Quaternion.identity);
            kit.BoxCol("GOfficeRoof", root.transform, new Vector3(0f, IY + 3.4f, OfficeZ), new Vector3(14.4f, 0.4f, 12.4f), Quaternion.identity);
            // the fleet round the bus
            for (int i = 0; sbusfM != null && i < 36; i++) { var go = i == 0 ? sbusfM.Pivot : UnityEngine.Object.Instantiate(sbusfM.Pivot, root.transform); go.name = "GFleet" + i; if (i > 0) clones.Add(go); go.SetActive(false); fleet.Add(go); fleetX.Add((Hash(i, 3) - 0.5f) * 520f); fleetY.Add((Hash(i, 4) - 0.5f) * 170f); fleetZ.Add(Hash(i, 5) * 700f - 200f); }
            if (corM != null) { var h0 = Sub(corM, "Hall"); var b0 = Sub(corM, "Beams"); for (int i = 0; i < 4 && h0 != null; i++) { var h = UnityEngine.Object.Instantiate(h0.gameObject, root.transform); h.name = "GHall" + i; h.SetActive(false); clones.Add(h); halls.Add(h); if (b0 != null) { var b = UnityEngine.Object.Instantiate(b0.gameObject, root.transform); b.name = "GBeams" + i; b.SetActive(false); clones.Add(b); beams.Add(b); } } }
            if (galM != null)
            {
                gxRoot = Sub(galM, "Gx"); gxBelt = gxRoot != null ? galM.Pivot.transform.InverseTransformPoint(gxRoot.position) : Vector3.zero; gxTorso = Sub(galM, "Torso"); gxHead = Sub(galM, "Head"); gxShL = Sub(galM, "ShL"); gxElL = Sub(galM, "ElL"); gxShR = Sub(galM, "ShR"); gxElR = Sub(galM, "ElR"); gxHandL = Sub(galM, "HandL"); gxHandR = Sub(galM, "HandR"); gxEyeL = Sub(galM, "EyeL"); gxEyeR = Sub(galM, "EyeR"); gxMouthP = Sub(galM, "MouthP"); gxChestP = Sub(galM, "ChestP");
                gxParts.Clear(); foreach (var tr in new[] { gxTorso, gxHead, gxShL, gxElL, gxShR, gxElR }) gxParts.Add(tr);
                gxPartBase = new Vector3[gxParts.Count]; gxDir = new Vector3[gxParts.Count]; gxSpin = new Vector3[gxParts.Count]; Vector3[] dirs = { new Vector3(0f, 0.1f, 1f), new Vector3(0f, 1f, 0.5f), new Vector3(1f, 0.2f, 0.4f), new Vector3(1f, -0.3f, 0.6f), new Vector3(-1f, 0.2f, 0.4f), new Vector3(-1f, -0.3f, 0.6f) };
                for (int i = 0; i < gxParts.Count; i++) { gxPartBase[i] = gxParts[i] != null ? gxParts[i].localPosition : Vector3.zero; gxDir[i] = dirs[i] * 6f; gxSpin[i] = new Vector3(Hash(i, 7) - 0.5f, Hash(i, 8) - 0.5f, Hash(i, 9) - 0.5f); }
                BuildSkin();
            }
            boltMat = kit.Glow(new Color(1f, 0.55f, 0.85f, 1f), lineTex, 3003); convMat = kit.Glow(new Color(1f, 0.4f, 0.7f, 0.9f), lineTex, 3002);
            for (int i = 0; i < 8; i++) bolts.Add(MakeLine("GBolt" + i, boltMat, 14, 3f)); for (int i = 0; i < 12; i++) conv.Add(MakeLine("GConv" + i, convMat, 16, 4f));
            eyeL = Quad("GEyeL", kit.Glow(new Color(1f, 0.3f, 0.2f, 1f), glowTex, 3003)); eyeR = Quad("GEyeR", kit.Glow(new Color(1f, 0.3f, 0.2f, 1f), glowTex, 3003)); coreQ = Quad("GCore", kit.Glow(new Color(1f, 0.35f, 0.25f, 1f), glowTex, 3003)); mouthQ = Quad("GMouth", kit.Glow(new Color(1f, 0.95f, 0.9f, 1f), glowTex, 3003));
            foreach (var q in new[] { eyeL, eyeR, coreQ, mouthQ }) q.gameObject.SetActive(false);
            orbMat = kit.Glow(new Color(0.4f, 1f, 0.55f, 0.85f), null, 3002); orb = Prim(PrimitiveType.Sphere, "GOrb", orbMat); orb.gameObject.SetActive(false); orbGlow = Quad("GOrbGlow", kit.Glow(new Color(0.4f, 1f, 0.5f, 1f), glowTex, 3003)); orbGlow.gameObject.SetActive(false);
            ballMat = kit.Glow(new Color(0.55f, 0.8f, 1f, 0.6f), null, 3001); ball = Prim(PrimitiveType.Sphere, "GBall", ballMat); ball.gameObject.SetActive(false);
            hitPt = Quad("GHit", kit.Glow(new Color(0.7f, 0.8f, 1f, 1f), glowTex, 3003)); hitPt.gameObject.SetActive(false); flameQ = Quad("GFlame", kit.Glow(new Color(1f, 0.55f, 0.2f, 0.9f), glowTex, 3002)); flameQ.gameObject.SetActive(false);
            planet = Prim(PrimitiveType.Sphere, "GPlanet", kit.Lit(new Color(0.55f, 0.65f, 0.95f), null, 0.05f)); planet.localScale = Vector3.one * 700f; planet.gameObject.SetActive(false);
            warnTex = kit.Tex(128, 128, (x, y) => { float u = x / 127f, v = y / 127f; bool tri = v < 0.9f && Mathf.Abs(u - 0.5f) < (0.9f - v) * 0.55f + 0.0f && v > 0.06f; bool mark = Mathf.Abs(u - 0.5f) < 0.045f && v > 0.35f && v < 0.68f || (Mathf.Abs(u - 0.5f) < 0.05f && v > 0.2f && v < 0.28f); return !tri ? new Color(0, 0, 0, 0) : (mark ? new Color(1f, 0.95f, 0.5f, 1f) : new Color(1f, 0.45f, 0.1f, 1f)); }, TextureWrapMode.Clamp);
            warnQ = Quad("GWarn", kit.Glow(Color.white, warnTex, 3003)); warnQ.gameObject.SetActive(false);
            var pylonTex = kit.Tex(32, 64, (x, y) => { bool band = y % 16 < 3 || (y % 16 >= 8 && y % 16 < 9); bool ribs = x % 8 == 0; return band ? new Color(1f, 0.28f, 0.2f, 1f) : (ribs ? new Color(0.22f, 0.2f, 0.27f, 1f) : new Color(0.1f, 0.09f, 0.13f, 1f)); }, TextureWrapMode.Repeat); var pylonMat = kit.Lit(new Color(1f, 1f, 1f), pylonTex, 0.4f); var droneMat = kit.Lit(new Color(0.13f, 0.12f, 0.16f), null, 0.5f); var redGl = kit.Glow(new Color(1f, 0.3f, 0.2f, 0.9f), glowTex, 3003); laserMat = kit.Glow(new Color(1f, 0.3f, 0.15f, 1f), lineTex, 3003); pylons.Clear(); pylonGl.Clear(); drones.Clear(); droneGl.Clear(); lasers.Clear();
            var tunTex = kit.Tex(128, 128, (x, y) => { float fx = x % 32, fy = y % 64; bool seam = fx < 2 || fy < 2; bool seam2 = fx > 14 && fx < 17 && fy > 8 && fy < 56; if (seam) return new Color(0.02f, 0.015f, 0.025f, 1f); if (seam2) return new Color(0.75f, 0.6f, 1f, 1f); bool rivet = (fx == 5 || fx == 26) && (fy % 16 == 8); if (rivet) return new Color(0.5f, 0.2f, 0.18f, 1f); float v = 0.17f + 0.07f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f); return new Color(v * 0.8f, v * 0.7f, v * 1.5f, 1f); }, TextureWrapMode.Repeat);
            tunMat = kit.Flat(Color.white); tunMat.mainTexture = tunTex; try { tunMat.SetFloat("_Cull", 0f); } catch { } tunnel = Prim(PrimitiveType.Cylinder, "GTunnel", tunMat); tunnel.gameObject.SetActive(false);
            tunEnd = Quad("GTunEnd", kit.Glow(new Color(1f, 0.55f, 0.45f, 1f), glowTex, 3003)); tunEnd.gameObject.SetActive(false); condMat = kit.Glow(new Color(0.7f, 0.8f, 1f, 0.95f), lineTex, 3002); var gateMat = kit.Flat(new Color(0.1f, 0.09f, 0.2f)); trimMat = kit.Glow(new Color(0.75f, 0.7f, 1f, 1f), null, 3001); gates.Clear(); conduits.Clear();
            for (int i = 0; i < 192; i++) { var gt = Prim(PrimitiveType.Cube, "GGate" + i, i < 96 ? gateMat : trimMat); gt.gameObject.SetActive(false); gates.Add(gt); }
            for (int i = 0; i < 8; i++) conduits.Add(MakeLine("GCond" + i, condMat, 2, 3.5f));
            for (int i = 0; i < 24; i++) { var d = Prim(PrimitiveType.Sphere, "GDrone" + i, droneMat); d.localScale = Vector3.one * 7f; d.gameObject.SetActive(false); drones.Add(d); var g = Quad("GDroneGl" + i, redGl); g.gameObject.SetActive(false); droneGl.Add(g); droneDz[i] = 1e9f; }
            for (int i = 0; i < 4; i++) lasers.Add(MakeLine("GLaser" + i, laserMat, 2, 1.6f));
            crackMat = kit.Glow(new Color(1f, 0.5f, 0.12f, 0f), glowTex, 3001); cracks.Clear();
            for (int i = 0; i < 48; i++) { var c = Quad("GCrack" + i, crackMat); c.gameObject.SetActive(false); cracks.Add(c); }
            cdTm = MakeText(root.transform, 0.9f, new Color(1f, 0.85f, 0.4f, 0f), 3003); capTm = MakeText(root.transform, 0.045f, new Color(1f, 1f, 1f, 0f), 3600); subTm = MakeText(root.transform, 0.02f, new Color(1f, 0.95f, 0.9f, 0f), 3600);
            AstronomicalDebug.Anchors["head"] = () => HeadNow(); AstronomicalDebug.Anchors["bus"] = () => W(BusPos + BusRoof + Vector3.up * 2f); AstronomicalDebug.Anchors["deck"] = () => W(new Vector3(0f, IY + DeckY + 2f, -20f));
            AstronomicalDebug.Anchors["gx"] = () => gxRoot != null ? gxRoot.TransformPoint(new Vector3(0f, 14f, 0f)) : Vector3.zero; AstronomicalDebug.Anchors["iron"] = () => actBy.ContainsKey("iron") ? actBy["iron"].Go.transform.position + Vector3.up : Vector3.zero;
            AstronomicalDebug.Anchors["gxbelt"] = () => gxRoot != null ? gxRoot.position : Vector3.zero; AstronomicalDebug.Anchors["gxhead"] = () => gxHead != null ? gxHead.position : Vector3.zero; AstronomicalDebug.Anchors["gxfeet"] = () => galM != null ? galM.Pivot.transform.position : Vector3.zero; JsLiveEventsMod.Log.LogInfo("Galactus: belt offset in pivot space " + gxBelt + ", head " + (gxHead != null ? galM.Pivot.transform.InverseTransformPoint(gxHead.position).ToString() : "-"));
            AstronomicalDebug.Anchors["office"] = () => W(new Vector3(0f, IY + 1f, OfficeZ)); AstronomicalDebug.Anchors["fall"] = () => W(FallPos(Mathf.Clamp(lastT, 180f, 270f))); BuildExtra();
            if (corM != null) foreach (var r in corM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) { if (m == null) continue; try { m.mainTexture = null; bool lit = m.HasProperty("_Glossiness") || m.shader.name.Contains("Standard"); m.color = lit ? new Color(0.22f, 0.18f, 0.5f, m.color.a) : new Color(0.85f, 0.7f, 1f, m.color.a); } catch { } }
        }
        void ContentCleanup()
        {
            acts.Clear(); actBy.Clear(); fleet.Clear(); fleetX.Clear(); fleetY.Clear(); fleetZ.Clear(); halls.Clear(); beams.Clear(); bolts.Clear(); conv.Clear(); galMats.Clear(); gxParts.Clear(); gxTorso = gxHead = gxShL = gxElL = gxShR = gxElR = gxHSh = gxHEl = gxOSh = gxOEl = gxHandL = gxHandR = gxEyeL = gxEyeR = gxMouthP = gxChestP = gxHHand = gxOHand = null;
            ExtraCleanup(); skinBy.Clear(); gxHipL = gxKneeL = gxHipR = gxKneeR = null; cracks.Clear(); pylons.Clear(); pylonGl.Clear(); gates.Clear(); conduits.Clear(); tunnel = tunEnd = null; drones.Clear(); droneGl.Clear(); lasers.Clear(); heliGo = busGo = null; heliM = sbusM = sbusfM = galM = corM = offM = ironM = thorM = wolvM = jonM = null; cdTm = capTm = subTm = null; orb = ball = planet = null;
        }

        // ---------------------------------------------------------------- the frame
        void Content(float t, float tp, float dt, Seg g, Camera cam)
        {
            foreach (var a in acts) ActFrame(a, t, dt); DeckFrame(t, tp, dt, cam); GxFrame(t, tp, dt, g, cam); GxFx(t, tp, dt, cam); RunFx(t, tp, dt, cam); ExtraFrame(t, tp, dt, cam); BusFrame(t, tp, dt); CorridorFrame(t, tp, dt); BlastFrame(t, tp, dt); OfficeFrame(t); CaptionFrame(t, cam); AmbientFrame(t, dt, cam); CloudsFrame(t);
            RenderSettings.ambientLight = Color.Lerp(ambientSave, MoodAmbient(g, t), 0.8f); sun.intensity = 0.6f * Lit; sun.color = Color.Lerp(Color.white, MoodAmbient(g, t), 0.4f);
            // the red lamp follows the bus inside Galactus
            redLamp.color = new Color(0.6f, 0.4f, 1f); redLamp.intensity = (g.S == Sc.Inside ? Lit * (2.4f + 1.2f * Mathf.Sin(t * 13f) * Mathf.Sin(t * 5f)) : 0f); redLamp.transform.position = W(BusPos + new Vector3(0f, 9f, 18f));
        }
        void CloudsFrame(float t) { if (clouds == null) return; var r = clouds.GetComponent<ParticleSystemRenderer>(); bool on = t < 421f || t > 579f && t < 601f; if (r.enabled != on) r.enabled = on; }
        Color MoodAmbient(Seg g, float t)
        {
            switch (g.S) { case Sc.Inside: return new Color(1f, 0.35f, 0.3f); case Sc.Space: case Sc.Mouth: return new Color(0.5f, 0.6f, 1f); case Sc.Blast: return new Color(0.7f, 0.9f, 1f); case Sc.Office: return new Color(1f, 0.4f, 0.3f); case Sc.Reach: return new Color(1f, 0.8f, 0.8f); default: return new Color(1f, 0.95f, 0.95f); }
        }
        // ---------------------------------------------------------------- 0:00 the Helicarrier deck: countdown, the warning, the carrier burning after the hit
        void DeckFrame(float t, float tp, float dt, Camera cam)
        {
            if (heliGo != null) { bool on = t < 214f; if (heliGo.activeSelf != on) heliGo.SetActive(on); if (on) { var s = heliM.RendererBounds.size; float dy = (AstronomicalDebug.Tweak[2] != 0f ? AstronomicalDebug.Tweak[2] : 0.78f) * s.y * heliS; float sk = t >= HitT && t < HitT + 4f ? 1.2f * (1f - (t - HitT) / 4f) : 0f; heliGo.transform.localPosition = new Vector3(sk * Mathf.Sin(t * 47f), IY + DeckY - dy - HeliSink(t) + sk * 0.6f * Mathf.Sin(t * 61f), -10f + sk * Mathf.Sin(t * 53f)); heliGo.transform.localRotation = Quaternion.identity; heliGo.transform.localScale = Vector3.one * heliS; } }
            Vector3 hp = HeadNow(); Vector3 fw = cam != null ? cam.transform.forward : Vector3.forward;
            // countdown: 0:00 - 0:10
            if (cdTm != null)
            {
                bool on = t < 11f; if (cdTm.gameObject.activeSelf != on) cdTm.gameObject.SetActive(on);
                if (on) { int sec = Mathf.Max(0, 10 - (int)t); cdTm.text = "00 DAY   00 HRS   00 MIN   " + sec.ToString("00") + " SEC"; cdTm.color = new Color(1f, 0.85f, 0.4f, 1f - Sm(9.6f, 10.6f, t)); Vector3 p = W(new Vector3(0f, IY + DeckY + 36f, 80f)); cdTm.transform.position = p; cdTm.transform.rotation = Quaternion.LookRotation(p - hp, Vector3.up); cdTm.transform.localScale = Vector3.one * 1.8f; if (!paused && (int)tp != (int)t && t < 10f) Sfx(sZap, W(new Vector3(0f, IY + 36f, 70f)), 0.4f, 1.2f); }
                bool wo = t >= 10f && t < 21f; if (warnQ.gameObject.activeSelf != wo) warnQ.gameObject.SetActive(wo);
                if (wo) { Vector3 p = W(new Vector3(0f, IY + DeckY + 40f, 90f)); warnQ.position = p; warnQ.rotation = Quaternion.LookRotation(p - hp, Vector3.up); float bl = 0.8f + 0.2f * Mathf.Sin(t * 8f); warnQ.localScale = Vector3.one * 34f * bl * (1f - Sm(18f, 21f, t)); if (!paused && tp < 10f && t >= 10f) Sfx(sAlarm, p, 0.7f, 1f); if (!paused && (int)(tp * 2f) != (int)(t * 2f) && t < 16f) Sfx(sAlarm, p, 0.35f, 1f, 0.2f); }
            }
            // burning after 3:00
            float snk = HeliSink(t);
            for (int i = 0; i < cracks.Count; i++)
            {
                bool cr = t >= HitT && t < 214f; var c = cracks[i]; if (c.gameObject.activeSelf != cr) c.gameObject.SetActive(cr); if (!cr) continue;
                float len = 5f + 11f * Hash(i, 31), wid = 0.7f + 1.1f * Hash(i, 32); c.position = W(new Vector3((Hash(i, 33) - 0.5f) * 26f, IY + DeckY + 0.25f - snk, -78f + 130f * Hash(i, 34)));
                c.rotation = Quaternion.Euler(90f, 360f * Hash(i, 35), 0f); c.localScale = new Vector3(len, wid, 1f);
            }
            if (crackMat != null) crackMat.color = new Color(1f, 0.5f, 0.12f, Mathf.Clamp01((t - HitT) / 2f) * (1f - Sm(200f, 213f, t)) * (0.75f + 0.25f * Mathf.Sin(t * 13f)));
            if (!paused && dt > 0f && t >= HitT && t < 215f) { for (int k = 0; k < (int)(6 * Parts + 1); k++) { Vector3 p = W(new Vector3(UnityEngine.Random.Range(-30f, 30f), IY + DeckY + UnityEngine.Random.Range(0f, 6f) - snk, UnityEngine.Random.Range(-70f, 50f))); Emit(fx, p, Vector3.up * UnityEngine.Random.Range(10f, 28f), new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(2f, 6f), UnityEngine.Random.Range(1f, 2f)); if (UnityEngine.Random.value < 0.4f) Emit(smoke, p, Vector3.up * 16f, new Color(0.2f, 0.17f, 0.2f, 0.7f), UnityEngine.Random.Range(8f, 18f), 3.5f); } }
            // the fist lands: first blast at the fist, then the deck goes up in a run from that end towards you
            if (!paused && tp < HitT && t >= HitT) { Vector3 fp = FistPoint(); var lp = root.transform.InverseTransformPoint(fp); JsLiveEventsMod.Log.LogInfo("Galactus: fist lands at local " + lp + " (deck top y " + (IY + DeckY) + ")"); Boom(fp, 36f, 1.6f, new Color(1f, 0.55f, 0.2f)); Sfx(sThunder, fp, 1f, 0.6f); }
            for (int i = 0; i < 5; i++) { float bt = HitT + 0.35f * (i + 1); if (!paused && tp < bt && t >= bt) Boom(W(new Vector3((Hash(i, 41) - 0.5f) * 30f, IY + DeckY + 3f, 30f - 28f * i)), 16f + 4f * (i % 2), 1.0f, new Color(1f, 0.5f, 0.15f)); }
        }
        // ---------------------------------------------------------------- Galactus' light: eyes, chest, lightning, energy under his hand, the chest / mouth overload, the green blast
        void Glow(Transform q, Vector3 w, float size, Color c, Camera cam, bool on)
        {
            if (q.gameObject.activeSelf != on) q.gameObject.SetActive(on); if (!on) return; q.position = w; if (cam != null) q.rotation = Quaternion.LookRotation(w - cam.transform.position); q.localScale = Vector3.one * size; q.GetComponent<Renderer>().sharedMaterial.color = c;
        }
        void GxFx(float t, float tp, float dt, Camera cam)
        {
            Vector3 gp; bool on = GxPlace(t, SegAt(t), out gp) && t < 576f; if (galM == null) return; var gt = gxRoot != null ? gxRoot : galM.Pivot.transform;
            Vector3 eL = gt.TransformPoint(new Vector3(-1.2f, 23f, -3.6f)), eR = gt.TransformPoint(new Vector3(1.2f, 23f, -3.6f)), chest = gt.TransformPoint(new Vector3(0f, 12f, 3.2f)), mouth = gt.TransformPoint(new Vector3(0f, 20.2f, 3.6f));
            // the model faces +z in its own frame, so his front is local +z
            eL = gt.TransformPoint(new Vector3(-1.1f, 22.2f, 3.2f)); eR = gt.TransformPoint(new Vector3(1.1f, 22.2f, 3.2f));
            if (gxEyeL != null && gxEyeR != null && gxMouthP != null && gxChestP != null) { eL = gxEyeL.position; eR = gxEyeR.position; mouth = gxMouthP.position; chest = gxChestP.position; }   // the new model carries its own marker points
            float eyes = on ? Sm(95f, 125f, t) : 0f; float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f); float es = gxS * 1.6f * (1f + (t > 540f ? 1f : 0f));
            Glow(eyeL, eL, es, new Color(1f, 0.25f, 0.2f, 0.9f * eyes * pulse), cam, on && eyes > 0.02f); Glow(eyeR, eR, es, new Color(1f, 0.25f, 0.2f, 0.9f * eyes * pulse), cam, on && eyes > 0.02f);
            float core = on ? Mathf.Max(Sm(340f, 395f, t) * (1f - Sm(412f, 420f, t)), Sm(550f, 575f, t)) : 0f; Glow(coreQ, chest, gxS * (6f + 8f * core), new Color(1f, 0.4f + 0.4f * Sm(550f, 575f, t), 0.3f + 0.5f * Sm(550f, 575f, t), 0.9f * core), cam, on && core > 0.02f);
            float mo = on ? Sm(535f, 556f, t) : 0f; Glow(mouthQ, mouth, gxS * (4f + 10f * Sm(540f, 575f, t)), new Color(0.6f + 0.2f * Sm(556f, 575f, t), 1f, 0.75f, 0.95f * mo), cam, on && mo > 0.02f);
            if (!paused && dt > 0f) { shake = Mathf.Max(shake, on && t > 60f && t < 180f ? 0.05f + 0.1f * Sm(60f, 120f, t) : 0f); }
            // lightning round him 2:35 - 3:00 and again before the blast
            bool lit = on && ((t >= 155f && t < 181f) || (t >= 556f && t < 575f)); Color lc = t > 500f ? new Color(0.5f, 1f, 0.65f, 1f) : new Color(1f, 0.55f, 0.85f, 1f); boltMat.color = lc;
            for (int i = 0; i < bolts.Count; i++)
            {
                var l = bolts[i]; if (l.enabled != lit) l.enabled = lit; if (!lit) continue; if (Time.frameCount % 3 != i % 3) continue; Vector3 a = gt.TransformPoint(new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(8f, 24f), UnityEngine.Random.Range(-3f, 5f))); Vector3 d = (UnityEngine.Random.onUnitSphere + Vector3.up * 0.3f).normalized; int n = l.positionCount;
                for (int k = 0; k < n; k++) l.SetPosition(k, a + d * (k * 5.5f * GS * 0.35f) + UnityEngine.Random.insideUnitSphere * 5f); l.widthMultiplier = UnityEngine.Random.Range(1.5f, 4f);
            }
            if (!paused && tp < 155f && t >= 155f) Sfx(sThunder, eL, 1f, 0.8f);
            // the hand's energy 3:35 - 4:30: streams of red / purple from the palm and fingers converge on a bright point under it, which blows up into the white flash
            bool cv = on && t >= 212f && t < 272f; float ce = Sm(212f, 265f, t); Vector3 hand = gxOHand != null ? gxOHand.position : gp; Vector3 pt = W(FallPos(t) + new Vector3(10f, -45f, 70f * (1f - 0.4f * ce)));
            Vector3 cdir = (pt - hand).sqrMagnitude < 1f ? Vector3.down : (pt - hand).normalized; Vector3 cside = Vector3.Cross(cdir, Vector3.up); cside = cside.sqrMagnitude < 1e-3f ? Vector3.right : cside.normalized;
            for (int i = 0; i < conv.Count; i++) { var l = conv[i]; if (l.enabled != cv) l.enabled = cv; if (!cv) continue; int n = l.positionCount; float fk = (i - (conv.Count - 1) * 0.5f) / ((conv.Count - 1) * 0.5f); Vector3 a = hand + cside * (fk * 2.0f * gxS) + cdir * (0.5f * gxS); for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); Vector3 q = Vector3.Lerp(a, pt, s * Mathf.Min(1f, 0.25f + 1.2f * ce)); q += (cside * Mathf.Sin(s * 11f + t * 6f + i) + Vector3.Cross(cdir, cside) * Mathf.Cos(s * 8f + t * 5f - i)) * 5f * Mathf.Sin(s * Mathf.PI); l.SetPosition(k, q); } l.widthMultiplier = 2.2f + 2.5f * ce; convMat.color = Color.Lerp(new Color(0.55f, 0.8f, 1f, 0.95f), new Color(0.9f, 0.97f, 1f, 1f), ce); }
            Glow(hitPt, pt, 10f + 110f * ce * ce * ce + 400f * Sm(262f, 270f, t), Color.Lerp(new Color(0.6f, 0.8f, 1f, 1f), new Color(0.9f, 0.97f, 1f, 1f), ce), cam, cv);
        }
        // ---------------------------------------------------------------- 5:45 - 7:00 up his arm: pylons on the arm to weave round, a swarm of drones the bus shoots down (all placed from the arm's points that GxRun stores)
        void RunFx(float t, float tp, float dt, Camera cam)
        {
            bool on = runOn && t >= 336f && t < 419f;
            // the thick pale mist of the recording: big soft white puffs streaming past the bus (not inside the arm: there the liner is the backdrop)
            if (!paused && dt > 0f && t >= 328f && t < 420f && !(t >= 362f && t < 412f)) { for (int k = 0; k < (int)(3 * Parts + 1); k++) Emit(dust, W(BusPos + new Vector3(UnityEngine.Random.Range(-110f, 110f), UnityEngine.Random.Range(-70f, 80f), UnityEngine.Random.Range(-30f, 320f))), root.transform.TransformDirection(new Vector3(0f, 0f, -48f)), new Color(0.88f, 0.94f, 1f, UnityEngine.Random.Range(0.22f, 0.4f)), UnityEngine.Random.Range(45f, 110f), 3.5f); }
            // ONE long straight tunnel along the arm's axis (root z): the bus stays in the middle, the liner / ribs / light strips stream past (a treadmill, so it is as long as the music needs)
            bool ins = runOn && runInside && tunnel != null;
            if (tunnel != null && tunnel.gameObject.activeSelf != ins) tunnel.gameObject.SetActive(ins);
            if (ins)
            {
                const float R = 50f, LEN = 1500f, SPEED = 33f; float te = t - 362f; Vector3 c0 = BusPos + new Vector3(0f, 7f, 0f);
                tunnel.localPosition = c0 + new Vector3(0f, 0f, LEN * 0.5f - 200f); tunnel.localRotation = Quaternion.Euler(90f, 0f, 0f); tunnel.localScale = new Vector3(2f * R, LEN * 0.5f, 2f * R);
                tunMat.mainTextureScale = new Vector2(10f, LEN / 64f); tunMat.mainTextureOffset = new Vector2(0f, te * SPEED / 64f);
                float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
                for (int r = 0; r < 12; r++)
                {
                    float z = Mathf.Repeat(r * 62f - te * SPEED, 744f) - 120f; for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.785398f + 0.3927f; var rq = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f); Vector3 dirR = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                        var seg = gates[r * 8 + k]; if (!seg.gameObject.activeSelf) seg.gameObject.SetActive(true); seg.localPosition = c0 + new Vector3(0f, 0f, z) + dirR * (R - 2f); seg.localRotation = rq; seg.localScale = new Vector3(0.84f * R, 4.5f, 8f);
                        var tr = gates[96 + r * 8 + k]; if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true); tr.localPosition = c0 + new Vector3(0f, 0f, z) + dirR * (R - 4.6f); tr.localRotation = rq; tr.localScale = new Vector3(0.78f * R, 0.9f, 8.6f);
                    }
                }
                for (int k = 0; k < conduits.Count; k++) { var l = conduits[k]; if (!l.enabled) l.enabled = true; float a = k * 0.785398f; Vector3 off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (R - 5.5f); l.SetPosition(0, W(c0 + off + new Vector3(0f, 0f, -150f))); l.SetPosition(1, W(c0 + off + new Vector3(0f, 0f, 900f))); l.widthMultiplier = (k % 2 == 0 ? 3.2f : 1.6f) * pulse; }
                condMat.color = Color.Lerp(new Color(0.7f, 0.85f, 1f, 0.95f), new Color(1f, 0.45f, 0.9f, 0.95f), 0.5f + 0.5f * Mathf.Sin(t * 0.8f)); trimMat.color = Color.Lerp(new Color(0.7f, 0.75f, 1f, 1f), new Color(1f, 0.55f, 0.95f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 1.3f));
                if (!paused) for (int bi = 0; bi < 8; bi++) { float bt = 368f + 6f * bi; if (tp < bt && t >= bt) Boom(W(c0 + new Vector3((Hash(bi, 91) - 0.5f) * 70f, (Hash(bi, 92) - 0.5f) * 50f, 110f + 60f * Hash(bi, 93))), 20f, 0.18f, new Color(1f, 0.55f, 0.2f)); }
                Glow(tunEnd, W(c0 + new Vector3(0f, 0f, 720f)), R * 7f, new Color(0.8f, 0.8f + 0.1f * Mathf.Sin(t * 2f), 1f, 0.85f), cam, true);
            }
            else { if (gates.Count > 0 && gates[0].gameObject.activeSelf) foreach (var gt in gates) gt.gameObject.SetActive(false); foreach (var l in conduits) if (l.enabled) l.enabled = false; if (tunEnd != null && tunEnd.gameObject.activeSelf) tunEnd.gameObject.SetActive(false); }
            if (!paused && runOn && tp < 362f && t >= 362f) { Boom(W(BusPos), 24f, 0.25f, new Color(1f, 0.6f, 0.5f)); }
            for (int i = 0; i < drones.Count; i++)
            {
                var d = drones[i]; var gl = droneGl[i]; float dz = 40f + Mathf.Repeat(560f * Hash(i, 71) - (t - 340f) * 22f, 540f), dd = 90f + 130f * Hash(i, 72); bool act = on && t >= 342f && dz >= dd;
                bool inT = t >= 362f && t < 412f; Vector3 p = BusPos + new Vector3((Hash(i, 73) - 0.5f) * (inT ? 70f : 120f) + 6f * Mathf.Sin(t * 1.3f + i), (inT ? 7f + (Hash(i, 74) - 0.5f) * 70f : 8f + 80f * Hash(i, 74)) + 5f * Mathf.Sin(t * 1.7f + i * 2f), dz);
                if (on && !paused && droneDz[i] >= dd && dz < dd && t >= 342f && droneDz[i] < 1e8f && Mathf.Abs(droneDz[i] - dz) < 60f) { Vector3 wp = W(p); for (int k = 0; k < 14; k++) Emit(fx, wp, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(8f, 30f), Color.Lerp(new Color(1f, 0.45f, 0.15f, 0.95f), new Color(1f, 0.9f, 0.6f, 0.95f), UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f), UnityEngine.Random.Range(0.4f, 1.1f)); Sfx(sBoom, wp, 0.3f, UnityEngine.Random.Range(0.9f, 1.3f), 0.04f); }
                droneDz[i] = dz;
                if (d.gameObject.activeSelf != act) d.gameObject.SetActive(act); if (!act) { if (gl.gameObject.activeSelf) gl.gameObject.SetActive(false); continue; }
                d.localPosition = p; d.localRotation = Quaternion.identity; d.localScale = Vector3.one * 1.6f; Glow(gl, W(p + new Vector3(0f, 0f, 6.5f)), 9f, new Color(1f, 0.4f, 0.25f, 0.95f), cam, true);
            }
            for (int i = 0; i < lasers.Count; i++)
            {
                int j = i * 6; bool fire = false; Vector3 tgt = Vector3.zero;
                if (on && j < drones.Count && drones[j].gameObject.activeSelf) { float dz = droneDz[j], dd = 90f + 130f * Hash(j, 72); if (dz < dd + 90f) { fire = true; tgt = drones[j].position; } }
                var l = lasers[i]; if (l.enabled != fire) { l.enabled = fire; if (fire && !paused) Sfx(sZap, tgt, 0.4f, 1.4f, 0.1f); } if (!fire) continue;
                Vector3 m = W(BusPos + new Vector3((i & 1) == 0 ? -5f : 5f, 3f, 14f)); l.SetPosition(0, m); l.SetPosition(1, tgt); l.widthMultiplier = 1.2f + 0.8f * Mathf.Sin(t * 40f + i);
            }
        }
        // ---------------------------------------------------------------- 4:30 - 9:45 the Stark Battle Bus, the fleet and the green payload
        void BusFrame(float t, float tp, float dt)
        {
            bool bus = t >= 270f && t < 580f; if (busGo != null) { if (busGo.activeSelf != bus) busGo.SetActive(bus); if (bus) { busGo.transform.localPosition = BusPos + Vector3.up * (0.3f * Mathf.Sin(t * 1.1f)); busGo.transform.localRotation = Quaternion.Euler(1.2f * Mathf.Sin(t * 0.7f), 0f, 2f * Mathf.Sin(t * 0.9f)); busGo.transform.localScale = Vector3.one * busS; } }
            bool fl = ((t >= 270f && t < 422f) && !(t >= 362f && t < 412f)) || (t >= 508f && t < 578f);
            for (int i = 0; i < fleet.Count; i++) { var go = fleet[i]; if (!fl) { if (go.activeSelf) go.SetActive(false); continue; } float zz = Mathf.Repeat(fleetZ[i] - (t - 270f) * 28f, 700f) - 200f; float yv = fleetY[i] + 4f * Mathf.Sin(t * 0.6f + i); Vector3 p = BusPos + new Vector3(fleetX[i], yv, zz); if (Mathf.Abs(fleetX[i]) < 30f && Mathf.Abs(yv) < 12f && zz > -40f && zz < 40f) p.x += 60f * Mathf.Sign(fleetX[i] + 0.01f); PlaceGo(go, p, busS, Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(t * 0.5f + i)), true); }
            // the payload: a green orb on the roof, swelling before the end
            float pay = bus ? 1f : 0f; float swell = 1f + 2.5f * Sm(540f, 575f, t) + 14f * Sm(575f, 579f, t); Vector3 op = BusPos + new Vector3(0f, 1.6f, -7.5f);   // the payload sits on the rear of the roof, not on you
            if (orb != null) { if (orb.gameObject.activeSelf != bus) orb.gameObject.SetActive(bus); if (bus) { orb.localPosition = op; orb.localScale = Vector3.one * 6f * swell * (1f + 0.06f * Mathf.Sin(t * 9f)); orbMat.color = new Color(0.4f + 0.5f * Sm(570f, 578f, t), 1f, 0.55f + 0.4f * Sm(570f, 578f, t), 0.8f); } }
            if (orbGlow != null && bus) { var cam = Camera.main; orbGlow.gameObject.SetActive(true); orbGlow.position = W(op); if (cam != null) orbGlow.rotation = Quaternion.LookRotation(W(op) - cam.transform.position); orbGlow.localScale = Vector3.one * 13f * swell; orbGlow.GetComponent<Renderer>().sharedMaterial.color = new Color(0.4f, 1f, 0.5f, 0.55f + 0.2f * Mathf.Sin(t * 6f)); } else if (orbGlow != null && orbGlow.gameObject.activeSelf) orbGlow.gameObject.SetActive(false);
            if (!paused && dt > 0f && bus) { Vector3 tail = W(BusPos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 1f, -10f)); Emit(dust, tail, new Vector3(0f, 0f, -40f), new Color(1f, 0.9f, 0.9f, 0.4f), 4f, 1.5f); }
            // 5:00 - 7:00 explosions round the bus on the way to his chest
            if (!paused) foreach (float bt in BOOMS) if (tp < bt && t >= bt) { bool inside = bt >= 440f && bt < 510f; Vector3 p = W(BusPos + new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(2f, 14f), UnityEngine.Random.Range(35f, 70f))); Boom(p, inside ? 16f : 12f, inside ? 1.1f : 0.8f, inside ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.5f, 0.3f)); if (bt == 460f || bt == 485f) Sfx(sBig, p, 1f, 0.8f); }
        }
        // ---------------------------------------------------------------- 7:00 - 8:30 the red-lit corridor inside Galactus, scrolling past the bus
        void CorridorFrame(float t, float tp, float dt)
        {
            bool on = t >= 421f && t < 511f; float sc = (t - 420f) * 42f; for (int i = 0; i < halls.Count; i++)
            {
                if (!on) { if (halls[i].activeSelf) halls[i].SetActive(false); if (i < beams.Count && beams[i].activeSelf) beams[i].SetActive(false); continue; }
                float z = -90f + i * 60f - Mathf.Repeat(sc, 60f); Vector3 p = BusPos + new Vector3(0f, -1.6f, z); PlaceGo(halls[i], p, 1f, Quaternion.identity, true); if (i < beams.Count) PlaceGo(beams[i], p, 1f, Quaternion.Euler(0f, 0f, 0f), true);
            }
            if (!paused && dt > 0f && on) { for (int k = 0; k < 4; k++) Emit(fx, W(BusPos + new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(0.5f, 14f), 70f)), new Vector3(0f, 0f, -90f), Color.Lerp(new Color(0.6f, 0.5f, 1f), new Color(1f, 0.7f, 0.95f), UnityEngine.Random.value), 0.6f, 1.2f); if (UnityEngine.Random.value < 0.12f) Emit(fx, W(BusPos + new Vector3(UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(1f, 12f), 50f)), new Vector3(0f, 8f, -60f), new Color(0.4f, 1f, 0.5f), 1.2f, 1.2f); }
            // the planet in the space scene
            bool sp = t >= 510f && t < 580f; if (planet != null) { if (planet.gameObject.activeSelf != sp) planet.gameObject.SetActive(sp); if (sp) planet.position = W(BusPos + new Vector3(-420f, 150f, 900f)); }
        }
        // ---------------------------------------------------------------- 9:35 the green blast, Galactus comes apart, 9:45 the cosmic explosion, 9:50 a blue point in the dark
        void BlastFrame(float t, float tp, float dt)
        {
            bool on = t >= 578f && t < 601f; if (ball != null)
            {
                if (ball.gameObject.activeSelf != on) ball.gameObject.SetActive(on);
                if (on) { Vector3 c = W(BusPos + BusRoof + new Vector3(0f, 6f, 20f)); ball.position = c; float r = 12f + 160f * Sm(578f, 582f, t) + 2400f * Mathf.Pow(Sm(585f, 592f, t), 2f); r = Mathf.Lerp(r, 3f, Sm(594f, 599f, t)); ball.localScale = Vector3.one * r * 2f; Color col = Color.Lerp(new Color(0.45f, 1f, 0.6f, 0.7f), new Color(0.55f, 0.8f, 1f, 0.8f), Sm(583f, 588f, t)); if (t > 594f) col = new Color(0.4f, 0.65f, 1f, 1f); ballMat.color = col; }
            }
            if (!paused && dt > 0f)
            {
                if (tp < 578f && t >= 578f) { Vector3 c = W(BusPos + BusRoof + new Vector3(0f, 6f, 20f)); Boom(c, 30f, 1.4f, new Color(0.5f, 1f, 0.6f)); Sfx(sBig, c, 1f, 0.7f); ShowCap("", "", 0.1f, 578f); }
                if (tp < 585f && t >= 585f) { Vector3 c = W(BusPos + new Vector3(0f, 6f, 60f)); Boom(c, 50f, 1.6f, new Color(0.7f, 0.9f, 1f)); Sfx(sBig, c, 1f, 0.6f); Sfx(sThunder, c, 1f, 0.6f); }
                if (t >= 585f && t < 593f) { float e = t - 585f; for (int k = 0; k < 40; k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, W(BusPos + new Vector3(0f, 6f, 60f)) + d * (e * 330f), d * 200f, Color.Lerp(new Color(0.55f, 0.75f, 1f, 0.95f), new Color(0.85f, 0.55f, 1f, 0.95f), UnityEngine.Random.value), UnityEngine.Random.Range(14f, 40f), 1.2f); } }
                if (t >= 578f && t < 596f) for (int k = 0; k < 6; k++) Emit(fx, W(BusPos + new Vector3(0f, 6f, 60f)) + UnityEngine.Random.insideUnitSphere * 30f, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(60f, 220f), Color.Lerp(new Color(0.5f, 0.9f, 1f), Color.white, UnityEngine.Random.value), UnityEngine.Random.Range(4f, 14f), 2f);
            }
        }
        // ---------------------------------------------------------------- 10:05 the office
        void OfficeFrame(float t)
        {
            if (offM == null) return; bool on = t >= 604f; var go = offM.Pivot; if (go.activeSelf != on) go.SetActive(on); if (on) { go.transform.localPosition = new Vector3(0f, IY, OfficeZ); go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one; }
        }
        void AmbientFrame(float t, float dt, Camera cam)
        {
            if (paused || dt <= 0f) return; Vector3 h = HeadNow();
            if (t < 270f || (t >= 270f && t < 421f) || (t >= 510f && t < 600f)) { for (int k = 0; k < 2; k++) Emit(dust, h + new Vector3(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-60f, 60f)), (t >= 180f && t < 272f ? Vector3.up * 70f : t >= 270f ? new Vector3(0f, 0f, -55f) : Vector3.zero), new Color(1f, 1f, 1f, 0.35f), UnityEngine.Random.Range(0.6f, 2.5f), 3f); }
        }
        void ShowCap(string a, string b, float dur, float at) { capA = a; capB = b; capDur = dur; capT = at; }
        void CaptionFrame(float t, Camera cam)
        {
            if (capTm == null) return;
            if (t >= 2f && t < 8f) { capA = "THE GALACTUS EVENT"; capB = "CHAPTER 2  SEASON 4"; capDur = 6f; capT = 2f; }
            else if (t >= 612f && t < 621f) { capA = "TO BE CONTINUED"; capB = ""; capDur = 9f; capT = 612f; }
            else if (t >= 622f && t < 650f) { capA = "IN SEASON FIVE!"; capB = t > 633f ? "FORTNITE  CHAPTER 2  SEASON 5" : ""; capDur = 28f; capT = 622f; }
            float e = t - capT; float al = e < 0f || e > capDur ? 0f : Sm(0f, 0.8f, e) * (1f - Sm(capDur - 1.2f, capDur, e));
            if (al < 0.01f) { if (capTm.gameObject.activeSelf) capTm.gameObject.SetActive(false); if (subTm.gameObject.activeSelf) subTm.gameObject.SetActive(false); return; }
            if (!capTm.gameObject.activeSelf) capTm.gameObject.SetActive(true); capTm.text = capA; capTm.color = new Color(1f, 1f, 1f, al); subTm.gameObject.SetActive(capB.Length > 0); subTm.text = capB; subTm.color = new Color(1f, 0.95f, 0.85f, al);
            if (cam != null) { Vector3 p = cam.transform.position + cam.transform.forward * 3.0f + cam.transform.up * 0.35f; capTm.transform.position = p; capTm.transform.rotation = Quaternion.LookRotation(p - cam.transform.position, cam.transform.up); subTm.transform.position = p - cam.transform.up * 0.34f; subTm.transform.rotation = capTm.transform.rotation; }
        }
    }
}
