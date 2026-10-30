using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AstronomicalEvent
{
    // Minimal runtime glTF/GLB loader (meshes, skins, TRS animation, base-colour textures) + MP3 loader.
    // Unity-side only: nothing here touches networking. Shortcut: no morph targets, sparse accessors, tangents or PBR maps.
    internal sealed class GltfModel
    {
        public sealed class Channel
        {
            public Transform T;
            public int Path;
            public float[] Times,
                Vals;
            public int Hint;
        }

        public sealed class Clip
        {
            public string Name;
            public float Duration;
            public List<Channel> Channels = new List<Channel>();
        }

        public GameObject Pivot; // feet-centred holder; scale this
        public GameObject Model;
        public List<Clip> Clips = new List<Clip>();
        public List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        public Transform[] Joints = new Transform[0];
        public float BoneHeight = 1f; // skeleton height at scale 1
        public Bounds RendererBounds; // world bounds at load (scale 1)

        public void Sample(Clip c, float t)
        {
            if (c == null || c.Duration <= 0)
                return;
            t = t % c.Duration;
            foreach (var ch in c.Channels)
            {
                var ts = ch.Times;
                int n = ts.Length;
                if (n == 0)
                    continue;
                int i = Mathf.Clamp(ch.Hint, 0, n - 1);
                while (i > 0 && ts[i] > t)
                    i--;
                while (i < n - 1 && ts[i + 1] <= t)
                    i++;
                ch.Hint = i;
                int j = Mathf.Min(i + 1, n - 1);
                float f = j == i ? 0 : Mathf.Clamp01((t - ts[i]) / (ts[j] - ts[i]));
                var v = ch.Vals;
                if (ch.Path == 1)
                {
                    var a = new Quaternion(v[i * 4], v[i * 4 + 1], v[i * 4 + 2], v[i * 4 + 3]);
                    var b = new Quaternion(v[j * 4], v[j * 4 + 1], v[j * 4 + 2], v[j * 4 + 3]);
                    ch.T.localRotation = Quaternion.Slerp(a, b, f);
                }
                else
                {
                    var a = new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
                    var b = new Vector3(v[j * 3], v[j * 3 + 1], v[j * 3 + 2]);
                    var r = Vector3.Lerp(a, b, f);
                    if (ch.Path == 0)
                        ch.T.localPosition = r;
                    else
                        ch.T.localScale = r;
                }
            }
        }

        static void Eval(Channel ch, float t, out Quaternion q, out Vector3 v)
        {
            q = Quaternion.identity;
            v = Vector3.zero;
            var ts = ch.Times;
            int n = ts.Length;
            if (n == 0)
                return;
            int i = Mathf.Clamp(ch.Hint, 0, n - 1);
            while (i > 0 && ts[i] > t)
                i--;
            while (i < n - 1 && ts[i + 1] <= t)
                i++;
            ch.Hint = i;
            int j = Mathf.Min(i + 1, n - 1);
            float f = j == i ? 0 : Mathf.Clamp01((t - ts[i]) / (ts[j] - ts[i]));
            var a = ch.Vals;
            if (ch.Path == 1)
                q = Quaternion.Slerp(
                    new Quaternion(a[i * 4], a[i * 4 + 1], a[i * 4 + 2], a[i * 4 + 3]),
                    new Quaternion(a[j * 4], a[j * 4 + 1], a[j * 4 + 2], a[j * 4 + 3]),
                    f
                );
            else
                v = Vector3.Lerp(new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]), new Vector3(a[j * 3], a[j * 3 + 1], a[j * 3 + 2]), f);
        }

        // Crossfade two clips that share the same channel layout (the Travis clips are built that way): w=0 -> a, w=1 -> b.
        public void SampleBlend(Clip a, float ta, Clip b, float tb, float w)
        {
            if (a == null || b == null || a.Duration <= 0 || b.Duration <= 0 || a.Channels.Count != b.Channels.Count)
            {
                Sample(b ?? a, tb);
                return;
            }
            ta %= a.Duration;
            tb %= b.Duration;
            for (int k = 0; k < a.Channels.Count; k++)
            {
                var ca = a.Channels[k];
                var cb = b.Channels[k];
                Quaternion qa,
                    qb;
                Vector3 va,
                    vb;
                Eval(ca, ta, out qa, out va);
                Eval(cb, tb, out qb, out vb);
                if (cb.Path == 1)
                    cb.T.localRotation = Quaternion.Slerp(qa, qb, w);
                else if (cb.Path == 0)
                    cb.T.localPosition = Vector3.Lerp(va, vb, w);
                else
                    cb.T.localScale = Vector3.Lerp(va, vb, w);
            }
        }

        public void Destroy()
        {
            foreach (var o in Owned)
                if (o != null)
                    UnityEngine.Object.Destroy(o);
            if (Pivot != null)
                UnityEngine.Object.Destroy(Pivot);
        }

        public Clip FindClip(params string[] partials)
        {
            if (Clips == null || Clips.Count == 0)
                return null;
            if (partials != null)
            {
                foreach (var p in partials)
                {
                    if (string.IsNullOrEmpty(p))
                        continue;
                    foreach (var c in Clips)
                        if (c.Name != null && c.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                            return c;
                }
            }
            return Clips[0];
        }

        public Clip FindClipExact(string name)
        {
            if (Clips == null || string.IsNullOrEmpty(name))
                return null;
            foreach (var c in Clips)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            return null;
        }
    }

    internal sealed class AstronomicalAssetLoader
    {
        public GltfModel Avatar,
            Stage,
            World,
            Tree;
        public GltfModel CoasterTrack,
            CoasterCart;
        public JNode CoasterPath; // Blender-built roller coaster (tools/make_coaster.py)

        public void Use(AstronomicalEventDef d)
        {
            Music = d.Music;
            Avatar = d.Avatar;
            Choreo = d.Choreo;
        }

        public JNode Choreo; // travis_choreo.json: the move log + seat windows written when the show clip was baked
        public AudioClip Music;
        public string Status = "idle";
        public bool Done;
        public readonly List<string> Problems = new List<string>();
        GameObject holder;
        public static string Dir;

        public IEnumerator LoadAll()
        {
            holder = new GameObject("AstronomicalAssets");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.SetActive(false);
            Status = "loading music";
            yield return LoadMusicTo(Path.Combine(Dir, "TravisScottConcert.mp3"), c => Music = c);
            Status = "loading world";
            yield return LoadGlb(Path.Combine(Dir, "astro_world.glb"), true, false, m => World = m);
            Status = "loading tree";
            yield return LoadGlb(Path.Combine(Dir, "tree.glb"), true, true, m => Tree = m);
            Status = "loading stage";
            yield return LoadGlb(Path.Combine(Dir, "stage.glb"), false, true, m => Stage = m);
            Status = "loading dancer";
            // This is the Blender-baked static mesh, not the former broken skinned rig.
            yield return LoadGlb(Path.Combine(Dir, "travis_avatar.glb"), true, true, m => Avatar = m);
            Status = "loading coaster";
            yield return LoadGlb(Path.Combine(Dir, "coaster_track.glb"), false, false, m => CoasterTrack = m);
            yield return LoadGlb(Path.Combine(Dir, "coaster_cart.glb"), false, false, m => CoasterCart = m);
            try
            {
                var pp = Path.Combine(Dir, "coaster_path.json");
                if (File.Exists(pp))
                    CoasterPath = JNode.Parse(File.ReadAllText(pp));
            }
            catch (Exception e)
            {
                Problems.Add("coaster path: " + e.Message);
            }
            try
            {
                var cp = Path.Combine(Dir, "travis_choreo.json");
                if (File.Exists(cp))
                    Choreo = JNode.Parse(File.ReadAllText(cp));
            }
            catch (Exception e)
            {
                Problems.Add("choreo: " + e.Message);
            }
            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId);
            if (bd != null)
            {
                bd.Music = Music;
                bd.Avatar = Avatar;
                bd.Choreo = Choreo;
                bd.Loaded = true;
            }
            Done = true;
            Status =
                "ready (music "
                + (Music != null)
                + ", stage "
                + (Stage != null)
                + ", dancer "
                + (Avatar != null)
                + ", world "
                + (World != null)
                + ")";
            AstronomicalEventMod.Log.LogInfo("Assets: " + Status);
        }

        // Loads the music / performer / choreography of an extra event (the built-in one is loaded by LoadAll).
        public IEnumerator LoadEvent(AstronomicalEventDef d)
        {
            if (d.Loaded || d.Loading)
                yield break;
            d.Loading = true;
            var bd = AstronomicalEvents.Find(AstronomicalEvents.BuiltinId);
            if (
                d.Kind == "showdown"
                || d.Kind == "rocket"
                || d.Kind == "end"
                || d.Kind == "unvault"
                || d.Kind == "concert"
                || d.Kind == "device"
                || d.Kind == "cube"
                || d.Kind == "cubelake"
                || d.Kind == "icestorm"
                || d.Kind == "newyear"
                || d.Kind == "butterfly"
                || d.Kind == "collision"
                || d.Kind == "end21"
            )
            {
                if (!string.IsNullOrEmpty(d.MusicFile))
                    yield return LoadMusicTo(Path.Combine(d.Folder, d.MusicFile), c => d.Music = c);
                d.ActorModels.Clear();
                for (int k = 0; k < d.ActorFiles.Count; k++)
                {
                    GltfModel gm = null;
                    Status = "loading " + d.ActorIds[k];
                    yield return LoadGlb(Path.Combine(d.Folder, d.ActorFiles[k]), true, true, m => gm = m);
                    d.ActorModels.Add(gm);
                }
                d.PropModels.Clear();
                for (int k = 0; k < d.PropFiles.Count; k++)
                {
                    GltfModel pm = null;
                    if (k < d.PropLazy.Count && d.PropLazy[k])
                    {
                        d.PropModels.Add(null);
                        continue;
                    }
                    Status = "loading " + d.PropIds[k];
                    yield return LoadGlb(Path.Combine(d.Folder, d.PropFiles[k]), true, k < d.PropLit.Count && d.PropLit[k], m => pm = m);
                    d.PropModels.Add(pm);
                }
                if (!string.IsNullOrEmpty(d.SwordFile))
                    yield return LoadGlb(Path.Combine(d.Folder, d.SwordFile), false, false, m => d.Sword = m);
                try
                {
                    if (!string.IsNullOrEmpty(d.CuesFile))
                        d.Cues = JNode.Parse(File.ReadAllText(Path.Combine(d.Folder, d.CuesFile)));
                }
                catch (Exception e)
                {
                    Problems.Add("cues " + d.Id + ": " + e.Message);
                }
                d.Loaded = true;
                d.Loading = false;
                Status = "ready";
                AstronomicalEventMod.Log.LogInfo("Event loaded: " + d.Id);
                yield break;
            }
            if (!string.IsNullOrEmpty(d.MusicFile))
                yield return LoadMusicTo(Path.Combine(d.Folder, d.MusicFile), c => d.Music = c);
            if (!string.IsNullOrEmpty(d.AvatarFile))
                yield return LoadGlb(Path.Combine(d.Folder, d.AvatarFile), true, true, m => d.Avatar = m);
            if (d.Avatar == null && bd != null)
                d.Avatar = bd.Avatar;
            try
            {
                if (!string.IsNullOrEmpty(d.ChoreoFile))
                {
                    var cp = Path.Combine(d.Folder, d.ChoreoFile);
                    if (File.Exists(cp))
                        d.Choreo = JNode.Parse(File.ReadAllText(cp));
                }
            }
            catch (Exception e)
            {
                Problems.Add("choreo " + d.Id + ": " + e.Message);
            }
            if (d.Choreo == null && d.Avatar == (bd != null ? bd.Avatar : null) && bd != null)
                d.Choreo = bd.Choreo;
            d.Loaded = true;
            d.Loading = false;
            AstronomicalEventMod.Log.LogInfo("Event loaded: " + d.Id);
        }

        // a standalone model (Klombo): textured and lit, loaded on demand
        // a lazy prop ("lazy": true in event.json), loaded when the scene asks for it
        public IEnumerator LoadProp(AstronomicalEventDef d, string id)
        {
            int k = d.PropIds.IndexOf(id);
            if (k < 0 || k >= d.PropModels.Count || d.PropModels[k] != null)
                yield break;
            GltfModel pm = null;
            yield return LoadGlb(Path.Combine(d.Folder, d.PropFiles[k]), true, k < d.PropLit.Count && d.PropLit[k], m => pm = m);
            d.PropModels[k] = pm;
        }

        public IEnumerator LoadExtra(string path, bool lit, Action<GltfModel> done)
        {
            return LoadGlb(path, true, lit, done);
        }

        IEnumerator LoadMusicTo(string path, Action<AudioClip> done)
        {
            if (!File.Exists(path))
            {
                Problems.Add("music missing: " + path);
                yield break;
            }
            var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG);
            ((DownloadHandlerAudioClip)req.downloadHandler).compressed = true;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Problems.Add("music failed: " + req.error);
                AstronomicalEventMod.Log.LogError("Music load failed: " + req.error);
                req.Dispose();
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            req.Dispose();
            if (clip != null)
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
                UnityEngine.Object.DontDestroyOnLoad(clip);
                AstronomicalEventMod.Log.LogInfo("Music loaded, " + clip.length + "s");
            }
            done(clip);
        }

        IEnumerator LoadGlb(string path, bool textures, bool lit, Action<GltfModel> done)
        {
            if (!File.Exists(path))
            {
                Problems.Add("missing: " + path);
                yield break;
            }
            byte[] data = null;
            Exception err = null;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                err = e;
            }
            if (err != null)
            {
                Problems.Add(path + ": " + err.Message);
                yield break;
            }
            yield return null;
            var b = new Builder(data, holder.transform, textures, lit);
            var it = b.Run();
            while (true)
            {
                bool more;
                try
                {
                    more = it.MoveNext();
                }
                catch (Exception e)
                {
                    Problems.Add(Path.GetFileName(path) + ": " + e.Message);
                    AstronomicalEventMod.Log.LogError(e);
                    yield break;
                }
                if (!more)
                    break;
                yield return null;
            }
            done(b.Result);
        }

        public void Park(GameObject pivot)
        {
            pivot.transform.SetParent(holder.transform, false);
        }

        public void Dispose()
        {
            if (Avatar != null)
                Avatar.Destroy();
            if (Stage != null)
                Stage.Destroy();
            if (World != null)
                World.Destroy();
            if (Tree != null)
                Tree.Destroy();
            if (Music != null)
                UnityEngine.Object.Destroy(Music);
            if (holder != null)
                UnityEngine.Object.Destroy(holder);
            Avatar = Stage = World = Tree = null;
            Music = null;
        }

        // ---- glTF builder ----
        sealed class Builder
        {
            readonly JNode j;
            readonly byte[] d;
            readonly int binOff;
            readonly bool tex,
                lit;
            readonly Transform parent;

            // reflection: the Span-based overloads of LoadImage cannot be resolved against the net472 reference set
            static readonly System.Reflection.MethodInfo LoadImg = typeof(ImageConversion).GetMethod(
                "LoadImage",
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }
            );
            public GltfModel Result = new GltfModel();
            GameObject[] nodes;
            Material[] mats;
            readonly Dictionary<int, Texture2D> texCache = new Dictionary<int, Texture2D>();

            public Builder(byte[] data, Transform p, bool textures, bool useLit)
            {
                d = data;
                parent = p;
                tex = textures;
                lit = useLit;
                if (BitConverter.ToUInt32(d, 0) != 0x46546C67)
                    throw new Exception("not a GLB");
                int jl = BitConverter.ToInt32(d, 12);
                j = JNode.Parse(System.Text.Encoding.UTF8.GetString(d, 20, jl));
                binOff = 20 + jl + 8; // skip BIN chunk header
            }

            static readonly int[] CompSize = { 0 };

            static int Comps(string t)
            {
                switch (t)
                {
                    case "SCALAR":
                        return 1;
                    case "VEC2":
                        return 2;
                    case "VEC3":
                        return 3;
                    case "VEC4":
                        return 4;
                    case "MAT4":
                        return 16;
                    default:
                        return 1;
                }
            }

            static int Sz(int ct)
            {
                switch (ct)
                {
                    case 5120:
                    case 5121:
                        return 1;
                    case 5122:
                    case 5123:
                        return 2;
                    default:
                        return 4;
                }
            }

            float[] Acc(int ai, out int comps)
            {
                var a = j["accessors"][ai];
                comps = Comps((string)a["type"]);
                int count = (int)a["count"];
                int ct = (int)a["componentType"];
                bool norm = a["normalized"] != null && (bool)a["normalized"];
                var r = new float[count * comps];
                if (a["bufferView"] == null)
                    return r;
                var bv = j["bufferViews"][(int)a["bufferView"]];
                int start =
                    binOff + (bv["byteOffset"] != null ? (int)bv["byteOffset"] : 0) + (a["byteOffset"] != null ? (int)a["byteOffset"] : 0);
                int sz = Sz(ct);
                int stride = bv["byteStride"] != null ? (int)bv["byteStride"] : comps * sz;
                for (int i = 0; i < count; i++)
                for (int c = 0; c < comps; c++)
                {
                    int p = start + i * stride + c * sz;
                    float v;
                    switch (ct)
                    {
                        case 5126:
                            v = BitConverter.ToSingle(d, p);
                            break;
                        case 5121:
                            v = d[p];
                            if (norm)
                                v /= 255f;
                            break;
                        case 5123:
                            v = BitConverter.ToUInt16(d, p);
                            if (norm)
                                v /= 65535f;
                            break;
                        case 5120:
                            v = (sbyte)d[p];
                            if (norm)
                                v = Mathf.Max(v / 127f, -1f);
                            break;
                        case 5122:
                            v = BitConverter.ToInt16(d, p);
                            if (norm)
                                v = Mathf.Max(v / 32767f, -1f);
                            break;
                        default:
                            v = BitConverter.ToUInt32(d, p);
                            break;
                    }
                    r[i * comps + c] = v;
                }
                return r;
            }

            // glTF is right-handed, Unity left-handed: mirror X.
            static Matrix4x4 Mirror(Matrix4x4 m)
            {
                var s = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                return s * m * s;
            }

            public IEnumerator Run()
            {
                var jn = j["nodes"];
                nodes = new GameObject[jn.Count];
                for (int i = 0; i < jn.Count; i++)
                {
                    var g = new GameObject((string)jn[i]["name"] ?? ("node" + i));
                    nodes[i] = g;
                    var n = jn[i];
                    if (n["matrix"] != null)
                    {
                        var a = n["matrix"].Floats();
                        var m = new Matrix4x4();
                        for (int k = 0; k < 16; k++)
                            m[k % 4, k / 4] = a[k];
                        m = Mirror(m);
                        g.transform.localPosition = m.GetColumn(3);
                        g.transform.localRotation = m.rotation;
                        g.transform.localScale = m.lossyScale;
                    }
                    else
                    {
                        if (n["translation"] != null)
                        {
                            var t = n["translation"].Floats();
                            g.transform.localPosition = new Vector3(-t[0], t[1], t[2]);
                        }
                        if (n["rotation"] != null)
                        {
                            var r = n["rotation"].Floats();
                            g.transform.localRotation = new Quaternion(r[0], -r[1], -r[2], r[3]);
                        }
                        if (n["scale"] != null)
                        {
                            var s = n["scale"].Floats();
                            g.transform.localScale = new Vector3(s[0], s[1], s[2]);
                        }
                    }
                }
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(parent, false);
                Result.Pivot = pivot;
                var model = new GameObject("Model");
                model.transform.SetParent(pivot.transform, false);
                Result.Model = model;
                var roots = j["scenes"][(j["scene"] != null ? (int)j["scene"] : 0)]["nodes"];
                var isChild = new HashSet<int>();
                for (int i = 0; i < jn.Count; i++)
                    if (jn[i]["children"] != null)
                        foreach (var c in jn[i]["children"])
                        {
                            nodes[(int)c].transform.SetParent(nodes[i].transform, false);
                            isChild.Add((int)c);
                        }
                foreach (var r in roots)
                    nodes[(int)r].transform.SetParent(model.transform, false);

                BuildMaterials();
                int cnt = 0;
                for (int i = 0; i < jn.Count; i++)
                {
                    if (jn[i]["mesh"] == null)
                        continue;
                    AddMesh(i, jn[i]);
                    if (++cnt % 6 == 0)
                        yield return null;
                }
                if (tex)
                {
                    foreach (var x in LoadTextures())
                        yield return x;
                }
                foreach (var x in BuildAnims())
                    yield return x;
                Measure();
            }

            readonly List<Material> matList = new List<Material>();
            readonly Dictionary<int, int> matTex = new Dictionary<int, int>();

            static Shader Pick(params string[] names)
            {
                foreach (var n in names)
                {
                    var s = Shader.Find(n);
                    if (s != null)
                        return s;
                }
                throw new Exception("no shader");
            }

            void BuildMaterials()
            {
                var ms = j["materials"] as JNode;
                if (ms == null)
                    return;
                mats = new Material[ms.Count];
                var sh = tex
                    ? (
                        lit
                            ? Pick(
                                "Universal Render Pipeline/Lit",
                                "Universal Render Pipeline/Simple Lit",
                                "Universal Render Pipeline/Unlit",
                                "Sprites/Default"
                            )
                            : Pick("Universal Render Pipeline/Unlit", "Unlit/Texture", "Sprites/Default")
                    )
                    : Pick(
                        "Universal Render Pipeline/Lit",
                        "Universal Render Pipeline/Simple Lit",
                        "Universal Render Pipeline/Unlit",
                        "Sprites/Default"
                    );
                for (int i = 0; i < ms.Count; i++)
                {
                    string name = ((string)ms[i]["name"] ?? "").ToLowerInvariant();
                    string am = (string)ms[i]["alphaMode"];
                    // Foliage cards need their alpha channel; Sprites/Default is alpha-blended, double-sided and always present in the build.
                    bool cutout = tex && (name.Contains("leaves") || name.Contains("leaf") || (am != null && am != "OPAQUE"));
                    var m = new Material(cutout ? Pick("Sprites/Default", sh.name) : sh);
                    m.SetFloat("_Cull", 0f);
                    var pbr = ms[i]["pbrMetallicRoughness"];
                    Color c = Color.white;
                    if (pbr != null && pbr["baseColorFactor"] != null)
                    {
                        var f = pbr["baseColorFactor"].Floats();
                        c = new Color(f[0], f[1], f[2], 1);
                    }
                    else if (ms[i]["emissiveFactor"] != null)
                    {
                        var ef = ms[i]["emissiveFactor"].Floats();
                        c = new Color(ef[0], ef[1], ef[2], 1);
                    }
                    if (!tex)
                    {
                        if (name.Contains("head"))
                            c = new Color(0.48f, 0.32f, 0.22f);
                        else if (name.Contains("face"))
                            c = new Color(0.85f, 0.65f, 0.2f);
                        else if (name.Contains("light_blue"))
                            c = new Color(0.2f, 0.85f, 1f);
                        else if (name.Contains("bahan"))
                            c = new Color(0.75f, 0.75f, 0.8f);
                        else if (name.Contains("alas"))
                            c = new Color(0.12f, 0.12f, 0.15f);
                        else
                            c = new Color(0.16f, 0.1f, 0.09f);
                        m.SetFloat("_Smoothness", 0.35f);
                    }
                    m.color = c;
                    if (ms[i]["emissiveFactor"] != null && !cutout)
                    {
                        var ef = ms[i]["emissiveFactor"].Floats();
                        float es = 1f;
                        var ex = ms[i]["extensions"];
                        var ees = ex != null ? ex["KHR_materials_emissive_strength"] : null;
                        if (ees != null && ees["emissiveStrength"] != null)
                            es = (float)ees["emissiveStrength"];
                        if (es > 0f)
                        {
                            m.EnableKeyword("_EMISSION");
                            m.SetColor("_EmissionColor", new Color(ef[0] * es, ef[1] * es, ef[2] * es));
                        }
                    }
                    if (tex && pbr != null && pbr["baseColorTexture"] != null)
                        matTex[i] = (int)pbr["baseColorTexture"]["index"];
                    mats[i] = m;
                    Result.Owned.Add(m);
                }
            }

            IEnumerable<object> LoadTextures()
            {
                foreach (var kv in matTex)
                {
                    int ti = kv.Value;
                    if (!texCache.ContainsKey(ti))
                    {
                        var img = j["images"][(int)j["textures"][ti]["source"]];
                        var bv = j["bufferViews"][(int)img["bufferView"]];
                        int off = binOff + (bv["byteOffset"] != null ? (int)bv["byteOffset"] : 0),
                            len = (int)bv["byteLength"];
                        var bytes = new byte[len];
                        Buffer.BlockCopy(d, off, bytes, 0, len);
                        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                        if ((bool)LoadImg.Invoke(null, new object[] { t, bytes, false }))
                        {
                            if (t.width > 1024 || t.height > 1024)
                            {
                                float k = 1024f / Mathf.Max(t.width, t.height);
                                int w = Mathf.Max(4, (int)(t.width * k)),
                                    h = Mathf.Max(4, (int)(t.height * k));
                                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                                Graphics.Blit(t, rt);
                                var prev = RenderTexture.active;
                                RenderTexture.active = rt;
                                var t2 = new Texture2D(w, h, TextureFormat.RGBA32, true);
                                t2.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                                t2.Apply(true, true);
                                RenderTexture.active = prev;
                                RenderTexture.ReleaseTemporary(rt);
                                UnityEngine.Object.Destroy(t);
                                t = t2;
                            }
                            else
                                t.Apply(true, true);
                        }
                        texCache[ti] = t;
                        Result.Owned.Add(t);
                        yield return null;
                    }
                    mats[kv.Key].mainTexture = texCache[ti];
                }
            }

            void AddMesh(int ni, JNode n)
            {
                var prims = j["meshes"][(int)n["mesh"]]["primitives"];
                JNode skin = n["skin"] != null ? j["skins"][(int)n["skin"]] : null;
                for (int pi = 0; pi < prims.Count; pi++)
                {
                    var p = prims[pi];
                    if (p["mode"] != null && (int)p["mode"] != 4)
                        continue;
                    var at = p["attributes"];
                    int c;
                    var pos = Acc((int)at["POSITION"], out c);
                    int vc = pos.Length / 3;
                    var verts = new Vector3[vc];
                    for (int i = 0; i < vc; i++)
                        verts[i] = new Vector3(-pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]);
                    var mesh = new Mesh { name = "gltfMesh" };
                    if (vc > 65535)
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.vertices = verts;
                    if (at["NORMAL"] != null)
                    {
                        var nr = Acc((int)at["NORMAL"], out c);
                        var nn = new Vector3[vc];
                        for (int i = 0; i < vc; i++)
                            nn[i] = new Vector3(-nr[i * 3], nr[i * 3 + 1], nr[i * 3 + 2]);
                        mesh.normals = nn;
                    }
                    if (at["TEXCOORD_0"] != null)
                    {
                        var uv = Acc((int)at["TEXCOORD_0"], out c);
                        var uu = new Vector2[vc];
                        for (int i = 0; i < vc; i++)
                            uu[i] = new Vector2(uv[i * 2], 1 - uv[i * 2 + 1]);
                        mesh.uv = uu;
                    }
                    int[] idx;
                    if (p["indices"] != null)
                    {
                        var f = Acc((int)p["indices"], out c);
                        idx = new int[f.Length];
                        for (int i = 0; i + 2 < f.Length; i += 3)
                        {
                            idx[i] = (int)f[i];
                            idx[i + 1] = (int)f[i + 2];
                            idx[i + 2] = (int)f[i + 1];
                        }
                    }
                    else
                    {
                        idx = new int[vc];
                        for (int i = 0; i + 2 < vc; i += 3)
                        {
                            idx[i] = i;
                            idx[i + 1] = i + 2;
                            idx[i + 2] = i + 1;
                        }
                    }
                    mesh.triangles = idx;
                    if (at["NORMAL"] == null)
                        mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    Result.Owned.Add(mesh);
                    var target = prims.Count == 1 ? nodes[ni] : new GameObject("prim" + pi);
                    if (prims.Count != 1)
                        target.transform.SetParent(nodes[ni].transform, false);
                    Material mat = p["material"] != null && mats != null ? mats[(int)p["material"]] : null;
                    if (skin != null && at["JOINTS_0"] != null)
                    {
                        var jo = Acc((int)at["JOINTS_0"], out c);
                        var we = Acc((int)at["WEIGHTS_0"], out c);
                        var bw = new BoneWeight[vc];
                        for (int i = 0; i < vc; i++)
                            bw[i] = new BoneWeight
                            {
                                boneIndex0 = (int)jo[i * 4],
                                boneIndex1 = (int)jo[i * 4 + 1],
                                boneIndex2 = (int)jo[i * 4 + 2],
                                boneIndex3 = (int)jo[i * 4 + 3],
                                weight0 = we[i * 4],
                                weight1 = we[i * 4 + 1],
                                weight2 = we[i * 4 + 2],
                                weight3 = we[i * 4 + 3],
                            };
                        mesh.boneWeights = bw;
                        var js = skin["joints"];
                        var bones = new Transform[js.Count];
                        for (int i = 0; i < bones.Length; i++)
                            bones[i] = nodes[(int)js[i]].transform;
                        var ib = Acc((int)skin["inverseBindMatrices"], out c);
                        var bp = new Matrix4x4[bones.Length];
                        for (int i = 0; i < bp.Length; i++)
                        {
                            var m = new Matrix4x4();
                            for (int k = 0; k < 16; k++)
                                m[k % 4, k / 4] = ib[i * 16 + k];
                            bp[i] = Mirror(m);
                        }
                        mesh.bindposes = bp;
                        var smr = target.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.bones = bones;
                        smr.rootBone = bones[0];
                        smr.sharedMaterial = mat;
                        smr.updateWhenOffscreen = true;
                        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        smr.receiveShadows = false;
                        Result.Joints = bones;
                    }
                    else
                    {
                        target.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mr = target.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                    }
                }
            }

            IEnumerable BuildAnims()
            {
                var an = j["animations"] as JNode;
                if (an == null)
                    yield break;
                int chCount = 0;
                var timeCache = new Dictionary<int, float[]>();
                foreach (var a in an)
                {
                    var clip = new GltfModel.Clip { Name = (string)a["name"] };
                    var samplers = a["samplers"];
                    foreach (var ch in a["channels"])
                    {
                        string path = (string)ch["target"]["path"];
                        int pth =
                            path == "translation" ? 0
                            : path == "rotation" ? 1
                            : path == "scale" ? 2
                            : -1;
                        if (pth < 0 || ch["target"]["node"] == null)
                            continue;
                        var s = samplers[(int)ch["sampler"]];
                        int c;
                        int ia = (int)s["input"];
                        if (!timeCache.TryGetValue(ia, out var times))
                        {
                            times = Acc(ia, out c);
                            timeCache[ia] = times;
                        }
                        var vals = Acc((int)s["output"], out c);
                        if (pth == 0)
                            for (int i = 0; i + 2 < vals.Length; i += 3)
                                vals[i] = -vals[i];
                        else if (pth == 1)
                            for (int i = 0; i + 3 < vals.Length; i += 4)
                            {
                                vals[i + 1] = -vals[i + 1];
                                vals[i + 2] = -vals[i + 2];
                            }
                        clip.Channels.Add(
                            new GltfModel.Channel
                            {
                                T = nodes[(int)ch["target"]["node"]].transform,
                                Path = pth,
                                Times = times,
                                Vals = vals,
                            }
                        );
                        if (times.Length > 0)
                            clip.Duration = Mathf.Max(clip.Duration, times[times.Length - 1]);
                        if (++chCount % 8 == 0)
                            yield return null; // a 500 s clip is ~40 MB of keys: parse it over several frames
                    }
                    Result.Clips.Add(clip);
                }
            }

            // Centre the pivot on the lowest bone / skeleton midpoint so scale and placement are predictable.
            void Measure()
            {
                var m = Result;
                // Do not sample an arbitrary first clip here: exported GLBs can retain animation
                // donor clips whose channels target a removed hierarchy.
                if (m.Joints.Length > 0 && m.Clips.Count > 0)
                {
                    var rest = m.FindClipExact("Travis_IntroIdle") ?? m.FindClipExact("Travis_Show");
                    if (rest != null)
                        m.Sample(rest, 0f);
                }
                var b = new Bounds();
                bool first = true;
                foreach (var mf in m.Model.GetComponentsInChildren<MeshFilter>(true)) // manual: Renderer.bounds is unreliable while inactive
                {
                    var mb = mf.sharedMesh.bounds;
                    var l2w = mf.transform.localToWorldMatrix;
                    for (int k = 0; k < 8; k++)
                    {
                        var corner = l2w.MultiplyPoint3x4(
                            mb.center
                                + Vector3.Scale(mb.extents, new Vector3((k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1))
                        );
                        if (first)
                        {
                            b = new Bounds(corner, Vector3.zero);
                            first = false;
                        }
                        else
                            b.Encapsulate(corner);
                    }
                }
                if (m.Joints.Length > 0)
                {
                    var jb = new Bounds(m.Joints[0].position, Vector3.zero);
                    foreach (var jt in m.Joints)
                        jb.Encapsulate(jt.position);
                    // Skinned exports use arbitrary units (this skin is ~17,000 units tall). Normalise the skeleton to 1.8 units so
                    // later scaling is a sane multiplier; feet at the pivot, centred in X/Z.
                    float norm = 1.8f / Mathf.Max(0.001f, jb.size.y);
                    m.Model.transform.localScale = Vector3.one * norm;
                    m.Model.transform.localPosition = new Vector3(-jb.center.x, -jb.min.y, -jb.center.z) * norm;
                    m.BoneHeight = 1.8f;
                    b = new Bounds(Vector3.zero, jb.size * norm);
                    first = false;
                }
                else if (!first)
                {
                    m.Model.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                }
                m.RendererBounds = b;
                AstronomicalEventMod.Log.LogInfo(
                    "glTF measured: size " + b.size + " joints " + m.Joints.Length + " clips " + m.Clips.Count
                );
            }
        }
    }
}
