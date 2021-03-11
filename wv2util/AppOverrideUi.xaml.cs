using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

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
            ApplyRegistryToView();
        }

        private void ApplyRegistryToView()
        {
            List<AppOverrideEntry> entries = AppOverrideList.CreateFromRegistry();
            UpdateEntries(entries);
        }

        private void UpdateEntries(List<AppOverrideEntry> newEntries)
        {
            AppOverrideEntry addEntry = new AppOverrideEntry();
            addEntry.HostApp = "Add New";
            m_entriesInListBox = newEntries.ToList();
            m_entriesInListBox.Add(addEntry);
            AppOverrideListBox.ItemsSource = newEntries;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppOverrideListBox.SelectedIndex == m_entriesInListBox.Count - 1)
            {
                ApplyEntryToView(new AppOverrideEntry());
            }
            else
            {
                string hostAppName = (string)((ListBoxItem)AppOverrideListBox.SelectedItem).Content;
                AppOverrideEntry selectedEntry = m_entriesInListBox.Where(entry => entry.HostApp == hostAppName).First();
                ApplyEntryToView(selectedEntry);
            }
        }

        private List<AppOverrideEntry> m_entriesInListBox = new List<AppOverrideEntry>();

        private void AddEntry(AppOverrideEntry entry)
        {
            ListBoxItem item = new ListBoxItem();
            item.Content = entry.HostApp;
            AppOverrideListBox.Items.Add(item);
        }

        private void ApplyEntryToView(AppOverrideEntry entry)
        {
            AppOverrideBrowserArgumentsTextBox.Text = entry.BrowserArguments;
            AppOverrideHostAppTextBox.Text = entry.HostApp;
            AppOverrideReverseRuntimeSearchOrderCheckBox.IsChecked = entry.ReverseSearchOrder;
            AppOverrideRuntimePathTextBox.Text = entry.RuntimePath;
            AppOverrideUserDataPathTextBox.Text = entry.UserDataPath;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            List<AppOverrideEntry> newEntries = m_entriesInListBox.ToList();
            string hostAppName = (string)((ListBoxItem)AppOverrideListBox.SelectedItem).Content;
            AppOverrideEntry selectedEntry = m_entriesInListBox.Where(entry => entry.HostApp == hostAppName).First();
            newEntries.Remove(selectedEntry);
            UpdateEntries(newEntries);
        }
    }
}
