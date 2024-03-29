﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace wv2util
{
    public static class HwndUtil
    {
        public static int GetWindowProcessId(IntPtr hwnd)
        {
            int processId = 0;
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

        public static IntPtr GetChildWindow(IntPtr hwnd)
        {
            IntPtr result = IntPtr.Zero;
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

        public delegate bool HwndFilterCallback(IntPtr hwnd);
        public static IEnumerable<IntPtr> GetTopLevelHwnds(HwndFilterCallback filterCallback = null, bool includeHwndMessage = false)
        {
            IntPtr child = IntPtr.Zero;
            var results = GetChildWindows(IntPtr.Zero, filterCallback);

            if (includeHwndMessage)
            {
                // HWND_MESSAGE = -3
                results = results.Concat(GetChildWindows((IntPtr)(-3), filterCallback));
            }

            if (filterCallback != null)
            {
                results = results.Where(hwnd => filterCallback(hwnd));
            }

            return results;
        }

        public static Dictionary<int, List<IntPtr>> CreatePidToHwndsMapFromHwnds(IEnumerable<IntPtr> hwnds)
        {
            Dictionary<int, List<IntPtr>> pidToHwndMap = new Dictionary<int, List<IntPtr>>();
            // Turn the list of hwnds into a dictionary of pid to hwnd list.
            foreach (IntPtr hwnd in hwnds)
            {
                int hwndPid = GetWindowProcessId(hwnd);
                if (!pidToHwndMap.TryGetValue(hwndPid, out List<IntPtr> hwndList))
                {
                    hwndList = new List<IntPtr>();
                    pidToHwndMap.Add(hwndPid, hwndList);
                }
                hwndList.Add(hwnd);
            }

            return pidToHwndMap;
        }

        public static Dictionary<int, List<IntPtr>> GetPidToTopLevelHwndsMap(HwndFilterCallback filterCallback = null)
        {
            return CreatePidToHwndsMapFromHwnds(GetTopLevelHwnds(filterCallback));
        }

        public static IEnumerable<IntPtr> GetChildWindows(IntPtr parentHwnd, HwndFilterCallback filterCallback = null)
        {
            HashSet<IntPtr> hwnds = new HashSet<IntPtr>();

            try
            {
                IntPtr child = IntPtr.Zero;
                while ((child = PInvoke.User32.FindWindowEx(parentHwnd, child, null, null)) != IntPtr.Zero)
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
                PInvoke.User32.EnumChildWindows(parentHwnd, Marshal.GetFunctionPointerForDelegate(enumChildrenCallback), IntPtr.Zero);
            }
            catch (Exception e)
            {
                Trace.WriteLine("EnumChildWindows related exception " + e);
            }

            IEnumerable<IntPtr> hwndsResult = hwnds;
            if (filterCallback != null)
            {
                hwndsResult = hwndsResult.Where(hwnd => filterCallback(hwnd));
            }

            return hwndsResult;
        }

        public static HashSet<IntPtr> GetDescendantWindows(
            IntPtr parentHwnd, // Parent HWND for which to find all children and children of children
            HwndFilterCallback filterExploreCallback = null, // Delegate return true to explore its children
            HwndFilterCallback filterResultCallback = null) // Delegate return true to include in returned list
        {
            List<IntPtr> borderHwnds = new List<IntPtr>();
            HashSet<IntPtr> resultHwnds = new HashSet<IntPtr>();

            borderHwnds.Add(parentHwnd);
            do
            {
                // .NET doesn't want us to modify the collection while we enumerate it
                // so we have an expandBorderHwnds which contains the next list of border
                // HWNDs to examine after we're done with the current list.
                List<IntPtr> expandBorderHwnds = new List<IntPtr>();
                foreach (IntPtr borderHwnd in borderHwnds)
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

        public static string GetClassName(IntPtr hwnd)
        {
            const int bufferSize = 256;
            string className = "";
            try
            {
                unsafe
                {
                    char* buffer = stackalloc char[bufferSize];
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
}
