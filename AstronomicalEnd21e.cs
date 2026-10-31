using System;
using System.Collections.Generic;
using GorillaLocomotion;
using UnityEngine;

namespace AstronomicalEvent
{
    // THE END: your weapon. Pull a trigger (either hand; Space or the left mouse button on the desktop) and a bolt leaves that hand along the way it points. It hits the caretakers on the island (the waves of part 1 and the
    // ones the Seven fight at the hatch): a hit throws sparks and a flash, shoves the monster back (it walks on afterwards) and makes it stagger; three hits kill a small caretaker on the spot (ten a big one) and it never
    // reaches the force field. The rest of the show stays a function of the music time, only the monsters you shoot leave their script.
    internal sealed partial class AstronomicalEnd21
    {
        readonly List<LineRenderer> boltPool = new List<LineRenderer>(); readonly List<float> boltAge = new List<float>(); Material boltMat; float nextFireL, nextFireR, nextFireK; const float BOLT_LIFE = 0.14f;

        void BoltsFrame(float dt)
        {
            for (int i = 0; i < boltPool.Count; i++)
            {
                var l = boltPool[i]; if (l == null || !l.enabled) continue; boltAge[i] += dt; float k = boltAge[i] / BOLT_LIFE; if (k >= 1f) { l.enabled = false; continue; } l.widthMultiplier = 0.12f * (1f - k);
            }
        }
        void AddBolt(Vector3 a, Vector3 b)
        {
            if (boltMat == null) boltMat = kit.Glow(new Color(0.5f, 0.95f, 1f, 1f), lineTex, 3002);
            int idx = -1; for (int i = 0; i < boltPool.Count; i++) if (boltPool[i] != null && !boltPool[i].enabled) { idx = i; break; }
            if (idx < 0) { if (boltPool.Count >= 16) idx = 0; else { boltPool.Add(MakeLine("EPlayerBolt" + boltPool.Count, boltMat, 2, 0.1f)); boltAge.Add(0f); idx = boltPool.Count - 1; } }
            var l = boltPool[idx]; l.enabled = true; l.SetPosition(0, a); l.SetPosition(1, b); boltAge[idx] = 0f; l.widthMultiplier = 0.12f;
        }
        static bool RaySphere(Vector3 o, Vector3 d, Vector3 c, float r, out float s)
        {
            s = 0f; Vector3 oc = c - o; float tca = Vector3.Dot(oc, d); if (tca < 0f) return false; float d2 = oc.sqrMagnitude - tca * tca; if (d2 > r * r) return false; s = Mathf.Max(0f, tca - Mathf.Sqrt(r * r - d2)); return true;
        }

        public void Shoot(Vector3 o, Vector3 d)
        {
            if (!built || root == null || paused || d.sqrMagnitude < 1e-4f) return; d.Normalize(); float t = lastT; Sc sc = SceneAt(t);
            bool island = sc == Sc.Island && t > 1f && t < T_CUBE_DIE; if (!island && sc != Sc.Surface) return;
            float best = 260f, s; int bi = -1; Act ba = null;
            if (island)
            {
                for (int i = 0; i < mons.Count; i++)
                {
                    var m = mons[i]; if (m.Go == null || !m.Go.activeSelf || m.Killed >= 0f) continue; float h = 1.8f * m.Scale; Vector3 p = m.Go.transform.position;
                    if (RaySphere(o, d, p + Vector3.up * (h * 0.3f), h * 0.2f, out s) && s < best) { best = s; bi = i; }
                    if (RaySphere(o, d, p + Vector3.up * (h * 0.68f), h * 0.2f, out s) && s < best) { best = s; bi = i; }
                }
            }
            else
            {
                foreach (var a in acts)
                {
                    if (a == null || !a.Creature || a.Go == null || !a.Go.activeSelf || !a.Name.StartsWith("Care") || a.Cur == "Death") continue; float h = 1.8f * a.Scale; Vector3 p = a.Go.transform.position;
                    if (RaySphere(o, d, p + Vector3.up * (h * 0.3f), h * 0.2f, out s) && s < best) { best = s; ba = a; bi = -1; }
                    if (RaySphere(o, d, p + Vector3.up * (h * 0.68f), h * 0.2f, out s) && s < best) { best = s; ba = a; bi = -1; }
                }
            }
            bool hitSomething = bi >= 0 || ba != null; Vector3 hit = o + d * (hitSomething ? best : 90f); AddBolt(o + d * 0.1f, hit);
            for (int k = 0; k < 4; k++) Emit(fx, o + d * 0.15f, d * UnityEngine.Random.Range(2f, 6f) + UnityEngine.Random.insideUnitSphere * 0.6f, new Color(0.5f, 0.95f, 1f, 1f), UnityEngine.Random.Range(0.05f, 0.14f), 0.12f);
            if (!hitSomething) return;
            for (int k = 0; k < 18; k++) Emit(fx, hit, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(3f, 9f) - d * 2f, Color.Lerp(new Color(0.5f, 0.95f, 1f, 1f), Color.white, UnityEngine.Random.value), UnityEngine.Random.Range(0.12f, 0.5f), UnityEngine.Random.Range(0.25f, 0.7f));
            flashLamp.transform.position = hit; flashLamp.color = new Color(0.5f, 0.95f, 1f); flashLamp.intensity = Mathf.Max(flashLamp.intensity, Lit * 4f);
            Vector3 flat = new Vector3(d.x, 0f, d.z); Vector3 dl = flat.sqrMagnitude > 1e-4f ? root.transform.InverseTransformDirection(flat.normalized) : Vector3.zero;
            if (bi >= 0)
            {
                var mo = mons[bi]; mo.Hp -= 1f; mo.Hits++; mo.HitT = t; float kb = mo.Big ? 2.2f : 6.5f; mo.VX += dl.x * kb; mo.VZ += dl.z * kb;
                if (mo.Hp <= 0f) { mo.Killed = t; mo.KillP = mo.Go.transform.localPosition; mo.Boomed = true; Boom(mo.Go.transform.position + Vector3.up * (0.9f * mo.Scale), mo.Big ? 9f : 4.5f, mo.Big ? 0.9f : 0.45f, new Color(0.45f, 0.9f, 1f)); }
                mons[bi] = mo;
            }
            else if (ba != null) { ba.HitT = t; float kb = 6f; ba.VX += dl.x * kb; ba.VZ += dl.z * kb; }
        }

        // the triggers (and the desktop fallback) once per frame
        void WeaponFrame(float t, float dt, Sc sc)
        {
            BoltsFrame(dt); if (paused || dt <= 0f) return; if (!(sc == Sc.Island && t > 1f && t < T_CUBE_DIE) && sc != Sc.Surface) return;
            float now = Time.unscaledTime; var gt = GorillaTagger.Instance;
            try
            {
                var cp = ControllerInputPoller.instance;
                if (cp != null && gt != null)
                {
                    if (cp.leftControllerIndexFloat > 0.65f && now >= nextFireL && gt.leftHandTransform != null) { nextFireL = now + 0.17f; Shoot(gt.leftHandTransform.position + gt.leftHandTransform.forward * 0.12f, gt.leftHandTransform.forward); try { gt.StartVibration(true, 0.35f, 0.06f); } catch { } }
                    if (cp.rightControllerIndexFloat > 0.65f && now >= nextFireR && gt.rightHandTransform != null) { nextFireR = now + 0.17f; Shoot(gt.rightHandTransform.position + gt.rightHandTransform.forward * 0.12f, gt.rightHandTransform.forward); try { gt.StartVibration(false, 0.35f, 0.06f); } catch { } }
                }
            }
            catch { }
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current; var ms = UnityEngine.InputSystem.Mouse.current; bool k = (kb != null && kb.spaceKey.isPressed) || (ms != null && ms.leftButton.isPressed);
                if (k && now >= nextFireK) { var c = Camera.main; if (c != null) { nextFireK = now + 0.17f; Shoot(c.transform.position + c.transform.forward * 0.3f - c.transform.up * 0.15f, c.transform.forward); } }
            }
            catch { }
        }
    }
}
