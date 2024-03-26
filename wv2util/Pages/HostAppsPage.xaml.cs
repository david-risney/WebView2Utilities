using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using wv2util.Pages;
using Timer = System.Timers.Timer;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for HostAppsPage.xaml
    /// </summary>
    public partial class HostAppsPage : Page, IReloadable
    {
        private Timer m_watchForChangesTimer = new Timer();
        private IEnumerable<HostAppEntry> m_previousHostAppEntries = null;
        private readonly SortUtil.SortColumnContext m_hostAppSortColumn = new SortUtil.SortColumnContext();
        private HostAppList HostAppsListData => AppState.GetHostAppList();
        private HostAppEntry HostAppTreeViewSelectedItem =>
            (HostAppEntry)(((HostAppEntryTreeItem)HostAppTreeView?.SelectedItem)?.Model);

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

        public HostAppsPage()
        {
            InitializeComponent();

            m_watchForChangesTimer.Interval = 3000;
            m_watchForChangesTimer.Elapsed += WatchForChangesTimer_Elapsed;
            m_watchForChangesTimer.Enabled = true;
        }

        private void WatchForChangesTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // When the timer elapses we want to check if there are any new hostt apps or
            // old host apps removed.
            var currentHostAppEntries = HostAppList.GetHostAppEntriesFromMachineByPipeEnumeration();
            // If we haven't run before then previous host app entries is null and we just
            // record this run's host app entries to compare against next time's run.
            if (m_previousHostAppEntries != null)
            {
                int previousCount = m_previousHostAppEntries.Count();
                // We know there are changes if the count of host apps has changed
                bool changed = currentHostAppEntries.Count() != previousCount;
                if (!changed)
                {
                    // If they're the same size, then we can check if any entry from one list
                    // isn't in the other to know if they're equal.
                    changed = currentHostAppEntries.Any(entry => !m_previousHostAppEntries.Contains(entry));
                }

                // If we have seen differences in the host app entries then we want to
                // 'click' the refresh button to update the UI.
                if (changed)
                {
                    Reload();
                }
            }
            m_previousHostAppEntries = currentHostAppEntries;
        }

        private void GridViewColumnHeader_HostApps_Executable_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(0);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * SortUtil.CompareStrings(left.ExecutableName, right.ExecutableName));
        }

        private void GridViewColumnHeader_HostApps_PID_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(1);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * (left.PID - right.PID));
        }

        private void GridViewColumnHeader_HostApps_BrowserPID_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(2);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * (left.BrowserProcessPID - right.BrowserProcessPID));
        }

        private void GridViewColumnHeader_HostApps_StatusDescription_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(3);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * (left.Status - right.Status));
        }

        public event EventHandler ReloadingChanged;

        private void HostAppsDiscoverSlowlyCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            Reload();
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

        private void HostAppsCreateReport_Click(object sender, RoutedEventArgs e)
        {
            HostAppEntry selectedHostAppEntry = HostAppTreeViewSelectedItem;
            if (selectedHostAppEntry != null)
            {
                ReportCreator creator = new ReportCreator(
                    selectedHostAppEntry, 
                    AppState.GetAppOverrideList(),
                    AppState.GetRuntimeList());
                CreateReportWindow createReportWindow = new CreateReportWindow(Window.GetWindow(this), creator);
                createReportWindow.ShowDialog();
            }
        }

        private void HostAppsGoToOverride_Click(object sender, RoutedEventArgs e)
        {
            HostAppEntry selectedHostAppEntry = HostAppTreeViewSelectedItem;
            if (selectedHostAppEntry != null)
            {
                Window parent = Window.GetWindow(this);
                ((IShowHostAppEntryAsAppOverrideEntry)parent).ShowHostAppEntryAsAppOverrideEntry(selectedHostAppEntry);
            }
        }

        public void Reload()
        {
            _ = ReloadInternalAsync();
        }

        private async Task ReloadInternalAsync()
        {
            if (!this.Reloading)
            {
                this.Reloading = true;
                await HostAppsListData.FromMachineAsync();
                this.Reloading = false;
            }
        }
    }
}
