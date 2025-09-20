using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;

namespace CodexVS22
{
    [Command(PackageIds.DiagnosticsCommand)]
    internal sealed class DiagnosticsCommand : BaseCommand<DiagnosticsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var pane = await VS.Windows.CreateOutputWindowPaneAsync("Codex Diagnostics", false);
            await pane.ActivateAsync();
            await pane.WriteLineAsync("Diagnostics opened. Logs will appear here.");
        }
    }
}

