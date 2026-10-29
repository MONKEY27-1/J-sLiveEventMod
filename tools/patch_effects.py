p = r'C:\Users\drago\Documents\AstronomicalEventMod\AstronomicalEffectsManager.cs'
s = open(p, encoding='utf-8').read()
a = s.index("            // Travis Scott: skinned model driven by the retargeted event dance")
b = s.index("            TickFestivalCity(t, hue, kick, fx);")
new = '''            // Travis Scott: one baked 500 s choreography (body, mouth, blinks, dreads/necklace physics) sampled at the event time. Nothing loops, switches or repeats.
            if (avatarModel != null && performerRoot != null)
            {
                float st = AstronomicalSettings.StageScale.Value, H0 = AstronomicalSettings.TravisScale.Value;
                float deckGrow = i == 0 ? 0f : (i == 1 ? Smooth(u * 1.4f) : 1f); // the stage rises out of the ground during sequence 1
                Vector3 deck = new Vector3(0, (stageModel != null ? 5.85f : 0.5f) * st * deckGrow, (stageModel != null ? 2.5f : 0f) * st);
                float height = H0, yaw = AstronomicalSettings.PerformerYaw.Value, glow = 1f;
                Vector3 pos = deck; bool visible = true;
                float tExplode = AstronomicalSequence.Start[6] + 0.86f * AstronomicalSequence.Duration[6];
                // The clip carries its own root motion (walking, hovering, swimming strokes) in body-heights, so the performer's size is capped: even at full size
                // that motion stays inside the playable area around the stage.
                const float MaxH = 14f;
                switch (i)
                {
                    case 0: // Intro: rises out of the stage floor, then breathes, looks around, opens his arms
                        visible = u > 0.3f;
                        { float em = Smooth((u - 0.3f) / 0.5f); pos = deck + Vector3.down * (H0 * 1.3f * (1f - em)); }
                        break;
                    case 1: // Stage: raps and walks the deck
                        break;
                    case 2: // Energy Surge: grows into a giant on the deck
                        { float g = Smooth(u * 1.6f); height = Mathf.Lerp(H0, Mathf.Min(MaxH, H0 * 3f), g); glow = 1.4f; }
                        break;
                    case 3: // Moon Rise: levitates skyward (the clip lifts him; the root climbs with it) and turns toward the moon
                        height = Mathf.Min(MaxH, H0 * 3f); pos = deck + new Vector3(Mathf.Sin(t * 0.2f) * 4f, 16f * Smooth(u * 1.5f), 4f * Smooth(u)); yaw += 110f * Smooth(Mathf.Min(1f, u * 2.5f));
                        break;
                    case 4: // Cosmic: colossal, floating among the stars, seated on small planets between floats
                        height = Mathf.Min(MaxH, H0 * 3.5f); pos = deck + new Vector3(Mathf.Sin(t * 0.12f) * 8f, 20f + Mathf.Sin(t * 0.3f) * 3f, Mathf.Cos(t * 0.12f) * 4f); yaw += 110f - 90f * Smooth(u * 3f) + Mathf.Sin(t * 0.2f) * 20f;
                        break;
                    case 5: // Underwater: swimming drift
                        height = Mathf.Min(MaxH * 0.85f, H0 * 2.5f); pos = deck + new Vector3(Mathf.Sin(t * 0.25f) * 6f, 9f + Mathf.Sin(t * 0.4f) * 2.5f, 5f); yaw += Mathf.Sin(t * 0.2f) * 40f; glow = 0.7f;
                        break;
                    default: // Finale: lands back on the stage, swells, and the slam hits exactly on the explosion
                        { float ch = Smooth((u - 0.4f) / 0.4f); height = Mathf.Lerp(Mathf.Min(MaxH * 0.85f, H0 * 2.5f), Mathf.Min(MaxH, H0 * 3.5f), ch);
                          float sinceStart = t - AstronomicalSequence.Start[6]; pos = deck + new Vector3(0, 9f * (1f - Smooth(sinceStart / 2.2f)), 4f * Smooth(sinceStart / 2.2f)); glow = 1f + 1.5f * ch; visible = u < 0.86f; }
                        break;
                }
                { Vector2 hp = new Vector2(pos.x, pos.z); if (hp.magnitude > 16f) { hp = hp.normalized * 16f; pos.x = hp.x; pos.z = hp.y; } }   // never leave the playable area around the stage
                if (!perfInit || back) { perfPos = pos; perfH = height; perfYaw = yaw; perfInit = true; }
                float ks = 1f - Mathf.Exp(-dt * 2.5f);
                perfPos = Vector3.Lerp(perfPos, pos, ks); perfH = Mathf.Lerp(perfH, height, ks); perfYaw = Mathf.LerpAngle(perfYaw, yaw, ks);
                if (performerRoot.activeSelf != visible) performerRoot.SetActive(visible);
                float tc = AstronomicalSequence.ChoreoTime(t);
                TickMoveLog(tc);
                if (visible)
                {
                    performerRoot.transform.localPosition = perfPos; performerRoot.transform.localRotation = Quaternion.Euler(0, perfYaw, 0);
                    avatarModel.Pivot.transform.localScale = Vector3.one * PerformerUnitScale(perfH * (1f + 0.03f * kick));
                    if (showClip != null) avatarModel.Sample(showClip, tc);
                    TickSeat(tc, perfH);
                    if (travisLight != null)
                    {
                        travisLight.transform.localPosition = new Vector3(0, perfH * 0.8f, -perfH * 0.9f);
                        travisLight.range = Mathf.Max(20f, perfH * 3f);
                        travisLight.color = i == 5 ? new Color(0.3f, 0.8f, 1f) : Color.Lerp(Color.white, c1, 0.5f);
                        travisLight.intensity = (3f + 9f * kick) * li * glow;
                    }
                }
                else if (seatOrb != null && seatOrb.gameObject.activeSelf) seatOrb.gameObject.SetActive(false);
            }
'''
s = s[:a] + new + s[b:]

# helper methods before PerformerUnitScale
helpers = '''        // Move log + seat windows written next to the show clip when it was baked (travis_choreo.json). Optional: without it only the seat orb and the move label are missing.
        void LoadChoreoWindows(JNode ch)
        {
            moveT0 = moveT1 = seatT0 = seatT1 = null; moveName = null; moveIdx = 0; CurrentMove = "";
            if (ch == null) return;
            try
            {
                var log = ch["log"]; int n = log != null ? log.Count : 0;
                moveT0 = new float[n]; moveT1 = new float[n]; moveName = new string[n];
                for (int k = 0; k < n; k++) { var e = log[k]; moveT0[k] = (float)e[0]; moveT1[k] = (float)e[1]; moveName[k] = (string)e[2]; }
                var ev = ch["events"]; var a0 = new List<float>(); var a1 = new List<float>();
                if (ev != null) for (int k = 0; k < ev.Count; k++) { var e = ev[k]; if ((string)e[2] == "seat") { a0.Add((float)e[0]); a1.Add((float)e[1]); } }
                seatT0 = a0.ToArray(); seatT1 = a1.ToArray();
            }
            catch (Exception e) { AstronomicalEventMod.Log.LogWarning("Choreography log unreadable: " + e.Message); moveName = null; seatT0 = seatT1 = null; }
        }

        void TickMoveLog(float tc)
        {
            if (moveName == null || moveName.Length == 0) return;
            if (moveIdx >= moveName.Length || tc < moveT0[Mathf.Clamp(moveIdx, 0, moveName.Length - 1)]) moveIdx = 0;
            while (moveIdx < moveName.Length - 1 && tc >= moveT1[moveIdx]) moveIdx++;
            CurrentMove = tc >= moveT0[moveIdx] ? moveName[moveIdx] : "";
        }

        // While the choreography has him seated, a small glowing planetoid appears under the pelvis (the pose itself is baked: thighs level, calves hanging).
        void TickSeat(float tc, float h)
        {
            if (seatOrb == null) return;
            bool seated = false;
            if (seatT0 != null) for (int k = 0; k < seatT0.Length; k++) if (tc >= seatT0[k] + 0.9f && tc <= seatT1[k] - 0.2f) { seated = true; break; }
            if (seated != seatOrb.gameObject.activeSelf) seatOrb.gameObject.SetActive(seated);
            if (!seated || pelvisJoint == null) return;
            float r = 0.17f * h;
            seatOrb.position = pelvisJoint.position + Vector3.down * (0.23f * h) + performerRoot.transform.forward * (-0.03f * h);
            seatOrb.localScale = Vector3.one * (2f * r);
        }

'''
k = s.index("        // The loader normalises the avatar so its skeleton is 1.8 units tall")
s = s[:k] + helpers + s[k:]
open(p, 'w', encoding='utf-8').write(s)
print('patched effects')
