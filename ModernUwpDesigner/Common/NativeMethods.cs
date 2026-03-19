using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernUwpDesigner
{
    internal static partial class NativeMethods
    {
        [DllImport("Comctl32.dll", SetLastError = true)]
        internal static extern int TaskDialogIndirect([In] ref TaskDialogConfiguration taskConfig, out int button, out int radioButton, [MarshalAs(UnmanagedType.Bool)] out bool verificationFlagChecked);

        [DllImport("kernel32.dll")]
        internal static extern nint CreateActCtx(ref ACTCTX actctx);

        [DllImport("kernel32.dll")]
        internal static extern bool ActivateActCtx(nint hActCtx, out nint lpCookie);

        internal static bool CreateActivationContext(string dllPath, int nativeResourceManifestID)
        {
            ACTCTX ctx = default;
            ctx.cbSize = Marshal.SizeOf<ACTCTX>();
            ctx.lpSource = dllPath;
            ctx.lpResourceName = (nint)nativeResourceManifestID;
            ctx.dwFlags = 8u;
            var hActCtx = CreateActCtx(ref ctx);

            if (hActCtx != -1)
            {
                return ActivateActCtx(hActCtx, out _);
            }

            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal unsafe struct TaskDialogConfiguration
    {
        public static uint StructSize { get; } = (uint)Marshal.SizeOf<TaskDialogConfiguration>();

        internal uint Size;

        internal nint ParentHandle;

        internal nint Instance;

        internal TaskDialogFlags Flags;

        internal TaskDialogCommonButtons CommonButtons;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string WindowTitle;

        internal TaskDialogIcon MainIcon;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string MainInstruction;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string Content;

        internal uint ButtonCount;

        internal TaskDialogButton* Buttons;

        internal int DefaultButtonId;

        internal uint RadioButtonCount;

        internal TaskDialogButton* RadioButtons;

        internal int DefaultRadioButtonId;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string VerificationText;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string ExpandedInformation;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string ExpandedControlText;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string CollapsedControlText;

        internal TaskDialogIcon FooterIcon;

        [MarshalAs(UnmanagedType.LPWStr)]
        internal string FooterText;

        internal TaskDialogCallback Callback;

        internal nint CallbackData;

        internal uint Width;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct TaskDialogIcon
    {
        internal TaskDialogIcon(nint hicon)
        {
            this.IconHandle = hicon;
            this.IconReference = 0;
        }

        internal TaskDialogIcon(TaskDialogIcons icon)
        {
            this.IconHandle = 0;
            this.IconReference = (nint)icon;
        }

        [FieldOffset(0)]
        internal readonly nint IconHandle;

        [FieldOffset(0)]
        private readonly nint IconReference;
    }

    internal enum TaskDialogIcons : ulong
    {
        Warning = unchecked((ushort)-1),
        Error = unchecked((ushort)-2),
        Information = unchecked((ushort)-3),
        Shield = unchecked((ushort)-4),
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly unsafe struct TaskDialogButton
    {
        public TaskDialogButton(int buttonId, char* text)
        {
            this.Id = buttonId;
            this.Text = text;
        }

        public TaskDialogButton(TaskDialogButtonIds buttonId, char* text)
        {
            this.Id = (int)buttonId;
            this.Text = text;
        }

        internal readonly int Id;
        internal readonly char* Text;
    }

    [Flags]
    internal enum TaskDialogCommonButtons
    {
        Ok = 1,
        Yes = 2,
        No = 4,
        Cancel = 8,
        Retry = 16,
        Close = 32
    }

    internal enum TaskDialogButtonIds
    {
        Ok = 1,
        Cancel,
        Abort,
        Retry,
        Ignore,
        Yes,
        No,
        Close,
        Help,
        TryAgain,
        Continue
    }

    internal enum TaskDialogElements
    {
        Content,
        ExpandedInformation,
        Footer,
        MainInstruction
    }

    internal enum TaskDialogIconElement
    {
        Main,
        Footer
    }

    [Flags]
    internal enum TaskDialogFlags
    {
        None = 0,
        EnableHyperlinks = 0x1,
        UseMainIcon = 0x2,
        UseFooterIcon = 0x4,
        AllowCancellation = 0x8,
        UseCommandLinks = 0x10,
        UseNoIconCommandLinks = 0x20,
        ExpandFooterArea = 0x40,
        ExpandedByDefault = 0x80,
        CheckVerificationFlag = 0x100,
        ShowProgressBar = 0x200,
        ShowMarqueeProgressBar = 0x400,
        UseCallbackTimer = 0x800,
        PositionRelativeToWindow = 0x1000,
        RightToLeftLayout = 0x2000,
        NoDefaultRadioButton = 0x4000,
        CanBeMinimized = 0x8000,
        NoSetForeground = 0x10000,
        SizeToContent = 0x1000000
    }

    internal enum TaskDialogMessages
    {
        UserMessage = 1024,
        NavigatePage = 1125,
        ClickButton,
        SetMarqueeProgressBar,
        SetProgressBarState,
        SetProgressBarRange,
        SetProgressBarPosition,
        SetProgressBarMarquee,
        SetElementText,
        ClickRadioButton = 1134,
        EnableButton,
        EnableRadioButton,
        ClickVerification,
        UpdateElementText,
        SetButtonElevationRequiredState,
        UpdateIcon
    }

    internal enum TaskDialogNotifications
    {
        Created,
        Navigated,
        ButtonClicked,
        HyperlinkClicked,
        Timer,
        Destroyed,
        RadioButtonClicked,
        Constructed,
        VerificationClicked,
        Help,
        ExpandButtonClicked
    }

    internal delegate int TaskDialogCallback(nint hwnd, uint message, nint wparam, nint lparam, nint referenceData);

    internal struct ACTCTX
    {
        public int cbSize;

        public uint dwFlags;

        public string lpSource;

        public ushort wProcessorArchitecture;

        public ushort wLangId;

        public string lpAssemblyDirectory;

        public IntPtr lpResourceName;

        public string lpApplicationName;
    }
}
