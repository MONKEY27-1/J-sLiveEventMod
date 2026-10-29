using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Toys. All local: only you see them.
    public static class Fun
    {
        static readonly List<GameObject> toys = new List<GameObject>();
        static readonly List<LineRenderer> strokes = new List<LineRenderer>();
        static readonly List<GameObject> blocks = new List<GameObject>();

        static GameObject Toy(PrimitiveType t, float size, Color c, bool physics = true)
        {
            var g = Fx.Prim(t, Vector3.one * size * Game.Scale, c, collider: true);
            if (physics)
            {
                var rb = g.AddComponent<Rigidbody>();
                rb.mass = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            toys.Add(g);
            if (toys.Count > 80)
            {
                Fx.Kill(toys[0]);
                toys.RemoveAt(0);
            }
            return g;
        }

        // Short-lived falling bit of colour (no collider, so it never bumps the player)
        static void Spark(Vector3 at, Vector3 vel, Color c)
        {
            var g = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.05f * Game.Scale, c);
            g.transform.position = at;
            var rb = g.AddComponent<Rigidbody>();
            rb.linearVelocity = vel;
            rb.linearDamping = 1.5f;
            Object.Destroy(g, 2.5f);
        }

        static Vector3 Aim(Transform hand) =>
            Inp.VR ? (Settings.GunForward.On ? hand.forward : -hand.up) : Game.DesktopCam.transform.forward;

        static Vector3 PaintPoint =>
            Inp.VR ? Game.RHand.position : Game.DesktopCam.transform.position + Game.DesktopCam.transform.forward * 1.5f;

        public static void Register()
        {
            const string c = "Fun";
            GameObject held = null;
            Mods.T(
                    c,
                    "Throw Ball",
                    () =>
                    {
                        if (Inp.LGrip.Down || Inp.KeyDown(Key.N))
                        {
                            held = Toy(PrimitiveType.Sphere, 0.25f, Color.HSVToRGB(Random.value, 0.8f, 1f));
                            held.GetComponent<Rigidbody>().isKinematic = true;
                        }
                        if (held != null && (Inp.LGrip.Held || Inp.Key(Key.N)))
                            held.transform.position = Inp.VR ? Game.LHand.position : PaintPoint;
                        if (held != null && (Inp.LGrip.Up || Inp.Kb != null && Inp.Kb[Key.N].wasReleasedThisFrame))
                        {
                            var rb = held.GetComponent<Rigidbody>();
                            rb.isKinematic = false;
                            rb.linearVelocity = Inp.VR
                                ? ControllerInputPoller.instance.leftVelocity * 1.5f
                                : Game.DesktopCam.transform.forward * 12f;
                            held = null;
                        }
                    }
                )
                .Info("Hold left grip (desktop: N) to grab a bouncy ball, let go to throw it");
            float rain = 0f;
            Mods.T(
                    c,
                    "Ball Rain",
                    () =>
                    {
                        if ((rain -= Time.deltaTime) > 0f)
                            return;
                        rain = 0.3f;
                        var b = Toy(PrimitiveType.Sphere, 0.3f, Fx.Rainbow(0.2f, Random.value));
                        b.transform.position = Game.Head.position + new Vector3(Random.Range(-4f, 4f), 8f, Random.Range(-4f, 4f));
                    }
                )
                .Info("Bouncy balls fall from the sky around you");
            Mods.B(
                    c,
                    "Ball Pit",
                    () =>
                    {
                        for (int i = 0; i < 40; i++)
                        {
                            var b = Toy(PrimitiveType.Sphere, 0.25f, Color.HSVToRGB(Random.value, 0.8f, 1f));
                            b.transform.position = Game.Head.position + Vector3.up + Random.insideUnitSphere;
                        }
                    }
                )
                .Info("Dumps 40 balls on your head");
            Mods.T(
                    c,
                    "Magnet Hands",
                    () =>
                    {
                        foreach (var (grip, hand) in new[] { (Inp.LGrip.Held, Game.LHand), (Inp.RGrip.Held && !Gun.Aiming, Game.RHand) })
                            if (grip)
                                foreach (var t in toys)
                                {
                                    if (t == null)
                                        continue;
                                    var rb = t.GetComponent<Rigidbody>();
                                    if (rb != null && !rb.isKinematic)
                                        rb.linearVelocity = Vector3.Lerp(
                                            rb.linearVelocity,
                                            (hand.position - t.transform.position) * 6f,
                                            0.2f
                                        );
                                }
                    }
                )
                .Info("Hold grip to pull all your toys toward that hand");
            Mods.B(
                    c,
                    "Clear Toys",
                    () =>
                    {
                        Fx.Kill(toys);
                        Hud.Toast("Toys cleared");
                    }
                )
                .Info("Removes every ball, pet and toy");

            GameObject orb = null,
                cube = null;
            Mods.T(
                    c,
                    "Pet Orb",
                    () =>
                    {
                        if (orb == null)
                            orb = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.18f, Fx.Accent);
                        var target = Game.Head.position + Game.Head.right * 0.5f + Vector3.up * (0.1f + Mathf.Sin(Time.time * 3f) * 0.08f);
                        orb.transform.position = Vector3.Lerp(orb.transform.position, target, 4f * Time.deltaTime);
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(orb);
                        orb = null;
                    }
                )
                .Info("A little glowing orb follows you around");
            Mods.T(
                    c,
                    "Pet Cube",
                    () =>
                    {
                        if (cube == null)
                            cube = Fx.Prim(PrimitiveType.Cube, Vector3.one * 0.2f, new Color(1f, 0.6f, 0.2f));
                        var f = Game.Head.forward;
                        f.y = 0;
                        var target =
                            Game.P.transform.position
                            - f.normalized * 0.8f
                            + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.15f - 0.6f);
                        cube.transform.position = Vector3.Lerp(cube.transform.position, target, 3f * Time.deltaTime);
                        cube.transform.rotation = Quaternion.LookRotation(Game.Head.position - cube.transform.position);
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(cube);
                        cube = null;
                    }
                )
                .Info("A hopping cube that follows behind you");

            Mod paint = null;
            LineRenderer stroke = null;
            Vector3 lastPt = Vector3.zero;
            paint = Mods.T(
                    c,
                    "Air Paint",
                    () =>
                    {
                        bool draw = !Board.VROpen && !Board.DesktopOpen && (Inp.RTrig.Held || Inp.Key(Key.V));
                        if (!draw)
                        {
                            stroke = null;
                            return;
                        }
                        var pt = PaintPoint;
                        if (stroke == null)
                        {
                            stroke = Fx.Line(Fx.Accent, 0.02f, false);
                            stroke.positionCount = 0;
                            strokes.Add(stroke);
                            lastPt = pt + Vector3.one;
                        }
                        if ((pt - lastPt).sqrMagnitude < 0.0004f)
                            return;
                        lastPt = pt;
                        stroke.positionCount++;
                        stroke.SetPosition(stroke.positionCount - 1, pt);
                        stroke.material.color =
                            paint.Opt == 1 ? Fx.Rainbow(0.3f)
                            : paint.Opt == 2 ? Color.white
                            : Fx.Accent;
                    }
                )
                .Opts("Paint Colour", "Accent", "Rainbow", "White")
                .Info("Hold right trigger (desktop: V) to draw lines in the air");
            Mods.B(
                    c,
                    "Clear Paint",
                    () =>
                    {
                        foreach (var s in strokes)
                            Fx.Kill(s);
                        strokes.Clear();
                    }
                )
                .Info("Erases everything you've drawn");

            Mod block = null;
            block = Mods.T(
                    c,
                    "Place Blocks",
                    () =>
                    {
                        if (!Gun.Get(out var p, out _) || !Gun.FireDown)
                            return;
                        var b = Fx.Prim(
                            PrimitiveType.Cube,
                            Vector3.one * 0.5f,
                            new[]
                            {
                                Fx.Accent,
                                new Color(0.6f, 0.4f, 0.2f),
                                new Color(0.3f, 0.8f, 0.3f),
                                Color.white,
                                Fx.Rainbow(0.1f, Random.value),
                            }[block.Opt],
                            collider: true
                        );
                        b.transform.position = new Vector3(
                            Mathf.Round(p.x * 2f) / 2f,
                            Mathf.Round(p.y * 2f) / 2f + 0.25f,
                            Mathf.Round(p.z * 2f) / 2f
                        );
                        blocks.Add(b);
                        if (blocks.Count > 200)
                        {
                            Fx.Kill(blocks[0]);
                            blocks.RemoveAt(0);
                        }
                    }
                )
                .Opts("Block Type", "Neon", "Wood", "Grass", "Snow", "Random")
                .Info("Fire the gun to place solid blocks you can climb (only you see them)");
            Mods.T(
                    c,
                    "Remove Block Gun",
                    () =>
                    {
                        if (!Gun.Get(out var p, out _) || !Gun.FireDown)
                            return;
                        var b = blocks.Where(x => x != null).OrderBy(x => (x.transform.position - p).sqrMagnitude).FirstOrDefault();
                        if (b != null && (b.transform.position - p).magnitude < 0.8f)
                        {
                            blocks.Remove(b);
                            Fx.Kill(b);
                        }
                    }
                )
                .Info("Fire at one of your blocks to remove it");
            Mods.B(c, "Clear Blocks", () => Fx.Kill(blocks)).Info("Removes all your blocks");

            Mods.B(
                    c,
                    "Confetti Cannon",
                    () =>
                    {
                        for (int i = 0; i < 25; i++)
                            Spark(
                                Game.RHand.position,
                                (Aim(Game.RHand) * 2f + Random.insideUnitSphere).normalized * 10f,
                                Color.HSVToRGB(Random.value, 0.8f, 1f)
                            );
                    }
                )
                .Info("Blasts confetti out of your right hand");
            Mods.T(
                    c,
                    "Bubble Blower",
                    () =>
                    {
                        if (!(Inp.LTrig.Held || Inp.Key(Key.Y)) || Random.value > 0.3f)
                            return;
                        var b = Toy(PrimitiveType.Sphere, Random.Range(0.08f, 0.2f), new Color(0.7f, 0.9f, 1f, 0.4f));
                        b.transform.position = Inp.VR ? Game.LHand.position : PaintPoint;
                        var rb = b.GetComponent<Rigidbody>();
                        rb.useGravity = false;
                        rb.linearVelocity = Aim(Inp.VR ? Game.LHand : Game.Head) * 2f + Vector3.up * 0.5f;
                        Object.Destroy(b, 6f);
                    }
                )
                .Info("Hold left trigger (desktop: Y) to blow floating bubbles");
            Mods.B(
                    c,
                    "Balloon",
                    () =>
                    {
                        var b = Toy(PrimitiveType.Sphere, 0.35f, Color.HSVToRGB(Random.value, 0.8f, 1f));
                        b.transform.position = Game.Head.position + Vector3.up * 0.5f;
                        var rb = b.GetComponent<Rigidbody>();
                        rb.useGravity = false;
                        rb.linearVelocity = Vector3.up * 0.8f;
                        Object.Destroy(b, 30f);
                    }
                )
                .Info("Releases a balloon that floats away");

            GameObject sword = null;
            Mods.T(
                    c,
                    "Sword",
                    () =>
                    {
                        if (sword == null)
                            sword = Fx.Prim(PrimitiveType.Cube, new Vector3(0.03f, 0.03f, 0.9f), new Color(0.8f, 0.85f, 0.9f));
                        var h = Game.RHand;
                        var dir = Aim(h);
                        sword.transform.position = h.position + dir * 0.45f * Game.Scale;
                        sword.transform.rotation = Quaternion.LookRotation(dir);
                        sword.transform.localScale = new Vector3(0.03f, 0.03f, 0.9f) * Game.Scale;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(sword);
                        sword = null;
                    }
                )
                .Info("A sword in your right hand");
            GameObject saber = null;
            Mods.T(
                    c,
                    "Lightsaber",
                    () =>
                    {
                        if (saber == null)
                        {
                            saber = Fx.Prim(PrimitiveType.Cylinder, new Vector3(0.04f, 0.5f, 0.04f), Fx.Accent);
                            var l = saber.AddComponent<Light>();
                            l.range = 2f;
                            l.intensity = 2f;
                        }
                        var h = Game.RHand;
                        var dir = Aim(h);
                        saber.transform.position = h.position + dir * 0.5f * Game.Scale;
                        saber.transform.up = dir;
                        saber.transform.localScale = new Vector3(0.04f, 0.5f, 0.04f) * Game.Scale;
                        saber.GetComponent<Renderer>().material.color = Fx.Accent;
                        saber.GetComponent<Light>().color = Fx.Accent;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(saber);
                        saber = null;
                    }
                )
                .Info("A glowing lightsaber in your right hand");

            var clone = new List<GameObject>();
            var history = new Queue<(Vector3 h, Vector3 l, Vector3 r)>();
            Mods.T(
                    c,
                    "Shadow Clone",
                    () =>
                    {
                        history.Enqueue((Game.Head.position, Game.LHand.position, Game.RHand.position));
                        if (history.Count < 60)
                            return;
                        var p = history.Dequeue();
                        clone[0].transform.position = p.h;
                        clone[1].transform.position = p.l;
                        clone[2].transform.position = p.r;
                        clone[3].transform.position = p.h - Vector3.up * 0.35f * Game.Scale;
                    },
                    () =>
                    {
                        history.Clear();
                        clone.Add(Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.25f, new Color(0.1f, 0.1f, 0.15f, 0.7f)));
                        clone.Add(Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.1f, new Color(0.1f, 0.1f, 0.15f, 0.7f)));
                        clone.Add(Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.1f, new Color(0.1f, 0.1f, 0.15f, 0.7f)));
                        clone.Add(Fx.Prim(PrimitiveType.Capsule, new Vector3(0.35f, 0.25f, 0.35f), new Color(0.1f, 0.1f, 0.15f, 0.7f)));
                    },
                    () => Fx.Kill(clone)
                )
                .Info("A dark copy of you copies your moves one second behind");

            Mods.B(
                    c,
                    "Snowman",
                    () =>
                    {
                        var at = Game.P.transform.position + Game.Head.forward * 1.5f;
                        float[] sizes = { 0.6f, 0.45f, 0.3f };
                        float y = -0.9f;
                        foreach (var s in sizes)
                        {
                            var b = Toy(PrimitiveType.Sphere, s, Color.white, false);
                            y += s * 0.85f;
                            b.transform.position = at + Vector3.up * y;
                        }
                    }
                )
                .Info("Builds a snowman in front of you");
            Mods.T(
                    c,
                    "Dance Floor",
                    () =>
                    {
                        if (toys.Count(t => t != null && t.name == "JClientTile") == 0)
                            for (int x = -3; x <= 3; x++)
                            for (int z = -3; z <= 3; z++)
                            {
                                var tile = Toy(PrimitiveType.Cube, 1f, Color.black, false);
                                tile.name = "JClientTile";
                                tile.transform.localScale = new Vector3(0.95f, 0.02f, 0.95f);
                                tile.transform.position = Game.P.transform.position + new Vector3(x, -1.05f, z);
                            }
                        foreach (var t in toys)
                            if (t != null && t.name == "JClientTile")
                                t.GetComponent<Renderer>().material.color = Color.HSVToRGB(
                                    Mathf.Repeat(
                                        Mathf.Floor(Time.time * 2f) * 0.13f
                                            + t.transform.position.x * 0.11f
                                            + t.transform.position.z * 0.07f,
                                        1f
                                    ),
                                    0.9f,
                                    1f
                                );
                    },
                    null,
                    () =>
                    {
                        foreach (var t in toys.Where(t => t != null && t.name == "JClientTile").ToList())
                        {
                            toys.Remove(t);
                            Fx.Kill(t);
                        }
                    }
                )
                .Info("A flashing disco dance floor appears under you");
            float show = 0f;
            Mods.T(
                    c,
                    "Fireworks Show",
                    () =>
                    {
                        if ((show -= Time.deltaTime) > 0f)
                            return;
                        show = Random.Range(0.4f, 1.2f);
                        var at = Game.Head.position + new Vector3(Random.Range(-15f, 15f), Random.Range(8f, 16f), Random.Range(-15f, 15f));
                        var col = Color.HSVToRGB(Random.value, 0.8f, 1f);
                        for (int i = 0; i < 24; i++)
                            Spark(at, Random.onUnitSphere * 7f, col);
                    }
                )
                .Info("Fireworks keep going off in the sky around you");
        }
    }
}
