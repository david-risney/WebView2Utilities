using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace wv2util
{
    public class HostAppEntryStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is HostAppEntry.HostAppStatus)
            {
                switch ((HostAppEntry.HostAppStatus)value)
                {
                    case HostAppEntry.HostAppStatus.Running:
                    default:
                        return new SolidColorBrush(SystemColors.ControlTextColor);

                    case HostAppEntry.HostAppStatus.Terminated:
                        return new SolidColorBrush(SystemColors.GrayTextColor);
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HostAppEntry : IEquatable<HostAppEntry>, IComparable<HostAppEntry>
    {
        public HostAppEntry(
            string exePath, // Path to the host app executable
            int pid, // PID of the host app process
            string sdkPath, // Path to a WebView2 SDK DLL
            string runtimePath, // Path to the WebView2 client DLL
            string userDataPath, // Path to the user data folder
            string[] interestingLoadedDllPaths, // a list of full paths of DLLs that are related to WebView2 in some way
            int browserProcessPid) // PID of the browser process
        {
            ExecutablePath = exePath == null ? "Unknown" : exePath;
            PID = pid;
            SdkInfo = new SdkFileInfo(sdkPath, interestingLoadedDllPaths);
            Runtime = new RuntimeEntry(runtimePath);
            UserDataPath = userDataPath == null ? "Unknown" : userDataPath;
            InterestingLoadedDllPaths = interestingLoadedDllPaths;
            BrowserProcessPID = browserProcessPid;
        }

        public string ExecutablePath { get; private set; }
        public string ExecutableName => Path.GetFileName(ExecutablePath);
        public string ExecutablePathDirectory => Path.GetDirectoryName(ExecutablePath);
        public int PID { get; private set; } = 0;
        public string PIDAndStatus => 
            "" + PID + 
            (this.Status != HostAppStatus.Running ? " " + this.StatusDescription : "");

        public SdkFileInfo SdkInfo { get; private set; }
        public RuntimeEntry Runtime { get; private set; }
        public string UserDataPath { get; private set; }
        public string[] InterestingLoadedDllPaths { get; private set; }
        public int BrowserProcessPID { get; private set; } = 0;
        public string IntegrityLevel
        {
            get
            {
                try
                {
                    return ProcessUtil.GetIntegrityLevelOfProcess(PID);
                }
                catch (Exception)
                {
                    // This may fail if PID is already invalid because
                    // the process closed. That's fine. Just return a
                    // unknown in that case.
                }
                return "Unknown";
            }
        }
        public string PackageFullName { get => ProcessUtil.GetPackageFullName(PID); }
        public enum HostAppStatus
        {
            Terminated,
            Running,
        };
        public HostAppStatus Status { get; set; } = HostAppStatus.Running;
        public string StatusDescription => this.Status == HostAppStatus.Running ? "Running" : "Terminated";

        public int CompareTo(HostAppEntry other)
        {
            int result = this.ExecutablePath.ToLower().CompareTo(other.ExecutablePath.ToLower());
            if (result == 0)
            {
                result = this.UserDataPath.ToLower().CompareTo(other.UserDataPath.ToLower());
                if (result == 0)
                {
                    result = this.Runtime.CompareTo(other.Runtime);
                }
            }
            return result;
        }

        public bool Equals(HostAppEntry other)
        {
            return this.CompareTo(other) == 0;
        }
    }

    public class SdkFileInfo
    {
        // Create an SdkFileInfo object from a path to a WebView2 SDK DLL
        // such as the full path to Microsoft.Web.WebView2.Core.dll or WebView2Loader.dll.
        public SdkFileInfo(string sdkPath, string[] interestingDlls)
        {
            Path = sdkPath;
            m_interestingDlls = interestingDlls;

<<<<<<< HEAD
            if (Path != null)
            {
                string fileName = System.IO.Path.GetFileName(Path).ToLower();
                m_isWinRT = fileName == "microsoft.web.webview2.core.winmd" ||
                    (fileName == "microsoft.web.webview2.core.dll" && !ProcessUtil.IsDllDotNet(Path));
            }
=======
            string fileName = System.IO.Path.GetFileName(Path)?.ToLower();
            m_isWinRT = fileName == "microsoft.web.webview2.core.winmd" ||
                (fileName == "microsoft.web.webview2.core.dll" && !ProcessUtil.IsDllDotNet(Path));
>>>>>>> 37b79b2 (add channelsearchkind and releasechannels)
        }
        private readonly bool m_isWinRT = false;
        private readonly string[] m_interestingDlls;

        public string Path { get; private set; }
        public string PathDirectory => System.IO.Path.GetDirectoryName(Path);
        public string Version => VersionUtil.GetVersionStringFromFilePath(Path);

        public string ApiKind
        {
            get
            {
                // Because DLL enumeration isn't telling us about .NET DLLs we
                // assume the API kind based on the UI framework if we have it.
                switch (UIFrameworkKind)
                {
                    case "WinForms":
                    case "WPF":
                        return "DotNet";
                    case "WinUI2":
                    case "WinUI3":
                        return "WinRT";
                    default:
                        {
                            if (Path != null && Path != "")
                            {
                                string fileName = System.IO.Path.GetFileName(Path).ToLower();
                                if (fileName == "webview2loader.dll")
                                {
                                    return "Win32";
                                }
                                else if (fileName == "microsoft.web.webview2.core.dll")
                                {
                                    return m_isWinRT ? "WinRT" : "DotNet";
                                }
                            }
                            break;
                        }
                }
                return "Unknown";
            }
        }

        public string UIFrameworkKind
        {
            get
            {
                string xamlDllPath = m_interestingDlls.FirstOrDefault(
                    dllPath => System.IO.Path.GetFileName(dllPath).ToLower() == "microsoft.ui.xaml.dll");
                if (xamlDllPath != null)
                {
                    var xamlDllVersion = VersionUtil.TryGetVersionFromFilePath(xamlDllPath);
                    if (xamlDllVersion != null)
                    {
                        switch (xamlDllVersion.ProductMajorPart)
                        {
                            case 2:
                                return "WinUI2";
                            case 3:
                                return "WinUI3";
                            default:
                                return "Unknown";
                        }
                    }
                }
                else
                {
                    string wpfDllPath = m_interestingDlls.FirstOrDefault(dllPath =>
                        {
                            string dllName = System.IO.Path.GetFileName(dllPath).ToLower();
                            return dllName == "microsoft.web.webview2.wpf.dll" ||
                                   dllName == "presentationframework.ni.dll" ||
                                   dllName == "presentationframework.dll";
                        });
                    if (wpfDllPath != null)
                    {
                        return "WPF";
                    }
                    else
                    {
                        string winFormsDllPath = m_interestingDlls.FirstOrDefault(dllPath =>
                            {
                                string dllName = System.IO.Path.GetFileName(dllPath).ToLower();
                                return dllName == "microsoft.web.webview2.winforms.dll" ||
                                       dllName == "system.windows.forms.ni.dll" ||
                                       dllName == "system.windows.forms.dll";

                            });
                        if (winFormsDllPath != null)
                        {
                            return "WinForms";
                        }
                    }
                }

                return "Unknown";
            }
        }
    }

    public class HostAppList : ObservableCollection<HostAppEntry>
    {
        public HostAppList()
        {
            _ = FromMachineAsync();
        }

        // This is clearly not thread safe. It assumes FromDiskAsync will only
        // be called from the same thread.
        public async Task FromMachineAsync()
        {
            if (m_fromMachineInProgress != null)
            {
                await m_fromMachineInProgress;
            }
            else
            {
                m_fromMachineInProgress = FromMachineInnerAsync();
                await m_fromMachineInProgress;
                m_fromMachineInProgress = null;
            }
        }
        public bool ShouldDiscoverSlowly { get; set; } = false;
        private bool m_previousFromMachineDiscoveredSlowly = false;
        protected Task m_fromMachineInProgress = null;
        protected async Task FromMachineInnerAsync()
        {
            bool nextDiscoveredSlowly = ShouldDiscoverSlowly;
            List<HostAppEntry> nextEntries = null;
            // Cache old entries. After we get the new entries,
            // remove old entries replaced by new entries,
            // update the remaining Status to Terminated, and add them back in.
            List<HostAppEntry> previousEntries = this.ToList();
            bool previousDiscoveredSlowly = m_previousFromMachineDiscoveredSlowly;

            await Task.Factory.StartNew(() =>
            {
                nextEntries = GetHostAppEntriesFromMachine(nextDiscoveredSlowly).ToList<HostAppEntry>();
            });
            

            // Update remaining oldEntries to note they are terminated (since we didn't find them in the list)
            // But only do this if we're at the same level of discovery.
            // If we change how we discover we might not find something the previous did and its still running
            if (nextDiscoveredSlowly == previousDiscoveredSlowly)
            {
                // Remove entries from oldEntries that are also in newEntries
                foreach (var nextEntry in nextEntries)
                {
                    previousEntries.Remove(nextEntry);
                }
                foreach (var previousEntry in previousEntries)
                {
                    previousEntry.Status = HostAppEntry.HostAppStatus.Terminated;
                    nextEntries.Add(previousEntry);
                }
            }

            // Only update the entries on the caller thread to ensure the
            // caller isn't trying to enumerate the entries while
            // we're updating them.
            m_previousFromMachineDiscoveredSlowly = nextDiscoveredSlowly;
            SetEntries(nextEntries);
        }

        private void SetEntries(IEnumerable<HostAppEntry> newEntries)
        {
            // Use ToList to get a fixed collection that won't get angry that we're calling
            // Add and Remove on it while enumerating.
            foreach (HostAppEntry entry in this.Except(newEntries).ToList<HostAppEntry>())
            {
                Items.Remove(entry);
            }
            foreach (HostAppEntry entry in newEntries.Except(this).ToList<HostAppEntry>())
            {
                Items.Add(entry);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Sort<T>(Comparison<T> comparison)
        {
            ArrayList.Adapter((IList)Items).Sort(new wv2util.SortUtil.ComparisonComparer<T>(comparison));

            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachine(bool shouldDiscoverSlowly)
        {
            IEnumerable<HostAppEntry> results;
            if (!shouldDiscoverSlowly)
            {
                results = GetHostAppEntriesFromMachineByPipeEnumeration();
                results = AddRuntimeProcessInfoToHostAppEntriesByHwndWalking(results);
            }
            else
            {
                results = GetHostAppEntriesFromMachineByProcessModules();
                results = AddRuntimeProcessInfoToHostAppEntriesByAllHwndWalking(results);
                results = AddRuntimeProcessInfoToHostAppEntriesByParentProcess(results);
            }

            return results;
        }

        private static IEnumerable<HostAppEntry> AddRuntimeProcessInfoToHostAppEntriesByParentProcess(IEnumerable<HostAppEntry> hostAppEntriesOriginal)
        {
            // Put in a list so we can replace entries or not as we go.
            List<HostAppEntry> hostAppEntriesResult = hostAppEntriesOriginal.ToList();
            var msedgewebview2Processes = Process.GetProcessesByName("msedgewebview2");
            foreach (var msedgewebview2Process in msedgewebview2Processes)
            {
                try
                {
                    int pid = msedgewebview2Process.Id;
                    // Get parent process of pid
                    var parentProcess = msedgewebview2Process.GetParentProcess();
                    if (parentProcess != null &&
                        parentProcess.ProcessName.ToLower() != "msedgewebview2")
                    {
                        int idx = hostAppEntriesResult.FindIndex(hostAppEntry => hostAppEntry.PID == parentProcess.Id);
                        if (idx != -1)
                        {
                            var hostAppEntry = hostAppEntriesResult[idx];
                            if (hostAppEntry.BrowserProcessPID == 0)
                            {
                                hostAppEntriesResult.RemoveAt(idx);

                                var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(msedgewebview2Process);
                                string userDataFolder = userDataPathAndProcessType.Item1;
                                hostAppEntriesResult.Add(
                                    new HostAppEntry(
                                        hostAppEntry.ExecutablePath,
                                        hostAppEntry.PID,
                                        hostAppEntry.SdkInfo.Path,
                                        hostAppEntry.Runtime.ExePath,
                                        userDataFolder,
                                        hostAppEntry.InterestingLoadedDllPaths,
                                        msedgewebview2Process.Id));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Exceptions from interacting with a process that's already terminated
                    // Just to be safe, catch those and skip the entry.
                }
            }
            return hostAppEntriesResult;
        }

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByProcessModules()
        {
            List<HostAppEntry> results = new List<HostAppEntry>();
            foreach (Process process in Process.GetProcesses())
            {
                var interestingDllPaths = ProcessUtil.GetInterestingDllsUsedByPid(process.Id);
                if (interestingDllPaths.Item1 != null || interestingDllPaths.Item2 != null)
                {
                    results.Add(new HostAppEntry(
                            process.MainModule.FileName,
                            process.Id,
                            interestingDllPaths.Item2,
                            ClientDllPathToRuntimePath(interestingDllPaths.Item1),
                            null,
                            interestingDllPaths.Item3,
                            0));
                }
            }
            return results;
        }

        public static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByPipeEnumeration()
        {
            // Mojo creates named pipes with a name like
            //  \\.\pipe\(LOCAL\)mojo.({name}_){creation PID}.{number}.{number}
            // So if we find all these pipes we can find out the PIDs of processes
            // that created mojo pipes. A subset of those will be webview2 host app
            // processes which we can check for by looking for them loading
            // embeddedbrowserwebview.dll
            string[] namedPipePaths = System.IO.Directory.GetFiles("\\\\.\\pipe\\");
            HashSet<int> pids = new HashSet<int>();

            foreach (string namedPipePath in namedPipePaths)
            {
                // Filter named pipes to just those with \\mojo in the name.
                if (namedPipePath.Contains("mojo."))
                {
                    // Take just the name of the named pipes and strip off the preceding \\.\pipe\... part.
                    string mojoPipeName = namedPipePath.Split('\\').LastOrDefault();

                    // Extract the PID from the named pipe name.
                    string[] mojoPipeNameParts = mojoPipeName.Split('.');
                    if (mojoPipeNameParts.Length > 1)
                    {
                        if (Int32.TryParse(mojoPipeNameParts[1], out int pid))
                        {
                            pids.Add(pid);
                        }
                    }
                }
            }

            // Now we take each PID and create a HostAppEntry.
            // We use ProcessSnapshot to figure out if the process has actually
            // loaded WebView2 related DLLs.
            List<HostAppEntry> hostAppEntries = new List<HostAppEntry>();
            foreach (var pid in pids)
            {
                Process process = TryGetProcessById(pid);
                if (process != null)
                {
                    var interestingDlls = ProcessUtil.GetInterestingDllsUsedByPid(pid);
                    string clientDllPath = interestingDlls.Item1;
                    string sdkDllPath = interestingDlls.Item2;
                    string[] interestingDllPaths = interestingDlls.Item3;
                    if (clientDllPath != null || sdkDllPath != null)
                    {
                        hostAppEntries.Add(new HostAppEntry(
                            process.MainModule.FileName,
                            process.Id,
                            sdkDllPath,
                            ClientDllPathToRuntimePath(clientDllPath),
                            null,
                            interestingDllPaths,
                            0));
                    }
                }
            };

            return hostAppEntries;
        }

        private static readonly string[] s_hostAppLeafHwndClassNames = new string[]
        {
            "Chrome_WidgetWin_0",
            "Windows.UI.Core.CoreComponentInputSource"
        };

        private static IEnumerable<HostAppEntry> AddRuntimeProcessInfoToHostAppEntriesByHwndWalking(
            IEnumerable<HostAppEntry> hostAppEntries)
        {
            List<HostAppEntry> hostAppEntriesWithRuntimePID = new List<HostAppEntry>();

            // We do work to avoid calling EnumWindows more than once.
            var pids = hostAppEntries.Select(entry => entry.PID).ToList();
            var allTopLevelHwnds = HwndUtil.GetTopLevelHwnds(hwnd => pids.Contains(HwndUtil.GetWindowProcessId(hwnd)));
            var pidToTopLevelHwndsMap = HwndUtil.CreatePidToHwndsMapFromHwnds(allTopLevelHwnds);

            // Now explore child windows to find running WebView2 runtime processes
            // We get all the PIDs of the host apps.
            foreach (HostAppEntry hostAppEntry in hostAppEntries)
            {
                bool added = false;

                try
                {
                    if (hostAppEntry.BrowserProcessPID == 0)
                    {
                        HashSet<int> runtimePids = new HashSet<int>();

                        // And find corresponding top level windows for just this PID.
                        if (pidToTopLevelHwndsMap.TryGetValue(hostAppEntry.PID, out var topLevelHwnds))
                        {
                            // Then find all child (and child of child of...) windows that have appropriate class name
                            foreach (var topLevelHwnd in topLevelHwnds)
                            {
                                var hostAppLeafHwnds = HwndUtil.GetDescendantWindows(
                                    topLevelHwnd,
                                    hwnd => !s_hostAppLeafHwndClassNames.Contains(HwndUtil.GetClassName(hwnd)),
                                    hwnd => s_hostAppLeafHwndClassNames.Contains(HwndUtil.GetClassName(hwnd)));
                                foreach (var hostAppLeafHwnd in hostAppLeafHwnds)
                                {
                                    IntPtr childHwnd = HwndUtil.GetChildWindow(hostAppLeafHwnd);
                                    if (childHwnd == IntPtr.Zero)
                                    {
                                        childHwnd = PInvoke.User32.GetProp(hostAppLeafHwnd, "CrossProcessChildHWND");
                                    }
                                    if (childHwnd != IntPtr.Zero)
                                    {
                                        runtimePids.Add(HwndUtil.GetWindowProcessId(childHwnd));
                                    }
                                }
                            }

                            foreach (var runtimePid in runtimePids)
                            {
                                if (runtimePid != hostAppEntry.PID)
                                {
                                    string userDataFolder = null;
                                    Process runtimeProcess = TryGetProcessById(runtimePid);
                                    if (runtimeProcess != null)
                                    {
                                        var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(runtimeProcess);
                                        userDataFolder = userDataPathAndProcessType.Item1;

                                        var runtimeEntry = new HostAppEntry(
                                            hostAppEntry.ExecutablePath,
                                            hostAppEntry.PID,
                                            hostAppEntry.SdkInfo.Path,
                                            hostAppEntry.Runtime.ExePath,
                                            userDataFolder,
                                            hostAppEntry.InterestingLoadedDllPaths,
                                            runtimePid);
                                        hostAppEntriesWithRuntimePID.Add(runtimeEntry);
                                        added = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Failure for hostAppEntry " + hostAppEntry + ": " + e);
                }

                if (!added)
                {
                    hostAppEntriesWithRuntimePID.Add(hostAppEntry);
                }
            }

            return hostAppEntriesWithRuntimePID;
        }

        private static IEnumerable<HostAppEntry> AddRuntimeProcessInfoToHostAppEntriesByAllHwndWalking(
            IEnumerable<HostAppEntry> hostAppEntriesOriginal)
        {
            if (hostAppEntriesOriginal.Any(entry => entry.BrowserProcessPID == 0))
            {
                List<HostAppEntry> hostAppEntriesResults = new List<HostAppEntry>();
                Dictionary<int, HashSet<int>> parentPidToChildPidsMap = new Dictionary<int, HashSet<int>>();
                var topLevelHwnds = HwndUtil.GetTopLevelHwnds(null, true);

                // Then find all child (and child of child of...) windows that have appropriate class name
                foreach (var topLevelHwnd in topLevelHwnds)
                {
                    var hostAppLeafHwnds = HwndUtil.GetDescendantWindows(
                        topLevelHwnd,
                        hwnd => !s_hostAppLeafHwndClassNames.Contains(HwndUtil.GetClassName(hwnd)),
                        hwnd => s_hostAppLeafHwndClassNames.Contains(HwndUtil.GetClassName(hwnd)));
                    foreach (var hostAppLeafHwnd in hostAppLeafHwnds)
                    {
                        IntPtr childHwnd = HwndUtil.GetChildWindow(hostAppLeafHwnd);
                        if (childHwnd == IntPtr.Zero)
                        {
                            childHwnd = PInvoke.User32.GetProp(hostAppLeafHwnd, "CrossProcessChildHWND");
                        }
                        if (childHwnd != IntPtr.Zero)
                        {
                            int parentPid = HwndUtil.GetWindowProcessId(hostAppLeafHwnd);
                            int childPid = HwndUtil.GetWindowProcessId(childHwnd);

                            if (parentPid != childPid)
                            {
                                if (!parentPidToChildPidsMap.TryGetValue(parentPid, out HashSet<int> childPids))
                                {
                                    parentPidToChildPidsMap.Add(parentPid, (childPids = new HashSet<int>()));
                                }
                                childPids.Add(childPid);
                            }
                        }
                    }
                }

                foreach (var hostAppEntry in hostAppEntriesOriginal)
                {
                    bool added = false;

                    try
                    {
                        if (hostAppEntry.BrowserProcessPID == 0)
                        {
                            if (parentPidToChildPidsMap.TryGetValue(hostAppEntry.PID, out HashSet<int> childPids))
                            {
                                foreach (int childPid in childPids)
                                {
                                    Process runtimeProcess = TryGetProcessById(childPid);
                                    if (runtimeProcess != null)
                                    {
                                        var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(runtimeProcess);
                                        string userDataFolder = userDataPathAndProcessType.Item1;

                                        var runtimeEntry = new HostAppEntry(
                                            hostAppEntry.ExecutablePath,
                                            hostAppEntry.PID,
                                            hostAppEntry.SdkInfo.Path,
                                            hostAppEntry.Runtime.ExePath,
                                            userDataFolder,
                                            hostAppEntry.InterestingLoadedDllPaths,
                                            childPid);
                                        hostAppEntriesResults.Add(runtimeEntry);
                                        added = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine("Error in AddRuntimeProcessInfoToHostAppEntriesByAllHwndWalking on hostAppEntry " + hostAppEntry + ": " + e);
                    }
                    parentPidToChildPidsMap.Remove(hostAppEntry.PID);

                    if (!added)
                    {
                        hostAppEntriesResults.Add(hostAppEntry);
                    }
                }

                return hostAppEntriesResults;
            }
            return hostAppEntriesOriginal;
        }

        private static Process TryGetProcessById(int pid)
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (Exception)
            {
                // The process may be gone by the time we call GetProcessById
                // That's fine. Just return null to the caller so they know
                // to skip.
            }
            return process;
        }

        private static string ClientDllPathToRuntimePath(string clientDllPath)
        {
            if (clientDllPath != null && clientDllPath != "")
            {
                return Path.Combine(clientDllPath, "..\\..\\..\\msedgewebview2.exe");
            }
            // We allow for a null or emptry string client DLL path to make
            // it to pass through null to the HostAppEntry ctor.
            return null;
        }

        private static Tuple<string, string> GetUserDataPathAndProcessTypeFromProcessViaCommandLine(Process process)
        {
            CommandLineUtil.CommandLine commandLine = new CommandLineUtil.CommandLine(process.GetCommandLine());
            string processType = commandLine.GetKeyValue("--type");
            string userDataPath = commandLine.GetKeyValue("--user-data-dir");

            if (userDataPath == "")
            {
                userDataPath = null;
            }
            if (processType == "")
            {
                processType = null;
            }

            return new Tuple<string, string>(userDataPath, processType);
        }
    }
}
