using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public static class ProcessUtil
    {
        private readonly static string[] InterestingDllFileNames = new string[]
        {
            "embeddedbrowserwebview.dll",
            "microsoft.ui.xaml.dll",
            "microsoft.web.webview2.core.dll",
            "microsoft.web.webview2.core.winmd",
            "microsoft.web.webview2.winforms.dll",
            "microsoft.web.webview2.wpf.dll",
            "presentationframework.dll",
            "presentationframework.ni.dll",
            "system.windows.forms.dll",
            "system.windows.forms.ni.dll",
            "webview2loader.dll",
        };

        public static Tuple<string, string, string[]> GetInterestingDllsUsedByPid(int pid)
        {
            string[] interestingDllPaths = GetInterestingDllsUsedByPidViaCreateToolhelp32Snapshot(pid);

            string clientDllPath = null;
            string sdkDllPath = null;
            foreach (string interestingDllPath in interestingDllPaths)
            {
                string interestingDllFileName = Path.GetFileName(interestingDllPath).ToLower();
                if (interestingDllFileName == "embeddedbrowserwebview.dll")
                {
                    clientDllPath = interestingDllPath;
                }
                else if ((interestingDllFileName == "webview2loader.dll" && sdkDllPath == null)
                    || interestingDllFileName == "microsoft.web.webview2.core.dll")
                {
                    // Microsoft.Web.WebView2.Core.dll provides more info about the host app so let that win against webview2loader.dll
                    sdkDllPath = interestingDllPath;
                }
            }
            return new Tuple<string, string, string[]>(clientDllPath, sdkDllPath, interestingDllPaths);
        }

        public static string[] GetInterestingDllsUsedByPidViaCreateToolhelp32Snapshot(int pid)
        { 
            List<string> interestingDllPaths = new List<string>();

            unsafe
            {
                PInvoke.Kernel32.MODULEENTRY32 modEntry = new PInvoke.Kernel32.MODULEENTRY32() { dwSize = Marshal.SizeOf(typeof(PInvoke.Kernel32.MODULEENTRY32)) };
                var moduleSnapshot = PInvoke.Kernel32.CreateToolhelp32Snapshot(
                    PInvoke.Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPMODULE | PInvoke.Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPMODULE32, pid);

                if (PInvoke.Kernel32.Module32First(moduleSnapshot, ref modEntry))
                {
                    do
                    {
                        string moduleFileName = new string(modEntry.szModule).ToLower();
                        string modulePath = new string(modEntry.szExePath).ToLower();
                        if (InterestingDllFileNames.Contains(moduleFileName))
                        {
                            interestingDllPaths.Add(modulePath);
                        }
                    }
                    while (PInvoke.Kernel32.Module32Next(moduleSnapshot, ref modEntry));
                }
                moduleSnapshot.Close();
            }

            return interestingDllPaths.ToArray();
        }

        public static string GetCommandLine(this Process process)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
            using (ManagementObjectCollection objects = searcher.Get())
            {
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }
        }

        public static void OpenExplorerToFile(string path)
        {
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        // Returns true if the path is a DotNet DLL and returns false if its a Win32 DLL.
        public static bool IsDllDotNet(string path)
        {
            if (path != null && path != "")
            {
                try
                {
                    AssemblyName.GetAssemblyName(path);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }
    }
}
