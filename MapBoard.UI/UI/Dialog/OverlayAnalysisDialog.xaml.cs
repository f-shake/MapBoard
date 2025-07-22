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

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 选择图层对话框
    /// </summary>
    public partial class OverlayAnalysisDialog : CommonDialog
    {
        /// <summary>
        /// 是否能够选择
        /// </summary>
        private bool canSelect = false;

        public OverlayAnalysisDialog(MapLayerCollection layers, ILayerInfo mainLayer)
        {
            InitializeComponent();
            MainLayer = mainLayer;
            var list = layers.Cast<MapLayerInfo>()
                .Where(p => p != layers.Selected);
            if (list.Any())
            {
                lbx.ItemsSource = list.ToList();
                lbx.SelectedIndex = 0;
                canSelect = true;
            }
            else
            {
                IsPrimaryButtonEnabled = false;
            }
        }

        /// <summary>
        /// 拓扑操作
        /// </summary>
        public OverlayAnalysisOperation Operation { get; set; }

        public ILayerInfo MainLayer { get; set; }

        /// <summary>
        /// 选择的图层
        /// </summary>
        public MapLayerInfo SelectedLayer { get; set; }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (canSelect == false)
            {
                Content = new TextBlock()
                {
                    Text = "没有可选择的图层",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
        }

        private void CommonDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            Operation = (OverlayAnalysisOperation)
                (stkOperations.Children.OfType<RadioButton>()
                .Where(p => p.IsChecked == true).First().Tag);
        }
    }
}