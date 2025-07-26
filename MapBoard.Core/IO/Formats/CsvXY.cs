using CsvHelper;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.IO.Abstractions;
using MapBoard.Mapping.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.IO.Formats
{
    internal class CsvXY : IFeatureTableExporter
    {
        public async Task ExportFeatureTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features)
        {
            await Task.Run(() =>
            {
                var featureList = features.ToList();
                List<(int FeatureIndex, int PartIndex, IList<MapPoint> Points)> parts = new();

                for (int featureIndex = 0; featureIndex < featureList.Count; featureIndex++)
                {
                    var feature = featureList[featureIndex];
                    int logicalFeatureIndex = featureIndex + 1;
                    Geometry geometry = feature.Geometry;

                    switch (feature.FeatureTable.GeometryType)
                    {
                        case GeometryType.Multipoint:
                            {
                                var pts = ((Multipoint)geometry).Points.ToList();
                                parts.Add((logicalFeatureIndex, 1, pts));
                                break;
                            }
                        case GeometryType.Point:
                            {
                                var pts = new List<MapPoint> { (MapPoint)geometry };
                                parts.Add((logicalFeatureIndex, 1, pts));
                                break;
                            }
                        case GeometryType.Polygon:
                            {
                                int partIndex = 0;
                                foreach (var part in ((Polygon)geometry).Parts)
                                {
                                    partIndex++;
                                    var pts = part.Points.ToList();
                                    parts.Add((logicalFeatureIndex, partIndex, pts));
                                }
                                break;
                            }
                        case GeometryType.Polyline:
                            {
                                int partIndex = 0;
                                foreach (var part in ((Polyline)geometry).Parts)
                                {
                                    partIndex++;
                                    var pts = part.Points.ToList();
                                    parts.Add((logicalFeatureIndex, partIndex, pts));
                                }
                                break;
                            }
                    }
                }

                using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                // 写表头
                csv.WriteField("FeatureIndex");
                csv.WriteField("PartIndex");
                csv.WriteField("PointIndex");
                csv.WriteField("X");
                csv.WriteField("Y");

                var firstFeature = featureList.FirstOrDefault();
                var attrKeys = (firstFeature != null) ? firstFeature.Attributes.Keys.ToList() : new List<string>();

                foreach (var key in attrKeys)
                {
                    csv.WriteField(key);
                }

                csv.NextRecord();

                // 写数据
                foreach (var (featureIndex, partIndex, points) in parts)
                {
                    var feature = featureList[featureIndex - 1];
                    var attributes = feature.Attributes;

                    for (int i = 0; i < points.Count; i++)
                    {
                        var point = points[i];
                        csv.WriteField(featureIndex);
                        csv.WriteField(partIndex);
                        csv.WriteField(i + 1);
                        csv.WriteField(point.X);
                        csv.WriteField(point.Y);

                        foreach (var key in attrKeys)
                        {
                            csv.WriteField(attributes.TryGetValue(key, out var value) ? value : null);
                        }

                        csv.NextRecord();
                    }
                }

                csv.Flush();
            });
        }
            }
}