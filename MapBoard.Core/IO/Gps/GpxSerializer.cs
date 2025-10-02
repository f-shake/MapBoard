using Esri.ArcGISRuntime.Geometry;
using MapBoard.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace MapBoard.IO.Formats.Gps
{
    public static class GpxSerializer
    {
        public const string GpxTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        #region 文件读写

        /// 从文件加载GPX
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task<GpxDocument> FromFileAsync(string path)
        {
            GpxDocument gpx = null;
            await Task.Run(() =>
            {
                var xmlString = File.ReadAllText(path);
                gpx = LoadFromString(xmlString, path);
            });
            return gpx;
        }

        /// <summary>
        /// 保存到原始位置
        /// </summary>
        /// <param name="path"></param>
        public static void Save(this GpxDocument gpx, string path)
        {
            File.WriteAllText(path, gpx.ToXmlString());
        }
        #endregion

        #region Metadata

        public static async Task<GpxDocument> LoadMetadatasFromFileAsync(string file)
        {
            //XmlReader有点难用，让ChatGPT帮我写了。
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                throw new ArgumentException("Invalid file path or file not found.");
            }

            GpxDocument gpx = new GpxDocument();

            using (XmlReader reader = XmlReader.Create(file, new XmlReaderSettings()
            {
                Async = true,
            }))
            {
                reader.ReadStartElement("gpx");

                while (reader.MoveToNextAttribute())
                {
                    if (reader.Name == "creator")
                    {
                        gpx.Creator = reader.Value;
                    }
                }

                while (await reader.ReadAsync())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "metadata":
                                await ReadMetadataAsync(reader, gpx);
                                break;
                        }
                    }
                }
            }

            return gpx;
        }

        private static async Task ReadMetadataAsync(XmlReader reader, GpxDocument gpx)
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "name":
                            gpx.Name = await reader.ReadElementContentAsStringAsync();
                            break;
                        case "desc":
                            gpx.Description = await reader.ReadElementContentAsStringAsync();
                            break;
                        case "author":
                            gpx.Author = await reader.ReadElementContentAsStringAsync();
                            break;
                        case "time":
                            gpx.Time = DateTime.Parse(await reader.ReadElementContentAsStringAsync(), null, DateTimeStyles.AdjustToUniversal);
                            break;

                        case "extensions":
                            await ReadExtensionsAsync(reader, gpx);
                            break;
                    }
                }

                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "metadata")
                {
                    break;
                }
            }
        }

        private static async Task ReadExtensionsAsync(XmlReader reader, GpxDocument gpx)
        {
            while (await reader.ReadAsync())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "distance")
                {
                    gpx.Distance = double.Parse(await reader.ReadElementContentAsStringAsync());
                }
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "duration")
                {
                    gpx.Duration = TimeSpan.Parse(await reader.ReadElementContentAsStringAsync());
                }

                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "extensions")
                {
                    break;
                }
            }
        }

        #endregion

        #region XML => Object

        private static readonly Dictionary<string, Action<GpxDocument, string>> xml2GpxProperty = new Dictionary<string, Action<GpxDocument, string>>()
        {
            ["creator"] = (g, v) => g.Creator = v,
            ["name"] = (g, v) => g.Name = v,
            ["author"] = (g, v) => g.Author = v,
            ["distance"] = (g, v) => { if (double.TryParse(v, out double result)) g.Distance = result; },
            ["duration"] = (g, v) => { if (TimeSpan.TryParse(v, out TimeSpan result)) g.Duration = result; },
            ["time"] = (g, v) => { if (DateTime.TryParse(v, CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out DateTime time)) g.Time = time; },
        };

        private static readonly Dictionary<string, Action<GpxTrack, string>> xml2TrackProperty = new Dictionary<string, Action<GpxTrack, string>>()
        {
            ["name"] = (g, v) => g.Name = v,
            ["desc"] = (g, v) => g.Description = v,
        };

        /// <summary>
        /// 从文件和读取后的内容加载GPX
        /// </summary>
        /// <param name="xmlString"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="XmlException"></exception>
        public static GpxDocument LoadFromString(string xmlString, string path)
        {
            GpxDocument gpx = new GpxDocument();
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);
            XmlElement gpxNode = xmlDoc["gpx"] ?? throw new XmlException("没有找到gpx元素");
            gpx.FilePath = path;
            foreach (XmlAttribute attribute in gpxNode.Attributes)
            {
                SetGpxValue(gpx, attribute.Name, attribute.Value);
            }
            //老版本的GPX类错误地将metadata中的元数据放在了gpx节点下
            foreach (XmlElement element in gpxNode.ChildNodes)
            {
                SetGpxValue(gpx, element.Name, element.InnerText);
            }
            //读取元数据
            if (gpxNode["metadata"] != null)
            {
                var metadataNode = gpxNode["metadata"];
                foreach (XmlElement element in metadataNode.ChildNodes)
                {
                    SetGpxValue(gpx, element.Name, element.InnerText);
                }
                //扩展元数据
                if (metadataNode["extensions"] != null)
                {
                    var extensionNode = metadataNode["extensions"];
                    foreach (XmlElement extensionElement in extensionNode.ChildNodes)
                    {
                        //单独处理Distance
                        if (extensionElement.Name == "distance")
                        {
                            SetGpxValue(gpx, "distance", extensionElement.InnerText);
                        }
                        else if (extensionElement.Name == "duration")
                        {
                            SetGpxValue(gpx, "duration", extensionElement.InnerText);
                        }
                        else
                        {
                            gpx.Extensions.Add(extensionElement.Name, extensionElement.InnerText);
                        }
                    }
                }
            }
            if (gpxNode["trk"] != null)
            {
                LoadTrack(gpx.CreateTrack(), gpxNode["trk"]);
            }
            return gpx;
        }

        private static void AppendElement(this XmlElement parent, string name, string value)
        {
            XmlElement child = parent.OwnerDocument.CreateElement(name);
            child.InnerText = value;
            parent.AppendChild(child);
        }

        private static void LoadSegment(GpxSegment seg, XmlElement segmentNode)
        {
            foreach (XmlElement pointElement in segmentNode.GetElementsByTagName("trkpt"))
            {
                if (pointElement.Attributes["lon"] == null || pointElement.Attributes["lat"] == null)
                {
                    throw new FormatException("缺少必要属性lon或lat");
                }
                double x = double.Parse(pointElement.Attributes["lon"].Value);
                double y = double.Parse(pointElement.Attributes["lat"].Value);
                DateTime? time = null;
                double? z = null;
                Dictionary<string, string> extensions = new Dictionary<string, string>();
                foreach (XmlElement child in pointElement.ChildNodes)
                {
                    switch (child.Name)
                    {
                        case "time" when !string.IsNullOrWhiteSpace(child.InnerText):
                            time = DateTime.Parse(child.InnerText, null, DateTimeStyles.AdjustToUniversal);
                            break;
                        case "ele" when !string.IsNullOrWhiteSpace(child.InnerText):
                            z = double.Parse(child.InnerText);
                            break;
                        default:
                            extensions.Add(child.Name, child.InnerText);
                            break;
                    }
                }
                GpxPoint point = new GpxPoint(x, y, z, time);
                point.Extensions = extensions;
                seg.Points.Add(point);
            }
        }

        private static void LoadTrack(GpxTrack track, XmlElement trackNode)
        {
            foreach (XmlElement element in trackNode.Attributes)
            {
                SetTrackValue(track, element.Name, element.Value);
            }
            foreach (XmlElement segmentElement in trackNode.GetElementsByTagName("trkseg"))
            {
                LoadSegment(track.CreateSegment(), segmentElement);
            }
        }

        private static void SetGpxValue(GpxDocument gpx, string key, string value)
        {
            if (xml2GpxProperty.TryGetValue(key, out Action<GpxDocument, string> func))
            {
                func(gpx, value);
            }
        }

        private static void SetTrackValue(GpxTrack track, string key, string value)
        {
            if (xml2TrackProperty.TryGetValue(key, out Action<GpxTrack, string> func))
            {
                func(track, value);
            }
        }
        #endregion

        #region Object => XML

        /// <summary>
        /// 获取GPX的XML字符串
        /// </summary>
        /// <returns></returns>
        public static string ToXmlString(this GpxDocument gpx)
        {
            XmlDocument doc = new XmlDocument();
            XmlDeclaration dec = doc.CreateXmlDeclaration("1.0", "utf-8", "no");
            doc.AppendChild(dec);

            XmlElement gpxElement = doc.CreateElement("gpx");
            doc.AppendChild(gpxElement);
            XmlElement metadataElement = doc.CreateElement("metadata");
            gpxElement.AppendChild(metadataElement);

            gpxElement.SetAttribute("version", "1.1");
            gpxElement.SetAttribute("creator", gpx.Creator);
            metadataElement.AppendElement("name", gpx.Name);
            metadataElement.AppendElement("author", gpx.Author);
            metadataElement.AppendElement("time", gpx.Time.ToString(GpxTimeFormat));

            var boundsElement = doc.CreateElement("bounds");
            var extent = gpx.GetPoints().GetExtent();
            boundsElement.AppendElement("minlat", extent.YMin.ToString());
            boundsElement.AppendElement("minlon", extent.XMin.ToString());
            boundsElement.AppendElement("maxlat", extent.YMax.ToString());
            boundsElement.AppendElement("maxlon", extent.XMax.ToString());
            metadataElement.AppendChild(boundsElement);

            var metadataExtensionsElement = doc.CreateElement("extensions");
            metadataExtensionsElement.AppendElement("distance", Math.Round(gpx.Tracks.Sum(p => p.GetPoints().GetDistance()), 2).ToString());
            var durations = gpx.Tracks.Select(p => p.GetPoints().GetDuration());
            if (durations.All(p => p.HasValue))
            {
                metadataExtensionsElement.AppendElement("duration", durations.Select(p => p.Value)
                    .Aggregate(TimeSpan.Zero, (current, duration) => current.Add(duration)).ToString());
            }
            foreach (var item in gpx.Extensions)
            {
                if (gpx.HiddenElements.Contains(item.Key))
                {
                    metadataElement.AppendElement(item.Key, item.Value);
                }
                else
                {
                    metadataExtensionsElement.AppendElement(item.Key, item.Value);
                }
            }
            metadataElement.AppendChild(metadataExtensionsElement);
            foreach (var trk in gpx.Tracks)
            {
                XmlElement node = doc.CreateElement("trk");
                trk.WriteTrackXml(node);
                gpxElement.AppendChild(node);
            }

            using var stringWriter = new StringWriter();
            XmlWriterSettings xmlSettingsWithIndentation = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
            };
            using var xmlTextWriter = XmlWriter.Create(stringWriter, xmlSettingsWithIndentation);
            doc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();
            return stringWriter.GetStringBuilder().ToString();
        }

        private static void AppendExtensions(IGpxElement gpxObj, XmlElement parentElement)
        {
            if (gpxObj.Extensions != null || gpxObj.Extensions.Count > 0)
            {
                XmlElement extensionElement = null;
                foreach (var extension in gpxObj.Extensions)
                {
                    if (gpxObj.HiddenElements.Contains(extension.Key))
                    {
                        parentElement.AppendElement(extension.Key, extension.Value);
                    }
                    else
                    {
                        if (extensionElement == null)
                        {
                            extensionElement = parentElement.OwnerDocument.CreateElement("extensions");
                            parentElement.AppendChild(extensionElement);
                        }
                        extensionElement.AppendElement(extension.Key, extension.Value);
                    }
                }
            }
        }

        private static void WriteSegmentXml(this GpxSegment seg, XmlElement segElement)
        {
            foreach (var point in seg.Points)
            {
                var pointElement = segElement.OwnerDocument.CreateElement("trkpt");
                segElement.AppendChild(pointElement);
                pointElement.SetAttribute("lon", point.X.ToString());
                pointElement.SetAttribute("lat", point.Y.ToString());
                if (point.Z.HasValue)
                {
                    pointElement.AppendElement("ele", point.Z.ToString());
                }
                if (point.Time.HasValue)
                {
                    pointElement.AppendElement("time", point.Time.Value.ToString(GpxTimeFormat));
                }
                AppendExtensions(point, pointElement);
            }
            AppendExtensions(seg, segElement);
        }

        private static void WriteTrackXml(this GpxTrack track, XmlElement trackElement)
        {
            trackElement.AppendElement("name", track.Name);
            trackElement.AppendElement("desc", track.Description);

            AppendExtensions(track, trackElement);
            foreach (var seg in track.Segments)
            {
                var segElement = trackElement.OwnerDocument.CreateElement("trkseg");
                trackElement.AppendChild(segElement);
                WriteSegmentXml(seg, segElement);
            }

        }
        #endregion
    }
}