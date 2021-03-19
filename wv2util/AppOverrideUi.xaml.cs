using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
using System.Windows.Shapes;
using Clipboard = System.Windows.Clipboard;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for AppOverride.xaml
    /// </summary>
    public partial class AppOverrideUi : Window
    {
        public AppOverrideUi()
        {
            InitializeComponent();
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

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RuntimeList.SelectedIndex >= 0)
            {
                RuntimeEntry selection = (RuntimeEntry)RuntimeList.SelectedItem;
                Clipboard.SetText(selection.RuntimeLocation);
            }
        }

        private void AppOverrideRuntimePathButton_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = "Select a WebView2 Runtime folder";
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AppOverrideRuntimePathTextBox.Text = folderBrowserDialog.SelectedPath;
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

        private void RuntimesReload_Click(object sender, RoutedEventArgs e)
        {
            RuntimeListData.FromDisk();
        }

        private void HostAppsReload_Click(object sender, RoutedEventArgs e)
        {
            HostAppsListData.FromMachine();
        }
    }
}
