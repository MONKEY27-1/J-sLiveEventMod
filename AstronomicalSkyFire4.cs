using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Sky Fire, part 4: debris. Fourteen different pieces built in Blender (hull plates, I-beams, pipes, struts, an engine ring, a glowing panel, tanks, seats, crates and two chunks of burning hull)
    // rain down in the Cube hall after the blast, stream past you in the fall and break away from the Mothership.
    internal sealed partial class AstronomicalSkyFire
    {
        GltfModel debM;
        readonly List<GameObject> debFall = new List<GameObject>(), debHall = new List<GameObject>(), debChunks = new List<GameObject>();
        void BuildDebris()
        {
            debFall.Clear(); debHall.Clear(); debChunks.Clear(); if (debM == null) return;
            var tpl = new List<Transform>(); foreach (var t in debM.Pivot.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Deb_")) tpl.Add(t);
            if (tpl.Count == 0) { JsLiveEventsMod.Log.LogWarning("SkyFire: no debris pieces"); return; }
            int c1 = tpl.FindIndex(x => x.name == "Deb_Chunk1"), c2 = tpl.FindIndex(x => x.name == "Deb_Chunk2"); if (c1 < 0) c1 = 0; if (c2 < 0) c2 = 0;
            for (int i = 0; i < 110; i++) debFall.Add(DebClone(tpl[i % tpl.Count], "SDebF" + i));
            for (int i = 0; i < 36; i++) debHall.Add(DebClone(tpl[(i * 5 + 2) % tpl.Count], "SDebH" + i));
            for (int i = 0; i < 12; i++) debChunks.Add(DebClone(tpl[i % 2 == 0 ? c1 : c2], "SChunk" + i));
            JsLiveEventsMod.Log.LogInfo("SkyFire: debris: " + tpl.Count + " kinds, " + debFall.Count + " in the fall, " + debHall.Count + " in the hall, " + debChunks.Count + " ship chunks");
        }
        GameObject DebClone(Transform src, string n) { var go = UnityEngine.Object.Instantiate(src.gameObject, root.transform); go.name = n; go.SetActive(false); clones.Add(go); return go; }
        void DebrisCleanup() { debFall.Clear(); debHall.Clear(); debChunks.Clear(); }

        // after the blast (9:12 - 9:30): pieces of the ship's roof and racks fall around you and smash into the floor
        void HallDebris(float t, float dt, Vector3 headW)
        {
            bool on = t >= T_ZERO + 1f && t < T_FALL; Vector3 hl = root.transform.InverseTransformPoint(headW); float e = t - T_ZERO - 1f; float floorY = hl.y - 1.6f;
            for (int i = 0; i < debHall.Count; i++)
            {
                var go = debHall[i]; if (!on) { if (go.activeSelf) go.SetActive(false); continue; }
                float h1 = Hash(i, 31), h2 = Hash(i, 32), h3 = Hash(i, 33), h4 = Hash(i, 34); float ee = e - h1 * 15f; if (ee < 0f) { if (go.activeSelf) go.SetActive(false); continue; }
                float y = Mathf.Max(floorY + 0.4f * MK, hl.y + 34f * MK - 6f * MK * ee * ee); bool landed = y <= floorY + 0.45f * MK;
                Vector3 p = new Vector3(hl.x + Mathf.Lerp(-16f, 16f, h2) * MK, y, hl.z + Mathf.Lerp(-4f, 44f, h3) * MK);
                float size = (0.5f + 0.9f * h4) * MK; var rot = landed ? Quaternion.Euler(10f * h2, h3 * 360f, 14f * h4) : Quaternion.Euler(ee * (90f + 160f * h2) + i * 40f, ee * 120f * h3, ee * 70f);
                PlaceGo(go, p, size, rot, true);
                if (!paused && dt > 0f && !landed && i % 3 == 0) Emit(fx, W(p), Vector3.up * 3f, new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(0.8f, 2f), 0.6f);
                if (!paused && dt > 0f && landed && ee < h1 * 0 + 99f && UnityEngine.Random.value < dt * 0.6f) Emit(dust, W(p), Vector3.up * 2f, new Color(0.3f, 0.22f, 0.2f, 0.5f), 3f, 2f);
            }
        }
        // the fall (9:30 - 10:00): a stream of debris past you, and the Mothership's hull chunks tumbling away
        void FallDebris(float t, float tp, float dt, Vector3 an)
        {
            bool on = t >= T_FALL && t < T_LAND + 8f; float e = t - T_FALL;
            for (int i = 0; i < debFall.Count; i++)
            {
                var go = debFall[i]; if (!on || t >= T_LAND) { if (go.activeSelf) go.SetActive(false); continue; }
                float h1 = Hash(i, 41), h2 = Hash(i, 42), h3 = Hash(i, 43), h4 = Hash(i, 44); float a = h1 * 6.2832f, r = 28f + 120f * h2 * h2; float rel = Mathf.Repeat(h3 * 170f - e * (14f + 16f * h4), 170f) - 80f;
                Vector3 p = new Vector3(Mathf.Cos(a) * r, an.y + rel, Mathf.Sin(a) * r + 20f); float size = (0.45f + 1.5f * h4 * h4) * Sm(0f, 2f, e);
                PlaceGo(go, p, size, Quaternion.Euler(e * (30f + 110f * h1) + i * 33f, e * (20f + 90f * h2), e * 40f * (h3 - 0.5f) + i * 11f), true);
                if (!paused && dt > 0f && i % 4 == 0 && r < 100f) { Emit(fx, W(p), Vector3.up * UnityEngine.Random.Range(5f, 12f), new Color(1f, UnityEngine.Random.Range(0.35f, 0.7f), 0.12f, 0.9f), UnityEngine.Random.Range(1.2f, 3f), UnityEngine.Random.Range(0.5f, 1.1f)); if (UnityEngine.Random.value < 0.3f) Emit(dust, W(p), Vector3.up * 4f, new Color(0.25f, 0.18f, 0.2f, 0.5f), UnityEngine.Random.Range(3f, 8f), 2f); }
            }
            Vector3 sp = new Vector3(30f, 470f - 10f * Sm(T_FALL, T_END, t), 330f);
            for (int j = 0; j < debChunks.Count; j++)
            {
                var go = debChunks[j]; bool vis = on && t < T_END + 1f; if (!vis) { if (go.activeSelf) go.SetActive(false); continue; }
                float a = j * 2.4f + 0.7f, ee = Mathf.Max(0f, e - j * 1.6f); Vector3 dir = new Vector3(Mathf.Cos(a), 0.25f + 0.3f * Mathf.Sin(j), Mathf.Sin(a));
                Vector3 p = sp + dir * (50f + ee * (10f + 4f * (j % 3))) + Vector3.down * ee * ee * 0.7f; float size = (5f + 2.2f * (j % 4)) * Sm(0f, 1.2f, ee + 0.2f);
                PlaceGo(go, p, size, Quaternion.Euler(ee * (14f + 6f * j), ee * 9f * (1 + j % 3), ee * 11f), ee > 0f);
                if (ee > 0f && !paused && dt > 0f) { Emit(fx, W(p), UnityEngine.Random.insideUnitSphere * 3f, new Color(1f, UnityEngine.Random.Range(0.4f, 0.8f), 0.15f, 0.95f), UnityEngine.Random.Range(3f, 8f), UnityEngine.Random.Range(0.6f, 1.4f)); if (UnityEngine.Random.value < 0.5f) Emit(dust, W(p), Vector3.up * 3f, new Color(0.2f, 0.15f, 0.17f, 0.6f), UnityEngine.Random.Range(8f, 16f), 3f); }
            }
        }
    }
}
