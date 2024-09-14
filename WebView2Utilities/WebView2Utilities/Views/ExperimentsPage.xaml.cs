using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class ExperimentsPage : Page
{
    public ExperimentsViewModel ViewModel
    {
        get;
    }

    public ExperimentsPage()
    {
        ViewModel = App.GetService<ExperimentsViewModel>();
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
