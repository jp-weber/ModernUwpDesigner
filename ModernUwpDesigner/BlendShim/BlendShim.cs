using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32.SafeHandles;
using ModernUwpDesigner.Common;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using static ModernUwpDesigner.NativeMethods;

namespace ModernUwpDesigner
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class BlendShim
    {
        private static bool isAttached = false;
        private static readonly Lazy<bool> isBlend = new(() =>
        {
            object sku = null;
            var appId = Package.GetGlobalService(typeof(SVsAppId)) as IVsAppId;
            appId?.GetProperty((int)__VSAPROPID.VSAPROPID_SubSKUEdition, out sku);
            return sku is not null && ((int)sku & (int)__VSASubSKUEdition6.VSASubSKUEdition_Blend) is not 0;
        });

        internal static bool IsBlend => isBlend.Value;

        public static int Attach(string args)
        {
            if (IsBlend && !isAttached)
            {
                isAttached = true;
                ModernUwpDesignerPackage.InitializeDesignerPackage();

                var arguments = args?.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                if (arguments?.Length is > 0)
                {
                    if (arguments.Contains(Constants.ShimConfirmLaunchCmd, StringComparer.Ordinal))
                    {
                        UIHelpers.ShowMessageBox(
                            ServiceProvider.GlobalProvider,
                            $"You can now start using Blend.",
                            "Modern UWP Designer has been initialized",
                            OLEMSGICON.OLEMSGICON_INFO);
                    }
                }
            }

            _ = args;
            return 0;
        }

        internal static DTE2 LaunchInjectedBlend(string cmdArgs = null, string shimArgs = null)
        {
            var currentAssemblyLocation = typeof(BlendShim).Assembly.Location;
            var blendExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Blend.exe");
            var isExp = PackageUtilities.IsExperimentalVersionOfVsForVsipDevelopment(out var rootSuffix);
            cmdArgs = ($"\"{blendExe}\"" + 
                       $" {(isExp && !string.IsNullOrWhiteSpace(rootSuffix) ? $"/RootSuffix {rootSuffix}" : string.Empty)}" +
                       $" {cmdArgs}").TrimEnd();

            uint pid = 0;
            nint hProcess = 0;
            Exception exception = null;
            SafeProcessHandle processHandle = null;

            unsafe
            {
                var block = new Dictionary<string, string>()
                {
                    { Constants.ShimDllEnvVar, currentAssemblyLocation },
                    { Constants.ShimArgsEnvVar, shimArgs },
                };

                block.AddRange(Environment.GetEnvironmentVariables());
                fixed (char* pBlock = GetEnvironmentVariablesBlock(block))
                {
                    Unsafe.SkipInit(out PROCESS_INFORMATION pi);
                    STARTUPINFOW si = new() { cb = (uint)sizeof(STARTUPINFOW) };
                    bool success = CreateProcessW(null,
                                                  cmdArgs,
                                                  null,
                                                  null,
                                                  false,
                                                  CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_PROCESS_GROUP,
                                                  pBlock,
                                                  null, 
                                                  &si,
                                                  out pi);

                    if (success)
                    {
                        pid = pi.dwProcessId;
                        hProcess = pi.hProcess;
                        processHandle = new(hProcess, true);
                        CloseHandle(pi.hThread);
                    }
                    else
                    {
                        exception = Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                    }
                }
            }

            if (processHandle is null || processHandle.HasExited is true)
            {
                UIHelpers.ShowMessageBox(
                    ServiceProvider.GlobalProvider,
                    $"Failed to launch Blend." +
                    $"{(processHandle is not null ? $"\r\nExit Code: {processHandle.ExitCode}" : string.Empty)}" +
                    $"{(exception is not null ? $"\r\nException: {exception.Message} (0x{exception.HResult:X8})" : string.Empty)}",
                    "Process Creation Failed",
                    OLEMSGICON.OLEMSGICON_CRITICAL);

                return null;
            }

            DTE2 dte = null;
            bool dteLoaded = false;
            var task = Task.Run(async () =>
            {
                while (!processHandle.HasExited)
                {
                    if ((dte = GetDTE(pid)) is not null)
                    {
                        dteLoaded = true;
                        break;
                    }

                    await Task.Delay(200);
                }
            });

            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1));
            var whenAnyTask = Task.WhenAny(task, timeoutTask);
            whenAnyTask.Wait();

            if (whenAnyTask.Result == timeoutTask)
            {
                UIHelpers.ShowMessageBox(
                    ServiceProvider.GlobalProvider,
                    $"Timed out while waiting for Blend to load.",
                    "Operation Timeout",
                    OLEMSGICON.OLEMSGICON_CRITICAL);

                return null;
            }
            else if (!dteLoaded)
            {
                UIHelpers.ShowMessageBox(
                    ServiceProvider.GlobalProvider,
                    $"Blend process exited before Modern UWP Designer could be injected.\r\nExit Code: {processHandle.ExitCode}",
                    "Process Exited Prematurely",
                    OLEMSGICON.OLEMSGICON_CRITICAL);

                return null;
            }

            var assemblyDir = Path.GetDirectoryName(typeof(BlendShim).Assembly.Location);
            var arch = RuntimeInformation.ProcessArchitecture is Architecture.X64 ? "x64" : "arm64";
            var blendShimDll = Path.Combine(assemblyDir, "BlendShim", arch, "BlendShim.dll") + '\0';
            var blendShimDllBytes = Encoding.Unicode.GetBytes(blendShimDll);

            unsafe
            {
                var mem = VirtualAllocEx(hProcess, null, (uint)blendShimDllBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (mem is null)
                {
                    UIHelpers.ShowMessageBox(
                        ServiceProvider.GlobalProvider,
                        $"Failed to allocate memory in Blend process.\r\nError Code: {Marshal.GetLastWin32Error()}",
                        "Memory Allocation Failed",
                        OLEMSGICON.OLEMSGICON_CRITICAL);

                    return null;
                }

                fixed (byte* pBlendShimDll = blendShimDllBytes)
                    WriteProcessMemory(hProcess, mem, pBlendShimDll, (uint)blendShimDllBytes.Length, out _);

                var pLoadLibraryW = GetProcAddress(GetModuleHandleW("kernel32.dll"), "LoadLibraryW");
                var thread = CreateRemoteThread(hProcess, null, 0, (void*)pLoadLibraryW, mem, 0, out _);
                if (thread is 0)
                {
                    UIHelpers.ShowMessageBox(
                        ServiceProvider.GlobalProvider,
                        $"Failed to create remote thread in Blend process.\r\nError Code: {Marshal.GetLastWin32Error()}",
                        "Remote Thread Creation Failed",
                        OLEMSGICON.OLEMSGICON_CRITICAL);

                    return null;
                }

                WaitForSingleObject(thread, uint.MaxValue);
                CloseHandle(thread);
                processHandle?.Dispose();
                return dte;
            }
        }

        private static IRunningObjectTable rot = IsBlend ? null : GetRunningObjectTable(0);
        private static IBindCtx ctx = IsBlend ? null : CreateBindCtx(0);
        private static IMoniker[] moniker = IsBlend ? null : new IMoniker[1];

        private static DTE2 GetDTE(uint pid)
        {
            rot.EnumRunning(out IEnumMoniker enumMoniker);
            enumMoniker.Reset();

            nint fetched = 0;
            while (enumMoniker.Next(1, moniker, fetched) is 0)
            {
                moniker[0].GetDisplayName(ctx, null, out string displayName);

                if (displayName.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal) &&
                    displayName.EndsWith($":{pid}", StringComparison.Ordinal))
                {
                    rot.GetObject(moniker[0], out object comObject);
                    return comObject as DTE2;
                }
            }

            return null;
        }
    }
}
