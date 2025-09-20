using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace CodexVs {
  [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
  [InstalledProductRegistration("Codex for Visual Studio (Unofficial)",
    "OpenAI Codex integration", "0.1.0")]
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [ProvideToolWindow(typeof(CodexToolWindow))]
  [Guid("9d29a75f-8049-4c96-beb9-fdc8cbb4c2fc")]
  public sealed class CodexPackage : AsyncPackage {
    protected override async Task InitializeAsync(
      CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
      await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
      await Commands.OpenCodexCommand.InitializeAsync(this);
      await Commands.AddToChatCommand.InitializeAsync(this);
    }
  }
}