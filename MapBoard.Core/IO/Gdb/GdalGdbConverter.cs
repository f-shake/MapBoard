using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using MaxRev.Gdal.Core;
using OSGeo.OGR;
using OSGeo.OSR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FieldType = OSGeo.OGR.FieldType;
using OGeometry = OSGeo.OGR.Geometry;
using AGeometry = Esri.ArcGISRuntime.Geometry.Geometry;
using APolyline = Esri.ArcGISRuntime.Geometry.Polyline;
using APolygon = Esri.ArcGISRuntime.Geometry.Polygon;
using AFeature = Esri.ArcGISRuntime.Data.Feature;
using AFieldInfo = Esri.ArcGISRuntime.Data.Field;
using OFeature = OSGeo.OGR.Feature;
using MapBoard.Model;
using MapBoard.Mapping.Model;
using FzLib.Collection;
using Swan;
using ASR = Esri.ArcGISRuntime.Geometry.SpatialReference;
using OSR = OSGeo.OSR.SpatialReference;

namespace MapBoard.IO.Gdb;

public class GdalGdbConverter
{
    private DataSource dataSource;

    private string gdbPath;

    public List<GdbLayer> Convert(string path, CancellationToken cancellationToken = default)
    {
        gdbPath = Path.GetFullPath(path);

        cancellationToken.ThrowIfCancellationRequested();
        GdalBase.ConfigureAll();
        Driver driver = Ogr.GetDriverByName("OpenFileGDB");
        dataSource = driver.Open(gdbPath, 0);

        var gdbItems = dataSource.ExecuteSQL("SELECT * FROM GDB_Items ", null, null);
        Dictionary<string, string> name2Alias = new Dictionary<string, string>();
        if (gdbItems != null)
        {
            gdbItems.ResetReading();
            OFeature feature;
            while ((feature = gdbItems.GetNextFeature()) != null)
            {
                try
                {
                    string name = feature.GetFieldAsString("Name");
                    string def = feature.GetFieldAsString("Definition");
                    if (!string.IsNullOrWhiteSpace(def))
                    {
                        XElement xDef = XElement.Parse(def);
                        var aliases = xDef.Elements("AliasName");
                        if (aliases.Any())
                        {
                            name2Alias.Add(name, aliases.First().Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Assert(false);
                }
                feature.Dispose();
            }
        }

        var layers = new List<GdbLayer>();

        try
        {
            for (int i = 0; i < dataSource.GetLayerCount(); i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                //获取图层
                Layer layer = dataSource.GetLayerByIndex(i);
                try
                {
                    (var gdbLayer, var map) = GetTableDescription(layer);

                    if (name2Alias.TryGetValue(gdbLayer.Name, out string value) && !string.IsNullOrWhiteSpace(value))
                    {
                        gdbLayer.Name = value;
                    }
                    gdbLayer.Features = ConvertFeatures(layer, map, gdbLayer.SpatialReference);

                    layers.Add(gdbLayer);
                }
                catch (Exception ex)
                {
                    throw new Exception($"无法创建图层{layer.GetNameUTF8()}：{ex.Message}");
                }
            }
        }
        finally
        {

        }

        return layers;
    }


    private static int GetEpsgId(OSR sr)
    {
        string authCode = sr.GetAuthorityCode("PROJCS") ?? sr.GetAuthorityCode("GEOGCS");
        if (authCode == null || !int.TryParse(authCode, out int epsgCode))
        {
            return 0;
        }

        return epsgCode;
    }

    private void Assert(bool value, string message)
    {
        if (!value)
        {
            throw new ArgumentException(message);
        }
    }

    private List<GdbFeature> ConvertFeatures(Layer layer, IDictionary<string, string> fieldNameMap, ASR sr)
    {
        List<GdbFeature> newFeatures = new List<GdbFeature>();
        while (true)
        {
            OFeature feature = layer.GetNextFeature();
            if (null == feature)
            {
                break;
            }
            var attr = GetAttributes(layer.GetFIDColumn(), feature, fieldNameMap);
            AGeometry geom = layer.GetGeomType() == wkbGeometryType.wkbNone ? null : GetGeometry(feature, sr);
            newFeatures.Add(new GdbFeature
            {
                Attributes = attr,
                Geometry = geom,
            });
        }
        return newFeatures;
    }

    private APolyline ConvertMultiCurve(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbMultiCurve or wkbGeometryType.wkbMultiCurveZ
            or wkbGeometryType.wkbMultiCurveZM or wkbGeometryType.wkbMultiCurveM, "不是MultiCurve");
        return ConvertMultiPolyline(geom.GetLinearGeometry(1, []), sr);
    }

    private APolygon ConvertMultiPolygon(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbMultiPolygon
            or wkbGeometryType.wkbMultiPolygon25D
            or wkbGeometryType.wkbMultiPolygonM
            or wkbGeometryType.wkbMultiPolygonZM
            , "不是MultiPolygon");

        List<IList<MapPoint>> list = new List<IList<MapPoint>>();
        for (int i = 0; i < geom.GetGeometryCount(); i++)
        {
            list.AddRange(GetPoints3(geom.GetGeometryRef(i), sr));
        }
        var polygon = new APolygon(list);
        return polygon;
    }

    private APolyline ConvertMultiPolyline(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbMultiLineString
            or wkbGeometryType.wkbMultiLineStringM
            or wkbGeometryType.wkbMultiLineStringZM, "不是MultiPolyline");

        var polyline = new APolyline(GetPoints3(geom, sr));
        return polyline;
    }

    private APolygon ConvertMultiSurface(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbMultiSurface or wkbGeometryType.wkbMultiSurfaceZ, "不是MultiSurface");
        return ConvertMultiPolygon(geom.GetLinearGeometry(1, []), sr);
    }

    private MapPoint ConvertPoint(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbPoint
            or wkbGeometryType.wkbPointM
            or wkbGeometryType.wkbPointZM, "不是Point");
        return GetPoint(geom, 0, sr);
    }

    private APolygon ConvertPolygon(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbPolygon
            or wkbGeometryType.wkbPolygonM
            or wkbGeometryType.wkbPolygonZM
            or wkbGeometryType.wkbSurface, "不是Polygon");

        var polygon = new APolygon(GetPoints3(geom, sr));
        return polygon;
    }

    private APolyline ConvertPolyline(OGeometry geom, ASR sr)
    {
        Assert(geom.GetGeometryType() is wkbGeometryType.wkbLineString, "不是LineString");
        var line = new APolyline(GetPoints2(geom, sr));
        return line;
    }

    private Dictionary<string, object> GetAttributes(string idFieldName, OFeature feature, IDictionary<string, string> fieldNameMap)
    {
        Dictionary<string, object> dic = new Dictionary<string, object>();
        var id = feature.GetFID();
        dic.Add(idFieldName, id);
        for (int i = 0; i < feature.GetFieldCount(); i++)
        {
            var fieldDef = feature.GetFieldDefnRef(i);
            string name = fieldDef.GetNameUTF8();
            if (fieldNameMap.TryGetValue(name, out string newName))
            {
                name = newName;
            }
            object value = fieldDef.GetFieldType() switch
            {
                FieldType.OFTInteger => feature.GetFieldAsInteger(i),
                FieldType.OFTReal => feature.GetFieldAsDouble(i),
                FieldType.OFTString => feature.GetFieldAsStringUTF8(i),
                FieldType.OFTWideString => feature.GetFieldAsStringUTF8(i),
                FieldType.OFTDate => GetDatetime(feature, i) == null ? null : DateOnly.FromDateTime(GetDatetime(feature, i).Value),
                FieldType.OFTInteger64 => feature.GetFieldAsInteger64(i),
                FieldType.OFTDateTime => GetDatetime(feature, i),
                _ => throw new NotImplementedException(),
            };

            dic.Add(name, value);
        }
        return dic;

        DateTime? GetDatetime(OFeature feature, int index)
        {
            feature.GetFieldAsDateTime(index, out int year, out int month, out int day, out int hour, out int minute, out float second, out _);
            return year == 0 ? null : new DateTime(year, month, day, hour, minute, (int)second, (int)(1000 * (second - (int)second)));
        }
    }

    private (List<FieldInfo> fields, Dictionary<string, string> fieldNameMap) GetFields(FeatureDefn layerDef)
    {
        int fieldCount = layerDef.GetFieldCount();
        List<FieldInfo> fields = new List<FieldInfo>();
        Dictionary<string, string> fieldNameMap = new Dictionary<string, string>();
        for (int j = 0; j < fieldCount; j++)
        {
            var fieldDef = layerDef.GetFieldDefn(j);
            string name = fieldDef.GetNameUTF8();
            if (IsFieldIgnored(name))
            {
                continue;
            }
            FieldInfoType type = fieldDef.GetFieldType() switch
            {
                FieldType.OFTInteger => FieldInfoType.Integer,
                FieldType.OFTReal => FieldInfoType.Float,
                FieldType.OFTString => FieldInfoType.Text,
                FieldType.OFTWideString => FieldInfoType.Text,
                FieldType.OFTDate => FieldInfoType.Date,
                FieldType.OFTTime => FieldInfoType.DateTime,
                FieldType.OFTInteger64 => FieldInfoType.Integer,
                FieldType.OFTDateTime => FieldInfoType.DateTime,
                _ => throw new NotImplementedException(),
            };

            string alias = fieldDef.GetFieldAliasAsStringUTF8();
            int length = fieldDef.GetWidth();
            var newName = FieldInfo.GetValidFieldName(name);
            fieldNameMap.Add(name, newName);
            fields.Add(new FieldInfo()
            {
                Name = newName,
                Type = type,
                DisplayName = string.IsNullOrWhiteSpace(alias) ? newName : alias,
            });
        }
        return (fields, fieldNameMap);
    }

    private AGeometry GetGeometry(OFeature feature, ASR sr)
    {
        var geom = feature.GetGeometryRef();

        if (geom.GetGeometryType() is
            wkbGeometryType.wkbMultiSurface
            or wkbGeometryType.wkbMultiSurfaceZ
            or wkbGeometryType.wkbMultiSurfaceZM)
        {

        }

        AGeometry newGeom = geom.GetGeometryType() switch
        {
            wkbGeometryType.wkbPoint
            or wkbGeometryType.wkbPointM
            or wkbGeometryType.wkbPointZM => ConvertPoint(geom, sr),

            wkbGeometryType.wkbLineString
            or wkbGeometryType.wkbLineStringM
            or wkbGeometryType.wkbLineStringZM => ConvertPolyline(geom, sr),

            wkbGeometryType.wkbMultiLineString
           or wkbGeometryType.wkbMultiLineStringM
           or wkbGeometryType.wkbMultiLineStringZM => ConvertMultiPolyline(geom, sr),

            wkbGeometryType.wkbPolygon
            or wkbGeometryType.wkbPolygonZM
            or wkbGeometryType.wkbPolygonM => ConvertPolygon(geom, sr),

            wkbGeometryType.wkbMultiPolygonM
            or wkbGeometryType.wkbMultiPolygon
            or wkbGeometryType.wkbMultiPolygonZM
            or wkbGeometryType.wkbMultiPolygon25D => ConvertMultiPolygon(geom, sr),

            wkbGeometryType.wkbMultiSurface
            or wkbGeometryType.wkbMultiSurfaceZ
            or wkbGeometryType.wkbMultiSurfaceZM => ConvertMultiSurface(geom, sr),

            wkbGeometryType.wkbMultiCurve
            or wkbGeometryType.wkbMultiCurveM
            or wkbGeometryType.wkbMultiCurveZM => ConvertMultiCurve(geom, sr),

            wkbGeometryType.wkbMultiPoint
            or wkbGeometryType.wkbMultiPointM
            or wkbGeometryType.wkbMultiPointZM => null,
            _ => null
        };

        if (newGeom == null)
        {

        }
        return newGeom;

    }

    private MapPoint GetPoint(OGeometry geom, int i, ASR sr)
    {
        double x = geom.GetX(i);
        double y = geom.GetY(i);
        return new MapPoint(x, y, sr);
    }

    /// <summary>
    /// 获取一系列的点的集合
    /// </summary>
    /// <param name="geom"></param>
    /// <returns></returns>
    private IList<MapPoint> GetPoints2(OGeometry geom, ASR sr)
    {
        List<MapPoint> results = new List<MapPoint>();
        for (int i = 0; i < geom.GetPointCount(); i++)
        {
            MapPoint point = GetPoint(geom, i, sr);
            results.Add(point);
        }
        return results;
    }

    /// <summary>
    /// 获取点集合的集合
    /// </summary>
    /// <param name="geom"></param>
    /// <returns></returns>
    private IList<IList<MapPoint>> GetPoints3(OGeometry geom, ASR sr)
    {
        List<IList<MapPoint>> results = new List<IList<MapPoint>>();
        for (int i = 0; i < geom.GetGeometryCount(); i++)
        {
            results.Add(GetPoints2(geom.GetGeometryRef(i), sr));
        }
        return results;
    }
    private (GdbLayer layer, Dictionary<string, string> fieldNameMap) GetTableDescription(Layer layer)
    {
        string name = layer.GetNameUTF8();
        StringBuilder sanitizedName = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            sanitizedName.Append(IsValidTableChar(c) ? c : '_');
        }

        var srid = GetEpsgId(layer.GetSpatialRef());
        ASR sr = srid == 0 ? null : ASR.Create(srid);
        name = sanitizedName.ToString();

        var layerInfo = new GdbLayer()
        {
            Name = name,
            SpatialReference = sr
        };
        List<FieldInfo> fields = [];
        (var tempFields, var map) = GetFields(layer.GetLayerDefn());
        foreach (var field in tempFields)
        {
            if (IsFieldIgnored(field.Name))
            {
                continue;
            }
            fields.Add(field);
        }
        layerInfo.Fields = [.. fields];
        var type = layer.GetGeomType();
        layerInfo.GeometryType = type switch
        {
            wkbGeometryType.wkbPoint
            or wkbGeometryType.wkbPointM
            or wkbGeometryType.wkbPointZM => GeometryType.Point,

            wkbGeometryType.wkbLineString
            or wkbGeometryType.wkbMultiLineString
            or wkbGeometryType.wkbLineStringM
            or wkbGeometryType.wkbMultiLineStringM
            or wkbGeometryType.wkbLineStringZM
            or wkbGeometryType.wkbMultiLineStringZM => GeometryType.Polyline,

            wkbGeometryType.wkbPolygon
            or wkbGeometryType.wkbMultiPolygon
            or wkbGeometryType.wkbPolygonM
            or wkbGeometryType.wkbMultiPolygonM
            or wkbGeometryType.wkbPolygonZM
            or wkbGeometryType.wkbMultiPolygonZM
            or wkbGeometryType.wkbMultiPolygon25D => GeometryType.Polygon,

            wkbGeometryType.wkbMultiPoint
            or wkbGeometryType.wkbMultiPointM
            or wkbGeometryType.wkbMultiPointZM => GeometryType.Multipoint,

            wkbGeometryType.wkbNone => GeometryType.Unknown,

            _ => throw new ArgumentOutOfRangeException($"图层的几何类型{type}不在可处理范围内"),
        };
        return (layerInfo, map);
    }

    private bool IsFieldIgnored(string name)
    {
        return name.StartsWith("shape_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("fid", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("st_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("objectid", StringComparison.OrdinalIgnoreCase);
    }
    private bool IsValidTableChar(char c)
    {
        //ChatGPT

        // 检查是否为字母或中文（支持 Unicode 字符）
        if (char.IsLetter(c))
            return true;

        // 检查是否为数字
        if (char.IsDigit(c))
            return true;

        // 检查是否为下划线
        if (c == '_')
            return true;

        // 其他字符（包括符号和控制字符）均视为非法
        return false;
    }
}