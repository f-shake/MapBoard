using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Model;
using MapBoard.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Data;
using CsvHelper;
using System.Globalization;
using FzLib;
using System.Dynamic;
using MapBoard.Util;

namespace MapBoard.IO.Formats
{
    public static class Csv
    {
        public static void ExportAttributes(string path, IEnumerable<FieldInfo> fields, IEnumerable<Feature> features)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteField("ID");
            foreach (var field in fields)
            {
                csv.WriteField(field.DisplayName);
            }
            csv.NextRecord();

            int index = 0;
            foreach (var feature in features)
            {
                csv.WriteField(++index);
                foreach (var field in fields)
                {
                    csv.WriteField(feature.GetAttributeValue(field.Name));
                }
                csv.NextRecord();
            }

        }

        public static Task ExportAttributesAsync(string path, IEnumerable<FieldInfo> fields, IEnumerable<Feature> features)
        {
            return Task.Run(() =>
            {
                ExportAttributes(path, fields, features);
            });
        }

        public static Task ExportAttributesAsync(string path, IMapLayerInfo layer)
        {
            return Task.Run(() =>
            {
                ExportAttributes(path, layer.Fields, layer.GetAllFeatures());
            });
        }

        /// <summary>
        /// 导出到CSV
        /// </summary>
        /// <param name="path"></param>
        /// <param name="features"></param>
        public static void ExportPoints(string path, Feature[] features)
        {
            /*
            这是一个名为 Export 的 C# 函数。它接受两个参数：path 和 features。其中 path 表示要导出的CSV文件的路径，而 features 表示要导出的要素数组。
            函数首先创建一个名为 parts 的列表来存储所有部分。然后，它创建两个字典：featureIndexs 和 partIndexs，用于存储每个部分对应的要素索引和部分索引。
            接下来，函数检查 path 参数是否为非空。如果是，则开始处理每个要素。
            函数遍历每个要素，并根据要素的几何类型将其拆分为多个部分。然后，它将每个部分添加到 parts 列表中，并在 featureIndexs 和 partIndexs 字典中存储该部分对应的要素索引和部分索引。
            接下来，函数使用 StreamWriter 和 CsvWriter 类来创建一个CSV文件，并写入表头。
            然后，函数创建一个名为 records 的列表来存储所有记录。如果 parts 列表不为空，则遍历每个部分，并为每个点创建一个动态对象来表示一条记录。
            接下来，函数将记录添加到 records 列表中，并使用 CsvWriter 类的 WriteRecords 方法将所有记录写入CSV文件。
            最后，函数调用 CsvWriter 类的 Flush 方法以确保所有数据都已写入CSV文件。     
            */

            List<IEnumerable<MapPoint>> parts = new List<IEnumerable<MapPoint>>();
            Dictionary<object, int> featureIndexs = new Dictionary<object, int>();
            Dictionary<object, int> partIndexs = new Dictionary<object, int>();
            if (path != null)
            {
                int featureIndex = 0;
                int partIndex = 0;
                foreach (var feature in features)
                {
                    featureIndex++;
                    Geometry geometry = feature.Geometry;
                    dynamic part = null;
                    switch (feature.FeatureTable.GeometryType)
                    {
                        case GeometryType.Multipoint:
                            part = (geometry as Multipoint).Points;
                            featureIndexs.Add(part, featureIndex);
                            parts.Add(part);
                            break;

                        case GeometryType.Point:
                            part = new MapPoint[] { geometry as MapPoint };
                            featureIndexs.Add(part, featureIndex);
                            parts.Add(part);
                            break;

                        case GeometryType.Polygon:
                            partIndex = 0;
                            (geometry as Polygon).Parts.ForEach(p =>
                            {
                                partIndex++;
                                part = p.Points;
                                parts.Add(part);
                                featureIndexs.Add(part, featureIndex);
                                partIndexs.Add(part, partIndex);
                            });
                            break;

                        case GeometryType.Polyline:
                            partIndex = 0;
                            (geometry as Polyline).Parts.ForEach(p =>
                            {
                                partIndex++;
                                part = p.Points;
                                parts.Add(part);
                                featureIndexs.Add(part, featureIndex);
                                partIndexs.Add(part, partIndex);
                            });
                            break;
                    }
                }

                using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                List<dynamic> records = new List<dynamic>();
                if (parts.Any())
                {
                    foreach (var part in parts)
                    {
                        var featureID = featureIndexs[part];
                        var partID = partIndexs.ContainsKey(part) ? partIndexs[part] : 1;

                        int id = 0;
                        foreach (var point in part)
                        {
                            dynamic record = new ExpandoObject();
                            var dic = record as IDictionary<string, object>;
                            dic.Add("FeatureIndex", featureID);
                            dic.Add("PartIndex", partID);
                            dic.Add("PointIndex", ++id);
                            record.X = point.X;
                            record.Y = point.Y;
                            foreach (var attr in features[featureID - 1].Attributes)
                            {
                                dic.Add(attr.Key, attr.Value);
                            }
                            records.Add(record);
                        }
                    }
                    csv.WriteRecords(records);
                    csv.Flush();
                }
                else
                {
                }
            }
        }

        /// <summary>
        /// 异步导出到CSV
        /// </summary>
        /// <param name="path"></param>
        /// <param name="features"></param>
        /// <returns></returns>
        public static Task ExportPointsAsync(string path, Feature[] features)
        {
            return Task.Run(() =>
            {
                ExportPoints(path, features);
            });
        }
        /// <summary>
        /// 从CSV创建要素集合。CSV的每一行包含两个由逗号分隔的值，分别表示X和Y坐标；如果一行为空，则表示一个新的部分的开始
        /// </summary>
        /// <param name="path"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public static async Task<IReadOnlyList<Feature>> ImportAsync(string path, IMapLayerInfo layer)
        {
            string[] lines = File.ReadAllLines(path);
            List<List<MapPoint>> parts = new List<List<MapPoint>>();
            List<MapPoint> lastPoints = null;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    lastPoints = null;
                    continue;
                }
                string[] xy = line.Split(',');
                if (xy.Length != 2)
                {
                    throw new FormatException("CSV格式不正确");
                }

                if (!double.TryParse(xy[0], out double x) || !double.TryParse(xy[1], out double y))
                {
                    continue;
                }

                if (lastPoints == null)
                {
                    lastPoints = new List<MapPoint>();
                    parts.Add(lastPoints);
                }

                lastPoints.Add(new MapPoint(x, y));
            }

            List<Feature> features = new List<Feature>();

            foreach (var part in parts)
            {
                Feature feature = layer.CreateFeature();
                features.Add(feature);
                switch (layer.GeometryType)
                {
                    case GeometryType.Multipoint:
                        feature.Geometry = new Multipoint(part);
                        break;

                    case GeometryType.Point:
                        if (part.Count != 1)
                        {
                            throw new FormatException("若要导入为点，每一部分必须只有一个坐标");
                        }
                        feature.Geometry = part[0];

                        break;

                    case GeometryType.Polygon:
                        feature.Geometry = new Polygon(part);
                        break;

                    case GeometryType.Polyline:
                        feature.Geometry = new Polyline(part);
                        break;
                }
            }
            await layer.AddFeaturesAsync(features, FeaturesChangedSource.Import);
            return features.AsReadOnly();
        }

        /// <summary>
        /// CSV转为DataTable
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Task<DataTable> ImportToDataTableAsync(string path)
        {
            return Task.Run(() =>
            {
                using var reader = new StreamReader(path);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                using var dr = new CsvDataReader(csv);
                using var dt = new DataTable();
                dt.Load(dr);
                return dt;
            });
        }
    }
}