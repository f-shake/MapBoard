using MapBoard.Model;
using MapBoard.Mapping;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using MapBoard.Mapping.Model;
using System.Linq;
using Newtonsoft.Json.Linq;
using MapBoard.Util;
using System.Net;
using System.Threading;
using Esri.ArcGISRuntime.Data;
using System.Collections.Generic;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Geometry;
namespace MapBoard.IO
{
    public class MobileMapPackage
    {
        /// <summary>
        /// 导入shapefile文件到新图层
        /// </summary>
        /// <param name="path"></param>
        public static async Task ImportAsync(string path, MapLayerCollection layers)
        {
            Esri.ArcGISRuntime.Mapping.MobileMapPackage mmpk = await Esri.ArcGISRuntime.Mapping.MobileMapPackage.OpenAsync(path);
            var mmpkLayers = new List<FeatureLayer>();
            foreach (var map in mmpk.Maps)
            {
                mmpkLayers.AddRange(map.OperationalLayers.OfType<FeatureLayer>());
            }
            if (mmpkLayers.Count == 0)
            {
                return;
            }

            foreach (var mmpkLayer in mmpkLayers)
            {
                var rendererJson = mmpkLayer.Renderer.ToJson();
                var labelJsons = mmpkLayer.LabelDefinitions.ToDictionary(p => p.WhereClause, p => p.ToJson());

                var table = mmpkLayer.FeatureTable;

                var layer = await Importer.ImportFromFeatureTable(Path.GetFileNameWithoutExtension(path), layers, table);

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
}