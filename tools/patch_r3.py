import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
s = rd('AstronomicalGalactus2.cs')
# --- arm run: slower approach, farther from the arm
i = s.index("            if (t < 362f) { u = t < 345f"); j = s.index("\n", i)
s = s[:i] + "            if (t < 362f) { u = t < 352f ? Mathf.Lerp(-0.9f, 0f, Sm(330f, 352f, t)) : Mathf.Lerp(0f, 0.02f, Sm(352f, 362f, t)); float upf = t < 352f ? 1f : 1f - Sm(352f, 362f, t); T = H + (S - H) * u + Vector3.up * ((rad * (1f + 0.45f * Mathf.Clamp01(u)) + 70f) * upf); lat = t < 352f ? RunWeave(u) : 0f; }" + s[j:]
# --- Galactus side + height + distance in the opening (he stands on HIS left = your right, high in the sky, further back)
s = sub(s, "Mathf.Lerp(1f, 0.16f, Sm(60f, 165f, t))); pos = new Vector3(0f, IY - Mathf.Lerp(140f, 95f, Sm(100f, 165f, t)), d); return true; }",
        "Mathf.Lerp(1.3f, 0.16f, Sm(60f, 165f, t))); pos = new Vector3(d * 0.34f * (1f - Sm(60f, 172f, t)), IY + Mathf.Lerp(620f, -95f, Sm(64f, 168f, t)), d); return true; }")
# --- the fall: he is bigger and closer and reaches over the island with his open hand (3:35 - 4:30)
s = sub(s, "if (t < 272f) { Vector3 f = FallPos(t); pos = new Vector3(40f, f.y - 150f, 360f); return true; }",
        "if (t < 272f) { Vector3 f = FallPos(t); float k = Sm(180f, 200f, t); pos = new Vector3(Mathf.Lerp(0f, 90f, k), f.y - Mathf.Lerp(150f, 190f, k), Mathf.Lerp(360f, 250f, k)); return true; }")
s = sub(s, "bool run = t >= 330f && t < 420f; s = run ? GS * RunS : GS; gxS = s;",
        "bool run = t >= 330f && t < 420f; s = run ? GS * RunS : GS * (1f + 1.1f * Env(184f, 204f, 266f, 276f, t)); gxS = s;")
s = sub(s, "GxHitAim(t); GxRun(t, go, s); GxCup(t);", "GxHitAim(t); GxReach(t); GxRun(t, go, s); GxCup(t);")
s = sub(s, "        void GxHitAim(float t)", """        // 3:35 - 4:30 the arm reaches out over the island, palm open and fingers spread, to the point under the falling player where the energy gathers
        void GxReach(float t)
        {
            if (gxOSh == null || gxOEl == null || t < 196f || t >= 276f) return; float w = Sm(198f, 214f, t) * (1f - Sm(262f, 276f, t)); if (w <= 0.001f) return;
            Vector3 f = FallPos(t); Vector3 pt = new Vector3(f.x + 10f, f.y - 45f, f.z + 70f * (1f - 0.4f * Sm(212f, 265f, t)));
            AimArm(gxOSh, gxOEl, gxOHand, (W(pt) - gxOSh.position).normalized, w);
        }
        void GxHitAim(float t)""")
wr('AstronomicalGalactus2.cs', s)
s = rd('AstronomicalGalactus3.cs')
s = sub(s, "Vector3 pt = W(FallPos(t)) + new Vector3(10f, -45f, 70f * (1f - 0.4f * ce));", "Vector3 pt = W(FallPos(t) + new Vector3(10f, -45f, 70f * (1f - 0.4f * ce)));")
wr('AstronomicalGalactus3.cs', s)
