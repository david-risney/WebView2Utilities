using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class OverridesPage : Page
{
    public OverridesViewModel ViewModel
    {
        get;
    }

    public OverridesPage()
    {
        ViewModel = App.GetService<OverridesViewModel>();
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
