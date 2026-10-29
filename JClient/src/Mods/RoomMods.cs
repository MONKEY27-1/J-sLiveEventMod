using System;
using System.Collections;
using BepInEx.Configuration;
using GorillaNetworking;
using HarmonyLib;
using UnityEngine;

namespace JClient
{
    // On-board keyboard for names and room codes. Desktop can also just type while the page is open.
    public static class Keys
    {
        public static string Buffer = "";
        static Action<string> onDone; static string returnCat; static int max = 12;

        public static void Open(string what, string initial, int maxLen, Action<string> done)
        {
            Buffer = initial ?? ""; max = maxLen; onDone = done; returnCat = Board.Cat;
            Board.OpenCategory("Keyboard");
            Hud.Toast(what + ": type it, then DONE");
        }
        public static void Type(char c) { if (Buffer.Length < max) Buffer += c; }
        public static void Delete() { if (Buffer.Length > 0) Buffer = Buffer.Substring(0, Buffer.Length - 1); }
        public static void Cancel() { onDone = null; }
        public static void Done() { var d = onDone; var b = Buffer; onDone = null; Board.OpenCategory(returnCat); if (b.Length > 0) d?.Invoke(b); }

        public static void Register()
        {
            foreach (char ch in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") Mods.B("Keyboard", ch.ToString(), () => Type(ch));
            Mods.B("Keyboard", "DEL", Delete);
            Mods.B("Keyboard", "CLR", () => Buffer = "");
            Mods.B("Keyboard", "BACK", () => { Cancel(); Board.OpenCategory(returnCat); });
            Mods.B("Keyboard", "DONE", Done);
        }
    }

    public static class RoomMods
    {
        static ConfigEntry<string> savedCode;
        const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        static string Random(int n) { var s = ""; for (int i = 0; i < n; i++) s += Chars[UnityEngine.Random.Range(0, Chars.Length)]; return s; }
        static string Clean(string s, int max) { var o = ""; foreach (var c in s.ToUpperInvariant()) if (Chars.IndexOf(c) >= 0 && o.Length < max) o += c; return o; }

        // Goes through the game's own computer flow, so name moderation and k-ID permissions still apply.
        public static void SetName(string name)
        {
            var gc = GorillaComputer.instance;
            name = Clean(name, 12);
            if (gc == null || name.Length == 0) return;
            if (!gc.NametagsEnabled) { Hud.Toast("Custom names are turned off for this account"); return; }
            gc.currentName = name;
            gc.ProcessNameState(GorillaKeyboardBindings.enter);
            Hud.Toast("Name change requested: " + name);
        }

        // Mirrors the computer's colour screen: 0-9 steps per channel, saved to PlayerPrefs, then broadcast like the computer does.
        public static void SetColour(int r, int g, int b)
        {
            float rv = r / 9f, gv = g / 9f, bv = b / 9f;
            PlayerPrefs.SetFloat("redValue", rv); PlayerPrefs.SetFloat("greenValue", gv); PlayerPrefs.SetFloat("blueValue", bv); PlayerPrefs.Save();
            GorillaComputer.instance?.UpdateColor(rv, gv, bv);
            GorillaTagger.Instance.UpdateColor(rv, gv, bv);
            if (Game.InRoom) GorillaTagger.Instance.myVRRig.SendRPC("RPC_InitializeNoobMaterial", Photon.Pun.RpcTarget.All, rv, gv, bv);
        }

        public static void ChangeIdentity()
        {
            SetName("MONKE" + UnityEngine.Random.Range(1000, 9999));
            SetColour(UnityEngine.Random.Range(0, 10), UnityEngine.Random.Range(0, 10), UnityEngine.Random.Range(0, 10));
        }

        // Same path as typing a code on the in-game computer (includes the room-name check).
        public static void JoinCode(string code)
        {
            var gc = GorillaComputer.instance;
            code = Clean(code, 10);
            if (gc == null || code.Length == 0) return;
            if (!KIDManager.HasPermissionToUseFeature(EKIDFeatures.Groups) || !KIDManager.HasPermissionToUseFeature(EKIDFeatures.Multiplayer))
            { Hud.Toast("Joining room codes is not allowed for this account"); return; }
            gc.roomToJoin = code;
            AccessTools.Method(typeof(GorillaComputer), "CheckAutoBanListForRoomName").Invoke(gc, new object[] { code });
            Hud.Toast("Joining " + code);
        }

        public static void Leave() { if (Game.InRoom) { NetworkSystem.Instance.ReturnToSinglePlayer(); Hud.Toast("Left room"); } }

        static IEnumerator LeaveThen(Action then)
        {
            if (Game.InRoom) { NetworkSystem.Instance.ReturnToSinglePlayer(); float t = 0; while (Game.InRoom && (t += Time.deltaTime) < 8f) yield return null; yield return new WaitForSeconds(0.5f); }
            then();
        }

        static void JoinRandomPublic()
        {
            var pnc = PhotonNetworkController.Instance;
            var trigger = pnc != null && pnc.currentJoinTrigger != null ? pnc.currentJoinTrigger : UnityEngine.Object.FindFirstObjectByType<GorillaNetworkJoinTrigger>();
            if (pnc == null || trigger == null) { Hud.Toast("No join trigger found - walk into a map first"); return; }
            Plugin.I.StartCoroutine(LeaveThen(() => pnc.AttemptToJoinPublicRoom(trigger)));
            Hud.Toast("Joining a random public room");
        }

        public static void Register()
        {
            const string c = "Room";
            savedCode = Plugin.I.Config.Bind("Room", "SavedCode", "JCLIENT", "Room code used by Join Saved Room");
            Mods.B(c, "Change Name", () => Keys.Open("Name", GorillaComputer.instance?.savedName, 12, SetName)).Info("Type a new name (max 12) on the board or keyboard");
            Mods.B(c, "Random Name", () => SetName("MONKE" + UnityEngine.Random.Range(1000, 9999))).Info("Sets a random MONKE#### name");
            Mods.B(c, "Set Room Code", () => Keys.Open("Room code", savedCode.Value, 10, s => { savedCode.Value = Clean(s, 10); Hud.Toast("Saved code: " + savedCode.Value); }))
                .Live(() => "Set Room Code: " + savedCode.Value).Info("Choose the code used by Join Saved Room");
            Mods.B(c, "Join Saved Room", () => JoinCode(savedCode.Value)).Info("Join (or create) the room code you set");
            Mods.B(c, "Join Code Now", () => Keys.Open("Join code", "", 10, JoinCode)).Info("Type any code and join it straight away");
            Mods.B(c, "Join Random Code", () => JoinCode(Random(6))).Info("Join a brand-new private room with a random 6-character code");
            Mods.B(c, "Join Random Public", JoinRandomPublic).Info("Leave and join a random public room for the current map");
            Mods.B(c, "Leave Room", Leave).Info("Disconnect to single player");
            Mods.B(c, "Rejoin Room", () =>
            {
                if (!Game.InRoom) return;
                var code = NetworkSystem.Instance.RoomName;
                Plugin.I.StartCoroutine(LeaveThen(() => JoinCode(code)));
            }).Info("Leave and join the same code again");
            Mods.B(c, "Copy Room Code", () => { if (Game.InRoom) { GUIUtility.systemCopyBuffer = NetworkSystem.Instance.RoomName; Hud.Toast("Copied " + NetworkSystem.Instance.RoomName); } })
                .Info("Copies the current room code to the clipboard");
        }
    }
}
