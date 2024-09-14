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

namespace WebView2Utilities.Core.Models;

/*
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
*/

public class HostAppEntry : IEquatable<HostAppEntry>, IComparable<HostAppEntry>
{
    public HostAppEntry(
        string kind, // 'host', or the Edge browser kind 'browser', 'renderer', and so on.
        string exePath, // Path to the host app executable
        int pid, // PID of the host app process
        int parentPid, // PID of the parent process
        string sdkPath, // Path to a WebView2 SDK DLL
        string runtimePath, // Path to the WebView2 client DLL
        string userDataPath, // Path to the user data folder
        string[] interestingLoadedDllPaths, // a list of full paths of DLLs that are related to WebView2 in some way
        int browserProcessPid) // PID of the browser process
    {
        Kind = kind;
        ExecutablePath = exePath == null ? "Unknown" : exePath;
        PID = pid;
        ParentPID = parentPid;
        SdkInfo = new SdkFileInfo(sdkPath, interestingLoadedDllPaths);
        Runtime = new RuntimeEntry(runtimePath);
        UserDataPath = userDataPath == null ? "Unknown" : userDataPath;
        InterestingLoadedDllPaths = interestingLoadedDllPaths;
        BrowserProcessPID = browserProcessPid;
    }

    public string DisplayLabel
    {
        get
        {
            var displayLabel = "";
            switch (Kind)
            {
                case "host":
                    displayLabel = ExecutableName + " " + PIDAndStatus;
                    break;

                default:
                    displayLabel = Kind + " " + PIDAndStatus;
                    break;
            }

            return displayLabel;
        }
    }

    public string Kind
    {
        get; private set;
    }
    public string ExecutablePath
    {
        get; private set;
    }
    public string ExecutableName => Path.GetFileName(ExecutablePath);
    public string ExecutablePathDirectory => Path.GetDirectoryName(ExecutablePath);
    public int PID { get; private set; } = 0;
    public int ParentPID { get; private set; } = 0;
    public string PIDAndStatus =>
        "" + PID +
        (Status != HostAppStatus.Running ? " " + StatusDescription : "");

    public SdkFileInfo SdkInfo
    {
        get; private set;
    }
    public RuntimeEntry Runtime
    {
        get; private set;
    }
    public string UserDataPath
    {
        get; private set;
    }
    public string[] InterestingLoadedDllPaths
    {
        get; private set;
    }
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
    public string PackageFullName
    {
        get => ProcessUtil.GetPackageFullName(PID);
    }
    public enum HostAppStatus
    {
        Terminated,
        Running,
    };
    public HostAppStatus Status { get; set; } = HostAppStatus.Running;
    public string StatusDescription => Status == HostAppStatus.Running ? "Running" : "Terminated";

    public int CompareTo(HostAppEntry other)
    {
        var result = ExecutablePath.ToLower().CompareTo(other.ExecutablePath.ToLower());
        if (result == 0)
        {
            result = UserDataPath.ToLower().CompareTo(other.UserDataPath.ToLower());
            if (result == 0)
            {
                result = Runtime.CompareTo(other.Runtime);
            }
        }
        return result;
    }

    public bool Equals(HostAppEntry other)
    {
        return CompareTo(other) == 0;
    }

    public List<HostAppEntry> Children { get; } = new List<HostAppEntry>();
}

public class SdkFileInfo
{
    // Create an SdkFileInfo object from a path to a WebView2 SDK DLL
    // such as the full path to Microsoft.Web.WebView2.Core.dll or WebView2Loader.dll.
    public SdkFileInfo(string sdkPath, string[] interestingDlls)
    {
        Path = sdkPath ?? "";
        m_interestingDlls = interestingDlls ?? new string[] { };

        if (!string.IsNullOrEmpty(Path))
        {
            var fileName = System.IO.Path.GetFileName(Path).ToLower();
            m_isWinRT = fileName == "microsoft.web.webview2.core.winmd" ||
                fileName == "microsoft.web.webview2.core.dll" && !ProcessUtil.IsDllDotNet(Path);
        }
    }
    private readonly bool m_isWinRT = false;
    private readonly string[] m_interestingDlls;

    public string Path
    {
        get; private set;
    }
    public string PathDirectory => !string.IsNullOrEmpty(Path) ? System.IO.Path.GetDirectoryName(Path) : "";
    public string Version => !string.IsNullOrEmpty(Path) ? VersionUtil.GetVersionStringFromFilePath(Path) : "";

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
                            var fileName = System.IO.Path.GetFileName(Path).ToLower();
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
            var xamlDllPath = m_interestingDlls.FirstOrDefault(
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
                var wpfDllPath = m_interestingDlls.FirstOrDefault(dllPath =>
                    {
                        var dllName = System.IO.Path.GetFileName(dllPath).ToLower();
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
                    var winFormsDllPath = m_interestingDlls.FirstOrDefault(dllPath =>
                        {
                            var dllName = System.IO.Path.GetFileName(dllPath).ToLower();
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
        var nextDiscoveredSlowly = ShouldDiscoverSlowly;
        List<HostAppEntry> nextEntries = null;
        // Cache old entries. After we get the new entries,
        // remove old entries replaced by new entries,
        // update the remaining Status to Terminated, and add them back in.
        var previousEntries = this.ToList();
        var previousDiscoveredSlowly = m_previousFromMachineDiscoveredSlowly;

        await Task.Factory.StartNew(() =>
        {
            nextEntries = GetHostAppEntriesFromMachine(nextDiscoveredSlowly).ToList();
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
        foreach (var entry in this.Except(newEntries).ToList())
        {
            Items.Remove(entry);
        }
        foreach (var entry in newEntries.Except(this).ToList())
        {
            Items.Add(entry);
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void Sort<T>(Comparison<T> comparison)
    {
        ArrayList.Adapter((IList)Items).Sort(new SortUtil.ComparisonComparer<T>(comparison));

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
        var hostAppEntriesResult = hostAppEntriesOriginal.ToList();
        var allEntriesOriginal = hostAppEntriesOriginal.SelectMany(entry => entry.Children).ToList();

        var pidToRuntimeHostAppEntry = new Dictionary<int, HostAppEntry>();
        allEntriesOriginal.ForEach(entry => pidToRuntimeHostAppEntry[entry.PID] = entry);

        var msedgewebview2Processes = Process.GetProcessesByName("msedgewebview2");
        foreach (var msedgewebview2Process in msedgewebview2Processes)
        {
            try
            {
                var pid = msedgewebview2Process.Id;
                // Get parent process of pid
                var parentProcess = msedgewebview2Process.GetParentProcess();
                var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(msedgewebview2Process);

                HostAppEntry currentProcessEntry = null;
                if (!pidToRuntimeHostAppEntry.TryGetValue(pid, out currentProcessEntry))
                {
                    pidToRuntimeHostAppEntry[pid] = currentProcessEntry = new HostAppEntry(
                        userDataPathAndProcessType.Item2,
                        msedgewebview2Process.MainModule.FileName,
                        msedgewebview2Process.Id,
                        parentProcess.Id,
                        null,
                        msedgewebview2Process.MainModule.FileName,
                        userDataPathAndProcessType.Item1,
                        null,
                        0);
                }

                if (parentProcess != null)
                {
                    if (parentProcess.ProcessName.ToLower() != "msedgewebview2")
                    {
                        var idx = hostAppEntriesResult.FindIndex(hostAppEntry => hostAppEntry.PID == parentProcess.Id);
                        if (idx != -1)
                        {
                            var hostAppEntry = hostAppEntriesResult[idx];
                            if (hostAppEntry.BrowserProcessPID == 0)
                            {
                                hostAppEntriesResult.RemoveAt(idx);

                                var userDataFolder = userDataPathAndProcessType.Item1;
                                var newHostAppEntry = new HostAppEntry(
                                    "host",
                                    hostAppEntry.ExecutablePath,
                                    hostAppEntry.PID,
                                    0,
                                    hostAppEntry.SdkInfo.Path,
                                    hostAppEntry.Runtime.ExePath,
                                    userDataFolder,
                                    hostAppEntry.InterestingLoadedDllPaths,
                                    msedgewebview2Process.Id);
                                newHostAppEntry.Children.AddRange(hostAppEntry.Children);
                                newHostAppEntry.Children.Add(currentProcessEntry);

                                hostAppEntriesResult.Add(newHostAppEntry);
                            }
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
        foreach (var childProcessEntry in pidToRuntimeHostAppEntry.Values)
        {
            HostAppEntry parentProcessEntry = null;
            if (pidToRuntimeHostAppEntry.TryGetValue(childProcessEntry.ParentPID, out parentProcessEntry))
            {
                parentProcessEntry.Children.Add(childProcessEntry);
            }
        }
        return hostAppEntriesResult;
    }

    private static IEnumerable<HostAppEntry> GetHostAppEntriesFromMachineByProcessModules()
    {
        var results = new List<HostAppEntry>();
        foreach (var process in Process.GetProcesses())
        {
            var interestingDllPaths = ProcessUtil.GetInterestingDllsUsedByPid(process.Id);
            if (interestingDllPaths.Item1 != null || interestingDllPaths.Item2 != null)
            {
                results.Add(new HostAppEntry(
                        "host",
                        process.MainModule.FileName,
                        process.Id,
                        0,
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
        var namedPipePaths = Directory.GetFiles("\\\\.\\pipe\\");
        var pids = new HashSet<int>();

        foreach (var namedPipePath in namedPipePaths)
        {
            // Filter named pipes to just those with \\mojo in the name.
            if (namedPipePath.Contains("mojo."))
            {
                // Take just the name of the named pipes and strip off the preceding \\.\pipe\... part.
                var mojoPipeName = namedPipePath.Split('\\').LastOrDefault();

                // Extract the PID from the named pipe name.
                var mojoPipeNameParts = mojoPipeName.Split('.');
                if (mojoPipeNameParts.Length > 1)
                {
                    if (int.TryParse(mojoPipeNameParts[1], out var pid))
                    {
                        pids.Add(pid);
                    }
                }
            }
        }

        // Now we take each PID and create a HostAppEntry.
        // We use ProcessSnapshot to figure out if the process has actually
        // loaded WebView2 related DLLs.
        var hostAppEntries = new List<HostAppEntry>();
        foreach (var pid in pids)
        {
            var process = TryGetProcessById(pid);
            if (process != null)
            {
                var interestingDlls = ProcessUtil.GetInterestingDllsUsedByPid(pid);
                var clientDllPath = interestingDlls.Item1;
                var sdkDllPath = interestingDlls.Item2;
                var interestingDllPaths = interestingDlls.Item3;
                if (clientDllPath != null || sdkDllPath != null)
                {
                    hostAppEntries.Add(new HostAppEntry(
                        "host",
                        process.MainModule.FileName,
                        process.Id,
                        0,
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
        var hostAppEntriesWithRuntimePID = new List<HostAppEntry>();

        // We do work to avoid calling EnumWindows more than once.
        var pids = hostAppEntries.Select(entry => entry.PID).ToList();
        var allTopLevelHwnds = HwndUtil.GetTopLevelHwnds(hwnd => pids.Contains(HwndUtil.GetWindowProcessId(hwnd)));
        var pidToTopLevelHwndsMap = HwndUtil.CreatePidToHwndsMapFromHwnds(allTopLevelHwnds);

        // Now explore child windows to find running WebView2 runtime processes
        // We get all the PIDs of the host apps.
        foreach (var hostAppEntry in hostAppEntries)
        {
            var added = false;

            try
            {
                if (hostAppEntry.BrowserProcessPID == 0)
                {
                    var runtimePids = new HashSet<int>();

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
                                var childHwnd = HwndUtil.GetChildWindow(hostAppLeafHwnd);
                                if (childHwnd == nint.Zero)
                                {
                                    childHwnd = PInvoke.User32.GetProp(hostAppLeafHwnd, "CrossProcessChildHWND");
                                }
                                if (childHwnd != nint.Zero)
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
                                var runtimeProcess = TryGetProcessById(runtimePid);
                                if (runtimeProcess != null)
                                {
                                    var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(runtimeProcess);
                                    userDataFolder = userDataPathAndProcessType.Item1;

                                    var runtimeEntry = new HostAppEntry(
                                        "host",
                                        hostAppEntry.ExecutablePath,
                                        hostAppEntry.PID,
                                        0,
                                        hostAppEntry.SdkInfo.Path,
                                        hostAppEntry.Runtime.ExePath,
                                        userDataFolder,
                                        hostAppEntry.InterestingLoadedDllPaths,
                                        runtimePid);
                                    runtimeEntry.Children.AddRange(hostAppEntry.Children);
                                    runtimeEntry.Children.Add(new HostAppEntry(
                                        userDataPathAndProcessType.Item2,
                                        runtimeProcess.MainModule.FileName,
                                        runtimeProcess.Id,
                                        runtimeEntry.PID,
                                        null,
                                        runtimeProcess.MainModule.FileName,
                                        userDataFolder,
                                        null,
                                        0));
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
            var hostAppEntriesResults = new List<HostAppEntry>();
            var parentPidToChildPidsMap = new Dictionary<int, HashSet<int>>();
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
                    var childHwnd = HwndUtil.GetChildWindow(hostAppLeafHwnd);
                    if (childHwnd == nint.Zero)
                    {
                        childHwnd = PInvoke.User32.GetProp(hostAppLeafHwnd, "CrossProcessChildHWND");
                    }
                    if (childHwnd != nint.Zero)
                    {
                        var parentPid = HwndUtil.GetWindowProcessId(hostAppLeafHwnd);
                        var childPid = HwndUtil.GetWindowProcessId(childHwnd);

                        if (parentPid != childPid)
                        {
                            if (!parentPidToChildPidsMap.TryGetValue(parentPid, out var childPids))
                            {
                                parentPidToChildPidsMap.Add(parentPid, childPids = new HashSet<int>());
                            }
                            childPids.Add(childPid);
                        }
                    }
                }
            }

            foreach (var hostAppEntry in hostAppEntriesOriginal)
            {
                var added = false;

                try
                {
                    if (hostAppEntry.BrowserProcessPID == 0)
                    {
                        if (parentPidToChildPidsMap.TryGetValue(hostAppEntry.PID, out var childPids))
                        {
                            foreach (var childPid in childPids)
                            {
                                var runtimeProcess = TryGetProcessById(childPid);
                                if (runtimeProcess != null)
                                {
                                    var userDataPathAndProcessType = GetUserDataPathAndProcessTypeFromProcessViaCommandLine(runtimeProcess);
                                    var userDataFolder = userDataPathAndProcessType.Item1;

                                    var runtimeEntry = new HostAppEntry(
                                        "host",
                                        hostAppEntry.ExecutablePath,
                                        hostAppEntry.PID,
                                        0,
                                        hostAppEntry.SdkInfo.Path,
                                        hostAppEntry.Runtime.ExePath,
                                        userDataFolder,
                                        hostAppEntry.InterestingLoadedDllPaths,
                                        childPid);
                                    runtimeEntry.Children.Add(new HostAppEntry(
                                        userDataPathAndProcessType.Item2,
                                        runtimeProcess.MainModule.FileName,
                                        runtimeProcess.Id,
                                        runtimeEntry.PID,
                                        null,
                                        runtimeProcess.MainModule.FileName,
                                        userDataFolder,
                                        null,
                                        0));
                                    runtimeEntry.Children.AddRange(hostAppEntry.Children);
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

    public static Tuple<string, string> GetUserDataPathAndProcessTypeFromProcessViaCommandLine(Process process)
    {
        var commandLine = new CommandLineUtil.CommandLine(process.GetCommandLine());
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

        if (processType == null)
        {
            processType = "browser";
        }

        return new Tuple<string, string>(userDataPath, processType);
    }
}
