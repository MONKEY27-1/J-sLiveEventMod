using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    [BepInPlugin("jclient.menu", "J CLIENT", "1.0.0")]
    [BepInDependency("local.astronomical.event", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin I;
        public static ManualLogSource Log;
        bool started;

        void Awake()
        {
            I = this; Log = Logger;
            new Harmony("jclient.menu").PatchAll();

            Settings.Register(); Movement.Register(); MovementPlus.Register(); RigMods.Register(); Soundboard.Register(); Master.Register(); Fun.Register(); VisualsPlus.Register(); MovementMore.Register(); RigEmotes.Register(); Extras.Register(); Visuals.Register(); TagMods.Register(); Utilities.Register();
            RoomMods.Register(); Keys.Register(); Cosmetics.Register(); Safety.Register(); WorldFx.Register();
            try { KlomboMods.Register(); } catch (Exception e) { Log.LogWarning("Klombo not available: " + e.Message); }
            try { EventMods.Register(); } catch (Exception e) { Log.LogWarning("Astronomical Event not available: " + e.Message); }

            Bind(Config);
            SelfCheck();
            Log.LogInfo("J CLIENT loaded: " + Mods.All.Count(m => m.Cat != "Keyboard") + " modules. Q = desktop menu, hold Y = VR menu.");
        }

        static string Key(Mod m) => new string(m.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        static void Bind(ConfigFile cfg)
        {
            foreach (var m in Mods.All)
            {
                if (m.Cat == "Keyboard") continue;
                string sec = "Mods." + m.Cat.Replace(" ", ""), k = Key(m);
                if (m.Toggle) m.cOn = cfg.Bind(sec, k + ".On", m.DefaultOn, m.Desc);
                if (m.Options != null) { m.cOpt = cfg.Bind(sec, k + ".Option", m.Opt, string.Join(" / ", m.Options)); m.Opt = Mathf.Clamp(m.cOpt.Value, 0, m.Options.Length - 1); }
                m.cKey = cfg.Bind(sec, k + ".Key", "None", "Keybind (Unity Input System key name)");
                m.cFav = cfg.Bind(sec, k + ".Favorite", false, "Shown on the Favorites page"); m.Fav = m.cFav.Value;
                if (Enum.TryParse(m.cKey.Value, out Key key)) m.Bind = key;
            }
        }

        // Fails loudly in the log if registration is broken (duplicate names would share config keys).
        static void SelfCheck()
        {
            var dupes = Mods.All.GroupBy(Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0) Log.LogError("Duplicate module names: " + string.Join(", ", dupes));
            var bad = Mods.All.Where(m => m.Cat != "Keyboard" && (m.Cat == "Favorites" || m.Cat == "Enabled" || !Mods.Categories.Contains(m.Cat))).Select(m => m.Name).ToList();
            if (bad.Count > 0) Log.LogError("Modules with unknown category: " + string.Join(", ", bad));
            int n = Mods.All.Count(m => m.Cat != "Keyboard");
            if (n < 100) Log.LogError("Expected at least 100 modules, found " + n);
            if (Board.CharOf(UnityEngine.InputSystem.Key.A) != 'A' || Board.CharOf(UnityEngine.InputSystem.Key.Z) != 'Z' || Board.CharOf(UnityEngine.InputSystem.Key.Digit0) != '0' || Board.CharOf(UnityEngine.InputSystem.Key.Digit9) != '9' || Board.CharOf(UnityEngine.InputSystem.Key.Numpad7) != '7')
                Log.LogError("Keyboard typing map is wrong for this Input System version");
        }

        void Update()
        {
            Inp.Poll();
            if (!Game.Ready) return;
            if (!started)
            {
                started = true;
                foreach (var m in Mods.All) if (m.Toggle && m.cOn != null && m.cOn.Value != m.On) m.Set(m.cOn.Value, quiet: true);
            }
            Board.Update();

        foreach (var m in Mods.All)
            {
                if (!m.On)
                    continue;

             m.Safe(m.Tick);
            }
            Tagging.Tick();
        }

        void LateUpdate()
        {
            if (!Game.Ready) return;
            foreach (var m in Mods.All) if (m.On) m.Safe(m.Late);
            Gun.EndFrame();
            Hud.VRHud();
        }

        void OnGUI() => Board.OnGUI();
    }
}
