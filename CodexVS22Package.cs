global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using CodexVS22.Core.Cli;
using CodexVS22.Core.State;
using CodexVS22.Shared.Cli;
using CodexVS22.Shared.Options;
using CodexVS22.Shared.Utilities;

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
            ConfigureServices();

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

        private static void ConfigureServices()
        {
            ServiceLocator.RegisterSingleton<ICliDiagnosticsSink>(() => new DiagnosticsPaneCliDiagnosticsSink());
            ServiceLocator.RegisterSingleton<ICliMessageSerializer>(() => new CliSubmissionFactory());
            ServiceLocator.RegisterSingleton<ICliMessageRouter>(() => new DefaultCliMessageRouter());
            ServiceLocator.RegisterSingleton<ICodexOptionsProvider>(() => new DefaultCodexOptionsProvider());
            ServiceLocator.RegisterSingleton<ICodexCliHost>(() => new ProcessCodexCliHost(ServiceLocator.GetRequiredService<ICliDiagnosticsSink>()));
            ServiceLocator.RegisterSingleton(() => new CliSessionService(
                ServiceLocator.GetRequiredService<ICodexCliHost>(),
                ServiceLocator.GetRequiredService<ICliMessageRouter>(),
                ServiceLocator.GetRequiredService<ICliMessageSerializer>(),
                ServiceLocator.GetRequiredService<ICodexOptionsProvider>()));
            ServiceLocator.RegisterSingleton<ICodexSessionStore>(() => new CodexSessionStore());
            ServiceLocator.RegisterSingleton<IWorkspaceContextStore>(() => new WorkspaceContextStore());
            ServiceLocator.RegisterSingleton<IOptionsCache>(() => new OptionsCache());
            ServiceLocator.RegisterSingleton<ICodexSessionCoordinator>(() =>
            {
                var coordinator = new CodexSessionCoordinator(
                    ServiceLocator.GetRequiredService<ICodexCliHost>(),
                    ServiceLocator.GetRequiredService<CliSessionService>(),
                    ServiceLocator.GetRequiredService<ICliMessageRouter>(),
                    ServiceLocator.GetRequiredService<ICodexOptionsProvider>(),
                    ServiceLocator.GetRequiredService<ICodexSessionStore>(),
                    ServiceLocator.GetRequiredService<IWorkspaceContextStore>(),
                    ServiceLocator.GetRequiredService<IOptionsCache>());
                coordinator.Initialize();
                return coordinator;
            });
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
