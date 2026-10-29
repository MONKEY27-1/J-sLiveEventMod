using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Flight: different ways of getting airborne and steering. Each one only changes your own movement.
    public static class Flight
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;

        // Both arms held out wide to the sides, roughly at head height
        static bool ArmsSpread()
        {
            var h = Head.position;
            float s = MoveKit.S;
            Vector3 l = Game.LHand.position - h,
                r = Game.RHand.position - h;
            return MoveKit.Flat(l).magnitude > 0.4f * s
                && MoveKit.Flat(r).magnitude > 0.4f * s
                && Mathf.Abs(l.y) < 0.5f * s
                && Mathf.Abs(r.y) < 0.5f * s
                && Vector3.Dot(MoveKit.Flat(l), MoveKit.Flat(r)) < 0f;
        }

        static void Grab(ref Vector3? anchor, Btn grip, Transform hand, bool allowed = true)
        {
            if (grip.Down && allowed)
                anchor = hand.position;
            if (!grip.Held)
                anchor = null;
        }

        public static void Register()
        {
            const string c = "Flight";

            Mods.T(
                    c,
                    "Wingsuit",
                    () =>
                    {
                        if (MoveKit.Grounded || !ArmsSpread())
                            return;
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        var v = Body.linearVelocity;
                        var f = Head.forward;
                        float speed = v.magnitude / s; // in body sizes, so it feels the same at any scale
                        speed += -f.y * Physics.gravity.magnitude * dt; // diving builds speed, pulling up spends it
                        speed = Mathf.Clamp(speed * (1f - 0.04f * dt), 3f, 45f);
                        var want = (f * speed + Vector3.down * 1.2f) * s; // a suit always sinks a little
                        Body.linearVelocity = Vector3.Lerp(v, want, 1f - Mathf.Exp(-3f * dt));
                        Movement.AntiGrav(1f);
                    }
                )
                .Info("Spread your arms while falling: dive to build speed, look up to turn it into height");

            float angL = 0f,
                angR = 0f,
                spin = 0f;
            bool haveAng = false;
            Mods.T(
                    c,
                    "Helicopter Arms",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        var h = Head.position;
                        float AngleOf(Transform t)
                        {
                            var d = t.position - h;
                            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                        }
                        bool Out(Transform t) => MoveKit.Flat(t.position - h).magnitude > 0.25f * s && t.position.y > h.y - 0.25f * s;
                        float al = AngleOf(Game.LHand),
                            ar = AngleOf(Game.RHand);
                        if (haveAng)
                        {
                            float w = 0f;
                            if (Out(Game.LHand))
                                w += Mathf.Abs(Mathf.DeltaAngle(angL, al));
                            if (Out(Game.RHand))
                                w += Mathf.Abs(Mathf.DeltaAngle(angR, ar));
                            spin = Mathf.Lerp(spin, w / dt, 1f - Mathf.Exp(-6f * dt)); // degrees per second
                        }
                        angL = al;
                        angR = ar;
                        haveAng = true;
                        if (spin < 250f) // slower than about 0.7 turns a second: no lift
                            return;
                        float lift = Mathf.Clamp((spin - 250f) / 60f, 0f, 10f) * s;
                        Movement.AntiGrav(1f);
                        MoveKit.SetY(Mathf.Lerp(Body.linearVelocity.y, lift, 1f - Mathf.Exp(-3f * dt)));
                    },
                    () =>
                    {
                        haveAng = false;
                        spin = 0f;
                    }
                )
                .Info("Swing your arms in big circles at shoulder height like rotor blades to lift off");

            Vector3? grabL = null,
                grabR = null;
            Mods.T(
                    c,
                    "Grab The Air",
                    () =>
                    {
                        Grab(ref grabL, Inp.LGrip, Game.LHand);
                        Grab(ref grabR, Inp.RGrip, Game.RHand, !Gun.Aiming);
                        if (grabL == null && grabR == null)
                            return;
                        var pull = Vector3.zero;
                        int n = 0;
                        if (grabL != null)
                        {
                            pull += grabL.Value - Game.LHand.position;
                            n++;
                        }
                        if (grabR != null)
                        {
                            pull += grabR.Value - Game.RHand.position;
                            n++;
                        }
                        // the grabbed hand stays where it grabbed, so your body moves instead
                        Body.linearVelocity = Vector3.ClampMagnitude(pull / n / MoveKit.Dt, 25f * MoveKit.S);
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () => grabL = grabR = null
                )
                .Info("Hold a grip anywhere to grab the air itself and pull yourself along; let go mid-pull to fling");

            Mods.T(
                    c,
                    "Broomstick",
                    () =>
                    {
                        if (!Inp.RGrip.Held || Gun.Aiming)
                            return;
                        var d = Game.RHand.position - Head.position;
                        float speed = Mathf.InverseLerp(0.2f, 0.65f, d.magnitude / MoveKit.S) * 22f * MoveKit.S;
                        Body.linearVelocity = d.normalized * speed;
                        Movement.AntiGrav(1f);
                    }
                )
                .Info("Hold right grip and steer with your hand like a broom: the further you reach, the faster you fly");

            float hopUntil = 0f;
            Mods.T(
                    c,
                    "Hoverboard",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        if (Inp.RB.Down)
                        {
                            MoveKit.SetY(6f * s);
                            hopUntil = Time.time + 0.5f;
                        }
                        bool throttle = Inp.RTrig.Held && !Board.VROpen || MoveKit.Key(Key.T);
                        var h = MoveKit.HVel;
                        if (throttle)
                            h += MoveKit.Fwd * 9f * s * dt;
                        if (h.sqrMagnitude > 0.01f)
                            h = Vector3.RotateTowards(h, MoveKit.Fwd * h.magnitude, (throttle ? 2.5f : 1f) * dt, 0f); // carve toward where you look
                        MoveKit.SetH(Vector3.ClampMagnitude(h * (1f - 0.25f * dt), 16f * s));
                        if (Time.time < hopUntil || !MoveKit.Cast(MoveKit.BodyPos, Vector3.down, 3f * s, out var g))
                            return; // mid-hop, or over a drop: gravity takes over
                        float want = g.point.y + 0.35f * s;
                        MoveKit.SetY(Mathf.Clamp((want - MoveKit.FeetY) * 10f, -8f * s, 8f * s));
                        Movement.AntiGrav(1f);
                    }
                )
                .Info("Ride a hoverboard just above the ground: right trigger (desktop: T) speeds up and you carve where you look. B hops");

            Mods.T(
                    c,
                    "Space Flight",
                    () =>
                    {
                        Movement.AntiGrav(1f);
                        if (Inp.RTrig.Held && !Board.VROpen || MoveKit.Key(Key.T))
                            Body.linearVelocity += Head.forward * 8f * MoveKit.S * MoveKit.Dt;
                        if (Inp.LTrig.Held && !Board.VROpen)
                            Body.linearVelocity *= 1f - Mathf.Min(1f, 2.5f * MoveKit.Dt);
                    }
                )
                .Info("Zero gravity with real momentum: right trigger (desktop: T) thrusts where you look, left trigger brakes");

            Vector3? droneRef = null;
            Mods.T(
                    c,
                    "Drone Mode",
                    () =>
                    {
                        if (!Inp.RGrip.Held || Gun.Aiming)
                        {
                            droneRef = null;
                            return;
                        }
                        var yaw = Quaternion.Euler(0f, Head.eulerAngles.y, 0f);
                        var local = Quaternion.Inverse(yaw) * (Game.RHand.position - Head.position) / MoveKit.S; // hand position in your facing frame
                        if (droneRef == null)
                            droneRef = local;
                        var off = local - droneRef.Value;
                        Body.linearVelocity = Vector3.ClampMagnitude(yaw * off * 30f, 14f) * MoveKit.S; // 10 cm off-centre = 3 m/s
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () => droneRef = null
                )
                .Info(
                    "Hold right grip and your hand becomes a joystick: move it forward, back, sideways, up or down from where you grabbed"
                );

            float hoverH = -1f;
            Mods.T(
                    c,
                    "Terrain Hover",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        float clear = MoveKit.Clearance(40f);
                        if (hoverH < 0f)
                            hoverH = float.IsInfinity(clear) ? 3f * s : clear;
                        hoverH = Mathf.Clamp(hoverH + Inp.RStick.y * 3f * s * dt, 0.8f * s, 30f * s);
                        var h = MoveKit.FromStick(Inp.LStick) * 8f * s;
                        float vy = float.IsInfinity(clear) ? 0f : Mathf.Clamp((hoverH - clear) * 4f, -10f * s, 10f * s);
                        Body.linearVelocity = new Vector3(h.x, vy, h.z);
                        Movement.AntiGrav(1f);
                    },
                    () => hoverH = -1f
                )
                .Info(
                    "Hover a fixed height above whatever is below you, following hills and drops. Left stick moves, right stick sets the height"
                );

            Vector3? cruise = null;
            Mods.T(
                    c,
                    "Cruise Control",
                    () =>
                    {
                        if (Inp.RB.Down || MoveKit.KeyDown(Key.G))
                        {
                            cruise = cruise == null ? Body.linearVelocity : (Vector3?)null;
                            Hud.Toast(
                                cruise != null ? "Cruise locked at " + cruise.Value.magnitude.ToString("0.0") + " m/s" : "Cruise off"
                            );
                        }
                        if (cruise == null)
                            return;
                        Body.linearVelocity = cruise.Value;
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () => cruise = null
                )
                .Info("Right B (desktop: G) locks your current speed and direction until you press it again");

            float air = 0f;
            Mod balloon = null;
            balloon = Mods.T(
                    c,
                    "Balloon Float",
                    () =>
                    {
                        float dt = MoveKit.Dt,
                            s = MoveKit.S;
                        if (Inp.LGrip.Held)
                            air = Mathf.Min(1.4f, air + 0.5f * dt);
                        if (Inp.LTrig.Held && !Board.VROpen)
                            air = Mathf.Max(0f, air - 0.8f * dt);
                        Movement.AntiGrav(air);
                        if (air < 0.5f)
                            return;
                        var v = Body.linearVelocity; // a balloon drifts, it doesn't race
                        Body.linearVelocity = new Vector3(
                            v.x * (1f - 0.5f * dt),
                            Mathf.Clamp(v.y, -4f * s, 4f * s),
                            v.z * (1f - 0.5f * dt)
                        );
                    },
                    () => air = 0f
                )
                .Live(() => "Balloon Float" + (balloon.On ? " (" + Mathf.RoundToInt(air * 100f) + "% air)" : ""))
                .Info(
                    "Hold left grip to fill your balloon and float, left trigger to let air out. Over 100% you rise, under it you sink slowly"
                );

            Mods.T(
                    c,
                    "Levitation Hands",
                    () =>
                    {
                        float s = MoveKit.S,
                            headY = Head.position.y;
                        float l = Game.LHand.position.y - headY,
                            r = Game.RHand.position.y - headY;
                        if (l < -0.05f * s || r < -0.05f * s)
                            return; // only while both hands are up
                        float lift = Mathf.Clamp((l + r) * 0.5f / s, 0f, 0.5f) * 16f * s; // hands 50 cm over your head = 8 m/s up
                        Movement.AntiGrav(1f);
                        MoveKit.SetY(Mathf.Lerp(Body.linearVelocity.y, lift, 1f - Mathf.Exp(-4f * MoveKit.Dt)));
                    }
                )
                .Info("Raise both hands above your head to float up - the higher you reach, the faster. Lower them to come back down");

            Vector3? orbitC = null;
            float orbitR = 6f,
                orbitA = 0f,
                orbitY = 0f;
            GameObject orbitMark = null;
            Mods.T(
                    c,
                    "Orbit Flight",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        var me = Game.P.transform.position;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            orbitC = p;
                            var d = MoveKit.Flat(me - p);
                            orbitR = Mathf.Clamp(d.magnitude, 2f * s, 40f * s);
                            orbitA = Mathf.Atan2(d.z, d.x);
                            orbitY = me.y - p.y;
                            if (orbitMark == null)
                                orbitMark = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.25f * s, Fx.Accent);
                            orbitMark.transform.position = p;
                        }
                        if (orbitC == null || Gun.Aiming)
                            return; // aiming again pauses the orbit
                        orbitA += (6f + Inp.LStick.x * 5f) * s / Mathf.Max(orbitR, 0.5f) * dt; // stick sideways: faster, slower or backwards
                        orbitR = Mathf.Clamp(orbitR - Inp.LStick.y * 3f * s * dt, 1f * s, 60f * s); // stick forward pulls you in
                        orbitY += Inp.RStick.y * 3f * s * dt;
                        var target = orbitC.Value + new Vector3(Mathf.Cos(orbitA) * orbitR, orbitY, Mathf.Sin(orbitA) * orbitR);
                        Body.linearVelocity = Vector3.ClampMagnitude((target - me) * 6f, 30f * s);
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () =>
                    {
                        orbitC = null;
                        Fx.Kill(orbitMark);
                        orbitMark = null;
                    }
                )
                .Info("Fire the gun at a spot and you circle around it. Left stick: speed and distance, right stick: height");

            Vector3? kiteAnchor = null;
            LineRenderer kiteLine = null;
            Mods.T(
                    c,
                    "Kite",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt,
                            len = 14f * s;
                        if (kiteAnchor == null)
                        {
                            kiteAnchor = Game.P.transform.position;
                            kiteLine = Fx.Line(Color.white, 0.008f, false);
                        }
                        float t = Time.time * 0.15f;
                        var gust = new Vector3(Mathf.PerlinNoise(t, 3.1f) - 0.5f, 0f, Mathf.PerlinNoise(t, 7.7f) - 0.5f);
                        Body.linearVelocity += (gust.normalized * 7f + Vector3.up * 4f) * s * dt; // a gusty breeze that also lifts
                        Movement.AntiGrav(0.6f);
                        var d = Head.position - kiteAnchor.Value;
                        float dist = d.magnitude;
                        if (dist > len)
                        {
                            var n = d / dist;
                            float outward = Vector3.Dot(Body.linearVelocity, n);
                            if (outward > 0f)
                                Body.linearVelocity -= n * outward; // the string is tight
                            Body.linearVelocity -= n * (dist - len) * 3f;
                        }
                        kiteLine.SetPosition(0, kiteAnchor.Value);
                        kiteLine.SetPosition(1, Game.RHand.position);
                    },
                    null,
                    () =>
                    {
                        kiteAnchor = null;
                        Fx.Kill(kiteLine);
                        kiteLine = null;
                    }
                )
                .Info("You're a kite on a 14 m string tied to where you turned it on: the wind lifts you and drags you around");

            var drafts = new List<GameObject>();
            Mods.T(
                    c,
                    "Updrafts",
                    () =>
                    {
                        float s = MoveKit.S;
                        var me = Game.P.transform.position;
                        for (int i = drafts.Count - 1; i >= 0; i--)
                            if (drafts[i] == null || MoveKit.Flat(drafts[i].transform.position - me).magnitude > 45f * s)
                            {
                                Fx.Kill(drafts[i]);
                                drafts.RemoveAt(i);
                            }
                        while (drafts.Count < 6) // keep six warm-air columns within 35 m of you
                        {
                            var a = UnityEngine.Random.insideUnitCircle * 35f * s;
                            var top = me + new Vector3(a.x, 30f * s, a.y);
                            var p = MoveKit.Cast(top, Vector3.down, 80f * s, out var hit) ? hit.point : top - Vector3.up * 35f * s;
                            var ps = Fx.Particles(new Color(1f, 1f, 1f, 0.35f), 8f, 0.15f * s, 4f * s, 5f, 2f * s, 80);
                            var sh = ps.shape;
                            sh.shapeType = ParticleSystemShapeType.Box;
                            sh.scale = new Vector3(5f, 0.1f, 5f) * s;
                            sh.rotation = new Vector3(-90f, 0f, 0f); // sparkles rise out of the column's base
                            ps.transform.position = p;
                            drafts.Add(ps.gameObject);
                        }
                        foreach (var d in drafts)
                        {
                            var off = me - d.transform.position;
                            if (MoveKit.Flat(off).magnitude > 2.5f * s || off.y > 60f * s)
                                continue;
                            Movement.AntiGrav(1f);
                            MoveKit.SetY(Mathf.Lerp(Body.linearVelocity.y, 9f * s, 1f - Mathf.Exp(-2f * MoveKit.Dt)));
                            break;
                        }
                    },
                    null,
                    () => Fx.Kill(drafts)
                )
                .Info("Columns of warm air (faint rising sparkles) appear around you; glide or fly into one and it carries you up");

            var lastPos = new Dictionary<VRRig, Vector3>();
            Mods.T(
                    c,
                    "Slipstream",
                    () =>
                    {
                        float s = MoveKit.S,
                            dt = MoveKit.Dt;
                        var me = Head.position;
                        var mine = MoveKit.HVel;
                        bool drafting = false;
                        foreach (var r in Game.Others)
                        {
                            var p = r.transform.position;
                            if (lastPos.TryGetValue(r, out var was))
                            {
                                var theirs = MoveKit.Flat(p - was) / dt;
                                var ahead = MoveKit.Flat(p - me);
                                if (
                                    theirs.magnitude > 2.5f * s
                                    && ahead.magnitude < 5f * s
                                    && Vector3.Dot(ahead.normalized, theirs.normalized) > 0.8f
                                    && Vector3.Dot(mine, theirs) > 0f
                                )
                                    drafting = true; // right behind someone, going their way
                            }
                            lastPos[r] = p;
                        }
                        if (drafting)
                            MoveKit.SetH(Vector3.ClampMagnitude(mine * (1f + 0.8f * dt), 20f * s));
                    },
                    null,
                    () => lastPos.Clear()
                )
                .Info("Ride in another player's wake: stay right behind someone moving fast and you speed up");

            Mods.T(
                    c,
                    "Feather Fall",
                    () =>
                    {
                        float cap = -2.5f * MoveKit.S;
                        if (Body.linearVelocity.y < cap)
                            MoveKit.SetY(cap);
                    }
                )
                .Info("You never fall faster than a slow float, from any height");
        }
    }
}
