using System;
using AstronomicalEvent;

namespace JClient
{
    // The Klombo page of the board (the Klombo itself lives in the Astronomical Event plugin: KlomboApi). Live labels show what is going on; a floating name plate over Klombo shows its mood, happiness and hunger.
    public static class KlomboMods
    {
        static readonly float[] Sizes = { 0.25f, 0.4f, 0.6f, 1f, 1.5f, 2f, 3f };

        static float NextSize(float cur)
        {
            foreach (var v in Sizes)
                if (v > cur + 0.001f)
                    return v;
            return Sizes[0];
        }

        static bool Ready()
        {
            if (!KlomboApi.IsAvailable)
            {
                Hud.Toast("Klombo: plugin not loaded");
                return false;
            }
            return true;
        }

        public static void Register()
        {
            const string c = "Klombo";
            Mods.B(c, "Klombo Status", () => Hud.Toast(KlomboApi.Status))
                .Live(() => !KlomboApi.IsAvailable ? "Klombo: plugin not loaded" : KlomboApi.Status)
                .Info("Live status: mood, whether you are riding / it is following you, its happiness and hunger");
            Mods.B(
                    c,
                    "Spawn Klombo",
                    () =>
                    {
                        if (!Ready())
                            return;
                        if (KlomboApi.Alive)
                            KlomboApi.Despawn();
                        else
                        {
                            KlomboApi.Spawn();
                            if (KlomboApi.Loading)
                                Hud.Toast("Klombo loading...");
                        }
                    }
                )
                .Live(() =>
                    KlomboApi.Loading ? "Klombo loading..."
                    : KlomboApi.Alive ? "Despawn Klombo"
                    : "Spawn Klombo"
                )
                .Info(
                    "Spawns Klombo in front of you (it loads the first time), or removes it. With Tell Others on, other mod users see it too"
                );
            Mods.B(
                    c,
                    "Klombo Mood",
                    () =>
                    {
                        if (!Ready())
                            return;
                        KlomboApi.Mood = (KlomboApi.Mood + 1) % 3;
                        Hud.Toast("Klombo mood: " + KlomboApi.MoodName);
                    }
                )
                .Live(() => "Mood: " + (KlomboApi.IsAvailable ? KlomboApi.MoodName : "-"))
                .Info(
                    "Calm: wanders and looks at you. Playful: bounds about and runs for berries. Hostile: chases you, stomps and roars with knockback"
                );
            Mods.B(
                    c,
                    "Klombo Size",
                    () =>
                    {
                        if (!Ready())
                            return;
                        KlomboApi.Size = NextSize(KlomboApi.Size);
                        Hud.Toast("Klombo size " + KlomboApi.Size.ToString("0.##") + "x");
                    }
                )
                .Live(() => "Size: " + (KlomboApi.IsAvailable ? KlomboApi.Size.ToString("0.##") + "x" : "-"))
                .Info("1x is about 13 m long; 0.25x is the size of a big dog-house, 3x is huge");
            Mods.B(
                    c,
                    "Ride Klombo",
                    () =>
                    {
                        if (!Ready())
                            return;
                        if (KlomboApi.Riding)
                            KlomboApi.Dismount();
                        else
                            KlomboApi.Ride();
                    }
                )
                .Live(() => KlomboApi.Riding ? "Dismount Klombo" : "Ride Klombo")
                .Info("Puts you on its back. Left stick (or W / S and A / D) steers: forward / back and turn. Jump off to get down");
            Mods.B(
                    c,
                    "Spawn Klomberries",
                    () =>
                    {
                        if (!Ready())
                            return;
                        KlomboApi.SpawnBerries();
                    }
                )
                .Live(() => "Klomberries: " + KlomboApi.BerryCount + "  (press = 5 more)")
                .Info(
                    "Glowing blue berries in front of you. Grab one with grip and hold it near Klombo's mouth (or drop it there) to feed it; a hungry Klombo walks to them by itself"
                );
            Mods.B(
                    c,
                    "Call Klombo",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.CallToMe();
                    }
                )
                .Info("Moves Klombo next to you");
            Mods.B(
                    c,
                    "Klombo Follow",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.Follow = !KlomboApi.Follow;
                    }
                )
                .Live(() => "Follow Me: " + (KlomboApi.Follow ? "ON" : "OFF"))
                .Info("Klombo trails after you like a pet");
            Mods.B(
                    c,
                    "Klombo Roar",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.Roar();
                    }
                )
                .Info("Klombo roars: a big shock that pushes you away (Event Knockback applies)");
            Mods.B(
                    c,
                    "Klombo Stomp",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.Stomp();
                    }
                )
                .Info("A ground stomp with a dust ring and knockback");
            Mods.B(
                    c,
                    "Klombo Hop",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.Hop();
                    }
                )
                .Info("A happy little hop");
            Mods.B(
                    c,
                    "Klombo Name Plate",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.NamePlate = !KlomboApi.NamePlate;
                    }
                )
                .Live(() => "Name Plate: " + (KlomboApi.IsAvailable && KlomboApi.NamePlate ? "ON" : "OFF"))
                .Info("The floating panel over Klombo with its mood and happiness / hunger bars");
            Mods.B(
                    c,
                    "Klombo Tell Others",
                    () =>
                    {
                        if (Ready())
                            KlomboApi.Sync = !KlomboApi.Sync;
                    }
                )
                .Live(() => "Tell Others: " + (KlomboApi.IsAvailable && KlomboApi.Sync ? "ON" : "OFF"))
                .Info(
                    "Spawn / despawn / mood / size are sent to other mod users, who then see the same Klombo wandering. Riding and feeding stay local"
                );
        }
    }
}
