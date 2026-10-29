using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // The classic board: category list -> pages of buttons -> < Home >. Same model drives the desktop window and the VR board.
    public static class Board
    {
        public struct Entry { public Mod M; public bool Cycle; public string Cat; }

        public static bool DesktopOpen, VROpen;
        public static string Cat; public static int Page;
        public static string Search = "";
        public static Mod Binding;

        public static bool IsKeyboard => Cat == "Keyboard" && Search.Length == 0;
        static int PerPage => IsKeyboard ? 48 : 8;
        public static int Cols => IsKeyboard ? 6 : 1;

        public static List<Entry> All()
        {
            var list = new List<Entry>();
            if (Search.Length > 0) { foreach (var m in Mods.All) if (m.Cat != "Keyboard" && m.Name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0) AddMod(list, m); }
            else if (Cat == null) foreach (var c in Mods.Categories) list.Add(new Entry { Cat = c });
            else if (Cat == "Favorites") { foreach (var m in Mods.All) if (m.Fav) AddMod(list, m); }
            else if (Cat == "Enabled") { foreach (var m in Mods.All) if (m.Toggle && m.On && m.Cat != "Settings") list.Add(new Entry { M = m }); }
            else foreach (var m in Mods.All) if (m.Cat == Cat) AddMod(list, m);
            return list;
        }

        static void AddMod(List<Entry> list, Mod m)
        {
            if (m.Toggle || m.Press != null) list.Add(new Entry { M = m });
            if (m.Options != null) list.Add(new Entry { M = m, Cycle = true });
        }

        public static int Pages => Math.Max(1, (All().Count + PerPage - 1) / PerPage);
        public static List<Entry> Visible() { var a = All(); Page = Mathf.Clamp(Page, 0, Pages - 1); return a.Skip(Page * PerPage).Take(PerPage).ToList(); }

        public static string Label(Entry e)
        {
            if (e.M == null) return e.Cat;
            var m = e.M;
            if (e.Cycle) return (m.Toggle || m.Press != null ? "  > " : "") + m.OptLabel + ": " + m.Option;
            var s = m.Status != null ? m.Status() : m.Name;
            if (m.Others) s += " *";
            if (m.Bind != Key.None) s += " [" + m.Bind + "]";
            return s;
        }

        public static bool Lit(Entry e) => e.M != null && !e.Cycle && e.M.Toggle && e.M.On;

        public static string Title => Search.Length > 0 ? "Search: " + Search : IsKeyboard ? "> " + Keys.Buffer + "_" : (Cat ?? "Home") + "  " + (Page + 1) + "/" + Pages;

        public static void Click(Entry e)
        {
            if (e.M == null) { Cat = e.Cat; Page = 0; return; }
            // Shift+click (desktop) or holding grip on the board hand (VR) stars a mod instead of using it
            bool star = Inp.Key(Key.LeftShift) || Inp.Key(Key.RightShift) || Inp.VR && (Settings.BoardRight.On ? Inp.RGrip.Held : Inp.LGrip.Held);
            if (star && e.M.Cat != "Keyboard")
            {
                e.M.Fav = !e.M.Fav; if (e.M.cFav != null) e.M.cFav.Value = e.M.Fav;
                Hud.Toast(e.M.Name + (e.M.Fav ? " added to Favorites" : " removed from Favorites")); return;
            }
            if (e.Cycle) e.M.Cycle(); else e.M.Activate();
        }

        public static void Prev() { Page = (Page - 1 + Pages) % Pages; }
        public static void Next() { Page = (Page + 1) % Pages; }
        public static void Home() { if (IsKeyboard) Keys.Cancel(); Cat = null; Page = 0; Search = ""; }
        public static void OpenCategory(string c) { Cat = c; Page = 0; Search = ""; }

        public static void Update()
        {
            if (Binding != null && Inp.Kb != null)
            {
                foreach (var k in Inp.Kb.allKeys)
                {
                    if (!k.wasPressedThisFrame) continue;
                    Binding.Bind = k.keyCode == Key.Escape ? Key.None : k.keyCode;
                    if (Binding.cKey != null) Binding.cKey.Value = Binding.Bind.ToString();
                    Hud.Toast(Binding.Name + (Binding.Bind == Key.None ? " unbound" : " bound to " + Binding.Bind));
                    Binding = null; break;
                }
            }
            else if (DesktopOpen) Typing();
            else if (Inp.KeyDown(Key.Q)) DesktopOpen = true;
            else foreach (var m in Mods.All) if (m.Bind != Key.None && Inp.KeyDown(m.Bind)) m.Activate(); // hotkeys only while the menu is closed

            VRBoard.Update();
        }

        // While the desktop menu is open the keyboard types: into the name/code page, otherwise into search.
        static void Typing()
        {
            if (Inp.Kb == null) return;
            if (Inp.KeyDown(Key.Escape)) { if (IsKeyboard) Home(); else if (Search.Length > 0) Search = ""; else DesktopOpen = false; return; }
            if (Inp.KeyDown(Key.Q) && Search.Length == 0 && !IsKeyboard) { DesktopOpen = false; return; }
            if (Inp.KeyDown(Key.Backspace)) { if (IsKeyboard) Keys.Delete(); else if (Search.Length > 0) { Search = Search.Substring(0, Search.Length - 1); Page = 0; } }
            if (IsKeyboard && (Inp.KeyDown(Key.Enter) || Inp.KeyDown(Key.NumpadEnter))) { Keys.Done(); return; }
            foreach (var k in Inp.Kb.allKeys)
            {
                if (!k.wasPressedThisFrame) continue;
                char ch = CharOf(k.keyCode);
                if (ch == '\0') continue;
                if (IsKeyboard) Keys.Type(ch); else if (Search.Length < 24) { Search += ch; Page = 0; }
            }
        }

        internal static char CharOf(Key k)
        {
            if (k >= Key.A && k <= Key.Z) return (char)('A' + (k - Key.A));
            if (k >= Key.Digit1 && k <= Key.Digit9) return (char)('1' + (k - Key.Digit1));
            if (k == Key.Digit0) return '0';
            if (k >= Key.Numpad0 && k <= Key.Numpad9) return (char)('0' + (k - Key.Numpad0));
            return '\0';
        }

        // Desktop window lives in DesktopUI
        public static void OnGUI() { Hud.Draw(); if (DesktopOpen) DesktopUI.Draw(); }
    }

    // World-space board in VR: hold Y (left secondary) to show it; poke with the other hand's finger or point the laser and pull trigger.
    public static class VRBoard
    {
        class B { public Collider Col; public Action Click; public Renderer R; public Color Base; }
        static GameObject root, pointer; static LineRenderer laser;
        static readonly List<B> buttons = new List<B>();
        static string sig = "";
        static float cooldown; static B hover, lastTouched; static bool requireRelease;

        // Card-style board (matches the desktop menu): sidebar of categories, header, 5 module cards per page
        const float W = 0.52f, H = 0.34f, SideW = 0.13f, CardH = 0.036f, CardGap = 0.005f;
        const int PerPage = 5;
        static int page; static string pageKey;
        static readonly Color Bg = new Color(0.03f, 0.035f, 0.065f), PanelC = new Color(0.045f, 0.05f, 0.09f), Line = new Color(0.13f, 0.14f, 0.22f),
            CardC = new Color(0.05f, 0.06f, 0.1f), Dim = new Color(0.55f, 0.58f, 0.68f), Off = new Color(0.12f, 0.13f, 0.19f);

        static bool LeftBoard => !Settings.BoardRight.On;
        static Transform BoardHand => LeftBoard ? Game.LHand : Game.RHand;
        static Transform PokeHand => LeftBoard ? Game.RHand : Game.LHand;

        static List<Mod> PageMods(List<Mod> all) => all.Skip(page * PerPage).Take(PerPage).ToList();
        static int PageCount(List<Mod> all) => Math.Max(1, (all.Count + PerPage - 1) / PerPage);

        public static void Update()
        {
            bool held = Inp.VR && (LeftBoard ? Inp.LY.Held : Inp.RB.Held);
            Board.VROpen = held;
            if (!held) { Hide(); return; }
            if (Board.Cat == null) Board.Cat = "Movement";

            string key = Board.Cat + "|" + Board.Search;
            if (key != pageKey) { pageKey = key; page = 0; }
            string s;
            if (Board.IsKeyboard) s = "kb|" + Keys.Buffer;
            else
            {
                var all = DesktopUI.View(); page = Mathf.Clamp(page, 0, PageCount(all) - 1);
                s = key + "|" + page + "|" + all.Count + "|" + all.Count(m => m.Toggle && m.On) + "|" + Game.InRoom + "|" +
                    string.Join("|", PageMods(all).Select(m => (m.Status != null ? m.Status() : m.Name) + m.On + m.Option + m.Fav));
            }
            s += "|" + Settings.AccentColor;
            if (root == null || s != sig) { sig = s; Build(); }
            root.SetActive(true);

            float sc = Game.Scale;
            var head = Game.Head.position;
            var pos = BoardHand.position + Vector3.up * 0.2f * sc;
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(pos - head, Vector3.up);
            root.transform.localScale = Vector3.one * sc;

            Poke(sc);
        }

        static void Poke(float sc)
        {
            cooldown -= Time.deltaTime;
            var tip = PokeHand.position;
            if (pointer == null) pointer = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.02f, Fx.Accent);
            pointer.SetActive(true); pointer.transform.position = tip; pointer.transform.localScale = Vector3.one * 0.02f * sc;

            B touched = null;
            foreach (var b in buttons) if ((b.Col.ClosestPoint(tip) - tip).sqrMagnitude < 0.0001f * sc * sc) { touched = b; break; }

            // Laser
            B aimed = null;
            if (Settings.Laser.On)
            {
                var ray = new Ray(PokeHand.position, Settings.GunForward.On ? PokeHand.forward : -PokeHand.up);
                float best = 3f * sc;
                foreach (var b in buttons) if (b.Col.Raycast(ray, out var hit, best)) { best = hit.distance; aimed = b; }
                if (laser == null) laser = Fx.Line(Fx.Accent, 0.004f);
                laser.gameObject.SetActive(true);
                laser.SetPosition(0, ray.origin); laser.SetPosition(1, ray.origin + ray.direction * (aimed != null ? best : 0.6f * sc));
                laser.material.color = aimed != null ? Color.white : Fx.Accent * 0.7f;
            }
            else if (laser != null) laser.gameObject.SetActive(false);

            if (hover != null && hover != aimed && hover != touched && hover.R != null) hover.R.material.color = hover.Base;
            hover = touched ?? aimed;
            if (hover != null && hover.R != null) hover.R.material.color = Color.Lerp(hover.Base, Color.white, 0.35f);

            bool trigger = LeftBoard ? Inp.RTrig.Down : Inp.LTrig.Down;
            // A poke must leave the button (or the board after a page change) before it can press again
            if (touched == null) requireRelease = false;
            var press = !requireRelease && touched != lastTouched ? touched : null;
            lastTouched = touched;
            if (press == null && trigger) press = aimed;
            if (press != null && cooldown <= 0f)
            {
                cooldown = 0.3f;
                press.Click();
                Game.Feedback(!LeftBoard);
            }
        }

        static void Hide()
        {
            if (root != null) root.SetActive(false);
            if (pointer != null) pointer.SetActive(false);
            if (laser != null) laser.gameObject.SetActive(false);
        }

        static string Cut(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        static TextMesh Label(Vector3 lp, string s, float size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = Fx.Text(root.transform, s, size, c, anchor);
            t.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : anchor == TextAnchor.MiddleRight ? TextAlignment.Right : TextAlignment.Left;
            t.transform.localPosition = lp; return t;
        }

        static void Build()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            buttons.Clear(); hover = null; lastTouched = null; requireRelease = true;
            root = new GameObject("JClientBoard");
            var accent = Fx.Accent;
            float top = H / 2f, left = -W / 2f;

            Panel(new Vector3(0, 0, 0.004f), new Vector3(W + 0.006f, H + 0.006f, 0.004f), Line);
            Panel(new Vector3(0, 0, 0.002f), new Vector3(W, H, 0.005f), Bg);

            // ---- sidebar ----
            float sx = left + 0.008f, scx = sx + SideW / 2f;
            Panel(new Vector3(scx, 0, 0f), new Vector3(SideW, H - 0.016f, 0.004f), PanelC);
            Label(new Vector3(sx + 0.01f, top - 0.024f, -0.004f), "<b>J</b> <i><color=#" + Hex(accent) + ">Client</color></i>", 0.0024f, Color.white);
            Label(new Vector3(sx + 0.011f, top - 0.041f, -0.004f), "R E L E A S E", 0.0007f, Dim);
            float y = top - 0.058f;
            foreach (var cat in DesktopUI.ModuleCats) { CatRow(scx, y, cat); y -= 0.0158f; }
            y -= 0.006f;
            foreach (var cat in DesktopUI.General) { CatRow(scx, y, cat); y -= 0.0158f; }

            // ---- main area ----
            float mx = sx + SideW + 0.012f, mw = W / 2f - 0.01f - mx, mcx = mx + mw / 2f;
            bool searching = Board.Search.Length > 0;
            string title = searching ? "Search" : Board.IsKeyboard ? "Keyboard" : Board.Cat;
            Label(new Vector3(mx, top - 0.024f, -0.004f), "<i><color=#" + Hex(accent) + ">" + title + "</color></i> <b>Modules</b>", 0.0026f, Color.white);
            AddButton(new Vector3(mx + mw - 0.035f, top - 0.024f, -0.002f), new Vector2(0.07f, 0.02f), Game.InRoom ? "Leave Room" : "No room", new Color(0.4f, 0.07f, 0.1f), Color.white, RoomMods.Leave, 0.0009f);

            if (Board.IsKeyboard) { KeyboardGrid(mx, mw, top); return; }

            var all = DesktopUI.View();
            Label(new Vector3(mx, top - 0.046f, -0.004f), all.Count + " modules  ·  " + all.Count(m => m.Toggle && m.On) + " enabled", 0.001f, Dim);

            float cy = top - 0.07f - CardH / 2f;
            foreach (var m in PageMods(all)) { Card(mcx, cy, mw, m, accent); cy -= CardH + CardGap; }
            if (all.Count == 0) Label(new Vector3(mcx, cy, -0.004f), "Nothing here", 0.0012f, Dim, TextAnchor.MiddleCenter);

            float ny = -top + 0.022f;
            AddButton(new Vector3(mx + 0.03f, ny, -0.002f), new Vector2(0.06f, 0.022f), "‹ Prev", CardC, Color.white, () => page = Math.Max(0, page - 1), 0.0011f);
            Label(new Vector3(mcx, ny, -0.004f), "Page " + (page + 1) + " / " + PageCount(all), 0.001f, Dim, TextAnchor.MiddleCenter);
            AddButton(new Vector3(mx + mw - 0.03f, ny, -0.002f), new Vector2(0.06f, 0.022f), "Next ›", CardC, Color.white, () => page = Math.Min(PageCount(all) - 1, page + 1), 0.0011f);
        }

        static void CatRow(float cx, float y, string cat)
        {
            bool sel = Board.Search.Length == 0 && Board.Cat == cat;
            float w = SideW - 0.012f;
            var bg = sel ? new Color(Fx.Accent.r * 0.2f, Fx.Accent.g * 0.2f, Fx.Accent.b * 0.25f + 0.05f) : PanelC;
            AddButton(new Vector3(cx, y, -0.002f), new Vector2(w, 0.014f), "", bg, Color.white, () => Board.OpenCategory(cat), 0.001f);
            if (sel) Panel(new Vector3(cx - w / 2f + 0.002f, y, -0.006f), new Vector3(0.0025f, 0.01f, 0.001f), Fx.Accent);
            Label(new Vector3(cx - w / 2f + 0.008f, y, -0.006f), cat, 0.00105f, sel ? Color.white : new Color(0.78f, 0.8f, 0.88f));
            Label(new Vector3(cx + w / 2f - 0.006f, y, -0.006f), DesktopUI.CountOf(cat).ToString(), 0.0009f, Dim, TextAnchor.MiddleRight);
        }

        static void Card(float cx, float cy, float w, Mod m, Color accent)
        {
            float l = cx - w / 2f, r = cx + w / 2f;
            // Option pill first so a poke on it wins over the card underneath
            if (m.Options != null)
            {
                float px = m.Toggle || m.Press != null ? r - 0.085f : r - 0.04f;
                AddButton(new Vector3(px, cy, -0.006f), new Vector2(0.06f, 0.016f), Cut(m.Option, 12), new Color(0.06f, 0.07f, 0.12f), Color.white, () => m.Cycle(), 0.0009f);
            }
            AddButton(new Vector3(cx, cy, -0.002f), new Vector2(w, CardH), "", CardC, Color.white, () => Board.Click(new Board.Entry { M = m }), 0.001f);
            if (m.Toggle && m.On) Panel(new Vector3(l + 0.003f, cy, -0.006f), new Vector3(0.0025f, CardH - 0.012f, 0.001f), accent);

            string name = (m.Status != null ? m.Status() : m.Name) + (m.Others ? " *" : "") + (m.Fav ? " [fav]" : ""); // the VR font has no star glyph
            Label(new Vector3(l + 0.012f, cy + 0.007f, -0.006f), "<b>" + Cut(name, 30) + "</b>", 0.00135f, Color.white);
            Label(new Vector3(l + 0.012f, cy - 0.008f, -0.006f), Cut(string.IsNullOrEmpty(m.Desc) ? m.Cat : m.Desc, 52), 0.0008f, Dim);

            if (m.Toggle)
            {
                float tx = r - 0.025f;
                Panel(new Vector3(tx, cy, -0.006f), new Vector3(0.03f, 0.014f, 0.002f), m.On ? accent : Off);
                Panel(new Vector3(tx + (m.On ? 0.008f : -0.008f), cy, -0.008f), new Vector3(0.011f, 0.011f, 0.002f), Color.white);
            }
            else if (m.Press != null) Label(new Vector3(r - 0.025f, cy, -0.006f), "Run", 0.0011f, accent, TextAnchor.MiddleCenter);
        }

        static void KeyboardGrid(float mx, float mw, float top)
        {
            Label(new Vector3(mx, top - 0.05f, -0.004f), Keys.Buffer + "_", 0.0024f, Color.white);
            var keys = Board.Visible();
            int cols = 8; float kw = (mw - 0.004f * (cols - 1)) / cols, kh = 0.022f, y0 = top - 0.08f;
            for (int i = 0; i < keys.Count; i++)
            {
                var e = keys[i]; int c = i % cols, row = i / cols;
                bool action = e.M != null && e.M.Name.Length > 1;
                AddButton(new Vector3(mx + kw / 2f + c * (kw + 0.004f), y0 - row * (kh + 0.004f), -0.002f), new Vector2(kw, kh), e.M.Name,
                    action ? new Color(Fx.Accent.r * 0.3f, Fx.Accent.g * 0.3f, Fx.Accent.b * 0.35f) : CardC, Color.white, () => Board.Click(e), 0.0013f);
            }
        }

        static void Panel(Vector3 lp, Vector3 size, Color c)
        {
            var g = Fx.Prim(PrimitiveType.Cube, size, c);
            g.transform.SetParent(root.transform, false); g.transform.localPosition = lp;
        }

        static void AddButton(Vector3 lp, Vector2 size, string label, Color bg, Color fg, Action click, float textSize)
        {
            var g = Fx.Prim(PrimitiveType.Cube, new Vector3(size.x, size.y, 0.006f), bg, collider: true);
            g.layer = 2; // Ignore Raycast: never blocks guns or locomotion
            g.transform.SetParent(root.transform, false); g.transform.localPosition = lp;
            var col = g.GetComponent<Collider>(); col.isTrigger = true;
            var t = Fx.Text(root.transform, label, textSize, fg); t.transform.localPosition = lp + new Vector3(0, 0, -0.0045f);
            buttons.Add(new B { Col = col, Click = click, R = g.GetComponent<Renderer>(), Base = bg });
        }
    }

    // On-screen extras: active mods list (top-right) and toasts (bottom-right).
    public static class Hud
    {
        struct T { public string Text; public float Time; }
        static readonly List<T> toasts = new List<T>();
        static GUIStyle sList, sToast, sMark;
        public static readonly List<Func<string>> Lines = new List<Func<string>>(); // info lines from HUD mods (FPS, speed, room...)
        public static readonly List<Action> Draws = new List<Action>(); // extra IMGUI drawing (crosshair...)
        static TextMesh vrText; static float vrNext;

        // VR: the same info floating at the top-left of your view
        public static void VRHud()
        {
            bool show = Inp.VR && Settings.VRHud != null && Settings.VRHud.On && Settings.ShowHud.On;
            if (!show) { if (vrText != null) vrText.gameObject.SetActive(false); return; }
            if (vrText == null) { var g = new GameObject("JClientVRHud"); Fx.Spawned.Add(g); vrText = Fx.Text(g.transform, "", 0.0018f, Fx.Accent, TextAnchor.UpperLeft); vrText.alignment = TextAlignment.Left; }
            var h = Game.Head; var t = vrText.transform.parent;
            t.gameObject.SetActive(true);
            t.position = h.position + h.forward * 0.7f - h.right * 0.32f + h.up * 0.22f; t.rotation = h.rotation;
            vrText.color = Fx.Accent;
            if (Time.unscaledTime < vrNext) return; vrNext = Time.unscaledTime + 0.25f;
            var sb = new System.Text.StringBuilder("J CLIENT\n");
            foreach (var f in Lines) { string l = null; try { l = f(); } catch { } if (!string.IsNullOrEmpty(l)) sb.Append(l).Append('\n'); }
            foreach (var m in Mods.All) if (m.Toggle && m.On && m.Cat != "Settings") sb.Append("> ").Append(m.Name).Append('\n');
            vrText.text = sb.ToString();
        }

        public static void Toast(string s)
        {
            if (Plugin.Log != null) Plugin.Log.LogInfo(s);
            if (Settings.Toasts != null && !Settings.Toasts.On) return;
            toasts.Add(new T { Text = s, Time = Time.unscaledTime });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        public static void Draw()
        {
            if (sList == null)
            {
                sList = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
                sToast = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleRight, padding = new RectOffset(10, 10, 4, 4) };
                sMark = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold };
            }
            float W = Screen.width, H = Screen.height;
            if (Settings.ShowHud != null && Settings.ShowHud.On)
            {
                sMark.normal.textColor = Fx.Accent;
                float ly = 8;
                GUI.Label(new Rect(10, ly, 300, 20), "J CLIENT", sMark); ly += 18;
                foreach (var f in Lines) { string l = null; try { l = f(); } catch { } if (string.IsNullOrEmpty(l)) continue; foreach (var part in l.Split('\n')) { GUI.Label(new Rect(10, ly, 500, 20), part, sMark); ly += 18; } }

                var on = Mods.All.Where(m => m.Toggle && m.On && m.Cat != "Settings").Select(m => m.Name).OrderByDescending(n => n.Length).ToList();
                for (int i = 0; i < on.Count; i++)
                {
                    sList.normal.textColor = Color.Lerp(Fx.Accent, new Color(0.2f, 0.4f, 1f), i / (float)Mathf.Max(1, on.Count));
                    GUI.Label(new Rect(W - 310, 8 + i * 19, 300, 20), on[i], sList);
                }
            }
            foreach (var d in Draws) try { d(); } catch { }
            toasts.RemoveAll(t => Time.unscaledTime - t.Time > 3.5f);
            for (int i = 0; i < toasts.Count; i++)
            {
                float a = Mathf.Clamp01(3.5f - (Time.unscaledTime - toasts[i].Time));
                sToast.normal.textColor = new Color(Fx.Accent.r, Fx.Accent.g, Fx.Accent.b, a);
                var r = new Rect(W - 420, H - 40 - (toasts.Count - i) * 26, 410, 24);
                GUI.color = new Color(0, 0, 0, 0.55f * a); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(r, toasts[i].Text, sToast);
            }
        }
    }
}
