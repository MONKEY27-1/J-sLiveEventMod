import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
# ---- sky: the reference is a deep teal-blue night with a moon
s = rd('AstronomicalGalactus.cs')
s = sub(s, "top = Color.Lerp(new Color(0.42f, 0.52f, 0.68f), new Color(0.03f, 0.05f, 0.3f), k); mid = Color.Lerp(new Color(0.65f, 0.68f, 0.8f), new Color(0.1f, 0.15f, 0.5f), k); hor = Color.Lerp(new Color(0.85f, 0.8f, 0.9f), new Color(0.35f, 0.3f, 0.75f), k);",
        "top = Color.Lerp(new Color(0.42f, 0.52f, 0.68f), new Color(0.01f, 0.05f, 0.2f), k); mid = Color.Lerp(new Color(0.65f, 0.68f, 0.8f), new Color(0.02f, 0.12f, 0.34f), k); hor = Color.Lerp(new Color(0.85f, 0.8f, 0.9f), new Color(0.06f, 0.26f, 0.5f), k);")
s = sub(s, "c = Color.Lerp(new Color(0.6f, 0.7f, 1f), new Color(0.7f, 0.45f, 1f), Sm(200f, 250f, t)); a = 0.1f;", "c = Color.Lerp(new Color(0.6f, 0.7f, 1f), new Color(0.25f, 0.55f, 1f), Sm(200f, 250f, t)); a = 0.1f + 0.08f * Sm(215f, 250f, t);")
wr('AstronomicalGalactus.cs', s)
# ---- Galactus: bent right over you (legs trail away out of view), hand spread over the island
s = rd('AstronomicalGalactus2.cs')
s = sub(s, "var rq = Quaternion.Euler(lean * 0.6f, 180f, 0f);", "float bend = 78f * Env(186f, 206f, 262f, 280f, t); var rq = Quaternion.Euler(lean * 0.6f + bend, 180f, 0f);")
s = sub(s, "pos = new Vector3(Mathf.Lerp(0f, 90f, k), f.y - Mathf.Lerp(150f, 190f, k), Mathf.Lerp(360f, 250f, k)); return true; }",
        "pos = new Vector3(Mathf.Lerp(0f, 30f, k), f.y + Mathf.Lerp(-150f, 330f, k), Mathf.Lerp(360f, 230f, k)); return true; }")
s = sub(s, "GS * (1f + 1.1f * Env(184f, 204f, 266f, 276f, t))", "GS * (1f + 0.9f * Env(184f, 204f, 266f, 276f, t))")
wr('AstronomicalGalactus2.cs', s)
# ---- energy: thin blue-white strands from every finger tip to the Zero Point, a light pillar and the moon
s = rd('AstronomicalGalactus3.cs')
i = s.index("            for (int i = 0; i < conv.Count; i++) { var l = conv[i];"); j = s.index("\n", i)
new = """            Vector3 cdir = (pt - hand).sqrMagnitude < 1f ? Vector3.down : (pt - hand).normalized; Vector3 cside = Vector3.Cross(cdir, Vector3.up); cside = cside.sqrMagnitude < 1e-3f ? Vector3.right : cside.normalized;
            for (int i = 0; i < conv.Count; i++) { var l = conv[i]; if (l.enabled != cv) l.enabled = cv; if (!cv) continue; int n = l.positionCount; float fk = (i - (conv.Count - 1) * 0.5f) / ((conv.Count - 1) * 0.5f); Vector3 a = hand + cside * (fk * 2.0f * gxS) + cdir * (0.5f * gxS); for (int k = 0; k < n; k++) { float s = k / (float)(n - 1); Vector3 q = Vector3.Lerp(a, pt, s * Mathf.Min(1f, 0.25f + 1.2f * ce)); q += (cside * Mathf.Sin(s * 11f + t * 6f + i) + Vector3.Cross(cdir, cside) * Mathf.Cos(s * 8f + t * 5f - i)) * 5f * Mathf.Sin(s * Mathf.PI); l.SetPosition(k, q); } l.widthMultiplier = 2.2f + 2.5f * ce; convMat.color = Color.Lerp(new Color(0.55f, 0.8f, 1f, 0.95f), new Color(0.9f, 0.97f, 1f, 1f), ce); }"""
s = s[:i] + new + s[j:]
s = sub(s, "Color.Lerp(new Color(0.8f, 0.5f, 1f, 1f), new Color(0.8f, 0.95f, 1f, 1f), ce), cam, cv);", "Color.Lerp(new Color(0.6f, 0.8f, 1f, 1f), new Color(0.9f, 0.97f, 1f, 1f), ce), cam, cv);")
wr('AstronomicalGalactus3.cs', s)
s = rd('AstronomicalGalactus4.cs')
s = sub(s, "GameObject cockGo;", "GameObject cockGo; Transform moon, pillar;")
s = sub(s, "            // ---- the Zero Point between his hands", "            moon = Prim(PrimitiveType.Sphere, \"GMoon\", kit.Flat(new Color(0.92f, 0.96f, 1f))); moon.gameObject.SetActive(false); pillar = Quad(\"GPillar\", kit.Glow(new Color(0.6f, 0.85f, 1f, 0.6f), glowTex, 3001)); pillar.gameObject.SetActive(false);\n            // ---- the Zero Point between his hands")
s = sub(s, "reticle = zpBall = zpGlow", "moon = pillar = reticle = zpBall = zpGlow")
s = sub(s, "            // ---- the Zero Point\n", """            // ---- the moon over the island and the light pillar at the Zero Point (3:35 - 4:30)
            bool mn = t >= 224f && t < 276f && cam != null; if (moon != null && moon.gameObject.activeSelf != mn) { moon.gameObject.SetActive(mn); pillar.gameObject.SetActive(mn); }
            if (mn) { Vector3 mdir = (root.transform.TransformDirection(new Vector3(0.45f, 0.28f, 1f))).normalized; moon.position = cam.transform.position + mdir * 2200f; moon.localScale = Vector3.one * 150f; Vector3 f0 = W(FallPos(t) + new Vector3(10f, -45f, 70f * (1f - 0.4f * Sm(212f, 265f, t)))); Vector3 up0 = f0 + Vector3.down * 120f; Glow(pillar, up0, 90f, new Color(0.6f, 0.85f, 1f, 0.5f * Sm(224f, 240f, t)), cam, true); }
            // ---- the Zero Point
""")
wr('AstronomicalGalactus4.cs', s)
