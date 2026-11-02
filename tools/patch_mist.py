import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:60].replace('\n', ' '))
    return s.replace(a, b, 1)
# --- mist tint: the recording is a pale blue-white fog the whole run, violet inside the arm
s = rd('AstronomicalGalactus.cs')
s = sub(s, "case Sc.Assault: c = new Color(1f, 0.6f, 0.65f); a = 0.14f; break;",
        "case Sc.Assault: c = t >= 351f && t < 397f ? new Color(0.45f, 0.4f, 1f) : new Color(0.82f, 0.9f, 1f); a = t >= 351f && t < 397f ? 0.16f : 0.34f * (1f - 0.5f * Sm(405f, 418f, t)); break;")
wr('AstronomicalGalactus.cs', s)
# --- Galactus fades into the mist
s = rd('AstronomicalGalactus2.cs')
s = sub(s, "float hz = t < 130f ? 1f - Sm(55f, 128f, t) : 0f; if (Mathf.Abs(hz - GxHaze) > 0.01f || t < 130f) { GxHaze = hz; Color c = Color.Lerp(Color.white, new Color(2.6f, 2.4f, 2.6f), hz);",
        "float hz = t < 130f ? 1f - Sm(55f, 128f, t) : 0f; float hm = Sm(325f, 336f, t) * (1f - Sm(412f, 420f, t)) * 0.6f; if (Mathf.Abs(hz + hm - GxHaze) > 0.01f || t < 130f || hm > 0f) { GxHaze = hz + hm; Color c = hm > 0f ? Color.Lerp(Color.white, new Color(1.9f, 2.1f, 2.7f), hm) : Color.Lerp(Color.white, new Color(2.6f, 2.4f, 2.6f), hz);")
wr('AstronomicalGalactus2.cs', s)
# --- tunnel palette, white mist puffs, explosions inside
s = rd('AstronomicalGalactus3.cs')
s = sub(s, "if (seam2) return new Color(1f, 0.32f, 0.22f, 1f);", "if (seam2) return new Color(0.75f, 0.6f, 1f, 1f);")
s = sub(s, "return new Color(v * 1.15f, v * 0.62f, v * 0.7f, 1f); }, TextureWrapMode.Repeat);", "return new Color(v * 0.8f, v * 0.7f, v * 1.5f, 1f); }, TextureWrapMode.Repeat);")
s = sub(s, "condMat = kit.Glow(new Color(1f, 0.38f, 0.3f, 0.95f), lineTex, 3002); var gateMat = kit.Flat(new Color(0.16f, 0.1f, 0.12f)); var trimMat = kit.Glow(new Color(1f, 0.3f, 0.2f, 1f), null, 3001);",
        "condMat = kit.Glow(new Color(0.7f, 0.8f, 1f, 0.95f), lineTex, 3002); var gateMat = kit.Flat(new Color(0.1f, 0.09f, 0.2f)); trimMat = kit.Glow(new Color(0.75f, 0.7f, 1f, 1f), null, 3001);")
s = sub(s, "Transform tunnel, tunEnd; Material tunMat, condMat;", "Transform tunnel, tunEnd; Material tunMat, condMat, trimMat;")
s = sub(s, "condMat.color = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0.95f), new Color(0.9f, 0.4f, 1f, 0.95f), 0.5f + 0.5f * Mathf.Sin(t * 0.8f));",
        "condMat.color = Color.Lerp(new Color(0.7f, 0.85f, 1f, 0.95f), new Color(1f, 0.45f, 0.9f, 0.95f), 0.5f + 0.5f * Mathf.Sin(t * 0.8f)); trimMat.color = Color.Lerp(new Color(0.7f, 0.75f, 1f, 1f), new Color(1f, 0.55f, 0.95f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 1.3f));\n                if (!paused) for (int bi = 0; bi < 8; bi++) { float bt = 356f + 5f * bi; if (tp < bt && t >= bt) Boom(W(c0 + new Vector3((Hash(bi, 91) - 0.5f) * 70f, (Hash(bi, 92) - 0.5f) * 50f, 110f + 60f * Hash(bi, 93))), 20f, 0.9f, new Color(1f, 0.55f, 0.2f)); }")
s = sub(s, "Glow(tunEnd, W(c0 + new Vector3(0f, 0f, 720f)), R * 7f, new Color(1f, 0.55f + 0.2f * Mathf.Sin(t * 2f), 0.4f, 0.8f), cam, true);",
        "Glow(tunEnd, W(c0 + new Vector3(0f, 0f, 720f)), R * 7f, new Color(0.8f, 0.8f + 0.1f * Mathf.Sin(t * 2f), 1f, 0.85f), cam, true);")
s = sub(s, "            bool on = runOn && t >= 336f && t < 419f;",
        "            bool on = runOn && t >= 336f && t < 419f;\n            // the thick pale mist of the recording: big soft white puffs streaming past the bus (not inside the arm: there the liner is the backdrop)\n            if (!paused && dt > 0f && t >= 328f && t < 420f && !(t >= 351f && t < 397f)) { for (int k = 0; k < (int)(3 * Parts + 1); k++) Emit(dust, W(BusPos + new Vector3(UnityEngine.Random.Range(-110f, 110f), UnityEngine.Random.Range(-70f, 80f), UnityEngine.Random.Range(-30f, 320f))), root.transform.TransformDirection(new Vector3(0f, 0f, -48f)), new Color(0.88f, 0.94f, 1f, UnityEngine.Random.Range(0.22f, 0.4f)), UnityEngine.Random.Range(45f, 110f), 3.5f); }")
wr('AstronomicalGalactus3.cs', s)
