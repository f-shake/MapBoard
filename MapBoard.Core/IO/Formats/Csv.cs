using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Model;
using MapBoard.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Data;
using CsvHelper;
using System.Globalization;
using FzLib;
using System.Dynamic;
using MapBoard.Util;
using MapBoard.IO.Abstractions;

namespace MapBoard.IO.Formats
{
    public class Csv : IFeatureTableExporter, IMemoryLayerImporter
    {
        public Task ExportFeatureTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features)
        {
            return Task.Run(() =>
            {
                using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                csv.WriteField("ID");
                var fields = layer.Fields;
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
            });
        }

        public async ValueTask<IEnumerable<SimpleLayer>> GetLayersAsync(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            using var dr = new CsvDataReader(csv);
            using var dt = new DataTable();
            dt.Load(dr);
            List<FieldInfo> fields = new List<FieldInfo>();
            DataColumn xc = null;
            DataColumn yc = null;
            foreach (DataColumn column in dt.Columns)
            {
                FieldInfo field = new FieldInfo
                {
                    Name = column.ColumnName,
                    DisplayName = column.ColumnName,
                    Type = FieldInfoType.Text
                };
                if ("x".Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    xc = column;
                    continue;
                }
                if ("y".Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    yc = column;
                    continue;
                }
                fields.Add(field);
            }

            if (xc == null || yc == null)
            {
                throw new FormatException("CSV中应当存在“X”列和“Y”列");
            }

            List<SimpleFeature> features = new List<SimpleFeature>();
            foreach (DataRow row in dt.Rows)
            {
                if (!double.TryParse(row[xc].ToString(), out double x))
                {
                    throw new FormatException($"CSV中“X”格式不正确：{row[xc]}");
                }
                if (!double.TryParse(row[yc].ToString(), out double y))
                {
                    throw new FormatException($"CSV中“Y”格式不正确：{row[yc]}");
                }
                MapPoint point = new MapPoint(x, y, SpatialReferences.Wgs84);

                Dictionary<string, object> attributes = new Dictionary<string, object>();
                foreach (DataColumn column in dt.Columns)
                {
                    if (column == xc || column == yc)
                    {
                        continue;
                    }
                    attributes.Add(column.ColumnName, row[column]);
                }
                var feature = new SimpleFeature(attributes, point);
                features.Add(feature);
            }

            SimpleLayer layer = new SimpleLayer(Path.GetFileNameWithoutExtension(path), GeometryType.Point,
               fields, SpatialReferences.Wgs84, features);
            return [layer];
        }
    }
}