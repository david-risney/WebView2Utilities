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
    }
}
