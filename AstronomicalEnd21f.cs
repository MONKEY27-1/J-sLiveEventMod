using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // THE END: the burning sky. A band of fiery clouds (several hundred soft billboard puffs, dark smoky underside / glowing orange crests, sorted back to front and rebuilt as one mesh each frame) runs round the
    // rift ring, and forked lightning (a glowing core + a wider orange halo, with side branches, some striking down, some crawling along the band) flickers in it. Part of the Queen's rift from 1:35 to the flash at 10:03.
    internal sealed partial class AstronomicalEnd21
    {
        sealed class Puff { public float A, Rad, H, Size, Seed, Heat; }
        sealed class Bolt { public LineRenderer[] L = new LineRenderer[4]; public float T0, Life; public bool On; }
        readonly List<Puff> puffs = new List<Puff>(); Mesh cloudMesh; GameObject cloudGo; Material cloudMat; Texture2D cloudTex; Bolt[] bolts; float nextStrike; Vector3[] cloudV; Color[] cloudC; Vector2[] cloudUV; int[] cloudT; readonly List<int> puffOrder = new List<int>(); float[] puffD;

        void SkyFxBuild()
        {
            puffs.Clear(); puffOrder.Clear();      // (the event can be started again in the same game session: nothing from the last run may be left in these lists)
            cloudTex = kit.Tex(64, 64, (x, y) =>
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy); float n = 0.62f + 0.38f * Mathf.PerlinNoise(x * 0.17f + 3f, y * 0.17f + 9f); float a = Mathf.Clamp01((1f - r) * 1.6f) * n; a = a * a * (3f - 2f * a); return new Color(1f, 1f, 1f, a);
            }, TextureWrapMode.Clamp);
            cloudMat = new Material(kit.Glow(Color.white, cloudTex, 2900)); owned.Add(cloudMat);
            System.Random rnd = new System.Random(11); Func<float> R01 = () => (float)rnd.NextDouble();
            for (int i = 0; i < 460; i++) puffs.Add(new Puff { A = R01() * Mathf.PI * 2f, Rad = 0.8f + 0.42f * R01() * R01() + 0.1f * (R01() - 0.5f), H = (R01() - 0.5f) * 2f, Size = Mathf.Lerp(0.1f, 0.3f, R01() * R01()), Seed = R01() * 50f, Heat = R01() });
            int n2 = puffs.Count; cloudV = new Vector3[n2 * 4]; cloudC = new Color[n2 * 4]; cloudUV = new Vector2[n2 * 4]; cloudT = new int[n2 * 6]; puffPos = new Vector3[n2]; puffD = new float[n2];
            for (int i = 0; i < n2; i++) { cloudUV[i * 4] = new Vector2(0, 0); cloudUV[i * 4 + 1] = new Vector2(1, 0); cloudUV[i * 4 + 2] = new Vector2(1, 1); cloudUV[i * 4 + 3] = new Vector2(0, 1); puffOrder.Add(i); }
            cloudMesh = new Mesh(); cloudMesh.MarkDynamic(); cloudMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20000f); owned.Add(cloudMesh);
            cloudGo = new GameObject("ECloudBand"); cloudGo.transform.SetParent(root.transform, false); cloudGo.AddComponent<MeshFilter>().sharedMesh = cloudMesh; var mr = cloudGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = cloudMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.allowOcclusionWhenDynamic = false; cloudGo.SetActive(false);
            var core = kit.Glow(new Color(1f, 0.97f, 0.85f, 1f), lineTex, 3006); var halo = kit.Glow(new Color(1f, 0.5f, 0.12f, 1f), lineTex, 3005); bolts = new Bolt[10];
            for (int i = 0; i < bolts.Length; i++)
            {
                var b = new Bolt(); for (int k = 0; k < 4; k++) { var l = MakeLine("EStrike" + i + "_" + k, k == 1 ? halo : core, 2, 1f); l.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)); b.L[k] = l; }
                bolts[i] = b;
            }
        }

        // the band: basis u (ring x), v (ring y, squashed to 0.5), tilt (the ring's up); cp = centre, R = radius, k = 0..1 how far the rift has opened, oc = in the ocean scene (everything much bigger)
        void SkyFxFrame(float t, float dt, bool on, bool oc, Vector3 cp, Vector3 u, Vector3 v, Vector3 tilt, float R, float k)
        {
            if (cloudGo == null) return; if (cloudGo.activeSelf != on) cloudGo.SetActive(on);
            if (!on) { foreach (var b in bolts) if (b.On) KillBolt(b); return; }
            var cam = Camera.main; Vector3 cr = cam != null ? cam.transform.right : Vector3.right, cu = cam != null ? cam.transform.up : Vector3.up, cpos = cam != null ? cam.transform.position : cp;
            int n = puffs.Count; float spin = t * 0.012f;
            for (int i = 0; i < n; i++)
            {
                var p = puffs[i]; float a = p.A + spin, rr = p.Rad; Vector3 c = cp + u * Mathf.Cos(a) * R * rr + v * Mathf.Sin(a) * R * 0.5f * rr + tilt * (p.H * R * 0.07f + 18f * Mathf.Sin(t * 0.25f + p.Seed)); puffPos[i] = c; puffD[i] = (c - cpos).sqrMagnitude;
            }
            puffOrder.Sort((x, y) => puffD[y].CompareTo(puffD[x]));
            for (int q = 0; q < n; q++)
            {
                int i = puffOrder[q]; var p = puffs[i]; float sz = p.Size * R * k * (1f + 0.12f * Mathf.Sin(t * 0.7f + p.Seed)); Vector3 c = puffPos[i]; float flick = 0.5f + 0.5f * Mathf.Sin(t * (0.6f + p.Heat) + p.Seed * 3f);
                // dark smoke at the low / inner edge, white-hot orange on the crest: heat from the puff's height in the band and a slow pulse
                float heat = Mathf.Clamp01(0.12f + 0.5f * (p.H * 0.5f + 0.5f) + 0.28f * flick * p.Heat); Color col = Color.Lerp(new Color(0.11f, 0.04f, 0.06f, 0.9f), new Color(1f, 0.42f, 0.08f, 0.92f), heat); col = Color.Lerp(col, new Color(1f, 0.85f, 0.45f, 0.95f), Mathf.Clamp01((heat - 0.82f) * 3f)); col.a *= Mathf.Clamp01(k * 1.5f);
                Vector3 r = cr * sz, up = cu * sz * 0.62f; int b = q * 4; cloudV[b] = c - r - up; cloudV[b + 1] = c + r - up; cloudV[b + 2] = c + r + up; cloudV[b + 3] = c - r + up; cloudC[b] = cloudC[b + 1] = cloudC[b + 2] = cloudC[b + 3] = col;
                int ti = q * 6; cloudT[ti] = b; cloudT[ti + 1] = b + 2; cloudT[ti + 2] = b + 1; cloudT[ti + 3] = b; cloudT[ti + 4] = b + 3; cloudT[ti + 5] = b + 2;
            }
            cloudGo.transform.position = Vector3.zero; cloudGo.transform.rotation = Quaternion.identity; cloudGo.transform.localScale = Vector3.one; var m = cloudMesh; m.Clear(); m.vertices = cloudV; m.uv = cloudUV; m.colors = cloudC; m.triangles = cloudT; m.bounds = new Bounds(cp, Vector3.one * R * 6f);
            // ---- lightning
            foreach (var b in bolts)
            {
                if (!b.On) continue; float age = t - b.T0; if (age < 0f || age > b.Life) { KillBolt(b); continue; }
                float a = age < 0.06f ? 1f : Mathf.Clamp01(0.55f + 0.45f * Mathf.Sin(age * 80f)) * (1f - age / b.Life); foreach (var l in b.L) { l.startColor = new Color(1f, 1f, 1f, a); l.endColor = new Color(1f, 1f, 1f, a * 0.7f); }
            }
            if (paused || dt <= 0f) return; float rate = oc ? 3.0f : 1.6f; rate *= Mathf.Clamp01(k); nextStrike -= dt; if (nextStrike <= 0f) { nextStrike = UnityEngine.Random.Range(0.1f, 2f / Mathf.Max(0.3f, rate)); Strike(t, cp, u, v, tilt, R, oc); }
        }
        Vector3[] puffPos = new Vector3[0];

        void KillBolt(Bolt b) { b.On = false; foreach (var l in b.L) l.enabled = false; }
        void Strike(float t, Vector3 cp, Vector3 u, Vector3 v, Vector3 tilt, float R, bool oc)
        {
            Bolt b = null; foreach (var x in bolts) if (!x.On) { b = x; break; } if (b == null) return;
            float a0 = UnityEngine.Random.value * Mathf.PI * 2f, r0 = UnityEngine.Random.Range(0.85f, 1.1f); Vector3 s = cp + u * Mathf.Cos(a0) * R * r0 + v * Mathf.Sin(a0) * R * 0.5f * r0, e;
            float seaY = root.transform.position.y + (oc ? WY : 0f);
            if (UnityEngine.Random.value < 0.6f) { float drop = oc ? UnityEngine.Random.Range(600f, 1500f) : UnityEngine.Random.Range(300f, 800f); e = s + new Vector3(UnityEngine.Random.Range(-1f, 1f) * R * 0.3f, 0f, UnityEngine.Random.Range(-1f, 1f) * R * 0.3f); e.y = Mathf.Max(seaY, s.y - drop); }
            else { float a1 = a0 + (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(0.25f, 0.8f); float r1 = r0 + UnityEngine.Random.Range(-0.1f, 0.1f); e = cp + u * Mathf.Cos(a1) * R * r1 + v * Mathf.Sin(a1) * R * 0.5f * r1 + Vector3.down * UnityEngine.Random.Range(0f, R * 0.06f); }
            int np = 16; var pts = new Vector3[np]; float len = (e - s).magnitude; Vector3 dir = (e - s) / Mathf.Max(1f, len); Vector3 pa = Vector3.Cross(dir, Vector3.up); if (pa.sqrMagnitude < 0.01f) pa = Vector3.right; pa.Normalize(); Vector3 pb = Vector3.Cross(dir, pa);
            float jit = len * 0.045f; float ja = 0f, jb = 0f;
            for (int i = 0; i < np; i++) { float f = i / (float)(np - 1); ja = Mathf.Lerp(ja, UnityEngine.Random.Range(-1f, 1f), 0.6f); jb = Mathf.Lerp(jb, UnityEngine.Random.Range(-1f, 1f), 0.6f); float env = Mathf.Sin(f * Mathf.PI); pts[i] = Vector3.Lerp(s, e, f) + (pa * ja + pb * jb) * jit * env; }
            float w = (oc ? 9f : 2.2f) * UnityEngine.Random.Range(0.75f, 1.35f); SetBoltLine(b.L[0], pts, w); SetBoltLine(b.L[1], pts, w * 3.2f);
            int bi = UnityEngine.Random.Range(4, 10); var bp = new Vector3[7]; Vector3 bd = (dir + (pa * UnityEngine.Random.Range(-1f, 1f) + pb * UnityEngine.Random.Range(-0.5f, 0.5f)) * 0.9f).normalized; for (int i = 0; i < 7; i++) bp[i] = pts[bi] + bd * (len * 0.035f * i) + (pa * UnityEngine.Random.Range(-1f, 1f)) * jit * 0.5f * (i > 0 ? 1f : 0f);
            SetBoltLine(b.L[2], bp, w * 0.6f); int bi2 = UnityEngine.Random.Range(8, 13); var bp2 = new Vector3[5]; Vector3 bd2 = (dir + (pa * UnityEngine.Random.Range(-1f, 1f)) * 0.8f).normalized; for (int i = 0; i < 5; i++) bp2[i] = pts[bi2] + bd2 * (len * 0.03f * i) + (pb * UnityEngine.Random.Range(-1f, 1f)) * jit * 0.4f * (i > 0 ? 1f : 0f);
            SetBoltLine(b.L[3], bp2, w * 0.5f); b.T0 = t; b.Life = UnityEngine.Random.Range(0.18f, 0.5f); b.On = true; Sfx(sThunder, e, oc ? 0.7f : 0.45f, UnityEngine.Random.Range(0.8f, 1.15f), 0.4f);
            if (e.y <= seaY + 2f && UnityEngine.Random.value < 0.5f) { for (int i = 0; i < 14; i++) Emit(fx, e, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(0.5f, 1.4f), UnityEngine.Random.Range(-1f, 1f)) * UnityEngine.Random.Range(4f, 14f), new Color(1f, 0.85f, 0.5f, 1f), oc ? UnityEngine.Random.Range(1.5f, 4f) : UnityEngine.Random.Range(0.4f, 1.2f), UnityEngine.Random.Range(0.4f, 1f)); }
        }
        static void SetBoltLine(LineRenderer l, Vector3[] pts, float w) { l.positionCount = pts.Length; l.SetPositions(pts); l.widthMultiplier = w; l.numCornerVertices = 2; l.enabled = true; }
    }
}
