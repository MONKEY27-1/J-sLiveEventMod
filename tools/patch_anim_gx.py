import os
os.chdir(r"C:\Users\drago\Documents\J'sLiveEventsMod")
def rd(p): return open(p, encoding='utf-8').read()
def wr(p, s): open(p, 'w', encoding='utf-8').write(s)
def sub(s, a, b):
    print('OK' if a in s else 'MISSING', a[:70].replace('\n', ' '))
    return s.replace(a, b, 1)
s = rd('AstronomicalGalactus2.cs')
s = sub(s, "        void GxFrame(float t, float tp, float dt, Seg g, Camera cam)\n        {", """        // ---- Galactus' living motion, layered on the pose table: 14 named animations, each with an envelope in time.  d = additive angles [0..10] like the table + [11] torso yaw, [12] torso roll
        static float Env(float a, float b, float c, float e, float t) { return Sm(a, b, t) * (1f - Sm(c, e, t)); }
        static float[] GxIdle(float t)
        {
            var d = new float[13]; float br = Mathf.Sin(t * 1.3f);
            d[10] += 0.6f * br; d[3] += 1.5f * br; d[7] += 1.5f * br; d[12] += 0.4f * Mathf.Sin(t * 0.6f);                                                      // 1 breathing
            { float e = Env(52f, 62f, 116f, 124f, t), s = Mathf.Sin(t * 2.1f); d[0] += 14f * s * e; d[4] -= 14f * s * e; d[11] += 5f * s * e; d[12] += 2.5f * Mathf.Cos(t * 2.1f) * e; d[10] += 2f * Mathf.Abs(s) * e; }   // 2 the slow walk-in
            { float e = Env(118f, 126f, 148f, 154f, t), s = Mathf.Sin((t - 118f) * 0.45f); d[8] += 30f * s * e; d[9] += 7f * Mathf.Sin((t - 118f) * 0.9f) * e; }   // 3 looks along the deck
            { float e = Env(124f, 132f, 158f, 166f, t); d[1] += 9f * Mathf.Sin(t * 1.3f) * e; d[5] -= 9f * Mathf.Sin(t * 1.3f + 1f) * e; d[11] += 4f * Mathf.Sin(t * 0.8f) * e; }   // 4 shoulder rolls
            { float e = Env(128f, 134f, 164f, 168f, t); float c = 0.5f + 0.5f * Mathf.Sin(t * 2.4f); d[3] += 14f * c * e; d[7] += 14f * (1f - c) * e; }             // 5 the fists clench in turn
            { float e = Env(148f, 154f, 163f, 168f, t); d[9] -= 24f * e; d[10] -= 7f * e; d[1] += 22f * e; d[5] -= 22f * e; d[12] += 3f * Mathf.Sin(t * 31f) * e; }  // 6 the roar before the blow: head back, arms flung wide, the body shakes
            { float e = Env(168f, 174f, 178f, 182f, t); d[10] += 10f * e; d[11] -= 12f * e; }                                                                    // 7 the wind-up lean and torso twist for the backhand
            { float e = Env(178f, 184f, 262f, 272f, t); d[9] += 14f * e; d[8] += 16f * Mathf.Sin(t * 0.5f) * e; d[10] += 4f * e; d[7] += 10f * Mathf.Sin(t * 3f) * e; }  // 8 he looks down at the falling player, fingers working
            { float e = Env(196f, 204f, 266f, 274f, t); d[11] += 9f * Mathf.Sin(t * 0.6f) * e; d[12] += 3f * Mathf.Sin(t * 0.9f) * e; }                          // 9 shifting his weight while the energy gathers
            { float e = Env(272f, 282f, 326f, 334f, t); d[8] += 18f * Mathf.Sin(t * 0.4f) * e; d[10] += 4f * Mathf.Sin(t * 0.5f) * e; d[0] += 6f * Mathf.Sin(t * 1.1f) * e; d[4] += 6f * Mathf.Sin(t * 1.1f + 2f) * e; }  // 10 sizing up the buses
            { float e = Env(334f, 342f, 414f, 422f, t); d[8] += 12f * Mathf.Sin(t * 0.5f) * e; d[11] += 7f * Mathf.Sin(t * 0.4f) * e; d[12] += 2f * Mathf.Sin(t * 0.7f) * e; }   // 11 tracking the bus up his arm
            { float e = Env(508f, 516f, 575f, 578f, t); float pc = Mathf.Repeat(t - 510f, 9f); float r = Sm(0.4f, 1.6f, pc) * (1f - Sm(2.6f, 4.2f, pc)); d[9] -= 18f * r * e; d[10] -= 6f * r * e; d[1] += 16f * r * e; d[5] -= 16f * r * e; d[12] += 2.5f * Mathf.Sin(t * 27f) * r * e; }   // 12 the roars in space, every 9 s
            { float e = Env(508f, 516f, 575f, 578f, t); d[0] += 12f * Mathf.Sin(t * 0.8f) * e; d[4] += 12f * Mathf.Sin(t * 0.8f + 1.7f) * e; d[11] += 6f * Mathf.Sin(t * 0.35f) * e; }   // 13 the arms drift like in zero gravity
            { float e = Env(540f, 550f, 575f, 578f, t); d[10] -= 7f * e * Mathf.Abs(Mathf.Sin(t * 5f)); d[8] += 14f * Mathf.Sin(t * 0.7f) * e; d[9] -= 8f * e; }                  // 14 the mouth opens wide, he rears back and shudders as the payload swells
            return d;
        }
        void GxFrame(float t, float tp, float dt, Seg g, Camera cam)
        {""")
s = sub(s, "if (gxTorso == null) return; float[] p = GxAng(t);", "if (gxTorso == null) return; float[] p = GxAng(t); var gi = GxIdle(t); for (int q = 0; q < 11; q++) p[q] += gi[q];")
s = sub(s, "gxTorso.localRotation = Quaternion.Euler(0f, 3f * Mathf.Sin(t * 0.3f), 0f);", "gxTorso.localRotation = Quaternion.Euler(0f, 3f * Mathf.Sin(t * 0.3f) + gi[11], gi[12]);")
wr('AstronomicalGalactus2.cs', s)
