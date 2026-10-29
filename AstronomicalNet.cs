using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AstronomicalEvent
{
    // Cross-lobby sync between people who have this mod. Lobbies are separate Photon rooms, so the game's own networking cannot carry a message between them;
    // instead the mod uses a tiny public publish/subscribe relay (ntfy, default https://ntfy.sh): "start event X in N seconds" / "stop". Nothing else is
    // sent: no player name, id or position, only a random per-session tag. Messages are HMAC-signed with a shared key so random strangers cannot trigger it.
    // Players without the mod never see anything. Disable with [Sync] Broadcast / Receive = false, or point Server/Topic at your own relay.
    internal static class AstronomicalNet
    {
        const string Secret = "astro-event-sync-v1-7f3c19ab52e0";
        public static readonly string MyTag = Guid.NewGuid().ToString("N").Substring(0, 8);
        static string lastId = null;
        static float lastWarn;
        static long skewMs; // skewMs: relay clock minus this PC's clock (HTTP Date header), so an event started 90 s ago is found 90 s in even if the PC clock is off
        public static string Status = "idle";
        public static int Received;

        static string Url(string suffix)
        {
            return AstronomicalSettings.SyncServer.Value.TrimEnd('/') + "/" + AstronomicalSettings.SyncTopic.Value + suffix;
        }

        static string Sign(string cmd, string ev, float cd, int seq, long ts)
        {
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret)))
            {
                var m = h.ComputeHash(
                    Encoding.UTF8.GetBytes(
                        cmd + "|" + ev + "|" + cd.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" + seq + "|" + ts
                    )
                );
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++)
                    sb.Append(m[i].ToString("x2"));
                return sb.ToString();
            }
        }

        static long NowMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        static void Warn(string s)
        {
            if (Time.unscaledTime - lastWarn > 20f)
            {
                lastWarn = Time.unscaledTime;
                AstronomicalEventMod.Log.LogWarning("Sync: " + s);
            }
        }

        // ---- sending
        public static void Publish(string cmd, string ev, float cd, int seq)
        {
            if (!AstronomicalSettings.SyncBroadcast.Value || AstronomicalEventMod.Inst == null)
                return;
            AstronomicalEventMod.Inst.StartCoroutine(PublishCo(cmd, ev, cd, seq));
        }

        static IEnumerator PublishCo(string cmd, string ev, float cd, int seq)
        {
            long ts = NowMs();
            string body =
                "{\"v\":1,\"cmd\":\""
                + cmd
                + "\",\"ev\":\""
                + ev
                + "\",\"cd\":"
                + cd.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                + ",\"seq\":"
                + seq
                + ",\"ts\":"
                + ts
                + ",\"from\":\""
                + MyTag
                + "\",\"sig\":\""
                + Sign(cmd, ev, cd, seq, ts)
                + "\"}";
            var req = new UnityWebRequest(Url(""), "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            req.SetRequestHeader("Content-Type", "text/plain");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Status = "send failed";
                Warn("publish failed: " + req.error);
            }
            else
            {
                Status = "sent " + cmd;
                AstronomicalEventMod.Log.LogInfo("Sync: broadcast " + cmd + " " + ev);
            }
            req.Dispose();
        }

        // ---- receiving: poll the relay every few seconds (only while Receive is on)
        public static IEnumerator Poll(AstronomicalEventManager mgr)
        {
            yield return new WaitForSeconds(3f);
            while (true)
            {
                if (AstronomicalSettings.SyncReceive.Value)
                {
                    string url = Url("/json?poll=1&since=" + (lastId ?? "6m")); // first poll after starting the game looks back 6 min: an event already in progress is still joined
                    var req = UnityWebRequest.Get(url);
                    req.timeout = 8;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Status = "listening";
                        try
                        {
                            DateTime sd;
                            if (
                                DateTime.TryParse(
                                    req.GetResponseHeader("Date"),
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AdjustToUniversal
                                        | System.Globalization.DateTimeStyles.AssumeUniversal,
                                    out sd
                                )
                            )
                                skewMs = (long)(sd - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds - NowMs();
                        }
                        catch { }
                        string text = req.downloadHandler.text;
                        if (!string.IsNullOrEmpty(text))
                            foreach (var line in text.Split('\n'))
                            {
                                if (line.Length < 5)
                                    continue;
                                try
                                {
                                    Handle(mgr, line);
                                }
                                catch (Exception e)
                                {
                                    Warn("bad message: " + e.Message);
                                }
                            }
                    }
                    else
                    {
                        Status = "offline";
                        Warn("poll failed: " + req.error);
                    }
                    req.Dispose();
                }
                yield return new WaitForSecondsRealtime(4f);
            }
        }

        static void Handle(AstronomicalEventManager mgr, string line)
        {
            var m = JNode.Parse(line);
            var id = (string)m["id"];
            if (id != null)
                lastId = id;
            if ((string)m["event"] != "message")
                return;
            string raw = (string)m["message"];
            var p = JNode.Parse(raw);
            string cmd = (string)p["cmd"],
                ev = (string)p["ev"],
                from = (string)p["from"],
                sig = (string)p["sig"];
            float cd = (float)p["cd"];
            int seq = (int)p["seq"];
            long ts = ReadLong(raw, "\"ts\":"); // 13-digit values: read from the text, floats would round them
            if (from == MyTag || sig != Sign(cmd, ev, cd, seq, ts))
                return;
            double age = Math.Max(0.0, (NowMs() + skewMs) / 1000.0 - ReadLong(line, "\"time\":")); // seconds since the relay received it
            Received++;
            if (cmd == "kl")
            {
                AstronomicalKlombo.FromNet(ev, seq, cd, (float)age);
                return;
            }
            if (cmd == "vote")
            {
                mgr.VoteFromNet(ev, seq, (int)cd);
                return;
            }
            if (cmd == "winner")
            {
                mgr.WinnerFromNet(ev, seq);
                return;
            }
            if (cmd == "stop")
            {
                mgr.StopFromNet(ev);
                return;
            }
            if (cmd != "start")
                return;
            mgr.StartFromNet(ev, seq, cd - (float)age);
        }

        static long ReadLong(string s, string key)
        {
            int i = s.IndexOf(key);
            if (i < 0)
                return 0;
            i += key.Length;
            int j = i;
            while (j < s.Length && char.IsDigit(s[j]))
                j++;
            long v;
            return long.TryParse(s.Substring(i, j - i), out v) ? v : 0;
        }
    }
}
