using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    public static class Movement
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;

        // Cancels a fraction of gravity this frame (1 = weightless, negative = heavier).
        public static void AntiGrav(float f) => Body.linearVelocity -= Physics.gravity * f * Time.deltaTime;

        static void Freeze()
        {
            Body.linearVelocity = Vector3.zero;
            AntiGrav(1f);
        }

        static float Pick(Mod m, params float[] v) => v[m.Opt];

        static GameObject leftPlat,
            rightPlat;
        static readonly List<Collider> noclipped = new List<Collider>();
        static float lastWallTouch;
        static Vector3 wallNormal = Vector3.up;
        static Vector3 grappleAnchor;
        static bool grappling;
        static bool slingArmed;

        public static void Register()
        {
            const string c = "Movement";
            Mod fly = null,
                wasd = null,
                joy = null,
                speed = null,
                lowG = null,
                plat = null,
                arms = null,
                size = null,
                dash = null,
                iron = null;

            fly = Mods.T(
                    c,
                    "Fly",
                    () =>
                    {
                        if (Inp.RA.Held || Inp.Key(Key.F))
                            Body.linearVelocity = Head.forward * Pick(fly, 8, 15, 25, 45) * Game.Scale;
                    }
                )
                .Opts("Fly Speed", "Slow", "Normal", "Fast", "Insane")
                .Def(1)
                .Info("Hold right A (desktop: F) to fly where you look");

            wasd = Mods.T(
                    c,
                    "WASD Fly",
                    () =>
                    {
                        var cam = Game.DesktopCam;
                        var d = Inp.WasdDir(cam != null ? cam.transform : Head);
                        Body.linearVelocity = d.normalized * Pick(wasd, 8, 15, 25, 45) * Game.Scale * (Inp.Key(Key.LeftShift) ? 2f : 1f);
                        AntiGrav(1f);
                    }
                )
                .OnLate(MouseLook)
                .Opts("WASD Speed", "Slow", "Normal", "Fast", "Insane")
                .Def(1)
                .Info("Desktop: WASD move, hold right mouse to look, Space up, Ctrl down, Shift boost. Hovers when idle");

            joy = Mods.T(
                    c,
                    "Joystick Fly",
                    () =>
                    {
                        var f = Head.forward;
                        f.y = 0;
                        f.Normalize();
                        var r = Head.right;
                        r.y = 0;
                        r.Normalize();
                        var d = f * Inp.LStick.y + r * Inp.LStick.x + Vector3.up * Inp.RStick.y;
                        Body.linearVelocity = d * Pick(joy, 6, 12, 20, 35) * Game.Scale;
                        AntiGrav(1f);
                    }
                )
                .Opts("Joystick Speed", "Slow", "Normal", "Fast", "Insane")
                .Def(1)
                .Info("VR: left stick moves, right stick up/down");

            speed = Mods.T(c, "Speed Boost", () => RigCtl.SpeedMul = Pick(speed, 1.25f, 1.5f, 2f, 3f), null, () => RigCtl.SpeedMul = 1f)
                .Opts("Boost", "1.25x", "1.5x", "2x", "3x")
                .Def(1)
                .Info("Multiplies your arm-swing speed and speed cap");
            Mods.T(c, "Speed Cap Off", () => RigCtl.Uncapped = true, null, () => RigCtl.Uncapped = false)
                .Info("Removes the max running speed cap");

            lowG = Mods.T(c, "Low Gravity", () => AntiGrav(Pick(lowG, 0.25f, 0.5f, 0.75f)))
                .Opts("Gravity Cut", "25%", "50%", "75%")
                .Def(1)
                .Info("Floatier jumps");
            Mods.T(c, "Zero Gravity", () => AntiGrav(1f)).Info("No gravity at all");
            Mods.T(c, "High Gravity", () => AntiGrav(-1f)).Info("Double gravity");

            plat = Mods.T(
                    c,
                    "Platforms",
                    () =>
                    {
                        Platform(ref leftPlat, Inp.LGrip.Held || Inp.Key(Key.Z), Game.LHand, plat.Opt);
                        Platform(ref rightPlat, Inp.RGrip.Held && !Gun.Aiming || Inp.Key(Key.X), Game.RHand, plat.Opt);
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(leftPlat);
                        Fx.Kill(rightPlat);
                    }
                )
                .Opts("Platform Style", "Cyan", "Invisible", "Rainbow", "Beat")
                .Info("Hold grip to stand on a platform under that hand (desktop: Z / X)");

            Mods.T(
                    c,
                    "NoClip",
                    () =>
                    {
                        // Platforms are box colliders so they stay solid; with no platform or fly input you hover instead of sinking
                        bool flying = fly.On && (Inp.RA.Held || Inp.Key(Key.F)) || wasd.On || joy.On;
                        if (!flying && leftPlat == null && rightPlat == null)
                        {
                            var v = Body.linearVelocity;
                            Body.linearVelocity = new Vector3(v.x, 0f, v.z);
                            AntiGrav(1f);
                        }
                    },
                    () =>
                    {
                        noclipped.Clear();
                        foreach (var col in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                            if (col.enabled && !col.isTrigger)
                            {
                                col.enabled = false;
                                noclipped.Add(col);
                            }
                    },
                    () =>
                    {
                        foreach (var col in noclipped)
                            if (col != null)
                                col.enabled = true;
                        noclipped.Clear();
                    }
                )
                .Info("Go through the map. Your platforms stay solid; with no platform or fly you hover in place");

            arms = Mods.T(
                    c,
                    "Long Arms",
                    () => Game.P.transform.localScale = Vector3.one * Pick(arms, 1.15f, 1.3f, 1.5f),
                    null,
                    () => Game.P.transform.localScale = Vector3.one
                )
                .Opts("Arm Length", "Short", "Medium", "Long")
                .Def(1)
                .Info("Longer reach");
            size = Mods.T(
                    c,
                    "Size Changer",
                    () => Game.P.SetScaleMultiplier(Pick(size, 0.5f, 0.75f, 1.5f, 2f)),
                    null,
                    () => Game.P.SetScaleMultiplier(1f)
                )
                .Opts("Size", "Tiny", "Small", "Big", "Huge")
                .Info("Changes your player scale");

            Mods.T(
                    c,
                    "Wall Walk",
                    () =>
                    {
                        var l = Game.P.LeftHand;
                        var r = Game.P.RightHand;
                        if (l.wasColliding || l.isColliding)
                        {
                            wallNormal = l.lastHitInfo.normal;
                            lastWallTouch = Time.time;
                        }
                        else if (r.wasColliding || r.isColliding)
                        {
                            wallNormal = r.lastHitInfo.normal;
                            lastWallTouch = Time.time;
                        }
                        if (Time.time - lastWallTouch < 0.4f && wallNormal != Vector3.zero)
                        {
                            AntiGrav(1f);
                            Body.linearVelocity -= wallNormal * 9.81f * Time.deltaTime;
                        }
                    }
                )
                .Info("Gravity pulls you toward the last surface your hands touched");

            Mods.T(
                    c,
                    "Air Grab",
                    () =>
                    {
                        if (Inp.LGrip.Held && Inp.RGrip.Held)
                            Freeze();
                    }
                )
                .Info("Hold both grips to freeze in mid-air");
            dash = Mods.T(
                    c,
                    "Dash",
                    () =>
                    {
                        if (Inp.RB.Down || Inp.KeyDown(Key.LeftShift))
                            Body.linearVelocity = Head.forward * Pick(dash, 10, 18, 30) * Game.Scale;
                    }
                )
                .Opts("Dash Power", "Light", "Strong", "Rocket")
                .Def(1)
                .Info("Right B (desktop: Left Shift) bursts you forward");
            Mods.T(
                    c,
                    "Air Jump",
                    () =>
                    {
                        if (Inp.LX.Down || Inp.KeyDown(Key.J))
                        {
                            var v = Body.linearVelocity;
                            v.y = 7f * Game.Scale;
                            Body.linearVelocity = v;
                        }
                    }
                )
                .Info("Left X (desktop: J) jumps in mid-air");
            Mods.T(
                    c,
                    "Hover",
                    () =>
                    {
                        if (Inp.LTrig.Held || Inp.Key(Key.H))
                            Freeze();
                    }
                )
                .Info("Hold left trigger (desktop: H) to hover in place");
            Mods.T(c, "Ice Skate").OnLate(() => Game.P.SetMaximumSlipThisFrame()).Info("Every surface is slippery");
            iron = Mods.T(
                    c,
                    "Iron Monke",
                    () =>
                    {
                        foreach (var (held, hand) in new[] { (Inp.LGrip.Held, Game.LHand), (Inp.RGrip.Held && !Gun.Aiming, Game.RHand) })
                            if (held)
                                Body.linearVelocity +=
                                    (Head.position - hand.position).normalized * Pick(iron, 12, 20, 32) * Time.deltaTime * Game.Scale;
                    }
                )
                .Opts("Thrust", "Low", "Medium", "High")
                .Def(1)
                .Info("Hold grips: thrusters push you away from your hands (point hands down to rise)");

            Mods.T(
                    c,
                    "Grapple Gun",
                    () =>
                    {
                        if (!Gun.Get(out var p, out _))
                        {
                            grappling = false;
                            return;
                        }
                        if (Gun.FireDown)
                        {
                            grappleAnchor = p;
                            grappling = true;
                        }
                        if (!Gun.Firing)
                            grappling = false;
                        if (grappling)
                        {
                            AntiGrav(1f);
                            Body.linearVelocity += (grappleAnchor - Head.position).normalized * 35f * Time.deltaTime * Game.Scale;
                        }
                    }
                )
                .Info("Aim and fire to latch on and get pulled toward that point");

            Mods.T(
                    c,
                    "Auto Run",
                    () =>
                    {
                        var f = Head.forward;
                        f.y = 0;
                        var v = Body.linearVelocity;
                        var h = f.normalized * 6.5f * Game.Scale;
                        Body.linearVelocity = new Vector3(h.x, v.y, h.z);
                    }
                )
                .Info("Keeps you running where you look");
            Mods.T(c, "Anchor", Freeze).Info("Locks you in place");
            Mods.T(
                    c,
                    "Slingshot",
                    () =>
                    {
                        bool both = Inp.LGrip.Held && Inp.RGrip.Held;
                        if (both)
                        {
                            slingArmed = true;
                            Freeze();
                        }
                        else if (slingArmed)
                        {
                            slingArmed = false;
                            Body.linearVelocity = Head.forward * 28f * Game.Scale;
                        }
                    }
                )
                .Info("Hold both grips to charge, release to launch where you look");
            Vector3 lastVel = Vector3.zero;
            Mods.T(
                    c,
                    "Momentum Keeper",
                    () =>
                    {
                        var l = Game.P.LeftHand;
                        var r = Game.P.RightHand;
                        bool landed = l.isColliding && !l.wasColliding || r.isColliding && !r.wasColliding;
                        var v = Body.linearVelocity;
                        if (landed && new Vector2(lastVel.x, lastVel.z).sqrMagnitude > new Vector2(v.x, v.z).sqrMagnitude)
                            Body.linearVelocity = new Vector3(lastVel.x, v.y, lastVel.z);
                        lastVel = Body.linearVelocity;
                    }
                )
                .Info("Keeps your sideways speed when your hands hit the ground");

            var pads = new List<GameObject>();
            Mods.T(
                    c,
                    "Bounce Pads",
                    () =>
                    {
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            var pad = Fx.Prim(PrimitiveType.Cylinder, new Vector3(1f, 0.02f, 1f) * Game.Scale, Fx.Accent);
                            pad.transform.position = p + Vector3.up * 0.02f;
                            pads.Add(pad);
                            if (pads.Count > 6)
                            {
                                Fx.Kill(pads[0]);
                                pads.RemoveAt(0);
                            }
                        }
                        var feet = Game.P.bodyCollider.transform.position - Vector3.up * 0.5f * Game.Scale;
                        foreach (var pad in pads)
                        {
                            var d = feet - pad.transform.position;
                            if (
                                Body.linearVelocity.y < 0
                                && new Vector2(d.x, d.z).magnitude < 0.6f * Game.Scale
                                && d.y > -0.3f
                                && d.y < 0.6f * Game.Scale
                            )
                            {
                                var v = Body.linearVelocity;
                                Body.linearVelocity = new Vector3(v.x, 13f * Game.Scale, v.z);
                            }
                            pad.GetComponent<Renderer>().material.color = Fx.Accent * (0.8f + 0.2f * Mathf.Sin(Time.time * 4f));
                        }
                    },
                    null,
                    () => Fx.Kill(pads)
                )
                .Info("Fire the gun to drop a bounce pad (max 6). Only you bounce on them");

            Mods.T(
                    c,
                    "Glide",
                    () =>
                    {
                        var h = Head.position;
                        float reach = 0.45f * Game.Scale;
                        bool armsOut =
                            Vector3.Distance(Game.LHand.position, h) > reach
                            && Vector3.Distance(Game.RHand.position, h) > reach
                            && Mathf.Abs(Game.LHand.position.y - h.y) < 0.4f * Game.Scale
                            && Mathf.Abs(Game.RHand.position.y - h.y) < 0.4f * Game.Scale
                            && Vector3.Dot(Game.LHand.position - h, Game.RHand.position - h) < 0f; // hands on opposite sides
                        if (!armsOut || Body.linearVelocity.y > -1.5f)
                            return;
                        var f = Head.forward;
                        f.y = 0;
                        var v = Body.linearVelocity;
                        Body.linearVelocity = new Vector3(v.x, -1.5f * Game.Scale, v.z) + f.normalized * 4f * Time.deltaTime * Game.Scale;
                    }
                )
                .Info("Hold both arms out wide while falling to glide down slowly");

            RigPose ghostPose = default;
            var orbs = new List<GameObject>();
            Mods.T(
                    c,
                    "Ghost Monkey",
                    () =>
                    {
                        RigCtl.Freeze(ghostPose);
                        if (orbs.Count == 2)
                        {
                            orbs[0].transform.position = Game.LHand.position;
                            orbs[1].transform.position = Game.RHand.position;
                        }
                    },
                    () =>
                    {
                        ghostPose = RigPose.Capture(Game.Me);
                        for (int i = 0; i < 2; i++)
                            orbs.Add(Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.08f, Fx.Accent));
                    },
                    () => Fx.Kill(orbs)
                )
                .Info("Your monke stays frozen where it was; you walk away as two glowing orbs");

            Mods.T(
                    c,
                    "Blink",
                    () =>
                    {
                        if (Inp.RB.Down || Inp.KeyDown(Key.E))
                            Game.Teleport(Game.P.transform.position + Head.forward * 5f * Game.Scale);
                    }
                )
                .Info("Right B (desktop: E) teleports you 5 m forward");
        }

        // Desktop has no head tracking, so yaw turns the player's turn parent and pitch tilts the camera.
        static float lookPitch;

        static void MouseLook()
        {
            if (Inp.VR || Inp.Ms == null || !Inp.Ms.rightButton.isPressed || Board.DesktopOpen)
                return;
            var d = Inp.Ms.delta.ReadValue() * 0.15f;
            Game.P.turnParent.transform.RotateAround(Head.position, Vector3.up, d.x);
            lookPitch = Mathf.Clamp(lookPitch - d.y, -85f, 85f);
            var cam = Game.P.mainCamera.transform;
            var e = cam.localEulerAngles;
            cam.localEulerAngles = new Vector3(lookPitch, e.y, e.z);
        }

        static void Platform(ref GameObject plat, bool held, Transform hand, int style)
        {
            if (held && plat == null)
            {
                plat = Fx.Prim(PrimitiveType.Cube, new Vector3(0.28f, 0.015f, 0.28f) * Game.Scale, Fx.Accent, collider: true);
                plat.transform.position = hand.position - Vector3.up * 0.06f * Game.Scale;
                plat.transform.rotation = Quaternion.Euler(0, Game.Head.eulerAngles.y, 0);
                plat.GetComponent<Renderer>().enabled = style != 1;
            }
            else if (!held && plat != null)
            {
                Fx.Kill(plat);
                plat = null;
            }
            if (plat != null && style == 2)
                plat.GetComponent<Renderer>().material.color = Fx.Rainbow();
            if (plat != null && style == 3)
                plat.GetComponent<Renderer>().material.color = Color.Lerp(
                    Fx.Accent * 0.25f,
                    Color.white,
                    Mathf.Clamp01(WorldFx.Level * 8f)
                );
        }
    }
}
