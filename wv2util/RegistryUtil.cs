using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace wv2util
{
    internal class RegistryUtil
    {
        public static readonly string s_webView2RegKey = @"Software\Policies\Microsoft\Edge\WebView2";

        public static void EnsureRegistryPaths(string[] registryPaths, RegistryKey root)
        {
            foreach (string registryPath in registryPaths)
            {
                RegistryUtil.EnsureRegistryPath(root, registryPath);
            }
        }

        public static void EnsureRegistryPath(RegistryKey root, string registryPath)
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

        public static RegistryKey OpenRegistryPath(RegistryKey root, string registryPath, bool write)
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

        public static void DeleteValueIfItExists(RegistryKey key, string valueName)
        {
            if (key != null && key.GetValue(valueName) != null)
            {
                key.DeleteValue(valueName);
            }
        }

        private static readonly string s_regEditKey = @"\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";
        public static void LaunchRegEdit()
        {
            RegistryKey regEditKey = OpenRegistryPath(Registry.CurrentUser, s_regEditKey, true);
            regEditKey.SetValue("LastKey", @"Computer\HKEY_CURRENT_USER\" + s_webView2RegKey);

            Process.Start(@"C:\Windows\regedit.exe");
        }
    }
}
