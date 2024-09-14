using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel
    {
        get;
    }

    public AboutPage()
    {
        ViewModel = App.GetService<AboutViewModel>();
        InitializeComponent();
    }
}
