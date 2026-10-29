using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstronomicalEvent
{
    // Procedural world pieces for events that replace the map (The Unvaulting): terrain with a baked colour map, boxes with a window grid, frustums / cones, rocks, tree blobs,
    // lit (URP Lit) materials and a way to stamp colliders on the layer the real floor uses. Everything created here is added to `owned` so the scene can destroy it.
    internal sealed class WorldKit
    {
        public readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        public int Layer;
        static Shader lit,
            unlit,
            sprite;

        public WorldKit()
        {
            if (lit == null)
                lit =
                    Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                    ?? Shader.Find("Standard");
            if (unlit == null)
                unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            if (sprite == null)
                sprite = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        }

        // ---------------------------------------------------------------- materials
        public Material Lit(Color c, Texture2D tex = null, float smooth = 0.08f)
        {
            var m = new Material(lit != null ? lit : sprite) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            try
            {
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_SpecularHighlights", 0f);
                m.SetFloat("_EnvironmentReflections", 0f);
            }
            catch { }
            Owned.Add(m);
            return m;
        }

        public Material Glow(Color c, Texture2D tex = null, int queue = 3000)
        {
            var m = new Material(sprite) { color = c };
            if (tex != null)
                m.mainTexture = tex;
            m.renderQueue = queue;
            Owned.Add(m);
            return m;
        }

        public Material Flat(Color c)
        {
            var m = new Material(unlit != null ? unlit : sprite) { color = c };
            Owned.Add(m);
            return m;
        }

        public Texture2D Tex(int w, int h, Func<int, int, Color> f, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.wrapMode = wrap;
            t.Apply(true);
            Owned.Add(t);
            return t;
        }

        public Texture2D GlowTex()
        {
            return Tex(
                64,
                64,
                (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    return new Color(1, 1, 1, a * a);
                },
                TextureWrapMode.Clamp
            );
        }

        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        public static float VNoise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x),
                yi = Mathf.FloorToInt(y);
            float fx = x - xi,
                fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(xi, yi, s), Hash(xi + 1, yi, s), fx),
                Mathf.Lerp(Hash(xi, yi + 1, s), Hash(xi + 1, yi + 1, s), fx),
                fy
            );
        }

        public static float Fbm(float x, float y, int s, int oct = 4)
        {
            float a = 0f,
                amp = 0.5f,
                f = 1f;
            for (int i = 0; i < oct; i++)
            {
                a += VNoise(x * f, y * f, s + i) * amp;
                f *= 2f;
                amp *= 0.5f;
            }
            return a;
        }

        // ---------------------------------------------------------------- meshes
        public Mesh Keep(Mesh m)
        {
            Owned.Add(m);
            return m;
        }

        // heightfield (x, z in [-size/2, size/2] around 0), (n+1)^2 vertices, uv 0..1 over the whole area (for a baked colour map)
        public Mesh Terrain(int n, float size, Func<float, float, float> h)
        {
            var v = new Vector3[(n + 1) * (n + 1)];
            var uv = new Vector2[v.Length];
            var tr = new int[n * n * 6];
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = (i / (float)n - 0.5f) * size,
                    z = (j / (float)n - 0.5f) * size;
                v[j * (n + 1) + i] = new Vector3(x, h(x, z), z);
                uv[j * (n + 1) + i] = new Vector2(i / (float)n, j / (float)n);
            }
            int t = 0;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i,
                    b = a + 1,
                    c = a + n + 1,
                    d = c + 1;
                tr[t++] = a;
                tr[t++] = c;
                tr[t++] = b;
                tr[t++] = b;
                tr[t++] = c;
                tr[t++] = d;
            }
            var me = new Mesh { indexFormat = IndexFormat.UInt32 };
            me.vertices = v;
            me.uv = uv;
            me.triangles = tr;
            me.RecalculateNormals();
            me.RecalculateBounds();
            return Keep(me);
        }

        // box standing on y = 0 (centre x / z = 0); uv in tiles of `tile` metres (windows)
        public Mesh Box(Vector3 s, float tile = 4f)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tr = new List<int>();
            float hx = s.x / 2,
                hz = s.z / 2;
            Action<Vector3, Vector3, Vector3, float, float> quad = (o, u, w, lu, lw) =>
            {
                int b = v.Count;
                v.Add(o);
                v.Add(o + u);
                v.Add(o + u + w);
                v.Add(o + w);
                uv.Add(new Vector2(0, 0));
                uv.Add(new Vector2(lu / tile, 0));
                uv.Add(new Vector2(lu / tile, lw / tile));
                uv.Add(new Vector2(0, lw / tile));
                tr.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            };
            quad(new Vector3(-hx, 0, -hz), new Vector3(s.x, 0, 0), new Vector3(0, s.y, 0), s.x, s.y);
            quad(new Vector3(hx, 0, hz), new Vector3(-s.x, 0, 0), new Vector3(0, s.y, 0), s.x, s.y);
            quad(new Vector3(hx, 0, -hz), new Vector3(0, 0, s.z), new Vector3(0, s.y, 0), s.z, s.y);
            quad(new Vector3(-hx, 0, hz), new Vector3(0, 0, -s.z), new Vector3(0, s.y, 0), s.z, s.y);
            quad(new Vector3(-hx, s.y, -hz), new Vector3(s.x, 0, 0), new Vector3(0, 0, s.z), s.x, s.z);
            quad(new Vector3(-hx, 0, hz), new Vector3(s.x, 0, 0), new Vector3(0, 0, -s.z), s.x, s.z);
            var me = new Mesh();
            me.SetVertices(v);
            me.SetUVs(0, uv);
            me.SetTriangles(tr, 0);
            me.RecalculateNormals();
            me.RecalculateBounds();
            return Keep(me);
        }

        // frustum / cone standing on y = 0 (r1 = 0 -> cone); `wob` roughens the sides, `top` = flat top cap
        public Mesh Frustum(float r0, float r1, float h, int seg, float wob = 0f, int seed = 1, int rings = 1)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tr = new List<int>();
            for (int k = 0; k <= rings; k++)
            {
                float f = k / (float)rings;
                float r = Mathf.Lerp(r0, r1, f);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float rr = r * (1f + wob * (VNoise(Mathf.Cos(a) * 2f + 7, Mathf.Sin(a) * 2f + f * 5, seed) - 0.5f));
                    v.Add(
                        new Vector3(
                            Mathf.Cos(a) * rr,
                            f * h + wob * h * 0.2f * (VNoise(i * 0.7f, k * 1.3f, seed + 3) - 0.5f),
                            Mathf.Sin(a) * rr
                        )
                    );
                    uv.Add(new Vector2(i / (float)seg, f));
                }
            }
            for (int k = 0; k < rings; k++)
            for (int i = 0; i < seg; i++)
            {
                int a = k * (seg + 1) + i,
                    b = a + 1,
                    c = a + seg + 1,
                    d = c + 1;
                tr.AddRange(new[] { a, b, c, b, d, c });
            }
            var me = new Mesh();
            me.SetVertices(v);
            me.SetUVs(0, uv);
            me.SetTriangles(tr, 0);
            me.RecalculateNormals();
            me.RecalculateBounds();
            return Keep(me);
        }

        // lumpy ellipsoid (rocks, tree crowns): icosphere-ish via a lat / long sphere with noise
        public Mesh Blob(float rx, float ry, float rz, int seed, float rough = 0.25f, int lat = 10, int lon = 14)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tr = new List<int>();
            for (int i = 0; i <= lat; i++)
            for (int j = 0; j <= lon; j++)
            {
                float th = i * Mathf.PI / lat,
                    ph = j * Mathf.PI * 2f / lon;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                float k = 1f + rough * (VNoise(d.x * 2.3f + 11, d.y * 2.3f + d.z * 1.7f, seed) - 0.5f) * 2f;
                v.Add(new Vector3(d.x * rx * k, d.y * ry * k, d.z * rz * k));
                uv.Add(new Vector2(j / (float)lon, i / (float)lat));
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j,
                    b = a + 1,
                    c = a + lon + 1,
                    d2 = c + 1;
                tr.AddRange(new[] { a, c, b, b, c, d2 });
            }
            var me = new Mesh();
            me.SetVertices(v);
            me.SetUVs(0, uv);
            me.SetTriangles(tr, 0);
            me.RecalculateNormals();
            me.RecalculateBounds();
            return Keep(me);
        }

        public Mesh Combine(List<CombineInstance> parts)
        {
            var me = new Mesh { indexFormat = IndexFormat.UInt32 };
            me.CombineMeshes(parts.ToArray(), true, true);
            me.RecalculateBounds();
            return Keep(me);
        }

        // ---------------------------------------------------------------- objects
        public GameObject Obj(
            string name,
            Transform parent,
            Mesh m,
            Material mat,
            Vector3 pos,
            Quaternion rot,
            Vector3 scale,
            bool collider = false,
            bool convex = false
        )
        {
            var g = new GameObject(name);
            g.layer = Layer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.allowOcclusionWhenDynamic = false;
            if (collider)
            {
                var mc = g.AddComponent<MeshCollider>();
                mc.sharedMesh = m;
                mc.convex = convex;
            }
            return g;
        }

        public GameObject BoxCol(string name, Transform parent, Vector3 centre, Vector3 size, Quaternion rot)
        {
            var g = new GameObject(name);
            g.layer = Layer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = centre;
            g.transform.localRotation = rot;
            var c = g.AddComponent<BoxCollider>();
            c.size = size;
            return g;
        }

        public static CombineInstance Part(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            return new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) };
        }
    }

    // Unloads the real map around a point for the length of an event and puts it back afterwards: the renderers and the solid (non-trigger) colliders within a radius are switched off,
    // except the player, the other players' rigs, the camera and the event's own objects. Triggers stay on (zone detection needs them). Nothing is destroyed or moved.
    internal static class AstronomicalForestHider
    {
        static readonly List<Renderer> rends = new List<Renderer>();
        static readonly List<Collider> cols = new List<Collider>();
        public static bool Hidden;

        public static void Hide(Vector3 centre, float radius, Func<Transform, bool> ours)
        {
            if (Hidden)
                return;
            Hidden = true;
            rends.Clear();
            cols.Clear();
            int skipped = 0;
            try
            {
                var skip = new HashSet<Transform>();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<GorillaLocomotion.GTPlayer>())
                    skip.Add(p.transform);
                foreach (var p in UnityEngine.Object.FindObjectsOfType<VRRig>())
                    skip.Add(p.transform);
                foreach (var p in UnityEngine.Object.FindObjectsOfType<GorillaTagger>())
                    skip.Add(p.transform);
                foreach (var p in UnityEngine.Object.FindObjectsOfType<Camera>())
                    skip.Add(p.transform);
                // the map's own scene (where the gazebo is): other mods' objects (J CLIENT, ...) and the game's UI live elsewhere or carry their own names and are left alone
                UnityEngine.SceneManagement.Scene mapScene = default(UnityEngine.SceneManagement.Scene);
                bool haveScene = false;
                float bd = float.MaxValue;
                foreach (var tr in UnityEngine.Object.FindObjectsOfType<Transform>())
                {
                    if (tr.name.IndexOf("gazebo", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    float dd = (tr.position - centre).sqrMagnitude;
                    if (dd < bd)
                    {
                        bd = dd;
                        mapScene = tr.gameObject.scene;
                        haveScene = true;
                    }
                }
                Func<Transform, bool> keep = t =>
                {
                    if (haveScene && t.gameObject.scene != mapScene)
                        return true;
                    for (var q = t; q != null; q = q.parent)
                    {
                        if (skip.Contains(q))
                            return true;
                        string n = q.name;
                        if (
                            n.StartsWith("JClient", StringComparison.Ordinal)
                            || n.StartsWith("Astronomical", StringComparison.Ordinal)
                            || n.StartsWith("BepInEx", StringComparison.Ordinal)
                        )
                            return true;
                    }
                    return ours != null && ours(t);
                };
                float r2 = radius * radius;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (r == null || !r.enabled || r is ParticleSystemRenderer && false)
                        continue;
                    var c = r.bounds.center;
                    if ((c - centre).sqrMagnitude > r2 && r.bounds.size.sqrMagnitude < 40000f)
                        continue;
                    if (keep(r.transform))
                    {
                        skipped++;
                        continue;
                    }
                    r.enabled = false;
                    rends.Add(r);
                }
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
                {
                    if (c == null || !c.enabled || c.isTrigger)
                        continue;
                    if ((c.bounds.center - centre).sqrMagnitude > r2 && c.bounds.size.sqrMagnitude < 40000f)
                        continue;
                    if (keep(c.transform))
                    {
                        skipped++;
                        continue;
                    }
                    c.enabled = false;
                    cols.Add(c);
                }
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Forest unload: " + e.Message);
            }
            AstronomicalEventMod.Log.LogInfo(
                "Forest unloaded: " + rends.Count + " renderers, " + cols.Count + " colliders switched off (" + skipped + " kept)"
            );
        }

        public static void Restore()
        {
            if (!Hidden)
                return;
            Hidden = false;
            foreach (var r in rends)
            {
                try
                {
                    if (r != null)
                        r.enabled = true;
                }
                catch { }
            }
            foreach (var c in cols)
            {
                try
                {
                    if (c != null)
                        c.enabled = true;
                }
                catch { }
            }
            AstronomicalEventMod.Log.LogInfo("Forest restored: " + rends.Count + " renderers, " + cols.Count + " colliders");
            rends.Clear();
            cols.Clear();
        }
    }
}
