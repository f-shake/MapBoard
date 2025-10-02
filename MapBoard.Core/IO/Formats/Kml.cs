using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Ogc;
using Esri.ArcGISRuntime.Symbology;
using FzLib.Collection;
using MapBoard.IO.Abstractions;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using Sharpen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MapBoard.IO.Formats
{
    internal class Kml : IFeatureTableExporter, IMemoryLayerImporter, IMapExporter
    {
        public async Task ExportAsync(string path, IEnumerable<IMapLayerInfo> layers)
        {
            KmlDocument kml = new KmlDocument();
            await Task.Run(async () =>
            {
                foreach (var layer in layers)
                {
                    KmlDocument subKml = new KmlDocument() { Name = layer.Name };
                    kml.ChildNodes.Add(subKml);
                    await AddToKmlAsync(layer, subKml.ChildNodes);
                }
            });
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            await kml.SaveAsAsync(path);
        }

        public async Task ExportFeatureTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features)
        {
            KmlDocument kml = new KmlDocument() { Name = layer.Name };
            await Task.Run(async () =>
            {
                await AddToKmlAsync(layer, kml.ChildNodes, features);
            });
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            await kml.SaveAsAsync(path);
        }

        public async ValueTask<IEnumerable<SimpleLayer>> GetLayersAsync(string path)
        {
            KmlDataset kml = new KmlDataset(new Uri(path));
            await kml.LoadAsync();
            List<SimpleFeature> points = new List<SimpleFeature>();
            List<SimpleFeature> lines = new List<SimpleFeature>();
            List<SimpleFeature> polygons = new List<SimpleFeature>();
            var fields = new List<FieldInfo>()
            {
                new FieldInfo(nameof(KmlPlacemark.Name), "名称", FieldInfoType.Text),
                new FieldInfo(nameof(KmlPlacemark.Description), "描述", FieldInfoType.Text),
            };

            await Task.Run(() =>
            {
                foreach (var node in GetAllKmlPlacemark(kml))
                {
                    var dic = new Dictionary<string, object>()
                    {
                        [nameof(KmlPlacemark.Name)] = node.Name,
                        [nameof(KmlPlacemark.Description)] = node.Description
                    };
                    switch (node.GraphicType)
                    {
                        case KmlGraphicType.Point:
                            points.Add(new SimpleFeature(dic, node.Geometry.RemoveZAndM()));
                            break;
                        case KmlGraphicType.Polyline:
                            lines.Add(new SimpleFeature(dic, node.Geometry.RemoveZAndM()));
                            break;
                        case KmlGraphicType.Polygon:
                            polygons.Add(new SimpleFeature(dic, node.Geometry.RemoveZAndM()));
                            break;
                    }
                }
            });
            string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));

            List<SimpleLayer> layers = new List<SimpleLayer>();
            if (points.Count > 0)
            {
                layers.Add(new SimpleLayer($"{name}（点）",
                    GeometryType.Point, fields, SpatialReferences.Wgs84, points));
            }
            if (lines.Count > 0)
            {
                layers.Add(new SimpleLayer($"{name}（线）",
                    GeometryType.Polyline, fields, SpatialReferences.Wgs84, lines));
            }
            if (polygons.Count > 0)
            {
                layers.Add(new SimpleLayer($"{name}（面）",
                    GeometryType.Polygon, fields, SpatialReferences.Wgs84, polygons));
            }
            return layers;
        }

        /// <summary>
        /// 将<see cref="KmlNode"/>加入到图层中
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static async Task AddToKmlAsync(IMapLayerInfo layer, KmlNodeCollection nodes, IEnumerable<Feature> features = null)
        {
            features ??= await layer.GetAllFeaturesAsync();
            foreach (var feature in features)
            {
                foreach (var g in feature.Geometry.EnsureSinglePart())
                {
                    var geometry = new KmlGeometry(g, KmlAltitudeMode.ClampToGround);
                    var placemark = new KmlPlacemark(geometry);
                    foreach (var p in feature.Attributes)
                    {
                        placemark.Attributes.AddOrSetValue(p.Key, p.Value);
                    }
                    placemark.Style = new KmlStyle();
                    SymbolInfo symbol = null;
                    if (layer.Renderer.HasCustomSymbols)
                    {
                        var c = feature.Attributes[layer.Renderer.KeyFieldName].ToString();
                        if (layer.Renderer.Symbols.ContainsKey(c))
                        {
                            symbol = layer.Renderer.Symbols[c];
                        }
                    }
                    if (symbol == null)
                    {
                        symbol = layer.Renderer.DefaultSymbol ?? layer.GetDefaultSymbol();
                    }
                    switch (layer.GeometryType)
                    {
                        case GeometryType.Point:
                            placemark.Style.LabelStyle = new KmlLabelStyle(symbol.FillColor, 1);
                            break;

                        case GeometryType.Polyline:
                            placemark.Style.LineStyle = new KmlLineStyle(symbol.LineColor, symbol.OutlineWidth);
                            break;

                        case GeometryType.Polygon:
                            placemark.Style.PolygonStyle = new KmlPolygonStyle(symbol.FillColor);
                            placemark.Style.PolygonStyle.IsFilled = symbol.FillStyle != (int)SimpleFillSymbolStyle.Null;
                            if (symbol.OutlineWidth > 0)
                            {
                                placemark.Style.PolygonStyle.IsOutlined = true;
                                placemark.Style.LineStyle = new KmlLineStyle(symbol.LineColor, symbol.OutlineWidth);
                            }
                            else
                            {
                                placemark.Style.PolygonStyle.IsOutlined = false;
                            }
                            break;
                    }
                    placemark.Description = string.Join('\n', feature.Attributes.Select(p => $"{p.Key}：{p.Value}"));
                    nodes.Add(placemark);
                }
            }
        }

        /// <summary>
        /// 获取KML中所有的图形
        /// </summary>
        /// <param name="dataset"></param>
        /// <returns></returns>
        private static IEnumerable<KmlPlacemark> GetAllKmlPlacemark(KmlDataset dataset)
        {
            foreach (var node in dataset.RootNodes)
            {
                foreach (var childNode in AddAll(node))
                {
                    yield return childNode;
                }
            }
            IEnumerable<KmlPlacemark> AddAll(KmlNode parentNode)
            {
                switch (parentNode)
                {
                    case KmlContainer container:
                        foreach (var node in container.ChildNodes)
                        {
                            foreach (var childNode in AddAll(node))
                            {
                                yield return childNode;
                            }
                        }
                        break;
                    case KmlPlacemark placemark:
                        yield return placemark;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}