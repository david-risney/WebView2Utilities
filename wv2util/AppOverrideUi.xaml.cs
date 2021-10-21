﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Clipboard = System.Windows.Clipboard;

namespace wv2util
{
    public class ValidListBoxSelection : INotifyPropertyChanged
    {
        public System.Windows.Controls.ListBox ListBox
        {
            get { return m_ListBox; }

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
        }

        private System.Windows.Controls.ListBox m_ListBox = null;

        public bool IsInvalidSelection { get { return !IsValidSelection; } }
        public bool IsValidSelection { get { return ListBox != null && ListBox.SelectedIndex != -1; } }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Interaction logic for AppOverride.xaml
    /// </summary>
    public partial class AppOverrideUi : Window
    {
        public AppOverrideUi()
        {
            InitializeComponent();
            ((ValidListBoxSelection)Resources["AppOverrideListSelection"]).ListBox = AppOverrideListBox;
            AppOverrideListBoxSelectionChanged(null, null);
            VersionInfo.Text = "v" + Assembly.GetExecutingAssembly().GetName().Version;
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
            AppOverrideEntry entry = new AppOverrideEntry();
            entry.HostApp = "New " + (++m_NewEntriesCount);
            AppOverrideListData.Add(entry);
        }

        protected AppOverrideList AppOverrideListData { get { return (AppOverrideList)AppOverrideListBox?.ItemsSource; } }
        private uint m_NewEntriesCount = 0;

        protected RuntimeList RuntimeListData { get { return (RuntimeList)RuntimeList?.ItemsSource; } }
        protected HostAppList HostAppsListData { get { return (HostAppList)HostAppListView?.ItemsSource; } }


        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            AppOverrideListData.FromRegistry();
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
            var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a WebView2 Runtime folder";
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AppOverrideRuntimePathComboBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void AppOverrideUserDataPathButton_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select the path to a user data folder";
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AppOverrideUserDataPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void AppOverrideBrowserArgumentsButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://peter.sh/experiments/chromium-command-line-switches/");
        }

        private async void RuntimesReload_Click(object sender, RoutedEventArgs e)
        {
            var originalContent = RuntimesReload.Content;
            RuntimesReload.Content = "⌚";
            RuntimesReload.IsEnabled = false;

            await RuntimeListData.FromDiskAsync();
            
            RuntimesReload.Content = originalContent;
            RuntimesReload.IsEnabled = true;
        }

        private async void HostAppsReload_Click(object sender, RoutedEventArgs e)
        {
            var originalContent = HostAppsReload.Content;
            HostAppsReload.Content = "⌚";
            HostAppsReload.IsEnabled = false;

            await HostAppsListData.FromMachineAsync();
            HostAppsReload.Content = originalContent;
            HostAppsReload.IsEnabled = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://docs.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void RuntimeListMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void MenuItemCopyRow(object sender, RoutedEventArgs e)
        {
            string text = ((System.Windows.Controls.ListView)sender).SelectedItem.ToString();
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // We might fail to open clipboard. Just ignore
            }

        }

        private SortUtil.SortColumnContext m_runtimeSortColumn = new SortUtil.SortColumnContext();
        private void GridViewColumnHeader_Runtime_Path_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(0);

            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * left.RuntimeLocation.CompareTo(right.RuntimeLocation));
        }

        private void GridViewColumnHeader_Runtime_Version_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(1);
            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * wv2util.SortUtil.CompareVersionStrings(left.Version, right.Version));
        }

        private void GridViewColumnHeader_Runtime_Channel_Click(object sender, RoutedEventArgs e)
        {
            m_runtimeSortColumn.SelectColumn(1);
            RuntimeListData.Sort<RuntimeEntry>((left, right) =>
                m_runtimeSortColumn.SortDirection * SortUtil.CompareChannelStrings(left.Channel, right.Channel));
        }

        private SortUtil.SortColumnContext m_hostAppSortColumn = new SortUtil.SortColumnContext();
        private void GridViewColumnHeader_HostApps_Executable_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(0);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * left.ExecutableName.CompareTo(right.ExecutableName));
        }

        private void GridViewColumnHeader_HostApps_PID_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(6);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * (left.PID - right.PID));
        }

        private void GridViewColumnHeader_HostApps_RuntimeVersion_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(1);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * SortUtil.CompareVersionStrings(left.Runtime.Version, right.Runtime.Version));
        }

        private void GridViewColumnHeader_HostApps_RuntimeChannel_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(2);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * SortUtil.CompareChannelStrings(left.Runtime.Channel, right.Runtime.Channel));
        }

        private void GridViewColumnHeader_HostApps_RuntimeLocation_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(3);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * left.Runtime.RuntimeLocation.CompareTo(right.Runtime.RuntimeLocation));
        }

        private void GridViewColumnHeader_HostApps_UserDataPath_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(4);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * left.UserDataPath.CompareTo(right.UserDataPath));
        }

        private void GridViewColumnHeader_HostApps_ExecutablePath_Click(object sender, RoutedEventArgs e)
        {
            m_hostAppSortColumn.SelectColumn(5);
            HostAppsListData.Sort<HostAppEntry>((left, right) =>
                m_hostAppSortColumn.SortDirection * left.ExecutablePath.CompareTo(right.ExecutablePath));
        }

        private void AppOverrideListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = AppOverrideListBox.SelectedIndex;
            bool mutable = false;
            if (selectedIndex >= 0 && selectedIndex < AppOverrideListData.Count)
            {
                AppOverrideEntry selectedEntry = (AppOverrideEntry)AppOverrideListData[selectedIndex];
                mutable = selectedEntry.Mutable;
            }

            if (RemoveButton != null)
            {
                RemoveButton.IsEnabled = mutable;
            }
            if (AppOverrideHostAppComboBox != null)
            {
                AppOverrideHostAppComboBox.IsEnabled = mutable;
            }
        }
    }
}
