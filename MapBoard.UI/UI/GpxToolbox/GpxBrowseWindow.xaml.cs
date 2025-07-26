using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using MapBoard.IO.Formats.Gps;
using FzLib.WPF.Dialog;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static MapBoard.UI.GpxToolbox.GpxSymbolResources;
using FzLib;
using MapBoard.Model;
using MapBoard.Mapping;
using MapBoard.Util;
using MapBoard.Mapping.Model;
using System.Drawing.Imaging;
using MapBoard.IO;

namespace MapBoard.UI.GpxToolbox
{
    /// <summary>
    /// GPX游览窗口
    /// </summary>
    public partial class GpxBrowseWindow : WindowBase
    {
        /// <summary>
        /// 是否正在停止
        /// </summary>
        private bool stopping = false;

        /// <summary>
        /// 是否正在播放或录制
        /// </summary>
        private bool working = false;

        public GpxBrowseWindow(TrackInfo track)
        {
            BrowseInfo = Config.Instance.Gpx_BrowseInfo;
            Track = track.Clone();
            InitializeComponent();
            Track.Initialize();
            arcMap.InteractionOptions = new SceneViewInteractionOptions() { IsEnabled = false };
        }

        /// <summary>
        /// 游览信息
        /// </summary>
        public BrowseInfo BrowseInfo { get; set; } = new BrowseInfo();

        /// <summary>
        /// 当前进度
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// 轨迹
        /// </summary>
        public TrackInfo Track { get; set; }

        /// <summary>
        /// 录制
        /// </summary>
        /// <returns></returns>
        public async Task Record()
        {
            working = true;
            if (!Directory.Exists(GetRecordPath()))
            {
                Directory.CreateDirectory(GetRecordPath());
            }
            var points = Track.Track.GetPoints();
            if (points.Any(p => !p.Time.HasValue))
            {
                throw new InvalidOperationException("存在没有时间信息的点");
            }
            DateTime startTime = points.First().Time.Value;
            int i = 0;
            int count = 0;
            bool useTimeFileName = rbtnFormatTime.IsChecked.Value;
            for (DateTime time = startTime; ; time = time.AddMilliseconds(BrowseInfo.RecordInterval))
            {
                while (i < points.Count - 2 && points[i + 1].Time < time)
                {
                    i++;
                }
                if (i >= points.Count - 2 || stopping)
                {
                    await StopRecordAsync();
                    return;
                }
                MapPoint interPoint = null;
                Camera camera = null;
                //计算点位置
                await Task.Run(() =>
                {
                    GpxPoint point = points[i];
                    GpxPoint nextPoint = points[i + 1];
                    Progress = 1.0 * (point.Time - points[0].Time).Value.Ticks / (points[^1].Time - points[0].Time).Value.Ticks;
                    var curve = GeometryUtility.CalculateGeodeticCurve(point.ToMapPoint(), nextPoint.ToMapPoint());
                    double percent = 1.0 * (time - point.Time).Value.Ticks / (nextPoint.Time - point.Time).Value.Ticks;
                    interPoint = GeometryUtility.CalculateEndingGlobalCoordinates(point.ToMapPoint(), curve.Azimuth, curve.Distance * percent);
                    var cameraPoint = GeometryUtility.CalculateEndingGlobalCoordinates(point.ToMapPoint(), curve.ReverseAzimuth, Math.Tan(BrowseInfo.Angle * Math.PI / 180) * BrowseInfo.Zoom);
                    camera = new Camera(cameraPoint.Y, cameraPoint.X, BrowseInfo.Zoom, curve.Azimuth.Degrees, BrowseInfo.Angle, 0);
                });
                //设置点的位置
                arcMap.SetLocation(interPoint);
                //设置摄像机位置
                await arcMap.SetViewpointCameraAsync(camera, TimeSpan.Zero);
                //等待渲染完成
                await arcMap.WaitForRenderCompletedAsync();
                //即使已经渲染完成，有时仍然不会立刻显示，需要再等待一段时间
                await Task.Delay(BrowseInfo.ExtraRecordDelay);
                //导出图像
                string filePath = null;
                if (useTimeFileName)
                {
                    filePath = Path.Combine(FolderPaths.RecordsPath, Track.FileName, time.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                }
                else
                {
                    filePath = Path.Combine(FolderPaths.RecordsPath, Track.FileName, ++count + ".png");
                }
                await arcMap.ExportImageAsync(filePath, ImageFormat.Png, GeoViewHelper.GetWatermarkThickness());
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void StartPlay()
        {
            working = true;
            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1.0 / BrowseInfo.FPS),
            };
            var points = Track.Track.GetPoints();

            int i = 0;
            DateTime startTime = DateTime.Now;
            if (points.Any(p => !p.Time.HasValue))
            {
                throw new InvalidOperationException("存在没有时间信息的点");
            }
            DateTime startGpxTime = points[0].Time.Value;
            int viewIndex = 0;
            timer.Tick += async (p1, p2) =>
             {
                 DateTime now = DateTime.Now;
                 while (i < points.Count - 2 && (points[i + 1].Time - startGpxTime).Value.Ticks < BrowseInfo.Speed * (now - startTime).Ticks)
                 {
                     i++;
                 }
                 if (i >= points.Count - 2 || stopping)
                 {
                     StopPlay();
                     timer.Stop();
                     return;
                 }
                 MapPoint interPoint = null;
                 Camera camera = null;
                 await Task.Run(() =>
                 {
                     GpxPoint point = points[i];
                     GpxPoint nextPoint = points[i + 1];
                     Progress = 1.0 * (point.Time - points[0].Time).Value.Ticks / (points[^1].Time - points[0].Time).Value.Ticks;
                     var curve = GeometryUtility.CalculateGeodeticCurve(point.ToMapPoint(), nextPoint.ToMapPoint());
                     double percent =
                     (BrowseInfo.Speed * (now - startTime).Ticks - (point.Time - startGpxTime).Value.Ticks)
                     / (nextPoint.Time - point.Time).Value.Ticks;
                     interPoint = GeometryUtility.CalculateEndingGlobalCoordinates(point.ToMapPoint(), curve.Azimuth, curve.Distance * percent);
                     var cameraPoint = GeometryUtility.CalculateEndingGlobalCoordinates(point.ToMapPoint(), curve.ReverseAzimuth, Math.Tan(BrowseInfo.Angle * Math.PI / 180) * BrowseInfo.Zoom);
                     camera = new Camera(cameraPoint.Y, cameraPoint.X, BrowseInfo.Zoom, curve.Azimuth.Degrees, BrowseInfo.Angle, 0);
                 });
                 arcMap.SetLocation(interPoint);
                 if (viewIndex++ % BrowseInfo.Sensitivity == 0)
                 {
                     await arcMap.SetViewpointCameraAsync(camera, TimeSpan.FromSeconds(2.0 / BrowseInfo.FPS * BrowseInfo.Sensitivity));
                 }
             };
            timer.Start();
        }

        /// <summary>
        /// 获取录制的目标路径
        /// </summary>
        /// <returns></returns>
        private string GetRecordPath()
        {
            return Path.Combine(FolderPaths.RecordsPath, Track.FileName);
        }

        /// <summary>
        /// GPX加载完成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GpxLoaded(object sender, GpxMapView.GpxLoadedEventArgs e)
        {
            //ZoomToTrackButton_Click(null, null);
            SetToFirstPoint();

            BrowseInfo.PropertyChanged += (p1, p2) =>
            {
                if (p2.PropertyName == nameof(BrowseInfo.Zoom)
                || p2.PropertyName == nameof(BrowseInfo.Angle))
                {
                    SetToFirstPoint();
                }
            };
        }

        /// <summary>
        /// 单击播放按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (working)
            {
                stopping = true;
                btnPlay.Content = "播放";
                SetUIEnabled(false, false);
            }
            else
            {
                StartPlay();
                btnPlay.Content = "停止";
                SetUIEnabled(true, false);
            }
        }
        /// <summary>
        /// 单击录制按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (working)
            {
                stopping = true;
                btnRecord.Content = "录制";
                SetUIEnabled(false, true);
            }
            else
            {
                Record();
                btnRecord.Content = "停止";
                SetUIEnabled(true, true);
                ResizeMode = ResizeMode.NoResize;
            }
        }

        /// <summary>
        /// 单击恢复摄像机按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RecoverCameraButton_Click(object sender, RoutedEventArgs e)
        {
            Camera camera = new Camera(arcMap.Camera.Location, 0, 0, 0);
            await arcMap.SetViewpointCameraAsync(camera);
        }

        /// <summary>
        /// 转到第一个点
        /// </summary>
        private void SetToFirstPoint()
        {
            var points = Track.Track.GetPoints();
            var curve = GeometryUtility.CalculateGeodeticCurve(points[0].ToMapPoint(), points[1].ToMapPoint());
            var cameraPoint = GeometryUtility.CalculateEndingGlobalCoordinates(points[0].ToMapPoint(), curve.ReverseAzimuth, Math.Tan(BrowseInfo.Angle * Math.PI / 180) * BrowseInfo.Zoom);
            var camera = new Camera(cameraPoint.Y, cameraPoint.X, BrowseInfo.Zoom, curve.Azimuth.Degrees, BrowseInfo.Angle, 0);
            arcMap.SetLocation(points[0].ToMapPoint());
            arcMap.SetViewpointCameraAsync(camera, TimeSpan.Zero);
        }

        /// <summary>
        /// 设置UI的可用性
        /// </summary>
        /// <param name="working"></param>
        /// <param name="record"></param>
        private void SetUIEnabled(bool working, bool record)
        {
            grdPlay.IsEnabled = !stopping && (!working || !record);
            grdRecord.IsEnabled = !stopping && (!working || record);
            sldSpeed.IsEnabled = !working;
            grdCommon.IsEnabled = !working && !stopping;
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        private void StopPlay()
        {
            stopping = false;
            working = false;
            SetUIEnabled(false, false);
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordAsync()
        {
            btnRecord.Content = "录制";
            stopping = false;
            working = false;
            SetUIEnabled(false, false);
            ResizeMode = ResizeMode.CanResize;
            if (await CommonDialog.ShowYesNoDialogAsync("是否打开目录？") == true)
            {
                await IOUtility.TryOpenFolderAsync(GetRecordPath());
            }
        }

        /// <summary>
        /// 窗口关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Config.Instance.Save();
        }

        /// <summary>
        /// 窗口加载后，加载轨迹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            arcMap.LoadTrack(Track, false, true, false);
            Track.UpdateTrackDisplay(TrackInfo.TrackSelectionDisplay.ColoredLine);
        }

    }
}