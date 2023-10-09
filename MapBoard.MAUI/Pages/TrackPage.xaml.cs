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

namespace MapBoard.Pages;

public partial class TrackPage : ContentPage
{
    public TrackPage()
    {
        InitializeComponent();
        BindingContext = new TrackPageViewModel();
        TrackService.ExceptionThrown += TrackService_ExceptionThrown;
    }

    private void TrackService_ExceptionThrown(object sender, ThreadExceptionEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Shell.Current.IsVisible)
            {
                Shell.Current.DisplayAlert("�켣��¼ʧ��", e.Exception.Message, "ȷ��");
                StopTrack(false);
                UpdateButtonsVisible(false, false);
            }
        });
    }

    private async void ContentPage_Loaded(object sender, EventArgs e)
    {
        UpdateButtonsVisible(TrackService.Current != null, false);
        if (TrackService.Current == null && Config.Instance.IsTracking)
        {
            var gpxFiles = Directory.EnumerateFiles(FolderPaths.TrackPath, "*.gpx").OrderDescending();
            if (gpxFiles.Any())
            {
                if (await DisplayAlert("������¼�켣", "�켣��¼�����˳����Ƿ������", "����", "ȡ��"))
                {
                    StartTrack(gpxFiles.First());
                    UpdateButtonsVisible(true, false);
                }
                else
                {
                    Config.Instance.IsTracking = false;
                }
            }
        }
    }

    private void ContentPage_Unloaded(object sender, EventArgs e)
    {
        Config.Instance.Save();
        MainMapView.Current.Layers.Save();
    }

    private void PauseButton_Clicked(object sender, EventArgs e)
    {
        StopTrack(true);
        UpdateButtonsVisible(true, true);
    }

    private void ResumeButton_Clicked(object sender, EventArgs e)
    {
        StartTrack();
        UpdateButtonsVisible(true, false);
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
            UpdateButtonsVisible(true, false);
        }
        catch (Exception ex)
        {
            await DisplayAlert("��ʼ��¼�켣ʧ��", ex.Message, "ȷ��");
        }
    }
    private void StopButton_Clicked(object sender, EventArgs e)
    {
        if (TrackService.Current == null) //��ͣ��ֹͣ
        {
            Config.Instance.IsTracking = false;
        }
        else //ֱ��ֹͣ
        {
            StopTrack(false);
        }
        UpdateButtonsVisible(false, false);
    }

    private void StopTrack(bool pause)
    {
        if (pause)
        {
            TrackService.Current.PutPausingFlag();
        }
#if ANDROID
        (Platform.CurrentActivity as MainActivity).StopTrackService();
#else
        TrackService.Current.Stop();
#endif
    }
    private void UpdateButtonsVisible(bool running, bool pausing)
    {
        btnStart.IsVisible = !running;
        grdStopAndPause.IsVisible = running;
        btnPause.IsVisible = !pausing;
        btnResume.IsVisible = pausing;
        lblPausing.IsVisible = pausing;
        grdDetail.IsVisible = running;
        lvwGpxList.IsVisible = !running;
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
            await DisplayAlert("����ʧ��", ex.Message, "ȷ��");
        }
    }

}
