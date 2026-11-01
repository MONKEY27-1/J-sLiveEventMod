m = open('AstronomicalEnd21.cs', encoding='utf-8').read()
b = open('AstronomicalEnd21b.cs', encoding='utf-8').read()
c = open('AstronomicalEnd21c.cs', encoding='utf-8').read()
d = open('AstronomicalEnd21d.cs', encoding='utf-8').read()
e = open('AstronomicalEnd21e.cs', encoding='utf-8').read()
f = open('AstronomicalEnd21f.cs', encoding='utf-8').read()
def rep(s, a, n, cnt=1):
    assert a in s, 'missing: ' + a[:100]
    return s.replace(a, n, cnt)

# ---------------- sound: init / cleanup / hooks
m = rep(m, "BuildSky(); BuildEffects(); BuildModels(); P2Start();", "BuildSky(); BuildEffects(); SfxInit(); BuildModels(); P2Start();")
m = rep(m, "try { P2Cleanup(); } catch { }", "try { P2Cleanup(); } catch { } SfxCleanup();")
# explosions: every Boom has a voice (size picks the clip, distance does the rest)
m = rep(m, "            Color a = tint ?? new Color(1f, 0.55f, 0.15f);\n", "            Color a = tint ?? new Color(1f, 0.55f, 0.15f);\n            Sfx(size > 14f ? sBig : sBoom, wp, Mathf.Clamp(0.22f + size / 22f, 0.22f, 1f), Mathf.Clamp(1.25f - size * 0.025f, 0.55f, 1.2f), size > 14f ? 0.25f : 0.06f);\n")
# actors fire: the rifles shoot with sound
b = rep(b, "            for (int q = 0; q < 6; q++) Emit(fx, mz, a.Gun.rotation", "            Sfx(sShot, mz, 0.3f, 1f, 0.07f);\n            for (int q = 0; q < 6; q++) Emit(fx, mz, a.Gun.rotation")
# the player's own weapon
e = rep(e, "bool hitSomething = bi >= 0 || ba != null; Vector3 hit = o + d * (hitSomething ? best : 90f); AddBolt(o + d * 0.1f, hit);", "bool hitSomething = bi >= 0 || ba != null; Vector3 hit = o + d * (hitSomething ? best : 90f); AddBolt(o + d * 0.1f, hit); Sfx(sZap, o, 0.45f, 1f, 0.06f);")
# lightning strikes: thunder from where it hits
f = rep(f, "b.T0 = t; b.Life = UnityEngine.Random.Range(0.18f, 0.5f); b.On = true;", "b.T0 = t; b.Life = UnityEngine.Random.Range(0.18f, 0.5f); b.On = true; Sfx(sThunder, e, oc ? 0.7f : 0.45f, UnityEngine.Random.Range(0.8f, 1.15f), 0.4f);")
# glass: the Caretaker's slam (9:05), the impact (9:07), the burst (9:10)
c = rep(c, "if (at(547.05f)) { Boom(top, 26f, 1.2f, new Color(0.4f, 0.8f, 1f));", "if (at(545.2f)) Sfx(sBig, top, 0.8f, 0.8f); if (at(547.05f)) { Sfx(sGlass, top, 1f, 0.9f, 0.1f); Boom(top, 26f, 1.2f, new Color(0.4f, 0.8f, 1f));")
c = rep(c, "if (at(T_FLOOD)) { glassGone = true;", "if (at(T_FLOOD)) { Sfx(sGlass, top, 1f, 0.75f, 0.1f); Sfx(sRumble, HeadNow(), 0.9f, 1f, 0.5f); glassGone = true;")
# water: the Queen hits the sea; the island rises (the flash), rolls, and sinks
d = rep(d, "if (tp < QT1 && t >= QT1) { AddRipple(22f, 85f, QT1, 2.4f, 16f, 22f, 24f, 45f);", "if (tp < QT1 && t >= QT1) { Sfx(sSplash, wp, 1f, 0.7f, 0.2f); Sfx(sBoom, wp, 0.8f, 0.8f, 0.2f); AddRipple(22f, 85f, QT1, 2.4f, 16f, 22f, 24f, 45f);")
d = rep(d, "if (!paused && tp < T_END_VIS && t >= T_END_VIS) {", "if (!paused && tp < T_END_VIS && t >= T_END_VIS) { Sfx(sThunder, W(new Vector3(0f, 200f, ISLE_Z)), 1f, 0.8f, 0.5f); Sfx(sBig, W(new Vector3(0f, 100f, ISLE_Z)), 0.9f, 0.7f, 0.5f);")
d = rep(d, "if (!paused && tp < T_END_VIS + 2.5f && t >= T_END_VIS + 2.5f) shake = Mathf.Max(shake, 1.2f);", "if (!paused && tp < T_END_VIS + 2.5f && t >= T_END_VIS + 2.5f) { shake = Mathf.Max(shake, 1.2f); Sfx(sRumble, W(new Vector3(0f, 150f, ISLE_Z)), 1f, 0.85f, 1f); Sfx(sSplash, W(new Vector3(0f, 3f, ISLE_Z - 200f)), 1f, 0.6f, 0.5f); }\n            if (!paused && tp < T_END_VIS + 26f && t >= T_END_VIS + 26f) { Sfx(sSplash, W(new Vector3(0f, 3f, ISLE_Z)), 1f, 0.55f, 0.5f); Sfx(sBig, W(new Vector3(0f, 20f, ISLE_Z)), 0.8f, 0.8f, 0.5f); }")
# beams on the hatch cover: a zap per lit beam
c = rep(c, "beams[i].SetPosition(0, abds[i].transform.position); beams[i].SetPosition(1, tgt);", "beams[i].SetPosition(0, abds[i].transform.position); beams[i].SetPosition(1, tgt); if (UnityEngine.Random.value < dt * 5f) Sfx(sZap, tgt, 0.5f, 0.6f, 0.12f);")

# ---------------- rooms: alarm lights after the breach, flicker when the base shakes, animated screens
b = rep(b, "case Sc.Fac: p1 = new Vector3(0f, Y_FAC + 5.5f, 0f); p2 = new Vector3(0f, Y_FAC + 6.2f, 4f); i1 = 2.2f + 0.5f * Mathf.Sin(t * 5f); i2 = 1.8f; amb = new Color(0.45f, 0.3f, 0.3f); break;",
        "case Sc.Fac: { p1 = new Vector3(0f, Y_FAC + 5.5f, 0f); p2 = new Vector3(0f, Y_FAC + 6.2f, 4f); i1 = 2.2f + 0.5f * Mathf.Sin(t * 5f); i2 = 1.8f; amb = new Color(0.45f, 0.3f, 0.3f); if (t > 295.9f) { float al = Mathf.Max(0f, Mathf.Sin(t * 7f)); i1 = 1.6f + 2.4f * al; i2 = 1.2f + 0.6f * (1f - al); lc1 = new Color(1f, 0.12f, 0.08f); amb = Color.Lerp(amb, new Color(0.6f, 0.15f, 0.12f), al); } break; }")
b = rep(b, "            facLight.transform.position = W(p1); facLight2.transform.position = W(p2);", "            if (IsRoom(sc) && shake > 0.4f) { float fl = Mathf.PerlinNoise(t * 24f, 3f) > 0.4f ? 1f : 0.2f; i1 *= fl; i2 *= fl; }      // the lights stutter when the place shakes\n            ScreensFrame(t, sc);\n            facLight.transform.position = W(p1); facLight2.transform.position = W(p2);")
b = rep(b, "        Vector3 hallLight; float baseGyro = 1f;", """        // the monitors: screens scroll (text lines roll, the vitals trace sweeps) - collected from the room models' materials by name at load
        readonly List<Material> screenMats = new List<Material>(), ecgMats = new List<Material>();
        void CollectScreens(GltfModel mdl)
        {
            if (mdl == null) return;
            foreach (var kv in origMats)
            {
                if (kv.Key == null || !kv.Key.transform.IsChildOf(mdl.Pivot.transform)) continue; var now = kv.Key.sharedMaterials;
                for (int i = 0; i < kv.Value.Length && i < now.Length; i++)
                {
                    string nm = kv.Value[i] != null ? kv.Value[i].name : ""; if (nm == "ECG") { if (!ecgMats.Contains(now[i])) ecgMats.Add(now[i]); }
                    else if (nm == "Green" || nm == "Blue" || nm == "Orange" || nm == "ScrG" || nm == "ScrB" || nm == "ScrO" || nm == "ScrR") { if (!screenMats.Contains(now[i])) screenMats.Add(now[i]); }
                }
            }
        }
        void ScreensFrame(float t, Sc sc)
        {
            if (!IsRoom(sc)) return; for (int i = 0; i < screenMats.Count; i++) if (screenMats[i] != null) screenMats[i].mainTextureOffset = new Vector2(0f, Mathf.Repeat(t * (0.08f + 0.03f * (i % 4)), 1f));
            for (int i = 0; i < ecgMats.Count; i++) if (ecgMats[i] != null) ecgMats[i].mainTextureOffset = new Vector2(Mathf.Repeat(t * 0.5f, 1f), 0f);
        }
        Vector3 hallLight; float baseGyro = 1f;""")
b = rep(b, "giant = null; giantGo = null; giantLight = null; p2Ready = false;", "giant = null; giantGo = null; giantLight = null; screenMats.Clear(); ecgMats.Clear(); p2Ready = false;")
c = rep(c, "            if (zpM2 != null) zpR2 = CRig.Of(zpM2, zpM2.Pivot);\n            // ================= the I.O. facility", "            if (zpM2 != null) zpR2 = CRig.Of(zpM2, zpM2.Pivot);\n            CollectScreens(facM); CollectScreens(hallM); CollectScreens(gyroM); CollectScreens(baseM);\n            // ================= the I.O. facility")
open('AstronomicalEnd21.cs', 'w', encoding='utf-8').write(m); open('AstronomicalEnd21b.cs', 'w', encoding='utf-8').write(b); open('AstronomicalEnd21c.cs', 'w', encoding='utf-8').write(c)
open('AstronomicalEnd21d.cs', 'w', encoding='utf-8').write(d); open('AstronomicalEnd21e.cs', 'w', encoding='utf-8').write(e); open('AstronomicalEnd21f.cs', 'w', encoding='utf-8').write(f)
print('ok')
