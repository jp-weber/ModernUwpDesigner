using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.DesignTools.DesignerContract;
using Microsoft.VisualStudio.DesignTools.DesignerHost;
using Microsoft.VisualStudio.DesignTools.DesignerHost.HostServices;
using Microsoft.VisualStudio.DesignTools.DesignerHost.Platform;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Documents.Project;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Views;
using Microsoft.VisualStudio.DesignTools.Utility;
using Microsoft.VisualStudio.DesignTools.Utility.Extensions;
using Microsoft.VisualStudio.DesignTools.UwpDesignerHost;
using Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner.Documents;
using Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner.Views;
using Microsoft.VisualStudio.DesignTools.XamlDesignerHost.DesignSurface;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ModernUwpDesigner
{
    [ProvideCommand("ModernUwpDesigner.LaunchBlend", 0x0100)]
    [ProvideAppCommandLine(BlendShim.Constants.VSLaunchCmd, typeof(ModernUwpDesignerPackage), Arguments = "0", DemandLoad = 1, HelpString = "#101")]
    [ProvideAppCommandLine(BlendShim.Constants.VSConfirmLaunchCmd, typeof(ModernUwpDesignerPackage), Arguments = "0", DemandLoad = 1, HelpString = "#102")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasMultipleProjects_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionHasSingleProject_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(ModernUwpDesignerPackage.BlendUIContextGuidString, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(ModernUwpDesignerPackage.BlendSolutionExistsContextGuidString, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = false, AllowsBackgroundLoading = true)]
    [Guid(ModernUwpDesignerPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class ModernUwpDesignerPackage : AsyncPackage
    {
        /// <summary>
        /// ModernUwpDesignerPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "c87560ed-02e7-4c00-8dc6-b1ac79534ce4";

        private const string BlendUIContextGuidString = "{98163396-3C1D-459A-A5E2-90F7E31A1433}";
        private const string BlendSolutionExistsContextGuidString = "{7D30C25D-A30B-451A-A4D5-9229204E6AA5}";

        private const int MinimumSupportedRuntimeVersion = 10;
        private const int MinimumSupportedSdkBuild = 26100;

        private static Hook _updateRuntimeArchitectureHook;
        private static Hook _incompatibleDesignerRuntimeArchitectureHook;
        private static Hook _getTargetPlatformFromProjectStorageHook;

        private static readonly PlatformSpecification ModernUwpSpecificationUap = new(PlatformNames.UAP, "10.0-..", ["Managed", "Native"], FrameworkNames.NetCoreApp, "10.0-..", null, XamlRuntimeNames.UAP, null);
        private static readonly PlatformSpecification ModernUwpSpecificationWindows = new(PlatformNames.Windows, "10.0-..", ["Managed", "Native"], FrameworkNames.NetCoreApp, "10.0-..", null, XamlRuntimeNames.UAP, null);

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (GetService(typeof(SVsAppCommandLine)) is IVsAppCommandLine cli &&
                cli.GetOption(BlendShim.Constants.VSLaunchCmd, out int isPresent, out _) is 0 &&
                isPresent is 1)
            {
                string shimArgs = null;
                if (cli.GetOption(BlendShim.Constants.VSConfirmLaunchCmd, out isPresent, out _) is 0 && isPresent is 1)
                {
                    shimArgs = BlendShim.Constants.ShimConfirmLaunchCmd;
                }

                BlendShim.LaunchInjectedBlend(shimArgs: shimArgs);
                
                if (GetService(typeof(SDTE)) is DTE2 dte)
                {
                    dte.Quit();
                    return;
                }
            }

            InitializeDesignerPackage();
            await LaunchBlendCommand.InitializeAsync(this);
        }

        internal static unsafe void InitializeDesignerPackage()
        {
            var platformProps = new Dictionary<string, string>
            {
                { "PlatformCreatorAssembly", "Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner" },
                { "PlatformCreatorType", "Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner.UwpPlatformCreator" },
                { "HostPlatformAssembly", typeof(UwpHostPlatform).Assembly.Location },
                { "HostPlatformType", "Microsoft.VisualStudio.DesignTools.UwpDesignerHost.UwpHostPlatform" },
                { "IsolationUnification", "true" },
                { "ReferenceAssemblyMode", "None" },
                { "DefaultTargetFramework", $"{FrameworkNames.NetCoreApp}, Version={MinimumSupportedRuntimeVersion}.0" },
                { "UserControlTemplateName", "MyUserControl.xaml" },
                { "PlatformSurfaceIsolatedGuid", "{D617FC9B-7AE9-4219-B022-359A3D13B875}" },
                { "SupportsToolboxAutoPopulation", "true" },
                { "SupportsExtensionSdks", "true" },
                { "LegacyExtensionSdkPlatformsAndRequiredVCLibs", "Windows, 10.0;Microsoft.VCLibs.120, Version=14.0" },
                { "ToolboxPage", "{8A63BDE2-AEB9-4AF9-A00D-DBC9BD7D509C}" },
                { "DesignerTechnology", "Microsoft:Windows.UI.Xaml" },
                { "ClipboardFormat", "CF_WINDOWSUIXAML_TOOL" },
                { "AppPackageType", "WindowsXaml" }
            };

            var RegisterPlatformConfiguration = (delegate*<PlatformSpecification, IDictionary<string, string>, void>)typeof(PlatformConfigurationService).GetMethod("RegisterPlatformConfiguration", BindingFlags.NonPublic | BindingFlags.Static).MethodHandle.GetFunctionPointer();
            RegisterPlatformConfiguration(ModernUwpSpecificationUap, platformProps);
            RegisterPlatformConfiguration(ModernUwpSpecificationWindows, platformProps);

            if (RuntimeInformation.ProcessArchitecture is Architecture.Arm64 &&
                _updateRuntimeArchitectureHook is null &&
                _incompatibleDesignerRuntimeArchitectureHook is null)
            {
                var property = typeof(UwpSceneView).GetProperty("IncompatibleDesignerRuntimeArchitecture", BindingFlags.NonPublic | BindingFlags.Instance);
                if (property is not null)
                {
                    _incompatibleDesignerRuntimeArchitectureHook = new Hook(property.GetMethod, IncompatibleDesignerRuntimeArchitectureHook);
                }

                var method = typeof(HostPlatformBase).GetMethod("UpdateRuntimeArchitecture", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method is not null)
                {
                    _updateRuntimeArchitectureHook = new Hook(method, UpdateRuntimeArchitectureHook);
                }
            }

            if (_getTargetPlatformFromProjectStorageHook is null)
            {
                var method = typeof(VSUtilities).GetMethod("GetTargetPlatformFromProjectStorage", BindingFlags.NonPublic | BindingFlags.Static);
                if (method is not null)
                {
                    _getTargetPlatformFromProjectStorageHook = new Hook(method, GetTargetPlatformFromProjectStorageHook);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            _incompatibleDesignerRuntimeArchitectureHook?.Dispose();
            _incompatibleDesignerRuntimeArchitectureHook = null;

            _updateRuntimeArchitectureHook?.Dispose();
            _updateRuntimeArchitectureHook = null;

            _getTargetPlatformFromProjectStorageHook?.Dispose();
            _getTargetPlatformFromProjectStorageHook = null;

            base.Dispose(disposing);
        }

        private delegate bool IncompatibleDesignerRuntimeArchitecture(UwpSceneView instance);

        private static bool IncompatibleDesignerRuntimeArchitectureHook(IncompatibleDesignerRuntimeArchitecture original, UwpSceneView instance)
        {
            if (((SceneView)(object)instance).ProjectContext is ProjectContextBase context)
            {
                var hostProject = context.HostProject;
                var platformIdentifier = hostProject.PlatformIdentifier;

                if (hostProject.BuildPlatform.Equals("ARM64", StringComparison.OrdinalIgnoreCase) &&
                    platformIdentifier.TargetFrameworkIdentifier.Equals(FrameworkNames.NetCoreApp, StringComparison.Ordinal) &&
                    platformIdentifier.TargetFrameworkVersion.Major >= MinimumSupportedRuntimeVersion)
                {
                    return false;
                }
            }

            return original(instance);
        }

        private delegate void UpdateRuntimeArchitecture(HostPlatformBase instance, SurfaceProcessInfo surfaceProcessInfo, IHostProject hostProject);

        private static void UpdateRuntimeArchitectureHook(UpdateRuntimeArchitecture original, HostPlatformBase instance, SurfaceProcessInfo surfaceProcessInfo, IHostProject hostProject)
        {
            if (string.Equals(surfaceProcessInfo.Architecture, "ARM64", StringComparison.OrdinalIgnoreCase))
            {
                string architecture = (surfaceProcessInfo.RuntimeArchitecture = "ARM64");
                surfaceProcessInfo.Architecture = architecture;
            }
            else
            {
                original(instance, surfaceProcessInfo, hostProject);
            }
        }

        private delegate PlatformName GetTargetPlatformFromProjectStorage(IVsBuildPropertyStorage projectStorage);

        private static PlatformName GetTargetPlatformFromProjectStorageHook(GetTargetPlatformFromProjectStorage original, IVsBuildPropertyStorage projectStorage)
        {
            var og = original(projectStorage);
            if (og is not null &&
                og.Version.Build >= MinimumSupportedSdkBuild &&
                og.Identifier.Equals(PlatformNames.Windows, StringComparison.Ordinal) &&
                VSUtilities.GetProjectFilePropertyValue((IVsHierarchy)projectStorage, "DefaultXamlRuntime", _PersistStorageType.PST_PROJECT_FILE)
                .Equals(XamlRuntimeNames.UAP, StringComparison.Ordinal) &&
                GetTargetFramework((IVsHierarchy)projectStorage) is { } framework &&
                framework.Identifier.Equals(FrameworkNames.NetCoreApp, StringComparison.Ordinal) &&
                framework.Version.Major >= MinimumSupportedRuntimeVersion)
            {
                og = new(PlatformNames.UAP, og.Version, og.MinVersion);
            }

            return og;
        }

        private static FrameworkName GetTargetFramework(IVsHierarchy hierarchy)
        {
            hierarchy = hierarchy.GetEffectiveHierarchy();

            if (hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID4.VSHPROPID_TargetFrameworkMoniker, out object obj)
                != 0)
            {
                return null;
            }

            string text = obj as string;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return new(text);
        }
    }
}
