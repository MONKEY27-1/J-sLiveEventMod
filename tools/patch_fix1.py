import re, io
def rd(f): return open(f, encoding='utf-8').read()
def wr(f, s): open(f, 'w', encoding='utf-8').write(s)
def rep(s, a, n, cnt=1):
    assert a in s, 'missing: ' + a[:110]
    return s.replace(a, n, cnt)

# =============================================================== Blender: the round walls (axis-aligned boxes left gaps between them = see-through walls)
w = rd('tools/end21_world.py')
helper = '''
def rwall(cx, cz, R, y0, y1, segs, mat, skip=None, tile=3.0):
    """a closed round wall of `segs` flat panels that really follow the circle (two faces each, so it is solid from both sides); skip(deg) leaves doorways"""
    import math as _m
    for k in range(segs):
        a0 = k * 2 * _m.pi / segs; a1 = (k + 1) * 2 * _m.pi / segs
        if skip is not None and skip(_m.degrees((a0 + a1) / 2) % 360.0): continue
        p0 = Bz(cx + _m.cos(a0) * R, y0, cz + _m.sin(a0) * R); p1 = Bz(cx + _m.cos(a1) * R, y0, cz + _m.sin(a1) * R); p2 = Bz(cx + _m.cos(a1) * R, y1, cz + _m.sin(a1) * R); p3 = Bz(cx + _m.cos(a0) * R, y1, cz + _m.sin(a0) * R)
        u0 = k * (2 * _m.pi * R / segs) / tile; u1 = (k + 1) * (2 * _m.pi * R / segs) / tile; v1 = (y1 - y0) / tile; uv = [(u0, 0), (u1, 0), (u1, v1), (u0, v1)]
        b.quad(p0, p1, p2, p3, mat, uv); b.quad(p3, p2, p1, p0, mat, [uv[3], uv[2], uv[1], uv[0]])
'''
w = rep(w, "if kind == 'hall':", helper + "if kind == 'hall':")
a = w.index("    for k in range(40):                                                                                                         # the round wall of dark purple panels")
e = w.index("    for sgn in (-1, 1):\n        bx(sgn * (R - 0.2), 2.0, 5.0")
w = w[:a] + "    rwall(0, 0, R, 0.0, 16.0, 60, MWP, lambda d: min(d, 360 - d) < 19 or abs(d - 180) < 19)       # solid round wall, two doorways (+-x)\n" + w[e:]
a = w.index("    for k in range(48):\n        a = (k + 0.5) * 2 * math.pi / 48; x, z = ccx")
e = w.index("    for k in range(16):                                                                                                         # crystals and pillars")
w = w[:a] + "    rwall(ccx, ccz, CR, 0.0, 31.0, 96, MWP, lambda d: abs(d - 270) < 5.5)      # solid round wall, the entrance in the south\n" + w[e:]
wr('tools/end21_world.py', w)

# =============================================================== Blender: the facility chair (open leg room) and a wreck set that appears after the breach
r = rd('tools/end21_rooms.py')
r = rep(r, "    bx(0, 0.22, 3.05, 0.9, 0.44, 0.9, D_, 1.0, top=S_)                                    # seat block\n", "    bx(0, 0.2, 3.25, 0.5, 0.4, 0.5, D_, 1.0); bx(0, 0.42, 3.15, 0.9, 0.1, 0.9, D_, 1.0, top=S_)         # a pedestal and a thin seat pan: the legs hang free in front (z < 2.7)\n")
wreck = '''
    # ---- the wreck: ceiling panels hanging at angles, fallen beams, rubble, a toppled rack and scorch marks round the blast door (shown from 4:56)
    import random, mathutils
    R_ = random.Random(5); wk = MB([FL, WL, CE, RED, AMB, GRN, BLU, ORG, STE, DK, WH, ECG, CYA])
    def wbox(c, s, rot, m):
        e = mathutils.Euler(rot, 'XYZ').to_matrix(); h = [(sx * s[0] / 2, sy * s[1] / 2, sz * s[2] / 2) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        P = {}; i = 0
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    v = e @ mathutils.Vector((sx * s[0] / 2, sy * s[1] / 2, sz * s[2] / 2)); P[(sx, sy, sz)] = (c[0] + v.x, c[1] + v.y, c[2] + v.z)
        for ax, sg in ((0, -1), (0, 1), (1, -1), (1, 1), (2, -1), (2, 1)):
            keys = [k for k in P if k[ax] == sg]; q = []
            o1, o2 = [a for a in range(3) if a != ax]
            for (u, vv) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                k = [0, 0, 0]; k[ax] = sg; k[o1] = u; k[o2] = vv; q.append(P[tuple(k)])
            wk.quad(q[0], q[1], q[2], q[3], m, [(0, 0), (1, 0), (1, 1), (0, 1)]); wk.quad(q[3], q[2], q[1], q[0], m, [(0, 1), (1, 1), (1, 0), (0, 0)])
    for i in range(9):    # hanging ceiling panels (unity x, y, z -> blender (x, -z, y))
        x = R_.uniform(-5.2, 5.2); z = R_.uniform(-8.6, -0.5); y = R_.uniform(5.2, 6.7); wbox((x, -z, y), (R_.uniform(1.6, 2.8), R_.uniform(1.4, 2.4), 0.14), (math.radians(R_.uniform(20, 60)) * R_.choice((-1, 1)), math.radians(R_.uniform(-25, 25)), math.radians(R_.uniform(0, 90))), R_.choice((F_, 8, 9)) if False else 8)
    for i in range(6):    # fallen beams
        x = R_.uniform(-4.5, 4.5); z = R_.uniform(-8.6, -1.5); wbox((x, -z, 0.28), (0.34, R_.uniform(2.4, 4.2), 0.34), (math.radians(R_.uniform(-8, 8)), math.radians(R_.uniform(-8, 8)), math.radians(R_.uniform(0, 180))), 9)
    for i in range(34):   # rubble
        x = R_.uniform(-5.5, 5.5); z = R_.uniform(-9.2, -1.0); s = R_.uniform(0.15, 0.75); wbox((x, -z, s * 0.4), (s, s * R_.uniform(0.6, 1.4), s * 0.7), (math.radians(R_.uniform(0, 40)), math.radians(R_.uniform(0, 40)), math.radians(R_.uniform(0, 180))), R_.choice((8, 9, 1)))
    wbox((-9.4, 3.2, 1.0), (1.4, 1.8, 2.6), (0.0, math.radians(38), math.radians(8)), 9); wbox((-9.2, 3.2, 2.0), (0.06, 1.4, 1.7), (0.0, math.radians(38), math.radians(8)), 5)      # a toppled rack, its screen smashed
    wbox((0.5, 6.0, 0.03), (5.0, 3.6, 0.02), (0, 0, math.radians(15)), 9); wbox((-3.2, 2.4, 0.03), (3.0, 2.6, 0.02), (0, 0, math.radians(-30)), 9)                                        # scorch marks
    wbox((-3.1, 9.0, 4.6), (0.4, 0.4, 3.6), (math.radians(28), 0, math.radians(-12)), 9); wbox((3.2, 8.9, 4.2), (0.4, 0.4, 3.4), (math.radians(-24), math.radians(10), 0), 9)     # the door frame pillars leaning in
    wk.build('Wreck')
'''
r = rep(r, "    b.build('Facility')\n", "    b.build('Facility')\n" + wreck)
wr('tools/end21_rooms.py', r)

# =============================================================== C#
b = rd('AstronomicalEnd21b.cs'); c = rd('AstronomicalEnd21c.cs'); m = rd('AstronomicalEnd21.cs'); d = rd('AstronomicalEnd21d.cs'); h = rd('AstronomicalEnd21h.cs')
b = rep(b, "Y_FAC = 700f,", "Y_PLAT_OFF = 0.7f, Y_FAC = 700f,")
b = rep(b, "        Vector3 hallLight;", "        Transform wreck;\n        Vector3 hallLight;")
# wreck appears with the breach
b = rep(b, "            // ---- doors: the blast door (4:56), the security door (4:34 Slone, 5:06 the guard)", "            if (wreck != null) { bool wv = sc == Sc.Fac && t > 295.9f; if (wreck.gameObject.activeSelf != wv) wreck.gameObject.SetActive(wv); }\n            // ---- doors: the blast door (4:56), the security door (4:34 Slone, 5:06 the guard)")
b = rep(b, "giant = null; giantGo = null; giantLight = null;", "wreck = null; giant = null; giantGo = null; giantLight = null;")
# robust 'you are not where you should be' check
b = rep(b, "if (dd > 30f && reseatTries2 < 8 && Time.unscaledTime - lastReseat2 > 0.7f)", "if ((dd > 30f || dd > 16f || (dd > 9f && HeadNow().y < aw.y - 2.2f)) && reseatTries2 < 40 && Time.unscaledTime - lastReseat2 > 0.7f)")
# the loader hurries when the event is already close to part 2 (skipped ahead / slow PC)
b = rep(b, "yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.5f : 6f);", "yield return new WaitForSeconds(AstronomicalDebug.NoMusic || lastT >= T_FAC - 40f ? 0.1f : 6f);")
b = rep(b, "yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.3f : 3f);", "yield return new WaitForSeconds(AstronomicalDebug.NoMusic || lastT >= T_FAC - 40f ? 0.05f : 3f);")
c = rep(c, "if (facM != null) { Colliders(facM.Pivot);", "if (facM != null) { Colliders(facM.Pivot); foreach (var tw in facM.Pivot.GetComponentsInChildren<Transform>(true)) if (tw.name == \"Wreck\") { wreck = tw; var wc = tw.GetComponent<MeshCollider>(); if (wc != null) UnityEngine.Object.Destroy(wc); tw.gameObject.SetActive(false); }")
# sparks and glow from the broken ceiling
c = rep(c, "                if (at(307.9f)) shake = Mathf.Max(shake, 0.6f);", "                if (at(307.9f)) shake = Mathf.Max(shake, 0.6f);\n                if (t > 296f && UnityEngine.Random.value < dt * 7f) { Vector3 sp = W(new Vector3(UnityEngine.Random.Range(-6f, 6f), Y_FAC + UnityEngine.Random.Range(5f, 6.6f), UnityEngine.Random.Range(-9f, 1f))); for (int k = 0; k < 8; k++) Emit(fx, sp, new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), -UnityEngine.Random.Range(1f, 5f), UnityEngine.Random.Range(-1.5f, 1.5f)), new Color(1f, UnityEngine.Random.Range(0.6f, 0.9f), 0.3f, 1f), UnityEngine.Random.Range(0.05f, 0.14f), UnityEngine.Random.Range(0.4f, 1.1f)); }")
# facility cast: no more overlaps
c = rep(c, 'fw(J, 329.6f, 0f, 2.2f, 180f, "Wake"); fw(J, 330.6f, 0f, 2.2f, 180f, "Talk"); fw(J, 331.9f, 0f, 2.2f, 180f, "Talk");', 'fw(J, 329.6f, 0f, 2.4f, 180f, "Wake"); fw(J, 330.6f, 0f, 2.4f, 180f, "Talk"); fw(J, 331.9f, 0f, 2.4f, 180f, "Talk");')
c = rep(c, 'fw(S, 325f, 0.1f, 1.75f, 90f, "Plead"); fw(S, 331.9f, 0.1f, 1.75f, 90f, "Plead");', 'fw(S, 324.3f, 0.1f, 1.75f, 90f, "Walk"); fw(S, 325.7f, -2.4f, 1.6f, 90f, "Plead"); fw(S, 331.9f, -2.4f, 1.6f, 90f, "Plead");')
c = rep(c, 'fw(F, 325.9f, 0.65f, 1.75f, -90f, "Pin");', 'fw(F, 325.9f, -1.3f, 1.6f, -90f, "Pin");')
c = rep(c, 'fw(F, 329.8f, 0.65f, 1.75f, -90f, "Idle"); fw(F, 330.2f, 0.55f, 1.95f, 0f, "Talk"); fw(F, 331.9f, 0.55f, 1.95f, 0f, "Talk");', 'fw(F, 329.8f, -1.3f, 1.6f, -90f, "Walk"); fw(F, 331.0f, 0f, 1.1f, 0f, "Talk"); fw(F, 331.9f, 0f, 1.1f, 0f, "Talk");')
# gyro: Jones and the Foundation stand ON the raised platform (0.7 m), not in it
lines = c.split('\n')
for i, l in enumerate(lines):
    if l.lstrip().startswith('if (J != null) { Wp(J, T_GYRO, -0.4f, Y_GYRO') or l.lstrip().startswith('if (F != null) { Wp(F, T_GYRO, 3.8f, Y_GYRO'):
        lines[i] = l.replace('Y_GYRO', 'Y_GYRO + Y_PLAT_OFF').replace('gw(', 'gp(')
c = '\n'.join(lines)
c = rep(c, "            // ================= the Zero Point machinery chamber (5:56 - 6:40)", "            Action<Act, float, float, float, float, string> gp = (a, t, x, z, yw, cl) => Wp(a, t, x, Y_GYRO + Y_PLAT_OFF, z, yw, cl);\n            // ================= the Zero Point machinery chamber (5:56 - 6:40)")
# the Seven walk to the hatch and go down the stairs
c = rep(c, 'sw(419f, -5.5f, 17f, 142f, "Walk");\n                sw(424f, -0.3f, 7.0f, 90f, "Walk"); sw(427.6f, 0f, 11.4f, 0f, "Walk"); sw(428.3f, 0f, 11.4f, 0f, "Idle");',
        'sw(419f, -5.5f, 17f, 142f, "Walk");\n                sw(422.6f, -1.8f, 6.2f, 45f, "Walk"); sw(424.2f, 0f, 7.6f, 0f, "Walk"); Wp(Sc, 425.6f, 0f, -0.9f, 8.9f, 0f, "Walk"); Wp(Sc, 428.6f, 0f, -3.0f, 11.3f, 0f, "Walk"); Wp(Sc, 429.5f, 0f, -3.1f, 11.8f, 0f, "Walk");')
c = rep(c, 'vw(424.6f, 0.9f, 6.5f, 90f, "Walk"); vw(427.9f, 0.4f, 11.2f, 0f, "Walk"); vw(428.4f, 0.4f, 11.2f, 0f, "Idle");', 'vw(423.0f, 2.2f, 5.6f, -45f, "Walk"); vw(424.8f, 0.7f, 6.6f, 0f, "Walk"); Wp(V, 426.4f, 0.5f, -0.7f, 8.4f, 0f, "Walk"); Wp(V, 429.0f, 0.5f, -3.0f, 10.9f, 0f, "Walk"); Wp(V, 429.6f, 0.5f, -3.1f, 11.3f, 0f, "Walk");')
# you follow them: the surface clamp circle slides to the hatch
m = rep(m, "KeepInside(t, stageX, stageZ, 16f); }", "float mg = Sm(418f, 426f, t); KeepInside(t, Mathf.Lerp(stageX, hatchX, mg), Mathf.Lerp(stageZ, hatchZ - 2f, mg), Mathf.Lerp(16f, 5f, mg)); }")
# not ready yet: stay on the island visuals under a black loading screen instead of showing the ocean scene's pieces over the real map
m = rep(m, "            Sc sc = SceneAt(t); if (back) {", "            Sc sc = SceneAt(t); if (!p2Ready && sc != Sc.Island) sc = Sc.Island; if (back) {")
m = rep(m, "P2Frame(t, dt, tp, cam);\n            Black(black, cam, bcol);", "P2Frame(t, dt, tp, cam);\n            bool loadingNow = !p2Ready && t >= T_FAC - 0.3f; if (loadingNow) { black = 1f; bcol = Color.black; } LoadingText(loadingNow);\n            Black(black, cam, bcol);")
m = rep(m, "BuildSky(); BuildEffects(); SfxInit();", "BuildSky(); BuildEffects(); SfxInit(); CaptionBuild();")
d = rep(d, "titleGo.SetActive(false); CaptionBuild();", "titleGo.SetActive(false);")
d = re.sub(r"var bc = oceanGo\.AddComponent<BoxCollider>\(\);[^\n]*", "// (no collider on the sea: rising from underwater through a solid plane threw the rig about)", d, count=1)
h = rep(h, "        void CaptionFrame(float t)", """        bool loadingShown;
        void LoadingText(bool on)
        {
            if (capGo == null) return; if (on) { var cam = Camera.main; if (cam == null) return; capGo.SetActive(true); loadingShown = true; capTx.text = "LOADING THE END..."; capTx.color = new Color(1f, 1f, 1f, 0.9f); capGo.transform.position = cam.transform.position + cam.transform.forward * 4f; capGo.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up); }
            else if (loadingShown) { loadingShown = false; capGo.SetActive(false); }
        }
        void CaptionFrame(float t)""")
for f_, s_ in (('AstronomicalEnd21b.cs', b), ('AstronomicalEnd21c.cs', c), ('AstronomicalEnd21.cs', m), ('AstronomicalEnd21d.cs', d), ('AstronomicalEnd21h.cs', h)): wr(f_, s_)

# =============================================================== animation: Jones in the chair rests his hands on the arms
a = open('tools/anim_human2.py').read()
old = "hl=(0.55, -0.55, 0.45, 0.85), hr=(0.55, -0.55, 0.45, 0.85), el=(0.8, -0.2, 0.3), er=(0.8, -0.2, 0.3))"
assert old in a
a = a.replace(old, "hl=(0.35, -0.9, 0.15, 0.62), hr=(0.35, -0.9, 0.15, 0.62), el=(0.9, -0.1, -0.2), er=(0.9, -0.1, -0.2))", 1)
open('tools/anim_human2.py', 'w').write(a)
print('ok')
