using EGIS.ShapeFileLib;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Model;
using MapBoard.Mapping;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Text.RegularExpressions;
using FzLib;

namespace MapBoard.IO
{
    public static class Shapefile
    {
        /// <summary>
        /// shapefile的可能的文件扩展名
        /// </summary>
        public static readonly string[] ShapefileExtensions =
        [
            ".shp",
            ".shx",
            ".dbf",
            ".prj",
            ".xml",
            ".shp.xml",
            ".cpg",
            ".sbn",
            ".sbx",
        ];

        /// <summary>
        /// 将图层克隆并保存到新的目录中的shapefile
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        public static async Task CloneFeatureToNewShpAsync(string directory, IMapLayerInfo layer)
        {
            var table = await CreateShapefileAsync(layer.GeometryType, layer.Name, directory, layer.Fields);
            List<Feature> newFeatures = new List<Feature>();
            Feature[] features = await layer.GetAllFeaturesAsync();
            try
            {
                foreach (var feature in features)
                {
                    newFeatures.Add(
                        table.CreateFeature(
                            feature.Attributes.Where(p => !FieldExtension.IsIdField(p.Key)),
                            feature.Geometry));
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            await table.AddFeaturesAsync(newFeatures);
            var extent = await table.QueryExtentAsync(new QueryParameters());
            string path = table.Path;
            table.Close();
            UpdateExtentAsync(path, extent);
        }

        /// <summary>
        /// 创建Shapefile文件
        /// </summary>
        /// <param name="type">几何类型</param>
        /// <param name="name">Shapefile文件名</param>
        /// <param name="folder">目标目录</param>
        /// <param name="fields">字段</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static async Task<ShapefileFeatureTable> CreateShapefileAsync(GeometryType type, string name, string folder = null, IEnumerable<FieldInfo> fields = null)
        {
            //如果没有指定目录则使用数据目录
            folder ??= FolderPaths.DataPath;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string path = Path.Combine(folder, name);

            //排除由ArcGIS自动创建的临时字段，判断字段合法性
            fields = fields
                .Where(p => !p.IsIdField())//ID
                .Where(p => p.Name.ToLower() != "shape_leng")//长度
                .Where(p => p.Name.ToLower() != "shape_area");//面积

            fields.CheckInvalidFieldNames();

            //创建文件
            await CreateEgisShapefileAsync(type, name, folder, fields);
            path += ".shp";
            //使用ArcGIS加载文件
            ShapefileFeatureTable table = new ShapefileFeatureTable(path);
            await table.LoadAsync();
            return table;
        }

        /// <summary>
        /// 获取各类型字段的默认长度
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int GetDefaultLength(FieldInfoType type)
        {
            return type switch
            {
                FieldInfoType.Integer => 9,
                FieldInfoType.Float => 13,
                FieldInfoType.Date => 9,
                FieldInfoType.Text => 254,
                FieldInfoType.DateTime => 20,
                _ => throw new ArgumentException(),
            };
        }

        /// <summary>
        /// 获取某一个无扩展名的shapefile的名称可能对应的shapefile的所有文件
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetExistShapefiles(string directory, string name)
        {
            if (name.EndsWith(".shp"))
            {
                name = name.RemoveEnd(".shp");
            }
            return Directory.EnumerateFiles(directory)
                 .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                 .Where(p => ShapefileExtensions.Contains(Path.GetExtension(p)));
        }

        /// <summary>
        /// 导入shapefile文件到新图层
        /// </summary>
        /// <param name="path"></param>
        public static async Task ImportAsync(string path, MapLayerCollection layers)
        {
            ShapefileFeatureTable table = new ShapefileFeatureTable(path);
            await LayerUtility.ImportFromFeatureTable(Path.GetFileNameWithoutExtension(path), layers, table);
        }

        public static async Task ExportToShapefile(string path, IMapLayerInfo layer)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string dir = Path.GetDirectoryName(path);
            await CreateEgisShapefileAsync(layer.GeometryType, name, dir, layer.Fields);
            ShapefileFeatureTable shp = await ShapefileFeatureTable.OpenAsync(Path.Combine(dir, name + ".shp"));
            var oldFeatures = await layer.QueryFeaturesAsync(new QueryParameters());
            List<Feature> newFeatures = new List<Feature>();
            foreach (var feature in oldFeatures)
            {
                newFeatures.Add(shp.CreateFeature(feature.Attributes, feature.Geometry));
            }
            await shp.AddFeaturesAsync(newFeatures);
            shp.Close();
        }

        /// <summary>
        /// 重新计算并更新Shapefile的空间范围
        /// </summary>
        /// <param name="path"></param>
        /// <param name="extent"></param>
        /// <returns></returns>
        public static async Task UpdateExtentAsync(string path, Envelope extent)
        {
            using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            fileStream.Seek(36, SeekOrigin.Begin);
            byte[] extentBytes = new byte[32];
            BitConverter.GetBytes(extent.XMin).CopyTo(extentBytes, 0);
            BitConverter.GetBytes(extent.YMin).CopyTo(extentBytes, 8);
            BitConverter.GetBytes(extent.XMax).CopyTo(extentBytes, 16);
            BitConverter.GetBytes(extent.YMax).CopyTo(extentBytes, 24);
            await fileStream.WriteAsync(extentBytes.AsMemory(0, 32));
            fileStream.Close();
        }

        /// <summary>
        /// 调用EGIS库，来创建一个Shapefile
        /// </summary>
        /// <param name="type">几何类型</param>
        /// <param name="name">Shapefile文件名</param>
        /// <param name="folder">目标目录</param>
        /// <param name="fields">字段</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="Exception"></exception>
        private static async Task CreateEgisShapefileAsync(GeometryType type, string name, string folder, IEnumerable<FieldInfo> fields)
        {
            //转换为EGIS几何类型
            var egisType = type switch
            {
                GeometryType.Point => ShapeType.Point,
                GeometryType.Polyline => ShapeType.PolyLine,
                GeometryType.Polygon => ShapeType.Polygon,
                GeometryType.Multipoint => ShapeType.Point,
                _ => throw new NotSupportedException(),
            };
            //转换为EGIS字段
            List<DbfFieldDesc> egisFields = new List<DbfFieldDesc>();
            foreach (var field in fields)
            {
                DbfFieldType fieldType = default;
                int decimalCount = 0;
                switch (field.Type)
                {
                    case FieldInfoType.Integer:
                        fieldType = DbfFieldType.Number;
                        break;

                    case FieldInfoType.Float:
                        fieldType = DbfFieldType.Number;
                        decimalCount = 6;
                        break;

                    case FieldInfoType.Date:
                        fieldType = DbfFieldType.Date;
                        break;

                    case FieldInfoType.Text:
                    case FieldInfoType.DateTime:
                        fieldType = DbfFieldType.Character;
                        break;
                }
                var f = new DbfFieldDesc()
                {
                    FieldLength = GetDefaultLength(field.Type),
                    FieldName = field.Name,
                    FieldType = fieldType,
                    DecimalCount = decimalCount
                };
                egisFields.Add(f);
            }
            //创建Shapefile
            using ShapeFileWriter sfw = ShapeFileWriter.CreateWriter(folder, name, egisType, egisFields.ToArray());
            sfw.Close();
            //EGIS无法创建多点，因此创建点以后手动修改类型
            //类型位于shape文件的[32..35]，大端序，点、折线、多边形、多点分别为1、3、5、8
            if (type == GeometryType.Multipoint)
            {
                var shpPath = Path.Combine(folder, name + ".shp");
                var bytes = await File.ReadAllBytesAsync(shpPath);
                var current = bytes[32];
                if (current != 1)
                {
                    throw new Exception("类型字节应为1，实际为" + current);
                }
                bytes[32] = 8;
                await File.WriteAllBytesAsync(shpPath, bytes);
            }
            //写入投影信息和编码信息
            await File.WriteAllTextAsync(Path.Combine(folder, name + ".prj"), SpatialReferences.Wgs84.WkText);
            await File.WriteAllTextAsync(Path.Combine(folder, name + ".cpg"), "UTF-8");
        }
    }
}