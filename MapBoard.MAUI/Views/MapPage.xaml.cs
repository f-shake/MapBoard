using Esri.ArcGISRuntime.UI;
using MapBoard.Mapping;

namespace MapBoard.Views
{
    public partial class MapPage : ContentView
    {
        public MapPage()
        {
            InitializeComponent();
            HandleLongPress();
        }

        private void ContentPage_Loaded(object sender, EventArgs e)
        {
        }

        private void HandleLongPress()
        {
            Microsoft.Maui.Handlers.ImageButtonHandler.Mapper.AppendToMapping("MapPageImageButtonHandler", (handler, view) =>
            {

#if WINDOWS
                handler.PlatformView.Holding += (s,e)=>
                {
                };
#endif
#if ANDROID
                handler.PlatformView.LongClick += async (s, e) =>
                {
                    if (view == btnZoomIn)
                    {
                        var map = MainMapView.Current;
                        await map.SetViewpointScaleAsync(map.MapScale / 10);
                    }
                    else if (view == btnZoomOut)
                    {
                        var map = MainMapView.Current;
                        await map.SetViewpointScaleAsync(map.MapScale * 10);
                    }
                    else if (view == btnLocate && map.LocationDisplay != null)
                    {
                        var index = await PopupMenu.PopupMenuAsync(btnLocate,
                                   [
                                    new PopupMenu.PopupMenuItem("定位"),
                                    new PopupMenu.PopupMenuItem("导航"),
                                    new PopupMenu.PopupMenuItem("指南针"),
                                   ], "定位图标模式");
                        map.LocationDisplay.AutoPanMode = (LocationDisplayAutoPanMode)(1 + index);
                    }
                };
#endif
#if IOS
			handler.PlatformView.UserInteractionEnabled = true;  
			handler.PlatformView.AddGestureRecognizer(new UILongPressGestureRecognizer(HandleLongClick));  
#endif

            });
        }

        private void LocationButton_Click(object sender, EventArgs e)
        {
            map.LocationDisplay.AutoPanMode = LocationDisplayAutoPanMode.Recenter;
        }


        private async void ZoomInButton_Click(object sender, EventArgs e)
        {
            var map = MainMapView.Current;
            await map.SetViewpointScaleAsync(map.MapScale / 3);
        }

        private async void ZoomOutButton_Click(object sender, EventArgs e)
        {
            var map = MainMapView.Current;
            await map.SetViewpointScaleAsync(map.MapScale * 3);
        }
    }

}
