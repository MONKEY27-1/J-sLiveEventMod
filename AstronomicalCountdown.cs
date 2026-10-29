using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // The one countdown: big world-space text floating at a fixed spot (above the Forest gazebo when it can be found, otherwise above the saved stage spot).
    // It is depth-tested like any object (no seeing it through walls) and turns to face the player.
    internal sealed class AstronomicalCountdown
    {
        GameObject go;
        TextMesh main,
            shadow;
        Material mat;
        static Font font; // created once and reused: Hide() destroys the text objects but not the font
        Vector3 spot;
        int shown = -1;
        bool hasSpot;

        public static Vector3 FindSpot(Vector3 fallback)
        {
            try
            {
                Transform best = null;
                float bestY = float.MinValue;
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
                {
                    if (t == null || t.name.IndexOf("gazebo", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (best == null || t.position.y > bestY)
                    {
                        best = t;
                        bestY = t.position.y;
                    }
                }
                if (best != null)
                {
                    float top = best.position.y;
                    foreach (var r in best.GetComponentsInChildren<Renderer>())
                        top = Mathf.Max(top, r.bounds.max.y);
                    return new Vector3(best.position.x, top + 9f, best.position.z);
                }
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Gazebo search failed: " + e.Message);
            }
            return fallback + Vector3.up * 22f;
        }

        // The gazebo's footprint on the floor (so the show can be centred on the pit); falls back to the given point.
        public static Vector3 FindGazeboBase(Vector3 fallback)
        {
            Transform best = null;
            float bd = 120f * 120f;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (t == null || t.name.IndexOf("gazebo", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                float d = (new Vector2(t.position.x - fallback.x, t.position.z - fallback.z)).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = t;
                }
            }
            if (best == null)
                return fallback;
            float low = best.position.y;
            foreach (var r in best.GetComponentsInChildren<Renderer>())
                low = Mathf.Min(low, r.bounds.min.y);
            return new Vector3(best.position.x, Mathf.Min(low, fallback.y + 3f), best.position.z);
        }

        void Build()
        {
            go = new GameObject("AstronomicalCountdown");
            try
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Black", "Impact", "Arial" }, 128);
            }
            catch { }
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mat = new Material(font.material);
            try
            {
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            }
            catch { } // GUI/Text Shader defaults to "always on top"; make it behave like a normal world object
            shadow = Make("shadow", new Color(0f, 0f, 0f, 0.85f), 0.045f);
            main = Make("main", Color.white, 0f);
        }

        TextMesh Make(string n, Color c, float off)
        {
            var g = new GameObject(n);
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = new Vector3(off, -off, off > 0 ? 0.05f : 0f);
            var tm = g.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 128;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = c;
            tm.lineSpacing = 0.9f;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }

        public void Show(Vector3 fallback)
        {
            spot = FindSpot(fallback);
            hasSpot = true;
            if (go == null)
                Build();
            go.SetActive(true);
        }

        public void Tick(float secondsLeft)
        {
            if (go == null || !hasSpot)
                return;
            int s = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
            if (s != shown)
            {
                // rebuild the text meshes only when the number changes, not every frame
                shown = s;
                string t = "EVENT STARTS IN\n" + (s >= 60 ? (s / 60) + ":" + (s % 60).ToString("00") : s.ToString());
                main.text = t;
                shadow.text = t;
            }
            var cam = Camera.main;
            go.transform.position = spot;
            if (cam != null)
            {
                Vector3 d = spot - cam.transform.position;
                float dist = Mathf.Max(5f, d.magnitude);
                d.y = 0;
                if (d.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.LookRotation(d.normalized); // turns on the spot to face you; the position never moves
                go.transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.12f, 2f, 14f);
            }
            float pulse = s <= 10 ? 1f + 0.12f * Mathf.Sin(Time.time * 6.28f) : 1f;
            go.transform.localScale *= pulse;
            main.color = s <= 10 ? Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), 0.6f) : Color.white;
        }

        public void Hide()
        {
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
                go = null;
            }
            if (mat != null)
            {
                UnityEngine.Object.Destroy(mat);
                mat = null;
            }
            hasSpot = false;
            shown = -1;
        }
    }
}
