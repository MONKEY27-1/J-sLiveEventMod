"""one-off: storm closer / taller, swimmable flood, extra detail for AstronomicalDevice.cs"""
import os
HERE = os.path.dirname(os.path.abspath(__file__)); p = os.path.join(os.path.dirname(HERE), 'AstronomicalDevice.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, (s.count(a), a[:80])
    s = s.replace(a, b)
rep("NEAR = 105f, MID = 150f", "NEAR = 70f, MID = 100f")
rep("Bolt[] bolts = new Bolt[30];", "Bolt[] bolts = new Bolt[48];")
rep("stormMesh = RingMesh(72, 6, 120f, true);", "stormMesh = RingMesh(72, 10, 320f, true);")
rep("uv.Add(new Vector2(j * 6f / lon, f * 2f));", "uv.Add(new Vector2(j * 6f / lon, f * 5f));")
rep("BuildSky(); BuildOffice(); BuildFlash();", "BuildSky(); BuildOffice(); BuildFlash(); BuildExtras();")
# bolts: forks, higher storm targets, impact glows, plasma
rep("void AddBolt(Vector3 a, Vector3 b, Color c, float width, float t, int seed)", "void AddBolt(Vector3 a, Vector3 b, Color c, float width, float t, int seed, bool fork = true)")
rep("bo.Core.startColor = bo.Core.endColor = new Color(1f, 1f, 1f, 0.95f);",
    "bo.Core.startColor = bo.Core.endColor = new Color(1f, 1f, 1f, 0.95f);\n            if (fork && width > 0.9f && boltN < bolts.Length) { var mid = Vector3.Lerp(a, b, 0.35f + 0.3f * BoltRnd(seed, 5, t)); var end = mid + (side * (BoltRnd(seed, 6, t) - 0.5f) + side2 * (BoltRnd(seed, 7, t) - 0.5f)) * len * 0.5f + dir.normalized * len * 0.08f; AddBolt(mid, end, c, width * 0.45f, t, seed + 999, false); }")
rep("ClearBolts(); int n = OrbBolts(t);", "ClearBolts(); impactN = 0; int n = OrbBolts(t);")
old = s[s.index("                if (toStorm && (t < 193f || t >= 231f))"):]
old = old[:old.index("\n")]
rep(old, "                if (toStorm && (t < 193f || t >= 231f)) { float a = PylonAng(i), r = StormR(t); var tg = new Vector3(Mathf.Cos(a) * r, gy + 80f + 40f * Mathf.Sin(t * 1.3f + i), Mathf.Sin(a) * r); AddBolt(tip, tg, pink, 1.5f, t, i * 7 + 3); if (impactN < impactPts.Length) impactPts[impactN++] = tg; if (i % 2 == 0) AddBolt(tip, new Vector3(Mathf.Cos(a + 0.5f) * r, gy + 150f, Mathf.Sin(a + 0.5f) * r), cyan, 0.9f, t, i * 7 + 5); }")
rep("            if (t >= 130f && t < 138f && n > 0 && !paused) { }",
    "            if (OrbOn(t)) { float orad = 9f * Sm(111f, 114f, t) * (1f - Sm(296f, 298f, t)); var oc = OrbCentre(t); int pk = 2 + (int)(3f * Mathf.Clamp01(n / 5f)); for (int k = 0; k < pk; k++) AddBolt(oc, oc + new Vector3(BoltRnd(300 + k, 1, t) - 0.5f, BoltRnd(300 + k, 2, t) - 0.5f, BoltRnd(300 + k, 3, t) - 0.5f).normalized * orad, pink, 0.7f, t, 300 + k, false); }     // the plasma inside the orb")
# water: a surface inside the wall, at a level that rises to swimming depth
rep("if (waterG.activeSelf != won) { waterG.SetActive(won); seaG.SetActive(won); }", "if (waterG.activeSelf != won) { waterG.SetActive(won); seaG.SetActive(won); discG.SetActive(won); }")
rep("seaG.transform.localPosition = new Vector3(0f, gy + 0.2f, 0f); seaG.transform.localScale = new Vector3(r, 1f, r);",
    "float wl = WaterLevel(t); seaG.transform.localPosition = new Vector3(0f, gy + wl, 0f); seaG.transform.localScale = new Vector3(r, 1f, r); discG.transform.localPosition = new Vector3(0f, gy + wl, 0f); discG.transform.localScale = new Vector3(r, 1f, r); surfMat.color = new Color(0.6f, 0.9f, 1f, 0.42f * wa);")
rep("Explosion(t, dt); RingStep(t); Atmosphere(t, en);", "Explosion(t, dt); RingStep(t); Atmosphere(t, en); Extras(t, dt, pulse); SwimStep(t);")
rep("            if (mode == 1) { OfficeStep(t); return; }", "            if (mode != 0) AstronomicalHazards.Swim(false, Vector3.zero);\n            if (mode == 1) { OfficeStep(t); return; }")
rep("(t >= 71f && t < 80f) ? 0.9f : 0f,", "(t >= 71f && t < 80f) ? 0.9f : 0f, (t >= 55f && t < 71f) ? 0.1f + 0.5f * Sm(55f, 71f, t) : 0f,")
# office: lightning through the window
rep("            jonesM = def.Prop(\"jones\");", "            officeFlash = new GameObject(\"OfficeStormLight\").AddComponent<Light>(); officeFlash.transform.SetParent(T, false); officeFlash.transform.localPosition = new Vector3(RW / 2 - 0.6f, 1.5f, 0f); officeFlash.type = LightType.Point; officeFlash.range = 8f; officeFlash.color = new Color(0.6f, 0.5f, 1f); officeFlash.intensity = 0.5f; officeFlash.shadows = LightShadows.None;\n            jonesM = def.Prop(\"jones\");")
rep("            if (screenMat != null) screenMat.mainTextureOffset", "            if (officeFlash != null) { float f = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(t * 2.3f) * Mathf.Sin(t * 5.1f + 1f)), 6f) * 2f + ((t * 0.31f) % 1f < 0.05f ? 4f : 0f); officeFlash.intensity = (0.5f + f) * Mathf.Max(0.5f, Lit); }\n            if (screenMat != null) screenMat.mainTextureOffset")
rep("Array.Clear(pylon, 0, 5);", "Array.Clear(halos, 0, halos.Length); Array.Clear(baseRings, 0, baseRings.Length); Array.Clear(towerRings, 0, towerRings.Length); Array.Clear(tipGlow, 0, tipGlow.Length); Array.Clear(impact, 0, impact.Length); Array.Clear(alarm, 0, alarm.Length); Array.Clear(rocks, 0, rocks.Length); beam = null; discG = null; officeFlash = null; alarmLight = null;\n            Array.Clear(pylon, 0, 5);")
block = open(os.path.join(HERE, 'extras_device.cs.txt'), encoding='utf-8').read()
anchor = "        public string DebugInfo()"
rep(anchor, block + "\n" + anchor)
open(p, 'w', encoding='utf-8').write(s); print('ok')
