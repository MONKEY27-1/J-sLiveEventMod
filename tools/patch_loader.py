p = r'C:\Users\drago\Documents\AstronomicalEventMod\AstronomicalAssetLoader.cs'
s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert a in s, a[:60]
    s = s.replace(a, b, 1)
rep("                BuildAnims();\n                Measure();", "                foreach (var x in BuildAnims()) yield return x;\n                Measure();")
rep("            void BuildAnims()\n            {\n                var an = j[\"animations\"] as JNode; if (an == null) return;", "            IEnumerable BuildAnims()\n            {\n                var an = j[\"animations\"] as JNode; if (an == null) yield break;\n                int chCount = 0;")
rep("                        if (times.Length > 0) clip.Duration = Mathf.Max(clip.Duration, times[times.Length - 1]);\n                    }\n                    Result.Clips.Add(clip);", "                        if (times.Length > 0) clip.Duration = Mathf.Max(clip.Duration, times[times.Length - 1]);\n                        if (++chCount % 8 == 0) yield return null;       // a 500 s clip is ~40 MB of keys: parse it over several frames\n                    }\n                    Result.Clips.Add(clip);")
rep('var rest = m.FindClipExact("Travis_IntroIdle");', 'var rest = m.FindClipExact("Travis_IntroIdle") ?? m.FindClipExact("Travis_Show");')
rep("        public GltfModel Avatar, Stage, World, Tree;\n", "        public GltfModel Avatar, Stage, World, Tree;\n        public JNode Choreo;                // travis_choreo.json: the move log + seat windows written when the show clip was baked\n")
rep("            Done = true; Status = \"ready (music \"", "            try { var cp = Path.Combine(Dir, \"travis_choreo.json\"); if (File.Exists(cp)) Choreo = JNode.Parse(File.ReadAllText(cp)); }\n            catch (Exception e) { Problems.Add(\"choreo: \" + e.Message); }\n            Done = true; Status = \"ready (music \"")
open(p, 'w', encoding='utf-8').write(s)
print('patched', s.count('BuildAnims'), 'Choreo' in s)
