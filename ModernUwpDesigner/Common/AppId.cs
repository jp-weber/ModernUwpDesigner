using System;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudio.Shell
{
    [ComImport]
    [Guid("1EAA526A-0898-11d3-B868-00C04F79F802")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface SVsAppId
    {

    }

    [ComImport]
    [Guid("1EAA526A-0898-11d3-B868-00C04F79F802")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVsAppId
    {
        [PreserveSig]
        int SetSite(IServiceProvider pSP);

        [PreserveSig]
        int GetProperty(int propid, [MarshalAs(UnmanagedType.Struct)] out object pvar);

        [PreserveSig]
        int SetProperty(int propid, [MarshalAs(UnmanagedType.Struct)] object var);

        [PreserveSig]
        int GetGuidProperty(int propid, out Guid guid);

        [PreserveSig]
        int SetGuidProperty(int propid, ref Guid rguid);

        [PreserveSig]
        int Initialize();
    }

    internal enum __VSAPROPID
    {
        VSAPROPID_NIL = -1,
        VSAPROPID_LAST = -8500,
        VSAPROPID_GuidAppIDPackage = -8501,
        VSAPROPID_AppName = -8502,
        VSAPROPID_CmdLineOptDialog = -8503,
        VSAPROPID_HideSolutionConcept = -8504,
        VSAPROPID_ShowStartupDialogs = -8505,
        VSAPROPID_ShowIDE = -8506,
        VSAPROPID_ShowHierarchyRootInTitle = -8507,
        VSAPROPID_SolutionFileExt = -8508,
        VSAPROPID_UserOptsFileExt = -8509,
        VSAPROPID_AltMSODLL = -8510,
        VSAPROPID_CreateProjShortcuts = -8511,
        VSAPROPID_AppIcon = -8512,
        VSAPROPID_AppSmallIcon = -8513,
        VSAPROPID_DefaultHomePage = -8514,
        VSAPROPID_DefaultSearchPage = -8515,
        VSAPROPID_WBExternalObject = -8516,
        VSAPROPID_AppShortName = -8517,
        VSAPROPID_ClsidAppIdServer = -8518,
        VSAPROPID_GuidGeneralOutput = -8519,
        VSAPROPID_UseDebugLaunchService = -8520,
        VSAPROPID_GuidDefaultDebugEngine = -8521,
        VSAPROPID_CmdLineOptStrFirst = -8522,
        VSAPROPID_CmdLineOptStrLast = -8523,
        VSAPROPID_IsRegisteredAsRuntimeJITDebugger = -8524,
        VSAPROPID_PersistProjExplorerState = -8525,
        VSAPROPID_PredefinedAliasesID = -8526,
        VSAPROPID_DisableDynamicHelp = -8527,
        VSAPROPID_UsesMRUCommandsOnFileMenu = -8528,
        VSAPROPID_AllowsDroppedFilesOnMainWindow = -8529,
        VSAPROPID_DisableAnswerWizardControl = -8530,
        VSAPROPID_DisableAnsiCodePageCheck = -8531,
        VSAPROPID_DisableInstructionUnitStepping = -8532,
        VSAPROPID_UseVisualStudioDialogShortcuts = -8533,
        VSAPROPID_SKUEdition = -8534,
        VSAPROPID_Logo = -8535,
        VSAPROPID_DDEApplication = -8536,
        VSAPROPID_DDETopic = -8537,
        VSAPROPID_VSIPLicenseRequired = -8538,
        VSAPROPID_DropFilesOnMainWindowHandler = -8539,
        VSAPROPID_SubSKUEdition = -8546,
        VSAPROPID_FIRST = -8546
    }

    internal enum __VSASubSKUEdition6
    {
        VSASubSKUEdition_Blend = 65536
    }
}
