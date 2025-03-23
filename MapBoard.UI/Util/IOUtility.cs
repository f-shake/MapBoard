using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Ogc;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using FzLib.WPF.Dialog;
using MapBoard.IO;
using MapBoard.Model;
using MapBoard.Mapping;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ModernWpf.FzExtension;
using MapBoard.Mapping.Model;
using MapBoard.UI.Dialog;
using System.Drawing.Imaging;
using FzLib.WPF;
using FzLib;
using MapBoard.UI.GpxToolbox;
using Microsoft.Win32;
using CommonDialog = ModernWpf.FzExtension.CommonDialog.CommonDialog;
using Esri.ArcGISRuntime.Mapping;
using MobileMapPackage = MapBoard.IO.MobileMapPackage;
using FluentFTP;

namespace MapBoard.Util
{
    public static class IOUtility
    {
        /// <summary>
        /// 拖放文件到窗口
        /// </summary>
        /// <param name="files"></param>
        /// <param name="layers"></param>
        /// <returns></returns>
        public static async Task DropFilesAsync(string[] files, MapLayerCollection layers)
        {
            try
            {
                if (files.Count(p => p.EndsWith(".gpx")) == files.Length)
                {
                    await ImportGpxAsync(files, layers.Selected, layers);
                }
                else if (files.Count(p => p.EndsWith(".mbmpkg")) == files.Length && files.Length == 1)
                {
                    await Package.ImportMapAsync(files[0], layers);
                }
                else if (files.Count(p => p.EndsWith(".mblpkg")) == files.Length)
                {
                    if (await CommonDialog.ShowYesNoDialogAsync("是否导入图层？") == true)
                    {
                        foreach (var file in files)
                        {
                            await Package.ImportLayerAsync(file, layers);
                        }
                    }
                }
                else if (files.Count(p => p.EndsWith(".csv")) == files.Length && layers.Selected is IMapLayerInfo w2)
                {
                    if (await CommonDialog.ShowYesNoDialogAsync("是否导入CSV文件？") == true)
                    {
                        foreach (var file in files)
                        {
                            await Csv.ImportAsync(file, w2);
                        }
                    }
                }
                else
                {
                    SnakeBar.ShowError("不支持的文件格式，文件数量过多，或文件集合的类型不都一样");
                }
            }
            catch (Exception ex)
            {
                ShowException(ex, "导入失败");
            }
        }

        /// <summary>
        /// 拖放目录到窗口
        /// </summary>
        /// <param name="folders"></param>
        /// <param name="layers"></param>
        /// <returns></returns>
        public static async Task DropFoldersAsync(string[] folders, MapLayerCollection layers)
        {
            int index = await CommonDialog.ShowSelectItemDialogAsync("请选择目录中的文件类型", new SelectDialogItem[]
            {
                new SelectDialogItem("照片位置","根据照片EXIF信息的经纬度，生成点图层"),
                new SelectDialogItem("GPS轨迹文件","作为GPX文件导入"),
            });
            if (index >= 0)
            {
                try
                {
                    List<string> files = null;
                    switch (index)
                    {
                        case 0:
                            string[] extensions = { ".jpg", ".jpeg", ".heif", ".heic", ".dng" };
                            files = await EnumerateFilesAsync(folders, extensions);
                            await Photo.ImportImageLocation(files, layers);

                            break;
                        case 1:
                            files = await EnumerateFilesAsync(folders, new string[] { ".gpx" });
                            await ImportGpxAsync(files, layers.Selected, layers);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ShowException(ex, "导入失败");
                }
            }
        }

        /// <summary>
        /// 导出 图层
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="path"></param>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static async Task ExportLayerAsync(Window owner, string path, IMapLayerInfo layer, MapLayerCollection layers, ExportLayerType type)
        {
            Debug.Assert(path != null);
            try
            {
                switch (type)
                {
                    case ExportLayerType.LayerPackge:
                        await Package.ExportLayerAsync(path, layer);
                        break;

                    case ExportLayerType.KML:
                        await Kml.ExportAsync(path, layer);
                        break;

                    case ExportLayerType.GeoJSON:
                        await GeoJson.ExportAsync(path, layer);
                        break;

                    case ExportLayerType.GeoJSONWithStyle:
                        await GeoJson.ExportWithStyleAsync(path, layer);
                        break;

                    case ExportLayerType.Shapefile:
                        await Shapefile.ExportToShapefile(path, layer);
                        break;

                    default:
                        break;
                }
                ShowExportedSnackbarAndClickToOpenFolder(path, Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                ShowException(ex, "导出失败");
            }
        }

        /// <summary>
        /// 导出地图
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="path"></param>
        /// <param name="mapView"></param>
        /// <param name="layers"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static async Task ExportMapAsync(Window owner, string path, MapView mapView, MapLayerCollection layers, ExportMapType type)
        {
            Debug.Assert(path != null);
            try
            {
                switch (type)
                {
                    case ExportMapType.MapPackage:
                        await Package.ExportMapAsync(path, layers, true);
                        break;

                    case ExportMapType.MapPackageRebuild:
                        await Package.ExportMapAsync(path, layers, false);
                        break;


                    case ExportMapType.KML:
                        await Kml.ExportAsync(path, layers.Cast<MapLayerInfo>());
                        break;

                    case ExportMapType.Screenshot:
                        await mapView.ExportImageAsync(path, ImageFormat.Png, GeoViewHelper.GetWatermarkThickness());

                        break;

                    default:
                        break;
                }

                ShowExportedSnackbarAndClickToOpenFolder(path, Application.Current.MainWindow);
            }
            catch (Exception ex)
            {
                ShowException(ex, "导出失败");
            }
        }

        /// <summary>
        /// 获取导出图层的路径
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="type"></param>
        /// <param name="parentWindow"></param>
        /// <returns></returns>
        public static string GetExportLayerPath(ILayerInfo layer, ExportLayerType type, Window parentWindow)
        {
            if (type is ExportLayerType.OpenLayers)
            {
                OpenFolderDialog folderDialog = new OpenFolderDialog();
                return folderDialog.GetPath(parentWindow);
            }
            else
            {
                SaveFileDialog dialog = new SaveFileDialog()
                        .AddFilterIf(type == ExportLayerType.LayerPackge, "地图画板图层包", "mblpkg")
                        //.AddFilterIf(type == ExportLayerType.LayerPackgeRebuild, "图层包", "mblpkg")
                        //.AddFilterIf(type == ExportLayerType.GISToolBoxZip, "GIS工具箱图层包", "zip")
                        .AddFilterIf(type == ExportLayerType.KML, "KML打包文件", "kmz")
                        .AddFilterIf(type == ExportLayerType.GeoJSON, "GeoJSON文件", "geojson")
                        .AddFilterIf(type == ExportLayerType.GeoJSONWithStyle, "带样式GeoJSON文件", "geojson")
                        .AddFilterIf(type == ExportLayerType.Shapefile, "Shapefile", "shp");
                dialog.FileName = $"地图画板图层 - {layer.Name}";
                return dialog.GetPath(parentWindow);
            }
        }

        /// <summary>
        /// 获取导出地图的路径
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parentWindow"></param>
        /// <returns></returns>
        public static string GetExportMapPath(ExportMapType type, Window parentWindow)
        {
            if (type is ExportMapType.OpenLayers)
            {
                var folderDialog = new OpenFolderDialog();
                return folderDialog.GetPath(parentWindow);
            }
            else
            {
                SaveFileDialog dialog = new SaveFileDialog()
                .AddFilterIf(type == ExportMapType.MapPackage, "地图画板地图包", "mbmpkg")
                .AddFilterIf(type == ExportMapType.MapPackageRebuild, "地图画板地图包", "mbmpkg")
                //.AddFilterIf(type == ExportMapType.GISToolBoxZip, "GIS工具箱图层包", "zip")
                .AddFilterIf(type == ExportMapType.KML, "KML打包文件", "kmz")
                .AddFilterIf(type == ExportMapType.Screenshot, "截图", "png");
                dialog.FileName = "地图画板 - " + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                return dialog.GetPath(parentWindow);
            }
        }

        /// <summary>
        /// 获取导入要素的路径
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parentWindow"></param>
        /// <returns></returns>
        public static string GetImportFeaturePath(ImportLayerType type, Window parentWindow)
        {
            OpenFileDialog dialog = new OpenFileDialog()
                .AddFilterIf(type == ImportLayerType.Gpx, "GPS轨迹文件", "gpx")
                .AddFilterIf(type == ImportLayerType.Csv, "CSV表格", "csv");
            return dialog.GetPath(parentWindow);
        }

        /// <summary>
        /// 获取导入地图的路径
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parentWindow"></param>
        /// <returns></returns>
        public static string GetImportMapPath(ImportMapType type, Window parentWindow)
        {
            OpenFileDialog dialog = new OpenFileDialog()
                .AddFilterIf(type == ImportMapType.MapPackageOverwrite, "地图画板地图包", "mbmpkg")
                //.AddFilterIf(type == ImportMapType.MapPackgeAppend, "地图画板地图包", "mbmpkg")
                .AddFilterIf(type == ImportMapType.LayerPackge, "mblpkg地图画板图层包", "mblpkg")
                .AddFilterIf(type == ImportMapType.Gpx, "GPS轨迹文件", "gpx")
                .AddFilterIf(type == ImportMapType.Shapefile, "Shapefile", "shp")
                .AddFilterIf(type == ImportMapType.CSV, "CSV表格", "csv")
                .AddFilterIf(type == ImportMapType.KML, "KML地理标记", "kml", "kmz")
                .AddFilterIf(type == ImportMapType.Mmpk, "移动地图包", "mmpk");
            return dialog.GetPath(parentWindow);
        }

        /// <summary>
        /// 在MSIX下运行时，程序数据目录的位置被重定向。在程序内访问时不需要修改，但通过外部程序调用时，需要获取真实的路径。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetRealPath(string path)
        {
            path = Path.GetFullPath(path);
            string oldPath = path;
            if (File.Exists(path) || Directory.Exists(path))
            {
                return path;
            }
            var helper = new DesktopBridge.Helpers();
            if (helper.IsRunningAsUwp())
            {
                string virtualAppData = Windows.Storage.ApplicationData.Current.LocalCacheFolder.Path;
                string local = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).Parent.FullName;
                if (path.Contains(local))
                {
                    path = path.Replace(local, virtualAppData);
                }
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException(path, new FileNotFoundException(oldPath));
            }
            return path;
        }

        /// <summary>
        /// 导入要素
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="path"></param>
        /// <param name="layer"></param>
        /// <param name="mapView"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static async Task ImportFeatureAsync(Window owner, string path, IMapLayerInfo layer, MainMapView mapView, ImportLayerType type)
        {
            Debug.Assert(path != null);

            try
            {
                IReadOnlyList<Feature> features = null;
                switch (type)
                {
                    case ImportLayerType.Gpx:
                        features = await Gps.ImportToLayerAsync(path, layer, Config.Instance.BasemapCoordinateSystem);
                        break;

                    case ImportLayerType.Csv:
                        features = await Csv.ImportAsync(path, layer);
                        break;

                    default:
                        break;
                }
                SnakeBar snake = new SnakeBar(owner);
                snake.ShowButton = true;
                snake.ButtonContent = "查看";
                snake.ButtonClick += async (p1, p2) =>
                {
                    var geom = features.Select(p => p.Geometry).CombineExtents();
                    await mapView.ZoomToGeometryAsync(geom);
                };
                mapView.Layers.Save();
                snake.ShowMessage("导入成功");
            }
            catch (Exception ex)
            {
                ShowException(ex, "导入失败");
            }
        }

        /// <summary>
        /// 导入地图
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="path"></param>
        /// <param name="layers"></param>
        /// <param name="type"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task ImportMapAsync(Window owner, string path, MapLayerCollection layers, ImportMapType type, ProgressRingOverlayArgs args)
        {
            Debug.Assert(path != null);
            try
            {
                switch (type)
                {
                    case ImportMapType.MapPackageOverwrite:
                        args.SetMessage("正在备份当前地图");
                        if (Config.Instance.BackupWhenReplace)
                        {
                            try
                            {
                                await Package.BackupAsync(layers, Config.Instance.MaxBackupCount);
                            }
                            catch (Exception ex)
                            {
                                App.Log.Error("备份失败", ex);
                                SnakeBar.ShowError("备份失败");
                            }
                        }
                        args.SetMessage("正在导入新的地图");
                        await Package.ImportMapAsync(path, layers);
                        break;

                    //case ImportMapType.MapPackgeAppend:
                    //    await Package.ImportMapAsync(path, layers, false);
                    //    break;

                    case ImportMapType.LayerPackge:
                        await Package.ImportLayerAsync(path, layers);
                        break;

                    case ImportMapType.Gpx:
                        await ImportGpxAsync(new[] { path }, layers.Selected, layers);
                        break;

                    case ImportMapType.Shapefile:
                        await Shapefile.ImportAsync(path, layers);
                        break;

                    case ImportMapType.CSV:
                        var table = await Csv.ImportToDataTableAsync(path);
                        await new ImportTableDialog(layers, table, Path.GetFileNameWithoutExtension(path))
                            .ShowAsync();
                        break;

                    case ImportMapType.KML:
                        await Kml.ImportAsync(path, layers);
                        break;

                    case ImportMapType.Mmpk:
                        await MobileMapPackage.ImportAsync(path, layers);
                        break;

                    default:
                        break;
                }
                layers.Save();
                SnakeBar.Show(owner, "导入成功");
            }
            catch (Exception ex)
            {
                ShowException(ex, "导入失败");
            }
        }

        /// <summary>
        /// 显示导出完成SnackBar
        /// </summary>
        /// <param name="path"></param>
        /// <param name="owner"></param>
        public static void ShowExportedSnackbarAndClickToOpenFolder(string path, Window owner)
        {
            SnakeBar snake = new SnakeBar(owner)
            {
                ShowButton = true,
                ButtonContent = "查看"
            };
            snake.ButtonClick += async (p1, p2) => await TryOpenFolderAsync(path);

            snake.ShowMessage("导出成功");
        }

        /// <summary>
        /// 若输入为目录，直接打开；若输入为文件，则打开目录并选中文件。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task TryOpenFolderAsync(string path)
        {
            try
            {
                var p = '"' + GetRealPath(path) + '"';
                if (Directory.Exists(path))
                {
                    new Process()
                    {
                        StartInfo = new ProcessStartInfo("explorer.exe", p)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
                if (File.Exists(path))
                {
                    new Process()
                    {
                        StartInfo = new ProcessStartInfo("explorer.exe", $"/select,{p}")
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
            }
            catch (Exception ex)
            {
                App.Log.Error("打开文件夹失败", ex);
                await CommonDialog.ShowErrorDialogAsync("文件或目录不存在", ex.Message);
            }
        }

        /// <summary>
        /// 使用Shell打开文件，文件夹或Url等。
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task TryOpenInShellAsync(string path)
        {
            try
            {
                new Process()
                {
                    StartInfo = new ProcessStartInfo(GetRealPath(path))
                    {
                        UseShellExecute = true
                    }
                }.Start();
            }
            catch (Exception ex)
            {
                App.Log.Error("打开失败", ex);
                await CommonDialog.ShowErrorDialogAsync(ex, "打开失败");
            }
        }

        public static async Task SaveToFtpAsync(string ip, MapLayerCollection layers, Action<string> newMessage = null)
        {
            newMessage?.Invoke("正在准备文件");
            string tempPath = $"{Path.GetTempFileName()}.mbmpkg";
            await Package.ExportMapAsync(tempPath, layers, true);

            ip = ip.Trim();
            if (ip.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                ip = ip["ftp://".Length..];
            }
            ip = ip.Trim('/').Trim('\\').Replace("：", ":");
            int port = 21;
            if (ip.Contains(':'))
            {
                var parts = ip.Split(':');
                ip = parts[0];
                if (int.TryParse(parts[1], out port))
                {
                    if (port is < 0 or > ushort.MaxValue)
                    {
                        throw new ArgumentException("端口号超出范围", nameof(ip));
                    }
                }
                else
                {
                    throw new ArgumentException("无法识别端口号", nameof(ip));
                }
            }
            await using var ftp = new AsyncFtpClient(ip, "anonymous", "anonymous@domain.com" ,port);
            newMessage?.Invoke("正在连接FTP");
            await ftp.Connect();
            newMessage?.Invoke("正在上传到FTP");
            await ftp.UploadFile(tempPath, Path.GetFileName($"地图画板 - {DateTime.Now:yyyyMMdd-HHmmss}.mbmpkg"));
            await ftp.Disconnect();
        }

        /// <summary>
        /// 枚举指定目录下的文件
        /// </summary>
        /// <param name="folders"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        private static async Task<List<string>> EnumerateFilesAsync(IEnumerable<string> folders, IEnumerable<string> extensions)
        {
            List<string> files = new List<string>();
            var exs = extensions
                .Select(p => p.StartsWith(".") ? p : $".{p}")
                .Select(p => p.ToLower())
                .ToList();
            await Task.Run(() =>
            {
                foreach (var folder in folders)
                {
                    if (File.Exists(folder)
                    && exs.Contains(Path.GetExtension(folder).ToLower()))
                    {
                        files.Add(folder);
                    }
                    else if (Directory.Exists(folder))
                    {
                        files.AddRange(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                            .Where(p => exs.Contains(Path.GetExtension(p).ToLower())));
                    }
                }
            });
            return files;
        }

        /// <summary>
        /// 导入GPX
        /// </summary>
        /// <param name="files"></param>
        /// <param name="layer"></param>
        /// <param name="layers"></param>
        /// <returns></returns>
        private static async Task ImportGpxAsync(IEnumerable<string> files, IMapLayerInfo layer, MapLayerCollection layers)
        {
            List<SelectDialogItem> items = new List<SelectDialogItem>()
                {
                       new SelectDialogItem("使用GPX工具箱打开", "使用GPX工具箱打开该轨迹"),
                        new SelectDialogItem("导入到新图层（线）","每一个文件将会生成一条线"),
                        new SelectDialogItem("导入到新图层（点）","生成所有文件的轨迹点"),
                };
            if (layer != null
                && layer is IMapLayerInfo
                && layer.GeometryType is GeometryType.Point or GeometryType.Polyline)
            {
                items.Add(new SelectDialogItem("导入到当前图层", "将轨迹导入到当前图层"));
            }
            int index = await CommonDialog.ShowSelectItemDialogAsync($"选择打开GPX文件的方式，共{files.Count()}个文件", items);
            switch (index)
            {
                case 0:
                    var win = new GpxWindow
                    {
                        LoadFiles = files.ToArray()
                    };
                    win.Show();
                    win.BringToFront();
                    break;
                case 1:
                    await Gps.ImportAllToNewLayerAsync(files, Gps.GpxImportType.Line, layers, Config.Instance.BasemapCoordinateSystem);
                    break;
                case 2:
                    await Gps.ImportAllToNewLayerAsync(files, Gps.GpxImportType.Point, layers, Config.Instance.BasemapCoordinateSystem);
                    break;
                case 3:
                    await Gps.ImportMultipleToLayerAsync(files, layer as IMapLayerInfo, Config.Instance.BasemapCoordinateSystem);
                    break;

            }
        }

        /// <summary>
        /// 弹出异常对话框
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task ShowException(Exception ex, string message)
        {
            App.Log.Error(message, ex);
            if (ex is ItemsOperationException e)
            {
                await ItemsOperaionErrorsDialog.TryShowErrorsAsync(message, e.Errors);
            }
            else
            {
                await CommonDialog.ShowErrorDialogAsync(ex, message);
            }
        }
    }
}