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
            new PopupMenu.PopupMenuItem("��ʽ����"),
            new PopupMenu.PopupMenuItem("ɾ��")
            ];
        var result = await (sender as ListView).PopupMenuAsync(e,items, "ͼ��ѡ��");
        if (result >= 0)
        {
            var layer = e.Item as IMapLayerInfo;
            switch (result)
            {
                case 0:
                    LayerStylePopup p = new LayerStylePopup(layer);
                    await MainPage.Current.ShowPopupAsync(p);
                    break;
                case 1:
                    if (await MainPage.Current.DisplayAlert("�Ƴ�ͼ��", "�Ƿ��Ƴ�ѡ���ͼ�㣿", "ȷ��", "ȡ��"))
                    {
                        MainMapView.Current.Layers.Remove(layer);
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
        //if (false && Config.Instance.LastLayerListGroupType == 0)
        //{
        //    rbtnByLevel.IsChecked = true;
        //}
        //else
        //{
        //    rbtnByGroup.IsChecked = true;
        //}
    }


    private void UpdateListType(bool group)
    {
        //������Ⱦ����
        //https://github.com/dotnet/maui/issues/16031
        lvwLevel.BeginRefresh();
        lvwLevel.RemoveBinding(ListView.ItemsSourceProperty);
        lvwLevel.SetBinding(ListView.ItemsSourceProperty, group ? nameof(LayerViewViewModel.Groups) : nameof(LayerViewViewModel.Layers));
        lvwLevel.IsGroupingEnabled = group;
        lvwLevel.EndRefresh();
        //UpdateListSelectedItem();
        if (lvwLevel.SelectedItem != null)
        {
            lvwLevel.ScrollTo(lvwLevel.SelectedItem, ScrollToPosition.MakeVisible, true);
        }
    }

    private void ViewTypeRadioButton_CheckChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            UpdateListType(sender == rbtnByGroup);
            UpdateListType(sender != rbtnByGroup);
            UpdateListType(sender == rbtnByGroup);
            int id = sender == rbtnByGroup ? 1 : 0;
            if (Config.Instance.LastLayerListGroupType != id)
            {
                Config.Instance.LastLayerListGroupType = id;
            }

        }
    }
}