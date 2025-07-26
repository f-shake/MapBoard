using System;
using System.Collections.Generic;
using System.Linq;

namespace MapBoard.IO.Formats.Gps
{
    /// <summary>
    /// GPX trkseg对象
    /// </summary>
    public class GpxSegment : IGpxElement
    {
        private static readonly HashSet<string> hiddenElements = [];

        internal GpxSegment(GpxTrack parent)
        {
            Parent = parent;
        }
        public Dictionary<string, string> Extensions { get; set; } = new Dictionary<string, string>();
        public GpxTrack Parent { get; private set; }

        /// <summary>
        /// 轨迹点集
        /// </summary>
        public IList<GpxPoint> Points { get; private set; } = new List<GpxPoint>();
        public HashSet<string> HiddenElements => hiddenElements;
        public object Clone()
        {
            var newObj = MemberwiseClone() as GpxSegment;
            newObj.Points = new List<GpxPoint>(Points.Select(p => p.Clone() as GpxPoint));
            return newObj;
        }

        object ICloneable.Clone()
        {
            throw new NotImplementedException();
        }
    }
}