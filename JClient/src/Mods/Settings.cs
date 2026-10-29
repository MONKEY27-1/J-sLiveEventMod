using UnityEngine;

namespace JClient
{
    public static class Settings
    {
        public static Mod VRHud, ShowHud, Toasts, Sounds, Haptics, Laser, BoardRight, GunForward, Accent, EspColor;
        static readonly Color[] accents = { new Color(0f, 0.9f, 1f), new Color(0.3f, 1f, 0.9f), new Color(0.1f, 0.5f, 1f), new Color(0.65f, 0.95f, 1f) };

        public static Color AccentColor => Accent == null ? accents[0] : Accent.Opt < accents.Length ? accents[Accent.Opt] : Fx.Rainbow(0.1f);

        public static Color Esp(VRRig r)
        {
            switch (EspColor.Opt)
            {
                case 1: return Game.IsTagged(r) ? new Color(1f, 0.25f, 0.2f) : new Color(0.2f, 1f, 0.5f);
                case 2: return Fx.Rainbow(0.3f, r.GetInstanceID() * 0.13f);
                default: return AccentColor;
            }
        }

        public static void Register()
        {
            const string c = "Settings";
            ShowHud = Mods.T(c, "Show HUD").StartOn().Info("Watermark, info lines and active mods list on the desktop window");
            VRHud = Mods.T(c, "VR Floating HUD").StartOn().Info("In VR, shows the HUD info and enabled mods floating at the top-left of your view");
            Toasts = Mods.T(c, "Notifications").StartOn().Info("Pop-ups like 'Fly ON'");
            Sounds = Mods.T(c, "Menu Sounds").StartOn().Info("Click sound on button press");
            Haptics = Mods.T(c, "Menu Haptics").StartOn().Info("Controller buzz on button press");
            Laser = Mods.T(c, "VR Laser Pointer").StartOn().Info("Point at board buttons and pull trigger (finger poke always works)");
            BoardRight = Mods.T(c, "Menu On Right Hand").Info("Board on the right hand, opened with B; poke with the left hand");
            GunForward = Mods.T(c, "Gun Aims Forward").Info("VR gun/laser direction. Off = out of the bottom of the hand, On = hand forward");
            Accent = Mods.C(c, "Accent", "Cyan", "Aqua", "Electric Blue", "Ice", "Rainbow").Info("Menu and effect colour");
            EspColor = Mods.C(c, "ESP Colour", "Accent", "Tag State", "Rainbow").Info("Colour used by ESP, tracers and chams");
            Mod fps = null; int fpsBackup = 0, vsyncBackup = 0;
            fps = Mods.T(c, "FPS Cap", () =>
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = new[] { 120, 90, 72, 60, 45, 30, 15, -1 }[fps.Opt]; // -1 = unlocked
            }, () => { fpsBackup = Application.targetFrameRate; vsyncBackup = QualitySettings.vSyncCount; },
               () => { Application.targetFrameRate = fpsBackup; QualitySettings.vSyncCount = vsyncBackup; })
                .Opts("FPS", "120", "90", "72", "60", "45", "30", "15", "Unlocked").Def(3).Info("Caps (or unlocks) your frame rate. In VR the headset usually sets its own rate");
            Mods.B(c, "Disable All Mods", () =>
            {
                foreach (var m in Mods.All) if (m.On && m.Cat != "Settings") m.Set(false, quiet: true);
                Hud.Toast("All mods disabled");
            }).Info("Turns off every enabled module");
            Mods.B(c, "Clear Keybinds", () =>
            {
                foreach (var m in Mods.All) { m.Bind = UnityEngine.InputSystem.Key.None; if (m.cKey != null) m.cKey.Value = "None"; }
                Hud.Toast("Keybinds cleared");
            }).Info("Removes every keybind");
        }
    }
}
