using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Galactus, part 2: models, the cast, Galactus' pose, the bus fleet and the sound
    internal sealed partial class AstronomicalGalactus
    {
        const float DeckY = 0f, OfficeZ = -250f, GS = 6f;
        static readonly Vector3 BusPos = new Vector3(0f, IY - 300f, 0f);
        Vector3 BusRoof = new Vector3(0f, 3.2f, 0f);

        // ---------------------------------------------------------------- models and actors
        sealed class K { public float T, X, Z, Yaw, Y, Pitch; public string Clip; }
        static K Kf(float t, float x, float z, float yawDeg, string clip, float y = float.NaN, float pitch = 0f) { return new K { T = t, X = x, Z = z, Yaw = yawDeg, Y = y, Clip = clip, Pitch = pitch }; }
        sealed class Act
        {
            public string Id; public GltfModel M; public GameObject Go; public CRig Rig; public float Scale = 1f, Seed, YawNow; public string Cur, Prev; public float CurT, PrevT, Blend = 1f, LastT = -9f; public readonly List<K> Keys = new List<K>(); public bool Vis; public Func<float, K> Ov;
        }
        readonly List<Act> acts = new List<Act>(); readonly Dictionary<string, Act> actBy = new Dictionary<string, Act>();
        GltfModel heliM, sbusM, sbusfM, galM, corM, offM, ironM, thorM, wolvM, jonM; GameObject heliGo, busGo;
        void Brighten(GltfModel mdl, float k)
        {
            foreach (var r in mdl.Pivot.GetComponentsInChildren<Renderer>(true))
            {
                r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; var src = r.sharedMaterials; origMats.Add(new KeyValuePair<Renderer, Material[]>(r, src)); var nw = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : ""; if (n.StartsWith("SF_")) n = n.Substring(3); Material mt;
                    bool alpha = n.StartsWith("A_") || (src[i] != null && src[i].shader != null && src[i].shader.name == "Sprites/Default"), glow = n.StartsWith("G_");
                    if (alpha) mt = kit.Glow(Color.white, null, 3000); else if (glow) mt = kit.Flat(Color.white * k); else mt = kit.Lit(Color.white * Mathf.Min(k, 1.3f), null, 0.28f);
                    try { mt.SetFloat("_Cull", 0f); mt.doubleSidedGI = true; } catch { }
                    try { if (src[i] != null && src[i].mainTexture != null) mt.mainTexture = src[i].mainTexture; else if (src[i] != null && !alpha && !glow) mt.color = src[i].color * k; } catch { }
                    nw[i] = mt; if (mdl == galM) galMats.Add(mt);
                }
                r.sharedMaterials = nw;
            }
        }
        readonly List<Material> galMats = new List<Material>();
        GltfModel Take(string id, float bright)
        {
            var m = def.Prop(id); if (m == null || m.Pivot == null) { JsLiveEventsMod.Log.LogWarning("Galactus: prop '" + id + "' missing"); return null; }
            m.Pivot.transform.SetParent(root.transform, false); m.Pivot.SetActive(false); Brighten(m, bright); models.Add(m); return m;
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
        static float Fit(GltfModel m, float h, bool longest = false) { if (m == null) return 1f; var s = m.RendererBounds.size; float v = longest ? Mathf.Max(s.x, s.y, s.z) : s.y; return h / Mathf.Max(0.05f, v); }
        void BuildModels()
        {
            heliM = Take("heli", 1.2f); sbusM = Take("sbus", 1.2f); sbusfM = Take("sbusfar", 1.2f); galM = Take("galactus", 1.15f); corM = Take("corridor", 1.2f); offM = Take("office", 1.2f); ironM = Take("iron", 1.4f); thorM = Take("thor", 1.3f); wolvM = Take("wolverine", 1.3f); jonM = Take("jones", 1.25f);
        }
        Act NewAct(string id, GltfModel m, float height)
        {
            if (m == null) return null; var go = m.Pivot; go.SetActive(false); var a = new Act { Id = id, M = m, Go = go, Rig = CRig.Of(m, go), Scale = Fit(m, height), Seed = Hash(acts.Count, 5) * 6f }; acts.Add(a); actBy[id] = a; return a;
        }
        static readonly HashSet<string> ONESHOT = new HashSet<string> { "Stagger", "Fall", "Hit", "KnockDown", "GetUp", "Punch", "Kick", "Lunge", "Brace", "Roar" };
        void ActFrame(Act a, float t, float dt)
        {
            K k0 = null, k1 = null; bool ovr = false; float x = 0, z = 0, y = float.NaN, pitch = 0f, yawD = a.YawNow; string clip = "Idle"; float ct = 0f;
            if (a.Ov != null) { var o = a.Ov(t); if (o != null) { ovr = true; x = o.X; z = o.Z; y = o.Y; yawD = o.Yaw; clip = o.Clip; pitch = o.Pitch; ct = t + a.Seed; } }
            if (!ovr)
            {
                var ks = a.Keys; int i = -1; for (int q = 0; q < ks.Count; q++) { if (ks[q].T <= t) i = q; else break; }
                if (i < 0 || ks[i].Clip == "Hide") { if (a.Go.activeSelf) a.Go.SetActive(false); a.Vis = false; a.LastT = -9f; return; }
                k0 = ks[i]; k1 = i + 1 < ks.Count ? ks[i + 1] : k0; float span = Mathf.Max(0.001f, k1.T - k0.T); float u = k1 == k0 ? 0f : Mathf.Clamp01((t - k0.T) / span);
                x = Mathf.Lerp(k0.X, k1.X, u); z = Mathf.Lerp(k0.Z, k1.Z, u); if (!float.IsNaN(k0.Y) && !float.IsNaN(k1.Y)) y = Mathf.Lerp(k0.Y, k1.Y, u); else y = k0.Y; pitch = Mathf.Lerp(k0.Pitch, k1.Pitch, u);
                yawD = float.IsNaN(k0.Yaw) ? yawD : (float.IsNaN(k1.Yaw) ? k0.Yaw : Mathf.LerpAngle(k0.Yaw, k1.Yaw, u)); clip = k0.Clip;
                ct = t - k0.T + a.Seed; if (ONESHOT.Contains(clip)) { var cc = FindClip(a.M, "H_" + clip); if (cc != null) ct = Mathf.Clamp(t - k0.T, 0f, cc.Duration - 0.05f); }
            }
            if (!a.Go.activeSelf) a.Go.SetActive(true); a.Vis = true; a.YawNow = yawD; if (float.IsNaN(y)) y = IY + DeckY;
            bool scrub = Mathf.Abs(t - a.LastT) > 0.5f || dt <= 0f || a.LastT < -8f;
            if (a.Cur != clip) { if (a.Cur != null && !scrub) { a.Prev = a.Cur; a.PrevT = a.CurT; a.Blend = 0f; } else { a.Prev = null; a.Blend = 1f; } a.Cur = clip; }
            if (a.Prev != null) { a.PrevT += dt; a.Blend = Mathf.Min(1f, a.Blend + dt / 0.28f); if (a.Blend >= 1f) a.Prev = null; }
            a.CurT = ct; a.LastT = t;
            if (a.Prev != null) PlayClip(a.Rig, a.M, "H_" + a.Prev, a.PrevT, "H_" + clip, ct, Sm(0f, 1f, a.Blend)); else PlayClip(a.Rig, a.M, "H_" + clip, ct);
            var tr = a.Go.transform; tr.localPosition = new Vector3(x, y, z); tr.localRotation = Quaternion.Euler(pitch, yawD, 0f); tr.localScale = Vector3.one * a.Scale * (a.Id == "iron" && t > 280f ? 4f : 1f);
        }
        void BuildActors()
        {
            acts.Clear(); actBy.Clear(); var th = NewAct("thor", thorM, 2.0f); var wv = NewAct("wolv", wolvM, 1.75f); var ir = NewAct("iron", ironM, 2.0f); var jo = NewAct("jones", jonM, 1.9f);
            Vector3 roof = BusPos + BusRoof;
            if (th != null) { th.Keys.Add(Kf(0f, -7f, -26f, 20f, "Taunt")); th.Keys.Add(Kf(174f, -7f, -26f, 20f, "Taunt")); th.Keys.Add(Kf(176f, -7f, -26f, 20f, "KnockDown")); th.Keys.Add(Kf(179.5f, -9f, -29f, 20f, "KnockDown")); th.Keys.Add(Kf(180f, -9f, -29f, 20f, "Hide")); th.Keys.Add(Kf(330f, roof.x - 1.7f, roof.z - 2.8f, 160f, "Taunt", roof.y)); th.Keys.Add(Kf(575f, roof.x - 1.7f, roof.z - 2.8f, 160f, "Taunt", roof.y)); th.Keys.Add(Kf(577f, roof.x - 1.7f, roof.z - 2.8f, 160f, "Hide", roof.y)); }
            if (wv != null) { wv.Keys.Add(Kf(0f, 6f, -28f, -10f, "Fight")); wv.Keys.Add(Kf(174f, 6f, -28f, -10f, "Fight")); wv.Keys.Add(Kf(176f, 6f, -28f, -10f, "Stagger")); wv.Keys.Add(Kf(179.5f, 8f, -31f, -10f, "KnockDown")); wv.Keys.Add(Kf(180f, 8f, -31f, -10f, "Hide")); wv.Keys.Add(Kf(270f, roof.x + 1.5f, roof.z - 1.8f, 180f, "Lunge", roof.y)); wv.Keys.Add(Kf(420f, roof.x + 1.5f, roof.z - 1.8f, 180f, "Fight", roof.y)); wv.Keys.Add(Kf(420.5f, roof.x + 1.5f, roof.z - 1.8f, 180f, "Hide", roof.y)); wv.Keys.Add(Kf(500f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Hide", roof.y)); wv.Keys.Add(Kf(510f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Fight", roof.y)); wv.Keys.Add(Kf(578f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Roar", roof.y)); wv.Keys.Add(Kf(579f, roof.x + 1.5f, roof.z + 1.2f, 180f, "Hide", roof.y)); }
            if (wv != null) wv.Ov = t =>
            {
                if (t < 504.5f || t >= 510f) return null; float u = (t - 504.5f) / 5.5f; Vector3 a0 = new Vector3(roof.x - 9f, roof.y + 16f, roof.z - 78f), a1 = new Vector3(roof.x + 1.5f, roof.y, roof.z + 1.2f); Vector3 p = Vector3.Lerp(a0, a1, u); p.y += 14f * Mathf.Sin(u * Mathf.PI);
                return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = 0f, Pitch = 20f * (1f - u), Clip = u < 0.8f ? "Lunge" : "Fight" };
            };
            // Iron Man: falls with you, hovers outside the bus window, escorts the bus, faces Galactus at 8:50
            if (ir != null) ir.Ov = t =>
            {
                if (t >= 195f && t < 272f) { Vector3 p = FallPos(t) + new Vector3(5f + 1.2f * Mathf.Sin(t * 0.7f), -1.6f, 6f + Mathf.Sin(t * 0.5f)); float k = Sm(195f, 205f, t); return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = 160f, Pitch = 62f * k, Clip = k < 0.5f ? "Hover" : "FlyPose" }; }
                if (t >= 300f && t < 420f) { float k = Sm(300f, 312f, t); Vector3 p = BusPos + new Vector3(Mathf.Lerp(0f, 9f, k), 6f, Mathf.Lerp(36f, 22f, k)) + Vector3.up * 1.2f * Mathf.Sin(t * 0.8f); return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = t < 316f ? 180f : 0f, Pitch = t < 316f ? 0f : 70f, Clip = t < 316f ? "Hover" : "FlyPose" }; }
                if (t >= 525f && t < 580f) { Vector3 p = BusPos + new Vector3(5f, 5f + Mathf.Sin(t * 0.8f), 30f - 4f * Sm(535f, 575f, t)); return new K { T = t, X = p.x, Y = p.y, Z = p.z, Yaw = 0f, Pitch = 0f, Clip = t < 560f ? "Hover" : "Repulsor" }; }
                return null;
            };
            if (jo != null) jo.Keys.Add(Kf(605f, 0.8f, OfficeZ + 0.6f, 200f, "KnockDown", IY + 0.15f));
            foreach (var a in acts) { a.Keys.Sort((p, q) => p.T.CompareTo(q.T)); }
        }
        float ActScaleFor(string id, float t) { if (id == "iron") return t > 280f ? 8f : 2.0f; return 0f; }

        // ---------------------------------------------------------------- Galactus: a pose is a row of angles; the parts are rigid pieces (Torso, Head, ShL > ElL, ShR > ElR)
        //        t     LX    LZ    LY    ELX   RX    RZ    RY    ERX   HYaw  HPit  Lean
        static readonly float[][] GP = {
            new[] { 0f,   0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f },
            new[] { 120f, 0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f,    0f },
            new[] { 140f, -35f,  -10f,  0f,    -20f,  0f,    0f,    0f,    -10f,  8f,    4f,    4f },
            new[] { 165f, -78f,  -6f,   0f,    -5f,   -10f,  0f,    0f,    -20f,  10f,   6f,    8f },
            new[] { 172f, -92f,  -6f,   -12f,  0f,    -15f,  0f,    0f,    -20f,  0f,    6f,    9f },
            new[] { 178f, -92f,  -6f,   55f,   0f,    -15f,  0f,    0f,    -20f,  -8f,   4f,    9f },
            new[] { 200f, -100f, -6f,   10f,   -5f,   0f,    0f,    0f,    0f,    0f,    6f,    8f },
            new[] { 262f, -100f, -6f,   10f,   -5f,   0f,    0f,    0f,    0f,    0f,    6f,    8f },
            new[] { 280f, -60f,  -50f,  0f,    -20f,  -60f,  50f,   0f,    -30f,  0f,    0f,    0f },
            new[] { 340f, -60f,  -50f,  0f,    -20f,  -60f,  50f,   0f,    -30f,  0f,    0f,    0f },
            new[] { 355f, -72f,  20f,   0f,    -70f,  -72f,  -20f,  0f,    -50f,  0f,    8f,    6f },
            new[] { 395f, -72f,  20f,   0f,    -70f,  -72f,  -20f,  0f,    -50f,  0f,    8f,    6f },
            new[] { 412f, -72f,  20f,   0f,    -70f,  -95f,  0f,    0f,    -5f,   0f,    8f,    8f },
            new[] { 520f, -60f,  -40f,  0f,    -20f,  -60f,  40f,   0f,    -30f,  0f,    0f,    0f },
            new[] { 565f, -80f,  -60f,  0f,    -15f,  -80f,  60f,   0f,    -15f,  0f,    -6f,   -4f },
            new[] { 700f, -80f,  -60f,  0f,    -15f,  -80f,  60f,   0f,    -15f,  0f,    -6f,   -4f } };
        static float[] GxAng(float t)
        {
            int i = 0; while (i + 1 < GP.Length && GP[i + 1][0] <= t) i++; var a = GP[i]; var b = GP[Mathf.Min(i + 1, GP.Length - 1)]; float k = b[0] > a[0] ? Mathf.Clamp01((t - a[0]) / (b[0] - a[0])) : 0f; k = k * k * (3f - 2f * k); var r = new float[a.Length - 1];
            for (int j = 1; j < a.Length; j++) r[j - 1] = Mathf.Lerp(a[j], b[j], k); return r;
        }
        Transform gxRoot; Vector3 gxBelt; Transform gxTorso, gxHead, gxShL, gxElL, gxShR, gxElR, gxHandL, gxHandR, gxEyeL, gxEyeR, gxMouthP, gxChestP, gxHHand; readonly List<Transform> gxParts = new List<Transform>();
        float GxHaze = 0f;
        // where Galactus stands (the belt) and how far he has come; he faces the player (yaw 180)
        bool GxPlace(float t, Seg g, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (t < 48f) return false; if (t < 180f) { float d = t < 105f ? Mathf.Lerp(1700f, 480f, Sm(48f, 105f, t)) : Mathf.Lerp(480f, 430f, Sm(105f, 180f, t)); d *= (AstronomicalDebug.Tweak[3] != 0f ? AstronomicalDebug.Tweak[3] : Mathf.Lerp(1.0f, 0.16f, Sm(112f, 165f, t))); float w2 = Sm(110f, 168f, t); pos = new Vector3(Mathf.Lerp(d * 0.34f * (1f - Sm(60f, 172f, t)), 150f, w2), IY + Mathf.Lerp(300f, -95f, Sm(64f, 168f, t)), Mathf.Lerp(d, 55f, w2)); return true; }   // he stands off the helicarrier's RIGHT side (your right) for the blow
            if (t < 272f) { Vector3 f = FallPos(t); float k = Sm(180f, 200f, t); pos = new Vector3(Mathf.Lerp(0f, 30f, k), f.y + Mathf.Lerp(-150f, 330f, k), Mathf.Lerp(360f, 230f, k)); return true; }
            if (t < 330f) { pos = new Vector3(0f, BusPos.y - 150f, Mathf.Lerp(900f, 560f, Sm(272f, 330f, t))); return true; }
            if (t < 420f) { pos = new Vector3(0f, BusPos.y - 150f, Mathf.Lerp(560f, 62f, Sm(330f, 418f, t))); return true; }
            if (t < 510f) return false;
            if (t < 535f) { pos = new Vector3(0f, BusPos.y - Mathf.Lerp(120f, 105f, Sm(510f, 535f, t)), Mathf.Lerp(250f, 170f, Sm(510f, 535f, t))); return true; }
            if (t < 576f) { pos = new Vector3(0f, BusPos.y - Mathf.Lerp(105f, 62f, Sm(535f, 560f, t)), Mathf.Lerp(170f, 95f, Sm(535f, 575f, t))); return true; }
            return t < 600f;
        }
        // 2:55 he brings a fist down on the carrier: the arm is aimed from the shoulder at the deck (the end nearest him) and speeds up until it lands at HitT
        const float HitT = 177.4f;
        // his RIGHT hand does the hitting (the user corrected: right, not left) (the energy of 3:35 is in it too): that is the arm on your right as he faces you, whichever of the model's two arm parts that is
        Transform gxHSh, gxHEl, gxOSh, gxOEl, gxOHand; bool gxSwap; float gxS = GS; Vector3 hitTgt = new Vector3(0f, 800f, 40f);
        Vector3 FistPoint() { return W(hitTgt); }
        // 3:35 - 4:30 the arm reaches out over the island, palm open and fingers spread, to the point under the falling player where the energy gathers
        void GxReach(float t)
        {
            if (gxOSh == null || gxOEl == null || t < 196f || t >= 276f) return; float w = Sm(198f, 214f, t) * (1f - Sm(262f, 276f, t)); if (w <= 0.001f) return;
            Vector3 f = FallPos(t); Vector3 pt = new Vector3(f.x + 10f, f.y - 45f, f.z + 70f * (1f - 0.4f * Sm(212f, 265f, t)));
            AimArm(gxOSh, gxOEl, gxOHand, (W(pt) - gxOSh.position).normalized, w);
        }
        void GxHitAim(float t)
        {
            if (gxOSh == null || gxOEl == null || t < 168f || t >= 180f) return;
            float w = Mathf.Pow(Mathf.Clamp01((t - 168f) / 5f), 1.2f); if (w <= 0f) return;
            // 2:55 - 3:00 a BACKHAND: the forearm swings in from outside the carrier, low and fast, across the deck (the recording: the arm sweeps through the whole foreground)
            Vector3 sl = root.transform.InverseTransformPoint(gxOSh.position); float sd = Mathf.Sign(sl.x == 0f ? 1f : sl.x); float u = Mathf.Clamp01((t - 168f) / 12f), e = Mathf.Pow(u, 1.8f);
            Vector3 tg = new Vector3(sd * Mathf.Lerp(78f, -38f, e), IY + DeckY + Mathf.Lerp(34f, 6f, Mathf.Min(1f, e * 1.6f)), Mathf.Lerp(12f, -8f, e)); Vector3 d = tg - sl; float reach = 170f; if (d.magnitude > reach) tg = sl + d.normalized * reach; hitTgt = tg;
            AimArm(gxOSh, gxOEl, gxOHand, (W(tg) - gxOSh.position).normalized, w);
        }
        // the new model's arms are sculpted in a guard pose (forearm bent ~90 deg): StraightEl is the elbow rotation that lines the forearm up with the upper arm, DownSh the shoulder rotation that makes the straight arm hang down (the table's angles are for a straight hanging arm)
        static Quaternion StraightEl(Transform el, Transform hand) { return hand != null ? Quaternion.FromToRotation(hand.localPosition.normalized, el.localPosition.normalized) : Quaternion.identity; }
        static Quaternion DownSh(Transform el) { return Quaternion.FromToRotation(el.localPosition.normalized, Vector3.down); }
        // point an arm straight along a world direction (upper arm along the shoulder -> elbow direction)
        void AimArm(Transform sh, Transform el, Transform hand, Vector3 dirW, float w)
        {
            var aim = Quaternion.FromToRotation(el.localPosition.normalized, sh.parent.InverseTransformDirection(dirW));
            sh.localRotation = Quaternion.Slerp(sh.localRotation, aim, w); el.localRotation = Quaternion.Slerp(el.localRotation, StraightEl(el, hand), w);
        }
        // 5:30 - 7:00 the bus flies UP HIS ARM (the Death Star trench run of the real event): his RIGHT arm (on your left as he faces you) is held straight out towards you like a runway, the bus goes from the fist along the forearm, over the elbow and the upper arm to the shoulder, then up in front of his chest to his face.
        // He is bigger in this part (x3) so the trench is long; the whole of him is moved so that the point of the arm under the bus stays at the bus (the bus does not move)
        public const float BusEye = 3.8f; public const float RunS = 3f; Vector3 runH, runS, runHd; float runRad, runU; bool runOn, runInside, armHidden;
        // while the bus is inside the arm the arm's own shell is hidden (the tunnel replaces it)
        void ShowArm(bool show) { if (gxOSh == null || armHidden == !show) return; armHidden = !show; foreach (var nm in new[] { gxOSh.name, gxOEl.name }) { List<Renderer> rl; if (skinBy.TryGetValue(nm, out rl)) foreach (var r in rl) if (r != null) r.enabled = show; } }
        static float RunWeave(float u) { return u > 0f && u < 1f ? 16f * Mathf.Sin(9f * u) * Sm(0f, 0.08f, u) * (1f - Sm(0.9f, 1f, u)) : 0f; }
        void GxRun(float t, GameObject go, float s)
        {
            runOn = t >= 330f && t < 420f && gxOSh != null && gxOEl != null && gxHead != null; if (!runOn) return;
            var rt = root.transform; AimArm(gxOSh, gxOEl, gxOHand, rt.TransformDirection(new Vector3(0f, 0.03f, -1f).normalized), Sm(330f, 338f, t));
            Vector3 H = rt.InverseTransformPoint(gxOHand != null ? gxOHand.position : gxOEl.TransformPoint(new Vector3(0f, -5.4f, 0f))), S = rt.InverseTransformPoint(gxOSh.position), Hd = rt.InverseTransformPoint(gxHead.position), Fp = rt.InverseTransformPoint(gxEyeL != null && gxEyeR != null ? (gxEyeL.position + gxEyeR.position) * 0.5f : gxHead.TransformPoint(new Vector3(0f, 4.5f, 6f)));
            float rad = 3.4f * s; Vector3 T; float u, lat; runInside = t >= 362f && t < 412f; ShowArm(!runInside);
            if (t < 362f) { u = t < 352f ? Mathf.Lerp(-0.9f, 0f, Sm(330f, 352f, t)) : Mathf.Lerp(0f, 0.02f, Sm(352f, 362f, t)); float upf = t < 352f ? 1f : 1f - Sm(352f, 362f, t); T = H + (S - H) * u + Vector3.up * ((rad * (1f + 0.45f * Mathf.Clamp01(u)) + 70f) * upf); lat = t < 352f ? RunWeave(u) : 0f; }
            else if (t < 412f) { u = Mathf.Lerp(0.03f, 0.97f, Sm(362f, 412f, t)); T = H + (S - H) * u; lat = 7f * Mathf.Sin(t * 0.6f); }   // inside the arm: the bus is on its axis
            else { u = 1f; lat = 0f; Vector3 P0 = S + Vector3.up * (rad * 1.45f + 22f), P1 = Fp + new Vector3(0f, -1f * s, -6f * s); T = Vector3.Lerp(P0, P1, Sm(412f, 420f, t)); }
            Vector3 want = BusPos - new Vector3(lat, 0f, 0f), shift = want - T; go.transform.localPosition += shift;
            runH = H + shift; runS = S + shift; runHd = Hd + shift; runRad = rad; runU = u;
        }
        // ---- Galactus' living motion, layered on the pose table: 14 named animations, each with an envelope in time.  d = additive angles [0..10] like the table + [11] torso yaw, [12] torso roll
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
        {
            if (galM == null) return; if (!(t >= 362f && t < 412f)) ShowArm(true); Vector3 pos; bool on = GxPlace(t, g, out pos); var go = galM.Pivot; if (go.activeSelf != on) go.SetActive(on); if (!on) return;
            if (gxTorso == null) return; float[] p = GxAng(t); var gi = GxIdle(t); for (int q = 0; q < 11; q++) p[q] += gi[q]; float s = GS * Fit(galM, 1f) * 60f / Mathf.Max(0.01f, galM.RendererBounds.size.y); bool run = t >= 330f && t < 420f; s = run ? GS * RunS : GS * (1f + 0.9f * Env(184f, 204f, 266f, 276f, t) + 0.5f * Env(130f, 156f, 176f, 186f, t)); gxS = s;
            // idle breathing / the walk-in sway
            float br = 0.5f * Mathf.Sin(t * 0.7f); float lean = p[10] + br; float bend = 100f * Env(186f, 206f, 262f, 280f, t); var rq = Quaternion.Euler(lean * 0.6f + bend, 180f, 0f); go.transform.localRotation = rq; go.transform.localScale = Vector3.one * s; go.transform.localPosition = pos - rq * (gxBelt * s) + (t < 130f ? Vector3.up * 2f * Mathf.Sin(t * 0.9f) : Vector3.zero);
            if (t < 575f)
            {
                gxSwap = root.transform.InverseTransformPoint(gxShL.position).x < root.transform.InverseTransformPoint(gxShR.position).x;   // the part called L stands on your left: then the table's L columns drive R, mirrored
                gxHSh = gxSwap ? gxShR : gxShL; gxHEl = gxSwap ? gxElR : gxElL; gxOSh = gxSwap ? gxShL : gxShR; gxOEl = gxSwap ? gxElL : gxElR; gxHHand = gxSwap ? gxHandR : gxHandL; gxOHand = gxSwap ? gxHandL : gxHandR; float sg = gxSwap ? -1f : 1f;
                // the sculpted guard pose until he starts to move (~2:05), then the table's angles act on straight hanging arms
                float str = Sm(122f, 136f, t);
                gxHSh.localRotation = Quaternion.Euler(p[0], sg * p[2], sg * p[1]) * Quaternion.Slerp(Quaternion.identity, DownSh(gxHEl), str); gxHEl.localRotation = Quaternion.Euler(p[3], 0f, 0f) * Quaternion.Slerp(Quaternion.identity, StraightEl(gxHEl, gxHHand), str);
                gxOSh.localRotation = Quaternion.Euler(p[4], sg * p[6], sg * p[5]) * Quaternion.Slerp(Quaternion.identity, DownSh(gxOEl), str); gxOEl.localRotation = Quaternion.Euler(p[7], 0f, 0f) * Quaternion.Slerp(Quaternion.identity, StraightEl(gxOEl, gxOHand), str);
                gxHead.localRotation = Quaternion.Euler(p[9] + 2f * Mathf.Sin(t * 0.5f), p[8], 0f); gxTorso.localRotation = Quaternion.Euler(0f, 3f * Mathf.Sin(t * 0.3f) + gi[11], gi[12]);
                for (int i = 0; i < gxParts.Count; i++) { gxParts[i].localPosition = gxPartBase[i]; }
                GxLegs(t); GxHitAim(t); GxReach(t); GxRun(t, go, s); GxCup(t);
            }
            else
            {   // 9:35: he comes apart: every piece drifts away, tumbling, burning
                float e = t - 575f; for (int i = 0; i < gxParts.Count; i++) { var d = gxDir[i]; gxParts[i].localPosition = gxPartBase[i] + d * (e * e * 1.2f + e * 4f) * 1.0f; gxParts[i].localRotation = Quaternion.Euler(gxSpin[i] * e * 25f); }
            }
            // haze: far Galactus is a pale silhouette in the clouds
            float hz = t < 130f ? 1f - Sm(55f, 128f, t) : 0f; float hl = t >= 100f && t < 190f ? 0.55f * Sm(100f, 130f, t) * (1f - Sm(176f, 190f, t)) : 0f; float hm = Sm(325f, 336f, t) * (1f - Sm(412f, 420f, t)) * 0.6f; if (Mathf.Abs(hz + hm + hl - GxHaze) > 0.01f || t < 130f || hm > 0f || hl > 0f) { GxHaze = hz + hm + hl; Color c = hl > 0f ? Color.Lerp(Color.white, new Color(1.9f, 1.45f, 2.1f), hl) : hm > 0f ? Color.Lerp(Color.white, new Color(1.9f, 2.1f, 2.7f), hm) : Color.Lerp(Color.white, new Color(2.6f, 2.4f, 2.6f), hz); foreach (var m in galMats) if (m != null) m.color = c; }
        }
        Vector3[] gxPartBase, gxDir, gxSpin;
        // Galactus' glowing places: eyes, chest, mouth (billboards at the model's front), lightning round him, energy under his hand
        Vector3 GxLocal(Vector3 modelLocal) { return galM.Pivot.transform.TransformPoint(modelLocal); }

        // ---------------------------------------------------------------- sound (synthesised, played from a pool of 3D sources)
        AudioClip sBoom, sBig, sThunder, sWhoosh, sZap; AudioSource[] sPool; int sNext; readonly Dictionary<AudioClip, float> sLast = new Dictionary<AudioClip, float>();
        float SfxVol { get { return Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f) * 0.55f; } }
        static AudioClip MakeSfxClip(string name, float len, Func<float, float> f)
        {
            int n = (int)(44100 * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / 44100f), -1f, 1f);
            int rp = 0; return AudioClip.Create(name, n, 1, 44100, false, buf => { for (int q = 0; q < buf.Length; q++) { buf[q] = d[Mathf.Min(rp, n - 1)]; rp++; } }, pos => { rp = pos; });
        }
        void SfxInit()
        {
            var rnd = new System.Random(31); Func<float> N = () => (float)(rnd.NextDouble() * 2.0 - 1.0); float lp = 0f, lp2 = 0f;
            sBoom = MakeSfxClip("GBoom", 1.7f, t => { lp += (N() - lp) * 0.05f; float ph = 6.2832f * (70f * (1f - Mathf.Exp(-1.6f * t)) / 1.6f + 30f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 2.4f) * 0.8f + lp * 2.2f * Mathf.Exp(-t * 3.2f) + N() * 0.25f * Mathf.Exp(-t * 18f); });
            lp = 0f; lp2 = 0f; sBig = MakeSfxClip("GBig", 3.4f, t => { lp += (N() - lp) * 0.025f; lp2 += (lp - lp2) * 0.2f; float ph = 6.2832f * (48f * (1f - Mathf.Exp(-0.9f * t)) / 0.9f + 22f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 1.3f) * 0.85f + lp2 * 5f * Mathf.Exp(-t * 1.6f) + N() * 0.3f * Mathf.Exp(-t * 12f); });
            lp = 0f; sThunder = MakeSfxClip("GThunder", 3.2f, t => { lp += (N() - lp) * 0.018f; float roll = 0.55f + 0.45f * Mathf.Sin(6.2832f * 4.3f * t) * Mathf.Sin(6.2832f * 1.7f * t); return lp * 7f * roll * Mathf.Exp(-t * 0.85f) + N() * 0.5f * Mathf.Exp(-t * 28f); });
            lp = 0f; lp2 = 0f; sWhoosh = MakeSfxClip("GWhoosh", 1.6f, t => { lp += (N() - lp) * 0.12f; lp2 += (lp - lp2) * 0.3f; float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f)); return lp2 * 4f * env + Mathf.Sin(6.2832f * (180f + 400f * t) * t) * 0.15f * env; });
            sZap = MakeSfxClip("GZap", 0.35f, t => { float ph = 6.2832f * (1800f * (1f - Mathf.Exp(-8f * t)) / 8f + 320f * t); return Mathf.Sin(ph) * Mathf.Exp(-t * 9f) * 0.55f + Mathf.Sin(ph * 2.01f) * Mathf.Exp(-t * 14f) * 0.2f + N() * 0.1f * Mathf.Exp(-t * 25f); });
            sAlarm = MakeSfxClip("GAlarm", 0.5f, t => Mathf.Sin(6.2832f * (t < 0.25f ? 880f : 660f) * t) * 0.35f * (t % 0.25f < 0.2f ? 1f : 0f));
            owned.Add(sBoom); owned.Add(sBig); owned.Add(sThunder); owned.Add(sWhoosh); owned.Add(sZap); owned.Add(sAlarm);
            sPool = new AudioSource[10]; for (int i = 0; i < sPool.Length; i++) { var gg = new GameObject("GSfx" + i); gg.transform.SetParent(root.transform, false); var a = gg.AddComponent<AudioSource>(); a.playOnAwake = false; a.spatialBlend = 0.85f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 10f; a.maxDistance = 900f; a.priority = 64; sPool[i] = a; }
        }
        AudioClip sAlarm;
        void Sfx(AudioClip c, Vector3 wp, float vol, float pitch = 1f, float gap = 0.05f)
        {
            if (c == null || sPool == null || paused) return; float now = Time.unscaledTime, last; if (sLast.TryGetValue(c, out last) && now - last < gap) return; sLast[c] = now;
            AudioSource a = null; for (int k = 0; k < sPool.Length; k++) { var s = sPool[(sNext + k) % sPool.Length]; if (!s.isPlaying) { a = s; sNext = (sNext + k + 1) % sPool.Length; break; } }
            if (a == null) { a = sPool[sNext]; sNext = (sNext + 1) % sPool.Length; }
            a.transform.position = wp; a.clip = c; a.volume = Mathf.Clamp01(vol * SfxVol); a.pitch = Mathf.Clamp(pitch * UnityEngine.Random.Range(0.94f, 1.06f), 0.4f, 2f); a.Play();
        }
        void SfxCleanup0() { sPool = null; sLast.Clear(); sBoom = sBig = sThunder = sWhoosh = sZap = sAlarm = null; }
    }
}
