#if ANDROID
using Android.App;
using Android.Content;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using Google.Android.Material.TextField;
#endif
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.ViewModels;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Application = Microsoft.Maui.Controls.Application;

namespace MapBoard.Views;

public partial class BaseLayerView : ContentView
{
    public BaseLayerView()
    {
        InitializeComponent();
        BindingContext = new BaseLayerViewViewModel();

    }
    private void ContentView_Loaded(object sender, EventArgs e)
    {

    }
#if ANDROID
    private void AddNewBaseLayerButton_Clicked(object sender, EventArgs e)
#else
    private async void AddNewBaseLayerButton_Clicked(object sender, EventArgs e)
#endif
    {
#if ANDROID
        var layout = new LinearLayoutCompat(Platform.CurrentActivity);
        layout.Orientation = LinearLayoutCompat.Vertical;
        layout.SetPadding(8, 8, 8, 8);

        var nameEdit = new TextInputEditText(Platform.CurrentActivity);
        nameEdit.SetSingleLine();
        nameEdit.Hint = "ͼ����";
        layout.AddView(nameEdit);

        var urlEdit = new TextInputEditText(Platform.CurrentActivity);
        urlEdit.SetSingleLine();
        urlEdit.Hint = "��Ƭͼ���ַ";
        layout.AddView(urlEdit);

        new AlertDialog.Builder(Platform.CurrentActivity)
             .SetTitle("������ͼ")
             .SetView(layout)
             .SetPositiveButton("ȷ��", new EventHandler<DialogClickEventArgs>(async (s, e) =>
             {
                 var name = nameEdit.Text;
                 var url = urlEdit.Text;
                 if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(name))
                 {
                     await AddLayerAsync(name, url);
                 }
             }))
             .SetCancelable(true)
             .Create()
             .Show();
#else
        string url = await MainPage.Current.DisplayPromptAsync("������ͼ", "����XYZ��Ƭ��ͼ����", "ȷ��", "ȡ��");
        if (!string.IsNullOrEmpty(url))
        {
            (BindingContext as BaseLayerViewViewModel).BaseLayers.Add(new Model.BaseLayerInfo()
            {
                Path = url,
                Type = Model.BaseLayerType.WebTiledLayer
            });
        }
#endif
    }

    private async Task AddLayerAsync(string name, string url)
    {
        (BindingContext as BaseLayerViewViewModel).BaseLayers.Insert(0, new Model.BaseLayerInfo()
        {
            Name = name,
            Path = url,
            Type = Model.BaseLayerType.WebTiledLayer
        });
        (BindingContext as BaseLayerViewViewModel).Save();
        await MainMapView.Current.LoadAsync();
    }

    private void DeleteSwipeItem_Clicked(object sender, EventArgs e)
    {

    }
}