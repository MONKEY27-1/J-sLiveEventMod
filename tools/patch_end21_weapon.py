m = open('AstronomicalEnd21.cs', encoding='utf-8').read()
b = open('AstronomicalEnd21b.cs', encoding='utf-8').read()
c = open('AstronomicalEnd21c.cs', encoding='utf-8').read()
def rep(s, a, n, cnt=1):
    assert a in s, 'missing: ' + a[:100]
    return s.replace(a, n, cnt)

# ---------------- the monsters can be shot: hit points, knock-back, death on the spot
m = rep(m, "struct Mon { public float T0, X0, Z0, Tx, Tz, Speed, T1, Seed, Scale, Die; public bool Big; public GameObject Go; public CRig Rig; public bool Boomed; }",
        "struct Mon { public float T0, X0, Z0, Tx, Tz, Speed, T1, Seed, Scale, Die; public bool Big; public GameObject Go; public CRig Rig; public bool Boomed; public float Hp, Killed, KX, KZ, VX, VZ, HitT; public int Hits; public Vector3 KillP; }")
m = rep(m, "for (int i = 0; i < mons.Count; i++) { var m = mons[i]; m.Boomed = false; mons[i] = m; } }", "for (int i = 0; i < mons.Count; i++) { var m = mons[i]; m.Boomed = false; m.Killed = -1f; m.Hp = m.Big ? 10f : 3f; m.Hits = 0; m.HitT = -9f; m.KX = m.KZ = m.VX = m.VZ = 0f; mons[i] = m; } }")
m = rep(m, "Scale = (big ? 10f : 6f) / 1.8f, Seed = Hash(i, 8) * 9f, Go = g, Rig = CRig.Of(careM, g) });", "Scale = (big ? 10f : 6f) / 1.8f, Seed = Hash(i, 8) * 9f, Go = g, Rig = CRig.Of(careM, g), Hp = big ? 10f : 3f, Killed = -1f, HitT = -9f });")
a = m.index("                var mo = mons[i]; bool on = t >= mo.T0 && t < mo.Die + 7f && t < T_CUBE_DIE; if (mo.Go.activeSelf != on) mo.Go.SetActive(on);")
e = m.index("            // ----- abductors circle the cube")
loop = '''                var mo = mons[i]; bool shotDead = mo.Killed >= 0f; float dieT = shotDead ? mo.Killed : mo.Die; bool on = t >= mo.T0 && t < dieT + 7f && t < T_CUBE_DIE; if (mo.Go.activeSelf != on) mo.Go.SetActive(on);
                if (on)
                {
                    float k = Mathf.Clamp01((t - mo.T0) / Mathf.Max(0.1f, mo.T1 - mo.T0)); Vector3 p = new Vector3(Mathf.Lerp(mo.X0, mo.Tx, k), 0f, Mathf.Lerp(mo.Z0, mo.Tz, k)); Vector3 dir = new Vector3(mo.Tx - mo.X0, 0f, mo.Tz - mo.Z0);
                    if (!shotDead && (mo.KX != 0f || mo.KZ != 0f || mo.VX != 0f || mo.VZ != 0f)) { if (dt > 0f && !paused) { mo.KX += mo.VX * dt; mo.KZ += mo.VZ * dt; float dm = Mathf.Exp(-3f * dt), sp = Mathf.Exp(-0.7f * dt); mo.VX *= dm; mo.VZ *= dm; mo.KX *= sp; mo.KZ *= sp; } p.x += mo.KX; p.z += mo.KZ; }      // shot: pushed back, then it walks on
                    p.y = GroundY(p.x, p.z); if (shotDead) p = mo.KillP;
                    var tr = mo.Go.transform; tr.localPosition = p; tr.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f); tr.localScale = Vector3.one * mo.Scale;
                    if (shotDead) PlayClip(mo.Rig, careM, "H_Death", t - mo.Killed);
                    else if (mo.HitT >= 0f && t - mo.HitT >= 0f && t - mo.HitT < 0.5f && (!mo.Big || mo.Hits % 3 == 0)) PlayClip(mo.Rig, careM, "H_Stagger", t - mo.HitT);
                    else if (t < mo.T1) { bool run = !mo.Big && t > mo.T0 + 0.5f * (mo.T1 - mo.T0) && mo.Speed > 6.5f; float ph = (t - mo.T0) * (run ? 1f / 0.9f : 1f / 1.35f) * 1.0f + mo.Seed; PlayClip(mo.Rig, careM, run ? "H_Run" : "H_Walk", ph); }
                    else if (t < mo.Die) { float lt = t - mo.T1; PlayClip(mo.Rig, careM, mo.Big ? "H_Slam" : "H_Attack", mo.Big ? lt % 4.5f : lt % 3f); if (!paused && ((int)(lt * 0.9f + mo.Seed) != (int)(tp * 0.9f + mo.Seed))) { hitGlow = 1f; Boom(W(new Vector3(mo.Tx, p.y + (mo.Big ? 6f : 3f), mo.Tz)), mo.Big ? 6f : 3f, mo.Big ? 0.9f : 0.4f, new Color(0.4f, 0.9f, 1f)); } }
                    else PlayClip(mo.Rig, careM, "H_Death", t - mo.Die);
                }
                if (!mo.Boomed && !shotDead && t >= mo.Die && t < T_CUBE_DIE && !paused && tp < mo.Die + 3f) { mo.Boomed = true; Boom(W(new Vector3(mo.Tx, GroundY(mo.Tx, mo.Tz) + 3f, mo.Tz)), mo.Big ? 10f : 5f, mo.Big ? 1.4f : 0.7f, new Color(1f, 0.55f, 0.2f)); }
                mons[i] = mo;
            }
'''
m = m[:a] + loop + m[e:]
# the weapon runs every frame (after part 2, so the scene is known)
m = rep(m, "Color bcol; float pb = P2Black(t, out bcol);", "WeaponFrame(t, dt, sc); Color bcol; float pb = P2Black(t, out bcol);")
# the debug console can fire too
m = rep(m, "            BuildSky(); BuildEffects(); BuildModels(); P2Start();", "            BuildSky(); BuildEffects(); BuildModels(); P2Start(); AstronomicalDebug.FireHook = (o, d) => Shoot(o, d);")
m = rep(m, "            try { P2Cleanup(); } catch { }", "            try { P2Cleanup(); } catch { } AstronomicalDebug.FireHook = null; boltPool.Clear(); boltAge.Clear();")

# ---------------- the cradle: its real materials (dark steel shell, magenta digital interior and gold cube glow) instead of one flat blue
m = rep(m, "            if (cradleM != null) foreach (var r in cradleM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var mt in r.sharedMaterials) if (mt != null) { mt.mainTexture = null; mt.color = new Color(0.35f, 0.65f, 1f, 1f); }      // the loaded texture is near-black: a lit blue-white structure instead",
        '''            if (cradleM != null)      // the Cube's Cradle with its own materials: a dark steel shell (lit, so its shape reads against the sky), the magenta digital-rain interior and the gold cube glow (unlit, bright)
                foreach (var kv in origMats)
                {
                    var r = kv.Key; if (r == null || !r.transform.IsChildOf(cradleM.Pivot.transform)) continue; var src = kv.Value; var nw = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        string n = src[i] != null ? src[i].name : ""; bool glow = n.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0;
                        Material mt = glow ? kit.Flat(Color.white * 1.9f) : kit.Lit(new Color(0.24f, 0.22f, 0.32f), null, 0.45f); try { mt.SetFloat("_Cull", 0f); } catch { }
                        if (glow && src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; nw[i] = mt;
                    }
                    r.sharedMaterials = nw;
                }''')
open('AstronomicalEnd21.cs', 'w', encoding='utf-8').write(m)

# ---------------- the actors: shot caretakers on the island's surface are pushed back and stagger
b = rep(b, "public int Shot = -1; public float Scale = 1f, Mir = -1f, Ox, Oz;", "public int Shot = -1; public float Scale = 1f, Mir = -1f, Ox, Oz, KX, KZ, VX, VZ, HitT = -9f;")
b = rep(b, "            if (!loco && oneShot.Contains(clip)) { var cc = FindClip(a.M, \"H_\" + clip);",
        "            if (a.Creature) { if (dt > 0f && !paused && (a.KX != 0f || a.KZ != 0f || a.VX != 0f || a.VZ != 0f)) { a.KX += a.VX * dt; a.KZ += a.VZ * dt; float dm = Mathf.Exp(-3f * dt), sp = Mathf.Exp(-0.7f * dt); a.VX *= dm; a.VZ *= dm; a.KX *= sp; a.KZ *= sp; } pos.x += a.KX; pos.z += a.KZ; if (a.HitT >= 0f && t - a.HitT >= 0f && t - a.HitT < 0.5f && clip != \"Death\") { clip = \"Stagger\"; ct = t - a.HitT; } }\n            if (!loco && oneShot.Contains(clip)) { var cc = FindClip(a.M, \"H_\" + clip);")
# the glass dome comes back as a nearly clear glow shell that disappears when the Caretaker breaks it
b = rep(b, "bool gl = false; /* the dome is left out: the Glow shader turns it into a solid cyan sky; the shattering is particles */ if (baseGlass.gameObject.activeSelf != gl)", "bool gl = t < T_FLOOD && !glassGone; if (baseGlass.gameObject.activeSelf != gl)")
open('AstronomicalEnd21b.cs', 'w', encoding='utf-8').write(b)
c = rep(c, "if (baseGlass != null) foreach (var gr in baseGlass.GetComponentsInChildren<Renderer>(true)) foreach (var gm in gr.sharedMaterials) if (gm != null) gm.color = new Color(0.55f, 0.85f, 1f, 0.07f);",
        "if (baseGlass != null) { var gmat = kit.Glow(new Color(0.55f, 0.85f, 1f, 0.06f), null, 3000); gmat.mainTexture = null; try { gmat.SetFloat(\"_Cull\", 0f); } catch { } foreach (var gr in baseGlass.GetComponentsInChildren<Renderer>(true)) { var sm = new Material[gr.sharedMaterials.Length]; for (int i = 0; i < sm.Length; i++) sm[i] = gmat; gr.sharedMaterials = sm; } }")
open('AstronomicalEnd21c.cs', 'w', encoding='utf-8').write(c)

# ---------------- the debug console: shoot
d = open('AstronomicalDebug.cs', encoding='utf-8').read()
d = rep(d, "        static Func<Vector3> follow, followAt;", "        static Func<Vector3> follow, followAt;\n        public static Action<Vector3, Vector3> FireHook;")
d = rep(d, '                case "tweak":', '''                case "shoot": // shoot <anchor> [count] [gap]: fires the player's weapon from your head at an anchor (a test for the trigger)
                {
                    Func<Vector3> fa2;
                    if (a.Length < 2 || !Anchors.TryGetValue(a[1], out fa2) || FireHook == null) { Say("shoot <anchor> [count] [gap]  (anchors: " + string.Join(", ", Anchors.Keys.ToArray()) + ")"); break; }
                    AstronomicalEventMod.Inst.StartCoroutine(ShootBurst(fa2, (int)F(a, 2, 1f), F(a, 3, 0.2f)));
                    break;
                }
                case "tweak":''')
d = rep(d, "        static string HeadPos()", '''        static System.Collections.IEnumerator ShootBurst(Func<Vector3> at, int n, float gap)
        {
            for (int i = 0; i < n; i++)
            {
                try { Vector3 o = GorillaLocomotion.GTPlayer.Instance.headCollider.transform.position + Vector3.down * 0.2f, tg = at(); if (FireHook != null) FireHook(o, (tg - o).normalized); } catch { }
                yield return new WaitForSeconds(gap);
            }
        }

        static string HeadPos()''')
open('AstronomicalDebug.cs', 'w', encoding='utf-8').write(d)
print('patched')
