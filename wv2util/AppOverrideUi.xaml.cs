using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            if (selectedIndex >= 0 && selectedIndex < ListData.Count)
            {
                ListData.RemoveAt(AppOverrideListBox.SelectedIndex);
            }
        }

        private void AddNewButton_Click(object sender, RoutedEventArgs e)
        {
            AppOverrideEntry entry = new AppOverrideEntry();
            entry.HostApp = "New " + (++m_NewEntriesCount);
            ListData.Add(entry);
        }

        protected AppOverrideList ListData { get { return (AppOverrideList)AppOverrideListBox.ItemsSource; } }
        private uint m_NewEntriesCount = 0;

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ListData.FromRegistry();
        }
    }
}
