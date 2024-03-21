using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using static wv2util.AppOverrideEntry;

namespace wv2util
{
    public class AppOverrideList : ObservableCollection<AppOverrideEntry>
    {
        public static bool IgnoreUpdatesToRegistry = true;
        public static Channels allChannels =
            Channels.Stable |
            Channels.Beta |
            Channels.Dev |
            Channels.Canary;

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
        public static readonly string s_registryPathChannelSearchKind = RegistryUtil.s_webView2RegKey + @"\ChannelSearchKind";
        public static readonly string s_registryPathReleaseChannels = RegistryUtil.s_webView2RegKey + @"\ReleaseChannels";
        public static readonly string s_registryPathAdditionalBrowserArguments = RegistryUtil.s_webView2RegKey + @"\AdditionalBrowserArguments";
        public static readonly string s_registryPathUserDataFolder = RegistryUtil.s_webView2RegKey + @"\UserDataFolder";
        private static readonly string[] s_registryPaths =
        {
            s_registryPathAdditionalBrowserArguments,
            s_registryPathBrowserExecutableFolder,
            s_registryPathReleaseChannelPreference,
            s_registryPathChannelSearchKind,
            s_registryPathReleaseChannels,
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
                    if (value is int)
                    {
                        entry.ReverseSearchOrder = ((int)value) == 1;
                    }
                    else if (value is string)
                    {
                        if (int.TryParse((string)value, out int valueAsInt))
                        {
                            entry.ReverseSearchOrder = valueAsInt == 1;
                        }
                        else
                        {
                            Debug.WriteLine("Ignoring malformed registry entries that don't use an int: path=" + regKey + "." + valueName);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Ignoring unsupported registry value type: path=" + regKey + "." + valueName + ", type=" + value.GetType().FullName);
                    }
                }
                catch (InvalidCastException)
                {
                    Debug.WriteLine("Ignoring malformed registry entries that don't use an int: path=" + regKey + "." + valueName);
                }
                entriesToRemove.Remove(entry);
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathChannelSearchKind, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                try
                {
                    var value = regKey.GetValue(valueName);

                    if (value is int)
                    {
                        entry.ReverseSearchOrder = ((int)value) == 1;
                    }
                    else if (value is string)
                    {
                        if (int.TryParse((string)value, out int valueAsInt))
                        {
                            entry.ReverseSearchOrder = valueAsInt == 1;
                        }
                        else
                        {
                            Debug.WriteLine("Ignoring malformed registry entries that don't use an int: path=" + regKey + "." + valueName);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Ignoring unsupported registry value type: path=" + regKey + "." + valueName + ", type=" + value.GetType().FullName);
                    }
                }
                catch (InvalidCastException)
                {
                    Debug.WriteLine("Ignoring malformed registry entries that don't use an int: path=" + regKey + "." + valueName);
                }
                entriesToRemove.Remove(entry);
            }

            regKey = RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannels, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = GetOrCreateEntry(appNameToEntry, collection, valueName, storageKind);
                entry.ReleaseChannels = ReleaseChannelsFromString((string)regKey.GetValue(valueName));
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

            // If we didn't have an HKCU wildcard entry, add it to the end
            if (collection.FirstOrDefault(e => e.HostApp == "*" && e.StorageKind == StorageKind.HKCU) == null)
            {
                AppOverrideEntry entry = new AppOverrideEntry
                {
                    HostApp = "*",
                    StorageKind = StorageKind.HKCU,
                };
                collection.Add(entry);
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
        }

        private static Channels ReleaseChannelsFromString(string channelString)
        {
            Channels channels = allChannels;
            if (!String.IsNullOrEmpty(channelString))
            {
                channels = Channels.None;
                var channelNumbers = channelString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var channelNumber in channelNumbers)
                {
                    if (int.TryParse(channelNumber.Trim(), out int number))
                    {
                        switch (number)
                        {
                            case 0:
                                channels |= Channels.Stable;
                                break;
                            case 1:
                                channels |= Channels.Beta;
                                break;
                            case 2:
                                channels |= Channels.Dev;
                                break;
                            case 3:
                                channels |= Channels.Canary;
                                break;
                        }
                    }
                }
            }
            return channels;
        }

        private static string ReleaseChannelsToString(Channels channels)
        {
            if (channels == allChannels)
            {
                return "";
            }
            else
            {
                List<string> channelStrings = new List<string>();
                if ((channels & Channels.Stable) != 0)
                {
                    channelStrings.Add("0");
                }
                if ((channels & Channels.Beta) != 0)
                {
                    channelStrings.Add("1");
                }
                if ((channels & Channels.Dev) != 0)
                {
                    channelStrings.Add("2");
                }
                if ((channels & Channels.Canary) != 0)
                {
                    channelStrings.Add("3");
                }
                return string.Join(",", channelStrings);
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
            string channelSearchKindAsString = (string)envVars["WEBVIEW2_CHANNEL_SEARCH_KIND"];
            string releaseChannelsAsString = (string)envVars["WEBVIEW2_RELEASE_CHANNELS"];
            bool reverseChannelSearch = (releaseChannelPreferenceAsString == "1") || (channelSearchKindAsString == "1");

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
                !String.IsNullOrEmpty(releaseChannelPreferenceAsString) ||
                !String.IsNullOrEmpty(channelSearchKindAsString) ||
                !String.IsNullOrEmpty(releaseChannelsAsString))
            {
                entry.HostApp = "*";
                entry.StorageKind = storageKind;
                entry.UserDataPath = userDataFolder;
                entry.RuntimePath = browserExecutableFolder;
                entry.BrowserArguments = additionalBrowserArguments;
                entry.ReverseSearchOrder = reverseChannelSearch;
                entry.ReleaseChannels = ReleaseChannelsFromString(releaseChannelsAsString);

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
            Environment.SetEnvironmentVariable("WEBVIEW2_CHANNEL_SEARCH_KIND", null, target);
            Environment.SetEnvironmentVariable("WEBVIEW2_RELEASE_CHANNELS", null, target);
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
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathChannelSearchKind, true), entry.HostApp);
                RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannels, true), entry.HostApp);
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
            SetEnvironmentVariableIfChanged("WEBVIEW2_CHANNEL_SEARCH_KIND", entry.ReverseSearchOrder ? "1" : null, target);
            SetEnvironmentVariableIfChanged("WEBVIEW2_RELEASE_CHANNELS", StringEmptyToNull(ReleaseChannelsToString(entry.ReleaseChannels)), target);
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

                    if (!String.IsNullOrEmpty(entry.RuntimePath))
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, true)?.SetValue(entry.HostApp, entry.RuntimePath, RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathBrowserExecutableFolder, true), entry.HostApp);
                    }
                    
                    if (entry.ReverseSearchOrder)
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathChannelSearchKind, true)?.SetValue(entry.HostApp, entry.ReverseSearchOrder ? 1 : 0, RegistryValueKind.DWord);
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, true)?.SetValue(entry.HostApp, entry.ReverseSearchOrder ? 1 : 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathChannelSearchKind, true), entry.HostApp);
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannelPreference, true), entry.HostApp);
                    }
                    
                    if (!String.IsNullOrEmpty(entry.UserDataPath))
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, true)?.SetValue(entry.HostApp, entry.UserDataPath, RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathUserDataFolder, true), entry.HostApp);
                    }

                    string releaseChannelsAsString = ReleaseChannelsToString(entry.ReleaseChannels);
                    if (!String.IsNullOrEmpty(releaseChannelsAsString))
                    {
                        RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannels, true)?.SetValue(entry.HostApp, releaseChannelsAsString, RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryUtil.DeleteValueIfItExists(RegistryUtil.OpenRegistryPath(registryRoot, s_registryPathReleaseChannels, true), entry.HostApp);
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
        EVCU = 0,
        EVLM = 1,
        HKLM = 2,
        HKCU = 3,
    };

    public class AppOverrideEntry : INotifyPropertyChanged
    {
        public string PrecedenceCategory
        {
            get
            {
                // Precedence is EVCU, EVLM, HKLM, and HKCU.
                // Within HKLM and HKCU it should be sorted
                // with explicit HostApps first and '*' after.
                // We want to return a string with an integer
                // such that the AppOverrideEntry objects are
                // sorted by their precedence when sorting
                // alphabetically on the string of this property.
                // This exists as a string in order to work with the
                // ListBox.Items.SortDescriptions. There's probably a
                // better way to ensure this is sorted properly.
                int precedence = (int)StorageKind * 2;
                if (HostApp == "*")
                {
                    ++precedence;
                }
                return precedence.ToString("D4");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private bool m_InitializationComplete = false;
        public StorageKind StorageKind
        {
            get => m_StorageKind;
            set
            {
                if (StorageKind != value)
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
                if (ReverseSearchOrder != value)
                {
                    m_ReverseSearchOrder = value;
                    OnPropertyChanged("ReverseSearchOrder");
                    OnPropertyChanged("IsRuntimeEvergreen");
                    OnPropertyChanged("IsRuntimeEvergreenPreview");
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
                if (HostApp != value)
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
                if (RuntimePath != value)
                {
                    m_RuntimePath = NullToEmpty(value);
                    OnPropertyChanged("RuntimePath");
                    OnPropertyChanged("IsRuntimeEvergreen");
                    OnPropertyChanged("IsRuntimeEvergreenPreview");
                    OnPropertyChanged("IsRuntimeFixedVersion");
                }
            }
        }
        private string m_RuntimePath = "";

        public string UserDataPath
        {
            get => m_UserDataPath;
            set
            {
                if (UserDataPath != value)
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
                if (BrowserArguments != value)
                {
                    m_BrowserArguments = NullToEmpty(value);
                    OnPropertyChanged("BrowserArguments");
                }
            }
        }
        private string m_BrowserArguments = "";

        public Channels ReleaseChannels
        {
            get => m_ReleaseChannels;
            set
            {
                if (ReleaseChannels != value)
                {
                    m_ReleaseChannels = value;
                    OnPropertyChanged("ReleaseChannels");
                    
                    OnPropertyChanged("IsRuntimeDev");
                    OnPropertyChanged("IsRuntimeStable");
                    OnPropertyChanged("IsRuntimeBeta");
                    OnPropertyChanged("IsRuntimeCanary");

                    OnPropertyChanged("IsRuntimeEvergreen");
                    OnPropertyChanged("IsRuntimeEvergreenPreview");
                }
            }
        }

        [Flags]
        public enum Channels
        {
            None = 0,
            Stable = 1,
            Beta = 2,
            Dev = 4,
            Canary = 8
        }
        private const Channels allChannels =
            Channels.Stable |
            Channels.Beta |
            Channels.Dev |
            Channels.Canary;
        private Channels m_ReleaseChannels = allChannels;

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
            get => !(ReverseSearchOrder || ReleaseChannels != allChannels) && RuntimePath == "";
            set
            {
                if (IsRuntimeEvergreen != value)
                {
                    if (value)
                    {
                        ReverseSearchOrder = false;
                        ReleaseChannels = allChannels;
                        RuntimePath = null;
                    }
                    OnPropertyChanged("IsRuntimeEvergreen");
                }
            }
        }

        public bool IsRuntimeEvergreenPreview
        {
            get => (ReverseSearchOrder || ReleaseChannels != allChannels) && RuntimePath == "";
            set
            {
                if (IsRuntimeEvergreenPreview != value)
                {
                    if (value)
                    {
                        ReverseSearchOrder = true;
                        RuntimePath = null;
                    }

                    OnPropertyChanged("IsRuntimeEvergreenPreview");
                }
            }
        }

        public bool IsRuntimeFixedVersion
        {
            get => RuntimePath != "";
            set
            {
                if (value != IsRuntimeFixedVersion)
                {
                    if (value)
                    {
                        ReverseSearchOrder = false;
                        ReleaseChannels = allChannels;
                        RuntimePath = ".";
                    }
                    OnPropertyChanged("IsRuntimeFixedVersion");
                }
            }
        }

        public bool IsRuntimeStable
        {
            get => (ReleaseChannels & Channels.Stable) != 0;
            set
            {
                if (IsRuntimeStable != value)
                {
                    if (value)
                    {
                        ReleaseChannels |= Channels.Stable;
                    }
                    else
                    {
                        ReleaseChannels &= ~Channels.Stable;
                    }
                    OnPropertyChanged("IsRuntimeStable");
                }
            }
        }

        public bool IsRuntimeBeta
        {
            get => (ReleaseChannels & Channels.Beta) != 0;
            set
            {
                if (value != IsRuntimeBeta)
                {
                    if (value)
                    {
                        ReleaseChannels |= Channels.Beta;
                    }
                    else
                    {
                        ReleaseChannels &= ~Channels.Beta;
                    }
                    OnPropertyChanged("IsRuntimeBeta");
                }
            }
        }

        public bool IsRuntimeDev
        {
            get => (ReleaseChannels & Channels.Dev) != 0;
            set
            {
                if (value != IsRuntimeDev)
                {
                    if (value)
                    {
                        ReleaseChannels |= Channels.Dev;
                    }
                    else
                    {
                        ReleaseChannels &= ~Channels.Dev;
                    }
                    OnPropertyChanged("IsRuntimeDev");
                }
            }
        }

        public bool IsRuntimeCanary
        {
            get => (ReleaseChannels & Channels.Canary) != 0;
            set
            {
                if (value != IsRuntimeCanary)
                {
                    if (value)
                    {
                        ReleaseChannels |= Channels.Canary;
                    }
                    else
                    {
                        ReleaseChannels &= ~Channels.Canary;
                    }
                    OnPropertyChanged("IsRuntimeCanary");
                }
            }
        }

        private string NullToEmpty(string inp) { return inp == null ? "" : inp; }
    }
}
