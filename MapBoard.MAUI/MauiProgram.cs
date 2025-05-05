using CommunityToolkit.Maui;
using Esri.ArcGISRuntime.Maui;
using Esri.ArcGISRuntime.Toolkit.Maui;
using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;
using MapBoard.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.Diagnostics;
using System.Net;
using UraniumUI;

namespace MapBoard
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder()
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseUraniumUI()
                .UseUraniumUIMaterial()
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android.OnCreate((activity, bundle) => MakeStatusBarTranslucent(activity)));

                    static void MakeStatusBarTranslucent(Android.App.Activity activity)
                    {
                        activity.Window.SetFlags(Android.Views.WindowManagerFlags.LayoutNoLimits, Android.Views.WindowManagerFlags.LayoutNoLimits);

                        activity.Window.ClearFlags(Android.Views.WindowManagerFlags.TranslucentStatus);

                        activity.Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
                    }
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseMauiCommunityToolkit(options =>
                {
                    options.SetShouldSuppressExceptionsInConverters(false);
                    options.SetShouldSuppressExceptionsInBehaviors(false);
                    options.SetShouldSuppressExceptionsInAnimations(false);
                })
                .UseArcGISRuntime()
                .UseArcGISToolkit();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
