using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WebView2Utilities.Core.Models;

namespace WebView2Utilities.Views;

public sealed partial class RuntimesDetailControl : UserControl
{
    public SampleOrder? ListDetailsMenuItem
    {
        get => GetValue(ListDetailsMenuItemProperty) as SampleOrder;
        set => SetValue(ListDetailsMenuItemProperty, value);
    }

    public static readonly DependencyProperty ListDetailsMenuItemProperty = DependencyProperty.Register("ListDetailsMenuItem", typeof(SampleOrder), typeof(RuntimesDetailControl), new PropertyMetadata(null, OnListDetailsMenuItemPropertyChanged));

    public RuntimesDetailControl()
    {
        InitializeComponent();
    }

    private static void OnListDetailsMenuItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RuntimesDetailControl control)
        {
            control.ForegroundElement.ChangeView(0, 0, 1);
        }
    }
}
