using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 5: a real skin. The six rigid parts of the model are replaced at build time by skinned meshes that share ten bones (torso, head, both arms 2 bones each, both legs 2 bones each),
    // with smooth weights at the shoulders, elbows, neck, belt, hips and knees, so he bends instead of breaking apart; plus the leg motion (walk-in, stance, dangling in the fall, drifting in space).
    internal sealed partial class AstronomicalGalactus
    {
        readonly Dictionary<string, List<Renderer>> skinBy = new Dictionary<string, List<Renderer>>(); Transform gxHipL, gxKneeL, gxHipR, gxKneeR;

        void BuildSkin()
        {
            skinBy.Clear(); gxHipL = gxKneeL = gxHipR = gxKneeR = null;
            if (galM == null || gxTorso == null || gxHead == null || gxShL == null || gxElL == null || gxShR == null || gxElR == null || gxRoot == null) return;
            var piv = galM.Pivot.transform; var nodes = new Dictionary<string, Transform> { { "Torso", gxTorso }, { "Head", gxHead }, { "ShL", gxShL }, { "ElL", gxElL }, { "ShR", gxShR }, { "ElR", gxElR } };
            var parts = new List<KeyValuePair<string, MeshRenderer>>();
            foreach (var mr in piv.GetComponentsInChildren<MeshRenderer>(true)) { for (var t = mr.transform; t != null && t != piv; t = t.parent) { string nm = null; foreach (var kv in nodes) if (kv.Value == t) nm = kv.Key; if (nm != null) { parts.Add(new KeyValuePair<string, MeshRenderer>(nm, mr)); break; } } }
            if (parts.Count == 0) { JsLiveEventsMod.Log.LogWarning("Galactus: skin - no part meshes found"); return; }
            // ---- rest-pose facts in pivot space
            Func<Transform, Vector3> P = tr => piv.InverseTransformPoint(tr.position);
            Vector3 belt = P(gxTorso), neck = P(gxHead), shL = P(gxShL), elL = P(gxElL), shR = P(gxShR), elR = P(gxElR);
            Vector3 hdL = gxHandL != null ? P(gxHandL) : elL + (elL - shL), hdR = gxHandR != null ? P(gxHandR) : elR + (elR - shR);
            float feetY = float.MaxValue, topY = float.MinValue; var tv = new List<Vector3>();
            foreach (var kv in parts) { var mf = kv.Value.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; foreach (var v in mf.sharedMesh.vertices) { var w = piv.InverseTransformPoint(kv.Value.transform.TransformPoint(v)); feetY = Mathf.Min(feetY, w.y); topY = Mathf.Max(topY, w.y); if (kv.Key == "Torso") tv.Add(w); } }
            float H = topY - feetY, cx = belt.x; float hipY = belt.y - 0.03f * H, kneeY = feetY + 0.26f * H, hx = 0.06f * H; float zc = 0f; int zn = 0; foreach (var w in tv) if (w.y > feetY + 0.08f * H && w.y < hipY) { zc += w.z; zn++; } zc = zn > 0 ? zc / zn : belt.z;
            bool leftIsPlusX = shL.x > cx; float sL = leftIsPlusX ? 1f : -1f;
            Func<string, Transform, Vector3, Transform> mk = (nm, par, pos) => { var g = new GameObject(nm).transform; g.SetParent(par, false); g.position = piv.TransformPoint(pos); g.localRotation = Quaternion.identity; return g; };
            gxHipL = mk("HipL", gxRoot, new Vector3(cx + sL * hx, hipY, zc)); gxKneeL = mk("KneeL", gxHipL, new Vector3(cx + sL * hx, kneeY, zc));
            gxHipR = mk("HipR", gxRoot, new Vector3(cx - sL * hx, hipY, zc)); gxKneeR = mk("KneeR", gxHipR, new Vector3(cx - sL * hx, kneeY, zc));
            var bones = new Transform[] { gxTorso, gxHead, gxShL, gxElL, gxShR, gxElR, gxHipL, gxKneeL, gxHipR, gxKneeR };   // 0..9
            var bind = new Matrix4x4[bones.Length]; for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * piv.localToWorldMatrix;
            int hipLi = 6, kneeLi = 7, hipRi = 8, kneeRi = 9;
            foreach (var kv in parts)
            {
                string pn = kv.Key; var mr = kv.Value; var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) { JsLiveEventsMod.Log.LogWarning("Galactus: skin - mesh of " + pn + " not readable"); continue; }
                var sm = mf.sharedMesh; var vs = sm.vertices; var ns = sm.normals; var us = sm.uv; var cs = sm.colors32; int n = vs.Length; var nv = new Vector3[n]; var nn = new Vector3[ns.Length]; var bw = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = piv.InverseTransformPoint(mr.transform.TransformPoint(vs[i])); nv[i] = p; if (i < ns.Length) nn[i] = piv.InverseTransformDirection(mr.transform.TransformDirection(ns[i])).normalized;
                    var w = new float[10];
                    if (pn == "Head") { float wt = 1f - Sm(neck.y - 0.004f * H, neck.y + 0.02f * H, p.y); w[0] = wt; w[1] = 1f - wt; }
                    else if (pn == "Torso")
                    {
                        float wH = Sm(neck.y - 0.03f * H, neck.y + 0.005f * H, p.y), leg = 1f - Sm(hipY - 0.035f * H, hipY + 0.02f * H, p.y), shin = 1f - Sm(kneeY - 0.03f * H, kneeY + 0.03f * H, p.y), sideL = Sm(-0.035f * H, 0.035f * H, (p.x - cx) * sL);
                        w[0] = (1f - leg) * (1f - wH); w[1] = (1f - leg) * wH; w[hipLi] = leg * sideL * (1f - shin); w[kneeLi] = leg * sideL * shin; w[hipRi] = leg * (1f - sideL) * (1f - shin); w[kneeRi] = leg * (1f - sideL) * shin;
                    }
                    else
                    {
                        bool left = pn.EndsWith("L"); Vector3 S = left ? shL : shR, E = left ? elL : elR, Hd = left ? hdL : hdR; int iS = left ? 2 : 4, iE = left ? 3 : 5;
                        if (pn.StartsWith("Sh")) { Vector3 ax = E - S; float t = Vector3.Dot(p - S, ax) / Mathf.Max(1e-4f, ax.sqrMagnitude); float wT = (1f - Sm(-0.05f, 0.28f, t)) * 0.65f, wE = Sm(0.78f, 1.15f, t) * 0.5f; w[0] = wT; w[iE] = wE; w[iS] = 1f - wT - wE; }
                        else { Vector3 ax = Hd - E; float t = Vector3.Dot(p - E, ax) / Mathf.Max(1e-4f, ax.sqrMagnitude); float wS = 1f - Sm(-0.12f, 0.22f, t); w[iS] = wS; w[iE] = 1f - wS; }
                    }
                    // the four strongest, normalised
                    int[] ix = { 0, 0, 0, 0 }; float[] wv = { 0f, 0f, 0f, 0f }; for (int b = 0; b < 10; b++) { for (int s = 0; s < 4; s++) { if (w[b] > wv[s]) { for (int q = 3; q > s; q--) { wv[q] = wv[q - 1]; ix[q] = ix[q - 1]; } wv[s] = w[b]; ix[s] = b; break; } } }
                    float sum = wv[0] + wv[1] + wv[2] + wv[3]; if (sum < 1e-5f) { wv[0] = 1f; sum = 1f; }
                    bw[i].boneIndex0 = ix[0]; bw[i].boneIndex1 = ix[1]; bw[i].boneIndex2 = ix[2]; bw[i].boneIndex3 = ix[3]; bw[i].weight0 = wv[0] / sum; bw[i].weight1 = wv[1] / sum; bw[i].weight2 = wv[2] / sum; bw[i].weight3 = wv[3] / sum;
                }
                var m2 = new Mesh { name = "GxSkin_" + pn }; m2.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16; m2.vertices = nv; if (nn.Length == n) m2.normals = nn; if (us.Length == n) m2.uv = us; if (cs.Length == n) m2.colors32 = cs;
                m2.subMeshCount = sm.subMeshCount; for (int s = 0; s < sm.subMeshCount; s++) m2.SetTriangles(sm.GetTriangles(s), s); m2.boneWeights = bw; m2.bindposes = bind; m2.RecalculateBounds(); owned.Add(m2);
                var go = new GameObject("SK_" + pn); go.transform.SetParent(piv, false); var smr = go.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = m2; smr.bones = bones; smr.rootBone = gxTorso; smr.sharedMaterials = mr.sharedMaterials; smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false; smr.allowOcclusionWhenDynamic = false; mr.enabled = false;
                List<Renderer> l; if (!skinBy.TryGetValue(pn, out l)) skinBy[pn] = l = new List<Renderer>(); l.Add(smr);
            }
            JsLiveEventsMod.Log.LogInfo("Galactus: skinned " + skinBy.Count + " parts, height " + H.ToString("0.0") + ", hips y " + hipY.ToString("0.0") + " knees y " + kneeY.ToString("0.0"));
        }

        // legs: the slow walk-in, a wide stance, the dangle while he leans over you, the drift in space
        void GxLegs(float t)
        {
            if (gxHipL == null) return;
            float walk = Env(52f, 62f, 116f, 124f, t), s = Mathf.Sin(t * 2.1f), c = Mathf.Cos(t * 2.1f);
            float stance = 5f + 9f * Env(326f, 336f, 414f, 422f, t), sway = 3f * Mathf.Sin(t * 0.5f);
            float dang = Env(186f, 206f, 262f, 280f, t), space = Env(508f, 516f, 575f, 578f, t), brace = Env(150f, 160f, 170f, 176f, t);
            float hl = 26f * walk * s + 32f * dang + 9f * space * Mathf.Sin(t * 0.6f) + sway * 0.4f - 6f * brace, hr = -26f * walk * s + 22f * dang + 9f * space * Mathf.Sin(t * 0.6f + 1.4f) - sway * 0.4f + 8f * brace;
            float kl = 8f + 34f * walk * Mathf.Max(0f, c) + 55f * dang + 16f * space + 14f * brace, kr = 8f + 34f * walk * Mathf.Max(0f, -c) + 40f * dang + 16f * space * Mathf.Abs(Mathf.Sin(t * 0.4f)) + 8f * brace;
            gxHipL.localRotation = Quaternion.Euler(hl, 0f, stance + sway); gxHipR.localRotation = Quaternion.Euler(hr, 0f, -stance + sway);
            gxKneeL.localRotation = Quaternion.Euler(kl, 0f, 0f); gxKneeR.localRotation = Quaternion.Euler(kr, 0f, 0f);
        }
    }
}
