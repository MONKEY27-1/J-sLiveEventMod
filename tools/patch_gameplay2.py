R = r'C:\Users\drago\Documents\AstronomicalEventMod' + '\\'
def edit(fn, pairs):
    s = open(R + fn, encoding='utf-8').read()
    for a, b in pairs:
        assert a in s, (fn, a[:80])
        s = s.replace(a, b, 1)
    open(R + fn, 'w', encoding='utf-8').write(s)

edit('AstronomicalEffectsManager.cs', [
 ("        Transform seatOrb; Material seatMat; Transform pelvisJoint;",
  "        Transform seatOrb; Material seatMat; Transform pelvisJoint;\n        AstronomicalLaunchRings rings; float[] shockT, shockSize; int shockIdx;"),
 ("            stage.SetActive(false); planets.SetActive(false);\n        }",
  "            try { rings = new AstronomicalLaunchRings(); rings.Build(root.transform, anchorPos, ringMats[0]); }\n            catch (Exception ex) { AstronomicalEventMod.Log.LogWarning(\"Launch rings failed: \" + ex.Message); rings = null; }\n            stage.SetActive(false); planets.SetActive(false);\n        }"),
 ("            if (spec != null) spec.Mega(c, hue);", "            if (spec != null) spec.Mega(c, hue);\n            AstronomicalHazards.Blast(c, 170f, 45f, 22f);"),
 ("            Color c1 = Col(hue, 0.8f, 1f), c2 = Col(hue + 0.33f, 0.8f, 1f), c3 = Col(hue + 0.66f, 0.7f, 1f);",
  "            Color c1 = Col(hue, 0.8f, 1f), c2 = Col(hue + 0.33f, 0.8f, 1f), c3 = Col(hue + 0.66f, 0.7f, 1f);\n            if (!paused) AstronomicalHazards.Swim(water > 0.3f, root.transform.position);      // the game's own swimming while the arena is flooded\n            if (rings != null && !paused) rings.Tick(dt, t, i, kick, hue);"),
 # shock triggers from the move log
 ("                seatT0 = a0.ToArray(); seatT1 = a1.ToArray();",
  "                seatT0 = a0.ToArray(); seatT1 = a1.ToArray();\n                var st = new List<float>(); var ss = new List<float>();     // footfalls that shake the ground: where each stomp / jump / slam lands inside its move\n                for (int k = 0; k < n; k++)\n                {\n                    string nm = moveName[k]; float f = nm == \"stomp\" ? 0.5f : nm == \"jump\" ? 0.66f : nm == \"fin_slam\" ? 0.66f : -1f;\n                    if (f < 0) continue; st.Add(moveT0[k] + f * (moveT1[k] - moveT0[k])); ss.Add(nm == \"fin_slam\" ? 1.6f : nm == \"jump\" ? 0.7f : 0.5f);\n                }\n                shockT = st.ToArray(); shockSize = ss.ToArray(); shockIdx = 0;"),
 ("                TickMoveLog(tc);\n",
  "                TickMoveLog(tc);\n                if (shockT != null)\n                {\n                    if (shockIdx > 0 && shockIdx <= shockT.Length && tc < shockT[shockIdx - 1] - 0.3f) shockIdx = 0;          // seek backwards\n                    while (shockIdx < shockT.Length && tc >= shockT[shockIdx])\n                    {\n                        if (visible && !paused && tc - shockT[shockIdx] < 0.5f)\n                        {\n                            Vector3 foot = performerRoot.transform.position; float sz = shockSize[shockIdx];\n                            AstronomicalHazards.Blast(foot, perfH * (1.2f + sz), 5f + perfH * 0.35f * sz, 11f);\n                            if (spec != null) spec.Boom(foot, Mathf.Min(14f, perfH * 0.35f * sz + 2f), hue);\n                        }\n                        shockIdx++;\n                    }\n                }\n"),
 ("            if (spec != null) { try { spec.Dispose(); }", "            if (rings != null) { try { rings.Dispose(); } catch { } rings = null; }\n            AstronomicalHazards.Reset(); shockT = null; shockSize = null; shockIdx = 0;\n            if (spec != null) { try { spec.Dispose(); }"),
])
edit('AstronomicalDestruction.cs', [
 ("            if (flash != null) flash.intensity = 0; flashI = 0;", "            if (marks != null) foreach (var mk in marks) if (mk != null) mk.SetActive(false);\n            if (flash != null) flash.intensity = 0; flashI = 0;"),
 ("mk.GetComponent<Renderer>().sharedMaterial = glow;", "var mkM = new Material(glow) { color = new Color(1f, 0.1f, 0.05f, 0.55f) }; ownedMats.Add(mkM); mk.GetComponent<Renderer>().sharedMaterial = mkM;"),
])
edit('AstronomicalEventApi.cs', [
 ("        public static bool CameraEffects {", "        /// <summary>Explosions, meteors, lightning, Travis's stomps and launch rings push the local player (default on).</summary>\n        public static bool Knockback { get { return AstronomicalSettings.Knockback.Value; } set { AstronomicalSettings.Knockback.Value = value; } }\n        public static float KnockbackStrength { get { return AstronomicalSettings.KnockbackStrength.Value; } set { AstronomicalSettings.KnockbackStrength.Value = value; } }\n        /// <summary>Use the game's own swimming during the flood sequence (default on).</summary>\n        public static bool SwimInFlood { get { return AstronomicalSettings.SwimInFlood.Value; } set { AstronomicalSettings.SwimInFlood.Value = value; } }\n        public static bool CameraEffects {"),
])
edit('AstronomicalUI.cs', [
 ("            AstronomicalSettings.CameraFx.Value = GUILayout.Toggle(", "            AstronomicalSettings.Knockback.Value = GUILayout.Toggle(AstronomicalSettings.Knockback.Value, \" Knockback (explosions, meteors, stomps, rings)\");\n            AstronomicalSettings.KnockbackStrength.Value = Slider(\"Knockback strength\", AstronomicalSettings.KnockbackStrength.Value, 0, 2);\n            AstronomicalSettings.SwimInFlood.Value = GUILayout.Toggle(AstronomicalSettings.SwimInFlood.Value, \" Swim in the flood (game's own swimming)\");\n            AstronomicalSettings.CameraFx.Value = GUILayout.Toggle("),
])
print('ok')
