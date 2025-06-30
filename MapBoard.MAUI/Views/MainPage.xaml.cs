using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Rasters;
using Esri.ArcGISRuntime.Maui;
using MapBoard.Mapping;
using MapBoard.Model;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using FzLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Map = Esri.ArcGISRuntime.Mapping.Map;
using Esri.ArcGISRuntime.UI;
using static MapBoard.Util.GeometryUtility;
using FubarDev.FtpServer.FileSystem.DotNet;
using FubarDev.FtpServer;
using MapBoard.Services;
using MapBoard.Views;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Microsoft.Maui.Controls.Shapes;
using MapBoard.Mapping.Model;
using System.Xml.Linq;
using MapBoard.IO;
using CommunityToolkit.Maui.Alerts;
using static MapBoard.Views.PopupMenu;
using CommunityToolkit.Maui.Views;
using MapBoard.Models;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Animations;


#if ANDROID
using MapBoard.Platforms.Android;
#endif

namespace MapBoard.Views
{
    public partial class MainPage : ContentPage
    {
        public const int AnimationDurationMs = 250;
        private Exception lastGeoShareException = null;
        private SidePanelInfo[] sidePanels;

        private Dictionary<Type, SidePanelInfo> type2SidePanels;
        public MainPage()
        {
            if (Current != null)
            {
                throw new Exception("仅允许一个实例");
            }
            Current = this;
            InitializeComponent();
            InitializeSidePanels();

            TrackService.CurrentChanged += TrackService_CurrentChanged;

            TileCacheDbContext.InitializeAsync().Wait();

            //大屏设备，底部操作栏在右下角悬浮
            if (DeviceInfo.Idiom != DeviceIdiom.Phone)
            {
                grdMain.RowDefinitions.RemoveAt(grdMain.RowDefinitions.Count - 1);
                Microsoft.Maui.Controls.Grid.SetRow(bdBottom, Microsoft.Maui.Controls.Grid.GetRow(bdBottom) - 1);
                bdBottom.Margin = new Thickness(16, 16);
                bdBottom.HorizontalOptions = LayoutOptions.End;
                bdBottom.VerticalOptions = LayoutOptions.End;
                bdBottom.WidthRequest = (bdBottom.Content as Microsoft.Maui.Controls.Grid).Children.Count * 60;
                bdBottom.StrokeShape = new RoundRectangle() { CornerRadius = new CornerRadius(8), Shadow = null };
#if DEBUG
                //bdBottom.Margin = new Thickness(16, 16, 16, 216);
#endif
            }

#if DEBUG && false
            grdMain.RowDefinitions.Add(new RowDefinition(200));
            ObservableCollection<string> items = new ObservableCollection<string>()
            {
                "调试输出"
            };
            CollectionView debugListView = new CollectionView()
            {
                ItemsSource = items,
                ItemTemplate = new DataTemplate(() =>
                {
                    var label = new Label();
                    label.SetBinding(Label.TextProperty, new Binding("."));
                    return label;
                }),
            };
            items.CollectionChanged += (s, e) =>
            {
                try
                {
                    debugListView.ScrollTo(items.Count - 1);
                }
                catch(Exception ex)
                { 
                }
            };
            grdMain.Children.Add(debugListView);
            Microsoft.Maui.Controls.Grid.SetRow(debugListView, grdMain.RowDefinitions.Count - 1);
            Trace.Listeners.Add(new DebugListener(items));
#endif
        }

        public static MainPage Current { get; private set; }

        public async Task CheckAndRequestLocationPermission()
        {
            while ((await CheckStatusAsync<LocationAlways>()) != PermissionStatus.Granted)
            {
                if (ShouldShowRationale<LocationAlways>())
                {
                    await DisplayAlert("需要权限", "该应用需要定位权限，否则无法正常工作", "确定");
                }
                else
                {
                    await DisplayAlert("需要权限", "该应用需要定位权限，否则无法正常工作", "进入设置");
                    AppInfo.ShowSettingsUI();
                    return;
                }
                await RequestAsync<LocationAlways>();
            }

            while ((await CheckStatusAsync<LocationWhenInUse>()) != PermissionStatus.Granted)
            {
                if (ShouldShowRationale<LocationWhenInUse>())
                {
                    await DisplayAlert("需要权限", "该应用需要定位权限，否则无法正常工作", "确定");
                }
                else
                {
                    await DisplayAlert("需要权限", "该应用需要定位权限，否则无法正常工作", "进入设置");
                    AppInfo.ShowSettingsUI();
                    return;
                }
                await RequestAsync<LocationWhenInUse>();
            }

#if ANDROID
            var a = await CheckStatusAsync<AndroidNotificationPermission>();
            if ((await CheckStatusAsync<AndroidNotificationPermission>()) != PermissionStatus.Granted)
            {
                await DisplayAlert("需要权限", "该应用需要通知权限，否则定位无法持续工作", "确定");
                _ = RequestAsync<AndroidNotificationPermission>();
            }
#endif
        }

        public void CloseAllPanel()
        {
            if (sidePanels.Any(p => p.IsOpened && !p.Standalone))
            {
                foreach (var panel in sidePanels.Where(p => p.IsOpened && !p.Standalone))
                {
                    ClosePanel(panel.Type);
                    panel.IsOpened = false;
                }
            }
        }

        public void ClosePanel<T>()
        {
            ClosePanel(typeof(T));
        }

        public bool IsAnyNotStandalonePanelOpened()
        {
            return type2SidePanels.Values.Where(p => !p.Standalone).Any(p => p.IsOpened);
        }

        public bool IsPanelOpened<T>()
        {
            var type = typeof(T);
            return type2SidePanels[type].IsOpened;
        }

        public void OpenOrClosePanel<T>()
        {
            Type type = typeof(T);
            if (type2SidePanels[type].IsOpened)
            {
                ClosePanel<T>();
            }
            else
            {
                OpenPanel<T>();
            }
        }

        public async void OpenPanel<T>()
        {
            var type = typeof(T);
            CloseAllPanel();
            SidePanelInfo panel = type2SidePanels[type];
            panel.Content.OnPanelOpening();
            panel.IsOpened = true;
            Task<bool> task;
            if (panel.Length > 0)
            {
                task = panel.Container.TranslateTo(0, 0);
            }
            else
            {
                panel.Container.Opacity = 0;
                panel.Container.IsVisible = true;
                task = panel.Container.FadeTo(1);
            }

            await task;
            //上面的await执行完毕的时候，其实还没有完全展开
            await Task.Delay(50);
            panel.Content.OnPanelOpened();
        }

        private async void AddGeometryButton_Clicked(object sender, EventArgs e)
        {
            if (MainMapView.Current.CurrentStatus is not Models.MapViewStatus.Ready)
            {
                return;
            }
            CloseAllPanel();
            var layers = MainMapView.Current.Layers
                .OfType<IMapLayerInfo>()
                .Where(p => p.LayerVisible)
                .Where(p => p.CanEdit)
                .Select(p => new PopupMenuItem(p.Name) { Tag = p })
                .ToList();
            if (layers.Count == 0)
            {
                await Toast.Make("不存在任何可见可编辑图层").Show();
                return;
            }
            var result = await (sender as View).PopupMenuAsync(layers, "选择图层");
            if (result >= 0)
            {
                MainMapView.Current.Editor.StartDraw(layers[result].Tag as IMapLayerInfo);
            }
        }

        private async Task CheckCrashAsync()
        {
            var file = Directory.EnumerateFiles(FolderPaths.LogsPath, "Crash*.log")
                .OrderByDescending(p => p)
                .FirstOrDefault();
            if (file != null && file != Config.Instance.LastCrashFile)
            {
                Config.Instance.LastCrashFile = file;
                Config.Instance.Save();
                await DisplayAlert("上一次崩溃", File.ReadAllText(file), "确定");
            }
        }

        private void ClosePanel(Type type)
        {
            var panel = type2SidePanels[type];
            if (panel.Length == 0)
            {
                panel.Container.FadeTo(0).ContinueWith(t =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        panel.Container.IsVisible = false;
                    });
                });
            }
            else
            {
                switch (panel.Direction)
                {
                    case SwipeDirection.Right:
                        panel.Container.TranslateTo(panel.Length, 0);
                        break;
                    case SwipeDirection.Left:
                        panel.Container.TranslateTo(-panel.Length, 0);
                        break;
                    case SwipeDirection.Up:
                        panel.Container.TranslateTo(0, -panel.Length);
                        break;
                    case SwipeDirection.Down:
                        panel.Container.TranslateTo(0, panel.Length);
                        break;
                }
            }
            type2SidePanels[type].IsOpened = false;
            if (type2SidePanels[type].Content is ISidePanel s)
            {
                s.OnPanelClosed();
            }
        }

        private async void ContentPage_Loaded(object sender, EventArgs e)
        {
            if (Window != null)
            {
                Window.Title = "地图画板";
            }
            MainMapView.Current.GeoViewTapped += (s, e) => CloseAllPanel();
            MainMapView.Current.MapViewStatusChanged += MapView_BoardTaskChanged;
            MainMapView.Current.GeoShareExceptionThrow += GeoShareExceptionThrow;
            MainMapView.Current.MapLoaded += Current_MapLoaded;

#if ANDROID
            var navBarHeight = (Platform.CurrentActivity as MainActivity).GetNavBarHeight();
            //navBarHeight /= DeviceDisplay.MainDisplayInfo.Density;
            if (navBarHeight > 0)
            {
                if (DeviceInfo.Idiom == DeviceIdiom.Phone)
                {
                    //目前我的设备都不需要额外增加高度，增加了反而有问题，所以就先隐藏了
                    //bdBottom.Padding = new Thickness(bdBottom.Padding.Left, bdBottom.Padding.Top, bdBottom.Padding.Right, bdBottom.Padding.Bottom + navBarHeight);
                }
                if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
                {
                    //navBarHeight *= DeviceDisplay.MainDisplayInfo.Density;
                    grdMain.Margin = new Thickness(0, 0, 0, navBarHeight);
                }
            }

#endif

            SetStatusBarConsidered(true);

            InitializeFromConfigs();

            await CheckAndRequestLocationPermission();

            await CheckCrashAsync();

            await MainMapView.Current.InitializeLocationDisplayAsync();
        }

        private void Current_MapLoaded(object sender, EventArgs e)
        {
            MainMapView.Current.SearchOverlay.SearchResultDisplayChanged -= SearchOverlay_SearchResultDisplayChanged;
            MainMapView.Current.SearchOverlay.SearchResultDisplayChanged += SearchOverlay_SearchResultDisplayChanged;
        }

        private void FtpTapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            OpenOrClosePanel<FtpView>();
        }

        private async void GeoShareError_Tapped(object sender, TappedEventArgs e)
        {
            await DisplayAlert("位置共享错误", lastGeoShareException.Message, "确定");
            bdGeoShareError.IsVisible = false;
        }

        private void GeoShareExceptionThrow(object sender, ExceptionEventArgs e)
        {
            lastGeoShareException = e.Exception;
            bdGeoShareError.IsVisible = true;
        }

        private void ImportButton_Clicked(object sender, EventArgs e)
        {
            OpenOrClosePanel<ImportView>();
        }

        private void InitializeFromConfigs()
        {
            SetScreenAlwaysOn(Config.Instance.ScreenAlwaysOn);
            Config.Instance.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Config.ScreenAlwaysOn):
                        SetScreenAlwaysOn(Config.Instance.ScreenAlwaysOn);
                        break;
                }
            };

            void SetScreenAlwaysOn(bool on)
            {
#if ANDROID
                if (on)
                {
                    Platform.CurrentActivity.Window.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
                }
                else
                {
                    Platform.CurrentActivity.Window.ClearFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
                }
#endif
            }
        }

        private void InitializeSidePanels()
        {
            sidePanels =
            [
                new SidePanelInfo(layer, layerView),
                new SidePanelInfo(track, trackView),
                new SidePanelInfo(ftp, ftpView),
                new SidePanelInfo(baseLayer, baseLayerView),
                new SidePanelInfo(import, importView),
                new SidePanelInfo(cTrack, tbar){Length=0},
                new SidePanelInfo(cEdit, ebar){Length=0},
                new SidePanelInfo(cSearch, sbar){Length=0}
            ];
            type2SidePanels = sidePanels.ToDictionary(p => p.Type);

            foreach (var panel in sidePanels)
            {
                int length = panel.Length;
                if (length == 0)
                {
                    continue;
                }
                switch (panel.Direction)
                {
                    case SwipeDirection.Right:
                        panel.Container.WidthRequest = length;
                        panel.Container.TranslationX = length;
                        break;
                    case SwipeDirection.Left:
                        panel.Container.WidthRequest = length;
                        panel.Container.TranslationX = -length;
                        break;
                    case SwipeDirection.Up:
                        panel.Container.HeightRequest = length;
                        panel.Container.TranslationY = -length;
                        break;
                    case SwipeDirection.Down:
                        panel.Container.HeightRequest = length;
                        panel.Container.TranslationY = length;
                        break;
                    default:
                        break;
                }
            }
        }

        private void LayerButton_Click(object sender, EventArgs e)
        {
            if (MainMapView.Current.CurrentStatus == MapViewStatus.Ready)
            {
                OpenOrClosePanel<LayerListView>();
            }
        }

        private void MapView_BoardTaskChanged(object sender, EventArgs e)
        {
            if (MainMapView.Current.CurrentStatus is MapViewStatus.Select or MapViewStatus.Draw)
            {
                OpenPanel<EditBar>();
            }
            else
            {
                ClosePanel<EditBar>();
            }
        }

        private async void MenuButton_Clicked(object sender, EventArgs e)
        {
            var index = await PopupMenu.PopupMenuAsync(sender as View,
                 [
                    new PopupMenuItem("测量长度"),
                    new PopupMenuItem("测量面积"),
                    new PopupMenuItem("位置共享"),
                    new PopupMenuItem("设置"),
                    new PopupMenuItem("退出")
                 ]);
            switch (index)
            {
                case 0:
                    MainMapView.Current.Editor.StartMeasureLength();
                    break;
                case 1:
                    MainMapView.Current.Editor.StartMeasureArea();
                    break;
                case 2:
                    GeoShareConfigPopup popup = new GeoShareConfigPopup();
                    this.ShowPopup(popup);
                    break;
                case 3:
                    SettingPopup popup2 = new SettingPopup();
                    this.ShowPopup(popup2);
                    break;
                case 4:
                    if (TrackService.Current != null)
                    {
                        if (!await MainPage.Current.DisplayAlert("退出", "正在进行轨迹记录，是否停止？", "是", "否") == true)
                        {
                            return;
                        }
                        else
                        {
#if ANDROID
                            (Platform.CurrentActivity as MainActivity).StopTrackService();
#else
                            throw new NotImplementedException();
#endif
                        }
                    }
                    Application.Current.Quit();
                    break;
            }
        }

        private void MeterButton_Clicked(object sender, EventArgs e)
        {
            double start = 0;
            double end = 0;
            if (grdMeter.Bounds.Height == 0)//需要展开
            {
                end = Bounds.Height / 3;
                meterBar.OnPanelOpening();
                SetStatusBarConsidered(false);
#if ANDROID
                if (Application.Current.RequestedTheme == AppTheme.Light)
                {
                    (Platform.CurrentActivity as MainActivity).SetStatusBarColorBlack(true);
                }
#endif
            }
            else
            {
                start = Bounds.Height / 3;
                meterBar.OnPanelClosed();
                SetStatusBarConsidered(true);
#if ANDROID
                (Platform.CurrentActivity as MainActivity).SetStatusBarColorBlack(false);
#endif
            }
            grdMeter.Animate("Expand", new Animation(x => grdMeter.HeightRequest = x, start, end, easing: Easing.CubicInOut));
        }

        private async void RefreshButton_Clicked(object sender, EventArgs e)
        {
            var handle = ProgressPopup.Show("正在重载");
            await Task.Delay(TimeSpan.FromSeconds(0.2));
            try
            {
                await MainMapView.Current.LoadAsync();
            }
            finally
            {
                handle.Close();
            }
        }

        private void SearchOverlay_SearchResultDisplayChanged(object sender, SearchOverlayHelper.SearchResultDisplayChangedEventArgs e)
        {
            if (e.IsVisible)
            {
                OpenPanel<SearchResultBar>();
            }
            else
            {
                ClosePanel<SearchResultBar>();
            }
        }
        private void SetBaseLayersTapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            OpenPanel<BaseLayerView>();
        }

        private void SetStatusBarConsidered(bool consider)
        {
#if ANDROID       
            var statusBarHeight = (Platform.CurrentActivity as MainActivity).GetStatusBarHeight();
            Resources["MeterBarPadding"] = new Thickness(8, statusBarHeight + 8, 8, 8);
            Resources["SidePanelPadding"] = new Thickness(4, (consider ? statusBarHeight : 0) + 4, 4, 4);
#endif
        }

        private void TrackButton_Clicked(object sender, EventArgs e)
        {
            OpenOrClosePanel<TrackView>();
        }

        private void TrackService_CurrentChanged(object sender, EventArgs e)
        {
            if (TrackService.Current == null)
            {
                ClosePanel<TrackingBar>();
            }
            else
            {
                OpenPanel<TrackingBar>();
            }
        }

        private async void ZoomToLayerButton_Click(object sender, EventArgs e)
        {
            var layers = MainMapView.Current.Layers.Where(p => p.LayerVisible).ToDictionary(p => p.Name);
            if (layers.Count != 0)
            {
                List<PopupMenuItem> items = [new PopupMenuItem("全部")];
                foreach (var name in layers.Keys)
                {
                    items.Add(new PopupMenuItem(name));
                }
                var result = await (sender as View).PopupMenuAsync(items, "缩放到图层");
                //string result = await DisplayActionSheet("缩放到图层", "取消", "全部", layers.Keys.ToArray());
                if (result >= 0)
                {
                    Envelope extent = null;
                    var handle = ProgressPopup.Show("正在查找范围");
                    try
                    {
                        if (result > 0)
                        {
                            var name = items[result].Text;
                            var layer = layers[name];
                            extent = await (layer as IMapLayerInfo).QueryExtentAsync(new QueryParameters());
                        }
                        else
                        {
                            EnvelopeBuilder eb = new EnvelopeBuilder(SpatialReferences.Wgs84);
                            foreach (var layer2 in layers.Values)
                            {
                                var tempExtent = await (layer2 as IMapLayerInfo).QueryExtentAsync(new QueryParameters());
                                eb.UnionOf(tempExtent);
                            }
                            extent = eb.ToGeometry();
                        }
                        handle.Close();
                        await MainMapView.Current.ZoomToGeometryAsync(extent);
                    }
                    catch (Exception ex)
                    {
                        handle.Close();
                        await DisplayAlert("错误", ex.Message, "确定");
                    }
                }
            }
        }


#if DEBUG
        class DebugListener(ObservableCollection<string> items) : TraceListener
        {
            private readonly ObservableCollection<string> items = items;

            public override void Write(string message)
            {
                if (items.Count > 0)
                {
                    items[^1] = items[^1] + message;
                }
                else
                {
                    WriteLine(message);
                }
            }

            public override void WriteLine(string message)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    items.Add($"{DateTime.Now:mm:ss} {message}");
                });
            }
        }
#endif
    }

}
