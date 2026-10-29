using System;
using System.Linq;
using ExitGames.Client.Photon;
using GorillaGameModes;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace JClient
{
    // Room-master tools. Every one is OthersOnly (private/modded rooms) and also needs you to be master.
    public static class Master
    {
        static GorillaTagManager Tm => GameMode.ActiveGameMode as GorillaTagManager;

        static bool IsMaster()
        {
            if (!Game.CanAffectOthers)
            {
                Hud.Toast("Master tools only work in private or modded rooms");
                return false;
            }
            if (Game.IsMaster)
                return true;
            Hud.Toast("You need to be room master (Join Random Code makes you master)");
            return false;
        }

        static bool TagGame(out GorillaTagManager tm)
        {
            tm = Tm;
            if (!IsMaster())
                return false;
            if (tm == null)
            {
                Hud.Toast("Not a tag/infection game");
                return false;
            }
            return true;
        }

        // RoomSystem is internal; this is the same call the game makes for stuns and tag freezes (only accepted from the master).
        static void Status(string effect, NetPlayer p)
        {
            var rs = AccessTools.TypeByName("RoomSystem");
            AccessTools
                .Method(rs, "SendStatusEffectToPlayer")
                .Invoke(null, new object[] { Enum.Parse(AccessTools.Inner(rs, "StatusEffects"), effect), p });
        }

        static void StatusGun(string effect, string verb)
        {
            if (!Gun.Get(out _, out var r) || !Gun.FireDown || r == null || !IsMaster())
                return;
            var p = Game.Owner(r);
            if (p == null)
                return;
            Status(effect, p);
            Hud.Toast(verb + " " + p.NickName);
        }

        static void StatusAll(string effect, string verb)
        {
            if (!IsMaster())
                return;
            foreach (var r in Game.Others)
            {
                var p = Game.Owner(r);
                if (p != null)
                    Status(effect, p);
            }
            Hud.Toast(verb + " everyone");
        }

        // The room's gameMode property holds map + queue + mode name; swap just the mode name.
        static void SetMode(GameModeType type)
        {
            if (!IsMaster())
                return;
            var target = GameMode.GetGameModeInstance(type);
            if (target == null)
            {
                Hud.Toast(type + " isn't available here");
                return;
            }
            string cur = NetworkSystem.Instance.GameModeString ?? "",
                name = target.GameTypeName();
            var old = GameMode
                .gameModeNames.Where(n => !string.IsNullOrEmpty(n) && cur.Contains(n))
                .OrderByDescending(n => n.Length)
                .FirstOrDefault();
            if (old == null)
            {
                Hud.Toast("Couldn't read the current gamemode");
                return;
            }
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { "gameMode", cur.Replace(old, name) } });
            Hud.Toast("Gamemode -> " + name);
        }

        static Mod M(Mod m)
        {
            m.Others = true;
            return m;
        } // shows the * marker; the room check is in IsMaster()

        public static void Register()
        {
            const string c = "Master";

            M(
                    Mods.B(
                        c,
                        "Restart Round",
                        () =>
                        {
                            if (TagGame(out var tm))
                            {
                                tm.ResetGame();
                                Hud.Toast("Round restarted");
                            }
                        }
                    )
                )
                .Info("Resets the tag/infection round");
            M(
                    Mods.B(
                        c,
                        "End Round",
                        () =>
                        {
                            if (!TagGame(out var tm))
                                return;
                            foreach (var p in GameMode.ParticipatingPlayers)
                                if (!tm.currentInfected.Contains(p))
                                    tm.AddInfectedPlayer(p);
                            Hud.Toast("Everyone infected - round over");
                        }
                    )
                )
                .Info("Infects everyone so the round ends");
            M(
                    Mods.B(
                        c,
                        "Infect Random",
                        () =>
                        {
                            if (!TagGame(out var tm))
                                return;
                            var free = GameMode.ParticipatingPlayers.Where(p => !tm.currentInfected.Contains(p)).ToList();
                            if (free.Count > 0)
                            {
                                var p = free[UnityEngine.Random.Range(0, free.Count)];
                                tm.AddInfectedPlayer(p);
                                Hud.Toast(p.NickName + " is now it");
                            }
                        }
                    )
                )
                .Info("Makes a random runner it");
            M(
                    Mods.T(
                        c,
                        "Set It Gun",
                        () =>
                        {
                            if (!Gun.Get(out _, out var r) || !Gun.FireDown || r == null || !TagGame(out var tm))
                                return;
                            var p = Game.Owner(r);
                            if (p != null && !tm.currentInfected.Contains(p))
                            {
                                tm.AddInfectedPlayer(p);
                                Hud.Toast(p.NickName + " is now it");
                            }
                        }
                    )
                )
                .Info("Fire at a player to make them it");
            M(
                    Mods.T(
                        c,
                        "Untag Gun",
                        () =>
                        {
                            if (!Gun.Get(out _, out var r) || !Gun.FireDown || r == null || !TagGame(out var tm))
                                return;
                            var p = Game.Owner(r);
                            if (p != null && tm.currentInfected.Remove(p))
                            {
                                tm.UpdateInfectionState();
                                Hud.Toast(p.NickName + " untagged");
                            }
                        }
                    )
                )
                .Info("Fire at a player to untag them");
            M(
                    Mods.B(
                        c,
                        "Untag Everyone",
                        () =>
                        {
                            if (TagGame(out var tm))
                            {
                                tm.ClearInfectionState();
                                Hud.Toast("Everyone untagged");
                            }
                        }
                    )
                )
                .Info("Clears every infected player");

            M(Mods.T(c, "Freeze Gun", () => StatusGun("FrozenTime", "Froze")))
                .Info("Fire at a player to freeze them (the game's freeze-tag effect)");
            M(Mods.T(c, "Slow Gun", () => StatusGun("SetSlowedTime", "Slowed")))
                .Info("Fire at a player to slow them (the game's slow effect)");
            M(Mods.T(c, "Tag Stun Gun", () => StatusGun("TaggedTime", "Stunned"))).Info("Fire at a player to give them the tagged stun");
            M(Mods.B(c, "Freeze Everyone", () => StatusAll("FrozenTime", "Froze"))).Info("Freezes every other player");
            M(Mods.B(c, "Slow Everyone", () => StatusAll("SetSlowedTime", "Slowed"))).Info("Slows every other player");
            M(Mods.B(c, "Stun Everyone", () => StatusAll("TaggedTime", "Stunned"))).Info("Gives everyone the tagged stun");
            M(Mods.B(c, "Clear Effects", () => StatusAll("UnTagged", "Cleared effects on")))
                .Info("Removes stun/slow effects from everyone");

            M(
                    Mods.T(
                        c,
                        "Lock Room",
                        () =>
                        {
                            if (Game.IsMaster && PhotonNetwork.CurrentRoom != null)
                                PhotonNetwork.CurrentRoom.IsOpen = false;
                        },
                        null,
                        () =>
                        {
                            if (Game.IsMaster && PhotonNetwork.CurrentRoom != null)
                                PhotonNetwork.CurrentRoom.IsOpen = true;
                        }
                    )
                )
                .Info("Nobody new can join while this is on");
            M(
                    Mods.T(
                        c,
                        "Hide Room",
                        () =>
                        {
                            if (Game.IsMaster && PhotonNetwork.CurrentRoom != null)
                                PhotonNetwork.CurrentRoom.IsVisible = false;
                        },
                        null,
                        () =>
                        {
                            if (Game.IsMaster && PhotonNetwork.CurrentRoom != null)
                                PhotonNetwork.CurrentRoom.IsVisible = true;
                        }
                    )
                )
                .Info("Room is hidden from matchmaking (code still works)");

            foreach (
                var (label, type) in new[]
                {
                    ("Casual", GameModeType.Casual),
                    ("Infection", GameModeType.Infection),
                    ("Hunt", GameModeType.HuntDown),
                    ("Paintbrawl", GameModeType.Paintbrawl),
                    ("Freeze Tag", GameModeType.FreezeTag),
                    ("Ghost", GameModeType.Ghost),
                    ("Ambush", GameModeType.Ambush),
                    ("Guardian", GameModeType.Guardian),
                    ("Super Infect", GameModeType.SuperInfect),
                }
            )
                M(Mods.B(c, "Mode: " + label, () => SetMode(type)))
                    .Info("Switches the room's gamemode to " + label + " (if the game allows it)");

            Visuals.HudLine(
                c,
                "Master Info",
                () =>
                    !Game.InRoom
                        ? null
                        : "Master: "
                            + (NetworkSystem.Instance.MasterClient?.NickName ?? "?")
                            + (Game.IsMaster ? " (you)" : "")
                            + (
                                PhotonNetwork.CurrentRoom != null
                                    ? (PhotonNetwork.CurrentRoom.IsOpen ? "" : "  LOCKED")
                                        + (PhotonNetwork.CurrentRoom.IsVisible ? "" : "  HIDDEN")
                                    : ""
                            ),
                "Shows who the room master is and whether the room is locked/hidden"
            );
        }
    }
}
