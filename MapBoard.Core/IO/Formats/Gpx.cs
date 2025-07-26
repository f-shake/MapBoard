using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using FzLib.IO;
using MapBoard.Mapping;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGpx = MapBoard.IO.Formats.Gps.GpxDocument;
using static MapBoard.Model.CoordinateSystem;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.IO.Formats.Gps;

namespace MapBoard.IO.Formats
{
    /// <summary>
    /// GPX文件与ArcGIS的互操作
    /// </summary>
    public static class Gpx
    {
        private const string Filed_Name = "Name";
        private const string Filed_Path = "Path";
        private const string Filed_Date = "Date";
        private const string Filed_Time = "DateTime";
        private const string Filed_Index = "Index";
        private const string Filed_PointIndex = "PIndex";

        /// <summary>
        /// 导入GPX文件到新图层中
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type">生成的类型</param>
        public static async Task<IMapLayerInfo> ImportToNewLayerAsync(string path, GpxImportType type, MapLayerCollection layers, CoordinateSystem baseCs)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            var gpx = await GpxSerializer.FromFileAsync(path);
            string newName = FileSystem.GetNoDuplicateFile(Path.Combine(FolderPaths.DataPath, name + ".shp"));
            var fields = new List<FieldInfo>()
                {
                    new FieldInfo(Filed_Name,"名称",FieldInfoType.Text),
                    new FieldInfo(Filed_Path,"文件路径",FieldInfoType.Text),
                    new FieldInfo(Filed_Date,"日期",FieldInfoType.Date),
                    new FieldInfo(Filed_Time,"时间",FieldInfoType.DateTime),
                    new FieldInfo(Filed_Index,"轨迹序号",FieldInfoType.Integer),
                };
            if (type == GpxImportType.Point)
            {
                fields.Add(new FieldInfo(Filed_PointIndex, "点序号", FieldInfoType.Integer));
            }

            var layer = await LayerUtility.CreateLayerAsync( type == GpxImportType.Point ? GeometryType.Point : GeometryType.Polyline,
                layers, name: Path.GetFileNameWithoutExtension(newName), fields);
            List<Feature> newFeatures = new List<Feature>();
            foreach (var track in gpx.Tracks)
            {
                if (type == GpxImportType.Point)
                {
                    newFeatures.AddRange(ImportAsPoint(track, layer, baseCs));
                }
                else
                {
                    newFeatures.AddRange(ImportAsPolyline(track, layer, baseCs));
                }
            }
            await layer.AddFeaturesAsync(newFeatures, FeaturesChangedSource.Import);
            return layer;
        }

        /// <summary>
        /// 作为折线导入
        /// </summary>
        /// <param name="track"></param>
        /// <param name="layer"></param>
        /// <param name="baseCs"></param>
        /// <returns></returns>
        private static IEnumerable<Feature> ImportAsPolyline(GpxTrack track, IMapLayerInfo layer, CoordinateSystem baseCs)
        {
            List<MapPoint> points = new List<MapPoint>();
            foreach (var point in track.GetPoints())
            {
                points.Add(CoordinateTransformation.Transformate(point.ToXYMapPoint(), WGS84, baseCs));
            }
            Feature feature = layer.CreateFeature();
            feature.Geometry = new Polyline(points);
            ApplyAttributes(track, layer, null, feature, track.Parent.Time);
            yield return feature;
        }

        /// <summary>
        /// 作为点导入
        /// </summary>
        /// <param name="track"></param>
        /// <param name="layer"></param>
        /// <param name="baseCs"></param>
        /// <returns></returns>
        private static IEnumerable<Feature> ImportAsPoint(GpxTrack track, IMapLayerInfo layer, CoordinateSystem baseCs)
        {
            int i = 0;
            foreach (var point in track.GetPoints())
            {
                MapPoint mapPoint = CoordinateTransformation.Transformate(point.ToXYMapPoint(), WGS84, baseCs);
                Feature feature = layer.CreateFeature();
                feature.Geometry = mapPoint;
                ApplyAttributes(track, layer, i++, feature, point.Time);
                yield return feature;
            }
        }

        /// <summary>
        /// 应用GPX的属性到<see cref="Feature"/>
        /// </summary>
        /// <param name="track"></param>
        /// <param name="layer"></param>
        /// <param name="index"></param>
        /// <param name="feature"></param>
        /// <param name="time"></param>
        private static void ApplyAttributes(GpxTrack track, IMapLayerInfo layer, int? index, Feature feature, DateTime? time)
        {
            if (layer.HasField(Filed_Name, FieldInfoType.Text))
            {
                feature.SetAttributeValue(Filed_Name, track.Parent.Name);
                //feature.SetAttributeValue(Filed_Name, Path.GetFileNameWithoutExtension(track.GpxInfo.FilePath));
            }
            if (layer.HasField(Filed_Path, FieldInfoType.Text))
            {
                feature.SetAttributeValue(Filed_Path, track.Parent.FilePath);
            }
            if (layer.HasField(Filed_Date, FieldInfoType.Date))
            {
                try
                {
                    feature.SetAttributeValue(Filed_Date, track.Parent.Time);
                }
                catch
                {

                }
            }
            if (layer.HasField(Filed_Time, FieldInfoType.DateTime))
            {
                feature.SetAttributeValue(Filed_Time, time);
            }
            if (layer.HasField(Filed_Index, FieldInfoType.Integer))
            {
                feature.SetAttributeValue(Filed_Index, track.Parent.Tracks.IndexOf(track));
            }
            if (layer.HasField(Filed_PointIndex, FieldInfoType.Integer) && index.HasValue)
            {
                feature.SetAttributeValue(Filed_PointIndex, index.Value);
            }

        }

        /// <summary>
        /// 导入多个GPX到新图层中
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="type"></param>
        /// <param name="layers"></param>
        /// <param name="baseCS"></param>
        /// <returns></returns>
        /// <exception cref="ItemsOperationException"></exception>
        public static async Task ImportAllToNewLayerAsync(IEnumerable<string> paths, GpxImportType type, MapLayerCollection layers, CoordinateSystem baseCS)
        {
            ItemsOperationErrorCollection errors = new ItemsOperationErrorCollection();
            IMapLayerInfo layer = null;
            try
            {
                layer = await ImportToNewLayerAsync(paths.First(), type, layers, baseCS);
            }
            catch (Exception ex)
            {
                errors.Add(new ItemsOperationError(paths.First(), ex));
            }

            foreach (var path in paths.Skip(1))
            {
                try
                {
                    await ImportToLayerAsync(path, layer, baseCS);
                }
                catch (Exception ex)
                {
                    errors.Add(new ItemsOperationError(paths.First(), ex));
                }
            }
            if (errors.Count > 0)
            {
                throw new ItemsOperationException(errors);
            }
        }

        /// <summary>
        /// 将多个GPX导入到现有图层中
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="layer"></param>
        /// <param name="baseCS"></param>
        /// <returns></returns>
        /// <exception cref="ItemsOperationException"></exception>
        public static async Task ImportMultipleToLayerAsync(IEnumerable<string> paths, IMapLayerInfo layer, CoordinateSystem baseCS)
        {
            ItemsOperationErrorCollection errors = new ItemsOperationErrorCollection();
            foreach (var path in paths)
            {
                try
                {
                    await ImportToLayerAsync(path, layer, baseCS);
                }
                catch (Exception ex)
                {
                    errors.Add(new ItemsOperationError(path, ex));
                }
            }
            if (errors.Count > 0)
            {
                throw new ItemsOperationException(errors);
            }
        }

        /// <summary>
        /// 将GPX导入到现有图层中
        /// </summary>
        /// <param name="path"></param>
        /// <param name="layer"></param>
        /// <param name="baseCS"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static async Task<IReadOnlyList<Feature>> ImportToLayerAsync(string path, IMapLayerInfo layer, CoordinateSystem baseCS)
        {
            var gpx = await GpxSerializer.FromFileAsync(path);
            List<Feature> importedFeatures = new List<Feature>();

            foreach (var track in gpx.Tracks)
            {
                if (layer.GeometryType == GeometryType.Point)
                {
                    importedFeatures.AddRange(ImportAsPoint(track, layer, baseCS));
                }
                else if (layer.GeometryType == GeometryType.Polyline)
                {
                    importedFeatures.AddRange(ImportAsPolyline(track, layer, baseCS));
                }
                else
                {
                    throw new NotSupportedException("不支持的格式图形类型");
                }
            }
            await layer.AddFeaturesAsync(importedFeatures, FeaturesChangedSource.Import);

            return importedFeatures.AsReadOnly();
        }

        /// <summary>
        /// 生成的图形的类型
        /// </summary>
        public enum GpxImportType
        {
            /// <summary>
            /// 每一个点就是一个点
            /// </summary>
            Point,

            /// <summary>
            /// 连点成线，所有点生成一条线
            /// </summary>
            Line,
        }
    }
}