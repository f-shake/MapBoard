#if ANDROID
using Android.App;
using Android.Net;
using Android.Net.Wifi;
#endif
using FubarDev.FtpServer.FileSystem.DotNet;
using FubarDev.FtpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using MapBoard.IO;

namespace MapBoard.Services
{
    public class FtpService
    {
        private IFtpServerHost ftpServerHost;
        private ServiceCollection services;
        private ServiceProvider serviceProvider;
        public FtpService(string dir)
        {
            services = new ServiceCollection();

            services.AddFtpServer(builder => builder
                .UseDotNetFileSystem()
                .EnableAnonymousAuthentication());

            services.Configure<DotNetFileSystemOptions>(opt => opt.RootPath = dir);
            services.Configure<FtpServerOptions>(opt =>
            {
                opt.ServerAddress = "0.0.0.0";
                opt.Port = 2222;
            });

            serviceProvider = services.BuildServiceProvider();

            ftpServerHost = serviceProvider.GetRequiredService<IFtpServerHost>();
        }
        public static IEnumerable<string> GetIpAddress()
        {
#if ANDROID
            var manager = Android.App.Application.Context.GetSystemService(Service.ConnectivityService);
            if (manager is ConnectivityManager m)
            {
                var link = m.GetLinkProperties(m.ActiveNetwork);
                foreach (var address in link.LinkAddresses.OrderBy(p => p.PrefixLength))
                {
                    yield return address.Address.HostAddress;
                }
            }
#elif WINDOWS
            string hostName = Dns.GetHostName();
            foreach (var address in Dns.GetHostEntry(hostName).AddressList)
            {
                yield return address.ToString();
            }
#else
            return null;
#endif
        }

        public Task StartServerAsync()
        {
            return ftpServerHost.StartAsync();
        }

        public async Task StopServerAsync()
        {
            await ftpServerHost.StopAsync();
            serviceProvider.Dispose();
        }
    }
}
