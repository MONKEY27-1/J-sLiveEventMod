using System.Linq;
using GorillaGameModes;
using UnityEngine;

namespace JClient
{
    // Everything marked OthersOnly switches itself off in public lobbies.
    public static class TagMods
    {
        static GorillaTagManager Tm => GameMode.ActiveGameMode as GorillaTagManager;
        static bool MasterTm(out GorillaTagManager tm)
        {
            tm = Tm;
            if (tm != null && Game.IsMaster) return true;
            Hud.Toast(tm == null ? "Not in a tag/infection game" : "You need to be room master for this");
            return false;
        }

        static float alarmCooldown, defaultReach = -1f;
        static LineRenderer finder;

        public static void Register()
        {
            const string c = "Tag";
            Mod aura = null, reach = null;

            Mods.T(c, "Tag Gun", () => { if (Gun.Get(out _, out var r) && Gun.FireDown && r != null) Tagging.Tag(r); }) .Info("Aim at a player and fire to tag them");
            Mods.B(c, "Tag All", () => { foreach (var r in Game.Others) Tagging.Tag(r); Hud.Toast("Tagging " + Tagging.Pending + " players"); }) .Info("Tags every untagged player, one at a time");
            Mods.B(c, "Tag Nearest", () => Tagging.Tag(Game.Nearest(Game.Head.position, r => !Game.IsTagged(r)))).Info("Tags the closest untagged player");
            Mods.B(c, "Tag Random", () => { var l = Game.Others.Where(r => !Game.IsTagged(r)).ToList(); if (l.Count > 0) Tagging.Tag(l[Random.Range(0, l.Count)]); }).Info("Tags a random untagged player");
            aura = Mods.T(c, "Tag Aura", () =>
            {
                float d = new[] { 2f, 4f, 8f }[aura.Opt] * Game.Scale;
                foreach (var r in Game.Others) if (!Game.IsTagged(r) && Vector3.Distance(r.transform.position, Game.Head.position) < d) Tagging.Tag(r);
            }).Opts("Aura Range", "2 m", "4 m", "8 m") .Info("Automatically tags anyone who gets close");
            reach = Mods.T(c, "Tag Reach", () => { if (defaultReach < 0) defaultReach = Game.T.maxTagDistance; Game.T.maxTagDistance = new[] { 3f, 5f, 8f }[reach.Opt]; },
                null, () => { if (defaultReach > 0) Game.T.maxTagDistance = defaultReach; defaultReach = -1f; })
                .Opts("Reach", "3 m", "5 m", "8 m") .Info("Longer tag distance from your hands");
            Mods.T(c, "No Tag Freeze").OnLate(() => Game.P.disableMovement = false).Info("Skip the freeze after getting tagged");

            Mods.B(c, "Tag Self", () => { if (MasterTm(out var tm)) tm.AddInfectedPlayer(NetworkSystem.Instance.LocalPlayer); }) .Info("Master only: make yourself it");
            Mods.B(c, "Untag Self", () => { if (MasterTm(out var tm)) { tm.currentInfected.Remove(NetworkSystem.Instance.LocalPlayer); tm.UpdateInfectionState(); } }) .Info("Master only: remove yourself from the infected");
            Mods.B(c, "Untag All", () => { if (MasterTm(out var tm)) { tm.ClearInfectionState(); Hud.Toast("Infection cleared"); } }) .Info("Master only: clears every infected player");

            Mods.T(c, "Follow Gun", () =>
            {
                if (Gun.Get(out _, out var r) && Gun.Firing && r != null) RigCtl.Hold(r.transform.position - r.transform.forward * 0.6f * r.scaleFactor, r.transform.rotation);
            }).Info("Hold fire on a player and your monke follows right behind them");
            Mods.T(c, "Orbit Gun", () =>
            {
                if (!Gun.Get(out _, out var r) || !Gun.Firing || r == null) return;
                float a = Time.time * 3f; RigCtl.Hold(r.transform.position + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 1.2f * r.scaleFactor);
            }).Info("Hold fire on a player and your monke circles them");

            // Local-only helpers (fine anywhere)
            Mods.T(c, "It Finder", () =>
            {
                bool meIt = Game.MeTagged;
                var target = Game.Nearest(Game.Head.position, r => Game.IsTagged(r) != meIt);
                if (finder == null) finder = Fx.Line(Color.red, 0.01f);
                finder.gameObject.SetActive(target != null);
                if (target == null) return;
                var start = Game.Head.position - Vector3.up * 0.3f;
                finder.SetPosition(0, start); finder.SetPosition(1, start + (target.transform.position - start).normalized * 0.8f);
                finder.material.color = meIt ? new Color(0.2f, 1f, 0.5f) : new Color(1f, 0.25f, 0.2f);
            }, null, () => { Fx.Kill(finder); finder = null; }).Info("Arrow toward the nearest tagger (or nearest runner when you're it)");
            Mods.T(c, "Tag Alarm", () =>
            {
                alarmCooldown -= Time.deltaTime;
                if (Game.MeTagged || alarmCooldown > 0) return;
                var t = Game.Nearest(Game.Head.position, Game.IsTagged);
                if (t != null && Vector3.Distance(t.transform.position, Game.Head.position) < 6f * Game.Scale)
                { alarmCooldown = 1.5f; Hud.Toast("Tagger close: " + Game.NameOf(t)); try { Game.T.StartVibration(true, 0.6f, 0.15f); Game.T.StartVibration(false, 0.6f, 0.15f); } catch { } }
            }).Info("Buzz + warning when a tagger is within 6 m");
            Visuals.HudLine(c, "Tag Counter", () => { int all = Game.Others.Count() + 1, tagged = Game.Others.Count(Game.IsTagged) + (Game.MeTagged ? 1 : 0); return "Tagged " + tagged + "/" + all; }, "How many players are tagged");
        }
    }
}
