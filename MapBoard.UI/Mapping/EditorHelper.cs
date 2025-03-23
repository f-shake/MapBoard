using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI;
using FzLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MapBoard.Mapping.Model;
using System.Windows.Controls;
using MapBoard.Util;
using Esri.ArcGISRuntime.Symbology;
using System.Windows;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using FzLib.WPF.Dialog;
using Esri.ArcGISRuntime.UI.Editing;
using MapBoard.Model;

namespace MapBoard.Mapping
{
    /// <summary>
    /// 图形编辑器帮助类
    /// </summary>
    public class EditorHelper : INotifyPropertyChanged
    {
        /// <summary>
        /// 正在编辑的要素
        /// </summary>
        private Feature editingFeature;

        /// <summary>
        /// 绘制完成的图形
        /// </summary>
        private Geometry geometry = null;

        /// <summary>
        /// 是否正在寻找最近的点
        /// </summary>
        private bool isSearchingNearestPoint = false;

        /// <summary>
        /// 最近的任意点
        /// </summary>
        private MapPoint nearestPoint;

        /// <summary>
        /// 最近结点
        /// </summary>
        private MapPoint nearestVertex;

        private bool oneTapPoint = false;
        private bool stopWhenGeometryChanged = false;
        private TaskCompletionSource tcs;
        public EditorHelper(MainMapView mapView)
        {
            MapView = mapView;
            GeometryEditor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GeometryEditor.Geometry))
                {
                    GeometryChanged?.Invoke(s, new GeometryUpdatedEventArgs(GeometryEditor.Geometry));
                    if (oneTapPoint && GeometryEditor.Geometry is MapPoint p && !double.IsNaN(p.X))
                    {
                        oneTapPoint = false;
                        StopAndSave();
                    }
                    if (stopWhenGeometryChanged)
                    {
                        StopAndSave();
                    }
                }
                else if (e.PropertyName == nameof(GeometryEditor.SelectedElement))
                {
                    SelectedElementChanged?.Invoke(s, new EventArgs());
                }
            };
            MapView.PreviewMouseRightButtonDown += MapView_PreviewMouseRightButtonDown;
            mapView.PreviewMouseMove += MapView_PreviewMouseMove;
            mapView.GeoViewTapped += MapviewTapped;
        }

        /// <summary>
        /// 编辑器状态发生改变
        /// </summary>
        public event EventHandler<EditorStatusChangedEventArgs> EditorStatusChanged;

        /// <summary>
        /// 编辑时图形发生改变
        /// </summary>
        public event EventHandler<GeometryUpdatedEventArgs> GeometryChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 选择的结点发生改变
        /// </summary>
        public event EventHandler SelectedElementChanged;
        /// <summary>
        /// 正在编辑的要素的属性
        /// </summary>
        public FeatureAttributeCollection Attributes { get; set; }

        public GeometryEditor GeometryEditor => MapView.GeometryEditor;
        public MapLayerCollection Layers => MapView.Layers;

        public MainMapView MapView { get; }

        /// <summary>
        /// 当前正在进行的绘制操作的类型
        /// </summary>
        public EditMode Mode { get; private set; }
        /// <summary>
        /// 停止并不保存
        /// </summary>
        public void Cancel()
        {
            if (Mode == EditMode.None)
            {
                return;
            }
            geometry = null;
            Stop();
        }

        /// <summary>
        /// 绘制新的图形
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        public async Task DrawAsync(GeometryType type, GeometryEditorTool tool)
        {
            if (Layers.Selected is not IMapLayerInfo)
            {
                throw new NotSupportedException("选中的图层不支持编辑");
            }
            var layer = Layers.Selected as IMapLayerInfo;
            if (Attributes != null && Config.Instance.RemainAttribute)
            {
                var oldAttributes = Attributes;
                Attributes = FeatureAttributeCollection.Empty(Layers.Selected);
                foreach (var attribute in oldAttributes.Attributes
                    .Where(p => p.Name is not (Parameters.CreateTimeFieldName or Parameters.ModifiedTimeFieldName)))
                {
                    if (Attributes.Attributes.FirstOrDefault(p => p.Name == attribute.Name) is FeatureAttribute newAttr
                        && newAttr.IsCompatibleType(attribute.Value, out object newValue))
                    {
                        newAttr.Value = newValue;
                    }
                }
            }
            else
            {
                Attributes = FeatureAttributeCollection.Empty(Layers.Selected);
            }
            PrepareToDraw(EditMode.Create);
            StartDraw(type, tool ?? new VertexTool());
            await WaitForStopAsync();
            if (geometry != null)
            {
                Feature feature = layer.CreateFeature();
                feature.Geometry = geometry;
                Attributes.SaveToFeature(feature);
                await layer.AddFeatureAsync(feature, FeaturesChangedSource.Draw);
            }
        }

        /// <summary>
        /// 编辑
        /// </summary>
        /// <returns></returns>
        public async Task EditAsync(IMapLayerInfo layer, Feature feature)
        {
            Attributes = FeatureAttributeCollection.FromFeature(layer, feature);
            PrepareToDraw(EditMode.Edit);
            editingFeature = feature;

            InitializeSnapping();
            GeometryEditor.Start(feature.Geometry.SpatialReference != MapView.Map.SpatialReference ?
               GeometryEngine.Project(feature.Geometry, MapView.Map.SpatialReference) : feature.Geometry);
            await WaitForStopAsync();
            editingFeature = null;
            if (geometry != null)
            {
                UpdatedFeature newFeature = new UpdatedFeature(feature, feature.Geometry, new Dictionary<string, object>(feature.Attributes));
                feature.Geometry = geometry;
                Attributes.SaveToFeature(feature);
                await layer.UpdateFeatureAsync(newFeature, FeaturesChangedSource.Edit);
            }
        }

        /// <summary>
        /// 获取一个用于选择要素矩形
        /// </summary>
        /// <returns></returns>
        public async Task<Envelope> GetEmptyRectangleAsync()
        {
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Polygon);
            ShapeTool st = ShapeTool.Create(ShapeToolType.Rectangle);
            GeometryEditor.Tool = st;
            stopWhenGeometryChanged = true;
            await WaitForStopAsync();
            if (geometry is Polygon rect)
            {
                return rect.Extent;
            }
            return null;
        }

        /// 获取一个多点
        /// </summary>
        /// <returns></returns>
        public async Task<Multipoint> GetMultiPointAsync()
        {
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Multipoint);
            await WaitForStopAsync();
            Cancel();
            if (geometry is Multipoint point)
            {
                return point;
            }
            return null;
        }

        /// <summary>
        /// 获取一个点
        /// </summary>
        /// <returns></returns>
        public async Task<MapPoint> GetPointAsync()
        {
            oneTapPoint = true;
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Point);
            await WaitForStopAsync();
            oneTapPoint = false;
            if (geometry is MapPoint point)
            {
                return point;
            }
            return null;
        }

        /// <summary>
        /// 获取一个多边形
        /// </summary>
        /// <returns></returns>
        public async Task<Polygon> GetPolygonAsync()
        {
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Polygon);
            await WaitForStopAsync();
            StopAndSave();
            if (geometry is Polygon rect)
            {
                return rect;
            }
            return null;
        }

        /// <summary>
        /// 获取一条折线
        /// </summary>
        /// <returns></returns>
        public async Task<Polyline> GetPolylineAsync()
        {
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Polyline);
            await WaitForStopAsync();
            if (geometry is Polyline line)
            {
                if (line.Parts[0].PointCount > 1)
                {
                    return line;
                }
                return null;
            }
            return null;
        }

        /// <summary>
        /// 获取一个矩形
        /// </summary>
        /// <returns></returns>
        public async Task<Envelope> GetRectangleAsync()
        {
            PrepareToDraw(EditMode.GetGeometry);
            StartDraw(GeometryType.Polygon);
            ShapeTool st = ShapeTool.Create(ShapeToolType.Rectangle);
            GeometryEditor.Tool = st;
            await WaitForStopAsync();
            StopAndSave();
            if (geometry is Polygon rect)
            {
                return rect.Extent;
            }
            return null;
        }

        /// <summary>
        /// 测量面积
        /// </summary>
        /// <returns></returns>
        public void MeasureArea()
        {
            PrepareToDraw(EditMode.MeasureArea);
            StartDraw(GeometryType.Polygon);
        }

        /// <summary>
        /// 测量长度
        /// </summary>
        /// <returns></returns>
        public void MeasureLength()
        {
            PrepareToDraw(EditMode.MeasureLength);
            StartDraw(GeometryType.Polyline);
        }

        /// <summary>
        /// 停止并保存当前结果
        /// </summary>
        public void StopAndSave()
        {
            if (Mode == EditMode.None)
            {
                return;
            }
            geometry = GeometryEditor.Geometry?.EnsureValid();
            Stop();
        }

        /// <summary>
        /// 选择最近的结点
        /// </summary>
        /// <param name="position"></param>
        private void AddPointToSketchEditor(MapPoint point)
        {
            if (point != null)
            {
                if (GeometryEditor.Geometry == null)
                {
                    switch (GeometryEditor.Geometry.GeometryType)
                    {
                        case GeometryType.Point:
                            GeometryEditor.ReplaceGeometry(point);
                            break;

                        case GeometryType.Multipoint:
                            GeometryEditor.ReplaceGeometry(new Multipoint(new[] { point }));
                            break;

                        case GeometryType.Polyline:
                            GeometryEditor.ReplaceGeometry(new Polyline(new[] { point }));
                            break;

                        case GeometryType.Polygon:
                            GeometryEditor.ReplaceGeometry(new Polygon(new[] { point }));
                            break;
                    }
                    return;
                }
                GeometryEditor.InsertVertex(point.ToWebMercator());
            }
        }

        /// <summary>
        /// 判断是否能够捕捉最近点
        /// </summary>
        /// <returns></returns>
        private bool CanCatchNearestPoint()
        {
            return GeometryEditor.Tool is VertexTool;
        }

        /// <summary>
        /// 获取离指定位置最近的结点
        /// </summary>
        /// <param name="position"></param>
        /// <param name="excludeSelf">是否排除当前正在编辑的图层</param>
        /// <returns></returns>
        private async Task<(MapPoint, MapPoint)> GetNearestVertexAndPointAsync(Point position, bool excludeSelf)
        {
            var location = MapView.ScreenToLocation(position).ToWgs84();
            IReadOnlyList<IdentifyLayerResult> results = null;
            try
            {
                results = (await MapView.IdentifyLayersAsync(position, Config.Instance.CatchDistance, false, int.MaxValue));
            }
            catch (Exception ex)
            {
                App.Log.Error("识别最近的图形失败", ex);
                return (null, null);
            }
            MapPoint minVertex = null;
            MapPoint minPoint = null;
            double minVertexDistance = double.MaxValue;
            double minPointDistance = double.MaxValue;
            foreach (var geometry in results
                        .Where(p => p.LayerContent.IsVisible)//图层可见
                        .Where(p => p.LayerContent is FeatureLayer)//需要矢量图层
                        .Select(p => new { Layer = Layers.Find(p.LayerContent), Elements = p.GeoElements })
                        .Where(p => p.Layer?.Interaction?.CanCatch ?? false)//图层可捕捉
                        .SelectMany(p => p.Elements)
                        .Where(p => !excludeSelf || editingFeature == null || (p as Feature).GetID() != editingFeature.GetID())//直接捕捉配置下，排除正在编辑的图形
                        .Select(p => p.Geometry)
                        .Select(p => p is Polygon ? (p as Polygon).ToPolyline() : p))//将面转为线
            {
                var nearestVertexResult = GeometryEngine.NearestVertex(geometry, location);
                if (nearestVertexResult != null && nearestVertexResult.Distance < minVertexDistance)
                {
                    minVertex = nearestVertexResult.Coordinate;
                    minVertexDistance = nearestVertexResult.Distance;
                }
                var nearestPointResult = GeometryEngine.NearestCoordinate(geometry, location);
                if (nearestPointResult != null && nearestPointResult.Distance < minPointDistance)
                {
                    minPoint = nearestPointResult.Coordinate;
                    minPointDistance = nearestPointResult.Distance;
                }
            }
            //最近任意点一定在要求的范围内（面除外），但是最近节点可能大于甚至远大于范围

            if (minVertex != null && !CheckInDistance(minVertex))
            {
                minVertex = null;
            }
            if (minPoint != null && !CheckInDistance(minPoint))
            {
                minPoint = null;
            }

            return (minVertex, minPoint);

            bool CheckInDistance(MapPoint p)
            {
                var screenPoint = MapView.LocationToScreen(p);
                var distance = Math.Sqrt((Math.Pow(screenPoint.X - position.X, 2) + Math.Pow(screenPoint.Y - position.Y, 2)));
                if (distance > Config.Instance.CatchDistance)
                {
                    return false;
                }
                return true;
            }
        }

        private void InitializeSnapping()
        {
            GeometryEditor.SnapSettings.IsEnabled = true;
            GeometryEditor.SnapSettings.IsHapticFeedbackEnabled = true;
            GeometryEditor.SnapSettings.IsFeatureSnappingEnabled = true;
            GeometryEditor.SnapSettings.IsGeometryGuidesEnabled = true;
            GeometryEditor.SnapSettings.SyncSourceSettings();
            foreach (var ss in GeometryEditor.SnapSettings.SourceSettings)
            {
                if (ss.Source is FeatureLayer fl)
                {
                    var l = Layers.Find(fl);
                    if (l != null)
                    {
                        ss.IsEnabled = l.Interaction.CanCatch;
                    }
                }
            }
        }
        /// <summary>
        /// 鼠标是否在绘制的图形旁。
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        private bool IsMouseNearSketch(MapPoint location)
        {
            Geometry geometry = GeometryEditor.Geometry;
            if (geometry == null || geometry.IsEmpty)
            {
                return false;
            }
            if (geometry is Polygon p)
            {
                geometry = p.ToPolyline();
            }
            location = location.ToWebMercator();
            var tolerance = 3; //像素
            var buffer = GeometryEngine.BufferGeodetic(location,
                tolerance * MapView.UnitsPerPixel * tolerance,
                LinearUnits.Meters);
            if (GeometryEngine.Intersects(buffer, geometry))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 鼠标位置移动事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MapView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(MapView);
            return; //ArcGIS Maps SDK for .NET 200.4的GeometryEditor已内置Snap吸附功能，因此废除之前自己实现的捕捉功能
            var location = MapView.ScreenToLocation(position);
            if (location == null)
            {
                return;
            }
            if (!CanCatchNearestPoint()//没开
                || MapView.CurrentTask != BoardTask.Draw//没有正在绘制
                || IsMouseNearSketch(location))//距离正在绘制的图形太近
            {
                MapView.Overlay.SetNearestPointPoint(null);
                MapView.Overlay.SetNearestVertexPoint(null);
                nearestVertex = nearestPoint = null;
                return;
            }
            if (!isSearchingNearestPoint)
            {
                isSearchingNearestPoint = true;

                (nearestVertex, nearestPoint) = await GetNearestVertexAndPointAsync(position, Config.Instance.AutoCatchToNearestVertex);
                isSearchingNearestPoint = false;
                //因为查询需要一定时间，结束以后可能就不在绘制状态了，所以需要再次判断
                if (MapView.CurrentTask != BoardTask.Draw || !GeometryEditor.IsStarted || !GeometryEditor.IsVisible)
                {
                    return;
                }
                if (Config.Instance.ShowNearestPointSymbol || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    MapView.Overlay.SetNearestPointPoint(nearestPoint);
                }
                else
                {
                    MapView.Overlay.SetNearestPointPoint(null);
                }

                if (Config.Instance.ShowNearestPointSymbol || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    MapView.Overlay.SetNearestVertexPoint(nearestVertex);
                }
                else
                {
                    MapView.Overlay.SetNearestVertexPoint(null);
                }
            }
        }

        /// <summary>
        /// 鼠标右键单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var location = MapView.ScreenToLocation(e.GetPosition(MapView)).ToWgs84();
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
            if (MapView.CurrentTask != BoardTask.Draw
                || !GeometryEditor.IsStarted
                || !GeometryEditor.IsVisible
                || !CanCatchNearestPoint())
            {
                return;
            }
            if (nearestVertex != null)
            {
                item = new MenuItem() { Header = "捕捉最近的结点" + (Config.Instance.ShowNearestPointSymbol ? "（红色）" : "") };
                item.Click += (s, e2) => AddPointToSketchEditor(nearestVertex);
                menu.Items.Add(item);
            }
            if (nearestPoint != null)
            {
                item = new MenuItem() { Header = "捕捉最近的结点" + (Config.Instance.ShowNearestPointSymbol ? "（黄色）" : "") };
                item.Click += (s, e2) => AddPointToSketchEditor(nearestPoint);
                menu.Items.Add(item);
            }
        }

        /// <summary>
        /// 地图被触摸或单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapviewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (MapView.CurrentTask != BoardTask.Draw)
            {
                return;
            }
            if (MapView.CurrentTask != BoardTask.Draw || !GeometryEditor.IsStarted || !GeometryEditor.IsVisible)
            {
                return;
            }
            if (!CanCatchNearestPoint())
            {
                return;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (nearestPoint != null)
                {
                    AddPointToSketchEditor(nearestPoint);
                    e.Handled = true;
                }
                return;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ^ Config.Instance.AutoCatchToNearestVertex)//只有一个可以为true
            {
                if (nearestVertex != null)
                {
                    AddPointToSketchEditor(nearestVertex);
                    e.Handled = true;
                }
                return;
            }
        }

        /// <summary>
        /// 开始绘制
        /// </summary>
        /// <param name="mode"></param>
        private void PrepareToDraw(EditMode mode)
        {
            stopWhenGeometryChanged = false;
            Mode = mode;
            MapView.CurrentTask = BoardTask.Draw;
            EditorStatusChanged?.Invoke(this, new EditorStatusChangedEventArgs(true));
        }

        private void StartDraw(GeometryType type, GeometryEditorTool tool = null)
        {
            InitializeSnapping();
            GeometryEditor.Start(type);
            GeometryEditor.Tool = tool ?? new VertexTool();
        }
        /// <summary>
        /// 停止绘制
        /// </summary>
        private void Stop()
        {
            Mode = EditMode.None;
            GeometryEditor.Stop();
            tcs?.SetResult();
            tcs = null;
            EditorStatusChanged?.Invoke(this, new EditorStatusChangedEventArgs(false));
            MapView.CurrentTask = BoardTask.Ready;
            MapView.Overlay.SetNearestVertexPoint(null);
            MapView.Overlay.SetNearestPointPoint(null);
        }
        private Task WaitForStopAsync()
        {
            tcs = new TaskCompletionSource();
            return tcs.Task;
        }
    }
}