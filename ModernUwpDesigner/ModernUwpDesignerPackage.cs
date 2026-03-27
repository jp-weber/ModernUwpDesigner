using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.DesignTools.DesignerContract;
using Microsoft.VisualStudio.DesignTools.DesignerHost;
using Microsoft.VisualStudio.DesignTools.DesignerHost.HostServices;
using Microsoft.VisualStudio.DesignTools.DesignerHost.Platform;
using Microsoft.VisualStudio.DesignTools.Markup.Metadata;
using Microsoft.VisualStudio.DesignTools.RuntimeHost.Networking;
using Microsoft.VisualStudio.DesignTools.RuntimeHost.TapOM;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Documents.Project;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Documents.SurfaceIsolation;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Tools;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Tools.Path;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Utility;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.ViewModel;
using Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Views;
using Microsoft.VisualStudio.DesignTools.Utility;
using Microsoft.VisualStudio.DesignTools.UwpDesignerHost;
using Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner;
using Microsoft.VisualStudio.DesignTools.UwpSurfaceDesigner.Views;
using Microsoft.VisualStudio.DesignTools.Xaml.LanguageService.Metadata;
using Microsoft.VisualStudio.DesignTools.Xaml.LanguageService.Platform.UIXaml;
using Microsoft.VisualStudio.DesignTools.XamlSurfaceDesigner.Views.NodeObjectConverters;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ModernUwpDesigner.Common;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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
    [InstalledProductRegistration("#103", "#104", Vsix.Version)]
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

        private static Hook _updateRuntimeArchitectureHook;
        private static Hook _incompatibleDesignerRuntimeArchitectureHook;
        private static Hook _getTargetPlatformFromProjectStorageHook;
        private static Hook _registerPlatformCapabilitiesHook;
        //private static Hook _createPlatformConverterHook;
        private static Hook _updateHook;
        private static Hook _onBeginHook;

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
            await DesignInBlendCommand.InitializeAsync(this);
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
                { "DefaultTargetFramework", $"{FrameworkNames.NetCoreApp}, Version={Constants.MinimumSupportedRuntimeVersion}.0" },
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

            if (_registerPlatformCapabilitiesHook is null)
            {
                var method = typeof(UIXamlPlatformMetadata).GetMethod("RegisterPlatformCapabilities", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method is not null)
                {
                    _registerPlatformCapabilitiesHook = new Hook(method, RegisterPlatformCapabilitiesHook);
                }
            }

            /*if (_createPlatformConverterHook is null)
            {
                var method = typeof(UwpPlatform).GetMethod("CreatePlatformConverter", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method is not null)
                {
                    _createPlatformConverterHook = new Hook(method, CreatePlatformConverterHook);
                }
            }*/

            if (_updateHook is null)
            {
                var method = typeof(LiveObject).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method is not null)
                {
                    _updateHook = new Hook(method, UpdateHook);
                }
            }

            if (_onBeginHook is null)
            {
                var type = typeof(PenAction).Assembly.GetType("Microsoft.VisualStudio.DesignTools.SurfaceDesigner.Tools.Path.StartAction");
                var method = type?.GetMethod("OnBegin", BindingFlags.NonPublic | BindingFlags.Instance);

                if (method is not null)
                {
                    _onBeginHook = new Hook(method, OnBeginHook);
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

            _registerPlatformCapabilitiesHook?.Dispose();
            _registerPlatformCapabilitiesHook = null;

            //_createPlatformConverterHook?.Dispose();
            //_createPlatformConverterHook = null;

            _updateHook?.Dispose();
            _updateHook = null;

            _onBeginHook?.Dispose();
            _onBeginHook = null;

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
                    platformIdentifier.GetTargetFramework()?.IsModernUwpCapable() is true)
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
                og.Version.Build >= Constants.MinimumSupportedSdkBuild &&
                og.Identifier.Equals(PlatformNames.Windows, StringComparison.Ordinal) &&
                ((IVsHierarchy)projectStorage).IsModernUwpProject())
            {
                var minVersion = og.MinVersion;
                if (minVersion.Major is 10 &&
                    minVersion.Build < 16299)
                {
                    minVersion = new(10, 0, 16299, 0);
                }

                og = new(PlatformNames.UAP, og.Version, minVersion);
            }

            return og;
        }

        private delegate void RegisterPlatformCapabilities(UIXamlPlatformMetadata instance);

        private static void RegisterPlatformCapabilitiesHook(RegisterPlatformCapabilities original, UIXamlPlatformMetadata instance)
        {
            original(instance);

            if (instance.TargetFramework?.IsModernUwpCapable() is true)
            {
                instance.SetCapabilityValue(PlatformCapability.SupportsPathTools, true);
                //instance.SetCapabilityValue(PlatformCapability.OnePointPathHasSize, true);
                //instance.SetCapabilityValue(PlatformCapability.SupportsEyedropperTool, true);
                //instance.SetCapabilityValue(PlatformCapability.SupportsPaintBucketTool, true);
            }
        }

        /*private delegate IPlatformConverter CreatePlatformConverter(UwpPlatform instance);

        private static IPlatformConverter CreatePlatformConverterHook(CreatePlatformConverter original, UwpPlatform instance)
        {
            var converter = original(instance);
            if (converter is NodeObjectPlatformConverter nodeConverter)
            {
                nodeConverter.RegisterPrimitiveConverter(new TypeId("Stretch", XamlTypeGroup.Media), (str) =>
                {
                    if (string.Equals(str, "None", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(str, "0", StringComparison.Ordinal))
                    {
                        return System.Windows.Media.Stretch.None;
                    }
                    else if (string.Equals(str, "Fill", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(str, "1", StringComparison.Ordinal))
                    {
                        return System.Windows.Media.Stretch.Fill;
                    }
                    else if (string.Equals(str, "Uniform", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(str, "2", StringComparison.Ordinal))
                    {
                        return System.Windows.Media.Stretch.Uniform;
                    }
                    else if (string.Equals(str, "UniformToFill", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(str, "3", StringComparison.Ordinal))
                    {
                        return System.Windows.Media.Stretch.UniformToFill;
                    }

                    return null;
                });
            }

            return converter;
        }*/

        private delegate void Update(LiveObject instance, LiveObjectState state);

        private static FieldInfo _objectCacheField = typeof(LiveObject).GetField("objectCache", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void UpdateHook(Update original, LiveObject instance, LiveObjectState state)
        {
            if (_objectCacheField is not null &&
                (XamlTypes.Shape.IsAssignableFrom(instance.Type) ||
                 XamlTypes.MatrixTransform.IsAssignableFrom(instance.Type)) &&
                instance.Type.PlatformMetadata is UIXamlPlatformMetadata metadata &&
                metadata.TargetFramework?.IsModernUwpCapable() is true)
            {
                var cache = (LiveObjectCache)_objectCacheField.GetValue(instance);
                if (instance.ProtocolHandler.TrySendMessage<PropertyValuesHolder>(
                    cache.SurfaceProcessContext,
                    XamlMessageType.PropertyChainRequest,
                    new PropertiesRequestInfo()
                    {
                        Object = instance.Handle,
                        PropertyRequestLevel = PropertyRequestLevel.All,
                    }, out var reply))
                {
                    var originalProps = state.Properties;
                    foreach (var prop in reply.Properties)
                    {
                        if (prop.PropertyName.Equals("GeometryTransform") ||
                            prop.PropertyName.Equals("Matrix"))
                        {
                            var isHandle = prop.MetadataBits.HasFlag(MetadataBit.IsValueHandle);

                            var liveProp = new LiveObjectPropertyValue()
                            {
                                Property = $"{prop.PropertyName}:{prop.DeclaringType}",
                                CurrentValue = new LiveValue()
                                {
                                    Type = prop.ValueType,
                                    Handle = isHandle ? long.Parse(prop.Value) : prop.Handle,
                                    SourceInfo = prop.SourceInfo,
                                    Value = isHandle ? null : prop.Value,
                                },
                                ValueSource = BaseValueSource.Local
                            };

                            liveProp.BaseValue = liveProp.CurrentValue;
                            originalProps.Add(liveProp);
                            break;
                        }
                    }
                }
            }

            original(instance, state);
        }

        private delegate void OnBegin(PenAction instance, PathEditContext pathEditContext, System.Windows.Input.MouseDevice mouseDevice);

        //private static unsafe delegate*<PenAction, void> _commitEditTransaction = (delegate*<PenAction, void>)typeof(PenAction).GetMethod("CommitEditTransaction", BindingFlags.NonPublic | BindingFlags.Instance, null, [], null)?.MethodHandle.GetFunctionPointer();

        private static unsafe void OnBeginHook(OnBegin original, PenAction instance, PathEditContext pathEditContext, System.Windows.Input.MouseDevice mouseDevice)
        {
            original(instance, pathEditContext, mouseDevice);

            if (instance.View is { } view &&
                view.ProjectMetadata is ManagedProjectMetadata managedProject &&
                managedProject.PlatformMetadata is UIXamlPlatformMetadata platformMetadata &&
                platformMetadata.TargetFramework?.IsModernUwpCapable() is true)
            {
                /*if (_commitEditTransaction is not null)
                {
                    _commitEditTransaction(instance);
                }*/

                var editor = instance.PathEditorTarget;
                var element = editor.EditingElement;

                element.SetValueAsWpf(PathElement.StretchProperty, System.Windows.Media.Stretch.None);
                //element.SetMarkupValueAsWpf(PathElement.StretchProperty, System.Windows.Media.Stretch.None);

                element.SetValueAsWpf(PathElement.StretchProperty, System.Windows.Media.Stretch.Fill);
                //element.SetMarkupValueAsWpf(PathElement.StretchProperty, System.Windows.Media.Stretch.Fill);

                /*if (view.DesignerContext?.ToolManager?.ActiveTool is PenTool tool)
                {
                    tool.RebuildAdornerSets();
                }*/

                view.AdornerLayer.InvalidateAdornersStructure(element);
                view.AdornerLayer.InvalidateAdornerVisuals(element);
            }
        }
    }
}
