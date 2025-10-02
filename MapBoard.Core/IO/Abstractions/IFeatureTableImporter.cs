using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace MapBoard.IO.Abstractions
{
    public interface IFeatureTableImporter
    {
        public ValueTask<IEnumerable<FeatureTable>> GetFeatureTablesAsync(string path);

        public void OnLayerImported(FeatureTable featureTable, IMapLayerInfo layer);

        public string GetLayerName(FeatureTable featureTable);
    }
}
