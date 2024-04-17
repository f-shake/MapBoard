using CommunityToolkit.Maui.Views;
using MapBoard.GeoShare.Core.Entity;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Services;
using MapBoard.Util;

namespace MapBoard.Views;

public partial class GeoShareConfigPopup : Popup
{
    public GeoShareConfigPopup()
    {
        BindingContext = Config.Instance.GeoShare;
        InitializeComponent();
    }


    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        Close();
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        HttpService httpService = new HttpService();
        try
        {
            await httpService.PostAsync(Config.Instance.GeoShare.Server + HttpService.Url_Login, new UserEntity()
            {
                Username = Config.Instance.GeoShare.UserName,
                Password = Config.Instance.GeoShare.Password,
                GroupName = Config.Instance.GeoShare.GroupName,
            });
            await MainPage.Current.DisplayAlert("��½�ɹ�", "λ�ù��������ע��", "ȷ��");
            Close();
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("��½ʧ��", ex.Message, "ȷ��");
        }
    }
}