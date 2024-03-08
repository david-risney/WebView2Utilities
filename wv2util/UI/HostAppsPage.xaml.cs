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
using Timer = System.Timers.Timer;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for HostAppsPage.xaml
    /// </summary>
    public partial class HostAppsPage : Page
    {
        public HostAppsPage()
        {
            InitializeComponent();
        }

        private AppOverrideList AppOverrideListData => AppState.GetAppOverrideList();
        private RuntimeList RuntimeListData => AppState.GetRuntimeList();
        private HostAppList HostAppsListData => AppState.GetHostAppList();

        private void HostAppsCreateReport_Click(object sender, RoutedEventArgs e)
        {
            HostAppEntry selectedHostAppEntry = (HostAppEntry)HostAppListView.SelectedValue;
            if (selectedHostAppEntry != null)
            {
                ReportCreator creator = new ReportCreator(selectedHostAppEntry, AppOverrideListData, RuntimeListData);
                throw new Exception("Need to redo this function.");
                // CreateReportWindow createReportWindow = new CreateReportWindow(this, creator);
                // createReportWindow.ShowDialog();
            }
        }

        private void HostAppsGoToOverride_Click(object sender, RoutedEventArgs e)
        {
            HostAppEntry selectedHostAppEntry = (HostAppEntry)HostAppListView.SelectedValue;
            if (selectedHostAppEntry != null)
            {
                AppOverrideListData.FromSystem();

                int foundOverrideIdx = -1;
                for (int appOverrideIdx = 0; appOverrideIdx < AppOverrideListData.Count; ++appOverrideIdx)
                {
                    var appOverride = AppOverrideListData[appOverrideIdx];
                    if (appOverride.HostApp.ToLower() == selectedHostAppEntry.ExecutableName.ToLower())
                    {
                        foundOverrideIdx = appOverrideIdx;
                        break;
                    }
                }

                if (foundOverrideIdx == -1)
                {
                    // Add an Override to the end for this host app
                    AppOverrideEntry entry = new AppOverrideEntry
                    {
                        HostApp = selectedHostAppEntry.ExecutableName,
                        StorageKind = StorageKind.HKCU,
                    };
                    entry.InitializationComplete();
                    AppOverrideListData.Add(entry);

                    // And set foundOverrideIdx to the new value's index
                    foundOverrideIdx = AppOverrideListData.Count - 1;
                }

                throw new Exception("Need to redo this function.");
                // AppOverrideListBox.SelectedIndex = foundOverrideIdx;
                // TabControl.SelectedIndex = 2;
            }
        }

        private static bool ProgrammaticallyClickButton(Button button)
        {
            if (button.IsEnabled)
            {
                ButtonAutomationPeer peer = new ButtonAutomationPeer(button);
                IInvokeProvider invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
                return true;
            }
            return false;
        }

        private async void HostAppsReload_Click(object sender, RoutedEventArgs e)
        {
            object originalContent = "🔃";
            var HostAppsReload = this.ReloadButton;
            HostAppsReload.Content = "⌚";
            HostAppsReload.IsEnabled = false;

            await HostAppsListData.FromMachineAsync();

            HostAppsReload.Content = originalContent;
            HostAppsReload.IsEnabled = true;

            if (m_hostAppsReReload)
            {
                m_hostAppsReReload = false;
                ProgrammaticallyClickButton(this.ReloadButton);
            }
        }

        private bool m_hostAppsReReload = false;
        private void HostAppsDiscoverSlowlyCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // The data binding will update the property on the HostAppList
            // This method will additionally ensure we refresh the list (by programmatically
            // clicking the refresh button) to match the new option.
            if (!ProgrammaticallyClickButton(this.ReloadButton))
            {
                // If we couldn't click the reload button because its already
                // reloading, we set a bool to re-reload once its done.
                m_hostAppsReReload = true;
            }
        }

        private Timer m_watchForChangesTimer = new Timer();
        private IEnumerable<HostAppEntry> m_previousHostAppEntries = null;
        private void HostAppsWatchForChangesCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            m_watchForChangesTimer.Enabled = checkbox.IsChecked.Value;
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
                    // The Timer thread isn't the UI thread, so switch to the UI thread
                    // in order to programmatically 'click' the refresh button.
                    Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                    {
                        ReloadButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                }
            }
            m_previousHostAppEntries = currentHostAppEntries;
        }

        private void HostAppListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HostAppListView.SelectedIndex >= 0)
            {
                HostAppEntry selection = (HostAppEntry)HostAppListView.SelectedItem;
                try
                {
                    Clipboard.SetText(selection.ExecutableName + "\t" +
                        selection.SdkInfo.Version + "\t" +
                        selection.Runtime.RuntimeLocation + "\t" +
                        selection.Runtime.Version + "\t" +
                        selection.Runtime.Channel + "\t" +
                        selection.UserDataPath + "\t" +
                        selection.ExecutablePath);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // We might fail to open clipboard. Just ignore
                }
            }
        }

        private readonly SortUtil.SortColumnContext m_hostAppSortColumn = new SortUtil.SortColumnContext();
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

    }
}
