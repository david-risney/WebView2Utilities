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
    public partial class RuntimesPage : Page, IReloadable
    {
        private RuntimeList RuntimeListData => AppState.GetRuntimeList();

        private bool m_reloading = false;
        public bool Reloading
        {
            get => m_reloading;
            private set
            {
                if (value != m_reloading)
                {
                    m_reloading = value;
                    this.ReloadingChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        public RuntimesPage()
        {
            InitializeComponent();
        }

        public event EventHandler ReloadingChanged;

        public void Reload()
        {
            _ = ReloadInternalAsync();
        }

        private async Task ReloadInternalAsync()
        {
            if (!this.Reloading)
            {
                this.Reloading = true;
                await RuntimeListData.FromDiskAsync();
                this.Reloading = false;
            }
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri.IsFile)
            {
                ProcessUtil.OpenExplorerToFile(e.Uri.LocalPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            }
            e.Handled = true;
        }


    }
}
