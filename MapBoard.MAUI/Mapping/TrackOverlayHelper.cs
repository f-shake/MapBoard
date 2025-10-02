using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using MapBoard.IO.Formats.Gps;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = System.Drawing.Color;

namespace MapBoard.Mapping
{
    public class TrackOverlayHelper
    {
        MapPoint firstPoint = null;

        public TrackOverlayHelper(GraphicsOverlay overlay)
        {
            Overlay = overlay;
        }

        public GraphicsOverlay Overlay { get; }

        public void AddPoint(double x, double y, DateTime time, double speed, double? altitude)
        {
            MapPoint thisPoint = new MapPoint(x, y, SpatialReferences.Wgs84);
            double distance = 0;
            Polyline line;
            if (firstPoint == null) //第一个点
            {
                firstPoint = thisPoint;
                return;
            }
            else if (Overlay.Graphics.Count == 0) //第二个点
            {
                line = new Polyline(new MapPoint[] { firstPoint, thisPoint }, SpatialReferences.Wgs84);
                distance = GeometryUtility.GetDistance(firstPoint, thisPoint);
            }
            else //第三个及以上的点
            {
                //通过连接最新点、之前的点、之前之前的点共三个点，
                //避免仅连接两个点时会出现的小角度转弯时端点存在“缝隙”的小问题
                var lastLine = Overlay.Graphics[^1].Geometry as Polyline;
                var lastPoint = lastLine.Parts[0].Points[^1];
                var lastLastPoint = lastLine.Parts[0].Points[^2];
                line = new Polyline(new MapPoint[] { lastLastPoint, lastPoint, thisPoint }, SpatialReferences.Wgs84);
                distance = (double)Overlay.Graphics[^1].Attributes["Distance"] + GeometryUtility.GetDistance(lastPoint, thisPoint);
            }
            Graphic graphic = new Graphic(line);
            graphic.Attributes.Add("Speed", speed);
            graphic.Attributes.Add("Time", time);
            if (altitude.HasValue)
            {
                graphic.Attributes.Add("Altitude", altitude.Value);
            }
            graphic.Attributes.Add("Distance", distance);
            Overlay.Graphics.Add(graphic);
        }

        public void Clear()
        {
            Overlay.Graphics.Clear();
            firstPoint = null;
        }

        public string GpxFile { get; private set; }

        public async Task<Envelope> LoadColoredGpxAsync(GpxDocument gpx)
        {
            Clear();
            GpxFile = gpx.FilePath;
            await Task.Run(() =>
            {
                GpxUtility.LoadColoredGpx(gpx.Tracks[0].GetPoints(), Overlay.Graphics);
            });
            return Overlay.Extent;
        }

        public Polyline LoadLine(IEnumerable<MapPoint> points)
        {
            Clear();
            Polyline line = new Polyline(points, SpatialReferences.Wgs84);
            Overlay.Graphics.Add(new Graphic(line));
            return line;
        }

    }
}
