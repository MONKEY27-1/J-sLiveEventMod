import re
p = 'AstronomicalEnd21.cs'
s = open(p, encoding='utf-8').read()
def rep(a, b, cnt=1):
    global s
    assert a in s, 'missing: ' + a[:90]
    s = s.replace(a, b, cnt)

# ---- retimed constants (the descriptive timeline: ring 1:35, fleet 2:05, monsters 2:40, big one 3:15, abductors 3:45, shield attacked 4:15, beam 4:20, cube dies 4:25, cut 4:30)
rep("const float T_RIFT = 105f, T_FLEET = 128f, T_MON = 120f, T_CARE = 170f, T_ABD = 225f, T_SHIELD = 255f, T_BREAK = 270f, T_CUBE_DIE = 276f, T_BLACK = 281f;",
    "const float T_RIFT = 95f, T_FLEET = 125f, T_MON = 138f, T_CARE = 195f, T_ABD = 225f, T_SHIELD = 255f, T_BREAK = 260f, T_CUBE_DIE = 265f, T_BLACK = 270f;")

# ---- the sky: dusk from 0:50, the red rift sky from the ring, black only inside the rooms, twilight then blue day in the ocean
a = s.index("        // sunset over the island; from the rift (1:45)")
b = s.index("        void BuildEffects()")
sky = '''        // sunset over the island that darkens to dusk from 0:50; from the ring (1:35) the sky bleeds red and purple; black inside the rooms; twilight and then a bright blue day (after the 10:03 flash) in the ocean
        void UpdateSky(float t)
        {
            if (skyMesh == null) return; Sc sc = SceneAt(t);
            float dusk = Sm(50f, 95f, t), rift = Sm(T_RIFT, T_RIFT + 40f, t), dark = Sm(T_ABD - 20f, T_CUBE_DIE, t) * 0.5f;
            Color top = Color.Lerp(new Color(0.22f, 0.12f, 0.38f), new Color(0.1f, 0.06f, 0.22f), dusk), mid = Color.Lerp(new Color(0.85f, 0.4f, 0.33f), new Color(0.4f, 0.16f, 0.3f), dusk), hor = Color.Lerp(new Color(1f, 0.62f, 0.3f), new Color(0.72f, 0.3f, 0.22f), dusk);
            top = Color.Lerp(top, new Color(0.25f, 0.04f, 0.14f), rift); mid = Color.Lerp(mid, new Color(0.75f, 0.2f, 0.14f), rift); hor = Color.Lerp(hor, new Color(1f, 0.38f, 0.1f), rift);
            float black = IsRoom(sc) ? 1f : 0f; if (t < T_FAC) black = Sm(T_CUBE_DIE, T_BLACK, t);
            if (sc == Sc.Ocean) { float blue = Sm(T_END_VIS - 0.2f, T_END_VIS + 1.5f, t); top = Color.Lerp(new Color(0.07f, 0.05f, 0.18f), new Color(0.2f, 0.45f, 0.95f), blue); mid = Color.Lerp(new Color(0.3f, 0.16f, 0.35f), new Color(0.5f, 0.75f, 1f), blue); hor = Color.Lerp(new Color(0.62f, 0.3f, 0.3f), new Color(0.86f, 0.94f, 1f), blue); dark = 0f; }
            for (int k = 0; k < skyDirs.Length; k++)
            {
                var d = skyDirs[k]; float up = Mathf.Clamp01(d.y); var c = up < 0.3f ? Color.Lerp(hor, mid, up / 0.3f) : Color.Lerp(mid, top, (up - 0.3f) / 0.7f);
                c = Color.Lerp(c, Color.black, dark * 0.5f); c = Color.Lerp(c, Color.black, black); c.a = 1f; skyCols[k] = c;
            }
            skyMesh.colors = skyCols;
        }

'''
s = s[:a] + sky + s[b:]

# ---- the monsters: arrival-based (the first purple monsters reach the shield at 2:46, a large one at 3:15, the last just before the cube dies)
a = s.index("                // the waves: 26 small caretakers")
b = s.index("            if (shipM != null) for (int i = 0; i < 7; i++)")
mon = '''                // the waves: 26 small caretakers arrive from 2:46 on, 6 big ones (10 m) from 3:15; they walk / run to the force field, hit it, then die
                for (int i = 0; i < 32; i++)
                {
                    bool big = i >= 26; int k = big ? i - 26 : i; float arrive = big ? 195f + k * 7.5f : 166f + k * 3.3f + Hash(i, 1) * 1.6f; float a0 = (Hash(i, 2) - 0.5f) * 2.4f; float r0 = 120f + Hash(i, 3) * 90f;
                    float x0 = Mathf.Sin(a0 * 0.9f) * r0, z0 = Mathf.Cos(a0 * 0.9f) * r0, ta = a0 * 0.45f, tx = Mathf.Sin(ta) * (SHIELD_R + 3f), tz = Mathf.Cos(ta) * (SHIELD_R + 3f); float sp = big ? 4.8f : 6.0f + Hash(i, 6) * 2.0f; float dist = Mathf.Sqrt((x0 - tx) * (x0 - tx) + (z0 - tz) * (z0 - tz));
                    float t1 = arrive, t0 = t1 - dist / sp, die = t1 + (big ? 6f : 3f + Hash(i, 9) * 2f);
                    var g = Clone(careM, "ECare" + i); mons.Add(new Mon { T0 = t0, X0 = x0, Z0 = z0, Tx = tx, Tz = tz, Speed = sp, T1 = t1, Die = die, Big = big, Scale = (big ? 10f : 6f) / 1.8f, Seed = Hash(i, 8) * 9f, Go = g, Rig = CRig.Of(careM, g) });
                }
            }
'''
s = s[:a] + mon + s[b:]

# ---- Black(): a colour (the 7:10 explosion and the 10:03 flash are white)
rep("void Black(float a, Camera cam) { if (flashGo.activeSelf != a > 0.003f) flashGo.SetActive(a > 0.003f); if (a > 0.003f) { flashMat.color = new Color(0f, 0f, 0f, a); flashGo.transform.position = cam != null ? cam.transform.position : originW; } }",
    "void Black(float a, Camera cam, Color c) { if (flashGo.activeSelf != a > 0.003f) flashGo.SetActive(a > 0.003f); if (a > 0.003f) { flashMat.color = new Color(c.r, c.g, c.b, a); flashGo.transform.position = cam != null ? cam.transform.position : originW; } }")

# ---- Apply: scene, ambient / sun per scene
rep("            if (back) { seated = false; cubeBoom = shieldBoom = false;", "            Sc sc = SceneAt(t); if (back) { seated = false; glassGone = false; cubeBoom = shieldBoom = false;")
rep("            RenderSettings.ambientLight = Color.Lerp(new Color(0.75f, 0.55f, 0.5f), new Color(0.45f, 0.2f, 0.25f), rift) * Mathf.Max(0.05f, 1f - black);\n            sun.color = Color.Lerp(new Color(1f, 0.7f, 0.45f), new Color(1f, 0.4f, 0.25f), rift); sun.intensity = 1f - black;",
    "            float ambK = t < T_FAC ? Mathf.Max(0.05f, 1f - black) : 0.55f, sunK = t < T_FAC ? 1f - black : 0.55f;\n            if (sc == Sc.Ocean) { float bl = Sm(T_END_VIS - 0.2f, T_END_VIS + 1.5f, t); ambK = Mathf.Lerp(0.4f, 1.15f, bl); sunK = Mathf.Lerp(0.3f, 1.15f, bl); }\n            RenderSettings.ambientLight = Color.Lerp(new Color(0.75f, 0.55f, 0.5f), new Color(0.45f, 0.2f, 0.25f), rift) * ambK; if (sc == Sc.Ocean && t >= T_END_VIS) RenderSettings.ambientLight = new Color(0.7f, 0.8f, 0.95f) * ambK;\n            sun.color = Color.Lerp(new Color(1f, 0.7f, 0.45f), new Color(1f, 0.4f, 0.25f), rift); if (sc == Sc.Ocean && t >= T_END_VIS) sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = sunK;")
# shield: brighter from 3:50
rep("smat.color = new Color(Mathf.Lerp(0.2f, 1f, g), Mathf.Lerp(0.9f, 0.35f, g), 1f - 0.4f * g, 0.08f + 0.3f * g); }",
    "smat.color = new Color(Mathf.Lerp(0.2f, 1f, g), Mathf.Lerp(0.9f, 0.35f, g), 1f - 0.4f * g, 0.08f + 0.06f * Sm(228f, 236f, t) + 0.3f * g); }")
rep("if (t < T_BREAK && !paused) KeepInside(t);", "if (t < T_BREAK && !paused) KeepInside(t); else if (sc == Sc.Surface && !paused) { seated = true; KeepInside(t); }\n            if (!paused && tp < 210f && t >= 210f) { flashLamp.transform.position = W(cubeC + Vector3.up * 4f); flashLamp.color = new Color(0.4f, 0.8f, 1f); flashLamp.intensity = Lit * 22f; hitGlow = 1f; shake = Mathf.Max(shake, 1f); for (int k = 0; k < 160; k++) { float a2 = k / 160f * 6.2832f; Emit(fx, W(cubeC), new Vector3(Mathf.Cos(a2), 0.15f * Mathf.Sin(k), Mathf.Sin(a2)) * UnityEngine.Random.Range(14f, 30f), new Color(0.3f, 0.9f, 1f, 1f), UnityEngine.Random.Range(0.6f, 1.6f), UnityEngine.Random.Range(1.2f, 2.4f)); } }      // 3:30: a bright blue energy burst round the cube")
# pyramid / orb / queen: not in the ocean
rep("PlaceOn(pyrM, pyrP, pyrS, 20f, t < T_END_VIS);", "PlaceOn(pyrM, pyrP, pyrS, 20f, t < T_END_VIS && sc != Sc.Ocean);")
rep("bool orbOn = t > T_RIFT - 8f && t < T_END_VIS;", "bool orbOn = t > T_RIFT - 8f && t < T_END_VIS && sc != Sc.Ocean;")
rep("bool qOn = queenM != null && t > T_RIFT - 8f && t < T_END_VIS;", "bool qOn = queenM != null && t > T_RIFT - 8f && t < T_END_VIS && sc != Sc.Ocean;")
# the rift ring from 1:35, the beam from 1:30 (vertical first), the lightning from 1:25
rep("bool riftOn = t > T_RIFT && t < T_END_VIS; riftRing.enabled = riftRing2.enabled = mainBeam.enabled = riftOn; if (riftDisc.gameObject.activeSelf != riftOn) riftDisc.gameObject.SetActive(riftOn);",
    "bool riftOn = t > T_RIFT && t < T_END_VIS; bool beamPre = t > 90f && t <= T_RIFT && sc == Sc.Island; riftRing.enabled = riftRing2.enabled = riftOn; mainBeam.enabled = (riftOn && sc != Sc.Ocean) || beamPre; if (riftDisc.gameObject.activeSelf != riftOn) riftDisc.gameObject.SetActive(riftOn);")
rep("            else bolt.enabled = false;\n            // ----- the Cube Queen: floats above her pyramid",
    "            else if (beamPre) { mainBeam.SetPosition(0, W(orbP)); mainBeam.SetPosition(1, W(orbP + Vector3.up * 1800f)); mainBeam.widthMultiplier = 22f * Sm(90f, 94f, t) * (1f + 0.1f * Mathf.Sin(t * 14f)); bolt.enabled = false; }\n            else bolt.enabled = false;\n            if (t > 85f && t < T_RIFT && sc == Sc.Island && !paused && t - lastBolt > 0.55f + 0.5f * Hash((int)(t * 3f), 4)) { lastBolt = t; bolt.enabled = true; Vector3 a0 = pyrP + new Vector3(UnityEngine.Random.Range(-260f, 260f), 1100f, UnityEngine.Random.Range(-260f, 260f)), b0 = pyrP + new Vector3(UnityEngine.Random.Range(-120f, 120f), 0f, UnityEngine.Random.Range(-120f, 120f)); for (int k = 0; k < 12; k++) bolt.SetPosition(k, W(Vector3.Lerp(a0, b0, k / 11f) + (k > 0 && k < 11 ? UnityEngine.Random.insideUnitSphere * 40f : Vector3.zero))); flashLamp.transform.position = W(b0 + Vector3.up * 40f); flashLamp.color = new Color(0.8f, 0.85f, 1f); flashLamp.intensity = Lit * 14f; }\n            // ----- the Cube Queen: floats above her pyramid")
rep("mainBeam.SetPosition(0, W(orbP)); mainBeam.SetPosition(1, cp); mainBeam.widthMultiplier = 26f * rift", "mainBeam.SetPosition(0, W(orbP)); mainBeam.SetPosition(1, cp); mainBeam.widthMultiplier = 26f * rift")
# the cradle / ships: hidden in the rooms, the first ship leaves at 7:04
rep("bool crOn = cradleM != null && t > T_FLEET - 4f && t < T_END_VIS;", "bool crOn = cradleM != null && t > T_FLEET - 4f && t < T_END_VIS && !IsRoom(sc);")
rep("float arrive = T_FLEET + i * 1.6f; float k = Sm(arrive, arrive + 9f, t); bool on = t > arrive && t < T_END_VIS;", "float arrive = T_FLEET + i * 1.6f; float k = Sm(arrive, arrive + 9f, t); bool on = t > arrive && t < T_END_VIS && !IsRoom(sc);")
rep("Vector3 pos = Vector3.Lerp(riftC, hover, k) + new Vector3(0f, 0f, 0f);", "Vector3 pos = Vector3.Lerp(riftC, hover, k) + (i == 0 ? Vector3.up * 1500f * Mathf.Pow(Sm(424f, 430f, t), 2f) : Vector3.zero);")
# abductors: visible in the fleet from 2:20, close in at 3:45, attack the shield 4:15 - 4:20
a = s.index("            for (int i = 0; i < abds.Count; i++)\n            {\n                float arrive = T_ABD + i * 1.4f;")
b = s.index("            // ----- after the cube is gone: black until the next milestone")
abd = '''            for (int i = 0; i < abds.Count; i++)
            {
                float arrive = 140f + i * 1.4f; float far = Sm(arrive, arrive + 10f, t), near = Sm(T_ABD + i * 1.0f, T_ABD + i * 1.0f + 8f, t); bool on = t > arrive && t < T_END_VIS && !IsRoom(sc); float ang = t * 0.35f + i * 0.63f; float rad = Mathf.Lerp(Mathf.Lerp(950f, 420f, far), 85f + 12f * (i % 3), near);
                Vector3 pos = cubeC + new Vector3(Mathf.Cos(ang) * rad, Mathf.Lerp(Mathf.Lerp(330f, 200f, far), 55f + 8f * (i % 4), near), Mathf.Sin(ang) * rad * 0.8f - 20f);
                PlaceGo(abds[i], pos, 0.1f * Mathf.Lerp(1.9f, 1f, near), Quaternion.Euler(Mathf.Sin(t + i) * 6f, -ang * Mathf.Rad2Deg, Mathf.Cos(t * 0.8f + i) * 6f), on);
                bool fire = on && t > T_SHIELD - 1f + i * 0.3f && t < T_BREAK + 6f; if (beams[i].enabled != fire) beams[i].enabled = fire;
                if (fire) { Vector3 a = W(pos + Vector3.down * 3f), b = W(cubeC + (pos - cubeC).normalized * 14f); beams[i].SetPosition(0, a); beams[i].SetPosition(1, b); beams[i].widthMultiplier = 2.5f + 1.5f * Mathf.Sin(t * 22f + i); }
                if (fire && !paused && dt > 0f && UnityEngine.Random.value < dt * 10f) Emit(fx, W(cubeC + (pos - cubeC).normalized * 14f), UnityEngine.Random.insideUnitSphere * 8f, new Color(1f, 0.5f, 0.95f, 1f), UnityEngine.Random.Range(0.8f, 2.2f), 0.6f);
            }
'''
s = s[:a] + abd + s[b:]
rep("float pb = P2Black(t); if (pb >= 0f) black = pb; P2Frame(t, dt, tp, cam);\n            Black(black, cam);", "Color bcol; float pb = P2Black(t, out bcol); if (pb >= 0f) black = pb; else bcol = Color.black; P2Frame(t, dt, tp, cam);\n            Black(black, cam, bcol);")
rep("        const float SHIELD_R = 32f;", "        const float SHIELD_R = 32f;")
open(p, 'w', encoding='utf-8').write(s)
print('ok')
