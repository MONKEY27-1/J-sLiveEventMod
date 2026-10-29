using UnityEngine;

namespace JClient
{
    // Parkour: wall moves, ledges, jumps, dashes, ropes and slopes. Each one only changes your own movement.
    public static class Parkour
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;

        public static void Register()
        {
            const string c = "Parkour";

            float runUntil = 0f,
                runSpeed = 0f,
                runStarted = 0f;
            bool runSpent = false;
            Vector3 runN = Vector3.zero;
            Mods.T(
                    c,
                    "Wall Run",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Game.P.BodyOnGround)
                            runSpent = false;
                        var h = MoveKit.HVel;
                        if (!runSpent && !Game.P.BodyOnGround && MoveKit.WallTouch(out var n, out _) && h.magnitude > 3f * s)
                        {
                            if (Time.time > runUntil + 0.3f)
                            {
                                runSpeed = h.magnitude; // a fresh run keeps the speed you came in with
                                runStarted = Time.time;
                            }
                            runN = n;
                            runUntil = Time.time + 0.25f; // short grace while your hand skips along the wall
                        }
                        if (Time.time > runUntil)
                            return;
                        if (Time.time - runStarted > 2.5f)
                        {
                            runSpent = true; // out of breath until you land
                            runUntil = 0f;
                            return;
                        }
                        var along = Vector3.ProjectOnPlane(h, runN);
                        if (along.sqrMagnitude < 0.01f)
                            return;
                        MoveKit.SetH(along.normalized * Mathf.Max(along.magnitude, runSpeed));
                        Movement.AntiGrav(0.85f);
                        if (Body.linearVelocity.y < -1f * s)
                            MoveKit.SetY(-1f * s);
                    }
                )
                .Info("Touch a wall with a hand while moving fast to run along it, keeping your speed and barely falling (up to 2.5 s)");

            float upUntil = 0f,
                upSpeed = 0f;
            bool upUsed = false;
            Mods.T(
                    c,
                    "Wall Run Up",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Game.P.BodyOnGround)
                            upUsed = false;
                        if (!upUsed && MoveKit.WallTouch(out var n, out _))
                        {
                            float into = Vector3.Dot(MoveKit.HVel, -n);
                            if (into > 3f * s)
                            {
                                upUsed = true;
                                upSpeed = Mathf.Min(into * 1.2f, 10f * s);
                                upUntil = Time.time + 0.6f;
                                MoveKit.BuzzBoth(0.25f, 0.08f);
                            }
                        }
                        if (Time.time >= upUntil)
                            return;
                        MoveKit.SetH(MoveKit.HVel * 0.2f);
                        MoveKit.SetY(upSpeed);
                    }
                )
                .Info("Sprint straight into a wall and touch it: your speed turns upward and you run up the wall (once per landing)");

            float wallSeen = -9f;
            Vector3 jumpN = Vector3.zero;
            Mods.T(
                    c,
                    "Wall Jump",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (MoveKit.WallTouch(out var n, out _))
                        {
                            wallSeen = Time.time;
                            jumpN = n;
                        }
                        if (!Inp.RA.Down || Time.time - wallSeen > 0.3f || Game.P.BodyOnGround)
                            return;
                        Body.linearVelocity = (jumpN * 7f + Vector3.up * 7.5f) * s;
                        wallSeen = -9f;
                        MoveKit.BuzzBoth(0.4f, 0.08f);
                    }
                )
                .Info("Touch a wall in the air and press right A to kick off it, up and away");

            Mods.T(
                    c,
                    "Wall Slide",
                    () =>
                    {
                        if (Game.P.BodyOnGround || !MoveKit.WallTouch(out _, out _))
                            return;
                        float cap = -2f * MoveKit.S;
                        if (Body.linearVelocity.y < cap)
                            MoveKit.SetY(cap);
                    }
                )
                .Info("Keep a hand on a wall while falling to slide down it slowly instead of dropping");

            Vector3? ledge = null;
            Vector3 ledgeIn = Vector3.zero;
            bool ledgeLeft = false;
            Mods.T(
                    c,
                    "Ledge Grab",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (ledge == null)
                        {
                            foreach (
                                var (state, hand, grip, isLeft) in new[]
                                {
                                    (Game.P.LeftHand, Game.LHand, Inp.LGrip, true),
                                    (Game.P.RightHand, Game.RHand, Inp.RGrip, false),
                                }
                            )
                            {
                                if (!grip.Held || !MoveKit.Touching(state))
                                    continue;
                                var n = state.lastHitInfo.normal;
                                if (Mathf.Abs(n.y) > 0.4f)
                                    continue; // only walls have ledges
                                var inward = -MoveKit.FlatDir(n);
                                if (
                                    MoveKit.Cast(
                                        hand.position + Vector3.up * 0.45f * s + inward * 0.15f * s,
                                        Vector3.down,
                                        0.55f * s,
                                        out var top
                                    )
                                    && top.normal.y > 0.7f
                                )
                                {
                                    ledge = top.point;
                                    ledgeIn = inward;
                                    ledgeLeft = isLeft;
                                    MoveKit.Buzz(isLeft, 0.3f, 0.08f);
                                    break;
                                }
                            }
                            if (ledge == null)
                                return;
                        }
                        if (!(ledgeLeft ? Inp.LGrip.Held : Inp.RGrip.Held))
                        {
                            ledge = null; // let go: drop
                            return;
                        }
                        MoveKit.Stop(); // hang
                        if (Inp.RA.Down || Inp.LX.Down)
                        {
                            Game.Teleport(MoveKit.StandAt(ledge.Value + ledgeIn * 0.45f * s));
                            ledge = null;
                            MoveKit.BuzzBoth(0.35f, 0.1f);
                        }
                    },
                    null,
                    () => ledge = null
                )
                .Info("Grip the top edge of a wall to hang from it; press A or X to pull yourself up onto it");

            float mantleCool = 0f;
            Mods.T(
                    c,
                    "Auto Mantle",
                    () =>
                    {
                        float s = MoveKit.S;
                        if ((mantleCool -= MoveKit.Dt) > 0f)
                            return;
                        var h = MoveKit.HVel;
                        if (h.magnitude < 1.2f * s)
                            return;
                        var dir = h.normalized;
                        var bp = MoveKit.BodyPos;
                        float feet = MoveKit.FeetY;
                        if (!MoveKit.Cast(new Vector3(bp.x, feet + 0.2f * s, bp.z), dir, 0.8f * s, out var wall) || wall.normal.y > 0.5f)
                            return; // nothing in front of your knees
                        if (MoveKit.Cast(Head.position + Vector3.up * 0.2f * s, dir, 1.1f * s, out _))
                            return; // blocked at head height too: that's a wall, not a ledge
                        if (
                            !MoveKit.Cast(wall.point + dir * 0.3f * s + Vector3.up * 1.8f * s, Vector3.down, 1.8f * s, out var top)
                            || top.normal.y < 0.7f
                        )
                            return;
                        float rise = top.point.y - feet;
                        if (rise < 0.15f * s || rise > 1.5f * s)
                            return;
                        MoveKit.SetY(Mathf.Sqrt(2f * Physics.gravity.magnitude * (rise + 0.25f * s)));
                        MoveKit.SetH(dir * Mathf.Max(h.magnitude, 2.5f * s));
                        mantleCool = 0.5f;
                    }
                )
                .Info("Run at a ledge, crate or step up to 1.5 m high and you hop up onto it automatically");

            bool pounding = false;
            Mods.T(
                    c,
                    "Ground Pound",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (!pounding && MoveKit.Airborne && Inp.RB.Down)
                            pounding = true;
                        if (!pounding)
                            return;
                        if (MoveKit.Grounded)
                        {
                            pounding = false;
                            Body.linearVelocity = Vector3.up * 5f * s; // a little bounce on impact
                            MoveKit.BuzzBoth(0.8f, 0.15f);
                            return;
                        }
                        Body.linearVelocity = Vector3.down * 25f * s;
                    },
                    null,
                    () => pounding = false
                )
                .Info("Press right B in the air to slam straight down; you bounce back up a little when you hit the ground");

            bool sliding = false;
            float slideAir = 0f;
            Mods.T(
                    c,
                    "Slide",
                    () =>
                    {
                        float s = MoveKit.S,
                            ratio = MoveKit.CrouchRatio();
                        var h = MoveKit.HVel;
                        if (!sliding)
                        {
                            if (Game.P.BodyOnGround && ratio < 0.7f && h.magnitude > 2.5f * s)
                            {
                                sliding = true;
                                slideAir = 0f;
                                MoveKit.SetH(h * 1.25f); // the kick into the slide
                                MoveKit.BuzzBoth(0.2f, 0.1f);
                            }
                            return;
                        }
                        slideAir = Game.P.BodyOnGround ? 0f : slideAir + MoveKit.Dt;
                        if (ratio > 0.82f || h.magnitude < 1f * s || slideAir > 0.3f)
                        {
                            sliding = false;
                            return;
                        }
                        MoveKit.SetH(h * (1f - 0.35f * MoveKit.Dt)); // a long, slowly fading slide
                    },
                    null,
                    () => sliding = false
                )
                .OnLate(() =>
                {
                    if (sliding)
                        Game.P.SetMaximumSlipThisFrame();
                })
                .Info("Crouch down low while running to slide along the ground with a burst of speed; stand up to stop");

            bool bhGround = false;
            float bhLanded = -9f,
                bhAirSpeed = 0f;
            Mods.T(
                    c,
                    "Bunny Hop",
                    () =>
                    {
                        float s = MoveKit.S;
                        bool g = MoveKit.Grounded;
                        var h = MoveKit.HVel;
                        if (g && !bhGround)
                            bhLanded = Time.time;
                        if (!g && bhGround && Time.time - bhLanded < 0.25f && bhAirSpeed > 2f * s)
                        {
                            var dir = h.sqrMagnitude > 0.25f ? h.normalized : MoveKit.Fwd;
                            MoveKit.SetH(dir * Mathf.Min(Mathf.Max(h.magnitude, bhAirSpeed) * 1.06f, 18f * s));
                        }
                        if (!g)
                            bhAirSpeed = h.magnitude;
                        bhGround = g;
                    }
                )
                .Info("Jump again right as you land (within a quarter second) to keep your speed and gain a little each hop");

            float charge = 0f,
                chargeBuzz = 0f;
            Mod chargeJump = null;
            chargeJump = Mods.T(
                    c,
                    "Charge Jump",
                    () =>
                    {
                        if (Inp.RA.Held && MoveKit.Grounded)
                        {
                            charge = Mathf.Min(1.5f, charge + MoveKit.Dt);
                            if ((chargeBuzz -= MoveKit.Dt) <= 0f)
                            {
                                chargeBuzz = 0.25f - charge * 0.1f; // the buzz speeds up as it charges
                                MoveKit.BuzzBoth(0.15f + charge * 0.3f, 0.04f);
                            }
                            return;
                        }
                        if (Inp.RA.Up && charge > 0.15f)
                            MoveKit.SetY((4f + 10f * charge / 1.5f) * MoveKit.S);
                        charge = 0f;
                    },
                    () => charge = 0f
                )
                .Live(() => "Charge Jump" + (chargeJump.On && charge > 0f ? " " + Mathf.RoundToInt(charge / 1.5f * 100f) + "%" : ""))
                .Info("Hold right A on the ground to charge (the buzz speeds up), let go to jump up to 3x higher");

            bool tjGround = false;
            float tjLanded = -9f;
            int combo = 0;
            Mods.T(
                    c,
                    "Triple Jump",
                    () =>
                    {
                        float s = MoveKit.S;
                        bool g = MoveKit.Grounded;
                        var v = Body.linearVelocity;
                        if (g && !tjGround)
                            tjLanded = Time.time;
                        if (!g && tjGround && v.y > 1.5f * s) // a jump just started
                        {
                            combo = Time.time - tjLanded < 0.4f ? combo + 1 : 1;
                            if (combo == 2)
                            {
                                MoveKit.SetY(v.y * 1.35f);
                                MoveKit.BuzzBoth(0.25f, 0.06f);
                            }
                            else if (combo >= 3)
                            {
                                MoveKit.SetY(v.y * 1.8f);
                                MoveKit.SetH(MoveKit.HVel * 1.2f);
                                MoveKit.BuzzBoth(0.5f, 0.12f);
                                combo = 0;
                            }
                        }
                        tjGround = g;
                    }
                )
                .Info("Chain three quick jumps: the second goes higher and the third is a big leap, like a platformer");

            float squatLow = 1f,
                squatCool = 0f;
            Mods.T(
                    c,
                    "Squat Jump",
                    () =>
                    {
                        float ratio = MoveKit.CrouchRatio();
                        squatLow = Mathf.Min(ratio, Mathf.MoveTowards(squatLow, 1f, 1.2f * MoveKit.Dt)); // how low you went a moment ago
                        if ((squatCool -= MoveKit.Dt) > 0f || !MoveKit.Grounded)
                            return;
                        if (squatLow < 0.72f && MoveKit.HeadVel.y > 1.2f * MoveKit.S)
                        {
                            MoveKit.SetY(9f * MoveKit.S);
                            squatCool = 0.6f;
                            squatLow = 1f;
                            MoveKit.BuzzBoth(0.4f, 0.1f);
                        }
                    }
                )
                .Info("Squat down for real, then spring up quickly to jump high");

            float lastGap = 1f,
                clapCool = 0f;
            Mods.T(
                    c,
                    "Clap Jump",
                    () =>
                    {
                        float s = MoveKit.S;
                        float gap = Vector3.Distance(Game.LHand.position, Game.RHand.position);
                        float closing = (lastGap - gap) / MoveKit.Dt;
                        lastGap = gap;
                        if ((clapCool -= MoveKit.Dt) > 0f || gap > 0.14f * s || closing < 1.5f * s)
                            return;
                        MoveKit.SetY(Mathf.Max(Body.linearVelocity.y, 7f * s));
                        clapCool = 0.35f;
                        MoveKit.BuzzBoth(0.45f, 0.06f);
                    },
                    () => lastGap = 1f
                )
                .Info("Clap your hands to jump, even in mid-air");

            int dashes = 2;
            float dashT = 0f;
            Vector3 dashV = Vector3.zero;
            Mod celeste = null;
            celeste = Mods.T(
                    c,
                    "Celeste Dash",
                    () =>
                    {
                        if (dashT > 0f)
                        {
                            dashT -= MoveKit.Dt;
                            Body.linearVelocity = dashT > 0f ? dashV : dashV * 0.45f;
                            Movement.AntiGrav(1f);
                            return;
                        }
                        if (MoveKit.Grounded)
                            dashes = 2;
                        if (dashes <= 0 || !Inp.RB.Down)
                            return;
                        var dir =
                            Inp.LStick.sqrMagnitude > 0.1f
                                ? MoveKit.FromStick(Inp.LStick) + Vector3.up * Mathf.Max(0f, Inp.RStick.y)
                                : Head.forward;
                        dashV = dir.normalized * 16f * MoveKit.S;
                        dashT = 0.15f;
                        dashes--;
                        MoveKit.BuzzBoth(0.4f, 0.06f);
                    },
                    () =>
                    {
                        dashes = 2;
                        dashT = 0f;
                    }
                )
                .Live(() => "Celeste Dash" + (celeste.On ? " (" + dashes + ")" : ""))
                .Info(
                    "Right B dashes in the left-stick direction (or where you look; push right stick up to aim upward). Two dashes, landing refills"
                );

            Mods.T(
                    c,
                    "Air Strafe",
                    () =>
                    {
                        if (!MoveKit.Airborne || Inp.LStick.sqrMagnitude < 0.04f)
                            return;
                        float s = MoveKit.S;
                        var h = MoveKit.HVel;
                        var n = h + MoveKit.FromStick(Inp.LStick) * 10f * s * MoveKit.Dt;
                        MoveKit.SetH(Vector3.ClampMagnitude(n, Mathf.Max(h.magnitude, 6f * s))); // steer, but never speed up past 6 m/s
                    }
                )
                .Info("Steer in mid-air with the left stick without gaining extra speed");

            float apex = 0f;
            Mods.T(
                    c,
                    "Hang Time",
                    () =>
                    {
                        if (MoveKit.Grounded)
                        {
                            apex = 0f;
                            return;
                        }
                        if (Mathf.Abs(Body.linearVelocity.y) < 2.5f * MoveKit.S && apex < 0.7f)
                        {
                            apex += MoveKit.Dt;
                            Movement.AntiGrav(0.7f);
                        }
                    }
                )
                .Info("Floaty peaks: you hang at the top of every jump for a moment");

            Mods.T(
                    c,
                    "Fast Fall",
                    () =>
                    {
                        if (MoveKit.Airborne && Body.linearVelocity.y < -0.5f)
                            Movement.AntiGrav(-1f);
                    }
                )
                .Info("Double gravity on the way down only: snappier jumps and quicker landings");

            bool ppL = false,
                ppR = false;
            float ppLUp = -9f,
                ppRUp = -9f,
                ppBoth = -9f,
                ppCool = 0f;
            Mods.T(
                    c,
                    "Power Push",
                    () =>
                    {
                        bool l = MoveKit.Touching(Game.P.LeftHand),
                            r = MoveKit.Touching(Game.P.RightHand);
                        if (l && r)
                            ppBoth = Time.time;
                        if (ppL && !l)
                            ppLUp = Time.time;
                        if (ppR && !r)
                            ppRUp = Time.time;
                        ppL = l;
                        ppR = r;
                        if ((ppCool -= MoveKit.Dt) > 0f || l || r)
                            return;
                        bool together = Mathf.Abs(ppLUp - ppRUp) < 0.08f, // both hands left the ground at the same moment
                            justNow = Time.time - Mathf.Max(ppLUp, ppRUp) < 0.02f + MoveKit.Dt;
                        if (!together || !justNow || Time.time - ppBoth > 0.15f)
                            return;
                        Body.linearVelocity = Vector3.ClampMagnitude(Body.linearVelocity * 1.35f, 20f * MoveKit.S);
                        ppCool = 0.5f;
                        MoveKit.BuzzBoth(0.35f, 0.06f);
                    }
                )
                .Info("Push off with both hands at exactly the same time for a 35% stronger launch");

            Mods.T(
                    c,
                    "Climb Boost",
                    () =>
                    {
                        float s = MoveKit.S;
                        var v = Body.linearVelocity;
                        if (v.y > 0.5f * s && MoveKit.WallTouch(out _, out _))
                            MoveKit.SetY(Mathf.Min(v.y * (1f + 1.5f * MoveKit.Dt), 10f * s));
                    }
                )
                .Info("Pulling yourself up walls gives extra upward speed: climb trees and towers much faster");

            var probes = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            Mods.T(
                    c,
                    "Surface Magnet",
                    () =>
                    {
                        float s = MoveKit.S,
                            reach = 0.3f * s;
                        foreach (var (state, hand) in new[] { (Game.P.LeftHand, Game.LHand), (Game.P.RightHand, Game.RHand) })
                        {
                            if (MoveKit.Touching(state))
                                continue;
                            float best = reach;
                            var to = Vector3.zero;
                            foreach (var d in probes)
                                if (MoveKit.Cast(hand.position, d, reach, out var hit) && hit.distance < best)
                                {
                                    best = hit.distance;
                                    to = d;
                                }
                            if (to != Vector3.zero)
                                Body.linearVelocity += to * 4f * s * MoveKit.Dt;
                        }
                    }
                )
                .Info("Your hands are drawn to any surface within 30 cm, so grabbing walls and branches is easier");

            float edgeBuzz = 0f;
            Mods.T(
                    c,
                    "Edge Guard",
                    () =>
                    {
                        float s = MoveKit.S;
                        edgeBuzz -= MoveKit.Dt;
                        if (!Game.P.BodyOnGround || Inp.RA.Held)
                            return;
                        var h = MoveKit.HVel;
                        if (
                            h.magnitude < 0.3f * s
                            || MoveKit.Cast(MoveKit.BodyPos + h.normalized * 0.45f * s, Vector3.down, 2.5f * s, out _)
                        )
                            return; // standing still, or there's ground ahead
                        MoveKit.SetH(Vector3.zero);
                        if (edgeBuzz <= 0f)
                        {
                            edgeBuzz = 0.5f;
                            MoveKit.BuzzBoth(0.2f, 0.05f);
                        }
                    }
                )
                .Info("Stops you walking off drops higher than 2.5 m, like sneaking at an edge. Hold right A to go over anyway");

            Vector3? rope = null;
            float ropeLen = 0f;
            LineRenderer ropeLine = null;
            Mods.T(
                    c,
                    "Rope Swing",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            rope = p;
                            ropeLen = Mathf.Clamp(Vector3.Distance(Head.position, p), 1.5f * s, 40f * s);
                        }
                        if (!Gun.Firing)
                            rope = null;
                        if (rope == null)
                        {
                            if (ropeLine != null)
                                ropeLine.gameObject.SetActive(false);
                            return;
                        }
                        var d = Head.position - rope.Value;
                        float dist = d.magnitude;
                        if (dist > ropeLen)
                        {
                            var n = d / dist;
                            float outward = Vector3.Dot(Body.linearVelocity, n);
                            if (outward > 0f)
                                Body.linearVelocity -= n * outward; // a rope can't stretch: only the swing is left
                            Body.linearVelocity -= n * (dist - ropeLen) * 5f;
                        }
                        if (ropeLine == null)
                            ropeLine = Fx.Line(new Color(0.85f, 0.7f, 0.45f), 0.015f, false);
                        ropeLine.gameObject.SetActive(true);
                        ropeLine.SetPosition(0, Game.RHand.position);
                        ropeLine.SetPosition(1, rope.Value);
                    },
                    null,
                    () =>
                    {
                        rope = null;
                        Fx.Kill(ropeLine);
                        ropeLine = null;
                    }
                )
                .Info(
                    "Fire the gun to tie a rope of fixed length to that point and swing like a pendulum while you hold fire; let go to fly off"
                );

            Vector3? zipA = null,
                zipB = null;
            LineRenderer zipLine = null;
            bool riding = false;
            float zipSpeed = 0f;
            Mods.T(
                    c,
                    "Zipline",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            if (zipA == null || zipB != null)
                            {
                                zipA = p + Vector3.up * 1.2f * s;
                                zipB = null;
                                riding = false;
                                Hud.Toast("Zipline start set - fire again for the end");
                            }
                            else
                            {
                                zipB = p + Vector3.up * 1.2f * s;
                                Hud.Toast("Zipline ready: hold a grip near the line to ride it");
                            }
                        }
                        if (zipA == null || zipB == null)
                        {
                            if (zipLine != null)
                                zipLine.gameObject.SetActive(false);
                            return;
                        }
                        Vector3 a = zipA.Value,
                            b = zipB.Value;
                        if (zipLine == null)
                            zipLine = Fx.Line(new Color(0.8f, 0.8f, 0.85f), 0.02f, false);
                        zipLine.gameObject.SetActive(true);
                        zipLine.SetPosition(0, a);
                        zipLine.SetPosition(1, b);
                        Transform hand =
                            Inp.LGrip.Held ? Game.LHand
                            : Inp.RGrip.Held && !Gun.Aiming ? Game.RHand
                            : null;
                        if (hand == null)
                        {
                            riding = false;
                            return;
                        }
                        Vector3 low = b.y <= a.y ? b : a,
                            down = (low - (low == b ? a : b)).normalized; // you always ride toward the lower end
                        float t = Mathf.Clamp01(Vector3.Dot(hand.position - a, b - a) / Mathf.Max(0.01f, (b - a).sqrMagnitude));
                        var onLine = a + (b - a) * t;
                        if (!riding)
                        {
                            if ((onLine - hand.position).magnitude > 0.4f * s)
                                return;
                            riding = true;
                            zipSpeed = Mathf.Max(3f * s, Vector3.Dot(Body.linearVelocity, down));
                        }
                        if ((low - onLine).magnitude < 0.5f * s)
                        {
                            riding = false; // fly off the end with your speed
                            return;
                        }
                        zipSpeed = Mathf.Min(zipSpeed + Physics.gravity.magnitude * Mathf.Max(0.1f, -down.y) * MoveKit.Dt, 25f * s);
                        Body.linearVelocity = down * zipSpeed + (onLine - hand.position) * 8f; // keep your hand on the line
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () =>
                    {
                        zipA = zipB = null;
                        riding = false;
                        Fx.Kill(zipLine);
                        zipLine = null;
                    }
                )
                .Info("Fire the gun twice to string a zipline between two spots, then hold a grip near the line to slide down it");

            Mods.T(
                    c,
                    "Arm Swinger",
                    () =>
                    {
                        float s = MoveKit.S;
                        Vector3 l = MoveKit.LeftHandVel,
                            r = MoveKit.RightHandVel;
                        if (l.y * r.y >= 0f || Mathf.Abs(l.y) < 0.7f * s || Mathf.Abs(r.y) < 0.7f * s)
                            return; // hands must swing opposite ways, like jogging
                        float pace = (Mathf.Abs(l.y) + Mathf.Abs(r.y)) * 0.5f / s;
                        var want = MoveKit.Fwd * Mathf.Clamp(pace * 2.5f, 0f, 7f) * s;
                        MoveKit.SetH(Vector3.Lerp(MoveKit.HVel, want, 1f - Mathf.Exp(-4f * MoveKit.Dt)));
                    }
                )
                .Info("Swing your arms like you're jogging (no need to touch the ground) to run where you look");

            Mods.T(
                    c,
                    "Ski Mode",
                    () =>
                    {
                        if (
                            !MoveKit.Grounded
                            || !MoveKit.Cast(MoveKit.BodyPos, Vector3.down, 1.5f * MoveKit.S, out var g)
                            || g.normal.y > 0.985f
                        )
                            return; // not on a slope
                        Body.linearVelocity += MoveKit.Flat(Vector3.ProjectOnPlane(Physics.gravity, g.normal)) * 0.8f * MoveKit.Dt;
                        var h = MoveKit.HVel;
                        if (h.sqrMagnitude > 0.25f)
                            MoveKit.SetH(Vector3.RotateTowards(h, MoveKit.Fwd * h.magnitude, 1.2f * MoveKit.Dt, 0f)); // carve toward where you look
                    }
                )
                .OnLate(() =>
                {
                    if (MoveKit.Grounded)
                        Game.P.SetMaximumSlipThisFrame();
                })
                .Info("Slopes become ski runs: you speed up downhill with no friction and carve by looking where you want to go");

            float punchCool = 0f;
            Mods.T(
                    c,
                    "Punch Dash",
                    () =>
                    {
                        float s = MoveKit.S;
                        if ((punchCool -= MoveKit.Dt) > 0f)
                            return;
                        foreach (
                            var (vel, fist, left) in new[]
                            {
                                (MoveKit.LeftHandVel, Inp.LGrip.Held, true),
                                (MoveKit.RightHandVel, Inp.RGrip.Held && !Gun.Aiming, false),
                            }
                        )
                        {
                            if (!fist || vel.magnitude < 4.5f * s || Vector3.Dot(vel.normalized, Head.forward) < 0.5f)
                                continue;
                            Body.linearVelocity = vel.normalized * 12f * s;
                            punchCool = 0.6f;
                            MoveKit.Buzz(left, 0.5f, 0.08f);
                            break;
                        }
                    }
                )
                .Info("Make a fist (grip) and punch hard forward to dash in that direction");

            float bob = 0f;
            Mods.T(
                    c,
                    "Walk In Place",
                    () =>
                    {
                        float s = MoveKit.S,
                            vy = MoveKit.HeadVel.y / s;
                        bob = Mathf.Lerp(bob, vy * vy, 1f - Mathf.Exp(-3f * MoveKit.Dt)); // how much your head bobs up and down
                        float pace = Mathf.Sqrt(bob);
                        if (!MoveKit.Grounded || pace < 0.12f)
                            return;
                        var want = MoveKit.Fwd * Mathf.Clamp((pace - 0.12f) * 18f, 0f, 4.5f) * s;
                        MoveKit.SetH(Vector3.Lerp(MoveKit.HVel, want, 1f - Mathf.Exp(-3f * MoveKit.Dt)));
                    },
                    () => bob = 0f
                )
                .Info("March on the spot (your head bobbing) to walk forward where you look - no arm movement needed");

            Mods.T(
                    c,
                    "Surf",
                    () =>
                    {
                        if (!MoveKit.SurfaceTouch(out var n) || n.y < 0.15f || n.y > 0.7f)
                            return; // steep ramps only
                        Movement.AntiGrav(1f);
                        Body.linearVelocity += Vector3.ProjectOnPlane(Physics.gravity, n) * 0.6f * MoveKit.Dt; // slide along the ramp, not into it
                        float into = Vector3.Dot(Body.linearVelocity, -n);
                        if (into > 0f)
                            Body.linearVelocity += n * into;
                    }
                )
                .OnLate(() =>
                {
                    if (MoveKit.SurfaceTouch(out var n) && n.y >= 0.15f && n.y <= 0.7f)
                        Game.P.SetMaximumSlipThisFrame();
                })
                .Info("Steep ramps become surf ramps: you glide along them without losing speed instead of sliding off");

            Mods.T(
                    c,
                    "Phase Through",
                    () =>
                    {
                        if (!Inp.RA.Down || !Inp.LGrip.Held || !Inp.RGrip.Held)
                            return;
                        float s = MoveKit.S;
                        if (!MoveKit.WallTouch(out var n, out _))
                        {
                            Hud.Toast("Put your hands on a wall first");
                            return;
                        }
                        var inward = -MoveKit.FlatDir(n);
                        var far = Head.position + inward * 6f * s;
                        if (!MoveKit.Cast(far, -inward, 6f * s, out var exit)) // looking back from the far side finds the wall's back face
                        {
                            Hud.Toast("Can't find the other side (walls up to 6 m)");
                            return;
                        }
                        var spot = exit.point + inward * 0.5f * s;
                        if (MoveKit.GroundBelow(spot + Vector3.up * 0.5f * s, 4f * s, out var floor))
                            spot = MoveKit.StandAt(floor);
                        Game.Teleport(spot);
                        MoveKit.BuzzBoth(0.5f, 0.12f);
                    }
                )
                .Info("Hold both grips with your hands on a wall and press right A to pass through to the other side");
        }
    }
}
