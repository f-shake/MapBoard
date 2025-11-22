//#define CHECK_TOTAL_POINTS_COUNT_AND_DISTANCE

using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using FzLib.DataAnalysis;
using FzLib;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static MapBoard.UI.GpxToolbox.GpxSymbolResources;
using Envelope = Esri.ArcGISRuntime.Geometry.Envelope;
using Esri.ArcGISRuntime.Symbology;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Drawing;
using MapBoard.IO.Formats.Gps;
using FzLib.Program;
using System.Collections.ObjectModel;
using ModernWpf.FzExtension.CommonDialog;
using System.Threading.Tasks;
using FzLib.WPF.Dialog;
using MapBoard.Util;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using FzLib.WPF.Controls;
using FzLib.WPF;
using MapBoard.IO;
using Microsoft.Win32;
using CommonDialog = ModernWpf.FzExtension.CommonDialog.CommonDialog;
using ModernWpf.Controls;
using System.Windows.Controls.Primitives;

namespace MapBoard.UI.GpxToolbox
{
    /// <summary>
    /// GPX工具箱
    /// </summary>
    public partial class GpxWindow : MainWindowBase
    {
        private TimeBasedChartHelper<GpxPoint, GpxPoint, GpxPoint> chartHelper;

        public GpxWindow()
        {
            InitializeComponent();
            arcMap.Tracks = Tracks;
            InitializeChart();
            ListViewHelper<TrackInfo> lvwHelper = new ListViewHelper<TrackInfo>(lvwFiles);
            lvwHelper.EnableDragAndDropItem();
            mapInfo.Initialize(arcMap);
            arcMap.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(arcMap.SelectedTrack))
                {
                    this.Notify(nameof(GpxTrack));
                }
            };
        }

        public async Task LoadGpxFilesAsync(IList<string> files)
        {
            await DoAsync(async args =>
            {
                bool result = await arcMap.LoadFilesAsync(files, i =>
                {
                    args.SetMessage($"{i} / {files.Count}");
                });
                if (!result)
                {
                    await CommonDialog.ShowErrorDialogAsync("部分GPX加载失败");
                }
            }, "正在加载轨迹");
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        protected override async Task InitializeAsync()
        {
            if (LoadFiles != null)
            {
                await LoadGpxFilesAsync(LoadFiles);
            }
            else if (File.Exists(FolderPaths.TrackHistoryPath))
            {
                string[] files = await File.ReadAllLinesAsync(FolderPaths.TrackHistoryPath);

                await LoadGpxFilesAsync(files);
            }
        }
        /// <summary>
        /// GPX加载完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GpxLoaded(object sender, GpxMapView.GpxLoadedEventArgs e)
        {
            if (e.Track.Count > 0)
            {
                lvwFiles.SelectedItem = e.Track[^1];
            }
        }

        /// <summary>
        /// 刷新轨迹的信息、表格和图表
        /// </summary>
        /// <returns></returns>
        private async Task UpdateUI()
        {
            try
            {
                var pointPoints = GpxTrack.Points.Clone();//.Clone() as GpxPointCollection;
                var linePoints = GpxTrack.Points.Clone();//.Clone() as GpxPointCollection;
                //下面两行没用到输出，因为会直接写入到Speed属性中
                await Task.Run(() =>
                {
                    GpxUtility.GetMeanFilteredSpeeds(pointPoints, 3, true);
                    GpxUtility.GetMeanFilteredSpeeds(linePoints, 19, true);
                });
                chartHelper.DrawActionAsync = () =>
                    DrawChartAsync(pointPoints, linePoints);

                chartHelper.BeginDraw();
            }
            catch (Exception ex)
            {
                App.Log.Error("绘制GPX图表失败", ex);
            }
        }
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            await File.WriteAllLinesAsync(FolderPaths.TrackHistoryPath, Tracks.Select(p => p.FilePath).ToArray());
            Tracks.Clear();
        }

        #region GPX和轨迹属性


        /// <summary>
        /// GPX轨迹对象
        /// </summary>
        public TrackInfo GpxTrack
        {
            get => arcMap?.SelectedTrack;
            set => arcMap.SelectedTrack = value;
        }

        /// <summary>
        /// 启动后需要加载的文件
        /// </summary>
        public IList<string> LoadFiles { get; set; } = null;

        /// <summary>
        /// 所有轨迹
        /// </summary>
        public ObservableCollection<TrackInfo> Tracks { get; } = new ObservableCollection<TrackInfo>();

        #endregion

        #region 轨迹操作

        /// <summary>
        /// 单击高程偏移按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ElevationOffsetMenu_Click(object sender, RoutedEventArgs e)
        {
            double? num = await CommonDialog.ShowDoubleInputDialogAsync("请选择整体移动高度，向上为正（m）");
            if (num.HasValue)
            {
                GpxTrack.Points.ForEach(p => p.Z += num.Value);
            }
        }

        /// <summary>
        /// 单击识别所有轨迹按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IdentifyAllButton_Click(object sender, RoutedEventArgs e)
        {
            arcMap.MapTapMode = GpxMapView.MapTapModes.AllLayers;
            Cursor = Cursor == Cursors.Help ? Cursors.Arrow : Cursors.Help;
        }

        /// <summary>
        /// 单击识别轨迹中的点按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IdentifyButton_Click(object sender, RoutedEventArgs e)
        {
            arcMap.MapTapMode = GpxMapView.MapTapModes.SelectedLayer;
            Cursor = Cursor == Cursors.Help ? Cursors.Arrow : Cursors.Help;
        }

        /// <summary>
        /// 单击打开文件按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.AddFilter("GPX轨迹文件", "gpx");
            string[] files = dialog.GetPaths(this);
            if (files != null)
            {
                await LoadGpxFilesAsync(files);
            }
        }

        /// <summary>
        /// 单击操作按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OperationButton_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuHeightSmooth = new MenuItem() { Header = "高度平滑" };
            menuHeightSmooth.Click += (p1, p2) => SmoothAsync(false, true);
            MenuItem menuSmooth = new MenuItem() { Header = "平滑" };
            menuSmooth.Click += (p1, p2) => SmoothAsync(true, true);
            MenuItem menuHeightOffset = new MenuItem() { Header = "高度整体偏移" };
            menuHeightOffset.Click += ElevationOffsetMenu_Click;
            MenuItem menuSpeed = new MenuItem() { Header = "计算速度" };
            menuSpeed.Click += SpeedButton_Click;

            //MenuItem menuDeletePoints = new MenuItem() { "删除一个区域的所有点" };
            //menuDeletePoints.Click += DeletePointsMenu_Click;

            ContextMenu menu = new ContextMenu()
            {
                PlacementTarget = sender as FrameworkElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
                Items = { menuSmooth, menuHeightSmooth, menuHeightOffset, menuSpeed },
                IsOpen = true,
            };
        }

        /// <summary>
        /// 单击恢复相机视角按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RecoverCameraButton_Click(object sender, RoutedEventArgs e)
        {
            MapPoint centerPoint = arcMap.ScreenToBaseSurface(new System.Windows.Point(arcMap.ActualWidth / 2, arcMap.ActualWidth / 2));
            Camera camera = new Camera(new MapPoint(centerPoint.X, centerPoint.Y, arcMap.Camera.Location.Z), 0, 0, 0);
            await arcMap.SetViewpointCameraAsync(camera);
        }

        /// <summary>
        /// 单击重置轨迹按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetTrackButton_Click(object sender, RoutedEventArgs e)
        {
            if (GpxTrack == null)
            {
                return;
            }

            IsEnabled = false;
            ReloadTrack();
            FlyoutService.GetFlyout(btnReset).Hide();
            IsEnabled = true;
        }

        private void ReloadTrack()
        {
            arcMap.LoadTrack(GpxTrack, true);
            UpdateUI();
        }

        /// <summary>
        /// 单击保存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog()
                .AddFilter("GPX轨迹文件", "gpx")
                .AddFilter("截图", "png");
            dialog.AddExtension = true;
            dialog.FileName = GpxTrack.Gpx.Name;
            string path = dialog.GetPath(this);
            if (path != null)
            {
                try
                {
                    if (path.EndsWith(".gpx"))
                    {
                        await File.WriteAllTextAsync(path, GpxTrack.Gpx.ToXmlString());
                    }
                    else if (path.EndsWith(".png"))
                    {
                        PanelExport export = new PanelExport(grd, 0, VisualTreeHelper.GetDpi(this).DpiScaleX, VisualTreeHelper.GetDpi(this).DpiScaleX);
                        var bitmap = export.GetBitmap().ToBitmap();

                        Graphics g = Graphics.FromImage(bitmap);
                        Bitmap image = await arcMap.GetImageAsync(GeoViewHelper.GetWatermarkThickness());

                        g.DrawImage(image, 0, 0, image.Width, image.Height);
                        g.Flush();
                        bitmap.Save(path);
                    }
                    else
                    {
                        throw new Exception("未知导出类型");
                    }
                    SnakeBar.Show("导出成功");
                }
                catch (Exception ex)
                {
                    App.Log.Error("导出失败", ex);
                    await CommonDialog.ShowErrorDialogAsync(ex, "导出失败");
                }
            }
        }

        /// <summary>
        /// 平滑轨迹
        /// </summary>
        /// <param name="xy"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        private async Task SmoothAsync(bool xy, bool z)
        {
            if (lvwFiles.SelectedItem == null)
            {
                SnakeBar.ShowError("请先选择一段轨迹！");
                return;
            }
            TrackInfo track = lvwFiles.SelectedItem as TrackInfo;
            var points = track.Track.GetPoints();
            int count = points.Count;
            int? result = await CommonDialog.ShowIntInputDialogAsync($"请输入平滑度（0~{count}）");
            if (result.HasValue)
            {
                int num = result.Value;
                if (num < 2 || num >= count)
                {
                    await CommonDialog.ShowErrorDialogAsync("输入的数值超出范围");
                    return;
                }
                if (z && points.All(p => p.Z.HasValue))
                {
                    GpxUtility.Smooth(points, num, p => p.Z.Value, (p, v) => p.Z = v);
                }
                if (xy)
                {
                    GpxUtility.Smooth(points, num, p => p.X, (p, v) => p.X = v);
                    GpxUtility.Smooth(points, num, p => p.Y, (p, v) => p.Y = v);
                }
                arcMap.LoadTrack(GpxTrack, true);
            }
        }

        /// <summary>
        /// 单击计算速度按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SpeedButton_Click(object sender, RoutedEventArgs e)
        {
            int? num = await CommonDialog.ShowIntInputDialogAsync("请选择单边采样率");
            if (num.HasValue)
            {
                foreach (var item in GpxTrack.Points)
                {
                    double speed = GpxTrack.Points.GetSpeed(item, num.Value);
                    item.Speed = speed;
                }
            }

            var source = grdPoints.ItemsSource;
            grdPoints.ItemsSource = null;
            grdPoints.ItemsSource = source;
        }

        /// <summary>
        /// 单击缩放到轨迹按钮
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private Task ZoomToTrackAsync(int time = 500)
        {
            if (time <= 0)
            {
                time = 0;
            }
            return arcMap.SetViewpointAsync(new Viewpoint(GpxTrack.Points.GetExtent()), TimeSpan.FromMilliseconds(time)); ;
        }
        #endregion 左下角按钮

        #region 文件操作
        /// <summary>
        /// 选择的GPX文件是否全部点都提供了高度和时间信息
        /// </summary>
        private bool hasZAndTimeInfo;

        /// <summary>
        /// 文件拖放，加载文件
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return;
            }

#if CHECK_TOTAL_POINTS_COUNT_AND_DISTANCE
            int points = 0;
            double total = 0;
            foreach (var file in files)
            {
                try
                {
                    var gpx = GpxSerializer.LoadFromString(File.ReadAllText(file), file);
                    foreach (var track in gpx.Tracks)
                    {
                        foreach (var seg in track.Segments)
                        {
                            points += seg.Points.Count;
                            total += seg.Points.GetDistance();
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            await CommonDialog.ShowOkDialogAsync("提示", $"共检测到{points / 10000.0:F1}万个点，约{total / 1000.0:F0}千米长度的轨迹文件。");
            return;

#endif

            List<string> fileList = new List<string>();
            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    fileList.Add(file);
                }
                else if (Directory.Exists(file))
                {
                    fileList.AddRange(Directory.EnumerateFiles(file, "*.gpx", SearchOption.AllDirectories));
                }
            }
            bool yes = true;
            if (fileList.Count > 10)
            {
                this.GetWindow().Activate();
                yes = await CommonDialog.ShowYesNoDialogAsync("导入文件较多，是否确定导入？", $"将导入{fileList.Count}个文件");
            }
            if (yes)
            {
                await LoadGpxFilesAsync(fileList);
            }
        }

        /// <summary>
        /// 单击清除轨迹按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearFileListButton_Click(object sender, RoutedEventArgs e)
        {
            Tracks.Clear();
        }
        /// <summary>
        /// 选择的的文件改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void File_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                GpxTrack?.UpdateTrackDisplay(TrackInfo.TrackSelectionDisplay.SimpleLine);
                if (lvwFiles.SelectedItem == null)
                {
                    GpxTrack = null;
                    this.Notify(nameof(GpxTrack));
                    chartHelper.Initialize();
                    return;
                }

                if (lvwFiles.SelectedItems.Count > 1)
                {
                    GpxTrack = null;
                    this.Notify(nameof(GpxTrack));
                    chartHelper.Initialize();
                }
                else
                {
                    GpxTrack = lvwFiles.SelectedItem as TrackInfo;

                    GpxTrack.UpdateTrackDisplay(TrackInfo.TrackSelectionDisplay.ColoredLine);


                    this.Notify(nameof(GpxTrack));

                    hasZAndTimeInfo = GpxTrack.Points.All(p => p.Z.HasValue && p.Time.HasValue);

                    if (hasZAndTimeInfo)
                    {
                        await Task.WhenAll(ZoomToTrackAsync(), UpdateUI());
                    }
                    else
                    {
                        await ZoomToTrackAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log.Error("选择Gpx文件时出现错误", ex);
            }
        }

        /// <summary>
        /// 右键文件菜单项
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu menu = new ContextMenu
            {
                PlacementTarget = sender as UIElement
            };

            if (lvwFiles.SelectedItems.Count > 0)
            {
                var menuRemove = new MenuItem() { Header = "移除" };
                menuRemove.Click += (s, e) => RemoveSelectedGpxs();
                menu.Items.Add(menuRemove);

                if (lvwFiles.SelectedItems.Count > 1)
                {
                    var menuLink = new MenuItem() { Header = "连接" };
                    menuLink.Click += LinkTrackMenu_Click;
                    menu.Items.Add(menuLink);
                }
                if (GpxTrack != null)
                {
                    var menuZoom = new MenuItem() { Header = "缩放到轨迹" };
                    menuZoom.Click += async (s, e) => await ZoomToTrackAsync();
                    menu.Items.Add(menuZoom);

                    var menuBrowse = new MenuItem() { Header = "游览" };
                    menuBrowse.Click += (s, e) => new GpxBrowseWindow(lvwFiles.SelectedItem as TrackInfo).Show();
                    menu.Items.Add(menuBrowse);
                }
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// 单击连接轨迹菜单项
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LinkTrackMenu_Click(object sender, RoutedEventArgs e)
        {
            TrackInfo[] tracks = lvwFiles.SelectedItems.Cast<TrackInfo>().OrderBy(p => p.Track.GetPoints().FirstOrDefault()?.Time ?? DateTime.MaxValue).ToArray();
            if (tracks.Length <= 1)
            {
                SnakeBar.ShowError("至少要2个轨迹才能进行连接操作");
                return;
            }

            GpxDocument gpx = tracks[0].Gpx.Clone() as GpxDocument;
            gpx.Tracks.Clear();
            var track = gpx.CreateTrack();
            var seg = track.CreateSegment();
            foreach (var t in tracks)
            {
                (seg.Points as List<GpxPoint>).AddRange(t.Track.GetPoints());
            }
            var dialog = new SaveFileDialog();
            dialog.AddFilter("GPX轨迹文件", "gpx");
            dialog.FileName = tracks[0].FileName + " - 连接.gpx";
            string filePath = dialog.GetPath(this);

            if (filePath != null)
            {
                gpx.Save(filePath);
                await arcMap.LoadGpxAsync(filePath, true);
            }
        }

        /// <summary>
        /// 选择文件后单击Delete按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewItemPreviewDeleteKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null || e.Key == Key.Delete)
            {
                RemoveSelectedGpxs();
            }
        }

        /// <summary>
        /// 移除选中的GPX
        /// </summary>
        private void RemoveSelectedGpxs()
        {
            lvwFiles.SelectionChanged -= File_SelectionChanged;
            foreach (var item in lvwFiles.SelectedItems.Cast<TrackInfo>().ToList())
            {
                //arcMap.GraphicsOverlays.Remove(item.Overlay);
                Tracks.Remove(item);
            }
            lvwFiles.SelectionChanged += File_SelectionChanged;
            File_SelectionChanged(null, null);
        }
        #endregion 文件操作

        #region 轨迹点

        /// <summary>
        /// 单击地图，可以将选中的点移动到鼠标位置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ArcMapTapped(object sender, Esri.ArcGISRuntime.UI.Controls.GeoViewInputEventArgs e)
        {
            if (arcMap.MapTapMode == GpxMapView.MapTapModes.None && lvwFiles.SelectedItem != null
                && grdPoints.SelectedItems.Count == 1)
            {
                GpxPoint point = grdPoints.SelectedItem as GpxPoint;
                var overlay = arcMap.TapOverlay;
                if (overlay == null || !arcMap.GraphicsOverlays.Contains(overlay))
                {
                    overlay = arcMap.TapOverlay = new GraphicsOverlay();
                    arcMap.GraphicsOverlays.Add(overlay);
                    overlay.Renderer = new SimpleRenderer(new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.X, System.Drawing.Color.Red, 12));
                }
                while (overlay.Graphics.Count > 0)
                {
                    overlay.Graphics.RemoveAt(overlay.Graphics.Count - 1);
                }
                MapPoint mapPoint = e.Location;
                overlay.Graphics.Add(new Graphic(mapPoint));

                ContextMenu menu = new ContextMenu()
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    HorizontalOffset = 20,
                    VerticalOffset = 20,
                };
                MenuItem menuSetToHere = new MenuItem() { Header = "移动点到此" };
                menuSetToHere.Click += (p1, p2) =>
                {
                    mapPoint = new MapPoint(mapPoint.X, mapPoint.Y, point.Z.Value, mapPoint.SpatialReference);
                    mapPoint = mapPoint.ToWgs84();
                    // mapPoint = new MapPoint(mapPoint.X, mapPoint.Y, oldZ, mapPoint.SpatialReference);
                    point.X = mapPoint.X;
                    point.Y = mapPoint.Y;
                    //point.Z = mapPoint.Z;
                    while (overlay.Graphics.Count > 0)
                    {
                        overlay.Graphics.RemoveAt(overlay.Graphics.Count - 1);
                    }
                    ReloadTrack();
                };
                menu.Items.Add(menuSetToHere);
                menu.IsOpen = true;
                menu.Closed += (p1, p2) =>
                {
                    while (overlay.Graphics.Count > 0)
                    {
                        overlay.Graphics.RemoveAt(overlay.Graphics.Count - 1);
                    }
                };
            }
        }

        /// <summary>
        /// 单击删除点菜单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeletePointMenu_Click(object sender, RoutedEventArgs e)
        {
            var deletingPoints = grdPoints.SelectedItems.Cast<GpxPoint>().ToHashSet();

            List<GpxPoint> newPoints = new List<GpxPoint>();

            foreach (var point in GpxTrack.Points)
            {
                if (!deletingPoints.Contains(point))
                {
                    newPoints.Add(point);
                }
            }
            GpxTrack.UpdatePoints(new ObservableCollection<GpxPoint>(newPoints));
            ReloadTrack();
        }

        /// <summary>
        /// 单击插入点菜单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InsertPointButton_Click(object sender, RoutedEventArgs e)
        {
            var points = grdPoints.SelectedItems.Cast<GpxPoint>().ToArray();
            if (points.Length == 0)
            {
                SnakeBar.ShowError("请先选择一个或多个点");
                return;
            }
            if (points.Length > 1)
            {
                SnakeBar.ShowError("请只选择一个点");
                return;
            }
            int index = grdPoints.SelectedIndex;
            if ((sender as FrameworkElement).Tag.Equals("After"))
            {
                index++;
            }

            GpxPoint point = points[0].Clone() as GpxPoint;
            GpxTrack.Points.Insert(index, point);
            //arcMap.gpxPointAndGraphics.Add(point,)
            //arcMap.pointToTrackInfo.Add(point, GpxTrack);
            //arcMap.pointToTrajectoryInfo.Add(point, GpxTrack);
            grdPoints.SelectedItem = point;
        }

        /// <summary>
        /// 选择了一个地图点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapPointSelected(object sender, GpxMapView.PointSelectedEventArgs e)
        {
            if (arcMap.MapTapMode == GpxMapView.MapTapModes.SelectedLayer)
            {
                if (e.Point == null)
                {
                    return;
                }
                grdPoints.SelectedItem = e.Point;
                grdPoints.ScrollIntoView(e.Point);
            }
            else
            {
                lvwFiles.SelectedItem = e.Track;
                SnakeBar.Show(this, "已跳转到轨迹：" + e.Track.FileName);
            }
            arcMap.MapTapMode = GpxMapView.MapTapModes.None;
            Cursor = Cursors.Arrow;
        }


        /// <summary>
        /// 某个点被选中
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PointsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (grdPoints.SelectedItem is GpxPoint point && !double.IsNaN(point.Z.Value) && point.Y != 0)
            {
                arcMap.SelectPoint(point);
                chartHelper.SetLine(point.Time.Value);
            }
            else
            {
                arcMap.UnselectAllPoints();
                chartHelper.ClearLine();
            }
        }

        /// <summary>
        /// 单击速度菜单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SpeedMenuItemClick(object sender, RoutedEventArgs e)
        {
            int? num = await CommonDialog.ShowIntInputDialogAsync("请选择单边采样率");
            if (num.HasValue)
            {
                double speed = GpxTrack.Points.GetSpeed(grdPoints.SelectedItem as GpxPoint, num.Value);
                await CommonDialog.ShowOkDialogAsync("速度为：" + speed.ToString("0.00") + "m/s，" + (3.6 * speed).ToString("0.00") + "km/h");
            }
        }


        #endregion 点菜单

        #region 图表

        /// <summary>
        /// 绘制图表
        /// </summary>
        /// <param name="points"></param>
        /// <param name="lines"></param>
        /// <returns></returns>
        private async Task DrawChartAsync(IEnumerable<GpxPoint> points, IEnumerable<GpxPoint> lines)
        {
            try
            {
                //1、初始化
                chartHelper.Initialize();

                //2、绘制坐标轴和网格
                await Task.WhenAll(
                 chartHelper.DrawAxisAsync(lines, true,
                       new TimeBasedChartHelper<GpxPoint, GpxPoint, GpxPoint>.CoordinateSystemSetting<GpxPoint>()
                       {
                           XAxisValueConverter = p => p.Time.Value,
                           YAxisValueConverter = p => p.Speed,
                       }),

                 chartHelper.DrawAxisAsync(GpxTrack.Points.Where(p => !double.IsNaN(p.Z.Value)),
                    false, new TimeBasedChartHelper<GpxPoint, GpxPoint, GpxPoint>.CoordinateSystemSetting<GpxPoint>()
                    {
                        XAxisValueConverter = p => p.Time.Value,
                        YAxisValueConverter = p => p.Z.Value,
                    }),
                 chartHelper.DrawAxisAsync(points, false,
                        new TimeBasedChartHelper<GpxPoint, GpxPoint, GpxPoint>.CoordinateSystemSetting<GpxPoint>()
                        {
                            XAxisValueConverter = p => p.Time.Value,
                            YAxisValueConverter = p => p.Speed,
                        }));

                //3、绘制数据
                await Task.WhenAll(
                 chartHelper.DrawPolygonAsync(GpxTrack.Points, 1),
          chartHelper.DrawPointsAsync(points, 0, Config.Instance.Gpx_DrawPoints),
                 chartHelper.DrawLinesAsync(lines, 0));

                //4、重新排版
                await chartHelper.StretchToFitAsync();
            }
            catch (Exception ex)
            {
                App.Log.Error("绘制图形失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 初始化图表，设置<see cref="TimeBasedChartHelper{TPoint, TLine, TPolygon}"/>的各项属性
        /// </summary>
        private void InitializeChart()
        {
            chartHelper = new TimeBasedChartHelper<GpxPoint, GpxPoint, GpxPoint>(speedChart);

            chartHelper.XAxisLineValueConverter = p => p.Time.Value;
            chartHelper.XAxisPointValueConverter = p => p.Time.Value;
            chartHelper.XAxisPolygonValueConverter = p => p.Time.Value;

            chartHelper.YAxisPointValueConverter = p => p.Speed;
            chartHelper.YAxisLineValueConverter = p => p.Speed;
            chartHelper.YAxisPolygonValueConverter = p => p.Z.Value;
            chartHelper.XLabelFormat = p => p.ToString("HH:mm");
            chartHelper.YLabelFormat = p => $"{p}m/s {(p * 3.6).ToString(".0")}km/h";
            chartHelper.ToolTipConverter = p =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(p.Time.Value.ToString("HH:mm:ss"));
                sb.Append(p.Speed.ToString("0.00")).AppendLine("m/s");
                sb.Append((3.6 * p.Speed).ToString("0.00")).AppendLine("km/h");
                if (p.Z.HasValue)
                {
                    sb.Append(p.Z.Value.ToString("0.00")).Append('m');
                }
                return sb.ToString();
            };

            chartHelper.MouseOverPoint += (p1, p2) =>
            {
                arcMap.SelectPoint(p2.Item);
            };
            chartHelper.LinePointEnable = (p1, p2) => (p2.Time.Value - p1.Time.Value) < TimeSpan.FromSeconds(200);
        }

        /// <summary>
        /// 鼠标移开速度图表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeedChartMouseLeave(object sender, MouseEventArgs e)
        {
            arcMap.UnselectAllPoints();
        }
        #endregion

    }
}