import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
# ================= Galactus.cs
s = rd('AstronomicalGalactus.cs')
# stars from late in the fall (4:00-4:30 the recording is a deep blue starry dusk)
s = sub(s, "bool stars = g.S >= Sc.Space;", "bool stars = g.S >= Sc.Space || (g.S == Sc.Fall && t >= 232f);")
# fall sky: grey-blue -> deep blue night
s = sub(s, "case Sc.Fall: { float k = Sm(195f, 240f, t); top = Color.Lerp(new Color(0.4f, 0.55f, 0.75f), new Color(0.2f, 0.18f, 0.5f), k); mid = Color.Lerp(new Color(0.75f, 0.7f, 0.85f), new Color(0.55f, 0.3f, 0.7f), k); hor = Color.Lerp(new Color(1f, 0.85f, 0.85f), new Color(0.9f, 0.5f, 0.75f), k); break; }",
        "case Sc.Fall: { float k = Sm(195f, 240f, t); top = Color.Lerp(new Color(0.42f, 0.52f, 0.68f), new Color(0.03f, 0.05f, 0.3f), k); mid = Color.Lerp(new Color(0.65f, 0.68f, 0.8f), new Color(0.1f, 0.15f, 0.5f), k); hor = Color.Lerp(new Color(0.85f, 0.8f, 0.9f), new Color(0.35f, 0.3f, 0.75f), k); break; }")
# anchors: inside the bus for the plan, chase camera behind it for the corridor / space
s = sub(s, "case Sc.BusIn: case Sc.Assault: case Sc.Inside: case Sc.Space: case Sc.Mouth: case Sc.Blast: { a = BusPos + BusRoof + Vector3.up * 1.6f; hx = 2.4f; zMin = -3.2f; zMax = 3.2f; return true; }",
        "case Sc.BusIn: { a = BusPos + new Vector3(0f, BusEye, 3f); hx = 1.7f; zMin = -4f; zMax = 3f; return true; }   // 4:30 - 5:30 you look out of the bus' windows\n                case Sc.Assault: { a = BusPos + BusRoof + Vector3.up * 3f; hx = 2.4f; zMin = -3.2f; zMax = 3.2f; return true; }\n                case Sc.Inside: { a = BusPos + new Vector3(0f, 9f, -36f); hx = 4f; zMin = -4f; zMax = 4f; return true; }   // 7:00 - 8:30 the recording follows the bus from behind\n                case Sc.Space: case Sc.Mouth: case Sc.Blast: { a = BusPos + new Vector3(-17f, 7f, -33f); hx = 4f; zMin = -4f; zMax = 4f; return true; }")
wr('AstronomicalGalactus.cs', s)
# ================= Galactus2.cs
s = rd('AstronomicalGalactus2.cs')
s = sub(s, "public const float RunS = 3f;", "public const float BusEye = -0.8f; public const float RunS = 3f;")
# Galactus closer in the Reach (he towers over the deck), lilac haze that stays until the hit, belt higher
s = sub(s, "Mathf.Lerp(1f, 0.25f, Sm(60f, 160f, t))); pos = new Vector3(0f, IY - Mathf.Lerp(140f, 85f, Sm(100f, 165f, t)), d); return true; }",
        "Mathf.Lerp(1f, 0.16f, Sm(60f, 165f, t))); pos = new Vector3(0f, IY - Mathf.Lerp(140f, 95f, Sm(100f, 165f, t)), d); return true; }")
# space / mouth: his face is ahead of the bus, not below it
s = sub(s, "if (t < 535f) { pos = new Vector3(0f, BusPos.y - 235f, Mathf.Lerp(190f, 120f, Sm(510f, 535f, t))); return true; }\n            if (t < 576f) { pos = new Vector3(0f, BusPos.y - 235f, Mathf.Lerp(120f, 100f, Sm(535f, 575f, t))); return true; }",
        "if (t < 535f) { pos = new Vector3(0f, BusPos.y - Mathf.Lerp(120f, 105f, Sm(510f, 535f, t)), Mathf.Lerp(250f, 170f, Sm(510f, 535f, t))); return true; }\n            if (t < 576f) { pos = new Vector3(0f, BusPos.y - Mathf.Lerp(105f, 62f, Sm(535f, 560f, t)), Mathf.Lerp(170f, 95f, Sm(535f, 575f, t))); return true; }")
# backhand slap: the arm sweeps across the carrier from outside to the middle
i = s.index("        void GxHitAim(float t)"); j = s.index("        // the new model's arms are sculpted")
new = """        void GxHitAim(float t)
        {
            if (gxHSh == null || gxHEl == null || t < 168f || t >= 180f) return;
            float w = Mathf.Pow(Mathf.Clamp01((t - 168f) / 5f), 1.2f); if (w <= 0f) return;
            // 2:55 - 3:00 a BACKHAND: the forearm swings in from outside the carrier, low and fast, across the deck (the recording: the arm sweeps through the whole foreground)
            Vector3 sl = root.transform.InverseTransformPoint(gxHSh.position); float sd = Mathf.Sign(sl.x == 0f ? 1f : sl.x); float u = Mathf.Clamp01((t - 168f) / 12f), e = Mathf.Pow(u, 1.8f);
            Vector3 tg = new Vector3(sd * Mathf.Lerp(78f, -38f, e), IY + DeckY + Mathf.Lerp(34f, 6f, Mathf.Min(1f, e * 1.6f)), Mathf.Lerp(12f, -8f, e)); Vector3 d = tg - sl; float reach = 104f; if (d.magnitude > reach) tg = sl + d.normalized * reach; hitTgt = tg;
            AimArm(gxHSh, gxHEl, gxHHand, (W(tg) - gxHSh.position).normalized, w);
        }
"""
s = s[:i] + new + s[j:]
# lilac haze on Galactus in the Reach (until the hit)
s = sub(s, "float hz = t < 130f ? 1f - Sm(55f, 128f, t) : 0f;", "float hz = t < 130f ? 1f - Sm(55f, 128f, t) : 0f; float hl = t >= 100f && t < 190f ? 0.55f * Sm(100f, 130f, t) * (1f - Sm(176f, 190f, t)) : 0f;")
s = sub(s, "if (Mathf.Abs(hz + hm - GxHaze) > 0.01f || t < 130f || hm > 0f) { GxHaze = hz + hm; Color c = hm > 0f ?",
        "if (Mathf.Abs(hz + hm + hl - GxHaze) > 0.01f || t < 130f || hm > 0f || hl > 0f) { GxHaze = hz + hm + hl; Color c = hl > 0f ? Color.Lerp(Color.white, new Color(1.9f, 1.45f, 2.1f), hl) : hm > 0f ?")
wr('AstronomicalGalactus2.cs', s)
# ================= Galactus3.cs: the carrier hit shows up after the sweep reaches the deck; BusRoof unchanged
s = rd('AstronomicalGalactus3.cs')
wr('AstronomicalGalactus3.cs', s)
