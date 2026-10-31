using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // THE END, part 2 (4:30 - 11:04). The island is left for seven scenes of the video, each a Blender room floating above the map that you are carried into (the same trick as Collision's cabin):
    //   4:30 the I.O. facility (Jones in the scanner chair, Slone, Gunnar; the Foundation breaks in 4:56)      5:32 the hall (green server hall, red halls; Jones and the Foundation sneak past guards)
    //   5:56 the Zero Point machinery chamber (Jones at the console, the Foundation holds two doors)           6:40 back on the island: the Seven's Scientist and Visitor fight the caretakers and open the hatch
    //   7:10 the underground base: you FOLLOW the Scientist and the Visitor through blue / red corridors and halls to the purple Zero Point chamber (8:28); the Caretaker breaks the glass at 9:05, the chamber floods
    //   9:15 you surface in the ocean; the old island flips over and sinks, the Chapter 3 island is on its other side (10:30); title card at 11:00.
    // Actors follow waypoint scripts (t, x, y, z, yaw, clip) or a path function; clips are crossfaded and moves are eased so nothing pops. Room models are authored in Blender (x is mirrored by the glTF loader: scripts use
    // authoring coordinates for the room actors and Wp() mirrors them).
    internal sealed partial class AstronomicalEnd21
    {
        const float T_FAC = 270f, T_HALL = 332f, T_GYRO = 356f, T_SURF = 400f, T_BASE = 430f, T_CHAM = 508f, T_FLOOD = 550f, T_OCEAN = 555f, T_END_VIS = 603.5f;
        const float Y_FAC = 700f, Y_HALL = 760f, Y_GYRO = 820f, Y_BASE = 900f;
        enum Sc { Island, Fac, Hall, Gyro, Surface, Base, Ocean }
        static Sc SceneAt(float t) { if (t < T_FAC) return Sc.Island; if (t < T_HALL) return Sc.Fac; if (t < T_GYRO) return Sc.Hall; if (t < T_SURF) return Sc.Gyro; if (t < T_BASE) return Sc.Surface; if (t < T_OCEAN) return Sc.Base; return Sc.Ocean; }
        static bool IsRoom(Sc s) { return s == Sc.Fac || s == Sc.Hall || s == Sc.Gyro || s == Sc.Base; }

        sealed class WP { public float T, X, Y, Z, Yaw; public string Clip; public bool Cut; }
        sealed class Act
        {
            public string Name; public GltfModel M; public GameObject Go; public CRig Rig; public List<WP> W = new List<WP>(); public float[] Vis = new float[0]; public Transform Gun; public Transform RMod; public GameObject Rifle;
            public int Shot = -1; public float Scale = 1f, Mir = -1f, Ox, Oz; public bool Armed, Ground, Creature; public Func<Vector3> Aim; public Func<float, bool> PathOn; public Func<float, Vector3> Pos; public Func<float, float> YawF; public List<WP> PClips = new List<WP>();
            public float[] cum, seg; public string Cur, Prev; public float CurT, PrevT, Blend = 1f, LastT = -9f;
        }
        GltfModel facM, hallM, gyroM, hatchM, baseM, ch3M, jonM, fouM, slnM, gunM, sciM, visM, grdM, rifM, zpM2;
        readonly List<Act> acts = new List<Act>(); readonly List<GameObject> p2Clones = new List<GameObject>();
        Light facLight, facLight2; Transform doorL, doorR, doorS, hatchCover, baseGlass, baseSwirl; float doorLBase, doorRBase, doorSBase, doorSgnL, doorSgnR, doorSgnS; Vector3 coverBase0; bool coverInit; CRig facR, gyroR, baseR, zpR2;
        bool p2Dead, p2Ready, doorsInit, glassGone; float noclipT2 = -9f, lastReseat2 = -9f, reseatHoldT = -9f, dbgT = -9f; bool carryErr; Vector3 reseatHoldPos; int reseatTries2; Sc curSc = Sc.Island;
        readonly List<Collider> noclipped2 = new List<Collider>(); Vector3 homeHead; bool haveHome; Act giant; GameObject giantGo;

        static readonly string[] RIFLE_CLIPS = { "Fire", "FireK", "Aim", "Ready", "Alert", "Hold", "Walk", "Run", "RunAim", "Sneak", "Crouch", "Peek", "Kneel", "Reload", "Climb" };
        static readonly HashSet<string> rifleSet = new HashSet<string>(RIFLE_CLIPS);
        static readonly HashSet<string> oneShot = new HashSet<string>(new[] { "Death", "Unmask", "Punch", "Wake", "Panic", "Plead", "Land", "Stagger", "Hit", "Throw", "Crushed", "Jump", "Slam", "Roar", "Attack", "Cast", "Shove", "Reload", "FallBumpR", "FallBumpL", "Retreat", "Fall" });
        // (clip period, stride): the distance one gait cycle covers; phase = distance / stride * period keeps the feet planted
        static void GaitOf(string clip, out float period, out float stride)
        {
            switch (clip) { case "Run": period = 0.75f; stride = 3.2f; break; case "RunAim": period = 0.75f; stride = 3.0f; break; case "Jog": case "JogAim": period = 0.85f; stride = 2.3f; break; case "Sneak": period = 1.35f; stride = 1.0f; break; case "Climb": period = 1.05f; stride = 1.35f; break; default: period = 1.0f; stride = 1.5f; break; }
        }
        // the gait that matches how fast an actor really travels (feet stay planted because the phase follows the distance)
        static string LocoClip(string designated, float speed)
        {
            bool aim = designated == "RunAim" || designated == "JogAim"; if (designated == "Sneak") return "Sneak";
            if (speed > 3.4f) return aim ? "RunAim" : "Run"; if (speed > 1.9f) return aim ? "JogAim" : "Jog"; return "Walk";
        }
        Vector3 Rm(Vector3 v, float y) { return new Vector3(v.x, v.y + y, v.z); }

        // ---------------- loading in waves while the island plays (the facility models arrive first)
        void P2Start() { p2Dead = false; p2Ready = false; curSc = Sc.Island; if (AstronomicalEventMod.Inst != null) AstronomicalEventMod.Inst.StartCoroutine(LoadP2()); }
        IEnumerator LoadP2()
        {
            var ld = AstronomicalEventMod.Inst.Loader; var steps = new[] { new[] { "facility", "hall", "gyro" }, new[] { "jones", "rifle" }, new[] { "foundation" }, new[] { "slone" }, new[] { "guard" }, new[] { "gunnar" }, new[] { "hatch", "base" }, new[] { "scientist", "visitor", "zeropoint" }, new[] { "chapter3" } };
            yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.5f : 6f);
            foreach (var st in steps)
            {
                foreach (var id in st) { if (p2Dead) yield break; yield return ld.LoadProp(def, id); yield return null; }
                if (p2Dead) yield break; GC.Collect(); yield return new WaitForSeconds(AstronomicalDebug.NoMusic ? 0.3f : 3f);
            }
            if (p2Dead) yield break; try { P2Setup(); p2Ready = true; AstronomicalEventMod.Log.LogInfo("End21: part 2 is loaded"); } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("End21 P2Setup: " + e.Message + "\n" + e.StackTrace); }
        }
        GltfModel Take2(string id, float bright, bool lit)
        {
            var m = def.Prop(id); if (m == null || m.Pivot == null) { AstronomicalEventMod.Log.LogWarning("End21: prop '" + id + "' missing"); return null; }
            m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false); Brighten(m, bright, lit); models.Add(m); return m;
        }
        Act NewAct(string name, GltfModel m, float scale, bool armed, bool clone, float mir, params float[] vis)
        {
            if (m == null) return null; var go = clone ? Clone(m, "E" + name) : m.Pivot; go.SetActive(false); var a = new Act { Name = name, M = m, Go = go, Rig = CRig.Of(m, go), Scale = scale, Armed = armed, Vis = vis, Mir = mir };
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "Gun") { a.Gun = t; break; }
            if (armed && rifM != null) { var rf = UnityEngine.Object.Instantiate(rifM.Pivot, root.transform); rf.name = "ERifle" + name; rf.SetActive(false); p2Clones.Add(rf); a.Rifle = rf; a.RMod = rf.transform.Find("Model"); }
            acts.Add(a); AstronomicalDebug.Anchors[name.ToLower()] = () => a.Go != null ? a.Go.transform.position + Vector3.up * 1.2f * a.Scale : Vector3.zero; return a;
        }
        // waypoints: authoring coordinates (the loader mirrors x) -> stored mirrored; yaw mirrors too. y = absolute height of the feet (root-local)
        static void Wp(Act a, float t, float x, float y, float z, float yaw, string clip) { if (a != null) a.W.Add(new WP { T = t, X = (x + a.Ox) * a.Mir, Y = y, Z = z + a.Oz, Yaw = yaw * a.Mir, Clip = clip }); }

        // ---------------- carrying you into the rooms (the same method as Collision's cabin: J CLIENT's NoClip for a moment, move the rigidbody, hold it there with gravity cancelled)
        void NoClip2(bool on)
        {
            if (on) { noclipped2.Clear(); noclipT2 = Time.unscaledTime; foreach (var c in UnityEngine.Object.FindObjectsOfType<MeshCollider>()) { if (c == null || !c.enabled || c.isTrigger || (root != null && c.transform.IsChildOf(root.transform))) continue; c.enabled = false; noclipped2.Add(c); } }
            else { foreach (var c in noclipped2) if (c != null) c.enabled = true; noclipped2.Clear(); noclipT2 = -9f; }
        }
        void Reseat2(Vector3 headWorld)
        {
            try { NoClip2(true); var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Vector3 d = headWorld - pl.headCollider.transform.position; rb.position += d; rb.linearVelocity = Vector3.zero; lastReseat2 = Time.unscaledTime; reseatHoldPos = headWorld; reseatHoldT = Time.unscaledTime + 1.4f; AstronomicalEventMod.Log.LogInfo("End21: moved you to " + headWorld + " (" + d.magnitude.ToString("0") + " m)"); }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("End21: could not move you: " + e.Message); }
        }
        // turn the player so they look along a world direction (the room's +z or the path): GTPlayer.Turn, by reflection (the signature differs between game versions)
        static MethodInfo turnM; static bool turnTried;
        void FaceDir(Vector3 worldDir)
        {
            try
            {
                var cam = Camera.main; if (cam == null) return; worldDir.y = 0f; if (worldDir.sqrMagnitude < 0.01f) return; Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) return;
                float d = Mathf.DeltaAngle(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg);
                if (!turnTried) { turnTried = true; turnM = typeof(GTPlayer).GetMethod("Turn", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null); AstronomicalEventMod.Log.LogInfo("End21: GTPlayer.Turn " + (turnM != null)); }
                if (turnM != null && Mathf.Abs(d) > 2f) turnM.Invoke(GTPlayer.Instance, new object[] { d });
            }
            catch { }
        }
        void Carry3(Vector3 anchor, float yawDeg, float dt, float hx, float zMin, float zMax)
        {
            try
            {
                var pl = GTPlayer.Instance; var rb = pl.playerRigidBody; Quaternion q = root.transform.rotation * Quaternion.Euler(0f, yawDeg, 0f); Vector3 local = Quaternion.Inverse(q) * (pl.headCollider.transform.position - anchor);
                Vector3 c = new Vector3(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.y, -0.3f, 0.5f), Mathf.Clamp(local.z, zMin, zMax)); if ((c - local).sqrMagnitude > 1e-6f) rb.position += q * (c - local);
                rb.linearVelocity = (rb.linearVelocity - Physics.gravity * dt) * (1f - Mathf.Min(0.6f, 2f * dt));
            }
            catch (Exception e) { if (!carryErr) { carryErr = true; AstronomicalEventMod.Log.LogWarning("End21 carry: " + e.Message + " " + e.StackTrace); } }
        }

        // ---------------- the cuts between scenes: a quick dip to black (white for the 7:10 explosion); -1 = part 1 controls the black sphere
        static readonly float[] CUTS = { T_FAC, T_HALL, T_GYRO, T_SURF, T_BASE, T_OCEAN };
        static float P2Black(float t, out Color col)
        {
            col = Color.black; if (t < T_FAC - 1.2f) return -1f; float a = 0f;
            for (int i = 0; i < CUTS.Length; i++) { float c = CUTS[i]; a = Mathf.Max(a, Sm(c - 0.5f, c, t) * (1f - Sm(c + 0.15f, c + 0.9f, t))); }
            if (t >= T_BASE - 1.2f && t < T_BASE + 1f) col = Color.white;
            if (t >= T_FAC - 1.2f && t < T_FAC) a = 1f;      // part 1 already faded to black at the end
            if (t > T_FLOOD + 0.5f && t < T_OCEAN) a = Mathf.Max(a, Sm(T_FLOOD + 0.5f, T_FLOOD + 3.2f, t));        // the flood: dark water takes the screen, the ocean scene opens under it
            if (t >= T_OCEAN && t < T_OCEAN + 11f) { float uw = (1f - Sm(T_OCEAN + 2f, T_OCEAN + 10.5f, t)) * 0.9f; if (uw > a) { a = uw; col = new Color(0.01f, 0.07f, 0.16f); } }    // underwater until you surface (9:25)
            if (t >= T_END_VIS - 1.2f && t < T_END_VIS + 3.2f) { float fl = Sm(T_END_VIS - 0.9f, T_END_VIS, t) * (1f - Sm(T_END_VIS + 0.25f, T_END_VIS + 3f, t)); if (fl > a) { a = fl; col = Color.white; } }   // 10:03 the flash
            if (t > 658.2f) { float bk = Sm(658.2f, 660.2f, t); if (bk > a) { a = bk; col = Color.black; } }       // 11:00 the title card on black
            return a;
        }

        // ---------------- the path of the base: the WP empties of base.glb (root-local after placement), time -> distance along it
        readonly List<Vector3> bPts = new List<Vector3>(); float[] bCum; Vector3 bZpc, bChc, bCh0; float bTotal;
        static readonly float[] PT_T = { 435f, 448f, 452f, 458f, 462f, 467f, 470f, 473f, 478f, 484f, 487f, 492f, 494.5f, 497f, 500f, 503.5f, 506f, 508f, 512f };
        static readonly int[] PT_I = { 0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };       // index of the polyline point each time belongs to (points: WP01 .. WP20 -> 0 .. 19)
        float[] ptS;
        float PathS(float t)
        {
            if (ptS == null || ptS.Length == 0) return 0f; if (t <= PT_T[0]) return 0f; int n = PT_T.Length; if (t >= PT_T[n - 1]) return ptS[n - 1];
            int i = 0; while (i < n - 2 && t >= PT_T[i + 1]) i++;
            // monotone cubic Hermite (Fritsch-Carlson) so the leader eases through the corners instead of lurching
            Func<int, float> slope = k => (ptS[k + 1] - ptS[k]) / (PT_T[k + 1] - PT_T[k]);
            Func<int, float> tan = k => { if (k == 0) return slope(0); if (k == n - 1) return slope(n - 2); float s0 = slope(k - 1), s1 = slope(k); return s0 * s1 <= 0f ? 0f : 2f * s0 * s1 / (s0 + s1); };
            float h = PT_T[i + 1] - PT_T[i], u = (t - PT_T[i]) / h, u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * ptS[i] + (u3 - 2f * u2 + u) * h * tan(i) + (-2f * u3 + 3f * u2) * ptS[i + 1] + (u3 - u2) * h * tan(i + 1);
        }
        void BuildPath()
        {
            bPts.Clear(); if (baseM == null) return; var wps = new SortedDictionary<string, Transform>(); foreach (var t in baseM.Pivot.GetComponentsInChildren<Transform>(true)) { if (t.name.StartsWith("WP")) wps[t.name] = t; else if (t.name == "ZPC") bZpc = root.transform.InverseTransformPoint(t.position); else if (t.name == "CHC") bChc = root.transform.InverseTransformPoint(t.position); else if (t.name == "CH0") bCh0 = root.transform.InverseTransformPoint(t.position); else if (t.name == "Glass") baseGlass = t; else if (t.name == "Swirl") baseSwirl = t; }
            foreach (var kv in wps) { Vector3 p = root.transform.InverseTransformPoint(kv.Value.position); if (kv.Key != "WP00") bPts.Add(new Vector3(p.x, p.y > Y_BASE + 0.5f ? p.y - 1f : p.y, p.z)); }
            bCum = new float[bPts.Count]; for (int i = 1; i < bPts.Count; i++) bCum[i] = bCum[i - 1] + Vector3.Distance(Flat(bPts[i]), Flat(bPts[i - 1])); bTotal = bCum.Length > 0 ? bCum[bCum.Length - 1] : 0f;
            ptS = new float[PT_I.Length]; for (int i = 0; i < PT_I.Length; i++) ptS[i] = PT_I[i] < bCum.Length ? bCum[PT_I[i]] : bTotal;
            AstronomicalEventMod.Log.LogInfo("End21: base path " + bPts.Count + " points, " + bTotal.ToString("0") + " m");
        }
        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
        Vector3 PathAt(float s, out float yawDeg)
        {
            yawDeg = 0f; if (bPts.Count < 2) return Vector3.zero; s = Mathf.Clamp(s, 0f, bTotal); int i = 0; while (i < bCum.Length - 2 && s > bCum[i + 1]) i++;
            Vector3 a = bPts[i], b = bPts[i + 1]; float seg = Mathf.Max(0.001f, bCum[i + 1] - bCum[i]), u = (s - bCum[i]) / seg; Vector3 d = Flat(b - a); yawDeg = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return Vector3.Lerp(a, b, u);
        }
        // a point a little round the corner for smooth heading: average the heading over +-1.2 m
        float PathYaw(float s) { float y0, y1; PathAt(s - 1.2f, out y0); PathAt(s + 1.2f, out y1); return Mathf.LerpAngle(y0, y1, 0.5f); }

        // ---------------- where you are held in each scene (root-local head target, the yaw of the clamp frame, the half width / back / front limits)
        bool SceneAnchor(float t, Sc sc, out Vector3 a, out float yawDeg, out float hx, out float zMin, out float zMax)
        {
            a = Vector3.zero; yawDeg = 0f; hx = 3f; zMin = -1f; zMax = 1f;
            switch (sc)
            {
                case Sc.Fac: a = new Vector3(0f, Y_FAC + 1.6f, -5.4f); hx = 3.5f; zMin = -1.5f; zMax = 2.4f; return true;
                case Sc.Hall:
                    { float z = HallZ(t); a = new Vector3(0f, Y_HALL + 1.6f, z); hx = 1.3f; zMin = -1.0f; zMax = 0.6f; return true; }
                case Sc.Gyro: a = new Vector3(-3.5f, Y_GYRO + 1.6f, -4.8f); hx = 2.5f; zMin = -1.5f; zMax = 2.5f; return true;
                case Sc.Base:
                    {
                        float yaw; float sA = Mathf.Max(0f, BaseS(t) - 3.2f); Vector3 p = PathAt(sA, out yaw); yawDeg = PathYaw(sA);
                        float drop = 12f * (1f - Mathf.Pow(Mathf.Clamp01((t - (T_BASE + 0.8f)) / 1.7f), 2f));      // 7:12: down the shaft (12 m of free fall), landed at 7:15
                        a = new Vector3(p.x, Y_BASE + 1.6f + drop, p.z);
                        hx = t < T_CHAM ? 1.2f : 3.5f; zMin = -1.2f; zMax = t < T_CHAM ? 1.0f : 3.0f; return true;
                    }
                case Sc.Ocean: a = new Vector3(0f, OceanHeadY(t), 0f); hx = 0.8f; zMin = -0.8f; zMax = 0.8f; return true;
            }
            return false;
        }
        float BaseS(float t) { return PathS(t); }
        static string RoomTag(Sc s) { return s.ToString(); }

        // ---------------- one frame of part 2
        void P2Frame(float t, float dt, float tp, Camera cam)
        {
            Sc sc = SceneAt(t); bool room = IsRoom(sc);
            if (noclipped2.Count > 0 && Time.unscaledTime - noclipT2 > 0.6f) NoClip2(false);
            if (!p2Ready) return;
            if (sc != curSc) OnScene(curSc, sc, t);
            curSc = sc;
            // ---- you: held in the room (or free on the island / surface)
            Vector3 an; float ayaw, hx, z0, z1;
            if (SceneAnchor(t, sc, out an, out ayaw, out hx, out z0, out z1))
            {
                Vector3 aw = W(an);
                float dd = Vector3.Distance(HeadNow(), aw);
                if (dd > 30f && reseatTries2 < 8 && Time.unscaledTime - lastReseat2 > 0.7f) { reseatTries2++; Reseat2(aw); }
                else if (dt > 0f) Carry3(aw, ayaw, dt, hx, z0, z1);
                if (dd > 6f && Time.unscaledTime - dbgT > 1f) { dbgT = Time.unscaledTime; AstronomicalEventMod.Log.LogInfo("End21: " + sc + " t=" + t.ToString("0.0") + " you are " + dd.ToString("0.0") + " m from your hold point: head " + HeadNow() + " hold " + aw); }
            }
            else if (Time.unscaledTime < reseatHoldT && dt > 0f) Carry3(reseatHoldPos, 0f, dt, 0.5f, -0.5f, 0.5f);
            // ---- rooms, lights, effects
            RoomsFrame(t, tp, dt, sc);
            // ---- the actors
            foreach (var a in acts) ActFrame(a, t, dt, sc);
            SceneFx(t, tp, dt, sc, cam);
        }
        void OnScene(Sc from, Sc to, float t)
        {
            reseatTries2 = 0;
            if (!IsRoom(from) && from != Sc.Ocean && (IsRoom(to) || to == Sc.Ocean) && !haveHome) { Vector3 hh = HeadNow(); if (hh.y < originW.y + 150f) { homeHead = hh; haveHome = true; } }      // (only when you really are on the island: scrubbing can leave you in a room)
            Vector3 an; float ayaw, hx, z0, z1;
            if (SceneAnchor(t, to, out an, out ayaw, out hx, out z0, out z1))
            {
                Vector3 aw = W(an); Reseat2(aw);
                Vector3 face = root.transform.rotation * Quaternion.Euler(0f, ayaw, 0f) * Vector3.forward; if (to == Sc.Gyro) face = root.transform.rotation * (new Vector3(0.45f, 0f, 1f)); if (to == Sc.Ocean) face = root.transform.rotation * Vector3.forward;
                FaceDir(face);
            }
            else if (to == Sc.Surface) { Reseat2(W(new Vector3(stageX, stageY + 1.9f, stageZ))); haveHome = false; FaceDir(root.transform.rotation * Vector3.forward); }
            else if (to == Sc.Island) { Reseat2(haveHome && homeHead.y < originW.y + 150f ? homeHead : originW + Vector3.up * 1.9f); haveHome = false; }
            if (to == Sc.Ocean || from == Sc.Ocean) OceanSwitch(to == Sc.Ocean, t);
        }
        // the placement of a room model so its file origin sits at (0, y, 0) of the root
        void Place(GltfModel m, float y, bool on)
        {
            if (m == null) return; if (m.Pivot.activeSelf != on) m.Pivot.SetActive(on); if (!on) return; m.Pivot.transform.localRotation = Quaternion.identity; PlaceByFile(m, new Vector3(0f, y, 0f));
        }
        Vector3 hallLight; float baseGyro = 1f; float hatchX = 0f, hatchZ = 4f;
        // the floor near YOUR floor: a ray from 5 m above the spawn point down (the plain GroundY starts in the sky and finds the hill / canopy above a spawn room like the Forest's stump room)
        float GroundNear(float x, float z)
        {
            var hits = Physics.RaycastAll(W(new Vector3(x, stageY + 40f, z)), Vector3.down, 90f, ~0, QueryTriggerInteraction.Ignore); float best = float.MaxValue, bestY = stageY;
            foreach (var h in hits) { if (h.normal.y < 0.5f) continue; float gy = h.point.y - originW.y, d = Mathf.Abs(gy - stageY); if (d < best) { best = d; bestY = gy; } }       // the surface nearest to the clearing's floor (not the hill / canopy above it)
            return best < 7f ? bestY : stageY;
        }
        // an open flat clearing near where you started (the Forest's spawn is a small stump room: the Seven's fight and the hatch have to be OUTSIDE it, where you can see them). First fit by distance:
        // flat within 0.8 m round it, nothing solid within 2.6 m at chest height, open sky above. stageX / stageZ are root-local; the hatch goes 7 m ahead of it.
        float stageX, stageZ, stageY; bool stageFound;
        void FindStage()
        {
            stageX = 0f; stageZ = 0f; stageY = 0f; stageFound = false; float[] rs = { 0f, 6f, 10f, 14f, 18f, 23f, 29f, 36f, 44f, 54f }; int tested = 0;
            foreach (float r in rs)
            {
                int n = r < 1f ? 1 : 24;
                for (int k = 0; k < n; k++)
                {
                    float a = k * 15f * Mathf.Deg2Rad, x = Mathf.Sin(a) * r, z = Mathf.Cos(a) * r; tested++; RaycastHit h; Vector3 top = W(new Vector3(x, 45f, z));
                    if (!Physics.Raycast(top, Vector3.down, out h, 120f, ~0, QueryTriggerInteraction.Ignore)) continue; float g = h.point.y - originW.y; if (g < -12f || g > 30f) continue; if (h.normal.y < 0.9f) continue;
                    bool flat = true; for (int q = 0; q < 4 && flat; q++) { float qa = q * 90f * Mathf.Deg2Rad; RaycastHit h2; if (!Physics.Raycast(h.point + new Vector3(Mathf.Sin(qa) * 3f, 8f, Mathf.Cos(qa) * 3f), Vector3.down, out h2, 20f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(h2.point.y - h.point.y) > 0.8f) flat = false; }
                    if (!flat) continue; if (Physics.CheckSphere(h.point + Vector3.up * 2.8f, 2.6f, ~0, QueryTriggerInteraction.Ignore)) continue; if (Physics.Raycast(h.point + Vector3.up * 1.2f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    stageX = x; stageZ = z; stageY = g; stageFound = true; break;
                }
                if (stageFound) break;
            }
            hatchX = stageX; hatchZ = stageZ + 4f;
            AstronomicalEventMod.Log.LogInfo("End21: the Seven's clearing is at (" + stageX.ToString("0.0") + ", " + stageZ.ToString("0.0") + ") height " + stageY.ToString("0.0") + (stageFound ? "" : " (none found, " + tested + " spots tried: using the spawn)"));
        }
        void RoomsFrame(float t, float tp, float dt, Sc sc)
        {
            Place(facM, Y_FAC, sc == Sc.Fac); Place(hallM, Y_HALL, sc == Sc.Hall); Place(gyroM, Y_GYRO, sc == Sc.Gyro); Place(baseM, Y_BASE, sc == Sc.Base);
            if (hatchM != null) { bool on = sc == Sc.Surface; if (hatchM.Pivot.activeSelf != on) hatchM.Pivot.SetActive(on); if (on) { hatchM.Pivot.transform.localRotation = Quaternion.identity; PlaceByFile(hatchM, new Vector3(hatchX, stageFound ? stageY : GroundNear(hatchX, hatchZ), hatchZ)); } }
            Color lc1 = new Color(1f, 0.32f, 0.22f), lc2 = new Color(0.7f, 0.85f, 1f); Vector3 p1 = Vector3.zero, p2 = Vector3.zero; float i1 = 0f, i2 = 0f; Color amb = RenderSettings.ambientLight;
            switch (sc)
            {
                case Sc.Fac: p1 = new Vector3(0f, Y_FAC + 5.5f, 0f); p2 = new Vector3(0f, Y_FAC + 6.2f, 4f); i1 = 2.2f + 0.5f * Mathf.Sin(t * 5f); i2 = 1.8f; amb = new Color(0.45f, 0.3f, 0.3f); break;
                case Sc.Hall: { float z = HallZ(t) + 2f; p1 = new Vector3(0f, Y_HALL + 3f, z); p2 = new Vector3(0f, Y_HALL + 3f, z + 6f); lc1 = z < 17f ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.25f, 0.18f); lc2 = z < 17f ? new Color(0.5f, 1f, 0.7f) : new Color(1f, 0.5f, 0.2f); i1 = 2.4f; i2 = 1.6f; amb = z < 17f ? new Color(0.25f, 0.45f, 0.3f) : new Color(0.45f, 0.2f, 0.18f); break; }
                case Sc.Gyro: p1 = new Vector3(0f, Y_GYRO + 8f, 4f); p2 = new Vector3(0f, Y_GYRO + 3f, -3f); lc1 = new Color(0.7f, 0.4f, 1f); lc2 = new Color(0.9f, 0.9f, 1f); i1 = 3f + 0.8f * Mathf.Sin(t * 7f); i2 = 1.5f; amb = new Color(0.32f, 0.25f, 0.5f); break;
                case Sc.Base: { float yy; float sA = BaseS(t); Vector3 p = PathAt(sA, out yy); Color rc = BaseColor(t); p1 = new Vector3(p.x, Y_BASE + 3f, p.z); p2 = new Vector3(p.x, Y_BASE + 2.5f, p.z - 6f); lc1 = rc; lc2 = Color.Lerp(rc, Color.white, 0.5f); i1 = 2.4f; i2 = 1.2f; amb = rc * 0.5f; if (t >= T_CHAM - 2f) { p1 = new Vector3(bChc.x, Y_BASE + 14f, bChc.z + 6f); p2 = new Vector3(bChc.x, Y_BASE + 4f, bChc.z - 10f); i1 = 4f; i2 = 2f; lc1 = new Color(0.7f, 0.4f, 1f); } break; }
            }
            facLight.transform.position = W(p1); facLight2.transform.position = W(p2); facLight.color = lc1; facLight2.color = lc2; facLight.intensity = Lit * i1; facLight2.intensity = Lit * i2; facLight.range = 44f; facLight2.range = 44f;
            if (IsRoom(sc)) RenderSettings.ambientLight = amb;
            // ---- doors: the blast door (4:56), the security door (4:34 Slone, 5:06 the guard)
            if (sc == Sc.Fac && doorL != null && doorR != null)
            {
                if (!doorsInit) { doorsInit = true; doorLBase = doorL.localPosition.x; doorRBase = doorR.localPosition.x; doorSBase = doorS != null ? doorS.localPosition.z : 0f; var rl = doorL.GetComponentInChildren<Renderer>(); var rr = doorR.GetComponentInChildren<Renderer>(); doorSgnL = rl != null ? Mathf.Sign(doorL.InverseTransformPoint(rl.bounds.center).x) : -1f; doorSgnR = rr != null ? Mathf.Sign(doorR.InverseTransformPoint(rr.bounds.center).x) : 1f; var rs = doorS != null ? doorS.GetComponentInChildren<Renderer>() : null; doorSgnS = rs != null ? Mathf.Sign(doorS.InverseTransformPoint(rs.bounds.center).z) : 1f; }
                float open = Sm(295.9f, 296.4f, t); doorL.localPosition = new Vector3(doorLBase + doorSgnL * 3.1f * open, doorL.localPosition.y, doorL.localPosition.z); doorR.localPosition = new Vector3(doorRBase + doorSgnR * 3.1f * open, doorR.localPosition.y, doorR.localPosition.z);
                if (doorS != null) { float so = Mathf.Max(Sm(272.8f, 273.6f, t) * (1f - Sm(277.5f, 278.3f, t)), Sm(305.2f, 305.9f, t) * (1f - Sm(309.5f, 310.3f, t))); doorS.localPosition = new Vector3(doorS.localPosition.x, doorS.localPosition.y, doorSBase + doorSgnS * 2.7f * so); }
            }
            if (facR != null && sc == Sc.Fac) { }
            if (gyroR != null && sc == Sc.Gyro) { float spin = t < 392f ? 1f : 3.5f; gyroR.Pose(FindClip(gyroM, "Animation"), t * spin); }
            // ---- the hatch cover slides away at 6:56
            if (sc == Sc.Surface && hatchCover != null) { if (!coverInit) { coverInit = true; coverBase0 = hatchCover.localPosition; } float o = Sm(415.2f, 417.2f, t); hatchCover.localPosition = coverBase0 + new Vector3(-2.6f * o, 0f, 0f); }
            // ---- the base and its chamber
            if (sc == Sc.Base)
            {
                if (baseR != null) baseR.Pose(FindClip(baseM, "Animation"), t * 1.0f);
                if (baseGlass != null) { bool gl = false; /* the dome is left out: the Glow shader turns it into a solid cyan sky; the shattering is particles */ if (baseGlass.gameObject.activeSelf != gl) baseGlass.gameObject.SetActive(gl); }
                bool zpOn = zpM2 != null && t >= T_CHAM - 6f && t < T_FLOOD + 1.5f; if (zpM2 != null && zpM2.Pivot.activeSelf != zpOn) zpM2.Pivot.SetActive(zpOn);
                if (zpOn) { var zt = zpM2.Pivot.transform; zt.localScale = Vector3.one * 5.5f * (1f + 0.05f * Mathf.Sin(t * 6f)); zt.localRotation = Quaternion.Euler(0f, t * 25f, 0f); PlaceByFile(zpM2, bZpc); if (zpR2 != null) zpR2.Pose(FindClip(zpM2, "Animation"), t); }
            }
            else if (zpM2 != null && zpM2.Pivot.activeSelf) zpM2.Pivot.SetActive(false);
        }
        // the colour of the base's lights at each moment of the walk (timeline 7:15 - 8:28)
        Color BaseColor(float t)
        {
            if (t < 450f) return new Color(0.3f, 0.6f, 1f); if (t < 458f) return new Color(1f, 0.3f, 0.6f); if (t < 470f) return new Color(1f, 0.3f, 0.2f); if (t < 486f) return new Color(0.3f, 0.7f, 1f); if (t < 495f) return new Color(0.3f, 0.6f, 1f);
            if (t < 500f) return new Color(1f, 0.6f, 0.2f); if (t < 505f) return new Color(1f, 0.3f, 0.25f); return new Color(0.7f, 0.4f, 1f);
        }

        void Colliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) { if (mf.sharedMesh == null) continue; var mc = mf.gameObject.GetComponent<MeshCollider>(); if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mf.gameObject.layer = floorLayer; }
        }
        static void PlaceByFile(GltfModel m, Vector3 localTarget)
        {
            if (m == null || m.Pivot == null || m.Model == null) return;
            var root0 = m.Pivot.transform.parent; if (root0 == null) return; Vector3 cur = root0.InverseTransformPoint(m.Model.transform.position); m.Pivot.transform.localPosition += localTarget - cur;
        }
        static float Ease(float u, bool ein, bool eout)
        {
            if (ein && eout) return u * u * (3f - 2f * u); if (ein) return u * u * (2f - u); if (eout) { float x = 1f - u; return 1f - x * x * (2f - x); } return u;
        }

        // ---------------- an actor: waypoints (eased between stops) or a path function -> position, yaw and the right clip; walk / run phases follow the DISTANCE walked so feet never slide; clips crossfade
        void ActFrame(Act a, float t, float dt, Sc sc)
        {
            if (a == null || a.Go == null) return;
            bool vis = false; for (int i = 0; i + 1 < a.Vis.Length; i += 2) if (t >= a.Vis[i] && t < a.Vis[i + 1]) vis = true; if (a.Vis.Length == 0) vis = true; vis &= p2Ready;
            if (a.Go.activeSelf != vis) a.Go.SetActive(vis); if (a.Rifle != null && !vis) a.Rifle.SetActive(false); if (!vis) { a.LastT = -9f; return; }
            Vector3 pos; float yaw, speed, ct, dist = 0f; string clip; bool loco = false; bool onPath = a.PathOn != null && a.PathOn(t);
            if (onPath)
            {
                pos = a.Pos(t); yaw = a.YawF(t); Vector3 p2 = a.Pos(t + 0.1f), p0 = a.Pos(t - 0.1f); speed = Flat(p2 - p0).magnitude / 0.2f; clip = "Idle"; ct = 0f; dist = BaseDist(a, t);
                int pk = 0; for (int i = 0; i < a.PClips.Count; i++) if (a.PClips[i].T <= t) pk = i; if (a.PClips.Count > 0) { clip = a.PClips[pk].Clip; ct = t - a.PClips[pk].T; }
                if (clip == "Run" || clip == "RunAim" || clip == "Walk") { if (speed > 0.3f) { loco = true; clip = LocoClip(clip, speed); float per, str; GaitOf(clip, out per, out str); ct = dist / str * per; } else { clip = "Idle"; ct = t; } }
            }
            else
            {
                if (a.W.Count == 0) return;
                if (a.cum == null) { a.cum = new float[a.W.Count]; a.seg = new float[a.W.Count]; for (int i = 1; i < a.W.Count; i++) { float d = a.W[i].Cut ? 0f : Mathf.Sqrt((a.W[i].X - a.W[i - 1].X) * (a.W[i].X - a.W[i - 1].X) + (a.W[i].Z - a.W[i - 1].Z) * (a.W[i].Z - a.W[i - 1].Z)); a.cum[i] = a.cum[i - 1] + d; a.seg[i - 1] = d / Mathf.Max(0.0001f, a.W[i].T - a.W[i - 1].T); } }
                int k = 0; for (int i = 0; i < a.W.Count; i++) if (a.W[i].T <= t) k = i; var w0 = a.W[k]; bool hasNext = k + 1 < a.W.Count, cutNext = hasNext && a.W[k + 1].Cut; var w1 = hasNext && !cutNext ? a.W[k + 1] : w0;
                float span = Mathf.Max(0.0001f, (hasNext ? a.W[k + 1].T : w0.T) - w0.T), u = hasNext ? Mathf.Clamp01((t - w0.T) / span) : 0f;
                float sp0 = hasNext ? a.seg[k] : 0f; bool ein = k == 0 || a.seg[k - 1] < 0.25f, eout = k + 2 >= a.W.Count || a.seg[k + 1] < 0.25f; float ue = sp0 > 0.25f ? Ease(u, ein, eout) : u;
                float x = Mathf.Lerp(w0.X, w1.X, ue), z = Mathf.Lerp(w0.Z, w1.Z, ue), y = Mathf.Lerp(w0.Y, w1.Y, ue); dist = hasNext ? a.cum[k + 1] - a.cum[k] : 0f; speed = sp0;
                yaw = w0.Yaw; if (speed > 0.25f) { yaw = Mathf.Atan2(w1.X - w0.X, w1.Z - w0.Z) * Mathf.Rad2Deg; if (u > 0.8f) yaw = Mathf.LerpAngle(yaw, w1.Yaw, Sm(0.8f, 1f, u)); } else if (hasNext && !cutNext) yaw = Mathf.LerpAngle(w0.Yaw, w1.Yaw, Sm(0.7f, 1f, u));
                if (speed > 0.25f && u < 0.12f && k > 0 && !w0.Cut) yaw = Mathf.LerpAngle(w0.Yaw, yaw, Sm(0f, 0.12f, u));
                clip = w0.Clip ?? "Idle"; bool forced = clip.StartsWith("!"); if (forced) clip = clip.Substring(1);
                if (a.Creature) ct = t - w0.T;
                else if (!forced && speed > 0.25f && (clip == "Idle" || clip == "Walk" || clip == "Run" || clip == "RunAim" || clip == "Sneak" || clip == "Ready" || clip == "Fire" || clip == "Alert" || clip == "Hold" || clip == "Aim"))
                {
                    loco = true; clip = LocoClip(clip, speed);
                    float per, str; GaitOf(clip, out per, out str); ct = (a.cum[k] + dist * ue) / str * per;
                }
                else ct = t - w0.T;
                pos = new Vector3(x, y, z);
            }
            if (!loco && oneShot.Contains(clip)) { var cc = FindClip(a.M, "H_" + clip); if (cc != null) ct = Mathf.Min(Mathf.Max(ct, 0f), cc.Duration - 0.05f); }
            // crossfade: when the clip changes, blend from the old clip (kept running) to the new one over 0.28 s (not while scrubbing)
            bool scrub = Mathf.Abs(t - a.LastT) > 0.5f || dt <= 0f || a.LastT < -8f;
            if (a.Cur != clip) { if (a.Cur != null && !scrub) { a.Prev = a.Cur; a.PrevT = a.CurT; a.Blend = 0f; } else { a.Prev = null; a.Blend = 1f; } a.Cur = clip; }
            if (a.Prev != null) { a.PrevT += dt; a.Blend = Mathf.Min(1f, a.Blend + dt / 0.28f); if (a.Blend >= 1f) a.Prev = null; }
            a.CurT = ct; a.LastT = t;
            if (a.Prev != null) PlayClip(a.Rig, a.M, "H_" + a.Prev, a.PrevT, "H_" + clip, ct, Sm(0f, 1f, a.Blend)); else PlayClip(a.Rig, a.M, "H_" + clip, ct);
            float ground = a.Ground && !onPath ? GroundNear(pos.x, pos.z) + pos.y : pos.y; var tr = a.Go.transform; tr.localPosition = new Vector3(pos.x, ground - 0.09f * a.Scale, pos.z); tr.localRotation = Quaternion.Euler(0f, yaw, 0f); tr.localScale = Vector3.one * a.Scale;
            if (a.Armed) Rifle2(a, clip, ct, dt);
        }
        float BaseDist(Act a, float t) { float s = PathS(t); return a.Name == "Visitor" ? s - 1.0f : s; }
        void Rifle2(Act a, string clip, float ct, float dt)
        {
            if (a.Rifle == null || a.Gun == null) return; bool show = a.Go.activeInHierarchy && rifleSet.Contains(clip); if (a.Rifle.activeSelf != show) a.Rifle.SetActive(show); if (!show) return;
            float gs = a.Name == "Foundation" ? 0.95f : (a.Name.StartsWith("Guard") ? 0.9f : 0.85f); float ks = gs * Mathf.Abs(a.Gun.lossyScale.x); var tr = a.Rifle.transform; tr.rotation = a.Gun.rotation; tr.localScale = Vector3.one * ks; if (a.RMod != null) tr.position += a.Gun.position - a.RMod.position;
            bool fire = clip == "Fire" || clip == "FireK"; float ph = Mathf.Repeat(ct, 2.4f); int idx = fire && ph < 0.9f ? (int)(ph / 0.1f) : -1; if (idx < 0) { a.Shot = -1; return; }
            if (idx == a.Shot || paused || dt <= 0f) return; a.Shot = idx; Vector3 target = a.Aim != null ? a.Aim() : a.Go.transform.position + a.Go.transform.forward * 30f; Vector3 mz = a.Gun.position + a.Gun.rotation * new Vector3(0f, 0.12f * ks, 0.77f * ks), dir = (target - mz).normalized;
            for (int q = 0; q < 6; q++) Emit(fx, mz, a.Gun.rotation * new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(4f, 12f)), new Color(1f, UnityEngine.Random.Range(0.7f, 0.95f), 0.4f, 0.9f), UnityEngine.Random.Range(0.12f, 0.32f), UnityEngine.Random.Range(0.08f, 0.18f));
            Emit(fx, mz, Vector3.zero, new Color(1f, 0.9f, 0.5f, 1f), 0.9f, 0.05f); Emit(fx, mz + dir * 0.8f, dir * 150f + UnityEngine.Random.insideUnitSphere * 1.2f, new Color(1f, 0.85f, 0.4f, 1f), 0.3f, Mathf.Clamp((target - mz).magnitude / 150f, 0.1f, 1.2f));
        }

        void P2Cleanup()
        {
            p2Dead = true; foreach (var g in p2Clones) if (g != null) UnityEngine.Object.Destroy(g); p2Clones.Clear(); acts.Clear();
            if (curSc != Sc.Island && curSc != Sc.Surface) { try { Reseat2(haveHome ? homeHead : originW + Vector3.up * 1.9f); } catch { } } curSc = Sc.Island; NoClip2(false); OceanCleanup();
            facM = hallM = gyroM = hatchM = baseM = ch3M = jonM = fouM = slnM = gunM = sciM = visM = grdM = rifM = zpM2 = null; doorL = doorR = doorS = hatchCover = baseGlass = baseSwirl = null; giant = null; giantGo = null; p2Ready = false; glassGone = false; doorsInit = false; coverInit = false; haveHome = false;
        }
    }
}
