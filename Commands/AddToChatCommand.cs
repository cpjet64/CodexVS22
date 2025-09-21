using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace CodexVS22
{
  [Command(PackageGuids.CodexVS22String, PackageIds.AddToChatCommand)]
  internal sealed class AddToChatCommand : BaseCommand<AddToChatCommand>
  {
    protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
    {
      await MyToolWindow.ShowAsync();
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      var dte = await VS.GetServiceAsync<DTE, DTE2>();
      var sel = dte?.ActiveDocument?.Selection as TextSelection;
      var text = sel?.Text;
      if (string.IsNullOrWhiteSpace(text))
      {
        // Inform the user instead of silently disabling the command
        var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
        await pane.WriteLineAsync("[info] No selection detected. Select text then run Add to Codex chat.");
        return;
      }
      // Forward selection into the tool window input box
      var ctrl = MyToolWindowControl.Current;
      if (ctrl != null)
        ctrl.AppendSelectionToInput(text);
    }

    protected override void BeforeQueryStatus(EventArgs e)
    {
      if (Command == null) return;
      if (ThreadHelper.CheckAccess())
      {
        Command.Visible = true;
        Command.Enabled = true;
        Command.Supported = true;
      }
      else
      {
        ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
          await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
          try
          {
            Command.Visible = true;
            Command.Enabled = true;
            Command.Supported = true;
          }
          catch
          {
            // Command may be disposed during package init; ignore.
          }
        });
      }
    }
  }
}
