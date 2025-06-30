using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using System.Threading.Tasks;

namespace MapBoard.Mapping
{
    public class SearchOverlayHelper
    {
        private Timer flashTimer;

        public SearchOverlayHelper(GraphicsOverlay overlay)
        {
            Overlay = overlay;
            flashTimer = new Timer(s =>
            {
                if (Overlay.Graphics.Count > 0)
                {
                    overlay.IsVisible = !overlay.IsVisible;
                }
            }, null, 500, 500);
        }

        ~SearchOverlayHelper()
        {
            flashTimer?.Dispose();
        }

        public event EventHandler<SearchResultDisplayChangedEventArgs> SearchResultDisplayChanged;

        public IList<Feature> CurrentFeatures { get; private set; }
        public IMapLayerInfo CurrentLayer { get; private set; }
        public GraphicsOverlay Overlay { get; }

        public void Hide()
        {
            CurrentFeatures = null;
            CurrentLayer = null;
            Overlay.Graphics.Clear();
            SearchResultDisplayChanged?.Invoke(this, new SearchResultDisplayChangedEventArgs(false));
        }

        public async void ShowSearchResult(IMapLayerInfo layer, IList<Feature> features)
        {
            CurrentLayer = layer;
            CurrentFeatures = features;

            Overlay.Graphics.Clear();
            if (features.Count == 0)
            {
                return;
            }
            foreach (var feature in features)
            {
                Graphic graphic = new Graphic(feature.Geometry, feature.Attributes);
                graphic.Symbol = feature.Geometry.GeometryType switch
                {
                    GeometryType.Point or GeometryType.Multipoint => new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Cross, System.Drawing.Color.Red, 12)
                    {
                        Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 2)
                    },
                    GeometryType.Envelope or GeometryType.Polygon => new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.Yellow, null),
                    GeometryType.Polyline => new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Yellow, 2),
                    _ => throw new ArgumentOutOfRangeException(),
                };
                Overlay.Graphics.Add(graphic);
            }
            SearchResultDisplayChanged?.Invoke(this, new SearchResultDisplayChangedEventArgs(true));
            await MainMapView.Current.ZoomToGeometryAsync(GeometryEngine.CombineExtents(features.Select(p => p.Geometry)));
        }

        public class SearchResultDisplayChangedEventArgs : EventArgs
        {
            public SearchResultDisplayChangedEventArgs(bool isVisible)
            {
                IsVisible = isVisible;
            }

            public bool IsVisible { get; }
        }
    }
}