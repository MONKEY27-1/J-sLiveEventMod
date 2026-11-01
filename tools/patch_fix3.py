def rd(f): return open(f, encoding='utf-8').read()
def wr(f, s): open(f, 'w', encoding='utf-8').write(s)
def rep(s, a, n, cnt=1):
    assert a in s, 'missing: ' + a[:100]
    return s.replace(a, n, cnt)
# chair: lower seat, so the hips sit ON it
r = rd('tools/end21_rooms.py')
r = rep(r, "bx(0, 0.2, 3.25, 0.5, 0.4, 0.5, D_, 1.0); bx(0, 0.42, 3.22, 0.9, 0.1, 0.76, D_, 1.0, top=S_)", "bx(0, 0.15, 3.25, 0.5, 0.3, 0.5, D_, 1.0); bx(0, 0.29, 3.22, 0.9, 0.1, 0.76, D_, 1.0, top=S_)")
r = rep(r, "o.location = Z(0, 0.44, 3.4)", "o.location = Z(0, 0.34, 3.4)")
wr('tools/end21_rooms.py', r)
# smaller chamber
w = rd('tools/end21_world.py')
w = rep(w, "CR = 38.0;", "CR = 30.0;")
w = rep(w, "rwall(ccx, ccz, CR, 0.0, 31.0, 96,", "rwall(ccx, ccz, CR, 0.0, 26.0, 96,")
w = rep(w, "cylv(x, 14.0, z, 0.9, 28.0, MD, 8); cylv(x, 14.0, z, 0.95, 0.5, LPU, 8)", "cylv(x, 11.0, z, 0.9, 22.0, MD, 8); cylv(x, 11.0, z, 0.95, 0.5, LPU, 8)")
w = rep(w, "wreck_round(ccx, ccz, CR, 31.0, 90, 17,", "wreck_round(ccx, ccz, CR, 26.0, 90, 17,")
w = rep(w, "go.location = Bz(ccx, 31.0, ccz)", "go.location = Bz(ccx, 26.0, ccz)")
wr('tools/end21_world.py', w)
c = rd('AstronomicalEnd21c.cs')
c = rep(c, "Y_BASE + 39f, bChc.z + 33f,", "Y_BASE + 33f, bChc.z + 24f,")
c = rep(c, "Vector3 top = W(bChc + new Vector3(0f, 40f, 15f));", "Vector3 top = W(bChc + new Vector3(0f, 33f, 10f));")
add = '''            // Jones and the Foundation are at the Zero Point machine in the chamber too (8:30 - 9:13): Jones works the machine, the Foundation guards the entrance, both react to the Caretaker
            if (jonM != null && fouM != null && bPts.Count > 1)
            {
                float cx = bChc.x, cz = bChc.z + 6f, py = Y_BASE + 1.1f; var JC = NewAct("JonesC", jonM, 1f, true, true, 1f, T_CHAM + 1.5f, T_FLOOD + 3f); var FC = NewAct("FoundationC", fouM, 1f, true, true, 1f, T_CHAM + 1.5f, T_FLOOD + 3f);
                Action<Act, float, float, float, float, string> cw = (a, t, x, z, yw, cl) => Wp(a, t, x, py, z, yw, cl);
                if (JC != null) { cw(JC, T_CHAM + 1.5f, cx + 3.6f, cz - 1.5f, 0f, "Console"); JC.W[0].Cut = true; cw(JC, 534f, cx + 3.6f, cz - 1.5f, 0f, "Console"); cw(JC, 534.3f, cx + 3.6f, cz - 1.5f, 0f, "Look"); cw(JC, 540f, cx + 3.6f, cz - 1.5f, 0f, "Alert"); cw(JC, 544.6f, cx + 3.6f, cz - 1.5f, 0f, "Brace"); cw(JC, 546.9f, cx + 3.6f, cz - 1.5f, 0f, "Stagger"); cw(JC, 548.6f, cx + 3.6f, cz - 1.5f, 0f, "Brace"); cw(JC, 553f, cx + 3.6f, cz - 1.5f, 0f, "Brace"); }
                if (FC != null) { cw(FC, T_CHAM + 1.5f, cx - 3.8f, cz - 1.5f, 180f, "Ready"); FC.W[0].Cut = true; cw(FC, 536f, cx - 3.8f, cz - 1.5f, 180f, "Aim"); cw(FC, 538.5f, cx - 3.8f, cz - 1.5f, 15f, "Aim"); cw(FC, 544.6f, cx - 3.8f, cz - 1.5f, 15f, "Fire"); cw(FC, 548.4f, cx - 3.8f, cz - 1.5f, 15f, "Stagger"); cw(FC, 550.2f, cx - 3.8f, cz - 1.5f, 15f, "Brace"); cw(FC, 553f, cx - 3.8f, cz - 1.5f, 15f, "Brace");
                    FC.Aim = () => giantGo != null && giantGo.activeSelf ? giantGo.transform.position + Vector3.up * 14f : W(new Vector3(cx, py + 20f, cz)); }
            }
'''
c = rep(c, '            AstronomicalDebug.Anchors["facroom"]', add + '            AstronomicalDebug.Anchors["facroom"]')
wr('AstronomicalEnd21c.cs', c)
b = rd('AstronomicalEnd21b.cs')
b = rep(b, "        void OnScene(Sc from, Sc to, float t)\n        {", "        void OnScene(Sc from, Sc to, float t)\n        {\n            AstronomicalEventMod.Log.LogInfo(\"End21: scene \" + from + \" -> \" + to + \" at t=\" + t.ToString(\"0.0\") + \" ready=\" + p2Ready + \" head \" + HeadNow());")
wr('AstronomicalEnd21b.cs', b)
print('ok')
