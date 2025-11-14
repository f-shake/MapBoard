using CommunityToolkit.Maui.Views;
using MapBoard.GeoShare.Core.Entity;
using MapBoard.Mapping;
using MapBoard.Mapping.Model;
using MapBoard.Model;
using MapBoard.Services;
using MapBoard.Util;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class GeoShareConfigPopup : Popup
{
    GeoShareViewModel viewModel = new GeoShareViewModel();
    public GeoShareConfigPopup()
    {
        BindingContext = viewModel;
        InitializeComponent();
    }


    private void CancelButton_Clicked(object sender, EventArgs e)
    {
        this.TryClose();
    }

    private async void LoginButton_Clicked(object sender, EventArgs e)
    {
        HttpService httpService = new HttpService();
        try
        {
            viewModel.IsReady = false;
            await httpService.PostAsync(Config.Instance.GeoShare.Server + HttpService.Url_Login, new UserEntity()
            {
                Username = Config.Instance.GeoShare.UserName,
                Password = Config.Instance.GeoShare.Password,
                GroupName = Config.Instance.GeoShare.GroupName,
            });
            Config.Instance.GeoShare.IsEnabled = true;
            viewModel.NotifyConfig();
            Config.Instance.Save();
            await MainPage.Current.DisplayAlert("登陆成功", "登陆成功", "确定");
            this.TryClose();
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await MainPage.Current.DisplayAlert("登陆失败", "用户名或密码错误", "确定");
            }
            else
            {
                await MainPage.Current.DisplayAlert("登陆失败", ex.Message, "确定");
            }
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("登陆失败", ex.Message, "确定");
        }
        finally
        {
            viewModel.IsReady = true;
        }
    }

    private async void RegisterButton_Clicked(object sender, EventArgs e)
    {
        HttpService httpService = new HttpService();
        try
        {
            viewModel.IsReady = false;
            await httpService.PostAsync(Config.Instance.GeoShare.Server + HttpService.Url_Register, new UserEntity()
            {
                Username = Config.Instance.GeoShare.UserName,
                Password = Config.Instance.GeoShare.Password,
                GroupName = Config.Instance.GeoShare.GroupName,
            });
            Config.Instance.GeoShare.IsEnabled = true;
            viewModel.NotifyConfig();
            Config.Instance.Save();
            await MainPage.Current.DisplayAlert("注册成功", "注册成功并已登录", "确定");
            this.TryClose();
        }
        catch (Exception ex)
        {
            await MainPage.Current.DisplayAlert("注册失败", ex.Message, "确定");
        }
        finally
        {
            viewModel.IsReady = true;
        }
    }

    private void LogoutButton_Clicked(object sender, EventArgs e)
    {
        Config.Instance.GeoShare.IsEnabled = false;
        Config.Instance.Save();
        viewModel.NotifyConfig();
    }
}