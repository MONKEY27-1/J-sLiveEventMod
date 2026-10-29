using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Experimental: odd physics, gravity games and movement experiments. Each one only changes your own movement.
    public static class Experimental
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;

        // Gravity toward a direction instead of down (the normal pull is cancelled first)
        static void PullToward(Vector3 dir)
        {
            Movement.AntiGrav(1f);
            Body.linearVelocity += dir.normalized * Physics.gravity.magnitude * MoveKit.Dt;
        }

        public static void Register()
        {
            const string c = "Experimental";

            Vector3? planet = null;
            GameObject planetDot = null;
            Mods.T(
                    c,
                    "Planet Gravity",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            planet = p;
                            if (planetDot == null)
                                planetDot = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.4f * s, new Color(0.4f, 0.8f, 1f));
                            planetDot.transform.position = p;
                        }
                        if (planet == null)
                            return;
                        var d = planet.Value - MoveKit.BodyPos;
                        if (d.magnitude < 80f * s)
                            PullToward(d);
                    },
                    null,
                    () =>
                    {
                        planet = null;
                        Fx.Kill(planetDot);
                        planetDot = null;
                    }
                )
                .Info("Fire the gun at a spot: gravity now pulls toward it, so you can walk around rocks and trees like tiny planets");

            Vector3? lookDown = null;
            Mods.T(
                    c,
                    "Look Gravity",
                    () =>
                    {
                        if (Inp.RB.Down || MoveKit.KeyDown(Key.U))
                        {
                            lookDown = lookDown == null ? Head.forward : (Vector3?)null;
                            Hud.Toast(lookDown == null ? "Gravity back to normal" : "Gravity now points where you looked");
                        }
                        if (lookDown != null)
                            PullToward(lookDown.Value);
                    },
                    null,
                    () => lookDown = null
                )
                .Info(
                    "Right B (desktop: U) makes gravity point the way you're looking, so you fall toward that wall. Press again for normal"
                );

            Mods.T(
                    c,
                    "Thick Air",
                    () =>
                    {
                        if (MoveKit.Airborne)
                            Body.linearVelocity *= 1f - Mathf.Min(1f, 2.5f * MoveKit.Dt);
                    }
                )
                .Info("The air is thick like honey: you drift slowly and float down gently after every jump");

            Mods.T(
                    c,
                    "Wind",
                    () =>
                    {
                        float t = Time.time;
                        float ang = Mathf.PerlinNoise(t * 0.02f, 1.3f) * 720f * Mathf.Deg2Rad; // the direction wanders slowly
                        float gust = 2f + 5f * Mathf.PerlinNoise(t * 0.4f, 9.1f);
                        Body.linearVelocity += new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * gust * MoveKit.S * MoveKit.Dt;
                    }
                )
                .Info("A gusty wind that slowly changes direction pushes you around - lean into it");

            float twisterT = -1f;
            Vector3 twisterAt = Vector3.zero;
            Mods.T(
                    c,
                    "Tornado",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (twisterT < 0f && Inp.RB.Down)
                        {
                            twisterT = 0f;
                            twisterAt = Game.P.transform.position;
                        }
                        if (twisterT < 0f)
                            return;
                        twisterT += MoveKit.Dt;
                        float a = twisterT * 5f,
                            r = 2.5f * s;
                        if (twisterT >= 4f)
                        {
                            var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                            Body.linearVelocity = (tangent * 12f + Vector3.up * 3f) * s; // flung out of the funnel
                            twisterT = -1f;
                            return;
                        }
                        var target = twisterAt + new Vector3(Mathf.Cos(a) * r, twisterT * 5f * s, Mathf.Sin(a) * r);
                        Body.linearVelocity = Vector3.ClampMagnitude((target - Game.P.transform.position) * 8f, 30f * s);
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () => twisterT = -1f
                )
                .Info("Press right B to get swept up in a tornado: you spin up into the air for 4 seconds, then get flung out");

            Vector3? bungee = null;
            LineRenderer cord = null;
            Mods.T(
                    c,
                    "Bungee Jump",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Inp.RB.Down)
                        {
                            bungee = bungee == null ? Head.position : (Vector3?)null;
                            Hud.Toast(bungee != null ? "Cord tied here - jump!" : "Cord cut");
                        }
                        if (bungee == null)
                        {
                            if (cord != null)
                                cord.gameObject.SetActive(false);
                            return;
                        }
                        var d = MoveKit.BodyPos - bungee.Value;
                        float dist = d.magnitude,
                            rest = 6f * s;
                        if (dist > rest)
                        {
                            var n = d / dist;
                            Body.linearVelocity -= n * (dist - rest) * 18f * MoveKit.Dt; // the cord stretches and pulls back
                            Body.linearVelocity -= n * Vector3.Dot(Body.linearVelocity, n) * Mathf.Min(1f, 0.8f * MoveKit.Dt); // and loses a little each bounce
                        }
                        if (cord == null)
                            cord = Fx.Line(new Color(1f, 0.4f, 0.1f), 0.02f, false);
                        cord.gameObject.SetActive(true);
                        cord.SetPosition(0, bungee.Value);
                        cord.SetPosition(1, MoveKit.BodyPos);
                    },
                    null,
                    () =>
                    {
                        bungee = null;
                        Fx.Kill(cord);
                        cord = null;
                    }
                )
                .Info(
                    "Press right B to tie a bungee cord where you are, then jump off something: it stretches and bounces you. B again cuts it"
                );

            int burns = 0;
            float lavaCool = 0f;
            Mod lava = null;
            lava = Mods.T(
                    c,
                    "Floor Is Lava",
                    () =>
                    {
                        if ((lavaCool -= MoveKit.Dt) > 0f)
                            return;
                        bool floor = Game.P.BodyOnGround || MoveKit.SurfaceTouch(out var n) && n.y > 0.75f;
                        if (!floor)
                            return;
                        MoveKit.SetY(8f * MoveKit.S);
                        MoveKit.BuzzBoth(0.7f, 0.15f);
                        burns++;
                        lavaCool = 0.4f;
                    },
                    () => burns = 0
                )
                .Live(() => "Floor Is Lava" + (lava.On ? " (burned " + burns + "x)" : ""))
                .Info(
                    "Mini-game: every floor burns and bounces you back up. Stay on walls, branches and slopes - the counter shows your burns"
                );

            var gravDirs = new[]
            {
                (Vector3.down, "down"),
                (Vector3.up, "up"),
                (Vector3.left, "west"),
                (Vector3.right, "east"),
                (Vector3.forward, "north"),
                (Vector3.back, "south"),
            };
            Vector3 randomDown = Vector3.down;
            float nextFlip = 0f;
            Mods.T(
                    c,
                    "Random Gravity",
                    () =>
                    {
                        if (Time.time >= nextFlip)
                        {
                            nextFlip = Time.time + 8f;
                            var pick = gravDirs[UnityEngine.Random.Range(0, gravDirs.Length)];
                            randomDown = pick.Item1;
                            Hud.Toast("Gravity points " + pick.Item2);
                            MoveKit.BuzzBoth(0.3f, 0.1f);
                        }
                        PullToward(randomDown);
                    },
                    () => nextFlip = 0f
                )
                .Info("Every 8 seconds gravity points a new random way: down, up or sideways");

            Mods.T(
                    c,
                    "Reverse Momentum",
                    () =>
                    {
                        if (Inp.RB.Down || MoveKit.KeyDown(Key.O))
                            Body.linearVelocity = -Body.linearVelocity;
                    }
                )
                .Info("Right B (desktop: O) instantly reverses the way you're moving");

            Vector3? bank = null;
            Mod bankMod = null;
            bankMod = Mods.T(
                    c,
                    "Momentum Bank",
                    () =>
                    {
                        if (Inp.RB.Down || MoveKit.KeyDown(Key.M))
                        {
                            if (bank == null)
                                bank = Body.linearVelocity;
                            else
                            {
                                Body.linearVelocity = bank.Value;
                                bank = null;
                                return;
                            }
                        }
                        if (bank != null)
                            MoveKit.Stop(); // frozen while your speed is banked
                    },
                    null,
                    () => bank = null
                )
                .Live(() =>
                    "Momentum Bank" + (bankMod.On && bank != null ? " (" + bank.Value.magnitude.ToString("0") + " m/s stored)" : "")
                )
                .Info("Right B (desktop: M) freezes you and stores your speed; press again to get it all back at once");

            Mod limit = null;
            limit = Mods.T(
                    c,
                    "Speed Limit",
                    () =>
                    {
                        float cap = new[] { 3f, 5f, 8f }[limit.Opt] * MoveKit.S;
                        if (Body.linearVelocity.magnitude > cap)
                            Body.linearVelocity = Body.linearVelocity.normalized * cap;
                    }
                )
                .Opts("Limit", "3 m/s", "5 m/s", "8 m/s")
                .Def(1)
                .Info("Caps how fast you can go - calmer for beginners, or a challenge for you");

            GameObject zone = null;
            Mods.T(
                    c,
                    "Zero-G Zone",
                    () =>
                    {
                        float s = MoveKit.S,
                            r = 5f * s;
                        if (Inp.RB.Down)
                        {
                            if (zone == null)
                                zone = Fx.Prim(PrimitiveType.Sphere, Vector3.one * r * 2f, new Color(0.5f, 0.8f, 1f, 0.12f));
                            zone.transform.position = Head.position;
                        }
                        if (zone != null && Vector3.Distance(Head.position, zone.transform.position) < r)
                            Movement.AntiGrav(1f);
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(zone);
                        zone = null;
                    }
                )
                .Info("Right B places a 10 m bubble where gravity is off - float around inside it. B again moves the bubble to you");

            float grow = 1f;
            Mods.T(
                    c,
                    "Size By Speed",
                    () =>
                    {
                        float want = 1f + Mathf.Clamp01(Body.linearVelocity.magnitude / 15f) * 0.8f;
                        grow = Mathf.MoveTowards(grow, want, 0.6f * MoveKit.Dt);
                        MoveKit.ScaleMul = grow;
                        Game.P.SetScaleMultiplier(grow);
                    },
                    () => grow = 1f,
                    () =>
                    {
                        MoveKit.ScaleMul = 1f;
                        Game.P.SetScaleMultiplier(1f);
                    }
                )
                .Info("You grow bigger the faster you go (up to 1.8x) and shrink back when you slow down");

            Vector3 hook = Vector3.zero;
            bool hookSet = false;
            LineRenderer stringL = null,
                stringR = null;
            Mods.T(
                    c,
                    "Puppet Strings",
                    () =>
                    {
                        float s = MoveKit.S,
                            len = 6f * s;
                        var head = Head.position;
                        if (!hookSet)
                        {
                            hook = head + Vector3.up * len;
                            hookSet = true;
                        }
                        hook.y += Inp.RStick.y * 3f * s * MoveKit.Dt; // right stick raises or lowers the puppeteer
                        var follow = new Vector3(head.x, hook.y, head.z);
                        hook = Vector3.Lerp(hook, follow, 1f - Mathf.Exp(-0.8f * MoveKit.Dt)); // the puppeteer drifts after you
                        var d = head - hook;
                        float dist = d.magnitude;
                        if (dist > len)
                        {
                            var n = d / dist;
                            float outward = Vector3.Dot(Body.linearVelocity, n);
                            if (outward > 0f)
                                Body.linearVelocity -= n * outward;
                            Body.linearVelocity -= n * (dist - len) * 4f;
                        }
                        if (stringL == null)
                        {
                            stringL = Fx.Line(Color.white, 0.006f, false);
                            stringR = Fx.Line(Color.white, 0.006f, false);
                        }
                        stringL.SetPosition(0, hook);
                        stringL.SetPosition(1, Game.LHand.position);
                        stringR.SetPosition(0, hook);
                        stringR.SetPosition(1, Game.RHand.position);
                    },
                    () => hookSet = false,
                    () =>
                    {
                        Fx.Kill(stringL);
                        Fx.Kill(stringR);
                        stringL = stringR = null;
                    }
                )
                .Info(
                    "You dangle from a puppeteer's strings 6 m above you that slowly follow you around. Right stick moves the puppeteer up or down"
                );

            Mods.T(
                    c,
                    "Drunk Monke",
                    () =>
                    {
                        float t = Time.time * 0.6f;
                        var push = new Vector3(Mathf.PerlinNoise(t, 2.3f) - 0.5f, 0f, Mathf.PerlinNoise(t, 8.9f) - 0.5f) * 7f * MoveKit.S;
                        Body.linearVelocity += push * MoveKit.Dt;
                    }
                )
                .Info("Your steps wander off on their own - try walking in a straight line");

            bool echoGround = false;
            float echoAt = -1f,
                echoVy = 0f;
            Mods.T(
                    c,
                    "Echo Jump",
                    () =>
                    {
                        bool g = MoveKit.Grounded;
                        var v = Body.linearVelocity;
                        if (!g && echoGround && v.y > 1.5f * MoveKit.S)
                        {
                            echoAt = Time.time + 0.6f;
                            echoVy = v.y * 0.8f;
                        }
                        if (echoAt > 0f && Time.time >= echoAt)
                        {
                            echoAt = -1f;
                            MoveKit.SetY(Mathf.Max(v.y, echoVy));
                            MoveKit.BuzzBoth(0.2f, 0.05f);
                        }
                        echoGround = g;
                    },
                    () => echoAt = -1f
                )
                .Info("Every jump echoes: 0.6 s later you get a second, slightly smaller jump in mid-air");

            Vector3 smooth = Vector3.zero;
            bool smoothSet = false;
            Mods.T(
                    c,
                    "Inertia Dampener",
                    () =>
                    {
                        var v = Body.linearVelocity;
                        smooth = smoothSet ? Vector3.Lerp(smooth, v, 1f - Mathf.Exp(-3f * MoveKit.Dt)) : v;
                        smoothSet = true;
                        Body.linearVelocity = new Vector3(smooth.x, v.y, smooth.z); // sideways only, so jumps and falls feel normal
                    },
                    () => smoothSet = false
                )
                .Info("Heavy, smooth movement: your speed changes gradually, like a monke with a lot of weight behind it");

            float lastPitch = 0f,
                nodDown = -9f,
                nodCool = 0f;
            bool nodHave = false;
            Mods.T(
                    c,
                    "Nod Jump",
                    () =>
                    {
                        float pitch = Mathf.Asin(Mathf.Clamp(Head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
                        float speed = nodHave ? (pitch - lastPitch) / MoveKit.Dt : 0f;
                        lastPitch = pitch;
                        nodHave = true;
                        nodCool -= MoveKit.Dt;
                        if (speed < -120f)
                            nodDown = Time.time; // the head dips...
                        else if (speed > 120f && Time.time - nodDown < 0.35f && nodCool <= 0f) // ...and comes straight back up
                        {
                            MoveKit.SetY(Mathf.Max(Body.linearVelocity.y, 6.5f * MoveKit.S));
                            nodCool = 0.5f;
                            nodDown = -9f;
                            MoveKit.BuzzBoth(0.25f, 0.05f);
                        }
                    },
                    () => nodHave = false
                )
                .Info("Nod your head (a quick down-up) to jump - hands-free");

            Mods.T(
                    c,
                    "Velocity Snap",
                    () =>
                    {
                        var h = MoveKit.HVel;
                        if (h.magnitude < 0.5f * MoveKit.S)
                            return;
                        float step = Mathf.PI / 4f,
                            a = Mathf.Round(Mathf.Atan2(h.z, h.x) / step) * step;
                        MoveKit.SetH(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * h.magnitude);
                    }
                )
                .Info("Retro movement: you can only travel in the 8 compass directions, like an old arcade game");

            float baseY = 0f;
            Mods.T(
                    c,
                    "Thin Air",
                    () =>
                    {
                        float up = (Game.P.transform.position.y - baseY) / (80f * MoveKit.S);
                        Movement.AntiGrav(Mathf.Clamp(up, 0f, 0.95f));
                    },
                    () => baseY = Game.P.transform.position.y
                )
                .Info("Gravity gets weaker the higher you climb above where you turned it on; 80 m up you barely fall");

            GameObject skyFloor = null;
            float skyY = 0f;
            Mods.T(
                    c,
                    "Sky Floor",
                    () =>
                    {
                        if (skyFloor == null)
                            return;
                        if (Inp.LGrip.Held)
                            skyY += Inp.RStick.y * 2f * MoveKit.S * MoveKit.Dt;
                        var p = Game.P.transform.position;
                        skyFloor.transform.position = new Vector3(p.x, skyY, p.z);
                    },
                    () =>
                    {
                        float s = MoveKit.S;
                        skyY = MoveKit.FeetY - 0.11f * s; // top face just under your feet
                        var col = Fx.Accent;
                        skyFloor = Fx.Prim(
                            PrimitiveType.Cube,
                            new Vector3(40f, 0.1f, 40f) * s,
                            new Color(col.r, col.g, col.b, 0.08f),
                            collider: true
                        );
                        var p = Game.P.transform.position;
                        skyFloor.transform.position = new Vector3(p.x, skyY, p.z);
                    },
                    () =>
                    {
                        Fx.Kill(skyFloor);
                        skyFloor = null;
                    }
                )
                .Info(
                    "A see-through floor appears at your feet and follows you, so you can walk on air at that height. Left grip + right stick moves it up or down"
                );

            float shrink = 1f;
            Mods.T(
                    c,
                    "Crouch Shrink",
                    () =>
                    {
                        float ratio = MoveKit.CrouchRatio();
                        float want =
                            ratio < 0.6f ? 0.35f
                            : ratio > 0.8f ? 1f
                            : shrink;
                        shrink = Mathf.MoveTowards(shrink, want, 1.5f * MoveKit.Dt);
                        MoveKit.ScaleMul = shrink;
                        Game.P.SetScaleMultiplier(shrink);
                    },
                    () => shrink = 1f,
                    () =>
                    {
                        MoveKit.ScaleMul = 1f;
                        Game.P.SetScaleMultiplier(1f);
                    }
                )
                .Info("Crouch down to shrink to ant size and squeeze into tiny gaps; stand up tall to grow back");

            float gravity = 1f;
            Mod dial = null;
            dial = Mods.T(
                    c,
                    "Gravity Dial",
                    () =>
                    {
                        float dt = MoveKit.Dt;
                        if (Inp.LGrip.Held)
                            gravity += Inp.LStick.y * dt;
                        if (MoveKit.Key(Key.RightBracket))
                            gravity += dt;
                        if (MoveKit.Key(Key.LeftBracket))
                            gravity -= dt;
                        gravity = Mathf.Clamp(gravity, -1f, 3f);
                        Movement.AntiGrav(1f - gravity);
                    },
                    () => gravity = 1f
                )
                .Live(() => "Gravity Dial" + (dial.On ? " (" + gravity.ToString("0.0") + "x)" : ""))
                .Info("Turn gravity up or down live: hold left grip and push the left stick (desktop: [ and ]), anywhere from -1x to 3x");

            Mods.T(
                    c,
                    "Repulsor Hands",
                    () =>
                    {
                        float s = MoveKit.S,
                            reach = 0.5f * s;
                        foreach (var hand in new[] { Game.LHand, Game.RHand })
                            if (MoveKit.Cast(hand.position, Vector3.down, reach, out var hit))
                            {
                                float k = 1f - hit.distance / reach; // stronger the closer your palm gets
                                Movement.AntiGrav(0.5f * k);
                                Body.linearVelocity += Vector3.up * k * 14f * s * MoveKit.Dt;
                            }
                    }
                )
                .Info("Surfaces under your hands push you up like repulsor jets: hold your hands low over the ground to hover");
        }
    }
}
