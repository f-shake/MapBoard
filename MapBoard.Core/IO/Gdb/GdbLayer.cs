using Esri.ArcGISRuntime.Geometry;
using System.Collections.Generic;
using MapBoard.Model;

namespace MapBoard.IO.Gdb;

public class GdbLayer : LayerInfo
{
    public GeometryType GeometryType { get; set; }
    public List<GdbFeature> Features { get; set; } = new List<GdbFeature>();

    public SpatialReference SpatialReference { get; set; }
}
