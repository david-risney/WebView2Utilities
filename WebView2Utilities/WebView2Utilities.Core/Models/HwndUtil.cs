using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace WebView2Utilities.Core.Models;

public static class HwndUtil
{
    public static int GetWindowProcessId(nint hwnd)
    {
        var processId = 0;
        try
        {
            PInvoke.User32.GetWindowThreadProcessId(hwnd, out processId);
        }
        catch (Exception e)
        {
            Trace.WriteLine("Exception related to GetWindowThreadProcessId " + e);
        }
        return processId;
    }

    public static nint GetChildWindow(nint hwnd)
    {
        var result = nint.Zero;
        try
        {
            result = PInvoke.User32.GetWindow(hwnd, PInvoke.User32.GetWindowCommands.GW_CHILD);
        }
        catch (Exception e)
        {
            Trace.WriteLine("Exception related to GetWindow " + e);
        }
        return result;
    }

    public delegate bool HwndFilterCallback(nint hwnd);
    public static IEnumerable<nint> GetTopLevelHwnds(HwndFilterCallback filterCallback = null, bool includeHwndMessage = false)
    {
        var child = nint.Zero;
        var results = GetChildWindows(nint.Zero, filterCallback);

        if (includeHwndMessage)
        {
            // HWND_MESSAGE = -3
            results = results.Concat(GetChildWindows(-3, filterCallback));
        }

        if (filterCallback != null)
        {
            results = results.Where(hwnd => filterCallback(hwnd));
        }

        return results;
    }

    public static Dictionary<int, List<nint>> CreatePidToHwndsMapFromHwnds(IEnumerable<nint> hwnds)
    {
        var pidToHwndMap = new Dictionary<int, List<nint>>();
        // Turn the list of hwnds into a dictionary of pid to hwnd list.
        foreach (var hwnd in hwnds)
        {
            var hwndPid = GetWindowProcessId(hwnd);
            if (!pidToHwndMap.TryGetValue(hwndPid, out var hwndList))
            {
                hwndList = new List<nint>();
                pidToHwndMap.Add(hwndPid, hwndList);
            }
            hwndList.Add(hwnd);
        }

        return pidToHwndMap;
    }

    public static Dictionary<int, List<nint>> GetPidToTopLevelHwndsMap(HwndFilterCallback filterCallback = null)
    {
        return CreatePidToHwndsMapFromHwnds(GetTopLevelHwnds(filterCallback));
    }

    public static IEnumerable<nint> GetChildWindows(nint parentHwnd, HwndFilterCallback filterCallback = null)
    {
        var hwnds = new HashSet<nint>();

        try
        {
            var child = nint.Zero;
            while ((child = PInvoke.User32.FindWindowEx(parentHwnd, child, null, null)) != nint.Zero)
            {
                hwnds.Add(child);
            }
        }
        catch (Exception e)
        {
            // Ignore exceptions trying to find windows.
            Trace.WriteLine("FindWindowEx related error: " + e);
        }

        // EnumChildWindows finds HWNDs that FindWindowEx does not and vice versa.
        // So we run both and collect HWNDs from both.
        try
        {
            HwndFilterCallback enumChildrenCallback = childHwnd =>
            {
                hwnds.Add(childHwnd);
                // Always return true to indicate to keep enumerating.
                return true;
            };
            PInvoke.User32.EnumChildWindows(parentHwnd, Marshal.GetFunctionPointerForDelegate(enumChildrenCallback), nint.Zero);
        }
        catch (Exception e)
        {
            Trace.WriteLine("EnumChildWindows related exception " + e);
        }

        IEnumerable<nint> hwndsResult = hwnds;
        if (filterCallback != null)
        {
            hwndsResult = hwndsResult.Where(hwnd => filterCallback(hwnd));
        }

        return hwndsResult;
    }

    public static HashSet<nint> GetDescendantWindows(
        nint parentHwnd, // Parent HWND for which to find all children and children of children
        HwndFilterCallback filterExploreCallback = null, // Delegate return true to explore its children
        HwndFilterCallback filterResultCallback = null) // Delegate return true to include in returned list
    {
        var borderHwnds = new List<nint>();
        var resultHwnds = new HashSet<nint>();

        borderHwnds.Add(parentHwnd);
        do
        {
            // .NET doesn't want us to modify the collection while we enumerate it
            // so we have an expandBorderHwnds which contains the next list of border
            // HWNDs to examine after we're done with the current list.
            var expandBorderHwnds = new List<nint>();
            foreach (var borderHwnd in borderHwnds)
            {
                if (filterExploreCallback == null || filterExploreCallback(borderHwnd))
                {
                    expandBorderHwnds.AddRange(GetChildWindows(borderHwnd));
                }
                if (filterResultCallback == null || filterResultCallback(borderHwnd))
                {
                    resultHwnds.Add(borderHwnd);
                }
            }
            borderHwnds = expandBorderHwnds;
        }
        while (borderHwnds.Count > 0);

        return resultHwnds;
    }

    public static string GetClassName(nint hwnd)
    {
        const int bufferSize = 256;
        var className = "";
        try
        {
            unsafe
            {
                var buffer = stackalloc char[bufferSize];
                PInvoke.User32.GetClassName(hwnd, buffer, bufferSize);
                className = new string(buffer);
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine("Exception related to GetClassName " + e);
        }
        return className;
    }
}
