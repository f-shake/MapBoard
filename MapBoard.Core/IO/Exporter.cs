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
        #region 各种类型的公开导出方法

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

        public static Task ExportCsvAttributeTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features = null)
        {
            return ExportLayerAsync(new Csv(), path, layer, features);
        }

        public static Task ExportCsvXYTableAsync(string path, IMapLayerInfo layer, IEnumerable<Feature> features = null)
        {
            return ExportLayerAsync(new CsvXY(), path, layer, features);
        }

        #endregion

        #region 内部方法

        private static async Task ExportLayerAsync(IFeatureTableExporter exporter, string path, IMapLayerInfo layer, IEnumerable<Feature> features)
        {
            if (features == null)
            {
                features = await layer.GetAllFeaturesAsync();
            }
            await exporter.ExportFeatureTableAsync(path, layer, features);
        }

        private static async Task ExportMapAsync(IMapExporter exporter, string path, IEnumerable<IMapLayerInfo> layers)
        {
            await exporter.ExportAsync(path, layers);
        }

        #endregion
    }
}