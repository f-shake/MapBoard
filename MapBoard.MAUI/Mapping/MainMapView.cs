using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.Maui;
using Esri.ArcGISRuntime.UI.Editing;
using FzLib;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Symbology;
using NGettext.Loaders;
using Esri.ArcGISRuntime.Mapping.Popups;
using System.Text;
using MapBoard.Views;
using System.Reflection;
using MapBoard.Models;
using MapBoard.Services;
using MapBoard.GeoShare.Core.Dto;

namespace MapBoard.Mapping
{
    /// <summary>
    /// 主地图画板地图
    /// </summary>
    public class MainMapView : MapView
    {
        /// <summary>
        /// 画板当前任务
        /// </summary>
        private static Models.MapViewStatus currentStatus = Models.MapViewStatus.Ready;

        /// <summary>
        /// 所有<see cref="MainMapView"/>实例
        /// </summary>
        private static List<MainMapView> instances = new List<MainMapView>();

        private readonly RuntimeImage closeImage = new RuntimeImage(
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEU" +
            "gAAADAAAAAwCAYAAABXAvmHAAAABHNCSVQICAgIfAhkiAAAAL" +
            "9JREFUaIHt10sOgzAQA1CrJ/WNyk3LDdpNs0G0JDMOZVQ/iRXEYyS+" +
            "gJmZmZmNWgA8AVCQxXfWXZDVpZVvGxNZ3GQtyW5d1s3Q6ElwJ2e" +
            "VNAwMHj0JRUZKpkBmrVSkSGTNVCOFRo49FXFcrOeYnyI+F/y271KI/aIlyj" +
            "dE4fINcWL5mzqwOqLwJUQUvomJwo9RovCLjCj8KREpFFkzRaZIZq2EooAiI" +
            "2zmL+VD0nBwMC+SFRqsGKjMMjMzM/srL6TW4v8zOk00AAAAAElFTkSuQmCC"));

        private readonly double watermarkHeight = 72;


        private bool isZoomingToLastExtent = false;

        private Feature selectedFeature = null;

        public MainMapView()
        {
            if (instances.Count > 1)
            {
                throw new Exception("该类仅支持单例");
            }
            instances.Add(this);
            Layers = new MapLayerCollection();
            Editor = new EditHelper(this);
            IsAttributionTextVisible = false;
            Margin = new Thickness(-watermarkHeight);

            InteractionOptions = new MapViewInteractionOptions()
            {
                IsRotateEnabled = Config.Instance.CanRotate,
            };
            Config.Instance.PropertyChanged += Config_PropertyChanged;

            //启动时恢复到原来的视角，并定时保存
            Loaded += MainMapView_Loaded;
            GeoViewTapped += MainMapView_GeoViewTapped;

            SelectedFeatureChanged += (s, e) => UpdateBoardTask();
            Editor.EditStatusChanged += (s, e) => UpdateBoardTask();
        }

        public event EventHandler<ExceptionEventArgs> GeoShareExceptionThrow;

        public event EventHandler MapLoaded;

        /// <summary>
        /// 画板当前任务改变事件
        /// </summary>
        public event EventHandler MapViewStatusChanged;

        public event EventHandler SelectedFeatureChanged;
        
        public static MainMapView Current => instances[0];

        /// <summary>
        /// 所有<see cref="MainMapView"/>实例
        /// </summary>
        public static IReadOnlyList<MainMapView> Instances => instances.AsReadOnly();

        /// <summary>
        /// 底图加载错误
        /// </summary>
        public ItemsOperationErrorCollection BaseMapLoadErrors { get; private set; }

        /// <summary>
        /// 画板当前任务
        /// </summary>
        public Models.MapViewStatus CurrentStatus
        {
            get => currentStatus;
            set
            {
                if (currentStatus != value)
                {
                    MapViewStatus oldStatus = currentStatus;
                    currentStatus = value;

                    MapViewStatusChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public EditHelper Editor { get; private set; }

        public GeoShareService GeoShareService { get; private set; }

        /// <summary>
        /// 图层
        /// </summary>
        public MapLayerCollection Layers { get; }

        public Feature SelectedFeature
        {
            get => selectedFeature;
            set
            {
                if (selectedFeature != value)
                {
                    selectedFeature = value;
                    SelectedFeatureChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public TrackOverlayHelper TrackOverlay { get; private set; }

        public SearchOverlayHelper SearchOverlay { get; private set; }

        public void ClearSelection()
        {
            DismissCallout();
            if (SelectedFeature != null)
            {
                (SelectedFeature.FeatureTable.Layer as FeatureLayer).UnselectFeature(SelectedFeature);
                SelectedFeature = null;
            }
        }

        public async void DeleteSelectedFeatureAsync()
        {
            if (SelectedFeature == null)
            {
                throw new Exception("没有选中任何要素");
            }

            var feature = SelectedFeature;
            ClearSelection();
            await feature.FeatureTable.DeleteFeatureAsync(feature);
        }

        public async Task InitializeLocationDisplayAsync()
        {
            if (LocationDisplay == null)
            {
                throw new Exception("LocationDispaly为空");
            }

            LocationDisplay.NavigationPointHeightFactor = 0.4;
            LocationDisplay.WanderExtentFactor = 0;

#if ANDROID
            if (LocationDisplay.DataSource is not Platforms.Android.LocationDataSourceAndroidImpl)
            {
                LocationDisplay.DataSource = new Platforms.Android.LocationDataSourceAndroidImpl();

                LocationDisplay.DataSource.ErrorChanged += async (s, e) =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        MainPage.Current.DisplayAlert("位置源错误", e.Message, "关闭");
                    });
                };
            }
#endif

#if DEBUG
            LocationDisplay.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocationDisplay.IsEnabled))
                {
                    Debug.WriteLine($"{nameof(LocationDisplay.IsEnabled)}: {LocationDisplay.IsEnabled}");
                }
            };
#endif
            await LocationDisplay.DataSource.StartAsync();
        }

        /// <summary>
        /// 初始化加载
        /// </summary>
        /// <returns></returns>
        public async Task LoadAsync()
        {
            BaseMapLoadErrors = await MapViewHelper.LoadBaseGeoViewAsync(this, Config.Instance.EnableBasemapCache);
            Map.MaxScale = Config.Instance.MaxScale;
            Map.OperationalLayers.Clear();
            await Layers.LoadAsync(Map.OperationalLayers);
            CurrentStatus = MapViewStatus.Ready;

            if (TrackOverlay == null)
            {
                var overlay = new GraphicsOverlay()
                {
                    Renderer = new SimpleRenderer(new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.FromArgb(0x54, 0xA5, 0xF6), 6))
                };
                GraphicsOverlays.Add(overlay);
                TrackOverlay = new TrackOverlayHelper(overlay);
            }

            if(SearchOverlay==null)
            {
                var overlay = new GraphicsOverlay();
                GraphicsOverlays.Add(overlay);
                SearchOverlay = new SearchOverlayHelper(overlay);
            }

            if (GeoShareService == null)
            {
                var overlay = new GraphicsOverlay()
                {
                    Renderer = new SimpleRenderer(new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, System.Drawing.Color.Orange, 10)
                    { Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 2) })
                };
                GraphicsOverlays.Add(overlay);
                GeoShareService = new GeoShareService(overlay);
                GeoShareService.GeoShareExceptionThrow += GeoShareService_GeoShareExceptionThrow;
                GeoShareService.Start();
            }
            MapLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void MoveToLocation()
        {
            if (LocationDisplay != null && LocationDisplay.IsEnabled)
            {
                var point = LocationDisplay.MapLocation;
                if (point != null)
                {
                    SetViewpointCenterAsync(point);
                }
            }
        }

        private string BuildCalloutText(Feature feature)
        {
            var attrStr = new StringBuilder();

            Dictionary<string, string> key2Desc = Layers
                .First(p => (p as MapLayerInfo).Layer == feature.FeatureTable.Layer)
                .Fields
                .ToDictionary(p => p.Name, p => p.DisplayName);
            foreach (var kv in feature.Attributes)
            {

                if (FieldExtension.IsIdField(kv.Key) || kv.Key is (Parameters.CreateTimeFieldName or Parameters.ModifiedTimeFieldName))
                {
                    continue;
                }

                if (kv.Value is not (null or ""))
                {
                    if (key2Desc.TryGetValue(kv.Key, out var value))
                    {
                        attrStr.Append(value);
                    }
                    else
                    {
                        attrStr.Append(kv.Key);
                    }

                    attrStr.Append('：');
                    if (kv.Value is DateTimeOffset dto)
                    {
                        attrStr.AppendLine(dto.DateTime.ToString(Parameters.DateFormat));
                    }
                    else
                    {
                        attrStr.AppendLine(kv.Value.ToString());
                    }
                }
            }

            return attrStr.ToString().Trim();
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Config.CanRotate):
                    InteractionOptions.IsRotateEnabled = Config.Instance.CanRotate;
                    break;
            }
        }

        private void GeoShareService_GeoShareExceptionThrow(object sender, ExceptionEventArgs e)
        {
            GeoShareExceptionThrow?.Invoke(sender, e);
        }

        private async void MainMapView_GeoViewTapped(object sender, GeoViewInputEventArgs e)
        {
            if (Editor.Status is not EditorStatus.NotRunning)
            {
                return;
            }
            try
            {
                if (!await TapToSelectOverlayAsync(e))
                {
                    if (!await TapToSelectFeatureAsync(e))
                    {
                        ClearSelection();
                    }
                }
            }
            catch (Exception ex)
            {
                await MainPage.Current.DisplayAlert("选取失败", ex.Message, "确定");
            }
        }

        private async void MainMapView_Loaded(object sender, EventArgs e)
        {
            if (Map == null)
            {
                await LoadAsync();
            }
            if (!isZoomingToLastExtent)
            {
                isZoomingToLastExtent = true;
                await this.TryZoomToLastExtent();
                isZoomingToLastExtent = false;
            }
        }

        private void SelectFeature(Feature feature, Point point)
        {
            if (SelectedFeature != null)
            {
                (SelectedFeature.FeatureTable.Layer as FeatureLayer).UnselectFeature(SelectedFeature);
            }
            (feature.FeatureTable.Layer as FeatureLayer).SelectFeature(feature);
            ShowCallout(point, feature, Layers.Find(feature.FeatureTable.Layer)?.Name ?? "未知图层", BuildCalloutText(feature), false);
            SelectedFeature = feature;
        }

        private void ShowCallout(Point point, GeoElement graphic, string text, string detail, bool cancelButton)
        {
            CalloutDefinition cd = new CalloutDefinition(graphic)
            {
                Text = text,
                DetailText = detail,
                OnButtonClick = a =>
                {
                    ClearSelection();
                }
            };
            if (cancelButton)
            {
                cd.ButtonImage = closeImage;
            }
            ShowCalloutForGeoElement(graphic, point, cd);
        }

        private async Task<bool> TapGeoShareOverlay(GeoViewInputEventArgs e)
        {
            var result = await IdentifyGraphicsOverlayAsync(GeoShareService.Overlay, e.Position, 10, false, 1);
            if (result != null && result.Graphics.Count > 0)
            {
                var graphic = result.Graphics[0];
                if (graphic.Attributes.Count == 0)
                {
                    return false;
                }
                var username = graphic.Attributes[nameof(UserLocationDto.UserName)] as string;
                var timeString = ((DateTime)graphic.Attributes[nameof(UserLocationDto.Location.Time)]).ToString("yyyy-MM-dd HH:mm:ss");
                var altitude = (double)graphic.Attributes[nameof(UserLocationDto.Location.Altitude)];
                var detailString = $"时间：{timeString}{Environment.NewLine}海拔：{altitude:0.0} m";
                ShowCallout(e.Position, graphic, username, detailString, true);
                return true;
            }
            return false;
        }

        private async Task<bool> TapToSelectFeatureAsync(GeoViewInputEventArgs e)
        {
            IEnumerable<IdentifyLayerResult> results = await IdentifyLayersAsync(e.Position, 10, false, 1);
            results = results.Where(p => Layers.Find(p.LayerContent).Interaction.CanSelect);
            if (results.Any())
            {
                if (results.Select(p => p.LayerContent)
                    .OfType<FeatureLayer>() //如果存在非面图形，只查找非面图形
                    .Any(p => p.FeatureTable.GeometryType is GeometryType.Point or GeometryType.Multipoint or GeometryType.Polyline))
                {
                    results = results
                    .Where(p => (p.LayerContent as FeatureLayer).FeatureTable.GeometryType is GeometryType.Point or GeometryType.Multipoint or GeometryType.Polyline);
                    //下面三条语句用于寻找各图层中最近的那个
                    var features = results.Select(p => p.GeoElements[0] as Feature).ToList();
                    var distances = features.Select(p => GeometryEngine.NearestCoordinate(p.Geometry, e.Location.ToWgs84())?.Distance ?? double.MaxValue).ToList();
                    var index = distances.IndexOf(distances.Min());
                    SelectFeature(features[index], e.Position);
                }
                else //如果只有面，那么随便选
                {
                    SelectFeature(results.First().GeoElements[0] as Feature, e.Position);
                }
                return true;
            }
            return false;
        }

        private async Task<bool> TapToSelectOverlayAsync(GeoViewInputEventArgs e)
        {
            return await TapTrackOverlay(e) || await TapGeoShareOverlay(e);
        }

        private async Task<bool> TapTrackOverlay(GeoViewInputEventArgs e)
        {
            var result = await IdentifyGraphicsOverlayAsync(TrackOverlay.Overlay, e.Position, 10, false, 1);
            if (result != null && result.Graphics.Count > 0)
            {
                var graphic = result.Graphics[0];
                if (graphic.Attributes.Count == 0)
                {
                    return false;
                }
                var time = (DateTime)graphic.Attributes["Time"];
                var speed = (double)graphic.Attributes["Speed"];
                var timeString = time.ToString("HH:mm:ss");
                var detailString = $"速度：{speed:0.0} m/s, {speed * 3.6:0.0} km/h";
                if (graphic.Attributes.TryGetValue("Altitude", out object objAlt))
                {
                    var altitude = (double)objAlt;
                    detailString = $"{detailString}{Environment.NewLine}海拔：{altitude:0.0}m";
                }
                if (graphic.Attributes.TryGetValue("Distance", out object objDist))
                {
                    var distance = (double)objDist;
                    string distanceString = distance < 1000 ? $"{distance:0}m" : $"{distance / 1000:0.00}km";
                    detailString = $"{detailString}{Environment.NewLine}距离：{distanceString}";
                }
                ShowCallout(e.Position, graphic, timeString, detailString, true);
                return true;
            }
            return false;
        }

        private void UpdateBoardTask()
        {
            if (Editor.Status is not EditorStatus.NotRunning)
            {
                CurrentStatus = MapViewStatus.Draw;
                ClearSelection();
            }
            else if (SelectedFeature != null)
            {
                CurrentStatus = MapViewStatus.Select;
            }
            else
            {
                CurrentStatus = MapViewStatus.Ready;
            }
        }
    }
}