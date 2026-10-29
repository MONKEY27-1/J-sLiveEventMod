using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JClient
{
    // Local world visuals. Nothing here is networked: other players never see it.
    public static class WorldFx
    {
        static readonly float[] spectrum = new float[256],
            samples = new float[256];
        internal static float Level
        {
            get
            {
                AudioListener.GetOutputData(samples, 0);
                float s = 0;
                foreach (var v in samples)
                    s += v * v;
                return Mathf.Sqrt(s / samples.Length);
            }
        }

        static Color fogColour,
            ambient;
        static bool fog;
        static float fogDensity;
        static FogMode fogMode;
        static AmbientMode ambientMode;

        static void SaveLighting()
        {
            fog = RenderSettings.fog;
            fogColour = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            fogMode = RenderSettings.fogMode;
            ambient = RenderSettings.ambientLight;
            ambientMode = RenderSettings.ambientMode;
        }

        static void RestoreLighting()
        {
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColour;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogMode = fogMode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = ambientMode;
        }

        static void Fog(Color c, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = c;
            RenderSettings.fogDensity = density;
        }

        static Mod Spawner(string name, Func<List<GameObject>> make, Action<List<GameObject>> tick, string info)
        {
            var objs = new List<GameObject>();
            return Mods.T(
                    "World FX",
                    name,
                    () =>
                    {
                        if (objs.Count > 0 && objs[0] != null)
                            tick(objs);
                    },
                    () => objs.AddRange(make()),
                    () => Fx.Kill(objs)
                )
                .Info(info);
        }

        static Vector3 Me => Game.P.transform.position;

        static void Wire(ScriptableRenderContext ctx, Camera cam) => GL.wireframe = true;

        static void Unwire(ScriptableRenderContext ctx, Camera cam) => GL.wireframe = false;

        public static void Register()
        {
            const string c = "World FX";
            Mod time = null,
                weather = null;
            var dn = (Func<BetterDayNightManager>)(() => BetterDayNightManager.instance);

            int applied = -1; // forced updates rebuild lightmaps, so only re-apply when the slot changes
            time = Mods.T(
                    c,
                    "Time Of Day",
                    () =>
                    {
                        if (dn() != null && applied != time.Opt)
                        {
                            dn().SetTimeOfDay(time.Opt, true);
                            applied = time.Opt;
                        }
                    },
                    null,
                    () =>
                    {
                        applied = -1;
                        if (dn() != null)
                            dn().ClearTimeOfDay(true);
                    }
                )
                .Opts("Time Slot", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10")
                .Def(3)
                .Live(() => "Time Of Day" + (dn() != null ? " (" + dn().GetTimeOfDayString() + ")" : ""))
                .Info("Locks the sky to a time slot (local only)");
            weather = Mods.T(
                    c,
                    "Weather",
                    () =>
                    {
                        if (dn() != null)
                            dn()
                                .SetFixedWeather(
                                    weather.Opt == 0 ? BetterDayNightManager.WeatherType.None : BetterDayNightManager.WeatherType.Raining
                                );
                    },
                    null,
                    () =>
                    {
                        if (dn() != null)
                            dn().ClearFixedWeather(true);
                    }
                )
                .Opts("Weather", "Clear", "Rain")
                .Info("Forces clear skies or rain (local only)");

            Mods.T(
                    c,
                    "Neon Wireframe",
                    null,
                    () =>
                    {
                        RenderPipelineManager.beginCameraRendering += Wire;
                        RenderPipelineManager.endCameraRendering += Unwire;
                    },
                    () =>
                    {
                        RenderPipelineManager.beginCameraRendering -= Wire;
                        RenderPipelineManager.endCameraRendering -= Unwire;
                        GL.wireframe = false;
                    }
                )
                .Info("Renders the world as wireframe (may not show on every GPU)");
            Mods.T(c, "Cyan Atmosphere", () => Fog(Fx.Accent * 0.6f, 0.02f), SaveLighting, RestoreLighting)
                .Info("Cyan fog and tint over everything");
            Mods.T(c, "Rainbow Fog", () => Fog(Fx.Rainbow(0.05f) * 0.7f, 0.025f), SaveLighting, RestoreLighting)
                .Info("Slowly shifting rainbow fog");
            Mods.T(
                    c,
                    "Bass Pulse",
                    () =>
                    {
                        RenderSettings.ambientMode = AmbientMode.Flat;
                        RenderSettings.ambientLight = Color.Lerp(ambient, Fx.Accent, Mathf.Clamp01(Level * 6f));
                    },
                    SaveLighting,
                    RestoreLighting
                )
                .Info("World lighting pulses to whatever is playing (great with the Astronomical Event)");

            AudioLowPassFilter muffle = null;
            ParticleSystem bubbles = null;
            Mods.T(
                    c,
                    "Underwater World",
                    () =>
                    {
                        Fog(new Color(0.05f, 0.25f, 0.4f), 0.06f);
                        if (bubbles != null)
                            bubbles.transform.position = Me - Vector3.up;
                    },
                    () =>
                    {
                        SaveLighting();
                        var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
                        if (listener != null)
                        {
                            muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
                            muffle.cutoffFrequency = 700f;
                        }
                        bubbles = Fx.Particles(new Color(0.7f, 0.9f, 1f, 0.6f), 30, 0.04f, 0.4f, 5f, 4f);
                        var m = bubbles.main;
                        m.gravityModifier = -0.05f;
                    },
                    () =>
                    {
                        RestoreLighting();
                        if (muffle != null)
                            UnityEngine.Object.Destroy(muffle);
                        muffle = null;
                        Fx.Kill(bubbles);
                        bubbles = null;
                    }
                )
                .Info("Deep blue fog, rising bubbles and muffled audio (local only)");

            Spawner(
                "Music Visualizer",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 32; i++)
                        l.Add(Fx.Prim(PrimitiveType.Cube, new Vector3(0.12f, 0.1f, 0.12f), Fx.Accent));
                    return l;
                },
                l =>
                {
                    AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
                    for (int i = 0; i < l.Count; i++)
                    {
                        float a = i / (float)l.Count * Mathf.PI * 2f,
                            h = Mathf.Clamp(spectrum[i * 2 + 1] * 60f, 0.05f, 4f);
                        var t = l[i].transform;
                        t.localScale = new Vector3(0.12f, h, 0.12f) * Game.Scale;
                        t.position =
                            Me + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.5f * Game.Scale + Vector3.up * (h / 2f - 0.8f) * Game.Scale;
                        l[i].GetComponent<Renderer>().material.color = Color.Lerp(Fx.Accent, new Color(0.2f, 0.3f, 1f), h / 4f);
                    }
                },
                "A ring of bars around you bouncing to the audio"
            );
            Spawner(
                "Starfield",
                () => new List<GameObject> { Fx.Particles(Color.white, 60, 0.15f, 0f, 20f, 60f, 1500).gameObject },
                l => l[0].transform.position = Me + Vector3.up * 40f,
                "Thousands of stars in the sky"
            );
            Spawner(
                "Snowfall",
                () => new List<GameObject> { Fx.Particles(Color.white, 120, 0.05f, 0.5f, 8f, 12f).gameObject },
                l => l[0].transform.position = Me + Vector3.up * 8f,
                "Snow falling around you"
            );
            Spawner(
                "Fireflies",
                () => new List<GameObject> { Fx.Particles(new Color(0.9f, 1f, 0.4f), 15, 0.04f, 0.2f, 6f, 8f).gameObject },
                l => l[0].transform.position = Me,
                "Glowing fireflies drifting around you"
            );
            Spawner(
                "Disco Lights",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 4; i++)
                    {
                        var g = new GameObject("JClientDisco");
                        Fx.Spawned.Add(g);
                        var li = g.AddComponent<Light>();
                        li.type = LightType.Point;
                        li.range = 12f;
                        li.intensity = 4f;
                        l.Add(g);
                    }
                    return l;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        float a = Time.time * 1.5f + i * Mathf.PI / 2f;
                        l[i].transform.position = Me + new Vector3(Mathf.Cos(a) * 4f, 3f, Mathf.Sin(a) * 4f);
                        l[i].GetComponent<Light>().color = Fx.Rainbow(0.4f, i * 0.25f);
                    }
                },
                "Coloured party lights spinning around you"
            );
            float nextFlash = 0f;
            Mods.T(
                    c,
                    "Lightning Storm",
                    () =>
                    {
                        RenderSettings.ambientMode = AmbientMode.Flat;
                        if (Time.time > nextFlash)
                            nextFlash = Time.time + UnityEngine.Random.Range(3f, 9f);
                        float since = nextFlash - Time.time;
                        bool flash = since < 0.25f && since > 0f || since < 0.6f && since > 0.45f;
                        RenderSettings.ambientLight = flash ? Color.white * 2f : ambient * 0.4f;
                    },
                    SaveLighting,
                    RestoreLighting
                )
                .Info("Dark sky with random lightning flashes");
            Spawner(
                "Neon Grid Floor",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 22; i++)
                    {
                        var ln = Fx.Line(Fx.Accent, 0.02f, false);
                        l.Add(ln.gameObject);
                    }
                    return l;
                },
                l =>
                {
                    var o = new Vector3(Mathf.Round(Me.x), Me.y - 1.2f * Game.Scale, Mathf.Round(Me.z));
                    for (int i = 0; i < 11; i++)
                    {
                        float k = (i - 5) * 2f;
                        var a = l[i].GetComponent<LineRenderer>();
                        a.SetPosition(0, o + new Vector3(k, 0, -10));
                        a.SetPosition(1, o + new Vector3(k, 0, 10));
                        var b = l[i + 11].GetComponent<LineRenderer>();
                        b.SetPosition(0, o + new Vector3(-10, 0, k));
                        b.SetPosition(1, o + new Vector3(10, 0, k));
                    }
                },
                "A glowing Tron grid under your feet"
            );
            Spawner(
                "Giant Moon",
                () => new List<GameObject> { Fx.Prim(PrimitiveType.Sphere, Vector3.one * 60f, new Color(0.8f, 0.95f, 1f)) },
                l => l[0].transform.position = Me + new Vector3(150f, 120f, 150f),
                "A huge glowing moon in the sky"
            );
            Spawner(
                "Shooting Stars",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 3; i++)
                    {
                        var g = new GameObject("JClientStar");
                        Fx.Spawned.Add(g);
                        var t = g.AddComponent<TrailRenderer>();
                        t.time = 0.8f;
                        t.startWidth = 0.6f;
                        t.endWidth = 0f;
                        t.material = Fx.Mat(Color.white, false, true);
                        l.Add(g);
                    }
                    return l;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        float cycle = 4f + i * 1.7f,
                            t = Mathf.Repeat(Time.time + i * 2.3f, cycle);
                        var start = Me + new Vector3(-80f + i * 40f, 70f, 60f - i * 30f);
                        if (t < 0.05f)
                            l[i].GetComponent<TrailRenderer>().Clear();
                        l[i].transform.position = start + new Vector3(1f, -0.4f, -0.6f) * t * 60f;
                    }
                },
                "Shooting stars streak across the sky"
            );
            Spawner(
                "Aurora Ribbons",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 3; i++)
                    {
                        var ln = Fx.Line(Fx.Accent, 4f, false);
                        ln.positionCount = 40;
                        l.Add(ln.gameObject);
                    }
                    return l;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        var ln = l[i].GetComponent<LineRenderer>();
                        for (int p = 0; p < 40; p++)
                        {
                            float x = (p - 20) * 6f;
                            ln.SetPosition(
                                p,
                                Me
                                    + new Vector3(
                                        x,
                                        45f + i * 6f + Mathf.Sin(Time.time * 0.6f + p * 0.3f + i) * 4f,
                                        80f + Mathf.Cos(Time.time * 0.4f + p * 0.2f) * 10f
                                    )
                            );
                        }
                        ln.material.color = Color.Lerp(Fx.Accent, new Color(0.5f, 0.2f, 1f), i / 2f) * new Color(1, 1, 1, 0.45f);
                    }
                },
                "Waving aurora curtains in the sky"
            );
            Spawner(
                "Floating Lanterns",
                () =>
                {
                    var l = new List<GameObject>();
                    for (int i = 0; i < 20; i++)
                        l.Add(Fx.Prim(PrimitiveType.Cube, Vector3.one * 0.25f, new Color(1f, 0.6f, 0.2f)));
                    return l;
                },
                l =>
                {
                    for (int i = 0; i < l.Count; i++)
                    {
                        float t = Mathf.Repeat(Time.time * 0.4f + i * 0.37f, 12f);
                        float a = i * 2.4f;
                        l[i].transform.position = Me + new Vector3(Mathf.Cos(a) * (6f + i % 5), t * 2f - 2f, Mathf.Sin(a) * (6f + i % 5));
                        l[i].transform.rotation = Quaternion.Euler(0, Time.time * 20f + i * 30f, 0);
                    }
                },
                "Glowing lanterns slowly rising around you"
            );
        }
    }
}
