//#define PipeTest
using FzLib.Program.Runtime;
using FzLib.WPF.Dialog;
using ModernWpf.Controls;
using ModernWpf.FzExtension;
using ModernWpf.FzExtension.CommonDialog;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using MapBoard.UI;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ModernWpf.Controls.Primitives;
using System.Windows.Media;
using MapBoard.UI.GpxToolbox;
using MapBoard.UI.TileDownloader;
using log4net.Appender;
using MapBoard.Util;
using System.Xml;
using log4net.Layout;
using System.Reflection;
using System.Diagnostics;
using MapBoard.IO;
using MapBoard.IO.Formats.Gps;
using MapBoard.Mapping;
using Esri.ArcGISRuntime;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace MapBoard
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 用于保证同时只运行一个进程的单例检测
        /// </summary>
        private SingleInstance singleInstance;

        public App()
        {
            AppContext.SetSwitch("Switch.System.Windows.Media.EnableHardwareAccelerationInRdp", true);
            Parameters.AppType = AppType.WPF;
            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;

            try
            {
                SplashWindow.CreateAndShow();
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 日志系统
        /// </summary>
        public static ILog Log { get; private set; }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Log.Info("程序正常关闭");
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
#if !DEBUG
            UnhandledException.RegistAll();
            UnhandledException.UnhandledExceptionCatched += UnhandledException_UnhandledExceptionCatched;
#endif
            InitializeLogs();


            //单例检测
            singleInstance = new SingleInstance(FzLib.Program.App.ProgramName);
            //await singleInstance.CheckAndOpenWindow<MainWindow>(this);
            if (singleInstance.ExistAnotherInstance)
            {
                Log.Info("已存在正在运行的程序");
                if (e.Args.Length > 0)
                {
                    string arg = e.Args[0];

                    if (arg.EndsWith(".gpx"))
                    {
                        PipeHelper.LoadGpxs(e.Args);
                        //pipe.SendGpxAsync(e.Args);
                    }
                }
                Environment.Exit(0);
                return;
            }
            PipeHelper.StartHost();


            System.IO.Directory.SetCurrentDirectory(FzLib.Program.App.ProgramDirectoryPath);

            //设置日期时间格式
            Model.FeatureAttribute.DateFormat = Parameters.DateFormat;
            Model.FeatureAttribute.DateTimeFormat = Parameters.TimeFormat;
            Parameters.GpxSpeedSmoothWindow = Config.Instance.GpxSpeedSmoothWindow;
            Resources["DateTimeFormat"] = Parameters.TimeFormat;
            Resources["DateFormat"] = Parameters.DateFormat;

            //设置属性
            Config.Instance.ThemeChanged += (p1, p2) =>
                Theme.SetTheme(Config.Instance.Theme);
            WindowBase.WindowCreated += (p1, p2) =>
                Theme.SetTheme(Config.Instance.Theme, p1 as Window);
            SnakeBar.DefaultOwner = new WindowOwner(true);

            //初始化数据库
            await TileCacheDbContext.InitializeAsync();

            try
            {
                if (e.Args.Length > 0)
                {
                    string arg = e.Args[0];
                    MainWindow = arg switch
                    {
                        "tile" => await MainWindowBase.CreateAndShowAsync<TileDownloaderWindow>(),
                        "gpx" => await MainWindowBase.CreateAndShowAsync<GpxWindow>(),
                        string s when s.EndsWith(".mbmpkg") => await MainWindowBase.CreateAndShowAsync<MainWindow>(w => w.LoadFile = arg),
                        string s when s.EndsWith(".gpx") => await MainWindowBase.CreateAndShowAsync<GpxWindow>(w => w.LoadFiles = e.Args),
                        _ => await MainWindowBase.CreateAndShowAsync<MainWindow>(),
                    };
                }
                else
                {
                    MainWindow = await MainWindowBase.CreateAndShowAsync<MainWindow>();
                }
            }
            //检测C++库
            catch (TargetInvocationException ex) when (ex.InnerException?.InnerException?.InnerException?.InnerException is DllNotFoundException)
            {
                SplashWindow.EnsureInvisible();
                Log.Error("启动失败", ex);

                var result = MessageBox.Show("启动失败，请确保C++2015-2019或更新版本的x86和x64已安装，检查ArcGIS相关dll。" + Environment.NewLine + ex.InnerException?.InnerException?.InnerException?.InnerException.Message + Environment.NewLine + "是否跳转到C++下载界面？", "MapBoard", MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string url = "https://docs.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist";
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    catch
                    {
                    }
                }
                Shutdown();
                return;
            }

            //设置UI为压缩模式
            var xcr = Resources.MergedDictionaries.OfType<XamlControlsResources>().FirstOrDefault();
            if (xcr != null)
            {
                xcr.UseCompactResources = true;
            }
        }

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        private void InitializeLogs()
        {
            Log = LogManager.GetLogger(GetType());
            RollingFileAppender fa = new RollingFileAppender()
            {
                AppendToFile = true,
                MaxSizeRollBackups = 100,
                StaticLogFileName = true,
                RollingStyle = RollingFileAppender.RollingMode.Date,
                Layout = new PatternLayout("[%date]-[%thread]-[%-p]-[%logger]-[%M] -> %message%newline%newline"),
                File = System.IO.Path.Combine(FolderPaths.LogsPath, "MapBoard.log"),
            };
            fa.ActivateOptions();
            ((log4net.Repository.Hierarchy.Logger)Log.Logger).AddAppender(fa);

            Log.Info("程序启动");
        }

        /// <summary>
        /// 未捕获异常处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnhandledException_UnhandledExceptionCatched(object sender, FzLib.Program.Runtime.UnhandledExceptionEventArgs e)
        {
            try
            {
                Log.Error("未捕获的异常", e.Exception);
                Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show("程序发生异常，可能出现数据丢失等问题。是否关闭？" + Environment.NewLine + Environment.NewLine + e.Exception.ToString(), "MapBoard - 未捕获的异常", MessageBoxButton.YesNo, MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        Shutdown(-1);
                    }
                });
            }
            catch (Exception ex)
            {
            }
        }
    }
}