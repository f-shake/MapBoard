using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.IO.Abstractions;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
namespace MapBoard.IO.Formats
{
    internal class MobileMapPackage : IFeatureTableImporter
    {
        public async ValueTask<IEnumerable<FeatureTable>> GetFeatureTablesAsync(string path)
        {
            Esri.ArcGISRuntime.Mapping.MobileMapPackage mmpk = await Esri.ArcGISRuntime.Mapping.MobileMapPackage.OpenAsync(path);
            var mmpkLayers = new List<FeatureLayer>();
            foreach (var map in mmpk.Maps)
            {
                mmpkLayers.AddRange(map.OperationalLayers.OfType<FeatureLayer>());
            }

            return mmpkLayers.Select(p => p.FeatureTable);
        }

        public string GetLayerName(FeatureTable featureTable)
        {
            return featureTable.TableName;
        }

        public void OnLayerImported(FeatureTable featureTable, IMapLayerInfo layer)
        {
            var mmpkLayer = featureTable.Layer as FeatureLayer;
            var rendererJson = mmpkLayer.Renderer.ToJson();
            var labelJsons = mmpkLayer.LabelDefinitions.ToDictionary(p => p.WhereClause, p => p.ToJson());

            layer.Renderer.RawJson = rendererJson;
            layer.Labels = [.. mmpkLayer.LabelDefinitions.Select(p => new LabelInfo()
                    {
                        RawJson = p.ToJson(),
                        UseRawJson = true
                    })];
            layer.Renderer.UseRawJson = true;
            layer.ApplyStyle();
        }

    }
}
