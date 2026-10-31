d = open('AstronomicalEnd21d.cs', encoding='utf-8').read()
def rep(a, n):
    global d
    assert a in d, a[:90]
    d = d.replace(a, n, 1)
rep("const float WY = -0.3f, ISLE_Z = 760f, ISLE_R = 258f, ISLE_D = 70f;", "const float WY = -0.3f, ISLE_Z = 520f, ISLE_R = 258f, ISLE_D = 150f, ISLE_S = 1.45f;      // the old island: radius 258 m, a 150 m crystal rock slab under it, shown 1.45x bigger and 520 m away so it towers over you (reference screenshot)")
rock = '''
        // the rock slab under the island: layered strata, big crystal facets (flat shaded: every face has its own three vertices) and glowing crystal spikes; reference: a vertical slab of blue-violet crystal rock with moss on the rim
        Mesh MakeRock(float R, float D, float rBot, int rings, int segs, int seed, out Mesh spikes)
        {
            var pts = new Vector3[rings + 1, segs]; float so = seed * 3.7f;
            for (int i = 0; i <= rings; i++)
            {
                float f = i / (float)rings, baseR = Mathf.Lerp(R, rBot, Mathf.Pow(f, 0.62f));
                for (int j = 0; j < segs; j++)
                {
                    float a = j * Mathf.PI * 2f / segs; float n1 = Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + so, Mathf.Sin(a) * 1.6f + f * 2.2f) * 2f - 1f, n2 = Mathf.PerlinNoise(Mathf.Cos(a) * 4.2f + 9f + so, Mathf.Sin(a) * 4.2f + f * 5.5f) * 2f - 1f;
                    float terr = 1f + 0.05f * Mathf.Sin(f * 38f + n1 * 3f), env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, f * 1.15f)); float rr = i == 0 ? R : baseR * terr * (1f + 0.20f * n1 * env + 0.09f * n2 * env);
                    pts[i, j] = new Vector3(Mathf.Cos(a) * rr, -2f - D * f + (i == 0 ? 0f : 9f * n2 * env), Mathf.Sin(a) * rr);
                }
            }
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
            Action<Vector3, Vector3, Vector3, Vector2, Vector2, Vector2> tri = (p0, p1, p2, u0, u1, u2) =>
            {
                Vector3 nrm = Vector3.Cross(p1 - p0, p2 - p0); Vector3 c = (p0 + p1 + p2) / 3f; Vector3 outd = new Vector3(c.x, 0f, c.z); if (Vector3.Dot(nrm, outd) < 0f) { var t = p1; p1 = p2; p2 = t; var tu = u1; u1 = u2; u2 = tu; }
                int b = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); uv.Add(u0); uv.Add(u1); uv.Add(u2); tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
            };
            for (int i = 0; i < rings; i++) for (int j = 0; j < segs; j++)
            {
                int j2 = (j + 1) % segs; Vector3 a = pts[i, j], b = pts[i, j2], c = pts[i + 1, j], e = pts[i + 1, j2]; float u0 = j / (float)segs * 5f, u1 = (j + 1) / (float)segs * 5f, v0 = i / (float)rings * 3f, v1 = (i + 1) / (float)rings * 3f;
                if (((i * 7 + j * 3) & 1) == 0) { tri(a, c, b, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v0)); tri(b, c, e, new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1)); }
                else { tri(a, c, e, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1)); tri(a, e, b, new Vector2(u0, v0), new Vector2(u1, v1), new Vector2(u1, v0)); }
            }
            Vector3 tip = new Vector3(0f, -2f - D - 10f, 0f); for (int j = 0; j < segs; j++) tri(pts[rings, j], pts[rings, (j + 1) % segs], tip, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1f));
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m);
            // glowing crystal spikes jutting from the face
            var sv = new List<Vector3>(); var sc = new List<Color>(); var st = new List<int>(); var rnd = new System.Random(seed + 5);
            for (int k = 0; k < 90; k++)
            {
                int i = rnd.Next(2, rings), j = rnd.Next(segs); Vector3 bp = pts[i, j]; Vector3 outd = new Vector3(bp.x, 0f, bp.z).normalized; Vector3 dir = (outd * 0.9f + new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() * 0.9f - 0.1f, (float)rnd.NextDouble() - 0.5f) * 0.7f).normalized;
                float len = 10f + 34f * (float)rnd.NextDouble() * (float)rnd.NextDouble(), wd = len * (0.10f + 0.07f * (float)rnd.NextDouble()); Vector3 s1 = Vector3.Cross(dir, Vector3.up).normalized, s2 = Vector3.Cross(dir, s1);
                Color cb = Color.Lerp(new Color(0.25f, 0.45f, 1f, 0.95f), new Color(0.55f, 0.95f, 1f, 0.95f), (float)rnd.NextDouble()), ct = new Color(0.9f, 1f, 1f, 1f); int b0 = sv.Count;
                sv.Add(bp + s1 * wd); sv.Add(bp + s2 * wd); sv.Add(bp - s1 * wd); sv.Add(bp - s2 * wd); sv.Add(bp + dir * len); sc.Add(cb); sc.Add(cb * 0.8f); sc.Add(cb); sc.Add(cb * 0.8f); sc.Add(ct);
                for (int q = 0; q < 4; q++) { st.Add(b0 + q); st.Add(b0 + (q + 1) % 4); st.Add(b0 + 4); }
            }
            spikes = new Mesh(); spikes.SetVertices(sv); spikes.SetColors(sc); spikes.SetTriangles(st, 0); spikes.RecalculateBounds(); owned.Add(spikes); return m;
        }
        Texture2D MakeRockTex()
        {
            return kit.Tex(128, 128, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.07f + 4f, y * 0.07f) * 0.6f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f + 8f) * 0.4f, band = 0.5f + 0.5f * Mathf.Sin(y * 0.33f + n * 4f);
                Color c = Color.Lerp(new Color(0.2f, 0.17f, 0.36f), new Color(0.38f, 0.52f, 0.9f), Mathf.Clamp01(n * 1.2f - 0.2f + 0.25f * band)); float vein = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(x * 0.19f + n * 6f)) * 5f), 2f) * (n > 0.55f ? 1f : 0f); c = Color.Lerp(c, new Color(0.6f, 1f, 1f), vein * 0.7f); c.a = 1f; return c;
            });
        }
'''
rep("        void SetupOcean()", rock + "        void SetupOcean()")
rep("coneMat = new Material(kit.Flat(new Color(0.46f, 0.37f, 0.28f))); coneMat.mainTexture = rockTex; try { coneMat.SetFloat(\"_Cull\", 0f); } catch { } owned.Add(coneMat);", "coneMat = kit.Lit(Color.white, MakeRockTex(), 0.55f);")
rep("isleCone.AddComponent<MeshFilter>().sharedMesh = MakeCone(ISLE_R, ISLE_D, 50f, 9, 44, 7);", "Mesh spk; isleCone.AddComponent<MeshFilter>().sharedMesh = MakeRock(ISLE_R, ISLE_D, 70f, 22, 72, 7, out spk); var spo = new GameObject(\"EIsleSpikes\"); spo.transform.SetParent(isle.transform, false); spo.AddComponent<MeshFilter>().sharedMesh = spk; var spr = spo.AddComponent<MeshRenderer>(); spr.sharedMaterial = kit.Glow(Color.white, null, 3004); spr.shadowCastingMode = ShadowCastingMode.Off;")
rep("float sc = 1.9f; p.transform.localScale = Vector3.one * sc;", "float sc = 1.9f / ISLE_S; p.transform.localScale = Vector3.one * sc;")
rep("p.transform.localPosition = new Vector3(0f, -48f, 0f); c3T = p.transform;", "p.transform.localPosition = new Vector3(0f, -ISLE_D + 14f, 0f); c3T = p.transform;")
rep("            else { roll = 90f + 90f * Sm(T_END_VIS + 1.5f, T_END_VIS + 25f, t); h = Mathf.Lerp(262f, -75f, Sm(T_END_VIS + 2.5f, T_END_VIS + 52f, t)); }\n            float z = ISLE_Z - 330f * Sm(T_END_VIS + 18f, T_END_VIS + 50f, t);",
    "            else { roll = 90f + 90f * Sm(T_END_VIS + 1.5f, T_END_VIS + 25f, t); h = Mathf.Lerp(ISLE_R * ISLE_S + 12f, 38f - ISLE_S * ISLE_D, Sm(T_END_VIS + 2.5f, T_END_VIS + 52f, t)); }\n            float z = ISLE_Z - 300f * Sm(T_END_VIS + 18f, T_END_VIS + 50f, t);")
rep("isle.transform.position = W(p); isle.transform.rotation", "isle.transform.localScale = Vector3.one * ISLE_S; isle.transform.position = W(p); isle.transform.rotation")
rep("AddRipple(0f, ISLE_Z - 330f, T_END_VIS + 30f,", "AddRipple(0f, ISLE_Z - 300f, T_END_VIS + 30f,")
rep("            QueenInWater(t, tp, dt);\n            // underwater", """            if (isle != null && isle.activeSelf && t > T_END_VIS && t < T_END_VIS + 52f && !paused && dt > 0f)      // water streaming off the rock slab and the rim
            {
                int nn = (int)(220f * dt * Parts) + 1;
                for (int k = 0; k < nn; k++) { float a = UnityEngine.Random.value * 6.2832f, rr = UnityEngine.Random.Range(0.55f, 1f) * ISLE_R, dep = UnityEngine.Random.value; Vector3 lp = new Vector3(Mathf.Cos(a) * rr * (1f - 0.6f * dep), -2f - ISLE_D * dep, Mathf.Sin(a) * rr * (1f - 0.6f * dep)); Vector3 wp = isle.transform.TransformPoint(lp); if (wp.y < root.transform.position.y + WY + 3f) continue; Emit(fx, wp, new Vector3(UnityEngine.Random.Range(-1f, 1f), -UnityEngine.Random.Range(6f, 22f), UnityEngine.Random.Range(-1f, 1f)), new Color(0.85f, 0.95f, 1f, 0.7f), UnityEngine.Random.Range(0.8f, 3.2f), UnityEngine.Random.Range(1.2f, 2.6f)); }
            }
            QueenInWater(t, tp, dt);
            // underwater""")
open('AstronomicalEnd21d.cs', 'w', encoding='utf-8').write(d)
print('ok')
