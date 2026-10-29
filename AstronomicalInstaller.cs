using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AstronomicalEvent
{
    // "Install from repo": lists the event folders of the GitHub repo (AstronomicalAssets/events/<id>/...) and downloads the ones you pick into BepInEx/plugins/JsLiveEventsMod/AstronomicalAssets/events/<id>/.
    // Files whose size already matches are skipped, so it doubles as an updater. After an install the event list is rescanned (only while no event runs).
    internal static class AstronomicalInstaller
    {
        public const string Repo = "MONKEY27-1/J-sLiveEventMod", Branch = "main", Prefix = "AstronomicalAssets/events/";
        public static string Status = "";
        public static bool Busy;
        public static float Progress;
        public static readonly SortedDictionary<string, List<KeyValuePair<string, long>>> Remote = new SortedDictionary<string, List<KeyValuePair<string, long>>>();
        static string eventsDir { get { return Path.Combine(AstronomicalAssetLoader.Dir, "events"); } }
        public static bool Installed(string id) { return Directory.Exists(Path.Combine(eventsDir, id)) && File.Exists(Path.Combine(Path.Combine(eventsDir, id), "event.json")); }
        public static long TotalBytes(string id) { long t = 0; List<KeyValuePair<string, long>> l; if (Remote.TryGetValue(id, out l)) foreach (var f in l) t += f.Value; return t; }

        public static IEnumerator Refresh()
        {
            if (Busy) yield break; Busy = true; Status = "reading the repo list...";
            var req = UnityWebRequest.Get("https://api.github.com/repos/" + Repo + "/git/trees/" + Branch + "?recursive=1"); req.SetRequestHeader("User-Agent", "JsLiveEventsMod"); req.timeout = 30; yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { Status = "repo list failed: " + req.error; Busy = false; yield break; }
            try
            {
                var j = JNode.Parse(req.downloadHandler.text); var tree = j["tree"]; Remote.Clear();
                for (int i = 0; i < tree.Count; i++)
                {
                    var n = tree[i]; if ((string)n["type"] != "blob") continue; string p = (string)n["path"]; if (!p.StartsWith(Prefix)) continue;
                    string rest = p.Substring(Prefix.Length); int s = rest.IndexOf('/'); if (s <= 0) continue; string id = rest.Substring(0, s); long size = n["size"] != null ? (long)(float)n["size"] : 0;
                    List<KeyValuePair<string, long>> l; if (!Remote.TryGetValue(id, out l)) { l = new List<KeyValuePair<string, long>>(); Remote[id] = l; }
                    l.Add(new KeyValuePair<string, long>(rest.Substring(s + 1), size));
                }
                Status = Remote.Count + " events in the repo";
            }
            catch (Exception e) { Status = "repo list unreadable: " + e.Message; }
            Busy = false;
        }

        public static IEnumerator Install(string id, AstronomicalEventManager mgr)
        {
            if (Busy) yield break; List<KeyValuePair<string, long>> files; if (!Remote.TryGetValue(id, out files)) { Status = "unknown event " + id; yield break; }
            Busy = true; Progress = 0f; long total = TotalBytes(id), done = 0; int fail = 0;
            foreach (var f in files)
            {
                string dst = Path.Combine(Path.Combine(eventsDir, id), f.Key.Replace('/', Path.DirectorySeparatorChar));
                try { if (File.Exists(dst) && new FileInfo(dst).Length == f.Value) { done += f.Value; Progress = total > 0 ? (float)done / total : 1f; continue; } Directory.CreateDirectory(Path.GetDirectoryName(dst)); } catch { }
                Status = "downloading " + id + "/" + f.Key + " (" + (f.Value / 1048576f).ToString("0.0") + " MB)";
                var req = UnityWebRequest.Get("https://raw.githubusercontent.com/" + Repo + "/" + Branch + "/" + Prefix + id + "/" + Uri.EscapeUriString(f.Key)); req.timeout = 600; req.downloadHandler = new DownloadHandlerFile(dst + ".part") { removeFileOnAbort = true };
                var op = req.SendWebRequest(); while (!op.isDone) { Progress = total > 0 ? (done + (long)req.downloadedBytes) / (float)total : 0f; yield return null; }
                if (req.result != UnityWebRequest.Result.Success) { fail++; Status = "failed: " + f.Key + " - " + req.error; try { if (File.Exists(dst + ".part")) File.Delete(dst + ".part"); } catch { } continue; }
                try { if (File.Exists(dst)) File.Delete(dst); File.Move(dst + ".part", dst); } catch (Exception e) { fail++; Status = "could not save " + f.Key + ": " + e.Message; continue; }
                done += f.Value;
            }
            Progress = 1f; Busy = false;
            if (fail == 0)
            {
                Status = id + " installed";
                if (mgr != null && mgr.State == EventState.Idle && !mgr.CountingDown) { try { AstronomicalEvents.Scan(AstronomicalAssetLoader.Dir); Status = id + " installed - event list refreshed"; } catch { } }
                else Status = id + " installed - it appears after the running event stops or the game restarts";
            }
            AstronomicalEventMod.Log.LogInfo("Installer: " + Status);
        }
    }
}
