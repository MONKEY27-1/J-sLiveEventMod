using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AstronomicalEvent
{
    // Test console for development: the plugin watches BepInEx/eventtest.txt; every line is a command (the file is emptied after it is read), results go to the BepInEx log and BepInEx/eventtest.log.
    //   dump                      list room / network methods of the game (to find the join call)
    //   join [code]               join a (random) private room
    //   start <event id> [t]      select the event, start it with no countdown (forest check bypassed) and jump to music time t
    //   seek <t> | stop | pause | resume | state
    //   cam x y z tx ty tz [fov]  a debug camera at a WORLD position looking at a world point;  camu = same in the event's local coordinates; camg = the underground's local coordinates (stairs / cave numbers);  cam off
    //   shot <name>               screenshot to BepInEx/shots/<name>.png (taken at the end of the frame)
    //   wait <seconds>            pause the script (lines after it run later)
    internal static class AstronomicalDebug
    {
        public static readonly System.Collections.Generic.Dictionary<string, Func<Vector3>> Anchors =
            new System.Collections.Generic.Dictionary<string, Func<Vector3>>();
        public static readonly float[] Tweak = { 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f };
        public static bool NoMusic;
        static Func<Vector3> follow, followAt;
        public static Action<Vector3, Vector3> FireHook;
        static Vector3 followOff;
        public static Func<Vector3, Vector3> L2W = v => v;
        static string cmdPath,
            logPath,
            shotDir;
        static float nextPoll,
            waitUntil;
        static Camera cam;
        static readonly System.Collections.Generic.Queue<string> queue = new System.Collections.Generic.Queue<string>();

        static void Say(string s)
        {
            AstronomicalEventMod.Log.LogInfo("[eventtest] " + s);
            try
            {
                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n");
            }
            catch { }
        }

        public static void Init(string pluginDir)
        {
            var bep = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
            cmdPath = Path.Combine(bep, "eventtest.txt");
            logPath = Path.Combine(bep, "eventtest.log");
            shotDir = Path.Combine(bep, "shots");
            try
            {
                Directory.CreateDirectory(shotDir);
                if (!File.Exists(cmdPath))
                    File.WriteAllText(cmdPath, "");
            }
            catch { }
            Say("test console ready: write commands to " + cmdPath);
        }

        public static void Tick(AstronomicalEventManager mgr)
        {
            if (cmdPath == null)
                return;
            if (follow != null && cam != null)
            {
                try
                {
                    var tg = follow();
                    if (followAt != null) { cam.transform.position = tg; var at = followAt(); cam.transform.rotation = Quaternion.LookRotation((at - tg).sqrMagnitude < 1e-4f ? Vector3.forward : (at - tg).normalized); }
                    else { cam.transform.position = tg + followOff; cam.transform.rotation = Quaternion.LookRotation((tg - cam.transform.position).normalized); }
                }
                catch
                {
                    follow = null; /* the anchor's event is gone */
                }
            }
            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.4f;
                try
                {
                    var txt = File.ReadAllText(cmdPath);
                    if (txt.Trim().Length > 0)
                    {
                        File.WriteAllText(cmdPath, "");
                        foreach (var l in txt.Split('\n'))
                        {
                            var s = l.Trim();
                            if (s.Length > 0 && !s.StartsWith("#"))
                                queue.Enqueue(s);
                        }
                    }
                }
                catch { }
            }
            while (queue.Count > 0 && Time.unscaledTime >= waitUntil)
            {
                var line = queue.Dequeue();
                try
                {
                    Run(line, mgr);
                }
                catch (Exception e)
                {
                    Say("ERROR in '" + line + "': " + e.Message);
                }
            }
        }

        static float F(string[] a, int i, float d = 0f)
        {
            float v;
            return
                i < a.Length
                && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)
                ? v
                : d;
        }

        static void Run(string line, AstronomicalEventManager mgr)
        {
            var a = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string c = a[0].ToLowerInvariant().TrimStart('/');
            Say("> " + line);
            switch (c)
            {
                case "dump":
                    Dump();
                    break;
                case "join":
                    Join(a.Length > 1 ? a[1] : null);
                    break;
                case "wait":
                    waitUntil = Time.unscaledTime + F(a, 1, 1f);
                    break;
                case "start":
                    AstronomicalEventMod.Inst.StartCoroutine(StartEvent(mgr, a.Length > 1 ? a[1] : "collision", F(a, 2, 0f)));
                    break;
                case "seek":
                    mgr.SeekToTime(F(a, 1));
                    break;
                case "stop":
                    mgr.UserStop();
                    break;
                case "pause":
                    mgr.Pause();
                    break;
                case "resume":
                    mgr.Resume();
                    break;
                case "cam":
                case "camu":
                case "camg":
                    if (a.Length > 1 && a[1] == "off")
                    {
                        follow = null; followAt = null;
                        if (cam != null)
                            UnityEngine.Object.Destroy(cam.gameObject);
                        cam = null;
                        break;
                    }
                    follow = null;
                    Vector3 p = new Vector3(F(a, 1), F(a, 2), F(a, 3)),
                        t = new Vector3(F(a, 4), F(a, 5), F(a, 6));
                    if (c == "camu")
                    {
                        p = L2W(p);
                        t = L2W(t);
                    }
                    else if (c == "camg")
                    {
                        var o = new Vector3(0f, 700f, 0f);
                        p = L2W(p + o);
                        t = L2W(t + o);
                    }
                    if (cam == null)
                    {
                        var go = new GameObject("EvtTestCam");
                        cam = go.AddComponent<Camera>();
                        cam.depth = 100;
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = Color.black;
                        cam.farClipPlane = 3000f;
                        cam.nearClipPlane = 0.05f;
                    }
                    cam.transform.position = p;
                    cam.transform.rotation = Quaternion.LookRotation((t - p).sqrMagnitude < 1e-4f ? Vector3.forward : (t - p).normalized);
                    cam.fieldOfView = F(a, 7, 70f);
                    break;
                case "camf": // camf <anchor> dx dy dz [fov]: the camera hangs at anchor + (dx,dy,dz) (world axes) and looks at the anchor (anchors: hand palm head jones found tank player ...)
                {
                    Func<Vector3> fa;
                    if (a.Length < 2 || !Anchors.TryGetValue(a[1], out fa))
                    {
                        Say("anchors: " + string.Join(", ", Anchors.Keys.ToArray()));
                        break;
                    }
                    Vector3 tg = fa(),
                        pp = tg + new Vector3(F(a, 2), F(a, 3), F(a, 4));
                    if (cam == null)
                    {
                        var go = new GameObject("EvtTestCam");
                        cam = go.AddComponent<Camera>();
                        cam.depth = 100;
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = Color.black;
                        cam.farClipPlane = 3000f;
                        cam.nearClipPlane = 0.05f;
                    }
                    follow = fa; followAt = null;
                    followOff = new Vector3(F(a, 2), F(a, 3), F(a, 4));
                    cam.fieldOfView = F(a, 5, 60f);
                    break;
                }
                case "camb": // camb <fromAnchor> <atAnchor> [fov] [dx dy dz]: the camera sits at the first anchor (+ offset) and looks at the second
                {
                    Func<Vector3> f1, f2;
                    if (a.Length < 3 || !Anchors.TryGetValue(a[1], out f1) || !Anchors.TryGetValue(a[2], out f2)) { Say("anchors: " + string.Join(", ", Anchors.Keys.ToArray())); break; }
                    if (cam == null)
                    {
                        var go = new GameObject("EvtTestCam");
                        cam = go.AddComponent<Camera>();
                        cam.depth = 100;
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = Color.black;
                        cam.farClipPlane = 3000f;
                        cam.nearClipPlane = 0.05f;
                    }
                    Vector3 off = new Vector3(F(a, 4), F(a, 5), F(a, 6));
                    follow = () => f1() + off; followAt = f2; cam.fieldOfView = F(a, 3, 70f);
                    break;
                }
                case "shoot": // shoot <anchor> [count] [gap]: fires the player's weapon from your head at an anchor (a test for the trigger)
                {
                    Func<Vector3> fa2;
                    if (a.Length < 2 || !Anchors.TryGetValue(a[1], out fa2) || FireHook == null) { Say("shoot <anchor> [count] [gap]  (anchors: " + string.Join(", ", Anchors.Keys.ToArray()) + ")"); break; }
                    AstronomicalEventMod.Inst.StartCoroutine(ShootBurst(fa2, (int)F(a, 2, 1f), F(a, 3, 0.2f)));
                    break;
                }
                case "tweak":
                {
                    int i = (int)F(a, 1);
                    if (i >= 0 && i < Tweak.Length)
                    {
                        Tweak[i] = F(a, 2);
                        Say("tweak[" + i + "]=" + Tweak[i]);
                    }
                    break;
                }
                case "trace":
                    AstronomicalEventMod.Inst.StartCoroutine(
                        Trace(mgr, a.Length > 1 ? a[1] : "player", a.Length > 2 ? a[2] : "head", F(a, 3, 10f))
                    );
                    break;
                case "seq":
                {
                    int cur = AstronomicalEventApi.CurrentSequence;
                    int to = a.Length > 1 ? (int)F(a, 1) : cur + 1;
                    Say(
                        "sequence "
                            + cur
                            + " of "
                            + AstronomicalEventApi.SequenceCount
                            + " -> "
                            + to
                            + " (start "
                            + AstronomicalEventApi.GetSequenceStartTime(to)
                            + ")"
                    );
                    AstronomicalEventApi.SeekToSequence(to);
                    break;
                }
                case "repo":
                    AstronomicalEventMod.Inst.StartCoroutine(RepoList());
                    break;
                case "install":
                    AstronomicalEventMod.Inst.StartCoroutine(RepoInstall(mgr, a.Length > 1 ? a[1] : ""));
                    break;
                case "find": { string q = a.Length > 1 ? a[1] : "gazebo"; int n = 0; foreach (var t2 in UnityEngine.Object.FindObjectsOfType<Transform>()) { if (t2 != null && t2.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 && n++ < 25) Say("found " + t2.name + " at " + t2.position + " active " + t2.gameObject.activeInHierarchy + " parent " + (t2.parent != null ? t2.parent.name : "-")); } Say("find '" + q + "': " + n + " matches"); break; }
                case "shot":
                    AstronomicalEventMod.Inst.StartCoroutine(Shot(a.Length > 1 ? a[1] : "shot"));
                    break;
                case "state":
                    Say("anchors " + string.Join(" ", Anchors.Select(k => k.Key + "=" + k.Value().ToString("0.0")).ToArray()));
                    Say("state " + mgr.State + " T=" + mgr.T.ToString("0.0") + " forest=" + mgr.CheckForest() + " player=" + HeadPos());
                    break;
                default:
                    Say("unknown command");
                    break;
            }
        }

        static System.Collections.IEnumerator ShootBurst(Func<Vector3> at, int n, float gap)
        {
            for (int i = 0; i < n; i++)
            {
                try { Vector3 o = GorillaLocomotion.GTPlayer.Instance.headCollider.transform.position + Vector3.down * 0.2f, tg = at(); if (FireHook != null) FireHook(o, (tg - o).normalized); } catch { }
                yield return new WaitForSeconds(gap);
            }
        }

        static string HeadPos()
        {
            try
            {
                return GorillaLocomotion.GTPlayer.Instance.headCollider.transform.position.ToString();
            }
            catch
            {
                return "?";
            }
        }

        static IEnumerator Trace(AstronomicalEventManager mgr, string an, string bn, float secs)
        {
            Func<Vector3> fa,
                fb;
            if (!Anchors.TryGetValue(an, out fa) || !Anchors.TryGetValue(bn, out fb))
            {
                Say("unknown anchor");
                yield break;
            }
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < secs)
            {
                Vector3 d = fa() - fb();
                if (an == "cabloc")
                    d = fa();
                Say(
                    "trace T="
                        + mgr.T.ToString("0.00")
                        + " "
                        + an
                        + "-"
                        + bn
                        + " = "
                        + d.ToString("0.00")
                        + " |"
                        + d.magnitude.ToString("0.00")
                        + "|"
                );
                yield return new WaitForSeconds(0.25f);
            }
        }

        static IEnumerator RepoList()
        {
            yield return AstronomicalInstaller.Refresh();
            foreach (var kv in AstronomicalInstaller.Remote)
                Say(
                    "repo event "
                        + kv.Key
                        + " "
                        + (AstronomicalInstaller.TotalBytes(kv.Key) / 1048576f).ToString("0.0")
                        + " MB"
                        + (AstronomicalInstaller.Installed(kv.Key) ? " [installed]" : "")
                );
            Say(AstronomicalInstaller.Status);
        }

        static IEnumerator RepoInstall(AstronomicalEventManager mgr, string id)
        {
            if (AstronomicalInstaller.Remote.Count == 0)
                yield return AstronomicalInstaller.Refresh();
            yield return AstronomicalInstaller.Install(id, mgr);
            Say(AstronomicalInstaller.Status);
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var p = Path.Combine(shotDir, name + ".png");
            try
            {
                var sc = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
                sc.GetMethod("CaptureScreenshot", new[] { typeof(string) }).Invoke(null, new object[] { p });
            }
            catch (Exception e)
            {
                Say("screenshot failed: " + e.Message);
            }
            Say("shot -> " + p);
        }

        static IEnumerator StartEvent(AstronomicalEventManager mgr, string id, float t0)
        {
            if (mgr.State != EventState.Idle)
            {
                mgr.UserStop();
                yield return new WaitForSeconds(1f);
            }
            AstronomicalSettings.BypassForestCheck.Value = true;
            NoMusic = true;
            float guard = 0f;
            while (!mgr.SelectEvent(id) && guard < 120f)
            {
                guard += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
            if (guard >= 120f)
            {
                Say("could not select " + id);
                yield break;
            }
            try
            {
                AstronomicalSettings.Countdown.Value = 0;
            }
            catch { }
            mgr.Start(0);
            yield return new WaitForSeconds(1.5f);
            Say("started " + id + ", state " + mgr.State);
            if (t0 > 0.5f)
            {
                mgr.SeekToTime(t0);
                Say("seeked to " + t0);
            }
        }

        static void Dump()
        {
            foreach (
                var name in new[] { "NetworkSystem", "PhotonNetworkController", "GorillaComputer", "NetworkSystemRaw", "PhotonNetwork" }
            )
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try
                {
                    t = a.GetTypes().FirstOrDefault(x => x.Name == name);
                }
                catch { }
                if (t == null)
                    continue;
                Say("== " + t.FullName + (t.BaseType != null ? " : " + t.BaseType.Name : ""));
                foreach (
                    var m in t.GetMethods(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Instance
                            | BindingFlags.Static
                            | BindingFlags.DeclaredOnly
                    )
                )
                {
                    var n = m.Name;
                    if (
                        n.IndexOf("Join", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Create", StringComparison.OrdinalIgnoreCase) >= 0
                    )
                        Say(
                            "  "
                                + (m.IsStatic ? "static " : "")
                                + m.ReturnType.Name
                                + " "
                                + n
                                + "("
                                + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name).ToArray())
                                + ")"
                        );
                }
            }
        }

        static void Join(string code)
        {
            if (code == null)
            {
                var r = new System.Random();
                code = "EVT" + r.Next(1000, 9999);
            }
            Say("joining private room " + code);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type pc = null;
                try
                {
                    pc = a.GetTypes().FirstOrDefault(x => x.Name == "PhotonNetworkController");
                }
                catch { }
                if (pc == null)
                    continue;
                object inst = null;
                var pi = pc.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (pi != null)
                    inst = pi.GetValue(null, null);
                if (inst == null)
                {
                    var fi = pc.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (fi != null)
                        inst = fi.GetValue(null);
                }
                if (inst == null)
                {
                    Say("PhotonNetworkController.Instance is null");
                    continue;
                }
                var m = pc.GetMethod("AttemptToJoinSpecificRoom", BindingFlags.Public | BindingFlags.Instance);
                if (m == null)
                {
                    Say("AttemptToJoinSpecificRoom missing");
                    continue;
                }
                var jt = m.GetParameters()[1].ParameterType;
                Say("JoinType values: " + string.Join(", ", Enum.GetNames(jt)));
                object val = Enum.ToObject(jt, 0);
                foreach (var n in new[] { "Solo", "JoinWithFriends", "FriendStationPublic" })
                    if (Enum.IsDefined(jt, n))
                    {
                        val = Enum.Parse(jt, n);
                        break;
                    }
                try
                {
                    m.Invoke(inst, new object[] { code, val });
                    Say("AttemptToJoinSpecificRoom(" + code + ", " + val + ") called");
                    return;
                }
                catch (Exception e)
                {
                    Say("join failed: " + (e.InnerException ?? e).Message);
                }
            }
            Say("no join method worked");
        }
    }
}
