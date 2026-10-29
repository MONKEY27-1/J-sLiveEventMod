using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Teleport: portals, time-based returns, level hopping, saved spots and safety nets. Only you are moved.
    public static class TeleportMods
    {
        static Transform Head => Game.Head;

        static GameObject PlacePortal(GameObject g, Vector3 p, Vector3 n, Color col)
        {
            var size = new Vector3(1.3f, 0.02f, 1.3f) * MoveKit.S;
            if (g == null)
                g = Fx.Prim(PrimitiveType.Cylinder, size, col);
            g.transform.position = p + n * 0.03f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            g.transform.localScale = size;
            return g;
        }

        // Every surface hit straight down from `from` (one per collider), nearest first
        static RaycastHit[] AllBelow(Vector3 from, float dist) =>
            Physics
                .RaycastAll(from, Vector3.down, dist, GorillaLocomotion.GTPlayer.LocomotionEnabledLayers, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance)
                .ToArray();

        public static void Register()
        {
            const string c = "Teleport";

            GameObject portalA = null,
                portalB = null;
            Vector3 normalA = Vector3.up,
                normalB = Vector3.up;
            bool nextA = true;
            float portalCool = 0f;
            Mods.T(
                    c,
                    "Portal Gun",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Gun.Get(out var p, out _) && Gun.FireDown)
                        {
                            var dir = (p - Gun.Origin).normalized;
                            var n = MoveKit.Cast(Gun.Origin, dir, Vector3.Distance(Gun.Origin, p) + 0.5f, out var hit) ? hit.normal : -dir;
                            if (nextA)
                            {
                                portalA = PlacePortal(portalA, p, n, new Color(1f, 0.55f, 0.1f));
                                normalA = n;
                            }
                            else
                            {
                                portalB = PlacePortal(portalB, p, n, new Color(0.1f, 0.55f, 1f));
                                normalB = n;
                            }
                            nextA = !nextA;
                        }
                        if ((portalCool -= MoveKit.Dt) > 0f || portalA == null || portalB == null)
                            return;
                        var body = MoveKit.BodyPos;
                        GameObject to = null;
                        var outN = Vector3.up;
                        if (Vector3.Distance(body, portalA.transform.position) < 0.8f * s)
                        {
                            to = portalB;
                            outN = normalB;
                        }
                        else if (Vector3.Distance(body, portalB.transform.position) < 0.8f * s)
                        {
                            to = portalA;
                            outN = normalA;
                        }
                        if (to == null)
                            return;
                        float speed = Mathf.Max(Game.Body.linearVelocity.magnitude, 2f * s);
                        Game.Teleport(to.transform.position + outN * 0.9f * s);
                        Game.Body.linearVelocity = outN * speed; // out of the other portal, facing out, with your speed
                        portalCool = 0.8f;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(portalA);
                        Fx.Kill(portalB);
                        portalA = portalB = null;
                        nextA = true;
                    }
                )
                .Info(
                    "Fire the gun to place an orange portal, fire again for a blue one. Go into one to come out of the other with your speed"
                );

            var trail = new List<(float t, Vector3 p)>();
            float trailTick = 0f;
            Mods.T(
                    c,
                    "Recall",
                    () =>
                    {
                        float now = Time.time;
                        if ((trailTick -= MoveKit.Dt) <= 0f)
                        {
                            trailTick = 0.1f;
                            trail.Add((now, Game.P.transform.position));
                        }
                        while (trail.Count > 0 && now - trail[0].t > 3.2f)
                            trail.RemoveAt(0);
                        if (trail.Count == 0 || !(Inp.RB.Down || MoveKit.KeyDown(Key.R)))
                            return;
                        Game.Teleport(trail[0].p);
                        Game.Body.linearVelocity = Vector3.zero;
                        trail.Clear();
                        MoveKit.BuzzBoth(0.4f, 0.1f);
                    },
                    null,
                    () => trail.Clear()
                )
                .Info("Right B (desktop: R) snaps you back to where you were 3 seconds ago");

            var past = new List<Vector3>();
            float pastTick = 0f;
            bool rewinding = false;
            Mods.T(
                    c,
                    "Rewind",
                    () =>
                    {
                        bool hold = Inp.LGrip.Held && Inp.LTrig.Held || MoveKit.Key(Key.B);
                        if (!hold)
                        {
                            if (rewinding)
                            {
                                rewinding = false;
                                Game.Body.linearVelocity = Vector3.zero;
                            }
                            if ((pastTick -= MoveKit.Dt) <= 0f)
                            {
                                pastTick = 0.05f;
                                past.Add(Game.P.transform.position);
                                if (past.Count > 200) // 10 seconds
                                    past.RemoveAt(0);
                            }
                            return;
                        }
                        rewinding = true;
                        if (past.Count == 0)
                        {
                            MoveKit.Stop();
                            return;
                        }
                        var target = past[past.Count - 1];
                        for (int i = Mathf.Max(1, Mathf.RoundToInt(MoveKit.Dt / 0.025f)); i > 0 && past.Count > 0; i--) // twice real speed
                        {
                            target = past[past.Count - 1];
                            past.RemoveAt(past.Count - 1);
                        }
                        Game.Body.linearVelocity = Vector3.ClampMagnitude(
                            (target - Game.P.transform.position) / MoveKit.Dt,
                            40f * MoveKit.S
                        );
                        Movement.AntiGrav(1f);
                    },
                    null,
                    () =>
                    {
                        past.Clear();
                        rewinding = false;
                    }
                )
                .Info("Hold left grip + left trigger (desktop: B) to rewind your last 10 seconds of movement like a video");

            Mods.B(
                    c,
                    "Floor Down",
                    () =>
                    {
                        float s = MoveKit.S,
                            feet = MoveKit.FeetY;
                        foreach (var h in AllBelow(Head.position, 300f * s))
                            if (h.point.y < feet - 0.5f * s && h.normal.y > 0.5f)
                            {
                                Game.Teleport(MoveKit.StandAt(h.point));
                                return;
                            }
                        Hud.Toast("Nothing below you");
                    }
                )
                .Info("Drops you to the next surface below the one you're on (down a level, down a tree, down to the ground)");

            Mods.B(
                    c,
                    "Floor Up",
                    () =>
                    {
                        float s = MoveKit.S;
                        var head = Head.position;
                        // straight down from high above: the lowest top surface that is still above your head is the next level up
                        var above = AllBelow(head + Vector3.up * 60f * s, 60f * s)
                            .Where(h => h.point.y > head.y + 0.3f * s && h.normal.y > 0.5f)
                            .OrderBy(h => h.point.y)
                            .ToList();
                        if (above.Count == 0)
                        {
                            Hud.Toast("Nothing above you");
                            return;
                        }
                        Game.Teleport(MoveKit.StandAt(above[0].point));
                    }
                )
                .Info(
                    "Puts you on top of the next thing above you (up a level, onto the branch or roof overhead); press again to keep climbing"
                );

            Mods.B(
                    c,
                    "Hop Onto",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (!MoveKit.Cast(Head.position, Head.forward, 25f * s, out var hit))
                        {
                            Hud.Toast("Look at something to hop onto");
                            return;
                        }
                        var probe = hit.point - MoveKit.FlatDir(hit.normal) * 0.3f * s + Vector3.up * 6f * s;
                        if (!MoveKit.Cast(probe, Vector3.down, 7f * s, out var top) || top.normal.y < 0.5f)
                        {
                            Hud.Toast("No top to stand on there");
                            return;
                        }
                        Game.Teleport(MoveKit.StandAt(top.point));
                    }
                )
                .Info("Teleports you on top of whatever you're looking at: a crate, roof, branch or rock (up to 25 m away)");

            Mods.B(
                    c,
                    "Random Spot",
                    () =>
                    {
                        float s = MoveKit.S;
                        var me = Game.P.transform.position;
                        for (int i = 0; i < 25; i++)
                        {
                            var a = UnityEngine.Random.insideUnitCircle * 60f * s;
                            if (
                                MoveKit.Cast(me + new Vector3(a.x, 40f * s, a.y), Vector3.down, 120f * s, out var hit)
                                && hit.normal.y > 0.7f
                            )
                            {
                                Game.Teleport(MoveKit.StandAt(hit.point));
                                return;
                            }
                        }
                        Hud.Toast("No open ground found nearby");
                    }
                )
                .Info("Teleports you to a random open spot within 60 m - good for exploring (Undo Teleport takes you back)");

            Mods.B(
                    c,
                    "Highest Point",
                    () =>
                    {
                        float s = MoveKit.S;
                        var me = Game.P.transform.position;
                        bool found = false;
                        var best = default(RaycastHit);
                        for (int x = -4; x <= 4; x++)
                        for (int z = -4; z <= 4; z++)
                        {
                            var from = me + new Vector3(x * 10f * s, 50f * s, z * 10f * s);
                            if (
                                MoveKit.Cast(from, Vector3.down, 200f * s, out var hit)
                                && hit.normal.y > 0.6f
                                && (!found || hit.point.y > best.point.y)
                            )
                            {
                                best = hit;
                                found = true;
                            }
                        }
                        if (found)
                            Game.Teleport(MoveKit.StandAt(best.point));
                        else
                            Hud.Toast("Nothing found");
                    }
                )
                .Info("Teleports you onto the highest surface within 40 m (rooftops, treetops, peaks)");

            GameObject marker = null;
            Mods.B(
                    c,
                    "Swap Marker",
                    () =>
                    {
                        var here = Game.P.transform.position;
                        if (marker == null)
                        {
                            marker = Fx.Prim(PrimitiveType.Cylinder, new Vector3(0.35f, 0.6f, 0.35f) * MoveKit.S, Fx.Accent);
                            marker.transform.position = here;
                            Hud.Toast("Marker placed - press again to swap places with it");
                            return;
                        }
                        var there = marker.transform.position;
                        marker.transform.position = here;
                        Game.Teleport(there);
                    }
                )
                .Info("First press drops a marker; every press after that swaps you and the marker (bounce between two spots)");

            var tour = new List<Vector3>();
            int tourAt = -1;
            Mods.B(
                    c,
                    "Add Tour Stop",
                    () =>
                    {
                        if (tour.Count >= 20)
                        {
                            Hud.Toast("The tour is full (20 stops)");
                            return;
                        }
                        tour.Add(Game.P.transform.position);
                        Hud.Toast("Tour stop " + tour.Count + " added");
                    }
                )
                .Live(() => "Add Tour Stop (" + tour.Count + ")")
                .Info("Adds the spot you're on to your tour (up to 20 stops)");
            Mods.B(
                    c,
                    "Next Tour Stop",
                    () =>
                    {
                        if (tour.Count == 0)
                        {
                            Hud.Toast("Add tour stops first");
                            return;
                        }
                        tourAt = (tourAt + 1) % tour.Count;
                        Game.Teleport(tour[tourAt]);
                        Hud.Toast("Tour stop " + (tourAt + 1) + " / " + tour.Count);
                    }
                )
                .Info("Teleports to the next stop of your tour, looping back to the first");
            Mods.B(
                    c,
                    "Clear Tour",
                    () =>
                    {
                        tour.Clear();
                        tourAt = -1;
                        Hud.Toast("Tour cleared");
                    }
                )
                .Info("Removes every tour stop");
            Mods.B(
                    c,
                    "Share Tour",
                    () =>
                    {
                        GUIUtility.systemCopyBuffer = string.Join("; ", tour.Select(MoveKit.Fmt));
                        Hud.Toast("Copied " + tour.Count + " tour stops");
                    }
                )
                .Info("Copies your tour to the clipboard so a friend can load it");
            Mods.B(
                    c,
                    "Load Tour",
                    () =>
                    {
                        var stops = new List<Vector3>();
                        foreach (var part in (GUIUtility.systemCopyBuffer ?? "").Split(';'))
                            if (MoveKit.Parse(part, out var p))
                                stops.Add(p);
                        if (stops.Count == 0)
                        {
                            Hud.Toast("No tour in the clipboard (use Share Tour)");
                            return;
                        }
                        tour.Clear();
                        tour.AddRange(stops.Take(20));
                        tourAt = -1;
                        Hud.Toast("Loaded " + tour.Count + " tour stops");
                    }
                )
                .Info("Loads a tour from the clipboard (made with Share Tour)");

            var home = Plugin.I.Config.Bind("Teleport", "Home", "", "Saved home position (x, y, z) for Set Home / Go Home");
            Mods.B(
                    c,
                    "Set Home",
                    () =>
                    {
                        home.Value = MoveKit.Fmt(Game.P.transform.position);
                        Hud.Toast("Home set");
                    }
                )
                .Info("Saves where you're standing as home - kept after restarting the game");
            Mods.B(
                    c,
                    "Go Home",
                    () =>
                    {
                        if (MoveKit.Parse(home.Value, out var p))
                            Game.Teleport(p);
                        else
                            Hud.Toast("Set a home first");
                    }
                )
                .Info("Teleports you to your saved home");

            Vector3? statePos = null;
            Vector3 stateVel = Vector3.zero;
            Mods.B(
                    c,
                    "Save State",
                    () =>
                    {
                        statePos = Game.P.transform.position;
                        stateVel = Game.Body.linearVelocity;
                        Hud.Toast("State saved");
                    }
                )
                .Info("Saves your position AND your speed, like a save state - for practising a tricky jump");
            Mods.B(
                    c,
                    "Load State",
                    () =>
                    {
                        if (statePos == null)
                        {
                            Hud.Toast("Save a state first");
                            return;
                        }
                        Game.Teleport(statePos.Value);
                        Game.Body.linearVelocity = stateVel;
                    }
                )
                .Info("Puts you back at the saved state, moving exactly as fast as you were");

            Vector3? safe = null;
            Mods.T(
                    c,
                    "Fall Rescue",
                    () =>
                    {
                        float s = MoveKit.S;
                        var pos = Game.P.transform.position;
                        if (Game.P.BodyOnGround && Game.Body.linearVelocity.sqrMagnitude < 25f * s * s)
                            safe = pos;
                        if (safe == null || pos.y > safe.Value.y - 30f * s || Game.Body.linearVelocity.y > -12f * s)
                            return;
                        Game.Teleport(safe.Value);
                        Game.Body.linearVelocity = Vector3.zero;
                        Hud.Toast("Caught you - back on safe ground");
                        MoveKit.BuzzBoth(0.4f, 0.15f);
                    },
                    null,
                    () => safe = null
                )
                .Info("If you fall more than 30 m (off the map or a cliff) you're put back on the last ground you stood on");

            float stuckFor = 0f;
            Mods.T(
                    c,
                    "Auto Unstuck",
                    () =>
                    {
                        float s = MoveKit.S;
                        bool inside = Physics.CheckSphere(
                            Head.position,
                            0.05f * s,
                            GorillaLocomotion.GTPlayer.LocomotionEnabledLayers,
                            QueryTriggerInteraction.Ignore
                        );
                        stuckFor = inside ? stuckFor + MoveKit.Dt : 0f;
                        if (stuckFor < 0.75f)
                            return;
                        stuckFor = 0f;
                        var pos = Game.P.transform.position;
                        Game.Teleport(
                            MoveKit.Cast(pos + Vector3.up * 30f * s, Vector3.down, 30f * s, out var top)
                                ? MoveKit.StandAt(top.point)
                                : pos + Vector3.up * 3f * s
                        );
                        Hud.Toast("Unstuck");
                    },
                    null,
                    () => stuckFor = 0f
                )
                .Info("Notices when your head is stuck inside a wall or branch and pops you out on top");

            bool stepArmed = true;
            Mods.T(
                    c,
                    "Step Teleport",
                    () =>
                    {
                        float s = MoveKit.S;
                        var st = Inp.LStick;
                        if (st.magnitude < 0.3f)
                        {
                            stepArmed = true;
                            return;
                        }
                        if (!stepArmed || st.magnitude < 0.85f)
                            return;
                        stepArmed = false; // one hop per flick
                        var dir = MoveKit.FromStick(st).normalized;
                        float len = 2.5f * s;
                        if (MoveKit.Cast(Head.position, dir, len + 0.4f * s, out var wall))
                            len = Mathf.Max(0f, wall.distance - 0.4f * s);
                        if (len < 0.2f * s)
                            return;
                        var to = Game.P.transform.position + dir * len;
                        if (MoveKit.GroundBelow(to + Vector3.up * 0.5f * s, 4f * s, out var g))
                            to = MoveKit.StandAt(g);
                        Game.Teleport(to);
                    },
                    () => stepArmed = true
                )
                .Info("Flick the left stick to hop 2.5 m that way (comfortable movement that stops at walls)");

            LineRenderer arc = null;
            GameObject arcDot = null;
            Vector3? arcLand = null;
            Mods.T(
                    c,
                    "Arc Teleport",
                    () =>
                    {
                        float s = MoveKit.S;
                        if (Inp.LTrig.Held && !Board.VROpen)
                        {
                            var hand = Game.LHand;
                            var p = hand.position;
                            var v = MoveKit.HandDir(hand) * 10f * s;
                            var pts = new List<Vector3> { p };
                            arcLand = null;
                            for (int i = 0; i < 40; i++)
                            {
                                var next = p + v * 0.05f;
                                v += Physics.gravity * 0.05f;
                                if (MoveKit.Cast(p, next - p, (next - p).magnitude, out var hit))
                                {
                                    pts.Add(hit.point);
                                    if (hit.normal.y > 0.5f)
                                        arcLand = hit.point;
                                    break;
                                }
                                pts.Add(next);
                                p = next;
                            }
                            if (arc == null)
                            {
                                arc = Fx.Line(Fx.Accent, 0.012f, false);
                                arcDot = Fx.Prim(PrimitiveType.Sphere, Vector3.one * 0.2f * s, Fx.Accent);
                            }
                            arc.gameObject.SetActive(true);
                            arc.positionCount = pts.Count;
                            for (int i = 0; i < pts.Count; i++)
                                arc.SetPosition(i, pts[i]);
                            arc.material.color = arcLand != null ? Fx.Accent : new Color(1f, 0.3f, 0.3f);
                            arcDot.SetActive(arcLand != null);
                            if (arcLand != null)
                                arcDot.transform.position = arcLand.Value;
                            return;
                        }
                        if (arc == null || !arc.gameObject.activeSelf)
                            return;
                        arc.gameObject.SetActive(false); // trigger released: go
                        arcDot.SetActive(false);
                        if (arcLand != null)
                            Game.Teleport(MoveKit.StandAt(arcLand.Value));
                        arcLand = null;
                    },
                    null,
                    () =>
                    {
                        Fx.Kill(arc);
                        Fx.Kill(arcDot);
                        arc = null;
                        arcDot = null;
                        arcLand = null;
                    }
                )
                .Info(
                    "Hold left trigger to aim a curved teleport arc from your left hand; let go to land where it ends (red = nowhere to land)"
                );

            Mods.B(
                    c,
                    "Back To Room Start",
                    () =>
                    {
                        if (MoveKit.RoomStart != null)
                            Game.Teleport(MoveKit.RoomStart.Value);
                        else
                            Hud.Toast("Join a room first");
                    }
                )
                .Info("Teleports you to where you were just after joining this room");

            Mods.B(
                    c,
                    "Paste Position",
                    () =>
                    {
                        if (MoveKit.Parse(GUIUtility.systemCopyBuffer, out var p))
                            Game.Teleport(p);
                        else
                            Hud.Toast("No position in the clipboard (use Copy Position)");
                    }
                )
                .Info("Teleports to coordinates from your clipboard - share spots with friends using Copy Position");

            Mods.B(
                    c,
                    "Previous Spot",
                    () =>
                    {
                        var stops = MoveKit.Stops;
                        var here = Game.P.transform.position;
                        float near = 2f * MoveKit.S;
                        while (stops.Count > 0 && (stops[stops.Count - 1] - here).magnitude < near)
                            stops.RemoveAt(stops.Count - 1); // skip the spot you're standing on
                        if (stops.Count == 0)
                        {
                            Hud.Toast("No earlier spot");
                            return;
                        }
                        var p = stops[stops.Count - 1];
                        stops.RemoveAt(stops.Count - 1);
                        Game.Teleport(p);
                    }
                )
                .Info("Goes back to the last place you stood still; keep pressing to go further back (10 spots)");
        }
    }
}
