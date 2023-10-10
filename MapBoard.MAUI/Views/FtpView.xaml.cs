using MapBoard.IO;
using MapBoard.Services;
using MapBoard.ViewModels;
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

    }

    private void StartStopFtpButton_Clicked(object sender, EventArgs e)
    {
        if ((BindingContext as FtpViewViewModel).IsOn)
        {
            stkFtpDirs.IsEnabled= true;
            ftpService.StopServerAsync();
            ftpService = null;
            (sender as Button).Text = "��FTP";
        }
        else
        {
            stkFtpDirs.IsEnabled = false;
            string dir = null;
            if(rbtnDataDir.IsChecked)
            {
                dir = FolderPaths.DataPath;
            }
            else if(rbtnLogDir.IsChecked)
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
            ftpService = new FtpService(dir);
            ftpService.StartServerAsync();
            (sender as Button).Text = "�ر�FTP";
        }
        (BindingContext as FtpViewViewModel).IsOn = !(BindingContext as FtpViewViewModel).IsOn;
    }
}