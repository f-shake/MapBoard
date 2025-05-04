using FzLib;
using MapBoard.IO;
using MapBoard.Models;
using MapBoard.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MapBoard.ViewModels;

public class TrackViewViewModel : INotifyPropertyChanged
{
    private ObservableCollection<GpxAndFileInfo> gpxFiles = new ObservableCollection<GpxAndFileInfo>();
    private string status;

    private TrackService trackService;

    public TrackViewViewModel()
    {
        TrackService.CurrentChanged += TrackService_StaticPropertyChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<GpxAndFileInfo> GpxFiles
    {
        get => gpxFiles;
        set => this.SetValueAndNotify(ref gpxFiles, value, nameof(GpxFiles));
    }

    public string Status
    {
        get => status;
        set => this.SetValueAndNotify(ref status, value, nameof(Status));
    }

    public TrackService TrackService
    {
        get => trackService;
        set => this.SetValueAndNotify(ref trackService, value, nameof(TrackService));
    }

    public async Task LoadGpxFilesAsync()
    {
        GpxFiles.Clear();
        foreach (var file in Directory
            .EnumerateFiles(FolderPaths.TrackPath, "*.gpx")
            .OrderDescending()
            .Take(50))
        {
            try
            {
                GpxFiles.Add(new GpxAndFileInfo(file));
            }
            catch (Exception ex)
            {

            }
        }
        foreach (var gpxFile in GpxFiles)
        {
            try
            {
                await gpxFile.LoadGpxAsync();
            }
            catch (Exception ex)
            {

            }
        }
    }
    private void TrackService_StaticPropertyChanged(object sender, EventArgs e)
    {
        TrackService = TrackService.Current;
    }
}
