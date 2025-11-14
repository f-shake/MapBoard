using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using FzLib.Program;
using hyjiacan.py4n;
using MapBoard.IO.Abstractions;
using MapBoard.IO.Formats;
using MapBoard.IO.Gdb;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.IO
{
    public static class Importer
    {
        private static readonly PinyinFormat PinyinFormat = PinyinFormat.WITHOUT_TONE
            | PinyinFormat.CAPITALIZE_FIRST_LETTER
            | PinyinFormat.WITH_V;

        #region 各种类型的公开导入方法

        public static async Task<IMapLayerInfo> ImportCsvXYAsync(string path, MapLayerCollection layers)
        {
            var results = await ImportToNewLayers(new Csv(), path, layers);
            return results[0];
        }

#if !RELEASEWITHOUTGDAL
        public static Task<List<IMapLayerInfo>> ImportFileGdbAsync(string path, MapLayerCollection layers)
        {
            return ImportToNewLayers(new FileGeodatabase(), path, layers);
        }
#endif

        public static async Task<IList<Feature>> ImportGpxAsync(string path, IMapLayerInfo existingLayer)
        {
            switch (existingLayer.GeometryType)
            {
                case GeometryType.Point:
                    return await ImportToExistingLayer(new Gpx(Gpx.GpxImportType.Point), path, existingLayer);
                case GeometryType.Polyline:
                    return await ImportToExistingLayer(new Gpx(Gpx.GpxImportType.Line), path, existingLayer);
                default:
                    throw new ArgumentOutOfRangeException(nameof(existingLayer));
            }
        }

        public static async Task<IMapLayerInfo> ImportGpxLineAsync(string path, MapLayerCollection layers)
        {
            var results = await ImportToNewLayers(new Gpx(Gpx.GpxImportType.Line), path, layers);
            return results[0];
        }

        public static async Task<IMapLayerInfo> ImportGpxPointsAsync(string path, MapLayerCollection layers)
        {
            var results = await ImportToNewLayers(new Gpx(Gpx.GpxImportType.Point), path, layers);
            return results[0];
        }

        public static Task<List<IMapLayerInfo>> ImportKmlAsync(string path, MapLayerCollection layers)
        {
            return ImportToNewLayers(new Kml(), path, layers);
        }

        public static Task<List<IMapLayerInfo>> ImportMobileMapPackageAsync(string path, MapLayerCollection layers)
        {
            return ImportToNewLayers(new MobileMapPackage(), path, layers);
        }

        public static Task<List<IMapLayerInfo>> ImportPhotoLocationsAsync(string path, MapLayerCollection layers)
        {
            return ImportToNewLayers(new Photo(), path, layers);
        }
        public static async Task<IMapLayerInfo> ImportShapefileAsync(string path, MapLayerCollection layers)
        {
            var results = await ImportToNewLayers(new Shapefile(), path, layers);
            return results[0];
        }
        #endregion

        #region 中间方法


        private static async Task<IList<Feature>> ImportToExistingLayer(IMemoryLayerImporter importer, string path, IMapLayerInfo layer)
        {
            var importingLayers = await importer.GetLayersAsync(path);
            Debug.Assert(importingLayers.Count() == 1);
            return await ImportToExistingLayer(layer, importingLayers.First());
        }

        private static async Task<List<IMapLayerInfo>> ImportToNewLayers(IFeatureTableImporter importer, string path, MapLayerCollection layers)
        {
            var tables = await importer.GetFeatureTablesAsync(path);
            List<IMapLayerInfo> results = new List<IMapLayerInfo>();
            foreach (var table in tables)
            {
                var layer = await ImportFromFeatureTable(importer.GetLayerName(table), layers, table);
                importer.OnLayerImported(table, layer);
                results.Add(layer);
            }
            return results;
        }

        private static async Task<List<IMapLayerInfo>> ImportToNewLayers(IMemoryLayerImporter importer, string path, MapLayerCollection layers)
        {
            var importingLayers = await importer.GetLayersAsync(path);
            return await ImportToNewLayers(layers, importingLayers);
        }
        #endregion

        #region 私有方法

        public static string GetValidFieldName(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (name.Length > FieldInfo.MaxFieldNameLength)
            {
                name = name[..FieldInfo.MaxFieldNameLength];
            }
            if ((name[0] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')
                && name.Skip(1).All(p => p is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            {
                return name;
            }

            var chars = name.ToCharArray();
            StringBuilder targetName = new StringBuilder();
            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 0)
                {
                    // 第一个字符：必须是字母或下划线
                    if (char.IsAsciiLetter(chars[i]) || chars[i] == '_')
                    {
                        targetName.Append(chars[i]);
                    }
                    else
                    {
                        targetName.Append(GetPinyinOrUnderline(chars[i]));
                    }
                }
                else
                {
                    // 其他字符：可以是字母、数字或下划线
                    if (char.IsAsciiLetterOrDigit(chars[i]) || chars[i] == '_')
                    {
                        targetName.Append(chars[i]);
                    }
                    else
                    {
                        targetName.Append(GetPinyinOrUnderline(chars[i]));
                    }
                }
            }
            //Mobile Geodatabase的开头不支持下划线
            if (targetName[0] == '_')
            {
                targetName.Insert(0, 'f');
            }
            return targetName.ToString();
        }

        public static bool IsFieldIgnored(string name)
        {
            return name.StartsWith("shape_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("fid_", StringComparison.OrdinalIgnoreCase)
                || name.Equals("fid", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("st_", StringComparison.OrdinalIgnoreCase)
                || name.Equals("objectid", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPinyinOrUnderline(char c)
        {
            if (PinyinUtil.IsHanzi(c))
            {
                return Pinyin4Net.GetFirstPinyin(c, PinyinFormat);
            }
            return "_";
        }

        private static async Task<IMapLayerInfo> ImportFromFeatureTable(string layerName, MapLayerCollection layers, FeatureTable table)
        {
            await table.LoadAsync();
            FeatureQueryResult features = await table.QueryFeaturesAsync(new QueryParameters());

            var importingLayer = new SimpleLayer(layerName, table.GeometryType, [.. table.Fields.Select(p => p.ToFieldInfo())],
                table.SpatialReference, features.Select(p => new SimpleFeature(p.Attributes, p.Geometry)));

            return await ImportToNewLayer(layers, importingLayer);

            ////从原表字段名到新字段的映射
            //IMapLayerInfo layer = await CreateLayerAsync(
            //    table.GeometryType, layers, layerName, [.. fieldMap.Values]);
            //layer.LayerVisible = false;
            //var fields = layer.Fields.Select(p => p.Name).ToHashSet();
            //List<Feature> newFeatures = new List<Feature>();
            //await Task.Run(() =>
            //{
            //    foreach (var feature in features)
            //    {
            //        Dictionary<string, object> newAttributes = new Dictionary<string, object>();
            //        foreach (var attr in feature.Attributes)
            //        {
            //            if (attr.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
            //            {
            //                continue;
            //            }
            //            string name = attr.Key;//现在是源文件的字段名

            //            if (!fieldMap.ContainsKey(name))
            //            {
            //                continue;
            //            }
            //            name = fieldMap[name].Name;//切换到目标表的字段名

            //            object value = attr.Value;
            //            if (value is short)
            //            {
            //                value = Convert.ToInt32(value);
            //            }
            //            else if (value is float)
            //            {
            //                value = Convert.ToDouble(value);
            //            }
            //            newAttributes.Add(name, value);
            //        }
            //        Feature newFeature = layer.CreateFeature(newAttributes, feature.Geometry.RemoveZAndM());
            //        newFeatures.Add(newFeature);
            //    }
            //});
            //await layer.AddFeaturesAsync(newFeatures, FeaturesChangedSource.Import);

            //layer.LayerVisible = true;
            //return layer;
        }

        private static async Task<IList<Feature>> ImportToExistingLayer(IMapLayerInfo layer, SimpleLayer importingLayer)
        {
            Normalize(importingLayer);

            if (importingLayer.Features.Count == 0)
            {
                return [];
            }

            List<Feature> features = new List<Feature>(importingLayer.Features.Count);
            await Task.Run(() =>
            {
                var existingLayerFields = layer.Fields.ToDictionary(p => p.Name);
                foreach (var feature in importingLayer.Features)
                {
                    Dictionary<string, object> newAttributes = new Dictionary<string, object>();
                    foreach (var attribute in feature.Attributes)
                    {
                        if (attribute.Value == null)
                        {
                            continue;
                        }
                        if (existingLayerFields.TryGetValue(attribute.Key, out FieldInfo existingField))
                        {
                            if (FieldInfo.IsCompatibleType(existingField.Type, attribute.Value, out object newValue))
                            {
                                newAttributes.Add(attribute.Key, newValue);
                            }
                        }
                    }
                    var esriFeature = layer.CreateFeature(newAttributes, feature.Geometry);
                    features.Add(esriFeature);
                }
            });
            await layer.AddFeaturesAsync(features, FeaturesChangedSource.Import);
            return features;
        }

        private static async Task<IMapLayerInfo> ImportToNewLayer(MapLayerCollection layers, SimpleLayer importingLayer)
        {
            Normalize(importingLayer);
            var layer = await LayerUtility.CreateLayerAsync(importingLayer.GeometryType, layers, importingLayer.Name, importingLayer.Fields);

            if (importingLayer.Features.Count == 0)
            {
                return layer;
            }

            List<Feature> features = new List<Feature>(importingLayer.Features.Count);
            await Task.Run(() =>
            {
                foreach (var feature in importingLayer.Features)
                {
                    var esriFeature = layer.CreateFeature(feature.Attributes, feature.Geometry);
                    features.Add(esriFeature);
                }
            });
            await layer.AddFeaturesAsync(features, FeaturesChangedSource.Import);
            return layer;
        }

        private static async Task<List<IMapLayerInfo>> ImportToNewLayers(MapLayerCollection layers, IEnumerable<SimpleLayer> importingLayers)
        {
            List<IMapLayerInfo> results = new List<IMapLayerInfo>();
            foreach (var importingLayer in importingLayers)
            {
                results.Add(await ImportToNewLayer(layers, importingLayer));
            }
            return results;
        }
        private static void Normalize(SimpleLayer layer)
        {
            (var fields, var map) = NormalizeFields(layer.Fields);
            layer.Fields = [.. fields];
            NormalizeFeatures(layer.Features, layer.Fields, map);
        }

        private static Dictionary<string, object> NormalizeAttributes(IDictionary<string, object> attributes, IList<FieldInfo> fields, Dictionary<string, string> map)
        {
            var result = new Dictionary<string, object>();
            if (attributes == null)
            {
                return result;
            }

            foreach (var field in fields)
            {
                Debug.Assert(map.ContainsKey(field.Name));
                var oldName = map[field.Name];
                if (!attributes.TryGetValue(oldName, out var value))
                {
                    value = null;
                }
                result.Add(field.Name, value);
            }

            return result;
        }

        private static void NormalizeFeatures(IEnumerable<SimpleFeature> features, IList<FieldInfo> fields, Dictionary<string, string> map)
        {
            foreach (var feature in features)
            {
                if (feature.Geometry == null)
                {
                    continue;
                }
                feature.Attributes = NormalizeAttributes(feature.Attributes, fields, map);
                feature.Geometry = NormalizeGeometry(feature.Geometry);
            }
        }

        private static (List<FieldInfo> result, Dictionary<string, string> map) NormalizeFields(IEnumerable<FieldInfo> fields)
        {
            var result = new List<FieldInfo>();
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nameMap = new Dictionary<string, string>(); //从新名词映射到老名称
            result = [.. fields.Where(p => !IsFieldIgnored(p.Name))];//去掉ID、长度面积等字段

            //没问题的字段，先占位，防止被重名
            foreach (var field in result.Where(p => FieldInfo.IsValidFieldName(p.Name)))
            {
                existingNames.Add(field.Name);
                nameMap.Add(field.Name, field.Name);
            }

            //其他字段，正规化
            foreach (var field in result.Where(p => !FieldInfo.IsValidFieldName(p.Name)))
            {
                var newName = GetValidFieldName(field.Name);
                string suffix = "";
                int index = 1;
                while (existingNames.Contains(newName + suffix))
                {
                    suffix = $"_{++index}";
                }
                newName += suffix;
                nameMap.Add(newName, field.Name);
                field.Name = newName;
                existingNames.Add(newName);
            }

            return (result, nameMap);
        }

        private static Geometry NormalizeGeometry(Geometry geometry)
        {
            if (geometry.SpatialReference == null)
            {
                return geometry;
            }
            if (geometry.SpatialReference.Wkid == 4326)
            {
                return geometry;
            }
            return geometry.ToWgs84();
        }

        #endregion
    }
}
