using System;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using ModernUwpDesigner.Common;
using Microsoft.VisualStudio.DesignTools.Utility;
using EnvDTE;

namespace ModernUwpDesigner
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class DesignInBlendCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("8ebc6822-9501-41c9-b14b-b00316e47eb6");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly DTE2 currentDTE;

        /// <summary>
        /// Initializes a new instance of the <see cref="DesignInBlendCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DesignInBlendCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            currentDTE = package.GetService<SDTE, DTE2>(false);

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandID);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            var menuItem = (OleMenuCommand)sender;
            menuItem.Visible = false;

            if (BlendShim.IsBlend)
                return;

            if (currentDTE is not null)
            {
                var items = currentDTE.SelectedItems;
                if (items.Count > 0)
                {
                    var item = items.Item(1);
                    if (item.Project is { } project)
                    {
                        var hierarchy = project.GetHierarchy();
                        menuItem.Visible = hierarchy?.IsModernUwpProject() is true;
                    }
                    else if (item.ProjectItem is { } projectItem)
                    {
                        var hierarchy = projectItem.ContainingProject?.GetHierarchy();
                        menuItem.Visible = hierarchy?.IsModernUwpProject() is true &&
                                           projectItem.Name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DesignInBlendCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in TestCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new DesignInBlendCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (currentDTE is not null)
            {
                Project project = null;
                ProjectItem item = null;

                var items = currentDTE.SelectedItems;
                if (items.Count > 0)
                {
                    var selectedItem = items.Item(1);
                    if (selectedItem.Project is { } selectedProject)
                    {
                        project = selectedProject;
                    }
                    else if (selectedItem.ProjectItem is { } selectedProjectItem)
                    {
                        item = selectedProjectItem;
                        project = selectedProjectItem.ContainingProject;
                    }
                }

                if (project is not null)
                {
                    var dte = BlendShim.LaunchInjectedBlend();
                    if (dte is not null)
                    {
                        dte.MainWindow.Activate();
                        dte.Solution.AddFromFile(project.FullName);
                        if (item is not null)
                        {
                            var window = dte.ItemOperations.OpenFile(item.FileNames[1], EnvDTE.Constants.vsViewKindDesigner);
                            window.Visible = true;
                            window.Activate();
                        }
                    }
                }
            }
        }
    }
}
