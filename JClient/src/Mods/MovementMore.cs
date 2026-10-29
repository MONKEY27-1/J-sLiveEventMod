using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Third batch of Movement modules.
    public static class MovementMore
    {
        static Rigidbody Body => Game.Body;
        static Transform Head => Game.Head;
        static Vector3 HandDir(Transform h) => Settings.GunForward.On ? h.forward : -h.up;

        public static void Register()
        {
            const string c = "Movement";
            Mods.T(c, "Trigger Fly", () => { if (Inp.RTrig.Held && !Board.VROpen) Body.linearVelocity = Head.forward * 15f * Game.Scale; }).Info("Hold right trigger to fly where you look");
            Mods.T(c, "Hand Fly", () => { if (Inp.RGrip.Held && !Gun.Aiming) { Body.linearVelocity = HandDir(Game.RHand) * 15f * Game.Scale; } }).Info("Hold right grip to fly in the direction your hand points");
            Vector3 lastL = Vector3.zero, lastR = Vector3.zero;
            Mods.T(c, "Flap Fly", () =>
            {
                float dl = lastL.y - Game.LHand.position.y, dr = lastR.y - Game.RHand.position.y; lastL = Game.LHand.position; lastR = Game.RHand.position;
                if (dl > 0.01f && dr > 0.01f) Body.linearVelocity += Vector3.up * (dl + dr) * 40f * Game.Scale + Head.forward * 0.15f;
            }).Info("Flap both arms downward like wings to fly up");
            Mods.T(c, "Jetpack", () => { if (Inp.LGrip.Held && Inp.RGrip.Held) { Movement.AntiGrav(1f); Body.linearVelocity += Vector3.up * 12f * Time.deltaTime * Game.Scale; } }).Info("Hold both grips to rocket upward");
            Mods.T(c, "Rocket Boots", () => { if (Inp.RA.Down || Inp.KeyDown(Key.Space) && !Mods.Get("WASD Fly").On) { var v = Body.linearVelocity; Body.linearVelocity = new Vector3(v.x, 14f * Game.Scale, v.z); } }).Info("Right A (desktop: Space) blasts you straight up");
            Mods.T(c, "Super Monke", () =>
            {
                RigCtl.SpeedMul = 2f;
                if (Inp.RA.Held || Inp.Key(Key.F)) Body.linearVelocity = Head.forward * 30f * Game.Scale;
            }, null, () => RigCtl.SpeedMul = 1f).Info("2x speed plus fast flying on right A (desktop: F)");
            Vector3? anchorL = null, anchorR = null;
            LineRenderer webL = null, webR = null;
            Mods.T(c, "Spider Monke", () =>
            {
                Web(ref anchorL, ref webL, Inp.LTrig, Game.LHand); Web(ref anchorR, ref webR, Inp.RTrig, Game.RHand);
            }, null, () => { anchorL = anchorR = null; Fx.Kill(webL); Fx.Kill(webR); webL = webR = null; }).Info("Pull a trigger to shoot a web from that hand and swing on it");
            Mods.T(c, "Pogo", () => { if (Game.P.BodyOnGround || Game.P.IsGroundedHand) { var v = Body.linearVelocity; if (v.y < 3f) Body.linearVelocity = new Vector3(v.x, 7f * Game.Scale, v.z); } }).Info("You bounce automatically like on a pogo stick");
            Mods.T(c, "Elevator", () =>
            {
                float up = Inp.RStick.y + (Inp.Key(Key.PageUp) ? 1 : 0) - (Inp.Key(Key.PageDown) ? 1 : 0);
                var v = Body.linearVelocity; Body.linearVelocity = new Vector3(v.x, up * 6f * Game.Scale, v.z); Movement.AntiGrav(1f);
            }).Info("Hover; right stick up/down (desktop: Page Up/Down) moves you vertically");
            Mods.T(c, "Velocity Boost", () => { if (Inp.LX.Down || Inp.KeyDown(Key.K)) Body.linearVelocity *= 1.6f; }).Info("Left X (desktop: K) multiplies your current speed by 1.6");
            Mods.T(c, "Instant Stop", () => { if (Inp.KeyDown(Key.L) || Inp.LGrip.Held && Inp.RGrip.Held && Inp.LTrig.Held && Inp.RTrig.Held) Body.linearVelocity = Vector3.zero; }).Info("Desktop L (VR: both grips + both triggers) kills all your momentum");
            Mods.T(c, "Car Mode", () =>
            {
                var f = Head.forward; f.y = 0; f.Normalize(); var r = Head.right; r.y = 0; r.Normalize();
                var move = f * (Inp.LStick.y + (Inp.Key(Key.UpArrow) ? 1 : 0) - (Inp.Key(Key.DownArrow) ? 1 : 0)) + r * Inp.LStick.x;
                var v = Body.linearVelocity; var h = move * 10f * Game.Scale; Body.linearVelocity = new Vector3(h.x, v.y, h.z);
            }).Info("Drive along the ground with the left stick (desktop: arrow keys)");
            Mods.T(c, "Gravity Flip", () => Movement.AntiGrav(2f)).Info("Gravity pulls you up instead of down");
            bool wasDown = false;
            Mods.T(c, "Super Jump", () =>
            {
                bool down = Game.P.LeftHand.isColliding || Game.P.RightHand.isColliding;
                if (wasDown && !down && Body.linearVelocity.y > 1f) { var v = Body.linearVelocity; Body.linearVelocity = new Vector3(v.x, v.y * 1.8f, v.z); }
                wasDown = down;
            }).Info("Every jump goes 80% higher");
        }

        static void Web(ref Vector3? anchor, ref LineRenderer line, Btn trigger, Transform hand)
        {
            if (trigger.Down && !Board.VROpen && Physics.Raycast(hand.position, HandDir(hand), out var hit, 60f, GorillaLocomotion.GTPlayer.LocomotionEnabledLayers, QueryTriggerInteraction.Ignore)) anchor = hit.point;
            if (!trigger.Held) anchor = null;
            if (anchor == null) { if (line != null) line.gameObject.SetActive(false); return; }
            if (line == null) line = Fx.Line(Color.white, 0.01f, false);
            line.gameObject.SetActive(true); line.SetPosition(0, hand.position); line.SetPosition(1, anchor.Value);
            Body.linearVelocity += (anchor.Value - Head.position).normalized * 25f * Time.deltaTime * Game.Scale;
        }
    }
}
