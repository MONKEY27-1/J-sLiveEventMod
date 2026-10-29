using BepInEx.Configuration;

namespace AstronomicalEvent
{
    // Persistent settings (BepInEx config: BepInEx/config/local.astronomical.event.cfg). Ranges are clamped by BepInEx.
    internal static class AstronomicalSettings
    {
        public static ConfigEntry<float> Volume,
            Effects,
            Lighting,
            Particles,
            Speed,
            Distance,
            Height,
            Yaw,
            PerformerYaw,
            DestructionRadius;
        public static ConfigEntry<bool> Knockback,
            SwimInFlood,
            ShowStage;
        public static ConfigEntry<float> Countdown,
            ShowScale,
            ShowPlateau;
        public static ConfigEntry<bool> SyncBroadcast,
            SyncReceive;
        public static ConfigEntry<string> SyncServer,
            SyncTopic;
        public static ConfigEntry<float> KnockbackStrength;
        public static ConfigEntry<bool> KlomboSync;
        public static ConfigEntry<bool> CameraFx,
            AutoCleanup,
            Debug,
            BypassForestCheck,
            TreeDestruction;
        public static ConfigEntry<int> StartSequence;

        // Custom Spawn Location & Scale settings
        public static ConfigEntry<bool> UseFixedSpawn,
            BuiltInMenu;
        public static ConfigEntry<int> NewYearYear;
        public static ConfigEntry<float> FixedSpawnX,
            FixedSpawnY,
            FixedSpawnZ,
            FixedSpawnYaw;
        public static ConfigEntry<float> StageScale,
            TravisScale;

        public static void Init(ConfigFile c)
        {
            KlomboSync = c.Bind(
                "Klombo",
                "Sync",
                true,
                "Spawn / despawn / mood / size of Klombo are sent to other mod users through the relay (and theirs are received)"
            );
            Volume = c.Bind(
                "Audio",
                "EventVolume",
                2.0f,
                new ConfigDescription(
                    "Event volume. 0-1 = normal; above 1 adds soft-clipped gain boost",
                    new AcceptableValueRange<float>(0f, 3f)
                )
            );
            Effects = c.Bind(
                "Visual",
                "EffectsIntensity",
                1f,
                new ConfigDescription("Overall effects intensity", new AcceptableValueRange<float>(0f, 1f))
            );
            Lighting = c.Bind(
                "Visual",
                "LightingIntensity",
                1f,
                new ConfigDescription("Lights, fog and ambient tint strength", new AcceptableValueRange<float>(0f, 1f))
            );
            Particles = c.Bind(
                "Visual",
                "ParticleDensity",
                0.6f,
                new ConfigDescription("Particle count multiplier", new AcceptableValueRange<float>(0.1f, 1f))
            );
            CameraFx = c.Bind("Visual", "CameraEffects", true, "Screen tint overlay (underwater, flashes)");
            Speed = c.Bind(
                "Timeline",
                "PlaybackSpeed",
                1f,
                new ConfigDescription(
                    "Timeline speed (audio is not pitch-shifted; it is re-synced)",
                    new AcceptableValueRange<float>(0.5f, 2f)
                )
            );
            StartSequence = c.Bind(
                "Timeline",
                "PreferredStartSequence",
                0,
                new ConfigDescription("Sequence (section) index used by Start", new AcceptableValueRange<int>(0, 40))
            );
            AutoCleanup = c.Bind(
                "Behavior",
                "CleanupOnFinish",
                true,
                "Remove all event objects and restore Forest lighting when the timeline ends"
            );
            Debug = c.Bind("Behavior", "DebugLogging", false, "Verbose logging");
            Distance = c.Bind(
                "Placement",
                "StageDistance",
                14f,
                new ConfigDescription("Metres in front of you the stage is placed at Start", new AcceptableValueRange<float>(6f, 40f))
            );
            Height = c.Bind(
                "Placement",
                "HeightOffset",
                0f,
                new ConfigDescription("Vertical offset of the stage", new AcceptableValueRange<float>(-10f, 10f))
            );
            Yaw = c.Bind(
                "Placement",
                "YawOffset",
                0f,
                new ConfigDescription("Degrees to rotate the stage position around you", new AcceptableValueRange<float>(-180f, 180f))
            );
            PerformerYaw = c.Bind(
                "Placement",
                "PerformerYaw",
                0f,
                new ConfigDescription(
                    "Extra yaw (degrees) added to the performer's facing. 0 = faces the audience",
                    new AcceptableValueRange<float>(-180f, 180f)
                )
            );
            TreeDestruction = c.Bind(
                "Destruction",
                "TreeDestruction",
                true,
                "Visual-only: trees/treehouse near the stage topple and break (originals are hidden then restored; colliders untouched)"
            );
            DestructionRadius = c.Bind(
                "Destruction",
                "Radius",
                90f,
                new ConfigDescription("Metres around the stage searched for trees", new AcceptableValueRange<float>(20f, 150f))
            );
            BypassForestCheck = c.Bind(
                "Diagnostics",
                "BypassForestCheck",
                false,
                "DIAGNOSTIC ONLY: allow Start when Forest is not detected (the zone check is otherwise enforced)"
            );

            Knockback = c.Bind(
                "Gameplay",
                "Knockback",
                true,
                "Explosions, meteors, lightning, Travis's stomps and launch rings push you (local only, through the game's own knockback)"
            );
            KnockbackStrength = c.Bind(
                "Gameplay",
                "KnockbackStrength",
                1f,
                new ConfigDescription("Multiplier for all knockback", new AcceptableValueRange<float>(0f, 2f))
            );
            SwimInFlood = c.Bind(
                "Gameplay",
                "SwimInFlood",
                true,
                "During the underwater sequence the game's own swimming is switched on while you are near the stage"
            );
            Countdown = c.Bind(
                "Timeline",
                "CountdownSeconds",
                0f,
                new ConfigDescription(
                    "Seconds of countdown after Start before the event begins (0 = start immediately)",
                    new AcceptableValueRange<float>(0f, 600f)
                )
            );
            ShowStage = c.Bind("Visual", "ShowStage", false, "Show the 3D stage model (off by default: Travis performs in the open arena)");
            SyncBroadcast = c.Bind(
                "Sync",
                "Broadcast",
                true,
                "When you start/stop the event, tell other players who have this mod (any lobby) through the relay"
            );
            SyncReceive = c.Bind("Sync", "Receive", true, "Join events other mod users start (only while you are in Forest and idle)");
            SyncServer = c.Bind(
                "Sync",
                "Server",
                "https://ntfy.sh",
                "Public pub/sub relay used to reach other lobbies (ntfy-compatible). Only 'start/stop event X in N s' is sent"
            );
            SyncTopic = c.Bind(
                "Sync",
                "Topic",
                "astro-event-jclient-7c41e9",
                "Relay channel. Everyone who should see each other's events must use the same topic"
            );
            ShowScale = c.Bind(
                "Showdown",
                "ShowScale",
                1f,
                new ConfigDescription(
                    "Scale of the whole Final Showdown (giants, terrain, effects) around its origin",
                    new AcceptableValueRange<float>(0.3f, 2f)
                )
            );
            ShowPlateau = c.Bind(
                "Showdown",
                "FloorOffset",
                0f,
                new ConfigDescription(
                    "Metres the giants stand above the show origin (the gazebo floor). 0 = on the Forest floor; raise it if the surrounding walls hide their legs",
                    new AcceptableValueRange<float>(0f, 80f)
                )
            );
            NewYearYear = c.Bind(
                "Events",
                "NewYearYear",
                2021,
                new ConfigDescription(
                    "Year shown by the New Year event (2022 also uses the crystal ball)",
                    new AcceptableValueRange<int>(2019, 2022)
                )
            );
            // Spawn Location & Scale
            BuiltInMenu = c.Bind(
                "Menu",
                "BuiltInMenu",
                true,
                "P-key IMGUI menu. Set false if you use a separate menu through AstronomicalEventApi"
            );
            UseFixedSpawn = c.Bind(
                "Placement",
                "UseFixedSpawn",
                true,
                "Event always starts at the saved stage location (saved automatically on the first Start, or via Set Stage Here)"
            );
            FixedSpawnX = c.Bind("Placement", "FixedSpawnX", 0f, "Saved world X position for stage");
            FixedSpawnY = c.Bind("Placement", "FixedSpawnY", 0f, "Saved world Y position for stage");
            FixedSpawnZ = c.Bind("Placement", "FixedSpawnZ", 0f, "Saved world Z position for stage");
            FixedSpawnYaw = c.Bind("Placement", "FixedSpawnYaw", 0f, "Saved rotation/facing yaw for stage");
            StageScale = c.Bind(
                "Placement",
                "StageScale",
                1.0f,
                new ConfigDescription("Scale of the 3D stage model", new AcceptableValueRange<float>(0.2f, 4.0f))
            );
            // This is a target world-space height, not a raw GLB transform multiplier.  Avatar exports
            // use wildly different source units, so multiplying their import transform makes them vanish.
            TravisScale = c.Bind(
                "Placement",
                "TravisScale",
                9.0f,
                new ConfigDescription(
                    "Height in metres of Travis on the stage (he grows to several times this as a giant in later sequences)",
                    new AcceptableValueRange<float>(2.0f, 30.0f)
                )
            );
        }
    }
}
