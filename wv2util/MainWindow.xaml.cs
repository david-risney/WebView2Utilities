using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Threading;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using Timer = System.Timers.Timer;

namespace wv2util
{
    public class ValidListBoxSelection : INotifyPropertyChanged
    {
        public System.Windows.Controls.ListBox ListBox
        {
            get => m_ListBox;

            set
            {
                if (m_ListBox != value)
                {
                    if (m_ListBox != null)
                    {
                        m_ListBox.SelectionChanged -= OnSelectionChanged;
                    }
                    if (value != null)
                    {
                        m_ListBox = value;
                        m_ListBox.SelectionChanged += OnSelectionChanged;
                        OnSelectionChanged(null, null);
                    }
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsInvalidSelection"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsValidSelection"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsValidAndFixedVersionSelection"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsPerUserRegistry"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRegistry"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsValidEntryAndCanChangeRegistry"));

            if (m_Entry != null)
            {
                m_Entry.PropertyChanged -= Entry_PropertyChanged;
            }
            m_Entry = null;
            if (IsValidSelection)
            {
                m_Entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                m_Entry.PropertyChanged += Entry_PropertyChanged;
            }
        }

        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsValidAndFixedVersionSelection"));
        }

        private System.Windows.Controls.ListBox m_ListBox = null;
        private AppOverrideEntry m_Entry = null;

        public bool IsInvalidSelection => !IsValidSelection;
        public bool IsValidSelection => ListBox != null && ListBox.SelectedIndex != -1;
        public bool IsValidEntryAndCanChangeRegistry
        {
            get
            {
                if (IsValidSelection && IsRegistry)
                {
                    AppOverrideEntry entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                    return entry.HostApp != "*";
                }
                return false;
            }
        }
            
        public bool IsPerUserRegistry
        {
            get
            {
                if (IsValidSelection)
                {
                    AppOverrideEntry entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                    return entry.RegistryRoot == Registry.CurrentUser;
                }
                return false;
            }

            set
            {
                if (IsValidSelection)
                {
                    AppOverrideEntry entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                    entry.StorageKind = value ? StorageKind.HKCU : StorageKind.HKLM;
                }
            }
        }
        public bool IsRegistry
        {
            get
            {
                if (IsValidSelection)
                {
                    AppOverrideEntry entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                    return entry.RegistryRoot != null;
                }
                return false;
            }
        }
        public bool IsValidAndFixedVersionSelection
        {
            get
            {
                if (IsValidSelection)
                {
                    AppOverrideEntry entry = (AppOverrideEntry)ListBox.Items[ListBox.SelectedIndex];
                    return entry.IsRuntimeFixedVersion;
                }
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Interaction logic for AppOverride.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ((ValidListBoxSelection)Resources["AppOverrideListSelection"]).ListBox = AppOverrideListBox;
            AppOverrideListBoxSelectionChanged(null, null);
            VersionInfo.Text = "v" + VersionUtil.GetWebView2UtilitiesVersion();
            
            m_watchForChangesTimer.Interval = 3000;
            m_watchForChangesTimer.Elapsed += WatchForChangesTimer_Elapsed;
            m_watchForChangesTimer.Enabled = HostAppsWatchForChangesCheckbox.IsChecked.Value;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = AppOverrideListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < AppOverrideListData.Count)
            {
                AppOverrideListData.RemoveAt(AppOverrideListBox.SelectedIndex);
            }
        }
        private void AddNewButton_Click(object sender, RoutedEventArgs e)
        {
            AppOverrideEntry entry = new AppOverrideEntry
            {
                HostApp = "New " + (++m_NewEntriesCount),
                StorageKind = StorageKind.HKCU,
            };
            entry.InitializationComplete();
            AppOverrideListData.Add(entry);
        }

        private void RegEditButton_Click(object sender, RoutedEventArgs e)
        {
            RegistryUtil.LaunchRegEdit();
        }

        protected AppOverrideList AppOverrideListData => (AppOverrideList)AppOverrideListBox?.ItemsSource;
        private uint m_NewEntriesCount = 0;

        protected RuntimeList RuntimeListData => (RuntimeList)RuntimeList?.ItemsSource;
        protected HostAppList HostAppsListData => Resources["HostAppList"] as HostAppList;

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            AppOverrideListData.FromSystem();
        }

        private void HostAppsCreateReport_Click(object sender, RoutedEventArgs e)
        {
            HostAppEntry selectedHostAppEntry = (HostAppEntry)HostAppListView.SelectedValue;
            if (selectedHostAppEntry != null)
            {
                ReportCreator creator = new ReportCreator(selectedHostAppEntry, AppOverrideListData, RuntimeListData);
                CreateReportWindow createReportWindow = new CreateReportWindow(this, creator);
                createReportWindow.ShowDialog();
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

                AppOverrideListBox.SelectedIndex = foundOverrideIdx;
                TabControl.SelectedIndex = 2;
            }
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

        private void AppOverrideRuntimePathButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
            {
                Description = "Select a WebView2 Runtime folder"
            };
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AppOverrideRuntimePathComboBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void AppOverrideUserDataPathButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
            {
                Description = "Select the path to a user data folder"
            };
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AppOverrideUserDataPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void AppOverrideBrowserArgumentsButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://peter.sh/experiments/chromium-command-line-switches/");
        }

        private async void RuntimesReload_Click(object sender, RoutedEventArgs e)
        {
            object originalContent = "🔃";
            RuntimesReload.Content = "⌚";
            RuntimesReload.IsEnabled = false;

            await RuntimeListData.FromDiskAsync();

            RuntimesReload.Content = originalContent;
            RuntimesReload.IsEnabled = true;
        }

        private async void HostAppsReload_Click(object sender, RoutedEventArgs e)
        {
            object originalContent = "🔃";
            HostAppsReload.Content = "⌚";
            HostAppsReload.IsEnabled = false;

            await HostAppsListData.FromMachineAsync();

            HostAppsReload.Content = originalContent;
            HostAppsReload.IsEnabled = true;

            if (m_hostAppsReReload)
            {
                m_hostAppsReReload = false;
                ProgrammaticallyClickButton(this.HostAppsReload);
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
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

        private void AppOverrideListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = AppOverrideListBox.SelectedIndex;
            bool canRemove = false;
            bool canChangeHostApp = false;

            if (selectedIndex >= 0 && selectedIndex < AppOverrideListData.Count)
            {
                AppOverrideEntry selectedEntry = AppOverrideListData[selectedIndex];
                canRemove = selectedEntry.CanRemove;
                canChangeHostApp = selectedEntry.CanChangeHostApp;
            }

            if (RemoveButton != null)
            {
                RemoveButton.IsEnabled = canRemove;
            }
            if (AppOverrideHostAppComboBox != null)
            {
                AppOverrideHostAppComboBox.IsEnabled = canChangeHostApp;
            }
        }

        private bool m_hostAppsReReload = false;
        private void HostAppsDiscoverSlowlyCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            // The data binding will update the property on the HostAppList
            // This method will additionally ensure we refresh the list (by programmatically
            // clicking the refresh button) to match the new option.
            if (!ProgrammaticallyClickButton(this.HostAppsReload))
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
                        HostAppsReload.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                }
            }
            m_previousHostAppEntries = currentHostAppEntries;            
        }
    }
}
