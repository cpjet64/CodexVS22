using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodexVs.Commands {
  internal sealed class OpenCodexCommand {
    public const int CommandId = 0x0100;
    public static readonly Guid CommandSet = new Guid("e7bfdbbf-a171-4857-baf7-8ef931e6dccf");
    private readonly AsyncPackage _package;
    private OpenCodexCommand(AsyncPackage package, OleMenuCommandService commandService) {
      _package = package;
      var id = new CommandID(CommandSet, CommandId);
      var cmd = new MenuCommand(async (s, e) => await ExecuteAsync(), id);
      commandService.AddCommand(cmd);
    }
    public static async Task InitializeAsync(AsyncPackage package) {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
      _ = new OpenCodexCommand(package, mcs);
    }
    private async Task ExecuteAsync() {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var window = await _package.ShowToolWindowAsync(typeof(CodexVs.CodexToolWindow),
        0, true, _package.DisposalToken);
      if (window?.Frame is IVsWindowFrame frame)
        Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
    }
  }
}