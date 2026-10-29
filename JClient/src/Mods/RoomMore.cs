using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JClient
{
    // More Room tools. Names, colours and joins go through RoomMods, i.e. the game's own computer flow
    // (name moderation and account permissions still apply).
    public static class RoomMore
    {
        static string DailyCode => "JC" + DateTime.UtcNow.ToString("yyMMdd");

        public static void Register()
        {
            const string c = "Room";

            Mods.B(
                    c,
                    "Join Last Room",
                    () =>
                    {
                        var code = MoveKit.LastRoom; // read before leaving: leaving makes the current room the "last" one
                        if (string.IsNullOrEmpty(code))
                        {
                            Hud.Toast("No earlier room yet");
                            return;
                        }
                        if (Game.InRoom)
                            Plugin.I.StartCoroutine(RoomMods.LeaveThen(() => RoomMods.JoinCode(code)));
                        else
                            RoomMods.JoinCode(code);
                    }
                )
                .Live(() => "Join Last Room" + (string.IsNullOrEmpty(MoveKit.LastRoom) ? "" : ": " + MoveKit.LastRoom))
                .Info("Joins the room you were in before this one");

            float alone = 0f;
            Mods.T(
                    c,
                    "Leave When Alone",
                    () =>
                    {
                        if (!Game.InRoom)
                        {
                            alone = 0f;
                            return;
                        }
                        alone = Game.Others.Any() ? 0f : alone + Time.deltaTime;
                        if (alone < 15f)
                            return;
                        alone = 0f;
                        Hud.Toast("Everyone left - leaving too");
                        RoomMods.Leave();
                    }
                )
                .Info("Leaves the room once you've been the only one in it for 15 seconds");

            Mods.B(
                    c,
                    "Random Colour",
                    () =>
                    {
                        RoomMods.SetColour(
                            UnityEngine.Random.Range(0, 10),
                            UnityEngine.Random.Range(0, 10),
                            UnityEngine.Random.Range(0, 10)
                        );
                        Hud.Toast("New colour");
                    }
                )
                .Info("Picks a random fur colour, the same way the in-game computer does (everyone sees it)");

            Mods.B(
                    c,
                    "Match Colour",
                    () =>
                    {
                        var r = Game.Nearest(Game.Head.position);
                        if (r == null)
                        {
                            Hud.Toast("Nobody nearby");
                            return;
                        }
                        var col = r.playerColor;
                        RoomMods.SetColour(Mathf.RoundToInt(col.r * 9f), Mathf.RoundToInt(col.g * 9f), Mathf.RoundToInt(col.b * 9f));
                        Hud.Toast("Matched " + Game.NameOf(r) + "'s colour");
                    }
                )
                .Info("Copies the fur colour of the closest player - twin with your friends");

            Mods.B(c, "Name From Clipboard", () => RoomMods.SetName(GUIUtility.systemCopyBuffer ?? ""))
                .Info("Sets your name to the text in your clipboard (letters and numbers, max 12; name moderation still applies)");

            Mods.B(c, "Join Code From Clipboard", () => RoomMods.JoinCode(GUIUtility.systemCopyBuffer ?? ""))
                .Info("Joins the room code in your clipboard - paste codes from friends straight in");

            var present = new HashSet<string>();
            float scan = 0f;
            bool scanned = false;
            Mods.T(
                    c,
                    "Join/Leave Alerts",
                    () =>
                    {
                        if ((scan -= Time.deltaTime) > 0f)
                            return;
                        scan = 1f;
                        if (!Game.InRoom)
                        {
                            present.Clear();
                            scanned = false;
                            return;
                        }
                        var now = new HashSet<string>(Game.Others.Select(Game.NameOf).Where(n => !string.IsNullOrEmpty(n)));
                        if (scanned) // the first look after joining is the starting list, not a wave of joins
                        {
                            foreach (var n in now)
                                if (!present.Contains(n))
                                {
                                    Hud.Toast(n + " joined");
                                    MoveKit.Buzz(true, 0.2f, 0.05f);
                                }
                            foreach (var n in present)
                                if (!now.Contains(n))
                                    Hud.Toast(n + " left");
                        }
                        present.Clear();
                        present.UnionWith(now);
                        scanned = true;
                    },
                    () =>
                    {
                        present.Clear();
                        scanned = false;
                    }
                )
                .Info("Tells you (with a little buzz) when someone joins or leaves the room");

            bool autoJoined = false;
            float readyAt = -1f;
            Mods.T(
                    c,
                    "Auto Join On Start",
                    () =>
                    {
                        if (autoJoined)
                            return;
                        if (readyAt < 0f)
                            readyAt = Time.time;
                        if (Game.InRoom)
                        {
                            autoJoined = true;
                            return;
                        }
                        if (Time.time - readyAt < 6f) // let the game finish connecting first
                            return;
                        autoJoined = true;
                        RoomMods.JoinCode(RoomMods.SavedCode);
                    }
                )
                .Info("When the game starts, joins your saved room code (Set Room Code) by itself - once per launch");

            Mods.B(c, "Daily Room", () => RoomMods.JoinCode(DailyCode))
                .Live(() => "Daily Room: " + DailyCode)
                .Info("Joins today's J CLIENT meet-up code; it changes every day and is the same for everyone");

            Mods.B(
                    c,
                    "Copy Player List",
                    () =>
                    {
                        if (!Game.InRoom)
                        {
                            Hud.Toast("Not in a room");
                            return;
                        }
                        var names = Game.Others.Select(Game.NameOf).ToList();
                        GUIUtility.systemCopyBuffer = string.Join(", ", names);
                        Hud.Toast("Copied " + names.Count + " names");
                    }
                )
                .Info("Copies the names of everyone in the room to your clipboard");

            float publicFor = 0f;
            Mods.T(
                    c,
                    "Auto Leave Public",
                    () =>
                    {
                        publicFor = Game.InRoom && !Game.IsPrivate && !Game.IsModded ? publicFor + Time.deltaTime : 0f;
                        if (publicFor < 3f)
                            return;
                        publicFor = 0f;
                        Hud.Toast("Public lobby - leaving (Auto Leave Public)");
                        RoomMods.Leave();
                    }
                )
                .Info(
                    "Keeps you out of public lobbies: leaves any public, non-modded room after 3 seconds. Private and modded rooms are fine"
                );

            float played = 0f;
            bool warned = false;
            Mod session = null;
            session = Mods.T(
                    c,
                    "Session Time Limit",
                    () =>
                    {
                        if (!Game.InRoom)
                            return;
                        played += Time.deltaTime;
                        float limit = new[] { 15f, 30f, 60f, 90f }[session.Opt] * 60f;
                        if (!warned && played > limit - 60f)
                        {
                            warned = true;
                            Hud.Toast("1 minute left in this session");
                            MoveKit.BuzzBoth(0.3f, 0.2f);
                        }
                        if (played < limit)
                            return;
                        played = 0f;
                        warned = false;
                        Hud.Toast("Time's up - leaving the room");
                        RoomMods.Leave();
                    },
                    () =>
                    {
                        played = 0f;
                        warned = false;
                    }
                )
                .Opts("Limit", "15 min", "30 min", "60 min", "90 min")
                .Def(1)
                .Live(() => "Session Time Limit" + (session.On ? " (" + Mathf.FloorToInt(played / 60f) + " min played)" : ""))
                .Info(
                    "Counts your time in rooms and leaves when the limit is up, with a warning 1 minute before - handy for screen-time limits"
                );
        }
    }
}
