﻿using MapBoard.Mapping;
using MapBoard.Model;
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

namespace MapBoard.Util
{
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

        /// <summary>
        /// HTTP服务器
        /// </summary>
        private static TcpListener tcpListener;

        public static void StartServer(int port, string filePathFormat, string extension)
        {
            IPAddress localaddress = IPAddress.Loopback;

            IPEndPoint endpoint = new IPEndPoint(localaddress, port);
            if (tcpListener == null)
            {
                // 创建Tcp 监听器
                tcpListener = new TcpListener(endpoint);
            }
            Task.Run(async () =>
            {
                // 启动监听
                tcpListener.Start();
                while (true)
                {
                    if (tcpListener.GetType().GetRuntimeProperties().ToArray()[1].GetValue(tcpListener).Equals(false))
                    {
                        return;
                    }
                    try
                    {
                        // 等待客户连接
                        TcpClient client = await tcpListener.AcceptTcpClientAsync();
                        // 获得一个网络流对象
                        // 该网络流对象封装了Socket的输入和输出操作
                        // 此时通过对网络流对象进行写入来返回响应消息
                        // 通过对网络流对象进行读取来获得请求消息
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                        Task.Run(() =>
                        {
                            try
                            {
                                SendPic(client, filePathFormat, extension);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                        });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                    }
                    catch (Exception ex)
                    {
                    }
                    // 关闭与客户端的连接
                    //client.Close();
                    // break;
                }
            });
        }

        private static void SendPic(TcpClient client, string fileFormat, string extension)
        {
            NetworkStream netstream = client.GetStream();
            // 把客户端的请求数据读入保存到一个数组中
            byte[] buffer = new byte[2048];

            int receivelength = netstream.Read(buffer, 0, 2048);
            string requeststring = Encoding.UTF8.GetString(buffer, 0, receivelength);

            Regex r = new Regex("GET /([0-9]+)-([0-9]+)-([0-9]+) HTTP");
            if (r.IsMatch(requeststring))
            {
                var match = r.Match(requeststring);
                int x = int.Parse(match.Groups[1].Value);
                int y = int.Parse(match.Groups[2].Value);
                int z = int.Parse(match.Groups[3].Value);
                string file = fileFormat
                    .Replace("{x}", x.ToString())
                    .Replace("{y}", y.ToString())
                    .Replace("{z}", z.ToString())
                    .Replace("{ext}", extension);
                //"Download\\{z}\\{x}-{y}.{Config.Instance.FormatExtension}";
                byte[] responseBodyBytes = null;
                string statusLine;
                if (File.Exists(file))
                {
                    statusLine = "HTTP/1.1 200 OK\r\n";
                    responseBodyBytes = File.ReadAllBytes(file);
                }
                else
                {
                    statusLine = "HTTP/1.1 404 Not Found\r\n";
                    //responseBodyBytes = Properties.Resources.ErrorPic;
                }

                // 服务器端做出相应内容
                // 响应的状态行;
                byte[] responseStatusLineBytes = Encoding.UTF8.GetBytes(statusLine);
                netstream.Write(responseStatusLineBytes, 0, responseStatusLineBytes.Length);
                //string responseBody = "<html><head><title>Default Page</title></head><body><p style='font:bold;font-size:24pt'>Welcome you</p></body></html>";
                if (responseBodyBytes != null)
                {
                    string responseHeader = $"Content-Type: image/{extension}; charset=UTf-8\r\nContent-Length: {responseBodyBytes.Length}\r\n";
                    byte[] responseHeaderBytes = Encoding.UTF8.GetBytes(responseHeader);
                    netstream.Write(responseHeaderBytes, 0, responseHeaderBytes.Length);
                    netstream.Write(new byte[] { 13, 10 }, 0, 2);
                    netstream.Write(responseBodyBytes, 0, responseBodyBytes.Length);
                }

                //}
                //else
                //{
                //    string statusLine = "HTTP/1.1 404 Not Found\r\n";
                //    byte[] responseStatusLineBytes = Encoding.UTF8.GetBytes(statusLine);
                //    netstream.Write(responseStatusLineBytes, 0, responseStatusLineBytes.Length);

                //}

                netstream.Flush();
            }
        }

        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public static void StopServer()
        {
            tcpListener.Stop();
        }
    }
}