import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
s = rd('AstronomicalGalactus3.cs')
s = sub(s, 'AstronomicalDebug.Anchors["fall"] = () => W(FallPos(Mathf.Clamp(lastT, 180f, 270f)));', 'AstronomicalDebug.Anchors["fall"] = () => W(FallPos(Mathf.Clamp(lastT, 180f, 270f))); BuildExtra();')
s = sub(s, "cracks.Clear(); pylons.Clear();", "ExtraCleanup(); cracks.Clear(); pylons.Clear();")
s = sub(s, "RunFx(t, tp, dt, cam); BusFrame", "RunFx(t, tp, dt, cam); ExtraFrame(t, tp, dt, cam); BusFrame")
s = sub(s, "d.localPosition = p; d.localScale = new Vector3(7f, 5f, 7f); Glow(gl, W(p + new Vector3(0f, 0f, -3.2f)), 6f, new Color(1f, 0.25f, 0.2f, 0.95f), cam, true);",
        "d.localPosition = p; d.localRotation = Quaternion.identity; d.localScale = Vector3.one * 1.6f; Glow(gl, W(p + new Vector3(0f, 0f, 6.5f)), 9f, new Color(1f, 0.4f, 0.25f, 0.95f), cam, true);")
wr('AstronomicalGalactus3.cs', s)
s = rd('AstronomicalGalactus2.cs')
s = sub(s, "GxHitAim(t); GxRun(t, go, s);", "GxHitAim(t); GxRun(t, go, s); GxCup(t);")
# Wolverine leaps onto the bus at 8:30 (the recording: he jumps in over the camera and lands on the roof)
s = sub(s, 'wv.Keys.Add(Kf(500f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Hide", roof.y)); wv.Keys.Add(Kf(500.5f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Fight", roof.y));',
        'wv.Keys.Add(Kf(500f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Hide", roof.y)); wv.Keys.Add(Kf(510f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Fight", roof.y));')
s = sub(s, "            // Iron Man: falls with you,", """            if (wv != null) wv.Ov = t =>
            {
                if (t < 504.5f || t >= 510f) return null; float u = (t - 504.5f) / 5.5f; Vector3 a0 = new Vector3(roof.x - 9f, roof.y + 16f, roof.z - 78f), a1 = new Vector3(roof.x + 1.5f, roof.y, roof.z + 1.2f); Vector3 p = Vector3.Lerp(a0, a1, u); p.y += 14f * Mathf.Sin(u * Mathf.PI);
                return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = 0f, Pitch = 20f * (1f - u), Clip = u < 0.8f ? "Lunge" : "Fight" };
            };
            // Iron Man: falls with you,""")
wr('AstronomicalGalactus2.cs', s)
