using System;
using UnityEngine;

namespace JClient
{
    // Emotes and dances for your monke as others see it. Same RigCtl.After hook as RigMods: runs after the game poses the rig.
    public static class RigEmotes
    {
        static Mod Pose(string name, Action<VRRig> pose, string info)
        {
            Action<VRRig> hook = r => pose(r);
            return Mods.T("Rig", name, null, () => RigCtl.After += hook, () => RigCtl.After -= hook).Info(info);
        }

        // Body-relative point: side = right(+)/left(-), up, fwd, in monke-sized units
        static Vector3 At(VRRig r, float side, float up, float fwd = 0.1f) =>
            r.transform.position + (r.transform.right * side + Vector3.up * up + r.transform.forward * fwd) * 0.3f * r.scaleFactor;

        static void Hands(VRRig r, Vector3 l, Vector3 rt)
        {
            r.leftHand.rigTarget.position = l;
            r.rightHand.rigTarget.position = rt;
        }

        static float T => Time.time;

        public static void Register()
        {
            Pose(
                "Wave",
                r => r.rightHand.rigTarget.position = At(r, 1.2f + Mathf.Sin(T * 8f) * 0.4f, 1.6f),
                "Your monke waves its right hand"
            );
            Pose("T-Pose", r => Hands(r, At(r, -2.4f, 0.6f, 0f), At(r, 2.4f, 0.6f, 0f)), "Arms straight out to the sides");
            Pose("Hands Up", r => Hands(r, At(r, -0.6f, 2.2f), At(r, 0.6f, 2.2f)), "Both hands raised high");
            Pose(
                "Clap",
                r =>
                {
                    float g = 0.15f + Mathf.Abs(Mathf.Sin(T * 7f)) * 0.5f;
                    Hands(r, At(r, -g, 0.6f, 1.4f), At(r, g, 0.6f, 1.4f));
                },
                "Your monke claps in front of itself"
            );
            Pose("Point", r => r.rightHand.rigTarget.position = At(r, 0.5f, 0.8f, 2.6f), "Points straight ahead with the right hand");
            Pose(
                "Dance",
                r =>
                {
                    float a = Mathf.Sin(T * 6f);
                    Hands(r, At(r, -1.2f, 0.8f + a * 0.9f, 0.6f), At(r, 1.2f, 0.8f - a * 0.9f, 0.6f));
                    r.transform.position += Vector3.up * Mathf.Abs(Mathf.Sin(T * 6f)) * 0.06f * r.scaleFactor;
                },
                "A bouncy arm-pumping dance"
            );
            Pose(
                "Floss",
                r =>
                {
                    float a = Mathf.Sin(T * 7f);
                    Hands(r, At(r, -0.8f + a * 1.1f, -0.8f, a * 0.6f), At(r, 0.8f + a * 1.1f, -0.8f, -a * 0.6f));
                },
                "The floss dance"
            );
            Pose(
                "Headbang",
                r =>
                    r.head.rigTarget.localRotation =
                        Quaternion.Euler(Mathf.Sin(T * 10f) * 30f + 10f, 0, 0) * r.head.rigTarget.localRotation,
                "Your monke headbangs"
            );
            Pose(
                "Helicopter",
                r => r.transform.rotation = Quaternion.Euler(0, T * 540f % 360f, 0) * r.transform.rotation,
                "Your monke spins like a helicopter"
            );
            Pose(
                "Backwards Rig",
                r => r.transform.rotation = Quaternion.Euler(0, 180f, 0) * r.transform.rotation,
                "Your monke's body faces backwards"
            );
            Pose(
                "Bow",
                r =>
                {
                    r.head.rigTarget.localRotation = Quaternion.Euler(45f, 0, 0) * r.head.rigTarget.localRotation;
                    Hands(r, At(r, -0.4f, -0.5f, 0.6f), At(r, 0.4f, -0.5f, 0.6f));
                },
                "Your monke bows its head"
            );
            Pose("Sit", r => r.transform.position -= Vector3.up * 0.35f * r.scaleFactor, "Your monke sits lower");
            Pose(
                "Float Rig",
                r => r.transform.position += Vector3.up * (0.6f + Mathf.Sin(T * 2f) * 0.1f) * r.scaleFactor,
                "Your monke floats above where you are"
            );
            Pose(
                "Upside Down Head",
                r => r.head.rigTarget.localRotation = Quaternion.Euler(0, 0, 180f) * r.head.rigTarget.localRotation,
                "Your monke's head is upside down"
            );
            Pose(
                "Look At Nearest",
                r =>
                {
                    var n = Game.Nearest(r.transform.position);
                    if (n == null)
                        return;
                    r.head.rigTarget.rotation = Quaternion.LookRotation(n.head.rigTarget.position - r.head.rigTarget.position);
                },
                "Your monke always stares at the closest player"
            );
            Pose(
                "Rig Orbit",
                r => r.transform.position += new Vector3(Mathf.Cos(T * 2f), 0, Mathf.Sin(T * 2f)) * 1.2f * r.scaleFactor,
                "Your monke circles around where you really are"
            );
            Pose(
                "Zombie",
                r =>
                {
                    Hands(r, At(r, -0.5f, 1f, 2.4f), At(r, 0.5f, 1f, 2.4f));
                    r.head.rigTarget.localRotation = Quaternion.Euler(20f, Mathf.Sin(T * 2f) * 15f, 10f) * r.head.rigTarget.localRotation;
                },
                "Arms out in front like a zombie"
            );
            Pose(
                "Shrug",
                r =>
                {
                    float s = Mathf.Abs(Mathf.Sin(T * 3f)) * 0.4f;
                    Hands(r, At(r, -1.5f, 0.6f + s, 0.4f), At(r, 1.5f, 0.6f + s, 0.4f));
                },
                "Your monke shrugs"
            );
        }
    }
}
