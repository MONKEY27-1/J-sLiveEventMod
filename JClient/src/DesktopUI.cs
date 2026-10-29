using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Desktop menu in the style of modern Minecraft clients: sidebar of categories, header with search, module cards with toggle switches.
    public static class DesktopUI
    {
        const float W = 960,
            H = 640,
            Pad = 14,
            Side = 230;
        static Rect win = new Rect(-1, -1, W, H);
        static Vector2 scroll,
            sideScroll;
        static string lastView;
        static bool grid;
        static Mod expanded;
        static readonly Dictionary<Mod, float> knob = new Dictionary<Mod, float>();

        static Font sans,
            serif;
        static GUIStyle sWin,
            sPanel,
            sCard,
            sCardHover,
            sPill,
            sPillOn,
            sSearch,
            sToggleOff,
            sToggleOn,
            sKnob;
        static GUIStyle tName,
            tDesc,
            tSmall,
            tSection,
            tCat,
            tCatSel,
            tCount,
            tTitle,
            tTitleAccent,
            tLogo,
            tLogoAccent,
            tHint,
            tPillText,
            tPillTextOn,
            tChevron;
        static Texture2D white;
        static Color lastAccent;

        // ---- generated textures ----
        static Texture2D Round(Color fill, Color outline, int r)
        {
            int s = r * 2 + 4;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f,
                    py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, s - r),
                    cy = Mathf.Clamp(py, r, s - r);
                float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                var c = d > r - 1.5f ? outline : fill;
                t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
            }
            t.Apply();
            return t;
        }

        static GUIStyle Box(Texture2D t, int r) =>
            new GUIStyle { normal = { background = t }, border = new RectOffset(r + 2, r + 2, r + 2, r + 2) };

        static GUIStyle Text(Font f, int size, Color c, FontStyle st = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft) =>
            new GUIStyle
            {
                font = f,
                fontSize = size,
                fontStyle = st,
                alignment = a,
                normal = { textColor = c },
                clipping = TextClipping.Clip,
            };

        static void Styles()
        {
            var accent = Fx.Accent;
            if (sWin != null && lastAccent == accent)
                return;
            lastAccent = accent;
            if (sans == null)
            {
                sans = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Segoe UI Symbol", "Arial" }, 14);
                serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Cambria", "Times New Roman" }, 22);
                white = Texture2D.whiteTexture;
            }
            Color bg = new Color(0.03f, 0.035f, 0.065f, 0.97f),
                panel = new Color(0.045f, 0.05f, 0.09f, 1f),
                line = new Color(0.13f, 0.14f, 0.22f, 1f);
            Color dim = new Color(0.55f, 0.58f, 0.68f),
                text = new Color(0.92f, 0.94f, 0.98f);
            sWin = Box(Round(bg, line, 18), 18);
            sPanel = Box(Round(panel, line, 14), 14);
            sCard = Box(Round(new Color(0.05f, 0.06f, 0.1f), line, 10), 10);
            sCardHover = Box(Round(new Color(0.07f, 0.08f, 0.13f), new Color(0.2f, 0.22f, 0.32f), 10), 10);
            sPill = Box(Round(new Color(0.06f, 0.07f, 0.12f), line, 11), 11);
            sPillOn = Box(Round(accent * 0.85f, accent, 11), 11);
            sSearch = Box(Round(new Color(0.04f, 0.045f, 0.08f), new Color(0.22f, 0.24f, 0.36f), 10), 10);
            sToggleOff = Box(Round(new Color(0.12f, 0.13f, 0.19f), new Color(0.18f, 0.19f, 0.27f), 11), 11);
            sToggleOn = Box(Round(accent * 0.9f, accent, 11), 11);
            sKnob = Box(Round(Color.white, Color.white, 8), 8);

            tName = Text(sans, 15, text, FontStyle.Bold);
            tDesc = Text(sans, 11, dim);
            tSmall = Text(sans, 12, dim);
            tSection = Text(sans, 10, new Color(0.45f, 0.48f, 0.58f));
            tCat = Text(sans, 14, new Color(0.78f, 0.8f, 0.88f));
            tCatSel = Text(sans, 14, Color.white);
            tCount = Text(sans, 11, new Color(0.45f, 0.48f, 0.58f), FontStyle.Normal, TextAnchor.MiddleRight);
            tTitle = Text(serif, 28, Color.white, FontStyle.Bold);
            tTitleAccent = Text(serif, 28, accent, FontStyle.Italic);
            tLogo = Text(serif, 20, Color.white, FontStyle.Bold);
            tLogoAccent = Text(serif, 20, accent, FontStyle.Italic);
            tHint = Text(sans, 11, new Color(0.42f, 0.45f, 0.55f), FontStyle.Normal, TextAnchor.MiddleCenter);
            tPillText = Text(sans, 12, new Color(0.85f, 0.88f, 0.95f), FontStyle.Normal, TextAnchor.MiddleCenter);
            tPillTextOn = Text(sans, 12, new Color(0.02f, 0.04f, 0.07f), FontStyle.Bold, TextAnchor.MiddleCenter);
            tChevron = Text(sans, 18, dim, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        internal static readonly string[] General = { "Favorites", "Enabled", "Settings" };
        internal static IEnumerable<string> ModuleCats => Mods.Categories.Where(c => !General.Contains(c));

        internal static List<Mod> View()
        {
            var s = Board.Search;
            if (s.Length > 0)
                return Mods.All.Where(m => m.Cat != "Keyboard" && m.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (Board.Cat == "Favorites")
                return Mods.All.Where(m => m.Fav).ToList();
            if (Board.Cat == "Enabled")
                return Mods.All.Where(m => m.Toggle && m.On && m.Cat != "Settings").ToList();
            return Mods.All.Where(m => m.Cat == Board.Cat).ToList();
        }

        internal static int CountOf(string cat) =>
            cat == "Favorites" ? Mods.All.Count(m => m.Fav)
            : cat == "Enabled" ? Mods.All.Count(m => m.Toggle && m.On && m.Cat != "Settings")
            : Mods.All.Count(m => m.Cat == cat);

        static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        static bool Clicked(Rect r, int button = 0) => GUI.Button(r, GUIContent.none, GUIStyle.none) && Event.current.button == button;

        static bool Pill(Rect r, string label, bool on = false)
        {
            GUI.Box(r, GUIContent.none, on ? sPillOn : sPill);
            GUI.Label(r, label, on ? tPillTextOn : tPillText);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static void Draw()
        {
            Styles();
            if (Board.Cat == null)
                Board.Cat = "Movement";
            // Shrink on small screens so the whole window always fits
            float scale = Mathf.Min(1f, (Screen.height - 20) / H, (Screen.width - 20) / W);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (win.x < 0)
                win.position = new Vector2((Screen.width / scale - W) / 2f, (Screen.height / scale - H) / 2f);
            win = GUI.Window(0x4A44, win, Window, GUIContent.none, sWin);
            GUI.matrix = old;
        }

        static void Window(int id)
        {
            var accent = Fx.Accent;
            // ---------- sidebar ----------
            var side = new Rect(Pad, Pad, Side, H - Pad * 2);
            GUI.Box(side, GUIContent.none, sPanel);
            GUI.Label(new Rect(side.x + 18, side.y + 16, 30, 28), "J", tLogo);
            GUI.Label(new Rect(side.x + 38, side.y + 16, 160, 28), "Client", tLogoAccent);

            // Category list scrolls (mouse wheel) so nothing clips at the bottom
            var list = new Rect(side.x + 8, side.y + 56, side.width - 16, side.height - 64);
            float listH = 20 + ModuleCats.Count() * 30 + 30 + General.Length * 30;
            sideScroll = GUI.BeginScrollView(
                list,
                sideScroll,
                new Rect(0, 0, list.width, listH),
                false,
                false,
                GUIStyle.none,
                GUIStyle.none
            );
            float y = 0;
            GUI.Label(new Rect(10, y, 160, 16), "MODULES", tSection);
            y += 20;
            foreach (var cat in ModuleCats)
                y = CatRow(list.width, y, cat);
            y += 10;
            GUI.Label(new Rect(10, y, 160, 16), "GENERAL", tSection);
            y += 20;
            foreach (var cat in General)
                y = CatRow(list.width, y, cat);
            GUI.EndScrollView();

            // ---------- header ----------
            float mx = side.xMax + 18,
                mw = W - mx - Pad;
            var mods = View();
            bool searching = Board.Search.Length > 0;
            string title =
                searching ? "Search"
                : Board.IsKeyboard ? "Keyboard"
                : Board.Cat;
            var tsize = tTitleAccent.CalcSize(new GUIContent(title));
            GUI.Label(new Rect(mx, Pad + 6, tsize.x + 4, 38), title, tTitleAccent);
            GUI.Label(new Rect(mx + tsize.x + 10, Pad + 6, 220, 38), "Modules", tTitle);
            GUI.Label(
                new Rect(mx, Pad + 44, 400, 18),
                mods.Count + " modules  ·  " + mods.Count(m => m.Toggle && m.On) + " enabled",
                tSmall
            );

            var search = new Rect(mx + mw - 310, Pad + 12, 310, 36);
            GUI.Box(search, GUIContent.none, sSearch);
            GUI.Label(
                new Rect(search.x + 14, search.y, 280, search.height),
                searching ? Board.Search + "_" : "Search modules  (just type)",
                searching ? tCatSel : tSmall
            );

            float ry = Pad + 74;
            if (Pill(new Rect(mx, ry, 110, 26), Game.InRoom ? "Leave Room" : "Not in a room"))
            {
                RoomMods.Leave();
                Game.Feedback(false);
            }
            if (Pill(new Rect(mx + 118, ry, 90, 26), "Close (Q)"))
                Board.DesktopOpen = false;
            if (Pill(new Rect(mx + mw - 104, ry, 50, 26), "List", !grid))
                grid = false;
            if (Pill(new Rect(mx + mw - 50, ry, 50, 26), "Grid", grid))
                grid = true;

            // ---------- cards ----------
            var area = new Rect(mx, ry + 38, mw, H - (ry + 38) - Pad - 22);
            if (Board.IsKeyboard)
                KeyboardCard(area);
            else
                Cards(area, mods);

            GUI.Label(
                new Rect(mx, H - Pad - 20, mw, 18),
                Board.Binding != null
                    ? "Press a key for " + Board.Binding.Name + "  (Esc clears)"
                    : "Type to search  ·  Esc clears  ·  › opens options, keybind and favorite  ·  * = private/modded rooms only",
                tHint
            );
            GUI.DragWindow(new Rect(0, 0, W, Pad + 70));
        }

        static float CatRow(float w, float y, string cat)
        {
            var r = new Rect(0, y, w, 28);
            bool sel = Board.Search.Length == 0 && Board.Cat == cat;
            if (sel || Hover(r))
                GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, sel ? 0.06f : 0.03f), 0, 6);
            if (sel)
                GUI.DrawTexture(new Rect(r.x, r.y + 7, 2, 14), white, ScaleMode.StretchToFill, true, 0, Fx.Accent, 0, 1);
            GUI.Label(new Rect(r.x + 12, r.y, 150, r.height), cat, sel ? tCatSel : tCat);
            GUI.Label(new Rect(r.xMax - 46, r.y, 36, r.height), CountOf(cat).ToString(), tCount);
            if (Clicked(r))
            {
                Board.OpenCategory(cat);
                expanded = null;
                Game.Feedback(false);
            }
            return y + 30;
        }

        static void Cards(Rect area, List<Mod> mods)
        {
            string view = Board.Search + "|" + Board.Cat + "|" + grid;
            if (view != lastView)
            {
                scroll = Vector2.zero;
                lastView = view;
            }
            int cols = grid ? 2 : 1;
            float gap = 10,
                cw = (area.width - 6 - gap * (cols - 1)) / cols;
            var heights = mods.Select(m => (grid ? 92f : 72f) + (m == expanded ? 46f : 0f)).ToList();
            float total = 0;
            for (int i = 0; i < mods.Count; i += cols)
                total += Enumerable.Range(i, Math.Min(cols, mods.Count - i)).Max(j => heights[j]) + gap;
            scroll = GUI.BeginScrollView(
                area,
                scroll,
                new Rect(0, 0, area.width - 6, Mathf.Max(total, area.height)),
                false,
                false,
                GUIStyle.none,
                GUIStyle.none
            );
            float y = 0;
            for (int i = 0; i < mods.Count; i += cols)
            {
                float rowH = 0;
                for (int j = i; j < Math.Min(i + cols, mods.Count); j++)
                {
                    var r = new Rect((j - i) * (cw + gap), y, cw, heights[j]);
                    if (r.yMax >= scroll.y && r.y <= scroll.y + area.height)
                        Card(r, mods[j]);
                    rowH = Mathf.Max(rowH, heights[j]);
                }
                y += rowH + gap;
            }
            if (mods.Count == 0)
                GUI.Label(
                    new Rect(0, 20, area.width, 30),
                    Board.Cat == "Favorites" ? "No favorites yet - open a card with › and press the star" : "Nothing here",
                    tHint
                );
            GUI.EndScrollView();
        }

        static void Card(Rect r, Mod m)
        {
            bool hover = Hover(r),
                on = m.Toggle && m.On;
            GUI.Box(r, GUIContent.none, hover ? sCardHover : sCard);
            if (on)
                GUI.DrawTexture(
                    new Rect(r.x + 3, r.y + 12, 3, (grid ? 92f : 72f) - 24),
                    white,
                    ScaleMode.StretchToFill,
                    true,
                    0,
                    Fx.Accent,
                    0,
                    1.5f
                );

            string name = (m.Status != null ? m.Status() : m.Name) + (m.Others ? "  *" : "") + (m.Fav ? "  ★" : "");
            float textW = r.width - 170;
            GUI.Label(new Rect(r.x + 20, r.y + 12, textW, 24), name, tName);
            var desc = new Rect(r.x + 20, r.y + 36, textW, grid ? 44 : 22);
            tDesc.wordWrap = grid;
            GUI.Label(desc, string.IsNullOrEmpty(m.Desc) ? m.Cat : m.Desc, tDesc);

            // Right side: chevron + control
            float cy = r.y + (grid ? 46f : 36f) - 12f;
            var chev = new Rect(r.xMax - 156, cy, 24, 24);
            GUI.Label(chev, m == expanded ? "⌄" : "›", tChevron);
            if (Clicked(chev))
                expanded = m == expanded ? null : m;

            var ctrl = new Rect(r.xMax - 64, cy + 1, 46, 22);
            if (m.Toggle)
            {
                float k = knob.TryGetValue(m, out var v) ? v : (m.On ? 1 : 0);
                k = Mathf.MoveTowards(k, m.On ? 1 : 0, Time.unscaledDeltaTime * 8f);
                knob[m] = k;
                GUI.Box(ctrl, GUIContent.none, k > 0.5f ? sToggleOn : sToggleOff);
                GUI.Box(new Rect(ctrl.x + 3 + k * (ctrl.width - 22), ctrl.y + 3, 16, 16), GUIContent.none, sKnob);
            }
            else if (m.Press != null)
            {
                GUI.Box(ctrl, GUIContent.none, sPill);
                GUI.Label(ctrl, "Run", tPillText);
            }
            else if (m.Options != null)
            {
                ctrl = new Rect(r.xMax - 128, cy, 112, 24);
                GUI.Box(ctrl, GUIContent.none, sPill);
                GUI.Label(ctrl, m.Option, tPillText);
            }

            // Clicking the card (outside the chevron) uses the module; Ctrl+right-click binds a key; Shift+click favorites (Board.Click)
            var body = new Rect(r.x, r.y, r.width - 160, grid ? 92f : 72f);
            var ctrlHit = new Rect(r.xMax - 130, r.y, 120, grid ? 92f : 72f);
            foreach (var hit in new[] { body, ctrlHit })
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.button == 1)
                    {
                        if (Event.current.control)
                            StartBind(m);
                    }
                    else if (!m.Toggle && m.Press == null && m.Options != null)
                    {
                        m.Cycle();
                        Game.Feedback(false);
                    }
                    else
                    {
                        Board.Click(new Board.Entry { M = m });
                        Game.Feedback(false);
                    }
                }

            if (m != expanded)
                return;
            float ey = r.y + (grid ? 92f : 72f) - 2,
                ex = r.x + 20;
            if (m.Options != null)
            {
                if (Pill(new Rect(ex, ey, 28, 28), "‹"))
                    m.Cycle(-1);
                GUI.Label(new Rect(ex + 32, ey, 150, 28), m.OptLabel + ": " + m.Option, tPillText);
                if (Pill(new Rect(ex + 186, ey, 28, 28), "›"))
                    m.Cycle(1);
                ex += 228;
            }
            if (
                Pill(
                    new Rect(ex, ey, 120, 28),
                    Board.Binding == m ? "Press a key..." : "Key: " + (m.Bind == Key.None ? "none" : m.Bind.ToString()),
                    Board.Binding == m
                )
            )
                StartBind(m);
            ex += 128;
            if (Pill(new Rect(ex, ey, 100, 28), m.Fav ? "★ Favorite" : "☆ Favorite", m.Fav))
            {
                m.Fav = !m.Fav;
                if (m.cFav != null)
                    m.cFav.Value = m.Fav;
            }
        }

        static void StartBind(Mod m)
        {
            Board.Binding = m;
            Hud.Toast("Press a key for " + m.Name + " (Esc clears)");
        }

        static void KeyboardCard(Rect area)
        {
            var r = new Rect(area.x, area.y, area.width, 150);
            GUI.Box(r, GUIContent.none, sCard);
            GUI.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, 20), "Type with your keyboard", tSmall);
            GUI.Label(new Rect(r.x + 24, r.y + 42, r.width - 48, 40), Keys.Buffer + "_", tTitle);
            if (Pill(new Rect(r.x + 24, r.y + 100, 100, 30), "Done (Enter)", true))
                Keys.Done();
            if (Pill(new Rect(r.x + 132, r.y + 100, 110, 30), "Cancel (Esc)"))
                Board.Home();
            if (Pill(new Rect(r.x + 250, r.y + 100, 90, 30), "Clear"))
                Keys.Buffer = "";
        }
    }
}
