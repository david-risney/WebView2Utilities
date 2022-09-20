using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    internal class ProcessUtil
    {
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
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

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
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        /// <summary>
        /// Retrieves information about the next process recorded in a system snapshot.
        /// </summary>
        /// <param name="hSnapshot">A handle to the snapshot.</param>
        /// <param name="lppe">A pointer to a PROCESSENTRY32 structure.</param>
        /// <returns>
        /// Returns TRUE if the next entry of the process list has been copied to the buffer.
        /// Returns FALSE otherwise.</returns>
        [DllImport("kernel32.dll")]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("kernel32.dll")]
        public static extern bool Module32FirstW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        public static extern bool Module32NextW(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

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
        public struct PROCESSENTRY32
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
