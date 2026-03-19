using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace ModernUwpDesigner
{
    internal static unsafe partial class NativeMethods
    {
        internal const uint MEM_COMMIT = 0x1000;
        internal const uint MEM_RESERVE = 0x2000;
        internal const uint PAGE_READWRITE = 0x04;
        internal const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
        internal const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        internal const uint WAIT_TIMEOUT = 0x00000102;

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern bool CreateProcessW(string lpApplicationName, string lpCommandLine, void* lpProcessAttributes, void* lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, void* lpEnvironment, string lpCurrentDirectory, STARTUPINFOW* si, out PROCESS_INFORMATION pi);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern nint CreateRemoteThread(nint hProcess, void* lpThreadAttributes, uint dwStackSize, void* lpStartAddress, void* lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(nint hObject);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetExitCodeProcess(nint hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void* VirtualAllocEx(nint hProcess, void* lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(nint hProcess, void* lpBaseAddress, void* lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern nint GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern nint GetProcAddress(nint hModule, string lpProcName);

        [DllImport("ole32.dll", PreserveSig = false)]
        internal static extern IBindCtx CreateBindCtx(int reserved);

        [DllImport("ole32.dll", PreserveSig = false)]
        internal static extern IRunningObjectTable GetRunningObjectTable(int reserved);

        internal static string GetEnvironmentVariablesBlock(Dictionary<string, string> sd)
        {
            // https://learn.microsoft.com/windows/win32/procthread/changing-environment-variables
            // "All strings in the environment block must be sorted alphabetically by name. The sort is
            //  case-insensitive, Unicode order, without regard to locale. Because the equal sign is a
            //  separator, it must not be used in the name of an environment variable."

            var keys = new string[sd.Count];
            sd.Keys.CopyTo(keys, 0);
            Array.Sort(keys, StringComparer.OrdinalIgnoreCase);

            // Join the null-terminated "key=val\0" strings
            var result = new StringBuilder(8 * keys.Length);
            foreach (string key in keys)
            {
                string value = sd[key];

                // Ignore null values for consistency with Environment.SetEnvironmentVariable
                if (value != null)
                {
                    result.Append(key).Append('=').Append(value).Append('\0');
                }
            }

            return result.ToString();
        }

        extension(SafeProcessHandle handle)
        {
            internal bool HasExited
            {
                get
                {
                    uint result = WaitForSingleObject(handle.DangerousGetHandle(), 0);
                    return result != WAIT_TIMEOUT;
                }
            }

            internal int ExitCode
            {
                get
                {
                    if (!GetExitCodeProcess(handle.DangerousGetHandle(), out uint exitCode))
                    {
                        return -1;
                    }

                    return (int)exitCode;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct STARTUPINFOW
    {
        public uint cb;
        public char* lpReserved;
        public char* lpDesktop;
        public char* lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public byte* lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }
}
