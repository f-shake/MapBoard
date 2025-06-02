using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Ogc;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UI.Editing;
using FzLib.WPF.Dialog;
using MapBoard.Model;
using ModernWpf.FzExtension.CommonDialog;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static MapBoard.Mapping.Model.TileInfoExtension;
using static MapBoard.Util.GeometryUtility;

namespace MapBoard.Mapping
{
    /// <summary>
    /// 瓦片下载地图
    /// </summary>
    [DoNotNotify]
    public class TileDownloaderMapView : MapView
    {
        /// <summary>
        /// 所有<see cref="TileDownloaderMapView"/>实例
        /// </summary>
        private static List<TileDownloaderMapView> instances = new List<TileDownloaderMapView>();

        /// <summary>
        /// 当前数据源的图层
        /// </summary>
        private Layer baseLayer;

        /// <summary>
        /// 用于显示当前下载位置的图形
        /// </summary>
        private Graphic graphic = new Graphic();

        /// <summary>
        /// 是否正在选择范围
        /// </summary>
        private bool isSelecting = false;

        /// <summary>
        /// 是否已经加载
        /// </summary>
        private bool loaded = false;

        /// <summary>
        /// 覆盖层
        /// </summary>
        private GraphicsOverlay overlay = new GraphicsOverlay();

        /// <summary>
        /// 不懂是什么
        /// </summary>
        private MapPoint point = new MapPoint(0, 0);

        public TileDownloaderMapView()
        {
            instances.Add(this);
            Loaded += ArcMapViewLoaded;
            AllowDrop = true;
            IsAttributionTextVisible = false;
            this.SetHideWatermark();

            GraphicsOverlays.Add(overlay);
            graphic.Geometry = point;
            graphic.IsVisible = false;
            graphic.Symbol = new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, Color.FromArgb(0x77, 0xFF, 0x00, 0x00), new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Color.Green, 4));
            overlay.Graphics.Add(graphic);
            Map = new Map(SpatialReferences.WebMercator);

            Map.LoadAsync().Wait();

            Config.Instance.Tile_Urls.PropertyChanged += UrlCollectionPropertyChanged;
        }
        public event EventHandler SelectBoundaryComplete;

        /// <summary>
        /// 所有<see cref="TileDownloaderMapView"/>实例
        /// </summary>
        public static IReadOnlyList<TileDownloaderMapView> Instances => instances.AsReadOnly();

        /// <summary>
        /// 下载范围边界信息
        /// </summary>
        public Envelope Boundary { get; private set; }

        /// <summary>
        /// 是否为本地地图
        /// </summary>
        public bool IsLocal { get; set; } = false;

        /// <summary>
        /// 加载当前数据源到地图
        /// </summary>
        /// <returns></returns>
        public async Task LoadAsync()
        {
            loaded = true;
            try
            {
                if (IsLocal)
                {
                    baseLayer = XYZTiledLayer.Create($"http://127.0.0.1:" + Config.Instance.Tile_ServerPort + "/{z}/{x}-{y}."+Config.Instance.Tile_FormatExtension, null);
                }
                else
                {
                    if (Config.Instance.Tile_Urls.SelectedUrl == null || Config.Instance.Tile_Urls.SelectedUrl.Path == null)
                    {
                        return;
                    }
                    baseLayer = XYZTiledLayer.Create(Config.Instance.Tile_Urls.SelectedUrl, Config.Instance.HttpUserAgent);
                }
                Basemap basemap = new Basemap(baseLayer);

                await basemap.LoadAsync();

                Map.Basemap = basemap;
            }
            catch (Exception ex)
            {
                App.Log.Error("加载地图失败", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "加载地图失败");
                return;
            }
        }
        private GeometryEditorTool GetGeometryEditorTool()
        {
            var tool = ShapeTool.Create(ShapeToolType.Rectangle);
            tool.Configuration.AllowRotatingSelectedElement = false;
            return tool;
        }
        /// <summary>
        /// 选择下载范围
        /// </summary>
        /// <returns></returns>
        public async Task SelectAsync()
        {
            if (!isSelecting)
            {
                SnakeBar.Show("开始框选");

                isSelecting = true;
                GeometryEditor.Start(GeometryType.Polygon);
                GeometryEditor.Tool = GetGeometryEditorTool();
            }
            else
            {
                GeometryEditor.Stop();
                SnakeBar.Show("终止框选");
                isSelecting = false;
            }
        }

        /// <summary>
        /// 设置选择框的范围
        /// </summary>
        /// <param name="range"></param>
        public void SetBoundary(GeoRect<double> range)
        {
            Polygon polygon = RangeToPolygon(range);
            Geometry geometry = GeometryEngine.Project(polygon, SpatialReference);
            GeometryEditor.Start(geometry);
            GeometryEditor.Tool = GetGeometryEditorTool();
            isSelecting = true;
            this.ZoomToGeometryAsync(polygon);
        }
        /// <summary>
        /// 设置当前下载位置
        /// </summary>
        /// <param name="win"></param>
        /// <param name="tile"></param>
        public void ShowPosition(Window win, TileInfo tile)
        {
            if (tile == null)
            {
                graphic.IsVisible = false;
                return;
            }
            bool ignore = false;
            win.Dispatcher.Invoke(() =>
            {
                if (win.WindowState == WindowState.Minimized)
                {
                    ignore = true; ;
                }
            });
            if (ignore)
            {
                return;
            }
            var range = tile.GetExtent(Config.Instance.Tile_TileSize.width, Config.Instance.Tile_TileSize.height);

            graphic.IsVisible = true;
            graphic.Geometry = RangeToPolygon(range);
        }

        protected override async void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (GeometryEditor.Geometry != null)
            {
                await Task.Delay(500);
                Boundary = GeometryEditor.Geometry.ToWgs84().Extent;
                SelectBoundaryComplete?.Invoke(this, new EventArgs());
            }
        }

        private async void ArcMapViewLoaded(object sender, RoutedEventArgs e)
        {
            if (loaded)
            {
                return;
            }
            if (!IsLocal)
            {
                await LoadAsync();
            }
        }


        /// <summary>
        /// 将选择范围转换为多边形
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        private Polygon RangeToPolygon(GeoRect<double> range)
        {
            MapPoint wn = new MapPoint(range.XMin_Left, range.YMax_Top, SpatialReferences.Wgs84);
            MapPoint en = new MapPoint(range.XMax_Right, range.YMax_Top, SpatialReferences.Wgs84);
            MapPoint es = new MapPoint(range.XMax_Right, range.YMin_Bottom, SpatialReferences.Wgs84);
            MapPoint ws = new MapPoint(range.XMin_Left, range.YMin_Bottom, SpatialReferences.Wgs84);

            Polygon polygon = new Polygon(new MapPoint[] { wn, en, es, ws }, SpatialReferences.Wgs84);
            return polygon;
        }

        /// <summary>
        /// 选择的数据源修改后，重新加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UrlCollectionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Config.Tile_Urls.SelectedUrl))
            {
                await LoadAsync();
            }
        }
    }
}