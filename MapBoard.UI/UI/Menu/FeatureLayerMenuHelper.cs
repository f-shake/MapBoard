using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI.Controls;
using FzLib.WPF.Dialog;
using MapBoard.IO;
using MapBoard.IO.Formats;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.UI.Dialog;
using MapBoard.Util;
using Microsoft.Win32;
using ModernWpf.Controls;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommonDialog = ModernWpf.FzExtension.CommonDialog.CommonDialog;

namespace MapBoard.UI.Menu
{
    /// <summary>
    /// 矢量空间分析的菜单帮助类
    /// </summary>
    public class FeatureLayerMenuHelper
    {
        /// <summary>
        /// 地图
        /// </summary>
        public MainMapView mapView;

        /// <summary>
        /// 可编辑图层
        /// </summary>
        private readonly IMapLayerInfo editableLayer;

        /// <summary>
        /// 选择的要素集合
        /// </summary>
        private readonly Feature[] features;

        /// <summary>
        /// 图层
        /// </summary>
        private readonly IMapLayerInfo layer;

        /// <summary>
        /// 主窗口
        /// </summary>
        private readonly MainWindow mainWindow;

        public FeatureLayerMenuHelper(MainWindow mainWindow, MainMapView mapView, IMapLayerInfo layer, Feature[] features)
        {
            this.mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
            editableLayer = layer as IMapLayerInfo;
            this.features = features ?? throw new ArgumentNullException(nameof(features));
            this.mapView = mapView ?? throw new ArgumentNullException(nameof(mapView));
        }

        /// <summary>
        /// 获取编辑菜单
        /// </summary>
        /// <param name="getMessage"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public IEnumerable<MenuItem> GetEditMenus(Func<string, string> getMessage)
        {
            if (editableLayer == null || !layer.CanEdit)
            {
                throw new NotSupportedException("图层不可编辑");
            }
            return GetMenus(getMessage, GetEditMenus());
        }

        /// <summary>
        /// 获取导出菜单
        /// </summary>
        /// <param name="getMessage"></param>
        /// <returns></returns>
        public IEnumerable<MenuItem> GetExportMenus(Func<string, string> getMessage)
        {
            return GetMenus(getMessage, GetExportMenus());
        }

        /// <summary>
        /// 建立缓冲区
        /// </summary>
        /// <returns></returns>
        private async Task BufferAsync()
        {
            var dialog = new BufferDialog(mapView.Layers);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await LayerUtility.BufferAsync(layer, mapView.Layers, dialog.ToNewLayer ? null : dialog.TargetLayer, dialog.Distances, dialog.Union, features);
            }
        }

        /// <summary>
        /// 坐标转换
        /// </summary>
        /// <returns></returns>
        private async Task CoordinateTransformateAsync()
        {
            CoordinateTransformationDialog dialog = new CoordinateTransformationDialog();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Source != dialog.Target)
            {
                await FeatureUtility.CoordinateTransformateAsync(editableLayer, features, dialog.Source, dialog.Target);
            }
        }

        /// <summary>
        /// 复制属性
        /// </summary>
        /// <returns></returns>
        private async Task CopyAttributeAsync()
        {
            var dialog = new CopyAttributesDialog(editableLayer);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ItemsOperationErrorCollection errors = null;

                switch (dialog.Type)
                {
                    case FieldAssignmentType.Field:
                        errors = await AttributeUtility.CopyAttributesAsync(editableLayer, features, dialog.SourceField, dialog.TargetField);
                        break;

                    case FieldAssignmentType.Const:
                        errors = await AttributeUtility.SetAttributesAsync(editableLayer, features, dialog.TargetField, dialog.Text, false);
                        break;

                    case FieldAssignmentType.Custom:
                        errors = await AttributeUtility.SetAttributesAsync(editableLayer, features, dialog.TargetField, dialog.Text, true);
                        break;

                    default:
                        break;
                }
                await ItemsOperaionErrorsDialog.TryShowErrorsAsync("部分属性复制失败", errors);
            }
        }

        /// <summary>
        /// 建立副本
        /// </summary>
        /// <returns></returns>
        private async Task CreateCopyAsync()
        {
            mapView.Selection.ClearSelection();
            var newFeatures = await FeatureUtility.CreateCopyAsync(editableLayer, features);
            mapView.Selection.Select(newFeatures);
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <returns></returns>
        private async Task DensifyAsync()
        {
            double? num = await CommonDialog.ShowDoubleInputDialogAsync("请输入最大间隔（米）");
            if (num.HasValue)
            {
                await FeatureUtility.DensifyAsync(editableLayer, features, num.Value);
                SnakeBar.Show($"加密完成");
            }
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="task"></param>
        /// <returns></returns>
        private async Task ExportBase(string filterDisplay, string filterExtension, Func<string, Task> task)
        {
            var dialog = new SaveFileDialog()
                .AddFilter(filterDisplay, filterExtension);
            dialog.FileName = layer.Name;
            string path = dialog.GetPath(mainWindow);
            if (path != null)
            {
                await task(path);
                IOUtility.ShowExportedSnackbarAndClickToOpenFolder(path, mainWindow);
            }
        }

        /// <summary>
        /// 获取编辑菜单
        /// </summary>
        /// <returns></returns>
        private List<(string header, string desc, Func<Task> action, bool visible)> GetEditMenus()
        {
            return new List<(string header, string desc, Func<Task> action, bool visible)>()
           {
                ("合并","将多个图形合并为一个具有多个部分的图形",
                UnionAsync,
                layer.GeometryType is GeometryType.Polygon or GeometryType.Polyline or GeometryType.Multipoint
                    && features.Length>1
                ),
                ("分离","将拥有多个部分的图形分离为单独的图形",
                SeparateAsync
                ,layer.GeometryType is GeometryType.Polygon or GeometryType.Polyline
                ),
                ("连接","将折线的端点互相连接",
                LinkAsync,
                layer.GeometryType==GeometryType.Polyline
                    && features.Length>1
                    && features.All(p=>(p.Geometry as Polyline).Parts.Count==1)
                ),
                ("反转","交换点的顺序",ReverseAsync,
                layer.GeometryType is GeometryType.Polyline or GeometryType.Polygon),
                ("加密","在每两个折点之间添加更多的点",DensifyAsync,
                layer.GeometryType is GeometryType.Polyline or GeometryType.Polygon),
                ("简化","删除部分折点，降低图形的复杂度",SimplifyAsync,
                layer.GeometryType is GeometryType.Polyline or GeometryType.Polygon),
                ("平滑","在适当的位置添加节点，使图形更平滑",SmoothAsync,
                layer.GeometryType is GeometryType.Polyline or GeometryType.Polygon or GeometryType.Multipoint),
                ("建立副本","在原位置创建拥有相同图形和属性的要素",CreateCopyAsync, true),
                ("字段赋值","批量为选中的图形赋予新的属性",CopyAttributeAsync, true),
                ("缓冲区","为选中的图形建立缓冲区",BufferAsync, true),
                ("坐标转换","转换图形的坐标系",CoordinateTransformateAsync, true),
                ("叠加分析","两个图层之间进行合并、相交、裁切等操作",LayerTopoAsync, true),
             };
        }

        /// <summary>
        /// 获取导出菜单
        /// </summary>
        /// <returns></returns>
        private List<(string header, string desc, Func<Task> action, bool visible)> GetExportMenus()
        {
            return new List<(string header, string desc, Func<Task> action, bool visible)>()
            {
                ("坐标表（*.csv）","将图形坐标点导出为CSV表格",
                ()=>ExportBase("CSV表格", "csv", async path => await Exporter.ExportCsvXYTableAsync(path, layer, [.. mapView.Selection.SelectedFeatures])),
                true),
                ("属性表（*.csv）","将属性导出为CSV表格",
                ()=>ExportBase("CSV表格", "csv", async path => await Exporter.ExportCsvAttributeTableAsync(path, layer, [.. mapView.Selection.SelectedFeatures])),
                true),
                ("GeoJSON（*.geojson）","将图形导出为GeoJSON",
                ()=>ExportBase("GeoJSON", "geojson", async path => await Exporter.ExportGeoJsonAsync(path, layer, mapView.Selection.SelectedFeatures)),
                true),
                ("CesiumGeoJSON（*.geojson）","将图形导出为带样式的GeoJSON",
                ()=> ExportBase("GeoJSON", "geojson", async path => await Exporter.ExportGeoJsonWithStylesAsync(path, layer, mapView.Selection.SelectedFeatures))
                , true),
                ("KML打包文件（*.kmz）","将图形导出为KML文件",
                ()=> ExportBase("KML打包文件", "kmz", async path => await Exporter.ExportKmlAsync(path, layer, mapView.Selection.SelectedFeatures))
                , true),
                ("Shapefile（*.shp）","将图形导出为Shapefile文件",
                ()=> ExportBase("Shapefile", "shp", async path => await Exporter.ExportShapefileAsync(path, layer, mapView.Selection.SelectedFeatures))
                , true),
            };
        }

        /// <summary>
        /// 获取菜单项
        /// </summary>
        /// <param name="getMessage"></param>
        /// <param name="menus"></param>
        /// <returns></returns>
        private IEnumerable<MenuItem> GetMenus(Func<string, string> getMessage, List<(string header, string desc, Func<Task> action, bool visible)> menus)
        {
            foreach (var (header, desc, action, visible) in menus)
            {
                if (visible)
                {
                    StackPanel content = new StackPanel();
                    content.Children.Add(new TextBlock()
                    {
                        Text = header,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 4, 0, 4)
                    });
                    content.Children.Add(new TextBlock()
                    {
                        Text = desc,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    MenuItem item = new MenuItem() { Header = content };
                    item.Click += async (p1, p2) =>
                    {
                        try
                        {
                            await mainWindow.DoAsync(action, getMessage(header));
                        }
                        catch (Exception ex)
                        {
                            App.Log.Error("执行菜单失败", ex);
                            await CommonDialog.ShowErrorDialogAsync(ex);
                        }
                    };
                    yield return item;
                }
            }
        }

        /// <summary>
        /// 坐标转换
        /// </summary>
        /// <returns></returns>
        private async Task LayerTopoAsync()
        {
            OverlayAnalysisDialog dialog = new OverlayAnalysisDialog(mapView.Layers, layer);

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.SelectedLayer != null)
            {
                await OverlayAnalysisUtility.OverlayAnalysisAsync(layer, mapView.Layers, features, dialog.SelectedLayer, dialog.Operation);
            }
        }

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        private async Task LinkAsync()
        {
            List<SelectDialogItem> typeList = new List<SelectDialogItem>();
            bool headToHead = false; //是否头对头
            bool reverse = false; //是否反向
            bool auto = false; //是否自动识别
            typeList.Add(new SelectDialogItem("自动", "根据选择的顺序自动判断需要连接的点", () => auto = true));
            if (mapView.Selection.SelectedFeatures.Count == 2)
            {
                typeList.Add(new SelectDialogItem("起点相连", "起始点与起始点相连接", () => headToHead = true));
                typeList.Add(new SelectDialogItem("终点相连", "终结点与终结点相连接", () => headToHead = reverse = true));
                typeList.Add(new SelectDialogItem("头尾相连", "第一个要素的终结点与第二个要素的起始点相连接", null));
                typeList.Add(new SelectDialogItem("头尾相连（反转）", "第一个要素的起始点与第二个要素的终结点相连接", () => reverse = true));
            }
            else
            {
                typeList.Add(new SelectDialogItem("头尾相连", "每一个要素的终结点与前一个要素的起始点相连接", null));
                typeList.Add(new SelectDialogItem("头尾相连（反转）", "每一个要素的起始点与前一个要素的终结点相连接", () => reverse = true));
            }

            int result = await CommonDialog.ShowSelectItemDialogAsync(
                "请选择连接类型", typeList, "查看图形起点和终点", async () =>
                {
                    await mapView.Overlay.ShowHeadAndTailOfFeatures(features);
                    SnakeBar snake = new SnakeBar(mainWindow)
                    {
                        ShowButton = true,
                        ButtonContent = "继续",
                        Duration = Duration.Forever
                    };
                    snake.ButtonClick += async (p1, p2) =>
                    {
                        mapView.Overlay.ClearHeadAndTail();
                        snake.Hide();
                        await LinkAsync();
                    };

                    snake.ShowMessage("正在显示图形起点和终点");
                });

            if (result < 0)
            {
                return;
            }
            Feature feature;
            if (auto)
            {
                feature = await FeatureUtility.AutoLinkAsync(editableLayer, features);
            }
            else
            {
                feature = await FeatureUtility.LinkAsync(editableLayer, features, headToHead, reverse);
            }
            mapView.Selection.Select(feature, true);
        }

        /// <summary>
        /// 反向
        /// </summary>
        /// <returns></returns>
        private async Task ReverseAsync()
        {
            await FeatureUtility.ReverseAsync(editableLayer, features);
            SnakeBar.Show($"反转完成");
        }

        /// <summary>
        /// 分离
        /// </summary>
        /// <returns></returns>
        private async Task SeparateAsync()
        {
            var result = await FeatureUtility.SeparateAsync(editableLayer, features);
            if (result == null || result.Count == 0)
            {
                await CommonDialog.ShowErrorDialogAsync("选中的图形不需要分离");
            }
            else
            {
                SnakeBar.Show($"已分离出{result.Count}个图形");
                mapView.Selection.ClearSelection();
            }
        }

        /// <summary>
        /// 简化
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task SimplifyAsync()
        {
            int i = await CommonDialog.ShowSelectItemDialogAsync("请选择简化方法", new SelectDialogItem[]
               {
                        new SelectDialogItem("间隔取点法","或每几个点保留一点"),
                        new SelectDialogItem("垂距法","中间隔一个点连接两个点，然后计算垂距或角度，在某一个值以内则可以删除中间间隔的点"),
                        new SelectDialogItem("最大偏离法","保证新的图形与旧图形之间的距离不超过一个值")
               });
            if (i >= 0)
            {
                double? num = i switch
                {
                    0 => await CommonDialog.ShowDoubleInputDialogAsync("请输入间隔几点取一点"),
                    1 => await CommonDialog.ShowDoubleInputDialogAsync("请输入最大垂距（米）"),
                    2 => await CommonDialog.ShowDoubleInputDialogAsync("请输入最大垂距（米）"),
                    _ => throw new NotImplementedException()
                };
                if (num.HasValue)
                {
                    var task = i switch
                    {
                        0 => FeatureUtility.IntervalTakePointsSimplifyAsync(editableLayer, features, num.Value),
                        1 => FeatureUtility.VerticalDistanceSimplifyAsync(editableLayer, features, num.Value),
                        2 => FeatureUtility.GeneralizeSimplifyAsync(editableLayer, features, num.Value),
                        _ => throw new NotImplementedException()
                    };
                    await task;
                }
            }
        }

        /// <summary>
        /// 平滑
        /// </summary>
        /// <returns></returns>
        private async Task SmoothAsync()
        {
            var dialog = new SmoothDialog();
            if (await dialog.ShowAsync(ContentDialogPlacement.InPlace) == ContentDialogResult.Primary)
            {
                var newFeatures = (await FeatureUtility.Smooth(editableLayer, features, dialog.PointsPerSegment, dialog.Level, dialog.DeleteOldFeature, dialog.MinSmoothAngle)).ToArray();
                mapView.Selection.ClearSelection();
                if (dialog.Simplify)
                {
                    await FeatureUtility.GeneralizeSimplifyAsync(editableLayer, newFeatures, dialog.MaxDeviation);
                }
            }
        }

        /// <summary>
        /// 合并
        /// </summary>
        /// <returns></returns>
        private async Task UnionAsync()
        {
            var result = await FeatureUtility.UnionAsync(editableLayer, features);
            mapView.Selection.Select(result, true);
            SnakeBar.Show($"合并完成");
        }
    }
}