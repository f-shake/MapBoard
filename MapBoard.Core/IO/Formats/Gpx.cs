using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using FzLib.IO;
using MapBoard.IO.Abstractions;
using MapBoard.IO.Formats.Gps;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static MapBoard.IO.Formats.Gpx;
using static MapBoard.Model.CoordinateSystem;
using LibGpx = MapBoard.IO.Formats.Gps.GpxDocument;

namespace MapBoard.IO.Formats
{
    /// <summary>
    /// GPX文件与ArcGIS的互操作
    /// </summary>
    internal class Gpx(GpxImportType importType) : IMemoryLayerImporter
    {
        private const string Filed_Name = "Name";
        private const string Filed_Path = "Path";
        private const string Filed_PointIndex = "PIndex";
        private const string Filed_Time = "DateTime";
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

        public GpxImportType ImportType { get; } = importType;


        public async ValueTask<IEnumerable<SimpleLayer>> GetLayersAsync(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            var gpx = await GpxSerializer.FromFileAsync(path);
            string newName = FileSystem.GetNoDuplicateFile(Path.Combine(FolderPaths.DataPath, name + ".shp"));
            var fields = new List<FieldInfo>()
                {
                    new FieldInfo(Filed_Name,"名称",FieldInfoType.Text),
                    new FieldInfo(Filed_Path,"文件路径",FieldInfoType.Text),
                    new FieldInfo(Filed_Time,"时间",FieldInfoType.DateTime),
                };
            if (ImportType == GpxImportType.Point)
            {
                fields.Add(new FieldInfo(Filed_PointIndex, "点序号", FieldInfoType.Integer));
            }

            List<SimpleFeature> newFeatures = new List<SimpleFeature>();
            foreach (var track in gpx.Tracks)
            {
                if (ImportType == GpxImportType.Point)
                {
                    newFeatures.AddRange(GetPoints(track));
                }
                else
                {
                    newFeatures.Add(GetPolyline(track));
                }
            }

            SimpleLayer layer = new SimpleLayer(Path.GetFileNameWithoutExtension(newName),
                ImportType == GpxImportType.Point ? GeometryType.Point : GeometryType.Polyline,
                fields, SpatialReferences.Wgs84, newFeatures);

            return [layer];
        }

        /// <summary>
        /// 作为点导入
        /// </summary>
        /// <param name="track"></param>
        /// <param name="layer"></param>
        /// <param name="baseCs"></param>
        /// <returns></returns>
        private static IEnumerable<SimpleFeature> GetPoints(GpxTrack track)
        {
            int i = 0;
            foreach (var point in track.GetPoints())
            {
                i++;
                MapPoint mapPoint = point.ToXYMapPoint();
                Dictionary<string, object> attributes = new Dictionary<string, object>()
                {
                    [Filed_Name] = track.Parent.Name,
                    [Filed_Path] = track.Parent.FilePath,
                    [Filed_Time] = point.Time,
                    [Filed_PointIndex] = i,
                };
                yield return new SimpleFeature(attributes, mapPoint);
            }
        }

        /// <summary>
        /// 作为折线导入
        /// </summary>
        /// <param name="track"></param>
        /// <param name="layer"></param>
        /// <param name="baseCs"></param>
        /// <returns></returns>
        private static SimpleFeature GetPolyline(GpxTrack track)
        {
            var line = new Polyline(track.GetPoints().Select(p => p.ToXYMapPoint()));

            Dictionary<string, object> attributes = new Dictionary<string, object>()
            {
                [Filed_Name] = track.Parent.Name,
                [Filed_Path] = track.Parent.FilePath,
                [Filed_Time] = track.Parent.Time,
            };
            return new SimpleFeature(attributes, line);
        }
    }
}