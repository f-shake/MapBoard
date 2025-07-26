using Esri.ArcGISRuntime.Geometry;
using System.Collections.Generic;
using MapBoard.Model;
using Esri.ArcGISRuntime.Data;

namespace MapBoard.Mapping.Model;

public class SimpleLayer : LayerInfo
{
    public GeometryType GeometryType { get; set; }

    public List<SimpleFeature> Features { get; set; } = new List<SimpleFeature>();

    public SpatialReference SpatialReference { get; set; }
}
