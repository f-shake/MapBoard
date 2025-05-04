using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class LayerQueryPopup : Popup
{
    public LayerQueryPopup(ILayerInfo layer)
    {
        InitializeComponent();
        BindingContext = new LayerQueryViewModel(layer);
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }

    private void SearchButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }
}