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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for OverridesPage.xaml
    /// </summary>
    public partial class OverridesPage : Page
    {
        private AppOverrideList AppOverrideListData => AppState.GetAppOverrideList();
        private uint m_NewEntriesCount = 0;


        public OverridesPage()
        {
            InitializeComponent();
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

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            AppOverrideListData.FromSystem();
        }

    }
}
