using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for RuntimesPage.xaml
    /// </summary>
    public partial class RuntimesPage : Page
    {
        private RuntimeList RuntimeListData => AppState.GetRuntimeList();

        public RuntimesPage()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void RuntimeListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RuntimeList.SelectedIndex >= 0)
            {
                RuntimeEntry selection = (RuntimeEntry)RuntimeList.SelectedItem;
                try
                {
                    Clipboard.SetText(selection.RuntimeLocation + "\t" +
                        selection.Version + "\t" +
                        selection.Channel);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // We might fail to open clipboard. Just ignore
                }
            }
        }

        private async void RuntimesReload_Click(object sender, RoutedEventArgs e)
        {
            object originalContent = "🔃";
            ReloadButton.Content = "⌚";
            ReloadButton.IsEnabled = false;

            await RuntimeListData.FromDiskAsync();

            ReloadButton.Content = originalContent;
            ReloadButton.IsEnabled = true;
        }


        private readonly SortUtil.SortColumnContext m_runtimeSortColumn = new SortUtil.SortColumnContext();
        private void GridViewColumnHeader_Runtime_Path_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(0);

            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * SortUtil.CompareStrings(left.RuntimeLocation, right.RuntimeLocation));
        }

        private void GridViewColumnHeader_Runtime_Version_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(2);
            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * SortUtil.CompareVersionStrings(left.Version, right.Version));
        }

        private void GridViewColumnHeader_Runtime_Channel_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(2);
            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * SortUtil.CompareChannelStrings(left.Channel, right.Channel));
        }
    }
}
