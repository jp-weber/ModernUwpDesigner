using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ModernUwpDesigner.Common
{
    internal static class UIHelpers
    {
        private static bool _visualStylesEnsured;

        public static unsafe ExtendedMessageBoxResult ShowMessageBox(IServiceProvider serviceProvider,
                                                 string message,
                                                 string title,
                                                 OLEMSGICON icon,
                                                 OLEMSGBUTTON msgButton = OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                                 OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST)
        {
            var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
            bool hasGui = shell?.GetProperty((int)__VSSPROPID.VSSPROPID_Zombie, out object isZombie) is 0 && isZombie is false;

            if (hasGui)
            {
                return (ExtendedMessageBoxResult)VsShellUtilities.ShowMessageBox(serviceProvider, message, title, icon, msgButton, defaultButton);
            }
            else
            {
                EnsureVisualStyles();

                TaskDialogConfiguration config = new()
                {
                    Size = TaskDialogConfiguration.StructSize,
                    ParentHandle = 0,
                    Instance = 0,
                    Flags = TaskDialogFlags.UseCommandLinks | TaskDialogFlags.AllowCancellation | TaskDialogFlags.SizeToContent,
                    CommonButtons = TaskDialogCommonButtons.Close,
                    WindowTitle = "Modern UWP Designer",
                    MainInstruction = title,
                    Content = message,
                    MainIcon = icon switch
                    {
                        OLEMSGICON.OLEMSGICON_CRITICAL => new(TaskDialogIcons.Error),
                        OLEMSGICON.OLEMSGICON_WARNING => new(TaskDialogIcons.Warning),
                        _ => new(TaskDialogIcons.Information)
                    }
                };

                int count = msgButton switch
                {
                    OLEMSGBUTTON.OLEMSGBUTTON_OK => 1,
                    OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL => 2,
                    OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE => 3,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL => 3,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESALLNOCANCEL => 4,
                    OLEMSGBUTTON.OLEMSGBUTTON_YESNO => 2,
                    OLEMSGBUTTON.OLEMSGBUTTON_RETRYCANCEL => 2,
                    _ => 1
                };

                var buttons = stackalloc TaskDialogButton[count];

                char* buttonBuffer = null;
                switch (msgButton)
                {
                    case OLEMSGBUTTON.OLEMSGBUTTON_OK:
                        {
                            ReadOnlySpan<char> ok = "Okay\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal(ok.Length * sizeof(char));
                            ok.CopyTo(new(buttonBuffer, ok.Length));

                            buttons[0] = new(TaskDialogButtonIds.Ok, buttonBuffer);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL:
                        {
                            ReadOnlySpan<char> ok = "Okay\0";
                            ReadOnlySpan<char> cancel = "Cancel\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal((ok.Length + cancel.Length) * sizeof(char));
                            ok.CopyTo(new(buttonBuffer, ok.Length));
                            cancel.CopyTo(new(buttonBuffer + ok.Length, cancel.Length));

                            buttons[0] = new(TaskDialogButtonIds.Ok, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.Cancel, buttonBuffer + ok.Length);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE:
                        {
                            ReadOnlySpan<char> abort = "Abort\0";
                            ReadOnlySpan<char> retry = "Retry\0";
                            ReadOnlySpan<char> ignore = "Ignore\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal((abort.Length + retry.Length + ignore.Length) * sizeof(char));
                            abort.CopyTo(new(buttonBuffer, abort.Length));
                            retry.CopyTo(new(buttonBuffer + abort.Length, retry.Length));
                            ignore.CopyTo(new(buttonBuffer + abort.Length + retry.Length, ignore.Length));

                            buttons[0] = new(TaskDialogButtonIds.Abort, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.Retry, buttonBuffer + abort.Length);
                            buttons[2] = new(TaskDialogButtonIds.Ignore, buttonBuffer + abort.Length + retry.Length);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL:
                        {
                            ReadOnlySpan<char> yes = "Yes\0";
                            ReadOnlySpan<char> no = "No\0";
                            ReadOnlySpan<char> cancel = "Cancel\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal((yes.Length + no.Length + cancel.Length) * sizeof(char));
                            yes.CopyTo(new(buttonBuffer, yes.Length));
                            no.CopyTo(new(buttonBuffer + yes.Length, no.Length));
                            cancel.CopyTo(new(buttonBuffer + yes.Length + no.Length, cancel.Length));

                            buttons[0] = new(TaskDialogButtonIds.Yes, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.No, buttonBuffer + yes.Length);
                            buttons[2] = new(TaskDialogButtonIds.Cancel, buttonBuffer + yes.Length + no.Length);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_YESNO:
                        {
                            ReadOnlySpan<char> yes = "Yes\0";
                            ReadOnlySpan<char> no = "No\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal((yes.Length + no.Length) * sizeof(char));
                            yes.CopyTo(new(buttonBuffer, yes.Length));
                            no.CopyTo(new(buttonBuffer + yes.Length, no.Length));

                            buttons[0] = new(TaskDialogButtonIds.Yes, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.No, buttonBuffer + yes.Length);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_RETRYCANCEL:
                        {
                            ReadOnlySpan<char> retry = "Retry\0";
                            ReadOnlySpan<char> cancel = "Cancel\0";
                            buttonBuffer = (char*)Marshal.AllocHGlobal((retry.Length + cancel.Length) * sizeof(char));
                            retry.CopyTo(new(buttonBuffer, retry.Length));
                            cancel.CopyTo(new(buttonBuffer + retry.Length, cancel.Length));

                            buttons[0] = new(TaskDialogButtonIds.Retry, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.Cancel, buttonBuffer + retry.Length);
                            break;
                        }
                    case OLEMSGBUTTON.OLEMSGBUTTON_YESALLNOCANCEL:
                        {
                            ReadOnlySpan<char> yes = "Yes\0";
                            ReadOnlySpan<char> yesAll = "Yes to All\0";
                            ReadOnlySpan<char> no = "No\0";
                            ReadOnlySpan<char> cancel = "Cancel\0";

                            buttonBuffer = (char*)Marshal.AllocHGlobal((yes.Length + yesAll.Length + no.Length + cancel.Length) * sizeof(char));
                            yes.CopyTo(new(buttonBuffer, yes.Length));
                            yesAll.CopyTo(new(buttonBuffer + yes.Length, yesAll.Length));
                            no.CopyTo(new(buttonBuffer + yes.Length + yesAll.Length, no.Length));
                            cancel.CopyTo(new(buttonBuffer + yes.Length + yesAll.Length + no.Length, cancel.Length));

                            buttons[0] = new(TaskDialogButtonIds.Yes, buttonBuffer);
                            buttons[1] = new(TaskDialogButtonIds.Continue, buttonBuffer + yes.Length);
                            buttons[2] = new(TaskDialogButtonIds.No, buttonBuffer + yes.Length + yesAll.Length);
                            buttons[3] = new(TaskDialogButtonIds.Cancel, buttonBuffer + yes.Length + yesAll.Length + no.Length);
                            break;
                        }
                }

                config.Buttons = buttons;
                config.ButtonCount = (uint)count;
                config.DefaultButtonId = count > (int)defaultButton ? buttons[(int)defaultButton].Id : buttons[0].Id;

                try
                {
                    Marshal.ThrowExceptionForHR(NativeMethods.TaskDialogIndirect(ref config, out int btn, out _, out _));

                    if (buttonBuffer is not null)
                    {
                        Marshal.FreeHGlobal((nint)buttonBuffer);
                    }

                    return (TaskDialogButtonIds)btn switch
                    {
                        TaskDialogButtonIds.Close => ExtendedMessageBoxResult.None,
                        TaskDialogButtonIds.Help => ExtendedMessageBoxResult.None,
                        TaskDialogButtonIds.TryAgain => ExtendedMessageBoxResult.None,
                        TaskDialogButtonIds.Continue => ExtendedMessageBoxResult.Yes,
                        _ => (ExtendedMessageBoxResult)btn
                    };
                }
                catch
                {
                    if (buttonBuffer is not null)
                    {
                        Marshal.FreeHGlobal((nint)buttonBuffer);
                    }

                    return (ExtendedMessageBoxResult)MessageBox.Show(message, title,
                        msgButton switch
                        {
                            OLEMSGBUTTON.OLEMSGBUTTON_OK => MessageBoxButtons.OK,
                            OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL => MessageBoxButtons.OKCancel,
                            OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE => MessageBoxButtons.AbortRetryIgnore,
                            OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL => MessageBoxButtons.YesNoCancel,
                            OLEMSGBUTTON.OLEMSGBUTTON_YESALLNOCANCEL => MessageBoxButtons.YesNoCancel,
                            OLEMSGBUTTON.OLEMSGBUTTON_YESNO => MessageBoxButtons.YesNo,
                            OLEMSGBUTTON.OLEMSGBUTTON_RETRYCANCEL => MessageBoxButtons.RetryCancel,
                            _ => MessageBoxButtons.OK
                        }, 
                        icon switch
                        {
                            OLEMSGICON.OLEMSGICON_CRITICAL => MessageBoxIcon.Error,
                            OLEMSGICON.OLEMSGICON_WARNING => MessageBoxIcon.Warning,
                            _ => MessageBoxIcon.Information
                        });
                }
            }
        }

        private static void EnsureVisualStyles()
        {
            if (!_visualStylesEnsured)
            {
                _visualStylesEnsured = true;
                NativeMethods.CreateActivationContext(typeof(Application).Assembly.Location, 101);
            }
        }
    }
}
