using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Collision, milestones 2 and 3 (8:00 - 12:15). After the black at 8:00 the world is the underground (Blender cavern: the Collider's pillar with a spiral staircase, a Zero Point, guards, a boulder, crates), far below the map:
    // you are carried along the stairs behind Jones and the Foundation (and may walk about near them); 8:13 Jones wakes you, 8:24 you follow, 8:36 you reach the base and shoot the guards, 9:00 the Foundation drops in, he leads you up (boulder 9:12,
    // the brute and the crate bomb 9:30), Jones looks at the Zero Point 9:39, the stairs, guards, Slone in her tank, Jones negotiates 10:26 - 10:36, the mech's fist hits the tank 10:36, 10:44 its hand comes out and carries you, 11:03 Jones and
    // the Foundation jump into the Zero Point, the hand raises you 11:04 - 11:14, you shoot the four crystals until 11:29, the Zero Point sinks and the Collider is destroyed, the hands clamp round you at 11:45 (dark), 11:51 Jones and the
    // Foundation fall through a red sky (fist bump 12:01), black 12:07, TO BE CONTINUED 12:15.
    internal sealed partial class AstronomicalCollision
    {
        const float RAD = 40f, TURNS = 2.2f, RISE = 130f, T_UNDER = 493f, T_RED = 705f, T_DARK = 711f, T_TBC = 735f;
        static readonly Vector3 UG0 = new Vector3(0f, PAD_Y, 0f), FF0 = new Vector3(0f, PAD_Y, 0f), MU0 = new Vector3(0f, PAD_Y, 0f);      // the moon base's own airspace (0, 700, 0): it is known to work; the scenes never overlap in time
        CRig cavernR; Light ugWarm, ugPurple; GltfModel jonesM, foundM, sloneM, cavernM, zpM, crateM, boulderM, tbcM, mushM, rifleM; GameObject rifleJ, rifleF; Transform gunJ, gunF, rmodJ, rmodF; int shotJ = -1, shotF = -1; const float GS_J = 0.85f, GS_F = 0.95f; CRig jonesR, foundR, sloneR, zpR, tbcR, mushR; GltfModel guardM; sealed class GuardA { public GameObject Go; public CRig Rig; public Transform Gun, RMod; public GameObject Rifle; public int Shot = -1; public float Off; } readonly List<GuardA> gA = new List<GuardA>(); GameObject sloneTank; List<Transform> crystals = new List<Transform>();
        readonly List<Seg> jonesSegs = new List<Seg>(), foundSegs = new List<Seg>(), sloneSegs = new List<Seg>(); bool underOn, seatedUnder, seatedRed, seatedMush; Vector3 anchorPrev;
        struct Guard { public float T0, T1; public int Idx; public Vector3 P; public float Yaw; }
        readonly List<Guard> guards = new List<Guard>(); readonly HashSet<int> guardBoom = new HashSet<int>(); bool ceBoom, crateBoom, boulderBoom, crystalBoom0, tankBoom, collapseBoom; readonly bool[] cryBroke = new bool[4]; ParticleSystem clouds; float cloudAcc;
        const float SQ = 60f, SLONE_S = 0.733f, Q_END = 0.733f - 24f / 900f, MECH_S2 = 63f, GS_G = 0.9f; static readonly Vector3 PALM = new Vector3(-0.0861f, 0.02f, -0.0561f);
        static readonly Vector2[] SQP = { new Vector2(0f, -SQ), new Vector2(SQ, -SQ), new Vector2(SQ, SQ), new Vector2(-SQ, SQ), new Vector2(-SQ, -SQ), new Vector2(SQ, -SQ), new Vector2(SQ, SQ), new Vector2(-SQ, SQ), new Vector2(-SQ, -SQ) };
        // the stairs go up in a SQUARE round the tower: from the middle of the south side east, then round and round (same numbers as tools/make_cavern.py)
        static Vector3 Stair(float s)
        {
            s = Mathf.Clamp01(s); float total = 60f + 7f * 120f, L = s * total; int i = 0; float acc = 0f; while (i < SQP.Length - 2) { float seg = i == 0 ? 60f : 120f; if (L <= acc + seg) break; acc += seg; i++; }
            float len = i == 0 ? 60f : 120f; float u = Mathf.Clamp01((L - acc) / len); Vector2 p = Vector2.Lerp(SQP[i], SQP[i + 1], u); return new Vector3(p.x, 4f + 130f * s, p.y);
        }
        static Vector3 Out(float s) { Vector3 p = Stair(s); p.y = 0f; return p.sqrMagnitude < 1e-4f ? Vector3.forward : p.normalized; }                                       // away from the tower
        // the path: q in [-1, 0) = the floor (from -130 m to the stair base at -44 m), q in [0, 1] = the stairs
        static Vector3 PathAt(float q) { if (q < 0f) { float z = Mathf.Lerp(-150f, -60f, 1f + q); return new Vector3(0f, 4f * Mathf.Clamp01((z + 90f) / 30f), z); } var p = Stair(q); p.y = Mathf.Min(p.y, 4f + 130f * Q_END); return p; }     // the 30 steps up to the stairs; the deck is flat
        static readonly float[][] WAY = { new[] { 493f, -1f }, new[] { 504f, -1f }, new[] { 516f, 0f }, new[] { 540f, 0f }, new[] { 547f, 0.06f }, new[] { 554f, 0.06f }, new[] { 558f, 0.10f }, new[] { 571f, 0.10f }, new[] { 579f, 0.22f }, new[] { 582f, 0.22f }, new[] { 588f, 0.30f }, new[] { 613f, 0.50f }, new[] { 619f, 0.62f }, new[] { 625f, Q_END }, new[] { 900f, Q_END } };
        static float QAt(float t) { if (t <= WAY[0][0]) return WAY[0][1]; for (int i = 1; i < WAY.Length; i++) if (t < WAY[i][0]) return Mathf.Lerp(WAY[i - 1][1], WAY[i][1], Sm(WAY[i - 1][0], WAY[i][0], t)); return WAY[WAY.Length - 1][1]; }
        static float Yaw(float q) { Vector3 d = PathAt(q + 0.003f) - PathAt(q); return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        static float ArcLen(float q) { return q < 0f ? (q + 1f) * 90f : 90f + q * 900f; }
        static float DQ(float meters, float q) { return meters / (q < 0f ? 90f : 900f); }
        Vector3 UG(Vector3 v) { return W(UG0 + v); }
        static Vector3 ZPC { get { return new Vector3(0f, 124f, 0f); } } const float ZPR = 16f;
        static Vector3 L4() { return Stair(SLONE_S) + Out(SLONE_S) * 2f; }                                                                                              // the fourth landing (Slone's)

        void BuildModels2()
        {
            ugWarm = new GameObject("CUgWarm").AddComponent<Light>(); ugWarm.transform.SetParent(root.transform, false); ugWarm.type = LightType.Point; ugWarm.color = new Color(1f, 0.6f, 0.25f); ugWarm.range = 45f; ugWarm.intensity = 0f; ugWarm.shadows = LightShadows.None;
            ugPurple = new GameObject("CUgPurple").AddComponent<Light>(); ugPurple.transform.SetParent(root.transform, false); ugPurple.type = LightType.Point; ugPurple.color = new Color(0.7f, 0.4f, 1f); ugPurple.range = 190f; ugPurple.intensity = 0f; ugPurple.shadows = LightShadows.None;
            if (tankM != null && tankM.Pivot != null) { sloneTank = UnityEngine.Object.Instantiate(tankM.Pivot, root.transform); sloneTank.name = "CSloneTank"; sloneTank.SetActive(false); }
            clouds = MakePS("CClouds", 900, 0f, kit.Glow(Color.white, glowTex, 3001)); part2Dead = false; AstronomicalEventMod.Inst.StartCoroutine(LoadPart2());
            Action<List<Seg>, float, string, float, float, bool> S = (l, t, c, sp, off, loop) => l.Add(new Seg { T = t, Clip = c, Speed = sp, Off = off, Loop = loop });
            // Jones (clip names come from tools/anim_human2.py). Non-looping clips start at their segment time; the walk / run cycles are driven by distance (PlaySegs, loco), so the segments only say what they do when standing
            S(jonesSegs, 480f, "H_Idle", 1f, 0f, true); S(jonesSegs, 493f, "H_Wake", 1f, 493f, false); S(jonesSegs, 497f, "H_Beckon", 1f, 0f, true); S(jonesSegs, 514f, "H_Ready", 1f, 0f, true); S(jonesSegs, 516f, "H_Aim", 1f, 0f, true); S(jonesSegs, 517.5f, "H_Fire", 1f, 0f, true); S(jonesSegs, 527f, "H_Reload", 1f, 527f, false); S(jonesSegs, 531.2f, "H_Fire", 1f, 0f, true);
            S(jonesSegs, 540f, "H_Point", 1f, 540f, false); S(jonesSegs, 542.5f, "H_Ready", 1f, 0f, true); S(jonesSegs, 553f, "H_Dodge", 1f, 553f, false); S(jonesSegs, 554.5f, "H_Alert", 1f, 0f, true); S(jonesSegs, 558f, "H_Alert", 1f, 0f, true); S(jonesSegs, 561f, "H_FireK", 1f, 0f, true); S(jonesSegs, 566.5f, "H_Beckon", 1f, 0f, true); S(jonesSegs, 570.4f, "H_Hit", 1f, 570.4f, false); S(jonesSegs, 572f, "H_Ready", 1f, 0f, true);
            S(jonesSegs, 579f, "H_Look", 1f, 579f, false); S(jonesSegs, 582f, "H_Ready", 1f, 0f, true); S(jonesSegs, 588f, "H_Aim", 1f, 0f, true); S(jonesSegs, 588.5f, "H_Fire", 1f, 0f, true); S(jonesSegs, 596f, "H_Reload", 1f, 596f, false); S(jonesSegs, 600.2f, "H_Crouch", 1f, 0f, true); S(jonesSegs, 604f, "H_Peek", 1f, 604f, false); S(jonesSegs, 609f, "H_FireK", 1f, 0f, true); S(jonesSegs, 613f, "H_Ready", 1f, 0f, true); S(jonesSegs, 619f, "H_Crouch", 1f, 0f, true);
            S(jonesSegs, 626f, "H_Plead", 1f, 626f, false); S(jonesSegs, 630f, "H_Talk", 1f, 0f, true); S(jonesSegs, 636f, "H_Stagger", 1f, 636f, false); S(jonesSegs, 638.5f, "H_Look", 1f, 638.5f, false); S(jonesSegs, 644f, "H_Beckon", 1f, 0f, true); S(jonesSegs, 648f, "H_Ready", 1f, 0f, true); S(jonesSegs, 652f, "H_Aim", 1f, 0f, true); S(jonesSegs, 663.3f, "H_Jump", 1f, 663.3f, false);
            S(foundSegs, 480f, "H_Idle", 1f, 0f, true); S(foundSegs, 536f, "H_Fall", 1f, 0f, true); S(foundSegs, 540f, "H_Land", 1f, 540f, false); S(foundSegs, 543.2f, "H_Beckon", 1f, 0f, true); S(foundSegs, 546f, "H_Ready", 1f, 0f, true); S(foundSegs, 551.1f, "H_Shove", 1f, 551.1f, false); S(foundSegs, 554.5f, "H_Ready", 1f, 0f, true);
            S(foundSegs, 558f, "H_Alert", 1f, 0f, true); S(foundSegs, 561f, "H_Fire", 1f, 0f, true); S(foundSegs, 565.9f, "H_Throw", 1f, 565.9f, false); S(foundSegs, 569.3f, "H_Ready", 1f, 0f, true); S(foundSegs, 572.4f, "H_Hold", 1f, 0f, true); S(foundSegs, 582f, "H_Ready", 1f, 0f, true); S(foundSegs, 588f, "H_Fire", 1f, 0f, true); S(foundSegs, 603f, "H_Dodge", 1f, 603f, false); S(foundSegs, 604.5f, "H_Fire", 1f, 0f, true); S(foundSegs, 613f, "H_Ready", 1f, 0f, true); S(foundSegs, 619f, "H_Crouch", 1f, 0f, true);
            S(foundSegs, 626f, "H_Hold", 1f, 0f, true); S(foundSegs, 631f, "H_Shrug", 1f, 631f, false); S(foundSegs, 636f, "H_Stagger", 1f, 636f, false); S(foundSegs, 638.5f, "H_Brace", 1f, 0f, true); S(foundSegs, 640f, "H_Hold", 1f, 0f, true); S(foundSegs, 652f, "H_Aim", 1f, 0f, true); S(foundSegs, 663.4f, "H_Jump", 1f, 663.4f, false);
            S(sloneSegs, 480f, "H_Smug", 1f, 0f, true); S(sloneSegs, 626f, "H_STalk", 1f, 0f, true); S(sloneSegs, 631f, "H_Smug", 1f, 0f, true); S(sloneSegs, 636f, "H_Panic", 1f, 0f, true); S(sloneSegs, 638f, "H_Crushed", 1f, 638f, false);
            jonesSegs.Sort((a, b) => a.T.CompareTo(b.T)); foundSegs.Sort((a, b) => a.T.CompareTo(b.T));
            Func<float, float, int, Vector3, float, Guard> G = (t0, t1, i, p, y) => new Guard { T0 = t0, T1 = t1, Idx = i, P = p, Yaw = y };
            guards.Add(G(516f, 540f, 0, Stair(0.025f) + new Vector3(0f, 0f, 2.2f), 0f)); guards.Add(G(516f, 540f, 1, Stair(0.04f) + new Vector3(0f, 0f, -2.0f), 0f)); guards.Add(G(517f, 540f, 2, Stair(0.055f) + new Vector3(0f, 0f, 1.5f), 0f)); guards.Add(G(518f, 540f, 3, Stair(0.07f) + new Vector3(0f, 0f, -1.8f), 0f));   // ON the first stairs, in front of you
            guards.Add(G(561f, 572.5f, 3, Stair(0.115f) + new Vector3(0f, 0f, 0f), 0f));
            guards.Add(G(582f, 588f, 0, Stair(0.26f) + Out(0.26f) * 0f, 0f)); guards.Add(G(582f, 588f, 1, Stair(0.275f), 0f)); guards.Add(G(583f, 588f, 2, Stair(0.29f), 0f));
            guards.Add(G(590f, 599f, 0, Stair(0.36f), 0f)); guards.Add(G(590f, 599f, 1, Stair(0.38f), 0f)); guards.Add(G(601f, 612f, 2, Stair(0.44f), 0f)); guards.Add(G(602f, 612f, 3, Stair(0.47f), 0f));
        }
        bool part2Dead;
        // the underground's models are loaded in WAVES while the first part plays (loading them all at build time crashed the game): cave, then one character per wave, then the small props
        System.Collections.IEnumerator LoadPart2()
        {
            var ld = AstronomicalEventMod.Inst.Loader; var steps = new[] { new[] { "cavern" }, new[] { "jones", "rifle" }, new[] { "foundation" }, new[] { "guard" }, new[] { "slone" }, new[] { "zeropoint", "crate" }, new[] { "boulder", "tbc", "mushroom" } };
            yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.3f : 3f);
            foreach (var st in steps)
            {
                foreach (var id in st) { if (part2Dead) yield break; yield return ld.LoadProp(def, id); yield return null; }
                if (part2Dead) yield break; SetupWave(st); GC.Collect(); yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.3f : 5f);
            }
            AstronomicalEventMod.Log.LogInfo("Collision: the underground is loaded");
        }
        void SetupWave(string[] ids)
        {
            foreach (var id in ids)
            {
                var m = def.Prop(id); if (m == null || m.Pivot == null) continue; m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false);
                switch (id)
                {
                    case "cavern": cavernM = m; Brighten(m, 1.25f); cavernR = CRig.Of(m, m.Pivot); break;
                    case "jones": jonesM = m; Brighten(m, 1.15f, true); jonesR = CRig.Of(m, m.Pivot); gunJ = FindT(m, "Gun"); break;
                    case "foundation": foundM = m; Brighten(m, 1.15f, true); foundR = CRig.Of(m, m.Pivot); gunF = FindT(m, "Gun"); break;
                    case "guard":
                        guardM = m; Brighten(m, 1.3f, true); m.Pivot.SetActive(false);
                        for (int i = 0; i < 8; i++)
                        {
                            var go = i == 0 ? m.Pivot : UnityEngine.Object.Instantiate(m.Pivot, root.transform); go.name = "CGuard" + i; go.SetActive(false); var ga = new GuardA { Go = go, Rig = CRig.Of(m, go), Off = i * 0.37f }; ga.Gun = null; foreach (var tt in go.GetComponentsInChildren<Transform>(true)) if (tt.name == "Gun") { ga.Gun = tt; break; }
                            if (rifleM != null && rifleM.Pivot != null) { var rf = UnityEngine.Object.Instantiate(rifleM.Pivot, root.transform); rf.name = "CGuardRifle" + i; rf.SetActive(false); ga.Rifle = rf; ga.RMod = rf.transform.Find("Model"); }
                            gA.Add(ga);
                        }
                        break;
                    case "slone": sloneM = m; Brighten(m, 1.15f, true); sloneR = CRig.Of(m, m.Pivot); break;
                    case "zeropoint": zpM = m; Brighten(m, 1.3f); zpR = CRig.Of(m, m.Pivot); foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Crystal")) crystals.Add(t); crystals.Sort((x, y) => string.CompareOrdinal(x.name, y.name)); break;
                    case "crate": crateM = m; Brighten(m, 1.2f); break;
                    case "boulder": boulderM = m; Brighten(m, 1.2f); break;
                    case "tbc": tbcM = m; Brighten(m, 1f); break;
                    case "mushroom": mushM = m; Brighten(m, 1.3f); mushR = CRig.Of(m, m.Pivot); break;
                    case "rifle":
                        rifleM = m; Brighten(m, 1.2f, true); rifleJ = m.Pivot; rifleF = UnityEngine.Object.Instantiate(m.Pivot, root.transform); rifleF.name = "CRifleF"; rifleJ.name = "CRifleJ"; rifleJ.SetActive(false); rifleF.SetActive(false); rmodJ = rifleJ.transform.Find("Model"); rmodF = rifleF.transform.Find("Model"); break;
                }
            }
        }
        readonly List<Transform> shafts = new List<Transform>(); GameObject mossDisc;
        void BuildShafts(Vector3 centre)
        {
            if (shafts.Count > 0) return; var m = kit.Glow(new Color(0.55f, 0.75f, 1f, 0.16f), beamTex, 2998);
            for (int i = 0; i < 6; i++) { var q = Quad("CShaft" + i, m); q.position = centre + root.transform.rotation * new Vector3(-6f + i * 3.4f, 7f, 9f + (i % 2) * 5f); q.rotation = root.transform.rotation * Quaternion.Euler(0f, 0f, -24f + i * 2f); q.localScale = new Vector3(3.2f, 26f, 1f); shafts.Add(q); }
            mossDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.Destroy(mossDisc.GetComponent<Collider>()); mossDisc.name = "CMoss"; mossDisc.transform.SetParent(root.transform, false); mossDisc.transform.position = centre + Vector3.down * 2.2f; mossDisc.transform.localScale = new Vector3(60f, 0.2f, 60f); mossDisc.GetComponent<Renderer>().sharedMaterial = kit.Flat(new Color(0.03f, 0.07f, 0.12f));
        }
        void CleanupModels2()
        {
            part2Dead = true; foreach (var ga in gA) { if (ga.Go != null && ga.Go != (guardM != null ? guardM.Pivot : null)) UnityEngine.Object.Destroy(ga.Go); if (ga.Rifle != null) UnityEngine.Object.Destroy(ga.Rifle); } gA.Clear(); if (guardM != null && guardM.Pivot != null) { guardM.Pivot.transform.SetParent(null, false); guardM.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(guardM.Pivot); } guardM = null;
            if (rifleF != null) UnityEngine.Object.Destroy(rifleF); rifleF = rifleJ = null; rifleM = null;
            foreach (var mm in new[] { jonesM, foundM, sloneM, cavernM, zpM, crateM, boulderM, tbcM, mushM }) if (mm != null && mm.Pivot != null) { foreach (var bc in mm.Pivot.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(bc); mm.Pivot.transform.SetParent(null, false); mm.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(mm.Pivot); }
            if (sloneTank != null) UnityEngine.Object.Destroy(sloneTank); sloneTank = null; foreach (var sh in shafts) if (sh != null) UnityEngine.Object.Destroy(sh.gameObject); shafts.Clear(); if (mossDisc != null) UnityEngine.Object.Destroy(mossDisc); mossDisc = null; jonesM = foundM = sloneM = cavernM = zpM = crateM = boulderM = tbcM = mushM = null; jonesSegs.Clear(); foundSegs.Clear(); sloneSegs.Clear(); guards.Clear(); crystals.Clear(); underOn = false;
        }
        void ExitUnder()
        {   // scrubbed back before 8:00: the first part's world again
            underOn = false; seatedUnder = seatedRed = seatedMush = false; foreach (var mm in new[] { jonesM, foundM, sloneM, cavernM, zpM, crateM, boulderM, tbcM, mushM }) if (mm != null && mm.Pivot != null) mm.Pivot.SetActive(false); if (sloneTank != null) sloneTank.SetActive(false);
            if (islandM != null) islandM.Pivot.SetActive(true); if (colM != null) { colM.Pivot.SetActive(true); colM.Pivot.transform.localScale = Vector3.one * COLLIDER_S; PlaceByFileOrigin(colM, W(new Vector3(0f, GroundY(0f, COLLIDER_Z), COLLIDER_Z))); } if (cabM != null) cabM.Pivot.SetActive(true); if (mechM != null) mechM.Pivot.SetActive(true); cabHave = false; seatedUnder = false;
        }
        float lastCT;
        static Transform FindT(GltfModel m, string name) { if (m == null || m.Pivot == null) return null; foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
        // plays the segment clip active at t (crossfaded from the previous one); with loco, the character walks / runs by DISTANCE s (so the feet never slide) and blends with the segment clip while it speeds up / slows down (v = speed, m/s)
        string PlaySegs(CRig rig, GltfModel m, List<Seg> segs, float t, float s = 0f, float v = 0f, bool loco = false)
        {
            if (rig == null || m == null || segs.Count == 0) return null;
            int k = 0; for (int i = 0; i < segs.Count; i++) if (segs[i].T <= t) k = i; var sg = segs[k]; var a = Clip(m, sg.Clip); float ta = sg.Loop ? (t - sg.T) * sg.Speed + sg.Off : t - sg.Off; lastCT = ta; float mw = loco ? Sm(0.15f, 0.7f, v) : 0f;
            if (mw > 0.001f)
            {
                var cw = Clip(m, "H_Walk"); var cr = Clip(m, "H_Run"); float tw = s / 1.5f, tr = s / 3.2f * 0.75f;
                if (v < 2.7f) { if (mw >= 0.999f) rig.Pose(cw, tw); else rig.Pose(a, ta, cw, tw, mw); lastCT = tw; return mw > 0.5f ? "H_Walk" : sg.Clip; }
                if (v < 3.4f) { rig.Pose(cw, tw, cr, tr, Sm(2.7f, 3.4f, v)); lastCT = tw; return "H_Walk"; }
                rig.Pose(cr, tr); lastCT = tr; return "H_Run";
            }
            float w = 0f; GltfModel.Clip pc = null; float tpv = 0f;
            if (k > 0) { var p = segs[k - 1]; pc = Clip(m, p.Clip); tpv = p.Loop ? (t - p.T) * p.Speed + p.Off : t - p.Off; w = 1f - Sm(0f, 0.6f, t - sg.T); }
            if (pc != null && w > 0.001f) rig.Pose(a, ta, pc, tpv, w); else rig.Pose(a, ta);
            return sg.Clip;
        }
        // the SCAR: glued to the clips' Gun node (so the hands grip it), muzzle flash on the Fire clips' shots (a burst of nine every 2.4 s of clip time, see tools/anim_human2.py recoil())
        void Rifle(GameObject rifle, Transform rmod, Transform gun, float gs, Transform body, string clip, float ct, float dt, ref int shot, Vector3 target)
        {
            if (rifle == null) return; bool show = gun != null && body != null && body.gameObject.activeInHierarchy && clip != null; if (rifle.activeSelf != show) rifle.SetActive(show); if (!show) return;
            float ks = gs * Mathf.Abs(gun.lossyScale.x); var tr = rifle.transform; tr.rotation = gun.rotation; tr.localScale = Vector3.one * ks; if (rmod != null) tr.position += gun.position - rmod.position;
            bool fire = clip == "H_Fire" || clip == "H_FireK"; float ph = Mathf.Repeat(ct, 2.4f); int idx = fire && ph < 0.9f ? (int)(ph / 0.1f) : -1;
            if (idx < 0) { shot = -1; return; }
            if (idx == shot || paused || dt <= 0f) return; shot = idx; Vector3 mz = gun.position + gun.rotation * new Vector3(0f, 0.12f * ks, 0.77f * ks), dir = (target - mz).normalized;
            for (int k = 0; k < 6; k++) Emit(fx, mz, gun.rotation * (new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(4f, 12f))), new Color(1f, UnityEngine.Random.Range(0.7f, 0.95f), 0.4f, 0.9f), UnityEngine.Random.Range(0.12f, 0.32f), UnityEngine.Random.Range(0.08f, 0.18f));
            Emit(fx, mz, Vector3.zero, new Color(1f, 0.9f, 0.5f, 1f), 0.9f, 0.05f); Emit(fx, mz + dir * 0.8f, dir * 150f + UnityEngine.Random.insideUnitSphere * 1.2f, new Color(1f, 0.85f, 0.4f, 1f), 0.3f, Mathf.Clamp((target - mz).magnitude / 150f, 0.1f, 1.2f));
        }
        // what the shooters aim at: the nearest live guard, else a point ahead on the path
        Vector3 FireTarget(float t, Vector3 from)
        {
            Vector3 best = UG(from + new Vector3(0f, 2f, 0f)) + root.transform.rotation * new Vector3(0f, 0f, 30f); float bd = 1e9f;
            foreach (var g in guards) if (t >= g.T0 && t < g.T1) { float d = (g.P - from).sqrMagnitude; if (d < bd) { bd = d; best = UG(g.P + Vector3.up * 3f); } }
            return best;
        }
        void Place(GltfModel m, Vector3 worldPos, float yawLocal, float scale, bool on) { if (m == null || m.Pivot == null) return; if (m.Pivot.activeSelf != on) m.Pivot.SetActive(on); if (!on) return; var tr = m.Pivot.transform; tr.position = worldPos; tr.rotation = root.transform.rotation * Quaternion.Euler(0f, yawLocal, 0f); tr.localScale = Vector3.one * scale; }
        int reseatTries; float lastReseat = -9f, anchYaw;
        void Reseat(Vector3 headWorld)
        {
            try { NoClip(true); var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Vector3 delta = headWorld - pl.headCollider.transform.position; rb.position += delta; rb.linearVelocity = Vector3.zero; cabPrev = headWorld; cabHave = true; lastReseat = Time.unscaledTime; AstronomicalEventMod.Log.LogInfo("Collision: seated you at " + headWorld + " (moved " + delta.magnitude.ToString("0") + " m)"); } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Collision: could not seat you: " + e.Message); }
        }
        // if the game threw you back (out-of-world respawn) the seat is taken again
        void CheckSeat(Vector3 headTarget) { if (reseatTries < 10 && Time.unscaledTime - lastReseat > 0.7f && Vector3.Distance(HeadNow(), headTarget) > 25f) { reseatTries++; AstronomicalEventMod.Log.LogWarning("Collision: you were " + Vector3.Distance(HeadNow(), headTarget).ToString("0") + " m from your seat; seating you again (" + reseatTries + ")"); Reseat(headTarget); } }
        void Black(float a, Camera cam) { if (flashGo.activeSelf != a > 0.003f) flashGo.SetActive(a > 0.003f); if (a > 0.003f) { flashMat.color = new Color(0f, 0f, 0f, a); flashGo.transform.position = cam != null ? cam.transform.position : HeadNow(); } }
        Vector3 HeadNow() { try { return GTPlayer.Instance.headCollider.transform.position; } catch { } return originW; }
        void SkyRed(float t) { if (skyMesh == null) return; float wob = 0.5f + 0.5f * Mathf.Sin(t * 0.7f); for (int k = 0; k < skyN; k++) { var d = skyDirs[k]; float up = Mathf.Clamp01(d.y * 0.5f + 0.5f); var c = Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(0.55f, 0.15f, 0.45f), up); c.g += 0.12f * Mathf.Sin(d.x * 6f + t) * Mathf.Sin(d.z * 5f - t * 0.7f); c = Color.Lerp(c, new Color(1f, 0.55f, 0.35f), 0.2f * wob * (1f - up)); c.a = 1f; skyCols[k] = c; } skyMesh.colors = skyCols; var sr = stars.GetComponent<ParticleSystemRenderer>(); if (sr != null) sr.enabled = false; }
        void SkyNight(float t) { if (skyMesh == null) return; for (int k = 0; k < skyN; k++) { var d = skyDirs[k]; float up = Mathf.Clamp01(d.y); var c = Color.Lerp(new Color(0.02f, 0.03f, 0.09f), new Color(0.0f, 0.0f, 0.02f), up); c.a = 1f; skyCols[k] = c; } skyMesh.colors = skyCols; var sr = stars.GetComponent<ParticleSystemRenderer>(); if (sr != null) sr.enabled = true; }

        void ApplyAfter(float t, float dt)
        {
            var cam = Camera.main; bool back = t < lastT - 0.4f; float tp = lastT < 0f ? t : lastT; lastT = t; if (cam != null) dome.transform.position = cam.transform.position;
            if (!underOn || back) { underOn = true; guardBoom.Clear(); ceBoom = crateBoom = boulderBoom = tankBoom = collapseBoom = false; for (int i = 0; i < 4; i++) cryBroke[i] = false; seatedUnder = seatedRed = seatedMush = false; cabHave = false; reseatTries = 0;
                // the first part's world goes away
                foreach (var mm in new[] { islandM, moonM, cabM, swordM, originM }) if (mm != null && mm.Pivot != null) mm.Pivot.SetActive(false); foreach (var l in new[] { tanks, domes, rocks }) foreach (var a in l) a.Go.SetActive(false); foreach (var a in brutes) a.Go.SetActive(false); cannonGlow.gameObject.SetActive(false); tracer.enabled = tracerCore.enabled = bolt.enabled = boltCore.enabled = false; engineLamp.intensity = 0f; }
            if (noclipped.Count > 0 && Time.unscaledTime - noclipT > 0.6f) NoClip(false);
            float blk = 0f; Vector3 anchor = Vector3.zero; Quaternion aq = Quaternion.identity; float hx = 2.5f, zMin = -3f, zMax = 3f;
            bool phaseA = t < T_RED, phaseB = t >= T_RED && t < T_DARK + 16f, phaseC = t >= T_DARK + 16f;
            // ================= the underground (8:00 - 11:45) =================
            if (phaseA)
            {
                bool cav = cavernM != null && cavernM.Pivot != null; if (cav) { if (!cavernM.Pivot.activeSelf) cavernM.Pivot.SetActive(true); PlaceByFileOrigin(cavernM, W(UG0)); if (cavernM.Clips.Count > 0) cavernR.Pose(cavernM.Clips[0], t); }
                float q = QAt(t), q2 = QAt(t - 0.25f); bool moving = Mathf.Abs(q - q2) > 0.0002f; Vector3 pp = PathAt(q); float yaw0 = Yaw(Mathf.Min(q, 0.99f)); anchYaw = Mathf.LerpAngle(anchYaw, yaw0, Mathf.Clamp01(dt * 3f + 0.01f)); Quaternion pq = Quaternion.Euler(0f, anchYaw, 0f);
                // the hand carries you from 10:44; before that you walk the stairs behind Jones
                float handK = Sm(644f, 652f, t) * (1f - 0f); Vector3 handHead = Vector3.zero; bool useHand = t >= 644f && mechM != null && handRT != null;
                Vector3 walkHead = UG(pp + Vector3.up * 1.6f);
                // ---- the mech (10:20 on, 75 m tall): its hand sweeps in beside the stairs, from 10:44 the body stands still and the arm carries you on the open PALM (tools/anim_rig.py: palm up, M_Hand / M_HandUp / M_HandIn)
                bool mechIn = mechM != null && mechM.Pivot != null && handLT != null && t >= 622f && t < T_RED - 0.5f; if (mechM != null && mechM.Pivot.activeSelf != mechIn) mechM.Pivot.SetActive(mechIn);
                if (mechIn)
                {
                    Vector3 Sd = Stair(Q_END), dn = Stair(Q_END + 0.003f) - Sd; dn.y = 0f; dn.Normalize(); Vector3 sideV = Vector3.Cross(Vector3.up, dn); Vector3 Hside = Sd + sideV * 16.5f + Vector3.down * 0.4f, Hrest = Hside + sideV * 55f + Vector3.up * 30f, Hslam = L4() + Vector3.up * 3f, hd;
                    if (t < 635.2f) hd = Vector3.Lerp(Hrest + sideV * 30f + Vector3.up * 20f, Hrest, Sm(622f, 630f, t)); else if (t < 636.1f) hd = Vector3.Lerp(Hrest, Hslam, Sm(635.2f, 636f, t)); else hd = Vector3.Lerp(Hslam, Hside, Sm(636.1f, 644f, t));
                    string c; float off = 0f, spd = 1f; bool loop = true; if (t < 635.2f) c = "M_Idle2"; else if (t < 639f) { c = "M_SwordSlash"; off = 635.2f; loop = false; } else if (t < 664f) { c = "M_Hand"; off = 639f; loop = false; } else if (t < 689f) { c = "M_HandUp"; off = 664f; spd = 0.75f; loop = false; } else if (t < 705f) { c = "M_HandIn"; off = 689f; spd = 0.5f; loop = false; } else c = "M_Brace";
                    var cl = Clip(mechM, c); float tc = loop ? t : Mathf.Min((t - off) * spd, (cl != null ? cl.Duration : 8f) - 0.03f); var pt = mechM.Pivot.transform; pt.rotation = root.transform.rotation * Quaternion.Euler(0f, Mathf.Atan2(-Hside.x, -Hside.z) * Mathf.Rad2Deg, 0f); pt.localScale = Vector3.one * MECH_S2; pt.position = UG(Hside + sideV * 60f + Vector3.down * 40f);
                    if (t < 644f) { mechR.Pose(cl, tc); pt.position += UG(hd) - handLT.TransformPoint(PALM); }
                    else { mechR.Pose(Clip(mechM, "M_Hand"), 5f); pt.position += UG(Hside) - handLT.TransformPoint(PALM); mechR.Pose(cl, tc); }
                    MechAlive(t, false); if (useHand) handHead = handLT.TransformPoint(PALM) + Vector3.up * 1.7f;
                }
                Vector3 headTarget = walkHead; if (useHand && handHead != Vector3.zero) headTarget = Vector3.Lerp(walkHead, handHead, handK);
                // ---- carry: first seat you (teleport at 8:01 during the black), then carry
                if (!seatedUnder && t >= T_BLACK + 1f) { Reseat(headTarget); seatedUnder = true; }
                if (seatedUnder) CheckSeat(headTarget); if (seatedUnder && dt > 0f) Carry(headTarget, useHand ? Quaternion.identity : pq, dt, useHand ? 2.2f : 2.6f, useHand ? -2.2f : -3f, useHand ? 2.2f : 3f, 0f);
                // ---- Jones and the Foundation (and the cavern's lights, the Zero Point)
                float lead = Mathf.Lerp(2.6f, 8f, Sm(498f, 506f, t)), leadB = Mathf.Lerp(2.6f, 8f, Sm(498f, 506f, t - 0.25f)); float jq = q + DQ(lead, q), fq = q + DQ(4f, q); float jyaw = Yaw(Mathf.Clamp(jq, -1f, 0.99f)); jyaw = Mathf.LerpAngle(anchYaw, jyaw, 0.5f); if (t < 498f) jyaw = Mathf.LerpAngle(jyaw + 180f, jyaw, Sm(496.2f, 497.8f, t)); Vector3 jp = PathAt(jq), fp = PathAt(fq);
                Vector3 lat = Quaternion.Euler(0f, jyaw, 0f) * Vector3.right; jp += lat * 1.2f; fp -= lat * 1.6f;
                float qb = QAt(t - 0.25f); float sj = ArcLen(jq), sf = ArcLen(fq), vj = Mathf.Max(0f, (sj - ArcLen(qb + DQ(leadB, qb))) / 0.25f), vf = Mathf.Max(0f, (sf - ArcLen(qb + DQ(4f, qb))) / 0.25f);
                // from 10:50 they face the Zero Point (guns up), at 11:03 they jump into it
                Vector3 zdir = ZPC - Stair(SLONE_S); float zyaw = Mathf.Atan2(zdir.x, zdir.z) * Mathf.Rad2Deg; float zface = Sm(646f, 652f, t); jyaw = Mathf.LerpAngle(jyaw, zyaw, zface);
                float jk = Mathf.Clamp01((t - 663.3f) / 4.5f); Vector3 jleap = Vector3.Lerp(jp, ZPC - zdir.normalized * 6f, jk * jk) + Vector3.up * (20f * Mathf.Sin(jk * Mathf.PI)), fleap = Vector3.Lerp(fp, ZPC - zdir.normalized * 6f + lat * 3f, jk * jk) + Vector3.up * (16f * Mathf.Sin(jk * Mathf.PI)); if (t >= 663.3f) { jp = jleap; fp = fleap; }
                bool jon = jonesM != null && t < 668.2f; Place(jonesM, UG(jp + Vector3.down * 0.09f), jyaw, 1.0f, jon && t >= 480f); string jc = null; if (jon && jonesM.Pivot.activeSelf) jc = PlaySegs(jonesR, jonesM, jonesSegs, t, sj, vj, t < 640f); Rifle(rifleJ, rmodJ, gunJ, GS_J, jonesM != null ? jonesM.Pivot.transform : null, jon ? jc : null, lastCT, dt, ref shotJ, FireTarget(t, jp));
                Vector3 fpos = fp; if (t < 540f) fpos = fp + Vector3.up * (t < 538f ? 70f : 70f - 0.5f * 35f * (t - 538f) * (t - 538f));
                bool fon = foundM != null && t >= 538f && t < 668.2f; Place(foundM, UG(fpos + Vector3.down * 0.09f), jyaw, 1.0f, fon); string fc = null; if (fon) fc = PlaySegs(foundR, foundM, foundSegs, t, sf, vf, t >= 546f && t < 640f); Rifle(rifleF, rmodF, gunF, GS_F, foundM != null ? foundM.Pivot.transform : null, fon ? fc : null, lastCT, dt, ref shotF, FireTarget(t, fp));
                if (!ceBoom && t >= 540f && t < 542f && !paused) { ceBoom = true; Boom(UG(Stair(0.05f) + Vector3.up * 2f), 11f, 1.6f); shake = Mathf.Max(shake, 1.4f); }
                // ---- the I.O. patrol guards (clips H_Fire / H_FireK / H_Death): they shoot at you until their time, then fall
                for (int bi = 0; bi < gA.Count; bi++) { gA[bi].Go.SetActive(false); if (gA[bi].Rifle != null) gA[bi].Rifle.SetActive(false); }
                for (int gi = 0; gi < guards.Count; gi++)
                {
                    var g = guards[gi]; if (g.Idx >= gA.Count) continue; var ga = gA[g.Idx]; bool alive = t >= g.T0 && t < g.T1, dying = t >= g.T1 && t < g.T1 + 5f; if (!(alive || dying)) continue;
                    if (t >= g.T1 && !guardBoom.Contains(gi) && !paused) { guardBoom.Add(gi); Boom(UG(g.P + Vector3.up * 3f), 5f, 0.5f); }
                    Vector3 toP = (pp + Vector3.up * 1.6f) - g.P; float gy = Mathf.Atan2(toP.x, toP.z) * Mathf.Rad2Deg; var go = ga.Go; go.SetActive(true); go.transform.position = UG(g.P + Vector3.down * 0.09f); go.transform.rotation = root.transform.rotation * Quaternion.Euler(0f, gy, 0f); go.transform.localScale = Vector3.one;
                    string c = alive ? (gi % 3 == 1 ? "H_FireK" : "H_Fire") : "H_Death"; float ct = alive ? t + ga.Off : Mathf.Min(8f, t - g.T1); ga.Rig.Pose(Clip(guardM, c), ct);
                    Vector3 aimAt = UG(pp + Vector3.up * 1.6f + new Vector3(Mathf.Sin(t * 3f + gi) * 4f, Mathf.Sin(t * 2f + gi * 2f) * 2f, 0f)); if (ga.Gun != null) Rifle(ga.Rifle, ga.RMod, ga.Gun, GS_G, go.transform, alive ? c : null, ct, dt, ref ga.Shot, aimAt);
                }
                // ---- the boulder (9:12): the Foundation shoves it off the stairs
                bool bon = boulderM != null && t >= 480f && t < 560f; float bq = 0.06f + DQ(7f, 0.06f); Vector3 bP = PathAt(bq), bD = PathAt(bq + 0.003f) - bP; bD.y = 0f; bD.Normalize(); Vector3 bSide = Vector3.Cross(Vector3.up, bD); Vector3 bp0 = bP; float bpush = Sm(551.8f, 552.7f, t); float bfall = Mathf.Max(0f, t - 552.7f); Vector3 bpos = bp0 + bSide * (bpush * 8f) + new Vector3(0f, -0.5f * 30f * bfall * bfall, 0f); bon &= bpos.y > 1.0f;
                Place(boulderM, UG(bpos), t * 20f * Sm(551.8f, 552.2f, t), 3.2f, bon); if (!boulderBoom && t >= 552.7f && bpos.y <= 2.5f && !paused) { boulderBoom = true; Boom(UG(new Vector3(bpos.x, 2f, bpos.z)), 7f, 0.8f); }
                // ---- the crate bomb (9:30): thrown at the brute, who is flung into the pillar and explodes
                bool con = crateM != null && t >= 566f && t < 572f; Vector3 cstart = fp + Vector3.up * 1.4f, cend = Stair(0.115f) + Vector3.up * 3f; float ck = Mathf.Clamp01((t - 568f) / 1.6f); Vector3 cpos = Vector3.Lerp(cstart, cend, ck) + Vector3.up * (4f * Mathf.Sin(ck * Mathf.PI));
                Place(crateM, UG(cpos), t * 120f, 1.6f, con && t < 570.2f); if (guards.Count > 4 && t >= 570.2f && t < 572.5f && gA.Count > 3) { var a = gA[3]; a.Go.SetActive(true); Vector3 bp = Vector3.Lerp(Stair(0.115f), new Vector3(0f, Stair(0.115f).y + 4f, 0f), Sm(570.2f, 572.4f, t)) + Vector3.up * 3f; a.Go.transform.position = UG(bp); a.Go.transform.rotation = root.transform.rotation * Quaternion.Euler(t * 90f, 0f, 20f); a.Rig.Pose(Clip(guardM, "H_Stagger"), t - 570.2f); if (a.Rifle != null) a.Rifle.SetActive(false); }
                if (!crateBoom && t >= 572.4f && t < 574f && !paused) { crateBoom = true; Boom(UG(new Vector3(0f, Stair(0.115f).y + 6f, 0f)), 14f, 1.8f); shake = Mathf.Max(shake, 1.8f); }
                // ---- Slone in her tank on the fourth landing (10:19 on): talks, then the mech's fist hits the tank (10:36)
                bool son = sloneM != null && sloneTank != null && t >= 612f && t < 650f; Vector3 tpos = new Vector3(60f, Stair(Q_END).y + 0.2f, 61f); float tf = Mathf.Max(0f, t - 636f); Vector3 tOut = Out(SLONE_S); float thead = Yaw(Q_END); if (t >= 636f) tpos += tOut * (14f * tf) + new Vector3(0f, -0.5f * 30f * tf * tf, 0f); bool tankOn = son && tpos.y > -30f;
                if (sloneTank != null) { sloneTank.SetActive(tankOn); if (tankOn) { var tt = sloneTank.transform; tt.position = UG(tpos); tt.rotation = root.transform.rotation * Quaternion.Euler(0f, thead, t >= 636f ? tf * 40f : 0f); tt.localScale = Vector3.one * 2f; var tc = Clip(tankM, "Animation"); if (tc != null) CRig.Of(tankM, sloneTank).Pose(tc, t); } }
                Place(sloneM, UG(tpos + Vector3.up * 3.9f), thead + 180f, 1.3f, tankOn); if (tankOn) PlaySegs(sloneR, sloneM, sloneSegs, t);
                if (!tankBoom && t >= 636f && t < 640f && !paused) { tankBoom = true; Boom(UG(L4() + Vector3.up * 5f), 16f, 2.2f); shake = Mathf.Max(shake, 2.5f); }
                // ---- the Zero Point (always there), its four crystals break at 11:17, 11:21, 11:25, 11:29, then it sinks; the Collider (the device) topples
                bool zon = zpM != null; Vector3 zpos = ZPC; float sink = Mathf.Max(0f, t - 689f); zpos.y -= 0.5f * 7f * sink * sink; zon &= zpos.y > -40f; Place(zpM, UG(zpos), t * 6f, ZPR, zon); if (zon) PlaceByFileOrigin(zpM, UG(zpos)); if (zon && zpM.Clips.Count > 0) zpR.Pose(zpM.Clips[0], t);
                for (int ci = 0; ci < crystals.Count && ci < 4; ci++) { float bt = 677f + ci * 4f; bool broke = t >= bt; if (crystals[ci] != null) crystals[ci].gameObject.SetActive(!broke); if (broke && !cryBroke[ci] && !paused) { cryBroke[ci] = true; Boom(UG(zpos + new Vector3(Mathf.Cos(ci * 1.57f + 0.8f) * 12f, ZPR * 1.4f, Mathf.Sin(ci * 1.57f + 0.8f) * 12f)), 6f, 0.9f); } if (!broke && crystals[ci] != null && false) { } }
                if (colM != null && colM.Pivot != null)
                {
                    bool colOn = t < 705f; colM.Pivot.SetActive(colOn); if (colOn) { float fall = Mathf.Max(0f, t - 692f); var ct = colM.Pivot.transform; colM.Pivot.transform.localScale = Vector3.one * 2f; ct.rotation = root.transform.rotation * Quaternion.Euler(Mathf.Min(85f, 3.2f * fall * fall), 0f, 3f * Mathf.Sin(t * 30f) * Sm(689f, 692f, t) * (1f - Sm(700f, 705f, t))); PlaceByFileOrigin(colM, UG(new Vector3(0f, -Mathf.Max(0f, fall - 5f) * Mathf.Max(0f, fall - 5f) * 6f, 0f))); if (colM.Clips.Count > 0) colR.Pose(colM.Clips[0], t); }
                    if (!collapseBoom && t >= 689f && !paused) { collapseBoom = true; shake = Mathf.Max(shake, 3f); }
                    if (t >= 689f && t < 705f && !paused && dt > 0f) { cloudAcc += 120f * Parts * dt; int n = (int)cloudAcc; cloudAcc -= n; for (int k = 0; k < n; k++) Emit(dust, UG(new Vector3(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(60f, 200f), UnityEngine.Random.Range(-40f, 40f))), new Vector3(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(-20f, 5f), UnityEngine.Random.Range(-8f, 8f)), new Color(0.6f, 0.4f, 0.5f, 0.5f), UnityEngine.Random.Range(10f, 30f), UnityEngine.Random.Range(2f, 5f)); if (((int)(t * 3f)) % 3 == 0 && ((int)(t * 3f)) != ((int)(tp * 3f))) { Boom(UG(new Vector3(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(60f, 200f), UnityEngine.Random.Range(-30f, 30f))), 12f, 1.3f); } }
                }
                // the black cuts: 8:00 -> 8:13, the clamp at 11:45
                blk = t < T_UNDER ? 1f : 1f - Sm(T_UNDER, T_UNDER + 3f, t); if (t >= 703.5f) blk = Mathf.Max(blk, Sm(703.5f, 705f, t));
                // light and air: warm work lamps near you, the Zero Point's purple light over everything, embers and haze, the Zero Point turning from teal (9:39) to purple (10:26)
                ugWarm.transform.position = walkHead + Vector3.up * 3f + pq * new Vector3(0f, 0f, 5f); ugWarm.intensity = Lit * (2.2f + 0.4f * Mathf.Sin(t * 7f)); ugPurple.transform.position = UG(ZPC); ugPurple.intensity = Lit * 4.5f * (t < 689f ? 1f : 1f + 3f * Mathf.Abs(Mathf.Sin(t * 9f)) * Sm(689f, 691f, t));
                if (zpM != null) { Color zt = Color.Lerp(new Color(0.45f, 1f, 0.95f), new Color(1f, 0.65f, 1f), Sm(600f, 630f, t)); foreach (var r in zpM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var mt in r.sharedMaterials) if (mt != null && !mt.name.StartsWith("A_")) mt.color = zt; }
                if (!paused && dt > 0f) { cloudAcc += 40f * Parts * dt; int n = (int)cloudAcc; cloudAcc -= n; for (int k = 0; k < n; k++) { Vector3 ep0 = walkHead + new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-3f, 10f), UnityEngine.Random.Range(-14f, 14f)); if (UnityEngine.Random.value < 0.6f) Emit(fx, ep0, new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(1f, 3f), UnityEngine.Random.Range(-0.5f, 0.5f)), new Color(1f, 0.6f, 0.2f, 0.8f), UnityEngine.Random.Range(0.1f, 0.25f), UnityEngine.Random.Range(3f, 6f)); else Emit(dust, ep0 + Vector3.down * 3f, UnityEngine.Random.insideUnitSphere * 0.6f, new Color(0.45f, 0.35f, 0.65f, 0.16f), UnityEngine.Random.Range(8f, 20f), UnityEngine.Random.Range(4f, 8f)); } }
                SkyNight(t); RenderSettings.ambientLight = new Color(0.42f, 0.34f, 0.56f); sun.color = new Color(0.75f, 0.6f, 1f); sun.intensity = 1.0f;
            }
            // ================= the red sky: Jones and the Foundation fall (11:51 - 12:07) =================
            else if (phaseB)
            {
                if (rifleJ != null) rifleJ.SetActive(false); if (rifleF != null) rifleF.SetActive(false); ugWarm.intensity = 0f; ugPurple.intensity = 0f;
                if (cavernM != null) cavernM.Pivot.SetActive(false); if (mechM != null) mechM.Pivot.SetActive(false); if (colM != null) colM.Pivot.SetActive(false); if (zpM != null) zpM.Pivot.SetActive(false); if (sloneTank != null) sloneTank.SetActive(false); if (sloneM != null) sloneM.Pivot.SetActive(false);
                Vector3 ah = W(FF0 + new Vector3(0f, 0f, 0f)); if (!seatedRed && t >= T_RED + 1f) { Reseat(ah); seatedRed = true; } if (seatedRed && dt > 0f) Carry(ah, root.transform.rotation, dt, 0.8f, -0.8f, 0.8f, 0f);
                bool vis = t >= 711f && t < 727.5f; float bump = Sm(716f, 721f, t) * (1f - Sm(723f, 726f, t)); float jx = Mathf.Lerp(-2.6f, -0.75f, bump), fx2 = Mathf.Lerp(2.6f, 0.75f, bump); float wob = 1f - bump; bool bumping = t >= 716f;
                Place(jonesM, ah + root.transform.rotation * new Vector3(jx, -0.55f + 0.2f * wob * Mathf.Sin(t * 1.1f), 3.4f), 0f, 1f, vis); if (vis) { jonesM.Pivot.transform.rotation = root.transform.rotation * Quaternion.Euler(70f + 5f * Mathf.Sin(t * 0.9f), 0f, 6f * wob * Mathf.Sin(t * 0.7f)); jonesR.Pose(bumping ? Clip(jonesM, "H_FallBumpR") : Clip(jonesM, "H_Fall"), bumping ? t - 716f : t); }
                Place(foundM, ah + root.transform.rotation * new Vector3(fx2, -0.55f + 0.2f * wob * Mathf.Sin(t * 1.3f + 1f), 3.4f), 0f, 1f, vis); if (vis) { foundM.Pivot.transform.rotation = root.transform.rotation * Quaternion.Euler(70f + 5f * Mathf.Sin(t * 1.1f + 1f), 0f, -6f * wob * Mathf.Sin(t * 0.8f)); foundR.Pose(bumping ? Clip(foundM, "H_FallBumpL") : Clip(foundM, "H_Fall"), bumping ? t - 716f : t + 1.7f); }
                if (!paused && dt > 0f && t >= T_DARK) { cloudAcc += 160f * Parts * dt; int n = (int)cloudAcc; cloudAcc -= n; for (int k = 0; k < n; k++) Emit(clouds, ah + new Vector3(UnityEngine.Random.Range(-60f, 60f), -70f, UnityEngine.Random.Range(-30f, 90f)), new Vector3(0f, UnityEngine.Random.Range(55f, 110f), 0f), Color.Lerp(new Color(1f, 0.55f, 0.5f, 0.35f), new Color(0.8f, 0.35f, 0.65f, 0.35f), UnityEngine.Random.value), UnityEngine.Random.Range(14f, 44f), 2.5f); }
                blk = t < T_DARK ? 1f : 1f - Sm(T_DARK, T_DARK + 3f, t); if (t >= 725.5f) blk = Mathf.Max(blk, Sm(725.5f, 727f, t)); if (t >= 727f) blk = 1f; SkyRed(t); RenderSettings.ambientLight = new Color(1f, 0.6f, 0.55f) * 0.9f; sun.color = new Color(1f, 0.55f, 0.45f); sun.intensity = 1.2f;
            }
            // ================= Geno's realm and TO BE CONTINUED (12:15) =================
            else
            {
                ugWarm.intensity = 0f; ugPurple.intensity = 0f; if (jonesM != null) jonesM.Pivot.SetActive(false); if (foundM != null) foundM.Pivot.SetActive(false); Vector3 ah = W(MU0); if (!seatedMush) { Reseat(ah); seatedMush = true; } if (dt > 0f) Carry(ah, root.transform.rotation, dt, 1.5f, -1.5f, 1.5f, 0f);
                BuildShafts(ah); foreach (var sh in shafts) sh.gameObject.SetActive(t >= 733f); if (mossDisc != null) mossDisc.SetActive(true); bool mon = mushM != null && t >= 733f; Place(mushM, ah + root.transform.rotation * new Vector3(-2.4f, -1.7f, 6f), 160f, 1.2f, mon); if (mon && mushM.Clips.Count > 0) mushR.Pose(mushM.Clips[0], t);
                bool ton = tbcM != null && t >= T_TBC; Place(tbcM, ah + root.transform.rotation * new Vector3(1.7f, -1.1f, 4.5f), 180f, 0.45f, ton);
                if (!paused && dt > 0f && t >= 733f) { cloudAcc += 30f * Parts * dt; int n = (int)cloudAcc; cloudAcc -= n; for (int k = 0; k < n; k++) Emit(clouds, ah + new Vector3(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-2f, 4f), UnityEngine.Random.Range(1f, 14f)), UnityEngine.Random.insideUnitSphere * 0.2f, new Color(0.5f, 0.9f, 1f, 0.8f), UnityEngine.Random.Range(0.1f, 0.3f), UnityEngine.Random.Range(3f, 6f)); }
                blk = 1f - Sm(733f, 735.5f, t) * 1f; if (t < 733f) blk = 1f; SkyNight(t); RenderSettings.ambientLight = new Color(0.25f, 0.35f, 0.6f); sun.color = new Color(0.5f, 0.75f, 1f); sun.intensity = 0.7f;
            }
            Black(blk, cam); flashLamp.intensity = Mathf.Max(0f, flashLamp.intensity - dt * 20f * Lit);
            if (!paused && dt > 0f) { rumbleT += dt; if (rumbleT > 0.12f) { rumbleT = 0f; float v = Mathf.Max(shake * 0.4f, (t >= 689f && t < 705f) ? 0.6f : 0f); if (v > 0.05f) { try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, Mathf.Clamp01(v), 0.14f); gt.StartVibration(false, Mathf.Clamp01(v), 0.14f); } } catch { } } } }
            shake = Mathf.Max(0f, shake - dt * 1.4f); if (t - skyT > 0.1f || t < skyT) { skyT = t; }
        }
    }
}
