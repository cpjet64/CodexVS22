global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;

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
        public static CodexOptions OptionsInstance { get; private set; }
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            this.RegisterToolWindows();

            try
            {
                OptionsInstance = (CodexOptions)GetDialogPage(typeof(CodexOptions));
            }
            catch
            {
                OptionsInstance = new CodexOptions();
            }

            await InitializeEnvironmentAsync(cancellationToken);

            if (OptionsInstance != null && OptionsInstance.OpenOnStartup)
            {
                try
                {
                    await MyToolWindow.ShowAsync();
                }
                catch
                {
                    // ignore failures opening the tool window on startup
                }
            }
        }

        private async Task InitializeEnvironmentAsync(CancellationToken cancellationToken)
        {
            try
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var solutionContext = KnownUIContexts.SolutionExistsAndFullyLoadedContext;
                var folderContext = MyToolWindowControl.TryGetFolderOpenUIContext();

                var tasks = new List<Task>
                {
                    MyToolWindowControl.WaitForUiContextAsync(solutionContext, cancellationToken)
                };

                if (folderContext != null)
                    tasks.Add(MyToolWindowControl.WaitForUiContextAsync(folderContext, cancellationToken));

                var grace = Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                await Task.WhenAny(Task.WhenAll(tasks), grace);

                var snapshot = await MyToolWindowControl.CaptureEnvironmentSnapshotAsync(cancellationToken);
                MyToolWindowControl.SignalEnvironmentReady(snapshot);
            }
            catch
            {
                MyToolWindowControl.SignalEnvironmentReady(MyToolWindowControl.EnvironmentSnapshot.Empty);
            }
        }
    }
}
