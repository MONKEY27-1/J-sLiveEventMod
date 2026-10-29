using UnityEngine;

namespace AstronomicalEvent
{
    // Self-contained IMGUI menu. IMGUI renders to the desktop window only (not inside the headset) and needs the mouse.
    internal sealed class AstronomicalUI
    {
        readonly AstronomicalEventManager m;
        bool open;
        bool savedCursorVisible;
        CursorLockMode savedLock;
        GUIStyle title,
            label,
            button,
            box;
        Texture2D bg,
            accent;
        Vector2 scroll;

        public AstronomicalUI(AstronomicalEventManager mgr)
        {
            m = mgr;
        }

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        public bool IsOpen
        {
            get { return open; }
        }

        public void Open()
        {
            if (open)
                return;
            open = true;
            savedCursorVisible = Cursor.visible;
            savedLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (!open)
                return;
            open = false;
            Cursor.visible = savedCursorVisible;
            Cursor.lockState = savedLock; // event keeps running
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        void Styles()
        {
            if (title != null)
                return;
            bg = Tex(new Color(0.04f, 0.03f, 0.1f, 0.93f));
            accent = Tex(new Color(1f, 0.55f, 0.15f));
            title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.7f, 0.35f) },
            };
            label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.85f, 0.85f, 1f) },
                wordWrap = true,
            };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            box = new GUIStyle(GUI.skin.box) { normal = { background = bg } };
        }

        float Slider(string name, float v, float lo, float hi)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, label, GUILayout.Width(130));
            v = GUILayout.HorizontalSlider(v, lo, hi);
            GUILayout.Label(v.ToString("0.00"), label, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return v;
        }

        public void Draw()
        {
            if (!open)
                return;
            Styles();
            var r = new Rect(20, 20, 440, Mathf.Min(Screen.height - 40, 760));
            GUI.Box(r, GUIContent.none, box);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("ASTRONOMICAL", title);
            float glow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f);
            GUI.color = new Color(1, 1, 1, 0.4f + 0.6f * glow);
            GUILayout.Box(GUIContent.none, new GUIStyle { normal = { background = accent } }, GUILayout.Height(2));
            GUI.color = Color.white;
            GUILayout.Label(
                "LOCAL EVENT - runs only on this client, nothing is sent to other players. Close with P (event keeps running).",
                label
            );

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start", button, GUILayout.Height(34)))
                m.Start(AstronomicalSettings.StartSequence.Value);
            if (GUILayout.Button("Pause", button, GUILayout.Height(34)))
                m.Pause();
            if (GUILayout.Button("Resume", button, GUILayout.Height(34)))
                m.Resume();
            if (GUILayout.Button("Restart", button, GUILayout.Height(34)))
                m.Restart();
            if (GUILayout.Button("Stop", button, GUILayout.Height(34)))
                m.Stop();
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SEQUENCES", label);
            for (int i = 0; i < AstronomicalSequence.Names.Length; i++)
            {
                bool cur = m.State != EventState.Idle && m.CurrentSequence == i;
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    (cur ? "> " : "  ") + AstronomicalSequence.Names[i] + "  (" + AstronomicalSequence.Duration[i] + "s)",
                    label
                );
                if (GUILayout.Button("Play from here", button, GUILayout.Width(120)))
                {
                    AstronomicalSettings.StartSequence.Value = i;
                    if (m.State == EventState.Idle)
                        m.Start(i);
                    else
                        m.T = AstronomicalSequence.Start[i];
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("EVENT SPAWN LOCATION (IN-GAME SET)", label);
            AstronomicalSettings.UseFixedSpawn.Value = GUILayout.Toggle(
                AstronomicalSettings.UseFixedSpawn.Value,
                " Always start event at saved location"
            );

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Stage Here", button, GUILayout.Height(30)))
                m.SaveStageHere();
            if (GUILayout.Button("Teleport to Stage", button, GUILayout.Height(30)))
                m.TeleportToStage();
            GUILayout.EndHorizontal();

            if (AstronomicalSettings.FixedSpawnX.Value != 0 || AstronomicalSettings.FixedSpawnZ.Value != 0)
                GUILayout.Label(
                    "Saved Stage: ("
                        + AstronomicalSettings.FixedSpawnX.Value.ToString("0.0")
                        + ", "
                        + AstronomicalSettings.FixedSpawnY.Value.ToString("0.0")
                        + ", "
                        + AstronomicalSettings.FixedSpawnZ.Value.ToString("0.0")
                        + ")  Facing: "
                        + AstronomicalSettings.FixedSpawnYaw.Value.ToString("0")
                        + "°",
                    label
                );
            else
                GUILayout.Label("Saved Stage: [Not set yet - stand where you want it and click 'Set Stage Here']", label);

            GUILayout.Space(8);
            GUILayout.Label("SETTINGS", label);
            AstronomicalSettings.Volume.Value = Slider("Event volume (boost)", AstronomicalSettings.Volume.Value, 0, 3);
            AstronomicalSettings.StageScale.Value = Slider("Stage model scale", AstronomicalSettings.StageScale.Value, 0.2f, 3f);
            AstronomicalSettings.Effects.Value = Slider("Effects intensity", AstronomicalSettings.Effects.Value, 0, 1);
            AstronomicalSettings.Lighting.Value = Slider("Lighting intensity", AstronomicalSettings.Lighting.Value, 0, 1);
            AstronomicalSettings.Particles.Value = Slider("Particle density", AstronomicalSettings.Particles.Value, 0.1f, 1);
            AstronomicalSettings.Speed.Value = Slider("Playback speed", AstronomicalSettings.Speed.Value, 0.5f, 2);
            AstronomicalSettings.TreeDestruction.Value = GUILayout.Toggle(
                AstronomicalSettings.TreeDestruction.Value,
                " Tree destruction (visual only, applies on next Start)"
            );
            AstronomicalSettings.Countdown.Value = Slider("Countdown (s)", AstronomicalSettings.Countdown.Value, 0, 120);
            AstronomicalSettings.ShowStage.Value = GUILayout.Toggle(AstronomicalSettings.ShowStage.Value, " Show stage model (next Start)");
            AstronomicalSettings.SyncBroadcast.Value = GUILayout.Toggle(
                AstronomicalSettings.SyncBroadcast.Value,
                " Sync: tell other mod users when I start/stop"
            );
            AstronomicalSettings.SyncReceive.Value = GUILayout.Toggle(
                AstronomicalSettings.SyncReceive.Value,
                " Sync: join events other mod users start"
            );
            AstronomicalSettings.Knockback.Value = GUILayout.Toggle(
                AstronomicalSettings.Knockback.Value,
                " Knockback (explosions, meteors, stomps, rings)"
            );
            AstronomicalSettings.KnockbackStrength.Value = Slider("Knockback strength", AstronomicalSettings.KnockbackStrength.Value, 0, 2);
            AstronomicalSettings.SwimInFlood.Value = GUILayout.Toggle(
                AstronomicalSettings.SwimInFlood.Value,
                " Swim in the flood (game's own swimming)"
            );
            AstronomicalSettings.CameraFx.Value = GUILayout.Toggle(AstronomicalSettings.CameraFx.Value, " Camera effects");
            AstronomicalSettings.AutoCleanup.Value = GUILayout.Toggle(
                AstronomicalSettings.AutoCleanup.Value,
                " Clean up & restore when finished"
            );
            AstronomicalSettings.Debug.Value = GUILayout.Toggle(AstronomicalSettings.Debug.Value, " Debug logging");
            GUILayout.Label("Particle density & tree destruction apply on next Start.", label);

            GUILayout.Space(8);
            GUILayout.Label("STATUS", label);
            GUILayout.Label(
                "State: "
                    + m.State
                    + (
                        m.State != EventState.Idle
                            ? "   t=" + m.T.ToString("0.0") + " / " + AstronomicalSequence.Total.ToString("0") + "s"
                            : ""
                    ),
                label
            );
            GUILayout.Label("Sequence: " + (m.State == EventState.Idle ? "-" : AstronomicalSequence.Names[m.CurrentSequence]), label);
            GUILayout.Label("Active zone is Forest: " + (m.CheckForest() ? "YES" : "NO") + "   |   Local only: YES", label);
            GUILayout.Label(
                "Audio: " + m.Audio.Status + "   |   Shader: " + m.Effects.ShaderUsed + "   |   Owned objects: " + m.Effects.OwnedCount,
                label
            );
            GUILayout.Label(
                "Assets: "
                    + m.Assets.Status
                    + (m.Assets.World == null ? "  [moon missing: placeholder planets]" : "")
                    + (m.Assets.Music == null ? "  [music missing: synth track]" : ""),
                label
            );
            foreach (var pr in m.Assets.Problems)
                GUILayout.Label("! " + pr, label);
            if (m.Effects.Destruction != null)
                GUILayout.Label(
                    "Destruction: "
                        + m.Effects.Destruction.GroupCount
                        + " tree groups ("
                        + m.Effects.Destruction.HouseCount
                        + " treehouse), fallen "
                        + m.Effects.Destruction.Fallen,
                    label
                );
            if (!string.IsNullOrEmpty(m.Message))
                GUILayout.Label("! " + m.Message, label);
            GUILayout.Space(8);
            GUILayout.Label("INSTALL FROM REPO (" + AstronomicalInstaller.Repo + ")", title);
            if (
                GUILayout.Button(
                    AstronomicalInstaller.Remote.Count == 0 ? "Load event list from GitHub" : "Reload list",
                    button,
                    GUILayout.Height(28)
                ) && !AstronomicalInstaller.Busy
            )
                AstronomicalEventMod.Inst.StartCoroutine(AstronomicalInstaller.Refresh());
            foreach (var kv in AstronomicalInstaller.Remote)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    kv.Key
                        + "  ("
                        + (AstronomicalInstaller.TotalBytes(kv.Key) / 1048576f).ToString("0")
                        + " MB)"
                        + (AstronomicalInstaller.Installed(kv.Key) ? "  [installed]" : ""),
                    label
                );
                if (
                    GUILayout.Button(AstronomicalInstaller.Installed(kv.Key) ? "Update" : "Install", button, GUILayout.Width(90))
                    && !AstronomicalInstaller.Busy
                )
                    AstronomicalEventMod.Inst.StartCoroutine(AstronomicalInstaller.Install(kv.Key, m));
                GUILayout.EndHorizontal();
            }
            if (AstronomicalInstaller.Busy)
                GUILayout.Label("Downloading " + (AstronomicalInstaller.Progress * 100f).ToString("0") + "%", label);
            if (!string.IsNullOrEmpty(AstronomicalInstaller.Status))
                GUILayout.Label(AstronomicalInstaller.Status, label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
