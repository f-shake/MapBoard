using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using System.Collections.Generic;

namespace MapBoard.Mapping.Model;

public class SimpleFeature
{
    public SimpleFeature()
    {
    }

    public SimpleFeature(IDictionary<string, object> attributes, Geometry geometry)
    {
        Attributes = attributes;
        Geometry = geometry;
    }

    public IDictionary<string, object> Attributes { get; set; }
    public Geometry Geometry { get; set; }
}
