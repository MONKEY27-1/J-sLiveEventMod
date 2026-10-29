using System;
using System.Collections.Generic;
using UnityEngine;
using GorillaLocomotion;

namespace AstronomicalEvent
{
    // Gameplay layer, local to this client: knockback from explosions / meteors / lightning / Travis's stomps, the game's own swimming during the
    // flood sequence, and floating launch rings. Everything goes through the game's public GTPlayer API (ApplyKnockback, forcedUnderwater);
    // nothing is networked and every effect can be switched off in the config (Knockback.Enabled / SwimInFlood).
    internal static class AstronomicalHazards
    {
        static float lastHit = -9f, lastWarn;
        static bool swimOn;
        public static int Hits { get; private set; }

        static GTPlayer Player() { try { return GTPlayer.Instance; } catch { return null; } }

        // Pushes the player away from p. radius = reach, speed = push at the centre (falls off linearly), cap = max speed given.
        public static void Blast(Vector3 p, float radius, float speed, float cap = 13f)
        {
            if (!AstronomicalSettings.Knockback.Value) return;
            var pl = Player(); if (pl == null || Time.time - lastHit < 0.15f) return;
            try
            {
                Vector3 head = pl.HeadCenterPosition, d = head - p; float dist = d.magnitude;
                if (dist >= radius) return;
                float fall = 1f - dist / radius; fall *= fall * 0.5f + 0.5f * (1f - dist / radius);
                float sp = Mathf.Min(cap, speed * fall) * AstronomicalSettings.KnockbackStrength.Value;
                if (sp < 1.2f) return;
                Vector3 dir = (d.sqrMagnitude < 0.01f ? Vector3.up : d.normalized) + Vector3.up * 0.35f;
                pl.ApplyKnockback(dir.normalized, sp, true);
                lastHit = Time.time; Hits++;
            }
            catch (Exception e) { if (Time.time - lastWarn > 5f) { lastWarn = Time.time; AstronomicalEventMod.Log.LogWarning("Knockback failed: " + e.Message); } }
        }

        // Pulls the player towards p (a black hole): same limits and strength setting as Blast.
        public static void Pull(Vector3 p, float radius, float speed, float cap = 9f)
        {
            if (!AstronomicalSettings.Knockback.Value) return;
            var pl = Player(); if (pl == null || Time.time - lastHit < 0.15f) return;
            try
            {
                Vector3 head = pl.HeadCenterPosition, d = p - head; float dist = d.magnitude;
                if (dist >= radius || dist < 1.5f) return;
                float sp = Mathf.Min(cap, speed * (1f - dist / radius * 0.6f)) * AstronomicalSettings.KnockbackStrength.Value;
                if (sp < 1.2f) return;
                pl.ApplyKnockback(d.normalized, sp, true);
                lastHit = Time.time; Hits++;
            }
            catch (Exception e) { if (Time.time - lastWarn > 5f) { lastWarn = Time.time; AstronomicalEventMod.Log.LogWarning("Pull failed: " + e.Message); } }
        }

        // A moving object (meteor) that passes within `radius` of the head hits it along its direction of travel.
        public static void Strike(Vector3 objPos, Vector3 travelDir, float radius, float speed)
        {
            if (!AstronomicalSettings.Knockback.Value) return;
            var pl = Player(); if (pl == null || Time.time - lastHit < 0.4f) return;
            try
            {
                if ((pl.HeadCenterPosition - objPos).sqrMagnitude > radius * radius) return;
                pl.ApplyKnockback((travelDir.normalized + Vector3.up * 0.25f).normalized, Mathf.Min(16f, speed * AstronomicalSettings.KnockbackStrength.Value), true);
                lastHit = Time.time; Hits++;
            }
            catch { }
        }

        // The game's built-in swimming: buoyancy, hand-stroke propulsion, drag and the underwater audio snapshot. Only while the player is near the stage.
        public static void Swim(bool want, Vector3 anchor)
        {
            if (!want && !swimOn) return;
            var pl = Player(); if (pl == null) return;
            bool on = want && AstronomicalSettings.SwimInFlood.Value && (pl.HeadCenterPosition - anchor).sqrMagnitude < 90f * 90f;
            try
            {
                if (pl.forcedUnderwater != on) { pl.forcedUnderwater = on; AstronomicalEventMod.Log.LogInfo("Flood swimming " + (on ? "on" : "off")); }
                swimOn = on;
            }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Swim toggle failed: " + e.Message); }
        }

        // Called every frame while no event is running (and on Stop): if we ever switched swimming on, make sure it really goes off; retries until it does.
        public static void Release()
        {
            if (!swimOn) return;
            var pl = Player(); if (pl == null) return;
            try { pl.forcedUnderwater = false; swimOn = false; AstronomicalEventMod.Log.LogInfo("Flood swimming released"); }
            catch (Exception e) { Warn2(e.Message); }
        }
        static void Warn2(string m) { if (Time.time - lastWarn > 5f) { lastWarn = Time.time; AstronomicalEventMod.Log.LogWarning("Swim release failed: " + m); } }

        public static void Reset() { Hits = 0; Release(); }
    }

    // Glowing rings floating around the arena. Fly or swing through one and it launches you along its axis.
    internal sealed class AstronomicalLaunchRings
    {
        Transform[] t; Material[] m; Vector3[] prev; float[] cool; Vector3 anchor; Material src; int count;
        public Action<Vector3, float> Burst;

        public void Build(Transform parent, Vector3 anchorPos, Material ringMat)
        {
            anchor = anchorPos; src = ringMat; count = 7;
            t = new Transform[count]; m = new Material[count]; prev = new Vector3[count]; cool = new float[count];
            for (int k = 0; k < count; k++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "LaunchRing"; q.transform.SetParent(parent, false);
                float a = k * 6.2832f / count + 0.4f, r = 14f + (k % 3) * 9f, h = 4f + (k * 5f) % 18f;
                q.transform.position = anchor + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                Vector3 outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                q.transform.rotation = Quaternion.LookRotation((Vector3.up * 0.8f + outward * 0.6f).normalized);   // axis points up and out: a natural launch path
                q.transform.localScale = Vector3.one * 6f;
                m[k] = new Material(src); q.GetComponent<Renderer>().sharedMaterial = m[k]; q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // double-sided look: a second quad facing the other way
                var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(q2.GetComponent<Collider>());
                q2.transform.SetParent(q.transform, false); q2.transform.localRotation = Quaternion.Euler(0, 180, 0); q2.GetComponent<Renderer>().sharedMaterial = m[k];
                q2.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t[k] = q.transform; q.SetActive(false); cool[k] = 0;
            }
        }

        public void Tick(float dt, float t0, int seq, float kick, float hue)
        {
            if (t == null) return;
            bool active = seq >= 1 && seq <= 6;
            Vector3 head = Vector3.zero; bool haveHead = false;
            try { var pl = GorillaLocomotion.GTPlayer.Instance; if (pl != null) { head = pl.HeadCenterPosition; haveHead = true; } } catch { }
            for (int k = 0; k < count; k++)
            {
                if (t[k] == null) continue;
                if (t[k].gameObject.activeSelf != active) t[k].gameObject.SetActive(active);
                if (!active) continue;
                cool[k] -= dt;
                float pulse = 1f + 0.12f * kick + 0.05f * Mathf.Sin(t0 * 2f + k);
                t[k].localScale = Vector3.one * 6f * pulse;
                var c = Color.HSVToRGB(Mathf.Repeat(hue + k * 0.14f, 1f), 0.5f, 1f); c.a = cool[k] > 0 ? 0.25f : 0.9f; m[k].color = c;
                if (!haveHead) continue;
                Vector3 loc = t[k].InverseTransformPoint(head);
                float prevZ = prev[k].z; prev[k] = loc;
                if (cool[k] > 0 || Mathf.Sqrt(loc.x * loc.x + loc.y * loc.y) > 0.5f || Mathf.Sign(prevZ) == Mathf.Sign(loc.z) || Mathf.Abs(loc.z) > 0.5f) continue;
                // crossed the ring's plane inside its radius (local units: the quad is 1 wide, scaled by 6 m, ring radius ~0.44)
                if (!AstronomicalSettings.Knockback.Value) continue;
                try
                {
                    Vector3 dir = t[k].forward * (prevZ > 0 ? -1f : 1f);
                    GorillaLocomotion.GTPlayer.Instance.ApplyKnockback(dir, 11f * AstronomicalSettings.KnockbackStrength.Value, true);
                    cool[k] = 1.5f; if (Burst != null) Burst(t[k].position, 6f);
                }
                catch { }
            }
        }

        public void Dispose() { if (m != null) foreach (var x in m) if (x != null) UnityEngine.Object.Destroy(x); t = null; m = null; }
    }
}
