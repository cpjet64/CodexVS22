using System.Threading.Tasks;
using Community.VisualStudio.Toolkit;
using CodexVS22.Core;

namespace CodexVS22
{
    [Command(PackageIds.DiagnosticsCommand)]
    internal sealed class DiagnosticsCommand : BaseCommand<DiagnosticsCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var pane = await DiagnosticsPane.GetAsync();
            await pane.ActivateAsync();
            await pane.WriteLineAsync("Diagnostics opened. Logs will appear here.");
        }
    }
}
