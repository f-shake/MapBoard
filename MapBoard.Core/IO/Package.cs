using MapBoard.Model;
using MapBoard.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Threading;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;

namespace MapBoard.IO
{
    /// <summary>
    /// 基于Zip的地图画板工程文件包
    /// </summary>
    public static class Package
    {
        /// <summary>
        /// 备份
        /// </summary>
        /// <param name="layers"></param>
        /// <param name="maxCount"></param>
        /// <param name="copyOnly"></param>
        /// <returns></returns>
        public static async Task BackupAsync(MapLayerCollection layers, int maxCount)
        {
            await ExportMapAsync(Path.Combine(FolderPaths.BackupPath,
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mbmpkg"),
                layers, true);

            var files = Directory.EnumerateFiles(FolderPaths.BackupPath).ToList();
            if (files.Count > maxCount)
            {
                foreach (var file in files
                    .Select(p => new FileInfo(p))
                    .ToList()
                    .OrderByDescending(p => p.LastWriteTime)
                    .Skip(maxCount)
                    .ToList())
                {
                    file.Delete();
                }
            }
        }

        /// <summary>
        /// 导出图层到图层包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="layer"></param>
        /// <param name="layers">图层集合</param>
        /// <param name="copyOnly">是否仅简单复制而非重建</param>
        /// <returns></returns>
        public static async Task ExportLayerAsync(string path, IMapLayerInfo layer)
        {
            if (layer is not MgdbMapLayerInfo mgdbLayer)
            {
                throw new NotSupportedException($"只支持{nameof(MgdbMapLayerInfo)}");
            }
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            DirectoryInfo directory = PathUtility.GetTempDir();


            Geodatabase newMgdb = await Geodatabase.CreateAsync(Path.Combine(directory.FullName, MobileGeodatabase.MgdbFileName));
            await CopyTableToMgdbRebuildAsync(mgdbLayer.Table, newMgdb);
            newMgdb.Close();


            File.WriteAllText(Path.Combine(directory.FullName, MapLayerCollection.LayerFileName), Newtonsoft.Json.JsonConvert.SerializeObject(layer));

            await ZipDirAsync(directory.FullName, path);
        }

        /// <summary>
        /// 导出所有图层到地图包
        /// </summary>
        /// <param name="path">目标路径</param>
        /// <param name="layers">图层集合</param>
        /// <param name="copyOnly">是否仅简单复制而非重建</param>
        /// <returns></returns>
        public static async Task ExportMapAsync(string path, MapLayerCollection layers, bool copyOnly)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            DirectoryInfo directory = PathUtility.GetTempDir();
            if (copyOnly)
            {
                await MobileGeodatabase.CopyToDirAsync(directory.FullName);
            }
            else
            {
                Geodatabase newMgdb = await Geodatabase.CreateAsync(Path.Combine(directory.FullName, MobileGeodatabase.MgdbFileName));
                //不知道为什么，这里GeodatabaseFeatureTables中有些Table，没有Load，可能是其他代码的问题，导致出现了部分废弃的表
                foreach (var table in MobileGeodatabase.Current.GeodatabaseFeatureTables.Where(p=>p.LoadStatus==Esri.ArcGISRuntime.LoadStatus.Loaded))
                {
                    await CopyTableToMgdbRebuildAsync(table, newMgdb);
                }
                newMgdb.Close();
            }
            layers.Save(Path.Combine(directory.FullName, MapLayerCollection.LayersFileName));

            await ZipDirAsync(directory.FullName, path);
        }

        /// <summary>
        /// 导入图层包
        /// </summary>
        /// <param name="path"></param>
        public static async Task ImportLayerAsync(string path, MapLayerCollection layers)
        {

            var tempDir = PathUtility.GetTempDir().FullName;

            ZipFile.ExtractToDirectory(path, tempDir);

            MgdbMapLayerInfo layer = Newtonsoft.Json.JsonConvert.DeserializeObject<MgdbMapLayerInfo>(
                   File.ReadAllText(Path.Combine(tempDir, MapLayerCollection.LayerFileName)));

            var mgdbPath = Path.Combine(tempDir, MobileGeodatabase.MgdbFileName);

            if (File.Exists(mgdbPath))
            {
                var layerMgdb = await Geodatabase.OpenAsync(mgdbPath);
                var oldTable = layerMgdb.GetGeodatabaseFeatureTable(layer.SourceName) ?? throw new Exception("图层包中的数据库找不到对应的图层表");
                await oldTable.LoadAsync();
                //这里后面需要考虑一下名称一致的问题，写一个函数，用来生成不重复的文件名
                layer = layer.Clone() as MgdbMapLayerInfo;//重新生成SourceName
                await CopyTableToMgdbRebuildAsync(oldTable, MobileGeodatabase.Current, layer.SourceName);
                layerMgdb.Close();
            }
            else if (File.Exists(Path.Combine(tempDir, $"{layer.Name}.shp")))
            {
                var shp = Path.Combine(tempDir, $"{layer.Name}.shp");
                ShapefileFeatureTable shpTable = new ShapefileFeatureTable(shp);
                await shpTable.LoadAsync();
                await PackageMigration.ImportOldVersionFeatureTableAsync(layer.SourceName, shpTable, layer.Fields);
                shpTable.Close();
            }
            else
            {
                throw new Exception($"找不到图层{layer.Name}的可导入源（包括gdb和shp）");
            }

            if (layers.Any(p => p.Name == layer.Name))
            {
                layer.Name = GetNoDuplicateName(layer.Name, layers.Select(p => p.Name));
            }
            await layers.AddAndLoadAsync(layer);
        }

        /// <summary>
        /// 导入地图包
        /// </summary>
        /// <param name="path"></param>
        public static async Task ImportMapAsync(string path, MapLayerCollection layers)
        {
            layers.Clear();
            var tempDir = PathUtility.GetTempDir().FullName;
            await Task.Run(() => ZipFile.ExtractToDirectory(path, tempDir));
            var configPath = Path.Combine(tempDir, MapLayerCollection.LayersFileName);
            var mgdbPath = Path.Combine(tempDir, MobileGeodatabase.MgdbFileName);

            await MobileGeodatabase.ClearAsync();
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("找不到图层配置文件");
            }
            var newLayers = LayerCollection.FromFile(configPath);
            foreach (var layer in newLayers)
            {
                if (layers.Any(p => p.Name == layer.Name))
                {
                    throw new ArgumentException("存在重复的图层名：" + layer.Name);
                }
            }
            if (File.Exists(mgdbPath))
            {
                await MobileGeodatabase.ReplaceFromMGDBAsync(mgdbPath);
                foreach (var layer in newLayers)
                {
                    await layers.AddAndLoadAsync(layer);
                }
            }
            else
            {
                foreach (var layer in newLayers)
                {
                    if (File.Exists(Path.Combine(tempDir, $"{layer.Name}.shp")))
                    {
                        var shp = Path.Combine(tempDir, $"{layer.Name}.shp");
                        ShapefileFeatureTable shpTable = new ShapefileFeatureTable(shp);
                        await shpTable.LoadAsync();
                        await PackageMigration.ImportOldVersionFeatureTableAsync(layer.SourceName, shpTable, layer.Fields);
                        shpTable.Close();
                    }
                    else
                    {
                        throw new Exception($"找不到图层{layer.Name}的可导入源（包括gdb和shp）");
                    }
                    await layers.AddAndLoadAsync(layer);
                }
            }
            layers.Save();
        }

        private static async Task<GeodatabaseFeatureTable> CopyTableToMgdbRebuildAsync(GeodatabaseFeatureTable table, Geodatabase mgdb, string newName = null)
        {
            newName ??= table.TableName;
            TableDescription td = new TableDescription(newName, table.SpatialReference, table.GeometryType);
            foreach (var field in table.EditableAttributeFields)
            {
                td.FieldDescriptions.Add(new FieldDescription(field.Name, field.FieldType));
            }
            var newTable = await mgdb.CreateTableAsync(td);
            var features = await table.QueryFeaturesAsync(new QueryParameters());
            await newTable.AddFeaturesAsync(features);
            return newTable;
        }
        private static string GetNoDuplicateName(string name, IEnumerable<string> existedNames)
        {
            var set = existedNames.ToHashSet();
            if (!set.Contains(name))
            {
                return name;
            }

            int i = 2;
            while (set.Contains($"{name} ({i})"))
            {
                i++;
            }
            return $"{name} ({i})";
        }
        /// <summary>
        /// 压缩一个目录
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private static async Task ZipDirAsync(string dir, string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(dir, path, CompressionLevel.Fastest, false);
            });
        }
    }
}