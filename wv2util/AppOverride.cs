using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public class AppOverrideList : ObservableCollection<AppOverrideEntry>
    {
        public static bool IgnoreUpdatesToRegistry = true;

        public AppOverrideList()
        {
            FromRegistry();
        }

        public void FromRegistry()
        {
            IgnoreUpdatesToRegistry = true;
            UpdateCollectionFromRegistry(this);
            IgnoreUpdatesToRegistry = false;
        }

        public void ToRegistry()
        {
            ApplyToRegistry(this);
        }

        protected void WatchEntries(IEnumerable<AppOverrideEntry> entries)
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    entry.PropertyChanged += WatchedEntryChanged;
                }
            }
        }

        private void WatchedEntryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HostApp")
            {
                ToRegistry();
            }
        }

        protected void UnwatchEntries(IEnumerable<AppOverrideEntry> entries)
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    entry.PropertyChanged -= WatchedEntryChanged;
                }
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            WatchEntries(e.NewItems?.Cast<AppOverrideEntry>());
            UnwatchEntries(e.OldItems?.Cast<AppOverrideEntry>());

            base.OnCollectionChanged(e);
            ToRegistry();
        }

        private static readonly string s_registryPathBrowserExecutableFolder = @"Software\Policies\Microsoft\Edge\WebView2\BrowserExecutableFolder";
        private static readonly string s_registryPathReleaseChannelPreference = @"Software\Policies\Microsoft\Edge\WebView2\ReleaseChannelPreference";
        private static readonly string s_registryPathAdditionalBrowserArguments = @"Software\Policies\Microsoft\Edge\WebView2\AdditionalBrowserArguments";
        private static readonly string s_registryPathUserDataFolder = @"Software\Policies\Microsoft\Edge\WebView2\UserDataFolder";
        private static readonly string[] s_registryPaths =
        {
            s_registryPathAdditionalBrowserArguments,
            s_registryPathBrowserExecutableFolder,
            s_registryPathReleaseChannelPreference,
            s_registryPathUserDataFolder
        };

        private static void EnsureRegistryPaths(RegistryKey root = null)
        {
            foreach (string registryPath in s_registryPaths)
            {
                EnsureRegistryPath(root == null ? Registry.CurrentUser : root, registryPath);
            }
        }

        private static void EnsureRegistryPath(RegistryKey root, string registryPath)
        {
            RegistryKey parent = root;
            foreach (string part in registryPath.Split('\\'))
            {
                RegistryKey child = null;
                try
                {
                    child = parent.OpenSubKey(part, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e);
                }
                if (child == null)
                {
                    child = parent.CreateSubKey(part, true);
                }
                parent = child;
            }
        }

        private static RegistryKey OpenRegistryPath(RegistryKey root, string registryPath, bool write)
        {
            RegistryKey parent = root;
            foreach (string part in registryPath.Split('\\'))
            {
                RegistryKey child = null;
                try
                {
                    child = parent.OpenSubKey(part, write);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e);
                }
                parent = child;
            }
            return parent;
        }

        private static AppOverrideEntry GetOrCreateEntry(Dictionary<string, AppOverrideEntry> appNameToEntry, ObservableCollection<AppOverrideEntry> collection, string valueName)
        {
            AppOverrideEntry entry = null;
            if (!appNameToEntry.TryGetValue(valueName, out entry))
            {
                entry = new AppOverrideEntry();
                entry.HostApp = valueName;
                appNameToEntry.Add(valueName, entry);
                collection.Add(entry);
            }
            return entry;
        }

        public static void UpdateCollectionFromRegistry(ObservableCollection<AppOverrideEntry> collection)
        {
            EnsureRegistryPaths();
            Dictionary<string, AppOverrideEntry> appNameToEntry = new Dictionary<string, AppOverrideEntry>();
            HashSet<AppOverrideEntry> entriesToRemove = new HashSet<AppOverrideEntry>();
            RegistryKey regKey;
            string[] valueNames;

            foreach (var entry in collection)
            {
                entriesToRemove.Add(entry);
                appNameToEntry.Add(entry.HostApp, entry);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName);
                entry.RuntimePath = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName);
                entry.ReverseSearchOrder = (1 == (int)regKey.GetValue(valueName));
                entriesToRemove.Remove(entry);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName);
                entry.BrowserArguments = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName);
                entry.UserDataPath = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            foreach (var entry in entriesToRemove)
            {
                collection.Remove(entry);
            }
        }

        public static ObservableCollection<AppOverrideEntry> CreateCollectionFromRegistry()
        {
            ObservableCollection<AppOverrideEntry> collection = new ObservableCollection<AppOverrideEntry>();
            UpdateCollectionFromRegistry(collection);
            return collection;
        }

        public static void ApplyToRegistry(IEnumerable<AppOverrideEntry> newEntries)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                ObservableCollection<AppOverrideEntry> registryEntries = CreateCollectionFromRegistry();
                foreach (var entry in registryEntries.Except(newEntries, new AppOverrideEntryHostAppEquality()))
                {
                    RemoveEntryFromRegistry(entry);
                }
                foreach (var entry in newEntries)
                {
                    ApplyEntryToRegistry(entry);
                }
            }
        }

        private static void DeleteValueIfItExists(RegistryKey key, string valueName)
        {
            if (key != null && key.GetValue(valueName) != null)
            {
                key.DeleteValue(valueName);
            }
        }

        private static void RemoveEntryFromRegistry(AppOverrideEntry entry)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, true), entry.HostApp);
                DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, true), entry.HostApp);
                DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, true), entry.HostApp);
                DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, true), entry.HostApp);
            }
        }

        public static void ApplyEntryToRegistry(AppOverrideEntry entry)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                if (entry.BrowserArguments != null)
                {
                    OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, true)?.SetValue(entry.HostApp, entry.BrowserArguments, RegistryValueKind.String);
                }
                else
                {
                    DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, true), entry.HostApp);
                }
                if (entry.RuntimePath != null)
                {
                    OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, true)?.SetValue(entry.HostApp, entry.RuntimePath, RegistryValueKind.String);
                }
                else
                {
                    DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, true), entry.HostApp);
                }
                if (entry.ReverseSearchOrder)
                {
                    OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, true)?.SetValue(entry.HostApp, entry.ReverseSearchOrder ? 1 : 0, RegistryValueKind.DWord);
                }
                else
                {
                    DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, true), entry.HostApp);
                }
                if (entry.UserDataPath != null)
                {
                    OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, true)?.SetValue(entry.HostApp, entry.UserDataPath, RegistryValueKind.String);
                }
                else
                {
                    DeleteValueIfItExists(OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, true), entry.HostApp);
                }
            }
        }
    }

    public class AppOverrideEntryHostAppEquality : IEqualityComparer<AppOverrideEntry>
    {
        public bool Equals(AppOverrideEntry x, AppOverrideEntry y)
        {
            return x.HostApp == y.HostApp;
        }

        public int GetHashCode(AppOverrideEntry obj)
        {
            return obj.HostApp.GetHashCode();
        }
    }

    public class AppOverrideEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            AppOverrideList.ApplyEntryToRegistry(this);
        }
        public override string ToString()
        {
            return HostApp;
        }

        public bool ReverseSearchOrder
        {
            get { return m_ReverseSearchOrder; }
            set
            {
                if (m_ReverseSearchOrder != value)
                {
                    m_ReverseSearchOrder = value;
                    OnPropertyChanged("ReverseSearchOrder");
                }
            }
        }
        private bool m_ReverseSearchOrder = false;

        public string HostApp
        {
            get { return m_HostApp; }
            set
            {
                if (m_HostApp != value)
                {
                    m_HostApp = value;
                    OnPropertyChanged("HostApp");
                }
            }
        }
        private string m_HostApp = "";

        public string RuntimePath
        {
            get { return m_RuntimePath; }
            set
            {
                if (m_RuntimePath != value)
                {
                    m_RuntimePath = value;
                    OnPropertyChanged("RuntimePath");
                }
            }
        }
        private string m_RuntimePath = "";

        public string UserDataPath
        {
            get { return m_UserDataPath; }
            set
            {
                if (m_UserDataPath != value)
                {
                    m_UserDataPath = value;
                    OnPropertyChanged("UserDataPath");
                }
            }
        }
        private string m_UserDataPath = "";

        public string BrowserArguments
        {
            get { return m_BrowserArguments; }
            set
            {
                if (m_BrowserArguments != value)
                {
                    m_BrowserArguments = value;
                    OnPropertyChanged("BrowserArguments");
                }
            }
        }
        private string m_BrowserArguments = "";
    }
}
