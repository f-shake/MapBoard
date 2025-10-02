using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using MapBoard.IO.Formats.Gps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MapBoard.Util
{
    public static class GpxUtility
    {
        public static IList<GpxPoint> Clone(this IList<GpxPoint> points)
        {
            return points.Select(p => p.Clone() as GpxPoint).ToList();
        }

        public static double GetAverageSpeed(this IList<GpxPoint> points)
        {
            var time = points.GetDuration();
            if (time.HasValue)
            {
                return points.GetDistance() / time.Value.TotalSeconds;
            }
            throw new Exception("无法计算总时间");
        }

        public static double GetDistance(this IList<GpxPoint> points)
        {
            double distance = 0;
            MapPoint last = null;
            foreach (var point in points)
            {
                if (last != null)
                {
                    distance += GeometryUtility.GetDistance(last, point.ToXYMapPoint());
                }
                last = point.ToXYMapPoint();
            }

            return distance;
        }
        /// <summary>
        /// 点的包围盒范围
        /// </summary>
        public static Envelope GetExtent(this IList<GpxPoint> points)
        {
            double minX, minY, maxX, maxY;
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(point.X, minX);
                maxX = Math.Max(point.X, maxX);
                minY = Math.Min(point.Y, minY);
                maxY = Math.Max(point.Y, maxY);
            }

            return new Envelope(minX, maxY, maxX, minY, SpatialReferences.Wgs84);
        }

        /// <summary>
        /// 根据提供的采样率参数，获取最大速度
        /// </summary>
        /// <param name="window"></param>
        /// <param name="jump"></param>
        /// <returns></returns>
        public static double GetMaxSpeed(this IList<GpxPoint> points, int window = 9)
        {
            var speeds = GetMeanFilteredSpeeds(points, window, false).Where(p => p != double.PositiveInfinity);
            return speeds.Any() ? speeds.Max() : 0;
        }

        public static IList<double> GetMeanFilteredSpeeds(IList<GpxPoint> points, int window, bool writeToPoints)
        {
            if (window % 2 == 0 || window < 3)
            {
                throw new ArgumentException("窗口大小必须为大于等于3的奇数", nameof(window));
            }
            double[] speeds = new double[points.Count];
            double[] distances = new double[points.Count - 1];

            try
            {
                int left = 0;//左端索引
                int right = 2;//右端索引（包含，因此长度为right-left+1）
                MapPoint lastPoint = points[1].ToXYMapPoint();
                double distance = GeometryUtility.GetDistance(points[0].ToXYMapPoint(), points[1].ToXYMapPoint());
                distances[0] = distance;
                while (right == 0 || left < right)
                {
                    if ((right - left) % 2 == 0) //在左端或右端时，窗口大小可能为偶数，此时不进行处理
                    {
                        var currentPoint = points[right].ToXYMapPoint();
                        distances[right - 1] = GeometryUtility.GetDistance(lastPoint, currentPoint);
                        lastPoint = currentPoint;
                        distance += distances[right - 1];
                        var speed = distance / ((points[right].Time.Value.Ticks - points[left].Time.Value.Ticks) / 10_000_000.0);
                        int index = (right + left) / 2;
                        speeds[index] = speed;
                        //Debug.WriteLine($"left={left}, right={right}, distance={distance}, speed={speed}, index={index}");
                        if (writeToPoints)
                        {
                            points[index].Speed = speed;
                        }
                    }
                    if (right == points.Count - 1)//右端已经到底
                    {
                        distance -= distances[left];
                        left++;
                    }
                    else if (right - left + 1 == window)//到达窗口大小
                    {
                        distance -= distances[left];
                        left++;
                        right++;
                    }
                    else
                    {
                        Debug.Assert(left == 0);
                        right++;
                    }
                }
                speeds[0] = speeds[1];
                speeds[^1] = speeds[^2];
                if (writeToPoints)
                {
                    points[0].Speed = speeds[0];
                    points[^1].Speed = speeds[^1];
                }


            }
            catch (InvalidOperationException ex)
            {
                throw;
            }

            return speeds;
        }

        /// <summary>
        /// 获取移动均速
        /// </summary>
        /// <param name="speedDevaluation"></param>
        /// <returns></returns>
        public static double GetMovingAverageSpeed(this IList<GpxPoint> points, double speedDevaluation = 0.3)
        {
            double totalDistance = 0;
            double totalSeconds = 0;
            GpxPoint last = null;
            try
            {
                foreach (var point in points)
                {
                    if (last != null)
                    {
                        double distance = GeometryUtility.GetDistance(last.ToXYMapPoint(), point.ToXYMapPoint());
                        double second = (point.Time - last.Time).Value.TotalSeconds;
                        double speed = distance / second;
                        if (speed > speedDevaluation)
                        {
                            totalDistance += distance;
                            totalSeconds += second;
                        }
                    }
                    last = point;
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("存在没有时间信息的点", ex);
            }
            return totalDistance / totalSeconds;
        }

        /// <summary>
        /// 获取移动时间
        /// </summary>
        /// <param name="speedDevaluation"></param>
        /// <returns></returns>
        public static TimeSpan GetMovingTime(this IList<GpxPoint> points, double speedDevaluation = 0.3)
        {
            double totalDistance = 0;
            double totalSeconds = 0;
            GpxPoint last = null;
            try
            {
                foreach (var point in points)
                {
                    if (last != null)
                    {
                        double distance = GeometryUtility.GetDistance(last.ToXYMapPoint(), point.ToXYMapPoint());
                        double second = (point.Time - last.Time).Value.TotalSeconds;
                        double speed = distance / second;
                        if (speed > speedDevaluation)
                        {
                            totalDistance += distance;
                            totalSeconds += second;
                        }
                    }
                    last = point;
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("存在没有时间信息的点", ex);
            }
            return TimeSpan.FromSeconds(totalSeconds);
        }
        public static IList<GpxPoint> GetPoints(this GpxTrack trk)
        {
            if (trk.Segments.Count == 0)
            {
                return new List<GpxPoint>();
            }

            List<GpxPoint> points = new List<GpxPoint>();
            foreach (var seg in trk.Segments)
            {
                points.AddRange(seg.Points);
            }
            return points;
        }
        public static int GetPointsCount(this GpxTrack trk)
        {
            return trk.Segments.Select(p => p.Points.Count).Sum();
        }

        public static IList<GpxPoint> GetPoints(this GpxDocument gpx)
        {

            List<GpxPoint> points = new List<GpxPoint>();
            foreach (var trk in gpx.Tracks)
            {
                foreach (var seg in trk.Segments)
                {
                    points.AddRange(seg.Points);
                }
            }
            return points;
        }
        public static int GetPointsCount(this GpxDocument gpx)
        {
            return gpx.Tracks.Select(trk => trk.Segments.Select(p => p.Points.Count).Sum()).Sum();
        }

        /// <summary>
        /// 获取某一点附近的速度
        /// </summary>
        /// <param name="point">目标点</param>
        /// <param name="unilateralSampleCount">采样点数量，单侧</param>
        /// <returns></returns>
        public static double GetSpeed(this IList<GpxPoint> points, GpxPoint point, int unilateralSampleCount)
        {
            return points.GetSpeed(points.IndexOf(point), unilateralSampleCount);
        }

        /// <summary>
        /// 获取某一点附近的速度
        /// </summary>
        /// <param name="point">目标点在集合中的索引</param>
        /// <param name="unilateralSampleCount">采样点数量，单侧</param>
        /// <returns></returns>
        public static double GetSpeed(this IList<GpxPoint> points, int index, int unilateralSampleCount)
        {
            if (points.Count <= 1)
            {
                throw new GpxException("集合拥有的点过少");
            }

            int min = index - unilateralSampleCount;
            if (min < 0)
            {
                min = 0;
            }

            int max = index + unilateralSampleCount;
            if (max > points.Count - 1)
            {
                max = points.Count - 1;
            }
            double totalDistance = 0;
            TimeSpan totalTime = TimeSpan.Zero;

            try
            {
                for (int i = min; i < max; i++)
                {
                    totalDistance += GeometryUtility.GetDistance(points[i].ToXYMapPoint(), points[i + 1].ToXYMapPoint());
                    totalTime += points[i + 1].Time.Value - points[i].Time.Value;
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("存在没有时间信息的点", ex);
            }
            return totalDistance / totalTime.TotalSeconds;
        }

        public static TimeSpan? GetDuration(this IList<GpxPoint> points)
        {
            if (points.Count <= 1)
            {
                return TimeSpan.Zero;
            }
            if (points[0].Time.HasValue && points[^1].Time.HasValue)
            {
                return points[^1].Time.Value - points[0].Time.Value;
            }
            return null;
        }
        public static void LoadColoredGpx(IList<GpxPoint> points, GraphicCollection graphics)
        {
            if (points.Count <= 3)
            {
                throw new ArgumentException("点的数量过少，无法加载", nameof(points));
            }
            var speeds = GetMeanFilteredSpeeds(points, Parameters.GpxSpeedSmoothWindow, false);
            var orderedSpeeds = speeds.OrderBy(p => p).ToList();
            int speedsCount = speeds.Count;
            int maxIndex = 0;
            int minIndex = 0;
            double maxMinusMinSpeed = 0;
            if (speedsCount > 2)
            {
                maxIndex = Math.Max(1, (int)(speedsCount * 0.95));//取95%最大值作为速度颜色上限
                minIndex = Math.Min(speedsCount - 2, (int)(speedsCount * 0.15)); //取15%最小值作为速度颜色下限
                maxMinusMinSpeed = orderedSpeeds[maxIndex] - orderedSpeeds[minIndex];
            }
            double distance = 0;
            for (int i = 2; i < points.Count; i++)
            {
                MapPoint p1 = new MapPoint(points[i - 2].X, points[i - 2].Y, points[i - 2].Z ?? 0, SpatialReferences.Wgs84);
                MapPoint p2 = new MapPoint(points[i - 1].X, points[i - 1].Y, points[i - 1].Z ?? 0, SpatialReferences.Wgs84);
                MapPoint p3 = new MapPoint(points[i - 0].X, points[i - 0].Y, points[i - 0].Z ?? 0, SpatialReferences.Wgs84);

                if (distance == 0)
                {
                    distance = GeometryUtility.GetDistance(p1, p2);
                }
                distance += GeometryUtility.GetDistance(p2, p3);
                //如果两个点时间差超过5分钟，那么认为信号断连，p1p2之间的连线不显示
                if ((points[i - 1].Time.Value - points[i - 2].Time.Value).TotalMinutes > 5)
                {
                    //还需要删去上一条线
                    if (graphics.Count > 0)
                    {
                        graphics.RemoveAt(graphics.Count - 1);
                    }
                    Polyline dashLine = new Polyline([p1, p2]);
                    Graphic dashGraphic = new Graphic(dashLine)
                    {
                        Symbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Dash, Color.LightGray, 6)
                    };
                    graphics.Add(dashGraphic);
                    continue;
                }
                var speed = speeds[i - 2];
                Polyline line = new Polyline([p1, p2, p3]);
                Color color = Color.FromArgb(0x54, 0xA5, 0xF6);
                if (maxMinusMinSpeed > 0)
                {
                    double speedPercent = Math.Max(0, Math.Min(1, (speed - orderedSpeeds[minIndex]) / maxMinusMinSpeed));
                    color = InterpolateColors([Color.FromArgb(0x54, 0xA5, 0xF6), Color.FromArgb(0xFF, 0xB3, 0x00), Color.FromArgb(0xFF, 0, 0)], speedPercent);
                }
                Graphic graphic = new Graphic(line)
                {
                    Symbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, color, 6)
                };
                graphic.Attributes.Add("Speed", speed);
                graphic.Attributes.Add("Time", points[i - 1].Time);
                if (points[i - 1].Z.HasValue)
                {
                    graphic.Attributes.Add("Altitude", points[i - 1].Z);
                }
                graphic.Attributes.Add("Distance", distance);
                graphics.Add(graphic);
            }
        }

        /// <summary>
        /// 平滑轨迹
        /// </summary>
        /// <param name="points"></param>
        /// <param name="level"></param>
        /// <param name="get"></param>
        /// <param name="set"></param>
        public static void Smooth(IList<GpxPoint> points, int level, Func<GpxPoint, double> get, Action<GpxPoint, double> set)
        {
            int count = points.Count;
            Queue<double> queue = new Queue<double>(level);

            for (int headIndex = 0; headIndex < count; headIndex++)
            {
                GpxPoint headPoint = points[headIndex];
                if (queue.Count == level)
                {
                    queue.Dequeue();
                }
                queue.Enqueue(get(headPoint));
                if (headIndex < level)
                {
                    set(points[headIndex / 2], queue.Average());
                }
                else
                {
                    set(points[headIndex - level / 2], queue.Average());
                }
            }
            for (int tailIndex = count - level; tailIndex < count - 1; tailIndex++)
            {
                queue.Dequeue();
                set(points[tailIndex + (count - tailIndex) / 2], queue.Average());
            }
        }

        private static Color InterpolateColors(Color[] colors, double p)
        {
            if (colors.Length < 2 || p <= 0)
            {
                return colors[0];
            }
            else if (p >= 1)
            {
                return colors[^1];
            }
            double segmentLength = 1f / (colors.Length - 1);
            int segmentIndex = (int)(p / segmentLength);
            double segmentProgress = (p - segmentIndex * segmentLength) / segmentLength;

            Color startColor = colors[segmentIndex];
            Color endColor = colors[segmentIndex + 1];

            int r = (int)(startColor.R + (endColor.R - startColor.R) * segmentProgress);
            int g = (int)(startColor.G + (endColor.G - startColor.G) * segmentProgress);
            int b = (int)(startColor.B + (endColor.B - startColor.B) * segmentProgress);

            return Color.FromArgb(r, g, b);
        }
    }
}