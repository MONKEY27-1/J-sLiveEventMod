using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 6: Thor's props - a red cape (a cloth strip hung from his shoulders and swayed by his own motion) and Mjolnir in his right hand.  Both are built from code and follow bones found by name.
    internal sealed partial class AstronomicalGalactus
    {
        Transform thSh, thShR, thHip, thHand, thToe, thHead; GameObject capeGo, hammerGo; Mesh capeMesh; Vector3[] capeV; Vector3 capeSway, capeSwayV, thPrev; bool thorPropsBuilt, thorPropsFail;
        const int CAPE_R = 8, CAPE_C = 5;
        static Transform BoneStarts(Transform root, string prefix) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith(prefix)) return t; return null; }
        void BuildThorProps(Act a)
        {
            thorPropsBuilt = true; var r = a.Go.transform; thSh = BoneStarts(r, "mixamorig:LeftShoulder"); thShR = BoneStarts(r, "mixamorig:RightShoulder"); thHip = BoneStarts(r, "mixamorig:Hips"); thHand = BoneStarts(r, "mixamorig:RightHand_"); thToe = BoneStarts(r, "mixamorig:LeftToeBase"); thHead = BoneStarts(r, "mixamorig:Head_");
            if (thSh == null || thShR == null || thHip == null || thHand == null || thToe == null) { thorPropsFail = true; JsLiveEventsMod.Log.LogWarning("Galactus: Thor props - bones not found"); return; }
            // cape: two-sided strip
            capeV = new Vector3[CAPE_R * CAPE_C]; var uv = new Vector2[capeV.Length * 2]; var col = new Color[capeV.Length * 2]; var tri = new List<int>();
            for (int i = 0; i < CAPE_R; i++) for (int j = 0; j < CAPE_C; j++) { float v = i / (float)(CAPE_R - 1); col[i * CAPE_C + j] = col[capeV.Length + i * CAPE_C + j] = Color.Lerp(new Color(0.78f, 0.08f, 0.1f), new Color(0.42f, 0.03f, 0.06f), v); }
            for (int i = 0; i < CAPE_R - 1; i++) for (int j = 0; j < CAPE_C - 1; j++) { int p = i * CAPE_C + j; tri.AddRange(new[] { p, p + CAPE_C, p + 1, p + 1, p + CAPE_C, p + CAPE_C + 1 }); int o = capeV.Length; tri.AddRange(new[] { o + p, o + p + 1, o + p + CAPE_C, o + p + 1, o + p + CAPE_C + 1, o + p + CAPE_C }); }
            capeMesh = new Mesh { name = "ThorCape" }; capeMesh.vertices = new Vector3[capeV.Length * 2]; capeMesh.colors = col; capeMesh.uv = uv; capeMesh.triangles = tri.ToArray(); owned.Add(capeMesh);
            capeGo = new GameObject("ThorCape"); capeGo.transform.SetParent(root.transform, false); capeGo.AddComponent<MeshFilter>().sharedMesh = capeMesh; var mr = capeGo.AddComponent<MeshRenderer>(); var cm = new Material(kit.Flat(new Color(0.62f, 0.05f, 0.08f))); owned.Add(cm); mr.sharedMaterial = cm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; clones.Add(capeGo);
            // Mjolnir
            hammerGo = new GameObject("ThorHammer"); hammerGo.transform.SetParent(root.transform, false); clones.Add(hammerGo);
            var hm = kit.Flat(new Color(0.55f, 0.58f, 0.64f)); var lm = kit.Flat(new Color(0.3f, 0.17f, 0.09f));
            Func<PrimitiveType, string, Material, Vector3, Vector3, Transform> part = (pt, nm, mat, lp, ls) => { var g = GameObject.CreatePrimitive(pt); UnityEngine.Object.Destroy(g.GetComponent<Collider>()); g.name = nm; g.transform.SetParent(hammerGo.transform, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.GetComponent<MeshRenderer>().sharedMaterial = mat; g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g.transform; };
            part(PrimitiveType.Cylinder, "Handle", lm, new Vector3(0f, 0f, 0f), new Vector3(0.034f, 0.2f, 0.034f)); part(PrimitiveType.Cube, "Head", hm, new Vector3(0f, 0.26f, 0f), new Vector3(0.2f, 0.12f, 0.12f)); part(PrimitiveType.Cube, "Band", lm, new Vector3(0f, -0.2f, 0f), new Vector3(0.05f, 0.03f, 0.05f));
        }
        void ThorProps(float t, float dt)
        {
            Act a; if (!actBy.TryGetValue("thor", out a) || a == null) return; if (!thorPropsBuilt) BuildThorProps(a); if (thorPropsFail || capeGo == null) return;
            bool on = a.Go.activeSelf && a.Vis; if (capeGo.activeSelf != on) { capeGo.SetActive(on); hammerGo.SetActive(on); } if (!on) { thPrev = thHip.position; return; }
            float sc = a.Go.transform.lossyScale.x; float H = Mathf.Max(0.5f, (thHead != null ? (thHead.position - thToe.position).magnitude : 2f)); Vector3 pL = thSh.position, pR = thShR.position, mid = (pL + pR) * 0.5f, side = pR - pL; float sw = side.magnitude; side = side.sqrMagnitude > 1e-8f ? side / sw : Vector3.right;
            Vector3 fwd = Vector3.ProjectOnPlane(thToe.position - thHip.position, Vector3.up); if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; fwd.Normalize(); Vector3 sdir = Vector3.Cross(Vector3.up, fwd).normalized; if (Vector3.Dot(sdir, side) < 0f) sdir = -sdir;
            // sway: the cape lags behind the hips' movement
            Vector3 mv = (thHip.position - thPrev) / Mathf.Max(0.001f, dt); thPrev = thHip.position; if (dt > 0f && dt < 0.2f) { capeSwayV += (-mv * 0.04f - capeSway * 18f * dt - capeSwayV * 3f * dt) * 1f; capeSway += capeSwayV * dt; capeSway = Vector3.ClampMagnitude(capeSway, H * 0.35f); }
            float len = H * 0.5f, hw = Mathf.Max(0.18f, sw * 0.9f), back = H * 0.045f; var vs = capeMesh.vertices;
            for (int i = 0; i < CAPE_R; i++)
            {
                float v = i / (float)(CAPE_R - 1); Vector3 c = mid + Vector3.down * (len * v) - fwd * (back * (1f + 1.4f * v)) + capeSway * (v * v) + Vector3.up * (H * 0.015f * (1f - v)); float half = hw * (0.55f + 0.5f * v);
                for (int j = 0; j < CAPE_C; j++) { float u = j / (float)(CAPE_C - 1) * 2f - 1f; Vector3 p = root.transform.InverseTransformPoint(c + sdir * (u * half) - fwd * (0.02f * H * Mathf.Sin(u * 3.1f + t * 2f + v * 4f) * v)); vs[i * CAPE_C + j] = p; vs[capeV.Length + i * CAPE_C + j] = p; }
            }
            capeMesh.vertices = vs; capeMesh.RecalculateBounds();
            // hammer: in the right fist, head up and forward
            Vector3 fing = thHand.parent != null ? (thHand.position - thHand.parent.position).normalized : fwd; hammerGo.transform.position = thHand.position + fing * (0.075f * H); hammerGo.transform.rotation = Quaternion.LookRotation(sdir, Vector3.up) * Quaternion.Euler(0f, 0f, 0f) * Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-14f, 0f, 0f); hammerGo.transform.localScale = Vector3.one * (H * 0.55f);
        }
    }
}
