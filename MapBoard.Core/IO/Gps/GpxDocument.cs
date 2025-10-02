using MapBoard.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;
using XmpCore.Impl;

namespace MapBoard.IO.Formats.Gps
{
    /// <summary>
    /// GPX数据类型
    /// </summary>
    public class GpxDocument : IGpxElement
    {
        public GpxDocument()
        {
        }

        public string Author { get; set; }

        public string Creator { get; set; }

        public string Description { get; set; }

        public double Distance { get; set; }

        private TimeSpan duration;
        public TimeSpan Duration
        {
            get
            {
                if(duration==default)
                {
                    if (Tracks.Count > 0
                     && Tracks[0].Segments.Count > 0
                     && Tracks[0].Segments[0].Points.Count > 0
                     && Tracks[0].Segments[0].Points[0].Time.HasValue && Tracks[^1].Segments[^1].Points[^1].Time.HasValue)
                    {
                        duration = Tracks[^1].Segments[^1].Points[^1].Time.Value- Tracks[0].Segments[0].Points[0].Time.Value;
                    }
                }
                return duration;
            }
            set => duration = value;
        }
        public Dictionary<string, string> Extensions { get; set; } = new Dictionary<string, string>();

        public string FilePath { get; internal set; }

        public string Name { get; set; }
        private DateTime time;
        public DateTime Time
        {
            get
            {
                if (time == default)
                {
                    if (Tracks.Count > 0
                        && Tracks[0].Segments.Count > 0
                        && Tracks[0].Segments[0].Points.Count > 0
                        && Tracks[0].Segments[0].Points[0].Time.HasValue)
                    {
                        time = Tracks[0].Segments[0].Points[0].Time.Value;
                    }
                }
                return time;
            }
            set => time = value;
        }
        public List<GpxTrack> Tracks { get; private set; } = new List<GpxTrack>();

        public HashSet<string> HiddenElements => hiddenElements;

        private static readonly HashSet<string> hiddenElements = ["copyright", "link", "keywords", "bounds"];

        public object Clone()
        {
            var info = MemberwiseClone() as GpxDocument;
            info.Tracks = Tracks.Select(p => p.Clone() as GpxTrack).ToList();
            return info;
        }

        public GpxTrack CreateTrack()
        {
            var track = new GpxTrack(this);
            Tracks.Add(track);
            return track;
        }


    }
}