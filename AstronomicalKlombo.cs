using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Klombo (Fortnite Chapter 3): a big friendly plant-backed dinosaur you can spawn in any map, feed Klomberries to, ride, and (in hostile mood) that roars and stomps with knockback. The model has no skeleton, so it is
    // animated by moving its parts: a walk made of bob / roll / pitch and footsteps, a head on a neck pivot that looks at you, a tongue, blinking eyes, breathing. Wandering is a small fixed-step simulation (seeded random
    // numbers), so everybody who has the mod and receives the same spawn message sees Klombo walk about the same way. Riding, feeding and following are local to the person doing them. Menu: J CLIENT > Klombo.
    public static class KlomboApi
    {
        public static bool IsAvailable { get { return AstronomicalEventApi.IsAvailable; } }
        public static bool Alive { get { return AstronomicalKlombo.Alive; } }
        public static bool Loading { get { return AstronomicalKlombo.Loading; } }
        public static string Status { get { return AstronomicalKlombo.StatusLine(); } }
        public static void Spawn() { AstronomicalKlombo.SpawnFromMenu(); }
        public static void Despawn() { AstronomicalKlombo.DespawnFromMenu(); }
        public static void CallToMe() { AstronomicalKlombo.CallToMe(); }
        public static void SpawnBerries() { AstronomicalKlombo.SpawnBerries(); }
        public static void Roar() { AstronomicalKlombo.Roar(true); }
        public static void Stomp() { AstronomicalKlombo.Stomp(true); }
        public static void Ride() { AstronomicalKlombo.Mount(); }
        public static void Dismount() { AstronomicalKlombo.Dismount(); }
        public static void Hop() { AstronomicalKlombo.Hop(); }
        public static bool Riding { get { return AstronomicalKlombo.Riding; } }
        public static int Mood { get { return AstronomicalKlombo.Mood; } set { AstronomicalKlombo.SetMood(value, true); } }
        public static string MoodName { get { return AstronomicalKlombo.MoodName(AstronomicalKlombo.Mood); } }
        public static float Size { get { return AstronomicalKlombo.Size; } set { AstronomicalKlombo.SetSize(value, true); } }
        public static bool Follow { get { return AstronomicalKlombo.Follow; } set { AstronomicalKlombo.Follow = value; } }
        public static bool NamePlate { get { return AstronomicalKlombo.Plate; } set { AstronomicalKlombo.Plate = value; } }
        public static bool Sync { get { return AstronomicalSettings.KlomboSync.Value; } set { AstronomicalSettings.KlomboSync.Value = value; } }
        public static float Happiness { get { return AstronomicalKlombo.Happy; } }
        public static float Hunger { get { return AstronomicalKlombo.Hunger; } }
        public static int BerryCount { get { return AstronomicalKlombo.BerryCount; } }
    }

    internal static class AstronomicalKlombo
    {
        const float STEP = 0.05f, BASE = 0.5f;                       // fixed simulation step; size 1.0 = model scaled by 0.5 (13 m long)
        public static bool Alive, Loading; public static int Mood; public static float Size = 0.6f; public static bool Follow, Riding, Plate = true; public static float Happy = 70f, Hunger = 25f;
        static string status = "not spawned"; static GltfModel model, berryModel; static bool loadFailed, pendingSpawn; static string pendingNet;
        static GameObject root, rig, plate, flashLight; static Transform neckP, eyeP, tongueP, headT; static GltfModel.Clip dummy; static BoxCollider col; static KeyValuePair<Vector3, float> pendingPos; static int pendingSeed;
        static Vector3 p, pPrev, mouthL, saddleL, headTopL; static float yaw, speedCur, phase, headYaw, headPitch, tNow, blinkT = 3f, blinkN, eatT = -9f, roarT = -9f, stompT = -9f, hopT = -9f, stompCd, roarCd, happyDecay, simAcc, lastStep, wobble;
        static int floorLayer, seed; static float walkSpeed { get { return 4.5f * Mathf.Sqrt(Mathf.Max(0.2f, Size)); } }
        // wander segments (deterministic)
        static int segIdx; static float segT, segDur; static bool segWalk; static Vector2 segTo; static Vector3 home; static System.Random rng;
        static Material matGlow, matBerry; static Texture2D glowTex; static ParticleSystem dust; static AudioSource sfx; static WorldKit kit;
        static readonly List<Berry> berries = new List<Berry>(); static LineRenderer[] rings = new LineRenderer[3]; static float[] ringT = { -9f, -9f, -9f }; static Vector3[] ringAt = new Vector3[3]; static int ringNext;
        sealed class Berry { public GameObject G; public Vector3 P, V; public int Held; public float Age, Eat; }
        static TextMesh tTitle, tMood, tHappy, tHunger; static Transform barHappy, barHunger; static Vector3 prevL, prevR; static bool gripPrevL, gripPrevR; static Vector3 carryPrev; static bool carryHave;
        public static int BerryCount { get { return berries.Count; } }
        public static string MoodName(int m) { return m == 0 ? "Calm" : m == 1 ? "Playful" : "Hostile"; }
        public static string StatusLine()
        {
            if (loadFailed) return "Klombo files missing"; if (Loading) return "Klombo loading..."; if (!Alive) return "Klombo: not spawned";
            return "Klombo: " + MoodName(Mood) + (Riding ? " | riding" : Follow ? " | following" : "") + " | happy " + (int)Happy + "% | hunger " + (int)Hunger + "%";
        }
        static int LocoMask() { try { return GTPlayer.LocomotionEnabledLayers.value; } catch { } return ~0; }
        static Vector3? Head() { try { var pl = GTPlayer.Instance; if (pl != null) return pl.HeadCenterPosition; } catch { } return null; }
        static float Sm(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }
        static float Rnd(float a, float b) { return UnityEngine.Random.Range(a, b); }
        // the floor under `at`, looked for from 3 m above the reference height (a spawn / walk must never latch on to a ceiling or a roof far above and climb it)
        static Vector3 Ground(Vector3 at, Vector3 refPos)
        {
            RaycastHit h; if (Physics.Raycast(new Vector3(at.x, refPos.y + 3f, at.z), Vector3.down, out h, 14f, LocoMask(), QueryTriggerInteraction.Ignore)) return h.point; return new Vector3(at.x, refPos.y, at.z);
        }

        // ------------------------------------------------------------------ spawning / loading
        public static void SpawnFromMenu()
        {
            if (Alive) return; var hd = Head(); if (!hd.HasValue) return; var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            Vector3 at = Ground(hd.Value + f * (9f + 12f * Size), hd.Value - Vector3.up * 1.4f); float y = Mathf.Atan2(-f.x, -f.z) * Mathf.Rad2Deg;           // faces the player
            int sd = UnityEngine.Random.Range(1, 99999); Begin(at, y, sd, 0f);
            if (AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "sp:" + at.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ":" + at.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ":" + at.z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ":" + y.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ":" + Mood, Size, sd);
        }
        public static void DespawnFromMenu() { if (!Alive && !pendingSpawn) return; Despawn(); if (AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "dp", 0f, 0); }
        static void Begin(Vector3 at, float y, int sd, float catchUp)
        {
            if (Alive) return; pendingSpawn = true; pendingPos = new KeyValuePair<Vector3, float>(at, y); pendingSeed = sd; pendingCatch = catchUp; if (model == null && !Loading) StartLoad();
        }
        static float pendingCatch;
        static void StartLoad()
        {
            if (AstronomicalEventMod.Inst == null || AstronomicalEventMod.Inst.Loader == null) { status = "plugin not ready"; return; }
            Loading = true; loadFailed = false; AstronomicalEventMod.Inst.StartCoroutine(LoadCo());
        }
        static IEnumerator LoadCo()
        {
            var ld = AstronomicalEventMod.Inst.Loader; string dir = Path.Combine(AstronomicalAssetLoader.Dir, "klombo"); GltfModel a = null, b = null;
            yield return ld.LoadExtra(Path.Combine(dir, "klombo.glb"), true, m => a = m); yield return ld.LoadExtra(Path.Combine(dir, "klomberry.glb"), true, m => b = m);
            Loading = false; if (a == null) { loadFailed = true; pendingSpawn = false; AstronomicalEventMod.Log.LogWarning("Klombo: klombo.glb missing / failed"); yield break; }
            model = a; berryModel = b; AstronomicalEventMod.Log.LogInfo("Klombo loaded (berries " + (b != null) + ")");
        }
        static Transform Node(string n) { foreach (var t in model.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        static Bounds LB(Transform rootT, Transform node)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in node.GetComponentsInChildren<MeshFilter>(true)) { if (mf.sharedMesh == null) continue; var mb = mf.sharedMesh.bounds; for (int k = 0; k < 8; k++) { var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1)); var q = rootT.InverseTransformPoint(mf.transform.TransformPoint(c)); if (first) { b = new Bounds(q, Vector3.zero); first = false; } else b.Encapsulate(q); } }
            return b;
        }
        static Transform Pivot(Transform parent, string name, Vector3 localPos) { var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = localPos; return g.transform; }
        static void Build()
        {
            pendingSpawn = false; floorLayer = 0; int m = LocoMask(); for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) { floorLayer = i; break; }
            kit = new WorldKit { Layer = floorLayer }; glowTex = kit.GlowTex(); matGlow = kit.Glow(Color.white, glowTex, 3000);
            root = new GameObject("AstronomicalKlombo"); rig = new GameObject("Rig"); rig.transform.SetParent(root.transform, false);
            var pv = model.Pivot.transform; pv.SetParent(rig.transform, false); pv.localPosition = Vector3.zero; pv.localRotation = Quaternion.identity; model.Pivot.SetActive(true);
            foreach (var r in model.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            Transform gums = Node("Object_2"), eye = Node("Object_3"), body = Node("Object_4"), head = Node("Object_5"), teeth = Node("Object_6"), tongue = Node("Object_7");
            Bounds hb = head != null ? LB(pv, head) : new Bounds(new Vector3(0, 4, 9), new Vector3(8, 4, 8)), bb = body != null ? LB(pv, body) : new Bounds(new Vector3(0, 3.5f, 0), new Vector3(10, 7, 22)), eb = eye != null ? LB(pv, eye) : hb, tb = tongue != null ? LB(pv, tongue) : hb;
            neckP = Pivot(pv, "Neck", new Vector3(hb.center.x, hb.center.y, hb.min.z + 0.5f)); eyeP = Pivot(neckP, "EyePivot", Vector3.zero); eyeP.position = pv.TransformPoint(eb.center); tongueP = Pivot(neckP, "TonguePivot", Vector3.zero); tongueP.position = pv.TransformPoint(new Vector3(tb.center.x, tb.center.y, tb.min.z));
            foreach (var t in new[] { head, gums, teeth }) if (t != null) t.SetParent(neckP, true); if (eye != null) eye.SetParent(eyeP, true); if (tongue != null) tongue.SetParent(tongueP, true); headT = head;
            mouthL = new Vector3(hb.center.x, hb.min.y + 0.35f * hb.size.y, hb.max.z); saddleL = new Vector3(bb.center.x, bb.max.y - 0.5f, bb.center.z + 1.0f); headTopL = new Vector3(hb.center.x, hb.max.y + 1.0f, hb.center.z);
            var cg = new GameObject("KlomboCollider"); cg.layer = floorLayer; cg.transform.SetParent(pv, false); col = cg.AddComponent<BoxCollider>(); col.center = bb.center - new Vector3(0, 0.3f, 0); col.size = new Vector3(bb.size.x * 0.86f, bb.size.y * 0.8f, bb.size.z * 0.8f);
            root.transform.localScale = Vector3.one * Size * BASE; p = pendingPos.Key; yaw = pendingPos.Value; seed = pendingSeed; pPrev = p; home = p; rng = new System.Random(seed); segIdx = 0; segT = 0f; segDur = 0f; segWalk = false; speedCur = 0f; Riding = false; carryHave = false;
            dust = MakePS(); BuildRings(); BuildPlate(); sfx = root.AddComponent<AudioSource>(); sfx.spatialBlend = 1f; sfx.rolloffMode = AudioRolloffMode.Linear; sfx.maxDistance = 200f; sfx.minDistance = 8f; sfx.playOnAwake = false;
            matBerry = null; Alive = true; SetPose(0f); status = "alive";
            for (float s = 0; s < pendingCatch; s += STEP) Step(STEP, true); pendingCatch = 0f;                // a late joiner fast-forwards the wander so Klombo is where it is for everybody else
            AstronomicalEventMod.Log.LogInfo("Klombo spawned at " + p + " size " + Size + " seed " + seed + " head " + (headT != null) + " body " + bb.size);
        }
        static ParticleSystem MakePS()
        {
            var g = new GameObject("KlomboDust"); g.transform.SetParent(root.transform, false); var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.maxParticles = 800; m.simulationSpace = ParticleSystemSimulationSpace.World; m.playOnAwake = false; m.startLifetime = 1.5f; m.gravityModifier = -0.02f; var e = ps.emission; e.enabled = false; g.GetComponent<ParticleSystemRenderer>().sharedMaterial = matGlow;
            var col2 = ps.colorOverLifetime; col2.enabled = true; var gr = new Gradient(); gr.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }; gr.alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) }; col2.color = gr;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f)); ps.Play(); return ps;
        }
        static void Puff(Vector3 at, int n, float size, Color c, float speed)
        {
            if (dust == null) return; n = (int)(n * Mathf.Max(0.3f, AstronomicalSettings.Particles.Value));
            for (int k = 0; k < n; k++) { var ep = new ParticleSystem.EmitParams(); ep.position = at + UnityEngine.Random.insideUnitSphere * size * 0.4f; ep.velocity = new Vector3(Rnd(-1f, 1f), Rnd(0.1f, 0.6f), Rnd(-1f, 1f)) * speed; ep.startColor = c; ep.startSize = size * Rnd(0.6f, 1.2f); ep.startLifetime = Rnd(0.9f, 1.8f); dust.Emit(ep, 1); }
        }
        static void BuildRings()
        {
            var lt = kit.Tex(4, 32, (x, y) => { float v = Mathf.Abs((y + 0.5f) / 16f - 1f); return new Color(1, 1, 1, Mathf.Pow(1f - v, 1.6f)); }, TextureWrapMode.Clamp); var soft = kit.Glow(Color.white, lt, 3001);
            for (int i = 0; i < rings.Length; i++) { var g = new GameObject("KlomboRing" + i); g.transform.SetParent(root.transform, false); var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = true; l.positionCount = 48; l.loop = true; l.sharedMaterial = soft; l.shadowCastingMode = ShadowCastingMode.Off; l.enabled = false; rings[i] = l; ringT[i] = -9f; }
        }
        static void Ring(Vector3 at) { ringAt[ringNext] = at; ringT[ringNext] = tNow; ringNext = (ringNext + 1) % rings.Length; }
        public static void Despawn()
        {
            Riding = false; pendingSpawn = false; Alive = false; foreach (var b in berries) if (b.G != null) UnityEngine.Object.Destroy(b.G); berries.Clear();
            if (model != null && model.Pivot != null) { var pv = model.Pivot.transform; pv.SetParent(null, false); model.Pivot.SetActive(false); AstronomicalEventMod.ParkModel(model.Pivot);
                // the parts were re-parented under pivots (and posed): put every pivot back to neutral, give the parts back to the model and destroy the pivots, so the next spawn starts clean
                pv.localScale = Vector3.one; var mdl = model.Model != null ? model.Model.transform : pv;
                foreach (var pair in new[] { eyeP, tongueP, neckP }) { if (pair == null) continue; pair.localRotation = Quaternion.identity; pair.localScale = Vector3.one; var kids = new List<Transform>(); foreach (Transform k in pair) kids.Add(k); foreach (var k in kids) if (k.name.StartsWith("Object_")) k.SetParent(mdl, true); UnityEngine.Object.Destroy(pair.gameObject); } neckP = eyeP = tongueP = null; }
            if (plate != null) UnityEngine.Object.Destroy(plate); plate = null; if (root != null) UnityEngine.Object.Destroy(root); root = null; rig = null;
            if (kit != null) foreach (var o in kit.Owned) if (o != null) UnityEngine.Object.Destroy(o); kit = null; status = "not spawned";
        }
        public static void Cleanup() { if (Alive || pendingSpawn) Despawn(); }

        // ------------------------------------------------------------------ menu actions / network
        public static void SetMood(int m, bool publish) { Mood = Mathf.Clamp(m, 0, 2); if (publish && Alive && AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "md:" + Mood, Mood, 0); }
        public static void SetSize(float s, bool publish) { Size = Mathf.Clamp(s, 0.15f, 4f); if (root != null) root.transform.localScale = Vector3.one * Size * BASE; if (publish && Alive && AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "sz", Size, 0); }
        public static void CallToMe()
        {
            if (!Alive) return; var hd = Head(); if (!hd.HasValue) return; var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; f.Normalize(); p = Ground(hd.Value + f * (8f + 10f * Size), hd.Value - Vector3.up * 1.4f); pPrev = p; home = p; Riding = false; Puff(p, 30, 3f * Size * BASE * 2f, new Color(0.8f, 0.75f, 0.6f, 0.6f), 3f); Ring(p);
        }
        public static void FromNet(string ev, int seq, float cd, float age)
        {
            if (!AstronomicalSettings.KlomboSync.Value) return; var parts = ev.Split(':'); var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (parts[0] == "sp" && parts.Length >= 6 && !Alive) { Size = cd; Mood = int.Parse(parts[5]); Begin(new Vector3(float.Parse(parts[1], ci), float.Parse(parts[2], ci), float.Parse(parts[3], ci)), float.Parse(parts[4], ci), seq, Mathf.Min(age, 120f)); }
            else if (parts[0] == "dp") { if (Alive || pendingSpawn) Despawn(); }
            else if (parts[0] == "md") Mood = Mathf.Clamp((int)cd, 0, 2); else if (parts[0] == "sz") SetSize(cd, false); else if (parts[0] == "ro") Roar(false); else if (parts[0] == "st") Stomp(false);
        }

        // ------------------------------------------------------------------ the simulation (fixed step)
        public static void Hop() { if (Alive) hopT = tNow; }
        public static void Roar(bool publish) { if (!Alive || tNow - roarT < 2.5f) return; roarT = tNow; roarCd = 12f; if (publish && AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "ro", 0f, 0); }
        public static void Stomp(bool publish) { if (!Alive || tNow - stompT < 1.5f) return; stompT = tNow; stompCd = 4f; if (publish && AstronomicalSettings.KlomboSync.Value) AstronomicalNet.Publish("kl", "st", 0f, 0); }
        static float WorldScale { get { return Size * BASE; } }
        static Vector3 MouthW() { return model.Pivot.transform.TransformPoint(mouthL); }
        static void NewSegment()
        {
            var r = new System.Random(seed * 7919 + segIdx++); double w = Mood == 1 ? 0.8 : Mood == 2 ? 0.4 : 0.55; segWalk = r.NextDouble() < w; segT = 0f;
            if (segWalk) { double a = r.NextDouble() * Math.PI * 2, d = (6 + r.NextDouble() * 22) * WorldScale * 2f; var h2 = new Vector2(home.x, home.z); segTo = h2 + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * (float)d; segDur = 40f; }
            else segDur = (float)(2 + r.NextDouble() * (Mood == 1 ? 3 : 6));
        }
        static void Turn(Vector2 dir, float dt, float rate) { if (dir.sqrMagnitude < 1e-4f) return; float want = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; yaw = Mathf.MoveTowardsAngle(yaw, want, rate * dt); }
        static void Step(float dt, bool catchUp)
        {
            if (!Alive) return; Vector3 old = p; Vector2 goal = Vector2.zero; bool hasGoal = false; float spd = walkSpeed, stopDist = 1.5f; bool busy = tNow - roarT < 1.6f || tNow - stompT < 1.0f;
            Vector3? hd = catchUp ? (Vector3?)null : Head(); Vector3? hp = hd; Vector2 pXZ = new Vector2(p.x, p.z);
            if (Riding && !catchUp) { Ride(dt); }
            else if (!busy)
            {
                float dPl = hp.HasValue ? Vector2.Distance(pXZ, new Vector2(hp.Value.x, hp.Value.z)) : 9999f; Berry target = null;
                if (!catchUp && (Hunger > 35f || Mood == 1)) { float best = 45f; foreach (var b in berries) { if (b.Held != 0 || b.Eat > 0f) continue; float d = Vector2.Distance(pXZ, new Vector2(b.P.x, b.P.z)); if (d < best) { best = d; target = b; } } }
                if (Mood == 2 && hp.HasValue && dPl < 80f * WorldScale * 2f) { goal = new Vector2(hp.Value.x, hp.Value.z); hasGoal = true; spd *= 1.25f; stopDist = 9f * WorldScale * 2f; }
                else if (target != null) { goal = new Vector2(target.P.x, target.P.z); hasGoal = true; spd *= 1.15f; stopDist = 2.2f * WorldScale * 2f; }
                else if (!catchUp && Follow && hp.HasValue && dPl > 10f * WorldScale * 2f) { var dir = (pXZ - new Vector2(hp.Value.x, hp.Value.z)).normalized; goal = new Vector2(hp.Value.x, hp.Value.z) + dir * 7f * WorldScale * 2f; hasGoal = true; }
                else
                {
                    if (segDur <= 0f) NewSegment(); segT += dt;
                    if (segWalk) { goal = segTo; hasGoal = true; stopDist = 1.2f; if (Vector2.Distance(pXZ, segTo) < 1.5f) { segDur = 0f; hasGoal = false; } }
                    else if (segT >= segDur) segDur = 0f;
                }
                if (hasGoal)
                {
                    var dir = goal - pXZ; float dist = dir.magnitude; if (dist > stopDist) { Turn(dir, dt, 55f); float al = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg)); speedCur = Mathf.MoveTowards(speedCur, al > 50f ? spd * 0.25f : spd, spd * 1.2f * dt); }
                    else { speedCur = Mathf.MoveTowards(speedCur, 0f, spd * 2f * dt); if (target != null && dist < stopDist + 0.5f) { EatBerry(target); } if (Mood == 2 && !catchUp) Hostile(dPl); }
                }
                else speedCur = Mathf.MoveTowards(speedCur, 0f, spd * 2f * dt);
                if (!catchUp && Mood == 2 && hp.HasValue) { Hostile(dPl); }
            }
            else speedCur = Mathf.MoveTowards(speedCur, 0f, walkSpeed * 3f * dt);
            if (!Riding || catchUp) { var fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)); p += fwd * speedCur * dt; }
            if (!catchUp) { var gp = Ground(p, p); p.y = Mathf.Lerp(p.y, gp.y, Mathf.Clamp01(dt * 8f)); } stompCd = Mathf.Max(0f, stompCd - dt); roarCd = Mathf.Max(0f, roarCd - dt);
        }
        static void Hostile(float d)
        {
            float ws = WorldScale * 2f; if (d < 24f * ws && stompCd <= 0f && tNow - roarT > 1.7f) Stomp(true); else if (d < 45f * ws && roarCd <= 0f && tNow - stompT > 1.2f) Roar(true);
        }
        static void Ride(float dt)
        {
            float fwdIn = 0f, turnIn = 0f; try { var cp = ControllerInputPoller.instance; if (cp != null) { fwdIn = cp.leftControllerPrimary2DAxis.y; turnIn = cp.leftControllerPrimary2DAxis.x; } } catch { }
            try { var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null) { if (kb.wKey.isPressed) fwdIn = 1f; if (kb.sKey.isPressed) fwdIn = -0.5f; if (kb.dKey.isPressed) turnIn = 1f; if (kb.aKey.isPressed) turnIn = -1f; } } catch { }
            yaw += turnIn * 55f * dt; float want = Mathf.Clamp(fwdIn, -0.5f, 1f) * walkSpeed * 1.4f; speedCur = Mathf.MoveTowards(speedCur, want, walkSpeed * 1.6f * dt); var fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)); p += fwd * speedCur * dt;
        }
        static void EatBerry(Berry b) { if (b.Eat > 0f) return; b.Eat = 0.01f; eatT = tNow; Hunger = Mathf.Max(0f, Hunger - 40f); Happy = Mathf.Min(100f, Happy + 25f); hopT = tNow + 0.3f; }

        // ------------------------------------------------------------------ mounting
        public static void Mount()
        {
            if (!Alive || Riding) return; var pl = GTPlayer.Instance; if (pl == null) return; var cam = Camera.main;
            Vector3 s = model.Pivot.transform.TransformPoint(saddleL) + Vector3.up * 1.0f; try { pl.TeleportTo(s, Quaternion.Euler(0f, yaw, 0f), false, false); } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Klombo mount: " + e.Message); return; }
            Riding = true; carryHave = false; Follow = false; hopT = tNow;
        }
        public static void Dismount() { Riding = false; carryHave = false; }
        static void Carry()
        {
            var pl = GTPlayer.Instance; if (pl == null || model == null) return; Vector3 s = model.Pivot.transform.TransformPoint(saddleL);
            if (carryHave) { Vector3 d = s - carryPrev; if (d.sqrMagnitude > 0f && d.sqrMagnitude < 25f) { try { pl.playerRigidBody.position += d; } catch { } } }
            carryPrev = s; carryHave = true; var hd = Head(); if (hd.HasValue && (hd.Value - s).magnitude > 14f * WorldScale * 2f + 6f) Dismount();          // jumped off / thrown off
        }

        // ------------------------------------------------------------------ berries
        public static void SpawnBerries()
        {
            if (berryModel == null) { if (!Loading && model == null) StartLoad(); status = "loading"; return; } var hd = Head(); if (!hd.HasValue) return; var cam = Camera.main; Vector3 f = cam != null ? cam.transform.forward : Vector3.forward; f.y = 0f; f.Normalize(); Vector3 r = Vector3.Cross(Vector3.up, f);
            for (int k = 0; k < 5; k++)
            {
                Vector3 at = Ground(hd.Value + f * Rnd(1.4f, 2.8f) + r * (k - 2) * 0.55f, hd.Value - Vector3.up * 1.4f); var g = UnityEngine.Object.Instantiate(berryModel.Pivot); g.SetActive(true); g.transform.SetParent(null, false); g.transform.position = at; g.transform.localScale = Vector3.one * 0.8f;
                foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) { rr.shadowCastingMode = ShadowCastingMode.Off; rr.receiveShadows = false; rr.allowOcclusionWhenDynamic = false; }
                berries.Add(new Berry { G = g, P = at }); if (matBerry == null) { var rr2 = g.GetComponentInChildren<Renderer>(); if (rr2 != null && rr2.sharedMaterials.Length > 1) matBerry = rr2.sharedMaterials[1]; }
            }
            Hunger = Mathf.Min(100f, Hunger + 0f); while (berries.Count > 14) { if (berries[0].G != null) UnityEngine.Object.Destroy(berries[0].G); berries.RemoveAt(0); }
        }
        static void BerryStep(float dt)
        {
            var gt = GorillaTagger.Instance; bool gl = false, gr = false; Vector3 lp = Vector3.zero, rp = Vector3.zero; try { var cp = ControllerInputPoller.instance; if (cp != null && gt != null) { gl = cp.leftControllerGripFloat > 0.6f; gr = cp.rightControllerGripFloat > 0.6f; lp = gt.leftHandTransform.position; rp = gt.rightHandTransform.position; } } catch { }
            Vector3 lv = dt > 0f ? (lp - prevL) / dt : Vector3.zero, rv = dt > 0f ? (rp - prevR) / dt : Vector3.zero; prevL = lp; prevR = rp;
            for (int i = berries.Count - 1; i >= 0; i--)
            {
                var b = berries[i]; b.Age += dt; if (b.G == null || b.Age > 400f) { if (b.G != null) UnityEngine.Object.Destroy(b.G); berries.RemoveAt(i); continue; }
                if (b.Eat > 0f) { b.Eat += dt * 2.5f; Vector3 m = Alive ? MouthW() : b.P; b.P = Vector3.Lerp(b.P, m, Mathf.Clamp01(dt * 8f)); b.G.transform.position = b.P; b.G.transform.localScale = Vector3.one * 0.8f * Mathf.Max(0.05f, 1f - b.Eat); if (b.Eat > 1f) { Puff(b.P, 18, 0.5f * WorldScale * 2f, new Color(0.4f, 0.8f, 1f, 0.8f), 2f); UnityEngine.Object.Destroy(b.G); berries.RemoveAt(i); } continue; }
                if (b.Held == 0)
                {
                    if (gl && (b.P - lp).sqrMagnitude < 0.45f * 0.45f) b.Held = 1; else if (gr && (b.P - rp).sqrMagnitude < 0.45f * 0.45f) b.Held = 2;
                    if (b.Held == 0 && b.V.sqrMagnitude > 0.01f) { b.V += Vector3.down * 9.8f * dt; b.P += b.V * dt; var gp = Ground(b.P + Vector3.up * 0.3f, b.P); if (b.P.y < gp.y) { b.P.y = gp.y; b.V = Vector3.zero; } b.G.transform.position = b.P; }
                }
                else
                {
                    bool still = b.Held == 1 ? gl : gr; Vector3 hp2 = b.Held == 1 ? lp : rp; b.P = hp2; b.G.transform.position = hp2 + Vector3.up * 0.0f;
                    if (!still) { b.V = b.Held == 1 ? lv : rv; b.Held = 0; }
                    if (Alive && (hp2 - MouthW()).magnitude < 2.2f * WorldScale * 2f) { b.Held = 0; EatBerry(b); }
                }
                if (b.Held == 0 && Alive && b.V.sqrMagnitude < 0.01f && (b.P - MouthW()).magnitude < 1.8f * WorldScale * 2f && b.Eat <= 0f && Hunger > 5f) EatBerry(b);
            }
            if (matBerry != null) { float k = 0.75f + 0.25f * Mathf.Sin(tNow * 3f); matBerry.color = new Color(k, k, 1f, 1f); }
        }

        // ------------------------------------------------------------------ the world-space name plate / status UI
        static Transform Quad(string n, Transform parent, Color c, Vector3 pos, Vector2 size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>()); q.name = n; q.transform.SetParent(parent, false); q.transform.localPosition = pos; q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var m = kit.Glow(c, null, 3010); q.GetComponent<Renderer>().sharedMaterial = m; q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; return q.transform;
        }
        static TextMesh Txt(Transform parent, string s, float size, Color c, Vector3 pos, TextAnchor an = TextAnchor.MiddleCenter)
        {
            var g = new GameObject("T"); g.transform.SetParent(parent, false); g.transform.localPosition = pos; var t = g.AddComponent<TextMesh>(); Font f = null; try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f != null) { t.font = f; t.GetComponent<MeshRenderer>().material = f.material; t.GetComponent<MeshRenderer>().material.renderQueue = 3020; }
            t.fontSize = 64; t.characterSize = size; t.anchor = an; t.alignment = TextAlignment.Center; t.color = c; t.text = s; return t;
        }
        static void BuildPlate()
        {
            plate = new GameObject("KlomboPlate"); var T = plate.transform;
            Quad("Bg", T, new Color(0.05f, 0.04f, 0.09f, 0.62f), Vector3.zero, new Vector2(2.4f, 1.25f)); Quad("Edge", T, new Color(0.7f, 0.4f, 0.95f, 0.9f), new Vector3(0f, 0.6f, -0.001f), new Vector2(2.4f, 0.05f));
            tTitle = Txt(T, "KLOMBO", 0.075f, new Color(1f, 0.85f, 0.4f), new Vector3(0f, 0.38f, -0.01f)); tMood = Txt(T, "Calm", 0.05f, Color.white, new Vector3(0f, 0.16f, -0.01f));
            tHappy = Txt(T, "HAPPY", 0.032f, new Color(1f, 0.7f, 0.85f), new Vector3(-0.75f, -0.1f, -0.01f), TextAnchor.MiddleLeft); tHunger = Txt(T, "HUNGER", 0.032f, new Color(1f, 0.8f, 0.5f), new Vector3(-0.75f, -0.38f, -0.01f), TextAnchor.MiddleLeft);
            Quad("HappyBack", T, new Color(0.2f, 0.12f, 0.2f, 0.9f), new Vector3(0.28f, -0.1f, -0.005f), new Vector2(1.3f, 0.12f)); barHappy = Quad("HappyFill", T, new Color(1f, 0.45f, 0.7f, 1f), new Vector3(0.28f, -0.1f, -0.01f), new Vector2(1.3f, 0.12f));
            Quad("HungerBack", T, new Color(0.2f, 0.14f, 0.08f, 0.9f), new Vector3(0.28f, -0.38f, -0.005f), new Vector2(1.3f, 0.12f)); barHunger = Quad("HungerFill", T, new Color(1f, 0.65f, 0.2f, 1f), new Vector3(0.28f, -0.38f, -0.01f), new Vector2(1.3f, 0.12f));
        }
        static void PlateStep()
        {
            if (plate == null) return; bool show = Plate && Alive; if (plate.activeSelf != show) plate.SetActive(show); if (!show) return; var cam = Camera.main; if (cam == null) return;
            Vector3 top = model.Pivot.transform.TransformPoint(headTopL); float d = Vector3.Distance(cam.transform.position, top); float s = Mathf.Clamp(d * 0.11f, 0.5f * Mathf.Max(0.6f, WorldScale * 2f), 3.2f);
            plate.transform.position = top + Vector3.up * (0.5f + 0.6f * s); plate.transform.rotation = Quaternion.LookRotation(plate.transform.position - cam.transform.position, Vector3.up); plate.transform.localScale = Vector3.one * s;
            tMood.text = MoodName(Mood) + (Riding ? "  - riding" : Follow ? "  - following" : berries.Count > 0 ? "  - " + berries.Count + " berries" : ""); tMood.color = Mood == 0 ? new Color(0.6f, 1f, 0.7f) : Mood == 1 ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            float hf = Mathf.Clamp01(Happy / 100f), uf = Mathf.Clamp01(Hunger / 100f); barHappy.localScale = new Vector3(1.3f * Mathf.Max(0.01f, hf), 0.12f, 1f); barHappy.localPosition = new Vector3(0.28f - 0.65f * (1f - hf), -0.1f, -0.01f);
            barHunger.localScale = new Vector3(1.3f * Mathf.Max(0.01f, uf), 0.12f, 1f); barHunger.localPosition = new Vector3(0.28f - 0.65f * (1f - uf), -0.38f, -0.01f); tHunger.text = Hunger > 60f ? "HUNGRY!" : "HUNGER";
        }

        // ------------------------------------------------------------------ per frame
        static void SetPose(float dt)
        {
            if (root == null) return; float a = Mathf.Clamp01(simAcc / STEP); Vector3 ip = Vector3.Lerp(pPrev, p, a); root.transform.position = ip; root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float w = Mathf.Clamp(speedCur / Mathf.Max(0.1f, walkSpeed), -0.5f, 1.5f); phase += dt * Mathf.Abs(speedCur) / (3.2f * WorldScale * 2f) * Mathf.PI * 2f * 0.5f; float s1 = Mathf.Sin(phase), s2 = Mathf.Sin(phase * 2f);
            float hop = Mathf.Clamp01((tNow - hopT) / 0.7f); float hopY = hopT > -5f && hop < 1f ? Mathf.Sin(hop * Mathf.PI) * 1.6f : 0f; float stompK = Mathf.Clamp01((tNow - stompT) / 1.0f), roarK = Mathf.Clamp01((tNow - roarT) / 1.6f), eatK = Mathf.Clamp01((tNow - eatT) / 1.2f);
            float stompPitch = stompT > -5f && stompK < 1f ? (stompK < 0.5f ? -9f * Mathf.Sin(stompK / 0.5f * Mathf.PI * 0.5f) : -9f * (1f - Mathf.Pow((stompK - 0.5f) / 0.5f, 3f)) + 4f * Mathf.Sin((stompK - 0.5f) * Mathf.PI * 2f)) : 0f;
            float breathe = 1f + 0.012f * Mathf.Sin(tNow * 1.6f) + (roarT > -5f && roarK < 1f ? 0.06f * Mathf.Sin(roarK * Mathf.PI) : 0f);
            rig.transform.localPosition = new Vector3(0f, Mathf.Abs(s1) * 0.22f * Mathf.Clamp01(w) + hopY - (stompT > -5f && stompK > 0.45f && stompK < 0.7f ? 0.35f : 0f), 0f);
            rig.transform.localRotation = Quaternion.Euler(1.4f * s2 * Mathf.Clamp01(w) + stompPitch + (eatT > -5f && eatK < 1f ? 5f * Mathf.Sin(eatK * Mathf.PI) : 0f), 0f, 2.6f * s1 * Mathf.Clamp01(w));
            model.Pivot.transform.localScale = new Vector3(1f, breathe, 1f);
            // head: looks at the player when calm / playful (limited turn), sways with the walk, dips to eat, rises to roar
            float wantYaw = 0f, wantPitch = 0f; var hd = Head();
            if (hd.HasValue && Mood != 2 && !Riding) { var loc = root.transform.InverseTransformPoint(hd.Value); wantYaw = Mathf.Clamp(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg, -55f, 55f); wantPitch = Mathf.Clamp(-Mathf.Atan2(loc.y - 4f, new Vector2(loc.x, loc.z).magnitude) * Mathf.Rad2Deg * 0.6f, -12f, 14f); }
            else if (hd.HasValue && Mood == 2) { var loc = root.transform.InverseTransformPoint(hd.Value); wantYaw = Mathf.Clamp(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg, -45f, 45f); }
            headYaw = Mathf.Lerp(headYaw, wantYaw, Mathf.Clamp01(dt * 3f)); headPitch = Mathf.Lerp(headPitch, wantPitch, Mathf.Clamp01(dt * 3f));
            float eatPitch = eatT > -5f && eatK < 1f ? 26f * Mathf.Sin(eatK * Mathf.PI) : 0f, roarPitch = roarT > -5f && roarK < 1f ? -34f * Mathf.Sin(Mathf.Min(1f, roarK * 1.4f) * Mathf.PI * 0.8f) : 0f;
            neckP.localRotation = Quaternion.Euler(headPitch + eatPitch + roarPitch + 2.5f * s2 * Mathf.Clamp01(w) + 1.2f * Mathf.Sin(tNow * 0.9f), headYaw - 5f * s1 * Mathf.Clamp01(w) + (roarT > -5f && roarK < 1f ? 4f * Mathf.Sin(tNow * 40f) * (1f - roarK) : 0f), 0f);
            float tong = Mathf.Max(roarT > -5f && roarK < 1f ? Mathf.Sin(roarK * Mathf.PI) : 0f, eatT > -5f && eatK < 1f ? Mathf.Sin(Mathf.Clamp01(eatK * 1.6f) * Mathf.PI) : 0f); tongueP.localScale = new Vector3(1f, 1f, 1f + 0.9f * tong);
            blinkT -= dt; if (blinkT < 0f) { blinkT = Rnd(2.5f, 6f); blinkN = 0.14f; } blinkN = Mathf.Max(0f, blinkN - dt); eyeP.localScale = new Vector3(1f, blinkN > 0f ? 0.12f : 1f, 1f);
            // footsteps
            float ft = Mathf.Sign(s1); if (ft != lastFt && Mathf.Clamp01(w) > 0.25f) { lastFt = ft; Footstep(); } else if (Mathf.Clamp01(w) <= 0.25f) lastFt = ft;
            // rings
            for (int i = 0; i < rings.Length; i++) { float age = tNow - ringT[i]; bool on = age >= 0f && age < 1.8f; if (rings[i].enabled != on) rings[i].enabled = on; if (!on) continue; float k = age / 1.8f, r = (2f + age * 28f) * WorldScale * 2f; for (int j = 0; j < 48; j++) { float an = j * Mathf.PI * 2f / 48f; rings[i].SetPosition(j, ringAt[i] + new Vector3(Mathf.Cos(an) * r, 0.2f, Mathf.Sin(an) * r)); } rings[i].startColor = rings[i].endColor = new Color(1f, 0.9f, 0.7f, 0.8f * (1f - k)); rings[i].widthMultiplier = 1.4f * WorldScale * 2f * (1f - 0.6f * k); }
        }
        static float lastFt = 1f;
        static void Footstep()
        {
            Vector3 foot = root.transform.TransformPoint(new Vector3(UnityEngine.Random.value > 0.5f ? 3f : -3f, 0f, UnityEngine.Random.Range(-6f, 6f))); Puff(foot, 5, 1.4f * WorldScale * 2f, new Color(0.7f, 0.65f, 0.5f, 0.5f), 1.5f);
            var hd = Head(); if (hd.HasValue) { float d = Vector3.Distance(hd.Value, foot), reach = 40f * WorldScale * 2f; if (d < reach) { float amp = Mathf.Clamp01((1f - d / reach) * 0.5f * Mathf.Clamp(WorldScale * 2f, 0.3f, 1.4f)); try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, amp, 0.1f); gt.StartVibration(false, amp, 0.1f); } } catch { } } }
            if (AstronomicalShowdown.thumpClip == null) AstronomicalShowdown.MakeSfx(); if (sfx != null && AstronomicalShowdown.thumpClip != null) sfx.PlayOneShot(AstronomicalShowdown.thumpClip, Mathf.Clamp01(0.25f * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f)));
        }
        static float lastStompDone = -9f, lastRoarDone = -9f;
        static void ImpactStep()
        {
            // the stomp lands about 0.5 s into the animation, the roar blast a little after it starts
            if (stompT != lastStompDone && tNow - stompT > 0.5f) { lastStompDone = stompT; Vector3 at = root.transform.TransformPoint(new Vector3(0f, 0f, 10f)); Ring(at); Puff(at, 40, 3f * WorldScale * 2f, new Color(0.75f, 0.7f, 0.55f, 0.7f), 4f); AstronomicalHazards.Blast(at, 26f * WorldScale * 2f, 13f, 14f); Shake(0.9f);
                if (AstronomicalShowdown.boomClip == null) AstronomicalShowdown.MakeSfx(); if (sfx != null) sfx.PlayOneShot(AstronomicalShowdown.boomClip, 0.5f * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f)); }
            if (roarT != lastRoarDone && tNow - roarT > 0.35f) { lastRoarDone = roarT; Vector3 at = root.transform.TransformPoint(new Vector3(0f, 0f, 6f)); Ring(at); AstronomicalHazards.Blast(at, 55f * WorldScale * 2f, 16f, 15f); Shake(1f); Hunger = Mathf.Min(100f, Hunger + 2f);
                if (AstronomicalShowdown.roarClip == null) AstronomicalShowdown.MakeSfx(); if (sfx != null && AstronomicalShowdown.roarClip != null) { sfx.pitch = Mathf.Clamp(0.9f / Mathf.Max(0.5f, WorldScale * 1.6f), 0.55f, 1.4f); sfx.PlayOneShot(AstronomicalShowdown.roarClip, 0.9f * Mathf.Clamp(AstronomicalSettings.Volume.Value, 0.2f, 1.5f)); } }
        }
        static void Shake(float k) { var hd = Head(); if (!hd.HasValue) return; float d = Vector3.Distance(hd.Value, p), reach = 70f * WorldScale * 2f; if (d > reach) return; float amp = Mathf.Clamp01((1f - d / reach) * k); try { var gt = GorillaTagger.Instance; if (gt != null) { gt.StartVibration(true, amp, 0.4f); gt.StartVibration(false, amp, 0.4f); } } catch { } }

        public static void Tick()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f); tNow += dt;
            if (pendingSpawn && model != null && !Alive) { try { Build(); } catch (Exception e) { AstronomicalEventMod.Log.LogError("Klombo build: " + e); pendingSpawn = false; Despawn(); } }
            if (!Alive || root == null) { if (berries.Count > 0) BerryStep(dt); return; }
            simAcc += dt; while (simAcc >= STEP) { simAcc -= STEP; pPrev = p; Step(STEP, false); }
            happyDecay += dt; if (happyDecay > 1f) { happyDecay = 0f; Hunger = Mathf.Min(100f, Hunger + 0.35f); Happy = Mathf.Clamp(Happy - (Hunger > 70f ? 0.8f : 0.1f) + (Mood == 1 ? 0.1f : 0f), 0f, 100f); }
            try { SetPose(dt); ImpactStep(); BerryStep(dt); PlateStep(); if (Riding) Carry(); } catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Klombo tick: " + e.Message); }
        }
    }
}
