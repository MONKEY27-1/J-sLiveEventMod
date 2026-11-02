import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:60].replace('\n', ' '))
    return s.replace(a, b, 1)
# ---------------- Galactus.cs: cuts
s = rd('AstronomicalGalactus.cs')
s = sub(s, "static readonly float[] CUTS = { 180f, 270f, 330f, 420f, 510f, 535f, 575f, 600f, 605f };", "static readonly float[] CUTS = { 180f, 270f, 330f, 351f, 397f, 420f, 510f, 535f, 575f, 600f, 605f };")
wr('AstronomicalGalactus.cs', s)
# ---------------- Galactus2.cs: path through the arm + hide the shell
s = rd('AstronomicalGalactus2.cs')
i = s.index("            float rad = 3.4f * s; Vector3 T; float u;"); j = s.index("            runH = H + shift;", i)
new = """            float rad = 3.4f * s; Vector3 T; float u, lat; runInside = t >= 351f && t < 397f; ShowArm(!runInside);
            if (t < 351f) { u = t < 345f ? Mathf.Lerp(-0.7f, 0f, Sm(330f, 345f, t)) : Mathf.Lerp(0f, 0.02f, Sm(345f, 351f, t)); float upf = t < 345f ? 1f : 1f - Sm(345f, 351f, t); T = H + (S - H) * u + Vector3.up * ((rad * (1f + 0.45f * Mathf.Clamp01(u)) + 22f) * upf); lat = t < 345f ? RunWeave(u) : 0f; }
            else if (t < 397f) { u = Mathf.Lerp(0.03f, 0.97f, Sm(351f, 397f, t)); T = H + (S - H) * u; lat = 7f * Mathf.Sin(t * 0.6f); }   // inside the arm: the bus is on its axis
            else { u = 1f; lat = 0f; Vector3 P0 = S + Vector3.up * (rad * 1.45f + 22f), P1 = Fp + new Vector3(0f, -1f * s, -6f * s); T = Vector3.Lerp(P0, P1, Sm(397f, 420f, t)); }
            Vector3 want = BusPos - new Vector3(lat, 0f, 0f), shift = want - T; go.transform.localPosition += shift;
"""
s = s[:i] + new + s[j:]
s = sub(s, "public const float RunS = 3f; Vector3 runH, runS, runHd; float runRad, runU; bool runOn;", "public const float RunS = 3f; Vector3 runH, runS, runHd; float runRad, runU; bool runOn, runInside, armHidden;\n        // while the bus is inside the arm the arm's own shell is hidden (the tunnel replaces it)\n        void ShowArm(bool show) { if (gxOSh == null || armHidden == !show) return; armHidden = !show; foreach (var r in gxOSh.GetComponentsInChildren<Renderer>(true)) r.enabled = show; }")
s = sub(s, "            if (galM == null) return; Vector3 pos; bool on = GxPlace(t, g, out pos);", "            if (galM == null) return; if (!(t >= 351f && t < 397f)) ShowArm(true); Vector3 pos; bool on = GxPlace(t, g, out pos);")
wr('AstronomicalGalactus2.cs', s)
# ---------------- Galactus3.cs
s = rd('AstronomicalGalactus3.cs')
s = sub(s, "// the arm run: pylons standing on his arm, a swarm of drones, the bus' lasers", "// the arm run: the bus flies INTO his arm and through a lit tunnel to the shoulder (gate frames, conduits), a swarm of drones, the bus' lasers\n        Transform tunnel, tunEnd; Material tunMat, condMat; readonly List<Transform> gates = new List<Transform>(); readonly List<LineRenderer> conduits = new List<LineRenderer>();")
i = s.index('            for (int i = 0; i < 12; i++) { var p = Prim(PrimitiveType.Cylinder, "GPylon"'); j = s.index("\n", i)
tun = """            var tunTex = kit.Tex(64, 64, (x, y) => { if (y < 5) return new Color(1f, 0.3f, 0.2f, 1f); if (x < 3 || (x >= 32 && x < 35)) return new Color(1f, 0.5f, 0.3f, 1f); if (y == 30 || y == 31) return new Color(0.75f, 0.2f, 0.5f, 1f); if (x % 16 == 0) return new Color(0.04f, 0.03f, 0.05f, 1f); float v = 0.12f + 0.05f * ((x * 7 + y * 3) % 5) / 5f; return new Color(v, v * 0.85f, v * 1.2f, 1f); }, TextureWrapMode.Repeat);
            tunMat = kit.Flat(Color.white); tunMat.mainTexture = tunTex; try { tunMat.SetFloat("_Cull", 0f); } catch { } tunnel = Prim(PrimitiveType.Cylinder, "GTunnel", tunMat); tunnel.gameObject.SetActive(false);
            tunEnd = Quad("GTunEnd", kit.Glow(new Color(1f, 0.55f, 0.45f, 1f), glowTex, 3003)); tunEnd.gameObject.SetActive(false); condMat = kit.Glow(new Color(1f, 0.4f, 0.35f, 0.9f), lineTex, 3002); var gateMat = kit.Flat(new Color(1f, 0.28f, 0.2f)); gates.Clear(); conduits.Clear();
            for (int i = 0; i < 48; i++) { var gt = Prim(PrimitiveType.Cube, "GGate" + i, gateMat); gt.gameObject.SetActive(false); gates.Add(gt); }
            for (int i = 0; i < 6; i++) conduits.Add(MakeLine("GCond" + i, condMat, 20, 3f));"""
s = s[:i] + tun + s[j:]
s = sub(s, "cracks.Clear(); pylons.Clear(); pylonGl.Clear();", "cracks.Clear(); pylons.Clear(); pylonGl.Clear(); gates.Clear(); conduits.Clear(); tunnel = tunEnd = null;")
i = s.index("            for (int i = 0; i < pylons.Count; i++)\n            {\n                float u = 0.04f"); j = s.index("            for (int i = 0; i < drones.Count; i++)", i)
rf = """            // the tunnel inside his arm: a ribbed liner round the arm's axis, 12 square gate frames, glowing conduits, a red door at the far end
            bool ins = runOn && runInside && tunnel != null;
            if (tunnel != null && tunnel.gameObject.activeSelf != ins) tunnel.gameObject.SetActive(ins);
            if (ins)
            {
                Vector3 ax = runS - runH; float L = ax.magnitude; Vector3 dir = ax / L, mid = (runH + runS) * 0.5f; float R = runRad * 0.72f; var rot = Quaternion.FromToRotation(Vector3.up, dir);
                tunnel.localPosition = mid; tunnel.localRotation = rot; tunnel.localScale = new Vector3(2f * R, L * 0.5f, 2f * R); tunMat.mainTextureScale = new Vector2(8f, L / 45f);
                Vector3 e1 = rot * Vector3.right, e2 = rot * Vector3.forward;
                for (int g = 0; g < 12; g++)
                {
                    float uu = 0.06f + 0.08f * g; Vector3 c = runH + ax * uu; var gq = rot * Quaternion.Euler(0f, 30f * g, 0f); float hs = R * 0.78f, th = 4f;
                    for (int b = 0; b < 4; b++) { var bar = gates[g * 4 + b]; if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true); Vector3 off = b == 0 ? new Vector3(0f, 0f, hs) : b == 1 ? new Vector3(0f, 0f, -hs) : b == 2 ? new Vector3(hs, 0f, 0f) : new Vector3(-hs, 0f, 0f); bar.localPosition = c + gq * off; bar.localRotation = gq; bar.localScale = b < 2 ? new Vector3(2f * hs + th, th, th) : new Vector3(th, th, 2f * hs); }
                }
                for (int k = 0; k < conduits.Count; k++) { var l = conduits[k]; if (!l.enabled) l.enabled = true; float ang = k * 1.0472f + 0.4f; Vector3 rad = (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * (R * 0.88f); for (int q = 0; q < l.positionCount; q++) { float sq = q / (float)(l.positionCount - 1); l.SetPosition(q, W(runH + ax * sq + rad + (e1 * Mathf.Sin(sq * 40f + t * 6f + k) + e2 * Mathf.Cos(sq * 33f - t * 5f)) * 2.5f)); } }
                Glow(tunEnd, W(runS - dir * 4f), R * 3.2f, new Color(1f, 0.5f + 0.2f * Mathf.Sin(t * 4f), 0.4f, 0.55f), cam, true);
            }
            else { if (gates.Count > 0 && gates[0].gameObject.activeSelf) foreach (var gt in gates) gt.gameObject.SetActive(false); foreach (var l in conduits) if (l.enabled) l.enabled = false; if (tunEnd != null && tunEnd.gameObject.activeSelf) tunEnd.gameObject.SetActive(false); }
            if (!paused && runOn && tp < 351f && t >= 351f) { Boom(W(BusPos), 24f, 1.0f, new Color(1f, 0.6f, 0.5f)); }
"""
s = s[:i] + rf + s[j:]
s = sub(s, "Vector3 p = BusPos + new Vector3((Hash(i, 73) - 0.5f) * 120f + 6f * Mathf.Sin(t * 1.3f + i), 8f + 80f * Hash(i, 74) + 5f * Mathf.Sin(t * 1.7f + i * 2f), dz);",
        "bool inT = t >= 351f && t < 397f; Vector3 p = BusPos + new Vector3((Hash(i, 73) - 0.5f) * (inT ? 62f : 120f) + 6f * Mathf.Sin(t * 1.3f + i), (inT ? (Hash(i, 74) - 0.5f) * 44f : 8f + 80f * Hash(i, 74)) + 5f * Mathf.Sin(t * 1.7f + i * 2f), dz);")
s = sub(s, "bool fl = (t >= 270f && t < 422f) || (t >= 508f && t < 578f);", "bool fl = ((t >= 270f && t < 422f) && !(t >= 351f && t < 397f)) || (t >= 508f && t < 578f);")
wr('AstronomicalGalactus3.cs', s)
