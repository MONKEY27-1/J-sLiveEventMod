using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace JClient
{
    // Second batch of local visuals and HUD lines.
    public static class VisualsPlus
    {
        static Vector3 HeadOf(VRRig r) =>
            r.head != null && r.head.rigTarget != null ? r.head.rigTarget.position : r.transform.position + Vector3.up * 0.3f;

        static Mod Pool(string name, Func<VRRig, GameObject> make, Action<VRRig, GameObject> update, string info)
        {
            var pool = new RigPool(make, update);
            return Mods.T("Visuals", name, pool.Tick, null, pool.Clear).Info(info);
        }

        static GameObject NewLine(float w, int points = 2)
        {
            var l = Fx.Line(Settings.AccentColor, w);
            l.positionCount = points;
            return l.gameObject;
        }

        static LineRenderer L(GameObject g) => g.GetComponent<LineRenderer>();

        public static void Register()
        {
            const string c = "Visuals";
            var lastPos = new Dictionary<VRRig, Vector3>();
            Pool(
                "Velocity ESP",
                r => NewLine(0.012f),
                (r, g) =>
                {
                    var p = r.transform.position;
                    lastPos.TryGetValue(r, out var was);
                    lastPos[r] = p;
                    var v = (p - was) / Mathf.Max(Time.deltaTime, 0.001f);
                    if (v.magnitude > 30f)
                        v = Vector3.zero;
                    L(g).SetPosition(0, p);
                    L(g).SetPosition(1, p + v * 0.4f);
                    L(g).material.color = Settings.Esp(r);
                },
                "A line from each player showing where they're heading and how fast"
            );
            Pool(
                "Beacons",
                r => NewLine(0.05f),
                (r, g) =>
                {
                    L(g).SetPosition(0, r.transform.position);
                    L(g).SetPosition(1, r.transform.position + Vector3.up * 60f);
                    L(g).material.color = Settings.Esp(r);
                },
                "A tall light beam above every player, visible from anywhere"
            );
            Pool(
                "Infection Tracers",
                r => NewLine(0.008f),
                (r, g) =>
                {
                    bool show = Game.IsTagged(r);
                    g.SetActive(show);
                    if (!show)
                        return;
                    L(g).SetPosition(0, Game.RHand.position);
                    L(g).SetPosition(1, r.transform.position);
                    L(g).material.color = new Color(1f, 0.25f, 0.2f);
                },
                "Tracers only to tagged players"
            );
            Pool(
                "Runner Tracers",
                r => NewLine(0.008f),
                (r, g) =>
                {
                    bool show = !Game.IsTagged(r);
                    g.SetActive(show);
                    if (!show)
                        return;
                    L(g).SetPosition(0, Game.RHand.position);
                    L(g).SetPosition(1, r.transform.position);
                    L(g).material.color = new Color(0.2f, 1f, 0.5f);
                },
                "Tracers only to untagged players"
            );
            Pool(
                "Arrow ESP",
                r => NewLine(0.02f),
                (r, g) =>
                {
                    var me = Game.Head.position - Vector3.up * 0.25f;
                    var dir = r.transform.position - me;
                    dir.y = 0;
                    dir.Normalize();
                    L(g).SetPosition(0, me + dir * 0.35f);
                    L(g).SetPosition(1, me + dir * 0.5f);
                    L(g).material.color = Settings.Esp(r);
                },
                "Short arrows around your chest pointing at every player"
            );
            Pool(
                "Look Direction ESP",
                r => NewLine(0.006f),
                (r, g) =>
                {
                    var h = r.head.rigTarget;
                    L(g).SetPosition(0, h.position);
                    L(g).SetPosition(1, h.position + h.forward * 3f);
                    L(g).material.color = Settings.Esp(r);
                },
                "Shows where every player is looking"
            );
            Pool(
                "Hand ESP",
                r =>
                {
                    var g = new GameObject("JClientHands");
                    Fx.Spawned.Add(g);
                    for (int i = 0; i < 2; i++)
                        Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.12f, Settings.AccentColor, false, true)
                            .transform.SetParent(g.transform, true);
                    return g;
                },
                (r, g) =>
                {
                    g.transform.GetChild(0).position = r.leftHandTransform.position;
                    g.transform.GetChild(1).position = r.rightHandTransform.position;
                    foreach (var rend in g.GetComponentsInChildren<Renderer>())
                        rend.material.color = Settings.Esp(r);
                },
                "Glowing hands on every player through walls"
            );
            Pool(
                "Tag Range ESP",
                r => Fx.Prim(PrimitiveType.Sphere, Vector3.one, new Color(1f, 0.2f, 0.2f, 0.12f)),
                (r, g) =>
                {
                    bool show = Game.IsTagged(r);
                    g.SetActive(show);
                    if (!show)
                        return;
                    g.transform.position = r.transform.position;
                    g.transform.localScale = Vector3.one * Game.T.maxTagDistance * 2f * r.scaleFactor;
                },
                "A red bubble around taggers showing roughly how far they can reach"
            );
            Pool(
                "Proximity Rings",
                r =>
                {
                    var l = Fx.Line(Settings.AccentColor, 0.02f, false);
                    l.positionCount = 25;
                    l.loop = true;
                    return l.gameObject;
                },
                (r, g) =>
                {
                    float d = Vector3.Distance(r.transform.position, Game.Head.position);
                    var col =
                        d < 3f ? new Color(1f, 0.2f, 0.2f)
                        : d < 8f ? new Color(1f, 0.8f, 0.2f)
                        : new Color(0.2f, 1f, 0.5f);
                    var p = r.transform.position - Vector3.up * 0.55f * r.scaleFactor;
                    for (int i = 0; i < 25; i++)
                    {
                        float a = i / 25f * Mathf.PI * 2f;
                        L(g).SetPosition(i, p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f * r.scaleFactor);
                    }
                    L(g).material.color = col;
                },
                "A ring under each player: red = close, yellow = medium, green = far"
            );

            Hud.Draws.Add(() =>
            {
                if (!Mods.Get("Crosshair").On || Inp.VR)
                    return;
                float x = Screen.width / 2f,
                    y = Screen.height / 2f;
                GUI.color = Fx.Accent;
                GUI.DrawTexture(new Rect(x - 10, y - 1, 20, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 1, y - 10, 2, 20), Texture2D.whiteTexture);
                GUI.color = Color.white;
            });
            Mods.T(c, "Crosshair").Info("A cyan crosshair in the middle of the desktop window");

            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            Visuals.HudLine(
                c,
                "Compass",
                () =>
                    "Facing "
                    + dirs[Mathf.RoundToInt(Mathf.Repeat(Game.Head.eulerAngles.y, 360f) / 45f) % 8]
                    + " ("
                    + Game.Head.eulerAngles.y.ToString("0")
                    + ")",
                "Which way you're facing"
            );
            Visuals.HudLine(c, "Ping", () => Game.InRoom ? "Ping " + PhotonNetwork.GetPing() + " ms" : null, "Your connection ping");
            Visuals.HudLine(c, "Server Region", () => "Region " + (PhotonNetwork.CloudRegion ?? "-"), "Which server region you're on");
            Visuals.HudLine(c, "Height", () => "Height " + Game.P.transform.position.y.ToString("0.0") + " m", "Your height in the map");
            float joined = -1f;
            Visuals.HudLine(
                c,
                "Room Timer",
                () =>
                {
                    if (!Game.InRoom)
                    {
                        joined = -1f;
                        return null;
                    }
                    if (joined < 0)
                        joined = Time.time;
                    return "In room " + TimeSpan.FromSeconds(Time.time - joined).ToString(@"m\:ss");
                },
                "How long you've been in this room"
            );
            Visuals.HudLine(
                c,
                "Nearest Player",
                () =>
                {
                    var r = Game.Nearest(Game.Head.position);
                    return r == null
                        ? null
                        : "Nearest "
                            + Game.NameOf(r)
                            + " "
                            + Vector3.Distance(r.transform.position, Game.Head.position).ToString("0.0")
                            + "m";
                },
                "Name and distance of the closest player"
            );
            Visuals.HudLine(
                c,
                "Tag State",
                () => Game.InRoom ? (Game.MeTagged ? "You are IT" : "You are a runner") : null,
                "Whether you're it"
            );
            Visuals
                .HudLine(
                    c,
                    "Peak Speed",
                    () =>
                    {
                        peak = Mathf.Max(peak, Game.Body.linearVelocity.magnitude);
                        return "Peak speed " + peak.ToString("0.0") + " m/s";
                    },
                    "Fastest you've gone since turning it on"
                )
                .Enable = () => peak = 0f;
        }

        static float peak;
    }
}
