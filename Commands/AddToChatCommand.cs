using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace CodexVs.Commands {
  internal sealed class AddToChatCommand {
    public const int CommandId = 0x0101;
    public static readonly Guid CommandSet = new Guid("e7bfdbbf-a171-4857-baf7-8ef931e6dccf");
    private readonly AsyncPackage _package;
    private AddToChatCommand(AsyncPackage package, OleMenuCommandService commandService) {
      _package = package;
      var id = new CommandID(CommandSet, CommandId);
      var cmd = new OleMenuCommand(async (s,e) => await ExecuteAsync(), id);
      cmd.BeforeQueryStatus += OnBeforeQueryStatus;
      commandService.AddCommand(cmd);
    }
    public static async Task InitializeAsync(AsyncPackage package) {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
      _ = new AddToChatCommand(package, mcs);
    }
    private void OnBeforeQueryStatus(object sender, EventArgs e) {
      ThreadHelper.ThrowIfNotOnUIThread();
      var dte = (DTE2)Package.GetGlobalService(typeof(DTE));
      var cmd = (OleMenuCommand)sender;
      try {
        var sel = dte.ActiveDocument?.Selection as TextSelection;
        cmd.Visible = cmd.Enabled = sel != null && !string.IsNullOrEmpty(sel.Text);
      } catch { cmd.Visible = cmd.Enabled = false; }
    }
    private async Task ExecuteAsync() {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var dte = (DTE2)Package.GetGlobalService(typeof(DTE));
      var sel = dte.ActiveDocument?.Selection as TextSelection;
      var text = sel?.Text;
      if (string.IsNullOrWhiteSpace(text)) return;
      var req = System.Text.Json.JsonSerializer.Serialize(new {
        jsonrpc = "2.0", id = Guid.NewGuid().ToString(), method = "UserMessage",
        @params = new { text = text }
      });
      await Core.CodexProcessService.Instance.SendAsync(req);
    }
  }
}