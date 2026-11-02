using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 4: the cockpit view of the assault (gun + crosshair), the Zero Point he holds in his hands, the swarm of buses flying into his mouth
    internal sealed partial class AstronomicalGalactus
    {
        GameObject cockGo; Transform reticle; Transform zpBall, zpGlow, zpHalo; Material zpMat; readonly List<GameObject> swarm = new List<GameObject>(); Transform mouthHalo;
        Vector3 zpLocal; float zpR; bool zpOn;     // the Zero Point, in the root's frame (set by GxCup)
        const int SWARM = 96;

        // a cone, tip at -z, base at +z (the drones are missiles flying at you)
        Mesh ConeMesh(float len, float rad)
        {
            int n = 10; var v = new List<Vector3>(); var tri = new List<int>(); v.Add(new Vector3(0f, 0f, -len * 0.5f));
            for (int i = 0; i < n; i++) { float a = i * 6.2832f / n; v.Add(new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, len * 0.5f)); }
            v.Add(new Vector3(0f, 0f, len * 0.5f));
            for (int i = 0; i < n; i++) { int a = 1 + i, b = 1 + (i + 1) % n; tri.Add(0); tri.Add(b); tri.Add(a); tri.Add(n + 1); tri.Add(a); tri.Add(b); }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m); return m;
        }

        void BuildExtra()
        {
            // ---- cockpit: the bus' gun at the bottom of the view and the crosshair
            cockGo = new GameObject("GCockpit"); cockGo.transform.SetParent(root.transform, false); cockGo.SetActive(false);
            var red = kit.Lit(new Color(0.9f, 0.22f, 0.2f), null, 0.25f); var grey = kit.Lit(new Color(0.62f, 0.64f, 0.68f), null, 0.3f); var dark = kit.Lit(new Color(0.18f, 0.18f, 0.22f), null, 0.3f);
            Action<Vector3, Vector3, Material> bx = (p, s, m) => { var c = Prim(PrimitiveType.Cube, "GCk", m); c.SetParent(cockGo.transform, false); c.localPosition = p; c.localScale = s; };
            bx(new Vector3(0f, -0.13f, 0.02f), new Vector3(0.74f, 0.1f, 0.4f), red); bx(new Vector3(-0.2f, -0.04f, 0.06f), new Vector3(0.13f, 0.1f, 0.3f), grey); bx(new Vector3(0.2f, -0.04f, 0.06f), new Vector3(0.13f, 0.1f, 0.3f), grey); bx(new Vector3(0f, -0.07f, 0.1f), new Vector3(0.3f, 0.07f, 0.24f), dark);
            var tex = kit.Tex(64, 64, (x, y) => { float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy); if (r > 31f) return new Color(0, 0, 0, 0); if (r < 9f) return new Color(0.35f, 1f, 0.55f, 1f); if (r < 13f) return new Color(0.04f, 0.2f, 0.12f, 1f); if (r < 20f) return new Color(0.5f, 0.95f, 0.65f, 1f); if (r < 24f) return new Color(0.1f, 0.15f, 0.12f, 1f); if (r < 28.5f) return new Color(1f, 0.78f, 0.25f, 1f); return new Color(0.2f, 0.2f, 0.22f, 1f); }, TextureWrapMode.Clamp);
            var disc = Quad("GCkDisc", kit.Glow(Color.white, tex, 3002)); disc.SetParent(cockGo.transform, false); disc.localPosition = new Vector3(0f, 0.0f, -0.05f); disc.localRotation = Quaternion.Euler(70f, 0f, 0f); disc.localScale = Vector3.one * 0.32f;
            var rt = kit.Tex(128, 128, (x, y) => { float dx = x - 63.5f, dy = y - 63.5f, r = Mathf.Sqrt(dx * dx + dy * dy); bool ring = r > 40f && r < 44f && !(Mathf.Abs(dx) < 14f || Mathf.Abs(dy) < 14f); bool tick = r >= 44f && r < 58f && (Mathf.Abs(dx) < 2.2f || Mathf.Abs(dy) < 2.2f); bool dot = r < 3f; bool inner = r > 14f && r < 16.5f; bool tri = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 2.2f && r > 21f && r < 33f; return ring || tick || dot || inner || tri ? new Color(1f, 1f, 1f, 0.95f) : new Color(0, 0, 0, 0); }, TextureWrapMode.Clamp);
            reticle = Quad("GReticle", kit.Glow(Color.white, rt, 3003)); reticle.gameObject.SetActive(false);
            // ---- the Zero Point between his hands
            zpMat = kit.Glow(new Color(1f, 0.85f, 0.7f, 0.9f), null, 3001); zpBall = Prim(PrimitiveType.Sphere, "GZP", zpMat); zpBall.gameObject.SetActive(false);
            zpGlow = Quad("GZPGlow", kit.Glow(new Color(1f, 0.6f, 0.45f, 0.7f), glowTex, 3003)); zpGlow.gameObject.SetActive(false); zpHalo = Quad("GZPHalo", kit.Glow(new Color(0.6f, 0.7f, 1f, 0.5f), glowTex, 3002)); zpHalo.gameObject.SetActive(false);
            AstronomicalDebug.Anchors["zp"] = () => W(zpLocal);
            // ---- drones become dark red cones
            var cone = ConeMesh(7f, 1.8f); var dm = kit.Lit(new Color(0.34f, 0.05f, 0.1f), null, 0.4f);
            foreach (var d in drones) { var mf = d.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = cone; var r = d.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = dm; }
            // ---- the swarm
            swarm.Clear(); for (int i = 0; sbusfM != null && i < SWARM; i++) { var go = UnityEngine.Object.Instantiate(sbusfM.Pivot, root.transform); go.name = "GSwarm" + i; go.SetActive(false); clones.Add(go); swarm.Add(go); }
            mouthHalo = Quad("GMouthHalo", kit.Glow(new Color(1f, 0.55f, 0.85f, 0.6f), glowTex, 3000)); mouthHalo.gameObject.SetActive(false);
        }

        void ExtraCleanup() { swarm.Clear(); cockGo = null; reticle = zpBall = zpGlow = zpHalo = mouthHalo = null; zpOn = false; }

        // two-bone IK for the left arm: the hand goes to `target` (world), the elbow drops
        void CupArm(Transform sh, Transform el, Transform hand, Vector3 target, float w)
        {
            if (sh == null || el == null || hand == null || w <= 0.001f) return;
            Quaternion shT = sh.localRotation, elT = el.localRotation;
            float L1 = (el.position - sh.position).magnitude, L2 = (hand.position - el.position).magnitude; Vector3 S = sh.position; Vector3 dv = target - S; float d = Mathf.Clamp(dv.magnitude, Mathf.Abs(L1 - L2) + 0.5f, L1 + L2 - 0.5f); Vector3 u0 = dv.normalized;
            float alpha = Mathf.Acos(Mathf.Clamp((L1 * L1 + d * d - L2 * L2) / (2f * L1 * d), -1f, 1f)) * Mathf.Rad2Deg; float bend = 180f - Mathf.Acos(Mathf.Clamp((L1 * L1 + L2 * L2 - d * d) / (2f * L1 * L2), -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 n = Vector3.Cross(u0, Vector3.down); if (n.sqrMagnitude < 1e-4f) n = Vector3.right; n.Normalize();
            Vector3 dA = Quaternion.AngleAxis(alpha, n) * u0, dB = Quaternion.AngleAxis(-alpha, n) * u0; Vector3 dir = (S + dA * L1).y < (S + dB * L1).y ? dA : dB;
            AimArm(sh, el, hand, dir, 1f);
            Vector3 E = el.position; el.rotation = Quaternion.FromToRotation((hand.position - E).normalized, (target - E).normalized) * el.rotation;   // forearm straight at the target
            Quaternion shIK = sh.localRotation, elIK = el.localRotation; sh.localRotation = Quaternion.Slerp(shT, shIK, w); el.localRotation = Quaternion.Slerp(elT, elIK, w);
        }

        // called from GxFrame after the pose: 5:35 - 7:00 and again at the end he holds the Zero Point in front of his chest and cups it with his left hand
        void GxCup(float t)
        {
            float w = Sm(336f, 346f, t) * (1f - Sm(410f, 418f, t)); zpOn = w > 0.01f && gxChestP != null && gxHHand != null;
            if (!zpOn) return;
            Vector3 front = galM.Pivot.transform.TransformDirection(Vector3.forward); front.y = 0f; front = front.sqrMagnitude < 1e-4f ? root.transform.TransformDirection(Vector3.back) : front.normalized;
            float s = gxS; Vector3 center = gxChestP.position + front * (2.3f * s) + Vector3.up * (0.2f * s); zpR = 3.2f * s * w;
            zpLocal = root.transform.InverseTransformPoint(center);
            // the hand rests on the side of the ball
            Vector3 side = (gxHSh.position - center); side.y = 0f; side = side.sqrMagnitude < 1e-4f ? Vector3.right : side.normalized;
            CupArm(gxHSh, gxHEl, gxHHand, center + side * (zpR * 0.95f) + Vector3.down * (zpR * 0.25f), w);
        }

        void ExtraFrame(float t, float tp, float dt, Camera cam)
        {
            // ---- cockpit
            bool ck = cam != null && t >= 330f && t < 420f;
            if (cockGo != null && cockGo.activeSelf != ck) cockGo.SetActive(ck); if (reticle != null && reticle.gameObject.activeSelf != ck) reticle.gameObject.SetActive(ck);
            if (ck)
            {
                var ct = cam.transform; cockGo.transform.position = ct.position + ct.forward * 0.95f + ct.up * -0.5f; cockGo.transform.rotation = ct.rotation; cockGo.transform.localScale = Vector3.one * 1.5f;
                reticle.position = ct.position + ct.forward * 2.6f; reticle.rotation = ct.rotation * Quaternion.Euler(0f, 0f, 8f * t); reticle.localScale = Vector3.one * 0.85f;
                var rm = reticle.GetComponent<Renderer>().sharedMaterial; bool lockOn = (int)(t * 2f) % 4 == 0; rm.color = lockOn ? new Color(1f, 0.35f, 0.3f, 0.95f) : new Color(0.9f, 0.97f, 1f, 0.9f);
            }
            // ---- the Zero Point
            bool zp = zpOn && zpBall != null; if (zpBall != null && zpBall.gameObject.activeSelf != zp) { zpBall.gameObject.SetActive(zp); zpGlow.gameObject.SetActive(zp); zpHalo.gameObject.SetActive(zp); }
            if (zp)
            {
                float pul = 1f + 0.05f * Mathf.Sin(t * 9f); Vector3 wp = W(zpLocal); zpBall.localPosition = zpLocal; zpBall.localScale = Vector3.one * (zpR * 2f * pul); zpMat.color = new Color(1f, 0.82f + 0.1f * Mathf.Sin(t * 5f), 0.65f, 0.92f);
                Glow(zpGlow, wp, zpR * 5f, new Color(1f, 0.6f, 0.45f, 0.55f), cam, true); Glow(zpHalo, wp, zpR * 9f, new Color(0.55f, 0.7f, 1f, 0.35f + 0.1f * Mathf.Sin(t * 3f)), cam, true);
                if (!paused && dt > 0f) for (int k = 0; k < 6; k++) { Vector3 o = UnityEngine.Random.onUnitSphere; Vector3 tang = Vector3.Cross(o, Vector3.up).normalized; Emit(fx, wp + o * zpR, tang * zpR * 0.8f + o * zpR * 0.4f, Color.Lerp(new Color(1f, 0.9f, 0.7f, 0.95f), new Color(0.6f, 0.8f, 1f, 0.95f), UnityEngine.Random.value), zpR * 0.12f, 0.8f); }
            }
            // ---- the swarm flying into his mouth (the recording: hundreds of buses inside his mouth)
            bool sw = t >= 535f && t < 577f && gxMouthP != null && galM != null && galM.Pivot.activeSelf; Vector3 M = sw ? root.transform.InverseTransformPoint(gxMouthP.position) : Vector3.zero;
            for (int i = 0; i < swarm.Count; i++)
            {
                var go = swarm[i]; if (!sw) { if (go.activeSelf) go.SetActive(false); continue; }
                float life = 20f, ph = Hash(i, 101) * life, uu = Mathf.Repeat((t - 535f + ph) / life, 1f); bool live = (t - 535f) + ph >= 0f && uu < 0.94f;
                float a = Hash(i, 102) * 6.2832f, R = 25f + 95f * Hash(i, 103); float rr = R * Mathf.Pow(1f - uu, 1.4f);
                Vector3 p = M + new Vector3(Mathf.Cos(a + 2.5f * uu) * rr, Mathf.Sin(a + 2.5f * uu) * rr * 0.8f, -330f * (1f - uu));
                PlaceGo(go, p, busS * 0.55f, Quaternion.LookRotation(Vector3.forward + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.15f * (1f - uu), Vector3.up) * Quaternion.Euler(0f, 0f, 20f * Mathf.Sin(t + i)), live);
                if (live && !paused && uu > 0.9f && dt > 0f && UnityEngine.Random.value < 0.5f) Emit(fx, W(p), UnityEngine.Random.onUnitSphere * 14f, new Color(1f, 0.85f, 0.95f, 0.95f), 4f, 0.5f);
            }
            if (mouthHalo != null) { bool mh = sw; if (mouthHalo.gameObject.activeSelf != mh) mouthHalo.gameObject.SetActive(mh); if (mh) Glow(mouthHalo, W(M), 150f * (0.7f + 0.3f * Sm(535f, 570f, t)), new Color(1f, 0.55f, 0.85f, 0.55f), cam, true); }
        }
    }
}
