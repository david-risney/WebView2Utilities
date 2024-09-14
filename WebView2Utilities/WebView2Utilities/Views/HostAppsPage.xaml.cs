using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class HostAppsPage : Page
{
    public HostAppsViewModel ViewModel
    {
        get;
    }

    public HostAppsPage()
    {
        ViewModel = App.GetService<HostAppsViewModel>();
        InitializeComponent();
    }

    private void OnViewStateChanged(object sender, ListDetailsViewState e)
    {
        if (e == ListDetailsViewState.Both)
        {
            ViewModel.EnsureItemSelected();
        }
    }
}
