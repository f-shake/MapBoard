using CommunityToolkit.Maui.Views;
using MapBoard.ViewModels;

namespace MapBoard.Views;

public partial class ProgressPopup : Popup
{
    public ProgressPopup(string message)
    {
        var viewModel = new ProgressPopupViewModel()
        {
            Message = message
        };
        BindingContext = viewModel;
        InitializeComponent();
    }

    public static ProgressPopup Show(string message)
    {
        var popup = new ProgressPopup(message);
        MainPage.Current.ShowPopup(popup);
        return popup;
    }

    public void Close()
    {
        this.TryClose();
    }
}