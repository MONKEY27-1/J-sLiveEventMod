using System.Collections.Generic;
using System.Linq;
using GorillaTagScripts;
using UnityEngine;

namespace JClient
{
    public static class Safety
    {
        static float joinedAt = -1f,
            leaveCooldown,
            lagTime,
            hideTimer;
        static readonly HashSet<VRRig> hidden = new HashSet<VRRig>();
        static readonly Dictionary<VRRig, float> loudTime = new Dictionary<VRRig, float>();

        static void Mute(VRRig r, bool on)
        {
            var rc = Game.Container(r);
            if (rc != null)
                rc.SetMuted(RigContainer.MuteReason.Manual, on);
        }

        static void Show(VRRig r, bool on)
        {
            foreach (var rend in r.GetComponentsInChildren<Renderer>(true))
                rend.forceRenderingOff = !on;
        }

        static float reportProximityThreshold = 1.0f;
        static float nextReportWarning;
        static readonly HashSet<VRRig> warnedRigs = new HashSet<VRRig>();

        static bool HandNearPosition(VRRig rig, Vector3 position)
        {
            if (rig == null)
                return false;

            return Vector3.Distance(rig.rightHandTransform.position, position) < reportProximityThreshold
                || Vector3.Distance(rig.leftHandTransform.position, position) < reportProximityThreshold
                || Vector3.Distance(rig.rightHand.syncPos, position) < reportProximityThreshold
                || Vector3.Distance(rig.leftHand.syncPos, position) < reportProximityThreshold;
        }

        static void AntiReport(VRRig rig)
        {
            if (rig == null || Time.time < nextReportWarning)
                return;

            nextReportWarning = Time.time + 1f;

            Hud.Toast("A player is near a report button: " + Game.NameOf(rig));

            RoomMods.Leave();
        }

        public static void Register()
        {
            const string c = "Safety";
            Mods.T(
                    c,
                    "AntiReport",
                    () =>
                    {
                        if (!Game.InRoom)
                            return;

                        foreach (GorillaPlayerScoreboardLine line in GorillaScoreboardTotalUpdater.allScoreboardLines)
                        {
                            if (line == null || line.linePlayer != NetworkSystem.Instance.LocalPlayer || line.reportButton == null)
                                continue;

                            Vector3 reportPosition = line.reportButton.gameObject.transform.position;

                            foreach (VRRig rig in Game.Others)
                            {
                                if (rig == null)
                                    continue;

                                if (HandNearPosition(rig, reportPosition))
                                {
                                    AntiReport(rig);
                                    return;
                                }
                            }
                        }
                    }
                )
                .Info("Detects nearby hands at your scoreboard report button and leaves the room");
            Mod space = null;

            // Personal space: leave the room the moment another player's head or hands come inside your bubble.
            space = Mods.T(
                    c,
                    "Personal Space Leave",
                    () =>
                    {
                        if (!Game.InRoom)
                        {
                            joinedAt = -1f;
                            return;
                        }
                        if (joinedAt < 0)
                            joinedAt = Time.time;
                        if (Time.time - joinedAt < 5f || (leaveCooldown -= Time.deltaTime) > 0)
                            return; // ignore spawn overlap
                        float radius = new[] { 1f, 2f, 3f, 5f, 8f }[space.Opt] * Game.Scale;
                        var me = Game.Head.position;
                        foreach (var r in Game.Others)
                        {
                            if (
                                Vector3.Distance(r.transform.position, me) > radius
                                && Vector3.Distance(r.leftHandTransform.position, me) > radius
                                && Vector3.Distance(r.rightHandTransform.position, me) > radius
                            )
                                continue;
                            leaveCooldown = 3f;
                            joinedAt = -1f;
                            Hud.Toast(Game.NameOf(r) + " came within " + space.Option + " - leaving");
                            RoomMods.Leave();
                            return;
                        }
                    }
                )
                .Opts("Bubble Radius", "1 m", "2 m", "3 m", "5 m", "8 m")
                .Def(1)
                .Info("Leaves the room when anyone's body or hands get closer than the radius you set");

            Mods.B(c, "Change Identity", RoomMods.ChangeIdentity)
                .Info("Random MONKE#### name and random fur colour (same flow as the in-game computer)");
            bool wasInRoom = false;
            Mods.T(
                    c,
                    "Change Identity On Leave",
                    () =>
                    {
                        if (wasInRoom && !Game.InRoom)
                            RoomMods.ChangeIdentity();
                        wasInRoom = Game.InRoom;
                    },
                    () => wasInRoom = Game.InRoom
                )
                .Info("Picks a new random name and colour every time you leave a room");

            Mods.T(
                    c,
                    "Mute Everyone",
                    () =>
                    {
                        foreach (var r in Game.Others)
                            Mute(r, true);
                    },
                    null,
                    () =>
                    {
                        foreach (var r in Game.Others)
                            Mute(r, false);
                    }
                )
                .Info("Locally mutes every player's voice");
            Mods.B(
                    c,
                    "Unmute Everyone",
                    () =>
                    {
                        foreach (var r in Game.Others)
                            Mute(r, false);
                        Hud.Toast("Everyone unmuted");
                    }
                )
                .Info("Clears manual mutes");
            Mods.T(
                    c,
                    "Mute Gun",
                    () =>
                    {
                        if (!Gun.Get(out _, out var r) || !Gun.FireDown || r == null)
                            return;
                        var rc = Game.Container(r);
                        if (rc == null)
                            return;
                        bool now = !rc.IsMutedFor(RigContainer.MuteReason.Manual);
                        rc.SetMuted(RigContainer.MuteReason.Manual, now);
                        Hud.Toast((now ? "Muted " : "Unmuted ") + Game.NameOf(r));
                    }
                )
                .Info("Fire at a player to mute/unmute them (local)");
            Mods.T(
                    c,
                    "Auto-Mute Loud",
                    () =>
                    {
                        foreach (var r in Game.Others)
                        {
                            var sp = r.GetComponentInChildren<GorillaSpeakerLoudness>();
                            if (sp == null)
                                continue;
                            loudTime.TryGetValue(r, out var t);
                            t = sp.LoudnessNormalized > 0.95f ? t + Time.deltaTime : 0f;
                            loudTime[r] = t;
                            if (t > 1.5f)
                            {
                                Mute(r, true);
                                loudTime[r] = 0f;
                                Hud.Toast("Auto-muted loud player " + Game.NameOf(r));
                            }
                        }
                    },
                    null,
                    () => loudTime.Clear()
                )
                .Info("Mutes anyone who screams at max volume for over 1.5 s");

            Mods.T(
                    c,
                    "Hide Everyone",
                    () =>
                    {
                        if ((hideTimer -= Time.deltaTime) > 0)
                            return;
                        hideTimer = 0.5f;
                        foreach (var r in Game.Others)
                        {
                            Show(r, false);
                            hidden.Add(r);
                        }
                    },
                    null,
                    () =>
                    {
                        foreach (var r in hidden)
                            if (r != null)
                                Show(r, true);
                        hidden.Clear();
                    }
                )
                .Info("Locally hides every other player");
            Mods.T(
                    c,
                    "Hide Gun",
                    () =>
                    {
                        if (!Gun.Get(out _, out var r) || !Gun.FireDown || r == null)
                            return;
                        bool hide = !hidden.Contains(r);
                        Show(r, !hide);
                        if (hide)
                            hidden.Add(r);
                        else
                            hidden.Remove(r);
                        Hud.Toast((hide ? "Hid " : "Showing ") + Game.NameOf(r));
                    },
                    null,
                    () =>
                    {
                        foreach (var r in hidden)
                            if (r != null)
                                Show(r, true);
                        hidden.Clear();
                    }
                )
                .Info("Fire at a player to hide/show them (local)");

            // Uses the game's own switch for its 2-minute idle disconnect
            Mods.T(
                    c,
                    "Anti AFK Kick",
                    () =>
                    {
                        var p = GorillaNetworking.PhotonNetworkController.Instance;
                        if (p != null)
                            p.disableAFKKick = true;
                    },
                    null,
                    () =>
                    {
                        var p = GorillaNetworking.PhotonNetworkController.Instance;
                        if (p != null)
                            p.disableAFKKick = false;
                    }
                )
                .Info("Stops the game kicking you from the room after 2 minutes without moving");
            Mods.T(
                    c,
                    "Lag Guard",
                    () =>
                    {
                        lagTime = Time.unscaledDeltaTime > 1f / 30f ? lagTime + Time.unscaledDeltaTime : 0f;
                        if (lagTime < 3f)
                            return;
                        lagTime = 0f;
                        int n = 0;
                        foreach (var m in Mods.All)
                            if (m.On && (m.Cat == "Visuals" || m.Cat == "World FX" || m.Cat == "Cosmetics"))
                            {
                                m.Set(false, quiet: true);
                                n++;
                            }
                        Hud.Toast(n > 0 ? "Lag Guard: FPS under 30, turned off " + n + " visual mods" : "Lag Guard: FPS under 30");
                    }
                )
                .StartOn()
                .Info("If FPS stays under 30 for 3 s, turns off heavy visual mods");

            Visuals.HudLine(
                c,
                "Room Info",
                () =>
                    !Game.InRoom
                        ? "Not in a room"
                        : NetworkSystem.Instance.RoomName
                            + (Game.IsPrivate ? "  PRIVATE" : "  PUBLIC")
                            + (Game.IsModded ? " MODDED" : "")
                            + "  "
                            + NetworkSystem.Instance.RoomPlayerCount
                            + " players"
                            + (Game.IsMaster ? "  (master)" : ""),
                "Room code, public/private, player count, master"
            );
            Visuals.HudLine(
                c,
                "Player List",
                () => !Game.InRoom ? null : string.Join("\n", Game.Others.Select(r => (Game.IsTagged(r) ? "[IT] " : "") + Game.NameOf(r))),
                "Names of everyone in the room"
            );
        }
    }
}
