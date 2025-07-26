using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using MapBoard.IO.Abstractions;
using MapBoard.IO.Formats;
using System.Collections.Generic;
using Esri.ArcGISRuntime.Data;
using MapBoard.Util;
using MapBoard.Model;

namespace MapBoard.IO
{
    public static class Exporter
    {
        public static Task ExportOpenlayersAsync(string path, IEnumerable<IMapLayerInfo> layers, IEnumerable<string> baseLayers, string[] webRes)
        {
            return ExportMapAsync(new OpenLayers(webRes, baseLayers), path, layers);
        }
        public static Task ExportOpenlayersAsync(string path, IMapLayerInfo layer, IEnumerable<string> baseLayers, string[] webRes)
        {
            return ExportMapAsync(new OpenLayers(webRes, baseLayers), path, [layer]);
        }

        public static Task ExportShapefileAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features = null)
        {
            return ExportLayerAsync(new Shapefile(), path, layer, features);
        }

        private static async Task ExportLayerAsync(IFeatureTableExporter exporter, string path, IMapLayerInfo layer, IEnumerable<Feature> features)
        {
            if (features == null)
            {
                features = await layer.GetAllFeaturesAsync();
            }
            await exporter.ExportAsync(path, layer, features);
        }

        private static async Task ExportMapAsync(IMapExporter exporter, string path, IEnumerable<IMapLayerInfo> layers)
        {
            await exporter.ExportAsync(path, layers);
        }
    }
}