using FzLib;
using FzLib.WPF.Dialog;
using MapBoard.Model;
using MapBoard.Mapping;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MapBoard.IO;
using MapBoard.Mapping.Model;
using ModernWpf.Controls;
using FzLib.DataStorage.Serialization;
using MapBoard.Util;
using System.ComponentModel;
using FzLib.Program;
using FzLib.IO;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;
using Microsoft.Win32;
using CommonDialog = ModernWpf.FzExtension.CommonDialog.CommonDialog;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 设置对话框
    /// </summary>
    public partial class SettingDialog : DialogWindowBase
    {
        /// <summary>
        /// GPX格式的ID
        /// </summary>
        private const string gpxID = "MapBoard_gpx";

        /// <summary>
        /// 地图画板地图包格式的ID
        /// </summary>
        private const string MbmpkgID = "MapBoard_mbmpkg";

        /// <summary>
        /// 是否可以关闭窗口
        /// </summary>
        private bool canClose = true;

        public SettingDialog(Window owner, MapLayerCollection layers, int tabIndex = 0) : base(owner)
        {
            FormatMbmpkgAssociated = FileFormatAssociationUtility.IsAssociated("mbmpkg", MbmpkgID);
            FormatGpxAssociated = FileFormatAssociationUtility.IsAssociated("gpx", gpxID);

            InitializeComponent();
            cbbCoords.ItemsSource = Enum.GetValues(typeof(CoordinateSystem)).Cast<CoordinateSystem>();
            Layers = layers;
            tab.SelectedIndex = tabIndex;

            var helper = new DesktopBridge.Helpers();
            if (helper.IsRunningAsUwp())
            {
                grpFilePath.Visibility = Visibility.Collapsed;
            }
        }


        /// <summary>
        /// 底图类型
        /// </summary>
        public IEnumerable<BaseLayerType> BaseLayerTypes { get; } = Enum.GetValues(typeof(BaseLayerType)).Cast<BaseLayerType>().ToList();

        /// <summary>
        /// 配置
        /// </summary>
        public Config Config => Config.Instance;

        /// <summary>
        /// 当前备份数量
        /// </summary>
        public int CurrentBackupCount => Directory.EnumerateFiles(FolderPaths.BackupPath, "*.mbmpkg").Count();

        /// <summary>
        /// 是否设置了GPX格式关联
        /// </summary>
        public bool FormatGpxAssociated { get; set; }

        /// <summary>
        /// 是否设置了地图画板地图包格式关联
        /// </summary>
        public bool FormatMbmpkgAssociated { get; set; }

        /// <summary>
        /// 所有图层
        /// </summary>
        public MapLayerCollection Layers { get; }

        /// <summary>
        /// 主题
        /// </summary>
        public int Theme
        {
            get => Config.Theme switch
            {
                0 => 0,
                1 => 1,
                -1 => 2,
                _ => throw new ArgumentOutOfRangeException()
            };
            set => Config.Theme = value switch
            {
                0 => 0,
                1 => 1,
                2 => -1,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// 点击新增底图图层按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void AddButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            BaseLayerInfo layerInfo = new BaseLayerInfo(BaseLayerType.WebTiledLayer, "");
            baseLayers.BaseLayers.Add(layerInfo);
            SelectAndScroll(layerInfo);
        }

        /// <summary>
        /// 点击新增文件按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
                .AddFilter("JPEG图片", "jpg,jpeg")
                .AddFilter("PNG图片", "png")
                .AddFilter("BMP图片", "bmp")
                .AddFilter("TIFF图片", "tif,tiff")
                .AddFilter("Shapefile矢量图", "shp")
                .AddFilter("TilePackage切片包", "tpk")
                .AddAllFilesFilter();

            string path = dialog.GetPath(this);
            if (path != null)
            {
                var layerInfo = Path.GetExtension(path) switch
                {
                    ".shp" => new BaseLayerInfo(BaseLayerType.ShapefileLayer, path),
                    ".tpk" => new BaseLayerInfo(BaseLayerType.TpkLayer, path),
                    _ => new BaseLayerInfo(BaseLayerType.RasterLayer, path),
                };
                baseLayers.BaseLayers.Add(layerInfo);
                SelectAndScroll(layerInfo);
            }
        }

        /// <summary>
        /// 点击新增WMS图层按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddWmsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddWmsLayerDialog(BaseLayerType.WmsLayer);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                BaseLayerInfo layerInfo = new BaseLayerInfo(BaseLayerType.WmsLayer, $"{dialog.Url}|{dialog.WmsLayerName}");
                baseLayers.BaseLayers.Add(layerInfo);
                SelectAndScroll(layerInfo);
            }
        }

        /// <summary>
        /// 点击新增WMTS图层按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddWmtsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddWmsLayerDialog(BaseLayerType.WmtsLayer);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                BaseLayerInfo layerInfo = new BaseLayerInfo(BaseLayerType.WmtsLayer, $"{dialog.Url}|{dialog.WmsLayerName}");
                baseLayers.BaseLayers.Add(layerInfo);
                SelectAndScroll(layerInfo);
            }
        }

        /// <summary>
        /// 单击网络服务API的重启按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ApiRestartButton_Click(object sender, RoutedEventArgs e)
        {
            RestartMainWindow();
        }

        /// <summary>
        /// 点击备份按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            canClose = false;
            (sender as Button).IsEnabled = false;
            try
            {
                await Package.BackupAsync(Layers, Config.Instance.MaxBackupCount);
            }
            catch (Exception ex)
            {
                App.Log.Error("备份失败", ex);
                CommonDialog.ShowErrorDialogAsync(ex, "备份失败");
            }
            this.Notify(nameof(CurrentBackupCount));

            (sender as Button).IsEnabled = true;
            canClose = true;
        }



        /// <summary>
        /// 单击删除底图缓存按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteAllBasemapCachesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                (sender as Button).IsEnabled = false;
                await TileCacheDbContext.ClearCacheAsync();
                await CommonDialog.ShowOkDialogAsync("清除缓存", "已清除缓存");
            }
            catch (Exception ex)
            {
                await CommonDialog.ShowErrorDialogAsync(ex, "清除缓存失败");
                App.Log.Error("清除缓存", ex);
            }
            finally
            {
                (sender as Button).IsEnabled = true;
            }
        }

        /// <summary>
        /// 点击删除底图按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (baseLayers.SelectedItem != null)
            {
                foreach (var layer in baseLayers.SelectedItems.Cast<BaseLayerInfo>().ToList())
                {
                    baseLayers.BaseLayers.Remove(layer);
                }
            }
        }

        /// <summary>
        /// 对话框窗口关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DialogWindowBase_Closing(object sender, CancelEventArgs e)
        {
            if (!canClose)
            {
                e.Cancel = true;
                return;
            }
            Config.Instance.Save();
        }

        /// <summary>
        /// 点击导出设置按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.AddFilter("MapBoard配置文件", "mbconfig");
            dialog.AddExtension = true;
            dialog.FileName = "地图画板配置";
            string path = dialog.GetPath(this);
            if (path != null)
            {
                Config.Save(path);
            }
        }

        /// <summary>
        /// 点击关联格式选择框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="InvalidEnumArgumentException"></exception>
        private void FileAssociateCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = (sender as CheckBox).IsChecked == true;
            if (isChecked)
            {
                switch ((sender as CheckBox).Tag as string)
                {
                    case "mbmpkg":
                        FileFormatAssociationUtility.SetAssociation("mbmpkg", MbmpkgID, "地图画板地图包", Path.Combine(FzLib.Program.App.ProgramDirectoryPath, "res", "icon.ico"));
                        break;

                    case "gpx":
                        FileFormatAssociationUtility.SetAssociation("gpx", gpxID, "GPS轨迹文件", Path.Combine(FzLib.Program.App.ProgramDirectoryPath, "res", "icon.ico"));
                        break;

                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
            else
            {
                switch ((sender as CheckBox).Tag as string)
                {
                    case "mbmpkg":
                        FileFormatAssociationUtility.DeleteAssociation("mbmpkg", MbmpkgID);
                        break;

                    case "gpx":
                        FileFormatAssociationUtility.DeleteAssociation("gpx", gpxID);
                        break;

                    default:
                        throw new InvalidEnumArgumentException();
                }
            }
        }

        /// <summary>
        /// 点击导入设置按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.AddFilter("MapBoard配置文件", "mbconfig");
            dialog.FileName = "地图画板配置";
            string path = dialog.GetPath(this);
            if (path != null)
            {
                try
                {
                    JsonConvert.PopulateObject(File.ReadAllText(path), Config.Instance);
                    await CommonDialog.ShowOkDialogAsync("导入成功","导入成功，部分配置重启应用后应用");
                }
                catch (Exception ex)
                {
                    App.Log.Error("加载配置文件失败", ex);
                    await CommonDialog.ShowErrorDialogAsync(ex, "加载配置文件失败");
                }
            }
        }

        /// <summary>
        /// 点击底图确定按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.BaseLayers = baseLayers.BaseLayers.ToList();

            RestartMainWindow();
        }

        /// <summary>
        /// 点击打开备份目录按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenBackupFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await IOUtility.TryOpenInShellAsync(FolderPaths.BackupPath);
        }

        /// <summary>
        /// 点击数据目录按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RbtnDataPath_Click(object sender, RoutedEventArgs e)
        {
            string appPath = FzLib.Program.App.ProgramDirectoryPath;
            try
            {
                if (File.Exists(Path.Combine(appPath, FolderPaths.ConfigHere)))
                {
                    File.Delete(Path.Combine(appPath, FolderPaths.ConfigHere));
                }
                if (File.Exists(Path.Combine(appPath, FolderPaths.ConfigUp)))
                {
                    File.Delete(Path.Combine(appPath, FolderPaths.ConfigUp));
                }
                if (rbtnHere.IsChecked.Value)
                {
                    await File.WriteAllTextAsync(Path.Combine(appPath, FolderPaths.ConfigHere), "");
                }
                else if (rbtnUp.IsChecked.Value)
                {
                    await File.WriteAllTextAsync(Path.Combine(appPath, FolderPaths.ConfigUp), "");
                }
                await CommonDialog.ShowOkDialogAsync("修改数据位置", "将在重启后生效");
            }
            catch (Exception ex)
            {
                App.Log.Error("修改数据位置失败", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "修改数据位置失败");
            }
        }

        /// <summary>
        /// 重启主窗口
        /// </summary>
        private void RestartMainWindow()
        {
            Config.Instance.Save();
            App.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            (Owner as MainWindow).Close(true);
            foreach (Window window in App.Current.Windows.Cast<Window>().ToList())
            {
                window.Close();
            }
            App.Current.MainWindow = new MainWindow();
            App.Current.MainWindow.Show();
            App.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            Close();
        }

        /// <summary>
        /// 选择并定位
        /// </summary>
        /// <param name="item"></param>
        private void SelectAndScroll(BaseLayerInfo item)
        {
            baseLayers.SelectedItem = item;
            baseLayers.ScrollIntoView(item);
        }

        /// <summary>
        /// 点击水印选择框按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WatermarkCheckBox_Click(object sender, RoutedEventArgs e)
        {
            foreach (var map in MainMapView.Instances)
            {
                map.SetHideWatermark();
            }
            foreach (var map in GpxMapView.Instances)
            {
                map.SetHideWatermark();
            }
            foreach (var map in TileDownloaderMapView.Instances)
            {
                map.SetHideWatermark();
            }
        }

        /// <summary>
        /// 窗口启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string appPath = FzLib.Program.App.ProgramDirectoryPath;
            if (File.Exists(Path.Combine(appPath, FolderPaths.ConfigHere)))
            {
                rbtnHere.IsChecked = true;
            }
            else if (File.Exists(Path.Combine(appPath, FolderPaths.ConfigUp)))
            {
                rbtnUp.IsChecked = true;
            }
            else
            {
                rbtnAppData.IsChecked = true;
            }
            try
            {
                var defaultBasemapLayers = GeoViewHelper.GetDefaultBaseLayers();
                if (defaultBasemapLayers.Any())
                {
                    var menu = btnAddBasemapLayer.Flyout as MenuFlyout;
                    menu.Items.Add(new Separator());
                    foreach (var layer in defaultBasemapLayers)
                    {
                        var menuItem = new MenuItem()
                        {
                            Header = layer.Name,
                        };
                        menuItem.Click += (s, e) =>
                        {
                            baseLayers.BaseLayers.Add(layer);
                        };
                        menu.Items.Add(menuItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Activate();
                await CommonDialog.ShowErrorDialogAsync(ex, "获取默认底图失败");
            }
        }

        private async void ImportCachesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.AddFilter("SQLite数据库", "db");
            string path = dialog.GetPath(this);
            if (path == null)
            {
                return;
            }
            canClose = false;
            IsEnabled = false;

            try
            {
                using var db1 = new TileCacheDbContext();
                using var db2 = new TileCacheDbContext(path);
                await Task.Run(() =>
                {
                    var db1Tiles = db1.Tiles.Select(p => p.TileUrl).ToHashSet();
                    int count = db2.Tiles.Count();
                    int index = 0;
                    foreach (var tile in db2.Tiles)
                    {
                        index++;
                        CacheImportProgress = 1.0 * index / count;
                        if (!db1Tiles.Contains(tile.TileUrl))
                        {
                            tile.Id = 0;
                            db1.Tiles.Add(tile);
                        }
                        if (index % 100 == 0)
                        {
                            db1.SaveChanges();
                        }
                    }
                    db1.SaveChanges();
                });
            }
            catch (Exception ex)
            {
                IsEnabled = true;
                Activate();
                await CommonDialog.ShowErrorDialogAsync(ex, "合并失败");
            }
            finally
            {
                canClose = true;
                IsEnabled = true;
            }
        }

        public double CacheImportProgress { get; set; } = 0;
    }
}