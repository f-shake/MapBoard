using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using MapBoard.Mapping;
using MapBoard.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MapBoard.Util;

public static class NetUtility
{
    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="url"></param>
    /// <param name="path"></param>
    /// <param name="layerInfo"></param>
    /// <param name="timeOut"></param>
    /// <param name="userAgent"></param>
    /// <param name="proxyAddress"></param>
    /// <returns></returns>
    public static async Task HttpDownloadAsync(string url, string path,BaseLayerInfo layerInfo, TimeSpan timeOut, string userAgent, string proxyAddress)
    {
        string tempPath = Path.Combine(Path.GetDirectoryName(path), "temp");
        Directory.CreateDirectory(tempPath);  //创建临时文件目录
        string tempFile = Path.Combine(tempPath, Guid.NewGuid().ToString("N")); //临时文件
        try
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);    //存在则删除
            }

            using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                if (!string.IsNullOrWhiteSpace(proxyAddress))
                {
                    var proxy = new WebProxy
                    {
                        Address = new Uri(proxyAddress),
                        BypassProxyOnLocal = false,
                    };
                    httpClientHandler.Proxy = proxy;
                }
                using HttpClient client = new HttpClient(httpClientHandler);
                XYZTiledLayer.ApplyHttpClientHeaders(client, layerInfo, userAgent);
                client.Timeout = timeOut;
                using var responseStream = await client.GetStreamAsync(url);

                byte[] bArr = new byte[1024 * 1024];
                int size = responseStream.Read(bArr, 0, bArr.Length);
                while (size > 0)
                {
                    fs.Write(bArr, 0, size);
                    size = responseStream.Read(bArr, 0, bArr.Length);
                }
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            int tryTimes = 0;
            while (File.Exists(path))
            {
                if (++tryTimes >= 10)
                {
                    throw new IOException("尝试删除已存在的文件失败");
                }
                Thread.Sleep(100);
            }

            File.Move(tempFile, path);
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
        }
    }

    private static WebServer server;
    private static CancellationTokenSource cts;

    private static readonly SemaphoreSlim _connectionLimiter = new SemaphoreSlim(10);
    public static void StartServer(int port, string dir, string extension)
    {
        cts = new CancellationTokenSource();
        var url = $"http://127.0.0.1:{port}/";

        // 创建 WebServer
        server = new WebServer(o => o
                .WithUrlPrefix(url)
                .WithMode(HttpListenerMode.EmbedIO))
            // 添加静态文件模块（服务瓦片图片）
            .WithStaticFolder("/", Path.GetFullPath(dir), false);

        // 启动服务器（非阻塞）
        _ = server.RunAsync(cts.Token);

        Console.WriteLine($"服务器已启动，访问地址: {url}");
    }

    /// <summary>
    /// 停止 HTTP 服务器
    /// </summary>
    public static void StopServer()
    {
        cts?.Cancel();
        server?.Dispose();
        Console.WriteLine("服务器已停止");
    }
}