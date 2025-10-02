using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping.Model;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MapBoard.IO.Abstractions
{
    public interface IFeatureTableExporter
    {
        public Task ExportFeatureTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features);
    }
}
