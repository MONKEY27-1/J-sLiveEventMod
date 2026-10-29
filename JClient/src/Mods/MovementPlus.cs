using System.Collections.Generic;
using System.Linq;
using GorillaLocomotion.Swimming;
using UnityEngine;

namespace JClient
{
    // Second batch of Movement modules.
    public static class MovementPlus
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;

        static bool Touching(GorillaLocomotion.GTPlayer.HandState h) => h.isColliding || h.wasColliding;

        public static void Register()
        {
            const string c = "Movement";

            float lastWall = 0f; Vector3 wallN = Vector3.up;
            Mods.T(c, "Gentle Wall Walk", () =>
            {
                var l = Game.P.LeftHand; var r = Game.P.RightHand;
                if (Inp.LGrip.Held && Touching(l)) { wallN = l.lastHitInfo.normal; lastWall = Time.time; }
                else if (Inp.RGrip.Held && Touching(r)) { wallN = r.lastHitInfo.normal; lastWall = Time.time; }
                if (Time.time - lastWall < 0.25f) { Movement.AntiGrav(0.6f); Body.linearVelocity -= wallN * 4f * Time.deltaTime; }
            }).Info("Hold grip while touching a wall to be pulled gently toward it");

            Mods.T(c, "Airstrike", () =>
            {
                if (!Gun.Get(out var p, out _) || !Gun.FireDown) return;
                Game.Teleport(p + Vector3.up * 30f * Game.Scale);
                Body.linearVelocity = Vector3.down * 40f * Game.Scale;
            }).Info("Fire the gun: teleport high above that spot and slam back down");

            GameObject pearl = null; Vector3 pearlVel = Vector3.zero; bool flying = false;
            Mods.T(c, "Ender Pearl", () =>
            {
                var hand = Game.RHand;
                if (pearl == null) pearl = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.08f, new Color(0.1f, 0.6f, 0.5f));
                if (!flying)
                {
                    pearl.SetActive(Inp.RGrip.Held);
                    if (Inp.RGrip.Held) pearl.transform.position = hand.position;
                    if (Inp.RGrip.Up) { flying = true; pearl.SetActive(true); pearlVel = ControllerInputPoller.instance.rightVelocity * 2f + Head.forward * 4f; }
                    return;
                }
                pearlVel += Physics.gravity * Time.deltaTime;
                var step = pearlVel * Time.deltaTime;
                if (Physics.Raycast(pearl.transform.position, step.normalized, out var hit, step.magnitude + 0.05f, GorillaLocomotion.GTPlayer.LocomotionEnabledLayers, QueryTriggerInteraction.Ignore))
                { Game.Teleport(hit.point + hit.normal * 0.6f * Game.Scale); flying = false; pearl.SetActive(false); return; }
                pearl.transform.position += step;
                if (pearl.transform.position.y < Head.position.y - 200f) { flying = false; pearl.SetActive(false); }
            }, null, () => { Fx.Kill(pearl); pearl = null; flying = false; }).Info("Hold right grip to hold a pearl, release to throw it, and teleport where it lands");

            var lastHands = new Dictionary<VRRig, (Vector3 l, Vector3 r)>();
            Mods.T(c, "Punch Mod", () =>
            {
                foreach (var rig in Game.Others)
                {
                    var now = (rig.leftHandTransform.position, rig.rightHandTransform.position);
                    if (lastHands.TryGetValue(rig, out var was))
                        foreach (var (pos, prev) in new[] { (now.Item1, was.l), (now.Item2, was.r) })
                        {
                            var v = (pos - prev) / Mathf.Max(Time.deltaTime, 0.001f);
                            if (v.magnitude > 4f && Vector3.Distance(pos, Head.position) < 0.35f * Game.Scale) Body.linearVelocity = v.normalized * 18f * Game.Scale;
                        }
                    lastHands[rig] = (now.Item1, now.Item2);
                }
            }, null, () => lastHands.Clear()).Info("Other players can punch you across the map by swinging at your head");

            Mods.T(c, "Safety Bubble", () =>
            {
                foreach (var rig in Game.Others)
                {
                    var away = Head.position - rig.transform.position; away.y = 0;
                    if (away.magnitude < 1.2f * Game.Scale) Body.linearVelocity += away.normalized * 12f * Time.deltaTime * Game.Scale;
                }
            }).Info("Gently pushes you away from anyone closer than 1.2 m");

            var solid = new RigPool(r =>
            {
                var g = Fx.Prim(PrimitiveType.Capsule, new Vector3(0.5f, 0.45f, 0.5f), Color.clear, collider: true);
                g.GetComponent<Renderer>().enabled = false; return g;
            }, (r, g) => { g.transform.position = r.transform.position; g.transform.localScale = new Vector3(0.5f, 0.45f, 0.5f) * r.scaleFactor; });
            Mods.T(c, "Solid Players", solid.Tick, null, solid.Clear).Info("Other players become solid so you can stand and climb on them (local)");

            var lastHand = new Vector3[2];
            Mods.T(c, "Pull Mod", () =>
            {
                var hands = new[] { (Game.P.LeftHand, Game.LHand, Inp.LGrip.Held), (Game.P.RightHand, Game.RHand, Inp.RGrip.Held) };
                for (int i = 0; i < 2; i++)
                {
                    var (state, t, grip) = hands[i];
                    var move = t.position - lastHand[i]; move.y = 0; lastHand[i] = t.position;
                    if (grip && Touching(state)) Body.linearVelocity -= move * 15f; // each frame of push adds a bit of extra speed opposite the hand
                }
            }).Info("Hold grip while running: each push off the ground pulls you a little further");

            bool wasDown = false;
            Mods.T(c, "Long Jump", () =>
            {
                bool down = Touching(Game.P.LeftHand) || Touching(Game.P.RightHand);
                if (wasDown && !down && Inp.RA.Held) { var v = Body.linearVelocity; Body.linearVelocity = new Vector3(v.x * 1.6f, v.y * 1.15f, v.z * 1.6f); }
                wasDown = down;
            }).Info("Hold A: every jump off the ground goes 60% further");

            Mods.T(c, "Sticky Hands", () => { if (Game.P.LeftHand.isColliding || Game.P.RightHand.isColliding) { Body.linearVelocity = Vector3.zero; Movement.AntiGrav(1f); } })
                .Info("Hands stick to whatever they touch");

            Vector3 anchorL = Vector3.zero, anchorR = Vector3.zero; bool holdL = false, holdR = false;
            Mods.T(c, "Climby Hands", () =>
            {
                if (Inp.LGrip.Down && Touching(Game.P.LeftHand)) { holdL = true; anchorL = Game.LHand.position; }
                if (Inp.RGrip.Down && Touching(Game.P.RightHand)) { holdR = true; anchorR = Game.RHand.position; }
                if (!Inp.LGrip.Held) holdL = false; if (!Inp.RGrip.Held) holdR = false;
                if (!holdL && !holdR) return;
                var pull = holdR ? anchorR - Game.RHand.position : anchorL - Game.LHand.position;
                Body.linearVelocity = pull / Mathf.Max(Time.deltaTime, 0.001f);
                Movement.AntiGrav(1f);
            }, null, () => holdL = holdR = false).Info("Grab any surface with grip and climb it like a rope");

            Mods.T(c, "Disable Body Collider", () => Game.T.bodyCollider.enabled = false, null, () => Game.T.bodyCollider.enabled = true).Info("Turns off your body collider");
            Mods.T(c, "Disable Head Collider", () => Game.T.headCollider.enabled = false, null, () => Game.T.headCollider.enabled = true).Info("Turns off your head collider");

            Mod slide = null; float slideBackup = -1f;
            slide = Mods.T(c, "Slide Control", () => { if (slideBackup < 0) slideBackup = Game.P.slideControl; Game.P.slideControl = slide.Opt == 0 ? 1f : 0.02f; },
                null, () => { if (slideBackup >= 0) Game.P.slideControl = slideBackup; slideBackup = -1f; })
                .Opts("Control", "Perfect", "Weak").Info("Steer on ice and slides");

            float armBackup = -1f;
            Mods.T(c, "Uncap Arm Length", () => { if (armBackup < 0) armBackup = Game.P.maxArmLength; Game.P.maxArmLength = 100f; },
                null, () => { if (armBackup >= 0) Game.P.maxArmLength = armBackup; armBackup = -1f; }).Info("Removes the arm distance limit");

            Vector3 bounceVel = Vector3.zero;
            Mods.T(c, "Bouncy", () =>
            {
                foreach (var h in new[] { Game.P.LeftHand, Game.P.RightHand })
                    if (h.isColliding && !h.wasColliding && bounceVel.magnitude > 2f) { Body.linearVelocity = Vector3.Reflect(bounceVel, h.lastHitInfo.normal) * 0.9f; break; }
                bounceVel = Body.linearVelocity;
            }).Info("You bounce off surfaces instead of stopping");

            var waterCols = new List<Collider>();
            Mods.T(c, "Disable Water", null, () =>
            {
                foreach (var w in Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None))
                    foreach (var col in w.volumeColliders) if (col != null && col.enabled) { col.enabled = false; waterCols.Add(col); }
            }, () => { foreach (var col in waterCols) if (col != null) col.enabled = true; waterCols.Clear(); }).Info("Water stops working as water (beach etc.)");
            // Trigger callbacks still fire on disabled components, so switch off the wind volumes' colliders instead.
            var windCols = new List<Collider>(); GameObject dome = null;
            Mods.T(c, "Disable Wind Barriers", null, () =>
            {
                foreach (var f in Object.FindObjectsByType<ForceVolume>(FindObjectsSortMode.None))
                    foreach (var col in f.GetComponents<Collider>()) if (col.enabled) { col.enabled = false; windCols.Add(col); }
                dome = GameObject.Find("Environment Objects/LocalObjects_Prefab/ForestToHoverboard/TurnOnInForestAndHoverboard/ForestDome_CollisionOnly");
                if (dome != null) dome.SetActive(false);
            }, () =>
            {
                foreach (var col in windCols) if (col != null) col.enabled = true; windCols.Clear();
                if (dome != null) dome.SetActive(true); dome = null;
            }).Info("Turns off wind pushes and the Forest dome barrier (re-enable after changing maps to catch new ones)");
            Mods.T(c, "Air Swim", () => Game.P.forcedUnderwater = true, null, () => Game.P.forcedUnderwater = false).Info("Swim through the air as if underwater");
        }
    }
}
