using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.IO
{
    public static class GeoJson
    {
        public async static Task<JObject> ConvertAsync(IMapLayerInfo layer)
        {
            var features = await layer.GetAllFeaturesAsync();
            JObject result = null;
            await Task.Run(() => result = Convert(features));
            return result;
        }

        public async static Task<string> ExportAsync(string path, IEnumerable<Feature> features)
        {
            string result = null;
            await Task.Run(() =>
            {
                result = Convert(features).ToString();
                File.WriteAllText(path, result, new UTF8Encoding(true));
            });
            return result;
        }

        public async static Task<string> ExportWithStyleAsync(string path, IEnumerable<Feature> features, ILayerInfo layer)
        {
            string result = null;
            await Task.Run(() =>
            {
                var json = Convert(features);
                var s = new JsonSerializer();
                s.Converters.Add(new GeoJsonStyleConverter());
                json.Add("style", JObject.FromObject(layer.Renderer.DefaultSymbol, s));
                result = json.ToString(Formatting.Indented);
                File.WriteAllText(path, result, new UTF8Encoding(true));
            });

            return result;
        }

        public async static Task ExportAsync(string path, IMapLayerInfo layer)
        {
            var features = await layer.GetAllFeaturesAsync();
            await ExportAsync(path, features);
        }

        public async static Task ExportWithStyleAsync(string path, IMapLayerInfo layer)
        {
            var features = await layer.GetAllFeaturesAsync();
            await ExportWithStyleAsync(path, features, layer);
        }

        private static JObject Convert(IEnumerable<Feature> features)
        {
            JObject jRoot = new JObject
            {
                { "type", "FeatureCollection" }
            };
            JArray jFeatures = new JArray();
            jRoot.Add("features", jFeatures);

            foreach (var f in features)
            {
                JObject jF = new JObject();
                jF.Add("type", "Feature");

                var g = GetGeometryJson(f);
                if (g == null)
                {
                    continue;
                }

                jF.Add("geometry", g);
                jF.Add("properties", GetPropertiesJson(f));
                jFeatures.Add(jF);
            }
            return jRoot;
        }

        private static JObject GetGeometryJson(Feature f)
        {
            JObject jGeo = new JObject();
            Geometry g = f.Geometry;
            if (g.IsEmpty)
            {
                return null;
            }
            if (g.SpatialReference != null && g.SpatialReference.Wkid != 4326)
            {
                g = GeometryEngine.Project(g, SpatialReferences.Wgs84);
            }

            switch (g)
            {
                case Polygon polygon:
                    if (polygon.Parts.Count == 1)
                    {
                        jGeo.Add("type", "Polygon");
                        jGeo.Add("coordinates", GetPolygonJson(polygon));
                    }
                    else
                    {
                        jGeo.Add("type", "MultiPolygon");
                        jGeo.Add("coordinates", GetMultiPolygonJson(polygon));
                    }
                    break;

                case Polyline polyline when polyline.Parts.Count == 1:
                    jGeo.Add("type", "LineString");
                    jGeo.Add("coordinates", GetLineStringJson(polyline));
                    break;

                case Polyline polyline when polyline.Parts.Count > 1:
                    jGeo.Add("type", "MultiLineString");
                    jGeo.Add("coordinates", GetMultiLineStringJson(polyline));
                    break;

                case MapPoint point:
                    jGeo.Add("type", "Point");
                    jGeo.Add("coordinates", GetPointJson(point));
                    break;

                case Multipoint multipoint:
                    jGeo.Add("type", "MultiPoint");
                    jGeo.Add("coordinates", GetMultiPointJson(multipoint));
                    break;

                default:
                    throw new NotSupportedException("不支持的图形类型");
            }

            return jGeo;
        }

        private static JArray GetMultiPolygonJson(Polygon polygon)
        {
            JArray jMultiPolygon = new JArray();

            foreach (var part in polygon.Parts)
            {
                JArray jRing = new JArray();

                foreach (var point in part.Points)
                {
                    jRing.Add(new JArray { point.X, point.Y });
                }

                // 闭合 ring（GeoJSON 要求）
                if (part.Points.Count > 0)
                {
                    var first = part.StartPoint;
                    var last = part.Points.Last();
                    if (!first.Equals(last))
                    {
                        jRing.Add(new JArray { first.X, first.Y });
                    }
                }

                // MultiPolygon 格式要求三层嵌套
                jMultiPolygon.Add(new JArray { jRing });
            }

            return jMultiPolygon;
        }

        private static JArray GetPointJson(MapPoint point)
        {
            return new JArray { point.X, point.Y };
        }

        private static JArray GetMultiPointJson(Multipoint multipoint)
        {
            JArray jPoints = new JArray();
            foreach (var p in multipoint.Points)
            {
                jPoints.Add(new JArray { p.X, p.Y });
            }
            return jPoints;
        }

        private static JArray GetLineStringJson(Polyline polyline)
        {
            Debug.Assert(polyline.Parts.Count == 1);
            JArray jLine = new JArray();
            foreach (var point in polyline.Parts[0].Points)
            {
                jLine.Add(new JArray { point.X, point.Y });
            }
            return jLine;
        }

        private static JArray GetMultiLineStringJson(Polyline polyline)
        {
            JArray jMultiLine = new JArray();
            foreach (var part in polyline.Parts)
            {
                JArray jLine = new JArray();
                foreach (var point in part.Points)
                {
                    jLine.Add(new JArray { point.X, point.Y });
                }
                jMultiLine.Add(jLine);
            }
            return jMultiLine;
        }

        private static JArray GetPolygonJson(Polygon polygon)
        {
            JArray jPolygon = new JArray();
            foreach (var ring in polygon.Parts)
            {
                JArray jRing = new JArray();
                foreach (var point in ring.Points)
                {
                    jRing.Add(new JArray { point.X, point.Y });
                }

                // GeoJSON 要求 ring 首尾闭合
                if (ring.Points.Count > 0)
                {
                    var first = ring.StartPoint;
                    var last = ring.Points.Last();
                    if (!first.Equals(last))
                    {
                        jRing.Add(new JArray { first.X, first.Y });
                    }
                }

                jPolygon.Add(jRing);
            }
            return jPolygon;
        }

        private static JObject GetPropertiesJson(Feature feature)
        {
            JObject jProps = new JObject();
            foreach (var prop in feature.Attributes)
            {
                if (prop.Value == null)
                {
                    jProps.Add(prop.Key, null);
                    continue;
                }

                switch (prop.Value)
                {
                    case string s:
                        jProps.Add(prop.Key, s);
                        break;
                    case int i32:
                        jProps.Add(prop.Key, i32);
                        break;
                    case long i64:
                        jProps.Add(prop.Key, i64);
                        break;
                    case float f:
                        jProps.Add(prop.Key, f);
                        break;
                    case double d:
                        jProps.Add(prop.Key, d);
                        break;
                    case DateTime dt:
                        jProps.Add(prop.Key, dt.ToString(Parameters.DateFormat));
                        break;
                    case DateTimeOffset dto:
                        jProps.Add(prop.Key, dto.UtcDateTime.ToString(Parameters.DateFormat));
                        break;
                    default:
                        jProps.Add(prop.Key, prop.Value.ToString());
                        break;
                }
            }
            return jProps;
        }

        class GeoJsonStyleConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(System.Drawing.Color);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value is System.Drawing.Color color)
                {
                    writer.WriteStartArray();
                    writer.WriteValue(color.A);
                    writer.WriteValue(color.R);
                    writer.WriteValue(color.G);
                    writer.WriteValue(color.B);
                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteValue(Array.Empty<int>());
                }
            }
        }
    }
}
