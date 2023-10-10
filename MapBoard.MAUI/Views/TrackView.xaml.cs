#if ANDROID
using Android.Content;
using Android.App;
using MapBoard.Platforms.Android;
using Application = Android.App.Application;
#endif
using MapBoard.Services;
using MapBoard.ViewModels;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls.PlatformConfiguration;
using MapBoard.IO;
using MapBoard.Mapping;
using MapBoard.IO.Gpx;
using Esri.ArcGISRuntime.Geometry;

namespace MapBoard.Views;

public partial class TrackView : ContentView
{
    public TrackView()
    {
        InitializeComponent();
        BindingContext = new TrackViewViewModel();

        //�켣��¼���쳣���ڴ˴�����
        TrackService.ExceptionThrown += TrackService_ExceptionThrown;

        //����TrackService.Current�����ı䣬���켣��¼�Ŀ�ʼ��ֹͣ�¼�
        TrackService.StaticPropertyChanged += TrackService_StaticPropertyChanged;

        //�켣��¼������GPX����󣬸���GPX�ļ��б�
        TrackService.GpxSaved += TrackService_GpxSaved;
    }

    private async void ContentPage_Loaded(object sender, EventArgs e)
    {
        await (BindingContext as TrackViewViewModel).LoadGpxFilesAsync();
        UpdateButtonsVisible(TrackService.Current != null);
    }

    private void ContentPage_Unloaded(object sender, EventArgs e)
    {
        Config.Instance.Save();
        MainMapView.Current.Layers.Save();
    }

    private async void GpxList_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        try
        {
            var file = e.Item as FileInfo;
            Gpx gpx = await Gpx.FromFileAsync(file.FullName);

            var overlay = MainMapView.Current.TrackOverlay;
            overlay.Graphics.Clear();
            var points = gpx.Tracks.Select(p => p.Points.Select(q => new MapPoint(q.X, q.Y)));
            var line = new Polyline(points, SpatialReferences.Wgs84);
            overlay.Graphics.Add(new Esri.ArcGISRuntime.UI.Graphic(line));
            await Shell.Current.GoToAsync("//MainPage");
            await Task.Delay(500);//�����ȵ������ŵ������λ�ã���Ҫ�ܿ�
            await MainMapView.Current.ZoomToGeometryAsync(line);
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("����ʧ��", ex.Message, "ȷ��");
        }
    }

    private async void ResumeButton_Clicked(object sender, EventArgs e)
    {
        var gpxs = (BindingContext as TrackViewViewModel).GpxFiles;
        if (!gpxs.Any())
        {
            await MainPage.Current.DisplayAlert("�޷�����", "���������еĹ켣", "ȷ��");
            return;
        }
        StartTrack(gpxs[0].FullName);
        //UpdateButtonsVisible(true);
    }

    private void StartTrack(string resume = null)
    {
        if (resume != null)
        {
            TrackService.ResumeGpx = resume;
        }
#if ANDROID
        (Platform.CurrentActivity as MainActivity).StartTrackService();
#else
        var trackService = new TrackService();
        trackService.Start();
#endif
    }

    private async void StartTrackButton_Click(object sender, EventArgs e)
    {
        try
        {
            Config.Instance.IsTracking = true;
            StartTrack();
            //UpdateButtonsVisible(true);
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("��ʼ��¼�켣ʧ��", ex.Message, "ȷ��");
        }
    }

    private void StopButton_Clicked(object sender, EventArgs e)
    {
        StopTrack();
        //UpdateButtonsVisible(false);
    }

    private void StopTrack()
    {
#if ANDROID
        (Platform.CurrentActivity as MainActivity).StopTrackService();
#else
        TrackService.Current.Stop();
#endif
    }

    private void TrackService_ExceptionThrown(object sender, ThreadExceptionEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (MainPage.Current.IsVisible)
            {
                MainPage.Current.DisplayAlert("�켣��¼ʧ��", e.Exception.Message, "ȷ��");
                StopTrack();
                UpdateButtonsVisible(false);
            }
        });
    }

    private void TrackService_GpxSaved(object sender, TrackService.GpxSavedEventArgs e)
    {
        var gpxs = (BindingContext as TrackViewViewModel)?.GpxFiles;
        if (gpxs != null && (gpxs.Count == 0 || gpxs[0].FullName != e.FilePath))
        {
            gpxs.Insert(0, new FileInfo(e.FilePath));
        }
    }

    private void TrackService_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackService.Current))
        {
            UpdateButtonsVisible(TrackService.Current != null);
        }
    }
    private void UpdateButtonsVisible(bool running)
    {
        btnStart.IsVisible = !running;
        btnStop.IsVisible = running;
        btnResume.IsVisible = !running;
        btnResume.IsEnabled = (BindingContext as TrackViewViewModel).GpxFiles.Count > 0;
        grdDetail.IsVisible = running;
        lvwGpxList.IsVisible = !running;
    }
}
