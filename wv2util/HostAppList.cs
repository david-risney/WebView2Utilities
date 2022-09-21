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

namespace wv2util
{
    public class HostAppEntry : IEquatable<HostAppEntry>
    {
        public HostAppEntry(
            string exePath, // Path to the host app executable
            int pid, // PID of the host app process
            string sdkPath, // Path to a WebView2 SDK DLL
            string runtimePath, // Path to the WebView2 client DLL
            string userDataPath, // Path to the user data folder
            int browserProcessPid) // PID of the browser process
        {
            ExecutablePath = exePath == null ? "Unknown" : exePath;
            PID = pid;
            SdkInfo = new SdkFileInfo(sdkPath);
            Runtime = new RuntimeEntry(runtimePath);
            UserDataPath = userDataPath == null ? "Unknown" : userDataPath;
            BrowserProcessPID = browserProcessPid;
        }

        public string ExecutablePath { get; private set; }
        public string ExecutableName => ExecutablePath.Split(new char[] { '\\', '/' }).ToList<string>().Last<string>();
        public int PID { get; private set; } = 0;
        public SdkFileInfo SdkInfo { get; private set; }
        public RuntimeEntry Runtime { get; private set; }
        public string UserDataPath { get; set; }
        public int BrowserProcessPID { get; private set; } = 0;

        public bool Equals(HostAppEntry other)
        {
            return ExecutablePath == other.ExecutablePath &&
                UserDataPath == other.UserDataPath &&
                Runtime.Equals(other.Runtime);
        }
    }

    public class SdkFileInfo
    {
        // Create an SdkFileInfo object from a path to a WebView2 SDK DLL
        // such as the full path to Microsoft.Web.WebView2.Core.dll or WebView2Loader.dll.
        public SdkFileInfo(string sdkPath) { Path = sdkPath; }

        public string Path { get; private set; }
        public string Version => VersionUtil.GetVersionStringFromFilePath(Path);
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
        protected Task m_fromMachineInProgress = null;
        protected async Task FromMachineInnerAsync()
        {

            IEnumerable<HostAppEntry> newEntries = null;

            await Task.Factory.StartNew(() =>
            {
                newEntries = GetHostAppEntriesFromMachine().ToList<HostAppEntry>();
            });

            // Only update the entries on the caller thread to ensure the
            // caller isn't trying to enumerate the entries while
            // we're updating them.
            SetEntries(newEntries);
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

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachine()
        {
            return AddRuntimeProcessInfoToHostAppEntries(GetHostAppEntriesFromMachineByPipeEnumeration());
        }

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByPipeEnumeration()
        {
            // Mojo creates named pipes with a name like
            //  \\.\pipe\(LOCAL\)mojo.({name}_){creation PID}.{number}.{number}
            // So if we find all these pipes we can find out the PIDs of processes
            // that created mojo pipes. A subset of those will be webview2 host app
            // processes which we can check for by looking for them loading
            // embeddedbrowserwebview.dll
            string[] namedPipePaths = System.IO.Directory.GetFiles("\\\\.\\pipe\\");
            HashSet<uint> pids = new HashSet<uint>();

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
                        if (UInt32.TryParse(mojoPipeNameParts[1], out uint pid))
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
                Process process = TryGetProcessById((int)pid);
                if (process != null)
                {
                    var clientDllPathAndSdkDllPath = ProcessUtil.GetClientDllPathAndSdkDllPathFromPid(pid);
                    string clientDllPath = clientDllPathAndSdkDllPath.Item1;
                    string sdkDllPath = clientDllPathAndSdkDllPath.Item2;
                    if (clientDllPath != null || sdkDllPath != null)
                    {
                        hostAppEntries.Add(new HostAppEntry(
                            process.MainModule.FileName,
                            process.Id,
                            sdkDllPath,
                            ClientDllPathToRuntimePath(clientDllPath),
                            null,
                            0));
                    }
                }
            };

            return hostAppEntries;
        }

        private static IEnumerable<HostAppEntry> AddRuntimeProcessInfoToHostAppEntries(
            IEnumerable<HostAppEntry> hostAppEntries)
        {
            List<HostAppEntry> hostAppEntriesWithRuntimePID = new List<HostAppEntry>();

            // We do work to avoid calling EnumWindows more than once.
            var pids = hostAppEntries.Select(entry => entry.PID).ToList();
            var allTopLevelHwnds = HwndUtil.GetTopLevelHwnds(hwnd => pids.Contains(HwndUtil.GetWindowProcessId(hwnd)));
            var pidToTopLevelHwndsMap = HwndUtil.CreatePidToHwndsMapFromHwnds(allTopLevelHwnds);

            // Now explore child windows to find running WebView2 runtime processes
            // We get all the PIDs of the host apps.
            foreach (var hostAppEntry in hostAppEntries)
            {
                HashSet<int> runtimePids = new HashSet<int>();

                // And find corresponding top level windows for just this PID.
                var topLevelHwnds = pidToTopLevelHwndsMap[hostAppEntry.PID];

                // Then find all child (and child of child of...) windows that have appropriate class name
                foreach (var topLevelHwnd in topLevelHwnds)
                {
                    const string hostAppLeafHwndClassName = "Chrome_WidgetWin_0";
                    var hostAppLeafHwnds = HwndUtil.GetDescendantWindows(
                        topLevelHwnd,
                        hwnd => HwndUtil.GetClassName(hwnd) != hostAppLeafHwndClassName,
                        hwnd => HwndUtil.GetClassName(hwnd) == hostAppLeafHwndClassName);
                    foreach (var hostAppLeafHwnd in hostAppLeafHwnds)
                    {
                        IntPtr childHwnd = HwndUtil.GetChildWindow(hostAppLeafHwnd);
                        if (childHwnd == IntPtr.Zero)
                        {
                            childHwnd = HwndUtil.GetPropW(hostAppLeafHwnd, "CrossProcessParentHWND");
                        }
                        if (childHwnd != IntPtr.Zero)
                        {
                            runtimePids.Add(HwndUtil.GetWindowProcessId(childHwnd));
                        }
                    }
                }

                bool added = false;
                foreach (var runtimePid in runtimePids)
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
                            runtimePid);
                        hostAppEntriesWithRuntimePID.Add(runtimeEntry);
                        added = true;
                    }
                }

                if (!added)
                {
                    hostAppEntriesWithRuntimePID.Add(hostAppEntry);
                }
            }

            return hostAppEntriesWithRuntimePID;
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
            string[] commandLineParts = process.GetCommandLine().Split(' ');
            string processType = null;
            string userDataPath = null;
            foreach (string commandLinePart in commandLineParts)
            {
                string commandLinePartTrimmed = commandLinePart.Trim().Replace("\\\"", "\"").Trim('"');
                if (commandLinePartTrimmed.StartsWith("--type"))
                {
                    processType = commandLinePartTrimmed.Split('=')[1].Trim(new char[] { '"', ' ' });
                }

                if (commandLinePartTrimmed.StartsWith("--user-data-dir"))
                {
                    userDataPath = commandLinePartTrimmed.Split('=')[1].Trim(new char[] { '"', ' ' });
                }
            }
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
