using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using MetadataExtractor.Formats.Photoshop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapBoard.IO.Gdb
{
    public class FileGdb
    {
        public static async Task ImportAsync(string path, MapLayerCollection layers)
        {
            var converter = new GdalGdbConverter();
            List<GdbLayer> gdbLayers = null;
            await Task.Run(() =>
            {
                gdbLayers = converter.Convert(path);
            });

            foreach (var gdbLayer in gdbLayers)
            {
                var layer = await LayerUtility.CreateLayerAsync(gdbLayer.GeometryType, layers, gdbLayer.Name, gdbLayer.Fields);
                foreach (var feature in gdbLayer.Features)
                {
                    var esriFeature = layer.CreateFeature(feature.Attributes, feature.Geometry.ToWgs84());
                    await layer.AddFeatureAsync(esriFeature, FeaturesChangedSource.Import);
                }
            }

        }
    }
}
