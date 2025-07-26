using Esri.ArcGISRuntime.Geometry;
using MapBoard.IO.Abstractions;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapBoard.IO.Formats
{
    /// <summary>
    /// OpenLayers网络GIS
    /// </summary>
    internal class OpenLayers : IMapExporter
    {
        public const string JS = @"var map = new ol.Map({
    target: 'map',
    layers: [
        {{base}}
        {{vector}}
    ],
    view: new ol.View({
        center: ol.proj.fromLonLat([{{lon}}, {{lat}}]),
        zoom: {{zoom}}
    }),
});
map.on('click', function(e) {
    let i=0;
    map.forEachFeatureAtPixel(e.pixel, function (feature, layer) {
       if(i!=0) return;
       i=1;
       alert(feature.getProperties().description);
    })
}); ";

        private const string JS_KML = @"new ol.layer.Vector({
            source: new ol.source.Vector({
                url: '{{url}}',
                format: new ol.format.KML({
                    extractStyles: true,
                    extractAttributes: true,
                    showPointNames:true
                }),
            }),
        }),";

        private const string JS_Tile = @"new ol.layer.Tile({
            source: new ol.source.XYZ({
                url:
                    '{{url}}',
            }),
        }),";

        private string mainJS = JS;

        public OpenLayers(IEnumerable<string> webResourcesPath, IEnumerable<string> baseLayers)
        {
            WebResourcesPath = [.. webResourcesPath];
            BaseLayers = [.. baseLayers];
        }

        public List<string> BaseLayers { get; }

        public List<string> WebResourcesPath { get; }

        public async Task ExportAsync(string path, IEnumerable<IMapLayerInfo> layers)
        {
            await PrepareFilesAsync(path);
            InsertBaseLayers();
            await InsertVectorLayersAsync(path, layers);
            await CalculateExtentAsync(layers);
            await File.WriteAllTextAsync(Path.Combine(path, "index.js"), mainJS, Encoding.UTF8);
        }

        /// <summary>
        /// 计算图层范围
        /// </summary>
        /// <returns></returns>
        private async Task CalculateExtentAsync(IEnumerable<IMapLayerInfo> layers)
        {
            List<Envelope> extents = new List<Envelope>();
            foreach (var layer in layers)
            {
                extents.Add(await layer.QueryExtentAsync(new Esri.ArcGISRuntime.Data.QueryParameters()));
            }
            var extent = extents.CombineExtents().ToWgs84();
            double lon = 0.5 * (extent.XMax + extent.XMin);
            double lat = 0.5 * (extent.YMax + extent.YMin);
            var webM = extent.ToWebMercator();

            int zoom = Convert.ToInt32(Math.Round(Math.Log(1e8 / webM.Width, 2)));
            mainJS = mainJS
                .Replace("{{lon}}", lon.ToString())
                .Replace("{{lat}}", lat.ToString())
                .Replace("{{zoom}}", zoom.ToString());
        }

        /// <summary>
        /// 插入底图
        /// </summary>
        private void InsertBaseLayers()
        {
            StringBuilder str = new StringBuilder();
            foreach (var layer in BaseLayers)
            {
                str.Append(JS_Tile.Replace("{{url}}", layer));
            }
            mainJS = mainJS.Replace("{{base}}", str.ToString());
        }

        /// <summary>
        /// 插入矢量图层
        /// </summary>
        /// <returns></returns>
        private async Task InsertVectorLayersAsync(string dir, IEnumerable<IMapLayerInfo> layers)
        {
            StringBuilder str = new StringBuilder();
            foreach (var layer in layers)
            {
                string temp = Path.GetTempFileName() + ".kmz";
                await Exporter.ExportKmlAsync(temp, layer);
                using var fs = File.OpenRead(temp);
                using ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read, false);
                zip.Entries[0].ExtractToFile(Path.Combine(dir, $"{layer.Name}.kml"));
                str.Append(JS_KML.Replace("{{url}}", $"{layer.Name}.kml"));
            }
            mainJS = mainJS.Replace("{{vector}}", str.ToString());
        }

        /// <summary>
        /// 准备文件
        /// </summary>
        /// <returns></returns>
        private async Task PrepareFilesAsync(string dir)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            Directory.CreateDirectory(dir);

            foreach (var r in WebResourcesPath)
            {
                File.Copy(r, Path.Combine(dir, Path.GetFileName(r)));
            }
            //KML显示的“点”其实是标签，自带的图表是很丑的大头针，因此需要进行替换。并且大头针有偏移，所以这里稍微修改了一下偏移。
            string oljs = (await File.ReadAllTextAsync(Path.Combine(dir, "ol.js")))
                .Replace("CC=\"https://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png\",$C=new zv({anchor:OC=[20,2]",
                "CC=\"point.png\",$C=new zv({anchor:OC=[12,52]");
            await File.WriteAllTextAsync(Path.Combine(dir, "ol.js"), oljs, Encoding.UTF8);
        }
    }
}