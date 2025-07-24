using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;
using FzLib.Program;
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
            if (!App.ProgramDirectoryPath.All(char.IsAscii))
            {
                throw new InvalidOperationException($"当使用GDAL相关功能时，程序所在目录应当仅包含ASCII字符。当前目录{App.ProgramDirectoryPath}不满足条件。");
            }
            var converter = new GdalGdbConverter();
            List<GdbLayer> gdbLayers = null;
            await Task.Run(() =>
            {
                gdbLayers = converter.Convert(path);
            });

            foreach (var gdbLayer in gdbLayers)
            {
                var layer = await LayerUtility.CreateLayerAsync(gdbLayer.GeometryType, layers, gdbLayer.Name, gdbLayer.Fields);
                List<Feature> features = new List<Feature>(gdbLayer.Features.Count);
                foreach (var feature in gdbLayer.Features)
                {
                    var esriFeature = layer.CreateFeature(feature.Attributes, feature.Geometry.ToWgs84());
                    features.Add(esriFeature);
                }
                await layer.AddFeaturesAsync(features, FeaturesChangedSource.Import);
            }

        }
    }
}
