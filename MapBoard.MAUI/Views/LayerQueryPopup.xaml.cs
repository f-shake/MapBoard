using CommunityToolkit.Maui.Views;
using Esri.ArcGISRuntime.Mapping;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Query;
using MapBoard.Util;
using MapBoard.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MapBoard.Views;

public partial class LayerQueryPopup : Popup
{
    public LayerQueryPopup(ILayerInfo layer)
    {
        InitializeComponent();
        BindingContext = new LayerQueryViewModel(layer);
        Layer = layer;
    }

    public ILayerInfo Layer { get; }

    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }

    private async void SearchButton_Clicked(object sender, EventArgs e)
    {
        Content.IsEnabled = false;
        try
        {
            var layer = (Layer as IMapLayerInfo) ?? throw new Exception("找不到图层");
            var vm = BindingContext as LayerQueryViewModel;
            var sql = vm.UseSql ? vm.Sql : QuerySqlBuilder.Build(vm.Items);
            Debug.WriteLine("图层查询SQL：" + sql);
            var result = (await layer.QueryFeaturesAsync(new Esri.ArcGISRuntime.Data.QueryParameters()
            {
                WhereClause = sql
            })).ToList();

            if (result.Count == 0)
            {
                if (!await MainPage.Current.DisplayAlert("查询", "没有查询到任何要素", "修改条件", "关闭"))
                {
                    Close();
                }
            }
            else
            {
                if (await MainPage.Current.DisplayAlert("查询", $"查询到{result.Count}个要素", "查看结果", "修改条件"))
                {
                    Close();
                    MainMapView.Current.SearchOverlay.ShowSearchResult(result);
                }
                else
                {
                }
            }
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("查询失败", ex.Message, "取消");
        }
        finally
        {
            Content.IsEnabled = true;
        }
    }
}