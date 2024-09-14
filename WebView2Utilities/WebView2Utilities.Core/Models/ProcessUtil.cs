using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace WebView2Utilities.Core.Models;

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

    // Returns a tuple where the items are:
    // 1. EmbeddedBrowserWebView.dll full path.
    // 2. Microsoft.Web.WebView2.Core.dll or webview2loader.dll full path.
    // 3. All interesting DLLs found including the above two.
    public static Tuple<string, string, string[]> GetInterestingDllsUsedByPid(int pid)
    {
        var interestingDllPaths = GetInterestingDllsUsedByPidViaCreateToolhelp32Snapshot(pid);

        string clientDllPath = null;
        string sdkDllPath = null;
        foreach (var interestingDllPath in interestingDllPaths)
        {
            var interestingDllFileName = Path.GetFileName(interestingDllPath).ToLower();
            if (interestingDllFileName == "embeddedbrowserwebview.dll")
            {
                clientDllPath = interestingDllPath;
            }
            else if (interestingDllFileName == "webview2loader.dll" && sdkDllPath == null
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
        var interestingDllPaths = new List<string>();

        unsafe
        {
            var modEntry = new PInvoke.Kernel32.MODULEENTRY32() { dwSize = Marshal.SizeOf(typeof(PInvoke.Kernel32.MODULEENTRY32)) };
            var moduleSnapshot = PInvoke.Kernel32.CreateToolhelp32Snapshot(
                PInvoke.Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPMODULE | PInvoke.Kernel32.CreateToolhelp32SnapshotFlags.TH32CS_SNAPMODULE32, pid);

            if (PInvoke.Kernel32.Module32First(moduleSnapshot, ref modEntry))
            {
                do
                {
                    var moduleFileName = new string(modEntry.szModule).ToLower();
                    var modulePath = new string(modEntry.szExePath).ToLower();
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
        using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
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

    // PInvoke for GetPackageFullName. Why doesn't PInvoke.Kernel32 have this?
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetPackageFullName(nint hProcess, ref uint packageFullNameLength, StringBuilder packageFullName);

    // Wrapper for the Win32 PInvoke GetPackageFullName to make it more C# friendly
    public static string GetPackageFullName(int processId)
    {
        string packageFullName = null;

        try
        {
            var processSafeHandle = PInvoke.Kernel32.OpenProcess(
                (PInvoke.Kernel32.ACCESS_MASK)0x1000, // PROCESS_QUERY_LIMITED_INFORMATION
                false,
                processId);
            if (!processSafeHandle.IsInvalid)
            {
                uint packageFullNameLength = 0;
                int result = GetPackageFullName(processSafeHandle.DangerousGetHandle(), ref packageFullNameLength, null);
                if (result == (int)PInvoke.Win32ErrorCode.ERROR_INSUFFICIENT_BUFFER)
                {
                    var packageFullNameBuilder = new StringBuilder((int)packageFullNameLength);
                    result = GetPackageFullName(processSafeHandle.DangerousGetHandle(), ref packageFullNameLength, packageFullNameBuilder);
                    if (result == (int)PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                    {
                        packageFullName = packageFullNameBuilder.ToString();
                    }
                }
            }
        }
        catch (Exception)
        {
            // Process may be gone when go to learn package full name and that's fine.
        }

        return packageFullName;
    }

    public static string GetIntegrityLevelOfProcess(int pid)
    {
        // Determine if this is admin
        var processSafeHandle = PInvoke.Kernel32.OpenProcess(
            PInvoke.Kernel32.ProcessAccess.PROCESS_QUERY_INFORMATION,
            false,
            pid);
        if (!PInvoke.AdvApi32.OpenProcessToken(
            processSafeHandle.DangerousGetHandle(),
            (PInvoke.Kernel32.ACCESS_MASK)0x8, // TOKEN_QUERY
            out var tokenHandle))
        {
            PInvoke.Win32ErrorCode errorCode = PInvoke.Kernel32.GetLastError();
            if (errorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
            {
                throw new PInvoke.Win32Exception(errorCode, "Error calling OpenProcessToken");
            }
        }

        var elevationType =
            new PInvoke.AdvApi32.TOKEN_ELEVATION_TYPE[] { PInvoke.AdvApi32.TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault };
        unsafe
        {
            fixed (void* elevationTypeAsVoidPointer = elevationType)
                if (!PInvoke.AdvApi32.GetTokenInformation(
                    tokenHandle,
                    PInvoke.AdvApi32.TOKEN_INFORMATION_CLASS.TokenElevationType,
                    elevationTypeAsVoidPointer,
                    sizeof(PInvoke.AdvApi32.TOKEN_ELEVATION_TYPE),
                    out int returnLength))
                {
                    PInvoke.Win32ErrorCode errorCode = PInvoke.Kernel32.GetLastError();
                    if (errorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                    {
                        throw new PInvoke.Win32Exception(errorCode, "Error calling GetTokenInformation");
                    }
                }
            if (elevationType[0] == PInvoke.AdvApi32.TOKEN_ELEVATION_TYPE.TokenElevationTypeFull)
            {
                return "High";
            }
        }

        var isAppContainer = new uint[] { 0 };
        unsafe
        {
            fixed (void* isAppContainerAsVoidPointer = isAppContainer)
                if (!PInvoke.AdvApi32.GetTokenInformation(
                        tokenHandle,
                        PInvoke.AdvApi32.TOKEN_INFORMATION_CLASS.TokenIsAppContainer,
                        isAppContainerAsVoidPointer,
                        sizeof(uint),
                        out int returnLength))
                {
                    PInvoke.Win32ErrorCode errorCode = PInvoke.Kernel32.GetLastError();
                    if (errorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                    {
                        throw new PInvoke.Win32Exception(errorCode, "Error calling GetTokenInformation");
                    }
                }
            if (isAppContainer[0] == 1)
            {
                return "AppContainer";
            }
        }

        return "Normal";
    }

    public static Process GetParentProcess(this Process process)
    {
        return ParentProcessUtil.GetParentProcess(process.Id);
    }

    // https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
    public struct ParentProcessUtil
    {
        // These members must match PROCESS_BASIC_INFORMATION
        internal nint Reserved1;
        internal nint PebBaseAddress;
        internal nint Reserved2_0;
        internal nint Reserved2_1;
        internal nint UniqueProcessId;
        internal nint InheritedFromUniqueProcessId;

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, ref ParentProcessUtil processInformation, int processInformationLength, out int returnLength);

        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(int id)
        {
            var process = Process.GetProcessById(id);
            return GetParentProcess(process.Handle);
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(nint handle)
        {
            var pbi = new ParentProcessUtil();
            int returnLength;
            var status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
            if (status != 0)
                throw new PInvoke.Win32Exception(status);

            try
            {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (ArgumentException)
            {
                // not found
                return null;
            }
        }
    }
}
