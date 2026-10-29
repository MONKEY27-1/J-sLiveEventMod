using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using GorillaGameModes;
using GorillaLocomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace JClient
{
    // One menu entry. Toggle mods run Tick/Late while On; button mods run Press once; Options add a cycle button.
    public class Mod
    {
        public string Name,
            Cat,
            Desc = "";
        public bool Toggle = true,
            On,
            DefaultOn,
            Others,
            Fav;
        public string[] Options;
        public string OptLabel;
        public int Opt;
        public Action Enable,
            Disable,
            Tick,
            Late,
            Press;
        public Func<string> Status;
        public Key Bind = Key.None;
        internal ConfigEntry<bool> cOn;
        internal ConfigEntry<int> cOpt;
        internal ConfigEntry<string> cKey;
        internal ConfigEntry<bool> cFav;
        int errors;

        public string Option => Options == null ? null : Options[Opt];

        public Mod Opts(string label, params string[] o)
        {
            OptLabel = label;
            Options = o;
            return this;
        }

        public Mod Def(int opt)
        {
            Opt = opt;
            return this;
        }

        public Mod OnLate(Action a)
        {
            Late = a;
            return this;
        }

        public Mod Info(string d)
        {
            Desc = d;
            return this;
        }

        public Mod Live(Func<string> s)
        {
            Status = s;
            return this;
        }

        public Mod StartOn()
        {
            DefaultOn = On = true;
            return this;
        }

        public void Set(bool v, bool quiet = false)
        {
            if (!Toggle || On == v)
                return;
            On = v;
            Safe(v ? Enable : Disable);
            if (cOn != null)
                cOn.Value = On;
            if (!quiet)
                Hud.Toast(Name + (On ? " ON" : " OFF"));
        }

        public void Activate()
        {
            if (Toggle)
            {
                Set(!On);
                return;
            }
            Safe(Press);
        }

        public void Cycle(int dir = 1)
        {
            if (Options == null)
                return;
            Opt = (Opt + dir + Options.Length) % Options.Length;
            if (cOpt != null)
                cOpt.Value = Opt;
            Hud.Toast(OptLabel + ": " + Option);
        }

        // Anti-crash: a throwing mod is logged, and switched off after repeated failures instead of breaking the frame.
        internal void Safe(Action a)
        {
            if (a == null)
                return;
            try
            {
                a();
            }
            catch (Exception e)
            {
                if (errors++ < 3)
                    Plugin.Log.LogError(Name + ": " + e);
                if (errors > 30 && On)
                {
                    On = false;
                    if (cOn != null)
                        cOn.Value = false;
                    Hud.Toast(Name + " disabled (errors)");
                    try
                    {
                        Disable?.Invoke();
                    }
                    catch { }
                }
            }
        }
    }

    public static class Mods
    {
        public static readonly List<Mod> All = new List<Mod>();
        public static readonly string[] Categories =
        {
            "Favorites",
            "Enabled",
            "Movement",
            "Visuals",
            "Tag",
            "Utilities",
            "Room",
            "Cosmetics",
            "Safety",
            "World FX",
            "Rig",
            "Fun",
            "Klombo",
            "Soundboard",
            "Master",
            "Events",
            "Settings",
        };

        public static Mod T(string cat, string name, Action tick = null, Action on = null, Action off = null) =>
            Add(
                new Mod
                {
                    Cat = cat,
                    Name = name,
                    Tick = tick,
                    Enable = on,
                    Disable = off,
                }
            );

        public static Mod B(string cat, string name, Action press) =>
            Add(
                new Mod
                {
                    Cat = cat,
                    Name = name,
                    Toggle = false,
                    Press = press,
                }
            );

        public static Mod C(string cat, string label, params string[] opts) =>
            Add(
                new Mod
                {
                    Cat = cat,
                    Name = label,
                    Toggle = false,
                }.Opts(label, opts)
            );

        static Mod Add(Mod m)
        {
            All.Add(m);
            return m;
        }

        public static Mod Get(string name) => All.First(m => m.Name == name);
    }

    public class Btn
    {
        public bool Held,
            Down,
            Up;

        public void Feed(bool v)
        {
            Down = v && !Held;
            Up = !v && Held;
            Held = v;
        }
    }

    public static class Inp
    {
        public static readonly Btn LY = new Btn(),
            LX = new Btn(),
            RA = new Btn(),
            RB = new Btn(),
            LGrip = new Btn(),
            RGrip = new Btn(),
            LTrig = new Btn(),
            RTrig = new Btn();
        public static Vector2 LStick,
            RStick;
        public static bool VR => XRSettings.isDeviceActive;
        public static Keyboard Kb => Keyboard.current;
        public static Mouse Ms => Mouse.current;

        public static void Poll()
        {
            var p = ControllerInputPoller.instance;
            bool ok = p != null;
            LY.Feed(ok && p.leftControllerSecondaryButton);
            LX.Feed(ok && p.leftControllerPrimaryButton);
            RA.Feed(ok && p.rightControllerPrimaryButton);
            RB.Feed(ok && p.rightControllerSecondaryButton);
            LGrip.Feed(ok && p.leftControllerGripFloat > 0.6f);
            RGrip.Feed(ok && p.rightControllerGripFloat > 0.6f);
            LTrig.Feed(ok && p.leftControllerIndexFloat > 0.6f);
            RTrig.Feed(ok && p.rightControllerIndexFloat > 0.6f);
            LStick = ok ? p.leftControllerPrimary2DAxis : Vector2.zero;
            RStick = ok ? p.rightControllerPrimary2DAxis : Vector2.zero;
        }

        public static bool Key(Key k) => k != UnityEngine.InputSystem.Key.None && Kb != null && Kb[k].isPressed;

        public static bool KeyDown(Key k) => k != UnityEngine.InputSystem.Key.None && Kb != null && Kb[k].wasPressedThisFrame;

        // Desktop WASD + Space/Ctrl as a camera-relative direction
        public static Vector3 WasdDir(Transform view)
        {
            var d = Vector3.zero;
            if (Key(UnityEngine.InputSystem.Key.W))
                d += view.forward;
            if (Key(UnityEngine.InputSystem.Key.S))
                d -= view.forward;
            if (Key(UnityEngine.InputSystem.Key.D))
                d += view.right;
            if (Key(UnityEngine.InputSystem.Key.A))
                d -= view.right;
            if (Key(UnityEngine.InputSystem.Key.Space))
                d += Vector3.up;
            if (Key(UnityEngine.InputSystem.Key.LeftCtrl))
                d -= Vector3.up;
            return d;
        }
    }

    public static class Game
    {
        public static GTPlayer P => GTPlayer.Instance;
        public static GorillaTagger T => GorillaTagger.Instance;
        public static VRRig Me => VRRig.LocalRig != null ? VRRig.LocalRig : T.offlineVRRig;
        public static bool Ready =>
            GTPlayer.hasInstance
            && GTPlayer.Instance != null
            && GorillaTagger.Instance != null
            && GorillaTagger.Instance.offlineVRRig != null;
        public static Rigidbody Body => P.playerRigidBody;
        public static Transform Head => P.headCollider.transform;
        public static Transform LHand => T.leftHandTransform;
        public static Transform RHand => T.rightHandTransform;
        public static float Scale => P.scale;

        public static bool InRoom => NetworkSystem.Instance != null && NetworkSystem.Instance.InRoom;
        public static bool IsPrivate => InRoom && NetworkSystem.Instance.SessionIsPrivate;
        public static bool IsModded => InRoom && (NetworkSystem.Instance.GameModeString ?? "").ToUpperInvariant().Contains("MODDED");
        public static bool IsMaster => InRoom && NetworkSystem.Instance.IsMasterClient;

        // Mods that touch other players only run outside public lobbies.
        public static bool CanAffectOthers => !InRoom || IsPrivate || IsModded;

        public static IEnumerable<VRRig> Others =>
            VRRigCache.ActiveRigs.Where(r => r != null && !r.isOfflineVRRig && !r.isLocal && r.gameObject.activeInHierarchy);

        public static NetPlayer Owner(VRRig r) => r.Creator;

        public static string NameOf(VRRig r)
        {
            var p = Owner(r);
            return p != null ? p.NickName : r.playerNameVisible;
        }

        public static bool IsTagged(VRRig r)
        {
            var p = Owner(r);
            return p != null && GameMode.ActiveGameMode != null && GameMode.LocalIsTagged(p);
        }

        public static bool MeTagged =>
            InRoom && GameMode.ActiveGameMode != null && GameMode.LocalIsTagged(NetworkSystem.Instance.LocalPlayer);

        public static RigContainer Container(VRRig r) => r.GetComponent<RigContainer>() ?? r.GetComponentInParent<RigContainer>();

        public static VRRig Nearest(Vector3 from, Func<VRRig, bool> filter = null)
        {
            VRRig best = null;
            float bd = float.MaxValue;
            foreach (var r in Others)
            {
                if (filter != null && !filter(r))
                    continue;
                float d = (r.transform.position - from).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = r;
                }
            }
            return best;
        }

        public static Vector3? LastTeleportFrom;

        public static void Teleport(Vector3 pos)
        {
            LastTeleportFrom = P.transform.position;
            P.TeleportTo(pos, P.transform.rotation);
        }

        public static Camera DesktopCam
        {
            get
            {
                var tp = T != null ? T.thirdPersonCamera : null;
                var c = tp != null ? tp.GetComponentInChildren<Camera>() : null;
                return c != null && c.isActiveAndEnabled ? c : Camera.main;
            }
        }

        public static void Feedback(bool left)
        {
            if (Settings.Haptics.On)
                try
                {
                    T.StartVibration(left, 0.25f, 0.04f);
                }
                catch { }
            if (Settings.Sounds.On)
                try
                {
                    Me.PlayHandTapLocal(66, left, 0.25f);
                }
                catch { }
        }
    }

    // Local-only visual helpers
    public static class Fx
    {
        static Shader opaque,
            seeThrough,
            alpha;
        static Font font;
        public static readonly List<UnityEngine.Object> Spawned = new List<UnityEngine.Object>();

        static Shader Pick(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null)
                    return s;
            }
            return null;
        }

        public static Material Mat(Color c, bool throughWalls = false, bool transparent = false)
        {
            if (opaque == null)
                opaque = Pick("Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default", "GUI/Text Shader");
            if (seeThrough == null)
                seeThrough = Pick("GUI/Text Shader", "Sprites/Default");
            if (alpha == null)
                alpha = Pick("Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "GUI/Text Shader");
            var m = new Material(
                throughWalls ? seeThrough
                : transparent ? alpha
                : opaque
            )
            {
                color = c,
            };
            if (throughWalls)
                m.renderQueue = 4000;
            return m;
        }

        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static GameObject Prim(PrimitiveType t, Vector3 scale, Color c, bool collider = false, bool throughWalls = false)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!collider)
                UnityEngine.Object.Destroy(g.GetComponent<Collider>());
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.material = Mat(c, throughWalls, c.a < 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Spawned.Add(g);
            return g;
        }

        public static LineRenderer Line(Color c, float width, bool throughWalls = true)
        {
            var g = new GameObject("JClientLine");
            var l = g.AddComponent<LineRenderer>();
            l.material = Mat(c, throughWalls, true);
            l.startWidth = l.endWidth = width;
            l.positionCount = 2;
            l.useWorldSpace = true;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Spawned.Add(g);
            return l;
        }

        public static TextMesh Text(Transform parent, string s, float size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject("JClientText");
            g.transform.SetParent(parent, false);
            var t = g.AddComponent<TextMesh>();
            t.font = Font;
            t.GetComponent<MeshRenderer>().material = Font.material;
            t.fontSize = 64;
            t.characterSize = size;
            t.anchor = anchor;
            t.alignment = TextAlignment.Center;
            t.color = c;
            t.text = s;
            return t;
        }

        public static ParticleSystem Particles(Color c, float rate, float size, float speed, float life, float radius, int max = 600)
        {
            var g = new GameObject("JClientFx");
            Spawned.Add(g);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startColor = c;
            main.startSize = size;
            main.startSpeed = speed;
            main.startLifetime = life;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = radius;
            var pr = g.GetComponent<ParticleSystemRenderer>();
            pr.material = Mat(Color.white, false, true);
            ps.Play();
            return ps;
        }

        public static void Kill(UnityEngine.Object o)
        {
            if (o != null)
            {
                Spawned.Remove(o);
                UnityEngine.Object.Destroy(o is Component cm ? cm.gameObject : o);
            }
        }

        public static void Kill<T>(List<T> list)
            where T : UnityEngine.Object
        {
            foreach (var o in list)
                Kill(o);
            list.Clear();
        }

        public static Color Accent => Settings.AccentColor;

        public static Color Rainbow(float speed = 0.25f, float offset = 0f) =>
            Color.HSVToRGB(Mathf.Repeat(Time.time * speed + offset, 1f), 0.9f, 1f);
    }

    // Shared gun: VR = right grip aims, right trigger fires; desktop = hold right mouse to aim, left click fires.
    public static class Gun
    {
        static LineRenderer line;
        static GameObject dot;
        static int frame = -1,
            usedFrame = -1;
        public static bool Aiming,
            Firing,
            FireDown;
        public static Vector3 Point,
            Origin;
        public static VRRig Rig,
            Locked;

        public static bool Get(out Vector3 point, out VRRig rig)
        {
            usedFrame = Time.frameCount;
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                Compute();
            }
            point = Point;
            rig = Locked != null ? Locked : Rig;
            return Aiming;
        }

        static void Compute()
        {
            bool vr = Inp.VR;
            bool blocked = Board.DesktopOpen && !vr || Board.VROpen;
            Aiming = !blocked && (vr ? Inp.RGrip.Held : Inp.Ms != null && Inp.Ms.rightButton.isPressed);
            bool fire = Aiming && (vr ? Inp.RTrig.Held : Inp.Ms.leftButton.isPressed);
            FireDown = fire && !Firing;
            Firing = fire;
            if (!Aiming)
            {
                Locked = null;
                Rig = null;
                Show(false);
                return;
            }

            Ray ray;
            if (vr)
            {
                var h = Game.RHand;
                ray = new Ray(h.position, Settings.GunForward.On ? h.forward : -h.up);
            }
            else
            {
                var cam = Game.DesktopCam;
                ray = cam.ScreenPointToRay(Inp.Ms.position.ReadValue());
            }
            Origin = vr ? ray.origin : Game.RHand.position;
            Point = Physics.Raycast(ray, out var hit, 512f, GTPlayer.LocomotionEnabledLayers, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + ray.direction * 40f;

            // Aim assist: closest rig to the ray, in front and not past the wall we hit
            Rig = null;
            float best = 0.8f,
                maxD = Vector3.Distance(ray.origin, Point) + 1f;
            foreach (var r in Game.Others)
            {
                var to = r.transform.position - ray.origin;
                float along = Vector3.Dot(to, ray.direction);
                if (along < 0 || along > maxD)
                    continue;
                float off = Vector3.Cross(ray.direction, to).magnitude;
                if (off < best)
                {
                    best = off;
                    Rig = r;
                }
            }
            if (Firing && Locked == null)
                Locked = Rig;
            if (!Firing)
                Locked = null;
            var target = Locked != null ? Locked : Rig;
            if (target != null)
                Point = target.transform.position;
            Show(true, target != null);
        }

        static void Show(bool on, bool onRig = false)
        {
            if (line == null)
            {
                line = Fx.Line(Fx.Accent, 0.01f);
                dot = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.12f, Fx.Accent, false, true);
            }
            line.gameObject.SetActive(on);
            dot.SetActive(on);
            if (!on)
                return;
            var c = onRig ? Color.white : Fx.Accent;
            line.material.color = c;
            dot.GetComponent<Renderer>().material.color = c;
            line.SetPosition(0, Origin);
            line.SetPosition(1, Point);
            dot.transform.position = Point;
        }

        public static void EndFrame()
        {
            if (usedFrame != Time.frameCount && line != null && line.gameObject.activeSelf)
                Show(false);
        }
    }

    // Moves your networked rig for a frame without disabling it (disabling the offline rig drops its net view in this game version).
    public static class RigCtl
    {
        public static Vector3 Pos;
        public static Quaternion? Rot;
        public static int Until = -1;
        static RigPose? limbs;

        public static void Hold(Vector3 p, Quaternion? r = null)
        {
            Pos = p;
            Rot = r;
            limbs = null;
            Until = Time.frameCount + 1;
        }

        // Full freeze: position, body rotation, head and hands all stay exactly as captured.
        public static void Freeze(RigPose pose)
        {
            Hold(pose.Pos, pose.Rot);
            limbs = pose;
        }

        public static bool Active => Time.frameCount <= Until;

        // PostTick rewrites the player's speed values every tick, so speed mods scale them right after it.
        public static float SpeedMul = 1f;
        public static bool Uncapped;
        public static Action<VRRig> After;
        public static Vector3 Real;
        public static RigPose RealPose; // the game's pose for this tick, before any override

        internal static void Apply(VRRig r)
        {
            RealPose = RigPose.Capture(r);
            Real = RealPose.Pos;
            if (!Active)
                return;
            r.transform.position = Pos;
            if (Rot.HasValue)
                r.transform.rotation = Rot.Value;
            if (limbs.HasValue)
                limbs.Value.ApplyLimbs(r);
        }
    }

    public struct RigPose
    {
        public Vector3 Pos,
            LPos,
            RPos;
        public Quaternion Rot,
            HeadRot,
            LRot,
            RRot;

        public static RigPose Capture(VRRig r) =>
            new RigPose
            {
                Pos = r.transform.position,
                Rot = r.transform.rotation,
                HeadRot = r.head.rigTarget.localRotation,
                LPos = r.leftHand.rigTarget.localPosition,
                LRot = r.leftHand.rigTarget.localRotation,
                RPos = r.rightHand.rigTarget.localPosition,
                RRot = r.rightHand.rigTarget.localRotation,
            };

        public void ApplyLimbs(VRRig r)
        {
            r.head.rigTarget.localRotation = HeadRot;
            r.leftHand.rigTarget.localPosition = LPos;
            r.leftHand.rigTarget.localRotation = LRot;
            r.rightHand.rigTarget.localPosition = RPos;
            r.rightHand.rigTarget.localRotation = RRot;
        }
    }

    [HarmonyPatch(typeof(VRRig), nameof(VRRig.PostTick))]
    static class RigPatch
    {
        static void Postfix(VRRig __instance)
        {
            if (!__instance.isOfflineVRRig)
                return;
            var p = GTPlayer.Instance;
            if (p != null && RigCtl.SpeedMul != 1f)
            {
                p.jumpMultiplier *= RigCtl.SpeedMul;
                p.maxJumpSpeed *= RigCtl.SpeedMul;
            }
            if (p != null && RigCtl.Uncapped)
                p.maxJumpSpeed = 100f;
            RigCtl.Apply(__instance);
            RigCtl.After?.Invoke(__instance); // rig pose mods (head spin, frozen limbs...) run after the game has posed the rig
        }
    }

    // Tagging (callers are OthersOnly mods). Master: add to infected directly. Otherwise move rig next to the target, then report.
    public static class Tagging
    {
        static readonly Queue<VRRig> queue = new Queue<VRRig>();
        static VRRig cur;
        static float t;

        public static void Tag(VRRig r)
        {
            if (r != null && r != cur && !queue.Contains(r) && !Game.IsTagged(r))
                queue.Enqueue(r);
        }

        public static int Pending => queue.Count + (cur != null ? 1 : 0);

        public static void Tick()
        {
            if (cur == null)
            {
                if (queue.Count == 0)
                    return;
                cur = queue.Dequeue();
                t = 0f;
                if (cur == null || !cur.gameObject.activeInHierarchy || Game.IsTagged(cur))
                {
                    cur = null;
                    return;
                }
                var p = Game.Owner(cur);
                if (Game.IsMaster && GameMode.ActiveGameMode is GorillaTagManager tm)
                {
                    if (p != null && !tm.currentInfected.Contains(p))
                        tm.AddInfectedPlayer(p);
                    cur = null;
                    return;
                }
                if (!Game.MeTagged)
                {
                    Hud.Toast("You're not it: get tagged first, or host the room (Join Random Code makes you master)");
                    queue.Clear();
                    cur = null;
                    return;
                }
            }
            // Sit inside the target long enough for the master's interpolated copy of our rig to arrive, report twice in case the first lands early
            float before = t;
            t += Time.deltaTime;
            RigCtl.Hold(cur.transform.position, cur.transform.rotation);
            var owner = Game.Owner(cur);
            if (owner != null && (before < 0.3f && t >= 0.3f || before < 0.6f && t >= 0.6f))
                GameMode.ReportTag(owner);
            if (t >= 0.7f || Game.IsTagged(cur))
                cur = null;
        }
    }
}
