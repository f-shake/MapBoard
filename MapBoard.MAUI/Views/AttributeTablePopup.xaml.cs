using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Util;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class AttributeTablePopup : Popup
{
    public AttributeTablePopup(Feature feature,bool creating)
    {
        var layerInfo = MainMapView.Current.Layers.Find(feature.FeatureTable.Layer);
        if (layerInfo == null)
        {
            throw new Exception("找不到feature对应的MapLayerInfo");
        }
        FeatureAttributeCollection attributes = null;
        if (creating)
        {
            attributes = FeatureAttributeCollection.Empty(layerInfo);
        }
        else
        {
            attributes = FeatureAttributeCollection.FromFeature(layerInfo, feature);
        }
        BindingContext = new AttributeTablePopupViewModel()
        {
            Attributes = attributes
        };
        InitializeComponent();
        Feature = feature;
    }

    public Feature Feature { get; }

    private void ApplyButton_Clicked(object sender, EventArgs e)
    {
        (BindingContext as AttributeTablePopupViewModel).Attributes.SaveToFeature(Feature);
        Close();
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }
}