using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using WebView2Utilities.Contracts.ViewModels;
using WebView2Utilities.Core.Contracts.Services;
using WebView2Utilities.Core.Models;

namespace WebView2Utilities.ViewModels;

public partial class OverridesViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    private AppOverrideEntry? selected;

    public AppOverrideList Items { get; private set; } = new();

    public OverridesViewModel()
    {
    }

    public async void OnNavigatedTo(object parameter)
    {
        Items.FromSystem();
    }

    public void OnNavigatedFrom()
    {
    }

    public void EnsureItemSelected()
    {
        Selected ??= Items.First();
    }
}
