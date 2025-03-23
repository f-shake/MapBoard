using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using FzLib;
using FzLib.IO;
using MapBoard.IO;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MapBoard.Util
{
    public static class LayerUtility
    {
        /// <summary>
        /// 建立缓冲区
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <param name="targetLayer"></param>
        /// <param name="meters"></param>
        /// <param name="union"></param>
        /// <param name="features"></param>
        /// <returns></returns>
        public static async Task BufferAsync(this IMapLayerInfo layer, MapLayerCollection layers, IMapLayerInfo targetLayer, double[] meters, bool union, Feature[] features = null)
        {
            if (targetLayer == null)
            {
                var template = EmptyMapLayerInfo.CreateTemplate();
                foreach (var symbol in layer.Renderer.Symbols)
                {
                    template.Renderer.Symbols.Add(symbol.Key, new SymbolInfo()
                    {
                        OutlineWidth = 0,
                        FillColor = symbol.Value.LineColor
                    });
                }
                template.Fields = new FieldInfo[]
                {
                    new FieldInfo("RingIndex","环索引",FieldInfoType.Integer)
                };
                targetLayer = await CreateLayerAsync(GeometryType.Polygon, layers, template, true, layer.Name + "-缓冲区");
            }
            await FeatureUtility.BufferToLayerAsync(layer, targetLayer, features == null ? await layer.GetAllFeaturesAsync() : features, meters, union);
        }

        /// <summary>
        /// 复制所有要素
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static async Task CopyAllFeaturesAsync(IMapLayerInfo source, IMapLayerInfo target)
        {
            var features = await source.GetAllFeaturesAsync();

            await target.AddFeaturesAsync(features, FeaturesChangedSource.FeatureOperation);
        }

        /// <summary>
        /// 复制样式
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void CopyStyles(IMapLayerInfo source, IMapLayerInfo target)
        {
            target.Labels = source.Labels.Select(p => p.Clone() as LabelInfo).ToArray();
            target.Renderer = source.Renderer.Clone() as UniqueValueRendererInfo;
        }

        /// <summary>
        /// 创建图层的副本
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <param name="includeFeatures"></param>
        /// <param name="includeFields"></param>
        /// <returns></returns>
        public static async Task CreateCopyAsync(this IMapLayerInfo layer, MapLayerCollection layers, bool includeFeatures, bool includeFields)
        {
            if (includeFeatures)
            {
                var features = await layer.GetAllFeaturesAsync();

                var newLayer = await CreateLayerAsync(layer.GeometryType, layers, layer, includeFields);
                await newLayer.AddFeaturesAsync(features, FeaturesChangedSource.Initialize, true);
                layer.LayerVisible = false;
            }
            else
            {
                await CreateLayerAsync(layer.GeometryType, layers, layer, includeFields);
            }
        }


        /// <summary>
        /// 创建Shapefile图层
        /// </summary>
        /// <param name="type"></param>
        /// <param name="layers"></param>
        /// <param name="name"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public static Task<IMapLayerInfo> CreateLayerAsync(
                                                                       GeometryType type,
                                                                       MapLayerCollection layers,
                                                                       string name = null,
                                                                       IList<FieldInfo> fields = null)
        {
            return CreateLayerAsync(type, layers, null, false, fields, name);
        }

        /// <summary>
        /// 通过模板和指定字段创建Shapefile图层
        /// </summary>
        /// <param name="type"></param>
        /// <param name="layers"></param>
        /// <param name="template"></param>
        /// <param name="importTemplateFields"></param>
        /// <param name="fields"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static async Task<IMapLayerInfo> CreateLayerAsync(
                                                                             GeometryType type,
                                                                             MapLayerCollection layers,
                                                                             IMapLayerInfo template,
                                                                             bool importTemplateFields,
                                                                             IEnumerable<FieldInfo> fields,
                                                                             string name = null)
        {
            if (fields != null && importTemplateFields)
            {
                throw new ArgumentException("不可同时继承字段又指定字段");
            }

            //处理图层名
            name ??= template == null ?
                    "新图层 - " + DateTime.Now.ToString("yyyyMMdd-HHmmss")
                    : template.Name;

            //处理字段
            if (importTemplateFields)
            {
                fields = template.Fields;
            }
            else
            {
                fields ??= [];
            }

            //创建图层
            Debug.Assert(template is null or MapLayerInfo);
            MgdbMapLayerInfo layer = template == null ?
                new MgdbMapLayerInfo(name)
                : new MgdbMapLayerInfo(template as MapLayerInfo, name, importTemplateFields);
            await MobileGeodatabase.CreateMgdbLayerAsync(type, layer.SourceName, null, fields);

            layer.Fields = fields.ToArray();
            if (layers != null)
            {
                await layers.AddAndLoadAsync(layer);
                layer.LayerVisible = true;
                layers.Selected = layer;
            }
            return layer;
        }

        /// <summary>
        /// 从模板创建Shapefile图层
        /// </summary>
        /// <param name="type"></param>
        /// <param name="layers"></param>
        /// <param name="template"></param>
        /// <param name="includeFields"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Task<IMapLayerInfo> CreateLayerAsync(
                                                                            GeometryType type,
                                                                            MapLayerCollection layers,
                                                                            IMapLayerInfo template,
                                                                            bool includeFields,
                                                                            string name = null)
        {
            return CreateLayerAsync(type, layers, template, includeFields, null, name);
        }

        /// <summary>
        /// 删除图层
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <param name="deleteFiles"></param>
        /// <returns></returns>
        public static async Task DeleteLayerAsync(this MapLayerInfo layer, MapLayerCollection layers)
        {
            if (layers != null && layers.Contains(layer))
            {
                await layers.RemoveAsync(layer);
            }
        }

        public static async Task<IMapLayerInfo> ExportLayerAsync(IMapLayerInfo oldLayer, MapLayerCollection layers, string name, IEnumerable<ExportingFieldInfo> fields)
        {
            var fieldList = fields.Where(p => p.Enable).ToList();
            if (fieldList.Count != fieldList.Select(p => p.Name).Distinct().Count())
            {
                throw new Exception("存在重复的字段名");
            }
            if(layers.Any(p=>p.Name==name))
            {
                throw new Exception("存在重复的图层名");
            }
            var layer = await CreateLayerAsync(oldLayer.GeometryType, layers, oldLayer, false, fieldList, name);
            Dictionary<string, string> oldName2newName = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                if (field.OldField != null)
                {
                    oldName2newName.Add(field.OldField.Name, field.Name);
                }
            }

            var oldFeatures = await oldLayer.GetAllFeaturesAsync();
            List<Feature> newFeatures = null;
            await Task.Run(() =>
            {
                newFeatures = new List<Feature>(oldFeatures.Length);
                foreach (var feature in oldFeatures)
                {
                    Dictionary<string, object> newAttributes = new Dictionary<string, object>();
                    foreach (var attribute in feature.Attributes)
                    {
                        if (oldName2newName.TryGetValue(attribute.Key, out string value))
                        {
                            newAttributes.Add(value, attribute.Value);
                        }
                    }

                    newFeatures.Add(layer.CreateFeature(newAttributes, feature.Geometry));
                }
            });

            await layer.AddFeaturesAsync(newFeatures, FeaturesChangedSource.Import);

            return layer;
        }

        /// <summary>
        /// 获取所有要素
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static FeatureQueryResult GetAllFeatures(this IMapLayerInfo layer)
        {
            return layer.QueryFeaturesAsync(new QueryParameters()).Result;
        }

        /// <summary>
        /// 获取所有要素
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static async Task<Feature[]> GetAllFeaturesAsync(this IMapLayerInfo layer)
        {
            FeatureQueryResult result = await layer.QueryFeaturesAsync(new QueryParameters());
            Feature[] array = null;
            await Task.Run(() =>
            {
                array = [.. result];
            });
            return array;
        }

        /// <summary>
        /// 获取图层中要素属性值的唯一值
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static async Task<object[]> GetUniqueAttributeValues(this IMapLayerInfo layer, string fieldName)
        {
            var parameters = new StatisticsQueryParameters(new StatisticDefinition[]
            {
                new StatisticDefinition(fieldName,StatisticType.Count,null)
            });
            parameters.GroupByFieldNames.Add(fieldName);
            var result = await layer.Layer.FeatureTable.QueryStatisticsAsync(parameters);
            return result.Select(p => p.Group[fieldName]).ToArray();
        }

        /// <summary>
        /// 从Feature导入到地图图层
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="layers"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static async Task<IMapLayerInfo> ImportFromFeatureTable(string layerName, MapLayerCollection layers, FeatureTable table)
        {
            await table.LoadAsync();
            FeatureQueryResult features = await table.QueryFeaturesAsync(new QueryParameters());
            var fieldMap = table.Fields.ToFieldInfos();//从原表字段名到新字段的映射
            IMapLayerInfo layer = await CreateLayerAsync(
                table.GeometryType, layers, layerName, [.. fieldMap.Values]);
            layer.LayerVisible = false;
            var fields = layer.Fields.Select(p => p.Name).ToHashSet();
            List<Feature> newFeatures = new List<Feature>();
            await Task.Run(() =>
            {
                foreach (var feature in features)
                {
                    Dictionary<string, object> newAttributes = new Dictionary<string, object>();
                    foreach (var attr in feature.Attributes)
                    {
                        if (attr.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        string name = attr.Key;//现在是源文件的字段名

                        if (!fieldMap.ContainsKey(name))
                        {
                            continue;
                        }
                        name = fieldMap[name].Name;//切换到目标表的字段名

                        object value = attr.Value;
                        if (value is short)
                        {
                            value = Convert.ToInt32(value);
                        }
                        else if (value is float)
                        {
                            value = Convert.ToDouble(value);
                        }
                        newAttributes.Add(name, value);
                    }
                    Feature newFeature = layer.CreateFeature(newAttributes, feature.Geometry.RemoveZAndM());
                    newFeatures.Add(newFeature);
                }
            });
            await layer.AddFeaturesAsync(newFeatures, FeaturesChangedSource.Import);

            layer.LayerVisible = true;
            return layer;
        }
        /// <summary>
        /// 建立缓冲区
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <param name="meters"></param>
        /// <returns></returns>

        /// <summary>
        /// 合并
        /// </summary>
        /// <param name="layers"></param>
        /// <param name="layerCollection"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static async Task<IMapLayerInfo> UnionAsync(IEnumerable<MapLayerInfo> layers, MapLayerCollection layerCollection)
        {
            if (layers == null || !layers.Any())
            {
                throw new ArgumentException("图层为空");
            }
            var type = layers.Select(p => p.GeometryType).Distinct();
            if (type.Count() != 1)
            {
                throw new ArgumentException("图层的类型并非统一");
            }
            var layer = await CreateLayerAsync(type.First(), layerCollection);
            List<Feature> newFeatures = new List<Feature>();
            await Task.Run(() =>
            {
                foreach (var oldLayer in layers)
                {
                    var oldFeatures = oldLayer.GetAllFeatures();
                    var features = oldFeatures.Select(p => layer.CreateFeature(p.Attributes, p.Geometry));
                    newFeatures.AddRange(features);
                }
            });
            await layer.AddFeaturesAsync(newFeatures, FeaturesChangedSource.FeatureOperation);
            layers.ForEach(p => p.LayerVisible = false);

            return layer;
        }
    }
}