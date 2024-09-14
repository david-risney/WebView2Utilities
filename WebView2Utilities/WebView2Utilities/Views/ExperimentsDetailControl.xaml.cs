using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.Core.Models;

namespace WebView2Utilities.Views;

public sealed partial class ExperimentsDetailControl : UserControl
{
    public SampleOrder? ListDetailsMenuItem
    {
        get => GetValue(ListDetailsMenuItemProperty) as SampleOrder;
        set => SetValue(ListDetailsMenuItemProperty, value);
    }

    public static readonly DependencyProperty ListDetailsMenuItemProperty = DependencyProperty.Register("ListDetailsMenuItem", typeof(SampleOrder), typeof(ExperimentsDetailControl), new PropertyMetadata(null, OnListDetailsMenuItemPropertyChanged));

    public ExperimentsDetailControl()
    {
        InitializeComponent();
    }

    private static void OnListDetailsMenuItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ExperimentsDetailControl control)
        {
            control.ForegroundElement.ChangeView(0, 0, 1);
        }
    }
}
