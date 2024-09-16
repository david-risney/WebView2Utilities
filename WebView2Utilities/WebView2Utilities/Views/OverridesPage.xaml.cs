using CommunityToolkit.WinUI.UI.Controls;

using Microsoft.UI.Xaml.Controls;
using WebView2Utilities.Core.Models;
using WebView2Utilities.ViewModels;

namespace WebView2Utilities.Views;

public sealed partial class OverridesPage : Page
{
    private int m_NewEntriesCount = 0;

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

    private void Add_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppOverrideEntry entry = new AppOverrideEntry
        {
            HostApp = "New " + (++m_NewEntriesCount),
            StorageKind = StorageKind.HKCU,
        };
        entry.InitializationComplete();

        ViewModel.Items.Add(entry);
    }

    private void Remove_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selectedItem = ViewModel.Selected;
        if (selectedItem != null)
        {
            ViewModel.Items.Remove(selectedItem);
        }
    }
}
