using MapBoard.Model;
using MapBoard.Mapping;
using ModernWpf.FzExtension.CommonDialog;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Esri.ArcGISRuntime.Geometry;
using MapBoard.Mapping.Model;
using ABI.System;
using System;
using System.Collections.ObjectModel;
using MapBoard.Util;

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 选择图层对话框
    /// </summary>
    public partial class OverlayAnalysisDialog : CommonDialog
    {

        public OverlayAnalysisDialog(MapLayerCollection layers, IMapLayerInfo mainLayer)
        {
            InitializeComponent();
            AllLayers = layers;
            MainLayer = mainLayer;

            UdpateSelectableLayers();
        }

        public MapLayerCollection AllLayers { get; }

        public ObservableCollection<IMapLayerInfo> Layers { get; set; }

        public IMapLayerInfo MainLayer { get; set; }

        /// <summary>
        /// 拓扑操作
        /// </summary>
        public OverlayAnalysisOperation Operation { get; set; } = OverlayAnalysisOperation.Intersect;

        /// <summary>
        /// 选择的图层
        /// </summary>
        public MapLayerInfo SelectedLayer { get; set; }

        private void UdpateSelectableLayers()
        {
            var list = AllLayers.Cast<MapLayerInfo>()
                .Where(p => p != AllLayers.Selected)
                .Where(p => OverlayAnalysisUtility.GetValidAnotherLayerGeometryType(Operation, MainLayer.GeometryType).Contains(p.GeometryType));
            Layers = [.. list];
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedLayer))
                {
                    IsPrimaryButtonEnabled = SelectedLayer != null;
                }
            };
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if ((sender as RadioButton).Tag is OverlayAnalysisOperation o)
            {
                Operation = o;
                UdpateSelectableLayers();
            }
        }
    }
}