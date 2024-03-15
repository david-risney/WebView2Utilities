using Markdig.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Threading;
using Clipboard = System.Windows.Clipboard;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using Timer = System.Timers.Timer;
using System.Windows.Input;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for AppOverride.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            VersionInfo.Text = "v" + VersionUtil.GetWebView2UtilitiesVersion();
            GenerateNewsBlocksAsync();

            m_watchForChangesTimer.Interval = 3000;
            m_watchForChangesTimer.Elapsed += WatchForChangesTimer_Elapsed;
            m_watchForChangesTimer.Enabled = true;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // We use SelectedItems to remove it by value rather than by
            // selected index, because the index is relative to the sorted
            // view, and we need to remove from the unsorted list.
            var selectedItems = AppOverrideListBox.SelectedItems;
            if (selectedItems.Count == 1)
            {
                var selectedItem = selectedItems[0];
                AppOverrideListData.Remove((AppOverrideEntry)selectedItem);
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

        private void EnvVarButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("rundll32.exe", "sysdm.cpl,EditEnvironmentVariables");
        }

        protected AppOverrideList AppOverrideListData => AppState.GetAppOverrideList();
        private uint m_NewEntriesCount = 0;
        
        protected RuntimeList RuntimeListData => AppState.GetRuntimeList();
        protected HostAppList HostAppsListData => AppState.GetHostAppList();

        private void OverridesReload_Click(object sender, RoutedEventArgs e)
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

                // Find the first override entry that explicitly matches the host app.
                // If none exist, then create and add it.
                // Then select that entry and show that tab.
                AppOverrideEntry selectedAppOverrideEntry = AppOverrideListData.FirstOrDefault(
                    entry => entry.HostApp.ToLower() == selectedHostAppEntry.ExecutableName.ToLower());

                if (selectedAppOverrideEntry == null)
                {
                    selectedAppOverrideEntry = new AppOverrideEntry
                    {
                        HostApp = selectedHostAppEntry.ExecutableName,
                        StorageKind = StorageKind.HKCU,
                    };
                    selectedAppOverrideEntry.InitializationComplete();
                    AppOverrideListData.Add(selectedAppOverrideEntry);
                }

                AppOverrideListBox.SelectedItem = selectedAppOverrideEntry;
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

        private void Hyperlink_ExecutedRouted(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start(e.Parameter.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
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

        private void GridViewColumnHeader_HostApps_StatusDescription_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(3);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * (left.Status - right.Status));
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

        private static string s_newsLink = "https://api.github.com/repos/MicrosoftEdge/WebView2Announcements/issues";
        private async Task<string> GetNewsDataAsync()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            client.DefaultRequestHeaders.Add("User-Agent", ".NET Foundation Repository Reporter");

            var response = await client.GetAsync(s_newsLink);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();
            else
                return await Task.FromResult($"{{ \"error\": \"{response.StatusCode}\" }}");
        }

        private Dictionary<TextBlock, UIElement> m_newsBodyCollapsedControllerMap = new Dictionary<TextBlock, UIElement>();
        private async void GenerateNewsBlocksAsync()
        {
            string newsData = await GetNewsDataAsync();
            var newsJson = JsonSerializer.Deserialize<JsonElement>(newsData);
            int newsDisplayed = 3;
            int bodyFontSize = 12;

            if (newsJson.ValueKind != JsonValueKind.Null &&
                newsJson.ValueKind == JsonValueKind.Array &&
                newsJson.EnumerateArray().Any())
            {
                for (int i = 0; i < newsDisplayed && i < newsJson.GetArrayLength(); i++)
                {
                    // Skip a newsBlock when the data is invalid.
                    if (newsJson[i].ValueKind != JsonValueKind.Object ||
                        !newsJson[i].TryGetProperty("title", out var title) ||
                        !newsJson[i].TryGetProperty("body", out var body))
                    {
                        continue;
                    }

                    // Add the title to the block.
                    TextBlock newsBlock = new TextBlock
                    {
                        Text = title.GetString(),
                        FontSize = bodyFontSize + 2,
                        Margin = new Thickness(10, 10, 0, 0),
                        Cursor = Cursors.Hand
                    };

                    // Add body content to the block.
                    MarkdownViewer bodyBlock = new MarkdownViewer
                    {
                        Markdown = body.GetString(),
                        FontSize = bodyFontSize,
                    };
                    foreach (Block block in bodyBlock.Document.Blocks)
                        if (block.FontSize > bodyFontSize)
                            block.FontSize = bodyFontSize;
                    bodyBlock.CommandBindings.Add(new CommandBinding(Commands.Hyperlink, Hyperlink_ExecutedRouted));
                    Grid bodyGrid = new Grid
                    {
                        Children = { bodyBlock },
                        Width = 500,
                    };

                    // Enable click to collapse function.
                    newsBlock.MouseDown += NewsBlock_Click;
                    m_newsBodyCollapsedControllerMap[newsBlock] = bodyGrid;
                    bodyGrid.Visibility = Visibility.Collapsed;

                    newsBlock.Inlines.Add(new LineBreak());
                    newsBlock.Inlines.Add(bodyGrid);
                    NewsPanel.Children.Add(newsBlock);
                }
            }
        }

        private void NewsBlock_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock clickedTextBlock && m_newsBodyCollapsedControllerMap.ContainsKey(clickedTextBlock))
            {
                UIElement targetContainer = m_newsBodyCollapsedControllerMap[clickedTextBlock];
                targetContainer.Visibility = targetContainer.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
    }
}
