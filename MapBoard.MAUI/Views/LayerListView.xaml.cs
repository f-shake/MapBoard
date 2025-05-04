using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using System.Diagnostics;

namespace MapBoard.Views;

public partial class LayerListView : ContentView, ISidePanel
{
    private bool isLoaded = false;

    public LayerListView()
    {
        InitializeComponent();
        BindingContext = new LayerViewViewModel();

    }

    public SwipeDirection Direction => SwipeDirection.Left;

    public int Length => 300;

    public bool Standalone => false;

    public void OnPanelClosed()
    {
    }

    public void OnPanelOpening()
    {
    }

    private void ContentView_Loaded(object sender, EventArgs e)
    {
        if (isLoaded)
        {
            return;
        }
        isLoaded = true;
        MainMapView.Current.MapLoaded += MapView_MapLoaded;
        MainMapView.Current.Layers.CollectionChanged += Layers_CollectionChanged;
        MapView_MapLoaded(sender, e);
    }

    private void Layers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!MainMapView.Current.Layers.IsBatchLoading)
        {
            (BindingContext as LayerViewViewModel).Update();
        }
    }

    private async void lvwLevel_ItemTapped(object sender, ItemTappedEventArgs e)
    {
        PopupMenu.PopupMenuItem[] items = [
            new PopupMenu.PopupMenuItem("查询"),
            new PopupMenu.PopupMenuItem("设置"),
            new PopupMenu.PopupMenuItem("删除")
            ];
        var result = await (sender as ListView).PopupMenuAsync(e, items, "图层选项");
        if (result >= 0)
        {
            var layer = e.Item as IMapLayerInfo;
            switch (result)
            {
                case 0:
                    LayerQueryPopup p1 = new LayerQueryPopup(layer);
                    await MainPage.Current.ShowPopupAsync(p1);
                    break;
                case 1:
                    LayerStylePopup p2 = new LayerStylePopup(layer);
                    await MainPage.Current.ShowPopupAsync(p2);
                    break;
                case 2:
                    if (await MainPage.Current.DisplayAlert("移除图层", "是否移除选择的图层？", "确定", "取消"))
                    {
                        await MainMapView.Current.Layers.RemoveAsync(layer);
                    }
                    MainMapView.Current.Layers.Save();
                    break;
                default:
                    break;
            }
        }
    }

    private void MapView_MapLoaded(object sender, EventArgs e)
    {
        var layers = MainMapView.Current.Layers;
        (BindingContext as LayerViewViewModel).Layers = layers;
        (BindingContext as LayerViewViewModel).Update();
        rbtnByLevel.IsChecked = !Config.Instance.GroupLayers;
        rbtnByGroup.IsChecked = Config.Instance.GroupLayers;
    }


    private void UpdateListType(bool group)
    {
        //ListView自带Group存在渲染问题
        //https://github.com/dotnet/maui/issues/16031

        lvwGroups.IsVisible = group;
        lvwLayers.IsVisible = !group;
        Config.Instance.GroupLayers = group;
    }

    private void ViewTypeRadioButton_CheckChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            UpdateListType(sender == rbtnByGroup);
        }
    }
}