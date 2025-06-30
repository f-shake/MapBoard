using MapBoard.Mapping;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using MapBoard.Util;
using ModernWpf.FzExtension.CommonDialog;
using FzLib;
using Esri.ArcGISRuntime.Data;
using System.Diagnostics;
using MapBoard.Mapping.Model;
using ModernWpf.Controls;
using Esri.ArcGISRuntime.Portal;
using MapBoard.Query;

namespace MapBoard.UI.Dialog
{
    /// <summary>
    /// 定义表达式对话框
    /// </summary>
    public partial class DefinitionExpressionDialog : LayerDialogBase
    {
        private DefinitionExpressionDialog(Window owner, IMapLayerInfo layer, MainMapView arcMap) : base(owner, layer, arcMap)
        {
            InitializeComponent();
            Title = "筛选显示图形 - " + layer.Name;
            Expression = layer.DefinitionExpression;
        }

        /// <summary>
        /// 表达式
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// 创建或跳转到指定图层的<see cref="DefinitionExpressionDialog"/>
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="layer"></param>
        /// <param name="mapView"></param>
        /// <returns></returns>
        public static DefinitionExpressionDialog Get(Window owner, IMapLayerInfo layer, MainMapView mapView)
        {
            return GetInstance(layer, () => new DefinitionExpressionDialog(owner, layer, mapView));
        }

        /// <summary>
        /// 单击应用按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Layer.DefinitionExpression = Expression;
        }

        /// <summary>
        /// 地图状态改变，只有在正常状态下才可以设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ArcMap_BoardTaskChanged(object sender, BoardTaskChangedEventArgs e)
        {
            IsEnabled = e.NewTask == BoardTask.Ready;
        }

        //单击清除按钮
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Layer.DefinitionExpression = "";
        }

        /// <summary>
        /// 单击确定按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Layer.DefinitionExpression = Expression;
            Close();
        }

        private async void BuildSqlButton_Click(object sender, RoutedEventArgs e)
        {
            if (Layer.Fields.Length == 0)
            {
                await CommonDialog.ShowErrorDialogAsync("该图层不含任何字段");
                return;
            }
            QuerySqlBuilderDialog dialog = new QuerySqlBuilderDialog(Layer);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Expression = QuerySqlBuilder.Build(dialog.Items);
            }
        }
    }
}