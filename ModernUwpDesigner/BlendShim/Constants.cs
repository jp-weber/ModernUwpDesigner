using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernUwpDesigner
{
    public static partial class BlendShim
    {
        internal static class Constants
        {
            internal const string VSLaunchCmd = "LaunchBlendWithModernUwpDesigner";
            internal const string VSConfirmLaunchCmd = "MudConfirmLaunch";
            internal const string ShimConfirmLaunchCmd = "ConfirmLaunch";
            internal const string ShimDllEnvVar = "MODERN_UWP_DESIGNER_DLL";
            internal const string ShimArgsEnvVar = "MODERN_UWP_DESIGNER_ARGS";
        }
    }
}
