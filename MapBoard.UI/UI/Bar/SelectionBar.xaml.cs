using Esri.ArcGISRuntime.Geometry;
using FzLib;
using FzLib.WPF.Dialog;
using MapBoard.UI.Dialog;
using MapBoard.Mapping;
using MapBoard.Util;
using ModernWpf.Controls;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MapBoard.Mapping.Model;
using FzLib.WPF;
using MapBoard.IO;
using System.IO;
using MapBoard.UI.Menu;
using Esri.ArcGISRuntime.Data;

namespace MapBoard.UI.Bar
{

    /// <summary>
    /// 选择条
    /// </summary>
    public partial class SelectionBar : BarBase
    {
        private FeatureAttributeCollection attributes;

        /// <summary>
        /// 缩略图吸附窗口
        /// </summary>
        private ShowImageDialog imageDialog;

        /// <summary>
        /// 选择要素吸附窗口
        /// </summary>
        private SelectFeatureDialog selectFeatureDialog;
        public SelectionBar()
        {
            InitializeComponent();
        }

        public override FeatureAttributeCollection Attributes => attributes;

        /// <summary>
        /// 图层是否可编辑
        /// </summary>
        public bool IsLayerEditable =>
            (Layers?.Selected) != null
            && Layers?.Selected?.CanEdit == true;

        /// <summary>
        /// 提示信息
        /// </summary>
        public string Message { get; set; } = "正在编辑";

        protected override ExpandDirection ExpandDirection => ExpandDirection.Down;

        public override void Initialize()
        {
            MapView.BoardTaskChanged += BoardTaskChanged;
            MapView.Selection.CollectionChanged += SelectedFeaturesChanged;
        }

        /// <summary>
        /// 如果在选择模式，则展开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BoardTaskChanged(object sender, BoardTaskChangedEventArgs e)
        {
            if (e.NewTask == BoardTask.Select)
            {
                //SelectedFeaturesChanged(null, null);
                Expand();
            }
            else
            {
                Collapse();
            }
        }

        /// <summary>
        /// 单击导出按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnExportClick(object sender, RoutedEventArgs e)
        {
            var layer = Layers.Selected;
            var features = MapView.Selection.SelectedFeatures.ToArray();

            FeatureLayerMenuHelper menu = new FeatureLayerMenuHelper(this.GetWindow() as MainWindow, MapView, layer, features);
            OpenMenus(menu.GetExportMenus(header => $"正在{header}"), sender as UIElement);
        }


        /// <summary>
        /// 单击取消按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MapView.Selection.ClearSelection();
        }

        /// <summary>
        /// 单击拷贝按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            await (this.GetWindow() as MainWindow).DoAsync(async () =>
            {
                SelectLayerDialog dialog = new SelectLayerDialog(MapView.Layers,
                    p => p.CanEdit && p.GeometryType == MapView.Layers.Selected.GeometryType,
                    true);
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await FeatureUtility.CopyToLayerAsync(Layers.Selected, dialog.SelectedLayer, MapView.Selection.SelectedFeatures);

                    if ((sender as Button)?.Tag as string == "Move")
                    {
                        await FeatureUtility.DeleteAsync(Layers.Selected, MapView.Selection.SelectedFeatures);
                    }

                    MapView.Selection.ClearSelection();
                }
            }, "正在复制图形");
        }

        /// <summary>
        /// 单击分割按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CutButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(Layers.Selected is IMapLayerInfo);
            var features = MapView.Selection.SelectedFeatures.ToArray();
            var line = await MapView.Editor.GetPolylineAsync();

            if (line != null)
            {
                await (this.GetWindow() as MainWindow).DoAsync(async () =>
                {
                    var result = await FeatureUtility.CutAsync(Layers.Selected as IMapLayerInfo, features, line);
                    SnakeBar.Show($"已分割为{result.Count}个图形");
                    MapView.Selection.ClearSelection();
                }, "正在分割", true);
            }
        }

        /// <summary>
        /// 单击删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(Layers.Selected is IMapLayerInfo);
            await (this.GetWindow() as MainWindow).DoAsync(async () =>
            {
                await FeatureUtility.DeleteAsync(Layers.Selected as IMapLayerInfo, MapView.Selection.SelectedFeatures.ToArray());
                MapView.Selection.ClearSelection();
            }, "正在删除", true);
        }

        /// <summary>
        /// 单击编辑按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(MapView.Selection.SelectedFeatures.Count == 1);
            Debug.Assert(Layers.Selected is IMapLayerInfo);
            var feature = MapView.Selection.SelectedFeatures.First();
            MapView.Selection.ClearSelection();
            await MapView.Editor.EditAsync(Layers.Selected as IMapLayerInfo, feature);
        }

        /// <summary>
        /// 单击要素操作按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FeatureOperationButton_Click(object sender, RoutedEventArgs e)
        {
            if (Layers.Selected is not IMapLayerInfo layer)
            {
                return;
            }
            var features = MapView.Selection.SelectedFeatures.ToArray();
            FeatureLayerMenuHelper menu = new FeatureLayerMenuHelper(this.GetWindow() as MainWindow, MapView, layer, features);
            OpenMenus(menu.GetEditMenus(header => $"正在进行{header}操作"), sender as UIElement);

        }
        /// <summary>
        /// 更新信息
        /// </summary>
        /// <param name="count"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task LoadMessages(int count, IMapLayerInfo layer)
        {
            StringBuilder sb = new StringBuilder($"已选择{MapView.Selection.SelectedFeatures.Count}个图形");
            Message = sb.ToString();
            await Task.Run(() =>
            {
                try
                {
                    switch (layer.GeometryType)
                    {
                        case GeometryType.Point when count == 1:
                            var point = MapView.Selection.SelectedFeatures.First().Geometry as MapPoint;
                            sb.Append($"，经度={point.X:0.0000000}，纬度={point.Y:0.0000000}");
                            break;

                        case GeometryType.Envelope:
                            return;

                        case GeometryType.Polyline:
                            double length = MapView.Selection.SelectedFeatures.Sum(p => p.Geometry.GetLength());
                            sb.Append("，长度：" + (length < 10000 ? $"{length:0.000}米" : $"{length / 1000:0.000}千米"));

                            break;

                        case GeometryType.Polygon:
                            double length2 = MapView.Selection.SelectedFeatures.Sum(p => p.Geometry.GetLength());
                            double area = MapView.Selection.SelectedFeatures.Sum(p => p.Geometry.GetArea());
                            sb.Append("，周长：" + (length2 < 10000 ? $"{length2:0.000}米" : $"{length2 / 1000:0.000}千米"));
                            sb.Append("，面积：" + (area < 1_000_000 ? $"{area:0.000}平方米" : $"{area / 1_000_000:0.000}平方千米"));

                            break;

                        case GeometryType.Multipoint:
                            int pointCount = MapView.Selection.SelectedFeatures
                            .Select(p => (p.Geometry as Multipoint).Points.Count).Sum();
                            sb.Append($"，{pointCount}个点");
                            break;

                        default:
                            return;
                    }
                }
                catch (InvalidOperationException)
                {
                    //当MapView.Selection.SelectedFeatures集合发生改变时会抛出错误，此时不需要继续进行计算
                    return;
                }
            });
            Message = sb.ToString();

            this.Notify(nameof(Attributes));
        }

        /// <summary>
        /// 加载选择要素窗口
        /// </summary>
        /// <param name="count"></param>
        private void LoadSelectFeatureDialog(int count)
        {
            if (count > 1 && (selectFeatureDialog == null || selectFeatureDialog.IsClosed))
            {
                selectFeatureDialog = new SelectFeatureDialog(this.GetWindow(), MapView, MapView.Layers);
                selectFeatureDialog.Show(); ;
            }
        }

        /// <summary>
        /// 选择了包含图片的要素
        /// </summary>
        /// <returns></returns>
        private async Task OnSelectingImagePointAsync()
        {
            if (MapView.Selection.SelectedFeatures.Count != 1)
            {
                return;
            }
            var feature = MapView.Selection.SelectedFeatures.FirstOrDefault();
            if (feature.Attributes.ContainsKey(Photo.ImagePathField))
            {
                string path = feature.GetAttributeValue(Photo.ImagePathField) as string;
                if (path != null && File.Exists(path))
                {
                    if (imageDialog == null || imageDialog.Visibility != Visibility.Visible)
                    {
                        imageDialog = ShowImageDialog.CreateAndShow(this.GetWindow(), MapView);
                    }
                    await imageDialog.SetImageAsync(path);
                }
            }
        }

        /// <summary>
        /// 打开菜单
        /// </summary>
        /// <param name="menus"></param>
        /// <param name="parent"></param>
        private void OpenMenus(IEnumerable<MenuItem> menus, UIElement parent)
        {
            ContextMenu menu = new ContextMenu();

            menus.ForEach(p => menu.Items.Add(p));
            menu.Placement = PlacementMode.Bottom;
            menu.PlacementTarget = parent;
            menu.IsOpen = true;
        }

        /// <summary>
        /// 选择的要素改变，更新按钮可用性，按需弹出图片框和选择要素框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SelectedFeaturesChanged(object sender, EventArgs e)
        {
            if (MapView.CurrentTask != BoardTask.Select)
            {
                OnSelectingImagePointAsync();
                return;
            }
            int count = MapView.Selection.SelectedFeatures.Count;
            btnEdit.IsEnabled = count == 1 && IsLayerEditable;
            //btnMoreAttributes.IsEnabled = count == 1;
            var layer = Layers.Selected;
            btnCut.IsEnabled = (layer.GeometryType == GeometryType.Polygon
                || layer.GeometryType == GeometryType.Polyline)
                && IsLayerEditable;
            btnDelete.IsEnabled = IsLayerEditable;
            btnMenu.IsEnabled = IsLayerEditable;
            attributes = count != 1 ?
                null : FeatureAttributeCollection.FromFeature(layer, MapView.Selection.SelectedFeatures.First());
            LoadSelectFeatureDialog(count);
            await LoadMessages(count, layer);
            Debug.WriteLine(count);
            await OnSelectingImagePointAsync();
        }
    }
}