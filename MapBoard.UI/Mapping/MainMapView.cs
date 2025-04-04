﻿using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.UI.Editing;
using FzLib;
using FzLib.WPF;
using FzLib.WPF.Dialog;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.UI;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MapBoard.Mapping
{
    /// <summary>
    /// 主地图画板地图
    /// </summary>
    [DoNotNotify]
    public class MainMapView : MapView, IMapBoardGeoView
    {
        /// <summary>
        /// 画板当前任务
        /// </summary>
        private static BoardTask currentTask = BoardTask.NotReady;

        /// <summary>
        /// 所有<see cref="MainMapView"/>实例
        /// </summary>
        private static List<MainMapView> instances = new List<MainMapView>();

        /// <summary>
        /// 是否允许旋转
        /// </summary>
        private bool canRotate = true;

        /// <summary>
        /// 鼠标中键按下时起始位置
        /// </summary>
        private Point startPosition = default;

        /// <summary>
        /// 旋转开始角度
        /// </summary>
        private double startRotation = 0;

        public MainMapView()
        {
            instances.Add(this);
            Layers = new MapLayerCollection();
            AllowDrop = true;
            IsAttributionTextVisible = false;
            this.SetHideWatermark();

            InteractionOptions = new MapViewInteractionOptions()
            {
                IsRotateEnabled = true
            };
            SetLocationDisplay();
            Config.Instance.PropertyChanged += Config_PropertyChanged;
        }

        /// <summary>
        /// 画板当前任务改变事件
        /// </summary>
        public event EventHandler<BoardTaskChangedEventArgs> BoardTaskChanged;

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
        public BoardTask CurrentTask
        {
            get => currentTask;
            set
            {
                if (currentTask != value)
                {
                    BoardTask oldTask = currentTask;
                    currentTask = value;

                    BoardTaskChanged?.Invoke(null, new BoardTaskChangedEventArgs(oldTask, value));
                }
            }
        }

        /// <summary>
        /// 编辑器相关
        /// </summary>
        public EditorHelper Editor { get; private set; }

        /// <summary>
        /// 图层
        /// </summary>
        public MapLayerCollection Layers { get; }

        /// <summary>
        /// 覆盖层相关
        /// </summary>
        public OverlayHelper Overlay { get; private set; }

        /// <summary>
        /// 选择相关
        /// </summary>
        public SelectionHelper Selection { get; private set; }

        /// <summary>
        /// 初始化加载
        /// </summary>
        /// <returns></returns>
        public async Task LoadAsync()
        {
            BaseMapLoadErrors = await GeoViewHelper.LoadBaseGeoViewAsync(this, Config.Instance.EnableBasemapCache);
            Map.MaxScale = Config.Instance.MaxScale;
            await Layers.LoadAsync(Map.OperationalLayers);
            this.TryZoomToLastExtent().ContinueWith(t => ViewpointChanged += ArcMapView_ViewpointChanged);
            Editor = new EditorHelper(this);
            Selection = new SelectionHelper(this);
            Overlay = new OverlayHelper(GraphicsOverlays, async p => await this.ZoomToGeometryAsync(p));
            Selection.CollectionChanged += Selection_CollectionChanged;
            CurrentTask = BoardTask.Ready;
        }

        /// <summary>
        /// 设置设备位置的显示
        /// </summary>
        public void SetLocationDisplay()
        {
            LocationDisplay.ShowLocation = Config.Instance.ShowLocation;
            LocationDisplay.IsEnabled = Config.Instance.ShowLocation;
        }


        /// <summary>
        /// 键盘按下事件
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            switch (e.Key)
            {
                //Delete：移除节点、删除要素
                case Key.Delete when GeometryEditor.SelectedElement is GeometryEditorVertex:
                    GeometryEditor.DeleteSelectedElement();
                    break;

                case Key.Delete when CurrentTask == BoardTask.Select && Layers.Selected is IMapLayerInfo w:
                    await (this.GetWindow() as MainWindow).DoAsync(async () =>
                   {
                       await FeatureUtility.DeleteAsync(w, Selection.SelectedFeatures.ToArray());
                       Selection.ClearSelection();
                   }, "正在删除", true);
                    break;

                //空格、回车：开始/结束绘图、选择模式下开始编辑
                case Key.Space:
                case Key.Enter:
                    switch (CurrentTask)
                    {
                        case BoardTask.Draw:
                            Editor.StopAndSave();
                            break;

                        case BoardTask.Ready
                        when Layers.Selected?.LayerVisible == true
                        && Layers.Selected?.Interaction?.CanEdit == true:
                            await Editor.DrawAsync(Layers.Selected.GeometryType, null);
                            break;

                        case BoardTask.Select
                        when Selection.SelectedFeatures.Count == 1
                            && Layers.Selected?.LayerVisible == true
                            && Layers.Selected?.CanEdit == true:
                            var feature = Selection.SelectedFeatures.Single();
                            Selection.ClearSelection();
                            Editor.EditAsync(Layers.Selected as IMapLayerInfo, feature);
                            break;
                    }
                    break;

                //ESC：退出当前状态，返回就绪状态
                case Key.Escape
                when CurrentTask == BoardTask.Draw:
                    Editor.Cancel();
                    break;

                case Key.Escape
                when Selection.SelectedFeatures.Count > 0:
                    Selection.ClearSelection();
                    break;

                //Ctrl Z：撤销
                case Key.Z when Keyboard.Modifiers == ModifierKeys.Control && GeometryEditor.CanUndo:
                    GeometryEditor.Undo();
                    break;

                //Ctrl SHift Z/Ctrl Y：重做
                case Key.Z when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && GeometryEditor.CanRedo:
                    GeometryEditor.Redo();
                    break;
                case Key.Y when Keyboard.Modifiers == ModifierKeys.Control && GeometryEditor.CanRedo:
                    GeometryEditor.Redo();
                    break;
            }
        }

        protected override void OnPreviewMouseDoubleClick(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDoubleClick(e);
            if (e.ChangedButton.HasFlag(MouseButton.Middle))
            {
                SetViewpointRotationAsync(0);
            }
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                startRotation = MapRotation;
                startPosition = e.GetPosition(this);
            }
        }

        /// <summary>
        /// 鼠标移动，中键按下时旋转地图
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (e.MiddleButton == MouseButtonState.Pressed && canRotate)
            {
                Point position = e.GetPosition(this);
                double distance = position.X - startPosition.X;
                if (Math.Abs(distance) < 10)
                {
                    return;
                }
                Debug.WriteLine(distance);
                canRotate = false;
                SetViewpointRotationAsync(startRotation + distance / 5);
                await Task.Delay(100);
                canRotate = true;
                //防止旋转过快造成卡顿
            }
        }

        /// <summary>
        /// 视角改变时，保存当前地图位置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ArcMapView_ViewpointChanged(object sender, EventArgs e)
        {
            if (Layers != null
                && GetCurrentViewpoint(ViewpointType.BoundingGeometry)?.TargetGeometry is Envelope envelope)
            {
                Layers.MapViewExtentJson = envelope.ToJson();
            }
        }


        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Config.ShowLocation))
            {
                SetLocationDisplay();
            }
        }


        private void Selection_CollectionChanged(object sender, EventArgs e)
        {
            if (Selection.SelectedFeatures.Count > 0)
            {
                Layer selectionLayer = Selection.SelectedFeatures.First().FeatureTable.Layer;
                if (selectionLayer != Layers.Selected.Layer)
                {
                    Layers.Selected = Layers.FirstOrDefault(p => (p as MapLayerInfo).Layer == selectionLayer) as MapLayerInfo;
                }
            }
        }
    }
}