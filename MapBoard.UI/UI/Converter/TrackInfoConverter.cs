using MapBoard.IO.Formats.Gps;
using MapBoard.Util;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace MapBoard.UI.Converter
{
    public class TrackInfoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(value is GpxTrack);
            var track = (GpxTrack)value;
            System.Collections.Generic.IList<GpxPoint> points = track.GetPoints();
            switch (parameter as string)
            {
                case "d":
                    return GetDistance(points.GetDistance());
                case "s":
                    var speed = points.GetAverageSpeed();
                    return SpeedValueConverter.Convert(speed);
                case "sm":
                    var speedM = points.GetMovingAverageSpeed();
                    return SpeedValueConverter.Convert(speedM);
                case "tm":
                    var timeM = points.GetMovingTime();
                    return timeM.ToString();
                case "ms":
                    return SpeedValueConverter.Convert(points.GetMaxSpeed());
                default:
                    throw new Exception();
            }
            static string GetDistance(double distance)
            {
                if (distance < 1000)
                {
                    return distance.ToString("0 m");
                }
                return (distance / 1000).ToString("0.00 km");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}