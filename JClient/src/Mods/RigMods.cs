using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JClient
{
    // Your own avatar as others see it. Position mods use RigCtl.Hold; pose mods hook RigCtl.After (runs after the game poses the rig).
    public static class RigMods
    {
        static Vector3 Real => RigCtl.Real;

        static Mod Pose(string name, Action<VRRig> pose, string info)
        {
            Action<VRRig> hook = r => pose(r);
            return Mods.T("Rig", name, null, () => RigCtl.After += hook, () => RigCtl.After -= hook).Info(info);
        }

        static Vector3 Side(VRRig r, float side, float up) => r.transform.position + r.transform.right * side * 0.3f * r.scaleFactor + Vector3.up * up * r.scaleFactor;
        static Vector3 Rand(float s) => UnityEngine.Random.insideUnitSphere * s;

        public static void Register()
        {
            const string c = "Rig";

            RigPose? ghost = null;
            Mods.T(c, "Ghost (A)", () =>
            {
                if (!Inp.RA.Held) { ghost = null; return; }
                if (ghost == null) ghost = RigCtl.RealPose;
                RigCtl.Freeze(ghost.Value);
            }).Info("Hold A: your monke stays still while you move");
            Mods.T(c, "Invisible (B)", () => { if (Inp.RB.Held) RigCtl.Hold(Real - Vector3.up * 300f); }).Info("Hold B: your monke is moved far below the map so nobody can see it");

            RigPose? angel = null;
            Mods.T(c, "Weeping Angel", () =>
            {
                bool watched = Game.Others.Any(o =>
                {
                    var h = o.head.rigTarget; var to = Real - h.position;
                    return to.magnitude < 30f && Vector3.Dot(h.forward, to.normalized) > 0.85f;
                });
                if (!watched) { angel = null; return; }
                if (angel == null) angel = RigCtl.RealPose;
                RigCtl.Freeze(angel.Value);
            }).Info("Your monke freezes whenever someone is looking at it");

            Mods.T(c, "Rig Gun", () => { if (Gun.Get(out var p, out _) && Gun.Firing) RigCtl.Hold(p + Vector3.up * 0.5f * Game.Scale); }).Info("Hold fire to put your monke wherever you aim");
            Mods.T(c, "Grab Rig", () => { if (Inp.RGrip.Held && !Gun.Aiming) RigCtl.Hold(Game.RHand.position); }).Info("Hold right grip to carry your own monke around in your hand");

            RigPose lag = default; float lagTimer = 0f;
            Mods.T(c, "Laggy Rig", () => { if ((lagTimer -= Time.deltaTime) <= 0f) { lagTimer = 0.4f; lag = RigCtl.RealPose; } RigCtl.Freeze(lag); }).Info("Your monke only updates a few times a second, like bad internet");
            RigPose snap = default;
            Mods.T(c, "Update Rig (A)", () => { if (Inp.RA.Down) snap = RigCtl.RealPose; RigCtl.Freeze(snap); }, () => snap = RigPose.Capture(Game.Me)).Info("Your monke stays put until you press A");
            RigPose frozenPose = default;
            Mods.T(c, "Freeze Rig", () => RigCtl.Freeze(frozenPose), () => frozenPose = RigPose.Capture(Game.Me)).Info("Freezes your whole monke where it was");
            Action<VRRig> liveHands = r => { r.leftHand.rigTarget.position = Game.LHand.position; r.rightHand.rigTarget.position = Game.RHand.position; };
            Vector3 frozen = Vector3.zero; Quaternion frozenRot = Quaternion.identity;
            Mods.T(c, "Freeze Rig Body", () => RigCtl.Hold(frozen, frozenRot), () => { frozen = Game.Me.transform.position; frozenRot = Game.Me.transform.rotation; RigCtl.After += liveHands; }, () => RigCtl.After -= liveHands)
                .Info("Body stays frozen, hands still follow you");

            Vector3 lPos = Vector3.zero, rPos = Vector3.zero; Quaternion hRot = Quaternion.identity;
            Mods.T(c, "Freeze Rig Limbs", null, () => { var r = Game.Me; lPos = r.leftHand.rigTarget.localPosition; rPos = r.rightHand.rigTarget.localPosition; hRot = r.head.rigTarget.localRotation; RigCtl.After += FreezeLimbs; },
                () => RigCtl.After -= FreezeLimbs).Info("Head and hands lock in place on your body");
            void FreezeLimbs(VRRig r) { r.leftHand.rigTarget.localPosition = lPos; r.rightHand.rigTarget.localPosition = rPos; r.head.rigTarget.localRotation = hRot; }

            Pose("Paralyze Rig", r => { r.leftHand.rigTarget.position = Side(r, -1, -0.45f); r.rightHand.rigTarget.position = Side(r, 1, -0.45f); }, "Your monke's arms hang limp at its sides");
            Pose("Chicken Rig", r =>
            {
                float flap = Mathf.Sin(Time.time * 12f) * 0.08f;
                r.leftHand.rigTarget.position = Side(r, -0.8f, 0.05f + flap); r.rightHand.rigTarget.position = Side(r, 0.8f, 0.05f + flap);
            }, "Your monke flaps its arms like a chicken");
            Pose("Amputate Rig", r => { r.leftHand.rigTarget.position = r.transform.position; r.rightHand.rigTarget.position = r.transform.position; }, "Hides your monke's arms inside its body");

            Mod spin = null;
            spin = Pose("Spin Head", r =>
            {
                var axis = spin.Opt == 0 ? Vector3.right : spin.Opt == 1 ? Vector3.up : Vector3.forward;
                r.head.rigTarget.localRotation *= Quaternion.AngleAxis(Time.time * 360f % 360f, axis);
            }, "").Opts("Axis", "X", "Y", "Z").Def(1).Info("Spins your monke's head");
            Pose("Spaz Head", r => r.head.rigTarget.localRotation *= Quaternion.Euler(Rand(40f)), "Your monke's head twitches everywhere");
            Pose("Spaz Hands (A)", r => { if (!Inp.RA.Held) return; r.leftHand.rigTarget.position += Rand(0.25f * r.scaleFactor); r.rightHand.rigTarget.position += Rand(0.25f * r.scaleFactor); }, "Hold A: your monke's hands spaz out");
            Pose("Spaz Rig (A)", r =>
            {
                if (!Inp.RA.Held) return;
                r.transform.position += Rand(0.08f * r.scaleFactor); r.head.rigTarget.localRotation *= Quaternion.Euler(Rand(30f));
                r.leftHand.rigTarget.position += Rand(0.15f * r.scaleFactor); r.rightHand.rigTarget.position += Rand(0.15f * r.scaleFactor);
            }, "Hold A: your whole monke spazzes out a little");

            float yaw = 0f;
            Pose("Control Rig Rotation", r =>
            {
                yaw += (Inp.RStick.x + (Inp.Key(Key.RightArrow) ? 1 : 0) - (Inp.Key(Key.LeftArrow) ? 1 : 0)) * 180f * Time.deltaTime;
                if (Inp.KeyDown(Key.Enter) || Inp.RB.Down) yaw = 0f;
                r.transform.rotation = Quaternion.Euler(0, yaw, 0) * r.transform.rotation;
            }, "Turn your monke's body with the right stick or arrow keys. B or Enter resets");

            int rateBackup = 0;
            Mods.T(c, "Smooth Rig", () => Photon.Pun.PhotonNetwork.SerializationRate = 30, () => rateBackup = Photon.Pun.PhotonNetwork.SerializationRate,
                () => Photon.Pun.PhotonNetwork.SerializationRate = rateBackup > 0 ? rateBackup : 10).Info("Sends your monke's position 3x more often so it looks smoother to others");
        }
    }
}
