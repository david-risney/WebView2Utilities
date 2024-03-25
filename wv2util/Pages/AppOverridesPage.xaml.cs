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
    /// Interaction logic for AppOverridesPage.xaml
    /// </summary>
    public partial class AppOverridesPage : Page, IReloadable
    {
        public AppOverridesPage()
        {
            InitializeComponent();
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
                AppOverrideListData.FromSystem();
                this.Reloading = false;
            }
        }

    }
}
