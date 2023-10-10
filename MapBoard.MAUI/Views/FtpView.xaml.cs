using MapBoard.IO;
using MapBoard.Services;
using MapBoard.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace MapBoard.Views;

public partial class FtpView : ContentView
{
    FtpService ftpService;
    public FtpView()
    {
        var ip = FtpService.GetIpAddress();
        if (ip == null || !ip.Any())
        {
            ip = null;
        }
        BindingContext = new FtpViewViewModel()
        {
            IP = ip == null ? "δ֪" : string.Join(Environment.NewLine, ip),
            IsOn = false,
        };
        InitializeComponent();
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            btnFtp.IsVisible = false;
            btnOpenDir.IsVisible = true;
            grdFtpInfo.IsVisible = false;
        }
    }

    private async void StartStopFtpButton_Clicked(object sender, EventArgs e)
    {
        if ((BindingContext as FtpViewViewModel).IsOn)
        {
            stkFtpDirs.IsEnabled = true;
            ftpService = null;
            (sender as Button).Text = "��FTP";
            await ftpService.StopServerAsync();
        }
        else
        {
            stkFtpDirs.IsEnabled = false;
            string dir = GetSelectedDir();
            ftpService = new FtpService(dir);
            await ftpService.StartServerAsync();
            (sender as Button).Text = "�ر�FTP";
        }
        (BindingContext as FtpViewViewModel).IsOn = !(BindingContext as FtpViewViewModel).IsOn;
    }

    private string GetSelectedDir()
    {
        string dir = null;
        if (rbtnDataDir.IsChecked)
        {
            dir = FolderPaths.DataPath;
        }
        else if (rbtnLogDir.IsChecked)
        {
            dir = FolderPaths.LogsPath;
        }
        else if (rbtnTrackDir.IsChecked)
        {
            dir = FolderPaths.TrackPath;
        }
        else if (rbtnRootDir.IsChecked)
        {
            dir = FileSystem.AppDataDirectory;
        }
        else if (rbtnCacheDir.IsChecked)
        {
            dir = FileSystem.CacheDirectory;
        }

        return dir;
    }

    private void OpenDirButton_Clicked(object sender, EventArgs e)
    {
        if(DeviceInfo.Platform!=DevicePlatform.WinUI)
        {
            throw new NotSupportedException("��֧��Windows");
        }
        string dir = GetSelectedDir();
        Process.Start("explorer.exe", dir);
    }
}