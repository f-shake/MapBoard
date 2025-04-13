using Android.Runtime;
using CommunityToolkit.Maui.Alerts;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using GoogleGson;
using MapBoard.IO;
using MapBoard.Mapping;
using MapBoard.Views;

namespace MapBoard
{
    public partial class App : Application
    {
        public App()
        {
            Parameters.AppType = AppType.MAUI;
            ArcGISRuntimeEnvironment.EnableTimestampOffsetSupport = true;

            MauiExceptions.UnhandledException += MauiExceptions_UnhandledException;

            InitializeComponent();

            MainPage = new MainPage();
        }

        public static void SaveConfigsAndStatus()
        {
            Config.Instance.Save();
            var map = MainMapView.Current;
            if (map.IsLoaded
                && map.Layers != null
             && map.GetCurrentViewpoint(ViewpointType.BoundingGeometry)?.TargetGeometry is Envelope envelope)
            {
                var json = envelope.ToJson();
                if (map.Layers.MapViewExtentJson != json)
                {
                    map.Layers.MapViewExtentJson = json;
                }
            }
            try
            {
                map.Layers.Save();
            }
            catch(Exception ex)
            {
   //             System.ArgumentNullException: ArgumentNull_Generic Arg_ParamName_Name, o
   //at Newtonsoft.Json.Linq.JToken.FromObjectInternal(Object o, JsonSerializer jsonSerializer)
   //at Newtonsoft.Json.Linq.JArray.FromObject(Object o, JsonSerializer jsonSerializer)
   //at Newtonsoft.Json.Linq.JArray.FromObject(Object o)
   //at MapBoard.Model.LayerCollection.Save(String path)
   //at MapBoard.Mapping.Model.MapLayerCollection.Save()
   //at MapBoard.App.SaveConfigsAndStatus()
   //at MapBoard.MainActivity.OnPause()
   //at Android.App.Activity.n_OnPause(IntPtr jnienv, IntPtr native__this)
   //at Android.Runtime.JNINativeWrapper.Wrap_JniMarshal_PP_V(_JniMarshal_PP_V callback, IntPtr jnienv, IntPtr klazz)
            }
        }

        protected override void OnStart()
        {
            base.OnStart();
            Resources["DateTimeFormat"] = "{0:" + Parameters.TimeFormat + "}";
            Resources["DateFormat"] = "{0:" + Parameters.DateFormat + "}";
        }
        private void MauiExceptions_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string strEx = e.ExceptionObject.ToString();
            if (strEx.Contains("System.Net.Sockets")
                || strEx.Contains("Http2Connection")
                || strEx.Contains("Esri.ArcGISRuntime.Http.Caching.CacheManager"))
            {
                //FTP服务存在问题，打开后关闭应用，再次打开就会报错
                return;
            }
            File.WriteAllText(Path.Combine(FolderPaths.LogsPath, $"Crash_{DateTime.Now:yyyyMMdd_HHmmss}.log"),
                (e.ExceptionObject as Exception).ToString());
            //Toast.Make((e.ExceptionObject as Exception).ToString());
        }
    }
}
