import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:60].replace('\n', ' '))
    return s.replace(a, b, 1)
s = rd('AstronomicalGalactus3.cs')
# ---- build: better liner texture, 96 dark + 96 glowing ring segments, 8 light strips
i = s.index("            var tunTex = kit.Tex(64, 64"); j = s.index("            for (int i = 0; i < 6; i++) conduits.Add(")
j = s.index("\n", j)
build = """            var tunTex = kit.Tex(128, 128, (x, y) => { float fx = x % 32, fy = y % 64; bool seam = fx < 2 || fy < 2; bool seam2 = fx > 14 && fx < 17 && fy > 8 && fy < 56; if (seam) return new Color(0.02f, 0.015f, 0.025f, 1f); if (seam2) return new Color(1f, 0.32f, 0.22f, 1f); bool rivet = (fx == 5 || fx == 26) && (fy % 16 == 8); if (rivet) return new Color(0.5f, 0.2f, 0.18f, 1f); float v = 0.17f + 0.07f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f); return new Color(v * 1.15f, v * 0.62f, v * 0.7f, 1f); }, TextureWrapMode.Repeat);
            tunMat = kit.Flat(Color.white); tunMat.mainTexture = tunTex; try { tunMat.SetFloat("_Cull", 0f); } catch { } tunnel = Prim(PrimitiveType.Cylinder, "GTunnel", tunMat); tunnel.gameObject.SetActive(false);
            tunEnd = Quad("GTunEnd", kit.Glow(new Color(1f, 0.55f, 0.45f, 1f), glowTex, 3003)); tunEnd.gameObject.SetActive(false); condMat = kit.Glow(new Color(1f, 0.38f, 0.3f, 0.95f), lineTex, 3002); var gateMat = kit.Flat(new Color(0.16f, 0.1f, 0.12f)); var trimMat = kit.Glow(new Color(1f, 0.3f, 0.2f, 1f), null, 3001); gates.Clear(); conduits.Clear();
            for (int i = 0; i < 192; i++) { var gt = Prim(PrimitiveType.Cube, "GGate" + i, i < 96 ? gateMat : trimMat); gt.gameObject.SetActive(false); gates.Add(gt); }
            for (int i = 0; i < 8; i++) conduits.Add(MakeLine("GCond" + i, condMat, 2, 3.5f));"""
s = s[:i] + build + s[j:]
# ---- frame
i = s.index("            // the tunnel inside his arm"); j = s.index("            if (!paused && runOn && tp < 351f")
frame = """            // ONE long straight tunnel along the arm's axis (root z): the bus stays in the middle, the liner / ribs / light strips stream past (a treadmill, so it is as long as the music needs)
            bool ins = runOn && runInside && tunnel != null;
            if (tunnel != null && tunnel.gameObject.activeSelf != ins) tunnel.gameObject.SetActive(ins);
            if (ins)
            {
                const float R = 50f, LEN = 1500f, SPEED = 52f; float te = t - 351f; Vector3 c0 = BusPos + new Vector3(0f, 7f, 0f);
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
                condMat.color = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0.95f), new Color(0.9f, 0.4f, 1f, 0.95f), 0.5f + 0.5f * Mathf.Sin(t * 0.8f));
                Glow(tunEnd, W(c0 + new Vector3(0f, 0f, 720f)), R * 7f, new Color(1f, 0.55f + 0.2f * Mathf.Sin(t * 2f), 0.4f, 0.8f), cam, true);
            }
            else { if (gates.Count > 0 && gates[0].gameObject.activeSelf) foreach (var gt in gates) gt.gameObject.SetActive(false); foreach (var l in conduits) if (l.enabled) l.enabled = false; if (tunEnd != null && tunEnd.gameObject.activeSelf) tunEnd.gameObject.SetActive(false); }
"""
s = s[:i] + frame + s[j:]
# drones: bounded by the tunnel radius, passing the bus
s = sub(s, "(inT ? 62f : 120f)", "(inT ? 70f : 120f)")
s = sub(s, "(inT ? (Hash(i, 74) - 0.5f) * 44f : 8f + 80f * Hash(i, 74))", "(inT ? 7f + (Hash(i, 74) - 0.5f) * 70f : 8f + 80f * Hash(i, 74))")
wr('AstronomicalGalactus3.cs', s)
