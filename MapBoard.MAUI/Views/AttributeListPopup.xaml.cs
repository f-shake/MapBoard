using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Data;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Util;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class AttributeListPopup : Popup
{
    public AttributeListPopup(Feature feature, AttributeTableType type)
    {
        var layerInfo = MainMapView.Current.Layers.Find(feature.FeatureTable.Layer);
        if (layerInfo == null)
        {
            throw new Exception("找不到feature对应的MapLayerInfo");
        }

        InitializeComponent();

        FeatureAttributeCollection attributes = null;
        switch (type)
        {
            case AttributeTableType.Create:
                attributes = FeatureAttributeCollection.Empty(layerInfo);
                gViewButtons.IsVisible = false;
                break;
            case AttributeTableType.Edit:
                attributes = FeatureAttributeCollection.FromFeature(layerInfo, feature);
                gViewButtons.IsVisible = false;
                break;
            case AttributeTableType.View:
                attributes = FeatureAttributeCollection.FromFeature(layerInfo, feature);
                gEditButtons.IsVisible = false;
                break;
        }

        BindingContext = new AttributeListViewModel()
        {
            Attributes = attributes
        };
        Feature = feature;
    }

    public enum AttributeTableType
    {
        Create,
        Edit,
        View
    }

    public Feature Feature { get; }

    private void ApplyButton_Clicked(object sender, EventArgs e)
    {
        (BindingContext as AttributeListViewModel).Attributes.SaveToFeature(Feature);
        Close();
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }
}