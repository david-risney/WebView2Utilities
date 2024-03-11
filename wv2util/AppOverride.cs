using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace wv2util
{
    public class AggregateObservableCollections<T> : ObservableCollection<T>
    {
        public AggregateObservableCollections(List<ObservableCollection<T>> collections)
        {
            collections_ = collections;
            foreach (var collection in collections)
            {
                collection.CollectionChanged += Collection_CollectionChanged;
            }
        }

        private void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs innerEventArgs)
        {
            var senderCollectionIdx = collections_.IndexOf((ObservableCollection<T>)sender);
            int valueIdx = 0;
            for (int collectionIdx = 0; collectionIdx < senderCollectionIdx; ++collectionIdx)
            {
                valueIdx += collections_[collectionIdx].Count;
            }

            NotifyCollectionChangedEventArgs outerEventArgs = null;

            if (innerEventArgs.NewItems != null)

            OnCollectionChanged(outerEventArgs);
        }

        private List<ObservableCollection<T>> collections_;
    }

    public class AppOverrideList : ObservableCollection<AppOverrideEntry>
    {
        public static bool IgnoreUpdatesToRegistry = true;

        public AppOverrideList()
        {
            FromSystem();
        }

        public void FromSystem()
        {
            IgnoreUpdatesToRegistry = true;
            UpdateCollectionFromEnvVar(this, StorageKind.EVCU);
            UpdateCollectionFromEnvVar(this, StorageKind.EVLM);
            UpdateCollectionFromRegistry(this, StorageKind.HKCU);
            UpdateCollectionFromRegistry(this, StorageKind.HKLM);

            // We may have created entries above when going through the registry.
            // Different reg entries can apply to the same AppOverrideEntry so we
            // wait till the end to mark it initialized.
            foreach (AppOverrideEntry entry in this)
            {
                entry.InitializationComplete();
            }
            IgnoreUpdatesToRegistry = false;
        }

        public void ToSystem()
        {
            ApplyToRegistry(this, StorageKind.HKCU);
            ApplyToRegistry(this, StorageKind.HKLM);
            ApplyToEnvVar(this, StorageKind.EVCU);
            ApplyToEnvVar(this, StorageKind.EVLM);
        }

        private void ApplyToEnvVar(IEnumerable<AppOverrideEntry> newEntries, StorageKind storageKind)
        {
            var filteredNewEntries = newEntries.Where(e => e.StorageKind == storageKind);
            int filteredNewEntriesCount = filteredNewEntries.Count();
            var currentEntries = new ObservableCollection<AppOverrideEntry>();
            UpdateCollectionFromEnvVar(currentEntries, storageKind);
            int currentEntriesCount = currentEntries.Count();

            if (filteredNewEntriesCount > 0)
            {
                foreach (var entry in filteredNewEntries)
                {
                    ApplyEntryToEnvVar(entry);
                }
            }
            else if (currentEntriesCount > 0)
            {
                foreach (var entry in currentEntries)
                {
                    RemoveEntryFromEnvVar(entry);
                }
            }
        }

        protected void WatchEntries(IEnumerable<AppOverrideEntry> entries)
        {
            if (entries != null)
            {
                foreach (AppOverrideEntry entry in entries)
                {
                    entry.PropertyChanged += WatchedEntryChanged;
                }
            }
        }

        private void WatchedEntryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HostApp")
            {
                ToSystem();
            }
        }

        protected void UnwatchEntries(IEnumerable<AppOverrideEntry> entries)
        {
            if (entries != null)
            {
                foreach (AppOverrideEntry entry in entries)
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
            ToSystem();
        }

        public static readonly string s_registryPathBrowserExecutableFolder = RegistryUtil.s_webView2RegKey + @"\BrowserExecutableFolder";
        public static readonly string s_registryPathReleaseChannelPreference = RegistryUtil.s_webView2RegKey + @"\ReleaseChannelPreference";
        public static readonly string s_registryPathAdditionalBrowserArguments = RegistryUtil.s_webView2RegKey + @"\AdditionalBrowserArguments";
        public static readonly string s_registryPathUserDataFolder = RegistryUtil.s_webView2RegKey + @"\UserDataFolder";
        private static readonly string[] s_registryPaths =
        {
            s_registryPathAdditionalBrowserArguments,
            s_registryPathBrowserExecutableFolder,
            s_registryPathReleaseChannelPreference,
            s_registryPathUserDataFolder
        };

        private static AppOverrideEntry GetOrCreateEntry(Dictionary<string, AppOverrideEntry> appNameToEntry, ObservableCollection<AppOverrideEntry> collection, string valueName, StorageKind storageKind)
        {
            if (!appNameToEntry.TryGetValue(valueName, out AppOverrideEntry entry))
            {
                entry = new AppOverrideEntry
                {
                    HostApp = valueName,
                    StorageKind = storageKind,
                };
                appNameToEntry.Add(valueName, entry);
                collection.Add(entry);
            }
            return entry;
        }

        public static void UpdateCollectionFromRegistry(ObservableCollection<AppOverrideEntry> collection, StorageKind storageKind)
        {
            RegistryUtil.EnsureRegistryPaths(s_registryPaths, Registry.CurrentUser);
            RegistryUtil.EnsureRegistryPaths(s_registryPaths, Registry.LocalMachine);
            Dictionary<string, AppOverrideEntry> appNameToEntry = new Dictionary<string, AppOverrideEntry>();
            HashSet<AppOverrideEntry> entriesToRemove = new HashSet<AppOverrideEntry>();
            RegistryKey regKey;
            string[] valueNames;
            RegistryKey registryRoot = storageKind == StorageKind.HKLM ? Registry.LocalMachine : Registry.CurrentUser;

            // We want to minimally change collection to make it match the registry
            // We start by assuming everything in the collection is no longer in the
            // registry. We add every entry to entriesToRemove. Later we go through
            // the registry and remove reg entries from the entriesToRemove since
            // they still exist.
            foreach (AppOverrideEntry entry in collection)
            {
                if (entry.StorageKind == storageKind)
                {
                    entriesToRemove.Add(entry);
                    appNameToEntry.Add(entry.HostApp, entry);
                }
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                entry.RuntimePath = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                try
                {
                    var value = regKey.GetValue(valueName);
                    int valueAsInt = (int)value;
                    entry.ReverseSearchOrder = 1 == valueAsInt;
                }
                catch (InvalidCastException)
                {
                    Debug.WriteLine("Ignoring malformed registry entries that don't use an int: path=" + regKey + "." + valueName);
                }
                entriesToRemove.Remove(entry);
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathAdditionalBrowserArguments, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                entry.BrowserArguments = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                entry.UserDataPath = (string)regKey.GetValue(valueName);
                entriesToRemove.Remove(entry);
            }

            foreach (AppOverrideEntry entry in entriesToRemove)
            {
                collection.Remove(entry);
            }

            // Move all wildcard entries to the top
            int firstNonWildcardIdx = 0;
            
            for (int idx = 0; idx < collection.Count; ++idx)
            {
                if (collection[idx].HostApp != "*" && firstNonWildcardIdx == 0)
                {
                    firstNonWildcardIdx = idx;
                }
                if (collection[idx].HostApp == "*" && firstNonWildcardIdx != 0)
                {
                    collection.Move(idx, 0);
                }
            }
            // If we didn't have a wildcard entry, add it at the start
            if (collection.FirstOrDefault(e => e.HostApp == "*" && e.StorageKind == StorageKind.HKCU) == null)
            {
                AppOverrideEntry entry = new AppOverrideEntry
                {
                    HostApp = "*",
                    StorageKind = StorageKind.HKCU,
                };
                collection.Insert(0, entry);
            }
        }

        public static ObservableCollection<AppOverrideEntry> CreateCollectionFromSystem()
        {
            ObservableCollection<AppOverrideEntry> collection = new ObservableCollection<AppOverrideEntry>();
            UpdateCollectionFromEnvVar(collection, StorageKind.EVCU);
            UpdateCollectionFromEnvVar(collection, StorageKind.EVLM);
            UpdateCollectionFromRegistry(collection, StorageKind.HKCU);
            UpdateCollectionFromRegistry(collection, StorageKind.HKLM);

            // We may have created entries above when going through the registry.
            // Different reg entries can apply to the same AppOverrideEntry so we
            // wait till the end to mark it initialized.
            foreach (AppOverrideEntry entry in collection)
            {
                entry.InitializationComplete();
            }
            return collection;
        }

        private static void UpdateCollectionFromEnvVar(ObservableCollection<AppOverrideEntry> collection, StorageKind storageKind)
        {
            EnvironmentVariableTarget target = storageKind == StorageKind.EVLM ? 
                EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            var envVars = Environment.GetEnvironmentVariables(target);
            string browserExecutableFolder = (string)envVars["WEBVIEW2_BROWSER_EXECUTABLE_FOLDER"];
            string userDataFolder = (string)envVars["WEBVIEW2_USER_DATA_FOLDER"];
            string additionalBrowserArguments = (string)envVars["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"];
            string releaseChannelPreferenceAsString = (string)envVars["WEBVIEW2_RELEASE_CHANNEL_PREFERENCE"];
            bool reverseChannelSearch = releaseChannelPreferenceAsString == "1";

            bool foundInCollection = true;
            AppOverrideEntry entry = collection.FirstOrDefault(existingEntry => existingEntry.StorageKind == storageKind);
            if (entry == null)
            {
                entry = new AppOverrideEntry();
                foundInCollection = false;
            }

            if (!String.IsNullOrEmpty(browserExecutableFolder) ||
                !String.IsNullOrEmpty(userDataFolder) ||
                !String.IsNullOrEmpty(additionalBrowserArguments) ||
                !String.IsNullOrEmpty(releaseChannelPreferenceAsString))
            {
                entry.HostApp = "*";
                entry.StorageKind = storageKind;
                entry.UserDataPath = userDataFolder;
                entry.RuntimePath = browserExecutableFolder;
                entry.BrowserArguments = additionalBrowserArguments;
                entry.ReverseSearchOrder = reverseChannelSearch;

                // If found in the env vars then we either need to update the
                // existing entry or add the entry to the collection.
                if (!foundInCollection)
                {
                    collection.Insert(0, entry);
                }
            }
            else
            {
                // If removed from env vars but still in the collection,
                // then we need to update the collection to remove it
                if (foundInCollection)
                {
                    collection.Remove(entry);
                }
            }

        }

        public static void ApplyToRegistry(IEnumerable<AppOverrideEntry> newEntries, StorageKind storageKind)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                var registryEntries = CreateCollectionFromSystem().Where(e => e.StorageKind == storageKind);
                var filteredNewEntries = newEntries.Where(e => e.StorageKind == storageKind);
                foreach (AppOverrideEntry entry in registryEntries.Except(filteredNewEntries, new AppOverrideEntryHostAppEquality()))
                {
                    RemoveEntryFromRegistry(entry);
                }
                foreach (AppOverrideEntry entry in filteredNewEntries)
                {
                    ApplyEntryToRegistry(entry);
                }
            }
        }

        public static void RemoveEntry(AppOverrideEntry entry)
        {
            if (entry.StorageKind == StorageKind.HKCU ||
                entry.StorageKind == StorageKind.HKLM)
            {
                RemoveEntryFromRegistry(entry);
            }
            else
            {
                RemoveEntryFromEnvVar(entry);
            }
        }

        private static void RemoveEntryFromEnvVar(AppOverrideEntry entry)
        {
            EnvironmentVariableTarget target = entry.StorageKind == StorageKind.EVLM ? 
                EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", null, target);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", null, target);
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", null, target);
            Environment.SetEnvironmentVariable("WEBVIEW2_RELEASE_CHANNEL_PREFERENCE", null, target);
        }

        public static void RemoveEntryFromRegistry(AppOverrideEntry entry, RegistryKey removeRoot = null)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                var registryRoot = removeRoot == null ? entry.RegistryRoot : removeRoot;
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathAdditionalBrowserArguments, true), entry.HostApp);
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, true), entry.HostApp);
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, true), entry.HostApp);
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, true), entry.HostApp);
            }
        }
        
        public static void ApplyEntry(AppOverrideEntry entry)
        {
            if (entry.StorageKind == StorageKind.HKLM ||
                entry.StorageKind == StorageKind.HKCU)
            {
                ApplyEntryToRegistry(entry);
            }
            else
            {
                ApplyEntryToEnvVar(entry);
            }
        }

        private static string StringEmptyToNull(string value)
        {
            return value == "" ? null : value;
        }

        public static void ApplyEntryToEnvVar(AppOverrideEntry entry)
        {
            EnvironmentVariableTarget target = entry.StorageKind == StorageKind.EVLM ?
                EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            SetEnvironmentVariableIfChanged("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", StringEmptyToNull(entry.RuntimePath), target);
            SetEnvironmentVariableIfChanged("WEBVIEW2_USER_DATA_FOLDER", StringEmptyToNull(entry.UserDataPath), target);
            SetEnvironmentVariableIfChanged("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", StringEmptyToNull(entry.BrowserArguments), target);
            SetEnvironmentVariableIfChanged("WEBVIEW2_RELEASE_CHANNEL_PREFERENCE", entry.ReverseSearchOrder ? "1" : null, target);
        }

        private static void SetEnvironmentVariableIfChanged(string name, string value, EnvironmentVariableTarget target)
        {
            if (Environment.GetEnvironmentVariable(name, target) != value)
            {
                // Changing the user or machine environment variables sends (not posts!) a window message to all processes
                // so they can respond to the env var change. This can take a while so its cheaper to first check for
                // the current env var before setting.
                Environment.SetEnvironmentVariable(name, value, target);
            }
        }

        private static void ApplyEntryToRegistry(AppOverrideEntry entry)
        {
            if (!IgnoreUpdatesToRegistry)
            {
                var registryRoot = entry.RegistryRoot;
                if (registryRoot == null)
                {
                    throw new Exception("Cant apply env var entry to registry.");
                }
                else
                {
                    // Use empty string for browser arguments if its null. But always write it to ensure we have something in the registry
                    // recording the entry. An empty string browser arguments is fine because its merged with normal command line arguments
                    // and won't change anything, unlike the paths.
                    RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathAdditionalBrowserArguments, true)?.SetValue(
                        entry.HostApp,
                        entry.BrowserArguments != null ? entry.BrowserArguments : "",
                        RegistryValueKind.String);

                    if (entry.RuntimePath != null && entry.RuntimePath != "")
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, true)?.SetValue(entry.HostApp, entry.RuntimePath, RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, true), entry.HostApp);
                    }
                    if (entry.ReverseSearchOrder)
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, true)?.SetValue(entry.HostApp, entry.ReverseSearchOrder ? 1 : 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, true), entry.HostApp);
                    }
                    if (entry.UserDataPath != null && entry.UserDataPath != "")
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, true)?.SetValue(entry.HostApp, entry.UserDataPath, RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, true), entry.HostApp);
                    }
                }
            }
        }
    }

    public class AppOverrideEntryHostAppEquality : IEqualityComparer<AppOverrideEntry>
    {
        public bool Equals(AppOverrideEntry x, AppOverrideEntry y)
        {
            return x.HostApp == y.HostApp && x.RegistryRoot == y.RegistryRoot;
        }

        public int GetHashCode(AppOverrideEntry obj)
        {
            return obj.HostApp.GetHashCode();
        }
    }

    public enum StorageKind
    {
        HKCU = 0,
        HKLM = 1,
        EVCU = 2,
        EVLM = 3,
    };

    public class AppOverrideEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool m_InitializationComplete = false;
        public StorageKind StorageKind
        { 
            get => m_StorageKind;
            set
            {
                if (m_StorageKind != value)
                {
                    m_StorageKind = value;
                    OnPropertyChanged("RegistryRoot");
                    OnPropertyChanged("StorageKind");
                    OnPropertyChanged("DisplayLabel");
                }
            }
        }
        private StorageKind m_StorageKind = StorageKind.HKCU;
        public string StorageKindDescription
        {
            get
            {
                string description = "Stored in ";
                switch (StorageKind)
                {
                    case StorageKind.HKCU:
                        description += "registry (HKCU)";
                        break;
                    case StorageKind.HKLM:
                        description += "registry (HKLM)";
                        break;
                    case StorageKind.EVCU:
                        description += "environment variables (User)";
                        break;
                    case StorageKind.EVLM:
                        description += "environment variables (Machine)";
                        break;
                }
                return description;
            }
        }
        
        public RegistryKey RegistryRoot
        {
            get
            {
                switch (StorageKind)
                {
                    case StorageKind.HKCU:
                        return Registry.CurrentUser;
                    case StorageKind.HKLM:
                        return Registry.LocalMachine;
                    default:
                        return null;
                }
            }
        }

        public void InitializationComplete()
        {
            m_InitializationComplete = true;
        }
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (m_InitializationComplete)
            {
                if (name == "RegistryRoot")
                {
                    var removeRoot = this.StorageKind == StorageKind.HKCU ? Registry.LocalMachine : Registry.CurrentUser;
                    AppOverrideList.RemoveEntryFromRegistry(this, removeRoot);
                }
                AppOverrideList.ApplyEntry(this);
            }
        }
        public override string ToString()
        {
            return DisplayLabel;
        }

        public bool ReverseSearchOrder
        {
            get => m_ReverseSearchOrder;
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

        public string DisplayLabel
        {
            get
            {
                string storageKindSuffix = "";
                switch (StorageKind)
                {
                    case StorageKind.HKCU:
                        storageKindSuffix = "";
                        break;
                    case StorageKind.HKLM:
                        storageKindSuffix = " (HKLM)";
                        break;
                    case StorageKind.EVCU:
                        storageKindSuffix = " (EVCU)";
                        break;
                    case StorageKind.EVLM:
                        storageKindSuffix = " (EVLM)";
                        break;
                    default:
                        throw new Exception("Unknown StorageKind");
                }
                string nameSuffix = "";
                if (HostApp == "*")
                {
                    nameSuffix = " (All Apps)";
                }
                return HostApp + nameSuffix + storageKindSuffix;
            }
        }

        public bool CanRemove => !(HostApp == "*" && StorageKind == StorageKind.HKCU);
        public bool CanChangeHostApp => HostApp != "*";

        public string HostApp
        {
            get => m_HostApp;
            set
            {
                if (m_HostApp != value)
                {
                    m_HostApp = NullToEmpty(value);
                    OnPropertyChanged("HostApp");
                    OnPropertyChanged("DisplayLabel");
                }
            }
        }
        private string m_HostApp = "";

        public string RuntimePath
        {
            get => m_RuntimePath;
            set
            {
                if (m_RuntimePath != value)
                {
                    m_RuntimePath = NullToEmpty(value);
                    OnPropertyChanged("RuntimePath");
                }
            }
        }
        private string m_RuntimePath = "";

        public string UserDataPath
        {
            get => m_UserDataPath;
            set
            {
                if (m_UserDataPath != value)
                {
                    m_UserDataPath = NullToEmpty(value);
                    OnPropertyChanged("UserDataPath");
                }
            }
        }
        private string m_UserDataPath = "";

        public string BrowserArguments
        {
            get => m_BrowserArguments;
            set
            {
                if (m_BrowserArguments != value)
                {
                    m_BrowserArguments = NullToEmpty(value);
                    OnPropertyChanged("BrowserArguments");
                }
            }
        }
        private string m_BrowserArguments = "";

        public bool IsCommonBrowserArgumentEnabledLogging
        {
            get
            {
                var commandLine = new CommandLineUtil.CommandLine(BrowserArguments);
                return commandLine.Contains("--enable-logging") && commandLine.Contains("--v=1");
            }

            set
            {
                var commandLine = new CommandLineUtil.CommandLine(BrowserArguments);
                if (value)
                {
                    commandLine.Add("--enable-logging");
                    commandLine.Add("--v=1");
                }
                else
                {
                    commandLine.Remove("--enable-logging");
                    commandLine.Remove("--v=1");
                }
                BrowserArguments = commandLine.ToString();
            }
        }

        public bool IsCommonBrowserArgumentEnabledAutoOpenDevTools
        {
            get
            {
                var commandLine = new CommandLineUtil.CommandLine(BrowserArguments);
                return commandLine.Contains("--auto-open-devtools-for-tabs");
            }

            set
            {
                var commandLine = new CommandLineUtil.CommandLine(BrowserArguments);
                if (value)
                {
                    commandLine.Add("--auto-open-devtools-for-tabs");
                }
                else
                {
                    commandLine.Remove("--auto-open-devtools-for-tabs");
                }
                BrowserArguments = commandLine.ToString();
            }
        }

        public bool IsRuntimeEvergreen
        {
            get => !ReverseSearchOrder && RuntimePath == "";
            set
            {
                if (value)
                {
                    ReverseSearchOrder = false;
                    RuntimePath = null;
                }
                OnPropertyChanged("IsRuntimeEvergreen");
            }
        }
        public bool IsRuntimeEvergreenPreview
        {
            get => ReverseSearchOrder && RuntimePath == "";
            set
            {
                if (value)
                {
                    ReverseSearchOrder = true;
                    RuntimePath = null;
                }
                OnPropertyChanged("IsRuntimeEvergreenPreview");
            }
        }
        public bool IsRuntimeFixedVersion
        {
            get => !ReverseSearchOrder && RuntimePath != "";
            set
            {
                if (value)
                {
                    ReverseSearchOrder = false;
                    RuntimePath = ".";
                }
                OnPropertyChanged("IsRuntimeFixedVersion");
            }
        }

        private string NullToEmpty(string inp) { return inp == null ? "" : inp; }
    }
}
