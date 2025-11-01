using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using Esri.ArcGISRuntime.Maui;
using MapBoard.Model;
using System.ComponentModel;
using FzLib;
using System.Text.RegularExpressions;
using Map = Esri.ArcGISRuntime.Mapping.Map;
using Esri.ArcGISRuntime.UI;
using static MapBoard.Util.GeometryUtility;

namespace MapBoard.Mapping
{
    public static class MapViewHelper
    {
        private static readonly List<BaseLayerInfo> DefaultBaseLayers = @"ESRI影像 https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}
OpenTopoMap https://tile.opentopomap.org/{z}/{x}/{y}.png
OpenStreetMap http://a.tile.openstreetmap.org/{z}/{x}/{y}.png
OpenStreetMap自行车 https://a.tile-cyclosm.openstreetmap.fr/cyclosm/{z}/{x}/{y}.png
OpenStreetMap公交 https://tileserver.memomaps.de/tilegen/{z}/{x}/{y}.png
谷歌卫星 https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}
谷歌街道 https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}
谷歌卫星+街道 https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}
高德地图 http://webrd01.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&scl=1&style=8&x={x}&y={y}&z={z}
高德卫星 https://webst01.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={z}"
            .Split(Environment.NewLine)
            .Select(p => p.Split(' '))
            .Select(p => new BaseLayerInfo(BaseLayerType.WebTiledLayer, p[1]) { Name = p[0] })
            .ToList();

        private static Regex rColorRamp = new Regex(@$"(?<type>{nameof(PresetColorRampType.Elevation)}|{nameof(PresetColorRampType.DemScreen)}|{nameof(PresetColorRampType.DemLight)})(,(?<size>[0-9]+))?");

        private static Regex rMinMaxStretch = new Regex(@"m(inmax)?\((?<min>[0-9\.:]+),(?<max>[0-9\.:]+)\)");

        private static Regex rPercentStretch = new Regex(@"p(ercent)?\((?<min>[0-9\.]+),(?<max>[0-9\.]+)\)");

        private static Regex rRgbRenderer = new Regex(@"r(gb)?\((?<r>[0-9\.]+),(?<g>[0-9\.]+),(?<b>[0-9\.]+)\)");

        private static Regex rStdStretch = new Regex(@"s(td)?\((?<factor>[0-9\.]+)\)");

        private static Regex rStretchRenderer = new Regex(@"s(tretch)?");


        /// <summary>
        /// 根据应用的<see cref="BaseLayerInfo"/>对象，获取ArcGIS的对应图层
        /// </summary>
        /// <param name="baseLayer"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        public static async Task<Layer> AddLayerAsync(Basemap basemap, BaseLayerInfo baseLayer, bool cache = true, int index = -1)
        {
            var type = baseLayer.Type;
            var arg = baseLayer.Path;
            Layer layer = type switch
            {
                BaseLayerType.WebTiledLayer => AddTiledLayer(arg, cache),
                BaseLayerType.RasterLayer => AddRasterLayer(arg),
                BaseLayerType.ShapefileLayer => AddShapefileLayer(arg),
                BaseLayerType.TpkLayer => AddTpkLayer(arg),
                BaseLayerType.WmsLayer => AddWmsLayer(arg),
                BaseLayerType.WmtsLayer => AddWmtsLayer(arg),
                _ => throw new InvalidEnumArgumentException("未知类型"),
            };
            layer.Name = baseLayer.Name;
            await layer.LoadAsync().TimeoutAfter(Parameters.LoadTimeout);
            if (index < 0)
            {
                basemap.BaseLayers.Add(layer);
            }
            else
            {
                basemap.BaseLayers.Insert(index, layer);
            }
            baseLayer.ApplyBaseLayerStyles(layer);
            return layer;
        }


        /// <summary>
        /// 为<see cref="SceneView"/>或<see cref="MapView"/>加载底图
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static async Task<ItemsOperationErrorCollection> LoadBaseGeoViewAsync(this GeoView map, bool cache)
        {
            ItemsOperationErrorCollection errors = new ItemsOperationErrorCollection();
            try
            {
                Basemap basemap = new Basemap();
                await basemap.LoadAsync();
                if (Config.Instance.BaseLayers.Count == 0)
                {
                    //如果没有底图，一般是首次打开软件的时候，那么从默认瓦片底图的文件中读取第一条加载
                    try
                    {
                        Config.Instance.BaseLayers.Add(DefaultBaseLayers[0]);
                    }
                    catch
                    {
                    }
                }
                //加载底图
                var baseLayers = Config.Instance.BaseLayers.Reverse<BaseLayerInfo>();
                foreach (var item in baseLayers)
                {
                    Layer layer = null;
                    try
                    {
                        if (item.Type is BaseLayerType.RasterLayer or BaseLayerType.ShapefileLayer or BaseLayerType.TpkLayer && !File.Exists(item.Path))
                        {
                            throw new IOException("找不到文件：" + item.Path);
                        }
                        layer = await AddLayerAsync(basemap, item, cache);
                    }
                    catch (TimeoutException ex)
                    {
                        layer?.CancelLoad();
                        //App.Log.Error($"加载底图{item.Name}({item.Path})超时", ex);
                        errors.Add($"加载底图{item.Name}({item.Path})超时", ex);
                    }
                    catch (Exception ex)
                    {
                        //App.Log.Error($"加载底图{item.Name}({item.Path})失败", ex);
                        errors.Add($"加载底图{item.Name}({item.Path})失败", ex);
                    }
                }

                await basemap.LoadAsync();
                if (map is SceneView s)
                {
                    if (s.Scene == null)
                    {
                        s.Scene = basemap.BaseLayers.Count == 0 ? new Scene() : new Scene(basemap);
                    }
                    else
                    {
                        s.Scene.Basemap = basemap;
                    }
                    await s.Scene.LoadAsync();
                }
                else if (map is MapView m)
                {
                    if (m.Map == null)
                    {
                        m.Map = basemap.BaseLayers.Count == 0 ? new Map(SpatialReferences.Wgs84) : new Map(basemap);
                    }
                    else
                    {
                        m.Map.Basemap = basemap;
                    }
                    await m.Map.LoadAsync();
                }
                return errors.Count == 0 ? null : errors;
            }
            catch (Exception ex)
            {
                //App.Log.Error("加载底图失败", ex);
                throw new Exception("加载底图失败", ex);
            }
        }


        /// <summary>
        /// 等待渲染结束
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        public static Task WaitForRenderCompletedAsync(this GeoView view)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            if (view.DrawStatus == DrawStatus.Completed)
            {
                tcs.SetResult();
                return tcs.Task;
            }
            view.DrawStatusChanged += View_DrawStatusChanged;
            return tcs.Task;

            void View_DrawStatusChanged(object sender, DrawStatusChangedEventArgs e)
            {
                if (e.Status == DrawStatus.Completed)
                {
                    view.DrawStatusChanged -= View_DrawStatusChanged;
                    tcs.SetResult();
                }
            }
        }

        private static RasterLayer AddRasterLayer(string path)
        {
            RasterLayer layer = new RasterLayer(path);
            return layer;
        }

        private static FeatureLayer AddShapefileLayer(string path)
        {
            ShapefileFeatureTable table = new ShapefileFeatureTable(path);
            FeatureLayer layer = new FeatureLayer(table);
            return layer;
        }
        private static string HttpUserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; QQWubi 133; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CIBA; InfoPath.2)";
        private static ImageTiledLayer AddTiledLayer(string url, bool cache)
        {
            XYZTiledLayer layer = XYZTiledLayer.Create(url, HttpUserAgent, cache);
            //CacheableWebTiledLayer layer = CacheableWebTiledLayer.Create(url);
            //WebTiledLayer layer = new WebTiledLayer(url.Replace("{x}", "{col}").Replace("{y}", "{row}").Replace("{z}", "{level}"));
            return layer;
        }

        private static ArcGISTiledLayer AddTpkLayer(string path)
        {
            TileCache cache = new TileCache(path);
            ArcGISTiledLayer layer = new ArcGISTiledLayer(cache);
            return layer;
        }

        private static WmsLayer AddWmsLayer(string url)
        {
            string[] items = url.Split('|');
            if (items.Length < 2)
            {
                throw new ArgumentException("WMS的参数过少");
            }
            WmsLayer layer = new WmsLayer(new Uri(items[0]), items.Skip(1));
            return layer;
        }

        private static WmtsLayer AddWmtsLayer(string url)
        {
            string[] items = url.Split('|');
            if (items.Length < 2)
            {
                throw new ArgumentException("WMS的参数过少");
            }
            WmtsLayer layer = new WmtsLayer(new Uri(items[0]), items[1]);
            return layer;
        }

        /// <summary>
        /// 应用底图样式，包括透明度等、亮度等、拉伸、渲染器、缩放等级。
        /// </summary>
        /// <param name="baseLayer"></param>
        /// <param name="arcLayer"></param>
        public static void ApplyBaseLayerStyles(this BaseLayerInfo baseLayer, Layer arcLayer)
        {
            arcLayer.Opacity = baseLayer.Opacity;
            arcLayer.Id = baseLayer.TempID.ToString();
            arcLayer.IsVisible = baseLayer.Visible;

            if (arcLayer is ImageAdjustmentLayer i)
            {
                i.Gamma = baseLayer.Gamma;
                i.Brightness = baseLayer.Brightness;
                i.Contrast = baseLayer.Contrast;
            }

            if (arcLayer is RasterLayer raster)
            {
                if (string.IsNullOrEmpty(baseLayer.Renderer))
                {
                    return;
                }
                if (string.IsNullOrEmpty(baseLayer.StretchParameters))
                {
                    return;
                }
                RasterRenderer renderer = null;
                StretchParameters stretchParameters = null;
                ColorRamp colorRamp = null;
                Match match = null;

                if (baseLayer.ColorRampParameters != null)
                {
                    try
                    {
                        if (rColorRamp.IsMatch(baseLayer.ColorRampParameters))
                        {
                            match = rColorRamp.Match(baseLayer.ColorRampParameters);
                            var type = Enum.Parse<PresetColorRampType>(match.Groups["type"].Value);
                            uint size = 256;
                            if (match.Groups.ContainsKey("size") && match.Groups["size"].Value != "")
                            {
                                size = uint.Parse(match.Groups["size"].Value);
                            }
                            colorRamp = ColorRamp.Create(type, size);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("颜色渐变参数错误");
                    }
                }
                try
                {
                    if (rMinMaxStretch.IsMatch(baseLayer.StretchParameters))
                    {
                        match = rMinMaxStretch.Match(baseLayer.StretchParameters);
                        var mins = match.Groups["min"].Value.Split(':').Select(double.Parse);
                        var maxs = match.Groups["max"].Value.Split(':').Select(double.Parse);
                        stretchParameters = new MinMaxStretchParameters(mins, maxs);
                    }
                    else if (rStdStretch.IsMatch(baseLayer.StretchParameters))
                    {
                        match = rStdStretch.Match(baseLayer.StretchParameters);
                        var factor = double.Parse(match.Groups["factor"].Value);
                        stretchParameters = new StandardDeviationStretchParameters(factor);
                    }
                    else if (rPercentStretch.IsMatch(baseLayer.StretchParameters))
                    {
                        match = rPercentStretch.Match(baseLayer.StretchParameters);
                        var min = double.Parse(match.Groups["min"].Value);
                        var max = double.Parse(match.Groups["max"].Value);
                        stretchParameters = new PercentClipStretchParameters(min, max);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("拉伸参数错误");
                }


                try
                {
                    if (rRgbRenderer.IsMatch(baseLayer.Renderer))
                    {
                        match = rRgbRenderer.Match(baseLayer.Renderer);
                        int r = int.Parse(match.Groups["r"].Value);
                        int g = int.Parse(match.Groups["g"].Value);
                        int b = int.Parse(match.Groups["b"].Value);
                        (r, g, b) = (r - 1, g - 1, b - 1);
                        renderer = new RgbRenderer(stretchParameters, new[] { r, g, b }, null, true);
                    }
                    else if (rStretchRenderer.IsMatch(baseLayer.Renderer))
                    {
                        match = rStretchRenderer.Match(baseLayer.Renderer);
                        renderer = new StretchRenderer(stretchParameters, null, true, colorRamp);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("渲染器参数错误");
                }

                raster.Renderer = renderer;


            }

            if (arcLayer is XYZTiledLayer xyz)
            {
                xyz.MinLevel = baseLayer.MinLevel;
                xyz.MaxLevel = baseLayer.MaxLevel;
            }

        }
        /// <summary>
        /// 缩放到指定图形
        /// </summary>
        /// <param name="mapView"></param>
        /// <param name="geometry"></param>
        /// <param name="autoExtent"></param>
        /// <returns></returns>
        public static async Task ZoomToGeometryAsync(this GeoView mapView, Geometry geometry, bool autoExtent = true)
        {
            if (geometry is MapPoint || geometry is Multipoint m && m.Points.Count == 1 || geometry.Extent.Width == 0 || geometry.Extent.Height == 0)
            {
                if (geometry.SpatialReference.Wkid != SpatialReferences.WebMercator.Wkid)
                {
                    geometry = geometry.Project(SpatialReferences.WebMercator);
                }
                geometry = geometry.Buffer(500);
            }
            var extent = geometry.Extent;
            if (double.IsNaN(extent.Width) || double.IsNaN(extent.Height) || extent.Width == 0 || extent.Height == 0)
            {
                return;
            }
            if (mapView is MapView mv)
            {
                await mv.SetViewpointGeometryAsync(geometry, autoExtent ? Config.WatermarkHeight : 0);
            }
            else if (mapView is SceneView sv)
            {
                await sv.SetViewpointAsync(new Viewpoint(geometry));
            }
            else
            {
                throw new ArgumentException("必须为MapView类或SceneView类", nameof(mapView));
            }
        }

        /// <summary>
        /// 缩放到记忆的位置
        /// </summary>
        /// <returns></returns>
        public static async Task TryZoomToLastExtent<T>(this T mapView) where T : MainMapView
        {
            if (mapView.Layers.MapViewExtentJson != null)
            {
                if (!mapView.Layers.MapViewExtentJson.Equals(mapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry)?.TargetGeometry?.ToJson()))
                {
                    try
                    {
                        Envelope envelope = Geometry.FromJson(mapView.Layers.MapViewExtentJson) as Envelope;
                        var point1 = new MapPoint(envelope.XMin, envelope.YMin);
                        var point2 = new MapPoint(envelope.XMax, envelope.YMax);
                        envelope = new Envelope(point1.RegularizeWebMercatorPoint(), point2.RegularizeWebMercatorPoint());
                        await mapView.ZoomToGeometryAsync(envelope, false);
                    }
                    catch
                    {
                    }
                }
                else
                {

                }
            }
        }
    }

}
