using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using MapBoard.IO.Formats.Gps;
using FzLib.WPF.Dialog;
using MapBoard.Util;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static MapBoard.UI.GpxToolbox.GpxSymbolResources;
using MapBoard.Model;
using MapBoard.Mapping.Model;
using FzLib.Collection;
using PropertyChanged;
using System.Windows.Controls;
using Point = System.Windows.Point;
using System.Windows.Controls.Primitives;
using System.Diagnostics;

namespace MapBoard.Mapping
{
    /// <summary>
    /// GPX地图
    /// </summary>
    [DoNotNotify]
    public class GpxMapView : SceneView, IMapBoardGeoView
    {
        public Dictionary<GraphicsOverlay, TrackInfo> overlay2Track = new Dictionary<GraphicsOverlay, TrackInfo>();

        //当前点
        public Graphic pointGraphic = new Graphic()
        {
            Symbol = new PictureMarkerSymbol(new RuntimeImage(new Uri("./res/location.png", UriKind.Relative)))
            {
                OffsetY = 16,
                Width = 32,
                Height = 32
            }
        };

        /// <summary>
        /// 所有的<see cref="GpxMapView"/>对象
        /// </summary>
        private static List<GpxMapView> instances = new List<GpxMapView>();

        /// <summary>
        /// 游览中的覆盖层
        /// </summary>
        private GraphicsOverlay browseOverlay;

        /// <summary>
        /// 鼠标右键按下位置
        /// </summary>
        private Point mouseRightDownPosition;

        /// <summary>
        /// 选中的图形
        /// </summary>
        private HashSet<Graphic> selectedGraphics = new HashSet<Graphic>();

        /// <summary>
        /// 当前轨迹
        /// </summary>
        private TrackInfo selectedTrack = null;

        public GpxMapView()
        {
            instances.Add(this);
            Loaded += ArcMapViewLoaded;
            GeoViewTapped += MapViewTapped;
            this.SetHideWatermark();
            var currentPositionOverlay = new GraphicsOverlay()
            {

            };
            currentPositionOverlay.Graphics.Add(pointGraphic);
            GraphicsOverlays.Add(currentPositionOverlay);
            Overlay = new OverlayHelper(GraphicsOverlays, async p => await this.ZoomToGeometryAsync(p));
        }

        /// <summary>
        /// 一个GPX轨迹加载完成
        /// </summary>
        public event EventHandler<GpxLoadedEventArgs> GpxLoaded;

        /// <summary>
        /// 一个GPX点被点击
        /// </summary>
        public event EventHandler<PointSelectedEventArgs> PointSelected;

        /// <summary>
        /// 一个点被选中
        /// </summary>
        public event EventHandler PointSelecting;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 地图点击模式
        /// </summary>
        public enum MapTapModes
        {
            /// <summary>
            /// 就绪
            /// </summary>
            None,

            /// <summary>
            /// 选择选中的轨迹中的点
            /// </summary>
            SelectedLayer,

            /// <summary>
            /// 选择轨迹
            /// </summary>
            AllLayers,

            /// <summary>
            /// 框选
            /// </summary>
            Circle,
        }

        /// <summary>
        /// 所有的<see cref="GpxMapView"/>对象
        /// </summary>
        public static IReadOnlyList<GpxMapView> Instances => instances.AsReadOnly();

        public MapLayerCollection Layers { get; }
        /// <summary>
        /// 当前地图点击模式
        /// </summary>
        public MapTapModes MapTapMode { get; set; } = MapTapModes.None;

        public OverlayHelper Overlay { get; set; }
        /// <summary>
        /// 当前选择的轨迹
        /// </summary>
        public TrackInfo SelectedTrack
        {
            get => selectedTrack;
            set
            {
                selectedTrack = value;
                //这是为了保证在最前面吗？
                //if (value != null && value.Overlay != GraphicsOverlays.Last())
                //{
                //    GraphicsOverlays.Remove(value.Overlay);
                //    GraphicsOverlays.Add(value.Overlay);
                //}
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedTrajectorie.GpxDocument.Name"));
            }
        }

        /// <summary>
        /// 点击地图后显示点击位置的覆盖层
        /// </summary>
        public GraphicsOverlay TapOverlay { get; set; }

        /// <summary>
        /// 加载的轨迹
        /// </summary>
        public ObservableCollection<TrackInfo> Tracks { get; set; }
        /// <summary>
        /// 加载指定文件的GPX文件
        /// </summary>
        /// <param name="files"></param>
        public async Task<bool> LoadFilesAsync(IList<string> files, Action<int> indexChanged = null)
        {
            bool allLoaded = true;
            List<TrackInfo> loadedTrack = new List<TrackInfo>();
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                indexChanged?.Invoke(i);
                FileInfo fileInfo = new FileInfo(file);
                try
                {
                    if (!fileInfo.Exists)
                    {
                        throw new Exception(file + "不存在");
                    }
                    else if (fileInfo.Length > 10 * 1024 * 1024)
                    {
                        throw new Exception("gpx文件" + file + "大于1MB，跳过");
                    }
                    else if (fileInfo.Extension != ".gpx")
                    {
                        throw new Exception("文件" + file + "不是gpx");
                    }
                    else
                    {
                        var exist = Tracks.FirstOrDefault(p => p.FilePath == file);
                        if (exist == null)
                        {
                            loadedTrack.AddRange(await LoadGpxAsync(file, false));
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Log.Error("加载GPX失败", ex);
                    allLoaded = false;
                }
            }
            GpxLoaded?.Invoke(this, new GpxLoadedEventArgs(loadedTrack, false));
            return allLoaded;
        }

        /// <summary>
        /// 导入并加载一个轨迹
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="raiseEvent"></param>
        /// <returns></returns>
        public async Task<List<TrackInfo>> LoadGpxAsync(string filePath, bool raiseEvent)
        {
            GpxDocument gpx = await GpxSerializer.FromFileAsync(filePath);

            List<TrackInfo> loadedTrack = new List<TrackInfo>();
            for (int i = 0; i < gpx.Tracks.Count; i++)
            {

                var trackInfo = new TrackInfo(filePath, gpx, i);
                trackInfo.Initialize();
                try
                {
                    LoadTrack(trackInfo, false, false);
                    loadedTrack.Add(trackInfo);
                }
                catch (Exception ex)
                {
                    App.Log.Error("加载gpx文件" + filePath + "的Track(" + i.ToString() + ")错误", ex);
                }
            }
            if (raiseEvent)
            {
                GpxLoaded?.Invoke(this, new GpxLoadedEventArgs(loadedTrack.ToArray(), false));
            }

            return loadedTrack;
        }

        /// <summary>
        /// 加载一个轨迹
        /// </summary>
        /// <param name="trackInfo">轨迹信息</param>
        /// <param name="update">是否为更新，还是新建</param>
        /// <param name="raiseEvent">是否通知加载完成</param>
        /// <param name="gpxHeight">是否显示高程</param>
        public void LoadTrack(TrackInfo trackInfo, bool update = false, bool raiseEvent = false, bool? gpxHeight = null)
        {
            //如果更新的话，把原来的点和图形对应关系取消
            if (update)
            {
                trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.SimpleLine).Clear();
                trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.ColoredLine).Clear();
            }
            double minZ = 0;
            double mag = 0;
            var points = trackInfo.Track.GetPoints();
            try
            {
                //处理自动平滑
                if (Config.Instance.Gpx_AutoSmooth)
                {
                    GpxUtility.Smooth(points, Config.Instance.Gpx_AutoSmoothLevel, p => p.Z.Value, (p, v) => p.Z = v);
                    if (!Config.Instance.Gpx_AutoSmoothOnlyZ)
                    {
                        GpxUtility.Smooth(points, Config.Instance.Gpx_AutoSmoothLevel, p => p.X, (p, v) => p.X = v);
                        GpxUtility.Smooth(points, Config.Instance.Gpx_AutoSmoothLevel, p => p.Y, (p, v) => p.Y = v);
                    }
                    trackInfo.Smoothed = true;
                }

                //处理高程
                if (Config.Instance.Gpx_Height && Config.Instance.Gpx_RelativeHeight)
                {
                    var zs = points.Where(p => p.Z.HasValue);
                    minZ = zs.Any() ? zs.Min(p => p.Z.Value) : 0;
                }
                else
                {
                    minZ = 0;
                }
                mag = Config.Instance.Gpx_Height ? Config.Instance.Gpx_HeightExaggeratedMagnification : 1;

            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("存在没有高程信息的点", ex);
            }

            List<MapPoint> subMapPoints = new List<MapPoint>();
            MapPoint lastMapPoint = null;
            GpxPoint lastGpxPoint = null;

            //添加简单线
            Graphic lineGraphic = null;
            double lastZ = minZ;
            foreach (var gpxPoint in points)
            {
                if (Config.Instance.BasemapCoordinateSystem != CoordinateSystem.WGS84)
                {
                    var newP = CoordinateTransformation.Transformate(gpxPoint.ToMapPoint(), CoordinateSystem.WGS84, Config.Instance.BasemapCoordinateSystem);
                    gpxPoint.X = newP.X;
                    gpxPoint.Y = newP.Y;
                    gpxPoint.Z = newP.Z;
                }
                double z = gpxPoint.Z.HasValue ? gpxPoint.Z.Value : lastZ;
                lastZ = z;
                gpxPoint.Z *= mag;
                MapPoint mapPoint = new MapPoint(gpxPoint.X, gpxPoint.Y, z, SpatialReferences.Wgs84);


                //如果前后两个点离得太远了，那么就不连接
                if (lastMapPoint != null && (gpxPoint.Time.Value - lastGpxPoint.Time.Value).TotalMinutes > 5)
                {
                    //添加上一段
                    lineGraphic = new Graphic(new Polyline(subMapPoints));
                    trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.SimpleLine).Add(lineGraphic);

                    //添加中间的虚线
                    subMapPoints = new List<MapPoint>() { lastMapPoint, mapPoint };
                    lineGraphic = new Graphic(new Polyline(subMapPoints));
                    lineGraphic.Symbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Dash, Color.LightGray, 3);
                    trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.SimpleLine).Add(lineGraphic);

                    //准备下一段
                    subMapPoints = new List<MapPoint>();
                }

                //添加最后一段
                subMapPoints.Add(mapPoint);
                lastMapPoint = mapPoint;
                lastGpxPoint = gpxPoint;
            }
            //添加最后一段
            lineGraphic = new Graphic(new Polyline(subMapPoints));
            trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.SimpleLine).Add(lineGraphic);

            //添加速度彩色线
            GpxUtility.LoadColoredGpx(points, trackInfo.GetGraphic(TrackInfo.TrackSelectionDisplay.ColoredLine));

            //设置高程策略
            trackInfo.GetSceneProperties(TrackInfo.TrackSelectionDisplay.SimpleLine).SurfacePlacement =
                gpxHeight == true || !gpxHeight.HasValue && Config.Instance.Gpx_Height ? SurfacePlacement.Absolute : SurfacePlacement.DrapedFlat;
            trackInfo.GetSceneProperties(TrackInfo.TrackSelectionDisplay.ColoredLine).SurfacePlacement =
                gpxHeight == true || !gpxHeight.HasValue && Config.Instance.Gpx_Height ? SurfacePlacement.Absolute : SurfacePlacement.DrapedFlat;
            if (!update)
            {
                trackInfo.AddToOverlays(GraphicsOverlays, overlay2Track);
                Tracks?.Add(trackInfo);
            }
            if (raiseEvent)
            {
                GpxLoaded?.Invoke(this, new GpxLoadedEventArgs([trackInfo], false));
            }
        }

        /// <summary>
        /// 重新加载轨迹
        /// </summary>
        /// <param name="track"></param>
        /// <param name="raiseEvent"></param>
        /// <returns></returns>
        public async Task<List<TrackInfo>> ReloadGpxAsync(TrackInfo track, bool raiseEvent)
        {
            Tracks.Remove(track);
            var ts = await LoadGpxAsync(track.FilePath, false);
            if (raiseEvent)
            {
                GpxLoaded?.Invoke(this, new GpxLoadedEventArgs([.. ts], true));
            }
            return ts;
        }

        /// <summary>
        /// 选择某个轨迹开头到指定点之间的所有点
        /// </summary>
        /// <param name="point"></param>
        public void SelectPoint(GpxPoint point)
        {
            if (point == null)
            {
                UnselectAllPoints();
                return;
            }
            try
            {
                pointGraphic.Geometry = new MapPoint(point.X, point.Y, SpatialReferences.Wgs84);
            }
            catch (Exception ex)
            {
                //A null pointer: disconnected from collection.
            }
        }

        /// <summary>
        /// 游览模式中的点
        /// </summary>
        /// <param name="p"></param>
        public void SetLocation(MapPoint p)
        {
            if (browseOverlay == null)
            {
                browseOverlay = new GraphicsOverlay();
                GraphicsOverlays.Add(browseOverlay);
                browseOverlay.Renderer = BrowsePointRenderer;
            }
            var point = new MapPoint(p.X, p.Y, p.Z);
            if (browseOverlay.Graphics.Count == 0)
            {
                browseOverlay.Graphics.Add(new Graphic() { Geometry = point });
            }
            else
            {
                browseOverlay.Graphics[0].Geometry = point;
            }
        }

        /// <summary>
        /// 清空当前轨迹点的选择
        /// </summary>
        public void UnselectAllPoints()
        {
            if (SelectedTrack == null)
            {
                return;
            }
            selectedGraphics.Clear();
            try
            {
                pointGraphic.Geometry = null;
            }
            catch
            {
                //A null pointer: disconnected from collection.
            }
        }
        /// <summary>
        /// 鼠标右键按下
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            mouseRightDownPosition = e.GetPosition(this);
            base.OnPreviewMouseRightButtonDown(e);
        }

        /// <summary>
        /// 鼠标右键抬起
        /// </summary>
        /// <param name="e"></param>
        protected async override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonUp(e);
            var distance = Math.Sqrt(Math.Pow(e.GetPosition(this).X - mouseRightDownPosition.X, 2)
                + Math.Pow(e.GetPosition(this).Y - mouseRightDownPosition.Y, 2));
            if (distance < 5)
            {
                var location = (await ScreenToLocationAsync(e.GetPosition(this))).ToWgs84();
                ContextMenu menu = new ContextMenu();

                MenuItem item = new MenuItem()
                {
                    Header = LocationMenuUtility.GetLocationMenuString(location),
                };
                item.Click += (s, e) =>
                {
                    Clipboard.SetText(LocationMenuUtility.GetLocationClipboardString(location));
                    SnakeBar.Show("已复制经纬度到剪贴板");
                };
                menu.Items.Add(item);
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// 地图加载完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ArcMapViewLoaded(object sender, RoutedEventArgs e)
        {
            if (Tracks != null)
            {
                Tracks.CollectionChanged += TracksCollectionChanged;
            }
            await this.LoadBaseGeoViewAsync(Config.Instance.EnableBasemapCache);
        }

        /// <summary>
        /// 地图按下时，识别选择的轨迹或点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MapViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (MapTapMode != MapTapModes.AllLayers && MapTapMode != MapTapModes.SelectedLayer)
            {
                return;
            }
            PointSelecting?.Invoke(this, EventArgs.Empty);

            if (MapTapMode == MapTapModes.AllLayers) //识别所有轨迹
            {

                var result = await IdentifyGraphicsOverlaysAsync(e.Position, 10, false, 1);
                var overlay = result.Where(p => p.Error == null).FirstOrDefault()?.GraphicsOverlay;
                if (overlay != null)
                {
                    var track = overlay2Track[overlay];
                    PointSelected?.Invoke(this, new PointSelectedEventArgs(track, null));
                }
                else
                {
                    SnakeBar.Show("没有识别到任何轨迹");
                }
            }
            else //识别当前轨迹
            {
                var clickPoint = await ScreenToLocationAsync(e.Position);

                double tolerance = 1 * Camera.Location.Z * 1e-7;
                double mapTolerance = tolerance;
                Envelope envelope = new Envelope(clickPoint.X - mapTolerance, clickPoint.Y - mapTolerance, clickPoint.X + mapTolerance, clickPoint.Y + mapTolerance, SpatialReference);
                bool multiple = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

                UnselectAllPoints();

                foreach (var point in SelectedTrack.Track.GetPoints())
                {
                    if (GeometryEngine.Within(point.ToMapPoint(), envelope))
                    {
                        SelectPoint(point);
                        PointSelected?.Invoke(this, new PointSelectedEventArgs(SelectedTrack, point));
                        return;
                    }
                }

                PointSelected?.Invoke(this, new PointSelectedEventArgs(null, null));

                SnakeBar.Show("没有识别到任何点");
            }
        }

        /// <summary>
        /// 轨迹集合发生改变，同步修改GPX点和图形的对应关系，以及覆盖层
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TracksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    foreach (TrackInfo track in e.OldItems)
                    {
                        track.RemoveFromOverlays(GraphicsOverlays, overlay2Track);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    overlay2Track.Clear();
                    GraphicsOverlays.Clear();
                    break;
            }
        }

        /// <summary>
        /// GPX加载完成事件
        /// </summary>
        public class GpxLoadedEventArgs : EventArgs
        {
            public GpxLoadedEventArgs(IList<TrackInfo> track, bool update)
            {
                Track = track;
                Update = update;
            }

            public IList<TrackInfo> Track { get; private set; }
            public bool Update { get; private set; }
        }

        public class PointSelectedEventArgs : EventArgs
        {
            public PointSelectedEventArgs(TrackInfo trajectory, GpxPoint point)
            {
                Track = trajectory;
                Point = point;
            }

            public GpxPoint Point { get; private set; }
            public TrackInfo Track { get; private set; }
        }
    }
}