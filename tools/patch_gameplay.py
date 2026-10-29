import re
R = r'C:\Users\drago\Documents\AstronomicalEventMod' + '\\'
def edit(fn, pairs):
    s = open(R + fn, encoding='utf-8').read()
    for a, b in pairs:
        assert a in s, (fn, a[:70])
        s = s.replace(a, b, 1)
    open(R + fn, 'w', encoding='utf-8').write(s)

edit('AstronomicalSettings.cs', [
 ("public static ConfigEntry<bool> CameraFx,", "public static ConfigEntry<bool> Knockback, SwimInFlood;\n        public static ConfigEntry<float> KnockbackStrength;\n        public static ConfigEntry<bool> CameraFx,"),
 ("            // Spawn Location & Scale\n", "            Knockback = c.Bind(\"Gameplay\", \"Knockback\", true, \"Explosions, meteors, lightning, Travis's stomps and launch rings push you (local only, through the game's own knockback)\");\n            KnockbackStrength = c.Bind(\"Gameplay\", \"KnockbackStrength\", 1f, new ConfigDescription(\"Multiplier for all knockback\", new AcceptableValueRange<float>(0f, 2f)));\n            SwimInFlood = c.Bind(\"Gameplay\", \"SwimInFlood\", true, \"During the underwater sequence the game's own swimming is switched on while you are near the stage\");\n            // Spawn Location & Scale\n"),
])
edit('AstronomicalSpectacle.cs', [
 ("            if (go == null) return;\n            int k = exNext;", "            if (go == null) return;\n            AstronomicalHazards.Blast(p, size * 2.2f, size * 0.9f, size > 40f ? 22f : 13f);\n            int k = exNext;"),
])
edit('AstronomicalDestruction.cs', [
 ("            Emit(p, (int)(90 * dens), Fire, 15f, 6f, 1.1f);", "            AstronomicalHazards.Blast(p, radius * 2.2f, 15f);\n            Emit(p, (int)(90 * dens), Fire, 15f, 6f, 1.1f);"),
 ("            boltAge[k] = 0; boltTarget[k] = target; bolts[k].enabled = true;", "            boltAge[k] = 0; boltTarget[k] = target; bolts[k].enabled = true;\n            AstronomicalHazards.Blast(target, 9f, 8f);"),
 ("                meteors[k].transform.position += mvel[k] * dt;", "                meteors[k].transform.position += mvel[k] * dt;\n                AstronomicalHazards.Strike(meteors[k].transform.position, mvel[k], 4.5f, 15f);\n                if (marks != null && marks[k] != null) { marks[k].SetActive(true); marks[k].transform.position = new Vector3(mtarget[k].x, mtarget[k].y + 0.15f, mtarget[k].z); float ms = 6f + 1.5f * Mathf.Sin(Time.time * 14f); marks[k].transform.localScale = new Vector3(ms, 0.05f, ms); }"),
 ("                    mact[k] = false; trails[k].emitting = false; meteors[k].SetActive(false);", "                    mact[k] = false; trails[k].emitting = false; meteors[k].SetActive(false); if (marks != null && marks[k] != null) marks[k].SetActive(false);"),
 ("            meteors = new GameObject[5];", "            marks = new GameObject[5];\n            for (int k = 0; k < 5; k++)\n            {\n                var mk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.DestroyImmediate(mk.GetComponent<Collider>());\n                mk.transform.SetParent(fx, false); mk.GetComponent<Renderer>().sharedMaterial = glow; mk.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mk.SetActive(false); marks[k] = mk;   // red impact warning disc under each incoming meteor\n            }\n            meteors = new GameObject[5];"),
 ("        GameObject[] meteors;", "        GameObject[] meteors, marks;"),
])
