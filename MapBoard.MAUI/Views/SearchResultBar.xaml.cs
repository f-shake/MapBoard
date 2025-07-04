﻿using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.UI.Editing;
using MapBoard.Mapping;
using MapBoard.Models;
using MapBoard.Util;
using MapBoard.ViewModels;
using System.Threading.Tasks;
using static MapBoard.Views.PopupMenu;

namespace MapBoard.Views;

public partial class SearchResultBar : ContentView, ISidePanel
{
    public SearchResultBar()
    {
        InitializeComponent();
    }

    public SwipeDirection Direction => SwipeDirection.Up;

    public int Length => 240;

    public bool Standalone => true;

    public void OnPanelClosed()
    {
    }

    public void OnPanelOpening()
    {
    }

    private void CancelSearchResultButton_Click(object sender, EventArgs e)
    {
        MainMapView.Current.SearchOverlay.Hide();
    }

    private void ContentView_Loaded(object sender, EventArgs e)
    {
    }

    private async void ViewTableButton_Click(object sender, EventArgs e)
    {
        var layer = MainMapView.Current.SearchOverlay.CurrentLayer;
        var features = MainMapView.Current.SearchOverlay.CurrentFeatures;
        AttributeTablePopup p = new AttributeTablePopup(layer, features);
        await MainPage.Current.ShowPopupAsync(p);
    }
}