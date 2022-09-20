using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace wv2util
{
    public static class HwndUtil
    {
        public delegate bool EnumWindowsCallback(IntPtr hwnd, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr extraData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsCallback callback, IntPtr extraData);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        public enum GetWindowType : uint
        {
            GW_HWNDFIRST = 0, /// The retrieved handle identifies the window of the same type that is highest in the Z order.
            GW_HWNDLAST = 1, /// The retrieved handle identifies the window of the same type that is lowest in the Z order.
            GW_HWNDNEXT = 2, /// The retrieved handle identifies the window below the specified window in the Z order.
            GW_HWNDPREV = 3, /// The retrieved handle identifies the window above the specified window in the Z order.
            GW_OWNER = 4, /// The retrieved handle identifies the specified window's owner window, if any.
                          /// <summary>
                          /// The retrieved handle identifies the child window at the top of the Z order,
                          /// if the specified window is a parent window; otherwise, the retrieved handle is NULL.
                          /// The function examines only child windows of the specified window. It does not examine descendant windows.
                          /// </summary>
            GW_CHILD = 5,
            /// <summary>
            /// The retrieved handle identifies the enabled popup window owned by the specified window (the
            /// search uses the first such window found using GW_HWNDNEXT); otherwise, if there are no enabled
            /// popup windows, the retrieved handle is that of the specified window.
            /// </summary>
            GW_ENABLEDPOPUP = 6
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GetWindowType uCmd);
        public static IntPtr GetChildWindow(IntPtr hwnd) { return GetWindow(hwnd, GetWindowType.GW_CHILD); }

        public static int GetWindowProcessId(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out int processId);
            return processId;
        }

        public delegate bool HwndFilterCallback(IntPtr hwnd);
        public static List<IntPtr> GetTopLevelHwnds(HwndFilterCallback filterCallback = null)
        {
            List<IntPtr> hwnds = new List<IntPtr>();

            EnumWindowsCallback enumCallback = (hwnd, lParam) =>
            {
                if (filterCallback == null || filterCallback(hwnd))
                {
                    hwnds.Add(hwnd);
                }
                // Always return true to keep enumerating.
                return true;
            };
            EnumWindows(enumCallback, IntPtr.Zero);

            return hwnds;
        }

        public static Dictionary<int, List<IntPtr>> CreatePidToHwndsMapFromHwnds(List<IntPtr> hwnds)
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

        public static List<IntPtr> GetChildWindows(IntPtr parentHwnd, HwndFilterCallback filterCallback = null)
        {
            List<IntPtr> hwnds = new List<IntPtr>();

            EnumWindowsCallback enumCallback = (hwnd, lParam) =>
            {
                if (filterCallback == null || filterCallback(hwnd))
                {
                    hwnds.Add(hwnd);
                }
                // Always return true to keep enumerating.
                return true;
            };
            EnumChildWindows(parentHwnd, enumCallback, IntPtr.Zero);

            return hwnds;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

        public static string GetClassName(IntPtr hwnd)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hwnd, className, className.Capacity);
            return className.ToString();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetPropW(IntPtr hwnd, string propertyName);
    }
}
