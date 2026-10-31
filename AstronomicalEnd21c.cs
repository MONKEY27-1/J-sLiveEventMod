using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // THE END part 2: the cast and their scripts (times are the video's: m:ss -> seconds) plus the one-shot effects of each scene.
    internal sealed partial class AstronomicalEnd21
    {
        static readonly float[][] HZ = { new[] { 332f, 2.0f }, new[] { 342.6f, 12.4f }, new[] { 350.0f, 12.4f }, new[] { 356.0f, 16.5f } };    // where you are in the hall (z), piecewise linear
        static float HallZ(float t)
        {
            if (t <= HZ[0][0]) return HZ[0][1]; for (int i = 1; i < HZ.Length; i++) if (t < HZ[i][0]) return Mathf.Lerp(HZ[i - 1][1], HZ[i][1], (t - HZ[i - 1][0]) / (HZ[i][0] - HZ[i - 1][0])); return HZ[HZ.Length - 1][1];
        }
        Vector3 AimAt(Act from, string prefix, float up)
        {
            Act best = null; float bd = 1e9f;
            foreach (var a in acts) if (a != from && a.Go != null && a.Name.StartsWith(prefix) && a.Go.activeSelf && a.Cur != "Death") { float d = (a.Go.transform.position - from.Go.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = a; } }
            return best != null ? best.Go.transform.position + Vector3.up * up : from.Go.transform.position + from.Go.transform.forward * 20f + Vector3.up * up;
        }
        Vector3 LeaderPos(float t, float sOff, float lat)
        {
            if (bPts.Count < 2) return Vector3.zero; float s = Mathf.Max(0f, PathS(t) + sOff), yw; Vector3 p = PathAt(s, out yw);
            if (Mathf.Abs(lat) > 0.01f) { float h = PathYaw(s) * Mathf.Deg2Rad; p += new Vector3(Mathf.Cos(h), 0f, -Mathf.Sin(h)) * lat; }
            float u = Mathf.Clamp01((t - (T_BASE + 0.8f)) / 1.7f); return new Vector3(p.x, u < 1f ? Y_BASE + 12f * (1f - u * u) : p.y, p.z);      // 7:12: a 12 m fall down the shaft, then the floor
        }

        void P2Setup()
        {
            facM = Take2("facility", 1.5f, false); hallM = Take2("hall", 1.5f, false); gyroM = Take2("gyro", 1.5f, false); hatchM = Take2("hatch", 1.4f, false); baseM = Take2("base", 1.5f, false); ch3M = Take2("chapter3", 1.25f, false);
            jonM = Take2("jones", 1.15f, true); fouM = Take2("foundation", 1.15f, true); slnM = Take2("slone", 1.15f, true); gunM = Take2("gunnar", 1.2f, true); sciM = Take2("scientist", 1.2f, true); visM = Take2("visitor", 1.2f, true);
            grdM = Take2("guard", 1.3f, true); rifM = Take2("rifle", 1.2f, true); zpM2 = Take2("zeropoint", 1.3f, false);
            if (rifM != null) rifM.Pivot.SetActive(false);
            facLight = new GameObject("EFacLight").AddComponent<Light>(); facLight.transform.SetParent(root.transform, false); facLight.type = LightType.Point; facLight.range = 44f; facLight.intensity = 0f; facLight.shadows = LightShadows.None;
            facLight2 = new GameObject("EFacLight2").AddComponent<Light>(); facLight2.transform.SetParent(root.transform, false); facLight2.type = LightType.Point; facLight2.range = 44f; facLight2.intensity = 0f; facLight2.shadows = LightShadows.None;
            if (facM != null) { Colliders(facM.Pivot); foreach (var t in facM.Pivot.GetComponentsInChildren<Transform>(true)) { if (t.name == "DoorL") doorL = t; else if (t.name == "DoorR") doorR = t; else if (t.name == "DoorS") doorS = t; } }
            if (hallM != null) Colliders(hallM.Pivot);
            if (gyroM != null) { Colliders(gyroM.Pivot); gyroR = CRig.Of(gyroM, gyroM.Pivot); }
            if (hatchM != null) { Colliders(hatchM.Pivot); foreach (var t in hatchM.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == "Cover") hatchCover = t; }
            if (baseM != null)
            {
                baseM.Pivot.SetActive(true); PlaceByFile(baseM, new Vector3(0f, Y_BASE, 0f));
                foreach (var mf in baseM.Pivot.GetComponentsInChildren<MeshFilter>(true)) { if (mf.sharedMesh == null || mf.name == "Glass" || mf.name == "Swirl") continue; var mc = mf.gameObject.GetComponent<MeshCollider>(); if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mf.gameObject.layer = floorLayer; }
                baseR = CRig.Of(baseM, baseM.Pivot); BuildPath(); if (baseGlass != null) { var gmat = kit.Glow(new Color(0.55f, 0.85f, 1f, 0.06f), null, 3000); gmat.mainTexture = null; try { gmat.SetFloat("_Cull", 0f); } catch { } foreach (var gr in baseGlass.GetComponentsInChildren<Renderer>(true)) { var sm = new Material[gr.sharedMaterials.Length]; for (int i = 0; i < sm.Length; i++) sm[i] = gmat; gr.sharedMaterials = sm; } } baseM.Pivot.SetActive(false);
            }
            if (zpM2 != null) zpR2 = CRig.Of(zpM2, zpM2.Pivot);
            // ================= the I.O. facility (4:30 - 5:32), authoring coordinates
            var J = NewAct("Jones", jonM, 1f, true, false, -1f, T_FAC, T_SURF); var F = NewAct("Foundation", fouM, 1f, true, false, -1f, 293f, T_SURF); var S = NewAct("Slone", slnM, 1f, false, false, -1f, 271.5f, T_HALL); var G = NewAct("Gunnar", gunM, 1f, true, false, -1f, T_FAC, T_HALL);
            var U0 = NewAct("Guard0", grdM, 1f, true, true, -1f, 304.7f, T_HALL);
            Action<Act, float, float, float, float, string> fw = (a, t, x, z, yw, c) => Wp(a, t, x, Y_FAC, z, yw, c);
            // Jones: the captive in the scanner chair (reclined, head in the crown) until the Foundation has dealt with everyone, then he sits up (5:28) and talks to him (5:30)
            fw(J, 270f, 0f, 2.92f, 180f, "Recline"); fw(J, 327.6f, 0f, 2.92f, 180f, "Recline"); fw(J, 327.8f, 0f, 2.92f, 180f, "Wake"); fw(J, 329.6f, 0f, 2.2f, 180f, "Wake"); fw(J, 330.6f, 0f, 2.2f, 180f, "Talk"); fw(J, 331.9f, 0f, 2.2f, 180f, "Talk");
            // Slone: the figure in the security doorway (4:34) walks to Jones's side (4:44), talks; panics when the door blows (4:56), backs away, tries to stop the Foundation (5:18), is stunned by the face (5:22), is held (5:26)
            fw(S, 271.5f, 16.5f, -4.0f, 270f, "Idle"); fw(S, 272.4f, 16.5f, -4.0f, 270f, "Walk"); fw(S, 274.1f, 11.6f, -4.0f, 270f, "Walk"); fw(S, 283.5f, -1.35f, 2.75f, 90f, "STalk"); fw(S, 289f, -1.35f, 2.75f, 90f, "Smug"); fw(S, 293f, -1.35f, 2.75f, 90f, "STalk"); fw(S, 296.1f, -1.35f, 2.75f, 90f, "Panic");
            fw(S, 298f, -1.35f, 2.75f, 90f, "Panic"); fw(S, 298.8f, -2.5f, 2.45f, 70f, "Plead"); fw(S, 306f, -2.5f, 2.45f, 70f, "Plead"); fw(S, 317.6f, -2.5f, 2.45f, 70f, "Run"); fw(S, 319.3f, 0.1f, 1.75f, 90f, "Shove"); fw(S, 321f, 0.1f, 1.75f, 90f, "Idle"); fw(S, 322.3f, 0.1f, 1.75f, 90f, "Panic");
            fw(S, 325f, 0.1f, 1.75f, 90f, "Plead"); fw(S, 331.9f, 0.1f, 1.75f, 90f, "Plead");
            // Gunnar: at the vitals computer (4:40), draws on the door when it blows, is flung into the computer by the Foundation (5:12)
            fw(G, 270f, 1.2f, 2.5f, 90f, "Console"); fw(G, 297.1f, 1.2f, 2.5f, 90f, "Console"); fw(G, 297.5f, 1.2f, 2.5f, 175f, "Alert"); fw(G, 311.9f, 1.2f, 2.5f, 120f, "Alert"); fw(G, 312.0f, 1.2f, 2.5f, 90f, "Hit"); fw(G, 312.3f, 1.6f, 2.75f, 90f, "Hit"); fw(G, 312.6f, 1.6f, 2.75f, 0f, "Death"); fw(G, 331.9f, 1.6f, 2.75f, 0f, "Death");        // he slumps off the computer towards you (not into Jones's chair)
            // the Foundation: bursts through the blast door (4:56), confronts them (5:04), drops the guard (5:08), attacks Gunnar (5:10), is confronted (5:18), unmasks (5:22), confines Slone (5:26), talks to Jones (5:30)
            fw(F, 293f, 0f, -15f, 0f, "Idle"); fw(F, 294.6f, 0f, -15f, 0f, "Run"); fw(F, 295.9f, 0f, -9.5f, 0f, "Run"); fw(F, 299.4f, -1.9f, -1.6f, 20f, "Ready"); fw(F, 304f, -1.9f, -1.6f, 20f, "Aim"); fw(F, 306f, 1.0f, -1.2f, 100f, "Aim"); fw(F, 307.4f, 1.0f, -1.2f, 100f, "Punch"); fw(F, 309.7f, 1.0f, -1.2f, 100f, "Run");
            fw(F, 311.0f, 1.15f, 1.55f, 90f, "Shove"); fw(F, 313.5f, 1.15f, 1.55f, 90f, "Ready"); fw(F, 319.2f, 1.15f, 1.7f, -90f, "Ready"); fw(F, 319.9f, 1.15f, 1.7f, -90f, "Hit"); fw(F, 320.0f, 1.15f, 1.7f, -90f, "Unmask"); fw(F, 324.6f, 1.15f, 1.7f, -90f, "Walk"); fw(F, 325.9f, 0.65f, 1.75f, -90f, "Pin");
            fw(F, 329.8f, 0.65f, 1.75f, -90f, "Idle"); fw(F, 330.2f, 0.55f, 1.95f, 0f, "Talk"); fw(F, 331.9f, 0.55f, 1.95f, 0f, "Talk");
            // the guard who comes through the security door (5:06) and is knocked down (5:08)
            fw(U0, 304.8f, 16.5f, -4f, 270f, "Run"); fw(U0, 307.9f, 2.4f, -1.3f, 270f, "Run"); fw(U0, 307.95f, 2.4f, -1.3f, 270f, "Hit"); fw(U0, 308.5f, 3.0f, -1.3f, 270f, "Death"); fw(U0, 331.9f, 3.0f, -1.3f, 270f, "Death");
            G.Aim = () => J != null && J.Go != null ? F.Go.transform.position + Vector3.up * 1.3f : Vector3.zero; U0.Aim = () => F.Go.transform.position + Vector3.up * 1.3f; F.Aim = () => AimAt(F, "Guard", 1.2f); J.Aim = () => AimAt(J, "Guard", 1.2f);
            // ================= the hall (5:32 - 5:56): you follow Jones (left) and the Foundation (right); the player is HallZ(t), the leaders 3 m ahead
            Action<Act, float, float, float, float, string> hw = (a, t, x, z, yw, c) => Wp(a, t, x, Y_HALL, z, yw, c);
            if (J != null) { Wp(J, T_HALL, -0.65f, Y_HALL, 5.0f, 0f, "Sneak"); J.W[J.W.Count - 1].Cut = true; hw(J, 342.6f, -0.65f, 15.4f, 0f, "Sneak"); hw(J, 343.0f, -0.65f, 15.4f, 0f, "Crouch"); hw(J, 347.6f, -0.65f, 15.4f, 0f, "Crouch"); hw(J, 347.9f, -0.65f, 15.4f, 0f, "Brace"); hw(J, 349.5f, -0.65f, 15.4f, 0f, "Brace");
                hw(J, 349.6f, -0.65f, 15.4f, -90f, "Walk"); hw(J, 351.0f, -1.2f, 18.0f, -90f, "Console"); hw(J, 355.9f, -1.2f, 18.0f, -90f, "Console"); }
            if (F != null) { Wp(F, T_HALL, 0.65f, Y_HALL, 5.6f, 0f, "Sneak"); F.W[F.W.Count - 1].Cut = true; hw(F, 342.6f, 0.65f, 16.0f, 0f, "Sneak"); hw(F, 343.0f, 0.65f, 16.0f, 0f, "Aim"); hw(F, 344.0f, 0.65f, 16.0f, 0f, "FireK"); hw(F, 346.9f, 0.65f, 16.0f, 0f, "Aim"); hw(F, 347.8f, 0.65f, 16.0f, 0f, "Brace"); hw(F, 349.5f, 0.65f, 16.0f, 0f, "Aim");
                hw(F, 354.1f, 0.65f, 17.8f, 0f, "Aim"); hw(F, 354.2f, 0.65f, 17.8f, 0f, "FireK"); hw(F, 355.9f, 0.65f, 17.8f, 0f, "FireK"); }
            var H1 = NewAct("Guard1", grdM, 1f, true, true, -1f, 342.4f, 349.5f); if (H1 != null) { Wp(H1, 342.5f, 0.5f, Y_HALL, 25.5f, 180f, "Alert"); Wp(H1, 343.4f, 0.5f, Y_HALL, 25.5f, 180f, "Fire"); Wp(H1, 346.9f, 0.5f, Y_HALL, 25.5f, 180f, "Death"); Wp(H1, 349.4f, 0.5f, Y_HALL, 25.5f, 180f, "Death"); H1.Aim = () => HeadNow() + Vector3.down * 0.3f; }
            var H2 = NewAct("Guard2", grdM, 1f, true, true, -1f, 353.4f, T_GYRO + 0.1f); if (H2 != null) { Wp(H2, 353.5f, -0.4f, Y_HALL, 33.0f, 180f, "Alert"); Wp(H2, 354.2f, -0.4f, Y_HALL, 33.0f, 180f, "Fire"); Wp(H2, 356.1f, -0.4f, Y_HALL, 33.0f, 180f, "Fire"); H2.Aim = () => HeadNow() + Vector3.down * 0.3f; }
            // ================= the Zero Point machinery chamber (5:56 - 6:40): Jones at the console, the Foundation holds the doors against the I.O. guards
            Action<Act, float, float, float, float, string> gw = (a, t, x, z, yw, c) => Wp(a, t, x, Y_GYRO, z, yw, c);
            if (J != null) { Wp(J, T_GYRO, -0.4f, Y_GYRO, 2.2f, 0f, "Console"); J.W[J.W.Count - 1].Cut = true; gw(J, 369.0f, -0.4f, 2.2f, 0f, "Console"); gw(J, 369.2f, -0.4f, 2.2f, 0f, "Look"); gw(J, 371.5f, -0.4f, 2.2f, 0f, "Console"); gw(J, 390.5f, -0.4f, 2.2f, 0f, "Console"); gw(J, 391.2f, 1.2f, 2.3f, 0f, "Walk"); gw(J, 392.2f, 1.2f, 2.3f, 0f, "Console"); gw(J, 395.5f, 1.2f, 2.3f, 0f, "Look"); gw(J, 397f, 1.2f, 2.3f, 0f, "Talk"); gw(J, 399.9f, 1.2f, 2.3f, 0f, "Talk"); }
            if (F != null) { Wp(F, T_GYRO, 3.8f, Y_GYRO, 1.2f, 75f, "FireK"); F.W[F.W.Count - 1].Cut = true; gw(F, 371.5f, 3.8f, 1.2f, 75f, "FireK"); gw(F, 372.4f, 3.8f, 1.2f, -75f, "Fire"); gw(F, 384.5f, 3.8f, 1.2f, -75f, "Fire"); gw(F, 385.2f, 3.8f, 1.2f, 75f, "FireK"); gw(F, 391.0f, 3.8f, 1.2f, 75f, "FireK"); gw(F, 391.4f, 3.8f, 1.2f, -75f, "Fire"); gw(F, 395.5f, 3.8f, 1.2f, -75f, "Fire"); gw(F, 396.2f, 3.8f, 1.2f, 0f, "Ready"); gw(F, 399.9f, 3.8f, 1.2f, 0f, "Idle"); }
            float[] gt = { 357.0f, 358.4f, 361.4f, 365.6f, 372.4f, 375.2f, 379.0f, 385.2f, 386.6f, 389.5f };
            for (int k = 0; k < gt.Length; k++)
            {
                float side = k < 4 ? 1f : (k < 7 ? -1f : (k % 2 == 0 ? 1f : -1f)), t0 = gt[k], tx = side * (8f + (k % 3) * 1.1f), tz = ((k * 37) % 5 - 2) * 0.75f, doorX = side * 13.9f, run = 2.2f + 0.1f * (k % 3);
                var g = NewAct("Guard" + (10 + k), grdM, 1f, true, true, -1f, t0 - 0.1f, t0 + run + 7.2f); if (g == null) continue; float yw = side > 0 ? 270f : 90f;
                Wp(g, t0, doorX, Y_GYRO, tz * 0.5f, yw, "Run"); Wp(g, t0 + run, tx, Y_GYRO, tz, yw, "Fire"); Wp(g, t0 + run + 4.4f, tx, Y_GYRO, tz, yw, "Fire"); Wp(g, t0 + run + 4.6f, tx, Y_GYRO, tz, yw, "Death"); Wp(g, t0 + run + 7.0f, tx, Y_GYRO, tz, yw, "Death");
                g.Aim = () => F != null && F.Go != null ? F.Go.transform.position + Vector3.up * 1.2f : Vector3.zero;
            }
            FindStage();
            // ================= back on the island (6:40 - 7:10): the Scientist and the Visitor of the Seven fight the caretakers, open the hatch and call you in; root-local coordinates (not mirrored)
            var Sc = NewAct("Scientist", sciM, 1f, true, false, 1f, T_SURF, T_BASE - 0.4f, T_BASE + 1f, T_FLOOD + 3f); var V = NewAct("Visitor", visM, 1f, true, false, 1f, T_SURF, T_BASE - 0.4f, T_BASE + 1f, T_FLOOD + 3f);
            if (Sc != null) { Sc.Ground = true; Sc.Ox = hatchX; Sc.Oz = hatchZ - 10f; Action<float, float, float, float, string> sw = (t, x, z, yw, c) => Wp(Sc, t, x, 0f, z, yw, c);
                Wp(Sc, T_SURF, -5.5f, 0f, 17f, 200f, "Fire"); Sc.W[0].Cut = true; sw(413f, -5.5f, 17f, 200f, "Fire"); sw(413.4f, -5.5f, 17f, 162f, "Ready"); sw(414.2f, -5.5f, 17f, 162f, "Beckon"); sw(417f, -5.5f, 17f, 142f, "Beckon"); sw(417.4f, -5.5f, 17f, 142f, "Point"); sw(419f, -5.5f, 17f, 142f, "Walk");
                sw(424f, -0.3f, 7.0f, 90f, "Walk"); sw(427.6f, 0f, 11.4f, 0f, "Walk"); sw(428.3f, 0f, 11.4f, 0f, "Idle");
                Sc.Aim = () => AimAt(Sc, "Care", 2.5f); }
            if (V != null) { V.Ground = true; V.Ox = hatchX; V.Oz = hatchZ - 10f; Action<float, float, float, float, string> vw = (t, x, z, yw, c) => Wp(V, t, x, 0f, z, yw, c);
                Wp(V, T_SURF, 5.5f, 0f, 15f, 160f, "Fire"); V.W[0].Cut = true; vw(413.2f, 5.5f, 15f, 160f, "Fire"); vw(413.8f, 5.5f, 15f, 200f, "Ready"); vw(416f, 5.5f, 15f, 200f, "Ready"); vw(417.5f, 5.5f, 15f, 215f, "Aim"); vw(419.5f, 5.5f, 15f, 215f, "Walk"); vw(424.6f, 0.9f, 6.5f, 90f, "Walk"); vw(427.9f, 0.4f, 11.2f, 0f, "Walk"); vw(428.4f, 0.4f, 11.2f, 0f, "Idle");
                V.Aim = () => AimAt(V, "Care", 2.5f); }
            if (careM != null)
            {
                for (int k = 0; k < 9; k++)
                {
                    bool big = k == 8; float td = big ? 413f : 402.6f + k * 1.25f, x0 = (k - 3.5f) * 11f + (k % 2) * 5f, z0 = 62f + (k % 3) * 9f, x1 = x0 * 0.35f, z1 = 25f + (k % 4) * 3f; float sp = big ? 5.2f : 7.5f, tr = Mathf.Sqrt((x0 - x1) * (x0 - x1) + (z0 - z1) * (z0 - z1)) / sp, ts = Mathf.Min(td - 0.4f - tr, T_SURF + 0.5f + k * 0.2f);
                    var c = NewAct("Care" + k, careM, (big ? 10f : 6f) / 1.8f, false, true, 1f, T_SURF, td + 5f); if (c == null) continue; c.Ground = true; c.Creature = true; c.Ox = hatchX; c.Oz = hatchZ - 10f;
                    Wp(c, T_SURF, x0, 0f, z0, 180f, "Run"); c.W[0].Cut = true; Wp(c, ts + tr, x1, 0f, z1, 180f, "Run"); Wp(c, td - 0.3f, x1, 0f, z1, 180f, "Attack"); Wp(c, td, x1, 0f, z1, 180f, "Death"); Wp(c, td + 5f, x1, 0f, z1, 180f, "Death");
                    if (ts > T_SURF + 0.01f) { c.W.Clear(); Wp(c, T_SURF, x0, 0f, z0, 180f, "Run"); c.W[0].Cut = true; Wp(c, T_SURF + 0.0f, x0, 0f, z0, 180f, "Run"); Wp(c, ts + tr, x1, 0f, z1, 180f, "Run"); Wp(c, td - 0.3f, x1, 0f, z1, 180f, "Attack"); Wp(c, td, x1, 0f, z1, 180f, "Death"); Wp(c, td + 5f, x1, 0f, z1, 180f, "Death"); }
                }
            }
            // ================= the underground base (7:12 - 9:13): the leaders run the path; you follow 3 m behind
            if (Sc != null && bPts.Count > 1)
            {
                Sc.PathOn = t => t >= T_BASE; Sc.Pos = t => LeaderPos(t, 0f, 0f); Sc.YawF = t => t < T_BASE + 2.5f ? 0f : PathYaw(Mathf.Max(0f, PathS(t)));
                Action<float, string> pc = (t, c) => Sc.PClips.Add(new WP { T = t, Clip = c }); pc(T_BASE, "Fall"); pc(T_BASE + 2.5f, "Land"); pc(435f, "RunAim"); pc(512.5f, "Idle"); pc(526f, "Look"); pc(538f, "Alert"); pc(545.6f, "Stagger"); pc(548f, "Brace"); pc(551f, "Panic");
            }
            if (V != null && bPts.Count > 1)
            {
                V.PathOn = t => t >= T_BASE; V.Pos = t => LeaderPos(t, -0.9f, 0.95f); V.YawF = t => t < T_BASE + 2.5f ? 0f : PathYaw(Mathf.Max(0f, PathS(t) - 0.9f));
                Action<float, string> pc = (t, c) => V.PClips.Add(new WP { T = t, Clip = c }); pc(T_BASE, "Fall"); pc(T_BASE + 2.6f, "Land"); pc(435.2f, "RunAim"); pc(512.5f, "Idle"); pc(527f, "Alert"); pc(545.7f, "Stagger"); pc(548.2f, "Brace"); pc(551.2f, "Panic");
            }
            // the Caretaker that comes through the glass (9:05): the same model, 13 times bigger, standing on the dome
            if (careM != null && baseM != null)
            {
                giantGo = Clone(careM, "EGiant"); giant = new Act { Name = "Giant", M = careM, Go = giantGo, Rig = CRig.Of(careM, giantGo), Scale = 38f / 1.8f, Vis = new[] { 542f, T_FLOOD + 3f }, Mir = 1f, Creature = true }; acts.Add(giant);
                Action<float, string> gg = (t, c) => Wp(giant, t, bChc.x, Y_BASE + 39f, bChc.z + 33f, 180f, c); gg(542f, "Roar"); giant.W[0].Cut = true; gg(545.2f, "Slam"); gg(550f, "Roar"); gg(556f, "Roar");
                giantLight = new GameObject("EGiantLight").AddComponent<Light>(); giantLight.transform.SetParent(root.transform, false); giantLight.type = LightType.Point; giantLight.range = 190f; giantLight.color = new Color(0.8f, 0.6f, 1f); giantLight.intensity = 0f; giantLight.shadows = LightShadows.None;
            }
            AstronomicalDebug.Anchors["facroom"] = () => W(new Vector3(0f, Y_FAC + 1.5f, 2.8f)); AstronomicalDebug.Anchors["hallp"] = () => W(new Vector3(0f, Y_HALL + 1.5f, HallZ(lastT) + 3f)); AstronomicalDebug.Anchors["gyroroom"] = () => W(new Vector3(0f, Y_GYRO + 3f, 3f));
            AstronomicalDebug.Anchors["chamber"] = () => W(bChc + Vector3.up * 8f); AstronomicalDebug.Anchors["hatch"] = () => W(new Vector3(hatchX, GroundNear(hatchX, hatchZ) + 1f, hatchZ)); AstronomicalDebug.Anchors["baseguide"] = () => W(LeaderPos(lastT, 0f, 0f) + Vector3.up * 1.2f);
            AstronomicalDebug.Anchors["giant"] = () => giantGo != null ? giantGo.transform.position + Vector3.up * 6f : Vector3.zero;
            AstronomicalDebug.Anchors["qwater"] = () => W(new Vector3(22f, WY + 6f, 85f));
            AstronomicalDebug.Anchors["isle"] = () => isle != null ? isle.transform.position : Vector3.zero; AstronomicalDebug.Anchors["c3"] = () => c3T != null ? c3T.position : Vector3.zero;
            AstronomicalDebug.Anchors["byou"] = () => { float yw; float sA = Mathf.Max(0f, BaseS(lastT) - 3.2f); Vector3 p = PathAt(sA, out yw); return W(new Vector3(p.x, Y_BASE + 1.6f, p.z)); }; AstronomicalDebug.Anchors["bhead"] = () => W(LeaderPos(lastT, 0f, 0f) + Vector3.up * 1.4f);
            SetupOcean();
        }

        // ---------------- the one-shot effects and ambient life of each scene
        void SceneFx(float t, float tp, float dt, Sc sc, Camera cam)
        {
            bool go = !paused && dt > 0f; if (!go) return;
            Func<float, bool> at = x => tp < x && t >= x;
            // ---- the facility
            if (sc == Sc.Fac)
            {
                if (at(295.9f)) { Vector3 dp = W(new Vector3(0f, Y_FAC + 1.5f, -8.6f)); Boom(dp, 14f, 1.4f, new Color(1f, 0.55f, 0.2f)); shake = Mathf.Max(shake, 2f); for (int k = 0; k < 120; k++) Emit(dust, dp + UnityEngine.Random.insideUnitSphere * 2f, new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(0f, 4f), UnityEngine.Random.Range(1f, 9f)), new Color(0.45f, 0.4f, 0.4f, 0.5f), UnityEngine.Random.Range(1.5f, 4f), UnityEngine.Random.Range(2f, 4f)); }
                if (at(312.0f)) { Vector3 cp = W(new Vector3(-1.9f, Y_FAC + 1.3f, 2.8f)); Boom(cp, 3.5f, 0.5f, new Color(0.5f, 0.9f, 1f)); shake = Mathf.Max(shake, 1.2f); for (int k = 0; k < 40; k++) Emit(fx, cp, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(3f, 9f), new Color(0.6f, 1f, 1f, 1f), UnityEngine.Random.Range(0.1f, 0.3f), UnityEngine.Random.Range(0.3f, 0.9f)); }
                if (at(307.9f)) shake = Mathf.Max(shake, 0.6f);
            }
            // ---- the hall: the corridor explodes at 5:48 and burns
            if (sc == Sc.Hall)
            {
                if (at(348.0f)) { Vector3 ep = W(new Vector3(0f, Y_HALL + 1.3f, 27f)); Boom(ep, 7f, 0.9f, new Color(1f, 0.5f, 0.15f)); shake = Mathf.Max(shake, 2.2f); }
                if (t > 348f && t < 353.5f && UnityEngine.Random.value < dt * 70f) { float z = UnityEngine.Random.Range(21f, 33f); Emit(fx, W(new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), Y_HALL + 0.3f, z)), new Vector3(0f, UnityEngine.Random.Range(2f, 5f), 0f), new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.1f, 0.9f), UnityEngine.Random.Range(0.8f, 1.8f), UnityEngine.Random.Range(0.5f, 1.1f)); }
                if (at(346.9f) || at(354.9f)) shake = Mathf.Max(shake, 0.5f);
            }
            // ---- the gyro chamber: keypad (6:10), explosions while it is held, the red button (6:32)
            if (sc == Sc.Gyro)
            {
                if (at(370f)) { flashLamp.transform.position = W(new Vector3(0f, Y_GYRO + 2.5f, 3f)); flashLamp.color = new Color(0.6f, 0.9f, 1f); flashLamp.intensity = Lit * 8f; }
                if (t > 372f && t < 396f && UnityEngine.Random.value < dt * 0.6f) { Boom(W(new Vector3(UnityEngine.Random.Range(-10f, 10f), Y_GYRO + UnityEngine.Random.Range(2f, 8f), UnityEngine.Random.Range(-8f, 6f))), 3f, 0.3f, new Color(1f, 0.6f, 0.3f)); shake = Mathf.Max(shake, 0.7f); }
                if (at(392f)) { Boom(W(new Vector3(0f, Y_GYRO + 8f, 6f)), 16f, 1.2f, new Color(0.8f, 0.5f, 1f)); shake = Mathf.Max(shake, 2.5f); flashLamp.intensity = Lit * 20f; }
            }
            // ---- the surface: lasers on the hatch cover (6:55), the UFO leaves, the hatch blows (7:10)
            if (sc == Sc.Surface)
            {
                Vector3 cov = W(new Vector3(hatchX, GroundNear(hatchX, hatchZ) + 0.3f, hatchZ));
                for (int i = 0; i < beams.Count; i++)
                {
                    bool on = i < 5 && t > 415f && t < 417.4f && i < abds.Count && abds[i] != null && abds[i].activeSelf; if (beams[i].enabled != on) beams[i].enabled = on;
                    if (on) { Vector3 tgt = cov + UnityEngine.Random.insideUnitSphere * 1.6f; beams[i].SetPosition(0, abds[i].transform.position); beams[i].SetPosition(1, tgt); beams[i].widthMultiplier = 1.5f + UnityEngine.Random.value * 1.5f; if (UnityEngine.Random.value < dt * 20f) Emit(fx, tgt, UnityEngine.Random.onUnitSphere * 6f + Vector3.up * 3f, new Color(1f, 0.6f, 0.9f, 1f), UnityEngine.Random.Range(0.3f, 0.9f), 0.5f); }
                }
                if (t > 400f && t < 430f && UnityEngine.Random.value < dt * 0.5f) { float a2 = UnityEngine.Random.value * 6.28f, r2 = UnityEngine.Random.Range(40f, 150f); Boom(W(new Vector3(Mathf.Sin(a2) * r2, GroundY(0f, 50f) + 4f, Mathf.Cos(a2) * r2)), UnityEngine.Random.Range(5f, 11f), 0.5f, new Color(1f, 0.5f, 0.2f)); shake = Mathf.Max(shake, 0.6f); }
                if (at(429.6f)) { Boom(cov + Vector3.up, 24f, 1.1f, new Color(1f, 0.85f, 0.6f)); shake = Mathf.Max(shake, 3f); flashLamp.intensity = Lit * 30f; }
            }
            // ---- the base: tremors, dust from the ceiling, then the Caretaker at the glass (9:05)
            if (sc == Sc.Base)
            {
                if (t > T_BASE + 0.3f && t < T_CHAM && UnityEngine.Random.value < dt * 0.4f) { shake = Mathf.Max(shake, 0.5f); Vector3 hp = HeadNow(); for (int k = 0; k < 14; k++) Emit(dust, hp + new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(1f, 2.5f), UnityEngine.Random.Range(-3f, 3f)), new Vector3(0f, -UnityEngine.Random.Range(0.4f, 1.2f), 0f), new Color(0.6f, 0.6f, 0.7f, 0.5f), UnityEngine.Random.Range(0.1f, 0.35f), UnityEngine.Random.Range(1.5f, 3f)); }
                if (at(T_BASE + 0.4f)) shake = Mathf.Max(shake, 1.5f);
                if (at(T_BASE + 2.5f)) { shake = Mathf.Max(shake, 1.5f); Boom(W(LeaderPos(t, 0f, 0f)), 5f, 0.3f, new Color(0.9f, 0.9f, 1f)); }
                Vector3 top = W(bChc + new Vector3(0f, 40f, 15f));      // where its fist meets the glass
                if (t > 543f && t < T_FLOOD && UnityEngine.Random.value < dt * 3f) shake = Mathf.Max(shake, 0.5f);
                if (at(547.05f)) { Boom(top, 26f, 1.2f, new Color(0.4f, 0.8f, 1f)); shake = Mathf.Max(shake, 3f); flashLamp.intensity = Lit * 25f; for (int k = 0; k < 120; k++) Emit(fx, top + UnityEngine.Random.insideUnitSphere * 8f, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(6f, 30f), new Color(0.5f, 0.9f, 1f, 1f), UnityEngine.Random.Range(0.2f, 0.7f), UnityEngine.Random.Range(0.8f, 2f)); }
                if (t > 547.2f && t < T_FLOOD && UnityEngine.Random.value < dt * 30f) Emit(fx, top + UnityEngine.Random.insideUnitSphere * 30f, new Vector3(0f, -UnityEngine.Random.Range(4f, 12f), 0f), new Color(0.8f, 0.95f, 1f, 0.9f), UnityEngine.Random.Range(0.2f, 0.7f), UnityEngine.Random.Range(1f, 2.5f));      // the glass cracks and showers down
                if (at(T_FLOOD)) { glassGone = true; Boom(top, 32f, 1.5f, new Color(0.6f, 0.9f, 1f)); shake = Mathf.Max(shake, 3.5f); for (int k = 0; k < 400; k++) Emit(fx, top + UnityEngine.Random.insideUnitSphere * 30f, new Vector3(UnityEngine.Random.Range(-8f, 8f), -UnityEngine.Random.Range(5f, 25f), UnityEngine.Random.Range(-8f, 8f)), new Color(0.7f, 0.95f, 1f, 1f), UnityEngine.Random.Range(0.15f, 0.6f), UnityEngine.Random.Range(1.5f, 3.5f)); }
                if (t > T_FLOOD && dt > 0f) { Vector3 hp = HeadNow(); if (UnityEngine.Random.value < dt * 60f) Emit(dust, hp + UnityEngine.Random.insideUnitSphere * 6f, new Vector3(0f, UnityEngine.Random.Range(1f, 3f), 0f), new Color(0.6f, 0.85f, 1f, 0.5f), UnityEngine.Random.Range(0.1f, 0.4f), UnityEngine.Random.Range(2f, 4f)); }
            }
            OceanFx(t, tp, dt, sc, cam);
        }
    }
}
