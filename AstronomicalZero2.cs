using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Zero Crisis, part 2: the models, the cast (every actor is a list of position / clip keys, a pure function of t), the pink skin over the map's floor and the synthesised sound
    internal sealed partial class AstronomicalZero
    {
        static readonly Vector3 P1 = new Vector3(-25f, 0f, 150f), P2 = new Vector3(60f, 0f, 172f), P3 = new Vector3(-62f, 0f, 172f), P4 = new Vector3(12f, 0f, 196f);
        static readonly float[] PORTAL_CLOSE = { 320f, 368f, 399f, 442f };
        static readonly Vector3 ZPOS = new Vector3(0f, IY + 250f, 640f), SpireC = new Vector3(0f, 0f, 265f), SpireTop = new Vector3(0f, SPY, 265f);
        Vector3 BusRoof = new Vector3(0f, -2f, 0f);
        // the bus circles the spawn at 96 m (the game ends your session if you leave the map's box, so it never flies out)
        static float BusAng(float t) { return Mathf.Lerp(-2.4f, 1.1f, Mathf.Clamp01((t - 4f) / 32f)); }
        static Vector3 BusPos(float t) { float a = BusAng(t); return new Vector3(46f * Mathf.Sin(a), 96f + 1.2f * Mathf.Sin(t * 0.45f), 20f + 46f * Mathf.Cos(a)); }
        static float BusYaw(float t) { float a = BusAng(t); return Mathf.Atan2(Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg; }
        // the climb: three turns of a helix round the Spire (radius 24 m), easing into the deck at the top
        Vector3 ClimbPos(float t)
        {
            float u = Mathf.Clamp01((t - 508f) / 44f); float th = -2f * Mathf.PI * 2f * (1f - u); float r = Mathf.Lerp(24f, 9.5f, Sm(0.86f, 1f, u)); float y = Mathf.Lerp(3f, SPY, Sm(0f, 1f, u));
            return SpireC + new Vector3(r * Mathf.Sin(th), 0f, -r * Mathf.Cos(th)) + Vector3.up * (y + spireY);
        }

        // ---------------------------------------------------------------- models
        sealed class K { public float T, X, Z, Yaw, Y; public string Clip; }
        static K Kf(float t, float x, float z, float yawDeg, string clip, float y = float.NaN) { return new K { T = t, X = x, Z = z, Yaw = yawDeg, Y = y, Clip = clip }; }
        sealed class Act
        {
            public string Id; public GltfModel M; public GameObject Go; public CRig Rig; public float Scale = 1f, Seed, YawNow; public string Cur, Prev; public float CurT, PrevT, Blend = 1f, LastT = -9f; public readonly List<K> Keys = new List<K>(); public Transform Hand; public Vector3 PosL; public bool Vis;
            public Func<float, K> Ov;
        }
        readonly List<Act> acts = new List<Act>();
        readonly Dictionary<string, Act> actBy = new Dictionary<string, Act>();
        readonly Dictionary<string, GltfModel> charM = new Dictionary<string, GltfModel>();
        GltfModel busM, spireM, wolfM, devM, metM, zpM, bflyM, cryM, lotM, riftM, rockM, chamM;
        static readonly string[] CHARS = { "jones", "foundation", "kratos", "xeno", "sarah", "chunli", "trigger", "hybrid", "mancake", "terminator", "dog", "meowscle", "chief", "ryu", "tntina", "ripley", "predator" };
        GltfModel peelyM;

        static readonly HashSet<string> GLOWN = new HashSet<string>();
        void Brighten(GltfModel mdl, float k)
        {
            foreach (var r in mdl.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; bool flare = false; var src = r.sharedMaterials; origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src)); var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : ""; if (n.StartsWith("SF_")) n = n.Substring(3); Material mt;
                    bool alpha = n.StartsWith("A_") || n.StartsWith("Globe") || n.StartsWith("Flare") || (src[i] != null && src[i].shader != null && src[i].shader.name == "Sprites/Default"), glow = n.StartsWith("G_");
                    if (alpha) mt = kit.Glow(Color.white, null, 3000); else if (glow) mt = kit.Flat(Color.white * k); else mt = kit.Lit(Color.white * Mathf.Min(k, 1.3f), null, 0.28f);
                    try { mt.SetFloat("_Cull", 0f); mt.doubleSidedGI = true; } catch { }
                    try
                    {
                        if (src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture;
                        if (n.StartsWith("Globe")) mt.color = new Color(0.45f, 1f, 0.7f, 0.55f); else if (n.StartsWith("Flare")) mt.color = new Color(0.6f, 1f, 0.8f, 0.45f);
                        else if (src[i] != null && !alpha && !glow && src[i].mainTexture == null) mt.color = src[i].color * k;
                    }
                    catch { }
                    if (n.StartsWith("A_Crystal")) mt.color = new Color(1f, 0.8f, 1f, 0.95f);
                    nw[i] = mt; if (n.StartsWith("Flare") || n.StartsWith("Globe") || n.StartsWith("Transparent")) flare = true;
                }
                r.sharedMaterials = nw; if (flare) r.enabled = false;
            }
        }
        GltfModel Take(string id, float bright, bool keepMats = false)
        {
            var m = def.Prop(id); if (m == null || m.Pivot == null) { JsLiveEventsMod.Log.LogWarning("ZeroCrisis: prop '" + id + "' missing"); return null; }
            m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false); if (!keepMats) Brighten(m, bright); models.Add(m); return m;
        }
        static GltfModel.Clip FindClip(GltfModel m, string n) { if (m == null) return null; var c = m.FindClipExact(n); return c ?? (m.Clips.Count > 0 ? m.Clips[0] : null); }
        static void PlayClip(CRig rig, GltfModel m, string a, float ta, string b = null, float tb = 0f, float w = 0f)
        {
            var ca = FindClip(m, a); if (ca == null) return; var cb = b != null && w > 0.001f ? FindClip(m, b) : null;
            if (cb != null && cb.Channels.Count == ca.Channels.Count) rig.Pose(ca, ta, cb, tb, w); else rig.Pose(ca, ta);
        }
        static void PlaceGo(GameObject g, Vector3 pos, float scale, Quaternion rot, bool on)
        {
            if (g == null) return; if (g.activeSelf != on) g.SetActive(on); if (!on) return; var tr = g.transform; tr.localPosition = pos; tr.localRotation = rot; tr.localScale = Vector3.one * scale;
        }
        static Transform Sub(GltfModel m, string name) { if (m == null) return null; foreach (var t in m.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
        static Transform SubStart(GameObject g, string pre) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(pre)) return t; return null; }
        // the scale that makes a model `h` metres tall (or long) from the world bounds it had when loaded
        static float Fit(GltfModel m, float h, bool longest = false) { if (m == null) return 1f; var s = m.RendererBounds.size; float v = longest ? Mathf.Max(s.x, s.y, s.z) : s.y; return h / Mathf.Max(0.05f, v); }

        void BuildModels()
        {
            foreach (var id in CHARS) charM[id] = Take(id, 1.25f);
            peelyM = Take("peely", 1.25f); busM = Take("bus", 1.15f); spireM = Take("spire", 1.2f); wolfM = Take("wolf", 1.1f); devM = Take("device", 1.2f); metM = Take("meteor", 1.2f); zpM = Take("zeropoint", 1.2f); bflyM = Take("butterfly", 1.3f);
            cryM = Take("crystals", 1.2f); lotM = Take("lotus", 1.2f); riftM = Take("rift", 1.2f); rockM = Take("rocks", 1.2f); chamM = Take("chamber", 1.2f);
        }
        Act NewAct(string id, string model, float height, bool clone)
        {
            GltfModel m; if (!charM.TryGetValue(model, out m) || m == null) return null;
            GameObject go = clone ? UnityEngine.Object.Instantiate(m.Pivot, root.transform) : m.Pivot; if (clone) { go.name = "Z" + id; clones.Add(go); } go.SetActive(false);
            var a = new Act { Id = id, M = m, Go = go, Rig = CRig.Of(m, go), Scale = Fit(m, height), Seed = Hash(acts.Count, 5) * 6f }; acts.Add(a); actBy[id] = a; return a;
        }

        // ---------------------------------------------------------------- the cast
        void BuildActors()
        {
            acts.Clear(); actBy.Clear(); var used = new HashSet<string>();
            Action<string, string, float> A = (id, mo, h) => { bool dup = used.Contains(mo); used.Add(mo); NewAct(id, mo, h, dup); };
            A("jones", "jones", 1.95f); A("found", "foundation", 2.35f); A("kratos", "kratos", 2.15f); A("sarah", "sarah", 1.8f); A("chunli", "chunli", 1.75f); A("trigger", "trigger", 1.9f); A("mancake", "mancake", 1.9f); A("meow", "meowscle", 2.3f);
            A("dog", "dog", 1.8f); A("hybrid", "hybrid", 1.95f); A("xeno1", "xeno", 2.5f); A("xeno2", "xeno", 2.5f); A("xeno3", "xeno", 2.5f); A("term1", "terminator", 2.05f); A("term2", "terminator", 2.05f);
            A("chief", "chief", 2.1f); A("ryu", "ryu", 1.8f); A("tntina", "tntina", 1.85f); A("ripley", "ripley", 1.75f); A("predator", "predator", 2.35f); A("xeno4", "xeno", 2.5f);
            BuildTracks();
            // the Portal Device: a glass globe in Jones' right hand
            if (devM != null && actBy.ContainsKey("jones"))
            {
                var jh = SubStart(actBy["jones"].Go, "hand_r"); actBy["jones"].Hand = jh; devGo = devM.Pivot; devGo.SetActive(false); devScale = Fit(devM, 0.34f); devGlow = Quad("ZDevGlow", kit.Glow(new Color(0.5f, 1f, 0.8f, 0.9f), glowTex, 3003)); devGlow.gameObject.SetActive(false);
            }
        }
        GameObject devGo; float devScale = 1f; Transform devGlow;
        static float Face(float ax, float az, float bx, float bz) { return Mathf.Atan2(bx - ax, bz - az) * Mathf.Rad2Deg; }
        // two fighters trade blows: the attacker steps in with a Punch / Kick / Lunge, the other flinches (Hit / Stagger / Block); every fourth one ends with a KnockDown and a GetUp
        void Duel(string ia, string ib, float t0, float t1, float cx, float cz, float phiDeg, float startR, string lead, float startRb = -1f)
        {
            Act a, b; if (!actBy.TryGetValue(ia, out a) || !actBy.TryGetValue(ib, out b)) return; Vector3 d = new Vector3(Mathf.Sin(phiDeg * Mathf.Deg2Rad), 0f, Mathf.Cos(phiDeg * Mathf.Deg2Rad));
            Vector3 pa = new Vector3(cx, 0, cz) + d * 0.95f, pb = new Vector3(cx, 0, cz) - d * 0.95f; float ya = Face(pa.x, pa.z, pb.x, pb.z), yb = ya + 180f;
            a.Keys.Add(Kf(t0 - 5f, pa.x + d.x * startR, pa.z + d.z * startR, float.NaN, "Auto")); b.Keys.Add(Kf(t0 - 5f, pb.x - d.x * (startRb < 0f ? startR : startRb), pb.z - d.z * (startRb < 0f ? startR : startRb), float.NaN, "Auto"));
            a.Keys.Add(Kf(t0, pa.x, pa.z, ya, lead)); b.Keys.Add(Kf(t0, pb.x, pb.z, yb, lead)); string[] atk = { "Punch", "Kick", "Lunge", "Punch", "Kick", "Lunge", "Punch" }; string[] rea = { "Hit", "Block", "Stagger", "Hit", "Stagger", "Block", "Hit" };
            float te = t0 + 1.2f; int e = 0;
            while (te + (e % 4 == 3 ? 6.8f : 2.1f) < t1 - 2f)
            {
                bool aAtk = e % 2 == 0; Act at = aAtk ? a : b, df = aAtk ? b : a; Vector3 pat = aAtk ? pa : pb, pdf = aAtk ? pb : pa; float yat = aAtk ? ya : yb, ydf = aAtk ? yb : ya; bool ko = e % 4 == 3; Vector3 back = (pdf - pat).normalized * (ko ? 1.6f : 0.4f);
                at.Keys.Add(Kf(te, pat.x, pat.z, yat, atk[e % atk.Length])); df.Keys.Add(Kf(te, pdf.x, pdf.z, ydf, "Fight"));
                df.Keys.Add(Kf(te + 0.45f, pdf.x, pdf.z, ydf, ko ? "KnockDown" : rea[e % rea.Length])); df.Keys.Add(Kf(te + 0.7f, pdf.x + back.x, pdf.z + back.z, ydf, ko ? "KnockDown" : rea[e % rea.Length]));
                at.Keys.Add(Kf(te + 1.3f, pat.x, pat.z, yat, "Fight"));
                if (ko) { df.Keys.Add(Kf(te + 3.4f, pdf.x + back.x, pdf.z + back.z, ydf, "GetUp")); df.Keys.Add(Kf(te + 6.6f, pdf.x, pdf.z, ydf, "Fight")); te += 7.2f; } else { df.Keys.Add(Kf(te + 1.9f, pdf.x, pdf.z, ydf, "Fight")); te += 2.5f; }
                e++;
            }
            a.Keys.Add(Kf(t1 - 2f, pa.x, pa.z, ya, "Fight")); b.Keys.Add(Kf(t1 - 2f, pb.x, pb.z, yb, "Fight"));
        }
        void BuildTracks()
        {
            Func<string, Act> g = id => actBy.ContainsKey(id) ? actBy[id] : null; Action<string, K[]> add = (id, ks) => { var a = g(id); if (a != null) a.Keys.AddRange(ks); };
            // ---- Jones: hangs off the Battle Bus, drops, stands in the meadow with the Portal Device, fights, runs for the Zero Point, meets the Foundation, is knocked down, pleads, then becomes a butterfly
            var j = g("jones");
            if (j != null) j.Ov = t => { if (t >= 36f) return null; var bp = BusPos(Mathf.Min(t, 29.5f)); float fall = Sm(29.5f, 35.5f, t); Vector3 p = Vector3.Lerp(bp + Quaternion.Euler(0f, BusYaw(Mathf.Min(t, 29.5f)), 0f) * new Vector3(1.4f, BusRoof.y - 4.2f, -7.6f), new Vector3(0f, 0f, 14f), fall * fall * (3f - 2f * fall)); if (t > 35.5f) p = new Vector3(0f, 0f, 14f); return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = t < 29.5f ? BusYaw(t) : 0f, Clip = t < 29.5f ? "Hang" : (t < 35.3f ? "Fall" : "Land") }; };
            add("jones", new[] { Kf(36f, 0f, 8f, 180f, "Land"), Kf(37.2f, 0f, 8f, 180f, "Device"), Kf(47f, 0f, 8f, 150f, "Device"), Kf(50f, 0f, 9f, 0f, "Fight"), Kf(52f, 0f, 9f, 0f, "Fight"),
                Kf(88f, 0f, 15f, 0f, "Fight"), Kf(90f, 0f, 15f, 0f, "Auto"), Kf(112f, 0f, 104f, 0f, "Auto"), Kf(113f, 0f, 106f, 0f, "LookUp"), Kf(139f, 0f, 106f, 0f, "LookUp"),
                Kf(145f, -1f, 106f, 20f, "Brace"), Kf(151f, -1f, 106f, 8f, "Walk"), Kf(153f, -0.9f, 109.2f, 5f, "Talk"), Kf(155.6f, -0.9f, 109.2f, 5f, "Talk"), Kf(155.9f, -1.0f, 109.2f, 5f, "KnockDown"), Kf(156.7f, -2.6f, 107.6f, 5f, "KnockDown"),
                Kf(165f, -2.6f, 107.6f, 5f, "GetUp"), Kf(170.5f, -2.6f, 107.6f, 10f, "Plead"), Kf(179f, -2.4f, 108f, 8f, "Plead"), Kf(180f, -2.4f, 108f, 8f, "Talk"), Kf(194f, -2.4f, 108f, 8f, "Beckon"), Kf(204f, -2.4f, 108f, 8f, "Talk"),
                Kf(214f, -2.4f, 108f, 0f, "LookUp"), Kf(236f, -2.4f, 108f, 0f, "Brace"), Kf(247f, -2.4f, 108f, 0f, "LookUp"), Kf(262f, -2.4f, 108f, 0f, "Point"), Kf(279.4f, -2.4f, 108f, 0f, "Hide") });
            // ---- the Foundation: steps out of the meteor, shoves Jones, talks, reaches for the Zero Point and holds it together
            add("found", new[] { Kf(141f, 0f, 127f, 180f, "Idle"), Kf(144f, 0f, 127f, 180f, "Auto"), Kf(150f, 0f, 112f, 180f, "Auto"), Kf(151.5f, 0f, 112f, 188f, "Idle"), Kf(154.4f, 0f, 112f, 188f, "Shove"), Kf(158f, 0f, 112f, 180f, "Talk"), Kf(172f, 0f, 112f, 180f, "Look"),
                Kf(181f, 0f, 112f, 0f, "Talk"), Kf(191f, 0f, 112f, 0f, "Point"), Kf(201f, 2.2f, 111f, 0f, "Reach"), Kf(236f, 2.2f, 111f, 0f, "Reach"), Kf(250f, 2.2f, 111f, 0f, "Channel"), Kf(339f, 2.2f, 111f, 0f, "Channel"), Kf(339.5f, 2.2f, 111f, 0f, "Hide"),
                Kf(409f, 6f, 150f, 0f, "Hide"), Kf(409.5f, 6f, 150f, 0f, "Channel"), Kf(500f, 6f, 150f, 0f, "Channel"), Kf(500.5f, 6f, 150f, 0f, "Hide"),
                Kf(551f, 0f, 271f, 180f, "Idle", SPY + spireY + 0.12f), Kf(556f, 0f, 271f, 180f, "Talk", SPY + spireY + 0.12f), Kf(566f, 0f, 271f, 180f, "Reach", SPY + spireY + 0.12f), Kf(574f, 0f, 271f, 180f, "Talk", SPY + spireY + 0.12f), Kf(578f, 0f, 271f, 180f, "Point", SPY + spireY + 0.12f),
                Kf(581f, 0f, 271f, 180f, "Walk", SPY + spireY + 0.12f), Kf(584f, 0f, 265f, 180f, "Idle", SPY + spireY + 0.12f), Kf(585f, 0f, 265f, 180f, "Float", SPY + spireY + 0.12f), Kf(588f, 0f, 265f, 180f, "Channel", SPY + spireY + 3.6f), Kf(598f, 0f, 265f, 180f, "Channel", SPY + spireY + 4.2f), Kf(601.4f, 0f, 265f, 180f, "Channel", SPY + spireY + 4.2f), Kf(601.5f, 0f, 265f, 180f, "Hide") });
            // ---- the Hunters charge in at 0:50 and fight the heroes: Kratos / Sarah / Chun-Li / Meowscle / Jones against two Xenomorphs, the Terminators and the Hybrid
            Duel("kratos", "xeno1", 56f, 88f, -10f, 24f, 90f, 36f, "Fight"); Duel("sarah", "term1", 56.5f, 88f, 10f, 27f, -80f, 34f, "Fight"); Duel("chunli", "hybrid", 57f, 88f, -3f, 36f, 20f, 40f, "Fight"); Duel("meow", "xeno2", 57.5f, 88f, 7f, 40f, -30f, 40f, "Fight");
            Duel("xeno3", "jones", 57f, 88f, 0f, 15f, 0f, 42f, "Fight", 0f); Duel("ryu", "predator", 58f, 88f, -17f, 44f, 80f, 40f, "Fight"); Duel("chief", "term2", 56.5f, 88f, 17f, 46f, -75f, 38f, "Fight"); Duel("ripley", "xeno4", 58.5f, 88f, 2f, 52f, 160f, 40f, "Fight");
            add("tntina", new[] { Kf(49f, 21f, 25f, -50f, "Hide"), Kf(50f, 21f, 25f, -50f, "Dance"), Kf(86f, 21f, 25f, -50f, "Victory"), Kf(100f, 21f, 25f, -50f, "Hide") });
            // the comic relief: Trigger, Mancake and the Dog dance at the side lines, then cheer when it is over
            add("trigger", new[] { Kf(49f, -17f, 12f, 40f, "Hide"), Kf(50f, -17f, 12f, 40f, "Taunt"), Kf(64f, -17f, 12f, 30f, "Dance"), Kf(86f, -17f, 12f, 30f, "Cheer"), Kf(100f, -17f, 12f, 30f, "Hide") });
            add("mancake", new[] { Kf(49f, 16f, 10f, -40f, "Hide"), Kf(50f, 16f, 10f, -40f, "Cheer"), Kf(60f, 16f, 10f, -40f, "Dance"), Kf(86f, 16f, 10f, -40f, "Victory"), Kf(100f, 16f, 10f, -40f, "Hide") });
            add("dog", new[] { Kf(49f, 3f, 5f, 180f, "Hide"), Kf(50f, 3f, 5f, 160f, "Dance"), Kf(88f, 3f, 5f, 160f, "Wave"), Kf(100f, 3f, 5f, 160f, "Hide") });
            // after the fight the Hunters run off and the heroes cheer
            foreach (var id in new[] { "xeno1", "xeno2", "xeno3", "xeno4", "term1", "term2", "hybrid", "predator" }) { var a = g(id); if (a != null) { var last = a.Keys.Count > 0 ? a.Keys[a.Keys.Count - 1] : null; if (last != null) { a.Keys.Add(Kf(88f, last.X, last.Z, last.Yaw, "Sprint")); a.Keys.Add(Kf(96f, last.X + (Hash(id.Length, a.Keys.Count) - 0.5f) * 90f, last.Z + 55f, float.NaN, "Hide")); } } }
            foreach (var id in new[] { "kratos", "sarah", "chunli", "meow", "chief", "ryu", "ripley" }) { var a = g(id); if (a != null && a.Keys.Count > 0) { var last = a.Keys[a.Keys.Count - 1]; a.Keys.Add(Kf(88.5f, last.X, last.Z, last.Yaw, id == "meow" ? "Flex" : "Victory")); a.Keys.Add(Kf(99f, last.X, last.Z, last.Yaw, "Hide")); } }
            // the hunters that block your way at 5:40 (they vanish with the wave at 5:45)
            add("xeno3", new[] { Kf(339.9f, 40f, 140f, 200f, "Hide"), Kf(340f, 40f, 140f, 200f, "Roar"), Kf(344.9f, 40f, 140f, 200f, "Fight"), Kf(345.1f, 40f, 140f, 200f, "Hide") });
            add("xeno2", new[] { Kf(339.9f, 22f, 138f, 190f, "Hide"), Kf(340f, 22f, 138f, 190f, "Fight"), Kf(345.1f, 22f, 138f, 190f, "Hide") });
            add("term2", new[] { Kf(339.9f, 36f, 128f, 200f, "Hide"), Kf(340f, 36f, 128f, 200f, "Alert"), Kf(345.1f, 36f, 128f, 200f, "Hide") });
            // ---- the line-up of the Season 6 reel (10:02 on): everybody on an arc, each with their own pose
            string[] reel = { "jones", "kratos", "sarah", "chunli", "ryu", "trigger", "mancake", "tntina", "meow", "dog", "chief", "ripley", "hybrid", "xeno1", "predator", "term1", "found" };
            string[][] poses = { new[] { "Wave", "Victory", "Taunt" }, new[] { "Roar", "Fight", "Taunt" }, new[] { "Fight", "Taunt", "Victory" }, new[] { "Kick", "Victory", "Fight" }, new[] { "Fight", "Kick", "Victory" }, new[] { "Dance", "Taunt", "Cheer" }, new[] { "Dance", "Victory", "Cheer" }, new[] { "Dance", "Taunt", "Cheer" }, new[] { "Flex", "Roar", "Victory" }, new[] { "Dance", "Wave", "Cheer" },
                new[] { "Victory", "Fight", "Taunt" }, new[] { "Fight", "Alert", "Victory" }, new[] { "Taunt", "Fight", "Roar" }, new[] { "Roar", "Fight", "Taunt" }, new[] { "Roar", "Taunt", "Fight" }, new[] { "Alert", "Fight", "Taunt" }, new[] { "Pray", "Channel", "Think" } };
            for (int i = 0; i < reel.Length; i++)
            {
                var a = g(reel[i]); if (a == null) continue; float ang = Mathf.Lerp(-70f, 70f, (i + 0.5f) / reel.Length); float x = Mathf.Sin(ang * Mathf.Deg2Rad) * 15f, z = 8f + Mathf.Cos(ang * Mathf.Deg2Rad) * 15f, yw = Face(x, z, 0f, 0f);
                a.Keys.Add(Kf(601.9f, x, z, yw, "Hide")); for (int k = 0; k < 9; k++) a.Keys.Add(Kf(602f + k * 7.4f, x, z, yw, poses[i][k % poses[i].Length]));
                a.Keys.Add(Kf(T_END + 5f, x, z, yw, "Hide"));
            }
            foreach (var a in acts) a.Keys.Sort((p, q) => p.T.CompareTo(q.T));
        }
        static readonly HashSet<string> ONESHOT = new HashSet<string> { "Death", "Stagger", "Fall", "Land", "Hit", "KnockDown", "GetUp", "Shove", "Punch", "Kick", "Lunge", "Wave", "Brace", "Roar" };
        static void GaitOf(string clip, out float period, out float stride)
        {
            switch (clip) { case "Sprint": period = 0.6f; stride = 4.0f; break; case "Run": period = 0.75f; stride = 3.2f; break; default: period = 1.0f; stride = 1.5f; break; }
        }
        void ActFrame(Act a, float t, float dt)
        {
            K k0 = null, k1 = null; bool ovr = false; float x = 0, z = 0, y = float.NaN, yawD = a.YawNow; string clip = "Idle"; float ct = 0f, speed = 0f;
            if (a.Ov != null) { var o = a.Ov(t); if (o != null) { ovr = true; x = o.X; z = o.Z; y = o.Y; yawD = o.Yaw; clip = o.Clip; ct = t - 4f + a.Seed; } }
            if (!ovr)
            {
                var ks = a.Keys; int i = -1; for (int q = 0; q < ks.Count; q++) { if (ks[q].T <= t) i = q; else break; }
                if (i < 0 || ks[i].Clip == "Hide") { if (a.Go.activeSelf) a.Go.SetActive(false); a.Vis = false; a.LastT = -9f; return; }
                k0 = ks[i]; k1 = i + 1 < ks.Count && ks[i + 1].Clip != "Hide" ? ks[i + 1] : (i + 1 < ks.Count ? ks[i + 1] : k0); float span = Mathf.Max(0.001f, k1.T - k0.T); float u = k1 == k0 ? 0f : Mathf.Clamp01((t - k0.T) / span);
                if (k1.Clip == "Hide" && k1 != k0) { k1 = new K { T = k1.T, X = k1.X, Z = k1.Z, Yaw = k1.Yaw, Y = k1.Y, Clip = "Hide" }; }
                x = Mathf.Lerp(k0.X, k1.X, u); z = Mathf.Lerp(k0.Z, k1.Z, u); float dist = Mathf.Sqrt((k1.X - k0.X) * (k1.X - k0.X) + (k1.Z - k0.Z) * (k1.Z - k0.Z)); speed = k1 == k0 ? 0f : dist / span;
                if (!float.IsNaN(k0.Y) && !float.IsNaN(k1.Y)) y = Mathf.Lerp(k0.Y, k1.Y, u);
                if (speed > 0.2f && (float.IsNaN(k0.Yaw) || k0.Clip == "Auto")) yawD = Mathf.Atan2(k1.X - k0.X, k1.Z - k0.Z) * Mathf.Rad2Deg; else if (!float.IsNaN(k0.Yaw)) yawD = float.IsNaN(k1.Yaw) ? k0.Yaw : Mathf.LerpAngle(k0.Yaw, k1.Yaw, u);
                clip = k0.Clip; if (clip == "Auto") clip = speed > 5.5f ? "Sprint" : speed > 2.6f ? "Run" : speed > 0.25f ? "Walk" : "Idle";
                ct = t - k0.T + a.Seed; if (ONESHOT.Contains(clip)) { var cc = FindClip(a.M, "H_" + clip); if (cc != null) ct = Mathf.Clamp(t - k0.T, 0f, cc.Duration - 0.05f); }
                if (clip == "Walk" || clip == "Run" || clip == "Sprint") { float per, str; GaitOf(clip, out per, out str); ct = (speed * (t - k0.T) + a.Seed) / (str * a.Scale * 1.15f) * per; }
            }
            if (!a.Go.activeSelf) a.Go.SetActive(true); a.Vis = true; a.YawNow = yawD;
            Vector3 wl = new Vector3(x, 0f, z); if (float.IsNaN(y)) y = IY;
            bool scrub = Mathf.Abs(t - a.LastT) > 0.5f || dt <= 0f || a.LastT < -8f;
            if (a.Cur != clip) { if (a.Cur != null && !scrub) { a.Prev = a.Cur; a.PrevT = a.CurT; a.Blend = 0f; } else { a.Prev = null; a.Blend = 1f; } a.Cur = clip; }
            if (a.Prev != null) { a.PrevT += dt; a.Blend = Mathf.Min(1f, a.Blend + dt / 0.28f); if (a.Blend >= 1f) a.Prev = null; }
            a.CurT = ct; a.LastT = t;
            if (a.Prev != null) PlayClip(a.Rig, a.M, "H_" + a.Prev, a.PrevT, "H_" + clip, ct, Sm(0f, 1f, a.Blend)); else PlayClip(a.Rig, a.M, "H_" + clip, ct);
            var tr = a.Go.transform; tr.localPosition = new Vector3(x, y, z); tr.localRotation = Quaternion.Euler(0f, yawD, 0f); tr.localScale = Vector3.one * a.Scale; a.PosL = tr.localPosition;
        }
        void ActorsFrame(float t, float dt)
        {
            foreach (var a in acts) ActFrame(a, t, dt);
            // the Portal Device in Jones' hand (36 s - 280 s)
            Act j; if (devGo != null && actBy.TryGetValue("jones", out j))
            {
                bool on = j.Vis && j.Hand != null && t < 280f;
                if (on) { if (devGo.transform.parent != j.Hand) { devGo.transform.SetParent(j.Hand, false); } if (!devGo.activeSelf) devGo.SetActive(true); devGo.transform.localPosition = new Vector3(0f, 0.04f, 0.1f) / Mathf.Max(0.0001f, j.Hand.lossyScale.x); devGo.transform.localRotation = Quaternion.identity; devGo.transform.localScale = Vector3.one * devScale / Mathf.Max(0.0001f, j.Hand.lossyScale.x); }
                else if (devGo.activeSelf) devGo.SetActive(false);
                if (devGlow != null) { bool go2 = on; if (devGlow.gameObject.activeSelf != go2) devGlow.gameObject.SetActive(go2); if (go2) { var cm = Camera.main; devGlow.position = devGo.transform.position + Vector3.up * 0.09f; if (cm != null) devGlow.rotation = Quaternion.LookRotation(devGlow.position - cm.transform.position); devGlow.localScale = Vector3.one * 0.62f * (1f + 0.12f * Mathf.Sin(t * 3f)); } }
            }
        }

        // ---------------------------------------------------------------- the island: a flat disc IY m over the spawn with a real floor collider and an invisible fence; its colour is the reality you are in
        Mesh isMesh; Color[] isCols; Vector3[] isV; float[] isN; GameObject isGo; Material isMat; float isT = -9f; Texture2D sandTex; GltfModel treeM; readonly List<Material> treeMats = new List<Material>(); readonly List<GameObject> treeGos = new List<GameObject>(); readonly List<Vector3> treePos = new List<Vector3>();
        const int IR = 36, IS = 72; const float IRAD = 250f; static readonly Vector3 IC = new Vector3(0f, IY, 70f);
        void BuildIsland()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>(); isN = new float[1 + IR * IS]; v.Add(IC); uv.Add(new Vector2(IC.x / 12f, IC.z / 12f)); isN[0] = 0.5f;
            for (int i = 1; i <= IR; i++) { float r = IRAD * i / IR; for (int k = 0; k < IS; k++) { float a = k * 6.2832f / IS; float x = IC.x + Mathf.Sin(a) * r, z = IC.z + Mathf.Cos(a) * r; v.Add(new Vector3(x, IY, z)); uv.Add(new Vector2(x / 12f, z / 12f)); isN[v.Count - 1] = WorldKit.Fbm(x * 0.03f, z * 0.03f, 4, 3); } }
            for (int k = 0; k < IS; k++) tris.AddRange(new[] { 0, 1 + (k + 1) % IS, 1 + k });
            for (int i = 1; i < IR; i++) for (int k = 0; k < IS; k++) { int k2 = (k + 1) % IS; int a = 1 + (i - 1) * IS + k, b = 1 + (i - 1) * IS + k2, c = 1 + i * IS + k, d = 1 + i * IS + k2; tris.AddRange(new[] { a, b, c, b, d, c }); }
            isMesh = new Mesh(); isMesh.SetVertices(v); isMesh.SetUVs(0, uv); isMesh.SetTriangles(tris, 0); isCols = new Color[v.Count]; isMesh.colors = isCols; isMesh.RecalculateBounds(); owned.Add(isMesh); isV = v.ToArray();
            sandTex = kit.Tex(64, 64, (x, y) => { float n = WorldKit.Fbm(x * 0.16f, y * 0.16f, 7, 3), n2 = WorldKit.Hash(x, y, 9); float b = 0.82f + 0.2f * n + 0.05f * n2; return new Color(b, b, b, 1f); });
            isGo = new GameObject("ZIsland"); isGo.transform.SetParent(root.transform, false); isGo.AddComponent<MeshFilter>().sharedMesh = isMesh; var mr = isGo.AddComponent<MeshRenderer>(); isMat = kit.Glow(Color.white, sandTex, 2001); mr.sharedMaterial = isMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false; isGo.SetActive(false);
            // the floor you walk on and a fence round it (the game ends your session if you fall out of the map's box)
            kit.BoxCol("ZFloor", root.transform, new Vector3(IC.x, IY - 1f, IC.z), new Vector3(2 * IRAD, 2f, 2 * IRAD), Quaternion.identity);
            for (int k = 0; k < 24; k++) { float a = k * 6.2832f / 24f; Vector3 p = IC + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (IRAD - 12f); kit.BoxCol("ZFence" + k, root.transform, p + Vector3.up * 30f, new Vector3(2f * Mathf.PI * (IRAD - 12f) / 24f * 1.2f, 80f, 2f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f)); }
            // trees
            treeM = Take("tree", 1.0f, true); if (treeM != null)
            {
                float ts = 12f / Mathf.Max(0.1f, treeM.RendererBounds.size.y); treeS = ts; foreach (var rr in treeM.Pivot.GetComponentsInChildren<Renderer>(true)) foreach (var m in rr.sharedMaterials) if (m != null && !treeMats.Contains(m)) treeMats.Add(m);
                for (int i = 0; i < 90; i++) { float a = Hash(i, 61) * 6.2832f, r = 36f + 205f * Mathf.Pow(Hash(i, 62), 0.7f); float x = IC.x + Mathf.Sin(a) * r, z = IC.z + Mathf.Cos(a) * r; if (Mathf.Abs(x) < 14f && z > -6f && z < 215f) continue; if (Mathf.Abs(x) < 50f && Mathf.Abs(z - 265f) < 50f) continue;
                    var go = i == 0 ? treeM.Pivot : UnityEngine.Object.Instantiate(treeM.Pivot, root.transform); go.name = "ZTree" + i; if (i > 0) clones.Add(go); go.SetActive(false); treeGos.Add(go); treePos.Add(new Vector3(x, IY, z)); }
            }
        }
        float treeS = 1f;
        // colour of the island in each reality
        Color IslandOf(float t, Seg g)
        {
            Color green = new Color(0.34f, 0.58f, 0.24f), pink = new Color(0.88f, 0.7f, 0.86f);
            switch (g.S)
            {
                case Sc.Meadow: return green;
                case Sc.Battle: return Color.Lerp(green, new Color(0.84f, 0.68f, 0.8f), Sm(54f, 86f, t));
                case Sc.Run: case Sc.Meteor: case Sc.Talk: return pink;
                case Sc.Zero: return Color.Lerp(pink, new Color(0.62f, 0.5f, 0.82f), Sm(220f, 270f, t));
                case Sc.Raz: return new Color(0.78f, 0.76f, 0.98f);
                case Sc.Cluck: return Color.Lerp(new Color(0.78f, 0.76f, 0.98f), new Color(0.92f, 0.62f, 0.16f), Sm(344f, 350f, t));
                case Sc.Wolves: return new Color(0.04f, 0.1f, 0.34f);
                case Sc.Lara: return Color.Lerp(new Color(0.04f, 0.1f, 0.34f), green, Sm(400f, 406f, t));
                case Sc.Bloom: return Color.Lerp(new Color(0.96f, 0.76f, 0.9f), new Color(0.85f, 0.35f, 0.4f), Sm(466f, 492f, t));
                case Sc.Spire: case Sc.Top: return Color.Lerp(new Color(0.5f, 0.5f, 0.8f), new Color(0.3f, 0.45f, 0.85f), Sm(520f, 550f, t));
                case Sc.Reel: return new Color(0.98f, 0.62f, 0.3f);
            }
            return green;
        }
        Color TreeOf(float t, Seg g)
        {
            switch (g.S)
            {
                case Sc.Battle: return Color.Lerp(Color.white, new Color(0.5f, 0.36f, 0.5f), Sm(54f, 86f, t));
                case Sc.Run: case Sc.Meteor: case Sc.Talk: case Sc.Zero: return new Color(0.5f, 0.36f, 0.5f);
                case Sc.Raz: return new Color(0.4f, 0.35f, 0.7f);
                case Sc.Cluck: return new Color(1.9f, 0.85f, 0.3f);
                case Sc.Wolves: return new Color(0.25f, 0.4f, 0.95f);
                case Sc.Lara: return new Color(1.5f, 0.9f, 0.45f);
                case Sc.Bloom: return Color.Lerp(new Color(0.6f, 0.4f, 0.55f), new Color(0.6f, 0.2f, 0.2f), Sm(466f, 490f, t));
                case Sc.Reel: return new Color(1.5f, 0.9f, 0.5f);
            }
            return Color.white;
        }
        void IslandFrame(float t, Seg g)
        {
            bool on = t >= 35.6f && t < 604f; if (isGo == null) return; if (isGo.activeSelf != on) isGo.SetActive(on); bool tOn = on && g.S != Sc.Spire && g.S != Sc.Top;
            for (int i = 0; i < treeGos.Count; i++) { var go = treeGos[i]; if (!tOn) { if (go.activeSelf) go.SetActive(false); continue; } PlaceGo(go, treePos[i], treeS * (0.8f + 0.7f * Hash(i, 63)), Quaternion.Euler(0f, Hash(i, 64) * 360f, 0f), true); }
            if (!on) return; Color tc = TreeOf(t, g); foreach (var m in treeMats) if (m != null) m.color = tc;
            if (Mathf.Abs(t - isT) < 0.25f && t >= isT) return; isT = t; Color c = IslandOf(t, g);
            for (int i = 0; i < isCols.Length; i++) { float n = isN[i]; float f = 0.84f + 0.26f * Mathf.Clamp01(n); Vector3 p = isV[i]; float r = Mathf.Sqrt((p.x - IC.x) * (p.x - IC.x) + (p.z - IC.z) * (p.z - IC.z)); isCols[i] = new Color(c.r * f, c.g * f, c.b * f, 1f - Sm(IRAD - 60f, IRAD - 4f, r)); }
            isMesh.colors = isCols;
        }

        // ---------------------------------------------------------------- sound (synthesised at load, played from a pool of 3D sources)
        AudioClip sBoom, sBig, sThunder, sWhoosh, sZap; AudioSource[] sPool; int sNext; readonly Dictionary<AudioClip, float> sLast = new Dictionary<AudioClip, float>();
        float SfxVol { get { return Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f) * 0.55f; } }
        static AudioClip MakeSfxClip(string name, float len, Func<float, float> f)
        {
            int n = (int)(44100 * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / 44100f), -1f, 1f);
            int rp = 0; return AudioClip.Create(name, n, 1, 44100, false, buf => { for (int q = 0; q < buf.Length; q++) { buf[q] = d[Mathf.Min(rp, n - 1)]; rp++; } }, pos => { rp = pos; });
        }
        void SfxInit()
        {
            var rnd = new System.Random(21); Func<float> N = () => (float)(rnd.NextDouble() * 2.0 - 1.0); float lp = 0f, lp2 = 0f;
            sBoom = MakeSfxClip("ZBoom", 1.7f, t => { lp += (N() - lp) * 0.05f; float ph = 6.2832f * (70f * (1f - Mathf.Exp(-1.6f * t)) / 1.6f + 30f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 2.4f) * 0.8f + lp * 2.2f * Mathf.Exp(-t * 3.2f) + N() * 0.25f * Mathf.Exp(-t * 18f); });
            lp = 0f; lp2 = 0f; sBig = MakeSfxClip("ZBig", 3.4f, t => { lp += (N() - lp) * 0.025f; lp2 += (lp - lp2) * 0.2f; float ph = 6.2832f * (48f * (1f - Mathf.Exp(-0.9f * t)) / 0.9f + 22f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 1.3f) * 0.85f + lp2 * 5f * Mathf.Exp(-t * 1.6f) + N() * 0.3f * Mathf.Exp(-t * 12f); });
            lp = 0f; sThunder = MakeSfxClip("ZThunder", 3.2f, t => { lp += (N() - lp) * 0.018f; float roll = 0.55f + 0.45f * Mathf.Sin(6.2832f * 4.3f * t) * Mathf.Sin(6.2832f * 1.7f * t); return lp * 7f * roll * Mathf.Exp(-t * 0.85f) + N() * 0.5f * Mathf.Exp(-t * 28f); });
            lp = 0f; lp2 = 0f; sWhoosh = MakeSfxClip("ZWhoosh", 1.6f, t => { lp += (N() - lp) * 0.12f; lp2 += (lp - lp2) * 0.3f; float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)); return lp2 * 4f * env + Mathf.Sin(6.2832f * (180f + 400f * t) * t) * 0.15f * env; });
            sZap = MakeSfxClip("ZZap", 0.35f, t => { float ph = 6.2832f * (1800f * (1f - Mathf.Exp(-8f * t)) / 8f + 320f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 9f) * 0.55f + Mathf.Sin(ph * 2.01f) * Mathf.Exp(-t * 14f) * 0.2f + N() * 0.1f * Mathf.Exp(-t * 25f); });
            owned.Add(sBoom); owned.Add(sBig); owned.Add(sThunder); owned.Add(sWhoosh); owned.Add(sZap);
            sPool = new AudioSource[10]; for (int i = 0; i < sPool.Length; i++) { var gg = new GameObject("ZSfx" + i); gg.transform.SetParent(root.transform, false); var a = gg.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0.85f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 10f; a.maxDistance = 520f; a.priority = 64; sPool[i] = a; }
        }
        void Sfx(AudioClip c, Vector3 wp, float vol, float pitch = 1f, float gap = 0.05f)
        {
            if (c == null || sPool == null || paused) return; float now = Time.unscaledTime, last; if (sLast.TryGetValue(c, out last) && now - last < gap) return; sLast[c] = now;
            AudioSource a = null; for (int k = 0; k < sPool.Length; k++) { var s = sPool[(sNext + k) % sPool.Length]; if (!s.isPlaying) { a = s; sNext = (sNext + k + 1) % sPool.Length; break; } }
            if (a == null) { a = sPool[sNext]; sNext = (sNext + 1) % sPool.Length; }
            a.transform.position = wp; a.clip = c; a.volume = Mathf.Clamp01(vol * SfxVol); a.pitch = Mathf.Clamp(pitch * UnityEngine.Random.Range(0.94f, 1.06f), 0.4f, 2f); a.Play();
        }
        void SfxCleanup0() { sPool = null; sLast.Clear(); sBoom = sBig = sThunder = sWhoosh = sZap = null; }
    }
}
