using System;
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
            PInvoke.User32.GetWindowThreadProcessId(hwnd, out int processId);
            return processId;
        }

        public static IntPtr GetChildWindow(IntPtr hwnd)
        {
            return PInvoke.User32.GetWindow(hwnd, PInvoke.User32.GetWindowCommands.GW_CHILD);
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

            IntPtr child = IntPtr.Zero;
            while ((child = PInvoke.User32.FindWindowEx(parentHwnd, child, null, null)) != IntPtr.Zero)
            {
                hwnds.Add(child);
            }

            /* // GetWindow GW_CHILD/GW_HWNDNEXT seems to find the same HWNDs as FindWindowEx
            child = PInvoke.User32.GetWindow(parentHwnd, PInvoke.User32.GetWindowCommands.GW_CHILD);
            if (child != IntPtr.Zero)
            {
                do
                {
                    hwnds.Add(child);
                } while ((child = PInvoke.User32.GetWindow(child, PInvoke.User32.GetWindowCommands.GW_HWNDNEXT)) != IntPtr.Zero);
            }
            */
            // EnumChildWindows finds HWNDs that FindWindowEx does not and vice versa.
            // So we run both and collect HWNDs from both.
            HwndFilterCallback enumChildrenCallback = childHwnd =>
            {
                hwnds.Add(childHwnd);
                // Always return true to indicate to keep enumerating.
                return true;
            };
            PInvoke.User32.EnumChildWindows(parentHwnd, Marshal.GetFunctionPointerForDelegate(enumChildrenCallback), IntPtr.Zero);

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
            string className = null;
            unsafe
            {
                char* buffer = stackalloc char[bufferSize];
                PInvoke.User32.GetClassName(hwnd, buffer, bufferSize);
                className = new string(buffer);
            }
            return className;
        }
    }
}
