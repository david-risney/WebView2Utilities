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
using System.Collections.ObjectModel;
using wv2util.Pages;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for AppOverride.xaml
    /// </summary>
    public partial class MainWindow : Window, IShowHostAppEntryAsAppOverrideEntry
    {
        public MainWindow()
        {
            InitializeComponent();

            // ReloadableHost connects up the reload button in the main page to the reloadable page
            new ReloadableHost(this.HostAppsReload, this.HostAppsPage);
            new ReloadableHost(this.RuntimesReload, this.RuntimesPage);
            new ReloadableHost(this.AppOverridesReload, this.AppOverridesPage);
        }

        public void ShowHostAppEntryAsAppOverrideEntry(HostAppEntry entry)
        {
            this.AppOverridesPage.ShowHostAppEntryAsAppOverrideEntry(entry);
            this.TabControl.SelectedItem = this.AppOverridesTab;
        }
    }
}
