"""one-off: AstronomicalDevice.cs uses the Blender office (prop 'office') and the new Agency (AgencyDeco node)"""
import os
HERE = os.path.dirname(os.path.abspath(__file__)); p = os.path.join(os.path.dirname(HERE), 'AstronomicalDevice.cs'); s = open(p, encoding='utf-8').read()
def rep(a, b):
    global s
    assert s.count(a) == 1, (s.count(a), a[:80])
    s = s.replace(a, b)
# agency deco
rep("agencyT = Node(agency, \"Agency\"); ruinsT", "decoT = Node(agency, \"AgencyDeco\"); agencyT = Node(agency, \"Agency\"); ruinsT")
rep("Transform agencyT, ruinsT, rubbleT,", "Transform decoT, agencyT, ruinsT, rubbleT,")
rep("            if (ruinsT != null && ruinsT.gameObject.activeSelf != ex) ruinsT.gameObject.SetActive(ex);", "            if (decoT != null && decoT.gameObject.activeSelf == ex) decoT.gameObject.SetActive(!ex);\n            if (ruinsT != null && ruinsT.gameObject.activeSelf != ex) ruinsT.gameObject.SetActive(ex);")
rep("agencyT = ruinsT = rubbleT =", "decoT = agencyT = ruinsT = rubbleT =")
# office from the Blender model
a = s.index("        void BuildOffice()"); b = s.index("        void BuildFlash()")
new = '''        GltfModel officeM; Material[] officeGlow;
        void BuildOffice()
        {
            office = new GameObject("AstronomicalOffice"); var T = office.transform; officeM = def.Prop("office");
            if (officeM != null && officeM.Pivot != null)
            {
                models.Add(officeM); officeM.Pivot.transform.SetParent(T, false); officeM.Pivot.SetActive(true); officeM.Pivot.transform.localPosition = Vector3.zero; officeM.Pivot.transform.localRotation = Quaternion.identity;
                foreach (var r in officeM.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
                foreach (var n in new[] { "Shell", "Furniture" }) MeshCol(Node(officeM, n));
                var gl = Node(officeM, "Glow"); if (gl != null) { var r = gl.GetComponentInChildren<Renderer>(true); if (r != null) officeGlow = r.sharedMaterials; }       // glow materials in slot order: 0 / 1 screens, 2 window, 3 ceiling panels, 4 lamp
            }
            else AstronomicalEventMod.Log.LogWarning("Device: office model missing");
            foreach (var p in new[] { new Vector3(0f, 2.7f, 1.6f), new Vector3(-2f, 2.7f, -1.2f), new Vector3(2f, 2.7f, -1.2f) }) { var l = new GameObject("OfficeLight").AddComponent<Light>(); l.transform.SetParent(T, false); l.transform.localPosition = p; l.type = LightType.Point; l.range = 10f; l.intensity = 1.5f * Mathf.Max(0.5f, Lit); l.color = new Color(1f, 0.96f, 0.88f); l.shadows = LightShadows.None; }
            officeFlash = new GameObject("OfficeStormLight").AddComponent<Light>(); officeFlash.transform.SetParent(T, false); officeFlash.transform.localPosition = new Vector3(3.9f, 1.5f, 0.4f); officeFlash.type = LightType.Point; officeFlash.range = 8f; officeFlash.color = new Color(0.6f, 0.5f, 1f); officeFlash.intensity = 0.5f; officeFlash.shadows = LightShadows.None;
            jonesM = def.Prop("jones");
            if (jonesM != null && jonesM.Pivot != null)
            {
                models.Add(jonesM); jonesM.Pivot.transform.SetParent(T, false); jonesM.Pivot.SetActive(true); jonesT = jonesM.Pivot.transform; float k = 1.8f / Mathf.Max(0.01f, jonesM.RendererBounds.size.y); var f = AstronomicalConcert.Facing(jonesM); jonesFace = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg + 180f;
                jonesT.localScale = Vector3.one * k; jonesT.localPosition = new Vector3(0f, 0.03f, 2.3f); jonesT.localRotation = Quaternion.Euler(0f, jonesFace, 0f); jonesClip = jonesM.FindClipExact("J_Idle") ?? (jonesM.Clips.Count > 0 ? jonesM.Clips[0] : null); foreach (var c in jonesM.Clips) if (c.Name.StartsWith("J_")) jonesClips.Add(c);
                foreach (var r in jonesM.Pivot.GetComponentsInChildren<Renderer>(true)) { r.allowOcclusionWhenDynamic = false; r.enabled = true; r.shadowCastingMode = ShadowCastingMode.Off; var sk = r as SkinnedMeshRenderer; if (sk != null) sk.updateWhenOffscreen = true; }
            }
            office.SetActive(false);
        }
'''
s = s[:a] + new + s[b:]
rep("            if (screenMat != null) screenMat.mainTextureOffset = new Vector2(0f, (t * 0.4f) % 1f);",
    "            if (officeGlow != null && officeGlow.Length >= 3) { float sf = 0.82f + 0.18f * Mathf.Sin(t * 23f) * Mathf.Sin(t * 3.1f); if (officeGlow[0] != null) officeGlow[0].color = new Color(sf, sf, sf, 1f); if (officeGlow[1] != null) officeGlow[1].color = new Color(sf, sf, sf, 1f); float wf = 0.75f + 0.25f * Mathf.Sin(t * 0.7f) + ((t * 0.31f) % 1f < 0.05f ? 0.6f : 0f); if (officeGlow[2] != null) officeGlow[2].color = new Color(wf, wf, wf, 1f); }")
rep("midasClip = jonesClip = null;", "midasClip = jonesClip = null; officeM = null; officeGlow = null;")
open(p, 'w', encoding='utf-8').write(s); print('ok')
