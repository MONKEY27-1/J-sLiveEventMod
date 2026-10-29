using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace JClient
{
    // Shared helpers for the Flight / Parkour / Teleport / Experimental modules: ground and wall checks, hand and head motion
    // relative to your body, crouch detection and a few velocity shortcuts. Everything here only reads or moves the local player.
    public static class MoveKit
    {
        public static Rigidbody Body => Game.Body;
        public static Transform Head => Game.Head;
        public static float S => Game.Scale;
        public static float Dt => Mathf.Max(Time.deltaTime, 0.0001f);

        public static bool Touching(GTPlayer.HandState h) => h.isColliding || h.wasColliding;

        public static bool HandsDown => Touching(Game.P.LeftHand) || Touching(Game.P.RightHand);
        public static bool Grounded => Game.P.BodyOnGround || Game.P.IsGroundedHand || HandsDown;
        public static bool Airborne => !Grounded;

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        public static Vector3 Fwd => FlatDir(Head.forward);
        public static Vector3 Right => FlatDir(Head.right);

        // A stick as a direction on the ground plane, relative to where you look
        public static Vector3 FromStick(Vector2 s) => Fwd * s.y + Right * s.x;

        public static Vector3 HVel => Flat(Body.linearVelocity);

        public static void SetH(Vector3 h)
        {
            var v = Body.linearVelocity;
            Body.linearVelocity = new Vector3(h.x, v.y, h.z);
        }

        public static void SetY(float y)
        {
            var v = Body.linearVelocity;
            Body.linearVelocity = new Vector3(v.x, y, v.z);
        }

        public static void Stop()
        {
            Body.linearVelocity = Vector3.zero;
            Movement.AntiGrav(1f);
        }

        // Pointing direction of a hand (same rule as the gun: Settings > Gun Aims Forward)
        public static Vector3 HandDir(Transform h) => Settings.GunForward.On ? h.forward : -h.up;

        public static bool Cast(Vector3 from, Vector3 dir, float dist, out RaycastHit hit) =>
            Physics.Raycast(from, dir, out hit, dist, GTPlayer.LocomotionEnabledLayers, QueryTriggerInteraction.Ignore);

        public static Vector3 BodyPos => Game.P.bodyCollider.transform.position;

        // Bottom of your body collider (a switched-off collider has empty bounds, so then estimate from its position)
        public static float FeetY
        {
            get
            {
                var col = Game.P.bodyCollider;
                return col.enabled ? col.bounds.min.y : col.transform.position.y - 0.55f * S;
            }
        }

        // Distance from the head down to the first surface (infinity when there is nothing within max metres)
        public static float Clearance(float max = 30f) =>
            Cast(Head.position, Vector3.down, max * S, out var h) ? h.distance : float.PositiveInfinity;

        // The player position that puts you standing on a surface point
        public static Vector3 StandAt(Vector3 surface) => surface + Vector3.up * 0.6f * S;

        // First surface below p (within max metres) that you can stand on
        public static bool GroundBelow(Vector3 p, float max, out Vector3 point)
        {
            if (Cast(p, Vector3.down, max, out var h) && h.normal.y > 0.5f)
            {
                point = h.point;
                return true;
            }
            point = p;
            return false;
        }

        // The wall a hand is touching right now (normal close to horizontal)
        public static bool WallTouch(out Vector3 normal, out bool left)
        {
            foreach (var (state, isLeft) in new[] { (Game.P.LeftHand, true), (Game.P.RightHand, false) })
                if (Touching(state) && Mathf.Abs(state.lastHitInfo.normal.y) < 0.35f)
                {
                    normal = state.lastHitInfo.normal;
                    left = isLeft;
                    return true;
                }
            normal = Vector3.zero;
            left = false;
            return false;
        }

        // Normal of any surface a hand is touching (floor, wall or slope)
        public static bool SurfaceTouch(out Vector3 normal)
        {
            foreach (var state in new[] { Game.P.LeftHand, Game.P.RightHand })
                if (Touching(state) && state.lastHitInfo.normal != Vector3.zero)
                {
                    normal = state.lastHitInfo.normal;
                    return true;
                }
            normal = Vector3.up;
            return false;
        }

        // Desktop shortcut keys of these modules are ignored while the desktop menu is open (you are typing into it)
        public static bool KeyDown(UnityEngine.InputSystem.Key k) => !Board.DesktopOpen && Inp.KeyDown(k);

        public static bool Key(UnityEngine.InputSystem.Key k) => !Board.DesktopOpen && Inp.Key(k);

        public static void Buzz(bool left, float amp = 0.3f, float dur = 0.06f)
        {
            try
            {
                Game.T.StartVibration(left, amp, dur);
            }
            catch { }
        }

        public static void BuzzBoth(float amp = 0.3f, float dur = 0.06f)
        {
            Buzz(true, amp, dur);
            Buzz(false, amp, dur);
        }

        public static string Fmt(Vector3 p) =>
            p.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            + ", "
            + p.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            + ", "
            + p.z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        // Reads "x, y, z" (the Copy Position format); returns false for anything else
        public static bool Parse(string s, out Vector3 p)
        {
            p = Vector3.zero;
            if (string.IsNullOrEmpty(s))
                return false;
            var parts = s.Split(',');
            if (parts.Length != 3)
                return false;
            var f = new float[3];
            for (int i = 0; i < 3; i++)
                if (
                    !float.TryParse(
                        parts[i].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out f[i]
                    )
                )
                    return false;
            p = new Vector3(f[0], f[1], f[2]);
            return true;
        }

        // ---- motion of your hands and head relative to your body, sampled once per frame on demand ----
        static int frame = -1;
        static Vector3 lastL,
            lastR,
            lastH;
        static Vector3 lVel,
            rVel,
            hVel;

        static void Sample()
        {
            if (frame == Time.frameCount)
                return;
            var root = Game.P.transform.position;
            Vector3 l = Game.LHand.position - root,
                r = Game.RHand.position - root,
                h = Head.position - root;
            bool fresh = frame == Time.frameCount - 1; // a gap (mods switched off a while) would read as a huge jump
            lVel = fresh ? (l - lastL) / Dt : Vector3.zero;
            rVel = fresh ? (r - lastR) / Dt : Vector3.zero;
            hVel = fresh ? (h - lastH) / Dt : Vector3.zero;
            lastL = l;
            lastR = r;
            lastH = h;
            frame = Time.frameCount;
        }

        public static Vector3 LeftHandVel
        {
            get
            {
                Sample();
                return lVel;
            }
        }
        public static Vector3 RightHandVel
        {
            get
            {
                Sample();
                return rVel;
            }
        }
        public static Vector3 HeadVel
        {
            get
            {
                Sample();
                return hVel;
            }
        }

        // ---- crouching: how high your head is compared with your normal standing height ----
        static float stand = -1f;
        static int standFrame = -1;
        static float crouch = 1f;

        // Size multiplier set by the modules that resize you (Crouch Shrink, Size By Speed), so crouch detection stays right while resized
        public static float ScaleMul = 1f;

        // 1 = standing tall, 0.5 = head at half your standing height. Calibrates itself while you stand on the ground.
        public static float CrouchRatio()
        {
            if (standFrame == Time.frameCount)
                return crouch;
            standFrame = Time.frameCount;
            float c = Clearance(4f);
            if (float.IsInfinity(c))
                return crouch = 1f;
            c /= Mathf.Max(0.05f, ScaleMul);
            if (Game.P.BodyOnGround || Game.P.IsGroundedHand)
                stand = stand < 0f ? c : (c > stand ? c : Mathf.MoveTowards(stand, c, 0.02f * Dt)); // rises at once, sinks very slowly
            return crouch = stand > 0.05f ? Mathf.Clamp01(c / stand) : 1f;
        }

        // ---- background tracking used by Teleport / Room modules (called every frame from Plugin.Update) ----
        public static string LastRoom;
        public static Vector3? RoomStart;
        public static readonly List<Vector3> Stops = new List<Vector3>();
        static bool wasInRoom;
        static float roomJoinedAt,
            stillFor;
        static string curRoom;

        public static void Track()
        {
            bool inRoom = Game.InRoom;
            if (inRoom && !wasInRoom)
            {
                roomJoinedAt = Time.time;
                RoomStart = null;
                curRoom = NetworkSystem.Instance.RoomName;
            }
            if (inRoom && RoomStart == null && Time.time - roomJoinedAt > 2f)
                RoomStart = Game.P.transform.position;
            if (!inRoom && wasInRoom && !string.IsNullOrEmpty(curRoom))
                LastRoom = curRoom;
            wasInRoom = inRoom;

            // places you stood still for a second (Previous Spot)
            stillFor = Body.linearVelocity.sqrMagnitude < 0.09f ? stillFor + Time.deltaTime : 0f;
            if (stillFor > 1f)
            {
                var p = Game.P.transform.position;
                if (Stops.Count == 0 || (Stops[Stops.Count - 1] - p).sqrMagnitude > 4f * S * S)
                {
                    Stops.Add(p);
                    if (Stops.Count > 10)
                        Stops.RemoveAt(0);
                }
            }
        }
    }
}
