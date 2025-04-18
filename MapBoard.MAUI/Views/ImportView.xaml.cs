using MapBoard.IO;
using MapBoard.Mapping;
using MapBoard.Models;
using MapBoard.Services;
using MapBoard.Util;
using MapBoard.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace MapBoard.Views;

public partial class ImportView : ContentView, ISidePanel
{
    public ImportView()
    {
        BindingContext = new ImportViewVideModel();
        InitializeComponent();

    }

    public SwipeDirection Direction => SwipeDirection.Left;

    public int Length => 300;

    public bool Standalone => false;

    public async void OnPanelOpened()
    {
        await (BindingContext as ImportViewVideModel).LoadFilesAsync();
    }

    private async Task ImportAsync(string file)
    {
        var handle = ProgressPopup.Show("正在导入地图");
        try
        {
            await Package.ImportMapAsync(file, MainMapView.Current.Layers);
            MainPage.Current.ClosePanel<ImportView>();
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("加载失败", ex.Message, "确定");
        }
        finally
        {
            handle.Close();
        }
    }

    private async void ImportButton_Clicked(object sender, EventArgs e)
    {
        PickOptions options = new PickOptions()
        {
            PickerTitle = "选取mbmpkg地图包文件",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>()
            {
                [DevicePlatform.Android] = ["application/octet-stream"],
                [DevicePlatform.WinUI] = ["*.mbmpkg"]
            })
        };
        var file = await FilePicker.Default.PickAsync(options);
        if (file != null)
        {
            await ImportAsync(file.FullPath);
        }
    }

    private async void ListView_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        var file = e.Item as SimpleFile;
        await ImportAsync(file.FullName);
    }
}