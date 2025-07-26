#if ANDROID
using Android.Content;
using Android.App;
using MapBoard.Platforms.Android;
using Application = Android.App.Application;
#endif
using MapBoard.Services;
using MapBoard.ViewModels;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls.PlatformConfiguration;
using MapBoard.IO;
using MapBoard.Mapping;
using MapBoard.IO.Formats.Gps;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Models;
using MapBoard.Util;
using System.Reflection.Metadata;

namespace MapBoard.Views;

public partial class TrackView : ContentView, ISidePanel
{
    private static readonly string DeletedGpxDir = Path.Combine(FolderPaths.TrackPath, "deleted");
    public TrackView()
    {
        InitializeComponent();
        BindingContext = new TrackViewViewModel();

        //轨迹记录有异常，在此处处理
        TrackService.ExceptionThrown += TrackService_ExceptionThrown;

        //处理TrackService.Current发生改变，即轨迹记录的开始和停止事件
        TrackService.CurrentChanged += TrackService_StaticPropertyChanged;

        //轨迹记录结束、GPX保存后，更新GPX文件列表
        TrackService.GpxSaved += TrackService_GpxSaved;
    }

    public SwipeDirection Direction => SwipeDirection.Left;

    public int Length => 300;

    public bool Standalone => false;

    public async void OnPanelOpened()
    {
        if ((BindingContext as TrackViewViewModel).GpxFiles.Count == 0)
        {
            await (BindingContext as TrackViewViewModel).LoadGpxFilesAsync();
        }
        UpdateButtonsVisible();
    }


    private void ContentPage_Loaded(object sender, EventArgs e)
    {
        UpdateButtonsVisible();
    }

    private void ContentPage_Unloaded(object sender, EventArgs e)
    {
    }

    private async void GpxList_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        PopupMenu.PopupMenuItem[] items = ["加载", "分享", "删除", "删除本条及更早的轨迹"];
        var result = await (sender as ListView).PopupMenuAsync(e, items, "轨迹");
        if (result >= 0)
        {
            var file = e.Item as GpxAndFileInfo;
            switch (result)
            {
                case 0:
                    await LoadGpxAsync(file.File.FullName);
                    return;
                case 1:
                    await Share.Default.RequestAsync(new ShareFileRequest("分享轨迹", new ShareFile(file.File.FullName)));
                    break;
                case 2:
                    if (await MainPage.Current.DisplayAlert("删除轨迹", $"是否要删除{file.File.Name}？", "是", "否"))
                    {
                        if (!Directory.Exists(DeletedGpxDir))
                        {
                            Directory.CreateDirectory(DeletedGpxDir);
                        }
                        File.Move(file.File.FullName, Path.Combine(DeletedGpxDir, Path.GetFileName(file.File.FullName)), true);
                    }
                    break;
                case 3:
                    var gpxs = (BindingContext as TrackViewViewModel).GpxFiles.Where(p => p.File.Time <= file.File.Time).ToList(); ;
                    if (await MainPage.Current.DisplayAlert("删除轨迹", $"是否要删除{gpxs.Count}个轨迹？", "是", "否"))
                    {
                        if (!Directory.Exists(DeletedGpxDir))
                        {
                            Directory.CreateDirectory(DeletedGpxDir);
                        }
                        foreach (var gpx in gpxs)
                        {
                            File.Move(gpx.File.FullName, Path.Combine(DeletedGpxDir, Path.GetFileName(gpx.File.FullName)), true);
                        }
                    }
                    break;
            }

            var handle = ProgressPopup.Show("正在加载");
            try
            {
                await (BindingContext as TrackViewViewModel).LoadGpxFilesAsync();
            }
            finally
            {
                handle.Close();
            }

        }
    }

    private async Task ConvertToLayerAsync(string path)
    {
        var handle = ProgressPopup.Show("正在转为图层");
        try
        {
            MainPage.Current.ClosePanel<TrackView>();

            var layer = await LayerUtility.CreateLayerAsync(GeometryType.Polyline, MainMapView.Current.Layers, Path.GetFileNameWithoutExtension(path));
            layer.Renderer.DefaultSymbol = new Model.SymbolInfo()
            {
                OutlineWidth = 6,
                LineColor = System.Drawing.Color.FromArgb(0x54, 0xA5, 0xF6)
            };
            layer.ApplyStyle();

            GpxDocument gpx = await GpxSerializer.FromFileAsync(path);
            var points = gpx.GetPoints();
            var line = new Polyline(points.Select(p => p.ToXYMapPoint()));
            var feature = layer.CreateFeature(null, line);
            await layer.AddFeatureAsync(feature, Mapping.Model.FeaturesChangedSource.Initialize);
            var extent = await layer.QueryExtentAsync(new Esri.ArcGISRuntime.Data.QueryParameters());
            await MainMapView.Current.ZoomToGeometryAsync(extent);
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("转换失败", ex.Message, "确定");
        }
        finally
        {
            handle.Close();
        }
    }

    private async Task LoadOverlayGpxAsync(string path)
    {
        var handle = ProgressPopup.Show("正在加载轨迹");
        try
        {
            GpxDocument gpx = await GpxSerializer.FromFileAsync(path);

            var overlay = MainMapView.Current.TrackOverlay;
            var extent = await overlay.LoadColoredGpxAsync(gpx);
            MainPage.Current.ClosePanel<TrackView>();
            await MainMapView.Current.ZoomToGeometryAsync(extent);
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            handle.Close();
        }
    }

    private async void ResumeButton_Clicked(object sender, EventArgs e)
    {
        var gpxs = (BindingContext as TrackViewViewModel).GpxFiles;
        if (!gpxs.Any())
        {
            await MainPage.Current.DisplayAlert("无法继续", "不存在现有的轨迹", "确定");
            return;
        }

        var handle = ProgressPopup.Show("正在加载轨迹");
        StartTrack(gpxs[0].File.FullName, handle);
    }

    private void StartTrack(string resume = null, ProgressPopup popup = null)
    {
        if (resume != null)
        {
            TrackService.ResumeGpx = resume;
            TrackService.BeforeLoop = () => popup.Close();
        }
#if ANDROID
        (Platform.CurrentActivity as MainActivity).StartTrackService();
#else
        throw new NotImplementedException();
#endif
        MainPage.Current.ClosePanel<TrackView>();
    }

    private async void StartTrackButton_Click(object sender, EventArgs e)
    {
        try
        {
            Config.Instance.IsTracking = true;
            StartTrack();
            //UpdateButtonsVisible(true);
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("开始记录轨迹失败", ex.Message, "确定");
        }
    }

    private void StopButton_Clicked(object sender, EventArgs e)
    {
        StopTrack();
        //UpdateButtonsVisible(false);
    }

    private void StopTrack()
    {
#if ANDROID
        (Platform.CurrentActivity as MainActivity).StopTrackService();
#else
        throw new NotImplementedException();
#endif
    }

    private async void LoadOtherGpx_Tapped(object sender, TappedEventArgs e)
    {
        PickOptions options = new PickOptions()
        {
            PickerTitle = "选取GPX轨迹文件",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>()
            {
                [DevicePlatform.Android] = ["application/gpx", "application/gpx+xml", "application/octet-stream"],
                [DevicePlatform.WinUI] = ["*.gpx"]
            })
        };
        var file = (await FilePicker.Default.PickAsync(options))?.FullPath;
        if (file != null)
        {
            await LoadGpxAsync(file);
        }
    }

    private async Task LoadGpxAsync(string file)
    {
        string[] actions = ["作为临时轨迹打开", "转换为图层"];
        var result = await MainPage.Current.DisplayActionSheet("打开轨迹", "取消", null, actions);
        int index = Array.IndexOf(actions, result);
        switch (index)
        {
            case 0:
                await LoadOverlayGpxAsync(file);
                break;
            case 1:
                await ConvertToLayerAsync(file);
                break;
        }
    }

    private void ClearLoadedTracks_Tapped(object sender, TappedEventArgs e)
    {
        MainMapView.Current.TrackOverlay.Clear();
        MainPage.Current.ClosePanel<TrackView>();
    }

    private void TrackService_ExceptionThrown(object sender, ThreadExceptionEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (MainPage.Current.IsVisible)
            {
                MainPage.Current.DisplayAlert("轨迹记录失败", e.Exception.Message, "确定");
            }
        });
    }

    private async void TrackService_GpxSaved(object sender, TrackService.GpxSavedEventArgs e)
    {
        var gpxs = (BindingContext as TrackViewViewModel)?.GpxFiles;
        if (gpxs != null)
        {
            if (gpxs.Count > 0 && gpxs[0].File.FullName == e.FilePath)
            {
                gpxs[0] = new GpxAndFileInfo(e.FilePath);
            }
            else
            {
                gpxs.Insert(0, new GpxAndFileInfo(e.FilePath));
            }

            try
            {
                await gpxs[0].LoadGpxAsync();
            }
            catch (Exception ex)
            {
                // 异常处理逻辑
            }
        }
        if (TrackService.Current == null)
        {
            await LoadOverlayGpxAsync(e.FilePath);
        }
    }

    private void TrackService_StaticPropertyChanged(object sender, EventArgs e)
    {
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            UpdateButtonsVisible();
        });
        if (TrackService.Current == null)
        {
            TrackService.BeforeLoop = null;
        }
    }

    private void UpdateButtonsVisible()
    {
        bool running = TrackService.Current != null;
        btnStart.IsVisible = !running;
        btnStop.IsVisible = running;
        btnResume.IsVisible = !running;
        btnResume.IsEnabled = (BindingContext as TrackViewViewModel).GpxFiles.Count > 0;
        grdDetail.IsVisible = running;
        lvwGpxList.IsVisible = !running;
        lblLoadGpx.IsVisible = !running;
        lblClearGpx.IsVisible = !running;
    }
}
