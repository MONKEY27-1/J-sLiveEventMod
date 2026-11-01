import re
p='AstronomicalEnd21d.cs'; s=open(p,encoding='utf-8').read()
a=s.index('        // the Cube Queen falls out of the sky into the sea'); b=s.index('        void OceanFx(')
new='''        // the Cube Queen: leaves her tower at 9:32 and hovers over the old map; when the Seven flips the island (10:03) she is thrown off it, falls 390 m into the sea, thrashes as the water takes her and sinks, a last flash at the end
        const float QT0 = 572f, QT1 = 582f, QD = T_END_VIS + 1.6f, QFALL = 7.5f, QL = QD + QFALL, QE = QL + 17f;
        static readonly Vector3 QLOC = new Vector3(0f, 60f, 0f), QSPLASH = new Vector3(30f, 0f, 250f);      // her hover point over the map (island-local) and where she hits the sea
        Vector3 IsleAnchor(float t, Vector3 lq)      // an island-local point, in root-local space, at time t (same pose as the island copy in OceanFx)
        {
            Vector3 p; float roll; IslePose(t, out p, out roll); return p + Quaternion.Euler(0f, 0f, -roll) * Quaternion.Euler(0f, (t - T_OCEAN) * 2.5f, 0f) * (lq * ISLE_S);
        }
        void QueenInWater(float t, float tp, float dt)
        {
            if (queenM == null || queenGo == null) return; bool on = t > T_OCEAN - 0.5f && t < QE + 0.5f; if (!on) { if (queenGo.activeSelf && SceneAt(t) == Sc.Ocean) queenGo.SetActive(false); return; }
            Vector3 orbQ = new Vector3(OC_PX, OC_PY + 150f * OC_S, OC_PZ), hov = IsleAnchor(Mathf.Min(t, QD), QLOC); hov.y += 1.5f * Mathf.Sin(t * 0.7f); Vector3 pos; float tilt = 0f, scale = Mathf.Lerp(140f / 1.8f, 70f / 1.8f, Sm(QT0, QT1, t)), sink = Sm(QL + 2f, QL + 15f, t);
            if (t < QD) { pos = Vector3.Lerp(orbQ, hov, Sm(QT0, QT1, t)); tilt = 4f * Mathf.Sin(t * 1.3f); }      // from the tower's orb to the map, then riding it
            else if (t < QL) { float u = (t - QD) / QFALL, e = u * u * (3f - 2f * u); Vector3 d = IsleAnchor(QD, QLOC); float g = 2f * (d.y - WY) / (QFALL * QFALL); float tau = t - QD; pos = new Vector3(Mathf.Lerp(d.x, QSPLASH.x, e), d.y - 0.5f * g * tau * tau, Mathf.Lerp(d.z, QSPLASH.z, e)); tilt = Mathf.Lerp(0f, 160f, u * u); }      // flung off the map: a ballistic fall
            else { float sgx, sgz; pos = new Vector3(QSPLASH.x, WY + SeaSample(QSPLASH.x, QSPLASH.z, t, 60f, out sgx, out sgz) * (1f - sink) - 18f * sink * sink + 0.8f * Mathf.Sin(t * 2.2f) * (1f - sink), QSPLASH.z); tilt = 160f + Mathf.Lerp(0f, 55f, sink) + 6f * Mathf.Sin(t * 3f) * (1f - sink); }
            PlaceGo(queenGo, pos, scale, Quaternion.Euler(tilt, 180f + 12f * Mathf.Sin(t * 0.8f), 8f * Mathf.Sin(t * 1.7f) * (t > QL ? sink : 0.3f)), true);
            string qc = t < QT0 ? "H_Float" : t < QD ? "H_Rage" : t < QL ? "H_Hit" : (t < QL + 8f ? "H_Hit" : "H_Rage"); float lt = t < QT0 ? t : t < QD ? (t - QT0) % 3f : t < QL ? (t - QD) % 1.6f : (t < QL + 8f ? (t - QL) % 1.6f : (t - QL - 8f) % 3f); PlayClip(queenRig, queenM, qc, lt);
            if (paused || dt <= 0f) return; Vector3 wp = W(new Vector3(pos.x, WY, pos.z));
            if (tp < QD && t >= QD) { Sfx(sThunder, W(pos), 1f, 0.7f, 0.2f); shake = Mathf.Max(shake, 1f); }
            if (tp < QL && t >= QL) { Sfx(sSplash, wp, 1f, 0.7f, 0.2f); Sfx(sBoom, wp, 0.8f, 0.8f, 0.2f); AddRipple(QSPLASH.x, QSPLASH.z, QL, 2.4f, 16f, 22f, 24f, 45f); AddRipple(QSPLASH.x, QSPLASH.z, QL + 0.4f, 1.1f, 11f, 11f, 14f, 30f); flashLamp.transform.position = wp + Vector3.up * 6f; flashLamp.color = new Color(0.7f, 0.85f, 1f); flashLamp.intensity = Lit * 18f; shake = Mathf.Max(shake, 1.5f); for (int k = 0; k < 260; k++) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(0f, 14f); Emit(fx, wp + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r), new Vector3(Mathf.Cos(a) * r * 0.5f, UnityEngine.Random.Range(8f, 34f), Mathf.Sin(a) * r * 0.5f), new Color(0.8f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.6f, 2.2f), UnityEngine.Random.Range(1.2f, 2.6f)); } }
            if (t > QL && t < QE - 2f && UnityEngine.Random.value < dt * 40f) { float a = UnityEngine.Random.value * 6.2832f, r = UnityEngine.Random.Range(2f, 11f); Emit(fx, wp + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r), new Vector3(0f, UnityEngine.Random.Range(3f, 12f), 0f), new Color(0.6f, 0.9f, 1f, 0.8f), UnityEngine.Random.Range(0.3f, 1.2f), UnityEngine.Random.Range(0.6f, 1.4f)); if (UnityEngine.Random.value < 0.4f) Emit(fx, wp + Vector3.up * 3f, UnityEngine.Random.onUnitSphere * 8f, new Color(1f, 0.6f, 0.95f, 1f), UnityEngine.Random.Range(0.3f, 0.9f), 0.8f); }
            if (tp < QE - 1f && t >= QE - 1f) { flashLamp.transform.position = wp + Vector3.up * 2f; flashLamp.color = new Color(1f, 0.7f, 0.95f); flashLamp.intensity = Lit * 25f; shake = Mathf.Max(shake, 1.8f); for (int k = 0; k < 300; k++) { var v = UnityEngine.Random.onUnitSphere; v.y = Mathf.Abs(v.y); Emit(fx, wp + Vector3.up * 1.5f, v * UnityEngine.Random.Range(6f, 36f), new Color(1f, UnityEngine.Random.Range(0.5f, 0.9f), 1f, 1f), UnityEngine.Random.Range(0.5f, 1.8f), UnityEngine.Random.Range(1f, 2.4f)); } }
        }
'''
s=s[:a]+new+s[b:]; open(p,'w',encoding='utf-8').write(s)
