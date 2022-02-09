using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace wv2util
{
    public class HostAppEntry : IEquatable<HostAppEntry>
    {
        public HostAppEntry(string exePath, int pid, string runtimePath, string userDataPath)
        {
            ExecutablePath = exePath == null ? "Unknown" : exePath;
            Runtime = new RuntimeEntry(runtimePath);
            UserDataPath = userDataPath == null ? "Unknown" : userDataPath;
            this.PID = pid;
        }
        public string ExecutableName
        {
            get
            {
                return ExecutablePath.Split(new char[] { '\\', '/' }).ToList<string>().Last<string>();
            }
        }
        public int PID { get; private set; } = 0;
        public string ExecutablePath { get; private set; }
        public RuntimeEntry Runtime { get; private set; }
        public string UserDataPath { get; set; }

        public bool Equals(HostAppEntry other)
        {
            return ExecutablePath == other.ExecutablePath &&
                UserDataPath == other.UserDataPath &&
                Runtime.Equals(other.Runtime);
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
        protected Task m_fromMachineInProgress = null;
        protected async Task FromMachineInnerAsync()
        {

            IEnumerable<HostAppEntry> newEntries = null;

            await Task.Factory.StartNew(() =>
            {
                ProcessSnapshotHelper.ReloadSnapshot();
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
            foreach (var entry in this.Except(newEntries).ToList<HostAppEntry>())
            {
                this.Items.Remove(entry);
            }
            foreach (var entry in newEntries.Except(this).ToList<HostAppEntry>())
            {
                this.Items.Add(entry);
            }
            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Sort<T>(Comparison<T> comparison)
        {
            ArrayList.Adapter((IList)this.Items).Sort(new wv2util.SortUtil.ComparisonComparer<T>(comparison));

            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachine()
        {
            var entriesByClientDllList = GetHostAppEntriesFromMachineByClientDll().ToList();
            var entriesByClientDll = entriesByClientDllList.ToLookup(entry => entry.PID);
            var entriesBySnapshot = GetHostAppEntriesFromMachineByProcessSnapshot().ToList().Where(entry => entry.PID != 0);

            foreach (var entry in entriesBySnapshot)
            {
                if (entry.UserDataPath != "Unknown")
                {
                    var entriesMatchingPID = entriesByClientDll[entry.PID];
                    foreach (var entryMatch in entriesMatchingPID)
                    {
                        entryMatch.UserDataPath = entry.UserDataPath;
                    }
                }
            }

            return entriesByClientDllList;
        }

        // Determining host apps by client DLL has some drawbacks:
        //  * The host app may not actively be using WebView2 may just have the DLL loaded from a previous use
        //  * We don't know associated browser processes
        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByClientDll()
        {
            foreach (var moduleEntry in ProcessSnapshotHelper.PidToClientDllPath)
            {
                uint pid = moduleEntry.Key;
                string clientDll = moduleEntry.Value;
                Process process = Process.GetProcessById((int)pid);
                HostAppEntry entry = new HostAppEntry(
                    process.MainModule.FileName,
                    process.Id,
                    Path.Combine(clientDll, "..\\..\\..\\msedgewebview2.exe"),
                    null);

                yield return entry;
            }
        }

        private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByProcessSnapshot()
        {
            var processes = Process.GetProcessesByName("msedgewebview2");
            foreach (var process in processes)
            {
                var commandLineParts = process.GetCommandLine().Split(' ');
                string processType = null;
                string userDataPath = null;
                foreach (var commandLinePart in commandLineParts)
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

                if (processType == null)
                {
                    int? parentPID = process?.ParentProcess()?.Id;
                    HostAppEntry entry = new HostAppEntry(
                        process?.ParentProcess()?.MainModule.FileName,
                        parentPID.GetValueOrDefault(0),
                        process?.MainModule.FileName,
                        userDataPath);
                    yield return entry;
                }
            }
        }
    }

    public static class ProcessExtensions
    {
        public static string GetCommandLine(this Process process)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
            using (ManagementObjectCollection objects = searcher.Get())
            {
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }

        }
    }

    public static class ProcessSnapshotHelper
    {
        private static ProcessSnapshot s_snapShot = new ProcessSnapshot();
        public static void ReloadSnapshot()
        {
            s_snapShot.Reload();
        }

        public static Process ParentProcess(this Process process)
        {
            return s_snapShot.GetParentProcess(process);
        }
        public static IReadOnlyDictionary<uint, string> PidToClientDllPath { get => s_snapShot.PidToClientDllPath; }
    }

    public class ProcessSnapshot
    {
        private static readonly uint TH32CS_SNAPPROCESS = 0x2;
        private static readonly uint TH32CS_SNAPMODULE = 0x8;
        private static readonly uint TH32CS_SNAPMODULE32 = 0x10;
        private IntPtr m_hSnapshot = IntPtr.Zero;
        private Dictionary<uint, uint> m_ChildPidToParentPid = new Dictionary<uint, uint>();
        public void Reload()
        {
            if (m_hSnapshot != IntPtr.Zero)
            {
                CloseHandle(m_hSnapshot);
            }

            m_hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

            CreateDictionaryCache();
        }

        public Dictionary<uint, string> PidToClientDllPath { get; private set; }

        private void CreateDictionaryCache()
        {
            m_ChildPidToParentPid = new Dictionary<uint, uint>();
            PidToClientDllPath = new Dictionary<uint, string>();

            PROCESSENTRY32 procInfo = new PROCESSENTRY32() { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };

            MODULEENTRY32 modEntry = new MODULEENTRY32() { dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32)) };

            if (Process32First(m_hSnapshot, ref procInfo))
            {
                do
                {
                    if (procInfo.szExeFile.ToLower() != "lsass.exe" ||
                        procInfo.szExeFile.ToLower() != "svchost.exe")
                        m_ChildPidToParentPid.Add(procInfo.th32ProcessID, procInfo.th32ParentProcessID);

                    IntPtr hModuleSnapshot = CreateToolhelp32Snapshot(
                        TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, procInfo.th32ProcessID);

                    if (Module32FirstW(hModuleSnapshot, ref modEntry))
                    {
                        do
                        {
                            if (modEntry.szModule.ToLower() == "embeddedbrowserwebview.dll")
                            {
                                PidToClientDllPath.Add(modEntry.th32ProcessID, modEntry.szExePath);
                            }
                        }
                        while (Module32NextW(hModuleSnapshot, ref modEntry));
                    }
                    CloseHandle(hModuleSnapshot);
                }
                while (Process32Next(m_hSnapshot, ref procInfo)); // Read next
            }
        }

        private void EnsureSnapshot()
        {
            if (m_hSnapshot == IntPtr.Zero)
            {
                Reload();
            }
        }

        /// <summary>
        /// Returns the Parent Process of a Process
        /// </summary>
        /// <param name="process">The Windows Process.</param>
        /// <returns>The Parent Process of the Process.</returns>
        public Process GetParentProcess(Process childProcess)
        {
            uint parentPid = 0;

            EnsureSnapshot();

            if (m_ChildPidToParentPid.TryGetValue((uint)childProcess.Id, out parentPid))
            {
                try
                {
                    return Process.GetProcessById((int)parentPid);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Takes a snapshot of the specified processes, as well as the heaps, 
        /// modules, and threads used by these processes.
        /// </summary>
        /// <param name="dwFlags">
        /// The portions of the system to be included in the snapshot.
        /// </param>
        /// <param name="th32ProcessID">
        /// The process identifier of the process to be included in the snapshot.
        /// </param>
        /// <returns>
        /// If the function succeeds, it returns an open handle to the specified snapshot.
        /// If the function fails, it returns INVALID_HANDLE_VALUE.
        /// </returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        /// <summary>
        /// Retrieves information about the first process encountered in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot.</param>
        /// <param name="lppe">A pointer to a PROCESSENTRY32 structure.</param>
        /// <returns>
        /// Returns TRUE if the first entry of the process list has been copied to the buffer.
        /// Returns FALSE otherwise.
        /// </returns>
        [DllImport("kernel32.dll")]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Retrieves information about the next process recorded in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot.</param>
        /// <param name="lppe">A pointer to a PROCESSENTRY32 structure.</param>
        /// <returns>
        /// Returns TRUE if the next entry of the process list has been copied to the buffer.
        /// Returns FALSE otherwise.</returns>
        [DllImport("kernel32.dll")]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("kernel32.dll")]
        static extern bool Module32FirstW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        static extern bool Module32NextW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [StructLayout(LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public struct MODULEENTRY32
        {
            internal uint dwSize;
            internal uint th32ModuleID;
            internal uint th32ProcessID;
            internal uint GlblcntUsage;
            internal uint ProccntUsage;
            internal IntPtr modBaseAddr;
            internal uint modBaseSize;
            internal IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            internal string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string szExePath;
        }

        /// <summary>
        /// Describes an entry from a list of the processes residing 
        /// in the system address space when a snapshot was taken.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }
    }
}
