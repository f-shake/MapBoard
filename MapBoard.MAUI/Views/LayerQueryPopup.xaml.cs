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
            var layer = (Layer as IMapLayerInfo) ?? throw new Exception("�Ҳ���ͼ��");
            var vm = BindingContext as LayerQueryViewModel;
            var sql = vm.UseSql ? vm.Sql : QuerySqlBuilder.Build(vm.Items);
            Debug.WriteLine("ͼ���ѯSQL��" + sql);
            var result = (await layer.QueryFeaturesAsync(new Esri.ArcGISRuntime.Data.QueryParameters()
            {
                WhereClause = sql
            })).ToList();

            if (result.Count == 0)
            {
                if (!await MainPage.Current.DisplayAlert("��ѯ", "û�в�ѯ���κ�Ҫ��", "�޸�����", "�ر�"))
                {
                    Close();
                }
            }
            else
            {
                if (await MainPage.Current.DisplayAlert("��ѯ", $"��ѯ��{result.Count}��Ҫ��", "�鿴���", "�޸�����"))
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
            await MainPage.Current.DisplayAlert("��ѯʧ��", ex.Message, "ȡ��");
        }
        finally
        {
            Content.IsEnabled = true;
        }
    }
}