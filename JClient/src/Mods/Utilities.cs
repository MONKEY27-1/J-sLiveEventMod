using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    public static class Utilities
    {
        static Vector3? checkpoint;
        static float stopwatch;
        static readonly List<GameObject> crumbs = new List<GameObject>();
        static float crumbTimer;
        static Vector3? tapeA;
        static GameObject light;

        // Free cam state
        static Camera fcCam;
        static Vector3 fcPos;
        static float fcYaw,
            fcPitch;
        static readonly List<Behaviour> fcDisabled = new List<Behaviour>();
        static Vector3 fcOrigLocalPos;
        static Quaternion fcOrigLocalRot;

        public static void Register()
        {
            const string c = "Utilities";
            Mods.T(c, "Free Cam", null, FreeCamOn, FreeCamOff)
                .OnLate(FreeCamTick)
                .Info("Desktop: detach the camera. WASD/Space/Ctrl move, hold right mouse to look, Shift = fast");
            Mods.T(
                    c,
                    "Spectate Gun",
                    () =>
                    {
                        if (!Gun.Get(out _, out var r) || !Gun.Firing || r == null)
                            return;
                        var cam = Game.DesktopCam;
                        if (cam == null || Inp.VR)
                            return;
                        cam.transform.position = r.transform.position - r.transform.forward * 2.5f + Vector3.up * 1f;
                        cam.transform.LookAt(r.transform.position);
                    }
                )
                .Info("Desktop: hold fire on a player to watch them from behind");
            Mods.T(
                    c,
                    "Teleport Gun",
                    () =>
                    {
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                            Game.Teleport(p + Vector3.up * 0.6f * Game.Scale);
                    }
                )
                .Info("Aim and fire to teleport to that spot");
            Mods.B(
                    c,
                    "Set Checkpoint",
                    () =>
                    {
                        checkpoint = Game.P.transform.position;
                        Hud.Toast("Checkpoint saved");
                    }
                )
                .Info("Saves your current position");
            Mods.B(
                    c,
                    "Go To Checkpoint",
                    () =>
                    {
                        if (checkpoint.HasValue)
                            Game.Teleport(checkpoint.Value);
                        else
                            Hud.Toast("No checkpoint yet");
                    }
                )
                .Info("Teleports to the saved position");
            Mods.B(
                    c,
                    "TP To Random Player",
                    () =>
                    {
                        var l = Game.Others.ToList();
                        if (l.Count == 0)
                        {
                            Hud.Toast("Nobody else here");
                            return;
                        }
                        Game.Teleport(l[UnityEngine.Random.Range(0, l.Count)].transform.position + Vector3.up * 0.5f);
                    }
                )
                .Info("Teleports you next to a random player");
            Mods.B(c, "Unstuck", () => Game.Teleport(Game.P.transform.position + Vector3.up * 3f * Game.Scale))
                .Info("Pops you 3 m straight up");
            Mods.B(
                    c,
                    "Screenshot",
                    () =>
                    {
                        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "J CLIENT");
                        Directory.CreateDirectory(dir);
                        var file = Path.Combine(dir, "JClient_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                        ScreenCapture.CaptureScreenshot(file);
                        Hud.Toast("Saved " + Path.GetFileName(file));
                    }
                )
                .Info("Saves a PNG to Pictures\\J CLIENT");
            Visuals
                .HudLine(
                    c,
                    "Stopwatch",
                    () => "Timer " + TimeSpan.FromSeconds(stopwatch).ToString(@"m\:ss\.f"),
                    "Starts timing when enabled"
                )
                .Enable = () => stopwatch = 0f;
            Mods.Get("Stopwatch").Tick = () => stopwatch += Time.deltaTime;
            Mods.T(
                    c,
                    "Breadcrumbs",
                    () =>
                    {
                        if ((crumbTimer += Time.deltaTime) < 0.4f)
                            return;
                        crumbTimer = 0f;
                        var g = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.07f, Fx.Accent);
                        g.transform.position = Game.P.transform.position;
                        crumbs.Add(g);
                        if (crumbs.Count > 300)
                        {
                            Fx.Kill(crumbs[0]);
                            crumbs.RemoveAt(0);
                        }
                    },
                    null,
                    () => Fx.Kill(crumbs)
                )
                .Info("Leaves a dotted trail of where you've been");
            Mods.T(
                    c,
                    "Measure Tape",
                    () =>
                    {
                        if (!Gun.Get(out var p, out _) || !Gun.FireDown)
                            return;
                        if (!tapeA.HasValue)
                        {
                            tapeA = p;
                            Hud.Toast("Point A set");
                        }
                        else
                        {
                            Hud.Toast("Distance: " + Vector3.Distance(tapeA.Value, p).ToString("0.00") + " m");
                            tapeA = null;
                        }
                    },
                    null,
                    () => tapeA = null
                )
                .Info("Fire twice with the gun to measure the distance between two points");
            Mods.T(
                    c,
                    "Flashlight",
                    () =>
                    {
                        if (light == null)
                        {
                            light = new GameObject("JClientLight");
                            Fx.Spawned.Add(light);
                            var l = light.AddComponent<Light>();
                            l.type = LightType.Spot;
                            l.range = 30f;
                            l.spotAngle = 50f;
                            l.intensity = 3f;
                        }
                        light.GetComponent<Light>().color = Fx.Accent;
                        var h = Inp.VR ? Game.RHand : Game.Head;
                        light.transform.position = h.position;
                        light.transform.rotation = Inp.VR
                            ? Quaternion.LookRotation(Settings.GunForward.On ? h.forward : -h.up)
                            : h.rotation;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(light);
                        light = null;
                    }
                )
                .Info("Cyan spotlight from your hand (desktop: from your head)");
            Mods.B(c, "Quit Game", Application.Quit).Info("Closes Gorilla Tag");
        }

        static void FreeCamOn()
        {
            fcCam = Game.DesktopCam;
            if (fcCam == null)
                return;
            fcDisabled.Clear();
            foreach (var b in fcCam.GetComponents<Behaviour>())
                if (!(b is Camera) && b.enabled && !(b is AudioListener))
                {
                    b.enabled = false;
                    fcDisabled.Add(b);
                }
            fcOrigLocalPos = fcCam.transform.localPosition;
            fcOrigLocalRot = fcCam.transform.localRotation;
            fcPos = fcCam.transform.position;
            var e = fcCam.transform.eulerAngles;
            fcYaw = e.y;
            fcPitch = e.x > 180 ? e.x - 360 : e.x;
        }

        static void FreeCamTick()
        {
            if (fcCam == null)
                return;
            if (Inp.Ms != null && Inp.Ms.rightButton.isPressed && !Board.DesktopOpen)
            {
                var d = Inp.Ms.delta.ReadValue() * 0.15f;
                fcYaw += d.x;
                fcPitch = Mathf.Clamp(fcPitch - d.y, -89f, 89f);
            }
            var rot = Quaternion.Euler(fcPitch, fcYaw, 0);
            var dir = Vector3.zero;
            if (Inp.Key(Key.W))
                dir += rot * Vector3.forward;
            if (Inp.Key(Key.S))
                dir -= rot * Vector3.forward;
            if (Inp.Key(Key.D))
                dir += rot * Vector3.right;
            if (Inp.Key(Key.A))
                dir -= rot * Vector3.right;
            if (Inp.Key(Key.Space))
                dir += Vector3.up;
            if (Inp.Key(Key.LeftCtrl))
                dir -= Vector3.up;
            fcPos += dir * (Inp.Key(Key.LeftShift) ? 25f : 8f) * Time.unscaledDeltaTime;
            fcCam.transform.SetPositionAndRotation(fcPos, rot);
        }

        static void FreeCamOff()
        {
            foreach (var b in fcDisabled)
                if (b != null)
                    b.enabled = true;
            fcDisabled.Clear();
            if (fcCam != null)
            {
                fcCam.transform.localPosition = fcOrigLocalPos;
                fcCam.transform.localRotation = fcOrigLocalRot;
            }
            fcCam = null;
        }
    }
}
