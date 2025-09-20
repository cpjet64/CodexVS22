global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodexVS22
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideToolWindow(typeof(MyToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(CodexOptions), "Codex", "General", 0, 0, true)]
    [Guid(PackageGuids.CodexVS22String)]
    public sealed class CodexVS22Package : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            this.RegisterToolWindows();

            // Optionally open tool window on startup if configured
            try
            {
                var options = (CodexOptions)GetDialogPage(typeof(CodexOptions));
                if (options.OpenOnStartup)
                {
                    await MyToolWindow.ShowAsync();
                }
            }
            catch { /* ignore option load errors */ }
        }
    }
}
