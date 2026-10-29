using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // One local visual object per other player, created/removed as players come and go.
    public class RigPool
    {
        readonly Dictionary<VRRig, GameObject> map = new Dictionary<VRRig, GameObject>();
        readonly Func<VRRig, GameObject> make;
        readonly Action<VRRig, GameObject> update;

        public RigPool(Func<VRRig, GameObject> make, Action<VRRig, GameObject> update)
        {
            this.make = make;
            this.update = update;
        }

        public void Tick()
        {
            var live = new HashSet<VRRig>(Game.Others);
            foreach (var r in map.Keys.Where(r => r == null || !live.Contains(r)).ToList())
            {
                Fx.Kill(map[r]);
                map.Remove(r);
            }
            foreach (var r in live)
            {
                if (!map.TryGetValue(r, out var g) || g == null)
                    map[r] = g = make(r);
                update(r, g);
            }
        }

        public void Clear()
        {
            foreach (var g in map.Values)
                Fx.Kill(g);
            map.Clear();
        }
    }

    public static class Visuals
    {
        static float fps;

        static Vector3 HeadOf(VRRig r) =>
            r.head != null && r.head.rigTarget != null ? r.head.rigTarget.position : r.transform.position + Vector3.up * 0.3f;

        static Transform View => Inp.VR ? Game.Head : (Game.DesktopCam != null ? Game.DesktopCam.transform : Game.Head);

        static void Tint(GameObject g, Color c)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
                r.material.color = c;
        }

        static Mod Pool(string name, Func<VRRig, GameObject> make, Action<VRRig, GameObject> update, string info)
        {
            var pool = new RigPool(make, update);
            return Mods.T("Visuals", name, pool.Tick, null, pool.Clear).Info(info);
        }

        public static Mod HudLine(string cat, string name, Func<string> line, string info)
        {
            var m = Mods.T(cat, name).Info(info);
            Hud.Lines.Add(() => m.On ? line() : null);
            return m;
        }

        static readonly List<GameObject> contacts = new List<GameObject>();
        static readonly Dictionary<string, Color> ambientBackup = new Dictionary<string, Color>();
        static UnityEngine.Rendering.AmbientMode ambientMode;
        static bool fogBackup;
        static float fovBackup = -1;
        static LineRenderer arc;

        public static void Register()
        {
            const string c = "Visuals";
            Pool(
                "Chams",
                r =>
                    Fx.Prim(
                        PrimitiveType.Capsule,
                        new Vector3(0.45f, 0.4f, 0.45f),
                        Settings.AccentColor * new Color(1, 1, 1, 0.5f),
                        false,
                        true
                    ),
                (r, g) =>
                {
                    g.transform.position = r.transform.position;
                    g.transform.localScale = new Vector3(0.45f, 0.4f, 0.45f) * r.scaleFactor;
                    Tint(g, Settings.Esp(r) * new Color(1, 1, 1, 0.5f));
                },
                "See every player's body through walls"
            );
            Pool(
                "Box ESP",
                r =>
                {
                    var l = Fx.Line(Settings.AccentColor, 0.01f);
                    l.positionCount = 5;
                    return l.gameObject;
                },
                (r, g) =>
                {
                    var l = g.GetComponent<LineRenderer>();
                    var right = View.right * 0.35f * r.scaleFactor;
                    var up = Vector3.up * 0.55f * r.scaleFactor;
                    var p = r.transform.position;
                    l.SetPositions(new[] { p - right - up, p + right - up, p + right + up, p - right + up, p - right - up });
                    l.material.color = Settings.Esp(r);
                },
                "A box around every player, visible through walls"
            );
            Pool(
                "Tracers",
                r => Fx.Line(Settings.AccentColor, 0.006f).gameObject,
                (r, g) =>
                {
                    var l = g.GetComponent<LineRenderer>();
                    l.SetPosition(0, Game.RHand.position);
                    l.SetPosition(1, r.transform.position);
                    l.material.color = Settings.Esp(r);
                },
                "Lines from your hand to every player"
            );
            Pool(
                "Head ESP",
                r => Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.3f, Settings.AccentColor, false, true),
                (r, g) =>
                {
                    g.transform.position = HeadOf(r);
                    g.transform.localScale = Vector3.one * 0.3f * r.scaleFactor;
                    Tint(g, Settings.Esp(r));
                },
                "Glowing heads through walls"
            );
            Pool(
                "Skeleton ESP",
                r =>
                {
                    var l = Fx.Line(Settings.AccentColor, 0.008f);
                    l.positionCount = 4;
                    return l.gameObject;
                },
                (r, g) =>
                {
                    var l = g.GetComponent<LineRenderer>();
                    l.SetPositions(new[] { r.leftHandTransform.position, r.transform.position, HeadOf(r), r.rightHandTransform.position });
                    l.material.color = Settings.Esp(r);
                },
                "Stick-figure lines for head, body and hands through walls"
            );
            Pool(
                "Nametags",
                r =>
                {
                    var g = new GameObject("JClientTag");
                    Fx.Spawned.Add(g);
                    var t = Fx.Text(g.transform, "", 0.025f, Settings.AccentColor);
                    t.GetComponent<MeshRenderer>().material.renderQueue = 4000;
                    return g;
                },
                (r, g) =>
                {
                    g.transform.position = HeadOf(r) + Vector3.up * 0.35f * r.scaleFactor;
                    g.transform.rotation = Quaternion.LookRotation(g.transform.position - View.position);
                    var t = g.GetComponentInChildren<TextMesh>();
                    t.text =
                        Game.NameOf(r)
                        + "\n"
                        + Vector3.Distance(Game.Head.position, r.transform.position).ToString("0.0")
                        + "m"
                        + (Game.IsTagged(r) ? "  TAGGED" : "");
                    t.color = Settings.Esp(r);
                },
                "Name, distance and tag state above every player"
            );

            HudLine(c, "FPS Counter", () => "FPS " + fps.ToString("0"), "Frames per second on the HUD").Tick = () =>
                fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            HudLine(
                c,
                "Speedometer",
                () => "Speed " + Game.Body.linearVelocity.magnitude.ToString("0.0") + " m/s",
                "Your speed on the HUD"
            );
            HudLine(
                c,
                "Coordinates",
                () =>
                {
                    var p = Game.P.transform.position;
                    return "XYZ " + p.x.ToString("0") + " " + p.y.ToString("0") + " " + p.z.ToString("0");
                },
                "Your position on the HUD"
            );
            HudLine(c, "Clock", () => DateTime.Now.ToString("h:mm tt"), "Real-world time on the HUD");

            Mods.T(
                    c,
                    "Fullbright",
                    () =>
                    {
                        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                        RenderSettings.ambientLight = Color.white;
                    },
                    () =>
                    {
                        ambientMode = RenderSettings.ambientMode;
                        ambientBackup["a"] = RenderSettings.ambientLight;
                    },
                    () =>
                    {
                        RenderSettings.ambientMode = ambientMode;
                        if (ambientBackup.TryGetValue("a", out var a))
                            RenderSettings.ambientLight = a;
                    }
                )
                .Info("Max ambient light everywhere");
            Mods.T(
                    c,
                    "Night Vision",
                    () =>
                    {
                        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                        RenderSettings.ambientLight = new Color(0.4f, 1f, 0.5f);
                    },
                    () =>
                    {
                        ambientMode = RenderSettings.ambientMode;
                        ambientBackup["n"] = RenderSettings.ambientLight;
                    },
                    () =>
                    {
                        RenderSettings.ambientMode = ambientMode;
                        if (ambientBackup.TryGetValue("n", out var a))
                            RenderSettings.ambientLight = a;
                    }
                )
                .Info("Green-tinted bright vision");
            Mods.T(
                    c,
                    "No Fog",
                    () => RenderSettings.fog = false,
                    () => fogBackup = RenderSettings.fog,
                    () => RenderSettings.fog = fogBackup
                )
                .Info("Removes distance fog");

            Mod fov = null;
            fov = Mods.T(
                    c,
                    "Desktop FOV",
                    () =>
                    {
                        var cam = Game.DesktopCam;
                        if (cam == null || Inp.VR)
                            return;
                        if (fovBackup < 0)
                            fovBackup = cam.fieldOfView;
                        cam.fieldOfView = new[] { 70f, 90f, 110f, 130f }[fov.Opt];
                    },
                    null,
                    () =>
                    {
                        var cam = Game.DesktopCam;
                        if (cam != null && fovBackup > 0 && !Inp.VR)
                            cam.fieldOfView = fovBackup;
                        fovBackup = -1;
                    }
                )
                .Opts("FOV", "70", "90", "110", "130")
                .Def(1)
                .Info("Desktop camera field of view");
            Mods.T(
                    c,
                    "Zoom",
                    () =>
                    {
                        var cam = Game.DesktopCam;
                        if (cam == null || Inp.VR)
                            return;
                        if (Inp.Key(Key.C))
                        {
                            if (fovBackup < 0)
                                fovBackup = cam.fieldOfView;
                            cam.fieldOfView = 20f;
                        }
                        else if (fovBackup > 0 && !fov.On)
                        {
                            cam.fieldOfView = fovBackup;
                            fovBackup = -1;
                        }
                    }
                )
                .Info("Desktop: hold C to zoom");

            Mods.T(
                    c,
                    "Trajectory Line",
                    () =>
                    {
                        if (arc == null)
                        {
                            arc = Fx.Line(Settings.AccentColor, 0.015f, false);
                            arc.positionCount = 30;
                        }
                        var p = Game.Head.position;
                        var v = Game.Body.linearVelocity;
                        for (int i = 0; i < 30; i++)
                        {
                            float t = i * 0.05f;
                            arc.SetPosition(i, p + v * t + 0.5f * Physics.gravity * t * t);
                        }
                        arc.material.color = Settings.AccentColor;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(arc);
                        arc = null;
                    }
                )
                .Info("Shows where your current momentum will carry you");
            Mods.T(
                    c,
                    "Hand Contact Markers",
                    () =>
                    {
                        foreach (var h in new[] { Game.P.LeftHand, Game.P.RightHand })
                        {
                            if (!h.isColliding || h.wasColliding)
                                continue;
                            var m = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.06f, Settings.AccentColor);
                            m.transform.position = h.lastHitInfo.point;
                            contacts.Add(m);
                            if (contacts.Count > 60)
                            {
                                Fx.Kill(contacts[0]);
                                contacts.RemoveAt(0);
                            }
                        }
                    },
                    null,
                    () => Fx.Kill(contacts)
                )
                .Info("Drops a dot wherever your hands hit a surface");
        }
    }
}
