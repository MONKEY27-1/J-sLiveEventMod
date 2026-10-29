using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstronomicalEvent
{
    // Purely visual, client-side destruction of event-owned trees. Forest's own trees are baked into combined meshes and are never touched:
    // this spawns copies of the user's tree model (plus one procedural treehouse) around the stage and then topples/explodes/floats them.
    internal sealed class AstronomicalDestruction
    {
        sealed class Piece { public Transform t; public Vector3 vel, ang; public float half, baseY; public bool settled; }
        sealed class Group
        {
            public bool house; public Vector3 basePos, dir, axis; public float height, width, yaw, scale, dist;
            public int state; public float timer, shake, fallDur, wob, yOff, seed;
            public Transform pivot; public List<Piece> pieces;
        }

        readonly List<Group> groups = new List<Group>();
        readonly List<UnityEngine.Object> ownedMats = new List<UnityEngine.Object>();
        GameObject fxGo; Transform fx; Vector3 anchor; GltfModel model;
        ParticleSystem debris; Material plain, glow;
        LineRenderer[] bolts; float[] boltAge; Vector3[] boltTarget; int boltNext;
        Light flash; float flashI;
        GameObject[] meteors, marks; TrailRenderer[] trails; Vector3[] mvel, mtarget; bool[] mact;
        float cool; bool houseDone; int pointer, chain;
        public int GroupCount => groups.Count;
        public int HouseCount { get; private set; }
        public int Fallen { get; private set; }
        public GameObject Root => fxGo;
        public Action<Vector3, float> Boom;
        public void Zap(Vector3 p, float hue) { if (fxGo != null) Strike(p, hue); }

        static Color Col(float h, float s, float v) { return Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v); }
        static readonly Color Brown = new Color(0.4f, 0.26f, 0.12f), Leaf = new Color(0.25f, 0.65f, 0.18f), Fire = new Color(1f, 0.55f, 0.1f);

        public void Init(Vector3 anchorPos, Transform eventRoot, Material glowMat, Material plainMat, float radius, GltfModel treeModel)
        {
            anchor = anchorPos; glow = glowMat; plain = plainMat; model = treeModel;
            fxGo = new GameObject("AstronomicalDestructionFx"); fx = fxGo.transform;

            var dg = new GameObject("Debris"); dg.transform.SetParent(fx, false);
            debris = dg.AddComponent<ParticleSystem>(); debris.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = debris.main; m.maxParticles = 4000; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = 1.2f; m.playOnAwake = false; m.startLifetime = 2.5f;
            var e = debris.emission; e.enabled = false;
            dg.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
            debris.Play();

            bolts = new LineRenderer[4]; boltAge = new float[4]; boltTarget = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                var b = new GameObject("Bolt").AddComponent<LineRenderer>(); b.transform.SetParent(fx, false);
                b.positionCount = 14; b.widthMultiplier = 0.7f; b.sharedMaterial = plain; b.useWorldSpace = true; b.enabled = false;
                b.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; bolts[k] = b; boltAge[k] = 9f;
            }
            var fg = new GameObject("StrikeFlash"); fg.transform.SetParent(fx, false);
            flash = fg.AddComponent<Light>(); flash.type = LightType.Point; flash.range = 40f; flash.intensity = 0; flash.shadows = LightShadows.None;

            marks = new GameObject[5];
            for (int k = 0; k < 5; k++)
            {
                var mk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.DestroyImmediate(mk.GetComponent<Collider>());
                mk.transform.SetParent(fx, false); var mkM = new Material(glow) { color = new Color(1f, 0.1f, 0.05f, 0.55f) }; ownedMats.Add(mkM); mk.GetComponent<Renderer>().sharedMaterial = mkM; mk.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mk.SetActive(false); marks[k] = mk;   // red impact warning disc under each incoming meteor
            }
            meteors = new GameObject[5]; trails = new TrailRenderer[5]; mvel = new Vector3[5]; mtarget = new Vector3[5]; mact = new bool[5];
            for (int k = 0; k < 5; k++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(fx, false); s.transform.localScale = Vector3.one * 3f;
                var r = s.GetComponent<Renderer>(); r.sharedMaterial = plain; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var tr = s.AddComponent<TrailRenderer>(); tr.time = 0.9f; tr.widthMultiplier = 3f; tr.sharedMaterial = plain; tr.emitting = false;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.SetActive(false); meteors[k] = s; trails[k] = tr;
            }
            SpawnForest(radius);
        }

        // ---- spawning ----
        void SpawnForest(float radius)
        {
            if (model == null || model.Pivot == null) { AstronomicalEventMod.Log.LogWarning("Destruction: tree model missing - no trees to destroy"); return; }
            float mh = Mathf.Max(0.1f, model.RendererBounds.size.y);
            var placed = new List<Vector3>(); int tries = 0;
            float maxR = Mathf.Min(radius, 80f);
            while (groups.Count < 36 && tries++ < 600)
            {
                bool house = groups.Count == 0;
                float a = UnityEngine.Random.value * 6.2832f, r = house ? UnityEngine.Random.Range(24f, 34f) : UnityEngine.Random.Range(17f, maxR);
                var p = new Vector3(anchor.x + Mathf.Cos(a) * r, 0, anchor.z + Mathf.Sin(a) * r);
                bool near = false; foreach (var q in placed) if ((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < 49f) { near = true; break; }
                if (near) continue;
                RaycastHit hit;
                if (!Physics.Raycast(new Vector3(p.x, anchor.y + 12f, p.z), Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.point.y - anchor.y) > 10f) continue; // only ground-level spots; skips canopies and cliffs
                p.y = hit.point.y;
                var g = new Group { house = house, basePos = p, yaw = UnityEngine.Random.value * 360f, height = house ? 22f : UnityEngine.Random.Range(11f, 19f), seed = UnityEngine.Random.value * 6.28f };
                g.scale = g.height / mh; g.width = model.RendererBounds.size.x * g.scale;
                g.dist = Mathf.Sqrt((p.x - anchor.x) * (p.x - anchor.x) + (p.z - anchor.z) * (p.z - anchor.z));
                placed.Add(p); groups.Add(g); if (house) HouseCount++;
                BuildStanding(g);
            }
            groups.Sort((x, y) => x.dist.CompareTo(y.dist));
            AstronomicalEventMod.Log.LogInfo("Destruction: spawned " + groups.Count + " trees (" + HouseCount + " treehouse) in " + tries + " tries");
        }

        Material Flat(Color c) { var m = new Material(plain.shader) { color = c }; ownedMats.Add(m); return m; }

        void Box(Transform parent, Vector3 lp, Vector3 ls, Quaternion lr, Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false); c.transform.localPosition = lp; c.transform.localRotation = lr; c.transform.localScale = ls;
            var r = c.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        void BuildStanding(Group g)
        {
            g.pivot = new GameObject(g.house ? "TreehouseTree" : "Tree").transform; g.pivot.SetParent(fx, false); g.pivot.position = g.basePos;
            var inst = UnityEngine.Object.Instantiate(model.Pivot); inst.SetActive(true);
            inst.transform.SetParent(g.pivot, false);
            inst.transform.localScale = Vector3.one * g.scale;
            inst.transform.localPosition = new Vector3(0, g.scale * model.RendererBounds.size.y * 0.5f, 0);
            inst.transform.localRotation = Quaternion.Euler(0, g.yaw, 0);
            if (g.house)
            {
                float s = g.height * 0.15f, y = g.height * 0.5f;
                var wall = Flat(new Color(0.55f, 0.36f, 0.18f)); var plank = Flat(new Color(0.42f, 0.27f, 0.13f)); var roof = Flat(new Color(0.55f, 0.1f, 0.08f));
                Box(g.pivot, new Vector3(0, y, 0), new Vector3(s * 2.4f, s * 0.15f, s * 2.4f), Quaternion.identity, plank);
                Box(g.pivot, new Vector3(s * 1.1f, y + s * 0.6f, 0), new Vector3(s * 0.15f, s * 1.2f, s * 2.4f), Quaternion.identity, wall);
                Box(g.pivot, new Vector3(-s * 1.1f, y + s * 0.6f, 0), new Vector3(s * 0.15f, s * 1.2f, s * 2.4f), Quaternion.identity, wall);
                Box(g.pivot, new Vector3(0, y + s * 0.6f, s * 1.1f), new Vector3(s * 2.4f, s * 1.2f, s * 0.15f), Quaternion.identity, wall);
                Box(g.pivot, new Vector3(0, y + s * 0.6f, -s * 1.1f), new Vector3(s * 2.4f, s * 1.2f, s * 0.15f), Quaternion.identity, wall);
                Box(g.pivot, new Vector3(s * 0.7f, y + s * 1.55f, 0), new Vector3(s * 1.7f, s * 0.12f, s * 2.8f), Quaternion.Euler(0, 0, -32), roof);
                Box(g.pivot, new Vector3(-s * 0.7f, y + s * 1.55f, 0), new Vector3(s * 1.7f, s * 0.12f, s * 2.8f), Quaternion.Euler(0, 0, 32), roof);
                for (int k = 0; k < 6; k++) Box(g.pivot, new Vector3(0, (k + 0.5f) * y / 6f, s * 1.45f), new Vector3(s * 0.9f, s * 0.08f, s * 0.12f), Quaternion.identity, plank); // ladder
            }
        }

        // ---- effects ----
        void Emit(Vector3 p, int n, Color c, float speed, float up, float size)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int k = 0; k < n; k++)
            {
                ep.position = p + UnityEngine.Random.insideUnitSphere * 0.8f;
                ep.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.3f, 1f) * speed + Vector3.up * up;
                ep.startColor = c; ep.startSize = size * UnityEngine.Random.Range(0.5f, 1.4f); ep.startLifetime = UnityEngine.Random.Range(1.5f, 3f);
                debris.Emit(ep, 1);
            }
        }

        void Strike(Vector3 target, float hue)
        {
            int k = boltNext; boltNext = (boltNext + 1) % bolts.Length;
            boltAge[k] = 0; boltTarget[k] = target; bolts[k].enabled = true;
            AstronomicalHazards.Blast(target, 9f, 8f);
            var c = Color.Lerp(Color.white, Col(hue, 0.5f, 1f), 0.5f); bolts[k].startColor = c; bolts[k].endColor = c;
            flashI = 8f; flash.transform.position = target + Vector3.up * 6f; flash.color = c;
        }

        void Impact(Vector3 p, float radius, float dens)
        {
            AstronomicalHazards.Blast(p, radius * 2.2f, 15f);
            Emit(p, (int)(90 * dens), Fire, 15f, 6f, 1.1f);
            Emit(p, (int)(50 * dens), Brown, 10f, 5f, 0.7f);
            flashI = 10f; flash.transform.position = p + Vector3.up * 4f; flash.color = Fire;
            if (Boom != null) Boom(p, radius * 0.8f);
            foreach (var g in groups)
            {
                if (g.state != 0 || g.house) continue;
                float dx = g.basePos.x - p.x, dz = g.basePos.z - p.z, d2 = dx * dx + dz * dz;
                if (d2 < 36f) Explode(g, p, dens); else if (d2 < radius * radius) Topple(g, p, 0.25f, dens);
            }
        }

        Group NextPending()
        {
            while (pointer < groups.Count && (groups[pointer].state != 0 || groups[pointer].house)) pointer++;
            return pointer < groups.Count ? groups[pointer] : null;
        }

        void Launch(float hue)
        {
            for (int k = 0; k < meteors.Length; k++)
            {
                if (mact[k]) continue;
                var g = NextPending();
                Vector3 target = g != null && UnityEngine.Random.value < 0.7f ? g.basePos : anchor + new Vector3(UnityEngine.Random.Range(-30f, 30f), 0, UnityEngine.Random.Range(-10f, 40f));
                Vector3 start = target + new Vector3(UnityEngine.Random.Range(-45f, 45f), 105f, UnityEngine.Random.Range(-45f, 45f));
                mtarget[k] = target; mvel[k] = (target - start).normalized * 75f; mact[k] = true;
                meteors[k].transform.position = start; meteors[k].SetActive(true);
                var tr = trails[k]; tr.Clear(); tr.emitting = true; tr.startColor = Col(hue, 0.6f, 1f); tr.endColor = new Color(1f, 0.25f, 0f, 0f);
                return;
            }
        }

        // ---- group lifecycle ----
        void Topple(Group g, Vector3 from, float shake, float dens)
        {
            if (g.state != 0) return;
            Vector3 d = g.basePos - from; d.y = 0;
            if (d.sqrMagnitude < 0.5f) { float a = UnityEngine.Random.value * 6.28f; d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
            g.dir = d.normalized; g.axis = Vector3.Cross(Vector3.up, g.dir);
            g.state = 1; g.timer = 0; g.shake = shake;
            Emit(g.basePos + Vector3.up * g.height * 0.7f, (int)(25 * dens), Leaf, 5f, 2f, 0.6f);
        }

        void Explode(Group g, Vector3 from, float dens)
        {
            if (g.state != 0) return;
            Vector3 d = g.basePos - from; d.y = 0; if (d.sqrMagnitude < 0.5f) d = Vector3.right;
            g.dir = d.normalized; g.axis = Vector3.Cross(Vector3.up, g.dir);
            g.state = 3; g.timer = 1f; Fallen++;
            Shatter(g, dens, 2f);
            if (Boom != null && !quiet) Boom(g.basePos + Vector3.up * g.height * 0.4f, 7f);
        }

        // Detaches every renderer of the tree (trunk, canopy, cabin boards) as an independent ballistic piece.
        void Shatter(Group g, float dens, float boost = 1f)
        {
            g.pieces = new List<Piece>();
            foreach (var mr in g.pivot.GetComponentsInChildren<MeshRenderer>())
            {
                var c = mr.transform; c.SetParent(fx, true);
                float ext = Mathf.Min(8f, mr.bounds.extents.magnitude);
                Vector3 outDir = c.position - g.basePos; outDir.y = 0; if (outDir.sqrMagnitude < 0.01f) outDir = g.dir; outDir.Normalize();
                float big = 1f / (1f + ext * 0.35f);
                g.pieces.Add(new Piece { t = c, half = Mathf.Min(ext, 3f) * 0.5f, vel = (outDir * UnityEngine.Random.Range(3f, 10f) * big + Vector3.up * UnityEngine.Random.Range(4f, 11f) * big) * boost, ang = UnityEngine.Random.insideUnitSphere * 160f * big });
            }
            Emit(g.basePos + g.dir * g.height * 0.4f + Vector3.up * 2f, (int)(160 * dens), Brown, 14f, 8f, 0.9f);
            Emit(g.basePos + Vector3.up * g.height * 0.6f, (int)(80 * dens), Leaf, 10f, 6f, 0.7f);
        }

        public void Tick(float dt, int i, float u, bool hit, float water, float hue, float dens)
        {
            if (fxGo == null) return;
            cool -= dt;
            if (i == 2 && hit && cool <= 0) { var g = NextPending(); Vector3 p = g != null ? g.basePos : anchor + new Vector3(UnityEngine.Random.Range(-25f, 25f), 0, UnityEngine.Random.Range(-10f, 35f)); Strike(p, hue); if (g != null) Topple(g, p + Vector3.up, 0.35f, dens); cool = 0.6f; }
            if (i == 3 && cool <= 0 && hit) { var g = NextPending(); if (g != null) { Strike(g.basePos, hue); Topple(g, anchor, 0.5f, dens); } cool = 1.3f; }
            if (!houseDone && (i > 3 || (i == 3 && u > 0.55f)))
            {
                houseDone = true;
                foreach (var g in groups) if (g.house && g.state == 0) { Strike(g.basePos + Vector3.up * 4f, hue); Topple(g, anchor, 3.5f, dens); }
            }
            if (i == 4 && hit && cool <= 0) { Launch(hue); cool = 0.8f; }
            if (i == 6 && cool <= 0)
            {
                var g = NextPending();
                if (g != null) { Strike(g.basePos, hue); if (++chain % 3 == 0) Explode(g, anchor, dens); else Topple(g, anchor, 0.2f, dens); }
                if (hit) Launch(hue);
                cool = 0.3f;
            }

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi]; if (g.state == 0) continue;
                g.timer += dt;
                if (g.state == 1)
                {
                    float amp = g.house ? 2.5f : 1.2f;
                    g.pivot.position = g.basePos + UnityEngine.Random.insideUnitSphere * 0.06f * amp;
                    g.pivot.rotation = Quaternion.Euler(UnityEngine.Random.Range(-amp, amp), 0, UnityEngine.Random.Range(-amp, amp));
                    if (UnityEngine.Random.value < 0.25f) Emit(g.basePos + Vector3.up * g.height * UnityEngine.Random.value, 2, Leaf, 3f, 1f, 0.5f);
                    if (g.timer >= g.shake) { g.state = 2; g.timer = 0; g.fallDur = Mathf.Min(4f, (g.house ? 2.2f : 1.1f) + g.height * 0.05f); g.pivot.position = g.basePos; }
                }
                else if (g.state == 2)
                {
                    float s = Mathf.Clamp01(g.timer / g.fallDur);
                    g.pivot.position = g.basePos; g.pivot.rotation = Quaternion.AngleAxis(92f * s * s, g.axis);
                    if (s >= 1f)
                    {
                        g.state = 3; g.timer = 0; Fallen++;
                        Vector3 hitP = g.basePos + g.dir * g.height * 0.8f;
                        Emit(hitP, (int)((g.house ? 120 : 55) * dens), Brown, 9f, 4f, 0.8f);
                        Emit(hitP, (int)(40 * dens), Leaf, 8f, 5f, 0.6f);
                        flashI = Mathf.Max(flashI, 3f); flash.transform.position = hitP + Vector3.up * 3f; flash.color = Fire;
                        if (Boom != null) Boom(hitP, g.house ? 18f : 4f);
                        if (g.house) Shatter(g, dens);
                    }
                }
                else
                {
                    g.yOff = Mathf.Lerp(g.yOff, water * (6f + 4f * Mathf.Sin(g.seed)), 1f - Mathf.Exp(-dt * 1.5f));
                    g.wob += dt * 0.6f;
                    if (g.pieces == null)
                    {
                        float bounce = g.timer < 0.7f ? 6f * Mathf.Exp(-g.timer * 7f) * Mathf.Cos(g.timer * 18f) : 0f;
                        float wa = Mathf.Min(1f, g.yOff / 3f) * 25f;
                        g.pivot.position = g.basePos + Vector3.up * g.yOff;
                        g.pivot.rotation = Quaternion.AngleAxis(Mathf.Sin(g.wob + g.seed) * wa, Vector3.up) * Quaternion.AngleAxis(92f - Mathf.Max(0, bounce), g.axis);
                    }
                    else
                    {
                        foreach (var p in g.pieces)
                        {
                            if (p.t == null) continue;
                            if (p.settled) { var q = p.t.position; q.y = p.baseY + g.yOff; p.t.position = q; continue; }
                            p.vel.y -= 20f * dt;
                            p.t.position += p.vel * dt;
                            p.t.rotation = Quaternion.Euler(p.ang * dt) * p.t.rotation;
                            if (p.t.position.y - p.half < g.basePos.y)
                            {
                                var q = p.t.position; q.y = g.basePos.y + p.half; p.t.position = q;
                                if (p.vel.y < -3f) { p.vel.y *= -0.3f; p.vel.x *= 0.6f; p.vel.z *= 0.6f; p.ang *= 0.5f; }
                                else { p.vel = Vector3.zero; p.ang = Vector3.zero; p.settled = true; p.baseY = q.y; }
                            }
                        }
                    }
                }
            }

            for (int k = 0; k < meteors.Length; k++)
            {
                if (!mact[k]) continue;
                meteors[k].transform.position += mvel[k] * dt;
                AstronomicalHazards.Strike(meteors[k].transform.position, mvel[k], 4.5f, 15f);
                if (marks != null && marks[k] != null) { marks[k].SetActive(true); marks[k].transform.position = new Vector3(mtarget[k].x, mtarget[k].y + 0.15f, mtarget[k].z); float ms = 6f + 1.5f * Mathf.Sin(Time.time * 14f); marks[k].transform.localScale = new Vector3(ms, 0.05f, ms); }
                if (meteors[k].transform.position.y <= mtarget[k].y + 0.5f)
                {
                    mact[k] = false; trails[k].emitting = false; meteors[k].SetActive(false); if (marks != null && marks[k] != null) marks[k].SetActive(false);
                    Impact(mtarget[k], 14f, dens);
                }
            }
            for (int k = 0; k < bolts.Length; k++)
            {
                if (boltAge[k] > 0.4f) { if (bolts[k].enabled) bolts[k].enabled = false; continue; }
                boltAge[k] += dt;
                Vector3 top = boltTarget[k] + new Vector3(0, 95f, 0), tg = boltTarget[k];
                for (int p = 0; p < 14; p++)
                {
                    float f = p / 13f; var pt = Vector3.Lerp(top, tg, f);
                    if (p > 0 && p < 13) pt += UnityEngine.Random.insideUnitSphere * 4f * Mathf.Sin(f * 3.14f);
                    bolts[k].SetPosition(p, pt);
                }
                bolts[k].widthMultiplier = 0.9f * (1f - boltAge[k] / 0.4f);
            }
            flashI = Mathf.Max(0f, flashI - dt * 25f);
            flash.intensity = flashI * AstronomicalSettings.Lighting.Value;
        }

        bool quiet;
        // Finale: every standing tree blows apart at once, with bolts and a huge fire burst.
        public void ExplodeAll(Vector3 c)
        {
            if (fxGo == null) return;
            quiet = true;
            foreach (var g in groups) if (g.state == 0) Explode(g, c, 1f);
            quiet = false;
            for (int k = 0; k < 4; k++) Strike(c + new Vector3(UnityEngine.Random.Range(-30f, 30f), 0, UnityEngine.Random.Range(-30f, 30f)), 0.1f);
            Emit(c + Vector3.up * 5f, 500, Fire, 45f, 22f, 2.6f);
            flashI = 30f; flash.transform.position = c + Vector3.up * 10f; flash.color = Color.white;
        }

        // Puts every tree back upright (seek backwards / restart).
        public void Reset()
        {
            foreach (var g in groups)
            {
                if (g.pieces != null) foreach (var p in g.pieces) if (p.t != null) UnityEngine.Object.Destroy(p.t.gameObject);
                if (g.pivot != null) UnityEngine.Object.Destroy(g.pivot.gameObject);
                g.pivot = null; g.pieces = null; g.state = 0; g.timer = 0; g.yOff = 0;
                if (fxGo != null && model != null) BuildStanding(g);
            }
            if (meteors != null) for (int k = 0; k < meteors.Length; k++) { mact[k] = false; if (meteors[k] != null) meteors[k].SetActive(false); trails[k].emitting = false; }
            if (bolts != null) for (int k = 0; k < bolts.Length; k++) { boltAge[k] = 9f; bolts[k].enabled = false; }
            if (marks != null) foreach (var mk in marks) if (mk != null) mk.SetActive(false);
            if (flash != null) flash.intensity = 0; flashI = 0;
            houseDone = false; pointer = 0; Fallen = 0; cool = 0;
        }

        public void Dispose()
        {
            groups.Clear();
            foreach (var o in ownedMats) if (o != null) UnityEngine.Object.Destroy(o);
            ownedMats.Clear();
            if (fxGo != null) UnityEngine.Object.Destroy(fxGo);
            fxGo = null;
        }
    }
}


