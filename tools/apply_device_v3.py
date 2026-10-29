"""one-off: AstronomicalDevice.cs plays many clips for Jonesy (office) and Midas (orb), crossfading from one to the next"""
import os
HERE = os.path.dirname(os.path.abspath(__file__)); p = os.path.join(os.path.dirname(HERE), 'AstronomicalDevice.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, (s.count(a), a[:80])
    s = s.replace(a, b)
rep("midasClip = orbMidas.FindClipExact(\"Hover\") ?? (orbMidas.Clips.Count > 0 ? orbMidas.Clips[0] : null);",
    "midasClip = orbMidas.FindClipExact(\"C_Hover\") ?? (orbMidas.Clips.Count > 0 ? orbMidas.Clips[0] : null);\n                foreach (var c in orbMidas.Clips) { if (!c.Name.StartsWith(\"C_\")) continue; string n = c.Name.Substring(2); if (n == \"Hover\" || n == \"Float\" || n == \"Cross\" || n == \"Wings\" || n == \"OrbHold\" || n == \"Look\") midasCalm.Add(c); else midasHot.Add(c); }")
rep("jonesClip = jonesM.FindClipExact(\"Talk\") ?? (jonesM.Clips.Count > 0 ? jonesM.Clips[0] : null);",
    "jonesClip = jonesM.FindClipExact(\"J_Idle\") ?? (jonesM.Clips.Count > 0 ? jonesM.Clips[0] : null); foreach (var c in jonesM.Clips) if (c.Name.StartsWith(\"J_\")) jonesClips.Add(c);")
rep("if (midasClip != null) orbMidas.Sample(midasClip, t);", "PlayMidas(t);")
rep("if (jonesClip != null) jonesM.Sample(jonesClip, t);", "PlayJones(t);")
rep("GltfModel.Clip midasClip, jonesClip;", "GltfModel.Clip midasClip, jonesClip; readonly List<GltfModel.Clip> midasCalm = new List<GltfModel.Clip>(), midasHot = new List<GltfModel.Clip>(), jonesClips = new List<GltfModel.Clip>();")
rep("midasClip = jonesClip = null;", "midasClip = jonesClip = null; midasCalm.Clear(); midasHot.Clear(); jonesClips.Clear();")
rep("        // the one-off moments", """        // ---- many clips: a new one every few seconds, crossfaded (all clips share one channel layout). Jonesy picks from his office clips; Midas picks calm ones until the lightning starts, then energetic ones.
        static GltfModel.Clip PickFrom(List<GltfModel.Clip> list, int seg)
        {
            if (list.Count == 0) return null; int i = (int)(WorldKit.Hash(seg, 7, 31) * list.Count) % list.Count; if (list.Count > 1) { int pv = (int)(WorldKit.Hash(seg - 1, 7, 31) * list.Count) % list.Count; if (pv == i) i = (i + 1) % list.Count; } return list[i];
        }
        GltfModel.Clip PickMidas(int seg) { return PickFrom(OrbBolts(seg * 5f + 0.5f) > 0 && midasHot.Count > 0 ? midasHot : midasCalm.Count > 0 ? midasCalm : midasHot, seg); }
        void PlayMidas(float t)
        {
            const float L = 5f, X = 0.7f; int seg = (int)(t / L); float tin = t - seg * L; var a = PickMidas(seg); if (a == null) { if (midasClip != null) orbMidas.Sample(midasClip, t); return; }
            if (tin > L - X) { var b2 = PickMidas(seg + 1); if (b2 != null && b2 != a) { orbMidas.SampleBlend(a, t, b2, t, Sm(L - X, L, tin)); return; } } orbMidas.Sample(a, t);
        }
        void PlayJones(float t)
        {
            const float L = 6f, X = 0.7f; int seg = (int)(t / L); float tin = t - seg * L; var a = PickFrom(jonesClips, seg); if (a == null) { if (jonesClip != null) jonesM.Sample(jonesClip, t); return; }
            if (tin > L - X) { var b2 = PickFrom(jonesClips, seg + 1); if (b2 != null && b2 != a) { jonesM.SampleBlend(a, t, b2, t, Sm(L - X, L, tin)); return; } } jonesM.Sample(a, t);
        }
        // the one-off moments""")
open(p, 'w', encoding='utf-8').write(s); print('ok')
