using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Util;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class SettingPopup : Popup
{
    public SettingPopup()
    {
        BindingContext = Config.Instance;
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, EventArgs e)
    {
        Config.Instance.Save();
        this.TryClose();
    }
    protected override Task OnDismissedByTappingOutsideOfPopup(CancellationToken token = default)
    {
        Config.Instance.Save();
        return base.OnDismissedByTappingOutsideOfPopup(token);
    }

    private async void AboutButton_Click(object sender, EventArgs e)
    {
        Uri uri = new Uri("https://github.com/f-shake/MapBoard");
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }
}