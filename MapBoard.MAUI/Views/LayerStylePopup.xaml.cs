using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Util;

namespace MapBoard.Views;

public partial class LayerStylePopup : Popup
{
    public LayerStylePopup(IMapLayerInfo layer)
    {
        InitializeComponent();
        RawLayer = layer;
        Layer = layer.Clone() as IMapLayerInfo;
        BindingContext = Layer;
    }

    public IMapLayerInfo Layer { get; }
    public IMapLayerInfo RawLayer { get; }
    private void ApplyButton_Clicked(object sender, EventArgs e)
    {
        RawLayer.Display = Layer.Display;
        RawLayer.Renderer = Layer.Renderer;
        RawLayer.Labels = Layer.Labels;
        RawLayer.Interaction = Layer.Interaction;
        RawLayer.ApplyStyle();
        RawLayer.Layer.Opacity = Layer.Display.Opacity;
        RawLayer.Layer.MinScale = Layer.Display.MinScale;
        RawLayer.Layer.MaxScale = Layer.Display.MaxScale;
        RawLayer.Layer.RenderingMode = (FeatureRenderingMode)Layer.Display.RenderingMode;
        Close();
    }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }
}