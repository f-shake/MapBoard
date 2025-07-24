using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using FzLib.WPF.Dialog;
using MapBoard.UI.Dialog;
using MapBoard.Util;
using ModernWpf.FzExtension.CommonDialog;

using System.Windows;
using System.Windows.Controls;
using MapBoard.Mapping;
using ModernWpf.Controls;
using ListView = System.Windows.Controls.ListView;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.IO;
using System.IO;
using FzLib.WPF;

namespace MapBoard.UI.Menu
{
    public class LayerListPanelHelper
    {
        /// <summary>
        /// 关联的<see cref="ListView"/>
        /// </summary>
        private readonly ListView list;

        public LayerListPanelHelper(ListView list, MainWindow win, MainMapView mapView)
        {
            this.list = list;
            MainWindow = win;
            MapView = mapView;
        }

        /// <summary>
        /// 主窗口
        /// </summary>
        public MainWindow MainWindow { get; }

        /// <summary>
        /// 地图
        /// </summary>
        public MainMapView MapView { get; }

        /// <summary>
        /// 显示右键菜单
        /// </summary>
        public void ShowContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            IMapLayerInfo layer = MapView.Layers.Selected;
            MapLayerInfo[] layers = list.SelectedItems.Cast<MapLayerInfo>().ToArray();
            if (list.SelectedItems.Count == 1)//单选
            {
                //menu.Items.Add(new MenuItem()
                //{
                //    IsEnabled = false,
                //    Header = new TextBlock()
                //    {
                //        Text = MapLayerInfo.Types.GetDescription(layer.Type),
                //        FontSize = 14,
                //        FontWeight = FontWeights.Bold,
                //        Foreground = App.Current.FindResource("SystemControlForegroundBaseHighBrush") as System.Windows.Media.Brush,
                //        TextAlignment = TextAlignment.Center,
                //        Margin = new Thickness(0, 0, 0, 24)
                //    }
                //});
                if (!layer.IsLoaded)
                {
                    AddToMenu(menu, "删除", () => DeleteLayersAsync(layers));
                    AddToMenu(menu, "重新加载", () => ReloadLayerAsync(layer));
                }
                else
                {
                    if (layer != null && layer.NumberOfFeatures > 0)
                    {
                        AddToMenu(menu, "缩放到图层", () => ZoomToLayerAsync(layer));
                    }
                    AddToMenu(menu, "属性表", () => ShowAttributeTableAsync(layer));
                    AddToMenu(menu, "删除", () => DeleteLayersAsync(layers));

                    AddToMenu<IMapLayerInfo>(menu, "属性", layer, EditLayerAsync);

                    menu.Items.Add(new Separator());
                    AddToMenu(menu, "建立副本", () => CreateCopyAsync(layer));
                    AddToMenu(menu, "复制图形到", () => CopyFeaturesAsync(layer));
                    AddToMenu(menu, "复制样式到", () => CopyStylesAsync(layer));
                    AddToMenu(menu, "导出到新图层", () => ExportAsync(layer));

                    menu.Items.Add(new Separator());
                    AddToMenu(menu, "查询要素", () => QueryAsync(layer));

                    if (layer.CanEdit)
                    {
                        AddToMenu<IMapLayerInfo>(menu, "操作历史记录", layer, OpenHistoryDialog);
                        if (layer.NumberOfFeatures > 0)
                        {
                            MenuItem subMenu = new MenuItem() { Header = "地理分析（正在加载）" };
                            menu.Items.Add(subMenu);
                            //需要获取所有要素后，再显示菜单
                            layer.GetAllFeaturesAsync().ContinueWith(featuresTask =>
                            {
                                if (featuresTask.IsCompletedSuccessfully)
                                {
                                    var features = featuresTask.Result;
                                    MainWindow.Dispatcher.Invoke(() =>
                                    {
                                        subMenu.Header = "地理分析";
                                        var menuHelper = new FeatureLayerMenuHelper(MainWindow, MapView, layer, features);
                                        foreach (var item in menuHelper.GetEditMenus(header => $"正在进行{header}操作"))
                                        {
                                            subMenu.Items.Add(item);
                                        }
                                    });
                                }
                            });
                        }
                    }
                    AddToMenu<IMapLayerInfo>(menu, "筛选显示图形", layer, SetDefinitionExpression, !string.IsNullOrEmpty(layer.DefinitionExpression));
                    menu.Items.Add(new Separator());

                    if (layer is IMapLayerInfo e && layer.CanEdit)
                    {
                        var menuImport = new MenuItem() { Header = "导入" };
                        menu.Items.Add(menuImport);
                        if (layer.GeometryType == GeometryType.Polyline
                            || layer.GeometryType == GeometryType.Point
                            || layer.GeometryType == GeometryType.Multipoint)
                        {
                            AddToMenu(menuImport, "GPX轨迹文件",
                                () => IOUtility.GetImportFeaturePath(ImportLayerType.Gpx, MainWindow),
                                p => IOUtility.ImportFeatureAsync(MainWindow, p, e, MapView, ImportLayerType.Gpx),
                                "正在导入GPX轨迹文件");
                        }

                        AddToMenu(menuImport, "CSV文件",
                            () => IOUtility.GetImportFeaturePath(ImportLayerType.Csv, MainWindow),
                            p => IOUtility.ImportFeatureAsync(MainWindow, p, e, MapView, ImportLayerType.Csv),
                            "正在导入CSV文件");
                    }
                    var menuExport = new MenuItem() { Header = "导出" };
                    menu.Items.Add(menuExport);

                    AddToMenu(menuExport, "图层包",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.LayerPackge, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.LayerPackge),
                        "正在导出图层包");
                    AddToMenu(menuExport, "KML打包文件",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.KML, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.KML),
                        "正在导出KML打包文件");
                    AddToMenu(menuExport, "GeoJSON文件",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.GeoJSON, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.GeoJSON),
                        "正在导出GeoJSON文件");
                    AddToMenu(menuExport, "CesiumGeoJSON文件",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.GeoJSONWithStyle, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.GeoJSONWithStyle),
                        "正在导出带样式的GeoJSON文件");
                    AddToMenu(menuExport, "OpenLayers网络地图",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.OpenLayers, MainWindow),
                        p => ExportOpenLayersLayer(layer, p),
                    "正在导出OpenLayers网络地图");
                    AddToMenu(menuExport, "Shapefile",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.Shapefile, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.Shapefile),
                    "正在导出Shapefile");
                    AddToMenu(menuExport, "CSV属性表",
                        () => IOUtility.GetExportLayerPath(layer, ExportLayerType.Csv, MainWindow),
                        p => IOUtility.ExportLayerAsync(MainWindow, p, layer, MapView.Layers, ExportLayerType.Csv),
                    "正在导出CSV属性表");
                }
            }
            else//多选
            {
                if (layers.All(p => p.IsLoaded)
                    && layers.Select(p => p.GeometryType).Distinct().Count() == 1)
                {
                    AddToMenu(menu, "合并", () => LayerUtility.UnionAsync(layers, MapView.Layers));
                }
                AddToMenu(menu, "删除", () => DeleteLayersAsync(layers));
            }
            if (menu.Items.Count > 0)
            {
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// 如果<paramref name="layer"/>是类型<typeparamref name="T"/>，那么就新增一个菜单项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="menu"></param>
        /// <param name="header"></param>
        /// <param name="layer"></param>
        /// <param name="func"></param>
        /// <param name="isChecked"></param>
        private void AddToMenu<T>(ItemsControl menu, string header, IMapLayerInfo layer, Func<T, Task> func, bool? isChecked = null) where T : class
        {
            if (layer is T)
            {
                AddToMenu(menu, header, () => func(layer as T), isChecked);
            }
        }

        /// <summary>
        /// 新增菜单项
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="header"></param>
        /// <param name="func"></param>
        /// <param name="isChecked"></param>
        private void AddToMenu(ItemsControl menu, string header, Func<Task> func, bool? isChecked = null)
        {
            MenuItem item = new MenuItem() { Header = header };
            if (isChecked.HasValue)
            {
                item.IsCheckable = isChecked.Value;//没开的时候也没必要设置可选择了
                item.IsChecked = isChecked.Value;
            }
            item.Click += async (p1, p2) =>
            {
                try
                {
                    await MainWindow.DoAsync(func, "正在处理");
                }
                catch (Exception ex)
                {
                    App.Log.Error("任务执行失败", ex);
                    await CommonDialog.ShowErrorDialogAsync(ex);
                }
            };
            menu.Items.Add(item);
        }

        /// <summary>
        /// 新增子菜单项
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="header"></param>
        /// <param name="getPath"></param>
        /// <param name="func"></param>
        /// <param name="message"></param>
        private void AddToMenu(ItemsControl menu, string header, Func<string> getPath, Func<string, Task> func, string message)
        {
            MenuItem item = new MenuItem() { Header = header };
            item.Click += async (p1, p2) =>
            {
                try
                {
                    string path = getPath();
                    if (path != null)
                    {
                        await MainWindow.DoAsync(async () => await func(path), message);
                    }
                }
                catch (Exception ex)
                {
                    App.Log.Error("任务执行失败", ex);
                    await CommonDialog.ShowErrorDialogAsync(ex);
                }
            };
            menu.Items.Add(item);
        }

        /// <summary>
        /// 复制要素
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task CopyFeaturesAsync(IMapLayerInfo layer)
        {
            SelectLayerDialog dialog = new SelectLayerDialog(MapView.Layers,
                p => p.CanEdit && p.GeometryType == MapView.Layers.Selected.GeometryType
                , true);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await LayerUtility.CopyAllFeaturesAsync(layer, dialog.SelectedLayer as IMapLayerInfo);
            }
        }

        /// <summary>
        /// 复制样式
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task CopyStylesAsync(IMapLayerInfo layer)
        {
            SelectLayerDialog dialog = new SelectLayerDialog(MapView.Layers, p => true, true);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                LayerUtility.CopyStyles(layer, dialog.SelectedLayer);
                dialog.SelectedLayer.ApplyStyle();
            }
        }

        /// <summary>
        /// 建立副本
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task CreateCopyAsync(IMapLayerInfo layer)
        {
            var check = await CommonDialog.ShowCheckBoxDialogAsync("请选择副本类型",
                    new CheckDialogItem[]
                {
              new  CheckDialogItem("样式",null,false,true),
               new CheckDialogItem("字段",null){Tag=1 },
               new CheckDialogItem("所有图形",null){Tag=2}
                }, true);
            if (check != null)
            {
                bool includeFields = check.Any(p => 1.Equals(p.Tag));
                bool includeFeatures = check.Any(p => 2.Equals(p.Tag));
                await LayerUtility.CreateCopyAsync(layer, MapView.Layers, includeFeatures, includeFields);
            }
        }

        /// <summary>
        /// 删除图层
        /// </summary>
        /// <param name="layers"></param>
        /// <returns></returns>
        private async Task DeleteLayersAsync(IList<IMapLayerInfo> layers)
        {
            if (list.SelectedItems.Count == 0)
            {
                SnakeBar.ShowError("没有选择任何图层");
                return;
            }
            foreach (MapLayerInfo layer in layers)
            {
                await layer.DeleteLayerAsync(MapView.Layers);
            }
            MapView.Layers.Save();
        }

        /// <summary>
        /// 导出图层
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task ExportAsync(IMapLayerInfo layer)
        {
            ExportLayerDialog dialog = new ExportLayerDialog(MapView.Layers, layer, MapView.Map.OperationalLayers);
            await dialog.ShowAsync();
        }

        /// <summary>
        /// 导出为OpenLayers
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task ExportOpenLayersLayer(IMapLayerInfo layer, string path)
        {
            try
            {
                await new OpenLayers(path, Directory.GetFiles("res/openlayers"), Config.Instance.BaseLayers.ToArray(), new[] { layer })
                 .ExportAsync();
                IOUtility.ShowExportedSnackbarAndClickToOpenFolder(path, MainWindow);
            }
            catch (Exception ex)
            {
                App.Log.Error("导出失败", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "导出失败");
            }
        }

        /// <summary>
        /// 打开操作历史记录
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task OpenHistoryDialog(IMapLayerInfo layer)
        {
            await Task.Yield();
            var dialog = FeatureHistoryDialog.Get(MainWindow, layer, MapView);
            dialog.BringToFront();
        }

        /// <summary>
        /// 查询图层
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task QueryAsync(IMapLayerInfo layer)
        {
            if (layer.NumberOfFeatures == 0)
            {
                await CommonDialog.ShowErrorDialogAsync("该图层没有任何要素");
                return;
            }
            var dialog = new QueryFeaturesDialog(MainWindow, MapView, layer);
            dialog.BringToFront();
        }

        /// <summary>
        /// 重新加载图层
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task ReloadLayerAsync(IMapLayerInfo layer)
        {
            await MainWindow.DoAsync(async () =>
            {
                try
                {
                    await layer.ReloadAsync(MapView.Layers);
                }
                catch (Exception ex)
                {
                    App.Log.Error("加载图层失败", ex);
                    await CommonDialog.ShowErrorDialogAsync(ex, "加载失败");
                }
            }, "正在重新加载图层");
        }


        /// <summary>
        /// 设置显示内容，定义表达式
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private Task SetDefinitionExpression(IMapLayerInfo layer)
        {
            DefinitionExpressionDialog dialog = DefinitionExpressionDialog.Get(MainWindow, layer, MapView);
            dialog.Show();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置Shapefile图层部分属性
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task EditLayerAsync(IMapLayerInfo layer)
        {
            MapView.Selection.ClearSelection();
            await CreateLayerDialog.OpenEditDialog(MapView.Layers, MapView.Map.OperationalLayers, layer);
        }

        /// <summary>
        /// 显示属性表
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task ShowAttributeTableAsync(IMapLayerInfo layer)
        {
            var dialog = AttributeTableDialog.Get(MainWindow, layer, MapView);
            try
            {
                await MainWindow.DoAsync(dialog.LoadAsync, "正在加载属性表");
            }
            catch (Exception ex)
            {
                App.Log.Error("加载属性失败", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "加载属性失败");
                return;
            }
            dialog.BringToFront();
        }

        /// <summary>
        /// 缩放到图层
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private async Task ZoomToLayerAsync(IMapLayerInfo layer)
        {
            try
            {
                await MapView.ZoomToGeometryAsync(await layer.QueryExtentAsync(new QueryParameters()));
            }
            catch (Exception ex)
            {
                App.Log.Error("缩放失败，可能是不构成有面积的图形", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "缩放失败，可能是不构成有面积的图形");
            }
        }
    }
}