using System;
using System.Collections.Generic;
using UnityEngine;

namespace JClient
{
    // Local-only looks: nothing here unlocks or changes purchased cosmetics.
    public static class Cosmetics
    {
        static readonly Color[] colours =
        {
            new Color(0f, 0.9f, 1f),
            new Color(1f, 0.2f, 0.2f),
            new Color(0.2f, 1f, 0.4f),
            new Color(0.7f, 0.3f, 1f),
            Color.white,
            new Color(0.08f, 0.08f, 0.1f),
        };

        static void Paint(Color c)
        {
            var r = Game.Me.mainSkin;
            if (r != null)
                r.material.color = c;
        }

        static void Unpaint()
        {
            var r = Game.Me.mainSkin;
            if (r != null)
                r.material.color = Game.Me.playerColor;
        }

        // Spawns objects on enable, keeps them glued to targets every LateUpdate, removes them on disable.
        static Mod Attached(string name, Func<List<GameObject>> make, Action<List<GameObject>> follow, string info)
        {
            var objs = new List<GameObject>();
            return Mods.T("Cosmetics", name, null, () => objs.AddRange(make()), () => Fx.Kill(objs))
                .OnLate(() =>
                {
                    if (objs.Count > 0 && objs[0] != null)
                        follow(objs);
                })
                .Info(info);
        }

        static GameObject Trail(float width, float time)
        {
            var g = new GameObject("JClientTrail");
            Fx.Spawned.Add(g);
            var t = g.AddComponent<TrailRenderer>();
            t.time = time;
            t.startWidth = width;
            t.endWidth = 0f;
            t.material = Fx.Mat(Color.white, false, true);
            t.startColor = Fx.Accent;
            t.endColor = new Color(0f, 0.3f, 1f, 0f);
            return g;
        }

        static GameObject Glow(float range)
        {
            var g = new GameObject("JClientGlow");
            Fx.Spawned.Add(g);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = 2f;
            l.color = Fx.Accent;
            return g;
        }

        static Vector3 Up => Vector3.up * Game.Scale;
        static Vector3 HeadTop => Game.Head.position + Up * 0.25f;

        public static void Register()
        {
            const string c = "Cosmetics";
            Mod colour = null,
                mat = null,
                heads = null;
            var bigHeads = new Dictionary<VRRig, Vector3>();

            Mods.T(c, "Rainbow Monke", null, null, Unpaint)
                .OnLate(() => Paint(Fx.Rainbow(0.2f)))
                .Info("Your fur cycles through colours (only you see it)");
            colour = Mods.T(c, "Monke Colour", null, null, Unpaint)
                .OnLate(() => Paint(colours[colour.Opt]))
                .Opts("Colour", "Cyan", "Red", "Green", "Purple", "White", "Black")
                .Info("Local fur colour");
            mat = Mods.T(c, "Body Material", null, null, () => Game.Me.ChangeMaterialLocal(0))
                .OnLate(() => Game.Me.ChangeMaterialLocal(mat.Opt + 1))
                .Opts("Material", "1", "2", "3", "4", "5", "6")
                .Info("Local body material from the game's own set");
            Mods.T(
                    c,
                    "Hide My Body",
                    () => Game.Me.mainSkin.forceRenderingOff = true,
                    null,
                    () => Game.Me.mainSkin.forceRenderingOff = false
                )
                .Info("Hides your own body in mirrors and third person (local)");

            Attached(
                "Hand Trails",
                () => new List<GameObject> { Trail(0.05f, 0.4f), Trail(0.05f, 0.4f) },
                l =>
                {
                    l[0].transform.position = Game.LHand.position;
                    l[1].transform.position = Game.RHand.position;
                },
                "Cyan trails behind your hands"
            );
            Attached(
                "Body Trail",
                () => new List<GameObject> { Trail(0.3f, 1.2f) },
                l => l[0].transform.position = Game.P.transform.position,
                "A long trail behind your body"
            );
            Attached(
                "Glow Hands",
                () => new List<GameObject> { Glow(2.5f), Glow(2.5f) },
                l =>
                {
                    l[0].transform.position = Game.LHand.position;
                    l[1].transform.position = Game.RHand.position;
                },
                "Your hands light up the world around them"
            );
            Attached(
                "Halo",
                () =>
                {
                    var g = Fx.Prim(PrimitiveType.Cylinder, new Vector3(0.28f, 0.004f, 0.28f), Color.white);
                    return new List<GameObject> { g };
                },
                l =>
                {
                    l[0].transform.position = HeadTop;
                    l[0].transform.localScale = new Vector3(0.28f, 0.004f, 0.28f) * Game.Scale;
                },
                "A glowing halo over your head"
            );
            Attached(
                "Crown",
                () =>
                {
                    var list = new List<GameObject>();
                    for (int i = 0; i < 5; i++)
                        list.Add(Fx.Prim(PrimitiveType.Cube, new Vector3(0.03f, 0.07f, 0.03f), new Color(1f, 0.85f, 0.2f)));
                    return list;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        float a = i / (float)l.Count * Mathf.PI * 2f + Game.Head.eulerAngles.y * Mathf.Deg2Rad;
                        l[i].transform.position = HeadTop + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.1f * Game.Scale;
                    }
                },
                "A gold crown"
            );
            Attached(
                "Hand Orbs",
                () =>
                {
                    var list = new List<GameObject>();
                    for (int i = 0; i < 6; i++)
                        list.Add(Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.03f, Fx.Accent));
                    return list;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        var hand = i < 3 ? Game.LHand : Game.RHand;
                        float a = Time.time * 4f + i % 3 * 2.09f;
                        l[i].transform.position =
                            hand.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.7f) * 0.5f, Mathf.Sin(a)) * 0.09f * Game.Scale;
                    }
                },
                "Little orbs circling your hands"
            );
            Attached(
                "Cyan Aura",
                () => new List<GameObject> { Fx.Particles(Fx.Accent, 40, 0.03f, 0.1f, 1.5f, 0.5f).gameObject },
                l => l[0].transform.position = Game.P.transform.position,
                "Sparkles floating around you"
            );
            Attached(
                "Sparkle Hands",
                () =>
                    new List<GameObject>
                    {
                        Fx.Particles(Color.white, 25, 0.02f, 0.3f, 0.6f, 0.03f).gameObject,
                        Fx.Particles(Color.white, 25, 0.02f, 0.3f, 0.6f, 0.03f).gameObject,
                    },
                l =>
                {
                    l[0].transform.position = Game.LHand.position;
                    l[1].transform.position = Game.RHand.position;
                },
                "Your hands sparkle"
            );
            Attached(
                "Wings",
                () =>
                    new List<GameObject>
                    {
                        Fx.Prim(PrimitiveType.Cube, new Vector3(0.5f, 0.02f, 0.25f), Fx.Accent * new Color(1, 1, 1, 0.6f)),
                        Fx.Prim(PrimitiveType.Cube, new Vector3(0.5f, 0.02f, 0.25f), Fx.Accent * new Color(1, 1, 1, 0.6f)),
                    },
                l =>
                {
                    var body = Game.P.bodyCollider.transform;
                    float flap = Mathf.Sin(Time.time * 6f) * 25f;
                    var back = body.position - body.forward * 0.15f * Game.Scale + Up * 0.15f;
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1 : 1;
                        l[i].transform.rotation = body.rotation * Quaternion.Euler(0, 0, side * (20f + flap));
                        l[i].transform.position = back + body.right * side * 0.27f * Game.Scale;
                        l[i].transform.localScale = new Vector3(0.5f, 0.02f, 0.25f) * Game.Scale;
                    }
                },
                "Flapping wings on your back"
            );

            heads = Mods.T(
                    c,
                    "Big Heads",
                    () =>
                    {
                        float s = new[] { 1.5f, 2f, 3f }[heads.Opt];
                        foreach (var r in Game.Others)
                        {
                            if (r.headMesh == null)
                                continue;
                            if (!bigHeads.ContainsKey(r))
                                bigHeads[r] = r.headMesh.transform.localScale;
                            r.headMesh.transform.localScale = bigHeads[r] * s;
                        }
                    },
                    null,
                    () =>
                    {
                        foreach (var kv in bigHeads)
                            if (kv.Key != null && kv.Key.headMesh != null)
                                kv.Key.headMesh.transform.localScale = kv.Value;
                        bigHeads.Clear();
                    }
                )
                .Opts("Head Size", "1.5x", "2x", "3x")
                .Info("Everyone else gets a big head (only you see it)");
            Mods.T(
                    c,
                    "Cyan Nametags",
                    () =>
                    {
                        foreach (var r in Game.Others)
                            if (r.playerText1 != null)
                                r.playerText1.color = Fx.Accent;
                    },
                    null,
                    () =>
                    {
                        foreach (var r in Game.Others)
                            if (r.playerText1 != null)
                                r.playerText1.color = Color.white;
                    }
                )
                .Info("Recolours everyone's name tag locally");
        }
    }
}
