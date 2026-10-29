p = r'C:\Users\drago\Documents\AstronomicalEventMod\AstronomicalNet.cs'
s = open(p, encoding='utf-8').read()
a = s.index("        static void Handle("); b = s.index("        static long ReadLong(string s")
new = '''        static void Handle(AstronomicalEventManager mgr, string line)
        {
            var m = JNode.Parse(line);
            var id = (string)m["id"]; if (id != null) lastId = id;
            if ((string)m["event"] != "message") return;
            string raw = (string)m["message"]; var p = JNode.Parse(raw);
            string cmd = (string)p["cmd"], ev = (string)p["ev"], from = (string)p["from"], sig = (string)p["sig"];
            float cd = (float)p["cd"]; int seq = (int)p["seq"]; long ts = ReadLong(raw, "\\"ts\\":");     // 13-digit values: read from the text, floats would round them
            if (from == MyTag || sig != Sign(cmd, ev, cd, seq, ts)) return;
            double age = Math.Max(0.0, NowMs() / 1000.0 - ReadLong(line, "\\"time\\":"));                    // seconds since the relay received it
            Received++;
            if (cmd == "stop") { mgr.StopFromNet(ev); return; }
            if (cmd != "start") return;
            mgr.StartFromNet(ev, seq, cd - (float)age);
        }

'''
s = s[:a] + new + s[b:]
s = s.replace("try { Handle(mgr, JNode.Parse(line)); }", "try { Handle(mgr, line); }")
s = s.replace("        static long ReadLong(JNode m, string key) { return (long)(float)m[key]; }\n", "")
open(p, 'w', encoding='utf-8').write(s)
print('ok')
