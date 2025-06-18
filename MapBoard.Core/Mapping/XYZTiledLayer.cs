using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using FzLib.Collection;
using ImageMagick;
using MapBoard.IO;
using MapBoard.Model;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static FzLib.Program.Runtime.SimplePipe;

namespace MapBoard.Mapping
{
    /// <summary>
    /// XYZ瓦片图层，WebTiledLayer的更灵活的实现
    /// </summary>
    public class XYZTiledLayer : ImageTiledLayer
    {
        private readonly ConcurrentDictionary<string, TileCacheEntity> cacheDic = new ConcurrentDictionary<string, TileCacheEntity>();
        private readonly ConcurrentQueue<TileCacheEntity> cacheQueue = new ConcurrentQueue<TileCacheEntity>();
        private readonly HttpClient httpClient;
        private XYZTiledLayer(BaseLayerInfo layerInfo, string userAgent, Esri.ArcGISRuntime.ArcGISServices.TileInfo tileInfo, Envelope fullExtent, bool enableCache) : base(tileInfo, fullExtent)
        {
            TemplateUrl = layerInfo.Path;
            EnableCache = enableCache;

            var socketsHttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            };
            httpClient = new HttpClient(socketsHttpHandler)
            {
                Timeout = TimeSpan.FromSeconds(5),
            };
            ApplyHttpClientHeaders(httpClient, layerInfo, userAgent);
            StartSavingCache();
        }

        ~XYZTiledLayer()
        {
            httpClient.Dispose();
        }

        /// <summary>
        /// 是否启用缓存机制
        /// </summary>
        public bool EnableCache { get; }

        /// <summary>
        /// 最大缩放比例
        /// </summary>
        public int MaxLevel { get; set; } = -1;

        /// <summary>
        /// 最小缩放比例
        /// </summary>
        public int MinLevel { get; set; } = -1;

        /// <summary>
        /// 瓦片地址的模板链接
        /// </summary>
        public string TemplateUrl { get; }

        /// <summary>
        /// 应用Http客户端的请求头
        /// </summary>
        /// <param name="client"></param>
        /// <param name="layerInfo"></param>
        /// <param name="defaultUserAgent"></param>
        public static void ApplyHttpClientHeaders(HttpClient client, BaseLayerInfo layerInfo, string defaultUserAgent)
        {
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            if (!string.IsNullOrWhiteSpace(layerInfo.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(layerInfo.UserAgent);
                //client.DefaultRequestHeaders.Add("User-Agent", layerInfo.UserAgent);
            }
            else if (!string.IsNullOrEmpty(defaultUserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(defaultUserAgent);
                //client.DefaultRequestHeaders.Add("User-Agent", defaultUserAgent);
            }
            if (!string.IsNullOrWhiteSpace(layerInfo.Host))
            {
                client.DefaultRequestHeaders.Add("Host", layerInfo.Host);
            }
            if (!string.IsNullOrWhiteSpace(layerInfo.Origin))
            {
                client.DefaultRequestHeaders.Add("Origin", layerInfo.Origin);
            }
            if (!string.IsNullOrWhiteSpace(layerInfo.Referer))
            {
                client.DefaultRequestHeaders.Add("Referer", layerInfo.Referer);
            }
            if (!string.IsNullOrWhiteSpace(layerInfo.OtherHeaders))
            {
                foreach (var headerAndValue in layerInfo.OtherHeaders.Split('|').Select(p => p.Split(':')))
                {
                    if (headerAndValue.Length >= 2)
                    {
                        client.DefaultRequestHeaders.Add(headerAndValue[0], headerAndValue[1]);
                    }
                }
            }
        }

        /// <summary>
        /// 创建图层
        /// </summary>
        /// <param name="layerInfo"></param>
        /// <param name="userAgent"></param>
        /// <param name="enableCache"></param>
        /// <returns></returns>
        public static XYZTiledLayer Create(BaseLayerInfo layerInfo, string userAgent, bool enableCache = false)
        {
            string url = layerInfo.Path;
            var webTiledLayer = new WebTiledLayer(url.Replace("{x}", "{col}").Replace("{y}", "{row}").Replace("{z}", "{level}"));
            return new XYZTiledLayer(layerInfo, userAgent, webTiledLayer.TileInfo, webTiledLayer.FullExtent, enableCache);
        }

        /// <summary>
        /// 创建图层
        /// </summary>
        /// <param name="url"></param>
        /// <param name="userAgent"></param>
        /// <param name="enableCache"></param>
        /// <returns></returns>
        public static XYZTiledLayer Create(string url, string userAgent, bool enableCache = false)
        {
            return Create(new BaseLayerInfo(BaseLayerType.WebTiledLayer, url), userAgent, enableCache);
        }

        public void StartSavingCache()
        {
            PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            new TaskFactory().StartNew(async () =>
            {
                while (await timer.WaitForNextTickAsync())
                {
                    if (!EnableCache)
                    {
                        return;
                    }
                    try
                    {
                        if (!cacheQueue.IsEmpty)
                        {
                            List<string> removedUrls = new List<string>();
                            Debug.WriteLine($"有{cacheQueue.Count}张瓦片正在缓存");
                            using (var db = new TileCacheDbContext())
                            {
                                while (cacheQueue.TryDequeue(out TileCacheEntity tile))
                                {
                                    db.Tiles.Add(tile);
                                    removedUrls.Add(tile.TileUrl);
                                }
                                await db.SaveChangesAsync();
                            }
                            foreach (var url in removedUrls)
                            {
                                var removedResult = cacheDic.TryRemove(url, out _);
                                Debug.Assert(removedResult);
                            }
                            Debug.WriteLine($"瓦片缓存完成，cacheDic.Count={cacheDic.Count}");
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// 获取指定的瓦片图层
        /// </summary>
        /// <param name="level"></param>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<ImageTileData> GetTileDataAsync(int level, int row, int column, CancellationToken cancellationToken)
        {
            if (MaxLevel >= 0 && level > MaxLevel || MinLevel >= 0 && level < MinLevel)
            {
                return null;
            }

            TileCacheEntity cache = null;
            //TileCacheDbContext dbContext = null;
            byte[] data = null;
            int tryCount = 0;
            while (data == null && tryCount < 3)
            {
                try
                {
                    tryCount++;
                    if (EnableCache)
                    {
                        using var dbContext = new TileCacheDbContext();
                        cache = await dbContext.Tiles
                             .Where(p => p.TemplateUrl == TemplateUrl && p.X == column && p.Y == row && p.Z == level)
                             .FirstOrDefaultAsync(cancellationToken);
                    }
                    if (cache != null)
                    {
                        data = cache.Data;
                    }
                    else
                    {
                        string url = TemplateUrl.Replace("{x}", column.ToString()).Replace("{y}", row.ToString()).Replace("{z}", level.ToString());
                        using var response = await httpClient.GetAsync(url, cancellationToken);
                        using var content = response.EnsureSuccessStatusCode().Content;
                        data = await content.ReadAsByteArrayAsync(cancellationToken);
                        data = await ConvertToValidImageFormatAsync(data);

                        if (EnableCache)
                        {
                            TileCacheEntity tileCache = null;
                            if (cacheDic.TryGetValue(url, out tileCache))
                            {
                                data = tileCache.Data;
                            }
                            else
                            {
                                tileCache = new TileCacheEntity()
                                {
                                    X = column,
                                    Y = row,
                                    Z = level,
                                    TemplateUrl = TemplateUrl,
                                    TileUrl = url,
                                    Data = data
                                };
                                cacheQueue.Enqueue(tileCache);
                                cacheDic.TryAdd(url, tileCache);
                                //dbContext.Tiles.Add(tileCache);
                                //await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (tryCount >= 3)
                    {
                        return null;
                    }
                    else
                    {
                        continue;
                    }
                }
                if (data == null)
                {
                    await Task.Delay(500);
                }
            }
            return new ImageTileData(level, row, column, data, "");
        }

        private static bool IsJpg(byte[] bytes)
        {
            if (bytes.Length < 2) return false;
            // JPG签名: FF D8
            return bytes[0] == 0xFF &&
                   bytes[1] == 0xD8;
        }

        private static bool IsPng(byte[] bytes)
        {
            if (bytes.Length < 8) return false;
            // PNG签名: 89 50 4E 47 0D 0A 1A 0A
            return bytes[0] == 0x89 &&
                   bytes[1] == 0x50 &&
                   bytes[2] == 0x4E &&
                   bytes[3] == 0x47 &&
                   bytes[4] == 0x0D &&
                   bytes[5] == 0x0A &&
                   bytes[6] == 0x1A &&
                   bytes[7] == 0x0A;
        }

        private async Task<byte[]> ConvertToValidImageFormatAsync(byte[] imageBytes)
        {
            //ArcGIS Maps SDK仅支持JPG或PNG
            if (IsJpg(imageBytes) || IsPng(imageBytes))
            {
                return imageBytes;
            }
            if (false || OperatingSystem.IsWindows())
            {
                using var image = new MagickImage(imageBytes);
                if (image.Format is MagickFormat.Jpg or MagickFormat.Png or MagickFormat.Png
                    or MagickFormat.Png00 or MagickFormat.Png24 or MagickFormat.Png32
                    or MagickFormat.Png48 or MagickFormat.Png64 or MagickFormat.Png8)
                {
                    return imageBytes;
                }
                image.Format = MagickFormat.Png;
                using var ms = new MemoryStream();
                image.Write(ms);
                return ms.ToArray();
            }
            else
            {
                using var image = Image.Load(imageBytes);
                using var ms = new MemoryStream();
                await image.SaveAsPngAsync(ms);
                return ms.ToArray();
            }
        }
    }
}
