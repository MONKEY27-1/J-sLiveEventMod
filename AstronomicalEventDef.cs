using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AstronomicalEvent
{
    // One show: its music, performer clip, choreography log and section timings. The built-in "astronomical" event uses the files in AstronomicalAssets/;
    // more events can be added without recompiling by dropping a folder AstronomicalAssets/events/<id>/ with an event.json:
    //   { "id":"myshow", "name":"My Show", "music":"music.mp3", "avatar":"avatar.glb", "choreo":"choreo.json",
    //     "lead":0, "length":300, "bounds":[0,20,60,100,140,180,240,300], "names":["Intro","Stage","Surge","Moon","Cosmic","Water","Finale"] }
    // (avatar / choreo are optional: the built-in performer and choreography are reused. "bounds" are the 7 section starts plus the end, in seconds of the music.)
    internal sealed class AstronomicalEventDef
    {
        public string Id,
            Name,
            Folder,
            MusicFile,
            AvatarFile,
            ChoreoFile;
        public float Lead,
            Length;
        public float[] Bounds;
        public string[] SeqNames;
        public AudioClip Music;
        public GltfModel Avatar;
        public JNode Choreo;

        // showdown-style events: several baked actors + props + a cue list
        public string Kind = "travis",
            SwordFile,
            CuesFile,
            Date;
        public float Plateau = 24f;
        public List<string> ActorIds = new List<string>(),
            ActorFiles = new List<string>();
        public List<float> ActorHeights = new List<float>(),
            ActorAnkle = new List<float>();
        public List<GltfModel> ActorModels = new List<GltfModel>();
        public GltfModel Sword;
        public JNode Cues;
        public List<string> PropIds = new List<string>(),
            PropFiles = new List<string>();
        public List<bool> PropLit = new List<bool>(),
            PropLazy = new List<bool>();
        public List<GltfModel> PropModels = new List<GltfModel>();

        public GltfModel Prop(string id)
        {
            for (int k = 0; k < PropIds.Count && k < PropModels.Count; k++)
                if (PropIds[k] == id)
                    return PropModels[k];
            return null;
        }

        public bool Loaded,
            Loading,
            Builtin;
    }

    internal static class AstronomicalEvents
    {
        public static readonly List<AstronomicalEventDef> All = new List<AstronomicalEventDef>();
        public static AstronomicalEventDef Selected;
        public const string BuiltinId = "astronomical";

        public static AstronomicalEventDef Find(string id)
        {
            foreach (var e in All)
                if (string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase))
                    return e;
            return null;
        }

        public static void Scan(string dir)
        {
            All.Clear();
            const float lead = 28f,
                len = 500.952f + lead;
            var b = new AstronomicalEventDef
            {
                Id = BuiltinId,
                Name = "Travis Scott - Astronomical",
                Folder = dir,
                MusicFile = "TravisScottConcert.mp3",
                AvatarFile = "travis_avatar.glb",
                ChoreoFile = "travis_choreo.json",
                Lead = lead,
                Length = len,
                Bounds = new[] { 0f, lead + 27.5f, lead + 92f, lead + 162.5f, lead + 228f, lead + 348.5f, lead + 449.5f, len },
                SeqNames = new[]
                {
                    "Intro",
                    "Stage & Environment",
                    "Energy Surge",
                    "Moon Rise",
                    "Cosmic Environment",
                    "Underwater Environment",
                    "Finale",
                },
                Builtin = true,
            };
            All.Add(b);
            Selected = b;
            try
            {
                var evDir = Path.Combine(dir, "events");
                if (!Directory.Exists(evDir))
                    return;
                foreach (var sub in Directory.GetDirectories(evDir))
                {
                    try
                    {
                        var jf = Path.Combine(sub, "event.json");
                        if (!File.Exists(jf))
                            continue;
                        var j = JNode.Parse(File.ReadAllText(jf));
                        string id = (string)j["id"];
                        if (string.IsNullOrEmpty(id) || Find(id) != null)
                            continue;
                        var d = new AstronomicalEventDef
                        {
                            Id = id,
                            Name = (string)j["name"] ?? id,
                            Folder = sub,
                            MusicFile = (string)j["music"],
                            AvatarFile = (string)j["avatar"],
                            ChoreoFile = (string)j["choreo"],
                        };
                        d.Lead = j["lead"] != null ? (float)j["lead"] : 0f;
                        d.Length = j["length"] != null ? (float)j["length"] : 0f;
                        d.Bounds = j["bounds"] != null && j["bounds"].Count >= 8 ? j["bounds"].Floats() : null;
                        if (d.Bounds == null && d.Length > 30f)
                        {
                            d.Bounds = new float[8];
                            for (int k = 0; k < 8; k++)
                                d.Bounds[k] = d.Length * k / 7f;
                        }
                        if (d.Bounds == null || (string.IsNullOrEmpty(d.MusicFile) && d.Kind != "showdown"))
                        {
                            AstronomicalEventMod.Log.LogWarning("Event '" + id + "' needs music + length/bounds; skipped");
                            continue;
                        }
                        d.Length = d.Bounds[d.Bounds.Length - 1];
                        d.SeqNames = b.SeqNames;
                        if (j["names"] != null && j["names"].Count == d.Bounds.Length - 1)
                        {
                            var nm = new string[j["names"].Count];
                            for (int k = 0; k < nm.Length; k++)
                                nm[k] = (string)j["names"][k];
                            d.SeqNames = nm;
                        }
                        if (j["date"] != null)
                            d.Date = (string)j["date"];
                        if (j["kind"] != null)
                            d.Kind = (string)j["kind"];
                        if (j["plateau"] != null)
                            d.Plateau = (float)j["plateau"];
                        d.SwordFile = (string)j["sword"];
                        d.CuesFile = (string)j["cues"];
                        if (j["actors"] != null)
                            for (int k = 0; k < j["actors"].Count; k++)
                            {
                                var an = j["actors"][k];
                                d.ActorIds.Add((string)an["id"]);
                                d.ActorFiles.Add((string)an["glb"]);
                                d.ActorHeights.Add((float)an["height"]);
                                d.ActorAnkle.Add(an["ankle"] != null ? (float)an["ankle"] : 5f);
                            }
                        if (j["props"] != null)
                            for (int k = 0; k < j["props"].Count; k++)
                            {
                                var pn = j["props"][k];
                                d.PropIds.Add((string)pn["id"]);
                                d.PropFiles.Add((string)pn["glb"]);
                                d.PropLit.Add(pn["lit"] != null && (bool)pn["lit"]);
                                d.PropLazy.Add(pn["lazy"] != null && (bool)pn["lazy"]);
                            }
                        if (j["names"] != null && j["names"].Count == d.Bounds.Length - 1)
                        {
                            d.SeqNames = new string[j["names"].Count];
                            for (int k = 0; k < d.SeqNames.Length; k++)
                                d.SeqNames[k] = (string)j["names"][k];
                        }
                        All.Add(d);
                        AstronomicalEventMod.Log.LogInfo("Event found: " + id + " (" + d.Name + ")");
                    }
                    catch (Exception e)
                    {
                        AstronomicalEventMod.Log.LogWarning("Event folder " + sub + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Event scan failed: " + e.Message);
            }
            SortAll();
        }

        // the list runs from the oldest real event to the newest (stable; unknown ids, e.g. folders you add later, go last; an event.json "date": "yyyy-mm-dd" overrides this table)
        static readonly Dictionary<string, string> dates = new Dictionary<string, string>
        {
            { "rocketlaunch", "2018-06-29" },
            { "kevincube", "2018-08-03" },
            { "cubelake", "2018-09-27" },
            { "butterfly", "2018-11-04" },
            { "newyear", "2018-12-31" },
            { "icestorm", "2019-01-19" },
            { "marshmello", "2019-02-02" },
            { "unvaulting", "2019-06-08" },
            { "finalshowdown", "2019-08-01" },
            { "theend", "2019-10-13" },
            { BuiltinId, "2020-04-23" },
            { "thedevice", "2020-06-07" },
            { "collision", "2022-06-04" },
        };

        static void SortAll()
        {
            try
            {
                var sorted = All.OrderBy(
                        e =>
                        {
                            string d;
                            return e.Date ?? (dates.TryGetValue(e.Id, out d) ? d : "9999-12-31");
                        },
                        StringComparer.Ordinal
                    )
                    .ToList();
                All.Clear();
                All.AddRange(sorted);
            }
            catch (Exception e)
            {
                AstronomicalEventMod.Log.LogWarning("Event sort: " + e.Message);
            }
        }
    }
}
