using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.IO
{
    public static class PackageMigration
    {
        public static async Task ImportOldVersionFeatureTableAsync(string name, ShapefileFeatureTable featureTable, IEnumerable<FieldInfo> oldFields)
        {
            if (oldFields == null)
            {
                throw new ArgumentNullException(nameof(oldFields));
                TableDescription tableDescription = new TableDescription(name, featureTable.SpatialReference, featureTable.GeometryType);
                foreach (var field in featureTable.Fields)
                {
                    tableDescription.FieldDescriptions.Add(new FieldDescription(field.Name, field.FieldType));
                }
                var newTable = await MobileGeodatabase.Current.CreateTableAsync(tableDescription);
                await newTable.AddFeaturesAsync(await featureTable.QueryFeaturesAsync(new QueryParameters()));
            }
            else
            {
                //在旧版本中，使用Shapefile存储空间数据。
                //由于Shapefile不支持时间类型，所以使用的Text来存储时间。
                //在新版本中，需要将字符串的时间类型转换为真正的时间类型。
                TableDescription tableDescription = new TableDescription(name, featureTable.SpatialReference, featureTable.GeometryType);
                HashSet<string> timeKeys = new HashSet<string>();
                HashSet<string> dateKeys = new HashSet<string>();
                HashSet<string> intKeys = new HashSet<string>();
                HashSet<string> oldFieldKeys = featureTable.Fields.Select(p => p.Name).ToHashSet();
                foreach (var field in oldFields)
                {
                    if (!oldFieldKeys.Contains(field.Name))
                    {
                        throw new Exception($"字段{field.Name}出现在旧的字段定义中，但在提供的要素类中不存在");
                    }
                    if (field.IsIdField())
                    {
                        continue;
                    }
                    tableDescription.FieldDescriptions.Add(field.ToFieldDescription());
                    if (field.Type == FieldInfoType.DateTime)
                    {
                        timeKeys.Add(field.Name);
                    }
                    if (field.Type == FieldInfoType.Date)
                    {
                        dateKeys.Add(field.Name);
                    }
                    if (field.Type == FieldInfoType.Integer)
                    {
                        intKeys.Add(field.Name);
                    }
                }

                var newTable = await MobileGeodatabase.Current.CreateTableAsync(tableDescription);
                var oldFeatures = (await featureTable.QueryFeaturesAsync(new QueryParameters())).ToList();
                List<Feature> features = new List<Feature>(oldFeatures.Count);
                foreach (var feature in oldFeatures)
                {
                    foreach (var attr in feature.Attributes.ToList())
                    {
                        // 删除ID字段
                        if (FieldExtension.IsIdField(attr.Key))
                        {
                            feature.Attributes.Remove(attr.Key);
                        }
                    }

                    // 单次循环内处理 timeKeys 和 dateKeys
                    foreach (var key in feature.Attributes.Keys.ToList())
                    {
                        // 处理 timeKeys 的逻辑

                        if (timeKeys.Contains(key))
                        {
                            ProcessShpTimes(feature, key);
                        }

                        // 处理 dateKeys 的逻辑
                        if (dateKeys.Contains(key))
                        {
                            ProcessShpDates(feature, key);
                        }

                        if (intKeys.Contains(key))
                        {
                            ProcessShpInts(feature, key);
                        }
                    }

                    features.Add(feature);
                }
                await newTable.AddFeaturesAsync(features);
            }
        }

        private static void ProcessShpDates(Feature feature, string key)
        {

            var value = feature.GetAttributeValue(key);
            if (value == null)
            {
                return;
            }

            if (value is not DateTime dt)
            {
                Debug.WriteLine($"旧的时间字段 {key} 不是 {nameof(DateTime)}：{value}");
                Debug.Assert(false);
                return;
            }

            feature.SetAttributeValue(key, DateOnly.FromDateTime(dt));
        }

        private static void ProcessShpInts(Feature feature, string key)
        {
            var value = feature.GetAttributeValue(key);
            if (value is int i)
            {
                feature.SetAttributeValue(key, (long)i);
            }
        }

        private static void ProcessShpTimes(Feature feature, string key)
        {
            var value = feature.GetAttributeValue(key);
            if (value is not string str)
            {
                Debug.WriteLine($"旧的时间字段 {key} 不是字符串：{value}");
                Debug.Assert(false);
                return;
            }

            if (string.IsNullOrEmpty(str))
            {
                feature.SetAttributeValue(key, null);
                return;
            }

            if (!DateTime.TryParse(str, out var timeValue))
            {
                Debug.WriteLine($"时间字段 {key} 无法转为时间：{str}");
                Debug.Assert(false);
                return;
            }

            feature.SetAttributeValue(key, timeValue);
        }
    }
}
