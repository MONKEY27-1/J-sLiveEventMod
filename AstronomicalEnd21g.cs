using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // THE END: the sea. A sum of six travelling swells (deep-water dispersion w = sqrt(g k), amplitude fading with distance so the small waves do not alias) plus expanding ring waves from impacts (the Queen
    // hitting the water, the island flipping and sinking) is evaluated on a polar grid that follows you but samples WORLD-fixed coordinates, so the surface stays where it is while you float on it (your head
    // rides the wave height). Vertex colours do the shading: Fresnel mix of deep water and the sky, a Blinn glitter lobe towards the Queen's beam (or the sun after the flash), slope shading, and foam on the crests.
    internal sealed partial class AstronomicalEnd21
    {
        static readonly float[] SW_DIR = { 0.35f, 0.95f, -0.55f, 1.65f, 2.45f, -1.35f }, SW_LEN = { 170f, 95f, 58f, 30f, 15f, 7.5f }, SW_AMP = { 0.62f, 0.4f, 0.24f, 0.13f, 0.065f, 0.032f };
        struct Ripple { public float X, Z, T0, A, C, K, W, Life; }
        readonly List<Ripple> ripples = new List<Ripple>();
        Mesh seaMesh; Vector3[] seaV; Color[] seaC; int seaNR = 58, seaNA = 100; float[] seaRad, seaCos, seaSin; float seaState = 1f;

        void AddRipple(float x, float z, float t0, float a, float c, float wavelen, float width, float life) { ripples.Add(new Ripple { X = x, Z = z, T0 = t0, A = a, C = c, K = 2f * Mathf.PI / wavelen, W = width, Life = life }); }

        // height (and gradient) of the sea at root-local (x, z); lod = distance from the viewer (fades short waves)
        float SeaSample(float x, float z, float t, float lod, out float gx, out float gz)
        {
            float h = 0f; gx = 0f; gz = 0f;
            for (int i = 0; i < 6; i++)
            {
                float fade = Mathf.Clamp01(1.2f - lod / (SW_LEN[i] * 22f)); if (fade <= 0f) continue; float k = 2f * Mathf.PI / SW_LEN[i], w = Mathf.Sqrt(9.81f * k), dx = Mathf.Cos(SW_DIR[i]), dz = Mathf.Sin(SW_DIR[i]);
                float ph = k * (x * dx + z * dz) - w * t + i * 1.7f; float a = SW_AMP[i] * seaState * fade; float s = Mathf.Sin(ph);
                // crests sharper than troughs (a cheap Gerstner profile): h = a * (s + 0.25 * s * |s|)
                h += a * (s + 0.25f * s * Mathf.Abs(s)); float dh = a * k * Mathf.Cos(ph) * (1f + 0.5f * Mathf.Abs(s)); gx += dh * dx; gz += dh * dz;
            }
            for (int i = 0; i < ripples.Count; i++)
            {
                var r = ripples[i]; float age = t - r.T0; if (age < 0f || age > r.Life) continue; float ddx = x - r.X, ddz = z - r.Z, d = Mathf.Sqrt(ddx * ddx + ddz * ddz) + 0.001f; float front = d - r.C * age; float env = Mathf.Exp(-(front * front) / (r.W * r.W)) * Mathf.Exp(-d / (r.W * 40f)) * (1f - age / r.Life);
                float ph = r.K * front; float s = Mathf.Sin(ph); h += r.A * env * s; float dh = r.A * env * (r.K * Mathf.Cos(ph) - 2f * front / (r.W * r.W) * s); gx += dh * ddx / d; gz += dh * ddz / d;
            }
            return h;
        }

        void SeaBuild()
        {
            seaRad = new float[seaNR]; for (int i = 0; i < seaNR; i++) seaRad[i] = 2.5f * Mathf.Pow(2700f / 2.5f, i / (float)(seaNR - 1));
            seaCos = new float[seaNA + 1]; seaSin = new float[seaNA + 1]; for (int j = 0; j <= seaNA; j++) { float a = j * 2f * Mathf.PI / seaNA; seaCos[j] = Mathf.Cos(a); seaSin[j] = Mathf.Sin(a); }
            int nv = seaNR * seaNA; seaV = new Vector3[nv]; seaC = new Color[nv]; var tris = new List<int>();
            for (int i = 0; i < seaNR - 1; i++) for (int j = 0; j < seaNA; j++) { int a = i * seaNA + j, b = i * seaNA + (j + 1) % seaNA, c = (i + 1) * seaNA + j, d = (i + 1) * seaNA + (j + 1) % seaNA; tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
            seaMesh = new Mesh(); seaMesh.MarkDynamic(); seaMesh.vertices = seaV; seaMesh.colors = seaC; seaMesh.triangles = tris.ToArray(); seaMesh.bounds = new Bounds(Vector3.zero, new Vector3(9000f, 400f, 9000f)); owned.Add(seaMesh);
            var mf = oceanGo.GetComponent<MeshFilter>(); mf.sharedMesh = seaMesh; var mt = new Material(kit.Glow(Color.white, null, 2450)); owned.Add(mt); oceanGo.GetComponent<MeshRenderer>().sharedMaterial = mt;
        }

        // the sky colour seen at an elevation (0 = horizon): the same gradient the sky dome uses
        static Color SkyAt(float e, bool day, float tw)
        {
            Color hor = day ? new Color(0.86f, 0.94f, 1f) : Color.Lerp(new Color(0.72f, 0.3f, 0.22f), new Color(0.62f, 0.3f, 0.3f), tw), mid = day ? new Color(0.5f, 0.75f, 1f) : new Color(0.4f, 0.16f, 0.3f), top = day ? new Color(0.2f, 0.45f, 0.95f) : new Color(0.12f, 0.07f, 0.24f);
            return e < 0.3f ? Color.Lerp(hor, mid, e / 0.3f) : Color.Lerp(mid, top, (e - 0.3f) / 0.7f);
        }

        // the sky gradient with the day / dusk blend: the sky dome and the sea's reflection use this ONE function so the far sea fades into the horizon with no seam
        static Color SkyAtB(float e, float blue, float tw) { return Color.Lerp(SkyAt(e, false, tw), SkyAt(e, true, tw), blue); }
        // rebuild the sea for this frame; cam = the viewer (world), returns nothing: the mesh is parented to the root at the viewer's root-local x, z
        void SeaFrame(float t, Vector3 camWorld)
        {
            if (seaMesh == null) return; Vector3 cl = root.transform.InverseTransformPoint(camWorld); bool day = t >= T_END_VIS + 0.4f; float flashK = Sm(T_END_VIS - 0.3f, T_END_VIS + 1.2f, t);
            seaState = 1f + 0.8f * Sm(T_END_VIS, T_END_VIS + 8f, t) + 0.5f * Sm(T_OCEAN, T_OCEAN + 20f, t); float camH = Mathf.Max(0.4f, cl.y - WY);
            oceanGo.transform.localPosition = new Vector3(cl.x, WY, cl.z); oceanGo.transform.localRotation = Quaternion.identity;
            // the glitter lobe points at the Queen's tower (or, after the flash, at a low sun ahead)
            Vector3 toL = day ? new Vector3(0.25f, 0f, 1f) : new Vector3(OC_PX - cl.x, 0f, OC_PZ - cl.z); toL.Normalize(); Vector3 lightDir = new Vector3(toL.x, day ? 0.42f : 0.07f, toL.z).normalized; Color lightCol = day ? new Color(1f, 0.97f, 0.88f) : new Color(1f, 0.62f, 0.25f);
            Color deepDusk = new Color(0.1f, 0.06f, 0.2f), deepDay = new Color(0.02f, 0.2f, 0.46f), deep = Color.Lerp(deepDusk, deepDay, flashK); float tw = Sm(T_OCEAN, T_OCEAN + 40f, t);
            for (int i = 0; i < seaNR; i++)
            {
                float r = seaRad[i];
                for (int j = 0; j < seaNA; j++)
                {
                    float dx = seaCos[j] * r, dz = seaSin[j] * r, wx = cl.x + dx, wz = cl.z + dz, gx, gz; float h = SeaSample(wx, wz, t, r, out gx, out gz);
                    int idx = i * seaNA + j; seaV[idx] = new Vector3(dx, h, dz);
                    Vector3 n = new Vector3(-gx, 1f, -gz).normalized; Vector3 v = new Vector3(-dx, camH - h, -dz); float dist = v.magnitude; v /= dist;
                    float cosT = Mathf.Clamp01(Vector3.Dot(n, v)); float F = 0.04f + 0.96f * Mathf.Pow(1f - cosT, 5f); F = Mathf.Clamp(F * 0.85f + 0.03f, 0f, day ? 0.7f : 0.5f);
                    float elev = Mathf.Clamp01((camH - h) / dist * 2.2f); Color refl = SkyAtB(elev, flashK, tw); float far = Sm(900f, 2300f, r); Color body = deep * (0.75f + 0.5f * Vector3.Dot(n, lightDir) * 0.8f + 0.2f);
                    // deeper blue-green in the crests' lee (cheap subsurface): lighten where the surface faces the light
                    body = Color.Lerp(body, body + new Color(0.02f, 0.1f, 0.12f), Mathf.Clamp01(h * 0.6f + 0.3f));
                    Color col = Color.Lerp(Color.Lerp(body, refl, F), SkyAtB(0f, flashK, tw), far); Vector3 hv = (lightDir + v).normalized; float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(n, hv)), day ? 240f : 140f); col += lightCol * spec * (day ? 3f : 2.4f);
                    float amp = 0f; for (int q = 0; q < 6; q++) amp += SW_AMP[q]; amp *= seaState; float foam = Mathf.Clamp01((h - 0.42f * amp) / (0.5f * amp)) * Mathf.Clamp01(Mathf.Sqrt(gx * gx + gz * gz) * 2.2f - 0.25f) * Mathf.Clamp01(1.4f - r / 900f);
                    col = Color.Lerp(col, day ? Color.white : new Color(0.95f, 0.8f, 0.78f), foam * 0.85f); col.a = 1f; seaC[idx] = col;
                }
            }
            seaMesh.vertices = seaV; seaMesh.colors = seaC; seaMesh.bounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(9000f, 600f, 9000f));
            for (int i = ripples.Count - 1; i >= 0; i--) if (t - ripples[i].T0 > ripples[i].Life + 1f) ripples.RemoveAt(i);
        }
    }
}
