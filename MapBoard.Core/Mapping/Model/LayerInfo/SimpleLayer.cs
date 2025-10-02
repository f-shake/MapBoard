using Esri.ArcGISRuntime.Geometry;
using System.Collections.Generic;
using MapBoard.Model;
using Esri.ArcGISRuntime.Data;

namespace MapBoard.Mapping.Model;

public class SimpleLayer : LayerInfo
{
    public SimpleLayer(string name, GeometryType geometryType, IEnumerable<FieldInfo> fields, SpatialReference spatialReference)
    {
        Name = name;
        GeometryType = geometryType;
        Fields = [.. fields];
        SpatialReference = spatialReference;
    }
    public SimpleLayer(string name, GeometryType geometryType, IEnumerable<FieldInfo> fields, SpatialReference spatialReference, IEnumerable<SimpleFeature> features)
        : this(name, geometryType, fields, spatialReference)
    {
        Features = [.. features];
    }

    public GeometryType GeometryType { get; set; }

    public List<SimpleFeature> Features { get; set; } = new List<SimpleFeature>();

    public SpatialReference SpatialReference { get; set; }
}
