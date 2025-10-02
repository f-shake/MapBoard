using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Tasks.Geoprocessing;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MapBoard.Util
{
    public static class OverlayAnalysisUtility
    {
        public static int GetDimension(GeometryType geometryType)
        {
            return geometryType switch
            {
                GeometryType.Point => 0,
                GeometryType.Multipoint => 0,
                GeometryType.Polyline => 1,
                GeometryType.Polygon => 2,
                GeometryType.Envelope => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(geometryType), "不支持的几何类型"),
            };
        }

        public static GeometryType GetGeometryTypeFromDimension(int dimension)
        {
            return dimension switch
            {
                0 => GeometryType.Point,
                1 => GeometryType.Polyline,
                2 => GeometryType.Polygon,
                _ => throw new ArgumentOutOfRangeException(nameof(dimension), "不支持的几何维度"),
            };
        }

        public static GeometryType GetTargetGeometryType(OverlayAnalysisOperation operation, GeometryType mainLayerType, GeometryType anotherLayerType)
        {
            var d1 = GetDimension(mainLayerType);
            var d2 = GetDimension(anotherLayerType);
            switch (operation)
            {
                case OverlayAnalysisOperation.Intersect:
                    return GetGeometryTypeFromDimension(Math.Min(d1, d2));

                case OverlayAnalysisOperation.Union:
                    if (mainLayerType != anotherLayerType)
                    {
                        throw new ArgumentException("并集操作要求两个图层的几何类型相同");
                    }
                    return mainLayerType;

                case OverlayAnalysisOperation.Clip:
                    if (d1 > d2) //用线裁面
                    {
                        throw new ArgumentException("擦除操作要求另一图层的维度相同或更高");
                    }
                    return mainLayerType;

                case OverlayAnalysisOperation.Erase:
                    if (d1 > d2) //用线擦面
                    {
                        throw new ArgumentException("擦除操作要求另一图层的维度相同或更高");
                    }
                    return mainLayerType;

                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), "不支持的叠加分析操作类型");
            }
        }

        public static IList<GeometryType> GetValidAnotherLayerGeometryType(OverlayAnalysisOperation operation, GeometryType mainLayerType)
        {
            var d = GetDimension(mainLayerType);
            GeometryType[] allTypes = [GeometryType.Point, GeometryType.Multipoint, GeometryType.Polyline, GeometryType.Polygon];
            switch (operation)
            {
                case OverlayAnalysisOperation.Intersect:
                    return allTypes;
                case OverlayAnalysisOperation.Union:
                    return [mainLayerType];
                case OverlayAnalysisOperation.Clip:
                    return [.. allTypes.Where(p => GetDimension(p) >= d)];
                case OverlayAnalysisOperation.Erase:
                    return [.. allTypes.Where(p => GetDimension(p) >= d)];
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), "不支持的叠加分析操作类型");
            }
        }

        public static async Task<List<Feature>> OverlayAnalysisAsync(this IMapLayerInfo mainLayer, MapLayerCollection layers, Feature[] features,
           IMapLayerInfo anotherLayer, OverlayAnalysisOperation operation)
        {
            List<FieldInfo> fields = GetTargetFields(mainLayer, anotherLayer, operation);
            GeometryType geometryType = GetTargetGeometryType(operation, mainLayer.GeometryType, anotherLayer.GeometryType);
            List<Feature> targetFeatures = null;
            var layer = await LayerUtility.CreateLayerAsync(geometryType, layers, mainLayer.Name + "-叠加分析", fields);
            await Task.Run(async () =>
            {
                var anotherLayerFeatures = await anotherLayer.GetAllFeaturesAsync();
                targetFeatures = operation switch
                {
                    OverlayAnalysisOperation.Intersect => ProcessIntersect(features, anotherLayerFeatures, layer),
                    OverlayAnalysisOperation.Clip => ProcessClip(features, anotherLayerFeatures, layer),
                    //OverlayAnalysisOperation.Union => throw new NotImplementedException(),
                    OverlayAnalysisOperation.Erase => ProcessErase(features, anotherLayerFeatures, layer),
                    _ => throw new NotImplementedException(),
                };
                if (targetFeatures.Count > 0)
                {
                    await layer.AddFeaturesAsync(targetFeatures, FeaturesChangedSource.FeatureOperation);
                }
            });
            return targetFeatures;
        }

        private static List<FieldInfo> GetTargetFields(IMapLayerInfo mainLayer, IMapLayerInfo anotherLayer, OverlayAnalysisOperation operation)
        {
            var fields = mainLayer.Fields
                .Where(p => p.Name != Parameters.CreateTimeFieldName)
                .Where(p => p.Name != Parameters.ModifiedTimeFieldName)
                .Where(p => !p.IsIdField())
                .Select(p => p.Clone() as FieldInfo)
                .ToList();

            if (operation is OverlayAnalysisOperation.Intersect or OverlayAnalysisOperation.Union)
            {
                foreach (var field in fields)
                {
                    field.Name = "L1_" + field.Name;
                    field.DisplayName = $"{mainLayer.Name} - {field.DisplayName}";
                }
                foreach (var field in anotherLayer.Fields
                .Where(p => p.Name != Parameters.CreateTimeFieldName)
                .Where(p => p.Name != Parameters.ModifiedTimeFieldName)
                .Select(p => p.Clone() as FieldInfo))
                {
                    field.Name = "L2_" + field.Name;
                    field.DisplayName = $"{anotherLayer.Name} - {field.DisplayName}";
                    fields.Add(field);
                }
            }

            return fields;
        }

        private static List<Feature> ProcessClip(Feature[] features, Feature[] anotherLayerFeatures, IMapLayerInfo layer)
        {
            List<Feature> targetFeatures = new List<Feature>();
            Geometry metgedAnotherLayer = GeometryEngine.Union(anotherLayerFeatures.Select(p => p.Geometry));
            foreach (var f1 in features)
            {
                if (!f1.Geometry.Intersects(metgedAnotherLayer))
                {
                    continue;
                }

                var geom = f1.Geometry.Intersection(metgedAnotherLayer);
                if (geom.IsEmpty || geom.GeometryType != layer.GeometryType)
                {
                    continue;
                }
                Feature feature = layer.CreateFeature(f1.Attributes, geom);
                targetFeatures.Add(feature);
            }

            return targetFeatures;
        }


        private static List<Feature> ProcessErase(Feature[] features, Feature[] anotherLayerFeatures, IMapLayerInfo layer)
        {
            List<Feature> targetFeatures = new List<Feature>();
            Geometry metgedAnotherLayer = GeometryEngine.Union(anotherLayerFeatures.Select(p => p.Geometry));
            foreach (var f1 in features)
            {
                if (f1.Geometry.Within(metgedAnotherLayer))
                {
                    continue;
                }
                Feature feature = null;
                if (!f1.Geometry.Intersects(metgedAnotherLayer))
                {
                    feature = layer.CreateFeature(f1.Attributes, f1.Geometry);
                    targetFeatures.Add(feature);
                    continue;
                }

                var geom = f1.Geometry.Difference(metgedAnotherLayer);
                if (geom.IsEmpty || geom.GeometryType != layer.GeometryType)
                {
                    continue;
                }
                feature = layer.CreateFeature(f1.Attributes, geom);
                targetFeatures.Add(feature);
            }

            return targetFeatures;
        }


        private static List<Feature> ProcessIntersect(Feature[] features, Feature[] anotherLayerFeatures, IMapLayerInfo layer)
        {
            List<Feature> targetFeatures = new List<Feature>();
            var twoFeatures = new Feature[2];
            foreach (var f1 in features)
            {
                twoFeatures[0] = f1;
                foreach (var f2 in anotherLayerFeatures)
                {
                    if (!f1.Geometry.Intersects(f2.Geometry))
                    {
                        continue;
                    }

                    var intersections = f1.Geometry.Intersections(f2.Geometry);
                    if (intersections == null || intersections.Count == 0)
                    {
                        continue;
                    }
                    Dictionary<string, object> attributes = new Dictionary<string, object>();
                    twoFeatures[1] = f2;
                    for (int i = 0; i < 2; i++)
                    {
                        foreach (var field in twoFeatures[i].Attributes
                            .Where(p => !FieldExtension.IsIdField(p.Key))
                            .Where(p => p.Key != Parameters.CreateTimeFieldName)
                            .Where(p => p.Key != Parameters.ModifiedTimeFieldName))
                        {
                            attributes.Add($"L{i + 1}_{field.Key}", field.Value);
                        }
                    }
                    foreach (var geom in intersections)
                    {
                        if (geom.IsEmpty || geom.GeometryType != layer.GeometryType)
                        {
                            continue;
                        }
                        Feature feature = layer.CreateFeature(attributes, geom);
                        targetFeatures.Add(feature);
                    }
                }
            }

            return targetFeatures;
        }
    }
}