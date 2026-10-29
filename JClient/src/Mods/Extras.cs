using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JClient
{
    // More modules for existing categories: Utilities, Cosmetics, World FX.
    public static class Extras
    {
        static Mod Follow(string cat, string name, Func<List<GameObject>> make, Action<List<GameObject>> follow, string info)
        {
            var objs = new List<GameObject>();
            return Mods.T(cat, name, null, () => objs.AddRange(make()), () => Fx.Kill(objs))
                .OnLate(() =>
                {
                    if (objs.Count > 0 && objs[0] != null)
                        follow(objs);
                })
                .Info(info);
        }

        static Color ambient;
        static AmbientMode mode;
        static bool fog;
        static Color fogC;
        static float fogD;

        static void Save()
        {
            ambient = RenderSettings.ambientLight;
            mode = RenderSettings.ambientMode;
            fog = RenderSettings.fog;
            fogC = RenderSettings.fogColor;
            fogD = RenderSettings.fogDensity;
        }

        static void Restore()
        {
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = mode;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogC;
            RenderSettings.fogDensity = fogD;
        }

        static Mod Mood(string name, Action tick, string info) => Mods.T("World FX", name, tick, Save, Restore).Info(info);

        public static void Register()
        {
            // ---- Utilities ----
            var waypoints = new Vector3?[3];
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                Mods.B(
                        "Utilities",
                        "Save Waypoint " + (n + 1),
                        () =>
                        {
                            waypoints[n] = Game.P.transform.position;
                            Hud.Toast("Waypoint " + (n + 1) + " saved");
                        }
                    )
                    .Info("Saves your position as waypoint " + (n + 1));
                Mods.B(
                        "Utilities",
                        "Go To Waypoint " + (n + 1),
                        () =>
                        {
                            if (waypoints[n].HasValue)
                                Game.Teleport(waypoints[n].Value);
                            else
                                Hud.Toast("Waypoint " + (n + 1) + " not set");
                        }
                    )
                    .Info("Teleports to waypoint " + (n + 1));
            }
            Mods.B(
                    "Utilities",
                    "Undo Teleport",
                    () =>
                    {
                        if (Game.LastTeleportFrom.HasValue)
                            Game.Teleport(Game.LastTeleportFrom.Value);
                        else
                            Hud.Toast("No teleport to undo");
                    }
                )
                .Info("Goes back to where you were before your last teleport");
            Mods.B(
                    "Utilities",
                    "TP To Nearest Player",
                    () =>
                    {
                        var r = Game.Nearest(Game.Head.position);
                        if (r != null)
                            Game.Teleport(r.transform.position + Vector3.up * 0.5f);
                    }
                )
                .Info("Teleports next to the closest player");
            Mods.B(
                    "Utilities",
                    "Copy Position",
                    () =>
                    {
                        var p = Game.P.transform.position;
                        GUIUtility.systemCopyBuffer = p.x.ToString("0.00") + ", " + p.y.ToString("0.00") + ", " + p.z.ToString("0.00");
                        Hud.Toast("Position copied");
                    }
                )
                .Info("Copies your coordinates to the clipboard");
            float dist = 0f;
            Vector3 last = Vector3.zero;
            Visuals
                .HudLine(
                    "Utilities",
                    "Distance Tracker",
                    () =>
                    {
                        var p = Game.P.transform.position;
                        float step = (p - last).magnitude;
                        if (step < 5f)
                            dist += step;
                        last = p;
                        return "Travelled " + (dist >= 1000 ? (dist / 1000f).ToString("0.00") + " km" : dist.ToString("0") + " m");
                    },
                    "How far you've moved since turning it on"
                )
                .Enable = () =>
            {
                dist = 0f;
                last = Game.P.transform.position;
            };

            // ---- Cosmetics (local) ----
            Follow(
                "Cosmetics",
                "Cape",
                () => new List<GameObject> { Fx.Prim(PrimitiveType.Cube, new Vector3(0.45f, 0.7f, 0.02f), new Color(0.6f, 0.05f, 0.1f)) },
                l =>
                {
                    var b = Game.P.bodyCollider.transform;
                    float sway = Mathf.Sin(Time.time * 3f) * 8f + Mathf.Min(Game.Body.linearVelocity.magnitude * 6f, 60f);
                    l[0].transform.rotation = b.rotation * Quaternion.Euler(sway, 0, 0);
                    l[0].transform.position =
                        b.position
                        - b.forward * 0.18f * Game.Scale
                        + Vector3.up * 0.05f * Game.Scale
                        - l[0].transform.up * 0.3f * Game.Scale
                        + Vector3.up * 0.3f * Game.Scale;
                    l[0].transform.localScale = new Vector3(0.45f, 0.7f, 0.02f) * Game.Scale;
                },
                "A red cape that flows behind you"
            );
            Follow(
                "Cosmetics",
                "Cool Glasses",
                () => new List<GameObject> { Fx.Prim(PrimitiveType.Cube, new Vector3(0.22f, 0.05f, 0.02f), Color.black) },
                l =>
                {
                    var h = Game.Head;
                    l[0].transform.position = h.position + h.forward * 0.12f * Game.Scale + h.up * 0.02f * Game.Scale;
                    l[0].transform.rotation = h.rotation;
                    l[0].transform.localScale = new Vector3(0.22f, 0.05f, 0.02f) * Game.Scale;
                },
                "Sunglasses (see them in mirrors / third person)"
            );
            Follow(
                "Cosmetics",
                "Backpack",
                () => new List<GameObject> { Fx.Prim(PrimitiveType.Cube, new Vector3(0.3f, 0.35f, 0.15f), new Color(0.2f, 0.4f, 0.8f)) },
                l =>
                {
                    var b = Game.P.bodyCollider.transform;
                    l[0].transform.position = b.position - b.forward * 0.22f * Game.Scale + Vector3.up * 0.1f * Game.Scale;
                    l[0].transform.rotation = b.rotation;
                    l[0].transform.localScale = new Vector3(0.3f, 0.35f, 0.15f) * Game.Scale;
                },
                "A backpack on your back"
            );
            Follow(
                "Cosmetics",
                "Fire Hands",
                () =>
                    new List<GameObject>
                    {
                        Fx.Particles(new Color(1f, 0.5f, 0.1f), 40, 0.05f, 0.6f, 0.5f, 0.04f).gameObject,
                        Fx.Particles(new Color(1f, 0.5f, 0.1f), 40, 0.05f, 0.6f, 0.5f, 0.04f).gameObject,
                    },
                l =>
                {
                    l[0].transform.position = Game.LHand.position;
                    l[1].transform.position = Game.RHand.position;
                },
                "Your hands are on fire"
            );
            var ghosts = new List<GameObject>();
            float gTimer = 0f;
            Mods.T(
                    "Cosmetics",
                    "Afterimages",
                    () =>
                    {
                        if ((gTimer -= Time.deltaTime) > 0f || Game.Body.linearVelocity.magnitude < 2f)
                            return;
                        gTimer = 0.08f;
                        var g = Fx.Prim(
                            PrimitiveType.Capsule,
                            new Vector3(0.4f, 0.35f, 0.4f) * Game.Scale,
                            Fx.Accent * new Color(1, 1, 1, 0.3f)
                        );
                        g.transform.position = Game.P.bodyCollider.transform.position;
                        ghosts.Add(g);
                        UnityEngine.Object.Destroy(g, 0.5f);
                        ghosts.RemoveAll(x => x == null);
                    },
                    null,
                    () => Fx.Kill(ghosts)
                )
                .Info("Leaves fading copies of yourself when you move fast");

            // ---- World FX (local) ----
            Mood(
                "Sunset Filter",
                () =>
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(1f, 0.55f, 0.35f);
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(1f, 0.5f, 0.3f);
                    RenderSettings.fogDensity = 0.01f;
                },
                "Warm orange sunset lighting"
            );
            Mood(
                "Blackout",
                () =>
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.05f);
                },
                "Almost pitch dark (pairs well with Flashlight)"
            );
            Mood(
                "Pink Haze",
                () =>
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Exponential;
                    RenderSettings.fogColor = new Color(1f, 0.5f, 0.8f);
                    RenderSettings.fogDensity = 0.03f;
                },
                "Dreamy pink fog"
            );
            Mood(
                "Blood Moon",
                () =>
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.5f, 0.08f, 0.08f);
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = new Color(0.3f, 0f, 0f);
                    RenderSettings.fogDensity = 0.02f;
                },
                "Dark red horror lighting"
            );
            // Hides the purple lurker ghost for you only (renderers + sounds); the ghost itself keeps running for the room.
            var ghostParts = new List<Component>();
            float ghostScan = 0f;
            Mods.T(
                    "World FX",
                    "Remove Purple Ghost",
                    () =>
                    {
                        if ((ghostScan -= Time.deltaTime) > 0f)
                            return;
                        ghostScan = 2f; // the ghost can spawn later, so re-scan now and then
                        foreach (var g in UnityEngine.Object.FindObjectsByType<GorillaTagScripts.LurkerGhost>(FindObjectsSortMode.None))
                        {
                            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                                if (!r.forceRenderingOff)
                                {
                                    r.forceRenderingOff = true;
                                    ghostParts.Add(r);
                                }
                            foreach (var a in g.GetComponentsInChildren<AudioSource>(true))
                                if (!a.mute)
                                {
                                    a.mute = true;
                                    ghostParts.Add(a);
                                }
                        }
                    },
                    null,
                    () =>
                    {
                        foreach (var p in ghostParts)
                        {
                            if (p is Renderer r)
                                r.forceRenderingOff = false;
                            else if (p is AudioSource a)
                                a.mute = false;
                        }
                        ghostParts.Clear();
                    }
                )
                .Info("Hides the purple lurker ghost and mutes its sounds (only for you)");

            var rainObjs = new List<GameObject>();
            Mods.T(
                    "World FX",
                    "Heavy Rain",
                    () =>
                    {
                        if (rainObjs.Count > 0 && rainObjs[0] != null)
                            rainObjs[0].transform.position = Game.Head.position + Vector3.up * 10f;
                    },
                    () =>
                    {
                        var ps = Fx.Particles(new Color(0.7f, 0.8f, 1f, 0.6f), 400, 0.03f, 12f, 1.5f, 10f, 2000);
                        var sh = ps.shape;
                        sh.shapeType = ParticleSystemShapeType.Box;
                        sh.scale = new Vector3(20f, 0.1f, 20f);
                        sh.rotation = new Vector3(90f, 0, 0);
                        rainObjs.Add(ps.gameObject);
                    },
                    () => Fx.Kill(rainObjs)
                )
                .Info("A downpour around you (local)");
            var meteors = new List<GameObject>();
            Mods.T(
                    "World FX",
                    "Meteor Shower",
                    () =>
                    {
                        meteors.RemoveAll(m => m == null);
                        if (UnityEngine.Random.value < 0.04f)
                        {
                            var g = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.8f, new Color(1f, 0.6f, 0.2f));
                            var t = g.AddComponent<TrailRenderer>();
                            t.time = 1f;
                            t.startWidth = 0.8f;
                            t.endWidth = 0f;
                            t.material = Fx.Mat(Color.white, false, true);
                            t.startColor = new Color(1f, 0.5f, 0.1f);
                            t.endColor = new Color(1f, 0.2f, 0f, 0f);
                            g.transform.position =
                                Game.Head.position
                                + new Vector3(UnityEngine.Random.Range(-60f, 60f), 80f, UnityEngine.Random.Range(-60f, 60f));
                            var rb = g.AddComponent<Rigidbody>();
                            rb.useGravity = false;
                            rb.linearVelocity = new Vector3(UnityEngine.Random.Range(-15f, 15f), -40f, UnityEngine.Random.Range(-15f, 15f));
                            meteors.Add(g);
                            UnityEngine.Object.Destroy(g, 4f);
                        }
                    },
                    null,
                    () => Fx.Kill(meteors)
                )
                .Info("Flaming meteors streak down from the sky (local)");
        }
    }
}
