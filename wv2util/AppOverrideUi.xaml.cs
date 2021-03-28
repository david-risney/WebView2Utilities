using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    }
}
