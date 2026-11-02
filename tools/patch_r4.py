import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
# ---- cockpit gun small and low
s = rd('AstronomicalGalactus4.cs')
s = sub(s, "cockGo.transform.position = ct.position + ct.forward * 0.95f + ct.up * -0.5f; cockGo.transform.rotation = ct.rotation; cockGo.transform.localScale = Vector3.one * 1.5f;",
        "cockGo.transform.position = ct.position + ct.forward * 0.95f + ct.up * -0.62f; cockGo.transform.rotation = ct.rotation; cockGo.transform.localScale = Vector3.one * 0.5f;")
wr('AstronomicalGalactus4.cs', s)
# ---- sky / tint: the corridor is blue-violet, the blast stays dark space until the white burst
s = rd('AstronomicalGalactus.cs')
s = sub(s, "case Sc.Inside: top = new Color(0.12f, 0.02f, 0.06f); mid = new Color(0.3f, 0.05f, 0.08f); hor = new Color(0.5f, 0.1f, 0.1f); break;",
        "case Sc.Inside: top = new Color(0.03f, 0.02f, 0.14f); mid = new Color(0.08f, 0.05f, 0.3f); hor = new Color(0.25f, 0.12f, 0.5f); break;")
s = sub(s, "case Sc.Inside: c = new Color(1f, 0.15f, 0.1f); a = 0.2f; break;", "case Sc.Inside: c = new Color(0.45f, 0.3f, 1f); a = 0.16f; break;")
i = s.index("                case Sc.Blast: { float k = Sm(580f, 592f, t);"); j = s.index("\n", i)
s = s[:i] + "                case Sc.Blast: { float k = Sm(580f, 590f, t) * (1f - Sm(593f, 598f, t)); top = Color.Lerp(new Color(0.01f, 0.01f, 0.07f), new Color(0.08f, 0.2f, 0.6f), k); mid = Color.Lerp(new Color(0.03f, 0.04f, 0.2f), new Color(0.25f, 0.45f, 0.95f), k); hor = Color.Lerp(new Color(0.1f, 0.08f, 0.32f), new Color(0.6f, 0.8f, 1f), k); break; }" + s[j:]
s = sub(s, "case Sc.Blast: c = new Color(0.5f, 0.8f, 1f); a = 0.15f; break;", "case Sc.Blast: c = new Color(0.5f, 0.8f, 1f); a = 0.06f * (1f - Sm(592f, 597f, t)); break;")
wr('AstronomicalGalactus.cs', s)
s = rd('AstronomicalGalactus3.cs')
# redLamp colour
s = sub(s, "redLamp.intensity = (g.S == Sc.Inside", "redLamp.color = new Color(0.6f, 0.4f, 1f); redLamp.intensity = (g.S == Sc.Inside")
# ---- recolour the corridor models once (shared materials)
s = sub(s, "            BuildExtra();", """            BuildExtra();
            if (corM != null) foreach (var r in corM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) { if (m == null) continue; try { m.mainTexture = null; bool lit = m.HasProperty("_Glossiness") || m.shader.name.Contains("Standard"); m.color = lit ? new Color(0.22f, 0.18f, 0.5f, m.color.a) : new Color(0.85f, 0.7f, 1f, m.color.a); } catch { } }""")
# explosions in the corridor: blue-white / pink rather than orange
s = sub(s, "Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.8f, 0.4f), UnityEngine.Random.value), 0.6f, 1.2f);", "Color.Lerp(new Color(0.6f, 0.5f, 1f), new Color(1f, 0.7f, 0.95f), UnityEngine.Random.value), 0.6f, 1.2f);")
# ---- 9:45 the cosmic burst: shells of blue-white / violet sparks + a bright flash glow, collapsing to a blue point at 9:54
s = sub(s, "                if (t >= 578f && t < 596f) for (int k = 0; k < 6; k++)", """                if (t >= 585f && t < 593f) { float e = t - 585f; for (int k = 0; k < 40; k++) { Vector3 d = UnityEngine.Random.onUnitSphere; Emit(fx, W(BusPos + new Vector3(0f, 6f, 60f)) + d * (e * 330f), d * 200f, Color.Lerp(new Color(0.55f, 0.75f, 1f, 0.95f), new Color(0.85f, 0.55f, 1f, 0.95f), UnityEngine.Random.value), UnityEngine.Random.Range(14f, 40f), 1.2f); } }
                if (t >= 578f && t < 596f) for (int k = 0; k < 6; k++)""")
wr('AstronomicalGalactus3.cs', s)
