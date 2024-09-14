using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class RuntimesPage : Page
{
    public RuntimesViewModel ViewModel
    {
        get;
    }

    public RuntimesPage()
    {
        ViewModel = App.GetService<RuntimesViewModel>();
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
