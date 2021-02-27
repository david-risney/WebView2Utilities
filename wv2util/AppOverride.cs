using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public class AppOverrideList
    {
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

        public static List<AppOverrideEntry> CreateFromRegistry()
        {
            EnsureRegistryPaths();
            Dictionary<string, AppOverrideEntry> appNameToEntry = new Dictionary<string, AppOverrideEntry>();
            RegistryKey regKey;
            string[] valueNames;

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = null;
                if (!appNameToEntry.TryGetValue(valueName, out entry))
                {
                    entry = new AppOverrideEntry();
                }
                entry.RuntimePath = (string)regKey.GetValue(valueName);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = null;
                if (!appNameToEntry.TryGetValue(valueName, out entry))
                {
                    entry = new AppOverrideEntry();
                }
                entry.ReverseSearchOrder = (1 == (int)regKey.GetValue(valueName));
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = null;
                if (!appNameToEntry.TryGetValue(valueName, out entry))
                {
                    entry = new AppOverrideEntry();
                }
                entry.BrowserArguments = (string)regKey.GetValue(valueName);
            }

            regKey = OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, false);
            valueNames = regKey.GetValueNames();
            foreach (string valueName in valueNames)
            {
                AppOverrideEntry entry = null;
                if (!appNameToEntry.TryGetValue(valueName, out entry))
                {
                    entry = new AppOverrideEntry();
                }
                entry.UserDataPath = (string)regKey.GetValue(valueName);
            }

            return appNameToEntry.Values.ToList();
        }

        public static void ApplyToRegistry(List<AppOverrideEntry> entries)
        {
            List<AppOverrideEntry> registryEntries = CreateFromRegistry();
            foreach (var entry in registryEntries.Except(entries))
            {
                RemoveEntryFromRegistry(entry);
            }
            foreach (var entry in entries)
            {
                ApplyEntryToRegistry(entry);
            }
        }

        private static void RemoveEntryFromRegistry(AppOverrideEntry entry)
        {
            OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, true)?.DeleteValue(entry.HostApp);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, true)?.DeleteValue(entry.HostApp);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, true)?.DeleteValue(entry.HostApp);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, true)?.DeleteValue(entry.HostApp);
        }

        private static void ApplyEntryToRegistry(AppOverrideEntry entry)
        {
            OpenRegistryPath(Registry.CurrentUser, s_registryPathAdditionalBrowserArguments, true)?.SetValue(entry.HostApp, entry.BrowserArguments, RegistryValueKind.String);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathBrowserExecutableFolder, true)?.SetValue(entry.HostApp, entry.RuntimePath, RegistryValueKind.String);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathReleaseChannelPreference, true)?.SetValue(entry.HostApp, entry.ReverseSearchOrder ? 1 : 0, RegistryValueKind.DWord);
            OpenRegistryPath(Registry.CurrentUser, s_registryPathUserDataFolder, true)?.SetValue(entry.HostApp, entry.UserDataPath, RegistryValueKind.String);
        }
    }

    public class AppOverrideEntry
    {
        public bool ReverseSearchOrder { get; set; }
        public string HostApp { get; set; }
        public string RuntimePath { get; set; }
        public string UserDataPath { get; set; }
        public string BrowserArguments { get; set; }
    }
}
