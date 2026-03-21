using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.DesignTools.DesignerHost;
using Microsoft.VisualStudio.DesignTools.DesignerHost.HostServices;
using Microsoft.VisualStudio.DesignTools.Utility;
using Microsoft.VisualStudio.DesignTools.Utility.Extensions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.Versioning;

namespace ModernUwpDesigner.Common
{
    internal static class HierarchyHelpers
    {
        internal static FrameworkName GetTargetFramework(this IVsHierarchy hierarchy)
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

        internal static IVsHierarchy GetHierarchy(this Project project)
        {
            string uniqueName = null;
            IVsSolution serviceSafe = ServiceProvider.GlobalProvider.GetServiceSafe<SVsSolution, IVsSolution>();
            ErrorHandler.CallWithCOMConvention(delegate
            {
                uniqueName = project.UniqueName;
            });

            if (serviceSafe != null &&
                !string.IsNullOrEmpty(uniqueName) &&
                serviceSafe.GetProjectOfUniqueName(uniqueName, out var ppHierarchy) >= 0)
            {
                return ppHierarchy.GetEffectiveHierarchy();
            }

            return null;
        }

        internal static bool IsModernUwpProject(this IVsHierarchy hierarchy)
        {
            return VSUtilities.GetProjectFilePropertyValue(hierarchy, "DefaultXamlRuntime", _PersistStorageType.PST_PROJECT_FILE)?
                .Equals(XamlRuntimeNames.UAP, StringComparison.Ordinal) is true &&
                hierarchy.GetTargetFramework() is { } framework &&
                framework.Identifier.Equals(FrameworkNames.NetCoreApp, StringComparison.Ordinal) &&
                framework.Version.Major >= Constants.MinimumSupportedRuntimeVersion;
        }
    }
}
