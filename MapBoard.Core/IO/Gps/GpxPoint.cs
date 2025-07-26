using Esri.ArcGISRuntime.Geometry;
using FzLib;
using MapBoard;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace MapBoard.IO.Formats.Gps
{
    /// <summary>
    /// GPX trkpt对象
    /// </summary>
    public class GpxPoint : IGpxElement, INotifyPropertyChanged
    {
        public HashSet<string> HiddenElements => hiddenElements;

        private static readonly HashSet<string> hiddenElements = ["magvar", "geoidheight", "name", "cmt", "desc",
        "src","link","sym","type","fix","sat","hdop","vdop","pdop","ageofdgpsdata","dgpsid"];
        public GpxPoint(double x, double y, double? z, DateTime? time)
        {
            X = x;
            Y = y;
            Z = z;
            Time = time;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 其他属性
        /// </summary>
        public Dictionary<string, string> Extensions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 速度，需要计算后得出
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// 时间
        /// </summary>
        public DateTime? Time { get; set; }

        /// <summary>
        /// 经度
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// 纬度
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// 高程
        /// </summary>
        public double? Z { get; set; }

        public object Clone()
        {
            return new GpxPoint(X, Y, Z, Time)
            {
                Extensions = Extensions.ToDictionary(p => p.Key, p => p.Value)
            };
        }

        /// <summary>
        /// 转为ArcGIS的点
        /// </summary>
        /// <returns></returns>
        public MapPoint ToMapPoint()
        {
            if (!Z.HasValue)
            {
                throw new InvalidOperationException("指定的" + nameof(GpxPoint) + "没有高度信息");
            }
            return new MapPoint(X, Y, Z.Value, SpatialReferences.Wgs84);
        }

        /// <summary>
        /// 转为不含高程的ArcGIS的点
        /// </summary>
        /// <returns></returns>
        public MapPoint ToXYMapPoint()
        {
            return new MapPoint(X, Y, SpatialReferences.Wgs84);
        }
    }
}