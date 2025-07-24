using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using System.Collections.Generic;

namespace MapBoard.IO.Gdb;

public class GdbFeature
{
    public Dictionary<string, object> Attributes { get; set; }
    public Geometry Geometry { get; set; }
}
