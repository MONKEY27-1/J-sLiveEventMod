"""one-off: wires the 'extras' (tools/extras_block.cs.txt) into AstronomicalConcert.cs"""
import os
HERE = os.path.dirname(os.path.abspath(__file__)); p = os.path.join(os.path.dirname(HERE), 'AstronomicalConcert.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:70]
    s = s.replace(a, b)
rep("        Light[] lights = new Light[8]; Light sun;", """        // extras: floor shockwaves, halos round the giant, searchlights, light pillars, ground fog, drifting orbs, equaliser bars, fireworks
        sealed class Firework { public float T0, Rise; public Vector3 A, B; public Color C; public bool Burst; public int Kind; }
        readonly List<Firework> fireworks = new List<Firework>(); LineRenderer[] waves = new LineRenderer[4], halos = new LineRenderer[3], search = new LineRenderer[8], pillars = new LineRenderer[6]; float[] waveT = { -99f, -99f, -99f, -99f }; Color[] waveC = new Color[4]; int waveNext;
        Vector3[] searchO = new Vector3[8], pillO = new Vector3[6]; Transform[] bars = new Transform[38]; Material[] barMat = new Material[3]; ParticleSystem fog, orbs; float floorY, accFog, accOrb; Vector3 waveAt;
        Light[] lights = new Light[8]; Light sun;""")
rep("foreach (var ps in new[] { fx, flame, confetti })", "foreach (var ps in new[] { fx, flame, confetti, fog, orbs })")
rep("if (back || lastT < 0f) { hitIdx = dropIdx = 0; lastBeat = -1; skyT = -9f; }", "if (back || lastT < 0f) { hitIdx = dropIdx = 0; lastBeat = -1; skyT = -9f; fireworks.Clear(); }")
rep("if (bi != lastBeat) { lastBeat = bi; if (!paused && en > 0.22f && bi % 4 == 0) Confetti(30, 0.7f); }",
    "if (bi != lastBeat) { lastBeat = bi; if (!paused && en > 0.22f && bi % 4 == 0) Confetti(30, 0.7f); if (!paused && en > 0.3f && bi % 8 == 0) Wave(t); if (!paused && t > endT - 22f && bi % 3 == 0) Firework(t, bi & 1); }")
rep("Dancers(t, dt, pulse); Splashes(t);", "Dancers(t, dt, pulse); Splashes(t); Extras(t, dt, bi, pulse, en);")
rep("            curSong = s; song = Mathf.Min(s, 5); flashV = 1f; screenFlash = 1f; skyT = -9f;", "            curSong = s; song = Mathf.Min(s, 5); flashV = 1f; screenFlash = 1f; skyT = -9f; if (!paused && t > 1f) { Wave(t); for (int k = 0; k < 3; k++) Firework(t + 0.3f * k, k & 1); }")
rep("            lightKick = Mathf.Max(lightKick, 0.6f + power * 0.4f);\n        }", """            if (power > 0.5f) for (int s = 0; s < 8; s++) for (int k = 0; k < (int)(10 * Parts); k++) Emit(fx, flameSpots[s], new Vector3(Rnd(-6f, 6f), Rnd(14f, 26f), Rnd(-8f, 3f)), new Color(1f, 0.9f, 0.55f), Rnd(0.3f, 0.6f), Rnd(1f, 1.8f));        // cold sparks
            lightKick = Mathf.Max(lightKick, 0.6f + power * 0.4f);
        }""")
rep("            if (s > 0.18f) Splat(s, t);\n", "            if (s > 0.18f) Splat(s, t);\n            if (s > 0.25f) Wave(t); if (s > 0.55f) Firework(t, 0);\n")
rep("for (int k = 0; k < 4; k++) Splat(1f, t); flashV = 1f; screenFlash = 1f; lightKick = 1.5f;", "for (int k = 0; k < 4; k++) Splat(1f, t); flashV = 1f; screenFlash = 1f; lightKick = 1.5f; Wave(t); Wave(t + 0.3f); for (int k = 0; k < 6; k++) Firework(t + 0.25f * k, k & 1);")
rep("Array.Clear(lights, 0, lights.Length);", "Array.Clear(lights, 0, lights.Length); fireworks.Clear(); Array.Clear(waves, 0, waves.Length); Array.Clear(halos, 0, halos.Length); Array.Clear(search, 0, search.Length); Array.Clear(pillars, 0, pillars.Length); Array.Clear(bars, 0, bars.Length); Array.Clear(barMat, 0, barMat.Length); fog = orbs = null; waveT = new[] { -99f, -99f, -99f, -99f };")
block = open(os.path.join(HERE, 'extras_block.cs.txt'), encoding='utf-8').read()
anchor = "        // ------------------------------------------------------------------ effects on the hits and the drops"
rep(anchor, block + "\n" + anchor)
open(p, 'w', encoding='utf-8').write(s); print('ok')
