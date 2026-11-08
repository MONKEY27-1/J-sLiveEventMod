using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Galactus, part 5: the REAL skeleton.  The model is a glb built in Blender (tools/gx_rig.py + gx_anim.py): 27 bones (hips, 2 spine, chest, neck, head, clavicles, 2-bone arms with wrists, 2-bone legs with feet and toes),
    // four skinned meshes (Body, Head, ArmL, ArmR) and seven authored clips (Idle, Walk, Roar, Backhand, Stoop, Brace, Drift) with planted feet, counter-rotating torso, weight shifts, a coiled backhand.
    // Here the clips are scheduled as a pure function of the music time (cross-faded), the procedural layers (looking at the bus, aimed arms, the Zero Point cup) are added on top, and at 9:35 the skinned meshes are baked into pieces that fly apart.
    internal sealed partial class AstronomicalGalactus
    {
        readonly Dictionary<string, List<Renderer>> skinBy = new Dictionary<string, List<Renderer>>(); Transform gxHipL, gxKneeL, gxHipR, gxKneeR;
        // ---- fingers: 5 fingers x 3 segments per hand; the glb gives every finger bone local X = the hinge axis (curl) and local Z = the back of the hand (spread)
        static readonly string[] FNAME = { "Idx", "Mid", "Rng", "Pnk", "Thm" }; readonly Transform[,,] fing = new Transform[2, 5, 3]; bool fingOk; float fingSign = 1f; float cupW;
        void FindFingers() { fingOk = true; for (int sd = 0; sd < 2; sd++) for (int f = 0; f < 5; f++) for (int g = 0; g < 3; g++) { fing[sd, f, g] = Sub(galM, FNAME[f] + (sd == 0 ? "L" : "R") + (g + 1)); if (fing[sd, f, g] == null) fingOk = false; } }
        // curl[0..2] = degrees per segment (towards the palm), spread = degrees between the fingers, thumbK = how much the thumb follows
        void Curl(int sd, float c0, float c1, float c2, float spread, float thumbK)
        {
            if (!fingOk) return;
            for (int f = 0; f < 5; f++)
            {
                float k = f == 4 ? thumbK : 1f; float sp = f == 4 ? -spread * 1.4f : (f - 1.5f) * (spread / 1.5f);
                for (int g = 0; g < 3; g++) { var b = fing[sd, f, g]; if (b == null) continue; float cv = (g == 0 ? c0 : g == 1 ? c1 : c2) * k; var q = Quaternion.AngleAxis(fingSign * cv, Vector3.right); if (g == 0) q = q * Quaternion.AngleAxis(sp, Vector3.forward); b.localRotation = b.localRotation * q; }
            }
        }
        // the state of each hand over the event (sd: 0 = the model's L bones, 1 = R); "his right" is the O arm
        void GxFingers(float t)
        {
            if (!fingOk || gxOSh == null) return; int oSide = gxOSh.name.EndsWith("L") ? 0 : 1;
            for (int sd = 0; sd < 2; sd++)
            {
                bool isO = sd == oSide; float c0 = 20f, c1 = 28f, c2 = 18f, spread = 3f, th = 0.8f;
                float clench = Env(146.5f, 148f, 152f, 154.5f, t); { float pc = Mathf.Repeat(t - 510f, 9f); clench = Mathf.Max(clench, Sm(0.8f, 1.8f, pc) * (1f - Sm(5.2f, 6.8f, pc)) * Sm(512f, 520f, t) * (1f - Sm(570f, 575f, t))); }
                float open = 0f, fist = 0f;
                if (isO) { open = Mathf.Max(Env(HitT - 5f, HitT - 3f, HitT + 1.5f, HitT + 4f, t), Env(196f, 206f, 262f, 276f, t), Env(330f, 338f, 414f, 422f, t)); }
                else fist = Env(HitT - 4f, HitT - 1f, HitT + 4f, HitT + 8f, t);
                c0 = Mathf.Lerp(c0, 88f, Mathf.Max(clench, fist) ); c1 = Mathf.Lerp(c1, 95f, Mathf.Max(clench, fist)); c2 = Mathf.Lerp(c2, 75f, Mathf.Max(clench, fist));
                c0 = Mathf.Lerp(c0, -2f, open); c1 = Mathf.Lerp(c1, -4f, open); c2 = Mathf.Lerp(c2, -6f, open); spread = Mathf.Lerp(spread, 14f, open);
                if (!isO && cupW > 0.001f) { c0 = Mathf.Lerp(c0, 50f, cupW); c1 = Mathf.Lerp(c1, 62f, cupW); c2 = Mathf.Lerp(c2, 44f, cupW); spread = Mathf.Lerp(spread, 7f, cupW); th = 1f; }
                Curl(sd, c0, c1, c2, spread, th);
            }
        }
        GltfModel.Clip cIdle, cWalk, cRoar, cBackhand, cStoop, cBrace, cDrift; bool clipsOk;
        readonly List<Transform> pieces = new List<Transform>(); readonly List<Vector3> pieceDir = new List<Vector3>(), pieceSpin = new List<Vector3>(); bool broken; readonly List<Mesh> bakedMeshes = new List<Mesh>();

        void BuildSkin()
        {
            skinBy.Clear(); pieces.Clear(); broken = false; clipsOk = false;
            if (galM == null) return;
            foreach (var smr in galM.Pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                List<Renderer> l; string key = smr.gameObject.name; if (!skinBy.TryGetValue(key, out l)) skinBy[key] = l = new List<Renderer>(); l.Add(smr);
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false; smr.allowOcclusionWhenDynamic = false; smr.updateWhenOffscreen = true;
            }
            FindFingers(); cIdle = galM.FindClipExact("Idle"); cWalk = galM.FindClipExact("Walk"); cRoar = galM.FindClipExact("Roar"); cBackhand = galM.FindClipExact("Backhand"); cStoop = galM.FindClipExact("Stoop"); cBrace = galM.FindClipExact("Brace"); cDrift = galM.FindClipExact("Drift");
            clipsOk = cIdle != null && cWalk != null && cRoar != null && cBackhand != null && cStoop != null && cBrace != null && cDrift != null;
            JsLiveEventsMod.Log.LogInfo("Galactus: skeleton - " + skinBy.Count + " skinned meshes, clips " + (clipsOk ? "all 7 found" : "MISSING") + " (" + galM.Clips.Count + " in the file), channels " + (cIdle != null ? cIdle.Channels.Count : -1) + "/" + (cWalk != null ? cWalk.Channels.Count : -1) + "/" + (cBackhand != null ? cBackhand.Channels.Count : -1));
        }

        // ---- the clip schedule: (start time, clip, loops?, cross-fade seconds).  Clip time = t - start.
        struct GSeg { public float T0; public GltfModel.Clip C; public bool Loop; public float Fade; }
        GSeg[] gsegs;
        void BuildSegs()
        {
            gsegs = new[] {
                new GSeg { T0 = 0f, C = cIdle, Loop = true, Fade = 0f }, new GSeg { T0 = 48f, C = cWalk, Loop = true, Fade = 4f }, new GSeg { T0 = 103f, C = cIdle, Loop = true, Fade = 5f },
                new GSeg { T0 = 145f, C = cRoar, Loop = false, Fade = 2.5f }, new GSeg { T0 = 152f, C = cIdle, Loop = true, Fade = 1.5f },
                new GSeg { T0 = HitT - 6f, C = cBackhand, Loop = false, Fade = 2f }, new GSeg { T0 = HitT + 6f, C = cStoop, Loop = true, Fade = 6f },
                new GSeg { T0 = 272f, C = cIdle, Loop = true, Fade = 8f }, new GSeg { T0 = 326f, C = cBrace, Loop = true, Fade = 8f }, new GSeg { T0 = 418f, C = cIdle, Loop = true, Fade = 6f }, new GSeg { T0 = 508f, C = cDrift, Loop = true, Fade = 8f } };
        }
        float ClipTime(GSeg g, float t) { float ct = t - g.T0; if (g.Loop || g.C == null) return ct; return Mathf.Min(ct, g.C.Duration - 0.03f); }
        void GxClips(float t)
        {
            if (!clipsOk) return; if (gsegs == null) BuildSegs();
            int i = 0; while (i + 1 < gsegs.Length && gsegs[i + 1].T0 <= t) i++; var cur = gsegs[i]; float tc = ClipTime(cur, t);
            float w = i > 0 && cur.Fade > 0f ? Mathf.Clamp01((t - cur.T0) / cur.Fade) : 1f; w = w * w * (3f - 2f * w);
            if (w < 0.999f && i > 0) { var pv = gsegs[i - 1]; galM.SampleBlend(pv.C, ClipTime(pv, t), cur.C, tc, w); } else galM.Sample(cur.C, tc);
            // 8:30 - 9:35 the roars in space: every 9 s the Roar clip is blended over the drift
            if (t >= 508f && t < 576f)
            {
                float pc = Mathf.Repeat(t - 510f, 9f); float rw = Sm(0.2f, 1.5f, pc) * (1f - Sm(5.4f, 7.2f, pc)) * Sm(512f, 520f, t) * (1f - Sm(570f, 575f, t));
                if (rw > 0.01f) galM.SampleBlend(cDrift, tc, cRoar, Mathf.Clamp(pc * 0.8f, 0f, 6.9f), rw);
            }
        }
        // ---- procedural layers added on top of the clip (all in the model's own frame, so they follow his bend)
        void GxAdd(float t)
        {
            if (gxHead == null || gxShL == null || gxShR == null) return; var go = galM.Pivot.transform; Vector3 up = go.up; Vector3 leftW = (gxShL.position - gxShR.position).normalized;
            Vector3 eyes = gxEyeL != null && gxEyeR != null ? (gxEyeL.position + gxEyeR.position) * 0.5f : gxHead.position + go.forward; Vector3 fwd = Vector3.ProjectOnPlane(eyes - gxHead.position, up); if (fwd.sqrMagnitude < 1e-6f) fwd = go.forward; fwd.Normalize();
            Vector3 r0 = Vector3.Cross(up, fwd); float ys = Mathf.Sign(Vector3.Dot(leftW, r0) == 0f ? 1f : Vector3.Dot(leftW, r0));
            float hy = 0f, hp = 0f, cy = 0f, cr = 0f, sh = 0f;
            { float e = Env(118f, 126f, 148f, 154f, t); hy += 30f * Mathf.Sin((t - 118f) * 0.45f) * e; hp += 7f * Mathf.Sin((t - 118f) * 0.9f) * e; }                       // looks along the deck
            { float e = Env(184f, 192f, 262f, 272f, t); hy += 16f * Mathf.Sin(t * 0.5f) * e; }                                                                     // looks over the island
            { float e = Env(276f, 286f, 326f, 334f, t); hy += 20f * Mathf.Sin(t * 0.4f) * e; hp += 4f * Mathf.Sin(t * 0.5f) * e; }                                  // sizes up the buses
            { float e = Env(508f, 516f, 575f, 578f, t); hy += 14f * Mathf.Sin(t * 0.35f) * e; }                                                                     // drifting, looking about
            { float e = Env(540f, 550f, 575f, 578f, t); hp -= 8f * e + 6f * e * Mathf.Abs(Mathf.Sin(t * 5f)); sh += 2f * e; }                                       // the mouth opens: he rears back and shudders
            float track = Mathf.Max(Sm(334f, 348f, t) * (1f - Sm(414f, 420f, t)), Sm(516f, 528f, t) * (1f - Sm(568f, 576f, t)));
            if (track > 0.001f && gxHead.parent != null)
            {   // his head follows the bus
                Vector3 toBus = W(BusPos) - gxHead.position; Vector3 flat = Vector3.ProjectOnPlane(toBus, up); float yawTo = Vector3.SignedAngle(fwd, flat, up) * -1f * ys * -1f; float pitchTo = Mathf.Atan2(Vector3.Dot(toBus, up), flat.magnitude) * Mathf.Rad2Deg;
                hy += Mathf.Clamp(yawTo, -34f, 34f) * track * 0.7f; hp += Mathf.Clamp(-pitchTo, -24f, 24f) * track * 0.55f;
            }
            float jolt = 0f, rattle = 0f; foreach (float bt in BOOMS) if (bt >= 540f && bt <= 575f) { float e = Sm(bt - 0.16f, bt + 0.04f, t) * (1f - Sm(bt + 0.12f, bt + 0.85f, t)); jolt = Mathf.Max(jolt, e); rattle += Mathf.Sin((t - bt) * 48f) * e; }
            hp += 10f * jolt; cr += 2.5f * rattle; sh -= 8f * jolt;
            Quaternion qh = Quaternion.AngleAxis(ys * hy, up) * Quaternion.AngleAxis(hp, r0);
            if (Mathf.Abs(hy) + Mathf.Abs(hp) > 0.01f) gxHead.rotation = qh * gxHead.rotation;
            var chest = gxChestP != null ? gxChestP.parent : null;
            if (chest != null && (Mathf.Abs(cr) > 0.01f || Mathf.Abs(cy) > 0.01f)) chest.rotation = Quaternion.AngleAxis(cr, fwd) * chest.rotation;
        }
        // ---- 9:35 he comes apart: the skinned meshes are baked once into rigid pieces that drift away tumbling
        void BreakApart(bool on, float e = 0f)
        {
            if (!on)
            {
                if (broken) { broken = false; foreach (var kv in skinBy) foreach (var r in kv.Value) if (r != null) r.enabled = !(armHidden && (kv.Key == "ArmL" || kv.Key == "ArmR") && gxOSh != null && gxOSh.name.EndsWith(kv.Key.Substring(3))); foreach (var p in pieces) if (p != null) p.gameObject.SetActive(false); }
                return;
            }
            if (!broken)
            {
                broken = true;
                if (pieces.Count == 0)
                {
                    int n = 0; Vector3[] dirs = { new Vector3(0f, 0.1f, 1f), new Vector3(0f, 1f, 0.5f), new Vector3(1f, 0.2f, 0.4f), new Vector3(-1f, 0.2f, 0.4f) };
                    foreach (var kv in skinBy) foreach (var r in kv.Value)
                    {
                        var smr = r as SkinnedMeshRenderer; if (smr == null) continue; var m = new Mesh(); smr.BakeMesh(m); bakedMeshes.Add(m); owned.Add(m);
                        var g = new GameObject("GxPiece_" + kv.Key); g.transform.SetParent(smr.transform.parent, false); g.transform.localPosition = smr.transform.localPosition; g.transform.localRotation = smr.transform.localRotation; g.transform.localScale = smr.transform.localScale;
                        g.AddComponent<MeshFilter>().sharedMesh = m; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterials = smr.sharedMaterials; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        pieces.Add(g.transform); pieceDir.Add(dirs[n % dirs.Length] * 6f); pieceSpin.Add(new Vector3(Hash(n, 7) - 0.5f, Hash(n, 8) - 0.5f, Hash(n, 9) - 0.5f)); n++;
                    }
                }
                foreach (var kv in skinBy) foreach (var r in kv.Value) if (r != null) r.enabled = false; foreach (var p in pieces) if (p != null) p.gameObject.SetActive(true);
            }
            for (int i = 0; i < pieces.Count; i++) { if (pieces[i] == null) continue; var p0 = pieces[i].parent; pieces[i].localPosition = pieceDir[i] * (e * e * 1.2f + e * 4f) * 1.0f; pieces[i].localRotation = Quaternion.Euler(pieceSpin[i] * e * 25f); }
        }
    }
}
